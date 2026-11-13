#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using RFiDGear.Bridge;

namespace RFiDGear.Services.ExternalControl
{
    /// <summary>Where a run that a client asked for stands. The values are moved with compare-and-swap, so each step is taken by exactly one party.</summary>
    internal enum RunRecordState
    {
        /// <summary>The request is known; nothing has been handed to the main window yet.</summary>
        Pending = 0,

        /// <summary>The main window has taken the request and is asking the host to admit it.</summary>
        Admitting = 1,

        /// <summary>The host admitted the run; it is in flight.</summary>
        Running = 2,

        /// <summary>The run ended, and its outcome is final.</summary>
        Completed = 3,

        /// <summary>The wait for the main window gave up before the window took the request; the run was never started and never will be.</summary>
        Abandoned = 4,

        /// <summary>The host refused the run.</summary>
        Refused = 5,

        /// <summary>The journal says that an earlier RFiDGear admitted the run and never wrote down that it ended: the restart interrupted it.</summary>
        Interrupted = 6,
    }

    /// <summary>How the host answered a request to run: it admitted it under the id of the record, or it refused.</summary>
    internal sealed class AdmissionResult
    {
        private AdmissionResult(BridgeFault? refusal)
        {
            Refusal = refusal;
        }

        /// <summary>Gets why the run was not admitted, or <see langword="null"/> when it was.</summary>
        internal BridgeFault? Refusal { get; }

        /// <summary>Makes the answer for a run that was admitted.</summary>
        /// <returns>The answer.</returns>
        internal static AdmissionResult Accepted() => new AdmissionResult(null);

        /// <summary>Makes the answer for a run that was refused.</summary>
        /// <param name="fault">Why.</param>
        /// <returns>The answer.</returns>
        internal static AdmissionResult Refused(BridgeFault fault) => new AdmissionResult(fault ?? throw new ArgumentNullException(nameof(fault)));
    }

    /// <summary>
    /// One request to run that a client sent. Every <c>startRun</c> with the same request id waits for <see cref="Admission"/> and
    /// answers what it says, so a duplicate that arrives while the first is still being admitted is never told "accepted" when
    /// the first is then refused.
    /// </summary>
    internal sealed class RunRecord
    {
        private int _state = (int)RunRecordState.Pending;
        private int _cancellationDisposed;
        private int _hostEnded;

        /// <summary>Creates the record of a request.</summary>
        /// <param name="requestId">The request id.</param>
        /// <param name="payloadHash">The hash of what the request asked for.</param>
        internal RunRecord(string requestId, string payloadHash)
            : this(requestId, payloadHash, Guid.NewGuid().ToString("N"), isRecovered: false)
        {
        }

        private RunRecord(string requestId, string payloadHash, string runId, bool isRecovered)
        {
            RequestId = requestId;
            PayloadHash = payloadHash;
            RunId = runId;
            IsRecovered = isRecovered;
        }

        /// <summary>Gets the request id the client chose.</summary>
        internal string RequestId { get; }

        /// <summary>Gets the hash of what the request asked for; a second request with the same id and another hash is a conflict.</summary>
        internal string PayloadHash { get; }

        /// <summary>
        /// Gets whether the record was read from the journal of an earlier RFiDGear. The journal does not hold what the request
        /// asked for (it must hold no value of a client), so a later request with the same id cannot be compared with it; it is
        /// taken for the same request and starts nothing.
        /// </summary>
        internal bool IsRecovered { get; }

        /// <summary>Makes the record of a run that an earlier RFiDGear started, from what its journal says.</summary>
        /// <param name="requestId">The request id.</param>
        /// <param name="runId">The run id.</param>
        /// <param name="ended">Whether the journal says that the run ended; if not, the restart interrupted it.</param>
        /// <returns>The record, final from the start.</returns>
        internal static RunRecord Recovered(string requestId, string runId, bool ended)
        {
            var record = new RunRecord(requestId, string.Empty, runId, isRecovered: true);
            record._state = (int)(ended ? RunRecordState.Completed : RunRecordState.Interrupted);
            record.Admission.TrySetResult(AdmissionResult.Accepted());
            record.Completion.TrySetResult(ended);
            record.Cancellation.Dispose();
            record._cancellationDisposed = 1;

            return record;
        }

        /// <summary>Gets the id the run has in its result and in the log. It is made with the record and used only if the run is admitted.</summary>
        internal string RunId { get; }

