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
        private static readonly ILogger Logger = Log.ForContext<TaskExecHostImpl>();

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
        }

        /// <inheritdoc/>
        public bool IsRunning => _coordinator.IsExecuting;

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
                throw new InvalidOperationException(
                    _coordinator.IsLoading
                        ? "A project load is in progress. Wait for LoadAsync to complete before executing."
                        : "An execution is already in progress.");

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
        public void ResetStatus() => ResetTaskState();

        // ── private helpers ──────────────────────────────────────────────────

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

        private static void LogFailedRun(string runId, ExecutionFailureReason reason, Exception ex)
        {
            switch (reason)
            {
                case ExecutionFailureReason.Cancelled:
                    Logger.Information("Extension-triggered run {RunId} was cancelled at a task boundary", runId);
                    break;
                case ExecutionFailureReason.TimedOut:
                    Logger.Warning(ex, "Extension-triggered run {RunId} timed out", runId);
                    break;
                default:
                    Logger.Error(ex, "Extension-triggered run {RunId} faulted", runId);
                    break;
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
                return;

            foreach (var handler in handlers.GetInvocationList().Cast<EventHandler<ExecResult>>())
            {
                try
                {
                    handler(this, result);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "ExecutionCompleted subscriber threw for run {RunId}; the exception was not propagated", result.RunId);
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
                return;

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
                reportOutputPath = StartupArgumentProcessor.NormalizeReportTargetPath(target)
                    ?? throw new ArgumentException(
                        $"Variable {targetKey} must be a file path whose directory exists.", "request");
            }

            if (TryGetFirstNonBlank(variables, out var templateKey, out var template, "REPORTTEMPLATEFILE", "REPORTTEMPLATEPATH"))
            {
                if (!File.Exists(template))
                    throw new ArgumentException($"Variable {templateKey} must name an existing file.", "request");

                reportTemplateFile = template;
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
