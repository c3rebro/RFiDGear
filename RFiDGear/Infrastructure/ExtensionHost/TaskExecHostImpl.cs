#nullable enable
using RFiDGear.Contracts;
using RFiDGear.Infrastructure.Tasks;
using RFiDGear.Infrastructure.Tasks.Interfaces;
using RFiDGear.Services;
using RFiDGear.Services.TaskExecution;

using Serilog;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RFiDGear.Infrastructure.ExtensionHost
{
    /// <summary>
    /// <see cref="ITaskExecHost"/> implementation backed by <see cref="ITaskExecutionService"/>.
    /// Drives headless single-pass execution without a WPF dispatcher or UI callbacks.
    /// </summary>
    internal sealed class TaskExecHostImpl : ITaskExecHost
    {
        // Resolved on each use, not cached, so the logger configured at startup (and the one tests install)
        // is the one that is used even if this type was touched before logging was configured.
        private static ILogger Logger => Log.ForContext<TaskExecHostImpl>();

        private readonly ITaskExecutionService _execService;
        private readonly ProjectHostImpl _project;
        private readonly ExtensionOperationCoordinator _coordinator;

        public TaskExecHostImpl(
            ITaskExecutionService execService,
            ProjectHostImpl project,
            ExtensionOperationCoordinator coordinator)
        {
            _execService = execService ?? throw new ArgumentNullException(nameof(execService));
            _project = project ?? throw new ArgumentNullException(nameof(project));
            _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));

            // The coordinator is shared with the main window, so this also republishes its runs. The host and the
            // coordinator live for the whole session, which is why there is nothing to unsubscribe.
            _coordinator.IsExecutingChanged += OnCoordinatorIsExecutingChanged;
        }

        /// <inheritdoc/>
        public bool IsRunning => _coordinator.IsExecuting;

        /// <inheritdoc/>
        public event EventHandler? IsRunningChanged;

        /// <inheritdoc/>
        public event EventHandler<ExecResult>? ExecutionCompleted;

        /// <inheritdoc/>
        public async Task<ExecResult> ExecuteAllAsync(ExecutionRequest request, CancellationToken ct = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            // Resolve and validate report variables before admission: a request that cannot produce
            // its report must be rejected before any card operation, not fail at the last step.
            var variablesFromArgs = new Dictionary<string, string>(
                request.Variables,
                StringComparer.OrdinalIgnoreCase);
            ResolveReportVariables(variablesFromArgs, out var reportOutputPath, out var reportTemplateFile);

            if (!_coordinator.TryBeginExecution())
                throw new InvalidOperationException(BusyMessage(
                    whenLoading: "A project load is in progress. Wait for LoadAsync to complete before executing.",
                    whenEditing: "A project edit is in progress. Wait for it to complete before executing.",
                    whenRunning: "An execution is already in progress."));

            // Allocate the RunId before the call so result, logs, and the execution service
            // all share the same identity regardless of where or when the run ends.
            var runId = Guid.NewGuid().ToString("N");
            var startedAt = DateTimeOffset.UtcNow;

            // The sink is owned here so the observations survive cancellation and faults, where the
            // execution service throws and returns nothing. One fresh list per run.
            var observations = new List<TaskExecutionObservation>();
            IReadOnlyList<object?> snapshot = Array.Empty<object?>();

            ExecResult execResult;
            try
            {
                // Same reset the main window performs before "Run All Tasks Once". Without it,
                // results from an earlier run would be reported for tasks skipped in this one.
                ResetTaskState();

                // Observation positions index into the collection as it was when the run started; the
                // live collection can change while the run is in progress.
                snapshot = _project.SetupModel?.TaskCollection?.ToList<object?>() ?? new List<object?>();

                var taskRequest = new TaskExecutionRequest
                {
                    RunId = runId,
                    Observations = observations,
                    TaskHandler = _project.SetupModel,
                    VariablesFromArgs = variablesFromArgs,
                    ReportOutputPath = reportOutputPath,
                    ReportTemplateFile = reportTemplateFile,
                    UpdateSelectedSetupViewModel = _ => { },
                    UpdateReaderBusy = _ => { },
                    NotifyTreeViewChanged = () => { },
                    NotifyTasksChanged = () => { },
                    RunSelectedOnly = false,
                };

                var rawResult = await _execService.ExecuteOnceAsync(taskRequest, ct).ConfigureAwait(false);

                var completedAt = DateTimeOffset.UtcNow;
                var outcome = MapOutcome(rawResult.TerminalStatus);
                var failureReason = ExecutionFailureReason.None;
                if (rawResult.TerminalStatus == null)
                {
                    // The service returned without evaluating the task loop; never report that as a clean result.
                    failureReason = ExecutionFailureReason.Faulted;
                    Logger.Warning("Extension-triggered run {RunId} ended without a terminal evaluation", runId);
                }

                var taskOutcomes = TaskOutcomeMapper.Build(snapshot, observations);
                var failedCount = taskOutcomes.Count(t => t.IsSuccess == false);

                execResult = new ExecResult(
                    runId,
                    outcome,
                    snapshot.Count,
                    failedCount,
                    startedAt,
                    completedAt,
                    taskOutcomes,
                    failureReason);
            }
            catch (Exception ex)
            {
                // Cancelled or faulted runs still produce a terminal ExecResult. Preserve
                // partial task outcomes so the caller can correlate completed tasks with logs.
                var failureReason = Classify(ex, ct);
                LogFailedRun(runId, failureReason, ex);

                var completedAt = DateTimeOffset.UtcNow;
                var taskOutcomes = TaskOutcomeMapper.Build(snapshot, observations);
                var failedCount = taskOutcomes.Count(t => t.IsSuccess == false);

                execResult = new ExecResult(
                    runId,
                    ExecutionOutcome.Failure,
                    snapshot.Count,
                    failedCount,
                    startedAt,
                    completedAt,
                    taskOutcomes,
                    failureReason);
            }
            finally
            {
                _coordinator.EndExecution();
            }

            // Raised after the coordinator is released and outside try/finally, so exactly one
            // publication happens per accepted run and subscribers see IsRunning == false.
            RaiseExecutionCompleted(execResult);
            return execResult;
        }

        /// <inheritdoc/>
