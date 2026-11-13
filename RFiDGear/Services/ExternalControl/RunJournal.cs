#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

using RFiDGear.Bridge;

using Serilog;

namespace RFiDGear.Services.ExternalControl
{
    /// <summary>Where the journal last saw a run.</summary>
    internal enum JournalState
    {
        /// <summary>The run was about to be started; nothing says that it ended.</summary>
        Started,

        /// <summary>The run ended.</summary>
        Ended,
    }

    /// <summary>What the journal remembers of one request: its id, the id of its run, and whether it ended.</summary>
    /// <param name="RequestId">The request id.</param>
    /// <param name="RunId">The run id.</param>
    /// <param name="State">Where the journal last saw the run.</param>
    internal sealed record JournalEntry(string RequestId, string RunId, JournalState State);

    /// <summary>
    /// A record, kept on disk, of the runs that were started over the bridge. A run that is admitted is written down before the
    /// host is asked to run it, and again when it ended, so that after RFiDGear has been restarted the registry can answer for
    /// a request id: "this ran, and the restart interrupted it" or "this ran and ended" instead of "unknown", which would let a
    /// client run the same job on the same card a second time.
    /// </summary>
    internal interface IRunJournal
    {
        /// <summary>Writes down that a run is about to be started, and makes sure it is on disk.</summary>
        /// <param name="requestId">The request id.</param>
        /// <param name="runId">The run id.</param>
        /// <returns><see langword="false"/> when it could not be written; the run must not be started then.</returns>
        bool TryRecordStarted(string requestId, string runId);

        /// <summary>Writes down that the host refused the run after all, so that the id is not remembered as one that ran.</summary>
        /// <param name="requestId">The request id.</param>
        /// <param name="runId">The run id.</param>
        void RecordRefused(string requestId, string runId);

        /// <summary>Writes down that the run ended. If this fails the run is remembered as interrupted, which is the safe way to be wrong.</summary>
        /// <param name="requestId">The request id.</param>
        /// <param name="runId">The run id.</param>
        void RecordEnded(string requestId, string runId);
    }

    /// <summary>
    /// The journal as a file of JSON lines below <c>%LocalAppData%\RFiDGear\bridge</c>. A line holds the id of the process, the
    /// request id, the run id, a state and a time, and nothing else: no variable, no path, no card, no result. A line that is
    /// damaged (a write that a crash cut short, a hand-made edit) is skipped, never trusted. The file is kept to the number of
    /// request ids that the registry remembers, by rewriting it when it has grown, and when it is opened.
    /// </summary>
    internal sealed class RunJournal : IRunJournal, IDisposable
    {
        /// <summary>The name of the file.</summary>
        internal const string FileName = "runs.jsonl";

        private const int Version = 1;
        private static readonly Regex RunIdPattern = new Regex(@"^[0-9a-f]{32}\z", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static ILogger Logger => Log.ForContext<RunJournal>();

        private readonly object _gate = new object();
        private readonly string _path;
        private readonly string _instanceId;
        private readonly int _maxEntries;
        private readonly int _maxLines;
        private readonly Dictionary<string, Entry> _index = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private FileStream? _stream;
        private long _seq;
        private int _lines;

        private readonly Func<string, FileStream> _openForAppend;

        private RunJournal(string path, string instanceId, int maxEntries, int maxLines, Func<string, FileStream>? openForAppend)
        {
            _path = path;
            _instanceId = instanceId;
            _maxEntries = maxEntries;
            _maxLines = maxLines;
            _openForAppend = openForAppend ?? OpenForAppend;
        }

        private static FileStream OpenForAppend(string path) =>
            new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough);

        /// <summary>Gets the folder the journal lives in unless a test gives another.</summary>
        internal static string DefaultDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RFiDGear", "bridge");

