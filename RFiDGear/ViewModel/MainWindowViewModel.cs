/* This is RFiDGear's Main Window Class
 * 
 * RFiDGear has a Set of objects in an ObservableCollection.
 * 
 * These objects has the Interface IGenericTask and can have a Type of:
 * - DesfireSetupViewModel
 * - ClassicSetupViewModel
 * - UltralightSetupViewModel
 * - PlusSetupViewModel
 * - CommonTaskSetupViewModel
 * - GenericChipSetupViewModel (i.e. check uid, check chiptype)
 * 
 * Each *SetupViewModel has one of the Following Properties:
 * 
 * - Dialogs: Showing Dialogs to the User. Inherited from MainWindow MVVMDialogs
 * - TaskError: Is "Empty" by default. A Task can only be Executed when its ErrorLevel is Empty
 * - RelayCommands: The "Tasks" that are needed to be done. The RelayCommands can be Executed by its ViewModel and a "Button" or by its 
 *   corresponding "Execute" Method when called as a Task.
 * - 
 */

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

using MefMvvm.SharedContracts.ViewModel;
using MVVMDialogs.ViewModels;
using RedCell.Diagnostics.Update;
using RFiDGear.DataAccessLayer;
using RFiDGear.DataAccessLayer.Remote.FromIO;
using RFiDGear.Model;
using RFiDGear.Services;
using RFiDGear.Services.Commands;

namespace RFiDGear.ViewModel
{
    /// <summary>
    /// Description of MainWindowViewModel.
    /// </summary>
    [ExportViewModel("MainWin")]
    public class MainWindowViewModel : ObservableObject
    {
        private readonly Version Version = Assembly.GetExecutingAssembly().GetName().Version;
        private readonly EventLog eventLog = new EventLog("Application", ".", Assembly.GetEntryAssembly().GetName().Name);
        private readonly string[] args;
        private readonly Dictionary<string, string> variablesFromArgs = new Dictionary<string, string>();
        private readonly IReaderInitializer readerInitializer;
        private readonly IUpdateChecker updateChecker;
        private readonly IPollingScheduler pollingScheduler;
        private readonly IMainWindowServiceFactory serviceFactory;
        private readonly IStartupConfigurator startupConfigurator;
        private readonly IMainWindowTimerFactory timerFactory;
        private readonly ICommandMenuBuilder commandMenuBuilder;

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

        private int currentTaskIndex = 0;
        // set if task was completed; indicates greenlight to continue execution
        // if programming takes too long; quit the process
        private bool userIsNotifiedForAvailableUpdate = false;
        private protected Mutex mutex;

        // one reader, one instance - only

        #region Constructors

        public MainWindowViewModel()
            : this(new MainWindowServiceFactory())
        {
        }

