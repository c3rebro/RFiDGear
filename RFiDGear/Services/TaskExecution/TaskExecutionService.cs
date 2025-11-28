using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using MVVMDialogs.ViewModels;
using RFiDGear.DataAccessLayer;
using RFiDGear.DataAccessLayer.Remote.FromIO;
using RFiDGear.Model;
using RFiDGear.ViewModel.TaskSetupViewModels;

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

    public interface IDispatcherTimerAdapter
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
    }

    public class TaskExecutionRequest
    {
        public ChipTaskHandlerModel TaskHandler { get; init; }
        public ObservableCollection<RFiDChipParentLayerViewModel> TreeViewParentNodes { get; init; }
        public Dictionary<string, string> VariablesFromArgs { get; init; }
        public ObservableCollection<IDialogViewModel> Dialogs { get; init; }
        public ReportReaderWriter ReportReaderWriter { get; set; }
        public string ReportOutputPath { get; set; }
        public string ReportTemplateFile { get; set; }
        public object SelectedSetupViewModel { get; set; }
        public bool RunSelectedOnly { get; set; }
        public Action<object> UpdateSelectedSetupViewModel { get; init; }
        public Action<bool> UpdateReaderBusy { get; init; }
        public Action NotifyTreeViewChanged { get; init; }
        public Action NotifyTasksChanged { get; init; }
        public EventLog EventLog { get; init; }
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
        Task<TaskExecutionResult> ExecuteOnceAsync(TaskExecutionRequest request);
        void HandleTaskTimeout();
    }

    public class TaskExecutionService : ITaskExecutionService
    {
        private readonly IReaderDeviceProvider readerDeviceProvider;
        private readonly IDispatcherTimerAdapter triggerReadChip;
        private readonly IDispatcherTimerAdapter taskTimeout;
        private TaskExecutionRequest activeRequest;

        public TaskExecutionService(
            IReaderDeviceProvider readerDeviceProvider,
            IDispatcherTimerAdapter triggerReadChip,
            IDispatcherTimerAdapter taskTimeout)
        {
            this.readerDeviceProvider = readerDeviceProvider;
            this.triggerReadChip = triggerReadChip;
            this.taskTimeout = taskTimeout;
        }

        public event EventHandler<TaskExecutionCompletedEventArgs> ExecutionCompleted;

        public int CurrentTaskIndex { get; private set; }

        public async Task<TaskExecutionResult> ExecuteOnceAsync(TaskExecutionRequest request)
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

            var taskDictionary = new Dictionary<string, int>();

            foreach (var rfidTaskObject in request.TaskHandler.TaskCollection)
            {
                taskDictionary.Add((rfidTaskObject as IGenericTaskModel).CurrentTaskIndex, request.TaskHandler.TaskCollection.IndexOf(rfidTaskObject));
            }

#if DEBUG
            taskTimeout.IsEnabled = false;
#else
            taskTimeout.IsEnabled = true;
            taskTimeout.Start();
#endif
            triggerReadChip.Tag = triggerReadChip.IsEnabled;
            triggerReadChip.IsEnabled = false;

            try
            {
                using (var device = readerDeviceProvider.GetInstance())
                {
                    var genericChip = ReaderDevice.Instance.GenericChip;

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

                    if (request.TreeViewParentNodes.Any(x => x.IsSelected))
                    {
                        request.TreeViewParentNodes.First(x => x.IsSelected).IsSelected = false;
                    }

                    if (!string.IsNullOrWhiteSpace(genericChip.UID) && request.TreeViewParentNodes.Any(x => x.UID == genericChip.UID))
                    {
                        request.TreeViewParentNodes.First(x => x.UID == genericChip.UID).IsSelected = true;
                        request.TreeViewParentNodes.First(x => x.IsSelected).IsBeingProgrammed = true;
                    }

                    while (CurrentTaskIndex < request.TaskHandler.TaskCollection.Count)
                    {
                        await Task.Delay(50);

                        if (request.RunSelectedOnly)
                        {
                            CurrentTaskIndex = request.TaskHandler.TaskCollection.IndexOf(request.SelectedSetupViewModel);
                        }

                        taskTimeout.Stop();
                        taskTimeout.Start();
                        taskTimeout.IsEnabled = true;
                        taskTimeout.Tag = CurrentTaskIndex;

                        request.SelectedSetupViewModel = request.TaskHandler.TaskCollection[CurrentTaskIndex];
                        request.UpdateSelectedSetupViewModel?.Invoke(request.SelectedSetupViewModel);

                        switch (request.TaskHandler.TaskCollection[CurrentTaskIndex])
                        {
                            case CommonTaskViewModel commonTask:
                                await HandleCommonTaskAsync(commonTask, request, result, taskDictionary, genericChip, device);
                                break;
                            case GenericChipTaskViewModel genericTask:
                                await HandleGenericChipTaskAsync(genericTask, request, taskDictionary, device);
                                break;
                            case MifareClassicSetupViewModel classicTask:
                                await HandleClassicTaskAsync(classicTask, request, taskDictionary);
                                break;
                            case MifareDesfireSetupViewModel desfireTask:
                                await HandleDesfireTaskAsync(desfireTask, request, taskDictionary);
                                break;
                            case MifareUltralightSetupViewModel _:
                                break;
                            default:
                                break;
                        }

                        if (request.RunSelectedOnly)
                        {
                            break;
                        }

                        request.NotifyTreeViewChanged?.Invoke();
                    }
                }

                request.NotifyTreeViewChanged?.Invoke();
                taskTimeout.Stop();
            }
            catch (Exception e)
            {
                request.EventLog?.WriteEntry(e.Message, EventLogEntryType.Error);
            }

            try
            {
                if (request.TreeViewParentNodes.Any(x => x.IsSelected))
                {
                    request.TreeViewParentNodes.First(y => y.IsSelected).IsBeingProgrammed = null;
                    triggerReadChip.IsEnabled = (bool)triggerReadChip.Tag;
                    result.RunSelectedOnly = false;
                }
            }
            catch
            {
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

        private async Task HandleCommonTaskAsync(CommonTaskViewModel commonTask, TaskExecutionRequest request, TaskExecutionResult result, Dictionary<string, int> taskDictionary, GenericChipModel genericChip, ReaderDevice device)
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
                                if (taskDictionary.TryGetValue((request.TaskHandler.TaskCollection[CurrentTaskIndex] as IGenericTaskModel).SelectedExecuteConditionTaskIndex, out var targetTaskIndex))
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
                                if (taskDictionary.TryGetValue((request.TaskHandler.TaskCollection[CurrentTaskIndex] as IGenericTaskModel).SelectedExecuteConditionTaskIndex, out var targetTaskIndex))
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
                                if (taskDictionary.TryGetValue((request.TaskHandler.TaskCollection[CurrentTaskIndex] as IGenericTaskModel).SelectedExecuteConditionTaskIndex, out var targetTaskIndex))
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

        private async Task HandleGenericChipTaskAsync(GenericChipTaskViewModel genericTask, TaskExecutionRequest request, Dictionary<string, int> taskDictionary, ReaderDevice device)
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
                                if (taskDictionary.TryGetValue((request.TaskHandler.TaskCollection[CurrentTaskIndex] as IGenericTaskModel).SelectedExecuteConditionTaskIndex, out var targetTaskIndex))
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
                                if (taskDictionary.TryGetValue((request.TaskHandler.TaskCollection[CurrentTaskIndex] as IGenericTaskModel).SelectedExecuteConditionTaskIndex, out var targetTaskIndex))
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

        private async Task HandleClassicTaskAsync(MifareClassicSetupViewModel classicTask, TaskExecutionRequest request, Dictionary<string, int> taskDictionary)
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
                        if (taskDictionary.TryGetValue((request.TaskHandler.TaskCollection[CurrentTaskIndex] as IGenericTaskModel).SelectedExecuteConditionTaskIndex, out var targetTaskIndex))
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

        private async Task HandleDesfireTaskAsync(MifareDesfireSetupViewModel desfireTask, TaskExecutionRequest request, Dictionary<string, int> taskDictionary)
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
                        if (taskDictionary.TryGetValue((request.TaskHandler.TaskCollection[CurrentTaskIndex] as IGenericTaskModel).SelectedExecuteConditionTaskIndex, out var targetTaskIndex))
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
