/*
 * Created by SharpDevelop.
 * Date: 10/11/2017
 * Time: 22:15
 *
 */

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using RFiDGear.Models;

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
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.Tasks;
using RFiDGear.Infrastructure.ReaderProviders;
using RFiDGear.Infrastructure.FileAccess;
using RFiDGear.UI.MVVMDialogs.ViewModels.Interfaces;
using RFiDGear.Infrastructure.Tasks.Interfaces;
namespace RFiDGear.ViewModel.TaskSetupViewModels
{
    /// <summary>
    /// Description of CommonTaskViewModel.
    /// </summary>
    public class GenericChipTaskViewModel : ObservableObject, IUserDialogViewModel, IGenericTask
    {
        #region fields
        private readonly EventLog eventLog = new EventLog("Application", ".", Assembly.GetEntryAssembly().GetName().Name);
        private readonly object editedTaskReference; // Tracks the original task instance during edit mode.

        private protected ReportReaderWriter reportReaderWriter;
        private protected Checkpoint checkpoint;
        private protected ObservableCollection<object> availableTasks;

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
                availableTasks = _tasks;

                if (_selectedSetupViewModel is GenericChipTaskViewModel)
                {
                    editedTaskReference = _selectedSetupViewModel;
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
                    editedTaskReference = null;
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

        [XmlIgnore]
        public ObservableCollection<TaskAttemptResult> AttemptResults { get; } = new ObservableCollection<TaskAttemptResult>();

        #endregion Dialogs

        #region Visual Properties

        /// <summary>
        /// The collection of available tasks for validation.
        /// </summary>
        [XmlIgnore]
        public ObservableCollection<object> AvailableTasks
        {
            get => availableTasks;
            set
            {
                availableTasks = value;
                RevalidateSelectedTaskIndex();
                RevalidateSelectedExecuteConditionTaskIndex();
            }
        }

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
                IsValidSelectedExecuteConditionTaskIndex = TaskIndexValidation.TryValidateExecuteConditionIndex(value, SelectedExecuteConditionErrorLevel, AvailableTasks, out _);
                int.TryParse(value, out selectedExecuteConditionTaskIndexAsInt);
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
                RevalidateSelectedExecuteConditionTaskIndex();
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
                IsValidSelectedTaskIndex = TaskIndexValidation.TryValidateTaskIndex(value, AvailableTasks, editedTaskReference ?? this, out _);
                int.TryParse(value, out selectedTaskIndexAsInt);
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
                if (isTaskCompletedSuccessfully == value)
                {
                    return;
                }

                isTaskCompletedSuccessfully = value;
                UiDispatcher.InvokeIfRequired(() => OnPropertyChanged(nameof(IsTaskCompletedSuccessfully)));
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

        private void RevalidateSelectedTaskIndex()
        {
            IsValidSelectedTaskIndex = TaskIndexValidation.TryValidateTaskIndex(CurrentTaskIndex, AvailableTasks, editedTaskReference ?? this, out _);
        }

        private void RevalidateSelectedExecuteConditionTaskIndex()
        {
            IsValidSelectedExecuteConditionTaskIndex = TaskIndexValidation.TryValidateExecuteConditionIndex(SelectedExecuteConditionTaskIndex, SelectedExecuteConditionErrorLevel, AvailableTasks, out _);
        }

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
        public IAsyncRelayCommand CheckChipType => new AsyncRelayCommand<GenericChipModel>((x) => OnNewCheckChipTypeCommand(x));

        private async Task<ERROR> OnNewCheckChipTypeCommand()
        {
            return await OnNewCheckChipTypeCommand(null);
        }
        private async Task<ERROR> OnNewCheckChipTypeCommand(GenericChipModel chipList)
        {
            CurrentTaskErrorLevel = ERROR.Empty;

            GenericChipModel chipToUse;

            using (var device = ReaderDevice.Instance)
            {
                if (device != null)
                {
                    var result = ERROR.Empty;

                    if (chipList != null)
                    {
                        chipToUse = chipList;
                    }
                    else
                    {
                        await device.ReadChipPublic();
                        chipToUse = device.GenericChip;
                    }
                    
                    if (((int)SelectedChipType | 0xF000) == 0xF000) // Do NOT Check for explicit Subtype e.g desfire ev1 >2k, 4k, 8k etc.<
                    {
                        if ((CARD_TYPE)((int)chipToUse.CardType & 0xF000) == (CARD_TYPE)SelectedChipType)
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
                        if (chipToUse.CardType == SelectedChipType)
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
                    CurrentTaskErrorLevel = ERROR.TransportError;
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
        public IAsyncRelayCommand CheckChipIsMultiTecChip => new AsyncRelayCommand<GenericChipModel>((x) => OnNewCheckChipIsMultiTecChipCommand(x));
        private async Task<ERROR> OnNewCheckChipIsMultiTecChipCommand(GenericChipModel chip)
        {
            CurrentTaskErrorLevel = ERROR.Empty;

            GenericChipModel chipToUse;

            using (var device = ReaderDevice.Instance)
            {
                if (device != null)
                {
                    var result = ERROR.Empty;

                    if (chip != null)
                    {
                        chipToUse = chip;
                    }
                    else
                    {
                        result = await device.ReadChipPublic();
                        chipToUse = device.GenericChip;
                    }


                    if (chipToUse != null && chipToUse.HasChilds == true)
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
                    CurrentTaskErrorLevel = ERROR.TransportError;
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
                        if (device.GenericChip.UID.ToLower(CultureInfo.CurrentCulture) == SelectedUIDOfChip.ToLower(CultureInfo.CurrentCulture))
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
                    CurrentTaskErrorLevel = ERROR.TransportError;
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