        /// <summary>Gets the answer of the host to the request; completed once, and every caller of the same request sees the same one.</summary>
        internal TaskCompletionSource<AdmissionResult> Admission { get; } = new TaskCompletionSource<AdmissionResult>(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// Gets what wakes up a wait for the result: <see langword="true"/> when the run has ended, <see langword="false"/> when the
        /// request was refused or abandoned. It carries no result: a waiter reads the registry when it wakes, so that a result the
        /// history has dropped is not kept alive by a task that someone still holds.
        /// </summary>
        internal TaskCompletionSource<bool> Completion { get; } = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// Gets the source of the token that cancels the run. It exists from the start, so that a cancellation that arrives
        /// before the request has been admitted is not lost: the run, if it is admitted, is handed a token that is cancelled.
        /// </summary>
        internal CancellationTokenSource Cancellation { get; } = new CancellationTokenSource();

        /// <summary>Says that the source of the cancellation token is to be disposed, once.</summary>
        /// <returns><see langword="true"/> for the one caller that may dispose it.</returns>
        internal bool TryClaimCancellationDisposal() => Interlocked.Exchange(ref _cancellationDisposed, 1) == 0;

        /// <summary>
        /// Gets or sets whether the host has reported the run as ended (it gave up its gate). The result of the run is final a
        /// moment later, when its task completes; in between the host may already be running something else.
        /// </summary>
        internal bool HostEnded
        {
            get => Volatile.Read(ref _hostEnded) != 0;
            set => Volatile.Write(ref _hostEnded, value ? 1 : 0);
        }

        /// <summary>Gets or sets when the host admitted the run.</summary>
        internal DateTimeOffset? StartedAt { get; set; }

        /// <summary>Gets or sets the final result; kept while the registry keeps results, then <see langword="null"/>.</summary>
        internal RunResultDto? Result { get; set; }

        /// <summary>Gets where the request stands.</summary>
        internal RunRecordState State => (RunRecordState)Volatile.Read(ref _state);

        /// <summary>Moves the record from one state to another if it is in the first.</summary>
        /// <param name="from">The state it must be in.</param>
        /// <param name="to">The state to move it to.</param>
        /// <returns><see langword="true"/> for the one caller that made the move.</returns>
        internal bool TryTransition(RunRecordState from, RunRecordState to) =>
            Interlocked.CompareExchange(ref _state, (int)to, (int)from) == (int)from;
    }

    /// <summary>
    /// The requests to run that the bridge has seen, for the life of the process, whichever client sent them: a client that
    /// reconnects asks about the same table. Admitted runs stay (only they make a request id sticky); a request that was
    /// refused or abandoned is forgotten, so the id can be used again. Results are kept up to a limit, request ids up to a
    /// larger one: past the first a result is "unknown" again, past the second the id can be used for a new request.
    /// </summary>
    internal sealed class RunRegistry
    {
        private readonly object _gate = new object();
        private readonly int _resultHistory;
        private readonly int _requestIdHistory;
        private readonly Dictionary<string, RunRecord> _byRequestId = new Dictionary<string, RunRecord>(StringComparer.Ordinal);
        private readonly Dictionary<string, RunRecord> _byRunId = new Dictionary<string, RunRecord>(StringComparer.Ordinal);
        private readonly Queue<RunRecord> _completedOrder = new Queue<RunRecord>();
        private readonly Queue<RunRecord> _withResult = new Queue<RunRecord>();
        private long _lastResultSeq;

        /// <summary>Creates the registry.</summary>
        /// <param name="resultHistory">How many results are kept.</param>
        /// <param name="requestIdHistory">How many finished requests are remembered by id.</param>
        internal RunRegistry(int resultHistory = BridgeProtocol.ResultHistory, int requestIdHistory = BridgeProtocol.RequestIdHistory)
        {
            _resultHistory = resultHistory;
            _requestIdHistory = requestIdHistory;
        }

        /// <summary>Gets the number of the latest result, 0 when there is none.</summary>
        internal long LastResultSeq => Interlocked.Read(ref _lastResultSeq);