#pragma warning disable CS0618 // implementing the obsolete interface member
        public Task<ExecResult> ExecuteAllAsync(CancellationToken ct = default) =>
            ExecuteAllAsync(ExecutionRequest.Empty, ct);
#pragma warning restore CS0618

        /// <inheritdoc/>
        public void ResetStatus()
        {
            // The project-edit slot is the right gate: it is refused while a run, a load or another edit is
            // active (clearing state under a running task loop is what breaks the one-attempt rule), and while
            // it is held no run can start in the middle of the reset.
            if (!_coordinator.TryBeginProjectOperation())
            {
                throw new InvalidOperationException(BusyMessage(
                    whenLoading: "A project load is in progress. Wait for LoadAsync to complete before resetting the task status.",
                    whenEditing: "A project edit is in progress. Wait for it to complete before resetting the task status.",
                    whenRunning: "A task run is in progress. Reset the task status after it has completed."));
            }

            try
            {
                ResetTaskState();
            }
            finally
            {
                _coordinator.EndProjectOperation();
            }
        }

        // ── private helpers ──────────────────────────────────────────────────

        /// <summary>
        /// Picks the message that matches what the coordinator is busy with. The state can change between the
        /// refused attempt and this read, in which case the last message is used.
        /// </summary>
        private string BusyMessage(string whenLoading, string whenEditing, string whenRunning) =>
            _coordinator.IsLoading ? whenLoading
            : _coordinator.IsMutating ? whenEditing
            : whenRunning;

        private static ExecutionOutcome MapOutcome(TaskLoopTerminalStatus? status) => status switch
        {
            TaskLoopTerminalStatus.Success                          => ExecutionOutcome.Success,
            TaskLoopTerminalStatus.CompletedWithIntentionalFailures => ExecutionOutcome.CompletedWithIntentionalFailures,
            _                                                       => ExecutionOutcome.Failure,
        };

        /// <summary>
        /// Maps an exception from the execution service to a non-sensitive reason. A bare
        /// <see cref="OperationCanceledException"/> only counts as caller cancellation when the
        /// caller's token was actually cancelled; anything else is an unexpected fault.
        /// </summary>
        private static ExecutionFailureReason Classify(Exception ex, CancellationToken ct) => ex switch
        {
            OperationCanceledException when ct.IsCancellationRequested => ExecutionFailureReason.Cancelled,
            TimeoutException                                            => ExecutionFailureReason.TimedOut,
            _                                                           => ExecutionFailureReason.Faulted,
        };

        /// <summary>
        /// Logs a run that ended abnormally. The exception is described through <see cref="SanitizedException"/>
        /// and never attached, so key material in its message cannot reach the log a second time.
        /// </summary>
        private static void LogFailedRun(string runId, ExecutionFailureReason reason, Exception ex)
        {
            var error = new SanitizedException(ex);

            switch (reason)
            {
                case ExecutionFailureReason.Cancelled:
                    Logger.Information("Extension-triggered run {RunId} was cancelled at a task boundary", runId);
                    break;
                case ExecutionFailureReason.TimedOut:
                    Logger.Warning(
                        "Extension-triggered run {RunId} timed out: {ExceptionType}: {ExceptionMessage}",
                        runId, error.TypeName, error.Message);
                    break;
                default:
                    Logger.Error(
                        "Extension-triggered run {RunId} faulted: {ExceptionType}: {ExceptionMessage} {ExceptionStackTrace}",
                        runId, error.TypeName, error.Message, error.StackTrace);
                    break;
            }
        }

        private void OnCoordinatorIsExecutingChanged(object? sender, EventArgs e) => RaiseIsRunningChanged();

        /// <summary>
        /// Raises <see cref="IsRunningChanged"/> to each subscriber in isolation, with this host as the sender (never
        /// the internal coordinator): a throwing subscriber is logged and must not break admission or release of the
        /// run, nor starve the subscribers after it.
        /// </summary>
        private void RaiseIsRunningChanged()
        {
            var handlers = IsRunningChanged;
            if (handlers == null)
            {
                return;
            }

            foreach (var handler in handlers.GetInvocationList().Cast<EventHandler>())
            {
                try
                {
                    handler(this, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    var error = new SanitizedException(ex);
                    Logger.Error(
                        "IsRunningChanged subscriber threw; the exception was not propagated. {ExceptionType}: {ExceptionMessage} {ExceptionStackTrace}",
                        error.TypeName, error.Message, error.StackTrace);
                }
            }
        }

        /// <summary>
        /// Raises <see cref="ExecutionCompleted"/> to each subscriber in isolation: a throwing
        /// subscriber is logged and must not turn a finished card operation into an exception,
        /// nor starve the subscribers after it.
        /// </summary>
        private void RaiseExecutionCompleted(ExecResult result)
        {
            var handlers = ExecutionCompleted;
            if (handlers == null)
            {
                return;
            }

            foreach (var handler in handlers.GetInvocationList().Cast<EventHandler<ExecResult>>())
            {
                try
                {
                    handler(this, result);
                }
                catch (Exception ex)
                {
                    var error = new SanitizedException(ex);
                    Logger.Error(
                        "ExecutionCompleted subscriber threw for run {RunId}; the exception was not propagated. {ExceptionType}: {ExceptionMessage} {ExceptionStackTrace}",
                        result.RunId, error.TypeName, error.Message, error.StackTrace);
                }
            }
        }

        /// <summary>
        /// Clears completion flag, error level, and execution state on every task, matching the
        /// main window's reset before a full run.
        /// </summary>
        private void ResetTaskState()
        {
            var tasks = _project.SetupModel?.TaskCollection;
            if (tasks == null)
            {
                return;
            }

            foreach (var item in tasks)
            {
                if (item is IGenericTask task)
                {
                    task.IsTaskCompletedSuccessfully = null;
                    task.CurrentTaskErrorLevel = ERROR.Empty;
                    task.ExecutionState = TaskExecutionState.NotStarted;
                }
            }
        }

        /// <summary>
        /// Applies the same interpretation as the startup arguments: the report variables set the
        /// request's <c>ReportOutputPath</c> / <c>ReportTemplateFile</c>, which the execution service
        /// reads instead of the variable dictionary. Blank values count as not supplied.
        /// </summary>
        /// <exception cref="ArgumentException">A supplied report variable cannot be honoured.</exception>
        private static void ResolveReportVariables(
            IReadOnlyDictionary<string, string> variables,
            out string? reportOutputPath,
            out string? reportTemplateFile)
        {
            reportOutputPath = null;
            reportTemplateFile = null;

            if (TryGetFirstNonBlank(variables, out var targetKey, out var target, "REPORTTARGETPATH", "REPORTOUTPUTPATH"))
            {
                reportOutputPath = ResolveReportTarget(targetKey, target);
            }

            if (TryGetFirstNonBlank(variables, out var templateKey, out var template, "REPORTTEMPLATEFILE", "REPORTTEMPLATEPATH"))
            {
                if (!File.Exists(template))
                {
                    throw new ArgumentException($"Variable {templateKey} must name an existing file.", "request");
                }

                reportTemplateFile = template;
            }
        }

        /// <summary>
        /// Validates a report target before the run is accepted: it must be a fully qualified path that names a
        /// file (not a folder) in a folder that exists. <see cref="StartupArgumentProcessor.NormalizeReportTargetPath"/>
        /// only checks the folder, so an existing folder or a path ending in a separator would pass it and the
        /// run would perform card operations before the report step discovered it cannot write anything.
        /// </summary>
        /// <returns>The path to create, with any <c>?</c> placeholder resolved to the next free number.</returns>
        /// <exception cref="ArgumentException">The target cannot be used as a report file.</exception>
        private static string ResolveReportTarget(string key, string target)
        {
            try
            {
                if (!Path.IsPathFullyQualified(target))
                {
                    throw new ArgumentException($"Variable {key} must be a full (absolute) file path.", "request");
                }

                var resolved = StartupArgumentProcessor.NormalizeReportTargetPath(target)
                    ?? throw new ArgumentException($"Variable {key} must be a file path whose folder exists.", "request");

                if (string.IsNullOrEmpty(Path.GetFileName(resolved)) || Directory.Exists(resolved))
                {
                    throw new ArgumentException($"Variable {key} must name a file, not a folder.", "request");
                }

                return resolved;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is NotSupportedException)
            {
                // Looking at the folder failed (unreadable, name too long, ...): the target cannot be used either.
                throw new ArgumentException($"Variable {key} cannot be used as a report file path.", "request");
            }
        }

        private static bool TryGetFirstNonBlank(
            IReadOnlyDictionary<string, string> variables,
            out string key,
            out string value,
            params string[] candidateKeys)
        {
            foreach (var candidate in candidateKeys)
            {
                if (variables.TryGetValue(candidate, out var found) && !string.IsNullOrWhiteSpace(found))
                {
                    key = candidate;
                    value = found;
                    return true;
                }
            }

            key = string.Empty;
            value = string.Empty;
            return false;
        }
    }
}
