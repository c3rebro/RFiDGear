/* Created by SharpDevelop.
 * Date: 10/11/2017
 * Time: 22:15
 *
 */

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using MefMvvm.SharedContracts;
using MefMvvm.SharedContracts.ViewModel;
using MVVMDialogs.ViewModels;

using RFiDGear.DataAccessLayer.Remote.FromIO;
using RFiDGear.DataAccessLayer;
using RFiDGear.Model;



using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Xml.Serialization;


namespace RFiDGear.ViewModel.TaskSetupViewModels
{
    /// <summary>
    /// Description of MifareClassicSetupViewModel.
    /// </summary>
    public class MifareUltralightSetupViewModel : ObservableObject, IUserDialogViewModel
    {
        #region Fields
        private readonly EventLog eventLog = new EventLog("Application", ".", Assembly.GetEntryAssembly().GetName().Name);

        private MifareUltralightChipModel chipModel;
        private MifareUltralightPageModel pageModel;

        private DatabaseReaderWriter databaseReaderWriter;
        private SettingsReaderWriter settings = new SettingsReaderWriter();

        #endregion fields

        #region Constructors

        /// <summary>
        ///
        /// </summary>
        public MifareUltralightSetupViewModel()
        {
            chipModel = new MifareUltralightChipModel(string.Format("Task Description: {0}", SelectedTaskDescription), CARD_TYPE.MifareUltralight);

            childNodeViewModelFromChip = new RFiDChipChildLayerViewModel(pageModel, null, CARD_TYPE.MifareUltralight, null, true);
            childNodeViewModelTemp = new RFiDChipChildLayerViewModel(pageModel, null, CARD_TYPE.MifareUltralight, null, true);

            MifareUltralightPages = CustomConverter.GenerateStringSequence(0, 15).ToArray();

            SelectedUltralightPageCurrent = "0";
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="_selectedSetupViewModel"></param>
        /// <param name="_dialogs"></param>
        public MifareUltralightSetupViewModel(object _selectedSetupViewModel, ObservableCollection<IDialogViewModel> _dialogs)
        {
            try
            {
                MefHelper.Instance.Container.ComposeParts(this); //Load Plugins

                databaseReaderWriter = new DatabaseReaderWriter();

                chipModel = new MifareUltralightChipModel(string.Format("Task Description: {0}", SelectedTaskDescription), CARD_TYPE.MifareUltralight);

                pageModel = new MifareUltralightPageModel(new byte[4], 0);
                pageModel.PageNumber = selectedUltralightPageCurrentAsInt;

                childNodeViewModelFromChip = new RFiDChipChildLayerViewModel(pageModel, null, CARD_TYPE.MifareUltralight, null, true);
                childNodeViewModelTemp = new RFiDChipChildLayerViewModel(pageModel, null, CARD_TYPE.MifareUltralight, null, true);


                if (_selectedSetupViewModel is MifareUltralightSetupViewModel)
                {
                    var properties = typeof(MifareUltralightSetupViewModel).GetProperties(BindingFlags.Public | BindingFlags.Instance);

                    foreach (var p in properties)
                    {
                        // If not writable then cannot null it; if not readable then cannot check it's value
                        if (!p.CanWrite || !p.CanRead) { continue; }

                        var mget = p.GetGetMethod(false);
                        var mset = p.GetSetMethod(false);

                        // Get and set methods have to be public
                        if (mget == null) { continue; }
                        if (mset == null) { continue; }

                        p.SetValue(this, p.GetValue(_selectedSetupViewModel));
                    }
                }

                else
                {
                    CurrentTaskIndex = "0";
                    SelectedTaskDescription = "Enter a Description";
                }

                MifareUltralightPages = CustomConverter.GenerateStringSequence(0, 15).ToArray();

                SelectedUltralightPageCurrent = "0";

                HasPlugins = items != null ? items.Any() : false;

                if (HasPlugins)
                {
                    SelectedPlugin = Items.FirstOrDefault();
                }
            }
            catch (Exception e)
            {
                eventLog.WriteEntry(e.Message, EventLogEntryType.Error);
            }

        }

        #endregion

        #region DataExplorer

        [XmlIgnore]
        public string[] MifareUltralightPages { get; set; }

        /// <summary>
        ///
        /// </summary>
        public string SelectedUltralightPageCurrent
        {
            get => selectedUltralightPageCurrent;
            set
            {
                selectedUltralightPageCurrent = value;
                int.TryParse(value, out selectedUltralightPageCurrentAsInt);
            }
        }
        private string selectedUltralightPageCurrent;
        private int selectedUltralightPageCurrentAsInt;

        /// <summary>
        ///
        /// </summary>
        public bool IsDataExplorerEditTabEnabled
        {
            get => isDataExplorerEditTabEnabled;
            set
            {
                isDataExplorerEditTabEnabled = value;
                OnPropertyChanged(nameof(IsDataExplorerEditTabEnabled));
            }
        }
        private bool isDataExplorerEditTabEnabled;

        /// <summary>
        ///
        /// </summary>
        public RFiDChipChildLayerViewModel ChildNodeViewModelFromChip
        {
            get => childNodeViewModelFromChip;
            set => childNodeViewModelFromChip = value;
        }
        private RFiDChipChildLayerViewModel childNodeViewModelFromChip;

        /// <summary>
        ///
        /// </summary>
        public RFiDChipChildLayerViewModel ChildNodeViewModelTemp
        {
            get => childNodeViewModelTemp;
            set => childNodeViewModelTemp = value;
        }
        private RFiDChipChildLayerViewModel childNodeViewModelTemp;

        #endregion DataExplorer

        #region Plugins

        /// <summary>
        /// True if Count of Imported Views is > 0; See constructor
        /// </summary>
        [XmlIgnore]
        public bool HasPlugins
        {
            get => hasPlugins;
            set
            {
                hasPlugins = value;
                OnPropertyChanged(nameof(HasPlugins));
            }
        }
        private bool hasPlugins;

        /// <summary>
        /// Selected Plugin in ComboBox
        /// </summary>
        [XmlIgnore]
        public object SelectedPlugin
        {
            get => selectedPlugin;
            set
            {
                selectedPlugin = value;
                OnPropertyChanged(nameof(SelectedPlugin));
            }
        }
        private object selectedPlugin;

        /// <summary>
        /// Imported Views by URI
        /// </summary>
        [XmlIgnore]
        [ImportMany()]
        public Lazy<IUIExtension, IUIExtensionDetails>[] Items
        {
            get => items;

            set
            {
                items = (from g in value
                         orderby g.Metadata.SortOrder, g.Metadata.Name
                         select g).ToArray();

                OnPropertyChanged(nameof(Items));
            }
        }
        private Lazy<IUIExtension, IUIExtensionDetails>[] items;
        #endregion

        #region Dependency Properties

        /// <summary>
        /// The Indexnumber of the ExecuteCondition Task As String
        /// </summary>
        public string SelectedExecuteConditionTaskIndex
        {
            get => selectedExecuteConditionTaskIndex;

            set
            {
                selectedExecuteConditionTaskIndex = value;
                IsValidSelectedExecuteConditionTaskIndex = int.TryParse(value, out selectedExecuteConditionTaskIndexAsInt);
                OnPropertyChanged(nameof(SelectedExecuteConditionTaskIndex));
            }
        }
        private string selectedExecuteConditionTaskIndex;

        /// <summary>
        ///
        /// </summary>
        public bool? IsValidSelectedExecuteConditionTaskIndex
        {
            get => isValidSelectedExecuteConditionTaskIndex;
            set
            {
                isValidSelectedExecuteConditionTaskIndex = value;
                OnPropertyChanged(nameof(IsValidSelectedExecuteConditionTaskIndex));
            }
        }
        private bool? isValidSelectedExecuteConditionTaskIndex;

        /// <summary>
        ///
        /// </summary>
        public int SelectedExecuteConditionTaskIndexAsInt => selectedExecuteConditionTaskIndexAsInt;
        private int selectedExecuteConditionTaskIndexAsInt;

        /// <summary>
        /// 
        /// </summary>
        public ERROR SelectedExecuteConditionErrorLevel
        {
            get => selectedExecuteConditionErrorLevel;

            set
            {
                selectedExecuteConditionErrorLevel = value;
                OnPropertyChanged(nameof(SelectedExecuteConditionErrorLevel));
            }
        }
        private ERROR selectedExecuteConditionErrorLevel;

        /// <summary>
        /// 
        /// </summary>
        [XmlIgnore]
        public ERROR CurrentTaskErrorLevel { get; set; }

        /// <summary>
        ///
        /// </summary>
        public bool? IsTaskCompletedSuccessfully
        {
            get => isTaskCompletedSuccessfully;
            set
            {
                isTaskCompletedSuccessfully = value;
                OnPropertyChanged(nameof(IsTaskCompletedSuccessfully));
            }
        }
        private bool? isTaskCompletedSuccessfully;

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public string StatusText
        {
            get => statusText;
            set
            {
                statusText = value;
                OnPropertyChanged(nameof(StatusText));
            }
        }
        private string statusText;

        /// <summary>
        ///
        /// </summary>
        public string CurrentTaskIndex
        {
            get =>
                currentTaskIndex;
            set
            {
                currentTaskIndex = value;
                IsValidSelectedTaskIndex = int.TryParse(value, out selectedTaskIndexAsInt);
            }
        }
        private string currentTaskIndex;

        /// <summary>
        ///
        /// </summary>
        public int SelectedTaskIndexAsInt => selectedTaskIndexAsInt;
        private int selectedTaskIndexAsInt;

        /// <summary>
        ///
        /// </summary>
        public bool? IsValidSelectedTaskIndex
        {
            get => isValidSelectedTaskIndex;
            set
            {
                isValidSelectedTaskIndex = value;
                OnPropertyChanged(nameof(IsValidSelectedTaskIndex));
            }
        }
        private bool? isValidSelectedTaskIndex;

        /// <summary>
        ///
        /// </summary>
        public TaskType_MifareUltralightTask SelectedTaskType
        {
            get => selectedTaskType;
            set
            {
                selectedTaskType = value;
                switch (selectedTaskType)
                {
                    case TaskType_MifareUltralightTask.ReadData:
                    case TaskType_MifareUltralightTask.WriteData:
                        IsDataExplorerEditTabEnabled = true;
                        break;

                    default:
                        IsDataExplorerEditTabEnabled = false;
                        break;
                }
                OnPropertyChanged(nameof(SelectedTaskType));
            }
        }
        private TaskType_MifareUltralightTask selectedTaskType;

        /// <summary>
        ///
        /// </summary>
        public string SelectedTaskDescription
        {
            get => selectedAccessBitsTaskDescription;
            set
            {
                selectedAccessBitsTaskDescription = value;
                OnPropertyChanged(nameof(SelectedTaskDescription));
            }
        }
        private string selectedAccessBitsTaskDescription;

        /// <summary>
        /// 
        /// </summary>
        [XmlIgnore]
        public SettingsReaderWriter Settings => settings;

        #endregion General Properties

        #region Commands

        public IAsyncRelayCommand SaveSettings => new AsyncRelayCommand(OnNewSaveSettingsCommand);
        private async Task OnNewSaveSettingsCommand()
        {
            SettingsReaderWriter settings = new SettingsReaderWriter();
            await settings.SaveSettings();
        }

        /// <summary>
        /// 
        /// </summary>
        public ICommand ReadDataCommand => new RelayCommand(OnNewReadDataCommand);
        private void OnNewReadDataCommand()
        {
            //Mouse.OverrideCursor = Cursors.Wait;
            CurrentTaskErrorLevel = ERROR.Empty;

            var classicTask =
                new Task(() =>
                        {
                            using (var device = ReaderDevice.Instance)
                            {
                                if (device != null)
                                {
                                    StatusText = string.Format("{0}: {1}\n", DateTime.Now, ResourceLoader.GetResource(""));
                                }
                                else
                                {
                                    CurrentTaskErrorLevel = ERROR.NotReadyError;
                                    return;
                                }

                                OnPropertyChanged(nameof(ChildNodeViewModelTemp));

                                return;
                            }
                        });

            classicTask.Start();

            classicTask.ContinueWith((x) =>
                                     {
                                         //Mouse.OverrideCursor = null;

                                         if (CurrentTaskErrorLevel == ERROR.NoError)
                                         {
                                             IsTaskCompletedSuccessfully = true;
                                         }
                                         else
                                         {
                                             IsTaskCompletedSuccessfully = false;
                                         }
                                     });
        }

        /// <summary>
        /// 
        /// </summary>
        public ICommand WriteDataCommand => new RelayCommand(OnNewWriteDataCommand);
        private void OnNewWriteDataCommand()
        {
            //Mouse.OverrideCursor = Cursors.Wait;

            CurrentTaskErrorLevel = ERROR.Empty;

            var classicTask =
                new Task(() =>
                         {
                             using (var device = ReaderDevice.Instance)
                             {
                                 StatusText = string.Format("{0}: {1}\n", DateTime.Now, ResourceLoader.GetResource("textBoxStatusTextBoxDllLoaded"));

                             }
                         });

            if (CurrentTaskErrorLevel == ERROR.Empty)
            {
                CurrentTaskErrorLevel = ERROR.NotReadyError;

                classicTask.ContinueWith((x) =>
                                         {
                                             //Mouse.OverrideCursor = null;

                                             if (CurrentTaskErrorLevel == ERROR.NoError)
                                             {
                                                 IsTaskCompletedSuccessfully = true;
                                             }
                                             else
                                             {
                                                 IsTaskCompletedSuccessfully = false;
                                             }
                                         }); //TaskScheduler.FromCurrentSynchronizationContext()

                classicTask.Start();
            }

            return;
        }

        #endregion Commands

        #region IUserDialogViewModel Implementation

        [XmlIgnore]
        public bool IsModal { get; private set; }

        public virtual void RequestClose()
        {
            if (OnCloseRequest != null)
            {
                OnCloseRequest(this);
            }
            else
            {
                Close();
            }
        }

        public event EventHandler DialogClosing;

        public ICommand OKCommand => new RelayCommand(Ok);

        protected virtual void Ok()
        {
            if (OnOk != null)
            {
                OnOk(this);
            }
            else
            {
                Close();
            }
        }

        public ICommand CancelCommand => new RelayCommand(Cancel);

        protected virtual void Cancel()
        {
            if (OnCancel != null)
            {
                OnCancel(this);
            }
            else
            {
                Close();
            }
        }

        public ICommand AuthCommand => new RelayCommand(Auth);

        protected virtual void Auth()
        {
            if (OnAuth != null)
            {
                OnAuth(this);
            }
            else
            {
                Close();
            }
        }

        [XmlIgnore]
        public Action<MifareUltralightSetupViewModel> OnOk { get; set; }

        [XmlIgnore]
        public Action<MifareUltralightSetupViewModel> OnCancel { get; set; }

        [XmlIgnore]
        public Action<MifareUltralightSetupViewModel> OnAuth { get; set; }

        [XmlIgnore]
        public Action<MifareUltralightSetupViewModel> OnCloseRequest { get; set; }

        public void Close()
        {
            DialogClosing?.Invoke(this, new EventArgs());
        }

        public void Show(IList<IDialogViewModel> collection)
        {
            collection.Add(this);
        }

        #endregion IUserDialogViewModel Implementation

        #region Localization

        /// <summary>
        /// localization strings
        /// </summary>
        public string LocalizationResourceSet { get; set; }

        [XmlIgnore]
        public string Caption
        {
            get => _Caption;
            set
            {
                _Caption = value;
                OnPropertyChanged(nameof(Caption));
            }
        }
        private string _Caption;

        #endregion Localization
    }
}