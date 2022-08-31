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

using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;

using MefMvvm.SharedContracts.ViewModel;

using MvvmDialogs.ViewModels;

using RedCell.Diagnostics.Update;

using RFiDGear.DataAccessLayer.Remote.FromIO;
using RFiDGear.DataAccessLayer;
using RFiDGear.Model;

using Log4CSharp;

using System;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

namespace RFiDGear.ViewModel
{
    /// <summary>
    /// Description of MainWindowViewModel.
    /// </summary>
    [ExportViewModel("MainWin")]
    public class MainWindowViewModel : ViewModelBase
    {
        private readonly Version Version = Assembly.GetExecutingAssembly().GetName().Version;
        private static readonly string FacilityName = "RFiDGear";

        private protected MainWindow mw;
        private readonly Updater updater;
        private protected DatabaseReaderWriter databaseReaderWriter;
        private protected ReportReaderWriter reportReaderWriter;
        private protected DispatcherTimer triggerReadChip;
        private protected DispatcherTimer taskTimeout;
        private string reportOutputPath;

        private ChipTaskHandlerModel taskHandler;
        private protected List<MifareClassicChipModel> mifareClassicUidModels = new List<MifareClassicChipModel>();
        private protected List<MifareDesfireChipModel> mifareDesfireViewModels = new List<MifareDesfireChipModel>();

        private int currentTaskIndex = 0;
        //set if task was completed; indicates greenlight to continue execution
        //if programming takes too long; quit the process
        private bool firstRun = true;
        private bool updateAvailable = false;
        private protected Mutex mutex;
        //one reader, one instance - only

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
            string[] args = Environment.GetCommandLineArgs();
            updater = new Updater();

            using (SettingsReaderWriter settings = new SettingsReaderWriter())
            {
                CurrentReader = string.IsNullOrWhiteSpace(settings.DefaultSpecification.DefaultReaderName)
                    ? Enum.GetName(typeof(ReaderTypes), settings.DefaultSpecification.DefaultReaderProvider)
                    : settings.DefaultSpecification.DefaultReaderName;

                if (!string.IsNullOrEmpty(CurrentReader))
                {
                    ReaderDevice.Reader = (ReaderTypes)Enum.Parse(typeof(ReaderTypes), CurrentReader);
                }
                culture = (settings.DefaultSpecification.DefaultLanguage == "german") ? new CultureInfo("de-DE") : new CultureInfo("en-US");

                autoLoadLastUsedDB = settings.DefaultSpecification.AutoLoadProjectOnStart;

                if (settings.DefaultSpecification.AutoCheckForUpdates)
                {
                    updater?.StartMonitoring();
                }

                if (args.Length == 2)
                {
                    if (File.Exists(args[1]))
                    {
                        settings.DefaultSpecification.LastUsedProjectPath = args[1];
                        settings.SaveSettings();
                    }
                }
            }

            triggerReadChip = new DispatcherTimer
            {
                Interval = new TimeSpan(0, 0, 0, 0, 500)
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
                Interval = new TimeSpan(0, 0, 0, 2, 0)
            };
#endif
            taskTimeout.Tick += TaskTimeout;
            taskTimeout.Start();
            taskTimeout.IsEnabled = false;

            treeViewParentNodes = new ObservableCollection<RFiDChipParentLayerViewModel>();

            taskHandler = new ChipTaskHandlerModel();

            ReaderStatus = CurrentReader == "None" ? "" : "ready";
            databaseReaderWriter = new DatabaseReaderWriter();
            resLoader = new ResourceLoader();

            rowContextMenuItems = new ObservableCollection<MenuItem>();
            emptySpaceContextMenuItems = new ObservableCollection<MenuItem>();
            emptySpaceTreeViewContextMenu = new ObservableCollection<MenuItem>();

            emptySpaceContextMenuItems.Add(new MenuItem()
            {
                Header = ResourceLoader.GetResource("contextMenuItemAddNewTask"),
                Command = GetAddEditCommand
            });