        /// <summary>
        /// Takes in what the journal says about the runs of earlier processes, oldest first. Each becomes a record that is final:
        /// a run that ended is a completed one whose result is gone, one that did not is interrupted. They count towards the
        /// history like the runs of this process.
        /// </summary>
        /// <param name="entries">What the journal held.</param>
        internal void Recover(IEnumerable<JournalEntry> entries)
        {
            ArgumentNullException.ThrowIfNull(entries);

            lock (_gate)
            {
                foreach (var entry in entries)
                {
                    if (_byRequestId.ContainsKey(entry.RequestId))
                    {
                        continue;
                    }

                    var record = RunRecord.Recovered(entry.RequestId, entry.RunId, entry.State == JournalState.Ended);
                    _byRequestId[entry.RequestId] = record;
                    _byRunId[entry.RunId] = record;
                    _completedOrder.Enqueue(record);
                }

                Evict();
            }
        }

        /// <summary>Finds the record of a request id, or makes one.</summary>
        /// <param name="requestId">The request id.</param>
        /// <param name="payloadHash">The hash of what the request asks for.</param>
        /// <returns>The record, and whether this call made it (the caller that made it is the one that starts the run).</returns>
        internal (RunRecord Record, bool Created) GetOrCreate(string requestId, string payloadHash)
        {
            lock (_gate)
            {
                if (_byRequestId.TryGetValue(requestId, out var existing))
                {
                    return (existing, false);
                }

                var record = new RunRecord(requestId, payloadHash);
                _byRequestId[requestId] = record;
                _byRunId[record.RunId] = record;

                return (record, true);
            }
        }

        /// <summary>Finds a record by the request id or by the run id.</summary>
        /// <param name="requestId">The request id, or <see langword="null"/>.</param>
        /// <param name="runId">The run id, or <see langword="null"/>.</param>
        /// <returns>The record, or <see langword="null"/> when the registry does not know the id.</returns>
        internal RunRecord? Find(string? requestId, string? runId)
        {
            lock (_gate)
            {
                if (requestId != null)
                {
                    return _byRequestId.TryGetValue(requestId, out var byRequest) ? byRequest : null;
                }

                return runId != null && _byRunId.TryGetValue(runId, out var byRun) ? byRun : null;
            }
        }

        /// <summary>Reads the state and the result of a record together, so that a caller never sees a completed state without its result.</summary>
        /// <param name="record">The record.</param>
        /// <returns>The state and the result, which is <see langword="null"/> until the run has ended and again after the result was dropped.</returns>
        internal (RunRecordState State, RunResultDto? Result) Read(RunRecord record)
        {
            lock (_gate)
            {
                return (record.State, record.Result);
            }
        }

        /// <summary>Forgets a request that was refused or abandoned, and ends the waits for its result.</summary>
        /// <param name="record">The record.</param>
        internal void Forget(RunRecord record)
        {
            lock (_gate)
            {
                if (_byRequestId.TryGetValue(record.RequestId, out var current) && ReferenceEquals(current, record))
                {
                    _byRequestId.Remove(record.RequestId);
                }

                _byRunId.Remove(record.RunId);
            }

            record.Completion.TrySetResult(false);
        }

        /// <summary>
        /// Ends a run that is in flight: numbers the result, keeps it, and makes the record final. Only the caller that moves the
        /// record from running to completed does this, so a run has exactly one final result.
        /// </summary>
        /// <param name="record">The record of the run.</param>
        /// <param name="build">Makes the result for a number.</param>
        /// <returns>The result, or <see langword="null"/> when another caller had already ended the run.</returns>
        internal RunResultDto? Complete(RunRecord record, Func<long, RunResultDto> build)
        {
            RunResultDto result;

            lock (_gate)
            {
                if (!record.TryTransition(RunRecordState.Running, RunRecordState.Completed))
                {
                    return null;
                }

                var seq = Interlocked.Increment(ref _lastResultSeq);
                result = build(seq);
                record.Result = result;
                _withResult.Enqueue(record);
                _completedOrder.Enqueue(record);
                Evict();
            }

            record.Completion.TrySetResult(true);

            return result;
        }

        // Called under the lock.
        private void Evict()
        {
            while (_withResult.Count > _resultHistory)
            {
                _withResult.Dequeue().Result = null;
            }

            while (_completedOrder.Count > _requestIdHistory)
            {
                var old = _completedOrder.Dequeue();
                old.Result = null;

                if (_byRequestId.TryGetValue(old.RequestId, out var current) && ReferenceEquals(current, old))
                {
                    _byRequestId.Remove(old.RequestId);
                }

                _byRunId.Remove(old.RunId);
            }
        }
    }
}
