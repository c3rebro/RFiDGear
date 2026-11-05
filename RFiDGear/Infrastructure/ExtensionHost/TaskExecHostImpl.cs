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

            if (!_coordinator.TryBeginExecution())
                throw new InvalidOperationException(
                    _coordinator.IsLoading
                        ? "A project load is in progress. Wait for LoadAsync to complete before executing."
                        : "An execution is already in progress.");

            // Allocate the RunId before the call so result, logs, and the execution service
            // all share the same identity regardless of where or when the run ends.
            var runId = Guid.NewGuid().ToString("N");
            var startedAt = DateTimeOffset.UtcNow;

            ExecResult execResult;
            try
            {
                var variablesFromArgs = new Dictionary<string, string>(
                    request.Variables,
                    StringComparer.OrdinalIgnoreCase);

                var taskRequest = new TaskExecutionRequest
                {
                    RunId = runId,
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

                execResult = new ExecResult(
                    runId,
                    outcome,
                    _project.TaskCount,
                    failedCount,
                    startedAt,
                    completedAt,
                    taskOutcomes);
            }
            catch (Exception)
            {
                // Cancelled or faulted runs still produce a terminal ExecResult. Preserve
                // partial task outcomes so the caller can correlate completed tasks with logs.
                var completedAt = DateTimeOffset.UtcNow;
                var taskOutcomes = BuildTaskOutcomes(_project.SetupModel?.TaskCollection);
                var failedCount = taskOutcomes.Count(t => t.IsSuccess == false);

                execResult = new ExecResult(
                    runId,
                    ExecutionOutcome.Failure,
                    _project.TaskCount,
                    failedCount,
                    startedAt,
                    completedAt,
                    taskOutcomes);
            }
            finally
            {
                _coordinator.EndExecution();
            }

            // Raise outside both try and finally so that:
            // 1. The coordinator is already released before subscribers run.
            // 2. A throwing subscriber cannot cause a second publication.
            ExecutionCompleted?.Invoke(this, execResult);
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