            rowContextMenuItems.Add(new MenuItem()
            {
                Header = ResourceLoader.GetResource("contextMenuItemAddOrEditTask"),
                Command = GetAddEditCommand
            });

            rowContextMenuItems.Add(new MenuItem()
            {
                Header = ResourceLoader.GetResource("contextMenuItemDeleteSelectedItem"),
                Command = new RelayCommand(() =>
                {
                    taskHandler.TaskCollection.Remove(SelectedSetupViewModel);
                })
            });

            rowContextMenuItems.Add(null);

            rowContextMenuItems.Add(new MenuItem()
            {
                Header = ResourceLoader.GetResource("contextMenuItemExecuteSelectedItem"),
                Command = WriteSelectedTaskToChipOnceCommand
            });

            rowContextMenuItems.Add(new MenuItem()
            {
                Header = ResourceLoader.GetResource("contextMenuItemResetSelectedItem"),
                Command = ResetSelectedTaskStatusCommand
            });

            rowContextMenuItems.Add(null);

            rowContextMenuItems.Add(new MenuItem()
            {
                Header = ResourceLoader.GetResource("contextMenuItemExecuteAllItems"),
                Command = WriteToChipOnceCommand
            });

            emptySpaceTreeViewContextMenu.Add(new MenuItem()
            {
                Header = ResourceLoader.GetResource("contextMenuItemReadChipPublic"),
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

        private ICommand GetAddEditCommand => new RelayCommand(OnNewGetAddEditCommand);
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
            using (SettingsReaderWriter settings = new SettingsReaderWriter())
            {
                bool autoLoadLastUsedDB = settings.DefaultSpecification.AutoLoadProjectOnStart;
                string lastUsedDBPath = settings.DefaultSpecification.LastUsedProjectPath;

                culture = (settings.DefaultSpecification.DefaultLanguage == "german") ? new CultureInfo("de-DE") : new CultureInfo("en-US");

                //Mouse.OverrideCursor = Cursors.AppStarting;

                if (ChipTasks.TaskCollection != null && ChipTasks.TaskCollection.Count > 0)
                {
                    ChipTasks.TaskCollection.Clear();
                }

                databaseReaderWriter.ReadDatabase(lastUsedDBPath);

                foreach (RFiDChipParentLayerViewModel vm in databaseReaderWriter.TreeViewModel)
                {
                    TreeViewParentNodes.Add(vm);
                }

                foreach (object setup in databaseReaderWriter.SetupModel.TaskCollection)
                {
                    ChipTasks.TaskCollection.Add(setup);
                }

                //Mouse.OverrideCursor = null;

                RaisePropertyChanged("ChipTasks");
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
                using (ReaderDevice device = ReaderDevice.Instance)
                {
                    //reader was ready - proceed
                    if (device != null)
                    {
                        device.ReadChipPublic();

                        GenericChip = device.GenericChip;
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
                    foreach (RFiDChipParentLayerViewModel item in treeViewParentNodes)
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

                LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""), FacilityName);
            }
        }

        /// <summary>
        /// What to do if timer has ended without success i.e. ErrorLevel != ERROR.NoError ?
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
		private void TaskTimeout(object sender, EventArgs e)
        {
#if DEBUG
            taskTimeout.Start();
            taskTimeout.Stop();
            taskTimeout.IsEnabled = false;


            switch (taskHandler.TaskCollection[currentTaskIndex])
            {
                case GenericChipTaskViewModel tsVM:
                    tsVM.IsTaskCompletedSuccessfully = false;
                    break;
                case CommonTaskViewModel tsVM:
                    tsVM.IsTaskCompletedSuccessfully = false;
                    break;
                case MifareClassicSetupViewModel tsVM:
                    tsVM.IsTaskCompletedSuccessfully = false;
                    break;
                case MifareDesfireSetupViewModel tsVM:
                    tsVM.IsTaskCompletedSuccessfully = false;
                    break;
                case MifareUltralightSetupViewModel tsVM:
                    tsVM.IsTaskCompletedSuccessfully = false;
                    break;
            }
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
                }
            });
        }

        /// <summary>
        /// Create a new "Common" Task of Type "Report Creator"
        /// </summary>
        public ICommand CreateGenericChipTaskCommand => new RelayCommand(OnNewCreateGenericChipTaskCommand);
        private void OnNewCreateGenericChipTaskCommand()
        {
            bool timerState = triggerReadChip.IsEnabled;

            triggerReadChip.IsEnabled = false;

            Mouse.OverrideCursor = Cursors.AppStarting;

            try
            {
                using (ReaderDevice device = ReaderDevice.Instance)
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

                                if (sender.SelectedTaskType == TaskType_GenericChipTask.ChipIsOfType)
                                {
                                    if ((ChipTasks.TaskCollection.OfType<GenericChipTaskViewModel>().Where(x => x.SelectedTaskIndexAsInt == sender.SelectedTaskIndexAsInt).Any()))
                                    {
                                        ChipTasks.TaskCollection.RemoveAt(ChipTasks.TaskCollection.IndexOf(SelectedSetupViewModel));
                                    }

                                    ChipTasks.TaskCollection.Add(sender);

                                    ChipTasks.TaskCollection = new ObservableCollection<object>(ChipTasks.TaskCollection.OrderBy(x => (x as IGenericTaskModel).SelectedTaskIndexAsInt));

                                    RaisePropertyChanged("ChipTasks");
                                }
                                sender.Close();
                            },

                            OnCancel = (sender) =>
                            {
                                sender.Close();
                            },

                            OnAuth = (sender) =>
                            {
                            },

                            OnCloseRequest = (sender) =>
                            {
                                sender.Close();
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

            RaisePropertyChanged("ChipTasks");
        }

        /// <summary>
        /// Create a new "Common" Task of Type "Report Creator"
        /// </summary>
        public ICommand CreateGenericTaskCommand => new RelayCommand(OnNewNewCreateReportTaskCommand);
        private void OnNewNewCreateReportTaskCommand()
        {
            bool timerState = triggerReadChip.IsEnabled;

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
                            sender.SelectedTaskType == TaskType_CommonTask.CheckLogicCondition)
                        {
                            if ((ChipTasks.TaskCollection.OfType<CommonTaskViewModel>().Where(x => (x as CommonTaskViewModel).SelectedTaskIndexAsInt == sender.SelectedTaskIndexAsInt).Any()))
                            {
                                ChipTasks.TaskCollection.RemoveAt(ChipTasks.TaskCollection.IndexOf(SelectedSetupViewModel));

                            }

                            ChipTasks.TaskCollection.Add(sender);

                            ChipTasks.TaskCollection = new ObservableCollection<object>(ChipTasks.TaskCollection.OrderBy(x => (x as IGenericTaskModel).SelectedTaskIndexAsInt));

                            RaisePropertyChanged("ChipTasks");
                        }
                        sender.Close();
                    },

                    OnCancel = (sender) =>
                    {
                        sender.Close();
                    },

                    OnAuth = (sender) =>
                    {
                    },

                    OnCloseRequest = (sender) =>
                    {
                        sender.Close();
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

            RaisePropertyChanged("ChipTasks");
        }

        /// <summary>
        /// Creates a new Task of Type Mifare Classic Card
        /// </summary>
        public ICommand CreateClassicTaskCommand => new RelayCommand(OnNewCreateClassicTaskCommand);
        private void OnNewCreateClassicTaskCommand()
        {
            bool timerState = triggerReadChip.IsEnabled;

            triggerReadChip.IsEnabled = false;

            Mouse.OverrideCursor = Cursors.AppStarting;

            try
            {
                using (ReaderDevice device = ReaderDevice.Instance)
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

                                    RaisePropertyChanged("ChipTasks");
                                }
                                sender.Close();
                            },

                            OnCancel = (sender) =>
                            {
                                sender.Close();
                            },

                            OnAuth = (sender) =>
                            {
                            },

                            OnCloseRequest = (sender) =>
                            {
                                sender.Close();
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
            bool timerState = triggerReadChip.IsEnabled;

            triggerReadChip.IsEnabled = false;

            Mouse.OverrideCursor = Cursors.AppStarting;

            using (ReaderDevice device = ReaderDevice.Instance)
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

                                RaisePropertyChanged("ChipTasks");

                                sender.Close();
                            }
                        },

                        OnCancel = (sender) =>
                        {
                            sender.Close();
                        },

                        OnCloseRequest = (sender) =>
                        {
                            sender.Close();
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

            bool timerState = triggerReadChip.IsEnabled;

            triggerReadChip.IsEnabled = false;

            Mouse.OverrideCursor = Cursors.AppStarting;

            using (ReaderDevice device = ReaderDevice.Instance)
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

                                RaisePropertyChanged("ChipTasks");

                                sender.Close();
                            }
                        },

                        OnCancel = (sender) =>
                        {
                            sender.Close();
                        },

                        OnCloseRequest = (sender) =>
                        {
                            sender.Close();
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

                if (treeViewParentNodes != null && !treeViewParentNodes.Any(x => x.IsSelected) && treeViewParentNodes.Count > 0)
                {
                    treeViewParentNodes.FirstOrDefault().IsSelected = true;
                }

                switch (treeViewParentNodes.Single(x => x.IsSelected == true).CardType)
                {
                    case CARD_TYPE.Mifare1K:
                    case CARD_TYPE.Mifare2K:
                    case CARD_TYPE.Mifare4K:

                        treeViewParentNodes.Single(x => x.IsSelected == true).ExecuteClassicQuickCheckCommand.Execute(null);

                        break;

                    case CARD_TYPE.DESFire:
                    case CARD_TYPE.DESFireEV1:
                    case CARD_TYPE.DESFireEV2:

                        treeViewParentNodes.Single(x => x.IsSelected == true).ExecuteDesfireQuickCheckCommand.Execute(null);

                        break;
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
            bool timerState = triggerReadChip.IsEnabled;

            triggerReadChip.IsEnabled = false;

            Mouse.OverrideCursor = Cursors.Wait;

            using (ReaderDevice device = ReaderDevice.Instance)
            {

                foreach (RFiDChipParentLayerViewModel item in treeViewParentNodes)
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
                            treeViewParentNodes.Add(new RFiDChipParentLayerViewModel(new MifareDesfireChipModel(device.GenericChip.UID, device.GenericChip.CardType), Dialogs, false));
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

            triggerReadChip.IsEnabled = timerState;
        }

        /// <summary>
        /// Reset all Task status information
        /// </summary>
        public ICommand ResetTaskStatusCommand => new RelayCommand(OnNewResetTaskStatusCommand);
        private void OnNewResetTaskStatusCommand()
        {
            foreach (object chipTask in taskHandler.TaskCollection)
            {
                switch (chipTask)
                {
                    case CommonTaskViewModel ssVM:
                        ssVM.IsTaskCompletedSuccessfully = null;
                        reportOutputPath = null;
                        reportReaderWriter = null;
                        ssVM.CurrentTaskErrorLevel = ERROR.Empty;
                        break;
                    case GenericChipTaskViewModel ssVM:
                        ssVM.IsTaskCompletedSuccessfully = null;
                        ssVM.CurrentTaskErrorLevel = ERROR.Empty;
                        break;
                    case MifareClassicSetupViewModel ssVM:
                        ssVM.IsTaskCompletedSuccessfully = null;
                        ssVM.CurrentTaskErrorLevel = ERROR.Empty;
                        break;
                    case MifareDesfireSetupViewModel ssVM:
                        ssVM.IsTaskCompletedSuccessfully = null;
                        ssVM.CurrentTaskErrorLevel = ERROR.Empty;
                        break;
                    case MifareUltralightSetupViewModel ssVM:
                        ssVM.IsTaskCompletedSuccessfully = null;
                        ssVM.CurrentTaskErrorLevel = ERROR.Empty;
                        break;
                }
            }
        }


        /// <summary>
        /// Reset selected Task status information
        /// </summary>
        public ICommand ResetSelectedTaskStatusCommand => new RelayCommand(OnNewResetSelectedTaskStatusCommand);
        private void OnNewResetSelectedTaskStatusCommand()
        {
            switch (SelectedSetupViewModel)
            {
                case CommonTaskViewModel ssVM:
                    ssVM.IsTaskCompletedSuccessfully = null;
                    reportOutputPath = null;
                    reportReaderWriter = null;
                    ssVM.CurrentTaskErrorLevel = ERROR.Empty;
                    break;
                case GenericChipTaskViewModel ssVM:
                    ssVM.IsTaskCompletedSuccessfully = null;
                    ssVM.CurrentTaskErrorLevel = ERROR.Empty;
                    break;
                case MifareClassicSetupViewModel ssVM:
                    ssVM.IsTaskCompletedSuccessfully = null;
                    ssVM.CurrentTaskErrorLevel = ERROR.Empty;
                    break;
                case MifareDesfireSetupViewModel ssVM:
                    ssVM.IsTaskCompletedSuccessfully = null;
                    ssVM.CurrentTaskErrorLevel = ERROR.Empty;
                    break;
                case MifareUltralightSetupViewModel ssVM:
                    ssVM.IsTaskCompletedSuccessfully = null;
                    ssVM.CurrentTaskErrorLevel = ERROR.Empty;
                    break;
            }
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
            OnNewWriteToChipOnceCommand(true);
        }

        /// <summary>
        /// 
        /// </summary>
        public ICommand WriteToChipOnceCommand => new RelayCommand<bool>(OnNewWriteToChipOnceCommand);
        private void OnNewWriteToChipOnceCommand(bool _runSelectedOnly = false)
        {
            OnNewReadChipCommand();
            RaisePropertyChanged("TreeViewParentNodes");
            RaisePropertyChanged("ChipTasks");

            GenericChipModel GenericChip = new GenericChipModel("", CARD_TYPE.Unspecified);
            MifareDesfireChipModel DesfireChip = new MifareDesfireChipModel("", CARD_TYPE.Unspecified);
            MifareClassicChipModel ClassicChip = new MifareClassicChipModel("", CARD_TYPE.Unspecified);

            currentTaskIndex = 0;
            Dictionary<string, int> taskDictionary = new Dictionary<string, int>();

            // create a new key,value pair of taskpositions (int) <-> taskindex (string)
            // (they could be different as because item at array position 0 can have index "100")
            foreach (object rfidTaskObject in taskHandler.TaskCollection)
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

            Task thread = new Task(() =>
            {
                try
                {
                    //try to get singleton instance
                    using (ReaderDevice device = ReaderDevice.Instance)
                    {
                        //reader was ready - proceed
                        if (device != null)
                        {
                            device.ReadChipPublic();

                            GenericChip.CardType = device.GenericChip.CardType;
                            GenericChip.UID = device.GenericChip.UID;

                            if (GenericChip != null)
                            {
                                if (GenericChip.CardType == CARD_TYPE.DESFireEV1 || GenericChip.CardType == CARD_TYPE.DESFireEV2)
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
                            taskTimeout.Tag = currentTaskIndex;

                            SelectedSetupViewModel = taskHandler.TaskCollection[currentTaskIndex];

                            //decide what type of task to process next. use exact array positions 
                            switch (taskHandler.TaskCollection[currentTaskIndex])
                            {

                                case CommonTaskViewModel csVM:
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

                                                    if ((taskHandler.TaskCollection[currentTaskIndex] as IGenericTaskModel).SelectedExecuteConditionErrorLevel == ERROR.Empty)
                                                    {
                                                        if (string.IsNullOrEmpty(reportOutputPath))
                                                        {
                                                            var dlg = new SaveFileDialogViewModel
                                                            {
                                                                Title = ResourceLoader.GetResource("windowCaptionSaveTasks"),
                                                                Filter = ResourceLoader.GetResource("filterStringSaveReport")
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

                                                        (taskHandler.TaskCollection[currentTaskIndex] as CommonTaskViewModel).GenericChip = device.GenericChip;
                                                        (taskHandler.TaskCollection[currentTaskIndex] as CommonTaskViewModel).DesfireChip = device.DesfireChip;
                                                        (taskHandler.TaskCollection[currentTaskIndex] as CommonTaskViewModel).AvailableTasks = taskHandler.TaskCollection;
                                                        (taskHandler.TaskCollection[currentTaskIndex] as CommonTaskViewModel).WriteReportCommand.Execute(reportReaderWriter);
                                                    }

                                                    else
                                                    {
                                                        // targeted ERRORLEVEL ist not "EMPTY" so check if targeted ERRORLEVEL fits current ERRORLEVEL
                                                        if (taskDictionary.TryGetValue((taskHandler.TaskCollection[currentTaskIndex] as IGenericTaskModel).SelectedExecuteConditionTaskIndex, out int targetTaskIndex))
                                                        {
                                                            if ((taskHandler.TaskCollection[targetTaskIndex] as IGenericTaskModel).CurrentTaskErrorLevel == (taskHandler.TaskCollection[currentTaskIndex] as IGenericTaskModel).SelectedExecuteConditionErrorLevel)
                                                            {
                                                                if (string.IsNullOrEmpty(reportOutputPath))
                                                                {
                                                                    var dlg = new SaveFileDialogViewModel
                                                                    {
                                                                        Title = ResourceLoader.GetResource("windowCaptionSaveTasks"),
                                                                        Filter = ResourceLoader.GetResource("filterStringSaveReport")
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

                                                                (taskHandler.TaskCollection[currentTaskIndex] as CommonTaskViewModel).GenericChip = device.GenericChip;
                                                                (taskHandler.TaskCollection[currentTaskIndex] as CommonTaskViewModel).DesfireChip = device.DesfireChip;
                                                                (taskHandler.TaskCollection[currentTaskIndex] as CommonTaskViewModel).AvailableTasks = taskHandler.TaskCollection;
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
                                                        (taskHandler.TaskCollection[currentTaskIndex] as CommonTaskViewModel).GenericChip = GenericChip;
                                                        (taskHandler.TaskCollection[currentTaskIndex] as CommonTaskViewModel).CheckLogicCondition.Execute(taskHandler.TaskCollection);
                                                    }

                                                    else
                                                    {
                                                        // targeted ERRORLEVEL ist not "EMPTY" so check if targeted ERRORLEVEL fits current ERRORLEVEL
                                                        if (taskDictionary.TryGetValue((taskHandler.TaskCollection[currentTaskIndex] as IGenericTaskModel).SelectedExecuteConditionTaskIndex, out int targetTaskIndex))
                                                        {
                                                            if ((taskHandler.TaskCollection[targetTaskIndex] as IGenericTaskModel).CurrentTaskErrorLevel == (taskHandler.TaskCollection[currentTaskIndex] as IGenericTaskModel).SelectedExecuteConditionErrorLevel)
                                                            {
                                                                (taskHandler.TaskCollection[currentTaskIndex] as CommonTaskViewModel).GenericChip = device.GenericChip;
                                                                (taskHandler.TaskCollection[currentTaskIndex] as CommonTaskViewModel).DesfireChip = device.DesfireChip;
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
                                                        if (taskDictionary.TryGetValue((taskHandler.TaskCollection[currentTaskIndex] as IGenericTaskModel).SelectedExecuteConditionTaskIndex, out int targetTaskIndex))
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
                                                if (taskDictionary.TryGetValue((taskHandler.TaskCollection[currentTaskIndex] as IGenericTaskModel).SelectedExecuteConditionTaskIndex, out int targetTaskIndex))
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
                                                if (taskDictionary.TryGetValue((taskHandler.TaskCollection[currentTaskIndex] as IGenericTaskModel).SelectedExecuteConditionTaskIndex, out int targetTaskIndex))
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

                            RaisePropertyChanged("TreeViewParentNodes");
                        }
                    }
                    RaisePropertyChanged("TreeViewParentNodes");

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
            using (SettingsReaderWriter settings = new SettingsReaderWriter())
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
            using (SettingsReaderWriter settings = new SettingsReaderWriter())
            {
                if (settings.DefaultSpecification.DefaultLanguage != "english")
                {
                    settings.DefaultSpecification.DefaultLanguage = "english";
                    settings.SaveSettings();
                    OnNewLanguageChangedDialog();
                }
            }
        }

        private void OnNewLanguageChangedDialog()
        {
            if(new MessageBoxViewModel
            {
                Message = ResourceLoader.GetResource("messageBoxRestartRequiredMessage"),
                Caption = ResourceLoader.GetResource("messageBoxRestartRequiredCaption"),
                Buttons = MessageBoxButton.OKCancel,
                Image = MessageBoxImage.Question

            }.Show(this.Dialogs) == MessageBoxResult.OK)
            
            {
                Environment.Exit(0);
            }

        }

        /// <summary>
        /// 
        /// </summary>
        public ICommand NewReaderSetupDialogCommand => new RelayCommand(OnNewReaderSetupDialog);
        private void OnNewReaderSetupDialog()
        {
            using (SettingsReaderWriter settings = new SettingsReaderWriter())
            {
                DefaultSpecification currentSettings = settings.DefaultSpecification;

                ReaderDevice.PortNumber = int.Parse(currentSettings.LastUsedComPort);
                ReaderDevice.Reader = currentSettings.DefaultReaderProvider;

                using (ReaderDevice device = ReaderDevice.Instance)
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
                        },

                        OnCloseRequest = (sender) =>
                        {
                            sender.Close();
                        }
                    });
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public ICommand NewOpenFileDialogCommand => new RelayCommand<bool>(OnNewOpenFileDialog);
        private void OnNewOpenFileDialog(bool omitFileDlg = false)
        {
            bool autoLoadLastUsedDB;
            string lastUsedDBPath;

            using (SettingsReaderWriter settings = new SettingsReaderWriter())
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

                    foreach (RFiDChipParentLayerViewModel vm in databaseReaderWriter.TreeViewModel)
                    {
                        TreeViewParentNodes.Add(vm);
                    }

                    foreach (object setup in databaseReaderWriter.SetupModel.TaskCollection)
                    {
                        ChipTasks.TaskCollection.Add(setup);
                    }
                }


            }
            Mouse.OverrideCursor = null;

            RaisePropertyChanged("ChipTasks");
        }

        /// <summary>
        /// Expose Command to Save As Menu Item
        /// </summary>
        public ICommand SaveTaskDialogCommand => new RelayCommand(OnNewSaveTaskDialogCommand);
        private void OnNewSaveTaskDialogCommand()
        {
            var dlg = new SaveFileDialogViewModel
            {
                Title = ResourceLoader.GetResource("windowCaptionSaveTasks"),
                Filter = ResourceLoader.GetResource("filterStringSaveTasks")
            };

            if (dlg.Show(Dialogs) && dlg.FileName != null)
            {
                databaseReaderWriter.WriteDatabase(ChipTasks, dlg.FileName);
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
                RaisePropertyChanged("SelectedSetupViewModel");
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
                RaisePropertyChanged("TreeViewParentNodes");
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
                RaisePropertyChanged("ChipTasks");
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
                RaisePropertyChanged("CurrentReader");
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
                RaisePropertyChanged("ReaderStatus");
            }
        }
        private string readerStatus;

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
                using (SettingsReaderWriter settings = new SettingsReaderWriter())
                {
                    return settings.DefaultSpecification != null && settings.DefaultSpecification.AutoCheckForUpdates;
                }
            }
            set
            {
                using (SettingsReaderWriter settings = new SettingsReaderWriter())
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
                RaisePropertyChanged("IsCheckForUpdatesChecked");

            }
        }

        /// <summary>
        /// 
        /// </summary>
        public bool RadioButtonGermanLanguageSelectedState
        {
            get
            {
                using (SettingsReaderWriter settings = new SettingsReaderWriter())
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
                using (SettingsReaderWriter settings = new SettingsReaderWriter())
                {
                    if (settings.DefaultSpecification.DefaultLanguage == "english")
                    {
                        value = false;
                    }

                    RaisePropertyChanged("RadioButtonGermanLanguageSelectedState");
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
                using (SettingsReaderWriter settings = new SettingsReaderWriter())
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
                using (SettingsReaderWriter settings = new SettingsReaderWriter())
                {
                    if (settings.DefaultSpecification.DefaultLanguage == "german")
                    {
                        value = false;
                    }

                    RaisePropertyChanged("RadioButtonEnglishLanguageSelectedState");
                }
            }
        }

        #endregion Dependency Properties

        #region Extensions

        private void EnableUpdate(object sender, EventArgs e)
        {
            updateAvailable = true;
        }

        private void AskForUpdateNow()
        {
            updateAvailable = false;

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
                },

                OnCloseRequest = (updateAction) =>
                {
                    updater.AllowUpdate = false;
                    updateAction.Close();
                }
            });
        }

        //Only one instance is allowed due to the singleton pattern of the reader class
        private void RunMutex(object sender, StartupEventArgs e)
        {
            mutex = new Mutex(true, "App", out bool aIsNewInstance);

            if (!aIsNewInstance)
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
            if (updateAvailable)
            {
                AskForUpdateNow();
            }

            mw = (MainWindow)Application.Current.MainWindow;
            mw.Title = string.Format("RFiDGear {0}.{1}", Version.Major, Version.Minor);

            if (firstRun)
            {        
                Task loadProjectOnStartThread;
                Task refreshStatusBarThread;

                firstRun = false;

                try
                {
                    var mySplash = new SplashScreenViewModel();

                    using (SettingsReaderWriter settings = new SettingsReaderWriter())
                    {
                        CurrentReader = string.IsNullOrWhiteSpace(settings.DefaultSpecification.DefaultReaderName)
                            ? Enum.GetName(typeof(ReaderTypes), settings.DefaultSpecification.DefaultReaderProvider)
                            : settings.DefaultSpecification.DefaultReaderName;

                        ReaderDevice.PortNumber = int.Parse(settings.DefaultSpecification.LastUsedComPort);

                        culture = (settings.DefaultSpecification.DefaultLanguage == "german") ? new CultureInfo("de-DE") : new CultureInfo("en-US");

                        var autoLoadLastUsedDB = settings.DefaultSpecification.AutoLoadProjectOnStart;

                        if (autoLoadLastUsedDB)
                        {
                            Dialogs.Add(mySplash);
                        }

                        loadProjectOnStartThread = new Task(() =>
                        {
                            if (autoLoadLastUsedDB)
                            {
                                OpenLastProjectFile();
                            }
                        });

                        loadProjectOnStartThread.ContinueWith((x) =>
                        {
                        });

                        
                        loadProjectOnStartThread.RunSynchronously();

                        refreshStatusBarThread = new Task(() =>
                        {
                            while(true)
                            {
                                Thread.Sleep(500);
                                ReaderStatus = string.Format("{0}", DateTime.Now);
                            }
                        });

                        refreshStatusBarThread.ContinueWith((x) =>
                        {
                        });

                        refreshStatusBarThread.Start();

                        Dialogs.Remove(mySplash);

                        OnNewResetTaskStatusCommand();
                    }
                }
                catch (Exception ex)
                {
                    LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, ex.Message, ex.InnerException != null ? ex.InnerException.Message : ""), FacilityName);
                }
            }
            #endregion
        }
    }
}