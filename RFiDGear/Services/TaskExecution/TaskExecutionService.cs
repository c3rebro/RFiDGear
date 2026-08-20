using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Threading;

using RFiDGear.Models;
using RFiDGear.ViewModel.TaskSetupViewModels;
using RFiDGear.ViewModel;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.Tasks;
using RFiDGear.Infrastructure.ReaderProviders;
using RFiDGear.Infrastructure.FileAccess;
using RFiDGear.UI.MVVMDialogs.ViewModels;
using RFiDGear.UI.MVVMDialogs.ViewModels.Interfaces;
using Serilog;
using RFiDGear.Infrastructure.Tasks.Interfaces;

namespace RFiDGear.Services.TaskExecution
{
    /// <summary>
    /// Provides the application with a reader device instance.
    /// </summary>
    public interface IReaderDeviceProvider
    {
        ReaderDevice GetInstance();
    }

    /// <summary>
    /// Supplies the singleton <see cref="ReaderDevice"/> to execution flows.
    /// </summary>
    public class ReaderDeviceProvider : IReaderDeviceProvider
    {
        public ReaderDevice GetInstance() => ReaderDevice.Instance;
    }

    /// <summary>
    /// Adapter abstraction around <see cref="DispatcherTimer"/> used by the task executor.
    /// </summary>
    public interface IDispatcherTimerAdapter : IDisposable
    {
        bool IsEnabled { get; set; }
        object Tag { get; set; }
        void Start();
        void Stop();
    }

    /// <summary>
    /// Wraps a <see cref="DispatcherTimer"/> to simplify testing and lifetime control.
    /// </summary>
    public class DispatcherTimerAdapter : IDispatcherTimerAdapter
    {
        private readonly DispatcherTimer dispatcherTimer;

        public DispatcherTimerAdapter(DispatcherTimer dispatcherTimer)
        {
            this.dispatcherTimer = dispatcherTimer;
        }

        public bool IsEnabled
        {
            get => dispatcherTimer.IsEnabled;
            set => dispatcherTimer.IsEnabled = value;
        }

        public object Tag
        {
            get => dispatcherTimer.Tag;
            set => dispatcherTimer.Tag = value;
        }

        public void Start() => dispatcherTimer.Start();

        public void Stop() => dispatcherTimer.Stop();

        public void Dispose()
        {
            dispatcherTimer.Stop();
        }
    }

    /// <summary>
    /// Aggregates timeout settings for each stage of task execution.
    /// </summary>
    public class TaskExecutionTimeouts
    {
        public TimeSpan? DeviceDiscoveryTimeout { get; set; }
        public TimeSpan? ChipHydrationTimeout { get; set; }
        public TimeSpan? SelectionSyncTimeout { get; set; }
        public TimeSpan? TaskLoopTimeout { get; set; }

        public static TaskExecutionTimeouts Default => new TaskExecutionTimeouts
        {
            DeviceDiscoveryTimeout = null,
            ChipHydrationTimeout = null,
            SelectionSyncTimeout = null,
            TaskLoopTimeout = null
        };
    }

    /// <summary>
    /// Describes a single executable task and its identifier.
    /// </summary>
    public class TaskDescriptor
    {
        public TaskDescriptor(int index, IGenericTask task, Func<CancellationToken, Task> executor = null)
        {
            Index = index;
            Task = task;
            Id = task?.CurrentTaskIndex ?? index.ToString(CultureInfo.CurrentCulture);
            ExecuteAsync = executor;
        }

        public int Index { get; }
        public string Id { get; }
        public IGenericTask Task { get; }
        public Func<CancellationToken, Task> ExecuteAsync { get; }
    }

    /// <summary>
    /// Captures the result of locating a reader device.
    /// </summary>
    public class DeviceDiscoveryResult
    {
        public DeviceDiscoveryResult(ReaderDevice device)
        {
            Device = device;
        }

        public ReaderDevice Device { get; }
    }

    /// <summary>
    /// Holds the hydrated chip model produced during execution.
    /// </summary>
    public class ChipHydrationResult
    {
        public ChipHydrationResult(GenericChipModel chip)
        {
            Chip = chip;
        }

        public GenericChipModel Chip { get; }
    }

    /// <summary>
    /// Represents the outcome of synchronizing the hydrated chip with UI selection.
    /// </summary>
    public class SelectionSyncResult
    {
        public SelectionSyncResult(GenericChipModel hydratedChip, bool selectionChanged)
        {
            HydratedChip = hydratedChip;
            SelectionChanged = selectionChanged;
        }

        public GenericChipModel HydratedChip { get; }
        public bool SelectionChanged { get; }
    }

    /// <summary>
    /// Defines structured logging hooks for task execution stages.
    /// </summary>
    public interface ITaskExecutionLogger
    {
        void LogInformation(string stage, object details = null);
        void LogError(string stage, Exception exception, object details = null);
    }

    /// <summary>
    /// Serilog-backed logger used when no external task execution logger is provided.
    /// </summary>
    internal static class TaskExecutionLogSanitizer
    {
        private static readonly Regex SensitiveAssignmentPattern = new Regex(
            @"(?i)\b(?:key|masterkey|picckey|appkey|readkey|writekey|payload)\b\s*[:=]\s*[^\s,;]+",
            RegexOptions.Compiled);

        private static readonly Regex KeyLikeHexPattern = new Regex(
            @"(?i)(?<![0-9a-f])(?:[0-9a-f]{48}|[0-9a-f]{32})(?![0-9a-f])",
            RegexOptions.Compiled);

