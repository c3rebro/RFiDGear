/* Created by SharpDevelop.
 * Date: 10/11/2017
 * Time: 22:15
 *
 */

using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using MefMvvm.SharedContracts;
using MefMvvm.SharedContracts.ViewModel;
using MvvmDialogs.ViewModels;

using RFiDGear.DataAccessLayer;
using RFiDGear.Model;

using Log4CSharp;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Xml.Serialization;

namespace RFiDGear.ViewModel
{
    /// <summary>
    /// Description of MifareClassicSetupViewModel.
    /// </summary>
    public class MifareUltralightSetupViewModel : ViewModelBase, IUserDialogViewModel
    {
        #region Fields

        private readonly string FacilityName = "RFiDGear";

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
            MefHelper.Instance.Container.ComposeParts(this); //Load Plugins

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
                    PropertyInfo[] properties = typeof(MifareUltralightSetupViewModel).GetProperties(BindingFlags.Public | BindingFlags.Instance);

                    foreach (PropertyInfo p in properties)
                    {
                        // If not writable then cannot null it; if not readable then cannot check it's value
                        if (!p.CanWrite || !p.CanRead) { continue; }

                        MethodInfo mget = p.GetGetMethod(false);
                        MethodInfo mset = p.GetSetMethod(false);

                        // Get and set methods have to be public
                        if (mget == null) { continue; }
                        if (mset == null) { continue; }

                        p.SetValue(this, p.GetValue(_selectedSetupViewModel));
                    }
                }

                else
                {
                    SelectedTaskIndex = "0";
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
                LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""), FacilityName);
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

                //ChildNodeViewModelTemp.IsFocused = true;

                RaisePropertyChanged("SelectedDataBlockToReadWrite");
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
                RaisePropertyChanged("IsDataExplorerEditTabEnabled");
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
                RaisePropertyChanged("HasPlugins");
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
                RaisePropertyChanged("SelectedPlugin");
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

                RaisePropertyChanged("Items");
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
                RaisePropertyChanged("SelectedExecuteConditionTaskIndex");
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
                RaisePropertyChanged("IsValidSelectedExecuteConditionTaskIndex");
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
                RaisePropertyChanged("SelectedExecuteConditionErrorLevel");
            }
        }
        private ERROR selectedExecuteConditionErrorLevel;

        /// <summary>
        /// 
        /// </summary>
        [XmlIgnore]
        public ERROR TaskErr { get; set; }

        /// <summary>
        ///
        /// </summary>
        public bool? IsTaskCompletedSuccessfully
        {
            get => isTaskCompletedSuccessfully;
            set
            {
                isTaskCompletedSuccessfully = value;
                RaisePropertyChanged("IsTaskCompletedSuccessfully");
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
                RaisePropertyChanged("StatusText");
            }
        }
        private string statusText;

        /// <summary>
        ///
        /// </summary>
        public string SelectedTaskIndex
        {
            get =>
                //classicKeyAKeyCurrent = SectorTrailer.Split(',')[0];
                selectedAccessBitsTaskIndex;
            set
            {
                selectedAccessBitsTaskIndex = value;
                IsValidSelectedTaskIndex = int.TryParse(value, out selectedTaskIndexAsInt);
            }
        }
        private string selectedAccessBitsTaskIndex;

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
                RaisePropertyChanged("IsValidSelectedTaskIndex");
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
                RaisePropertyChanged("SelectedTaskType");
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
                RaisePropertyChanged("SelectedTaskDescription");
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

        /// <summary>
        /// 
        /// </summary>
        public ICommand ReadDataCommand => new RelayCommand(OnNewReadDataCommand);
        private void OnNewReadDataCommand()
        {
            //Mouse.OverrideCursor = Cursors.Wait;
            TaskErr = ERROR.Empty;

            Task classicTask =
                new Task(() =>
                         {
                             using (RFiDDevice device = RFiDDevice.Instance)
                             {
                                 if (device != null && device.ReadChipPublic() == ERROR.NoError)
                                 {
                                     StatusText = string.Format("{0}: {1}\n", DateTime.Now, ResourceLoader.GetResource(""));



                                 }

                                 else
                                 {
                                     TaskErr = ERROR.DeviceNotReadyError;
                                     return;
                                 }

                                 RaisePropertyChanged("ChildNodeViewModelTemp");

                                 return;
                             }
                         });

            classicTask.Start();

            classicTask.ContinueWith((x) =>
                                     {
                                         //Mouse.OverrideCursor = null;

                                         if (TaskErr == ERROR.NoError)
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

            TaskErr = ERROR.Empty;

            Task classicTask =
                new Task(() =>
                         {
                             using (RFiDDevice device = RFiDDevice.Instance)
                             {
                                 StatusText = string.Format("{0}: {1}\n", DateTime.Now, ResourceLoader.GetResource("textBoxStatusTextBoxDllLoaded"));

                             }
                         });

            if (TaskErr == ERROR.Empty)
            {
                TaskErr = ERROR.DeviceNotReadyError;

                classicTask.ContinueWith((x) =>
                                         {
                                             //Mouse.OverrideCursor = null;

                                             if (TaskErr == ERROR.NoError)
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
                RaisePropertyChanged("Caption");
            }
        }
        private string _Caption;

        #endregion Localization
    }
}