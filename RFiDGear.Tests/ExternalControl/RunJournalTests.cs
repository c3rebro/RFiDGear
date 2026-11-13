#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using RFiDGear.Bridge;
using RFiDGear.Services.ExternalControl;

using Xunit;

namespace RFiDGear.Tests.ExternalControl
{
    /// <summary>The journal of the runs: what is written, what survives a restart, what is not trusted, and how it is kept small.</summary>
    public sealed class RunJournalTests : IDisposable
    {
        private readonly List<string> _folders = new List<string>();
        private readonly List<IDisposable> _journals = new List<IDisposable>();

        public void Dispose()
        {
            foreach (var journal in _journals)
            {
                journal.Dispose();
            }

            foreach (var folder in _folders)
            {
                try
                {
                    Directory.Delete(folder, recursive: true);
                }
                catch (IOException)
                {
                    // A folder in the temporary directory that stays behind is not a failure of the test.
                }
            }
        }

        private static string NewRunId() => Guid.NewGuid().ToString("N");

        private string NewFolder()
        {
            var folder = Path.Combine(Path.GetTempPath(), "RFiDGear.Tests", Guid.NewGuid().ToString("N"));
            _folders.Add(folder);

            return folder;
        }

        private RunJournal Open(string folder, out IReadOnlyList<JournalEntry> recovered, int maxEntries = BridgeProtocol.RequestIdHistory, int maxLines = 4096)
        {
            var journal = RunJournal.TryOpen(folder, "instance-" + Guid.NewGuid().ToString("N"), out recovered, maxEntries, maxLines);
            Assert.NotNull(journal);
            _journals.Add(journal!);

            return journal!;
        }

        // The writer keeps the file open for appending; a reader has to allow that.
        private static string[] LinesOf(RunJournal journal)
        {
            using var stream = new FileStream(journal.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream, Encoding.UTF8);

            return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        }

        [Fact]
        public void AFreshJournal_HasNothingToRecover_AndTheFileExists()
        {
            var journal = Open(NewFolder(), out var recovered);

            Assert.Empty(recovered);
            Assert.True(File.Exists(journal.FilePath));
        }

        [Fact]
        public void ARunThatStartedAndEnded_AndOneThatOnlyStarted_AreRememberedAsWhatTheyWere()
        {
            var folder = NewFolder();
            var (ended, interrupted) = (NewRunId(), NewRunId());
            var first = Open(folder, out _);

            Assert.True(first.TryRecordStarted("r-1", ended));
            first.RecordEnded("r-1", ended);
            Assert.True(first.TryRecordStarted("r-2", interrupted));
            first.Dispose();

            Open(folder, out var recovered);

            Assert.Equal(
                new[] { new JournalEntry("r-1", ended, JournalState.Ended), new JournalEntry("r-2", interrupted, JournalState.Started) },
                recovered);
        }

        [Fact]
        public void ARunThatTheHostRefused_IsNotRemembered()
        {
            var folder = NewFolder();
            var runId = NewRunId();
            var first = Open(folder, out _);
            Assert.True(first.TryRecordStarted("r-1", runId));
            first.RecordRefused("r-1", runId);
            first.Dispose();

            Open(folder, out var recovered);

            Assert.Empty(recovered);
        }

        [Fact]
        public void ARefusalOfAnotherRunWithTheSameId_DoesNotForgetTheRunThatRan()
        {
            var folder = NewFolder();
            var (ran, other) = (NewRunId(), NewRunId());
            var first = Open(folder, out _);
            Assert.True(first.TryRecordStarted("r-1", ran));

            first.RecordRefused("r-1", other);
            first.Dispose();

            Open(folder, out var recovered);

            Assert.Equal(new[] { new JournalEntry("r-1", ran, JournalState.Started) }, recovered);
        }

        [Fact]
        public void TheLastWordOnARequestId_IsTheOneThatCounts()
        {
            var folder = NewFolder();
            var (old, current) = (NewRunId(), NewRunId());
            var first = Open(folder, out _);
            Assert.True(first.TryRecordStarted("r-1", old));
            first.RecordRefused("r-1", old);
            Assert.True(first.TryRecordStarted("r-1", current));
            first.RecordEnded("r-1", current);
            first.Dispose();

            Open(folder, out var recovered);

            Assert.Equal(new[] { new JournalEntry("r-1", current, JournalState.Ended) }, recovered);
        }

