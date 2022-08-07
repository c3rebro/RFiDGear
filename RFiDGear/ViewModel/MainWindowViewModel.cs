/* This is RFiDGear's Main Window Class
 * 
 * RFiDGear has a Set of objects in an ObservableCollection.
 * 
 * These objects can have a Type T of:
 * - DesfireSetupViewModel
 * - ClassicSetupViewModel
 * - UltralightSetupViewModel
 * - PlusSetupViewModel
 * - CommonTaskSetupViewModel
 * - GenericChipSetupViewModel
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

using MefMvvm.SharedContracts;
using MefMvvm.SharedContracts.ViewModel;

using MvvmDialogs.ViewModels;

using RedCell.Diagnostics.Update;

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
        private readonly string FacilityName = "RFiDGear";

        private readonly Version Version = Assembly.GetExecutingAssembly().GetName().Version;

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

        private int taskIndex = 0;
        //set if task was completed; indicates greenlight to continue execution
        //if programming takes too long; quit the process
        private bool firstRun = true;
        private bool updateAvailable = false;
        private protected Mutex mutex;
        //one reader, one instance - only

        #region Plugins

        #endregion

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

            using (SettingsReaderWriter settings = new SettingsReaderWriter())
            {
                CurrentReader = string.IsNullOrWhiteSpace(settings.DefaultSpecification.DefaultReaderName)
                    ? Enum.GetName(typeof(ReaderTypes), settings.DefaultSpecification.DefaultReaderProvider)
                    : settings.DefaultSpecification.DefaultReaderName;

                culture = (settings.DefaultSpecification.DefaultLanguage == "german") ? new CultureInfo("de-DE") : new CultureInfo("en-US");

                autoLoadLastUsedDB = settings.DefaultSpecification.AutoLoadProjectOnStart;

                if (args.Length == 2)
                {
                    if (File.Exists(args[1]))
                    {
                        settings.DefaultSpecification.LastUsedProjectPath = args[1];
                        settings.SaveSettings();
                    }
                }
            }

            updater = new Updater();

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
            emptySpaceContextMenuItems = new ObservableCollection<MenuItem>
            {
                new MenuItem()
                {
                    Header = "contextMenuItemAddNewEvent", //resLoader.getResource("contextMenuItemAddNewEvent"),
                    Command = null
                }
            };

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

            rowContextMenuItems.Add(new MenuItem()
            {
                Header = ResourceLoader.GetResource("contextMenuItemExecuteSelectedItem"),
                Command = WriteSelectedTaskToChipOnceCommand
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

                foreach (RFiDChipParentLayerViewModel vm in databaseReaderWriter.treeViewModel)
                {
                    TreeViewParentNodes.Add(vm);
                }

                foreach (object setup in databaseReaderWriter.setupModel.TaskCollection)
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
                using (RFiDDevice device = RFiDDevice.Instance)
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
                    !treeViewParentNodes.Any(x => (x.UidNumber == GenericChip.UID)))
                {
                    foreach (RFiDChipParentLayerViewModel item in treeViewParentNodes)
                    {
                        item.IsExpanded = false;
                    }

                    // fill treeview with dummy models and viewmodels
                    switch (GenericChip.CardType)
                    {
                        case CARD_TYPE.Mifare1K:
                            treeViewParentNodes.Add(new RFiDChipParentLayerViewModel(new MifareClassicChipModel(GenericChip.UID, CARD_TYPE.Mifare1K), Dialogs));
                            break;

                        case CARD_TYPE.Mifare2K:
                            treeViewParentNodes.Add(new RFiDChipParentLayerViewModel(new MifareClassicChipModel(GenericChip.UID, CARD_TYPE.Mifare2K), Dialogs));
                            break;

                        case CARD_TYPE.Mifare4K:
                            treeViewParentNodes.Add(new RFiDChipParentLayerViewModel(new MifareClassicChipModel(GenericChip.UID, CARD_TYPE.Mifare4K), Dialogs));
                            break;

                        case CARD_TYPE.DESFire:
                        case CARD_TYPE.DESFireEV1:
                        case CARD_TYPE.DESFireEV2:
                            treeViewParentNodes.Add(new RFiDChipParentLayerViewModel(new MifareDesfireChipModel(GenericChip.UID, GenericChip.CardType), Dialogs));
                            break;
                    }
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


			switch (taskHandler.TaskCollection[taskIndex])
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
            if (taskHandler.GetTaskType(taskIndex) == typeof(MifareDesfireSetupViewModel))
            {
                (taskHandler.TaskCollection[(int)taskTimeout.Tag] as MifareDesfireSetupViewModel).IsTaskCompletedSuccessfully = false;
            }
            else if (taskHandler.GetTaskType(taskIndex) == typeof(MifareClassicSetupViewModel))
            {
                (taskHandler.TaskCollection[(int)taskTimeout.Tag] as MifareClassicSetupViewModel).IsTaskCompletedSuccessfully = false;
            }
            else if (taskHandler.GetTaskType(taskIndex) == typeof(CommonTaskViewModel))
            {
                (taskHandler.TaskCollection[(int)taskTimeout.Tag] as CommonTaskViewModel).IsTaskCompletedSuccessfully = false;
            }

            taskIndex = int.MaxValue;
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
                using (RFiDDevice device = RFiDDevice.Instance)
                {
                    // only call dialog if device is ready
                    if (device != null)
                    {
                        dialogs.Add(new GenericChipTaskViewModel(SelectedSetupViewModel, ChipTasks.TaskCollection, dialogs)
                        {
                            Caption = ResourceLoader.GetResource("windowCaptionAddEditMifareClassicTask"),
                            //IsClassicAuthInfoEnabled = true, //content.Contains("EditAccessBits"),

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

                                    //ChipTasks.TaskCollection = new ObservableCollection<object>(ChipTasks.TaskCollection.OfType<ReportTaskViewModel>().OrderBy(x =>(x as ReportTaskViewModel).SelectedTaskIndexAsInt));

                                    ChipTasks.TaskCollection = new ObservableCollection<object>(ChipTasks.TaskCollection.OrderBy(x =>

                                        (x is CommonTaskViewModel) ?
                                        (x as CommonTaskViewModel).SelectedTaskIndexAsInt :
                                        (x is GenericChipTaskViewModel) ?
                                        (x as GenericChipTaskViewModel).SelectedTaskIndexAsInt :
                                        (x is MifareDesfireSetupViewModel) ?
                                        (x as MifareDesfireSetupViewModel).SelectedTaskIndexAsInt :
                                        (x as MifareClassicSetupViewModel).SelectedTaskIndexAsInt)
                                        );

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
                LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""), FacilityName);
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
                    //IsClassicAuthInfoEnabled = true, //content.Contains("EditAccessBits"),

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

                            ChipTasks.TaskCollection = new ObservableCollection<object>(ChipTasks.TaskCollection.OrderBy(x =>

                                (x is CommonTaskViewModel) ?
                                (x as CommonTaskViewModel).SelectedTaskIndexAsInt :
                                (x is GenericChipTaskViewModel) ?
                                (x as GenericChipTaskViewModel).SelectedTaskIndexAsInt :
                                (x is MifareDesfireSetupViewModel) ?
                                (x as MifareDesfireSetupViewModel).SelectedTaskIndexAsInt :
                                (x as MifareClassicSetupViewModel).SelectedTaskIndexAsInt)
                                );

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
                LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""), FacilityName);
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
                using (RFiDDevice device = RFiDDevice.Instance)
                {
                    // only call dialog if device is ready
                    if (device != null)
                    {
                        dialogs.Add(new MifareClassicSetupViewModel(SelectedSetupViewModel, dialogs)
                        {
                            Caption = ResourceLoader.GetResource("windowCaptionAddEditMifareClassicTask"),
                            IsClassicAuthInfoEnabled = true, //content.Contains("EditAccessBits"),

                            OnOk = (sender) =>
                            {
                                if (sender.SelectedTaskType == TaskType_MifareClassicTask.ChangeDefault)
                                {
                                    sender.Settings.SaveSettings();
                                }

                                if (sender.SelectedTaskType == TaskType_MifareClassicTask.WriteData ||
                                                     sender.SelectedTaskType == TaskType_MifareClassicTask.ReadData)
                                {

                                    if ((ChipTasks.TaskCollection.OfType<MifareClassicSetupViewModel>().Where(x => (x as MifareClassicSetupViewModel).SelectedTaskIndexAsInt == sender.SelectedTaskIndexAsInt).Any()))
                                    {
                                        ChipTasks.TaskCollection.RemoveAt(ChipTasks.TaskCollection.IndexOf(SelectedSetupViewModel));
                                    }

                                    ChipTasks.TaskCollection.Add(sender);

                                    ChipTasks.TaskCollection = new ObservableCollection<object>(ChipTasks.TaskCollection.OrderBy(x =>

                                        (x is CommonTaskViewModel) ?
                                        (x as CommonTaskViewModel).SelectedTaskIndexAsInt :
                                        (x is GenericChipTaskViewModel) ?
                                        (x as GenericChipTaskViewModel).SelectedTaskIndexAsInt :
                                        (x is MifareDesfireSetupViewModel) ?
                                        (x as MifareDesfireSetupViewModel).SelectedTaskIndexAsInt :
                                        (x as MifareClassicSetupViewModel).SelectedTaskIndexAsInt)
                                        );

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
                LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""), FacilityName);
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

            using (RFiDDevice device = RFiDDevice.Instance)
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

                                ChipTasks.TaskCollection = new ObservableCollection<object>(ChipTasks.TaskCollection.OrderBy(x =>

                                    (x is CommonTaskViewModel) ?
                                    (x as CommonTaskViewModel).SelectedTaskIndexAsInt :
                                    (x is GenericChipTaskViewModel) ?
                                    (x as GenericChipTaskViewModel).SelectedTaskIndexAsInt :
                                    (x is MifareDesfireSetupViewModel) ?
                                    (x as MifareDesfireSetupViewModel).SelectedTaskIndexAsInt :
                                    (x as MifareClassicSetupViewModel).SelectedTaskIndexAsInt)
                                    );

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

            using (RFiDDevice device = RFiDDevice.Instance)
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

                                ChipTasks.TaskCollection = new ObservableCollection<object>(ChipTasks.TaskCollection.OrderBy(x =>

                                    (x is CommonTaskViewModel) ?
                                    (x as CommonTaskViewModel).SelectedTaskIndexAsInt :
                                    (x is GenericChipTaskViewModel) ?
                                    (x as GenericChipTaskViewModel).SelectedTaskIndexAsInt :
                                    (x is MifareDesfireSetupViewModel) ?
                                    (x as MifareDesfireSetupViewModel).SelectedTaskIndexAsInt :
                                    (x as MifareClassicSetupViewModel).SelectedTaskIndexAsInt)
                                    );

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

            using (RFiDDevice device = RFiDDevice.Instance)
            {

                foreach (RFiDChipParentLayerViewModel item in treeViewParentNodes)
                {
                    item.IsExpanded = false;
                }

                if (device?.ReadChipPublic() == ERROR.NoError &&
                    !treeViewParentNodes.Any(x => x.UidNumber == device.GenericChip.UID))
                {
                    // fill treeview with dummy models and viewmodels
                    switch (device.GenericChip.CardType)
                    {
                        case CARD_TYPE.Mifare1K:
                            treeViewParentNodes.Add(new RFiDChipParentLayerViewModel(new MifareClassicChipModel(device.GenericChip.UID, CARD_TYPE.Mifare1K), Dialogs));
                            break;

                        case CARD_TYPE.Mifare2K:
                            treeViewParentNodes.Add(new RFiDChipParentLayerViewModel(new MifareClassicChipModel(device.GenericChip.UID, CARD_TYPE.Mifare2K), Dialogs));
                            break;

                        case CARD_TYPE.Mifare4K:
                            treeViewParentNodes.Add(new RFiDChipParentLayerViewModel(new MifareClassicChipModel(device.GenericChip.UID, CARD_TYPE.Mifare4K), Dialogs));
                            break;

                        case CARD_TYPE.DESFire:
                        case CARD_TYPE.DESFireEV1:
                        case CARD_TYPE.DESFireEV2:
                            treeViewParentNodes.Add(new RFiDChipParentLayerViewModel(new MifareDesfireChipModel(device.GenericChip.UID, device.GenericChip.CardType), Dialogs));
                            break;

                        case CARD_TYPE.MifarePlus_SL1_1K:
                            treeViewParentNodes.Add(new RFiDChipParentLayerViewModel(new MifareClassicChipModel(device.GenericChip.UID, CARD_TYPE.MifarePlus_SL1_1K), Dialogs));
                            break;

                        case CARD_TYPE.MifarePlus_SL1_2K:
                            treeViewParentNodes.Add(new RFiDChipParentLayerViewModel(new MifareClassicChipModel(device.GenericChip.UID, CARD_TYPE.MifarePlus_SL1_2K), Dialogs));
                            break;

                        case CARD_TYPE.MifarePlus_SL1_4K:
                            treeViewParentNodes.Add(new RFiDChipParentLayerViewModel(new MifareClassicChipModel(device.GenericChip.UID, CARD_TYPE.MifarePlus_SL1_4K), Dialogs));
                            break;

                        case CARD_TYPE.MifarePlus_SL2_1K:
                            treeViewParentNodes.Add(new RFiDChipParentLayerViewModel(new MifareClassicChipModel(device.GenericChip.UID, CARD_TYPE.MifarePlus_SL2_1K), Dialogs));
                            break;

                        case CARD_TYPE.MifarePlus_SL2_2K:
                            treeViewParentNodes.Add(new RFiDChipParentLayerViewModel(new MifareClassicChipModel(device.GenericChip.UID, CARD_TYPE.MifarePlus_SL2_2K), Dialogs));
                            break;

                        case CARD_TYPE.MifarePlus_SL2_4K:
                            treeViewParentNodes.Add(new RFiDChipParentLayerViewModel(new MifareClassicChipModel(device.GenericChip.UID, CARD_TYPE.MifarePlus_SL2_4K), Dialogs));
                            break;

                        case CARD_TYPE.MifarePlus_SL3_1K:
                            treeViewParentNodes.Add(new RFiDChipParentLayerViewModel(new MifareClassicChipModel(device.GenericChip.UID, CARD_TYPE.MifarePlus_SL3_1K), Dialogs));
                            break;

                        case CARD_TYPE.MifarePlus_SL3_2K:
                            treeViewParentNodes.Add(new RFiDChipParentLayerViewModel(new MifareClassicChipModel(device.GenericChip.UID, CARD_TYPE.MifarePlus_SL3_2K), Dialogs));
                            break;

                        case CARD_TYPE.MifarePlus_SL3_4K:
                            treeViewParentNodes.Add(new RFiDChipParentLayerViewModel(new MifareClassicChipModel(device.GenericChip.UID, CARD_TYPE.MifarePlus_SL3_4K), Dialogs));
                            break;

                        case CARD_TYPE.MifareUltralight:
                            treeViewParentNodes.Add(new RFiDChipParentLayerViewModel(new MifareUltralightChipModel(device.GenericChip.UID, device.GenericChip.CardType), Dialogs));
                            break;

                            /*
                        case CARD_TYPE.GENERIC_T_CL_A:
                            treeViewParentNodes.Add(new RFiDChipParentLayerViewModel(new MifareDesfireChipModel(device.GenericChip.UID, device.GenericChip.CardType), Dialogs));
                            break;
                            */
                        case CARD_TYPE.ISO15693:
                            device.ReadISO15693Chip();
                            break;
                    }


                }
                else if (treeViewParentNodes.Any(x => x.UidNumber == device.GenericChip.UID))
                {
                    treeViewParentNodes.First(x => x.UidNumber == device.GenericChip.UID).IsSelected = true;
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
                        ssVM.TaskErr = ERROR.Empty;
                        break;
                    case GenericChipTaskViewModel ssVM:
                        ssVM.IsTaskCompletedSuccessfully = null;
                        ssVM.TaskErr = ERROR.Empty;
                        break;
                    case MifareClassicSetupViewModel ssVM:
                        ssVM.IsTaskCompletedSuccessfully = null;
                        ssVM.TaskErr = ERROR.Empty;
                        break;
                    case MifareDesfireSetupViewModel ssVM:
                        ssVM.IsTaskCompletedSuccessfully = null;
                        ssVM.TaskErr = ERROR.Empty;
                        break;
                    case MifareUltralightSetupViewModel ssVM:
                        ssVM.IsTaskCompletedSuccessfully = null;
                        ssVM.TaskErr = ERROR.Empty;
                        break;
                }
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
            //OnNewExecuteQuickCheckCommand();
            //RaisePropertyChanged("TreeViewParentNodes");
            //RaisePropertyChanged("ChipTasks");


            taskIndex = 0;
            Dictionary<string, int> taskIndices = new Dictionary<string, int>();

            // create a new key,value pair of taskpositions <-> taskindex 
            // (they could be different as because item at array position 0 can have index "100")
            foreach (object o in taskHandler.TaskCollection)
            {
                switch (o)
                {
                    case GenericChipTaskViewModel ssVM:
                        if (ssVM.IsValidSelectedTaskIndex != false)
                        {
                            taskIndices.Add(ssVM.SelectedTaskIndex, taskHandler.TaskCollection.IndexOf(ssVM));
                        }

                        break;
                    case CommonTaskViewModel ssVM:
                        if (ssVM.IsValidSelectedTaskIndex != false)
                        {
                            taskIndices.Add(ssVM.SelectedTaskIndex, taskHandler.TaskCollection.IndexOf(ssVM));
                        }

                        break;
                    case MifareClassicSetupViewModel ssVM:
                        if (ssVM.IsValidSelectedTaskIndex != false)
                        {
                            taskIndices.Add(ssVM.SelectedTaskIndex, taskHandler.TaskCollection.IndexOf(ssVM));
                        }

                        break;
                    case MifareDesfireSetupViewModel ssVM:
                        if (ssVM.IsValidSelectedTaskIndex != false)
                        {
                            taskIndices.Add(ssVM.SelectedTaskIndex, taskHandler.TaskCollection.IndexOf(ssVM));
                        }

                        break;
                    case MifareUltralightSetupViewModel ssVM:
                        if (ssVM.IsValidSelectedTaskIndex != false)
                        {
                            taskIndices.Add(ssVM.SelectedTaskIndex, taskHandler.TaskCollection.IndexOf(ssVM));
                        }

                        break;
                }
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
                GenericChipModel GenericChip = new GenericChipModel("", CARD_TYPE.Unspecified);
                MifareDesfireChipModel DesfireChip = new MifareDesfireChipModel("", CARD_TYPE.Unspecified);
                MifareClassicChipModel ClassicChip = new MifareClassicChipModel("", CARD_TYPE.Unspecified);

                try
                {
                    //try to get singleton instance
                    using (RFiDDevice device = RFiDDevice.Instance)
                    {
                        //reader was ready - proceed
                        if (device != null)
                        {
                            device.ReadChipPublic();

                            GenericChip = new GenericChipModel(device.GenericChip.UID, device.GenericChip.CardType);

                            if (GenericChip != null)
                            {
                                if (GenericChip.CardType == CARD_TYPE.DESFireEV1 || GenericChip.CardType == CARD_TYPE.DESFireEV2)
                                {
                                    device.GetMiFareDESFireChipAppIDs();

                                    DesfireChip = new MifareDesfireChipModel(GenericChip.UID, GenericChip.CardType)
                                    {
                                        AppList = new List<MifareDesfireAppModel>()
                                    };
                                    if (device.AppIDList.Any())
                                    {
                                        foreach (uint appID in device.AppIDList)
                                        {
                                            DesfireChip.AppList.Add(new MifareDesfireAppModel(appID));
                                        }
                                    }
                                }
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
                        treeViewParentNodes.Any(x => x.UidNumber == GenericChip.UID))
                    {
                        //select current parentnode (card) on reader
                        treeViewParentNodes.First(x => x.UidNumber == GenericChip.UID).IsSelected = true;
                        treeViewParentNodes.First(x => x.IsSelected).IsBeingProgrammed = true;
                    }

                    //are there tasks present to process?
                    while (taskIndex < taskHandler.TaskCollection.Count)
                    {
                        if (_runSelectedOnly)
                        {
                            switch (SelectedSetupViewModel)
                            {
                                case GenericChipTaskViewModel ssVM:
                                    if (ssVM.IsValidSelectedTaskIndex != false)
                                    {
                                        taskIndex = taskHandler.TaskCollection.IndexOf(ssVM);
                                    }

                                    break;
                                case CommonTaskViewModel ssVM:
                                    if (ssVM.IsValidSelectedTaskIndex != false)
                                    {
                                        taskIndex = taskHandler.TaskCollection.IndexOf(ssVM);
                                    }

                                    break;
                                case MifareClassicSetupViewModel ssVM:
                                    if (ssVM.IsValidSelectedTaskIndex != false)
                                    {
                                        taskIndex = taskHandler.TaskCollection.IndexOf(ssVM);
                                    }

                                    break;
                                case MifareDesfireSetupViewModel ssVM:
                                    if (ssVM.IsValidSelectedTaskIndex != false)
                                    {
                                        taskIndex = taskHandler.TaskCollection.IndexOf(ssVM);
                                    }

                                    break;
                                case MifareUltralightSetupViewModel ssVM:
                                    if (ssVM.IsValidSelectedTaskIndex != false)
                                    {
                                        taskIndex = taskHandler.TaskCollection.IndexOf(ssVM);
                                    }

                                    break;
                            }
                        }


                        Thread.Sleep(10);

                        taskTimeout.Stop();
                        taskTimeout.Start();
                        taskTimeout.Tag = taskIndex;

                        SelectedSetupViewModel = taskHandler.TaskCollection[taskIndex];

                        //decide what type of task to process next. use exact array positions 
                        switch (taskHandler.TaskCollection[taskIndex])
                        {

                            case CommonTaskViewModel csVM:
                                switch (csVM.SelectedTaskType)
                                {
                                    case TaskType_CommonTask.CreateReport:
                                        taskTimeout.Stop();

                                        switch ((taskHandler.TaskCollection[taskIndex] as CommonTaskViewModel).TaskErr)
                                        {
                                            case ERROR.AuthenticationError:
                                                taskIndex++;
                                                taskTimeout.Stop();
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.ItemAlreadyExistError:
                                                taskIndex++;
                                                taskTimeout.Stop();
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.DeviceNotReadyError:
                                                //taskIndex++;
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.IOError:
                                                taskIndex++;
                                                taskTimeout.Stop();
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.NoError:
                                                taskIndex++;
                                                taskTimeout.Stop();
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.IsNotFalse:
                                                taskIndex++;
                                                taskTimeout.Stop();
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.IsNotTrue:
                                                taskIndex++;
                                                taskTimeout.Stop();
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.Empty:
                                                taskTimeout.Start();
                                                taskTimeout.Stop();

                                                if ((taskHandler.TaskCollection[taskIndex] as CommonTaskViewModel).SelectedExecuteConditionErrorLevel == ERROR.Empty)
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

                                                    (taskHandler.TaskCollection[taskIndex] as CommonTaskViewModel).GenericChip = GenericChip;
                                                    (taskHandler.TaskCollection[taskIndex] as CommonTaskViewModel).DesfireChip = DesfireChip;
                                                    (taskHandler.TaskCollection[taskIndex] as CommonTaskViewModel).AvailableTasks = taskHandler.TaskCollection;
                                                    (taskHandler.TaskCollection[taskIndex] as CommonTaskViewModel).WriteReportCommand.Execute(reportReaderWriter);
                                                }

                                                else
                                                {
                                                    int targetIndex = 0;
                                                    // targeted ERRORLEVEL ist not "EMPTY" so check if targeted ERRORLEVEL fits current ERRORLEVEL
                                                    try
                                                    {
                                                        if (taskIndices.TryGetValue((taskHandler.TaskCollection[taskIndex] as CommonTaskViewModel).SelectedExecuteConditionTaskIndex, out targetIndex))
                                                        {
                                                            switch (taskHandler.TaskCollection[targetIndex])
                                                            {
                                                                case GenericChipTaskViewModel tsVM:
                                                                    if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as CommonTaskViewModel).SelectedExecuteConditionErrorLevel)
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

                                                                        (taskHandler.TaskCollection[taskIndex] as CommonTaskViewModel).GenericChip = GenericChip;
                                                                        (taskHandler.TaskCollection[taskIndex] as CommonTaskViewModel).DesfireChip = DesfireChip;
                                                                        (taskHandler.TaskCollection[taskIndex] as CommonTaskViewModel).AvailableTasks = taskHandler.TaskCollection;
                                                                        (taskHandler.TaskCollection[taskIndex] as CommonTaskViewModel).WriteReportCommand.Execute(reportReaderWriter);
                                                                    }

                                                                    else
                                                                    {
                                                                        taskIndex++;
                                                                    }

                                                                    break;

                                                                case CommonTaskViewModel tsVM:
                                                                    if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as CommonTaskViewModel).SelectedExecuteConditionErrorLevel)
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

                                                                        (taskHandler.TaskCollection[taskIndex] as CommonTaskViewModel).GenericChip = GenericChip;
                                                                        (taskHandler.TaskCollection[taskIndex] as CommonTaskViewModel).DesfireChip = DesfireChip;
                                                                        (taskHandler.TaskCollection[taskIndex] as CommonTaskViewModel).AvailableTasks = taskHandler.TaskCollection;
                                                                        (taskHandler.TaskCollection[taskIndex] as CommonTaskViewModel).WriteReportCommand.Execute(reportReaderWriter);
                                                                    }

                                                                    else
                                                                    {
                                                                        taskIndex++;
                                                                    }

                                                                    break;

                                                                case MifareClassicSetupViewModel tsVM:
                                                                    if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as CommonTaskViewModel).SelectedExecuteConditionErrorLevel)
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

                                                                        (taskHandler.TaskCollection[taskIndex] as CommonTaskViewModel).GenericChip = GenericChip;
                                                                        (taskHandler.TaskCollection[taskIndex] as CommonTaskViewModel).AvailableTasks = taskHandler.TaskCollection;
                                                                        (taskHandler.TaskCollection[taskIndex] as CommonTaskViewModel).WriteReportCommand.Execute(reportReaderWriter);
                                                                    }

                                                                    else
                                                                    {
                                                                        taskIndex++;
                                                                    }

                                                                    break;

                                                                case MifareDesfireSetupViewModel tsVM:
                                                                    if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as CommonTaskViewModel).SelectedExecuteConditionErrorLevel)
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

                                                                        (taskHandler.TaskCollection[taskIndex] as CommonTaskViewModel).GenericChip = GenericChip;
                                                                        (taskHandler.TaskCollection[taskIndex] as CommonTaskViewModel).DesfireChip = DesfireChip;
                                                                        (taskHandler.TaskCollection[taskIndex] as CommonTaskViewModel).AvailableTasks = taskHandler.TaskCollection;
                                                                        (taskHandler.TaskCollection[taskIndex] as CommonTaskViewModel).WriteReportCommand.Execute(reportReaderWriter);
                                                                    }

                                                                    else
                                                                    {
                                                                        taskIndex++;
                                                                    }

                                                                    break;

                                                                case MifareUltralightSetupViewModel tsVM:
                                                                    if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as CommonTaskViewModel).SelectedExecuteConditionErrorLevel)
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

                                                                        (taskHandler.TaskCollection[taskIndex] as CommonTaskViewModel).GenericChip = GenericChip;
                                                                        (taskHandler.TaskCollection[taskIndex] as CommonTaskViewModel).AvailableTasks = taskHandler.TaskCollection;
                                                                        (taskHandler.TaskCollection[taskIndex] as CommonTaskViewModel).WriteReportCommand.Execute(reportReaderWriter);
                                                                    }

                                                                    else
                                                                    {
                                                                        taskIndex++;
                                                                    }

                                                                    break;
                                                            }
                                                        }
                                                    }

                                                    catch
                                                    {
                                                        taskIndex++;
                                                    }
                                                }

                                                taskTimeout.Start();
                                                break;
                                        }
                                        break;

                                    case TaskType_CommonTask.CheckLogicCondition:
                                        switch ((taskHandler.TaskCollection[taskIndex] as CommonTaskViewModel).TaskErr)
                                        {
                                            case ERROR.AuthenticationError:
                                                taskIndex++;
                                                taskTimeout.Stop();
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.ItemAlreadyExistError:
                                                taskIndex++;
                                                taskTimeout.Stop();
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.DeviceNotReadyError:
                                                //taskIndex++;
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.IOError:
                                                taskIndex++;
                                                taskTimeout.Stop();
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.IsNotTrue:
                                            case ERROR.IsNotFalse:
                                                taskIndex++;
                                                break;

                                            case ERROR.NoError:
                                                taskIndex++;
                                                taskTimeout.Stop();
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.Empty:
                                                taskTimeout.Start();

                                                if ((taskHandler.TaskCollection[taskIndex] as CommonTaskViewModel).SelectedExecuteConditionErrorLevel == ERROR.Empty)
                                                {
                                                    (taskHandler.TaskCollection[taskIndex] as CommonTaskViewModel).GenericChip = GenericChip;
                                                    (taskHandler.TaskCollection[taskIndex] as CommonTaskViewModel).CheckLogicCondition.Execute(taskHandler.TaskCollection);
                                                }

                                                else
                                                {

                                                    if (taskIndices.TryGetValue((taskHandler.TaskCollection[taskIndex] as CommonTaskViewModel).SelectedExecuteConditionTaskIndex, out int targetIndex))
                                                    {
                                                        switch (taskHandler.TaskCollection[targetIndex])
                                                        {
                                                            case GenericChipTaskViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as CommonTaskViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as CommonTaskViewModel).GenericChip = GenericChip;
                                                                    (taskHandler.TaskCollection[taskIndex] as CommonTaskViewModel).DesfireChip = DesfireChip;
                                                                    (taskHandler.TaskCollection[taskIndex] as CommonTaskViewModel).CheckLogicCondition.Execute(taskHandler.TaskCollection);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                            case CommonTaskViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as CommonTaskViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as CommonTaskViewModel).GenericChip = GenericChip;
                                                                    (taskHandler.TaskCollection[taskIndex] as CommonTaskViewModel).DesfireChip = DesfireChip;
                                                                    (taskHandler.TaskCollection[taskIndex] as CommonTaskViewModel).CheckLogicCondition.Execute(taskHandler.TaskCollection);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                            case MifareClassicSetupViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as CommonTaskViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as CommonTaskViewModel).GenericChip = GenericChip;
                                                                    (taskHandler.TaskCollection[taskIndex] as CommonTaskViewModel).DesfireChip = DesfireChip;
                                                                    (taskHandler.TaskCollection[taskIndex] as CommonTaskViewModel).CheckLogicCondition.Execute(taskHandler.TaskCollection);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                            case MifareDesfireSetupViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as CommonTaskViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as CommonTaskViewModel).GenericChip = GenericChip;
                                                                    (taskHandler.TaskCollection[taskIndex] as CommonTaskViewModel).DesfireChip = DesfireChip;
                                                                    (taskHandler.TaskCollection[taskIndex] as CommonTaskViewModel).CheckLogicCondition.Execute(taskHandler.TaskCollection);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                            case MifareUltralightSetupViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as CommonTaskViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as CommonTaskViewModel).GenericChip = GenericChip;
                                                                    (taskHandler.TaskCollection[taskIndex] as CommonTaskViewModel).DesfireChip = DesfireChip;
                                                                    (taskHandler.TaskCollection[taskIndex] as CommonTaskViewModel).CheckLogicCondition.Execute(taskHandler.TaskCollection);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                        }
                                                    }
                                                }
                                                break;
                                        }
                                        break;
                                }
                                break;

                            case GenericChipTaskViewModel csVM:
                                switch (csVM.SelectedTaskType)
                                {
                                    case TaskType_GenericChipTask.ChipIsOfType:
                                        taskTimeout.Start();

                                        switch ((taskHandler.TaskCollection[taskIndex] as GenericChipTaskViewModel).TaskErr)
                                        {
                                            case ERROR.AuthenticationError:
                                                (taskHandler.TaskCollection[taskIndex] as GenericChipTaskViewModel).IsFocused = false;
                                                taskIndex++;
                                                break;

                                            case ERROR.ItemAlreadyExistError:
                                                (taskHandler.TaskCollection[taskIndex] as GenericChipTaskViewModel).IsFocused = false;
                                                taskIndex++;
                                                break;

                                            case ERROR.DeviceNotReadyError:
                                                taskIndex++;

                                                taskTimeout.Stop();
                                                break;

                                            case ERROR.IOError:
                                                (taskHandler.TaskCollection[taskIndex] as GenericChipTaskViewModel).IsFocused = false;
                                                taskIndex++;
                                                break;

                                            case ERROR.NoError:
                                                (taskHandler.TaskCollection[taskIndex] as GenericChipTaskViewModel).IsFocused = false;
                                                taskIndex++;
                                                break;

                                            case ERROR.IsNotTrue:
                                                (taskHandler.TaskCollection[taskIndex] as GenericChipTaskViewModel).IsFocused = false;
                                                taskIndex++;
                                                break;

                                            case ERROR.IsNotFalse:
                                                (taskHandler.TaskCollection[taskIndex] as GenericChipTaskViewModel).IsFocused = false;
                                                taskIndex++;
                                                break;

                                            case ERROR.Empty:

                                                (taskHandler.TaskCollection[taskIndex] as GenericChipTaskViewModel).IsFocused = true;

                                                if ((taskHandler.TaskCollection[taskIndex] as GenericChipTaskViewModel).SelectedExecuteConditionErrorLevel == ERROR.Empty)
                                                {
                                                    (taskHandler.TaskCollection[taskIndex] as GenericChipTaskViewModel).CheckChipType.Execute(null);
                                                }
                                                else
                                                {

                                                    if (taskIndices.TryGetValue((taskHandler.TaskCollection[taskIndex] as GenericChipTaskViewModel).SelectedExecuteConditionTaskIndex, out int index))
                                                    {
                                                        switch (taskHandler.TaskCollection[index])
                                                        {
                                                            case GenericChipTaskViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as GenericChipTaskViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as GenericChipTaskViewModel).CheckChipType.Execute(null);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                            case CommonTaskViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as CommonTaskViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as GenericChipTaskViewModel).CheckChipType.Execute(null);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                            case MifareClassicSetupViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareClassicSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as GenericChipTaskViewModel).CheckChipType.Execute(null);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                            case MifareDesfireSetupViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as GenericChipTaskViewModel).CheckChipType.Execute(null);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                            case MifareUltralightSetupViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareUltralightSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as GenericChipTaskViewModel).CheckChipType.Execute(null);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                        }
                                                    }
                                                }

                                                break;

                                            default:
                                                break;
                                        }
                                        break;
                                }
                                break;

                            case MifareClassicSetupViewModel csVM:
                                switch (csVM.SelectedTaskType) //[taskIndex] as MifareClassicSetupViewModel).SelectedTaskType) {
                                {
                                    case TaskType_MifareClassicTask.ReadData:
                                        switch ((taskHandler.TaskCollection[taskIndex] as MifareClassicSetupViewModel).TaskErr)
                                        {
                                            case ERROR.AuthenticationError:
                                                taskIndex++;
                                                taskTimeout.Stop();
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.ItemAlreadyExistError:
                                                taskIndex++;
                                                taskTimeout.Stop();
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.DeviceNotReadyError:
                                                //taskIndex++;
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.IOError:
                                                taskIndex++;
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.NoError:
                                                taskIndex++;
                                                taskTimeout.Stop();
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.Empty:
                                                taskTimeout.Start();

                                                if ((taskHandler.TaskCollection[taskIndex] as MifareClassicSetupViewModel).SelectedExecuteConditionErrorLevel == ERROR.Empty)
                                                {
                                                    (taskHandler.TaskCollection[taskIndex] as MifareClassicSetupViewModel).ReadDataCommand.Execute(null);
                                                }
                                                else
                                                {

                                                    if (taskIndices.TryGetValue((taskHandler.TaskCollection[taskIndex] as MifareClassicSetupViewModel).SelectedExecuteConditionTaskIndex, out int index))
                                                    {
                                                        switch (taskHandler.TaskCollection[index])
                                                        {
                                                            case GenericChipTaskViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareClassicSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as MifareClassicSetupViewModel).ReadDataCommand.Execute(null);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                            case CommonTaskViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareClassicSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as MifareClassicSetupViewModel).ReadDataCommand.Execute(null);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                            case MifareClassicSetupViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareClassicSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as MifareClassicSetupViewModel).ReadDataCommand.Execute(null);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                            case MifareDesfireSetupViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareClassicSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as MifareClassicSetupViewModel).ReadDataCommand.Execute(null);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                            case MifareUltralightSetupViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareClassicSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as MifareClassicSetupViewModel).ReadDataCommand.Execute(null);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                        }
                                                    }
                                                }

                                                break;
                                        }
                                        break;

                                    case TaskType_MifareClassicTask.WriteData:

                                        switch ((taskHandler.TaskCollection[taskIndex] as MifareClassicSetupViewModel).TaskErr)
                                        {
                                            case ERROR.AuthenticationError:
                                                taskIndex++;
                                                taskTimeout.Stop();
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.ItemAlreadyExistError:
                                                taskIndex++;
                                                taskTimeout.Stop();
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.DeviceNotReadyError:
                                                //taskIndex++;
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.IOError:
                                                taskIndex++;
                                                taskTimeout.Stop();
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.Empty:
                                                taskTimeout.Start();

                                                if ((taskHandler.TaskCollection[taskIndex] as MifareClassicSetupViewModel).SelectedExecuteConditionErrorLevel == ERROR.Empty)
                                                {
                                                    (taskHandler.TaskCollection[taskIndex] as MifareClassicSetupViewModel).WriteDataCommand.Execute(null);
                                                }
                                                else
                                                {

                                                    if (taskIndices.TryGetValue((taskHandler.TaskCollection[taskIndex] as MifareClassicSetupViewModel).SelectedExecuteConditionTaskIndex, out int index))
                                                    {
                                                        switch (taskHandler.TaskCollection[index])
                                                        {
                                                            case GenericChipTaskViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareClassicSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as MifareClassicSetupViewModel).WriteDataCommand.Execute(null);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                            case CommonTaskViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareClassicSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as MifareClassicSetupViewModel).WriteDataCommand.Execute(null);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                            case MifareClassicSetupViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareClassicSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as MifareClassicSetupViewModel).WriteDataCommand.Execute(null);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                            case MifareDesfireSetupViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareClassicSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as MifareClassicSetupViewModel).WriteDataCommand.Execute(null);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                            case MifareUltralightSetupViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareClassicSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as MifareClassicSetupViewModel).WriteDataCommand.Execute(null);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                        }
                                                    }
                                                }

                                                break;
                                        }
                                        break;
                                }
                                break;

                            case MifareDesfireSetupViewModel csVM:
                                switch (csVM.SelectedTaskType)
                                {
                                    case TaskType_MifareDesfireTask.FormatDesfireCard:
                                        switch ((taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).TaskErr)
                                        {
                                            case ERROR.AuthenticationError:
                                            case ERROR.ItemAlreadyExistError:
                                                taskIndex++;
                                                taskTimeout.Stop();
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.DeviceNotReadyError:
                                                //taskIndex++;
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.IOError:
                                                taskIndex++;
                                                taskTimeout.Stop();
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.NoError:
                                                taskIndex++;
                                                taskTimeout.Stop();
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.Empty:
                                                taskTimeout.Start();

                                                if ((taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel == ERROR.Empty)
                                                {
                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).FormatDesfireCardCommand.Execute(null);
                                                }
                                                else
                                                {

                                                    if (taskIndices.TryGetValue((taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionTaskIndex, out int targetIndex))
                                                    {
                                                        switch (taskHandler.TaskCollection[targetIndex])
                                                        {
                                                            case GenericChipTaskViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).FormatDesfireCardCommand.Execute(null);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                            case CommonTaskViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).FormatDesfireCardCommand.Execute(null);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                            case MifareClassicSetupViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).FormatDesfireCardCommand.Execute(null);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                            case MifareDesfireSetupViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).FormatDesfireCardCommand.Execute(null);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                            case MifareUltralightSetupViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).FormatDesfireCardCommand.Execute(null);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                        }
                                                    }
                                                }

                                                break;
                                        }
                                        break;

                                    case TaskType_MifareDesfireTask.AppExistCheck:
                                        switch ((taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).TaskErr)
                                        {
                                            case ERROR.AuthenticationError:
                                            case ERROR.ItemAlreadyExistError:
                                                taskIndex++;
                                                taskTimeout.Stop();
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.DeviceNotReadyError:
                                                //taskIndex++;
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.IOError:
                                                taskIndex++;
                                                taskTimeout.Stop();
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.NoError:
                                                taskIndex++;
                                                taskTimeout.Stop();
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.IsNotTrue:
                                                taskIndex++;
                                                taskTimeout.Stop();
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.NotAllowed:
                                                taskIndex++;
                                                taskTimeout.Stop();
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.Empty:
                                                taskTimeout.Start();

                                                if ((taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel == ERROR.Empty)
                                                {
                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).DoesAppExistCommand(GenericChip); //Command.Execute(null);
                                                }


                                                else
                                                {

                                                    if (taskIndices.TryGetValue((taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionTaskIndex, out int targetIndex))
                                                    {
                                                        switch (taskHandler.TaskCollection[targetIndex])
                                                        {
                                                            case GenericChipTaskViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).DoesAppExistCommand(GenericChip);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                            case CommonTaskViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).DoesAppExistCommand(GenericChip);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                            case MifareClassicSetupViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).DoesAppExistCommand(GenericChip);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                            case MifareDesfireSetupViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).DoesAppExistCommand(GenericChip);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                            case MifareUltralightSetupViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).DoesAppExistCommand(GenericChip);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                        }
                                                    }
                                                }

                                                break;
                                        }
                                        break;

                                    case TaskType_MifareDesfireTask.ReadAppSettings:
                                        switch ((taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).TaskErr)
                                        {
                                            case ERROR.AuthenticationError:
                                            case ERROR.ItemAlreadyExistError:
                                                taskIndex++;
                                                taskTimeout.Stop();
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.DeviceNotReadyError:
                                                //taskIndex++;
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.IOError:
                                                taskIndex++;
                                                taskTimeout.Stop();
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.NoError:
                                                taskIndex++;
                                                taskTimeout.Stop();
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.IsNotTrue:
                                                taskIndex++;
                                                taskTimeout.Stop();
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.NotAllowed:
                                                taskIndex++;
                                                taskTimeout.Stop();
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.Empty:
                                                taskTimeout.Start();

                                                if ((taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel == ERROR.Empty)
                                                {
                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).ReadAppSettingsCommand(GenericChip); //Command.Execute(null);
                                                }


                                                else
                                                {

                                                    if (taskIndices.TryGetValue((taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionTaskIndex, out int targetIndex))
                                                    {
                                                        switch (taskHandler.TaskCollection[targetIndex])
                                                        {
                                                            case GenericChipTaskViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).ReadAppSettingsCommand(GenericChip);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                            case CommonTaskViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).ReadAppSettingsCommand(GenericChip);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                            case MifareClassicSetupViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).ReadAppSettingsCommand(GenericChip);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                            case MifareDesfireSetupViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).ReadAppSettingsCommand(GenericChip);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                            case MifareUltralightSetupViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).ReadAppSettingsCommand(GenericChip);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                        }
                                                    }
                                                }

                                                break;
                                        }
                                        break;

                                    case TaskType_MifareDesfireTask.CreateApplication:
                                        switch ((taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).TaskErr)
                                        {
                                            case ERROR.AuthenticationError:
                                                taskIndex++;
                                                taskTimeout.Stop();
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.ItemAlreadyExistError:
                                                taskIndex++;
                                                taskTimeout.Stop();
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.DeviceNotReadyError:
                                                //taskIndex++;
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.IOError:
                                                taskIndex++;
                                                taskTimeout.Stop();
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.OutOfMemory:
                                                taskIndex++;
                                                taskTimeout.Stop();
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.NoError:
                                                taskIndex++;
                                                taskTimeout.Stop();
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.Empty:
                                                taskTimeout.Start();

                                                if ((taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel == ERROR.Empty)
                                                {
                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).CreateAppCommand.Execute(null);
                                                }
                                                else
                                                {

                                                    if (taskIndices.TryGetValue((taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionTaskIndex, out int targetIndex))
                                                    {
                                                        switch (taskHandler.TaskCollection[targetIndex])
                                                        {
                                                            case GenericChipTaskViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).CreateAppCommand.Execute(null);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                            case CommonTaskViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).CreateAppCommand.Execute(null);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                            case MifareClassicSetupViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).CreateAppCommand.Execute(null);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                            case MifareDesfireSetupViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).CreateAppCommand.Execute(null);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                            case MifareUltralightSetupViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).CreateAppCommand.Execute(null);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                        }
                                                    }
                                                }

                                                break;
                                        }
                                        break;

                                    case TaskType_MifareDesfireTask.AuthenticateApplication:
                                        switch ((taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).TaskErr)
                                        {
                                            case ERROR.AuthenticationError:
                                            case ERROR.ItemAlreadyExistError:
                                                taskIndex++;
                                                taskTimeout.Stop();
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.DeviceNotReadyError:
                                                //taskIndex++;
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.IOError:
                                                taskIndex++;
                                                taskTimeout.Stop();
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.NoError:
                                                taskIndex++;
                                                taskTimeout.Stop();
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.IsNotTrue:
                                                taskIndex++;
                                                taskTimeout.Stop();
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.NotAllowed:
                                                taskIndex++;
                                                taskTimeout.Stop();
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.Empty:
                                                taskTimeout.Start();

                                                if ((taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel == ERROR.Empty)
                                                {
                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).AuthenticateToCardApplicationCommand.Execute(null);
                                                }


                                                else
                                                {

                                                    if (taskIndices.TryGetValue((taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionTaskIndex, out int targetIndex))
                                                    {
                                                        switch (taskHandler.TaskCollection[targetIndex])
                                                        {
                                                            case GenericChipTaskViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).AuthenticateToCardApplicationCommand.Execute(null);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                            case CommonTaskViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).AuthenticateToCardApplicationCommand.Execute(null);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                            case MifareClassicSetupViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).AuthenticateToCardApplicationCommand.Execute(null);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                            case MifareDesfireSetupViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).AuthenticateToCardApplicationCommand.Execute(null);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                            case MifareUltralightSetupViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).AuthenticateToCardApplicationCommand.Execute(null);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                        }
                                                    }
                                                }

                                                break;
                                        }
                                        break;

                                    case TaskType_MifareDesfireTask.DeleteApplication:
                                        switch ((taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).TaskErr)
                                        {
                                            case ERROR.AuthenticationError:
                                                taskIndex++;
                                                taskTimeout.Stop();
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.ItemAlreadyExistError:
                                                taskIndex++;
                                                taskTimeout.Stop();
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.DeviceNotReadyError:
                                                //taskIndex++;
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.IOError:
                                                taskIndex++;
                                                taskTimeout.Stop();
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.NoError:
                                                taskIndex++;
                                                //taskTimeout.IsEnabled = false;
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.Empty:
                                                taskTimeout.Start();

                                                if ((taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel == ERROR.Empty)
                                                {
                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).DeleteSignleCardApplicationCommand.Execute(null);
                                                }
                                                else
                                                {

                                                    if (taskIndices.TryGetValue((taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionTaskIndex, out int targetIndex))
                                                    {
                                                        switch (taskHandler.TaskCollection[targetIndex])
                                                        {
                                                            case GenericChipTaskViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).DeleteSignleCardApplicationCommand.Execute(null);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                            case CommonTaskViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).DeleteSignleCardApplicationCommand.Execute(null);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                            case MifareClassicSetupViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).DeleteSignleCardApplicationCommand.Execute(null);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                            case MifareDesfireSetupViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).DeleteSignleCardApplicationCommand.Execute(null);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                            case MifareUltralightSetupViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).DeleteSignleCardApplicationCommand.Execute(null);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                        }
                                                    }
                                                }

                                                break;
                                        }
                                        break;

                                    case TaskType_MifareDesfireTask.PICCMasterKeyChangeover:
                                        switch ((taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).TaskErr)
                                        {
                                            case ERROR.AuthenticationError:
                                                taskIndex++;
                                                taskTimeout.Stop();
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.ItemAlreadyExistError:
                                                taskIndex++;
                                                taskTimeout.Stop();
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.DeviceNotReadyError:
                                                //taskIndex++;
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.IOError:
                                                taskIndex++;
                                                taskTimeout.Stop();
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.NoError:
                                                taskIndex++;
                                                taskTimeout.Stop();
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.Empty:
                                                taskTimeout.Start();

                                                if ((taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel == ERROR.Empty)
                                                {
                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).ChangeMasterCardKeyCommand.Execute(null);
                                                }
                                                else
                                                {

                                                    if (taskIndices.TryGetValue((taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionTaskIndex, out int targetIndex))
                                                    {
                                                        switch (taskHandler.TaskCollection[targetIndex])
                                                        {
                                                            case GenericChipTaskViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).ChangeMasterCardKeyCommand.Execute(null);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                            case CommonTaskViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).ChangeMasterCardKeyCommand.Execute(null);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                            case MifareClassicSetupViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).ChangeMasterCardKeyCommand.Execute(null);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                            case MifareDesfireSetupViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).ChangeMasterCardKeyCommand.Execute(null);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                            case MifareUltralightSetupViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).ChangeMasterCardKeyCommand.Execute(null);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                        }
                                                    }
                                                }

                                                break;
                                        }
                                        break;

                                    case TaskType_MifareDesfireTask.ApplicationKeyChangeover:
                                        switch ((taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).TaskErr)
                                        {
                                            case ERROR.AuthenticationError:
                                                taskIndex++;
                                                taskTimeout.Stop();
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.ItemAlreadyExistError:
                                                taskIndex++;
                                                taskTimeout.Stop();
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.DeviceNotReadyError:
                                                //taskIndex++;
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.IOError:
                                                taskIndex++;
                                                taskTimeout.Stop();
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.NoError:
                                                taskIndex++;
                                                //taskTimeout.IsEnabled = false;
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.Empty:
                                                taskTimeout.Start();

                                                if ((taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel == ERROR.Empty)
                                                {
                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).ChangeAppKeyCommand.Execute(null);
                                                }
                                                else
                                                {

                                                    if (taskIndices.TryGetValue((taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionTaskIndex, out int targetIndex))
                                                    {
                                                        switch (taskHandler.TaskCollection[targetIndex])
                                                        {
                                                            case GenericChipTaskViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).ChangeAppKeyCommand.Execute(null);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                            case CommonTaskViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).ChangeAppKeyCommand.Execute(null);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                            case MifareClassicSetupViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).ChangeAppKeyCommand.Execute(null);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                            case MifareDesfireSetupViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).ChangeAppKeyCommand.Execute(null);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                            case MifareUltralightSetupViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).ChangeAppKeyCommand.Execute(null);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                        }
                                                    }
                                                }

                                                break;
                                        }
                                        break;

                                    case TaskType_MifareDesfireTask.CreateFile:
                                        switch ((taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).TaskErr)
                                        {
                                            case ERROR.AuthenticationError:
                                                taskIndex++;
                                                taskTimeout.Stop();
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.ItemAlreadyExistError:
                                                taskIndex++;
                                                taskTimeout.Stop();
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.DeviceNotReadyError:
                                                //taskIndex++;
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.IOError:
                                                taskIndex++;
                                                taskTimeout.Stop();
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.OutOfMemory:
                                                taskIndex++;
                                                taskTimeout.Stop();
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.NoError:
                                                taskIndex++;
                                                //taskTimeout.IsEnabled = false;
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.Empty:
                                                taskTimeout.Start();

                                                if ((taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel == ERROR.Empty)
                                                {
                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).CreateFileCommand.Execute(null);
                                                }
                                                else
                                                {

                                                    if (taskIndices.TryGetValue((taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionTaskIndex, out int targetIndex))
                                                    {
                                                        switch (taskHandler.TaskCollection[targetIndex])
                                                        {
                                                            case GenericChipTaskViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).CreateFileCommand.Execute(null);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                            case CommonTaskViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).CreateFileCommand.Execute(null);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                            case MifareClassicSetupViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).CreateFileCommand.Execute(null);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                            case MifareDesfireSetupViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).CreateFileCommand.Execute(null);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                            case MifareUltralightSetupViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).CreateFileCommand.Execute(null);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                        }
                                                    }
                                                }

                                                break;
                                        }
                                        break;

                                    case TaskType_MifareDesfireTask.DeleteFile:
                                        switch ((taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).TaskErr)
                                        {
                                            case ERROR.AuthenticationError:
                                                taskIndex++;
                                                taskTimeout.Stop();
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.ItemAlreadyExistError:
                                                taskIndex++;
                                                taskTimeout.Stop();
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.DeviceNotReadyError:
                                                //taskIndex++;
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.IOError:
                                                taskIndex++;
                                                taskTimeout.Stop();
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.NoError:
                                                taskIndex++;
                                                //taskTimeout.IsEnabled = false;
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.Empty:
                                                taskTimeout.Start();

                                                if ((taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel == ERROR.Empty)
                                                {
                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).DeleteFileCommand.Execute(null);
                                                }
                                                else
                                                {

                                                    if (taskIndices.TryGetValue((taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionTaskIndex, out int targetIndex))
                                                    {
                                                        switch (taskHandler.TaskCollection[targetIndex])
                                                        {
                                                            case GenericChipTaskViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).DeleteFileCommand.Execute(null);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                            case CommonTaskViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).DeleteFileCommand.Execute(null);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                            case MifareClassicSetupViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).DeleteFileCommand.Execute(null);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                            case MifareDesfireSetupViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).DeleteFileCommand.Execute(null);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                            case MifareUltralightSetupViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).DeleteFileCommand.Execute(null);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                        }
                                                    }
                                                }

                                                break;
                                        }
                                        break;

                                    case TaskType_MifareDesfireTask.ReadData:
                                        switch ((taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).TaskErr)
                                        {
                                            case ERROR.AuthenticationError:
                                                taskIndex++;
                                                taskTimeout.Stop();
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.ItemAlreadyExistError:
                                                taskIndex++;
                                                taskTimeout.Stop();
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.DeviceNotReadyError:
                                                //taskIndex++;
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.IOError:
                                                taskIndex++;
                                                taskTimeout.Stop();
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.NoError:
                                                taskIndex++;
                                                taskTimeout.Stop();
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.Empty:
                                                taskTimeout.Start();

                                                if ((taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel == ERROR.Empty)
                                                {
                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).ReadDataCommand.Execute(null);
                                                }
                                                else
                                                {

                                                    if (taskIndices.TryGetValue((taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionTaskIndex, out int targetIndex))
                                                    {
                                                        switch (taskHandler.TaskCollection[targetIndex])
                                                        {
                                                            case GenericChipTaskViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).ReadDataCommand.Execute(null);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                            case CommonTaskViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).ReadDataCommand.Execute(null);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                            case MifareClassicSetupViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).ReadDataCommand.Execute(null);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                            case MifareDesfireSetupViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).ReadDataCommand.Execute(null);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                            case MifareUltralightSetupViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).ReadDataCommand.Execute(null);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                        }
                                                    }
                                                }

                                                break;
                                        }
                                        break;

                                    case TaskType_MifareDesfireTask.WriteData:
                                        switch ((taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).TaskErr)
                                        {
                                            case ERROR.AuthenticationError:
                                                taskIndex++;
                                                taskTimeout.Stop();
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.ItemAlreadyExistError:
                                                taskIndex++;
                                                taskTimeout.Stop();
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.DeviceNotReadyError:
                                                //taskIndex++;
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.IOError:
                                                taskIndex++;
                                                taskTimeout.Stop();
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.OutOfMemory:
                                                taskIndex++;
                                                taskTimeout.Stop();
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.NoError:
                                                taskIndex++;
                                                //taskTimeout.IsEnabled = false;
                                                taskTimeout.Start();
                                                break;

                                            case ERROR.Empty:
                                                taskTimeout.Start();

                                                if ((taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel == ERROR.Empty)
                                                {
                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).WriteDataCommand.Execute(null);
                                                }
                                                else
                                                {

                                                    if (taskIndices.TryGetValue((taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionTaskIndex, out int targetIndex))
                                                    {
                                                        switch (taskHandler.TaskCollection[targetIndex])
                                                        {
                                                            case GenericChipTaskViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).WriteDataCommand.Execute(null);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                            case CommonTaskViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).WriteDataCommand.Execute(null);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                            case MifareClassicSetupViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).WriteDataCommand.Execute(null);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                            case MifareDesfireSetupViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).WriteDataCommand.Execute(null);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                            case MifareUltralightSetupViewModel tsVM:
                                                                if (tsVM.TaskErr == (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedExecuteConditionErrorLevel)
                                                                {
                                                                    (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).WriteDataCommand.Execute(null);
                                                                }
                                                                else
                                                                {
                                                                    taskIndex++;
                                                                }

                                                                break;
                                                        }
                                                    }
                                                }

                                                break;
                                        }
                                        break;
                                }
                                break;

                            case MifareUltralightSetupViewModel ssVM:
                                break;
                        }

                        if (_runSelectedOnly)
                        {
                            break;
                        }

                        RaisePropertyChanged("TreeViewParentNodes");
                    }

                    RaisePropertyChanged("TreeViewParentNodes");

                    taskTimeout.Stop();

                }
                catch (Exception e)
                {
                    LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""), FacilityName);
                }
            });


            thread.ContinueWith((x) =>
            {

                try
                {
                    treeViewParentNodes.First(y => y.IsSelected).IsBeingProgrammed = null;
                    triggerReadChip.IsEnabled = (bool)triggerReadChip.Tag;
                }

                catch { }
            });

            OnNewResetTaskStatusCommand();

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
            Dialogs.Add(new CustomDialogViewModel
            {
                Message = ResourceLoader.GetResource("messageBoxRestartRequiredMessage"),
                Caption = ResourceLoader.GetResource("messageBoxRestartRequiredCaption"),

                OnOk = (sender) =>
                {
                    sender.Close();
                    App.Current.Shutdown();
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

        /// <summary>
        /// 
        /// </summary>
        public ICommand NewReaderSetupDialogCommand => new RelayCommand(OnNewReaderSetupDialog);
        private void OnNewReaderSetupDialog()
        {
            using (RFiDDevice device = RFiDDevice.Instance)
            {
                Dialogs.Add(new SetupViewModel(device)
                {
                    Caption = ResourceLoader.GetResource("windowCaptionReaderSetup"),

                    OnOk = (sender) =>
                    {
                        using (SettingsReaderWriter settings = new SettingsReaderWriter())
                        {
                            DefaultSpecification currentSettings = settings.DefaultSpecification;

                            currentSettings.DefaultReaderProvider = sender.SelectedReader;
                            currentSettings.AutoLoadProjectOnStart = sender.LoadOnStart;
                            currentSettings.LastUsedComPort = sender.ComPort;
                            currentSettings.AutoCheckForUpdates = sender.CheckOnStart;
                            currentSettings.LastUsedBaudRate = sender.SelectedBaudRate;

                            settings.DefaultSpecification = currentSettings;

                            sender.Close();

                            settings.SaveSettings();
                        }
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

                    foreach (RFiDChipParentLayerViewModel vm in databaseReaderWriter.treeViewModel)
                    {
                        TreeViewParentNodes.Add(vm);
                    }

                    foreach (object setup in databaseReaderWriter.setupModel.TaskCollection)
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
        public ObservableCollection<MenuItem> RowContextMenu => rowContextMenuItems;
        private readonly ObservableCollection<MenuItem> rowContextMenuItems;

        /// <summary>
        /// expose contextmenu on row click
        /// </summary>
        public ObservableCollection<MenuItem> EmptySpaceContextMenuItems => emptySpaceContextMenuItems;
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
            mw = (MainWindow)Application.Current.MainWindow;
            mw.Title = string.Format("RFiDGear {0}.{1}.{2} {3}", Version.Major, Version.Minor, Version.Build, Constants.TITLE_SUFFIX);

            if (updateAvailable)
            {
                AskForUpdateNow();
            }

            Task thread = new Task(() =>
            {
                while (true)
                {
                    for (int i = 0; i <= 10; i++)
                    {
                        Thread.Sleep(500);
                        ReaderStatus = string.Format("{0}", DateTime.Now);

                        if (i == 10)
                        {
                            //TODO: Update ReaderStatus frequently
                            //FIXME: Locking RFiDDevice Instance in different Threads not working correctly
                            /*
                            using (RFiDDevice device = RFiDDevice.Instance)
                            {

                                if (device != null)
                                {
                                    //device.ReadChipPublic();
                                    ReaderStatus = ReaderStatus + string.Format("Reader: {1}, UID: {2}", DateTime.Now, device.ReaderUnitName, device.GenericChip.UID);
                                    i = 0;
                                }
                                else
                                    i = 0;
                            }
                            */
                        }
                    }

                }
            });


            thread.ContinueWith((x) =>
            {
            });

            thread.Start();

            if (firstRun)
            {
                firstRun = false;

                try
                {
                    var mySplash = new SplashScreenViewModel();

                    //var catalog = new AssemblyCatalog(System.Reflection.Assembly.GetExecutingAssembly());
                    //var container = new CompositionContainer(catalog);
                    MefHelper.Instance.Container.ComposeParts(this); //Load Plugins Container.ComposeParts(this);
                                                                     //container.Compose(this);

                    using (SettingsReaderWriter settings = new SettingsReaderWriter())
                    {
                        CurrentReader = string.IsNullOrWhiteSpace(settings.DefaultSpecification.DefaultReaderName)
                            ? Enum.GetName(typeof(ReaderTypes), settings.DefaultSpecification.DefaultReaderProvider)
                            : settings.DefaultSpecification.DefaultReaderName;

                        culture = (settings.DefaultSpecification.DefaultLanguage == "german") ? new CultureInfo("de-DE") : new CultureInfo("en-US");

                        var autoLoadLastUsedDB = settings.DefaultSpecification.AutoLoadProjectOnStart;

                        if (autoLoadLastUsedDB)
                        {
                            Dialogs.Add(mySplash);
                        }

                        thread = new Task(() =>
                        {
                            if (autoLoadLastUsedDB)
                            {
                                OpenLastProjectFile();
                            }
                        });


                        thread.ContinueWith((x) =>
                        {
                        });

                        thread.RunSynchronously();

                        OnNewResetTaskStatusCommand();

                        Dialogs.Remove(mySplash);
                    }
                }
                catch (Exception ex)
                {
                    LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, ex.Message, ex.InnerException != null ? ex.InnerException.Message : ""), FacilityName);
                }

                using (SettingsReaderWriter settings = new SettingsReaderWriter())
                {
                    if (settings.DefaultSpecification.AutoCheckForUpdates)
                    {
                        updater.StartMonitoring();
                    }
                }

            }
            #endregion
        }
    }
}