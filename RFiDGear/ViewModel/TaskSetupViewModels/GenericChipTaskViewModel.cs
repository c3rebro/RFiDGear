/*
 * Created by SharpDevelop.
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
using System.Globalization;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Xml.Serialization;
using Elatec.NET;
using System.Linq;
using LibLogicalAccess;
using System.Diagnostics;

namespace RFiDGear.ViewModel
{
    /// <summary>
    /// Description of CommonTaskViewModel.
    /// </summary>
    public class GenericChipTaskViewModel : ObservableObject, IUserDialogViewModel, IGenericTaskModel
    {
        #region fields
        private readonly EventLog eventLog = new EventLog("Application", ".", Assembly.GetEntryAssembly().GetName().Name);

        private protected ReportReaderWriter reportReaderWriter;
        private protected Checkpoint checkpoint;
        private protected readonly ObservableCollection<object> _availableTasks;

        #endregion fields

        #region constructors

        /// <summary>
        ///
        /// </summary>
        public GenericChipTaskViewModel()
        {

        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="_selectedSetupViewModel"></param>
        /// <param name="_dialogs"></param>
        public GenericChipTaskViewModel(object _selectedSetupViewModel, ObservableCollection<object> _tasks, ObservableCollection<IDialogViewModel> _dialogs)
        {
            try
            {
                if (_selectedSetupViewModel is GenericChipTaskViewModel)
                {
                    var properties = typeof(GenericChipTaskViewModel).GetProperties(BindingFlags.Public | BindingFlags.Instance);

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
                    SelectedExecuteConditionErrorLevel = ERROR.Empty;
                    SelectedExecuteConditionTaskIndex = "0";
                }

            }
            catch (Exception e)
            {
                eventLog.WriteEntry(e.Message, EventLogEntryType.Error);
            }
        }

        #endregion

        #region Dialogs

        [XmlIgnore]
        public ObservableCollection<IDialogViewModel> Dialogs => dialogs;
        private ObservableCollection<IDialogViewModel> dialogs = new ObservableCollection<IDialogViewModel>();

        #endregion Dialogs

        #region Visual Properties

        /// <summary>
        ///
        /// </summary>
        public bool IsFocused
        {
            get => isFocused;
            set => isFocused = value;
        }
        private bool isFocused;

        #endregion

        #region Dependency Properties

        
        /// <summary>
        /// 
        /// </summary>
        public string SelectedUIDOfChip
        {
            get => selectedUIDOfChip;

            set
            {
                selectedUIDOfChip = value;
                OnPropertyChanged(nameof(SelectedUIDOfChip));
            }
        }
        private string selectedUIDOfChip;

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
        [XmlIgnore]
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
        [XmlIgnore]
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
        public CARD_TYPE SelectedChipType
        {
            get => selectedChipType;
            set
            {
                selectedChipType = value;
                OnPropertyChanged(nameof(SelectedChipType));
            }
        }
        private CARD_TYPE selectedChipType;

        /// <summary>
        /// 
        /// </summary>
        public string CurrentTaskIndex
        {
            get => selectedTaskIndex;

            set
            {
                selectedTaskIndex = value;
                IsValidSelectedTaskIndex = int.TryParse(value, out selectedTaskIndexAsInt);
            }
        }
        private string selectedTaskIndex;

        /// <summary>
        /// 
        /// </summary>
        [XmlIgnore]
        public ERROR CurrentTaskErrorLevel { get; set; }

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
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
        public int SelectedTaskIndexAsInt => selectedTaskIndexAsInt;
        private int selectedTaskIndexAsInt;

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
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
        public TaskType_GenericChipTask SelectedTaskType
        {
            get => selectedTaskType;
            set
            {
                selectedTaskType = value;
                switch (value)
                {
                    case TaskType_GenericChipTask.None:

                        break;

                    case TaskType_GenericChipTask.ChipIsOfType:

                        break;

                    case TaskType_GenericChipTask.ChangeDefault:

                        break;
                }
                OnPropertyChanged(nameof(SelectedTaskType));
            }
        }
        private TaskType_GenericChipTask selectedTaskType;

        /// <summary>
        ///
        /// </summary>
        public string SelectedTaskDescription
        {
            get => selectedTaskDescription;
            set
            {
                selectedTaskDescription = value;
                OnPropertyChanged(nameof(SelectedTaskDescription));
            }
        }
        private string selectedTaskDescription;

        #endregion General Properties

        #region Commands

        /// <summary>
        /// 
        /// </summary>
        public IAsyncRelayCommand SaveSettings => new AsyncRelayCommand(OnNewSaveSettingsCommand);
        private async Task OnNewSaveSettingsCommand()
        {
            SettingsReaderWriter settings = new SettingsReaderWriter();
            await settings.SaveSettings();
        }

        /// <summary>
        /// 
        /// </summary>
        public IAsyncRelayCommand CheckChipType => new AsyncRelayCommand<List<GenericChipModel>>((x) => OnNewCheckChipTypeCommand(x));

        private async Task<ERROR> OnNewCheckChipTypeCommand()
        {
            return await OnNewCheckChipTypeCommand(null);
        }
        private async Task<ERROR> OnNewCheckChipTypeCommand(List<GenericChipModel> chipList)
        {
            CurrentTaskErrorLevel = ERROR.Empty;

            List<GenericChipModel> chipListToUse;

            using (var device = ReaderDevice.Instance)
            {
                if (device != null)
                {
                    var result = ERROR.Empty;

                    if (chipList != null)
                    {
                        chipListToUse = chipList;
                    }
                    else
                    {
                        await device.ReadChipPublic();
                        chipListToUse = device.GenericChip;
                    }
                    
                    if (((int)SelectedChipType | 0xF000) == 0xF000) // Do NOT Check for explicit Subtype e.g desfire ev1 >2k, 4k, 8k etc.<
                    {
                        if (chipListToUse.Where(x => (CARD_TYPE)((int)x.CardType & 0xF000) == (CARD_TYPE)SelectedChipType).Any())
                        {
                            result = ERROR.NoError;
                        }
                        else
                        {
                            result = ERROR.IsNotTrue;
                        }

                    }
                    else // Take explicit Type into account
                    {
                        if (chipListToUse.Where(x => x.CardType == SelectedChipType).Any())
                        {
                            result = ERROR.NoError;
                        }
                        else
                        {
                            result = ERROR.IsNotTrue;
                        }
                    }

                    CurrentTaskErrorLevel = result;
                }
                else
                {
                    CurrentTaskErrorLevel = ERROR.NotReadyError;
                }
            }

            if (CurrentTaskErrorLevel == ERROR.NoError)
            {
                IsTaskCompletedSuccessfully = true;
            }
            else
            {
                IsTaskCompletedSuccessfully = false;
            }

            return CurrentTaskErrorLevel;
        }

        /// <summary>
        /// 
        /// </summary>
        public IAsyncRelayCommand CheckChipIsMultiTecChip => new AsyncRelayCommand<List<GenericChipModel>>((x) => OnNewCheckChipIsMultiTecChipCommand(x));
        private async Task<ERROR> OnNewCheckChipIsMultiTecChipCommand(List<GenericChipModel> chipList)
        {
            CurrentTaskErrorLevel = ERROR.Empty;

            List<GenericChipModel> chipListToUse;

            using (var device = ReaderDevice.Instance)
            {
                if (device != null)
                {
                    var result = ERROR.Empty;

                    if (chipList != null)
                    {
                        chipListToUse = chipList.FirstOrDefault().Childs;
                    }
                    else
                    {
                        result = await device.ReadChipPublic();
                        chipListToUse = device.GenericChip;
                    }


                    if (chipListToUse != null && chipListToUse.Count >= 1)
                    {
                        result = ERROR.NoError;
                    }
                    else
                    {
                        result = ERROR.IsNotTrue;
                    }

                    CurrentTaskErrorLevel = result;
                }
                else
                {
                    CurrentTaskErrorLevel = ERROR.NotReadyError;
                }
            }

            if (CurrentTaskErrorLevel == ERROR.NoError)
            {
                IsTaskCompletedSuccessfully = true;
            }
            else
            {
                IsTaskCompletedSuccessfully = false;
            }

            return CurrentTaskErrorLevel;
        }

        /// <summary>
        /// 
        /// </summary>
        public ICommand CheckChipUID => new AsyncRelayCommand(OnNewCheckChipUIDCommand);
        private async Task<ERROR> OnNewCheckChipUIDCommand()
        {
            CurrentTaskErrorLevel = ERROR.Empty;

            using (var device = ReaderDevice.Instance)
            {
                if (device != null)
                {
                    var result = await device.ReadChipPublic();

                    if (result == ERROR.NoError)
                    {
                        if (device.GenericChip
                            .Where(x => x.UID.ToLower(CultureInfo.CurrentCulture) == SelectedUIDOfChip.ToLower(CultureInfo.CurrentCulture))
                            .Any())
                        {
                            result = ERROR.NoError;
                        }
                        else
                        {
                            result = ERROR.IsNotTrue;
                        }

                        CurrentTaskErrorLevel = result;

                    }
                }
                else
                {
                    CurrentTaskErrorLevel = ERROR.NotReadyError;
                }
            }

            if (CurrentTaskErrorLevel == ERROR.NoError)
            {
                IsTaskCompletedSuccessfully = true;
            }
            else
            {
                IsTaskCompletedSuccessfully = false;
            }

            return CurrentTaskErrorLevel;
        }
        #endregion

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

        public IAsyncRelayCommand OKCommand => new AsyncRelayCommand(Ok);

        protected async virtual Task Ok()
        {
            if (OnOk != null)
            {
                await OnOk(this);
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
        public Func<GenericChipTaskViewModel, Task> OnOk { get; set; }

        [XmlIgnore]
        public Action<GenericChipTaskViewModel> OnCancel { get; set; }

        [XmlIgnore]
        public Action<GenericChipTaskViewModel> OnAuth { get; set; }

        [XmlIgnore]
        public Action<GenericChipTaskViewModel> OnCloseRequest { get; set; }

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