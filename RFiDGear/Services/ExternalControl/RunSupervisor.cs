#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using RFiDGear.Bridge;
using RFiDGear.Contracts;
using RFiDGear.Infrastructure.ExtensionHost;

using Serilog;

namespace RFiDGear.Services.ExternalControl
{
    /// <summary>
    /// Starts, cancels and looks up the runs that clients ask for. The rules that matter to a client that cannot tell whether
    /// its request arrived:
    /// <list type="bullet">
    /// <item>A request id starts at most one run. A second <c>startRun</c> with the same id and the same content answers with the
    /// run the first started, also while the first is still being admitted, and is never told "accepted" when the first is then
    /// refused. The same id with other content is refused.</item>
    /// <item><c>startRun</c> answers when the host has admitted the run, never when it has ended. The answer "not started" is only
    /// given when that is certain: the wait for the main window is ended by a compare-and-swap that the main window must win
    /// before it touches the host, so a request that was answered as refused can never start late.</item>
    /// <item>The bridge does not decide whether the host is busy: the host admits or refuses at that moment, and a refusal
    /// carries what the application is busy with.</item>
    /// <item>A run has exactly one final result and one <c>runCompleted</c>; they come from the task of the run the bridge
    /// started, never from the completion event of the host, which also fires for runs of other starters.</item>
    /// <item>A client that leaves does not stop a run; the result stays until the history drops it.</item>
    /// </list>
    /// </summary>
    internal sealed class RunSupervisor
    {
        private static readonly TimeSpan DefaultAdmissionTimeout = TimeSpan.FromSeconds(5);

        private static ILogger Logger => Log.ForContext<RunSupervisor>();

        private readonly IRFiDGearExtensionHost _host;
        private readonly IPreassignedRunExecution _execution;
        private readonly RunRegistry _registry;
        private readonly Func<Action, Task> _dispatchToUi;
        private readonly Action<string, bool, BridgeEventFactory> _publish;
        private readonly Func<bool> _isShuttingDown;
        private readonly Func<string?> _busyActivity;
        private readonly TimeSpan _admissionTimeout;
        private readonly object _activeGate = new object();
        private readonly List<RunRecord> _active = new List<RunRecord>();

        /// <summary>Creates the supervisor.</summary>
        /// <param name="host">The host service of RFiDGear.</param>
        /// <param name="execution">The execution of the host that takes the id of the run from the caller.</param>
        /// <param name="registry">The table of requests, which outlives the connections.</param>
        /// <param name="dispatchToUi">Runs an action on the thread of the main window; the host has to be asked from there.</param>
        /// <param name="publish">Hands an event to the client that is connected (name, whether it is durable, the maker of the event).</param>
        /// <param name="isShuttingDown">Says whether RFiDGear is closing.</param>
        /// <param name="busyActivity">Says what the application is busy with, for the hint in a "busy" refusal.</param>
        /// <param name="admissionTimeout">How long to wait for the main window to take a request; five seconds when <see langword="null"/>.</param>
        internal RunSupervisor(
            IRFiDGearExtensionHost host,
            IPreassignedRunExecution execution,
            RunRegistry registry,
            Func<Action, Task> dispatchToUi,
            Action<string, bool, BridgeEventFactory> publish,
            Func<bool> isShuttingDown,
            Func<string?> busyActivity,
            TimeSpan? admissionTimeout = null)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _execution = execution ?? throw new ArgumentNullException(nameof(execution));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _dispatchToUi = dispatchToUi ?? throw new ArgumentNullException(nameof(dispatchToUi));
            _publish = publish ?? throw new ArgumentNullException(nameof(publish));
            _isShuttingDown = isShuttingDown ?? throw new ArgumentNullException(nameof(isShuttingDown));
            _busyActivity = busyActivity ?? throw new ArgumentNullException(nameof(busyActivity));
            _admissionTimeout = admissionTimeout ?? DefaultAdmissionTimeout;
        }