        public MainWindowViewModel(IMainWindowServiceFactory factory)
        {
            serviceFactory = factory;
            readerInitializer = serviceFactory.CreateReaderInitializer();
            updateChecker = serviceFactory.CreateUpdateChecker();
            pollingScheduler = serviceFactory.CreatePollingScheduler();
            timerFactory = serviceFactory.CreateMainWindowTimerFactory();
            startupConfigurator = serviceFactory.CreateStartupConfigurator();
            commandMenuBuilder = serviceFactory.CreateCommandMenuBuilder();

            IsReaderBusy = false;

            if (!EventLog.SourceExists(Assembly.GetEntryAssembly().GetName().Name))
            {
                EventLog.CreateEventSource(new EventSourceCreationData(Assembly.GetEntryAssembly().GetName().Name, "Application"));
            }

            eventLog.Source = Assembly.GetEntryAssembly().GetName().Name;

            pollingScheduler.OnUpdateRequested += CheckUpdate;
            pollingScheduler.OnReaderRequested += CheckReader;

            RunMutex(this, null);

            args = Environment.GetCommandLineArgs();
            var startupConfiguration = startupConfigurator.Configure();

            CurrentReader = startupConfiguration.ReaderName;
            culture = startupConfiguration.Culture;

            triggerReadChip = timerFactory.CreateTriggerReadTimer(UpdateChip);
            taskTimeout = timerFactory.CreateTaskTimeoutTimer(TaskTimeout);

            treeViewParentNodes = new ObservableCollection<RFiDChipParentLayerViewModel>();

            taskHandler = new ChipTaskHandlerModel();

            ReaderStatus = "";
            DateTimeStatusBar = "";
            databaseReaderWriter = new DatabaseReaderWriter();
            resLoader = new ResourceLoader();

            
            var commandMenus = commandMenuBuilder.BuildMenus(GetAddEditCommand, WriteSelectedTaskToChipOnceCommand,
                DeleteSelectedTaskCommand, ResetSelectedTaskStatusCommand, WriteToChipOnceCommand,
                ResetReportTaskDirectoryCommand, ReadChipCommand);

            rowContextMenuItems = commandMenus.RowContextMenuItems;
            emptySpaceContextMenuItems = commandMenus.EmptySpaceContextMenuItems;
            emptySpaceTreeViewContextMenu = commandMenus.EmptyTreeViewContextMenuItems;

            Application.Current.MainWindow.Closing += new CancelEventHandler(CloseThreads);
            Application.Current.MainWindow.Activated += new EventHandler(LoadCompleted);

            //reminder: any dialog boxes added in the constructor won't appear until DialogBehavior.DialogViewModels gets bound to the Dialogs collection.
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
        private async Task OnNewGetAddEditCommand()
        {
            switch (selectedSetupViewModel)
            {
                case CommonTaskViewModel _:
                    await OnNewNewCreateReportTaskCommand();
                    break;
                case GenericChipTaskViewModel _:
                    await OnNewCreateGenericChipTaskCommand();
                    break;
                case MifareClassicSetupViewModel _:
                    await OnNewCreateClassicTaskCommand();
                    break;
                case MifareDesfireSetupViewModel _:
                    OnNewCreateDesfireTaskCommand();
                    break;
                case MifareUltralightSetupViewModel _:
                    OnNewCreateUltralightTaskCommand();
                    break;
            }
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

                Dialogs.RemoveAt(0);

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
                eventLog.WriteEntry(e.Message, EventLogEntryType.Error);
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
#if DEBUG
            taskTimeout.Start();
            taskTimeout.Stop();
            taskTimeout.IsEnabled = false;

            taskHandler.TaskCollection[currentTaskIndex].IsTaskCompletedSuccessfully = false;
#else
            taskTimeout.IsEnabled = false;
            taskTimeout.Stop();

            taskHandler.TaskCollection[(int)taskTimeout.Tag].IsTaskCompletedSuccessfully = false;

            currentTaskIndex = int.MaxValue;
#endif
        }

        /// <summary>
        /// "Remove all listed Chips from listing" was called
        /// </summary>
        public ICommand RemoveChipsFromTreeCommand => new RelayCommand(OnNewRemoveChipsFromTreeCommand);
        private void OnNewRemoveChipsFromTreeCommand()
        {
            TreeViewParentNodes.Clear();
        }

        public ICommand DeleteSelectedTaskCommand => new RelayCommand(() =>
        {
            if (SelectedSetupViewModel != null)
            {
                taskHandler.TaskCollection.Remove(SelectedSetupViewModel);
            }
        });

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
                AboutText = string.Format("RFiDGear {0}.{1}.{2} {3}\n\n", Version.Major, Version.Minor, Version.Build, RFiDGear.DataAccessLayer.Constants.TITLE_SUFFIX)
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
        private async Task OnNewCreateGenericChipTaskCommand()
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
                        dialogs.Add(new GenericChipTaskViewModel(SelectedSetupViewModel, ChipTasks.TaskCollection, dialogs)
                        {
                            Caption = ResourceLoader.GetResource("windowCaptionAddEditGenericChipTask"),

                            OnOk = async (sender) =>
                            {
                                if (sender.SelectedTaskType == TaskType_GenericChipTask.ChangeDefault)
                                {
                                    await sender.SaveSettings.ExecuteAsync(null);
                                }

                                if (sender.SelectedTaskType == TaskType_GenericChipTask.ChipIsOfType ||
                                sender.SelectedTaskType == TaskType_GenericChipTask.CheckUID ||
                                sender.SelectedTaskType == TaskType_GenericChipTask.ChipIsMultiChip)
                                {
                                    if ((ChipTasks.TaskCollection.OfType<GenericChipTaskViewModel>().Where(x => x.SelectedTaskIndexAsInt == sender.SelectedTaskIndexAsInt).Any()))
                                    {
                                        ChipTasks.TaskCollection.RemoveAt(ChipTasks.TaskCollection.IndexOf(SelectedSetupViewModel));
                                    }

                                    ChipTasks.TaskCollection.Add(sender);

                                    ChipTasks.TaskCollection = new ObservableCollection<IGenericTaskModel>(ChipTasks.TaskCollection.OrderBy(x => x.SelectedTaskIndexAsInt));

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

                    else
                    {
                        OnNewNoReaderFoundDialog();
                    }
                }
            }
            catch (Exception e)
            {
                eventLog.WriteEntry(e.Message, EventLogEntryType.Error);

                dialogs.Clear();

            }

            Mouse.OverrideCursor = null;

            triggerReadChip.IsEnabled = timerState;

            OnPropertyChanged(nameof(ChipTasks));
        }

        /// <summary>
        /// Create a new "Common" Task of Type "Report Creator"
        /// </summary>
        public IAsyncRelayCommand CreateGenericTaskCommand => new AsyncRelayCommand(OnNewNewCreateReportTaskCommand);
        private async Task OnNewNewCreateReportTaskCommand()
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
                            if (ChipTasks.TaskCollection.OfType<CommonTaskViewModel>().Where(x => (x as CommonTaskViewModel).SelectedTaskIndexAsInt == sender.SelectedTaskIndexAsInt).Any())
                            {
                                ChipTasks.TaskCollection.RemoveAt(ChipTasks.TaskCollection.IndexOf(SelectedSetupViewModel));
                            }

                            ChipTasks.TaskCollection.Add(sender);

                            ChipTasks.TaskCollection = new ObservableCollection<IGenericTaskModel>(ChipTasks.TaskCollection.OrderBy(x => x.SelectedTaskIndexAsInt));

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
                eventLog.WriteEntry(e.Message, EventLogEntryType.Error);

                dialogs.Clear();


            }

            Mouse.OverrideCursor = null;

            triggerReadChip.IsEnabled = timerState;

            OnPropertyChanged(nameof(ChipTasks));
        }

        /// <summary>
        /// Creates a new Task of Type Mifare Classic Card
        /// </summary>
        public IAsyncRelayCommand CreateClassicTaskCommand => new AsyncRelayCommand(OnNewCreateClassicTaskCommand);
        private async Task OnNewCreateClassicTaskCommand()
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
                        dialogs.Add(new MifareClassicSetupViewModel(SelectedSetupViewModel, dialogs)
                        {
                            Caption = ResourceLoader.GetResource("windowCaptionAddEditMifareClassicTask"),
                            IsClassicAuthInfoEnabled = true,

                            OnOk = (sender) =>
                            {
                                if (sender.SelectedTaskType == TaskType_MifareClassicTask.ChangeDefault)
                                {
                                    sender.SaveSettings.ExecuteAsync(null);
                                }

                                if (sender.SelectedTaskType == TaskType_MifareClassicTask.WriteData ||
                                    sender.SelectedTaskType == TaskType_MifareClassicTask.ReadData ||
                                    sender.SelectedTaskType == TaskType_MifareClassicTask.EmptyCheck)
                                {

                                    if (ChipTasks.TaskCollection.OfType<MifareClassicSetupViewModel>().Where(x => (x as MifareClassicSetupViewModel).SelectedTaskIndexAsInt == sender.SelectedTaskIndexAsInt).Any())
                                    {
                                        ChipTasks.TaskCollection.RemoveAt(ChipTasks.TaskCollection.IndexOf(SelectedSetupViewModel));
                                    }

                                    ChipTasks.TaskCollection.Add(sender);

                                    ChipTasks.TaskCollection = new ObservableCollection<IGenericTaskModel>(ChipTasks.TaskCollection.OrderBy(x => x.SelectedTaskIndexAsInt));

                                    OnPropertyChanged(nameof(ChipTasks));
                                }
                                sender.Close();

                                mw.Activate();
                            },

                            OnUpdateStatus = (sender) =>
                            {
                                IsReaderBusy = sender;
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

                    else
                    {
                        OnNewNoReaderFoundDialog();
                    }
                }
            }
            catch (Exception e)
            {
                eventLog.WriteEntry(e.Message, EventLogEntryType.Error);

                dialogs.Clear();


            }

            Mouse.OverrideCursor = null;

            triggerReadChip.IsEnabled = timerState;
        }