        [Fact]
        public void ARunThatIsWrittenAndThenRefused_DoesNotPushAnOlderIdOutForGood()
        {
            var folder = NewFolder();
            var first = Open(folder, out _, maxEntries: 3);
            var runs = Enumerable.Range(1, 3).Select(_ => NewRunId()).ToList();

            for (var i = 0; i < 3; i++)
            {
                Assert.True(first.TryRecordStarted("r-" + (i + 1), runs[i]));
                first.RecordEnded("r-" + (i + 1), runs[i]);
            }

            // The history is full. A fourth request is written down before the host is asked, and the host refuses it.
            var provisional = NewRunId();
            Assert.True(first.TryRecordStarted("r-4", provisional));
            first.RecordRefused("r-4", provisional);
            first.Dispose();

            Open(folder, out var recovered, maxEntries: 3);

            // Nothing that the registry still remembers is lost to a request that never ran.
            Assert.Equal(new[] { "r-1", "r-2", "r-3" }, recovered.Select(e => e.RequestId));
        }

        // A file that fails when written to: the first write that is armed leaves half a line and throws, as a full disk would.
        private sealed class FailingFile : FileStream
        {
            public FailingFile(string path)
                : base(path, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough)
            {
            }

            public bool FailTheNextWrite { get; set; }

            public override void Write(byte[] buffer, int offset, int count)
            {
                if (FailTheNextWrite)
                {
                    FailTheNextWrite = false;
                    base.Write(buffer, offset, count / 2);
                    Flush(true);

                    throw new IOException("There is not enough space on the disk.");
                }

                base.Write(buffer, offset, count);
            }
        }

        [Fact]
        public void AWriteThatFailedHalfWay_DoesNotSwallowTheLinesThatFollow()
        {
            var folder = NewFolder();
            var files = new List<FailingFile>();
            var journal = RunJournal.TryOpen(folder, "instance", out _, openForAppend: path =>
            {
                var file = new FailingFile(path);
                files.Add(file);

                return file;
            });
            _journals.Add(journal!);
            var (lost, kept) = (NewRunId(), NewRunId());
            files[0].FailTheNextWrite = true;

            var first = journal!.TryRecordStarted("r-1", lost);
            var second = journal.TryRecordStarted("r-2", kept);
            journal.Dispose();
            RunJournal.TryOpen(folder, "instance", out var recovered)!.Dispose();

            // The run that could not be written down is refused, and the one after it is not joined to what the failure left.
            Assert.False(first);
            Assert.True(second);
            Assert.Equal(new[] { new JournalEntry("r-2", kept, JournalState.Started) }, recovered);
            Assert.Equal(2, files.Count);
        }

        [Fact]
        public void AJournalThatCannotBeRepairedAfterAFailure_StaysClosed()
        {
            var folder = NewFolder();
            var opened = 0;
            FailingFile? first = null;
            var journal = RunJournal.TryOpen(folder, "instance", out _, openForAppend: path =>
            {
                if (++opened > 1)
                {
                    throw new IOException("The file cannot be opened.");
                }

                first = new FailingFile(path);

                return first;
            });
            _journals.Add(journal!);
            first!.FailTheNextWrite = true;

            Assert.False(journal!.TryRecordStarted("r-1", NewRunId()));

            // Nobody is told that a run was written down when the journal cannot be trusted any more.
            Assert.False(journal.TryRecordStarted("r-2", NewRunId()));
            Assert.False(journal.TryRecordStarted("r-3", NewRunId()));
        }

        [Fact]
        public void AFileWithDamage_IsReadAsFarAsItIsGood_AndNeverTrustedBeyondThat()
        {
            var folder = NewFolder();
            Directory.CreateDirectory(folder);
            var good = NewRunId();
            var path = Path.Combine(folder, RunJournal.FileName);
            File.WriteAllLines(path, new[]
            {
                "this is not json",
                "{\"v\":1,\"request\":\"r-1\",\"run\":\"" + good + "\",\"state\":\"started\"}",
                "{\"v\":2,\"request\":\"r-2\",\"run\":\"" + NewRunId() + "\",\"state\":\"started\"}",                       // another version
                "{\"v\":1,\"request\":\"has space\",\"run\":\"" + NewRunId() + "\",\"state\":\"started\"}",               // not a request id
                "{\"v\":1,\"request\":\"r-3\",\"run\":\"not-a-run-id\",\"state\":\"started\"}",                            // not a run id
                "{\"v\":1,\"request\":\"r-4\",\"run\":\"" + NewRunId() + "\",\"state\":\"finished\"}",                     // not a state
                "{\"v\":1,\"request\":\"r-5\",\"state\":\"started\"}",                                                     // no run
                "[1,2,3]",
                "{\"v\":1,\"request\":\"r-6\",\"run\":\"" + NewRunId() + "\",\"state\":\"started\"",                       // cut short by a crash
            });

            Open(folder, out var recovered);

            Assert.Equal(new[] { new JournalEntry("r-1", good, JournalState.Started) }, recovered);
        }