        /// <summary>
        /// Opens the journal, reads what an earlier process wrote, and rewrites the file to the entries worth keeping. If the
        /// folder or the file cannot be used the journal is off (and says so in the log): the bridge then does not offer the
        /// guarantee, it does not pretend to have it.
        /// </summary>
        /// <param name="directory">The folder.</param>
        /// <param name="instanceId">The id of this process, written on every line.</param>
        /// <param name="recovered">What the file held: the runs of earlier processes, oldest first. Empty when the journal is off.</param>
        /// <param name="maxEntries">How many request ids are kept.</param>
        /// <param name="maxLines">After how many lines the file is rewritten.</param>
        /// <param name="openForAppend">How the file is opened for appending; a test gives a file that fails.</param>
        /// <returns>The journal, or <see langword="null"/> when it cannot be used.</returns>
        internal static RunJournal? TryOpen(string directory, string instanceId, out IReadOnlyList<JournalEntry> recovered, int maxEntries = BridgeProtocol.RequestIdHistory, int maxLines = 4 * BridgeProtocol.RequestIdHistory, Func<string, FileStream>? openForAppend = null)
        {
            recovered = Array.Empty<JournalEntry>();

            try
            {
                Directory.CreateDirectory(directory);
                var journal = new RunJournal(Path.Combine(directory, FileName), instanceId, maxEntries, maxLines, openForAppend);
                journal.Load();
                journal.Rewrite();
                recovered = journal.Snapshot();

                return journal;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is System.Security.SecurityException || e is NotSupportedException || e is ArgumentException)
            {
                Logger.Warning("External control: the run journal could not be opened and is off ({ExceptionType})", e.GetType().FullName);

                return null;
            }
        }

        /// <inheritdoc />
        public bool TryRecordStarted(string requestId, string runId) => TryAppend("started", requestId, runId);

        /// <inheritdoc />
        public void RecordRefused(string requestId, string runId) => TryAppend("refused", requestId, runId);

        /// <inheritdoc />
        public void RecordEnded(string requestId, string runId) => TryAppend("ended", requestId, runId);

        /// <summary>Gets the full path of the file.</summary>
        internal string FilePath => _path;

        /// <inheritdoc />
        public void Dispose()
        {
            lock (_gate)
            {
                _stream?.Dispose();
                _stream = null;
            }
        }

        private static string? Valid(Line? line)
        {
            if (line == null || line.V != Version || !BridgeProtocol.IsValidRequestId(line.Request) || line.Run == null || !RunIdPattern.IsMatch(line.Run))
            {
                return null;
            }

            return line.State is "started" or "ended" or "refused" ? line.State : null;
        }

        private void Load()
        {
            if (!File.Exists(_path))
            {
                return;
            }

            foreach (var text in File.ReadAllLines(_path, Encoding.UTF8))
            {
                Line? line;

                try
                {
                    line = JsonSerializer.Deserialize<Line>(text);
                }
                catch (JsonException)
                {
                    // A line that a crash cut short, or that is not ours.
                    continue;
                }

                var state = Valid(line);

                if (state != null)
                {
                    Apply(state, line!.Request!, line.Run!);
                }
            }
        }

        // Called under the lock (or while nothing else can reach the journal).
        private void Apply(string state, string requestId, string runId)
        {
            switch (state)
            {
                case "started":
                    _index[requestId] = new Entry(runId, JournalState.Started, ++_seq);
                    break;

                case "ended":
                    _index[requestId] = new Entry(runId, JournalState.Ended, ++_seq);
                    break;

                default:
                    // Refused: the run never ran; the id is free again. Only the run that was refused is forgotten.
                    if (_index.TryGetValue(requestId, out var existing) && string.Equals(existing.RunId, runId, StringComparison.Ordinal))
                    {
                        _index.Remove(requestId);
                    }

                    break;
            }
        }

        // Keeps the most recent ids, the same number the registry remembers. Done when the file is rewritten and not on every line:
        // a run that is written down and then refused must not have pushed an older id out for good.
        private void Trim()
        {
            while (_index.Count > _maxEntries)
            {
                _index.Remove(_index.OrderBy(pair => pair.Value.Seq).First().Key);
            }
        }

        private IReadOnlyList<JournalEntry> Snapshot() =>
            _index.OrderBy(pair => pair.Value.Seq).Select(pair => new JournalEntry(pair.Key, pair.Value.RunId, pair.Value.State)).ToList();