        /// <summary>Gets the number of the latest result.</summary>
        internal long LastResultSeq => _registry.LastResultSeq;

        /// <summary>Gets whether a run that a client started is in flight.</summary>
        internal bool HasActiveRun
        {
            get
            {
                lock (_activeGate)
                {
                    return _active.Count > 0;
                }
            }
        }

        /// <summary>Gets the run that a client started and that is in flight, or <see langword="null"/>.</summary>
        internal ActiveRunDto? ActiveRun
        {
            get
            {
                RunRecord? latest;

                lock (_activeGate)
                {
                    latest = _active.Count > 0 ? _active[^1] : null;
                }

                return latest == null
                    ? null
                    : new ActiveRunDto { RequestId = latest.RequestId, RunId = latest.RunId, Source = RunSources.Bridge, StartedAt = latest.StartedAt };
            }
        }

        /// <summary>Starts a run, or answers with the run an earlier request with the same id started.</summary>
        /// <param name="request">The request.</param>
        /// <returns>The answer, when the host has admitted the run.</returns>
        internal async Task<StartRunResponse> StartAsync(StartRunRequest request)
        {
            var plan = RunPlan.From(request);

            if (_isShuttingDown())
            {
                throw BridgeFault.ShuttingDown();
            }

            var (record, created) = _registry.GetOrCreate(plan.RequestId, plan.PayloadHash);

            if (!created)
            {
                if (!string.Equals(record.PayloadHash, plan.PayloadHash, StringComparison.Ordinal))
                {
                    throw BridgeFault.RequestIdConflict();
                }

                // The same question again: it gets the answer the first one gets, whatever that turns out to be.
                var earlier = await record.Admission.Task.ConfigureAwait(false);

                if (earlier.Refusal != null)
                {
                    throw earlier.Refusal.Clone();
                }

                return Answer(record, duplicate: true);
            }

            AdmissionResult admission;

            try
            {
                admission = await AdmitAsync(record, plan).ConfigureAwait(false);
            }
            catch (Exception e) when (e is not BridgeFault)
            {
                // Not foreseen. Whoever waits for the same request must not wait for ever, and the guard around the call turns
                // this into an internal error with an id for the caller. Admitted already is admitted.
                admission = record.State is RunRecordState.Running or RunRecordState.Completed
                    ? AdmissionResult.Accepted()
                    : AdmissionResult.Refused(new BridgeFault(BridgeErrorCodes.InternalError, BridgeErrorKinds.InternalError));
                Conclude(record, admission);

                if (admission.Refusal == null)
                {
                    return Answer(record, duplicate: false);
                }

                throw;
            }

            Conclude(record, admission);

            if (admission.Refusal != null)
            {
                throw admission.Refusal.Clone();
            }

            return Answer(record, duplicate: false);
        }

        /// <summary>Asks a run to stop at the next task boundary.</summary>
        /// <param name="request">The request.</param>
        /// <returns>What is known of the run now; an id the host does not know is an answer, not an error.</returns>
        internal CancelRunResponse Cancel(CancelRunRequest request)
        {
            if (request == null || !BridgeProtocol.IsValidRequestId(request.RequestId))
            {
                throw BridgeFault.InvalidRequest("requestId");
            }

            var record = _registry.Find(request.RequestId, null);

            if (record == null)
            {
                return new CancelRunResponse { State = RunStates.Unknown, CancelRequested = false };
            }

            switch (_registry.Read(record).State)
            {
                case RunRecordState.Completed:
                    return new CancelRunResponse { State = RunStates.Completed, CancelRequested = false };

                case RunRecordState.Pending:
                case RunRecordState.Admitting:
                case RunRecordState.Running:
                    // A request that has not been admitted yet is cancelled too: the run, if the host admits it, ends at its
                    // first boundary without having done anything of the project.
                    RequestCancellation(record);

                    return new CancelRunResponse { State = RunStates.Running, CancelRequested = true };

                default:
                    return new CancelRunResponse { State = RunStates.Unknown, CancelRequested = false };
            }
        }