        [Fact]
        public void ALineThatHasMoreThanWeWrote_IsStillOurs_ButIsRewrittenAsWeWriteIt()
        {
            var folder = NewFolder();
            Directory.CreateDirectory(folder);
            var run = NewRunId();
            File.WriteAllText(Path.Combine(folder, RunJournal.FileName), "{\"v\":1,\"request\":\"r-1\",\"run\":\"" + run + "\",\"state\":\"ended\",\"result\":\"SECRET\",\"extra\":1}\n");

            var journal = Open(folder, out var recovered);

            Assert.Equal(new[] { new JournalEntry("r-1", run, JournalState.Ended) }, recovered);
            Assert.DoesNotContain("SECRET", string.Join("\n", LinesOf(journal)), StringComparison.Ordinal);
        }

        [Fact]
        public void OnlyTheLastRequestIdsAreKept()
        {
            var folder = NewFolder();
            var first = Open(folder, out _, maxEntries: 3);
            var runs = Enumerable.Range(1, 5).Select(_ => NewRunId()).ToList();

            for (var i = 0; i < runs.Count; i++)
            {
                Assert.True(first.TryRecordStarted("r-" + (i + 1), runs[i]));
            }

            first.Dispose();
            Open(folder, out var recovered, maxEntries: 3);

            Assert.Equal(new[] { "r-3", "r-4", "r-5" }, recovered.Select(e => e.RequestId));
        }

        [Fact]
        public void TheFileIsRewrittenWhenItHasGrown_AndNothingIsLost()
        {
            var folder = NewFolder();
            var journal = Open(folder, out _, maxLines: 10);
            var runs = new List<string>();

            // 12 runs of 2 lines: the file must have been rewritten along the way, and still know every run that is kept.
            for (var i = 0; i < 12; i++)
            {
                var run = NewRunId();
                runs.Add(run);
                Assert.True(journal.TryRecordStarted("r-" + i, run));
                journal.RecordEnded("r-" + i, run);
            }

            var lines = LinesOf(journal);
            journal.Dispose();
            Open(folder, out var recovered, maxLines: 10);

            Assert.True(lines.Length < 24, "The file has not been kept small.");
            Assert.Equal(12, recovered.Count);
            Assert.All(recovered, entry => Assert.Equal(JournalState.Ended, entry.State));
            Assert.Equal(runs, recovered.Select(e => e.RunId));
        }

        [Fact]
        public void AWrittenLine_HoldsOnlyTheIdsAState_AndATime()
        {
            var journal = Open(NewFolder(), out _);
            var run = NewRunId();

            Assert.True(journal.TryRecordStarted("r-1", run));
            journal.RecordEnded("r-1", run);

            foreach (var text in LinesOf(journal))
            {
                using var document = JsonDocument.Parse(text);
                var names = document.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal);

                // The id of the process, the ids of the request and the run, the state and the time: nothing of what was asked.
                Assert.Equal(new[] { "at", "instance", "request", "run", "state", "v" }, names);
                Assert.True(DateTimeOffset.TryParse(document.RootElement.GetProperty("at").GetString(), out var at));
                Assert.True(DateTimeOffset.UtcNow - at < TimeSpan.FromMinutes(1));
            }
        }

        [Fact]
        public void ALineIsOnTheDiskWhenTheCallReturns()
        {
            var journal = Open(NewFolder(), out _);
            var run = NewRunId();

            Assert.True(journal.TryRecordStarted("r-1", run));

            // Read through another handle at once, without closing the journal: that is what a crash leaves.
            Assert.Contains(run, string.Join("\n", LinesOf(journal)), StringComparison.Ordinal);
        }

        [Fact]
        public async Task ManyWritersAtOnce_LeaveEveryLineWhole()
        {
            var journal = Open(NewFolder(), out _);

            await Task.WhenAll(Enumerable.Range(0, 8).Select(t => Task.Run(() =>
            {
                for (var i = 0; i < 40; i++)
                {
                    Assert.True(journal.TryRecordStarted($"t{t}-{i}", NewRunId()));
                }
            })));

            var lines = LinesOf(journal);
            Assert.Equal(320, lines.Length);
            Assert.All(lines, text => JsonDocument.Parse(text).Dispose());
            journal.Dispose();
            Open(Path.GetDirectoryName(journal.FilePath)!, out var recovered);
            Assert.Equal(320, recovered.Count);
        }

        [Fact]
        public void AFolderThatCannotBeUsed_MeansNoJournal_NotAJournalThatPretends()
        {
            var folder = NewFolder();
            Directory.CreateDirectory(folder);
            var blocker = Path.Combine(folder, "a-file");
            File.WriteAllText(blocker, "x");

            var journal = RunJournal.TryOpen(Path.Combine(blocker, "below-a-file"), "instance", out var recovered);

            Assert.Null(journal);
            Assert.Empty(recovered);
        }

        [Fact]
        public void AClosedJournal_RefusesToWrite_SoThatARunIsNotStartedWithoutARecord()
        {
            var journal = Open(NewFolder(), out _);
            journal.Dispose();

            Assert.False(journal.TryRecordStarted("r-1", NewRunId()));
        }
    }
}