        /// <summary>
        /// 
        /// </summary>
        public IAsyncRelayCommand CreateDesfireTaskCommand => new AsyncRelayCommand(OnNewCreateDesfireTaskCommand);
        private async Task OnNewCreateDesfireTaskCommand()
        {
            var timerState = triggerReadChip.IsEnabled;

            triggerReadChip.IsEnabled = false;

            Mouse.OverrideCursor = Cursors.AppStarting;

            using (var device = ReaderDevice.Instance)
            {
                if (device != null)
                {
                    Dialogs.Add(new MifareDesfireSetupViewModel(SelectedSetupViewModel, dialogs)
                    {
                        Caption = ResourceLoader.GetResource("windowCaptionAddEditMifareDesfireTask"),

                        OnOk = (sender) =>
                        {
                            if (sender.SelectedTaskType == TaskType_MifareDesfireTask.ChangeDefault)
                            {
                                sender.SaveSettings.ExecuteAsync(null);
                            }

                            if (sender.SelectedTaskType == TaskType_MifareDesfireTask.FormatDesfireCard ||
                                sender.SelectedTaskType == TaskType_MifareDesfireTask.PICCMasterKeyChangeover ||
                                sender.SelectedTaskType == TaskType_MifareDesfireTask.ReadAppSettings ||
                                sender.SelectedTaskType == TaskType_MifareDesfireTask.AppExistCheck ||
                                sender.SelectedTaskType == TaskType_MifareDesfireTask.AuthenticateApplication ||
                                sender.SelectedTaskType == TaskType_MifareDesfireTask.ApplicationKeyChangeover ||
                                sender.SelectedTaskType == TaskType_MifareDesfireTask.DeleteApplication ||
                                sender.SelectedTaskType == TaskType_MifareDesfireTask.CreateApplication ||
                                sender.SelectedTaskType == TaskType_MifareDesfireTask.DeleteFile ||
                                sender.SelectedTaskType == TaskType_MifareDesfireTask.CreateFile ||
                                sender.SelectedTaskType == TaskType_MifareDesfireTask.ReadData ||
                                sender.SelectedTaskType == TaskType_MifareDesfireTask.WriteData)
                            {
                                if (ChipTasks.TaskCollection.OfType<MifareDesfireSetupViewModel>().Any(x => (x as MifareDesfireSetupViewModel).SelectedTaskIndexAsInt == sender.SelectedTaskIndexAsInt))
                                {
                                    ChipTasks.TaskCollection.RemoveAt(ChipTasks.TaskCollection.IndexOf(SelectedSetupViewModel));
                                }

                                ChipTasks.TaskCollection.Add(sender);

                                ChipTasks.TaskCollection = new ObservableCollection<IGenericTaskModel>(ChipTasks.TaskCollection.OrderBy(x => x.SelectedTaskIndexAsInt));

                                OnPropertyChanged(nameof(ChipTasks));

                                sender.Close();

                                mw.Activate();
                            }
                        },

                        OnUpdateStatus = (sender) =>
                        {
                            IsReaderBusy = sender;
                        },

                        OnCancel = (sender) =>
                        {
                            sender.Close();

                            mw.Activate();
                        },

                        OnCloseRequest = (sender) =>
                        {
                            sender.Close();

                            mw.Activate();
                        }
                    });
                }

                else
                {
                    OnNewNoReaderFoundDialog();
                }
            }

            Mouse.OverrideCursor = null;

            triggerReadChip.IsEnabled = timerState;
        }

        /// <summary>
        /// 
        /// </summary>
        public ICommand CreateUltralightTaskCommand => new RelayCommand(OnNewCreateUltralightTaskCommand);
        private void OnNewCreateUltralightTaskCommand()
        {


            var timerState = triggerReadChip.IsEnabled;

            triggerReadChip.IsEnabled = false;

            Mouse.OverrideCursor = Cursors.AppStarting;

            using (var device = ReaderDevice.Instance)
            {
                if (device != null)
                {

                    Dialogs.Add(new MifareUltralightSetupViewModel(SelectedSetupViewModel, dialogs)
                    {
                        Caption = ResourceLoader.GetResource("windowCaptionAddEditMifareDesfireTask"),

                        OnOk = (sender) =>
                        {
                            if (sender.SelectedTaskType == TaskType_MifareUltralightTask.ChangeDefault)
                            {
                                sender.Settings.SaveSettings();
                            }

                            if (sender.SelectedTaskType == TaskType_MifareUltralightTask.ReadData ||
                                            sender.SelectedTaskType == TaskType_MifareUltralightTask.WriteData)
                            {
                                if ((ChipTasks.TaskCollection.OfType<MifareUltralightSetupViewModel>().Where(x => (x as MifareUltralightSetupViewModel).SelectedTaskIndexAsInt == sender.SelectedTaskIndexAsInt).Any()))
                                {
                                    ChipTasks.TaskCollection.RemoveAt(ChipTasks.TaskCollection.IndexOf(SelectedSetupViewModel));
                                }

                                ChipTasks.TaskCollection.Add(sender);

                                ChipTasks.TaskCollection = new ObservableCollection<IGenericTaskModel>(ChipTasks.TaskCollection.OrderBy(x => x.SelectedTaskIndexAsInt));

                                OnPropertyChanged(nameof(ChipTasks));

                                sender.Close();

                                mw.Activate();
                            }
                        },

                        OnCancel = (sender) =>
                        {
                            sender.Close();

                            mw.Activate();
                        },

                        OnCloseRequest = (sender) =>
                        {
                            sender.Close();

                            mw.Activate();
                        }
                    });
                }
            }

            Mouse.OverrideCursor = null;

            triggerReadChip.IsEnabled = timerState;
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
        private async Task OnNewResetReportTaskDirectoryCommand()
        {
            await Task.Run(async () =>
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
                    eventLog.WriteEntry(e.Message, EventLogEntryType.Error);
                }
            });

        }

