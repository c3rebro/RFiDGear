using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using MVVMDialogs.ViewModels;
using RFiDGear.DataAccessLayer;
using RFiDGear.DataAccessLayer.Tasks;
using RFiDGear.DataAccessLayer.Remote.FromIO;
using RFiDGear.Model;
using RFiDGear.ViewModel.TaskSetupViewModels;
using RFiDGear.ViewModel;

using MVVMDialogs.ViewModels.Interfaces;
namespace RFiDGear.Services.TaskExecution
{
    public interface IReaderDeviceProvider
    {
        ReaderDevice GetInstance();
    }

    public class ReaderDeviceProvider : IReaderDeviceProvider
    {
        public ReaderDevice GetInstance() => ReaderDevice.Instance;
    }

    public interface IDispatcherTimerAdapter : IDisposable
    {
        bool IsEnabled { get; set; }
        object Tag { get; set; }
        void Start();
        void Stop();
    }

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

    public class TaskDescriptor
    {
        public TaskDescriptor(int index, IGenericTaskModel task, Func<CancellationToken, Task> executor = null)
        {
            Index = index;
            Task = task;
            Id = task?.CurrentTaskIndex ?? index.ToString(CultureInfo.CurrentCulture);
            ExecuteAsync = executor;
        }

        public int Index { get; }
        public string Id { get; }
        public IGenericTaskModel Task { get; }
        public Func<CancellationToken, Task> ExecuteAsync { get; }
    }

    public class DeviceDiscoveryResult
    {
        public DeviceDiscoveryResult(ReaderDevice device)
        {
            Device = device;
        }

        public ReaderDevice Device { get; }
    }

    public class ChipHydrationResult
    {
        public ChipHydrationResult(GenericChipModel chip)
        {
            Chip = chip;
        }

        public GenericChipModel Chip { get; }
    }

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

    public interface ITaskExecutionLogger
    {
        void LogInformation(string stage, object details = null);
        void LogError(string stage, Exception exception, object details = null);
    }

    public class NullTaskExecutionLogger : ITaskExecutionLogger
    {
        public void LogInformation(string stage, object details = null)
        {
            Debug.WriteLine($"[TaskExecution] {stage}: {details}");
        }

