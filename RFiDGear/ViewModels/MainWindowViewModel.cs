/// <summary>
/// Main application view model that orchestrates startup, UI binding, reader monitoring,
/// and task execution lifecycles. Instances are exported via MEF under the name
/// <c>"MainWin"</c> so XAML bindings can resolve this view model from the shared locator.
/// </summary>

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using RFiDGear.Infrastructure.DI;

using RedCell.Diagnostics.Update;
using RFiDGear.Models;
using RFiDGear.Services;
using RFiDGear.Services.TaskExecution;
using RFiDGear.ViewModel.DialogFactories;
using RFiDGear.ViewModel.TaskSetupViewModels;
using RFiDGear.Services.Interfaces;
using RFiDGear.Services.Factories;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.Tasks;
using RFiDGear.Infrastructure.ReaderProviders;
using RFiDGear.Infrastructure.FileAccess;
using RFiDGear.UI.MVVMDialogs.ViewModels;
using RFiDGear.UI.MVVMDialogs.ViewModels.Interfaces;

namespace RFiDGear.ViewModel
{
    /// <summary>
    /// Description of MainWindowViewModel.
    /// </summary>
    [ExportViewModel("MainWin")]
    public class MainWindowViewModel : ObservableObject
    {
        private readonly Version Version = Assembly.GetExecutingAssembly().GetName().Version;
#nullable enable
        private readonly EventLog? eventLog;
#nullable disable
        private readonly string[] args;
        private readonly Dictionary<string, string> variablesFromArgs = new Dictionary<string, string>();
        private readonly Updater updater;
        private TaskDialogFactory taskDialogFactory;
        private ITaskExecutionService taskExecutionService;
        private readonly ISettingsBootstrapper settingsBootstrapper;
        private readonly IUpdateNotifier updateNotifier;
        private readonly IContextMenuBuilder contextMenuBuilder;
        private readonly IStartupArgumentProcessor startupArgumentProcessor;
        private readonly IUpdateScheduler updateScheduler;
        private readonly IReaderMonitor readerMonitor;
        private readonly IProjectBootstrapper projectBootstrapper;
        private readonly ITimerFactory timerFactory;
        private readonly ITaskServiceInitializer taskServiceInitializer;
        private readonly IMenuInitializer menuInitializer;

        private protected MainWindow mw;
        private protected DatabaseReaderWriter databaseReaderWriter;
        private protected ReportReaderWriter reportReaderWriter;
        private protected DispatcherTimer triggerReadChip;
        private protected DispatcherTimer taskTimeout;
        private protected string reportOutputPath;
        private protected string reportTemplateFile;
        private protected ChipTaskHandlerModel taskHandler;
        private protected List<MifareClassicChipModel> mifareClassicUidModels = new List<MifareClassicChipModel>();
        private protected List<MifareDesfireChipModel> mifareDesfireViewModels = new List<MifareDesfireChipModel>();
        private protected bool _runSelectedOnly;

        // set if task was completed; indicates greenlight to continue execution
        // if programming takes too long; quit the process
        private bool userIsNotifiedForAvailableUpdate = false;
        private protected Mutex mutex;
        private Task initializationTask;

        // one reader, one instance - only

        #region Constructors

        public MainWindowViewModel()
            : this(
                  new SettingsBootstrapper(),
                  new UpdateNotifier(),
                  new ContextMenuBuilder(),
                  new AppStartupInitializer(),
                  new MainWindowTimerFactory(),
                  new TaskServiceInitializer(),
                  new MenuInitializer(),
                  new StartupArgumentProcessor(),
                  new UpdateScheduler(),
                  new ReaderMonitor(),
                  new ProjectBootstrapper())
        {
        }

        public MainWindowViewModel(
            ISettingsBootstrapper settingsBootstrapper,
            IUpdateNotifier updateNotifier,
            IContextMenuBuilder contextMenuBuilder,
            IAppStartupInitializer appStartupInitializer,
            ITimerFactory timerFactory,
            ITaskServiceInitializer taskServiceInitializer,
            IMenuInitializer menuInitializer,
            IStartupArgumentProcessor startupArgumentProcessor,
            IUpdateScheduler updateScheduler,
            IReaderMonitor readerMonitor,
            IProjectBootstrapper projectBootstrapper)
        {
            this.settingsBootstrapper = settingsBootstrapper ?? throw new ArgumentNullException(nameof(settingsBootstrapper));
            this.updateNotifier = updateNotifier ?? throw new ArgumentNullException(nameof(updateNotifier));
            this.contextMenuBuilder = contextMenuBuilder ?? throw new ArgumentNullException(nameof(contextMenuBuilder));
            this.timerFactory = timerFactory ?? throw new ArgumentNullException(nameof(timerFactory));
            this.taskServiceInitializer = taskServiceInitializer ?? throw new ArgumentNullException(nameof(taskServiceInitializer));
            this.menuInitializer = menuInitializer ?? throw new ArgumentNullException(nameof(menuInitializer));
            this.startupArgumentProcessor = startupArgumentProcessor ?? throw new ArgumentNullException(nameof(startupArgumentProcessor));
            this.updateScheduler = updateScheduler ?? throw new ArgumentNullException(nameof(updateScheduler));
            this.readerMonitor = readerMonitor ?? throw new ArgumentNullException(nameof(readerMonitor));
            this.projectBootstrapper = projectBootstrapper ?? throw new ArgumentNullException(nameof(projectBootstrapper));

            if (appStartupInitializer == null)
            {
                throw new ArgumentNullException(nameof(appStartupInitializer));
            }

            IsReaderBusy = false;

            var startupContext = appStartupInitializer.Initialize();
            eventLog = startupContext.EventLog;
            mutex = startupContext.Mutex;
            args = startupContext.Arguments;

            updater = new Updater();
        }

        /// <summary>
        /// Starts asynchronous initialization, ensuring the sequence only runs once per
        /// application lifetime even if invoked multiple times by the view layer.
        /// </summary>
        /// <returns>A task that completes when startup dependencies and timers are ready.</returns>
        public Task InitializeAsync()
        {
            initializationTask ??= InitializeInternalAsync();
            return initializationTask;
        }