        /// <summary>
        /// Reset selected Task status information
        /// </summary>
        public IAsyncRelayCommand ResetSelectedTaskStatusCommand => new AsyncRelayCommand(OnNewResetSelectedTaskStatusCommand);
        private async Task OnNewResetSelectedTaskStatusCommand()
        {
            await Task.Run(() =>
            {
                if (SelectedSetupViewModel is IGenericTaskModel task)
                {
                    task.IsTaskCompletedSuccessfully = null;
                    task.CurrentTaskErrorLevel = ERROR.Empty;
                }
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

            var GenericChip = ReaderDevice.Instance.GenericChip; //new GenericChipModel("", CARD_TYPE.NOTAG);

            currentTaskIndex = 0;
            var taskDictionary = new Dictionary<string, int>();

            // create a new key,value pair of taskpositions (int) <-> taskindex (string)
            // (they could be different as because item at array position 0 can have index "100")
            foreach (var rfidTaskObject in taskHandler.TaskCollection)
            {
                taskDictionary.Add(rfidTaskObject.CurrentTaskIndex, taskHandler.TaskCollection.IndexOf(rfidTaskObject));
            }

#if DEBUG
            taskTimeout.IsEnabled = false;
#else
            taskTimeout.IsEnabled = true;
            taskTimeout.Start();
#endif
            //TODO: Optimize Execute Conditions Check aka: "SelectedExecuteCondition"
            triggerReadChip.Tag = triggerReadChip.IsEnabled;
            triggerReadChip.IsEnabled = false;

            try
            {
                //try to get singleton instance
                using (var device = ReaderDevice.Instance)
                {
                    //reader was ready - proceed
                    if (device != null)
                    {
                        

                        if (device.GenericChip != null && !string.IsNullOrEmpty(device.GenericChip.UID))
                        {
                            if (GenericChip.CardType.ToString().ToLower(CultureInfo.CurrentCulture).Contains("desfire"))
                            {
                                await device.GetMiFareDESFireChipAppIDs();

                                GenericChip = device.GenericChip;
                            }
                        }

                        else
                        {
                            await device.ReadChipPublic();

                            GenericChip = device.GenericChip;
                        }
                    }


                    if (treeViewParentNodes.Any(x => x.IsSelected))
                    {
                        treeViewParentNodes.First(x => x.IsSelected).IsSelected = false;
                    }

                    //only run if theres a hfTag on the reader and its uid was previously added
                    if (
                        !string.IsNullOrWhiteSpace(GenericChip.UID) &&
                        treeViewParentNodes.Any(x => x.UID == GenericChip.UID))
                    {
                        //select current parentnode (hfTag) on reader
                        treeViewParentNodes.First(x => x.UID == GenericChip.UID).IsSelected = true;
                        treeViewParentNodes.First(x => x.IsSelected).IsBeingProgrammed = true;
                    }

                    //are there tasks present to process?
                    while (currentTaskIndex < taskHandler.TaskCollection.Count)
                    {
                        await Task.Delay(50);

                        if (_runSelectedOnly)
                        {
                            currentTaskIndex = taskHandler.TaskCollection.IndexOf(SelectedSetupViewModel);
                        }

                        taskTimeout.Stop();
                        taskTimeout.Start();
                        taskTimeout.IsEnabled = true;
                        taskTimeout.Tag = currentTaskIndex;

                        SelectedSetupViewModel = taskHandler.TaskCollection[currentTaskIndex];

                        //decide what type of task to process next. use exact array positions 
                        switch (taskHandler.TaskCollection[currentTaskIndex])
                        {

                            case CommonTaskViewModel csVM:

                                csVM.GenericChip = GenericChip;
                                csVM.DesfireChip = device.DesfireChip;
                                csVM.AvailableTasks = taskHandler.TaskCollection;
                                csVM.Args = variablesFromArgs;

                                switch (csVM.SelectedTaskType)
                                {
                                    case TaskType_CommonTask.CreateReport:
                                        taskTimeout.Stop();
                                        switch (csVM.CurrentTaskErrorLevel)
                                        {
                                            case ERROR.NotReadyError:
                                                break;

                                            case ERROR.Empty:
                                                csVM.CurrentTaskErrorLevel = ERROR.NotReadyError;
                                                taskTimeout.Start();
                                                taskTimeout.Stop();

                                                DirectoryInfo reportTargetPathDirInfo;

                                                try
                                                {
                                                    var targetReportDir = variablesFromArgs["REPORTTARGETPATH"];
                                                    var sourceTemplateFile = variablesFromArgs["REPORTTEMPLATEFILE"];
                                                    reportTargetPathDirInfo = new DirectoryInfo(Path.GetDirectoryName(targetReportDir));

                                                }
                                                catch
                                                {
                                                    reportTargetPathDirInfo = null;
                                                }

                                                if (csVM.SelectedExecuteConditionErrorLevel == ERROR.Empty)
                                                {
                                                    if (string.IsNullOrEmpty(reportOutputPath))
                                                    {
                                                        var dlg = new SaveFileDialogViewModel
                                                        {
                                                            Title = ResourceLoader.GetResource("windowCaptionSaveReport"),
                                                            Filter = ResourceLoader.GetResource("filterStringSaveReport"),
                                                            ParentWindow = this.mw,
                                                            InitialDirectory = reportTargetPathDirInfo != null ?
                                                            (reportTargetPathDirInfo.Exists ? reportTargetPathDirInfo.FullName : null) : null
                                                        };

                                                        if (dlg.Show(Dialogs) && dlg.FileName != null)
                                                        {
                                                            reportOutputPath = dlg.FileName;
                                                        }
                                                    }

                                                    if (reportReaderWriter == null)
                                                    {
                                                        reportReaderWriter = new ReportReaderWriter();
                                                    }

                                                    reportReaderWriter.ReportOutputPath = reportOutputPath;
                                                    if (!string.IsNullOrEmpty(reportTemplateFile))
                                                    {
                                                        reportReaderWriter.ReportTemplateFile = reportTemplateFile;
                                                    }

                                                    await csVM.WriteReportCommand.ExecuteAsync(reportReaderWriter);
                                                }

                                                else
                                                {
                                                    // targeted ERRORLEVEL ist not "EMPTY" so check if targeted ERRORLEVEL fits current ERRORLEVEL
                                                    if (taskDictionary.TryGetValue(csVM.SelectedExecuteConditionTaskIndex, out var targetTaskIndex))
                                                    {
                                                        if (taskHandler.TaskCollection[targetTaskIndex].CurrentTaskErrorLevel == csVM.SelectedExecuteConditionErrorLevel)
                                                        {
                                                            if (string.IsNullOrEmpty(reportOutputPath))
                                                            {

                                                                var dlg = new SaveFileDialogViewModel
                                                                {
                                                                    Title = ResourceLoader.GetResource("windowCaptionSaveReport"),
                                                                    Filter = ResourceLoader.GetResource("filterStringSaveReport"),
                                                                    InitialDirectory = reportTargetPathDirInfo != null ?
                                                                    (reportTargetPathDirInfo.Exists ? reportTargetPathDirInfo.FullName : null) : null
                                                                };

                                                                if (dlg.Show(Dialogs) && dlg.FileName != null)
                                                                {
                                                                    reportOutputPath = dlg.FileName;
                                                                }
                                                            }

                                                            if (reportReaderWriter == null)
                                                            {
                                                                reportReaderWriter = new ReportReaderWriter();
                                                            }

                                                            reportReaderWriter.ReportOutputPath = reportOutputPath;
                                                            if (!string.IsNullOrEmpty(reportTemplateFile))
                                                            {
                                                                reportReaderWriter.ReportTemplateFile = reportTemplateFile;
                                                            }

                                                            await csVM.WriteReportCommand.ExecuteAsync(reportReaderWriter);
                                                        }
                                                        else
                                                        {
                                                            currentTaskIndex++;
                                                        }
                                                    }
                                                }

                                                taskTimeout.Start();
                                                break;

                                            default:
                                                currentTaskIndex++;
                                                taskTimeout.Stop();
                                                taskTimeout.Start();
                                                break;
                                        }
                                        break;

                                    case TaskType_CommonTask.CheckLogicCondition:
                                        switch (csVM.CurrentTaskErrorLevel)
                                        {
                                            case ERROR.NotReadyError:
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.Empty:
                                                csVM.CurrentTaskErrorLevel = ERROR.NotReadyError;
                                                taskTimeout.Start();

                                                if (csVM.SelectedExecuteConditionErrorLevel == ERROR.Empty)
                                                {

                                                    await csVM.CheckLogicCondition.ExecuteAsync(taskHandler.TaskCollection);
                                                }

                                                else
                                                {
                                                    // targeted ERRORLEVEL ist not "EMPTY" so check if targeted ERRORLEVEL fits current ERRORLEVEL
                                                    if (taskDictionary.TryGetValue(csVM.SelectedExecuteConditionTaskIndex, out var targetTaskIndex))
                                                    {
                                                        if (taskHandler.TaskCollection[targetTaskIndex].CurrentTaskErrorLevel == csVM.SelectedExecuteConditionErrorLevel)
                                                        {

                                                            await csVM.CheckLogicCondition.ExecuteAsync(taskHandler.TaskCollection);
                                                        }
                                                        else
                                                        {
                                                            currentTaskIndex++;
                                                        }
                                                    }
                                                }
                                                break;

                                            default:
                                                currentTaskIndex++;
                                                taskTimeout.Stop();
                                                taskTimeout.Start();
                                                break;
                                        }
                                        break;

                                    case TaskType_CommonTask.ExecuteProgram:
                                        switch (csVM.CurrentTaskErrorLevel)
                                        {
                                            case ERROR.NotReadyError:
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.Empty:
                                                csVM.CurrentTaskErrorLevel = ERROR.NotReadyError;
                                                taskTimeout.Start();

                                                if (csVM.SelectedExecuteConditionErrorLevel == ERROR.Empty)
                                                {
                                                    csVM.ExecuteProgramCommand.Execute(null);
                                                }

                                                else
                                                {
                                                    // targeted ERRORLEVEL ist not "EMPTY" so check if targeted ERRORLEVEL fits current ERRORLEVEL
                                                    if (taskDictionary.TryGetValue(csVM.SelectedExecuteConditionTaskIndex, out var targetTaskIndex))
                                                    {
                                                        if (taskHandler.TaskCollection[targetTaskIndex].CurrentTaskErrorLevel == csVM.SelectedExecuteConditionErrorLevel)
                                                        {
                                                            csVM.ExecuteProgramCommand.Execute(null);
                                                        }
                                                        else
                                                        {
                                                            currentTaskIndex++;
                                                        }
                                                    }
                                                }
                                                break;

                                            default:
                                                currentTaskIndex++;
                                                taskTimeout.Stop();
                                                taskTimeout.Start();
                                                break;
                                        }
                                        break;


                                    default:
                                        break;
                                }
                                break;

                            case GenericChipTaskViewModel csVM:
                                switch (csVM.SelectedTaskType)
                                {
                                    case TaskType_GenericChipTask.ChipIsOfType:
                                        taskTimeout.Start();
                                        switch (csVM.CurrentTaskErrorLevel)
                                        {
                                            case ERROR.Empty:
                                                csVM.IsFocused = true;

                                                if (csVM.SelectedExecuteConditionErrorLevel == ERROR.Empty)
                                                {
                                                    IsReaderBusy = true;
                                                    await csVM.CheckChipType.ExecuteAsync(device.GenericChip);
                                                }
                                                else
                                                {
                                                    // targeted ERRORLEVEL ist not "EMPTY" so check if targeted ERRORLEVEL fits current ERRORLEVEL
                                                    if (taskDictionary.TryGetValue(csVM.SelectedExecuteConditionTaskIndex, out var targetTaskIndex))
                                                    {
                                                        if (taskHandler.TaskCollection[targetTaskIndex].CurrentTaskErrorLevel == csVM.SelectedExecuteConditionErrorLevel)
                                                        {
                                                            IsReaderBusy = true;
                                                            await csVM.CheckChipType.ExecuteAsync(device.GenericChip);
                                                        }
                                                        else
                                                        {
                                                            currentTaskIndex++;
                                                        }
                                                    }
                                                }

                                                IsReaderBusy = false;
                                                break;

                                            default:
                                                csVM.IsFocused = false;
                                                currentTaskIndex++;
                                                break;
                                        }
                                        break;

                                    case TaskType_GenericChipTask.ChipIsMultiChip:
                                        taskTimeout.Start();
                                        switch (csVM.CurrentTaskErrorLevel)
                                        {
                                            case ERROR.Empty:
                                                csVM.IsFocused = true;

                                                if (csVM.SelectedExecuteConditionErrorLevel == ERROR.Empty)
                                                {
                                                    IsReaderBusy = true;
                                                    await csVM.CheckChipIsMultiTecChip.ExecuteAsync(device.GenericChip);
                                                }
                                                else
                                                {
                                                    // targeted ERRORLEVEL ist not "EMPTY" so check if targeted ERRORLEVEL fits current ERRORLEVEL
                                                    if (taskDictionary.TryGetValue(csVM.SelectedExecuteConditionTaskIndex, out var targetTaskIndex))
                                                    {
                                                        if (taskHandler.TaskCollection[targetTaskIndex].CurrentTaskErrorLevel == csVM.SelectedExecuteConditionErrorLevel)
                                                        {
                                                            IsReaderBusy = true;
                                                            await csVM.CheckChipIsMultiTecChip.ExecuteAsync(device.GenericChip);
                                                        }
                                                        else
                                                        {
                                                            currentTaskIndex++;
                                                        }
                                                    }
                                                }

                                                IsReaderBusy = false;
                                                break;

                                            default:
                                                csVM.IsFocused = false;
                                                currentTaskIndex++;
                                                break;
                                        }
                                        break;
                                    default:
                                        break;
                                }
                                break;

                            case MifareClassicSetupViewModel csVM:
                                switch (csVM.CurrentTaskErrorLevel)
                                {
                                    case ERROR.NotReadyError:
                                        taskTimeout.Start();
                                        break;

                                    case ERROR.Empty:
                                        csVM.CurrentTaskErrorLevel = ERROR.NotReadyError;
                                        taskTimeout.Start();

                                        if (csVM.SelectedExecuteConditionErrorLevel == ERROR.Empty)
                                        {
                                            IsReaderBusy = true;
                                            await csVM.CommandDelegator.ExecuteAsync(csVM.SelectedTaskType);
                                        }

                                        else
                                        {
                                            // targeted ERRORLEVEL ist not "EMPTY" so check if targeted ERRORLEVEL fits current ERRORLEVEL
                                            if (taskDictionary.TryGetValue(csVM.SelectedExecuteConditionTaskIndex, out var targetTaskIndex))
                                            {
                                                if (taskHandler.TaskCollection[targetTaskIndex].CurrentTaskErrorLevel == csVM.SelectedExecuteConditionErrorLevel)
                                                {
                                                    IsReaderBusy = true;
                                                    await csVM.CommandDelegator.ExecuteAsync(csVM.SelectedTaskType);
                                                }
                                                else
                                                {
                                                    currentTaskIndex++;
                                                }
                                            }
                                        }

                                        IsReaderBusy = false;
                                        break;

                                    default:
                                        currentTaskIndex++;
                                        taskTimeout.Stop();
                                        taskTimeout.Start();
                                        break;
                                }
                                break;

                            case MifareDesfireSetupViewModel csVM:
                                switch (csVM.CurrentTaskErrorLevel)
                                {
                                    case ERROR.NotReadyError:
                                        taskTimeout.Start();
                                        break;

                                    case ERROR.Empty:
                                        csVM.CurrentTaskErrorLevel = ERROR.NotReadyError;
                                        taskTimeout.Start();

                                        if (csVM.SelectedExecuteConditionErrorLevel == ERROR.Empty)
                                        {
                                            IsReaderBusy = true;
                                            await csVM.CommandDelegator.ExecuteAsync(csVM.SelectedTaskType);
                                        }

                                        else
                                        {
                                            // targeted ERRORLEVEL ist not "EMPTY" so check if targeted ERRORLEVEL fits current ERRORLEVEL
                                            if (taskDictionary.TryGetValue(csVM.SelectedExecuteConditionTaskIndex, out var targetTaskIndex))
                                            {
                                                if (taskHandler.TaskCollection[targetTaskIndex].CurrentTaskErrorLevel == csVM.SelectedExecuteConditionErrorLevel)
                                                {
                                                    IsReaderBusy = true;
                                                    await csVM.CommandDelegator.ExecuteAsync(csVM.SelectedTaskType);
                                                }
                                                else
                                                {
                                                    currentTaskIndex++;
                                                }
                                            }
                                        }

                                        IsReaderBusy = false;
                                        break;

                                    default:
                                        currentTaskIndex++;
                                        taskTimeout.Stop();
                                        taskTimeout.Start();
                                        break;
                                }
                                break;

                            case MifareUltralightSetupViewModel ssVM:
                                break;

                            default:
                                break;
                        }

                        if (_runSelectedOnly)
                        {
                            break;
                        }

                        OnPropertyChanged(nameof(TreeViewParentNodes));
                    }
                }
                OnPropertyChanged(nameof(TreeViewParentNodes));

                taskTimeout.Stop();

            }
            catch (Exception e)
            {
                eventLog.WriteEntry(e.Message, EventLogEntryType.Error);
            }

            try
            {
                if(treeViewParentNodes.Any(x => x.IsSelected) == true)
                {
                    treeViewParentNodes.First(y => y.IsSelected).IsBeingProgrammed = null;
                    triggerReadChip.IsEnabled = (bool)triggerReadChip.Tag;
                    _runSelectedOnly = false;
                }
            }

            catch { 
            // do nothing if no element found. this is intended on autorun, to speed up
            }


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
        private async Task OnNewReaderSetupDialog()
        {
            using (var settings = new SettingsReaderWriter())
            {
                var currentSettings = settings.DefaultSpecification;

                ReaderDevice.PortNumber = int.TryParse(currentSettings.LastUsedComPort, out var portNumber) ? portNumber : 0;
                ReaderDevice.Reader = currentSettings.DefaultReaderProvider;

                using (var device = ReaderDevice.Instance)
                {
                    Dialogs.Add(new SetupViewModel(device)
                    {
                        Caption = ResourceLoader.GetResource("windowCaptionReaderSetup"),

                        OnOk = (sender) =>
                        {
                            currentSettings.DefaultReaderProvider = sender.SelectedReader;
                            currentSettings.AutoLoadProjectOnStart = sender.LoadOnStart;
                            currentSettings.LastUsedComPort = sender.ComPort;
                            currentSettings.AutoCheckForUpdates = sender.CheckOnStart;
                            currentSettings.LastUsedBaudRate = sender.SelectedBaudRate;

                            settings.DefaultSpecification = currentSettings;

                            sender.SaveSettings.ExecuteAsync(null);

                            CurrentReader = Enum.GetName(typeof(ReaderTypes), sender.SelectedReader);

                            ReaderDevice.Reader = (ReaderTypes)Enum.Parse(typeof(ReaderTypes), CurrentReader);

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
                                updateChecker?.StartMonitoringAsync().GetAwaiter().GetResult();
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
            }
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
        private async Task OnNewSaveTaskDialogCommand()
        {
            using (var settings = new SettingsReaderWriter())
            {
                databaseReaderWriter.WriteDatabase(ChipTasks, settings.DefaultSpecification.LastUsedProjectPath);
            }
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
        private async Task OnNewSaveChipDialogCommand()
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
        private async void CheckUpdate(object sender)
        {
            if (updateChecker.UpdateAvailable)
            {
                await AskForUpdateNow();
            }
        }
        private async Task OnNewCheckUpdateCommand()
        {
            userIsNotifiedForAvailableUpdate = false;

            if (updateChecker.UpdateAvailable)
            {
                await AskForUpdateNow();
            }
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

        private readonly ObservableCollection<MenuItem> emptySpaceTreeViewContextMenu;
        /// <summary>
        /// expose contextmenu on row click
        /// </summary>
        public ObservableCollection<MenuItem> RowContextMenu => rowContextMenuItems;
        private readonly ObservableCollection<MenuItem> rowContextMenuItems;

        /// <summary>
        /// expose contextmenu on empty space click
        /// </summary>
        public ObservableCollection<MenuItem> EmptySpaceContextMenu => emptySpaceContextMenuItems;
        private readonly ObservableCollection<MenuItem> emptySpaceContextMenuItems;


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
        public bool IsWriteToAllChipAutoChecked => triggerReadChip.IsEnabled;

        /// <summary>
        /// 
        /// </summary>
        public bool IsWriteSelectedToChipAutoChecked => triggerReadChip.IsEnabled;
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
                    pollingScheduler?.PauseReader();
                }
                else
                {
                    pollingScheduler?.ResumeReader();
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
                        updateChecker.StartMonitoringAsync().GetAwaiter().GetResult();
                    }
                    else
                    {
                        updateChecker.StopMonitoringAsync().GetAwaiter().GetResult();
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
                    Dialogs.Add(new UpdateNotifierViewModel(updateChecker.UpdateInfoText)
                    {
                        Caption = updateDisabled ? "Changelog" : "Update Available",

                        OnOk = async (updateAction) =>
                        {
                            if (!updateDisabled)
                            {
                                Mouse.OverrideCursor = Cursors.AppStarting;
                                await updateChecker.ApplyUpdateAsync();
                            }
                            updateAction.Close();
                        },

                        OnCancel = (updateAction) =>
                        {
                            updateChecker.AllowUpdate = false;
                            updateAction.Close();
                            mw.Activate();
                        },

                        OnCloseRequest = (updateAction) =>
                        {
                            updateChecker.AllowUpdate = false;
                            updateAction.Close();
                            mw.Activate();
                        }
                    });
                }));
            }

            userIsNotifiedForAvailableUpdate = true;
        }

        //Only one instance is allowed due to the singleton pattern of the reader class
        private void RunMutex(object sender, StartupEventArgs e)
        {
            mutex = new Mutex(true, "App", out var isANewInstance);

            if (!isANewInstance)
            {
                Environment.Exit(0);
            }
        }

        private void CloseThreads(object sender, CancelEventArgs e)
        {
            Environment.Exit(0);
        }

        private async void LoadCompleted(object sender, EventArgs e)
        {
            var autorun = false;

            Application.Current.MainWindow.Activated -= new EventHandler(LoadCompleted);

            mw = (MainWindow)Application.Current.MainWindow;
            mw.Title = string.Format("RFiDGear {0}.{1}", Version.Major, Version.Minor);
            var projectFileToUse = "";

            using (var settings = new SettingsReaderWriter())
            {
                if (args.Length > 1)
                {
                    foreach (var arg in args)
                    {
                        switch (arg.Split('=')[0])
                        {
                            case "REPORTTARGETPATH":

                                variablesFromArgs.Add(arg.Split('=')[0], arg.Split('=')[1]);

                                if (Directory.Exists(Path.GetDirectoryName(arg.Split('=')[1])))
                                {
                                    reportOutputPath = arg.Split('=')[1];
                                    var numbersInFileNames = new int[Directory.GetFiles(Path.GetDirectoryName(reportOutputPath)).Length];

                                    if (reportOutputPath.Contains("?"))
                                    {
                                        for (var i = 0; i < numbersInFileNames.Length; i++)
                                        {
                                            var fileName = Directory.GetFiles(Path.GetDirectoryName(reportOutputPath))[i];

                                            if (fileName.Replace(".pdf", string.Empty).ToLower().Contains(reportOutputPath.ToLower().Replace("?", string.Empty).Replace(".pdf", string.Empty)))
                                            {
                                                _ = int.TryParse(fileName.ToLower().Replace(
                                                    reportOutputPath.ToLower().Replace("?", string.Empty).Replace(".pdf", string.Empty), string.Empty).Replace(".pdf", string.Empty), out var n);
                                                numbersInFileNames[i] = n;
                                            }
                                        }
                                    }

                                    if (reportOutputPath.Contains("???"))
                                    {
                                        reportOutputPath = reportOutputPath.Replace("???", string.Format("{0:D3}", numbersInFileNames.Max() + 1));
                                    }

                                    else if (reportOutputPath.Contains("??"))
                                    {
                                        reportOutputPath = reportOutputPath.Replace("??", string.Format("{0:D2}", numbersInFileNames.Max() + 1));
                                    }

                                    else if (reportOutputPath.Contains("?"))
                                    {
                                        reportOutputPath = reportOutputPath.Replace("?", string.Format("{0:D1}", numbersInFileNames.Max() + 1));
                                    }
                                }
                                break;

                            case "REPORTTEMPLATEFILE":

                                variablesFromArgs.Add(arg.Split('=')[0], arg.Split('=')[1]);

                                if (File.Exists(arg.Split('=')[1]))
                                {
                                    reportTemplateFile = arg.Split('=')[1];
                                }
                                break;

                            case "AUTORUN":
                                if (arg.Split('=')[1] == "1")
                                {
                                    autorun = true;
                                }
                                break;

                            case "LASTUSEDPROJECTPATH":
                                if (File.Exists(arg.Split('=')[1]))
                                {
                                    settings.DefaultSpecification.LastUsedProjectPath = new DirectoryInfo(arg.Split('=')[1]).FullName;
                                    await settings.SaveSettings();
                                }
                                break;

                            case "CUSTOMPROJECTFILE":

                                if (File.Exists(arg.Split('=')[1]))
                                {
                                    projectFileToUse = new DirectoryInfo(arg.Split('=')[1]).FullName;
                                }
                                break;

                            default:
                                if (arg.Split('=')[0].Contains("$"))
                                {
                                    variablesFromArgs.Add(arg.Split('=')[0], arg.Split('=')[1]);
                                }
                                break;
                        }
                    }
                }
            }

            await InitOnFirstRun(projectFileToUse);

            if (autorun)
            {
                await OnNewReadChipCommand();
                await OnNewWriteToChipOnceCommand();
            }
        }

        private async Task InitOnFirstRun(string projectFileToUse)
        {
            try
            {
                using (var settings = new SettingsReaderWriter())
                {
                    await settings.ReadSettings();

                    settings.InitUpdateFile();

                    var setup = readerInitializer.ApplySettings(settings);
                    CurrentReader = setup.ReaderName;
                    culture = setup.Culture;

                    var autoLoadLastUsedDB = settings.DefaultSpecification.AutoLoadProjectOnStart;

                    var mySplash = new SplashScreenViewModel();

                    if (autoLoadLastUsedDB)
                    {
                        Dialogs.Add(mySplash);
                    }

                    if (autoLoadLastUsedDB)
                    {
                        if (string.IsNullOrEmpty(projectFileToUse))
                        {
                            await OpenLastProjectFile();
                        }
                        else
                        {
                            await OpenLastProjectFile(projectFileToUse);
                        }
                    }

                    Task.Run(async () =>
                    {
                        while (true)
                        {
                            await Task.Delay(300);
                            DateTimeStatusBar = string.Format("{0}", DateTime.Now);
                        }
                    });

                    await OnNewResetTaskStatusCommand();
                }
            }
            catch (Exception ex)
            {
                eventLog.WriteEntry(ex.Message, EventLogEntryType.Error);
            }
        }

        private async void CheckReader(object sender)
        {
            var status = await readerInitializer.RefreshReaderStatusAsync(IsReaderBusy);
            IsReaderBusy = status.IsBusy;
            CurrentReader = status.CurrentReader;
        }

        private bool ContainsAny(string haystack, params string[] needles)
        {
            return needles.Any(haystack.Contains);
        }

        #endregion
    }
}