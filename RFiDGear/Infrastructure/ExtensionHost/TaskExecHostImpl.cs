#nullable enable
using RFiDGear.Contracts;
using RFiDGear.Infrastructure.Tasks;
using RFiDGear.Infrastructure.Tasks.Interfaces;
using RFiDGear.Services.TaskExecution;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        private readonly ITaskExecutionService _execService;
        private readonly ProjectHostImpl _project;
        private int _running;

        public TaskExecHostImpl(ITaskExecutionService execService, ProjectHostImpl project)
        {
            _execService = execService ?? throw new ArgumentNullException(nameof(execService));
            _project = project ?? throw new ArgumentNullException(nameof(project));
        }

        /// <inheritdoc/>
        public bool IsRunning => _running != 0;

        /// <inheritdoc/>
        public event EventHandler<ExecResult>? ExecutionCompleted;

        /// <inheritdoc/>
        public async Task<ExecResult> ExecuteAllAsync(ExecutionRequest request, CancellationToken ct = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
                throw new InvalidOperationException("An execution is already in progress.");

            var startedAt = DateTimeOffset.UtcNow;
            try
            {
                var variablesFromArgs = new Dictionary<string, string>(
                    request.Variables,
                    StringComparer.OrdinalIgnoreCase);

                var taskRequest = new TaskExecutionRequest
                {
                    TaskHandler = _project.SetupModel,
                    VariablesFromArgs = variablesFromArgs,
                    UpdateSelectedSetupViewModel = _ => { },
                    UpdateReaderBusy = _ => { },
                    NotifyTreeViewChanged = () => { },
                    NotifyTasksChanged = () => { },
                    RunSelectedOnly = false,
                };

                var rawResult = await _execService.ExecuteOnceAsync(taskRequest, ct).ConfigureAwait(false);

                var completedAt = DateTimeOffset.UtcNow;
                var outcome = MapOutcome(rawResult.TerminalStatus);
                var taskOutcomes = BuildTaskOutcomes(_project.SetupModel?.TaskCollection);
                var failedCount = taskOutcomes.Count(t => t.IsSuccess == false);
                var runId = rawResult.RunId ?? Guid.NewGuid().ToString("N");

                var execResult = new ExecResult(
                    runId,
                    outcome,
                    _project.TaskCount,
                    failedCount,
                    startedAt,
                    completedAt,
                    taskOutcomes);

                ExecutionCompleted?.Invoke(this, execResult);
                return execResult;
            }
            catch (Exception ex)
            {
                // Cancelled or faulted runs still produce a terminal ExecResult so callers
                // can distinguish "never completed" from "failed" without inspecting exceptions.
                // The OperationCanceledException is NOT re-thrown; the caller checks
                // ct.IsCancellationRequested to determine why the run failed.
                var completedAt = DateTimeOffset.UtcNow;
                var outcome = ex is OperationCanceledException
                    ? ExecutionOutcome.Failure
                    : ExecutionOutcome.Failure;
                var failureResult = new ExecResult(
                    Guid.NewGuid().ToString("N"),
                    outcome,
                    _project.TaskCount,
                    _project.TaskCount,
                    startedAt,
                    completedAt,
                    Array.Empty<TaskOutcome>());

                ExecutionCompleted?.Invoke(this, failureResult);
                return failureResult;
            }
            finally
            {
                Interlocked.Exchange(ref _running, 0);
            }
        }

        /// <inheritdoc/>
#pragma warning disable CS0618 // implementing the obsolete interface member
        public Task<ExecResult> ExecuteAllAsync(CancellationToken ct = default) =>
            ExecuteAllAsync(ExecutionRequest.Empty, ct);
#pragma warning restore CS0618

        /// <inheritdoc/>
        public void ResetStatus()
        {
            if (_project.SetupModel?.TaskCollection == null)
                return;

            foreach (var item in _project.SetupModel.TaskCollection)
            {
                if (item is IGenericTask task)
                    task.IsTaskCompletedSuccessfully = null;
            }
        }

        // ── private helpers ──────────────────────────────────────────────────

        private static ExecutionOutcome MapOutcome(TaskLoopTerminalStatus? status) => status switch
        {
            TaskLoopTerminalStatus.Success                          => ExecutionOutcome.Success,
            TaskLoopTerminalStatus.CompletedWithIntentionalFailures => ExecutionOutcome.CompletedWithIntentionalFailures,
            _                                                       => ExecutionOutcome.Failure,
        };

        private static IReadOnlyList<TaskOutcome> BuildTaskOutcomes(
            ObservableCollection<object>? collection)
        {
            if (collection == null || collection.Count == 0)
                return Array.Empty<TaskOutcome>();

            var outcomes = new List<TaskOutcome>(collection.Count);
            int position = 0;
            foreach (var item in collection)
            {
                if (item is IGenericTask t)
                {
                    var skipReason = t.ExecutionState == TaskExecutionState.NotStarted
                        ? "NotStarted"
                        : null;

                    outcomes.Add(new TaskOutcome(
                        position,
                        t.CurrentTaskIndex ?? position.ToString(),
                        item.GetType().Name,
                        t.IsTaskCompletedSuccessfully,
                        t.CurrentTaskErrorLevel.ToString(),
                        skipReason));
                }
                position++;
            }
            return outcomes;
        }
    }
}