        /// <summary>
        /// Performs the full startup sequence: loads persisted settings, configures reader
        /// timers, initializes task services, and wires UI collections for data binding.
        /// </summary>
        private async Task InitializeInternalAsync()
        {
            var bootstrapResult = await settingsBootstrapper.LoadAsync();

            CurrentReader = bootstrapResult.CurrentReaderName;
            ReaderDevice.Reader = bootstrapResult.DefaultReaderProvider;
            culture = bootstrapResult.Culture;

            var timerInitialization = timerFactory.CreateTimers(UpdateChip, TaskTimeout);
            triggerReadChip = timerInitialization.TriggerReadTimer;
            taskTimeout = timerInitialization.TaskTimeoutTimer;

            TreeViewParentNodes = new ObservableCollection<RFiDChipParentLayerViewModel>();

            ChipTasks = new ChipTaskHandlerModel();

            ReaderStatus = "";
            DateTimeStatusBar = "";
            databaseReaderWriter = new DatabaseReaderWriter();
            resLoader = new ResourceLoader();

            var taskServices = taskServiceInitializer.Initialize(
                () => OnPropertyChanged(nameof(ChipTasks)),
                () => mw?.Activate(),
                status => IsReaderBusy = status,
                TaskExecutionService_ExecutionCompleted,
                triggerReadChip,
                taskTimeout);

            taskDialogFactory = taskServices.TaskDialogFactory;
            taskExecutionService = taskServices.TaskExecutionService;

            var deleteSelectedCommand = new RelayCommand(() =>
            {
                taskHandler.TaskCollection.Remove(SelectedSetupViewModel);
            });

            var menuInitialization = menuInitializer.Initialize(
                contextMenuBuilder,
                GetAddEditCommand,
                deleteSelectedCommand,
                WriteSelectedTaskToChipOnceCommand,
                ResetSelectedTaskStatusCommand,
                WriteToChipOnceCommand,
                ResetReportTaskDirectoryCommand,
                ReadChipCommand);

            rowContextMenuItems = menuInitialization.RowContextMenuItems;
            emptySpaceContextMenuItems = menuInitialization.EmptySpaceContextMenuItems;
            emptySpaceTreeViewContextMenu = menuInitialization.EmptySpaceTreeViewContextMenu;

            var mainWindow = Application.Current?.MainWindow;
            if (mainWindow != null)
            {
                mainWindow.Closing += new CancelEventHandler(CloseThreads);
                mainWindow.Activated += new EventHandler(LoadCompleted);

                if (mainWindow.IsActive)
                {
                    LoadCompleted(mainWindow, EventArgs.Empty);
                }
            }
        }

        #endregion Constructors

        #region Dialogs

        /// <summary>
        /// 
        /// </summary>
        private protected ObservableCollection<IDialogViewModel> dialogs = new ObservableCollection<IDialogViewModel>();
        public ObservableCollection<IDialogViewModel> Dialogs => dialogs;

        #endregion Dialogs

        #region Localization
        [ExportViewModel("Culture")]
        private protected CultureInfo culture;

        private protected ResourceLoader resLoader;

        /// <summary>
        /// Expose translated strings from ResourceLoader
        /// </summary>
        public string LocalizationResourceSet
        {
            get; set;
        }

        #endregion Localization

        #region Local Commands

