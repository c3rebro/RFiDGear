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

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Log4CSharp;

using MefMvvm.SharedContracts.ViewModel;

using MvvmDialogs.ViewModels;

using RedCell.Diagnostics.Update;

using RFiDGear.DataAccessLayer;
using RFiDGear.DataAccessLayer.Remote.FromIO;
using RFiDGear.Model;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace RFiDGear.ViewModel
{
    /// <summary>
    /// Description of MainWindowViewModel.
    /// </summary>
    [ExportViewModel("MainWin")]
    public class MainWindowViewModel : ObservableObject
    {
        private static readonly string FacilityName = "RFiDGear";

        private readonly Version Version = Assembly.GetExecutingAssembly().GetName().Version;
        private readonly string[] args;
        private readonly Dictionary<string, string> variablesFromArgs = new Dictionary<string, string>();
        private readonly Updater updater;

        private protected MainWindow mw;
        private protected DatabaseReaderWriter databaseReaderWriter;
        private protected ReportReaderWriter reportReaderWriter;
        private protected DispatcherTimer triggerReadChip;
        private protected DispatcherTimer taskTimeout;
        private protected string reportOutputPath;
        private protected ChipTaskHandlerModel taskHandler; 
        private protected List<MifareClassicChipModel> mifareClassicUidModels = new List<MifareClassicChipModel>();
        private protected List<MifareDesfireChipModel> mifareDesfireViewModels = new List<MifareDesfireChipModel>();
        private protected bool _runSelectedOnly;

        private int currentTaskIndex = 0;
        // set if task was completed; indicates greenlight to continue execution
        // if programming takes too long; quit the process
        private bool firstRun = true;
        private bool _isLoadingProject = true;
        private bool userIsNotifiedForAvailableUpdate = false;
        private bool updateIsAvailable = false;
        private protected Mutex mutex;

        // one reader, one instance - only

        #region Events / Delegates

        /// <summary>
        /// will raise notifier to inform user about available updates
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public delegate void updateReady(object sender, EventArgs e);
        #endregion

        #region Constructors

        public MainWindowViewModel()
        {
            RunMutex(this, null);

            bool autoLoadLastUsedDB;
            args = Environment.GetCommandLineArgs();

            using (var settings = new SettingsReaderWriter())
            {
                updater = new Updater();

                CurrentReader = string.IsNullOrWhiteSpace(settings.DefaultSpecification.DefaultReaderName)
                    ? Enum.GetName(typeof(ReaderTypes), settings.DefaultSpecification.DefaultReaderProvider)
                    : settings.DefaultSpecification.DefaultReaderName;

                if (!string.IsNullOrEmpty(CurrentReader))
                {
                    ReaderDevice.Reader = (ReaderTypes)Enum.Parse(typeof(ReaderTypes), CurrentReader);
                }
                culture = (settings.DefaultSpecification.DefaultLanguage == "german") ? new CultureInfo("de-DE") : new CultureInfo("en-US");

                autoLoadLastUsedDB = settings.DefaultSpecification.AutoLoadProjectOnStart;
            }

            triggerReadChip = new DispatcherTimer
            {
                Interval = new TimeSpan(0, 0, 0, 2, 500)
            };

            triggerReadChip.Tick += UpdateChip;

            triggerReadChip.Start();
            triggerReadChip.IsEnabled = false;
            triggerReadChip.Tag = triggerReadChip.IsEnabled;

#if DEBUG
            taskTimeout = new DispatcherTimer
            {
                Interval = new TimeSpan(0, 1, 0, 0, 0)
            };
#else
            taskTimeout = new DispatcherTimer
            {
                Interval = new TimeSpan(0, 0, 0, 4, 0)
            };
#endif
            taskTimeout.Tick += TaskTimeout;
            taskTimeout.Start();
            taskTimeout.IsEnabled = false;

            treeViewParentNodes = new ObservableCollection<RFiDChipParentLayerViewModel>();

            taskHandler = new ChipTaskHandlerModel();

            ReaderStatus = CurrentReader == "None" ? "" : "ready";
            DateTimeStatusBar = "";
            databaseReaderWriter = new DatabaseReaderWriter();
            resLoader = new ResourceLoader();

            rowContextMenuItems = new ObservableCollection<MenuItem>();
            emptySpaceContextMenuItems = new ObservableCollection<MenuItem>();
            emptySpaceTreeViewContextMenu = new ObservableCollection<MenuItem>();

            emptySpaceContextMenuItems.Add(new MenuItem()
            {
                Header = ResourceLoader.GetResource("contextMenuItemAddNewTask"),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Command = GetAddEditCommand
            });

            rowContextMenuItems.Add(new MenuItem()
            {
                Header = ResourceLoader.GetResource("contextMenuItemAddOrEditTask"),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Command = GetAddEditCommand
            });

            rowContextMenuItems.Add(new MenuItem()
            {
                Header = ResourceLoader.GetResource("contextMenuItemDeleteSelectedItem"),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Command = new RelayCommand(() =>
                {
                    taskHandler.TaskCollection.Remove(SelectedSetupViewModel);
                })
            });

            rowContextMenuItems.Add(null);

            rowContextMenuItems.Add(new MenuItem()
            {
                Header = ResourceLoader.GetResource("contextMenuItemExecuteSelectedItem"),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Command = WriteSelectedTaskToChipOnceCommand
            });

            rowContextMenuItems.Add(new MenuItem()
            {
                Header = ResourceLoader.GetResource("contextMenuItemResetSelectedItem"),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Command = ResetSelectedTaskStatusCommand
            });

            rowContextMenuItems.Add(null);

            rowContextMenuItems.Add(new MenuItem()
            {
                Header = ResourceLoader.GetResource("contextMenuItemExecuteAllItems"),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Command = WriteToChipOnceCommand
            });

            rowContextMenuItems.Add(new MenuItem()
            {
                Header = ResourceLoader.GetResource("contextMenuItemResetReportPath"),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Command = ResetReportTaskDirectoryCommand
            });

            emptySpaceTreeViewContextMenu.Add(new MenuItem()
            {
                Header = ResourceLoader.GetResource("contextMenuItemReadChipPublic"),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Command = ReadChipCommand
            });

            Application.Current.MainWindow.Closing += new CancelEventHandler(CloseThreads);
            Application.Current.MainWindow.Activated += new EventHandler(LoadCompleted);
            updater.NewVersionAvailable += new EventHandler(EnableUpdate);

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
        public string LocalizationResourceSet { get; set; }

        #endregion Localization

        #region Local Commands

        public ICommand GetAddEditCommand => new RelayCommand(OnNewGetAddEditCommand);
        private void OnNewGetAddEditCommand()
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
        }

        private void OpenLastProjectFile()
        {
            using (var settings = new SettingsReaderWriter())
            {
                var autoLoadLastUsedDB = settings.DefaultSpecification.AutoLoadProjectOnStart;
                var lastUsedDBPath = settings.DefaultSpecification.LastUsedProjectPath;

                culture = (settings.DefaultSpecification.DefaultLanguage == "german") ? new CultureInfo("de-DE") : new CultureInfo("en-US");

                if (ChipTasks.TaskCollection != null && ChipTasks.TaskCollection.Count > 0)
                {
                    ChipTasks.TaskCollection.Clear();
                }

                databaseReaderWriter.ReadDatabase(lastUsedDBPath);

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
        private void UpdateChip(object sender, EventArgs args)
        {
            GenericChipModel GenericChip;

            try
            {
                Mouse.OverrideCursor = Cursors.AppStarting;

                //try to get singleton instance
                using (var device = ReaderDevice.Instance)
                {
                    //reader was ready - proceed
                    if (device != null)
                    {
                        ReaderStatus = "busy";
                        device.ReadChipPublic();

                        GenericChip = device.GenericChip;

                        ReaderStatus = "ready";
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
                    OnNewReadChipCommand();
                    OnNewResetTaskStatusCommand();
                    OnNewWriteToChipOnceCommand();
                }

                Mouse.OverrideCursor = null;
            }
            catch (Exception e)
            {
                Mouse.OverrideCursor = null;

                LogWriter.CreateLogEntry(e, FacilityName);
            }
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

            (taskHandler.TaskCollection[currentTaskIndex] as IGenericTaskModel).IsTaskCompletedSuccessfully = false;
#else
            taskTimeout.IsEnabled = false;
            taskTimeout.Stop();

            (taskHandler.TaskCollection[(int)taskTimeout.Tag] as IGenericTaskModel).IsTaskCompletedSuccessfully = false;

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
        public ICommand CreateGenericChipTaskCommand => new RelayCommand(OnNewCreateGenericChipTaskCommand);
        private void OnNewCreateGenericChipTaskCommand()
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

                            OnOk = (sender) =>
                            {
                                if (sender.SelectedTaskType == TaskType_GenericChipTask.ChangeDefault)
                                {
                                    sender.Settings.SaveSettings();
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
                }
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(e, FacilityName);

                dialogs.Clear();
            }

            Mouse.OverrideCursor = null;

            triggerReadChip.IsEnabled = timerState;

            OnPropertyChanged(nameof(ChipTasks));
        }

        /// <summary>
        /// Create a new "Common" Task of Type "Report Creator"
        /// </summary>
        public ICommand CreateGenericTaskCommand => new RelayCommand(OnNewNewCreateReportTaskCommand);
        private void OnNewNewCreateReportTaskCommand()
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
                LogWriter.CreateLogEntry(e, FacilityName);

                dialogs.Clear();
            }

            Mouse.OverrideCursor = null;

            triggerReadChip.IsEnabled = timerState;

            OnPropertyChanged(nameof(ChipTasks));
        }

        /// <summary>
        /// Creates a new Task of Type Mifare Classic Card
        /// </summary>
        public ICommand CreateClassicTaskCommand => new RelayCommand(OnNewCreateClassicTaskCommand);
        private void OnNewCreateClassicTaskCommand()
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
                                    sender.Settings.SaveSettings();
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
                }
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(e, FacilityName);

                dialogs.Clear();
            }

            Mouse.OverrideCursor = null;

            triggerReadChip.IsEnabled = timerState;
        }

        /// <summary>
        /// 
        /// </summary>
        public ICommand CreateDesfireTaskCommand => new RelayCommand(OnNewCreateDesfireTaskCommand);
        private void OnNewCreateDesfireTaskCommand()
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
                                sender.Settings.SaveSettings();
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

                                ChipTasks.TaskCollection = new ObservableCollection<object>(ChipTasks.TaskCollection.OrderBy(x => (x as IGenericTaskModel).SelectedTaskIndexAsInt));

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

                                ChipTasks.TaskCollection = new ObservableCollection<object>(ChipTasks.TaskCollection.OrderBy(x => (x as IGenericTaskModel).SelectedTaskIndexAsInt));

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
        public ICommand ExecuteQuickCheckCommand => new RelayCommand(OnNewExecuteQuickCheckCommand);
        private void OnNewExecuteQuickCheckCommand()
        {
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;

                ReadChipCommand.Execute(null);

                if (treeViewParentNodes != null && treeViewParentNodes.Any(x => x.IsSelected) && treeViewParentNodes.Count > 0)
                {
                    treeViewParentNodes.FirstOrDefault().IsSelected = true;

                    if (treeViewParentNodes.Single(x => x.IsSelected == true).CardType == CARD_TYPE.Mifare1K ||
                        treeViewParentNodes.Single(x => x.IsSelected == true).CardType == CARD_TYPE.Mifare2K ||
                        treeViewParentNodes.Single(x => x.IsSelected == true).CardType == CARD_TYPE.Mifare4K)
                    {
                        treeViewParentNodes.Single(x => x.IsSelected == true).ExecuteClassicQuickCheckCommand.Execute(null);
                    } // Mifare Classic

                    else if (Enum.GetName(typeof(CARD_TYPE), treeViewParentNodes.Single(x => x.IsSelected == true).CardType).ToLower(CultureInfo.CurrentCulture).Contains("desfire"))
                    {
                        treeViewParentNodes.Single(x => x.IsSelected == true).ExecuteDesfireQuickCheckCommand.Execute(null);
                    } // Mifare Desfire
                }

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
        public ICommand ReadChipCommand => new RelayCommand(OnNewReadChipCommand);
        private void OnNewReadChipCommand()
        {
            var timerState = triggerReadChip.IsEnabled;

            triggerReadChip.IsEnabled = false;

            Mouse.OverrideCursor = Cursors.Wait;
            ReaderStatus = "busy";

            using (var device = ReaderDevice.Instance)
            {
                foreach (var item in treeViewParentNodes)
                {
                    item.IsExpanded = false;
                }

                if (device?.ReadChipPublic() == ERROR.NoError &&
                    !treeViewParentNodes.Any(x => x.UID == device.GenericChip.UID))
                {
                    // fill treeview with dummy models and viewmodels
                    switch (device.GenericChip.CardType)
                    {
                        case CARD_TYPE.Mifare1K:
                            treeViewParentNodes.Add(new RFiDChipParentLayerViewModel(new MifareClassicChipModel(device.GenericChip.UID, CARD_TYPE.Mifare1K), Dialogs, false));
                            break;

                        case CARD_TYPE.Mifare2K:
                            treeViewParentNodes.Add(new RFiDChipParentLayerViewModel(new MifareClassicChipModel(device.GenericChip.UID, CARD_TYPE.Mifare2K), Dialogs, false));
                            break;

                        case CARD_TYPE.Mifare4K:
                            treeViewParentNodes.Add(new RFiDChipParentLayerViewModel(new MifareClassicChipModel(device.GenericChip.UID, CARD_TYPE.Mifare4K), Dialogs, false));
                            break;

                        case CARD_TYPE.DESFire:
                        case CARD_TYPE.DESFireEV1:
                        case CARD_TYPE.DESFireEV2:
                        case CARD_TYPE.DESFireEV3:
                        case CARD_TYPE.DESFire_256:
                        case CARD_TYPE.DESFire_2K:
                        case CARD_TYPE.DESFire_4K:
                        case CARD_TYPE.DESFireEV1_256:
                        case CARD_TYPE.DESFireEV1_2K:
                        case CARD_TYPE.DESFireEV1_4K:
                        case CARD_TYPE.DESFireEV1_8K:
                        case CARD_TYPE.DESFireEV2_2K:
                        case CARD_TYPE.DESFireEV2_4K:
                        case CARD_TYPE.DESFireEV2_8K:
                        case CARD_TYPE.DESFireEV2_16K:
                        case CARD_TYPE.DESFireEV2_32K:
                        case CARD_TYPE.DESFireEV3_2K:
                        case CARD_TYPE.DESFireEV3_4K:
                        case CARD_TYPE.DESFireEV3_8K:
                        case CARD_TYPE.DESFireEV3_16K:
                        case CARD_TYPE.DESFireEV3_32K:
                        case CARD_TYPE.SmartMX_DESFire_Generic:
                        case CARD_TYPE.SmartMX_DESFire_2K:
                        case CARD_TYPE.SmartMX_DESFire_4K:
                        case CARD_TYPE.SmartMX_DESFire_8K:
                        case CARD_TYPE.SmartMX_DESFire_16K:
                        case CARD_TYPE.SmartMX_DESFire_32K:
                            treeViewParentNodes.Add(new RFiDChipParentLayerViewModel(new MifareDesfireChipModel(device.GenericChip), Dialogs, false));
                            break;

                        case CARD_TYPE.MifarePlus_SL1_1K:
                            treeViewParentNodes.Add(new RFiDChipParentLayerViewModel(new MifareClassicChipModel(device.GenericChip.UID, CARD_TYPE.MifarePlus_SL1_1K), Dialogs, false));
                            break;

                        case CARD_TYPE.MifarePlus_SL1_2K:
                            treeViewParentNodes.Add(new RFiDChipParentLayerViewModel(new MifareClassicChipModel(device.GenericChip.UID, CARD_TYPE.MifarePlus_SL1_2K), Dialogs, false));
                            break;

                        case CARD_TYPE.MifarePlus_SL1_4K:
                            treeViewParentNodes.Add(new RFiDChipParentLayerViewModel(new MifareClassicChipModel(device.GenericChip.UID, CARD_TYPE.MifarePlus_SL1_4K), Dialogs, false));
                            break;

                        case CARD_TYPE.MifarePlus_SL2_1K:
                            treeViewParentNodes.Add(new RFiDChipParentLayerViewModel(new MifareClassicChipModel(device.GenericChip.UID, CARD_TYPE.MifarePlus_SL2_1K), Dialogs, false));
                            break;

                        case CARD_TYPE.MifarePlus_SL2_2K:
                            treeViewParentNodes.Add(new RFiDChipParentLayerViewModel(new MifareClassicChipModel(device.GenericChip.UID, CARD_TYPE.MifarePlus_SL2_2K), Dialogs, false));
                            break;

                        case CARD_TYPE.MifarePlus_SL2_4K:
                            treeViewParentNodes.Add(new RFiDChipParentLayerViewModel(new MifareClassicChipModel(device.GenericChip.UID, CARD_TYPE.MifarePlus_SL2_4K), Dialogs, false));
                            break;

                        case CARD_TYPE.MifarePlus_SL3_1K:
                            treeViewParentNodes.Add(new RFiDChipParentLayerViewModel(new MifareClassicChipModel(device.GenericChip.UID, CARD_TYPE.MifarePlus_SL3_1K), Dialogs, false));
                            break;

                        case CARD_TYPE.MifarePlus_SL3_2K:
                            treeViewParentNodes.Add(new RFiDChipParentLayerViewModel(new MifareClassicChipModel(device.GenericChip.UID, CARD_TYPE.MifarePlus_SL3_2K), Dialogs, false));
                            break;

                        case CARD_TYPE.MifarePlus_SL3_4K:
                            treeViewParentNodes.Add(new RFiDChipParentLayerViewModel(new MifareClassicChipModel(device.GenericChip.UID, CARD_TYPE.MifarePlus_SL3_4K), Dialogs, false));
                            break;

                        case CARD_TYPE.MifareUltralight:
                            treeViewParentNodes.Add(new RFiDChipParentLayerViewModel(new MifareUltralightChipModel(device.GenericChip.UID, device.GenericChip.CardType), Dialogs, false));
                            break;


                        case CARD_TYPE.GENERIC_T_CL_A:
                            treeViewParentNodes.Add(new RFiDChipParentLayerViewModel(new MifareDesfireChipModel(device.GenericChip.UID, device.GenericChip.CardType), Dialogs, false));
                            break;

                        case CARD_TYPE.ISO15693:

                            break;

                        default:
                            treeViewParentNodes.Add(new RFiDChipParentLayerViewModel(new GenericChipModel(device.GenericChip.UID, device.GenericChip.CardType), Dialogs, false));
                            break;
                    }


                }
                else if (treeViewParentNodes.Any(x => x.UID == device?.GenericChip?.UID))
                {
                    treeViewParentNodes.First(x => x.UID == device.GenericChip.UID).IsSelected = true;
                }
            }

            Mouse.OverrideCursor = null;
            ReaderStatus = "ready";

            triggerReadChip.IsEnabled = timerState;
        }

        /// <summary>
        /// Reset all Task status information
        /// </summary>
        public ICommand ResetTaskStatusCommand => new RelayCommand(OnNewResetTaskStatusCommand);
        private void OnNewResetTaskStatusCommand()
        {
            foreach (IGenericTaskModel chipTask in taskHandler.TaskCollection)
            {
                chipTask.IsTaskCompletedSuccessfully = null;
                chipTask.CurrentTaskErrorLevel = ERROR.Empty;
            }
        }

        /// <summary>
        /// Reset all Reporttasks directory information
        /// </summary>
        public ICommand ResetReportTaskDirectoryCommand => new RelayCommand(OnNewResetReportTaskDirectoryCommand);
        private void OnNewResetReportTaskDirectoryCommand()
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
                            reportReaderWriter.ReportTemplatePath = null;
                            ssVM.CurrentTaskErrorLevel = ERROR.Empty;
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(e, FacilityName);
            }
        }

        /// <summary>
        /// Reset selected Task status information
        /// </summary>
        public ICommand ResetSelectedTaskStatusCommand => new RelayCommand(OnNewResetSelectedTaskStatusCommand);
        private void OnNewResetSelectedTaskStatusCommand()
        {
            (SelectedSetupViewModel as IGenericTaskModel).IsTaskCompletedSuccessfully = null;
            (SelectedSetupViewModel as IGenericTaskModel).CurrentTaskErrorLevel = ERROR.Empty;
        }

        /// <summary>
        /// Remove all Tasks from DataGrid
        /// </summary>
        public ICommand RemoveAllTasksCommand => new RelayCommand(OnNewRemoveAllTasksCommand);
        private void OnNewRemoveAllTasksCommand()
        {
            taskHandler.TaskCollection.Clear();
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
        public ICommand WriteSelectedTaskToChipOnceCommand => new RelayCommand(OnNewWriteSelectedTaskToChipOnceCommand);
        private void OnNewWriteSelectedTaskToChipOnceCommand()
        {
            _runSelectedOnly = true;
            OnNewWriteToChipOnceCommand();
        }

        /// <summary>
        /// 
        /// </summary>
        public ICommand WriteToChipOnceCommand => new RelayCommand(OnNewWriteToChipOnceCommand);
        private void OnNewWriteToChipOnceCommand()
        {
            OnNewReadChipCommand();
            OnPropertyChanged(nameof(TreeViewParentNodes));
            OnPropertyChanged(nameof(ChipTasks));

            var GenericChip = new GenericChipModel("", CARD_TYPE.Unspecified);
            var DesfireChip = new MifareDesfireChipModel("", CARD_TYPE.Unspecified);
            var ClassicChip = new MifareClassicChipModel("", CARD_TYPE.Unspecified);

            currentTaskIndex = 0;
            var taskDictionary = new Dictionary<string, int>();

            // create a new key,value pair of taskpositions (int) <-> taskindex (string)
            // (they could be different as because item at array position 0 can have index "100")
            foreach (var rfidTaskObject in taskHandler.TaskCollection)
            {
                taskDictionary.Add((rfidTaskObject as IGenericTaskModel).CurrentTaskIndex, taskHandler.TaskCollection.IndexOf(rfidTaskObject));
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

            var thread = new Task(() =>
            {
                try
                {
                    //try to get singleton instance
                    using (var device = ReaderDevice.Instance)
                    {
                        //reader was ready - proceed
                        if (device != null)
                        {
                            device.ReadChipPublic();

                            if (device.GenericChip != null)
                            {
                                //GenericChip.CardType = device.GenericChip.CardType;
                                //GenericChip.UID = device.GenericChip.UID;
                                GenericChip = device.GenericChip;

                                if (GenericChip.CardType.ToString().ToLower(CultureInfo.CurrentCulture).Contains("desfire"))
                                {
                                    device.GetMiFareDESFireChipAppIDs();
                                }
                            }
                        }


                        if (treeViewParentNodes.Any(x => x.IsSelected))
                        {
                            treeViewParentNodes.First(x => x.IsSelected).IsSelected = false;
                        }

                        //only run if theres a card on the reader and its uid was previously added
                        if (
                            !string.IsNullOrWhiteSpace(GenericChip.UID) &&
                            treeViewParentNodes.Any(x => x.UID == GenericChip.UID))
                        {
                            //select current parentnode (card) on reader
                            treeViewParentNodes.First(x => x.UID == GenericChip.UID).IsSelected = true;
                            treeViewParentNodes.First(x => x.IsSelected).IsBeingProgrammed = true;
                        }

                        //are there tasks present to process?
                        while (currentTaskIndex < taskHandler.TaskCollection.Count)
                        {
                            if (_runSelectedOnly)
                            {
                                currentTaskIndex = taskHandler.TaskCollection.IndexOf(SelectedSetupViewModel);
                            }

                            Thread.Sleep(20);

                            taskTimeout.Stop();
                            taskTimeout.Start();
                            taskTimeout.IsEnabled = true;
                            taskTimeout.Tag = currentTaskIndex;

                            SelectedSetupViewModel = taskHandler.TaskCollection[currentTaskIndex];

                            //decide what type of task to process next. use exact array positions 
                            switch (taskHandler.TaskCollection[currentTaskIndex])
                            {

                                case CommonTaskViewModel csVM:

                                    (taskHandler.TaskCollection[currentTaskIndex] as CommonTaskViewModel).GenericChip = GenericChip;
                                    (taskHandler.TaskCollection[currentTaskIndex] as CommonTaskViewModel).DesfireChip = device.DesfireChip;
                                    (taskHandler.TaskCollection[currentTaskIndex] as CommonTaskViewModel).AvailableTasks = taskHandler.TaskCollection;
                                    (taskHandler.TaskCollection[currentTaskIndex] as CommonTaskViewModel).Args = variablesFromArgs;

                                    switch (csVM.SelectedTaskType)
                                    {
                                        case TaskType_CommonTask.CreateReport:
                                            taskTimeout.Stop();
                                            switch ((taskHandler.TaskCollection[currentTaskIndex] as IGenericTaskModel).CurrentTaskErrorLevel)
                                            {
                                                case ERROR.NotReadyError:
                                                    break;

                                                case ERROR.Empty:
                                                    (taskHandler.TaskCollection[currentTaskIndex] as CommonTaskViewModel).CurrentTaskErrorLevel = ERROR.NotReadyError;
                                                    taskTimeout.Start();
                                                    taskTimeout.Stop();

                                                    DirectoryInfo reportTargetPathDirInfo;
                                                    
                                                    try
                                                    {
                                                        var initDir = variablesFromArgs["REPORTTARGETPATH"];
                                                        reportTargetPathDirInfo = new DirectoryInfo(Path.GetDirectoryName(initDir));
                                                        
                                                    }
                                                    catch
                                                    {
                                                        reportTargetPathDirInfo = null;
                                                    }

                                                    if ((taskHandler.TaskCollection[currentTaskIndex] as IGenericTaskModel).SelectedExecuteConditionErrorLevel == ERROR.Empty)
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

                                                        (taskHandler.TaskCollection[currentTaskIndex] as CommonTaskViewModel).WriteReportCommand.Execute(reportReaderWriter);
                                                    }

                                                    else
                                                    {
                                                        // targeted ERRORLEVEL ist not "EMPTY" so check if targeted ERRORLEVEL fits current ERRORLEVEL
                                                        if (taskDictionary.TryGetValue((taskHandler.TaskCollection[currentTaskIndex] as IGenericTaskModel).SelectedExecuteConditionTaskIndex, out var targetTaskIndex))
                                                        {
                                                            if ((taskHandler.TaskCollection[targetTaskIndex] as IGenericTaskModel).CurrentTaskErrorLevel == (taskHandler.TaskCollection[currentTaskIndex] as IGenericTaskModel).SelectedExecuteConditionErrorLevel)
                                                            {
                                                                if (string.IsNullOrEmpty(reportOutputPath))
                                                                {

                                                                    var dlg = new SaveFileDialogViewModel
                                                                    {
                                                                        Title = ResourceLoader.GetResource("windowCaptionSaveTasks"),
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

                                                                (taskHandler.TaskCollection[currentTaskIndex] as CommonTaskViewModel).WriteReportCommand.Execute(reportReaderWriter);
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
                                            switch ((taskHandler.TaskCollection[currentTaskIndex] as CommonTaskViewModel).CurrentTaskErrorLevel)
                                            {
                                                case ERROR.NotReadyError:
                                                    taskTimeout.Start();
                                                    break;

                                                case ERROR.Empty:
                                                    (taskHandler.TaskCollection[currentTaskIndex] as IGenericTaskModel).CurrentTaskErrorLevel = ERROR.NotReadyError;
                                                    taskTimeout.Start();

                                                    if ((taskHandler.TaskCollection[currentTaskIndex] as CommonTaskViewModel).SelectedExecuteConditionErrorLevel == ERROR.Empty)
                                                    {
                                                        (taskHandler.TaskCollection[currentTaskIndex] as CommonTaskViewModel).CheckLogicCondition.Execute(taskHandler.TaskCollection);
                                                    }

                                                    else
                                                    {
                                                        // targeted ERRORLEVEL ist not "EMPTY" so check if targeted ERRORLEVEL fits current ERRORLEVEL
                                                        if (taskDictionary.TryGetValue((taskHandler.TaskCollection[currentTaskIndex] as IGenericTaskModel).SelectedExecuteConditionTaskIndex, out var targetTaskIndex))
                                                        {
                                                            if ((taskHandler.TaskCollection[targetTaskIndex] as IGenericTaskModel).CurrentTaskErrorLevel == (taskHandler.TaskCollection[currentTaskIndex] as IGenericTaskModel).SelectedExecuteConditionErrorLevel)
                                                            {
                                                                (taskHandler.TaskCollection[currentTaskIndex] as CommonTaskViewModel).CheckLogicCondition.Execute(taskHandler.TaskCollection);
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
                                            switch ((taskHandler.TaskCollection[currentTaskIndex] as CommonTaskViewModel).CurrentTaskErrorLevel)
                                            {
                                                case ERROR.NotReadyError:
                                                    taskTimeout.Start();
                                                    break;

                                                case ERROR.Empty:
                                                    (taskHandler.TaskCollection[currentTaskIndex] as IGenericTaskModel).CurrentTaskErrorLevel = ERROR.NotReadyError;
                                                    taskTimeout.Start();

                                                    if ((taskHandler.TaskCollection[currentTaskIndex] as CommonTaskViewModel).SelectedExecuteConditionErrorLevel == ERROR.Empty)
                                                    {
                                                        (taskHandler.TaskCollection[currentTaskIndex] as CommonTaskViewModel).ExecuteProgramCommand.Execute(null);
                                                    }

                                                    else
                                                    {
                                                        // targeted ERRORLEVEL ist not "EMPTY" so check if targeted ERRORLEVEL fits current ERRORLEVEL
                                                        if (taskDictionary.TryGetValue((taskHandler.TaskCollection[currentTaskIndex] as IGenericTaskModel).SelectedExecuteConditionTaskIndex, out var targetTaskIndex))
                                                        {
                                                            if ((taskHandler.TaskCollection[targetTaskIndex] as IGenericTaskModel).CurrentTaskErrorLevel == (taskHandler.TaskCollection[currentTaskIndex] as IGenericTaskModel).SelectedExecuteConditionErrorLevel)
                                                            {
                                                                (taskHandler.TaskCollection[currentTaskIndex] as CommonTaskViewModel).ExecuteProgramCommand.Execute(null);
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
                                            switch ((taskHandler.TaskCollection[currentTaskIndex] as IGenericTaskModel).CurrentTaskErrorLevel)
                                            {
                                                case ERROR.Empty:
                                                    (taskHandler.TaskCollection[currentTaskIndex] as GenericChipTaskViewModel).IsFocused = true;

                                                    if ((taskHandler.TaskCollection[currentTaskIndex] as IGenericTaskModel).SelectedExecuteConditionErrorLevel == ERROR.Empty)
                                                    {
                                                        (taskHandler.TaskCollection[currentTaskIndex] as GenericChipTaskViewModel).CheckChipType.Execute(null);
                                                    }
                                                    else
                                                    {
                                                        // targeted ERRORLEVEL ist not "EMPTY" so check if targeted ERRORLEVEL fits current ERRORLEVEL
                                                        if (taskDictionary.TryGetValue((taskHandler.TaskCollection[currentTaskIndex] as IGenericTaskModel).SelectedExecuteConditionTaskIndex, out var targetTaskIndex))
                                                        {
                                                            if ((taskHandler.TaskCollection[targetTaskIndex] as IGenericTaskModel).CurrentTaskErrorLevel == (taskHandler.TaskCollection[currentTaskIndex] as IGenericTaskModel).SelectedExecuteConditionErrorLevel)
                                                            {
                                                                (taskHandler.TaskCollection[currentTaskIndex] as GenericChipTaskViewModel).CheckChipType.Execute(null);
                                                            }
                                                            else
                                                            {
                                                                currentTaskIndex++;
                                                            }
                                                        }
                                                    }
                                                    break;

                                                default:
                                                    (taskHandler.TaskCollection[currentTaskIndex] as GenericChipTaskViewModel).IsFocused = false;
                                                    currentTaskIndex++;
                                                    break;
                                            }
                                            break;

                                        case TaskType_GenericChipTask.ChipIsMultiChip:
                                            taskTimeout.Start();
                                            switch ((taskHandler.TaskCollection[currentTaskIndex] as IGenericTaskModel).CurrentTaskErrorLevel)
                                            {
                                                case ERROR.Empty:
                                                    (taskHandler.TaskCollection[currentTaskIndex] as GenericChipTaskViewModel).IsFocused = true;

                                                    if ((taskHandler.TaskCollection[currentTaskIndex] as IGenericTaskModel).SelectedExecuteConditionErrorLevel == ERROR.Empty)
                                                    {
                                                        (taskHandler.TaskCollection[currentTaskIndex] as GenericChipTaskViewModel).CheckChipIsMultiTecChip.Execute(null);
                                                    }
                                                    else
                                                    {
                                                        // targeted ERRORLEVEL ist not "EMPTY" so check if targeted ERRORLEVEL fits current ERRORLEVEL
                                                        if (taskDictionary.TryGetValue((taskHandler.TaskCollection[currentTaskIndex] as IGenericTaskModel).SelectedExecuteConditionTaskIndex, out var targetTaskIndex))
                                                        {
                                                            if ((taskHandler.TaskCollection[targetTaskIndex] as IGenericTaskModel).CurrentTaskErrorLevel == (taskHandler.TaskCollection[currentTaskIndex] as IGenericTaskModel).SelectedExecuteConditionErrorLevel)
                                                            {
                                                                (taskHandler.TaskCollection[currentTaskIndex] as GenericChipTaskViewModel).CheckChipIsMultiTecChip.Execute(null);
                                                            }
                                                            else
                                                            {
                                                                currentTaskIndex++;
                                                            }
                                                        }
                                                    }
                                                    break;

                                                default:
                                                    (taskHandler.TaskCollection[currentTaskIndex] as GenericChipTaskViewModel).IsFocused = false;
                                                    currentTaskIndex++;
                                                    break;
                                            }
                                            break;
                                        default:
                                            break;
                                    }
                                    break;

                                case MifareClassicSetupViewModel csVM:
                                    switch ((taskHandler.TaskCollection[currentTaskIndex] as IGenericTaskModel).CurrentTaskErrorLevel)
                                    {
                                        case ERROR.NotReadyError:
                                            taskTimeout.Start();
                                            break;

                                        case ERROR.Empty:
                                            (taskHandler.TaskCollection[currentTaskIndex] as IGenericTaskModel).CurrentTaskErrorLevel = ERROR.NotReadyError;
                                            taskTimeout.Start();

                                            if ((taskHandler.TaskCollection[currentTaskIndex] as IGenericTaskModel).SelectedExecuteConditionErrorLevel == ERROR.Empty)
                                            {
                                                (taskHandler.TaskCollection[currentTaskIndex] as MifareClassicSetupViewModel).CommandDelegator.Execute(csVM.SelectedTaskType);
                                            }

                                            else
                                            {
                                                // targeted ERRORLEVEL ist not "EMPTY" so check if targeted ERRORLEVEL fits current ERRORLEVEL
                                                if (taskDictionary.TryGetValue((taskHandler.TaskCollection[currentTaskIndex] as IGenericTaskModel).SelectedExecuteConditionTaskIndex, out var targetTaskIndex))
                                                {
                                                    if ((taskHandler.TaskCollection[targetTaskIndex] as IGenericTaskModel).CurrentTaskErrorLevel == (taskHandler.TaskCollection[currentTaskIndex] as IGenericTaskModel).SelectedExecuteConditionErrorLevel)
                                                    {
                                                        (taskHandler.TaskCollection[currentTaskIndex] as MifareClassicSetupViewModel).CommandDelegator.Execute(csVM.SelectedTaskType);
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

                                case MifareDesfireSetupViewModel csVM:
                                    switch ((taskHandler.TaskCollection[currentTaskIndex] as IGenericTaskModel).CurrentTaskErrorLevel)
                                    {
                                        case ERROR.NotReadyError:
                                            taskTimeout.Start();
                                            break;

                                        case ERROR.Empty:
                                            (taskHandler.TaskCollection[currentTaskIndex] as IGenericTaskModel).CurrentTaskErrorLevel = ERROR.NotReadyError;
                                            taskTimeout.Start();

                                            if ((taskHandler.TaskCollection[currentTaskIndex] as IGenericTaskModel).SelectedExecuteConditionErrorLevel == ERROR.Empty)
                                            {
                                                (taskHandler.TaskCollection[currentTaskIndex] as MifareDesfireSetupViewModel).CommandDelegator.Execute(csVM.SelectedTaskType);
                                            }

                                            else
                                            {
                                                // targeted ERRORLEVEL ist not "EMPTY" so check if targeted ERRORLEVEL fits current ERRORLEVEL
                                                if (taskDictionary.TryGetValue((taskHandler.TaskCollection[currentTaskIndex] as IGenericTaskModel).SelectedExecuteConditionTaskIndex, out var targetTaskIndex))
                                                {
                                                    if ((taskHandler.TaskCollection[targetTaskIndex] as IGenericTaskModel).CurrentTaskErrorLevel == (taskHandler.TaskCollection[currentTaskIndex] as IGenericTaskModel).SelectedExecuteConditionErrorLevel)
                                                    {
                                                        (taskHandler.TaskCollection[currentTaskIndex] as MifareDesfireSetupViewModel).CommandDelegator.Execute(csVM.SelectedTaskType);
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
                    LogWriter.CreateLogEntry(e, FacilityName);
                }
            });


            thread.ContinueWith((x) =>
            {

                try
                {
                    treeViewParentNodes.First(y => y.IsSelected).IsBeingProgrammed = null;
                    triggerReadChip.IsEnabled = (bool)triggerReadChip.Tag;
                    _runSelectedOnly = false;
                }

                catch { }
            });

            if(!_runSelectedOnly)
            {
                OnNewResetTaskStatusCommand();
            }

            thread.Start();
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
        public ICommand SwitchLanguageToGerman => new RelayCommand(SetGermanLanguage);
        private void SetGermanLanguage()
        {
            using (var settings = new SettingsReaderWriter())
            {
                if (settings.DefaultSpecification.DefaultLanguage != "german")
                {
                    settings.DefaultSpecification.DefaultLanguage = "german";
                    settings.SaveSettings();
                    OnNewLanguageChangedDialog();
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public ICommand SwitchLanguageToEnglish => new RelayCommand(SetEnglishLanguage);
        private void SetEnglishLanguage()
        {
            using (var settings = new SettingsReaderWriter())
            {
                if (settings.DefaultSpecification.DefaultLanguage != "english")
                {
                    settings.DefaultSpecification.DefaultLanguage = "english";
                    settings.SaveSettings();
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
        public ICommand NewReaderSetupDialogCommand => new RelayCommand(OnNewReaderSetupDialog);
        private void OnNewReaderSetupDialog()
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

                            sender.Close();

                            settings.SaveSettings();

                            CurrentReader = Enum.GetName(typeof(ReaderTypes), sender.SelectedReader);

                            ReaderDevice.Reader = (ReaderTypes)Enum.Parse(typeof(ReaderTypes), CurrentReader);

                        },

                        OnConnect = (sender) =>
                        {
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
        }

        /// <summary>
        /// 
        /// </summary>
        public ICommand NewOpenFileDialogCommand => new RelayCommand(OnNewOpenFileDialog);
        private void OnNewOpenFileDialog()
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
                    Multiselect = false
                };

                if (dlg.Show(Dialogs) && dlg.FileName != null)
                {
                    Mouse.OverrideCursor = Cursors.AppStarting;

                    if (ChipTasks.TaskCollection != null && ChipTasks.TaskCollection.Count > 0)
                    {
                        ChipTasks.TaskCollection.Clear();
                    }

                    databaseReaderWriter.ReadDatabase(dlg.FileName);

                    settings.DefaultSpecification.LastUsedProjectPath = dlg.FileName;
                    settings.SaveSettings();

                    foreach (var vm in databaseReaderWriter.TreeViewModel)
                    {
                        TreeViewParentNodes.Add(vm);
                    }

                    foreach (var setup in databaseReaderWriter.SetupModel.TaskCollection)
                    {
                        ChipTasks.TaskCollection.Add(setup);
                    }
                }


            }
            Mouse.OverrideCursor = null;

            OnPropertyChanged(nameof(ChipTasks));
        }

        /// <summary>
        /// Expose Command to Save ProjectFile Menu Item
        /// </summary>
        public ICommand SaveTaskDialogCommand => new RelayCommand(OnNewSaveTaskDialogCommand);
        private void OnNewSaveTaskDialogCommand()
        {
            using (var settings = new SettingsReaderWriter())
            {
                databaseReaderWriter.WriteDatabase(ChipTasks, settings.DefaultSpecification.LastUsedProjectPath);
            }  
        }            

        /// <summary>
        /// Expose Command to Save As Menu Item
        /// </summary>
        public ICommand SaveTaskAsDialogCommand => new RelayCommand(OnNewSaveTaskAsDialogCommand);
        private void OnNewSaveTaskAsDialogCommand()
        {
            var fileName = "";

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
                Caption = ResourceLoader.GetResource("windowCaptionAskTaskDefault"),
                Message = ResourceLoader.GetResource("messageBoxMessageSetProjectAsDefault"),
                Buttons = MessageBoxButton.YesNo
            };

            if (mbox.Show(Dialogs) == MessageBoxResult.Yes)
            {
                using (var settings = new SettingsReaderWriter())
                {
                    settings.DefaultSpecification.LastUsedProjectPath = fileName;
                    settings.SaveSettings();
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
        public ICommand SaveChipDialogCommand => new RelayCommand(OnNewSaveChipDialogCommand);
        private void OnNewSaveChipDialogCommand()
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
        public ICommand CheckUpdateCommand => new RelayCommand(OnNewCheckUpdateCommand);
        private void OnNewCheckUpdateCommand()
        {
            if (updateIsAvailable)
            {
                AskForUpdateNow();
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
        public bool IsSelected { get; set; }

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
                        updater.StartMonitoring();
                    }
                    else
                    {
                        updater.StopMonitoring();
                    }

                    settings.DefaultSpecification.AutoCheckForUpdates = value;
                    settings.SaveSettings();
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
            updateIsAvailable = true;
        }

        private void AskForUpdateNow()
        {
            userIsNotifiedForAvailableUpdate = false;

            Dialogs.Add(new UpdateNotifierViewModel(updater.UpdateInfoText)
            {
                Caption = "Update Available",

                OnOk = (updateAction) =>
                {
                    Mouse.OverrideCursor = Cursors.AppStarting;
                    updater.Update();
                    updateAction.Close();
                    Mouse.OverrideCursor = null;
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

        private void LoadCompleted(object sender, EventArgs e)
        {
            mw = (MainWindow)Application.Current.MainWindow;
            mw.Title = string.Format("RFiDGear {0}.{1}", Version.Major, Version.Minor);

                if (firstRun)
                {
                    Task refreshStatusBarThread;

                    firstRun = false;

                    try
                    {
                        using (var settings = new SettingsReaderWriter())
                        {
                            if (args.Length > 1)
                            {
                                foreach (var arg in args)
                                {
                                    switch (arg.Split('=')[0])
                                    {
                                        case "LASTUSEDPROJECTPATH":
                                            if (File.Exists(arg.Split('=')[1]))
                                            {
                                                settings.DefaultSpecification.LastUsedProjectPath = new DirectoryInfo(arg.Split('=')[1]).FullName;
                                                settings.SaveSettings();
                                            }
                                            break;

                                        default:
                                            break;
                                    }
                                }
                            }

                            CurrentReader = string.IsNullOrWhiteSpace(settings.DefaultSpecification.DefaultReaderName)
                                ? Enum.GetName(typeof(ReaderTypes), settings.DefaultSpecification.DefaultReaderProvider)
                                : settings.DefaultSpecification.DefaultReaderName;

                            if (int.TryParse(settings.DefaultSpecification.LastUsedComPort, out var portNumber))
                            {
                                ReaderDevice.PortNumber = portNumber;
                            }

                            else
                            {
                                ReaderDevice.PortNumber = 0;
                            }


                            culture = (settings.DefaultSpecification.DefaultLanguage == "german") ? new CultureInfo("de-DE") : new CultureInfo("en-US");

                            var autoLoadLastUsedDB = settings.DefaultSpecification.AutoLoadProjectOnStart;

                            var mySplash = new SplashScreenViewModel();

                            if (autoLoadLastUsedDB)
                            {
                                Dialogs.Add(mySplash);
                            }

                            if (autoLoadLastUsedDB)
                            {
                                OpenLastProjectFile();
                            }

                            refreshStatusBarThread = new Task(() =>
                            {
                                while (true)
                                {
                                    Thread.Sleep(500);
                                    DateTimeStatusBar = string.Format("{0}", DateTime.Now);
                                }
                            });

                            refreshStatusBarThread.ContinueWith((x) =>
                            {
                            });

                            refreshStatusBarThread.Start();

                            

                            OnNewResetTaskStatusCommand();
                        }

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
                                            }
                                            break;

                                        case "AUTORUN":
                                            if (arg.Split('=')[1] == "1")
                                            {
                                                _isLoadingProject = true;
                                                OnNewWriteToChipOnceCommand();
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
                    }
                    catch (Exception ex)
                    {
                        LogWriter.CreateLogEntry(ex, FacilityName);
                    }

                    using (var settings = new SettingsReaderWriter())
                    {
                        if (settings.DefaultSpecification.AutoCheckForUpdates)
                        {
                            updater?.StartMonitoring();
                        }
                    }
                }


            

            if (userIsNotifiedForAvailableUpdate)
            {
                AskForUpdateNow();
            }
        }

        private bool ContainsAny(string haystack, params string[] needles) { return needles.Any(haystack.Contains); }
        
        #endregion
    }
}