        // Writes the file anew from what is worth keeping (one line per request id), then opens it for appending. A temporary file
        // that replaces the old one, so that a crash while rewriting leaves one of the two intact.
        private void Rewrite()
        {
            _stream?.Dispose();
            _stream = null;
            Trim();

            var temp = _path + ".tmp";
            var builder = new StringBuilder();

            foreach (var entry in Snapshot())
            {
                builder.Append(Serialize(entry.State == JournalState.Started ? "started" : "ended", entry.RequestId, entry.RunId)).Append('\n');
            }

            // On the disk before it replaces the file that is: if the machine dies right after the replacement, the new file must
            // not be an empty one that stands in for the journal.
            using (var file = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                var bytes = new UTF8Encoding(false).GetBytes(builder.ToString());
                file.Write(bytes, 0, bytes.Length);
                file.Flush(true);
            }

            File.Move(temp, _path, overwrite: true);
            _lines = _index.Count;
            _stream = _openForAppend(_path);
        }

        private string Serialize(string state, string requestId, string runId) =>
            JsonSerializer.Serialize(new Line { V = Version, At = DateTimeOffset.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture), Instance = _instanceId, Request = requestId, Run = runId, State = state });

        private bool TryAppend(string state, string requestId, string runId)
        {
            lock (_gate)
            {
                if (_stream == null)
                {
                    return false;
                }

                try
                {
                    var bytes = new UTF8Encoding(false).GetBytes(Serialize(state, requestId, runId) + "\n");
                    _stream.Write(bytes, 0, bytes.Length);

                    // To the disk, not only to the operating system: the point of the line is that it is there after a crash.
                    _stream.Flush(true);
                    Apply(state, requestId, runId);
                    _lines++;
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ObjectDisposedException || e is NotSupportedException)
                {
                    Logger.Warning("External control: a line could not be written to the run journal ({ExceptionType})", e.GetType().FullName);
                    Heal();

                    return false;
                }

                if (_lines >= _maxLines)
                {
                    Compact();
                }

                return true;
            }
        }

        // A write failed, and may have left a fragment of a line at the end of the file; the next line would be joined to it and be
        // lost when the file is read, which would let a run that touched the card be forgotten. So the stream is never used
        // again: the file is written anew from what is known (the fragment is not part of that), or, if that fails too, the
        // journal stays closed and the runs are refused instead of started without a record.
        private void Heal()
        {
            try
            {
                _stream?.Dispose();
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ObjectDisposedException)
            {
                // Closing a stream that failed may fail again; it is not used anyway.
            }

            _stream = null;

            try
            {
                Rewrite();
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is NotSupportedException)
            {
                Logger.Warning("External control: the run journal could not be repaired and is closed ({ExceptionType})", e.GetType().FullName);
                _stream = null;
            }
        }

        // The line is written; keeping the file small is best effort, and a journal that cannot be reopened is closed (the next
        // run is then refused instead of started without a record).
        private void Compact()
        {
            try
            {
                Rewrite();
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is NotSupportedException)
            {
                Logger.Warning("External control: the run journal could not be rewritten ({ExceptionType})", e.GetType().FullName);

                try
                {
                    _stream?.Dispose();
                    _stream = _openForAppend(_path);
                }
                catch (Exception reopen) when (reopen is IOException || reopen is UnauthorizedAccessException)
                {
                    _stream = null;
                }
            }
        }

        private sealed record Entry(string RunId, JournalState State, long Seq);

        // The whole content of a line. Nothing of a request is in it but its id.
        private sealed class Line
        {
            [JsonPropertyName("v")]
            public int V { get; set; }

            [JsonPropertyName("at")]
            public string? At { get; set; }

            [JsonPropertyName("instance")]
            public string? Instance { get; set; }

            [JsonPropertyName("request")]
            public string? Request { get; set; }

            [JsonPropertyName("run")]
            public string? Run { get; set; }

            [JsonPropertyName("state")]
            public string? State { get; set; }
        }
    }
}