        public static string Sanitize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            var sanitized = SensitiveAssignmentPattern.Replace(value, "[REDACTED]");
            sanitized = KeyLikeHexPattern.Replace(sanitized, "[REDACTED_HEX]");
            return sanitized.Length > 256 ? sanitized.Substring(0, 256) : sanitized;
        }
    }

    public class NullTaskExecutionLogger : ITaskExecutionLogger
    {
        private readonly ILogger logger = Log.ForContext<NullTaskExecutionLogger>();

        public void LogInformation(string stage, object details = null)
        {
            logger.Information("{SerializedTaskLog}", Serialize(stage, "Information", details));
        }

        public void LogError(string stage, Exception exception, object details = null)
        {
            logger.Error("{SerializedTaskLog}", Serialize(stage, "Error", details, exception));
        }

        private static string Serialize(string stage, string level, object details, Exception exception = null)
        {
            var payload = new
            {
                Stage = stage,
                Level = level,
                Exception = exception == null ? null : new
                {
                    Message = TaskExecutionLogSanitizer.Sanitize(exception.Message),
                    ExceptionType = exception.GetType().Name,
                    StackTrace = TaskExecutionLogSanitizer.Sanitize(exception.StackTrace)
                },
                Details = details,
                Timestamp = DateTimeOffset.UtcNow
            };

            return JsonSerializer.Serialize(payload);
        }
    }

    /// <summary>
    /// Encapsulates the inputs and callbacks required for a single task execution pass.
    /// </summary>
    public class TaskExecutionRequest
    {
        public ChipTaskHandlerModel TaskHandler { get; set; }
        public ObservableCollection<RFiDChipParentLayerViewModel> TreeViewParentNodes { get; set; }
        public Dictionary<string, string> VariablesFromArgs { get; set; }
        public ObservableCollection<IDialogViewModel> Dialogs { get; set; }
        public ReportReaderWriter ReportReaderWriter { get; set; }
        public string ReportOutputPath { get; set; }
        public string ReportTemplateFile { get; set; }
        public object SelectedSetupViewModel { get; set; }
        public bool RunSelectedOnly { get; set; }
        public Action<object> UpdateSelectedSetupViewModel { get; set; }
        public Action<bool> UpdateReaderBusy { get; set; }
        public Action NotifyTreeViewChanged { get; set; }
        public Action NotifyTasksChanged { get; set; }
        public TaskExecutionTimeouts Timeouts { get; set; } = TaskExecutionTimeouts.Default;
        public IReadOnlyList<TaskDescriptor> TaskDescriptors { get; set; }
        public IDictionary<ERROR, string> ErrorRouting { get; set; } = new Dictionary<ERROR, string>();
        public IDictionary<string, string> AlternateExecutionKeys { get; set; }
        public Action<IGenericTask, IDictionary<string, string>> ConfigureTaskStrategy { get; set; }
    }

    /// <summary>
    /// Summarizes the state that should be preserved when execution completes.
    /// </summary>
    public class TaskExecutionResult
    {
        public ReportReaderWriter ReportReaderWriter { get; set; }
        public string ReportOutputPath { get; set; }
        public string ReportTemplateFile { get; set; }
        public object SelectedSetupViewModel { get; set; }
        public bool RunSelectedOnly { get; set; }

        /// <summary>
        /// Gets the correlation identifier shared by all structured events of this execution pass.
        /// </summary>
        public string RunId { get; internal set; }

        /// <summary>
        /// Gets the normalized terminal state derived from the individual task outcomes.
        /// </summary>
        public TaskExecutionTerminalStatus TerminalStatus { get; internal set; } = TaskExecutionTerminalStatus.NotRun;
    }

    /// <summary>
    /// Event arguments emitted when the task execution pipeline ends.
    /// </summary>
    public class TaskExecutionCompletedEventArgs : EventArgs
    {
        public TaskExecutionCompletedEventArgs(TaskExecutionResult result)
        {
            Result = result;
        }

        public TaskExecutionResult Result { get; }
    }

    /// <summary>
    /// Coordinates the sequence of discovery, hydration, synchronization, and task execution.
    /// </summary>
    public interface ITaskExecutionService
    {
        event EventHandler<TaskExecutionCompletedEventArgs> ExecutionCompleted;
        int CurrentTaskIndex { get; }

        /// <summary>
        /// Runs the full task pipeline once using the supplied request context.
        /// </summary>
        Task<TaskExecutionResult> ExecuteOnceAsync(TaskExecutionRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Handles timer-driven timeouts by marking the active task as failed.
        /// </summary>
        void HandleTaskTimeout();
    }

    /// <summary>
    /// Default implementation of <see cref="ITaskExecutionService"/> that bridges readers, hydration, and task loops.
    /// </summary>
    public class TaskExecutionService : ITaskExecutionService
    {
        private readonly IReaderDeviceProvider readerDeviceProvider;
        private readonly IDispatcherTimerAdapter triggerReadChip;
        private readonly IDispatcherTimerAdapter taskTimeout;
        private readonly ITaskExecutionLogger logger;
        private TaskExecutionRequest activeRequest;
        private static readonly HashSet<ERROR> RoutedErrorLevels = new HashSet<ERROR>
        {
            ERROR.AuthFailure,
            ERROR.PermissionDenied,
            ERROR.ProtocolConstraint
        };

        public TaskExecutionService(
            IReaderDeviceProvider readerDeviceProvider,
            IDispatcherTimerAdapter triggerReadChip,
            IDispatcherTimerAdapter taskTimeout,
            ITaskExecutionLogger logger = null)
        {
            this.readerDeviceProvider = readerDeviceProvider;
            this.triggerReadChip = triggerReadChip;
            this.taskTimeout = taskTimeout;
            this.logger = logger ?? new NullTaskExecutionLogger();
        }

        public event EventHandler<TaskExecutionCompletedEventArgs> ExecutionCompleted;

        public int CurrentTaskIndex { get; private set; }

        /// <inheritdoc />
        public async Task<TaskExecutionResult> ExecuteOnceAsync(TaskExecutionRequest request, CancellationToken cancellationToken = default)
        {
            activeRequest = request;
            CurrentTaskIndex = 0;

            var result = new TaskExecutionResult
            {
                ReportOutputPath = request.ReportOutputPath,
                ReportReaderWriter = request.ReportReaderWriter,
                ReportTemplateFile = request.ReportTemplateFile,
                RunSelectedOnly = request.RunSelectedOnly,
                SelectedSetupViewModel = request.SelectedSetupViewModel
            };

            var descriptors = BuildTaskDescriptors(request);
            var runId = Guid.NewGuid().ToString("N");
            result.RunId = runId;

#if DEBUG
            taskTimeout.IsEnabled = false;
#else
            taskTimeout.IsEnabled = true;
            taskTimeout.Start();
#endif
            triggerReadChip.Tag = triggerReadChip.IsEnabled;
            triggerReadChip.IsEnabled = false;

            using var timerScope = new TimerScope(taskTimeout);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (descriptors.Count > 0 && descriptors.All(d => d.ExecuteAsync != null))
                {
                    var taskLoopResult = await ExecuteStageWithTimeout(
                        "TaskLoop",
                        () => RunTaskLoopAsync(request, result, descriptors, null, null, runId, cancellationToken),
                        request.Timeouts?.TaskLoopTimeout,
                        cancellationToken,
                        runId,
                        loopResult => loopResult.TerminalStatus == TaskExecutionTerminalStatus.Succeeded);
                    result.TerminalStatus = taskLoopResult.TerminalStatus;
                }
                else
                {
                    var deviceResult = await ExecuteStageWithTimeout(
                        "DeviceDiscovery",
                        () => Task.FromResult(new DeviceDiscoveryResult(readerDeviceProvider.GetInstance())),
                        request.Timeouts?.DeviceDiscoveryTimeout,
                        cancellationToken,
                        runId);

                    if (deviceResult.Device == null)
                    {
                        throw new InvalidOperationException("No reader device available for execution.");
                    }

                    using (var device = deviceResult.Device)
                    {
                        var hydrationResult = await ExecuteStageWithTimeout(
                            "ChipHydration",
                            () => HydrateChipAsync(device, cancellationToken),
                            request.Timeouts?.ChipHydrationTimeout,
                            cancellationToken,
                        runId);

                        _ = await ExecuteStageWithTimeout(
                            "SelectionSync",
                            () => Task.FromResult(SynchronizeSelection(request, hydrationResult.Chip)),
                            request.Timeouts?.SelectionSyncTimeout,
                            cancellationToken,
                        runId);

                        var taskLoopResult = await ExecuteStageWithTimeout(
                            "TaskLoop",
                            () => RunTaskLoopAsync(request, result, descriptors, hydrationResult.Chip, device, runId, cancellationToken),
                            request.Timeouts?.TaskLoopTimeout,
                            cancellationToken,
                            runId,
                            loopResult => loopResult.TerminalStatus == TaskExecutionTerminalStatus.Succeeded);
                        result.TerminalStatus = taskLoopResult.TerminalStatus;
                    }
                }
            }
            catch (Exception e)
            {
                logger.LogError("ExecuteOnceAsync", e, new { RunId = runId, CurrentTaskIndex });
                throw;
            }
            finally
            {
                ResetSelectionState(request, result);
                triggerReadChip.IsEnabled = triggerReadChip.Tag is bool value && value;
            }

            result.SelectedSetupViewModel = request.SelectedSetupViewModel;
            ExecutionCompleted?.Invoke(this, new TaskExecutionCompletedEventArgs(result));
            return result;
        }

        /// <inheritdoc />
        public void HandleTaskTimeout()
        {
#if DEBUG
            taskTimeout.Start();
            taskTimeout.Stop();
            taskTimeout.IsEnabled = false;

            SetTaskFailed(CurrentTaskIndex);
#else
            taskTimeout.IsEnabled = false;
            taskTimeout.Stop();

            if (taskTimeout.Tag is int taskIndex)
            {
                SetTaskFailed(taskIndex);
            }

            CurrentTaskIndex = int.MaxValue;
#endif
        }

        private void SetTaskFailed(int taskIndex)
        {
            if (activeRequest?.TaskHandler.TaskCollection != null &&
                activeRequest.TaskHandler.TaskCollection.Count > taskIndex)
            {
                (activeRequest.TaskHandler.TaskCollection[taskIndex] as IGenericTask).IsTaskCompletedSuccessfully = false;
            }
        }

        private IReadOnlyList<TaskDescriptor> BuildTaskDescriptors(TaskExecutionRequest request)
        {
            if (request.TaskDescriptors != null && request.TaskDescriptors.Count > 0)
            {
                return request.TaskDescriptors;
            }

            var descriptors = new List<TaskDescriptor>();

            if (request?.TaskHandler?.TaskCollection != null)
            {
                for (var index = 0; index < request.TaskHandler.TaskCollection.Count; index++)
                {
                    var task = request.TaskHandler.TaskCollection[index] as IGenericTask;
                    descriptors.Add(new TaskDescriptor(index, task));
                }
            }

            return descriptors;
        }

        private async Task<T> ExecuteStageWithTimeout<T>(
            string stageName,
            Func<Task<T>> stageAction,
            TimeSpan? timeout,
            CancellationToken cancellationToken,
            string runId,
            Func<T, bool> terminalSuccessEvaluator = null)
        {
            logger.LogInformation(stageName + ".Start", new { RunId = runId, CurrentTaskIndex });

            using var linkedCts = timeout.HasValue && timeout.Value != Timeout.InfiniteTimeSpan
                ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
                : null;

            if (linkedCts != null)
            {
                linkedCts.CancelAfter(timeout.Value);
                cancellationToken = linkedCts.Token;
            }

            try
            {
                var stageTask = stageAction();

                if (timeout.HasValue && timeout.Value != Timeout.InfiniteTimeSpan)
                {
                    var completedTask = await Task.WhenAny(stageTask, Task.Delay(timeout.Value, cancellationToken));
                    if (completedTask != stageTask)
                    {
                        throw new TimeoutException($"Stage {stageName} exceeded timeout of {timeout.Value}.");
                    }
                }

                var result = await stageTask;
                if (terminalSuccessEvaluator == null)
                {
                    logger.LogInformation(stageName + ".Success", new
                    {
                        RunId = runId,
                        Stage = stageName,
                        CurrentTaskIndex,
                        Timestamp = DateTimeOffset.UtcNow
                    });
                }
                else
                {
                    var terminalSucceeded = terminalSuccessEvaluator(result);
                    logger.LogInformation(stageName + (terminalSucceeded ? ".Success" : ".Failure"), new
                    {
                        RunId = runId,
                        Stage = stageName,
                        CurrentTaskIndex,
                        TerminalStatus = terminalSucceeded
                            ? TaskExecutionTerminalStatus.Succeeded.ToString()
                            : TaskExecutionTerminalStatus.Failed.ToString(),
                        Timestamp = DateTimeOffset.UtcNow
                    });
                }

                return result;
            }
            catch (Exception ex)
            {
                logger.LogError(stageName + ".Failure", ex, new
                {
                    RunId = runId,
                    Stage = stageName,
                    CurrentTaskIndex,
                    ExceptionType = ex.GetType().Name,
                    Message = TaskExecutionLogSanitizer.Sanitize(ex.Message),
                    Timestamp = DateTimeOffset.UtcNow
                });
                throw;
            }
        }

        private Task ExecuteStageWithTimeout(string stageName, Func<Task> stageAction, TimeSpan? timeout, CancellationToken cancellationToken, string runId)
        {
            return ExecuteStageWithTimeout<object>(stageName, async () =>
            {
                await stageAction();
                return null;
            }, timeout, cancellationToken, runId);
        }

        private async Task<ChipHydrationResult> HydrateChipAsync(ReaderDevice device, CancellationToken cancellationToken)
        {
            var genericChip = device?.GenericChip ?? new GenericChipModel();

            if (device != null)
            {
                if (device.GenericChip != null && !string.IsNullOrEmpty(device.GenericChip.UID))
                {
                    if (genericChip.CardType.ToString().ToLower(CultureInfo.CurrentCulture).Contains("desfire"))
                    {
                        await device.GetMiFareDESFireChipAppIDs();
                        genericChip = device.GenericChip;
                    }
                }
                else
                {
                    await device.ReadChipPublic();
                    genericChip = device.GenericChip;
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            return new ChipHydrationResult(genericChip);
        }

        private SelectionSyncResult SynchronizeSelection(TaskExecutionRequest request, GenericChipModel hydratedChip)
        {
            if (request.TreeViewParentNodes == null)
            {
                return new SelectionSyncResult(hydratedChip, false);
            }

            var selectionChanged = false;

            if (request.TreeViewParentNodes.Any(x => x.IsSelected))
            {
                request.TreeViewParentNodes.First(x => x.IsSelected).IsSelected = false;
                selectionChanged = true;
            }

            if (!string.IsNullOrWhiteSpace(hydratedChip.UID) && request.TreeViewParentNodes.Any(x => x.UID == hydratedChip.UID))
            {
                request.TreeViewParentNodes.First(x => x.UID == hydratedChip.UID).IsSelected = true;
                request.TreeViewParentNodes.First(x => x.IsSelected).IsBeingProgrammed = true;
                selectionChanged = true;
            }

            return new SelectionSyncResult(hydratedChip, selectionChanged);
        }

        private async Task<TaskLoopRunResult> RunTaskLoopAsync(TaskExecutionRequest request, TaskExecutionResult result, IReadOnlyList<TaskDescriptor> descriptors, GenericChipModel genericChip, ReaderDevice device, string runId, CancellationToken cancellationToken)
        {
            if (request.TaskHandler?.TaskCollection != null)
            {
                foreach (var item in request.TaskHandler.TaskCollection)
                {
                    if (item is IGenericTask t)
                        t.ExecutionState = TaskExecutionState.NotStarted;
                }
            }

            var loggedTaskPositions = new HashSet<int>();
            var observations = new List<TaskExecutionObservation>();
            TaskLoopRunResult taskLoopResult = null;

            try
            {
                while (descriptors != null && descriptors.Count > 0 && CurrentTaskIndex < descriptors.Count)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Delay(50, cancellationToken);

                    if (request.RunSelectedOnly && request.TaskHandler.TaskCollection != null)
                    {
                        CurrentTaskIndex = request.TaskHandler.TaskCollection.IndexOf(request.SelectedSetupViewModel);
                    }

                    taskTimeout.Stop();
                    taskTimeout.Start();
                    taskTimeout.IsEnabled = true;
                    taskTimeout.Tag = CurrentTaskIndex;

                    var taskPosition = CurrentTaskIndex;
                    var descriptor = descriptors[taskPosition];
                    var taskModel = descriptor.Task ?? request.TaskHandler?.TaskCollection?[taskPosition] as IGenericTask;

                    request.ConfigureTaskStrategy?.Invoke(
                        taskModel,
                        request.AlternateExecutionKeys ?? request.VariablesFromArgs ?? new Dictionary<string, string>());

                    var executedTask = false;
                    if (request.TaskHandler?.TaskCollection != null && request.TaskHandler.TaskCollection.Count > taskPosition)
                    {
                        request.SelectedSetupViewModel = request.TaskHandler.TaskCollection[taskPosition];
                    }
                    else if (descriptor.Task != null)
                    {
                        request.SelectedSetupViewModel = descriptor.Task;
                    }

                    request.UpdateSelectedSetupViewModel?.Invoke(request.SelectedSetupViewModel);

                    try
                    {
                        if (descriptor.ExecuteAsync != null)
                        {
                            await descriptor.ExecuteAsync(cancellationToken);
                            executedTask = true;
                            if (!request.RunSelectedOnly)
                            {
                                CurrentTaskIndex++;
                            }
                        }
                        else if (request.TaskHandler?.TaskCollection != null && request.TaskHandler.TaskCollection.Count > taskPosition)
                        {
                            switch (request.TaskHandler.TaskCollection[taskPosition])
                            {
                                case CommonTaskViewModel commonTask:
                                    executedTask = await HandleCommonTaskAsync(commonTask, request, result, descriptors, genericChip, device);
                                    break;
                                case GenericChipTaskViewModel genericTask:
                                    executedTask = await HandleGenericChipTaskAsync(genericTask, request, descriptors, device);
                                    break;
                                case MifareClassicSetupViewModel classicTask:
                                    executedTask = await HandleClassicTaskAsync(classicTask, request, descriptors);
                                    break;
                                case MifareDesfireSetupViewModel desfireTask:
                                    executedTask = await HandleDesfireTaskAsync(desfireTask, request, descriptors);
                                    break;
                                case MifareUltralightSetupViewModel _:
                                    break;
                                default:
                                    break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        if (loggedTaskPositions.Add(taskPosition))
                        {
                            observations.Add(CreateTaskObservation(taskPosition, descriptor, taskModel, "Executed", false));
                            LogTaskOutcome(runId, descriptor, taskModel, "Executed", "Failed", null, ex);
                        }

                        throw;
                    }

                    if (executedTask)
                    {
                        RecordTaskAttempt(taskModel);
                        ApplyErrorRouting(taskModel, request, descriptors);

                        if (loggedTaskPositions.Add(taskPosition))
                        {
                            var success = GetTaskSuccess(taskModel);
                            observations.Add(CreateTaskObservation(taskPosition, descriptor, taskModel, "Executed", success));

                            LogTaskOutcome(
                                runId,
                                descriptor,
                                taskModel,
                                "Executed",
                                success == true ? "Successful" : success == false ? "Failed" : "Unknown");
                        }
                    }
                    else if (!executedTask && CurrentTaskIndex != taskPosition && loggedTaskPositions.Add(taskPosition))
                    {
                        var skipReason = GetSkipReason(taskModel);
                        observations.Add(CreateTaskObservation(taskPosition, descriptor, taskModel, "Skipped", null, skipReason));
                        LogTaskOutcome(
                            runId,
                            descriptor,
                            taskModel,
                            "Skipped",
                            "Skipped",
                            skipReason);
                    }

                    if (request.RunSelectedOnly)
                    {
                        break;
                    }

                    request.NotifyTreeViewChanged?.Invoke();
                }
            }
            finally
            {
                request.NotifyTreeViewChanged?.Invoke();
                taskTimeout.Stop();
                taskLoopResult = TaskLoopTerminalEvaluator.Evaluate(
                    descriptors?.Count ?? 0,
                    observations,
                    request?.ErrorRouting);
                logger.LogInformation("TaskLoop.Summary", new
                {
                    RunId = runId,
                    taskLoopResult.TotalTasks,
                    taskLoopResult.Executed,
                    taskLoopResult.Skipped,
                    taskLoopResult.Failed,
                    taskLoopResult.Successful,
                    taskLoopResult.Unknown,
                    taskLoopResult.AcceptedFailures,
                    taskLoopResult.UnhandledFailures,
                    taskLoopResult.InvalidSkips,
                    TerminalStatus = taskLoopResult.TerminalStatus.ToString(),
                    Timestamp = DateTimeOffset.UtcNow
                });
            }

            return taskLoopResult;
        }

        private static TaskExecutionObservation CreateTaskObservation(
            int taskPosition,
            TaskDescriptor descriptor,
            IGenericTask taskModel,
            string decision,
            bool? wasSuccessful,
            string skipReason = null)
        {
            return new TaskExecutionObservation(
                taskPosition,
                descriptor?.Id ?? taskModel?.CurrentTaskIndex,
                taskModel,
                decision,
                wasSuccessful,
                taskModel?.CurrentTaskErrorLevel ?? ERROR.Empty,
                skipReason);
        }

        private void LogTaskOutcome(
            string runId,
            TaskDescriptor descriptor,
            IGenericTask taskModel,
            string decision,
            string outcome,
            string skipReason = null,
            Exception exception = null)
        {
            logger.LogInformation("Task.Outcome", new
            {
                RunId = runId,
                TaskPosition = descriptor?.Index ?? CurrentTaskIndex,
                TaskId = TaskExecutionLogSanitizer.Sanitize(descriptor?.Id ?? taskModel?.CurrentTaskIndex),
                TaskType = GetTaskTypeName(taskModel),
                Description = GetTaskDescription(taskModel),
                Decision = decision,
                SkipReason = skipReason,
                Outcome = outcome,
                ErrorCode = taskModel?.CurrentTaskErrorLevel.ToString(),
                IsSuccessful = GetTaskSuccess(taskModel),
                ExceptionType = exception?.GetType().Name,
                ExceptionMessage = TaskExecutionLogSanitizer.Sanitize(exception?.Message),
                IO = GetSafeIoMetadata(taskModel),
                Timestamp = DateTimeOffset.UtcNow
            });
        }

        private static bool? GetTaskSuccess(IGenericTask taskModel)
        {
            if (taskModel == null)
            {
                return null;
            }

            if (taskModel.IsTaskCompletedSuccessfully.HasValue)
            {
                return taskModel.IsTaskCompletedSuccessfully.Value;
            }

            if (taskModel.CurrentTaskErrorLevel == ERROR.NoError)
            {
                return true;
            }

            return taskModel.CurrentTaskErrorLevel == ERROR.Empty ? null : false;
        }

        private static string GetSkipReason(IGenericTask taskModel)
        {
            if (taskModel == null)
            {
                return "TaskModelUnavailable";
            }

            return taskModel.SelectedExecuteConditionErrorLevel != ERROR.Empty
                ? "ExecuteConditionNotMet"
                : "NotExecuted";
        }

        private static string GetTaskTypeName(IGenericTask taskModel)
        {
            if (taskModel == null)
            {
                return "Unknown";
            }

            try
            {
                var value = taskModel.GetType().GetProperty("SelectedTaskType")?.GetValue(taskModel);
                return TaskExecutionLogSanitizer.Sanitize(value?.ToString() ?? taskModel.GetType().Name);
            }
            catch
            {
                return taskModel.GetType().Name;
            }
        }

        private static string GetTaskDescription(IGenericTask taskModel)
        {
            if (taskModel == null)
            {
                return null;
            }

            try
            {
                var value = taskModel.GetType().GetProperty("SelectedTaskDescription")?.GetValue(taskModel) as string;
                return TaskExecutionLogSanitizer.Sanitize(value);
            }
            catch
            {
                return null;
            }
        }

        private static object GetSafeIoMetadata(IGenericTask taskModel)
        {
            if (taskModel == null)
            {
                return null;
            }

            var metadata = new Dictionary<string, object>();
            AddSafeIntegerMetadata(metadata, taskModel, "FileNumberCurrentAsInt", "FileNumber");
            AddSafeIntegerMetadata(metadata, taskModel, "FileSizeCurrentAsInt", "FileSizeBytes");

            try
            {
                var outputPath = taskModel.GetType().GetProperty("DesfireReadDataFilePath")?.GetValue(taskModel) as string;
                if (outputPath != null)
                {
                    metadata["OutputConfigured"] = !string.IsNullOrWhiteSpace(outputPath);
                }
            }
            catch
            {
                // Diagnostic metadata must never affect task execution.
            }

            return metadata.Count == 0 ? null : metadata;
        }

        private static void AddSafeIntegerMetadata(
            IDictionary<string, object> metadata,
            object source,
            string propertyName,
            string outputName)
        {
            try
            {
                var value = source.GetType().GetProperty(propertyName)?.GetValue(source);
                if (value is int integerValue)
                {
                    metadata[outputName] = integerValue;
                }
            }
            catch
            {
                // Diagnostic metadata must never affect task execution.
            }
        }

        private void ResetSelectionState(TaskExecutionRequest request, TaskExecutionResult result)
        {
            var selectedNode = request.TreeViewParentNodes?.FirstOrDefault(y => y.IsSelected);
            if (selectedNode != null)
            {
                selectedNode.IsBeingProgrammed = null;
                result.RunSelectedOnly = false;
            }
        }

        private static bool TryResolveTaskIndex(IReadOnlyList<TaskDescriptor> descriptors, string descriptorId, out int targetIndex)
        {
            var descriptor = descriptors?.FirstOrDefault(x => x.Id == descriptorId);
            if (descriptor != null)
            {
                targetIndex = descriptor.Index;
                return true;
            }

            targetIndex = -1;
            return false;
        }

        private sealed class TimerScope : IDisposable
        {
            private readonly IDispatcherTimerAdapter timerAdapter;

            public TimerScope(IDispatcherTimerAdapter timerAdapter)
            {
                this.timerAdapter = timerAdapter;
            }

            public void Dispose()
            {
                timerAdapter?.Stop();
                if (timerAdapter != null)
                {
                    timerAdapter.IsEnabled = false;
                }
            }
        }

        private void RecordTaskAttempt(IGenericTask taskModel)
        {
            if (taskModel == null || taskModel.AttemptResults == null)
            {
                return;
            }

            taskModel.AttemptResults.Add(new TaskAttemptResult
            {
                AttemptedAt = DateTimeOffset.Now,
                ErrorLevel = taskModel.CurrentTaskErrorLevel,
                WasSuccessful = taskModel.IsTaskCompletedSuccessfully,
                Message = taskModel.CurrentTaskErrorLevel.ToString()
            });
        }

        private void ApplyErrorRouting(IGenericTask taskModel, TaskExecutionRequest request, IReadOnlyList<TaskDescriptor> descriptors)
        {
            if (taskModel == null || request?.ErrorRouting == null || descriptors == null)
            {
                return;
            }

            if (!RoutedErrorLevels.Contains(taskModel.CurrentTaskErrorLevel))
            {
                return;
            }

            if (request.ErrorRouting.TryGetValue(taskModel.CurrentTaskErrorLevel, out var descriptorId) &&
                TryResolveTaskIndex(descriptors, descriptorId, out var targetIndex))
            {
                CurrentTaskIndex = targetIndex;
            }
        }

        private async Task<bool> HandleCommonTaskAsync(CommonTaskViewModel commonTask, TaskExecutionRequest request, TaskExecutionResult result, IReadOnlyList<TaskDescriptor> descriptors, GenericChipModel genericChip, ReaderDevice device)
        {
            (request.TaskHandler.TaskCollection[CurrentTaskIndex] as CommonTaskViewModel).GenericChip = genericChip;
            (request.TaskHandler.TaskCollection[CurrentTaskIndex] as CommonTaskViewModel).DesfireChip = device.DesfireChip;
            (request.TaskHandler.TaskCollection[CurrentTaskIndex] as CommonTaskViewModel).AvailableTasks = request.TaskHandler.TaskCollection;
            (request.TaskHandler.TaskCollection[CurrentTaskIndex] as CommonTaskViewModel).Args = request.VariablesFromArgs;
            var executed = false;

            switch (commonTask.SelectedTaskType)
            {
                case TaskType_CommonTask.CreateReport:
                {
                    taskTimeout.Stop();
                    var taskModel = request.TaskHandler.TaskCollection[CurrentTaskIndex] as IGenericTask;

                    if (taskModel.ExecutionState == TaskExecutionState.NotStarted)
                    {
                        taskModel.ExecutionState = TaskExecutionState.Running;
                        taskTimeout.Start();
                        taskTimeout.Stop();

                        DirectoryInfo reportTargetPathDirInfo = null;

                        if (request.VariablesFromArgs != null &&
                            request.VariablesFromArgs.TryGetValue("REPORTTARGETPATH", out var targetReportDir))
                        {
                            var directoryName = Path.GetDirectoryName(targetReportDir);
                            if (!string.IsNullOrEmpty(directoryName))
                            {
                                reportTargetPathDirInfo = new DirectoryInfo(directoryName);
                            }
                        }

                        if (taskModel.SelectedExecuteConditionErrorLevel == ERROR.Empty)
                        {
                            if (string.IsNullOrEmpty(result.ReportOutputPath))
                            {
                                var dlg = new SaveFileDialogViewModel
                                {
                                    Title = ResourceLoader.GetResource("windowCaptionSaveReport"),
                                    Filter = ResourceLoader.GetResource("filterStringSaveReport"),
                                    ParentWindow = null,
                                    InitialDirectory = reportTargetPathDirInfo != null ?
                                    (reportTargetPathDirInfo.Exists ? reportTargetPathDirInfo.FullName : null) : null
                                };

                                if (dlg.Show(request.Dialogs) && dlg.FileName != null)
                                {
                                    result.ReportOutputPath = dlg.FileName;
                                }
                            }

                            if (result.ReportReaderWriter == null)
                            {
                                result.ReportReaderWriter = new ReportReaderWriter();
                            }

                            result.ReportReaderWriter.ReportOutputPath = result.ReportOutputPath;
                            if (!string.IsNullOrEmpty(result.ReportTemplateFile))
                            {
                                result.ReportReaderWriter.ReportTemplateFile = result.ReportTemplateFile;
                            }

                            await (request.TaskHandler.TaskCollection[CurrentTaskIndex] as CommonTaskViewModel).WriteReportCommand.ExecuteAsync(result.ReportReaderWriter);
                            executed = true;
                        }
                        else
                        {
                            if (TryResolveTaskIndex(descriptors, taskModel.SelectedExecuteConditionTaskIndex, out var targetTaskIndex))
                            {
                                if ((request.TaskHandler.TaskCollection[targetTaskIndex] as IGenericTask).CurrentTaskErrorLevel == taskModel.SelectedExecuteConditionErrorLevel)
                                {
                                    if (string.IsNullOrEmpty(result.ReportOutputPath))
                                    {
                                        var dlg = new SaveFileDialogViewModel
                                        {
                                            Title = ResourceLoader.GetResource("windowCaptionSaveReport"),
                                            Filter = ResourceLoader.GetResource("filterStringSaveReport"),
                                            InitialDirectory = reportTargetPathDirInfo != null ?
                                            (reportTargetPathDirInfo.Exists ? reportTargetPathDirInfo.FullName : null) : null
                                        };

                                        if (dlg.Show(request.Dialogs) && dlg.FileName != null)
                                        {
                                            result.ReportOutputPath = dlg.FileName;
                                        }
                                    }

                                    if (result.ReportReaderWriter == null)
                                    {
                                        result.ReportReaderWriter = new ReportReaderWriter();
                                    }

                                    result.ReportReaderWriter.ReportOutputPath = result.ReportOutputPath;
                                    if (!string.IsNullOrEmpty(result.ReportTemplateFile))
                                    {
                                        result.ReportReaderWriter.ReportTemplateFile = result.ReportTemplateFile;
                                    }

                                    await (request.TaskHandler.TaskCollection[CurrentTaskIndex] as CommonTaskViewModel).WriteReportCommand.ExecuteAsync(result.ReportReaderWriter);
                                    executed = true;
                                }
                                else
                                {
                                    CurrentTaskIndex++;
                                }
                            }
                        }

                        taskTimeout.Start();
                        taskModel.ExecutionState = TaskExecutionState.Completed;
                    }
                    else if (taskModel.ExecutionState == TaskExecutionState.Completed)
                    {
                        CurrentTaskIndex++;
                        taskTimeout.Stop();
                        taskTimeout.Start();
                    }
                    break;
                }

                case TaskType_CommonTask.CheckLogicCondition:
                {
                    var taskModel = request.TaskHandler.TaskCollection[CurrentTaskIndex] as IGenericTask;

                    if (taskModel.ExecutionState == TaskExecutionState.NotStarted)
                    {
                        taskModel.ExecutionState = TaskExecutionState.Running;
                        taskTimeout.Start();

                        if (taskModel.SelectedExecuteConditionErrorLevel == ERROR.Empty)
                        {
                            await (request.TaskHandler.TaskCollection[CurrentTaskIndex] as CommonTaskViewModel).CheckLogicCondition.ExecuteAsync(request.TaskHandler.TaskCollection);
                            executed = true;
                        }
                        else
                        {
                            if (TryResolveTaskIndex(descriptors, taskModel.SelectedExecuteConditionTaskIndex, out var targetTaskIndex))
                            {
                                if ((request.TaskHandler.TaskCollection[targetTaskIndex] as IGenericTask).CurrentTaskErrorLevel == taskModel.SelectedExecuteConditionErrorLevel)
                                {
                                    await (request.TaskHandler.TaskCollection[CurrentTaskIndex] as CommonTaskViewModel).CheckLogicCondition.ExecuteAsync(request.TaskHandler.TaskCollection);
                                    executed = true;
                                }
                                else
                                {
                                    CurrentTaskIndex++;
                                }
                            }
                        }

                        taskModel.ExecutionState = TaskExecutionState.Completed;
                    }
                    else if (taskModel.ExecutionState == TaskExecutionState.Completed)
                    {
                        CurrentTaskIndex++;
                        taskTimeout.Stop();
                        taskTimeout.Start();
                    }
                    break;
                }

                case TaskType_CommonTask.ExecuteProgram:
                {
                    var taskModel = request.TaskHandler.TaskCollection[CurrentTaskIndex] as IGenericTask;

                    if (taskModel.ExecutionState == TaskExecutionState.NotStarted)
                    {
                        taskModel.ExecutionState = TaskExecutionState.Running;
                        taskTimeout.Start();

                        if (taskModel.SelectedExecuteConditionErrorLevel == ERROR.Empty)
                        {
                            (request.TaskHandler.TaskCollection[CurrentTaskIndex] as CommonTaskViewModel).ExecuteProgramCommand.Execute(null);
                            executed = true;
                        }
                        else
                        {
                            if (TryResolveTaskIndex(descriptors, taskModel.SelectedExecuteConditionTaskIndex, out var targetTaskIndex))
                            {
                                if ((request.TaskHandler.TaskCollection[targetTaskIndex] as IGenericTask).CurrentTaskErrorLevel == taskModel.SelectedExecuteConditionErrorLevel)
                                {
                                    (request.TaskHandler.TaskCollection[CurrentTaskIndex] as CommonTaskViewModel).ExecuteProgramCommand.Execute(null);
                                    executed = true;
                                }
                                else
                                {
                                    CurrentTaskIndex++;
                                }
                            }
                        }

                        taskModel.ExecutionState = TaskExecutionState.Completed;
                    }
                    else if (taskModel.ExecutionState == TaskExecutionState.Completed)
                    {
                        CurrentTaskIndex++;
                        taskTimeout.Stop();
                        taskTimeout.Start();
                    }
                    break;
                }

                default:
                    break;
            }

            return executed;
        }

        private async Task<bool> HandleGenericChipTaskAsync(GenericChipTaskViewModel genericTask, TaskExecutionRequest request, IReadOnlyList<TaskDescriptor> descriptors, ReaderDevice device)
        {
            var executed = false;
            switch (genericTask.SelectedTaskType)
            {
                case TaskType_GenericChipTask.ChipIsOfType:
                    taskTimeout.Start();
                    switch ((request.TaskHandler.TaskCollection[CurrentTaskIndex] as IGenericTask).CurrentTaskErrorLevel)
                    {
                        case ERROR.Empty:
                            (request.TaskHandler.TaskCollection[CurrentTaskIndex] as GenericChipTaskViewModel).IsFocused = true;

                            if ((request.TaskHandler.TaskCollection[CurrentTaskIndex] as IGenericTask).SelectedExecuteConditionErrorLevel == ERROR.Empty)
                            {
                                request.UpdateReaderBusy?.Invoke(true);
                                await (request.TaskHandler.TaskCollection[CurrentTaskIndex] as GenericChipTaskViewModel).CheckChipType.ExecuteAsync(device.GenericChip);
                                executed = true;
                            }
                            else
                            {
                                if (TryResolveTaskIndex(descriptors, (request.TaskHandler.TaskCollection[CurrentTaskIndex] as IGenericTask).SelectedExecuteConditionTaskIndex, out var targetTaskIndex))
                                {
                                    if ((request.TaskHandler.TaskCollection[targetTaskIndex] as IGenericTask).CurrentTaskErrorLevel == (request.TaskHandler.TaskCollection[CurrentTaskIndex] as IGenericTask).SelectedExecuteConditionErrorLevel)
                                    {
                                        request.UpdateReaderBusy?.Invoke(true);
                                        await (request.TaskHandler.TaskCollection[CurrentTaskIndex] as GenericChipTaskViewModel).CheckChipType.ExecuteAsync(device.GenericChip);
                                        executed = true;
                                    }
                                    else
                                    {
                                        CurrentTaskIndex++;
                                    }
                                }
                            }

                            request.UpdateReaderBusy?.Invoke(false);
                            break;

                        default:
                            (request.TaskHandler.TaskCollection[CurrentTaskIndex] as GenericChipTaskViewModel).IsFocused = false;
                            CurrentTaskIndex++;
                            break;
                    }
                    break;
                case TaskType_GenericChipTask.ChipIsMultiChip:
                    taskTimeout.Start();
                    switch ((request.TaskHandler.TaskCollection[CurrentTaskIndex] as IGenericTask).CurrentTaskErrorLevel)
                    {
                        case ERROR.Empty:
                            (request.TaskHandler.TaskCollection[CurrentTaskIndex] as GenericChipTaskViewModel).IsFocused = true;

                            if ((request.TaskHandler.TaskCollection[CurrentTaskIndex] as IGenericTask).SelectedExecuteConditionErrorLevel == ERROR.Empty)
                            {
                                request.UpdateReaderBusy?.Invoke(true);
                                await (request.TaskHandler.TaskCollection[CurrentTaskIndex] as GenericChipTaskViewModel).CheckChipIsMultiTecChip.ExecuteAsync(device.GenericChip);
                                executed = true;
                            }
                            else
                            {
                                if (TryResolveTaskIndex(descriptors, (request.TaskHandler.TaskCollection[CurrentTaskIndex] as IGenericTask).SelectedExecuteConditionTaskIndex, out var targetTaskIndex))
                                {
                                    if ((request.TaskHandler.TaskCollection[targetTaskIndex] as IGenericTask).CurrentTaskErrorLevel == (request.TaskHandler.TaskCollection[CurrentTaskIndex] as IGenericTask).SelectedExecuteConditionErrorLevel)
                                    {
                                        request.UpdateReaderBusy?.Invoke(true);
                                        await (request.TaskHandler.TaskCollection[CurrentTaskIndex] as GenericChipTaskViewModel).CheckChipIsMultiTecChip.ExecuteAsync(device.GenericChip);
                                        executed = true;
                                    }
                                    else
                                    {
                                        CurrentTaskIndex++;
                                    }
                                }
                            }

                            request.UpdateReaderBusy?.Invoke(false);
                            break;

                        default:
                            (request.TaskHandler.TaskCollection[CurrentTaskIndex] as GenericChipTaskViewModel).IsFocused = false;
                            CurrentTaskIndex++;
                            break;
                    }
                    break;
                default:
                    break;
            }

            return executed;
        }

        private async Task<bool> HandleClassicTaskAsync(MifareClassicSetupViewModel classicTask, TaskExecutionRequest request, IReadOnlyList<TaskDescriptor> descriptors)
        {
            var executed = false;
            var taskModel = request.TaskHandler.TaskCollection[CurrentTaskIndex] as IGenericTask;

            if (taskModel.ExecutionState == TaskExecutionState.NotStarted)
            {
                taskModel.ExecutionState = TaskExecutionState.Running;
                taskTimeout.Start();

                if (taskModel.SelectedExecuteConditionErrorLevel == ERROR.Empty)
                {
                    request.UpdateReaderBusy?.Invoke(true);
                    await (request.TaskHandler.TaskCollection[CurrentTaskIndex] as MifareClassicSetupViewModel).CommandDelegator.ExecuteAsync(classicTask.SelectedTaskType);
                    executed = true;
                }
                else
                {
                    if (TryResolveTaskIndex(descriptors, taskModel.SelectedExecuteConditionTaskIndex, out var targetTaskIndex))
                    {
                        if ((request.TaskHandler.TaskCollection[targetTaskIndex] as IGenericTask).CurrentTaskErrorLevel == taskModel.SelectedExecuteConditionErrorLevel)
                        {
                            request.UpdateReaderBusy?.Invoke(true);
                            await (request.TaskHandler.TaskCollection[CurrentTaskIndex] as MifareClassicSetupViewModel).CommandDelegator.ExecuteAsync(classicTask.SelectedTaskType);
                            executed = true;
                        }
                        else
                        {
                            CurrentTaskIndex++;
                        }
                    }
                }

                request.UpdateReaderBusy?.Invoke(false);
                taskModel.ExecutionState = TaskExecutionState.Completed;
            }
            else if (taskModel.ExecutionState == TaskExecutionState.Completed)
            {
                CurrentTaskIndex++;
                taskTimeout.Stop();
                taskTimeout.Start();
            }

            return executed;
        }

        private async Task<bool> HandleDesfireTaskAsync(MifareDesfireSetupViewModel desfireTask, TaskExecutionRequest request, IReadOnlyList<TaskDescriptor> descriptors)
        {
            var executed = false;
            var taskModel = request.TaskHandler.TaskCollection[CurrentTaskIndex] as IGenericTask;

            if (taskModel.ExecutionState == TaskExecutionState.NotStarted)
            {
                taskModel.ExecutionState = TaskExecutionState.Running;
                taskTimeout.Start();

                if (taskModel.SelectedExecuteConditionErrorLevel == ERROR.Empty)
                {
                    request.UpdateReaderBusy?.Invoke(true);
                    await (request.TaskHandler.TaskCollection[CurrentTaskIndex] as MifareDesfireSetupViewModel).CommandDelegator.ExecuteAsync(desfireTask.SelectedTaskType);
                    executed = true;
                }
                else
                {
                    if (TryResolveTaskIndex(descriptors, taskModel.SelectedExecuteConditionTaskIndex, out var targetTaskIndex))
                    {
                        if ((request.TaskHandler.TaskCollection[targetTaskIndex] as IGenericTask).CurrentTaskErrorLevel == taskModel.SelectedExecuteConditionErrorLevel)
                        {
                            request.UpdateReaderBusy?.Invoke(true);
                            await (request.TaskHandler.TaskCollection[CurrentTaskIndex] as MifareDesfireSetupViewModel).CommandDelegator.ExecuteAsync(desfireTask.SelectedTaskType);
                            executed = true;
                        }
                        else
                        {
                            CurrentTaskIndex++;
                        }
                    }
                }

                request.UpdateReaderBusy?.Invoke(false);
                taskModel.ExecutionState = TaskExecutionState.Completed;
            }
            else if (taskModel.ExecutionState == TaskExecutionState.Completed)
            {
                CurrentTaskIndex++;
                taskTimeout.Stop();
                taskTimeout.Start();
            }

            return executed;
        }
    }
}