        /// <summary>Looks up the result of a run, waiting for it for a while if it is still going on.</summary>
        /// <param name="request">The request.</param>
        /// <returns>The state of the run and, when it has ended, its result.</returns>
        internal async Task<GetRunResultResponse> GetResultAsync(GetRunResultRequest request)
        {
            if (request == null)
            {
                throw BridgeFault.InvalidRequest("request");
            }

            var hasRequestId = request.RequestId != null;
            var hasRunId = request.RunId != null;

            // Exactly one way to name the run, and a name that could be one (ids are made of the same characters).
            if (hasRequestId == hasRunId || (hasRequestId && !BridgeProtocol.IsValidRequestId(request.RequestId)))
            {
                throw BridgeFault.InvalidRequest("requestId");
            }

            if (hasRunId && !BridgeProtocol.IsValidRequestId(request.RunId))
            {
                throw BridgeFault.InvalidRequest("runId");
            }

            if (request.WaitMs < 0 || request.WaitMs > BridgeProtocol.MaxWaitMs)
            {
                throw BridgeFault.InvalidRequest("waitMs");
            }

            var record = _registry.Find(request.RequestId, request.RunId);

            if (record == null)
            {
                return new GetRunResultResponse { State = RunStates.Unknown };
            }

            var (state, result) = _registry.Read(record);

            if ((state is RunRecordState.Pending or RunRecordState.Admitting or RunRecordState.Running) && request.WaitMs > 0)
            {
                await Task.WhenAny(record.Completion.Task, Task.Delay(request.WaitMs)).ConfigureAwait(false);
                (state, result) = _registry.Read(record);
            }

            return state switch
            {
                RunRecordState.Completed when result != null => new GetRunResultResponse { State = RunStates.Completed, Result = result },
                RunRecordState.Pending or RunRecordState.Admitting or RunRecordState.Running => new GetRunResultResponse { State = RunStates.Running },
                _ => new GetRunResultResponse { State = RunStates.Unknown },
            };
        }

        // ── Admission ──

        private sealed class Decision
        {
            internal AdmissionResult? Result { get; set; }

            internal Task<ExecResult>? Run { get; set; }
        }

        private async Task<AdmissionResult> AdmitAsync(RunRecord record, RunPlan plan)
        {
            // Replacing the job of a machine that works through cards is not for a client to do; the same rule as for loadProject.
            if (_host.AutoMode.IsAutoModeEnabled)
            {
                Fail(record, RunRecordState.Pending, RunRecordState.Refused);

                return AdmissionResult.Refused(BridgeFault.AutoModeActive());
            }

            var decision = new Decision();
            Task dispatched;

            try
            {
                dispatched = _dispatchToUi(() => AdmitOnUiThread(record, plan, decision));
            }
            catch (InvalidOperationException)
            {
                // The dispatcher is gone: the application is closing.
                Fail(record, RunRecordState.Pending, RunRecordState.Abandoned);

                return AdmissionResult.Refused(BridgeFault.ShuttingDown());
            }

            try
            {
                await dispatched.WaitAsync(_admissionTimeout).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                // The window has not taken the request. If this compare-and-swap is won here, the window will find the record
                // abandoned and do nothing, so "not started" is true for good. If the window won it first, it is working on the
                // request right now and its decision is what counts.
                if (record.TryTransition(RunRecordState.Pending, RunRecordState.Abandoned))
                {
                    DisposeCancellation(record);

                    return AdmissionResult.Refused(BridgeFault.UiUnresponsive());
                }

                // The window has the request and is asking the host right now. Its decision is the only truth, and it is short (the
                // admission is the synchronous part of the call), so it is waited for, not guessed at: to answer "refused" here
                // while the window then admits the run would tell the client that nothing was started when the card is in use.
                await dispatched.ConfigureAwait(false);
            }
            catch (Exception e) when (e is OperationCanceledException || e is InvalidOperationException)
            {
                // The dispatcher stopped before it ran the request.
                if (record.TryTransition(RunRecordState.Pending, RunRecordState.Abandoned))
                {
                    DisposeCancellation(record);

                    return AdmissionResult.Refused(BridgeFault.ShuttingDown());
                }

                throw;
            }

            var result = decision.Result ?? AdmissionResult.Refused(BridgeFault.UiUnresponsive());

            if (result.Refusal == null && decision.Run != null)
            {
                _ = decision.Run.ContinueWith(task => Finish(record, task), TaskScheduler.Default);
            }

            return result;
        }