        /// <summary>
        /// 
        /// </summary>
        public IAsyncRelayCommand GetAddEditCommand => new AsyncRelayCommand(OnNewGetAddEditCommand);
        private Task OnNewGetAddEditCommand()
        {
            switch (selectedSetupViewModel)
            {
                case CommonTaskViewModel _:
                    OnNewNewCreateReportTaskCommand();
                    break;
                case GenericChipTaskViewModel _:
                    OnNewCreateGenericChipTaskCommand();
                    break;
                case MifareClassicSetupViewModel _:
                    OnNewCreateClassicTaskCommand();
                    break;
                case MifareDesfireSetupViewModel _:
                    OnNewCreateDesfireTaskCommand();
                    break;
                case MifareUltralightSetupViewModel _:
                    OnNewCreateUltralightTaskCommand();
                    break;
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private async Task OpenLastProjectFile()
        {
            await OpenLastProjectFile(string.Empty);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="projectFileToUse"></param>
        /// <returns></returns>
        private async Task OpenLastProjectFile(string projectFileToUse)
        {
            using (var settings = new SettingsReaderWriter())
            {
                var autoLoadLastUsedDB = settings.DefaultSpecification.AutoLoadProjectOnStart;
                string lastUsedDBPath;

                if (string.IsNullOrEmpty(projectFileToUse))
                {
                    lastUsedDBPath = settings.DefaultSpecification.LastUsedProjectPath;
                }
                else
                {
                    lastUsedDBPath = projectFileToUse;
                }

                culture = (settings.DefaultSpecification.DefaultLanguage == "german") ? new CultureInfo("de-DE") : new CultureInfo("en-US");

                if (ChipTasks.TaskCollection != null && ChipTasks.TaskCollection.Count > 0)
                {
                    ChipTasks.TaskCollection.Clear();
                }

                await databaseReaderWriter.ReadDatabase(lastUsedDBPath);

                foreach (var vm in databaseReaderWriter.TreeViewModel)
                {
                    TreeViewParentNodes.Add(vm);
                }


                foreach (var setup in databaseReaderWriter.SetupModel.TaskCollection)
                {
                    ChipTasks.TaskCollection.Add(setup);
                }

                if (Dialogs.Any())
                {
                    Dialogs.RemoveAt(0);
                }

                OnPropertyChanged(nameof(ChipTasks));
            }
        }
        #endregion

        #region Menu Commands

        /// <summary>
        /// Here we perform all tasks on cards with a periodic check for new cards to work with.
        /// Added to Timer.Tick Event @ "triggerReadChip.Tick += UpdateChip;"
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void UpdateChip(object sender, EventArgs args)
        {
            GenericChipModel GenericChip;
            Mouse.OverrideCursor = Cursors.AppStarting;

            try
            {

                //try to get singleton instance
                using (var device = ReaderDevice.Instance)
                {
                    //reader was ready - proceed
                    if (device != null)
                    {
                        IsReaderBusy = true;
                        await device.ReadChipPublic();

                        GenericChip = device.GenericChip;

                        IsReaderBusy = false; ;
                    }
                    else
                    {
                        return;
                    }
                }
                //proceed to create dummy only when uid is yet unknown
                if (!string.IsNullOrWhiteSpace(GenericChip.UID) &&
                    !treeViewParentNodes.Any(x => (x.UID == GenericChip.UID)))
                {
                    foreach (var item in treeViewParentNodes)
                    {
                        item.IsExpanded = false;
                    }

                    // fill treeview with dummy models and viewmodels
                    await ReadChipCommand.ExecuteAsync(null);
                    await ResetTaskStatusCommand.ExecuteAsync(null);
                    await WriteToChipOnceCommand.ExecuteAsync(null);
                }

            }
            catch (Exception e)
            {
                eventLog?.WriteEntry(e.Message, EventLogEntryType.Error);
            }

            Mouse.OverrideCursor = null;
        }

        /// <summary>
        /// What to do if timer has ended without success i.e. ErrorLevel != ElatecError.NoError ?
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TaskTimeout(object sender, EventArgs e)
        {
            taskExecutionService?.HandleTaskTimeout();
        }

        /// <summary>
        /// "Remove all listed Chips from listing" was called
        /// </summary>
        public ICommand RemoveChipsFromTreeCommand => new RelayCommand(OnNewRemoveChipsFromTreeCommand);
        private void OnNewRemoveChipsFromTreeCommand()
        {
            TreeViewParentNodes.Clear();
        }

        /// <summary>
        /// Show Detailed Version Info
        /// </summary>
        public IAsyncRelayCommand ShowChangeLogCommand => new AsyncRelayCommand(OnNewShowChangeLogCommand);
        private async Task OnNewShowChangeLogCommand()
        {
            await AskForUpdateNow(true);
        }

        /// <summary>
        /// Show Detailed Version Info
        /// </summary>
        public ICommand NewAboutDialogCommand => new RelayCommand(OnNewNewAboutDialogCommand);
        private void OnNewNewAboutDialogCommand()
        {
            Dialogs.Add(new AboutViewModel()
            {
                Caption = ResourceLoader.GetResource("windowCaptionAboutRFiDGear"),
                AboutText = string.Format("RFiDGear {0}.{1}.{2} {3}\n\n", Version.Major, Version.Minor, Version.Build, Constants.TITLE_SUFFIX)
                + ResourceLoader.GetResource("textBoxTextAboutRFiDGear"),

                OnOk = (sender) =>
                {
                    sender.Close();
                },

                OnCloseRequest = (sender) =>
                {
                    sender.Close();
                    mw.Activate();
                }
            });
        }

        /// <summary>
        /// Create a new "Common" Task of Type "Report Creator"
        /// </summary>
        public IAsyncRelayCommand CreateGenericChipTaskCommand => new AsyncRelayCommand(OnNewCreateGenericChipTaskCommand);
        private Task OnNewCreateGenericChipTaskCommand()
        {

            var timerState = triggerReadChip.IsEnabled;
            triggerReadChip.IsEnabled = false;
            Mouse.OverrideCursor = Cursors.AppStarting;

            try
            {
                using (var device = ReaderDevice.Instance)
                {
                    // only call dialog if device is ready
                    if (device != null)
                    {
                        Dialogs.Add(taskDialogFactory.CreateGenericChipTaskDialog(SelectedSetupViewModel, ChipTasks, dialogs));
                    }

                    else
                    {
                        OnNewNoReaderFoundDialog();
                    }
                }
            }
            catch (Exception e)
            {
                eventLog?.WriteEntry(e.Message, EventLogEntryType.Error);

                dialogs.Clear();

            }

            Mouse.OverrideCursor = null;

            triggerReadChip.IsEnabled = timerState;

            OnPropertyChanged(nameof(ChipTasks));

            return Task.CompletedTask;
        }

        /// <summary>
        /// Create a new "Common" Task of Type "Report Creator"
        /// </summary>
        public IAsyncRelayCommand CreateGenericTaskCommand => new AsyncRelayCommand(OnNewNewCreateReportTaskCommand);
        private Task OnNewNewCreateReportTaskCommand()
        {


            var timerState = triggerReadChip.IsEnabled;

            triggerReadChip.IsEnabled = false;

            Mouse.OverrideCursor = Cursors.AppStarting;

            try
            {
                dialogs.Add(new CommonTaskViewModel(SelectedSetupViewModel, ChipTasks.TaskCollection, dialogs)
                {
                    Caption = ResourceLoader.GetResource("windowCaptionAddEditGenericTask"),

                    OnOk = (sender) =>
                    {

                        if (sender.SelectedTaskType == TaskType_CommonTask.CreateReport ||
                            sender.SelectedTaskType == TaskType_CommonTask.CheckLogicCondition ||
                            sender.SelectedTaskType == TaskType_CommonTask.ExecuteProgram)
                        {
                            if (!TryValidateTaskIndices(sender.CurrentTaskIndex, sender.SelectedExecuteConditionTaskIndex, sender.SelectedExecuteConditionErrorLevel))
                            {
                                return;
                            }

                            if (ChipTasks.TaskCollection.OfType<CommonTaskViewModel>().Where(x => (x as CommonTaskViewModel).SelectedTaskIndexAsInt == sender.SelectedTaskIndexAsInt).Any())
                            {
                                ChipTasks.TaskCollection.RemoveAt(ChipTasks.TaskCollection.IndexOf(SelectedSetupViewModel));
                            }

                            ChipTasks.TaskCollection.Add(sender);

                            ChipTasks.TaskCollection = new ObservableCollection<object>(ChipTasks.TaskCollection.OrderBy(x => (x as IGenericTaskModel).SelectedTaskIndexAsInt));

                            OnPropertyChanged(nameof(ChipTasks));
                        }
                        sender.Close();

                        mw.Activate();
                    },

                    OnCancel = (sender) =>
                    {
                        sender.Close();

                        mw.Activate();
                    },

                    OnAuth = (sender) =>
                    {
                    },

                    OnCloseRequest = (sender) =>
                    {
                        sender.Close();

                        mw.Activate();
                    }
                });
            }
            catch (Exception e)
            {
                eventLog?.WriteEntry(e.Message, EventLogEntryType.Error);

                dialogs.Clear();


            }

            Mouse.OverrideCursor = null;

            triggerReadChip.IsEnabled = timerState;

            OnPropertyChanged(nameof(ChipTasks));

            return Task.CompletedTask;
        }

        /// <summary>
        /// Validates task indices and shows a dialog when the input is invalid.
        /// </summary>
        /// <param name="taskIndex">The task index assigned to the current task.</param>
        /// <param name="executeConditionTaskIndex">The task index referenced by the execute condition.</param>
        /// <param name="executeConditionErrorLevel">The execute condition error level.</param>
        /// <returns><see langword="true"/> when validation succeeds; otherwise <see langword="false"/>.</returns>
        private bool TryValidateTaskIndices(string taskIndex, string executeConditionTaskIndex, ERROR executeConditionErrorLevel)
        {
            if (!TaskIndexValidation.TryValidateTaskIndex(taskIndex, ChipTasks?.TaskCollection, SelectedSetupViewModel, out var errorMessage) ||
                !TaskIndexValidation.TryValidateExecuteConditionIndex(executeConditionTaskIndex, executeConditionErrorLevel, ChipTasks?.TaskCollection, out errorMessage))
            {
                dialogs.Add(new CustomDialogViewModel
                {
                    Caption = ResourceLoader.GetResource("messageBoxDefaultCaption"),
                    Message = errorMessage,
                    OnOk = sender => sender.Close()
                });
                return false;
            }

            return true;
        }

        /// <summary>
        /// Creates a new Task of Type Mifare Classic Card
        /// </summary>
        public IAsyncRelayCommand CreateClassicTaskCommand => new AsyncRelayCommand(OnNewCreateClassicTaskCommand);
        private Task OnNewCreateClassicTaskCommand()
        {


            var timerState = triggerReadChip.IsEnabled;

            triggerReadChip.IsEnabled = false;

            Mouse.OverrideCursor = Cursors.AppStarting;

            try
            {
                using (var device = ReaderDevice.Instance)
                {
                    // only call dialog if device is ready
                    if (device != null)
                    {
                        Dialogs.Add(taskDialogFactory.CreateClassicTaskDialog(SelectedSetupViewModel, ChipTasks, dialogs));
                    }

                    else
                    {
                        OnNewNoReaderFoundDialog();
                    }
                }
            }
            catch (Exception e)
            {
                eventLog?.WriteEntry(e.Message, EventLogEntryType.Error);

                dialogs.Clear();


            }

            Mouse.OverrideCursor = null;

            triggerReadChip.IsEnabled = timerState;

            return Task.CompletedTask;
        }

        /// <summary>
        /// 
        /// </summary>
        public IAsyncRelayCommand CreateDesfireTaskCommand => new AsyncRelayCommand(OnNewCreateDesfireTaskCommand);
        private Task OnNewCreateDesfireTaskCommand()
        {
            var timerState = triggerReadChip.IsEnabled;

            triggerReadChip.IsEnabled = false;

            Mouse.OverrideCursor = Cursors.AppStarting;

            using (var device = ReaderDevice.Instance)
            {
                if (device != null)
                {
                    Dialogs.Add(taskDialogFactory.CreateDesfireTaskDialog(SelectedSetupViewModel, ChipTasks, dialogs));
                }

                else
                {
                    OnNewNoReaderFoundDialog();
                }
            }

            Mouse.OverrideCursor = null;

            triggerReadChip.IsEnabled = timerState;

            return Task.CompletedTask;
        }

        /// <summary>
        /// 
        /// </summary>
        public ICommand CreateUltralightTaskCommand => new AsyncRelayCommand(OnNewCreateUltralightTaskCommand);
        private Task OnNewCreateUltralightTaskCommand()
        {


            var timerState = triggerReadChip.IsEnabled;

            triggerReadChip.IsEnabled = false;

            Mouse.OverrideCursor = Cursors.AppStarting;

            using (var device = ReaderDevice.Instance)
            {
                if (device != null)
                {
                    Dialogs.Add(taskDialogFactory.CreateUltralightTaskDialog(SelectedSetupViewModel, ChipTasks, dialogs));
                }
            }

            Mouse.OverrideCursor = null;

            triggerReadChip.IsEnabled = timerState;

            return Task.CompletedTask;
        }

        /// <summary>
        /// 
        /// </summary>
        public IAsyncRelayCommand ExecuteQuickCheckCommand => new AsyncRelayCommand(OnNewExecuteQuickCheckCommand);
        private async Task OnNewExecuteQuickCheckCommand()
        {
            try
            {
                Mouse.OverrideCursor = Cursors.AppStarting;

                await ReadChipCommand.ExecuteAsync(null);

                IsReaderBusy = true;
                Mouse.OverrideCursor = Cursors.AppStarting;

                if (treeViewParentNodes != null && treeViewParentNodes.Any(x => x.IsSelected))
                {
                    treeViewParentNodes.FirstOrDefault(x => x.IsSelected).IsExpanded = true;

                    if (((int)treeViewParentNodes.Single(x => x.IsSelected == true).CardType & 0xF000) == (int)CARD_TYPE.MifareClassic)
                    {
                        await treeViewParentNodes.Single(x => x.IsSelected == true).ExecuteClassicQuickCheckCommand.ExecuteAsync(null);
                    } // Mifare Classic

                    else if (Enum.GetName(typeof(CARD_TYPE), treeViewParentNodes.Single(x => x.IsSelected == true).CardType).ToLower(CultureInfo.CurrentCulture).Contains("desfire"))
                    {
                        await treeViewParentNodes.Single(x => x.IsSelected == true).ExecuteDesfireQuickCheckCommand.ExecuteAsync(null);
                    } // Mifare Desfire
                }

                IsReaderBusy = false;
                Mouse.OverrideCursor = null;
            }
            catch
            {
                Mouse.OverrideCursor = null;
            }


        }

        /// <summary>
        /// 
        /// </summary>
        public IAsyncRelayCommand ReadChipCommand => new AsyncRelayCommand(OnNewReadChipCommand);
        private async Task OnNewReadChipCommand()
        {
            var timerState = triggerReadChip.IsEnabled;
            triggerReadChip.IsEnabled = false;

            Mouse.OverrideCursor = Cursors.AppStarting;

            if (ReaderDevice.Instance == null)
            {
                Mouse.OverrideCursor = null;

                return;
            }

            IsReaderBusy = true;

            using (var device = ReaderDevice.Instance)
            {
                if(device != null)
                {
                    foreach (var item in treeViewParentNodes)
                    {
                        item.IsExpanded = false;
                        item.IsSelected = false;
                    }

                    device.GenericChip = new GenericChipModel();
                    var result = await device.ReadChipPublic();
                    var wellKnownTvNodes = from nodes in treeViewParentNodes where nodes.UID == device.GenericChip.UID select nodes;

                    if (result == ERROR.NoError && !wellKnownTvNodes.Any())
                    {
                        switch ((CARD_TYPE)((short)device.GenericChip.CardType & 0xF000))
                            {
                                case CARD_TYPE.MifareClassic:
                                    treeViewParentNodes.Add(
                                        new RFiDChipParentLayerViewModel(
                                            new MifareClassicChipModel(device.GenericChip as MifareClassicChipModel), Dialogs, false));
                                    break;

                                case CARD_TYPE.DESFireEV0:
                                case CARD_TYPE.DESFireEV1:
                                case CARD_TYPE.DESFireEV2:
                                case CARD_TYPE.DESFireEV3:
                                    treeViewParentNodes.Add(
                                        new RFiDChipParentLayerViewModel(
                                            new MifareDesfireChipModel(device.GenericChip as MifareDesfireChipModel), Dialogs, false));
                                    break;

                                case CARD_TYPE.MifarePlus:
                                    treeViewParentNodes.Add(
                                        new RFiDChipParentLayerViewModel(
                                            new MifareClassicChipModel(device.GenericChip as MifareClassicChipModel), Dialogs, false));
                                    break;

                                case CARD_TYPE.MifareUltralight:
                                    treeViewParentNodes.Add(
                                        new RFiDChipParentLayerViewModel(
                                            new MifareUltralightChipModel(device.GenericChip), Dialogs, false));
                                    break;

                                default:
                                    if(device.GenericChip?.CardType != CARD_TYPE.NOTAG)
                                    {
                                        treeViewParentNodes.Add(
                                            new RFiDChipParentLayerViewModel(
                                                new GenericChipModel(device.GenericChip), Dialogs, false));
                                    }
                                    break;
                            }
                        // fill treeview with dummy models and viewmodels
                    }
                    else if (wellKnownTvNodes.Any())
                    {
                        //wellKnownTvNodes.FirstOrDefault().IsExpanded = true;
                        wellKnownTvNodes.FirstOrDefault().IsSelected = true;
                    }
                }

            }

            Mouse.OverrideCursor = null;
            IsReaderBusy = false;

            triggerReadChip.IsEnabled = timerState;


        }

        /// <summary>
        /// Reset all Task status information
        /// </summary>
        public IAsyncRelayCommand ResetTaskStatusCommand => new AsyncRelayCommand(OnNewResetTaskStatusCommand);
        private async Task OnNewResetTaskStatusCommand()
        {
            await Task.Run(() =>
            {
                foreach (IGenericTaskModel chipTask in taskHandler.TaskCollection)
                {
                    chipTask.IsTaskCompletedSuccessfully = null;
                    chipTask.CurrentTaskErrorLevel = ERROR.Empty;
                }
            });
        }

        /// <summary>
        /// Reset all Reporttasks directory information
        /// </summary>
        public IAsyncRelayCommand ResetReportTaskDirectoryCommand => new AsyncRelayCommand(OnNewResetReportTaskDirectoryCommand);
        private Task OnNewResetReportTaskDirectoryCommand()
        {
            try
            {
                foreach (var chipTask in taskHandler.TaskCollection)
                {
                    switch (chipTask)
                    {
                        case CommonTaskViewModel ssVM:
                            ssVM.IsTaskCompletedSuccessfully = null;
                            reportOutputPath = null;
                            reportReaderWriter.ReportOutputPath = null;
                            reportReaderWriter.ReportTemplateFile = null;
                            ssVM.CurrentTaskErrorLevel = ERROR.Empty;
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                eventLog?.WriteEntry(e.Message, EventLogEntryType.Error);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Reset selected Task status information
        /// </summary>
        public IAsyncRelayCommand ResetSelectedTaskStatusCommand => new AsyncRelayCommand(OnNewResetSelectedTaskStatusCommand);
        private async Task OnNewResetSelectedTaskStatusCommand()
        {
            await Task.Run(() =>
            {
                (SelectedSetupViewModel as IGenericTaskModel).IsTaskCompletedSuccessfully = null;
                (SelectedSetupViewModel as IGenericTaskModel).CurrentTaskErrorLevel = ERROR.Empty;
            });
        }

        /// <summary>
        /// Remove all Tasks from DataGrid
        /// </summary>
        public IAsyncRelayCommand RemoveAllTasksCommand => new AsyncRelayCommand(OnNewRemoveAllTasksCommand);
        private async Task OnNewRemoveAllTasksCommand()
        {
            await Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() => taskHandler.TaskCollection.Clear()));
        }

        /// <summary>
        /// 
        /// </summary>
        public ICommand WriteSelectedTaskToChipAutoCommand => new RelayCommand(OnNewWriteSelectedTaskToChipAutoCommand);
        private void OnNewWriteSelectedTaskToChipAutoCommand()
        {
            if (!isWriteSelectedToChipAutoCheckedTemp)
            {
                isWriteSelectedToChipAutoCheckedTemp = true;
            }
            else
            {
                isWriteSelectedToChipAutoCheckedTemp = false;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public ICommand WriteToAllChipAutoCommand => new RelayCommand(OnNewWriteToAllChipAutoCommand);
        private void OnNewWriteToAllChipAutoCommand()
        {
            if (!triggerReadChip.IsEnabled)
            {
                triggerReadChip.IsEnabled = true;
            }
            else
            {
                triggerReadChip.IsEnabled = false;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public IAsyncRelayCommand WriteSelectedTaskToChipOnceCommand => new AsyncRelayCommand(OnNewWriteSelectedTaskToChipOnceCommand);
        private async Task OnNewWriteSelectedTaskToChipOnceCommand()
        {
            _runSelectedOnly = true;
            await WriteToChipOnceCommand.ExecuteAsync(null);
        }

        /// <summary>
        /// 
        /// </summary>
        public IAsyncRelayCommand WriteToChipOnceCommand => new AsyncRelayCommand(OnNewWriteToChipOnceCommand);
        private async Task OnNewWriteToChipOnceCommand()
        {
            Mouse.OverrideCursor = Cursors.AppStarting;

            if (!_runSelectedOnly)
            {
                await OnNewResetTaskStatusCommand();
            }

            await OnNewReadChipCommand();
            OnPropertyChanged(nameof(TreeViewParentNodes));
            OnPropertyChanged(nameof(ChipTasks));

            var request = new TaskExecutionRequest
            {
                TaskHandler = taskHandler,
                TreeViewParentNodes = treeViewParentNodes,
                VariablesFromArgs = variablesFromArgs,
                Dialogs = Dialogs,
                ReportReaderWriter = reportReaderWriter,
                ReportOutputPath = reportOutputPath,
                ReportTemplateFile = reportTemplateFile,
                SelectedSetupViewModel = SelectedSetupViewModel,
                RunSelectedOnly = _runSelectedOnly,
                UpdateSelectedSetupViewModel = vm => SelectedSetupViewModel = vm,
                UpdateReaderBusy = status => IsReaderBusy = status,
                NotifyTreeViewChanged = () => OnPropertyChanged(nameof(TreeViewParentNodes)),
                NotifyTasksChanged = () => OnPropertyChanged(nameof(ChipTasks)),
                EventLog = eventLog
            };

            try
            {
                var result = await taskExecutionService.ExecuteOnceAsync(request, CancellationToken.None);

                reportReaderWriter = result.ReportReaderWriter;
                reportOutputPath = result.ReportOutputPath;
                reportTemplateFile = result.ReportTemplateFile;
                _runSelectedOnly = result.RunSelectedOnly;
                SelectedSetupViewModel = result.SelectedSetupViewModel;
            }
            catch (Exception ex)
            {
                eventLog?.WriteEntry(ex.ToString(), EventLogEntryType.Error);
                Dialogs.Add(new CustomDialogViewModel
                {
                    Caption = ResourceLoader.GetResource("messageBoxDefaultCaption"),
                    Message = ex.Message,
                    OnOk = sender => sender.Close()
                });
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private void TaskExecutionService_ExecutionCompleted(object sender, TaskExecutionCompletedEventArgs e)
        {
            Mouse.OverrideCursor = null;
            OnPropertyChanged(nameof(TreeViewParentNodes));
            OnPropertyChanged(nameof(ChipTasks));
        }
        /// <summary>
        ///
        /// </summary>
        public ICommand CloseAllCommand => new RelayCommand(OnCloseAll);
        private void OnCloseAll()
        {
            Dialogs.Clear();
        }

        /// <summary>
        /// 
        /// </summary>
        public IAsyncRelayCommand SwitchLanguageToGerman => new AsyncRelayCommand(SetGermanLanguage);
        private async Task SetGermanLanguage()
        {
            using (var settings = new SettingsReaderWriter())
            {
                if (settings.DefaultSpecification.DefaultLanguage != "german")
                {
                    settings.DefaultSpecification.DefaultLanguage = "german";
                    await settings.SaveSettings();
                    OnNewLanguageChangedDialog();
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public IAsyncRelayCommand SwitchLanguageToEnglish => new AsyncRelayCommand(SetEnglishLanguage);
        private async Task SetEnglishLanguage()
        {
            using (var settings = new SettingsReaderWriter())
            {
                if (settings.DefaultSpecification.DefaultLanguage != "english")
                {
                    settings.DefaultSpecification.DefaultLanguage = "english";
                    await settings.SaveSettings();
                    OnNewLanguageChangedDialog();
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private void OnNewLanguageChangedDialog()
        {
            Dialogs.Add(new CustomDialogViewModel()
            {
                Message = ResourceLoader.GetResource("messageBoxRestartRequiredMessage"),
                Caption = ResourceLoader.GetResource("messageBoxRestartRequiredCaption"),

                OnCancel = (sender) =>
                {
                    sender.Close();
                },

                OnOk = (sender) =>
                {
                    {
                        Environment.Exit(0);
                    }
                }
            });

        }

        /// <summary>
        /// 
        /// </summary>
        private void OnNewNoReaderFoundDialog()
        {
            Dialogs.Add(new CustomDialogViewModel()
            {
                Message = ResourceLoader.GetResource("messageBoxNoReaderFoundMessage"),
                Caption = ResourceLoader.GetResource("messageBoxNoReaderFoundCaption"),

                OnCancel = (sender) =>
                {
                    sender.Close();
                },

                OnOk = (sender) =>
                {
                    sender.Close();
                }
            });

        }

        /// <summary>
        /// 
        /// </summary>
        public IAsyncRelayCommand NewReaderSetupDialogCommand => new AsyncRelayCommand(OnNewReaderSetupDialog);
        private Task OnNewReaderSetupDialog()
        {
            var settings = new SettingsReaderWriter();

            var currentSettings = settings.DefaultSpecification;

            ReaderDevice.PortNumber = int.TryParse(currentSettings.LastUsedComPort, out var portNumber) ? portNumber : 0;
            ReaderDevice.Reader = currentSettings.DefaultReaderProvider;

            using (var device = ReaderDevice.Instance)
            {
                Dialogs.Add(new SetupViewModel(device, settings)
                {
                    Caption = ResourceLoader.GetResource("windowCaptionReaderSetup"),

                    OnOk = (sender) =>
                    {
                        currentSettings.DefaultReaderProvider = sender.SelectedReader;
                        currentSettings.DefaultReaderName = sender.SelectedReader == ReaderTypes.PCSC
                            ? sender.SelectedReaderName
                            : string.Empty;
                        currentSettings.AutoLoadProjectOnStart = sender.LoadOnStart;
                        currentSettings.LastUsedComPort = sender.ComPort;
                        currentSettings.AutoCheckForUpdates = sender.CheckOnStart;
                        currentSettings.LastUsedBaudRate = sender.SelectedBaudRate;

                        settings.DefaultSpecification = currentSettings;

                        sender.SaveSettings.ExecuteAsync(null);

                        CurrentReader = string.IsNullOrWhiteSpace(sender.SelectedReaderName)
                            ? Enum.GetName(typeof(ReaderTypes), sender.SelectedReader)
                            : sender.SelectedReaderName;

                        ReaderDevice.Reader = sender.SelectedReader;

                        sender.Close();

                        IsReaderBusy = false;
                    },

                    OnConnect = (sender) =>
                    {
                        IsReaderBusy = true;
                    },

                    OnUpdateReaderStatus = (sender) =>
                    {
                        IsReaderBusy = sender;
                    },

                    OnBeginUpdateCheck = (sender) =>
                    {
                        if (sender)
                        {
                            updater?.StartMonitoring();
                        }
                    },

                    OnCancel = (sender) =>
                    {
                        sender.Close();
                        IsReaderBusy = false;
                        mw.Activate();
                    },

                    OnCloseRequest = (sender) =>
                    {
                        sender.Close();
                        IsReaderBusy = false;
                        mw.Activate();
                    }
                });
            }

            return Task.CompletedTask;
        }

        private void MainWindowViewModel_ReaderStatusChanged(object sender, EventArgs e) => throw new NotImplementedException();

        /// <summary>
        /// 
        /// </summary>
        public IAsyncRelayCommand NewOpenFileDialogCommand => new AsyncRelayCommand(OnNewOpenFileDialog);
        private async Task OnNewOpenFileDialog()
        {
            bool autoLoadLastUsedDB;
            string lastUsedDBPath;

            using (var settings = new SettingsReaderWriter())
            {
                CurrentReader = string.IsNullOrWhiteSpace(settings.DefaultSpecification.DefaultReaderName)
                    ? Enum.GetName(typeof(ReaderTypes), settings.DefaultSpecification.DefaultReaderProvider)
                    : settings.DefaultSpecification.DefaultReaderName;

                autoLoadLastUsedDB = settings.DefaultSpecification.AutoLoadProjectOnStart;
                lastUsedDBPath = settings.DefaultSpecification.LastUsedProjectPath;

                culture = (settings.DefaultSpecification.DefaultLanguage == "german") ? new CultureInfo("de-DE") : new CultureInfo("en-US");


                var dlg = new OpenFileDialogViewModel
                {
                    Title = ResourceLoader.GetResource("windowCaptionOpenProject"),
                    Filter = ResourceLoader.GetResource("filterStringSaveTasks"),
                    ParentWindow = this.mw,
                    Multiselect = false
                };

                if (dlg.Show(Dialogs) && dlg.FileName != null)
                {
                    Mouse.OverrideCursor = Cursors.AppStarting;

                    if (ChipTasks.TaskCollection != null && ChipTasks.TaskCollection.Count > 0)
                    {
                        ChipTasks.TaskCollection.Clear();
                    }

                    await databaseReaderWriter.ReadDatabase(dlg.FileName).ConfigureAwait(false);

                    settings.DefaultSpecification.LastUsedProjectPath = dlg.FileName;
                    await settings.SaveSettings();

                    foreach (var vm in databaseReaderWriter.TreeViewModel)
                    {
                        TreeViewParentNodes.Add(vm);
                    }

                    foreach (var setup in databaseReaderWriter.SetupModel.TaskCollection)
                    {
                        await Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() => ChipTasks.TaskCollection.Add(setup)));
                    }
                }
            }

            await Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
            {
                Mouse.OverrideCursor = null;
            }));

            OnPropertyChanged(nameof(ChipTasks));
        }

        /// <summary>
        /// Expose Command to Save ProjectFile Menu Item
        /// </summary>
        public IAsyncRelayCommand SaveTaskDialogCommand => new AsyncRelayCommand(OnNewSaveTaskDialogCommand);
        private Task OnNewSaveTaskDialogCommand()
        {
            using (var settings = new SettingsReaderWriter())
            {
                databaseReaderWriter.WriteDatabase(ChipTasks, settings.DefaultSpecification.LastUsedProjectPath);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Expose Command to Save As Menu Item
        /// </summary>
        public IAsyncRelayCommand SaveTaskAsDialogCommand => new AsyncRelayCommand(OnNewSaveTaskAsDialogCommand);
        private async Task OnNewSaveTaskAsDialogCommand()
        {
            string fileName;

            var dlg = new SaveFileDialogViewModel
            {
                Title = ResourceLoader.GetResource("windowCaptionSaveTasks"),
                Filter = ResourceLoader.GetResource("filterStringSaveTasks"),
            };

            if (dlg.Show(Dialogs) && dlg.FileName != null)
            {
                fileName = dlg.FileName;
            }
            else
            {
                return;
            }

            var mbox = new MessageBoxViewModel
            {
                ParentWindow = mw,
                Caption = ResourceLoader.GetResource("windowCaptionAskTaskDefault"),
                Message = ResourceLoader.GetResource("messageBoxMessageSetProjectAsDefault"),
                Buttons = MessageBoxButton.YesNo
            };

            if (mbox.Show(Dialogs) == MessageBoxResult.Yes)
            {
                using (var settings = new SettingsReaderWriter())
                {
                    settings.DefaultSpecification.LastUsedProjectPath = fileName;
                    await settings.SaveSettings();
                }
            }

            if (!string.IsNullOrEmpty(fileName))
            {
                databaseReaderWriter.WriteDatabase(ChipTasks, fileName);
            }
        }

        /// <summary>
        /// Expose Command to Save Menu Item
        /// </summary>
        public IAsyncRelayCommand SaveChipDialogCommand => new AsyncRelayCommand(OnNewSaveChipDialogCommand);
        private Task OnNewSaveChipDialogCommand()
        {
            var dlg = new SaveFileDialogViewModel
            {
                Title = ResourceLoader.GetResource("windowCaptionSaveTasks"),
                Filter = ResourceLoader.GetResource("filterStringSaveTasks")
            };

            if (dlg.Show(Dialogs) && dlg.FileName != null)
            {
                databaseReaderWriter.WriteDatabase(TreeViewParentNodes, dlg.FileName);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// 
        /// </summary>
        public ICommand ShowHelpCommand => new RelayCommand(OnNewShowHelpCommand);
        private void OnNewShowHelpCommand()
        {
            var q = from p in Process.GetProcesses() where ContainsAny(p.MainWindowTitle, new string[] { "Help", "Hilfe" }) select p;

            using (var settingsReaderWriter = new SettingsReaderWriter())
            {
                if (!q.Any())
                {
                    var pathToHelpFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, settingsReaderWriter.DefaultSpecification.DefaultLanguage == "german" ? "RFiDGear_de.chm" : "RFiDGear_en.chm");
                    if (File.Exists(pathToHelpFile))
                    {
                        var startInfo = new ProcessStartInfo(pathToHelpFile);
                        Process.Start(startInfo);
                    }

                }
            }
        }

        /// <summary>
        ///
        /// </summary>
        public IAsyncRelayCommand CheckUpdateCommand => new AsyncRelayCommand(OnNewCheckUpdateCommand);
        private async Task OnNewCheckUpdateCommand()
        {
            userIsNotifiedForAvailableUpdate = false;

            await updateNotifier.TriggerUpdateCheckAsync(() => AskForUpdateNow());
        }

        /// <summary>
        /// 
        /// </summary>
        public ICommand CloseApplication => new RelayCommand(OnCloseRequest);
        private void OnCloseRequest()
        {
            Environment.Exit(0);
        }

        #endregion Menu Commands

        #region Dependency Properties

        /// <summary>
        /// expose contextmenu on row click
        /// </summary>
        public ObservableCollection<MenuItem> EmptySpaceTreeViewContextMenu => emptySpaceTreeViewContextMenu;

        private ObservableCollection<MenuItem> emptySpaceTreeViewContextMenu;
        /// <summary>
        /// expose contextmenu on row click
        /// </summary>
        public ObservableCollection<MenuItem> RowContextMenu => rowContextMenuItems;
        private ObservableCollection<MenuItem> rowContextMenuItems;

        /// <summary>
        /// expose contextmenu on empty space click
        /// </summary>
        public ObservableCollection<MenuItem> EmptySpaceContextMenu => emptySpaceContextMenuItems;
        private ObservableCollection<MenuItem> emptySpaceContextMenuItems;


        /// <summary>
        /// 
        /// </summary>
        public object SelectedSetupViewModel
        {
            get => selectedSetupViewModel;
            set
            {
                selectedSetupViewModel = value;
                OnPropertyChanged(nameof(SelectedSetupViewModel));
            }

        }
        private object selectedSetupViewModel;

        /// <summary>
        /// 
        /// </summary>
        public ObservableCollection<RFiDChipParentLayerViewModel> TreeViewParentNodes
        {
            get => treeViewParentNodes;

            set
            {
                treeViewParentNodes = value;
                OnPropertyChanged(nameof(TreeViewParentNodes));
            }
        }
        private ObservableCollection<RFiDChipParentLayerViewModel> treeViewParentNodes;

        /// <summary>
        /// 
        /// </summary>
        public ChipTaskHandlerModel ChipTasks
        {
            get => taskHandler;

            set
            {
                taskHandler = value;
                OnPropertyChanged(nameof(ChipTasks));
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public bool IsWriteToAllChipAutoChecked => triggerReadChip?.IsEnabled ?? false;

        /// <summary>
        /// 
        /// </summary>
        public bool IsWriteSelectedToChipAutoChecked => triggerReadChip?.IsEnabled ?? false;
        private bool isWriteSelectedToChipAutoCheckedTemp;

        /// <summary>
        ///
        /// </summary>
        public string CurrentReader
        {
            get => currentReader;
            set
            {
                currentReader = value;
                OnPropertyChanged(nameof(CurrentReader));
            }
        }
        private string currentReader;

        /// <summary>
        ///
        /// </summary>
        public string ReaderStatus
        {
            get => readerStatus;
            set
            {
                readerStatus = value;
                OnPropertyChanged(nameof(ReaderStatus));
            }
        }
        private string readerStatus;

        /// <summary>
        ///
        /// </summary>
        public string DateTimeStatusBar
        {
            get => dateTimeStatusBar;
            set
            {
                dateTimeStatusBar = value;
                OnPropertyChanged(nameof(DateTimeStatusBar));
            }
        }
        private string dateTimeStatusBar;

        /// <summary>
        /// 
        /// </summary>
        public bool IsSelected
        {
            get; set;
        }

        /// <summary>
        /// 
        /// </summary>
        public bool? IsReaderBusy
        {
            get => isReaderBusy;
            set
            {
                isReaderBusy = value;
                if (value == true)
                {
                    readerMonitor?.Pause();
                }
                else
                {
                    readerMonitor?.Resume();
                }
                OnPropertyChanged(nameof(IsReaderBusy));
            }
        }
        private bool? isReaderBusy;

        /// <summary>
        /// 
        /// </summary>
        public bool IsCheckForUpdatesChecked
        {
            get
            {
                using (var settings = new SettingsReaderWriter())
                {
                    return settings.DefaultSpecification != null && settings.DefaultSpecification.AutoCheckForUpdates;
                }
            }
            set
            {
                using (var settings = new SettingsReaderWriter())
                {
                    if (value)
                    {
                        updater.StartMonitoring().GetAwaiter().GetResult();
                    }
                    else
                    {
                        updater.StopMonitoring().GetAwaiter().GetResult();
                    }

                    settings.DefaultSpecification.AutoCheckForUpdates = value;
                    settings.SaveSettings().GetAwaiter().GetResult();
                }
                OnPropertyChanged(nameof(IsCheckForUpdatesChecked));

            }
        }

        /// <summary>
        /// 
        /// </summary>
        public bool RadioButtonGermanLanguageSelectedState
        {
            get
            {
                using (var settings = new SettingsReaderWriter())
                {
                    if (settings.DefaultSpecification.DefaultLanguage == "german")
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            set
            {
                using (var settings = new SettingsReaderWriter())
                {
                    if (settings.DefaultSpecification.DefaultLanguage == "english")
                    {
                        value = false;
                    }

                    OnPropertyChanged(nameof(RadioButtonGermanLanguageSelectedState));
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public bool RadioButtonEnglishLanguageSelectedState
        {
            get
            {
                using (var settings = new SettingsReaderWriter())
                {
                    if (settings.DefaultSpecification.DefaultLanguage == "german")
                    {
                        return false;
                    }
                    else
                    {
                        return true;
                    }
                }
            }
            set
            {
                using (var settings = new SettingsReaderWriter())
                {
                    if (settings.DefaultSpecification.DefaultLanguage == "german")
                    {
                        value = false;
                    }

                    OnPropertyChanged(nameof(RadioButtonEnglishLanguageSelectedState));
                }
            }
        }

        #endregion Dependency Properties

        #region Extensions
        private void EnableUpdate(object sender, EventArgs e)
        {
            userIsNotifiedForAvailableUpdate = true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private async Task AskForUpdateNow()
        {
            await AskForUpdateNow(false);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateDisabled"></param>
        /// <returns></returns>
        private async Task AskForUpdateNow(bool updateDisabled)
        {
            if (!userIsNotifiedForAvailableUpdate || updateDisabled)
            {
                await Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
                {
                    Dialogs.Add(new UpdateNotifierViewModel(updater.UpdateInfoText)
                    {
                        Caption = updateDisabled ? "Changelog" : "Update Available",

                        OnOk = async (updateAction) =>
                        {
                            if (!updateDisabled)
                            {
                                Mouse.OverrideCursor = Cursors.AppStarting;
                                await updater.Update();
                            }
                            updateAction.Close();
                        },

                        OnCancel = (updateAction) =>
                        {
                            updater.AllowUpdate = false;
                            updateAction.Close();
                            mw.Activate();
                        },

                        OnCloseRequest = (updateAction) =>
                        {
                            updater.AllowUpdate = false;
                            updateAction.Close();
                            mw.Activate();
                        }
                    });
                }));
            }

            userIsNotifiedForAvailableUpdate = true;
        }

        private void CloseThreads(object sender, CancelEventArgs e)
        {
            Environment.Exit(0);
        }

        private async void LoadCompleted(object sender, EventArgs e)
        {
            Application.Current.MainWindow.Activated -= new EventHandler(LoadCompleted);

            mw = (MainWindow)Application.Current.MainWindow;
            mw.Title = string.Format("RFiDGear {0}.{1}", Version.Major, Version.Minor);

            updateScheduler.Begin(() => AskForUpdateNow());
            readerMonitor.StartMonitoring(CheckReader);

            var startupArguments = startupArgumentProcessor.Process(args);
            ApplyStartupArguments(startupArguments);

            var splashScreenType = typeof(SplashScreenViewModel);

            await projectBootstrapper.BootstrapAsync(new ProjectBootstrapRequest
            {
                ProjectFilePath = startupArguments.ProjectFilePath,
                Autorun = startupArguments.Autorun,
                CreateSplashScreen = () => new SplashScreenViewModel(),
                AddDialog = dialog => Dialogs.Add(dialog as IDialogViewModel),
                RemoveSplash = () =>
                {
                    if (Dialogs.FirstOrDefault() != null && Dialogs.FirstOrDefault().GetType() == splashScreenType)
                    {
                        Dialogs.RemoveAt(0);
                    }
                },
                OpenProjectAsync = file => OpenLastProjectFile(file ?? string.Empty),
                ResetTaskStatusAsync = () => OnNewResetTaskStatusCommand(),
                ReadChipAsync = () => OnNewReadChipCommand(),
                WriteOnceAsync = () => OnNewWriteToChipOnceCommand(),
                UpdateDateTime = value => DateTimeStatusBar = value,
                SetCurrentReader = value => CurrentReader = value,
                SetReaderPort = value => ReaderDevice.PortNumber = value,
                SetCulture = value => culture = value
            });
        }

        private void ApplyStartupArguments(StartupArgumentResult startupArguments)
        {
            if (startupArguments == null)
            {
                return;
            }

            reportOutputPath = startupArguments.ReportOutputPath ?? reportOutputPath;
            reportTemplateFile = startupArguments.ReportTemplateFile ?? reportTemplateFile;

            variablesFromArgs.Clear();

            foreach (var variable in startupArguments.Variables)
            {
                variablesFromArgs[variable.Key] = variable.Value;
            }
        }

        private async void CheckReader(object sender)
        {
            readerMonitor?.Pause();

            using (var settings = new SettingsReaderWriter())
            {
                if (ReaderDevice.Instance != null && IsReaderBusy != true)
                {
                    try
                    {
                        if (!ReaderDevice.Instance.IsConnected)
                        {
                            var result = await ReaderDevice.Instance.ConnectAsync();

                            if (result == ERROR.NoError)
                            {
                                IsReaderBusy = false;
                                CurrentReader = settings.DefaultSpecification.DefaultReaderProvider == ReaderTypes.None
                                    ? "N/A"
                                    : settings.DefaultSpecification.DefaultReaderProvider + " " +
                                    ReaderDevice.Instance.ReaderUnitName +
                                    ReaderDevice.Instance.ReaderUnitVersion;
                            }
                            else
                            {
                                IsReaderBusy = null;
                                CurrentReader = settings.DefaultSpecification.DefaultReaderProvider == ReaderTypes.None
                                ? "N/A"
                                : settings.DefaultSpecification.DefaultReaderProvider.ToString();
                            }
                        }
                        else 
                        {
                            CurrentReader = settings.DefaultSpecification.DefaultReaderProvider == ReaderTypes.None
                                ? "N/A"
                                : settings.DefaultSpecification.DefaultReaderProvider + " " +
                                ReaderDevice.Instance.ReaderUnitName +
                                ReaderDevice.Instance.ReaderUnitVersion;
                        }
                    }
                    catch
                    {
                        CurrentReader = settings.DefaultSpecification.DefaultReaderProvider == ReaderTypes.None
                            ? "N/A"
                            : settings.DefaultSpecification.DefaultReaderProvider.ToString();
                    }
                }
                else if (ReaderDevice.Instance != null && IsReaderBusy != true)
                {
                    IsReaderBusy = null;
                    CurrentReader = settings.DefaultSpecification.DefaultReaderProvider == ReaderTypes.None
                        ? "N/A"
                        : settings.DefaultSpecification.DefaultReaderProvider.ToString();
                    await ReaderDevice.Instance.ConnectAsync().ConfigureAwait(false);
                    if (ReaderDevice.Instance.IsConnected)
                    {
                        IsReaderBusy = false;
                    }
                }
            };

            readerMonitor?.Resume();

        }

        private bool ContainsAny(string haystack, params string[] needles)
        {
            return needles.Any(haystack.Contains);
        }

        #endregion
    }
}
