#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using RFiDGear.Bridge;
using RFiDGear.Contracts;
using RFiDGear.Services.ExternalControl;
using RFiDGear.Tests.CardCheckAssistant;

using StreamJsonRpc;

using Xunit;

namespace RFiDGear.Tests.ExternalControl
{
    /// <summary>
    /// What RFiDGear knows about the runs of a process that is gone. A "process" here is a host, a registry, a supervisor and a
    /// journal over a folder; a crash is dropping one without letting its runs end, a restart is making the next over the same
    /// folder.
    /// </summary>
    public sealed class BridgeRunRecoveryTests : IDisposable
    {
        private readonly List<string> _folders = new List<string>();
        private readonly List<IDisposable> _disposables = new List<IDisposable>();

        public void Dispose()
        {
            foreach (var disposable in Enumerable.Reverse(_disposables))
            {
                disposable.Dispose();
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

        private string NewFolder()
        {
            var folder = Path.Combine(Path.GetTempPath(), "RFiDGear.Tests", Guid.NewGuid().ToString("N"));
            _folders.Add(folder);

            return folder;
        }

        private sealed class Process
        {
            public Process(string folder, IRunJournal? journalOverride = null, TimeSpan? admissionTimeout = null)
            {
                Host.ProjectHost.CurrentPath = @"C:\projects\one.rfPrj";
                Host.ProjectHost.TaskCount = 2;
                Journal = RunJournal.TryOpen(folder, "instance-" + Guid.NewGuid().ToString("N"), out var recovered);
                Registry.Recover(recovered);
                Supervisor = new RunSupervisor(
                    Host,
                    Host.ExecutionHost,
                    Registry,
                    action => Dispatch(action),
                    (_, _, _) => { },
                    () => false,
                    () => null,
                    admissionTimeout,
                    journalOverride ?? Journal);
            }

            public FakeExtensionHost Host { get; } = new FakeExtensionHost();

            public RunRegistry Registry { get; } = new RunRegistry();

            public RunJournal? Journal { get; }

            public RunSupervisor Supervisor { get; }

            public Func<Action, Task> Dispatch { get; set; } = action =>
            {
                action();

                return Task.CompletedTask;
            };

            /// <summary>The process dies: its journal is closed without a word, and nothing it runs ends.</summary>
            public void Crash() => Journal?.Dispose();
        }

        private static StartRunRequest Request(string id, params (string Name, string Value)[] variables) => new StartRunRequest
        {
            RequestId = id,
            Variables = variables.Select(v => new VariablePair { Name = v.Name, Value = v.Value }).ToList(),
        };

        private static Task<GetRunResultResponse> ResultOf(Process p, string requestId, int waitMs = 5000) =>
            p.Supervisor.GetResultAsync(new GetRunResultRequest { RequestId = requestId, WaitMs = waitMs });

        private static async Task<BridgeFault> RefusalOf(Func<Task> call)
        {
            try
            {
                await call();
            }
            catch (BridgeFault fault)
            {
                return fault;
            }

            throw new Xunit.Sdk.XunitException("The call was not refused.");
        }

        private static string[] LinesOf(RunJournal journal)
        {
            using var stream = new FileStream(journal.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);

            return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        }

        // ── A run that the restart interrupted ──

        [Fact]
        public async Task ARunThatWasInFlightWhenTheProcessDied_IsKnownAsInterrupted_AndNeverStartedAgain()
        {
            var folder = NewFolder();
            var before = new Process(folder);
            before.Host.ExecutionHost.RunGate = new TaskCompletionSource<bool>();
            var started = await before.Supervisor.StartAsync(Request("r-1", ("$JOBNUMBER", "J-42")));
            before.Crash();

            var after = new Process(folder);
            var again = await after.Supervisor.StartAsync(Request("r-1", ("$JOBNUMBER", "J-42")));
            var otherContent = await after.Supervisor.StartAsync(Request("r-1", ("$JOBNUMBER", "J-99")));

            // The same id is the same request after a restart, whatever it says now: the journal holds nothing to compare it with.
            Assert.Equal(new[] { true, true }, new[] { again.Duplicate, otherContent.Duplicate });
            Assert.Equal(new[] { started.RunId, started.RunId }, new[] { again.RunId, otherContent.RunId });
            Assert.Equal(new[] { RunStates.InterruptedByRestart, RunStates.InterruptedByRestart }, new[] { again.State, otherContent.State });
            Assert.Empty(after.Host.ExecutionHost.Requests);
            Assert.False(after.Supervisor.HasActiveRun);
        }

        [Fact]
        public async Task AnInterruptedRun_IsAskedForByItsRequestIdOrItsRunId_AndCannotBeCancelled()
        {
            var folder = NewFolder();
            var before = new Process(folder);
            before.Host.ExecutionHost.RunGate = new TaskCompletionSource<bool>();
            var started = await before.Supervisor.StartAsync(Request("r-1"));
            before.Crash();

            var after = new Process(folder);
            var byRequest = await ResultOf(after, "r-1", 0);
            var byRun = await after.Supervisor.GetResultAsync(new GetRunResultRequest { RunId = started.RunId });
            var cancel = after.Supervisor.Cancel(new CancelRunRequest { RequestId = "r-1" });

            Assert.Equal(RunStates.InterruptedByRestart, byRequest.State);
            Assert.Null(byRequest.Result);
            Assert.Equal(RunStates.InterruptedByRestart, byRun.State);
            Assert.Equal(RunStates.InterruptedByRestart, cancel.State);
            Assert.False(cancel.CancelRequested);
        }

        [Fact]
        public async Task AnIdTheJournalNeverSaw_IsUnknown_AndStartsAsUsual()
        {
            var folder = NewFolder();
            var before = new Process(folder);
            await before.Supervisor.StartAsync(Request("r-1"));
            await ResultOf(before, "r-1");
            before.Crash();

            var after = new Process(folder);
            var unknown = await ResultOf(after, "r-9", 0);
            var started = await after.Supervisor.StartAsync(Request("r-9"));

            Assert.Equal(RunStates.Unknown, unknown.State);
            Assert.False(started.Duplicate);
            Assert.Single(after.Host.ExecutionHost.Requests);
            await ResultOf(after, "r-9");
        }

        // ── A run that ended before the restart ──

        [Fact]
        public async Task ARunThatEndedBeforeTheProcessDied_IsKnownAsCompleted_ItsResultIsGone_AndItIsNotStartedAgain()
        {
            var folder = NewFolder();
            var before = new Process(folder);
            var started = await before.Supervisor.StartAsync(Request("r-1"));
            await ResultOf(before, "r-1");
            before.Crash();

            var after = new Process(folder);
            var again = await after.Supervisor.StartAsync(Request("r-1"));
            var lookup = await ResultOf(after, "r-1", 0);
            var cancel = after.Supervisor.Cancel(new CancelRunRequest { RequestId = "r-1" });

            Assert.True(again.Duplicate);
            Assert.Equal(started.RunId, again.RunId);
            Assert.Equal(RunStates.Completed, again.State);
            Assert.Empty(after.Host.ExecutionHost.Requests);

            // The result was never written down (it holds the outcomes of the tasks): the run ended, and that is all that is known.
            Assert.Equal(RunStates.Completed, lookup.State);
            Assert.Null(lookup.Result);
            Assert.Equal(RunStates.Completed, cancel.State);
        }

        // ── What is not remembered as a run ──

        [Fact]
        public async Task ARunTheHostRefused_IsNotARunAfterTheRestart_AndTheIdIsFree()
        {
            var folder = NewFolder();
            var before = new Process(folder);
            before.Host.ExecutionHost.ThrowOnExecute = new InvalidOperationException("An execution is already in progress.");
            await RefusalOf(() => before.Supervisor.StartAsync(Request("r-1")));
            before.Crash();

            var after = new Process(folder);
            var started = await after.Supervisor.StartAsync(Request("r-1"));

            Assert.False(started.Duplicate);
            Assert.Single(after.Host.ExecutionHost.Requests);
            await ResultOf(after, "r-1");
        }

        [Fact]
        public async Task ARequestThatNeverReachedTheHost_LeavesNoTrace()
        {
            var folder = NewFolder();
            var before = new Process(folder, admissionTimeout: TimeSpan.FromMilliseconds(100));
            before.Dispatch = _ => new TaskCompletionSource<bool>().Task;
            await RefusalOf(() => before.Supervisor.StartAsync(Request("r-1")));
            before.Host.ProjectHost.TaskCount = 0;
            before.Dispatch = action =>
            {
                action();

                return Task.CompletedTask;
            };
            await RefusalOf(() => before.Supervisor.StartAsync(Request("r-2")));
            before.Crash();

            var after = new Process(folder);

            Assert.Equal(RunStates.Unknown, (await ResultOf(after, "r-1", 0)).State);
            Assert.Equal(RunStates.Unknown, (await ResultOf(after, "r-2", 0)).State);
            Assert.Empty(LinesOf(after.Journal!));
        }

        // ── The order of things ──

        [Fact]
        public async Task TheRunIsOnTheDisk_BeforeTheHostIsAskedToRunIt()
        {
            var folder = NewFolder();
            var process = new Process(folder);
            string[] seenByTheHost = Array.Empty<string>();
            process.Host.ExecutionHost.OnExecute = _ => seenByTheHost = LinesOf(process.Journal!);

            var started = await process.Supervisor.StartAsync(Request("r-1"));
            await ResultOf(process, "r-1");

            // At the moment the host was asked, the journal already said "started" for this run, and not yet "ended".
            var line = Assert.Single(seenByTheHost);
            Assert.Contains(started.RunId, line, StringComparison.Ordinal);
            Assert.Contains("\"state\":\"started\"", line, StringComparison.Ordinal);
            Assert.Contains("\"state\":\"ended\"", string.Join("\n", LinesOf(process.Journal!)), StringComparison.Ordinal);
        }

        private sealed class OrderSpy : IRunJournal
        {
            public Func<string>? StateAtTheEnd { get; set; }

            public string? StateWhenTheEndWasWritten { get; private set; }

            public bool TryRecordStarted(string requestId, string runId)
            {
                Check(requestId, runId);

                return true;
            }

            public void RecordRefused(string requestId, string runId) => Check(requestId, runId);

            public void RecordEnded(string requestId, string runId)
            {
                Check(requestId, runId);
                StateWhenTheEndWasWritten = StateAtTheEnd?.Invoke();
            }

            private static void Check(string requestId, string runId)
            {
                ArgumentException.ThrowIfNullOrEmpty(requestId);
                ArgumentException.ThrowIfNullOrEmpty(runId);
            }
        }

        [Fact]
        public async Task TheEndOfARunIsWrittenDown_BeforeAnybodyCanReadItsResult()
        {
            var spy = new OrderSpy();
            var process = new Process(NewFolder(), journalOverride: spy);
            spy.StateAtTheEnd = () => process.Supervisor.GetResultAsync(new GetRunResultRequest { RequestId = "r-1" }).GetAwaiter().GetResult().State;

            await process.Supervisor.StartAsync(Request("r-1"));
            await ResultOf(process, "r-1");

            // While the line was written the run was still in flight for everybody who asked: a client that has read the result is
            // never contradicted by a restart that finds the run unfinished.
            Assert.Equal(RunStates.Running, spy.StateWhenTheEndWasWritten);
        }

        private sealed class BrokenJournal : IRunJournal
        {
            public bool TryRecordStarted(string requestId, string runId)
            {
                Check(requestId, runId);

                return false;
            }

            public void RecordRefused(string requestId, string runId) => Check(requestId, runId);

            public void RecordEnded(string requestId, string runId) => Check(requestId, runId);

            private static void Check(string requestId, string runId)
            {
                ArgumentException.ThrowIfNullOrEmpty(requestId);
                ArgumentException.ThrowIfNullOrEmpty(runId);
            }
        }

        [Fact]
        public async Task ARunThatCannotBeWrittenDown_IsNotStarted_AndTheIdIsFree()
        {
            var folder = NewFolder();
            var process = new Process(folder, journalOverride: new BrokenJournal());

            var fault = await RefusalOf(() => process.Supervisor.StartAsync(Request("r-1")));

            Assert.Equal(BridgeErrorCodes.InternalError, fault.Code);
            Assert.True(fault.NeverStarted);
            Assert.Empty(process.Host.ExecutionHost.Requests);
            Assert.False(process.Supervisor.HasActiveRun);
            Assert.Equal(RunStates.Unknown, (await ResultOf(process, "r-1", 0)).State);
        }

        [Fact]
        public async Task NothingOfTheRequestIsWritten_ButTheIds()
        {
            var folder = NewFolder();
            var process = new Process(folder);

            await process.Supervisor.StartAsync(Request("r-1", ("REPORTTARGETPATH", @"C:\SECRET-reports\SECRET-card.pdf"), ("$JOBNUMBER", "SECRET-job")));
            await ResultOf(process, "r-1");

            var text = string.Join("\n", LinesOf(process.Journal!));
            Assert.Contains("r-1", text, StringComparison.Ordinal);
            Assert.DoesNotContain("SECRET", text, StringComparison.Ordinal);
            Assert.DoesNotContain("JOBNUMBER", text, StringComparison.Ordinal);
        }

        // ── Over the pipe, as a client sees a restart ──

        private ExternalControlService StartService(string folder, string pipe, FakeExtensionHost host)
        {
            host.ProjectHost.CurrentPath = @"C:\projects\one.rfPrj";
            host.ProjectHost.TaskCount = 2;
            var service = new ExternalControlService(
                host,
                action =>
                {
                    action();

                    return Task.CompletedTask;
                },
                () => false,
                "2.0.10",
                pipe,
                journalDirectory: folder);
            _disposables.Add(service);
            Assert.True(service.Start());
            service.MarkStartupComplete();

            return service;
        }

        [Fact]
        public async Task AClientAfterARestart_LearnsThatTheRunWasInterrupted_NotThatItWasNeverStarted()
        {
            var folder = NewFolder();
            var firstPipe = "RFiDGear.Bridge.test-" + Guid.NewGuid().ToString("N");
            var firstHost = new FakeExtensionHost();
            firstHost.ExecutionHost.RunGate = new TaskCompletionSource<bool>();
            var first = StartService(folder, firstPipe, firstHost);
            var firstClient = await BridgeTestClient.ConnectAsync(firstPipe, 5000);
            _disposables.Add(firstClient);
            Assert.Contains(BridgeCapabilities.ExecutionJournal, (await firstClient.HelloAsync()).Capabilities);
            var started = await firstClient.Service.StartRun(Request("r-1", ("$JOBNUMBER", "J-42")));

            // The process dies while the run is in flight: the pipe, the registry and the host are gone; the folder is not.
            firstClient.Dispose();
            first.Dispose();

            var secondPipe = "RFiDGear.Bridge.test-" + Guid.NewGuid().ToString("N");
            var secondHost = new FakeExtensionHost();
            var second = StartService(folder, secondPipe, secondHost);
            var client = await BridgeTestClient.ConnectAsync(secondPipe, 5000);
            _disposables.Add(client);
            var hello = await client.HelloAsync();
            var lookup = await client.Service.GetRunResult(new GetRunResultRequest { RequestId = "r-1" });
            var again = await client.Service.StartRun(Request("r-1", ("$JOBNUMBER", "J-42")));
            var unknown = await client.Service.GetRunResult(new GetRunResultRequest { RequestId = "r-2" });

            Assert.NotEqual(first.InstanceId, hello.InstanceId);
            Assert.Contains(BridgeCapabilities.ExecutionJournal, hello.Capabilities);
            Assert.Equal(RunStates.InterruptedByRestart, lookup.State);
            Assert.True(again.Duplicate);
            Assert.Equal(started.RunId, again.RunId);
            Assert.Equal(RunStates.InterruptedByRestart, again.State);
            Assert.Equal(RunStates.Unknown, unknown.State);
            Assert.Empty(secondHost.ExecutionHost.Requests);
            Assert.False(second.RunInFlight());
        }

        [Fact]
        public async Task WithoutAJournalFolder_ThereIsNoGuarantee_AndWithAFolderThatCannotBeUsed_ThereIsNoneEither_ButRunsWork()
        {
            var pipeA = "RFiDGear.Bridge.test-" + Guid.NewGuid().ToString("N");
            var hostA = new FakeExtensionHost();
            hostA.ProjectHost.TaskCount = 2;
            var withoutFolder = new ExternalControlService(hostA, action => { action(); return Task.CompletedTask; }, () => false, "2.0.10", pipeA);
            _disposables.Add(withoutFolder);
            Assert.True(withoutFolder.Start());
            withoutFolder.MarkStartupComplete();

            var folder = NewFolder();
            Directory.CreateDirectory(folder);
            var blocker = Path.Combine(folder, "a-file");
            File.WriteAllText(blocker, "x");
            var pipeB = "RFiDGear.Bridge.test-" + Guid.NewGuid().ToString("N");
            var hostB = new FakeExtensionHost();
            var unusable = StartService(Path.Combine(blocker, "below-a-file"), pipeB, hostB);

            var clientA = await BridgeTestClient.ConnectAsync(pipeA, 5000);
            _disposables.Add(clientA);
            var clientB = await BridgeTestClient.ConnectAsync(pipeB, 5000);
            _disposables.Add(clientB);

            Assert.DoesNotContain(BridgeCapabilities.ExecutionJournal, (await clientA.HelloAsync()).Capabilities);
            Assert.DoesNotContain(BridgeCapabilities.ExecutionJournal, (await clientB.HelloAsync()).Capabilities);
            var response = await clientB.Service.StartRun(Request("r-1"));
            Assert.Equal(RunStates.Completed, (await clientB.Service.GetRunResult(new GetRunResultRequest { RequestId = "r-1", WaitMs = 5000 })).State);
            Assert.Matches("^[0-9a-f]{32}$", response.RunId);
            Assert.True(unusable.IsStartupComplete);
        }
    }
}