        // Runs on the thread of the main window.
        private void AdmitOnUiThread(RunRecord record, RunPlan plan, Decision decision)
        {
            // The window must win this before it touches the host. If the wait gave up first, the record is abandoned, the client
            // was told that nothing was started, and nothing is started.
            if (!record.TryTransition(RunRecordState.Pending, RunRecordState.Admitting))
            {
                return;
            }

            // The run counts as in flight from here: the host raises "running changed" inside the call below, and a notice
            // that is made a moment later must already know whose run it is. A refusal takes it out again.
            record.StartedAt = DateTimeOffset.UtcNow;

            lock (_activeGate)
            {
                _active.Add(record);
            }

            BridgeFault? refusal;

            try
            {
                refusal = CheckProject(plan);
            }
            catch (Exception e)
            {
                // The project could not be read; nothing was asked of the host yet, so nothing was started.
                Logger.Warning("External control: the project could not be checked before a run ({ExceptionType})", e.GetType().FullName);
                refusal = new BridgeFault(BridgeErrorCodes.InternalError, BridgeErrorKinds.InternalError, neverStarted: true);
            }

            if (refusal != null)
            {
                Refuse(record, decision, refusal);

                return;
            }

            Task<ExecResult> run;

            try
            {
                run = _execution.ExecuteAllAsync(plan.ToExecutionRequest(), record.RunId, record.Cancellation.Token);
            }
            catch (Exception e)
            {
                Logger.Warning("External control: the host could not be asked to run ({ExceptionType})", e.GetType().FullName);
                Refuse(record, decision, new BridgeFault(BridgeErrorCodes.InternalError, BridgeErrorKinds.InternalError));

                return;
            }

            // Admission is the synchronous part of the call: a refusal is a task that has already faulted.
            if (run.IsFaulted)
            {
                var cause = run.Exception!.GetBaseException();

                if (cause is InvalidOperationException || cause is ArgumentException)
                {
                    Refuse(
                        record,
                        decision,
                        cause is InvalidOperationException
                            ? BridgeFault.Busy(_busyActivity() ?? BusyActivities.Other)
                            : BridgeFault.InvalidRequest("variables"));

                    return;
                }
            }

            // Admitted. (A task that faulted for another reason is admitted too: the card may have been touched, so the run has a
            // result that says the bridge could not finish it, and the client is not told that nothing happened.)
            record.TryTransition(RunRecordState.Admitting, RunRecordState.Running);
            decision.Run = run;
            decision.Result = AdmissionResult.Accepted();
            PublishStarted(record);
            Logger.Information("External control: run {RunId} was admitted for request {RequestId}", record.RunId, record.RequestId);
        }

        // The run is not admitted: it is no longer in flight, and the callers that wait for the decision get the refusal.
        private void Refuse(RunRecord record, Decision decision, BridgeFault fault)
        {
            Fail(record, RunRecordState.Admitting, RunRecordState.Refused);

            lock (_activeGate)
            {
                _active.Remove(record);
            }

            decision.Result = AdmissionResult.Refused(fault);
        }

