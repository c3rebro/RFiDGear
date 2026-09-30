using RFiDGear.Contracts;
using RFiDGear.Infrastructure.Tasks.Interfaces;
using RFiDGear.Services.TaskExecution;

using System;
using System.Collections.Generic;
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
        public event EventHandler<ExecResult> ExecutionCompleted;

        /// <inheritdoc/>
        public async Task<ExecResult> ExecuteAllAsync(CancellationToken ct = default)
        {
            if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
                throw new InvalidOperationException("An execution is already in progress.");

            try
            {
                var request = new TaskExecutionRequest
                {
                    TaskHandler = _project.SetupModel,
                    VariablesFromArgs = new Dictionary<string, string>(),
                    UpdateSelectedSetupViewModel = _ => { },
                    UpdateReaderBusy = _ => { },
                    NotifyTreeViewChanged = () => { },
                    NotifyTasksChanged = () => { },
                    RunSelectedOnly = false,
                };

                var rawResult = await _execService.ExecuteOnceAsync(request, ct).ConfigureAwait(false);

                var outcome = Map(rawResult.TerminalStatus);
                var failedCount = CountFailed(_project.SetupModel?.TaskCollection);
                var execResult = new ExecResult(outcome, _project.TaskCount, failedCount);

                ExecutionCompleted?.Invoke(this, execResult);
                return execResult;
            }
            finally
            {
                Interlocked.Exchange(ref _running, 0);
            }
        }

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

        private static ExecutionOutcome Map(TaskLoopTerminalStatus? status) => status switch
        {
            TaskLoopTerminalStatus.Success                        => ExecutionOutcome.Success,
            TaskLoopTerminalStatus.CompletedWithIntentionalFailures => ExecutionOutcome.CompletedWithIntentionalFailures,
            _                                                    => ExecutionOutcome.Failure,
        };

        private static int CountFailed(System.Collections.ObjectModel.ObservableCollection<object>? collection)
        {
            if (collection == null) return 0;
            return collection.OfType<IGenericTask>().Count(t => t.IsTaskCompletedSuccessfully == false);
        }
    }
}