        public void LogError(string stage, Exception exception, object details = null)
        {
            Debug.WriteLine($"[TaskExecution] {stage} failed: {exception} | {details}");
        }
    }

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
        public EventLog EventLog { get; set; }
        public TaskExecutionTimeouts Timeouts { get; set; } = TaskExecutionTimeouts.Default;
        public IReadOnlyList<TaskDescriptor> TaskDescriptors { get; set; }
    }

    public class TaskExecutionResult
    {
        public ReportReaderWriter ReportReaderWriter { get; set; }
        public string ReportOutputPath { get; set; }
        public string ReportTemplateFile { get; set; }
        public object SelectedSetupViewModel { get; set; }
        public bool RunSelectedOnly { get; set; }
    }

    public class TaskExecutionCompletedEventArgs : EventArgs
    {
        public TaskExecutionCompletedEventArgs(TaskExecutionResult result)
        {
            Result = result;
        }

        public TaskExecutionResult Result { get; }
    }

    public interface ITaskExecutionService
    {
        event EventHandler<TaskExecutionCompletedEventArgs> ExecutionCompleted;
        int CurrentTaskIndex { get; }
        Task<TaskExecutionResult> ExecuteOnceAsync(TaskExecutionRequest request, CancellationToken cancellationToken = default);
        void HandleTaskTimeout();
    }

    public class TaskExecutionService : ITaskExecutionService
    {
        private readonly IReaderDeviceProvider readerDeviceProvider;
        private readonly IDispatcherTimerAdapter triggerReadChip;
        private readonly IDispatcherTimerAdapter taskTimeout;
        private readonly ITaskExecutionLogger logger;
        private TaskExecutionRequest activeRequest;

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

                var deviceResult = await ExecuteStageWithTimeout(
                    "DeviceDiscovery",
                    () => Task.FromResult(new DeviceDiscoveryResult(readerDeviceProvider.GetInstance())),
                    request.Timeouts?.DeviceDiscoveryTimeout,
                    cancellationToken);

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
                        cancellationToken);

                    _ = await ExecuteStageWithTimeout(
                        "SelectionSync",
                        () => Task.FromResult(SynchronizeSelection(request, hydrationResult.Chip)),
                        request.Timeouts?.SelectionSyncTimeout,
                        cancellationToken);

                    await ExecuteStageWithTimeout(
                        "TaskLoop",
                        () => RunTaskLoopAsync(request, result, descriptors, hydrationResult.Chip, device, cancellationToken),
                        request.Timeouts?.TaskLoopTimeout,
                        cancellationToken);
                }
            }
            catch (Exception e)
            {
                logger.LogError("ExecuteOnceAsync", e, new { CurrentTaskIndex });
                request.EventLog?.WriteEntry(e.ToString(), EventLogEntryType.Error);
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
                (activeRequest.TaskHandler.TaskCollection[taskIndex] as IGenericTaskModel).IsTaskCompletedSuccessfully = false;
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
                    var task = request.TaskHandler.TaskCollection[index] as IGenericTaskModel;
                    descriptors.Add(new TaskDescriptor(index, task));
                }
            }

            return descriptors;
        }

        private async Task<T> ExecuteStageWithTimeout<T>(string stageName, Func<Task<T>> stageAction, TimeSpan? timeout, CancellationToken cancellationToken)
        {
            logger.LogInformation(stageName + ".Start", new { CurrentTaskIndex });

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
                logger.LogInformation(stageName + ".Success", new { CurrentTaskIndex });
                return result;
            }
            catch (Exception ex)
            {
                logger.LogError(stageName + ".Failure", ex, new { CurrentTaskIndex });
                throw;
            }
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

        private async Task RunTaskLoopAsync(TaskExecutionRequest request, TaskExecutionResult result, IReadOnlyList<TaskDescriptor> descriptors, GenericChipModel genericChip, ReaderDevice device, CancellationToken cancellationToken)
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

                var descriptor = descriptors[CurrentTaskIndex];
                if (request.TaskHandler?.TaskCollection != null && request.TaskHandler.TaskCollection.Count > CurrentTaskIndex)
                {
                    request.SelectedSetupViewModel = request.TaskHandler.TaskCollection[CurrentTaskIndex];
                }
                else if (descriptor.Task != null)
                {
                    request.SelectedSetupViewModel = descriptor.Task;
                }

                request.UpdateSelectedSetupViewModel?.Invoke(request.SelectedSetupViewModel);

                if (descriptor.ExecuteAsync != null)
                {
                    await descriptor.ExecuteAsync(cancellationToken);
                    if (!request.RunSelectedOnly)
                    {
                        CurrentTaskIndex++;
                    }
                }
                else if (request.TaskHandler?.TaskCollection != null && request.TaskHandler.TaskCollection.Count > CurrentTaskIndex)
                {
                    switch (request.TaskHandler.TaskCollection[CurrentTaskIndex])
                    {
                        case CommonTaskViewModel commonTask:
                            await HandleCommonTaskAsync(commonTask, request, result, descriptors, genericChip, device);
                            break;
                        case GenericChipTaskViewModel genericTask:
                            await HandleGenericChipTaskAsync(genericTask, request, descriptors, device);
                            break;
                        case MifareClassicSetupViewModel classicTask:
                            await HandleClassicTaskAsync(classicTask, request, descriptors);
                            break;
                        case MifareDesfireSetupViewModel desfireTask:
                            await HandleDesfireTaskAsync(desfireTask, request, descriptors);
                            break;
                        case MifareUltralightSetupViewModel _:
                            break;
                        default:
                            break;
                    }
                }

                if (request.RunSelectedOnly)
                {
                    break;
                }

                request.NotifyTreeViewChanged?.Invoke();
            }

            request.NotifyTreeViewChanged?.Invoke();
            taskTimeout.Stop();
        }

        private void ResetSelectionState(TaskExecutionRequest request, TaskExecutionResult result)
        {
            try
            {
                if (request.TreeViewParentNodes != null && request.TreeViewParentNodes.Any(x => x.IsSelected))
                {
                    request.TreeViewParentNodes.First(y => y.IsSelected).IsBeingProgrammed = null;
                    result.RunSelectedOnly = false;
                }
            }
            catch
            {
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

        private async Task HandleCommonTaskAsync(CommonTaskViewModel commonTask, TaskExecutionRequest request, TaskExecutionResult result, IReadOnlyList<TaskDescriptor> descriptors, GenericChipModel genericChip, ReaderDevice device)
        {
            (request.TaskHandler.TaskCollection[CurrentTaskIndex] as CommonTaskViewModel).GenericChip = genericChip;
            (request.TaskHandler.TaskCollection[CurrentTaskIndex] as CommonTaskViewModel).DesfireChip = device.DesfireChip;
            (request.TaskHandler.TaskCollection[CurrentTaskIndex] as CommonTaskViewModel).AvailableTasks = request.TaskHandler.TaskCollection;
            (request.TaskHandler.TaskCollection[CurrentTaskIndex] as CommonTaskViewModel).Args = request.VariablesFromArgs;

            switch (commonTask.SelectedTaskType)
            {
                case TaskType_CommonTask.CreateReport:
                    taskTimeout.Stop();
                    switch ((request.TaskHandler.TaskCollection[CurrentTaskIndex] as IGenericTaskModel).CurrentTaskErrorLevel)
                    {
                        case ERROR.NotReadyError:
                            break;

                        case ERROR.Empty:
                            (request.TaskHandler.TaskCollection[CurrentTaskIndex] as IGenericTaskModel).CurrentTaskErrorLevel = ERROR.NotReadyError;
                            taskTimeout.Start();
                            taskTimeout.Stop();

                            DirectoryInfo reportTargetPathDirInfo;

                            try
                            {
                                var targetReportDir = request.VariablesFromArgs["REPORTTARGETPATH"];
                                var sourceTemplateFile = request.VariablesFromArgs["REPORTTEMPLATEFILE"];
                                reportTargetPathDirInfo = new DirectoryInfo(Path.GetDirectoryName(targetReportDir));
                            }
                            catch
                            {
                                reportTargetPathDirInfo = null;
                            }

                            if ((request.TaskHandler.TaskCollection[CurrentTaskIndex] as IGenericTaskModel).SelectedExecuteConditionErrorLevel == ERROR.Empty)
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
                            }

                            else
                            {
                                if (TryResolveTaskIndex(descriptors, (request.TaskHandler.TaskCollection[CurrentTaskIndex] as IGenericTaskModel).SelectedExecuteConditionTaskIndex, out var targetTaskIndex))
                                {
                                    if ((request.TaskHandler.TaskCollection[targetTaskIndex] as IGenericTaskModel).CurrentTaskErrorLevel == (request.TaskHandler.TaskCollection[CurrentTaskIndex] as IGenericTaskModel).SelectedExecuteConditionErrorLevel)
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
                                    }
                                    else
                                    {
                                        CurrentTaskIndex++;
                                    }
                                }
                            }

                            taskTimeout.Start();
                            break;

                        default:
                            CurrentTaskIndex++;
                            taskTimeout.Stop();
                            taskTimeout.Start();
                            break;
                    }
                    break;

                case TaskType_CommonTask.CheckLogicCondition:
                    switch ((request.TaskHandler.TaskCollection[CurrentTaskIndex] as CommonTaskViewModel).CurrentTaskErrorLevel)
                    {
                        case ERROR.NotReadyError:
                            taskTimeout.Start();
                            break;

                        case ERROR.Empty:
                            (request.TaskHandler.TaskCollection[CurrentTaskIndex] as IGenericTaskModel).CurrentTaskErrorLevel = ERROR.NotReadyError;
                            taskTimeout.Start();

                            if ((request.TaskHandler.TaskCollection[CurrentTaskIndex] as CommonTaskViewModel).SelectedExecuteConditionErrorLevel == ERROR.Empty)
                            {
                                await (request.TaskHandler.TaskCollection[CurrentTaskIndex] as CommonTaskViewModel).CheckLogicCondition.ExecuteAsync(request.TaskHandler.TaskCollection);
                            }
                            else
                            {
                                if (TryResolveTaskIndex(descriptors, (request.TaskHandler.TaskCollection[CurrentTaskIndex] as IGenericTaskModel).SelectedExecuteConditionTaskIndex, out var targetTaskIndex))
                                {
                                    if ((request.TaskHandler.TaskCollection[targetTaskIndex] as IGenericTaskModel).CurrentTaskErrorLevel == (request.TaskHandler.TaskCollection[CurrentTaskIndex] as IGenericTaskModel).SelectedExecuteConditionErrorLevel)
                                    {
                                        await (request.TaskHandler.TaskCollection[CurrentTaskIndex] as CommonTaskViewModel).CheckLogicCondition.ExecuteAsync(request.TaskHandler.TaskCollection);
                                    }
                                    else
                                    {
                                        CurrentTaskIndex++;
                                    }
                                }
                            }
                            break;

                        default:
                            CurrentTaskIndex++;
                            taskTimeout.Stop();
                            taskTimeout.Start();
                            break;
                    }
                    break;

                case TaskType_CommonTask.ExecuteProgram:
                    switch ((request.TaskHandler.TaskCollection[CurrentTaskIndex] as CommonTaskViewModel).CurrentTaskErrorLevel)
                    {
                        case ERROR.NotReadyError:
                            taskTimeout.Start();
                            break;

                        case ERROR.Empty:
                            (request.TaskHandler.TaskCollection[CurrentTaskIndex] as IGenericTaskModel).CurrentTaskErrorLevel = ERROR.NotReadyError;
                            taskTimeout.Start();

                            if ((request.TaskHandler.TaskCollection[CurrentTaskIndex] as CommonTaskViewModel).SelectedExecuteConditionErrorLevel == ERROR.Empty)
                            {
                                (request.TaskHandler.TaskCollection[CurrentTaskIndex] as CommonTaskViewModel).ExecuteProgramCommand.Execute(null);
                            }
                            else
                            {
                                if (TryResolveTaskIndex(descriptors, (request.TaskHandler.TaskCollection[CurrentTaskIndex] as IGenericTaskModel).SelectedExecuteConditionTaskIndex, out var targetTaskIndex))
                                {
                                    if ((request.TaskHandler.TaskCollection[targetTaskIndex] as IGenericTaskModel).CurrentTaskErrorLevel == (request.TaskHandler.TaskCollection[CurrentTaskIndex] as IGenericTaskModel).SelectedExecuteConditionErrorLevel)
                                    {
                                        (request.TaskHandler.TaskCollection[CurrentTaskIndex] as CommonTaskViewModel).ExecuteProgramCommand.Execute(null);
                                    }
                                    else
                                    {
                                        CurrentTaskIndex++;
                                    }
                                }
                            }
                            break;

                        default:
                            CurrentTaskIndex++;
                            taskTimeout.Stop();
                            taskTimeout.Start();
                            break;
                    }
                    break;

                default:
                    break;
            }
        }

        private async Task HandleGenericChipTaskAsync(GenericChipTaskViewModel genericTask, TaskExecutionRequest request, IReadOnlyList<TaskDescriptor> descriptors, ReaderDevice device)
        {
            switch (genericTask.SelectedTaskType)
            {
                case TaskType_GenericChipTask.ChipIsOfType:
                    taskTimeout.Start();
                    switch ((request.TaskHandler.TaskCollection[CurrentTaskIndex] as IGenericTaskModel).CurrentTaskErrorLevel)
                    {
                        case ERROR.Empty:
                            (request.TaskHandler.TaskCollection[CurrentTaskIndex] as GenericChipTaskViewModel).IsFocused = true;

                            if ((request.TaskHandler.TaskCollection[CurrentTaskIndex] as IGenericTaskModel).SelectedExecuteConditionErrorLevel == ERROR.Empty)
                            {
                                request.UpdateReaderBusy?.Invoke(true);
                                await (request.TaskHandler.TaskCollection[CurrentTaskIndex] as GenericChipTaskViewModel).CheckChipType.ExecuteAsync(device.GenericChip);
                            }
                            else
                            {
                                if (TryResolveTaskIndex(descriptors, (request.TaskHandler.TaskCollection[CurrentTaskIndex] as IGenericTaskModel).SelectedExecuteConditionTaskIndex, out var targetTaskIndex))
                                {
                                    if ((request.TaskHandler.TaskCollection[targetTaskIndex] as IGenericTaskModel).CurrentTaskErrorLevel == (request.TaskHandler.TaskCollection[CurrentTaskIndex] as IGenericTaskModel).SelectedExecuteConditionErrorLevel)
                                    {
                                        request.UpdateReaderBusy?.Invoke(true);
                                        await (request.TaskHandler.TaskCollection[CurrentTaskIndex] as GenericChipTaskViewModel).CheckChipType.ExecuteAsync(device.GenericChip);
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
                    switch ((request.TaskHandler.TaskCollection[CurrentTaskIndex] as IGenericTaskModel).CurrentTaskErrorLevel)
                    {
                        case ERROR.Empty:
                            (request.TaskHandler.TaskCollection[CurrentTaskIndex] as GenericChipTaskViewModel).IsFocused = true;

                            if ((request.TaskHandler.TaskCollection[CurrentTaskIndex] as IGenericTaskModel).SelectedExecuteConditionErrorLevel == ERROR.Empty)
                            {
                                request.UpdateReaderBusy?.Invoke(true);
                                await (request.TaskHandler.TaskCollection[CurrentTaskIndex] as GenericChipTaskViewModel).CheckChipIsMultiTecChip.ExecuteAsync(device.GenericChip);
                            }
                            else
                            {
                                if (TryResolveTaskIndex(descriptors, (request.TaskHandler.TaskCollection[CurrentTaskIndex] as IGenericTaskModel).SelectedExecuteConditionTaskIndex, out var targetTaskIndex))
                                {
                                    if ((request.TaskHandler.TaskCollection[targetTaskIndex] as IGenericTaskModel).CurrentTaskErrorLevel == (request.TaskHandler.TaskCollection[CurrentTaskIndex] as IGenericTaskModel).SelectedExecuteConditionErrorLevel)
                                    {
                                        request.UpdateReaderBusy?.Invoke(true);
                                        await (request.TaskHandler.TaskCollection[CurrentTaskIndex] as GenericChipTaskViewModel).CheckChipIsMultiTecChip.ExecuteAsync(device.GenericChip);
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
        }

        private async Task HandleClassicTaskAsync(MifareClassicSetupViewModel classicTask, TaskExecutionRequest request, IReadOnlyList<TaskDescriptor> descriptors)
        {
            switch ((request.TaskHandler.TaskCollection[CurrentTaskIndex] as IGenericTaskModel).CurrentTaskErrorLevel)
            {
                case ERROR.NotReadyError:
                    taskTimeout.Start();
                    break;

                case ERROR.Empty:
                    (request.TaskHandler.TaskCollection[CurrentTaskIndex] as IGenericTaskModel).CurrentTaskErrorLevel = ERROR.NotReadyError;
                    taskTimeout.Start();

                    if ((request.TaskHandler.TaskCollection[CurrentTaskIndex] as IGenericTaskModel).SelectedExecuteConditionErrorLevel == ERROR.Empty)
                    {
                        request.UpdateReaderBusy?.Invoke(true);
                        await (request.TaskHandler.TaskCollection[CurrentTaskIndex] as MifareClassicSetupViewModel).CommandDelegator.ExecuteAsync(classicTask.SelectedTaskType);
                    }

                    else
                    {
                        if (TryResolveTaskIndex(descriptors, (request.TaskHandler.TaskCollection[CurrentTaskIndex] as IGenericTaskModel).SelectedExecuteConditionTaskIndex, out var targetTaskIndex))
                        {
                            if ((request.TaskHandler.TaskCollection[targetTaskIndex] as IGenericTaskModel).CurrentTaskErrorLevel == (request.TaskHandler.TaskCollection[CurrentTaskIndex] as IGenericTaskModel).SelectedExecuteConditionErrorLevel)
                            {
                                request.UpdateReaderBusy?.Invoke(true);
                                await (request.TaskHandler.TaskCollection[CurrentTaskIndex] as MifareClassicSetupViewModel).CommandDelegator.ExecuteAsync(classicTask.SelectedTaskType);
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
                    CurrentTaskIndex++;
                    taskTimeout.Stop();
                    taskTimeout.Start();
                    break;
            }
        }

        private async Task HandleDesfireTaskAsync(MifareDesfireSetupViewModel desfireTask, TaskExecutionRequest request, IReadOnlyList<TaskDescriptor> descriptors)
        {
            switch ((request.TaskHandler.TaskCollection[CurrentTaskIndex] as IGenericTaskModel).CurrentTaskErrorLevel)
            {
                case ERROR.NotReadyError:
                    taskTimeout.Start();
                    break;

                case ERROR.Empty:
                    (request.TaskHandler.TaskCollection[CurrentTaskIndex] as IGenericTaskModel).CurrentTaskErrorLevel = ERROR.NotReadyError;
                    taskTimeout.Start();

                    if ((request.TaskHandler.TaskCollection[CurrentTaskIndex] as IGenericTaskModel).SelectedExecuteConditionErrorLevel == ERROR.Empty)
                    {
                        request.UpdateReaderBusy?.Invoke(true);
                        await (request.TaskHandler.TaskCollection[CurrentTaskIndex] as MifareDesfireSetupViewModel).CommandDelegator.ExecuteAsync(desfireTask.SelectedTaskType);
                    }

                    else
                    {
                        if (TryResolveTaskIndex(descriptors, (request.TaskHandler.TaskCollection[CurrentTaskIndex] as IGenericTaskModel).SelectedExecuteConditionTaskIndex, out var targetTaskIndex))
                        {
                            if ((request.TaskHandler.TaskCollection[targetTaskIndex] as IGenericTaskModel).CurrentTaskErrorLevel == (request.TaskHandler.TaskCollection[CurrentTaskIndex] as IGenericTaskModel).SelectedExecuteConditionErrorLevel)
                            {
                                request.UpdateReaderBusy?.Invoke(true);
                                await (request.TaskHandler.TaskCollection[CurrentTaskIndex] as MifareDesfireSetupViewModel).CommandDelegator.ExecuteAsync(desfireTask.SelectedTaskType);
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
                    CurrentTaskIndex++;
                    taskTimeout.Stop();
                    taskTimeout.Start();
                    break;
            }
        }
    }
}