        // On the thread of the main window, because the project belongs to it.
        private BridgeFault? CheckProject(RunPlan plan)
        {
            var taskCount = _host.Project.TaskCount;

            if (taskCount <= 0)
            {
                return BridgeFault.NoProjectLoaded();
            }

            if (plan.ExpectProjectPath != null && !string.Equals(Normalise(_host.Project.CurrentPath), plan.ExpectProjectPath, StringComparison.OrdinalIgnoreCase))
            {
                return BridgeFault.ProjectMismatch("expectProjectPath");
            }

            return plan.ExpectTaskCount.HasValue && plan.ExpectTaskCount.Value != taskCount ? BridgeFault.ProjectMismatch("expectTaskCount") : null;
        }

        private static string? Normalise(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            try
            {
                return Path.GetFullPath(path);
            }
            catch (Exception e) when (e is ArgumentException || e is NotSupportedException || e is PathTooLongException)
            {
                return path;
            }
        }

        // The request ends here for the callers that share it; a refused or abandoned one is forgotten afterwards, so that the
        // id can be used for a new request but nobody who asked in between got a different answer.
        private void Conclude(RunRecord record, AdmissionResult admission)
        {
            record.Admission.TrySetResult(admission);

            if (admission.Refusal != null)
            {
                DisposeCancellation(record);
                _registry.Forget(record);
            }
        }

        private void Fail(RunRecord record, RunRecordState from, RunRecordState to)
        {
            record.TryTransition(from, to);
            DisposeCancellation(record);
        }

        // ── Events and the end of a run ──

        private void PublishStarted(RunRecord record)
        {
            var requestId = record.RequestId;
            var runId = record.RunId;
            var startedAt = record.StartedAt ?? DateTimeOffset.UtcNow;

            _publish(
                BridgeEvents.RunStarted,
                true,
                (seq, sessionId, at) => new RunStartedEvent { Seq = seq, SessionId = sessionId, At = at, RequestId = requestId, RunId = runId, StartedAt = startedAt });
        }

        // The one place a run of the bridge ends. It is reached from the task of the run only.
        private void Finish(RunRecord record, Task<ExecResult> task)
        {
            ExecResult? hostResult = null;

            if (task.IsCompletedSuccessfully)
            {
                hostResult = task.Result;
            }
            else
            {
                Logger.Error("External control: run {RunId} ended without a result of the host ({ExceptionType})", record.RunId, task.Exception?.GetBaseException().GetType().FullName ?? "cancelled");
            }

            var startedAt = record.StartedAt ?? DateTimeOffset.UtcNow;
            var result = _registry.Complete(
                record,
                seq => hostResult != null
                    ? BridgeMapper.ToDto(hostResult, record.RunId, record.RequestId, seq)
                    : BridgeMapper.ToFaultedDto(record.RunId, record.RequestId, seq, startedAt));

            lock (_activeGate)
            {
                _active.Remove(record);
            }

            DisposeCancellation(record);

            if (result == null)
            {
                return;
            }

            Logger.Information(
                "External control: run {RunId} of request {RequestId} ended as {Status} (result {ResultSeq}, {TotalTasks} tasks, {FailedTasks} failed)",
                result.RunId, result.RequestId, result.Status, result.ResultSeq, result.TotalTasks, result.FailedTasks);
            _publish(
                BridgeEvents.RunCompleted,
                true,
                (seq, sessionId, at) => new RunCompletedEvent { Seq = seq, SessionId = sessionId, At = at, Result = result });
        }

        private StartRunResponse Answer(RunRecord record, bool duplicate) => new StartRunResponse
        {
            RequestId = record.RequestId,
            RunId = record.RunId,
            State = _registry.Read(record).State == RunRecordState.Completed ? RunStates.Completed : RunStates.Running,
            Duplicate = duplicate,
        };

        private static void RequestCancellation(RunRecord record)
        {
            try
            {
                record.Cancellation.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // The run ended while the request came in: there is nothing left to cancel.
            }
        }

        private static void DisposeCancellation(RunRecord record)
        {
            // Cancel() may be running on another thread; the source is disposed once, and RequestCancellation tolerates both.
            if (record.TryClaimCancellationDisposal())
            {
                record.Cancellation.Dispose();
            }
        }
    }
}
