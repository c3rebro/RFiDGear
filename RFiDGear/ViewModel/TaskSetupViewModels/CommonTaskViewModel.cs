/*
 * Created by SharpDevelop.
 * Date: 10/11/2017
 * Time: 22:15
 *
 */

using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;

using MvvmDialogs.ViewModels;

using RFiDGear.DataAccessLayer;
using RFiDGear.Model;

using Log4CSharp;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Xml.Serialization;

namespace RFiDGear.ViewModel
{
    /// <summary>
    /// Description of ReportTaskViewModel.
    /// </summary>
    public class CommonTaskViewModel : ViewModelBase, IUserDialogViewModel, IGenericTaskModel
    {
        #region Fields
        private static readonly string FacilityName = "RFiDGear";

        private protected ReportReaderWriter reportReaderWriter;
        private protected Checkpoint checkpoint;

        [XmlIgnore]
        public ReportReaderWriter ReportReaderWriterToUse { get; set; }

        [XmlIgnore]
        public ObservableCollection<object> AvailableTasks { get; set; }

        [XmlIgnore]
        public GenericChipModel GenericChip { get; set; }

        [XmlIgnore]
        public MifareDesfireChipModel DesfireChip { get; set; }

        [XmlIgnore]
        public MifareClassicChipModel ClassicChip { get; set; }

        [XmlIgnore]
        public Dictionary<string, string> Args { get; set; }
        #endregion

        #region Constructors

        /// <summary>
        ///
        /// </summary>
        public CommonTaskViewModel()
        {
            CurrentTaskErrorLevel = ERROR.Empty;

            checkpoint = new Checkpoint();
            Checkpoints = new ObservableCollection<Checkpoint>();
            Args = new Dictionary<string, string>();

            IsLogicFuncTaskLogicFuncEnabled = true;
            IsLogicFuncTaskCountFuncEnabled = false;
            IsLogicFuncTaskCompareWithEnabled = false;

        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="_selectedSetupViewModel"></param>
        /// <param name="_dialogs"></param>
        public CommonTaskViewModel(object _selectedSetupViewModel, ObservableCollection<object> _tasks = null, ObservableCollection<IDialogViewModel> _dialogs = null)
        {
            try
            {
                CurrentTaskErrorLevel = ERROR.Empty;

                checkpoint = new Checkpoint();
                Checkpoints = new ObservableCollection<Checkpoint>();
                Args = new Dictionary<string, string>();

                IsLogicFuncTaskLogicFuncEnabled = true;
                IsLogicFuncTaskCountFuncEnabled = false;
                IsLogicFuncTaskCompareWithEnabled = false;

                if (_selectedSetupViewModel is CommonTaskViewModel)
                {
                    PropertyInfo[] properties = typeof(CommonTaskViewModel).GetProperties(BindingFlags.Public | BindingFlags.Instance);

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

                    var copy = Checkpoints;
                    Checkpoints = new ObservableCollection<Checkpoint>(copy);

                    using (ReportReaderWriter reader = new ReportReaderWriter())
                    {
                        if (!string.IsNullOrEmpty(reportTemplatePath))
                        {
                            reader.ReportTemplatePath = reportTemplatePath;

                            TemplateFields = new ObservableCollection<string>(reader.GetReportFields().OrderBy(x => x));

                            RaisePropertyChanged("SelectedTemplateField");
                        }
                    }
                }

                else
                {
                    CurrentTaskIndex = "0";
                    SelectedTaskDescription = "Enter a Description";
                    SelectedExecuteConditionErrorLevel = ERROR.Empty;
                    SelectedExecuteConditionTaskIndex = "0";
                    SelectedCounterTrigger = EQUALITY_OPERATOR.EQUAL;
                    SelectedCheckpointCounter = "0";

                    try
                    {
                        reportReaderWriter = new ReportReaderWriter();
                    }

                    catch (Exception e)
                    {
                        LogWriter.CreateLogEntry(e, FacilityName);
                    }
                }

                AvailableTasks = _tasks;
                NumberOfCheckpoints = CustomConverter.GenerateStringSequence(0, 60).ToArray();
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(e, FacilityName);
            }
        }

        #endregion

        #region Dialogs
        [XmlIgnore]
        public ObservableCollection<IDialogViewModel> Dialogs => dialogs;
        private readonly ObservableCollection<IDialogViewModel> dialogs = new ObservableCollection<IDialogViewModel>();

        /// <summary>
        /// 
        /// </summary>
        //		[XmlIgnore]
        //		private ObservableCollection<IDialogViewModel> dialogs = new ObservableCollection<IDialogViewModel>();
        //		public ObservableCollection<IDialogViewModel> Dialogs
        //		{
        //			get { return dialogs; }
        //			set { dialogs = value; }
        //		}

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

        #region Common Task Properties

        #region Task Properties

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
        [XmlIgnore]
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
                RaisePropertyChanged("SelectedExecuteConditionErrorLevel");
            }
        }
        private ERROR selectedExecuteConditionErrorLevel;

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
                RaisePropertyChanged("IsTaskCompletedSuccessfully");
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
                RaisePropertyChanged("IsValidSelectedTaskIndex");
            }
        }
        private bool? isValidSelectedTaskIndex;

        /// <summary>
        ///
        /// </summary>
        public TaskType_CommonTask SelectedTaskType
        {
            get => selectedTaskType;
            set
            {
                selectedTaskType = value;
                switch (value)
                {
                    case TaskType_CommonTask.None:
                        IsLogicCheckerTabEnabled = false;
                        IsTabPageLogicTaskSettingsViewEnabled = false;
                        IsReportSetupTabEnabled = false;
                        IsTabPageReportSettingsViewEnabled = false;
                        break;

                    case TaskType_CommonTask.CreateReport:
                        IsLogicCheckerTabEnabled = false;
                        IsReportSetupTabEnabled = true;
                        SelectedTabIndex = 1;
                        IsTabPageLogicTaskSettingsViewEnabled = false;
                        IsTabPageReportSettingsViewEnabled = true;
                        break;

                    case TaskType_CommonTask.CheckLogicCondition:
                        IsLogicCheckerTabEnabled = true;
                        IsReportSetupTabEnabled = false;
                        SelectedTabIndex = 0;
                        IsTabPageLogicTaskSettingsViewEnabled = true;
                        IsTabPageReportSettingsViewEnabled = false;
                        break;

                    case TaskType_CommonTask.ChangeDefault:
                        IsLogicCheckerTabEnabled = true;
                        IsTabPageReportSettingsViewEnabled = true;
                        IsReportSetupTabEnabled = true;
                        IsTabPageLogicTaskSettingsViewEnabled = true;
                        break;
                }
                RaisePropertyChanged("SelectedTaskType");
            }
        }
        private TaskType_CommonTask selectedTaskType;

        /// <summary>
        /// 
        /// </summary>
        public int SelectedTabIndex
        {
            set
            {
                selectedTabIndex = value;
                RaisePropertyChanged("SelectedTabIndex");
            }
            get => selectedTabIndex;
        }
        private int selectedTabIndex;

        /// <summary>
        /// 
        /// </summary>
        public bool IsTabPageLogicTaskSettingsViewEnabled
        {
            set
            {
                isTabPageLogicTaskSettingsViewEnabled = value;
                RaisePropertyChanged("IsTabPageLogicTaskSettingsViewEnabled");
            }
            get => isTabPageLogicTaskSettingsViewEnabled;
        }
        private bool isTabPageLogicTaskSettingsViewEnabled;

        /// <summary>
        /// 
        /// </summary>
        public bool IsTabPageReportSettingsViewEnabled
        {
            set
            {
                isTabPageReportSettingsViewEnabled = value;
                RaisePropertyChanged("IsTabPageReportSettingsViewEnabled");
            }
            get => isTabPageReportSettingsViewEnabled;
        }
        private bool isTabPageReportSettingsViewEnabled;

        /// <summary>
        ///
        /// </summary>
        public string SelectedTaskDescription
        {
            get => selectedTaskDescription;
            set
            {
                selectedTaskDescription = value;
                RaisePropertyChanged("SelectedTaskDescription");
            }
        }
        private string selectedTaskDescription;

        /// <summary>
        /// The Indexnumber of the Task As String
        /// </summary>
        public string CurrentTaskIndex
        {
            get => selectedTaskIndex;

            set
            {
                selectedTaskIndex = value;
                IsValidSelectedTaskIndex = int.TryParse(value, out selectedTaskIndexAsInt);
                RaisePropertyChanged("SelectedTaskIndexForThisReport");
            }
        }
        private string selectedTaskIndex;

        /// <summary>
        /// Result of this Task
        /// </summary>
        [XmlIgnore]
        public ERROR CurrentTaskErrorLevel { get; set; }

        #endregion

        #region Shared Properties

        /// <summary>
        /// 
        /// </summary>
        public bool IsReportSetupTabEnabled
        {
            get => isReportSetupTabEnabled;

            set
            {
                isReportSetupTabEnabled = value;
                RaisePropertyChanged("IsReportSetupTabEnabled");
            }
        }
        private bool isReportSetupTabEnabled;

        /// <summary>
        /// 
        /// </summary>
        public bool IsLogicCheckerTabEnabled
        {
            get => isLogicCheckerTabEnabled;
            set
            {
                isLogicCheckerTabEnabled = value;
                RaisePropertyChanged("IsLogicCheckerTabEnabled");
            }
        }
        private bool isLogicCheckerTabEnabled;

        /// <summary>
        /// 
        /// </summary>
        public ObservableCollection<Checkpoint> Checkpoints
        {
            get => checkpoints;
            set
            {
                checkpoints = value;
                RaisePropertyChanged("Checkpoints");
            }
        }
        private ObservableCollection<Checkpoint> checkpoints;

        /// <summary>
        /// 
        /// </summary>
        public Checkpoint SelectedCheckpoint
        {
            get => selectedCheckpoint;
            set
            {
                selectedCheckpoint = value;
                if (selectedCheckpoint != null)
                {
                    Content = selectedCheckpoint.Content;
                    SelectedErrorLevel = selectedCheckpoint.ErrorLevel;
                    SelectedTaskIndexFromAvailableTasks = selectedCheckpoint.TaskIndex;
                    SelectedTemplateField = selectedCheckpoint.TemplateField;
                }

                RaisePropertyChanged("SelectedCheckpoint");
            }
        }
        private Checkpoint selectedCheckpoint;

        /// <summary>
        /// Collection of Tasks that were created. Select One to add a report entry. Called "Checkpoint"
        /// </summary>
        [XmlIgnore]
        public ObservableCollection<string> AvailableTaskIndices
        {
            get
            {
                var availableTaskIndices = new ObservableCollection<string>();
                foreach (object ssVMO in AvailableTasks)
                {
                    switch (ssVMO)
                    {
                        case CommonTaskViewModel ssVM:
                            availableTaskIndices.Add(ssVM.CurrentTaskIndex);
                            break;
                        case GenericChipTaskViewModel ssVM:
                            availableTaskIndices.Add(ssVM.CurrentTaskIndex);
                            break;
                        case MifareClassicSetupViewModel ssVM:
                            availableTaskIndices.Add(ssVM.CurrentTaskIndex);
                            break;
                        case MifareDesfireSetupViewModel ssVM:
                            availableTaskIndices.Add(ssVM.CurrentTaskIndex);
                            break;
                        case MifareUltralightSetupViewModel ssVM:
                            availableTaskIndices.Add(ssVM.CurrentTaskIndex);
                            break;
                    }
                }

                availableTaskIndices.Add(string.Empty);

                return availableTaskIndices;
            }
        }

        /// <summary>
        /// The Selected Error Level that Trigger the Field in the PDF to be filled
        /// </summary>
        public ERROR SelectedErrorLevel
        {
            get => selectedErrorLevel;
            set
            {
                selectedErrorLevel = value;
                RaisePropertyChanged("SelectedErrorLevel");
            }
        }
        private ERROR selectedErrorLevel;

        /// <summary>
        /// 
        /// </summary>
        public string SelectedTaskIndexFromAvailableTasks
        {
            get => selectedTaskIndexFromAvailableTasks;

            set
            {
                selectedTaskIndexFromAvailableTasks = value;
                RaisePropertyChanged("SelectedTaskIndexFromAvailableTasks");
            }
        }
        private string selectedTaskIndexFromAvailableTasks;

        /// <summary>
        /// 
        /// </summary>
        [XmlIgnore]
        public string[] NumberOfCheckpoints { get; set; }

        /// <summary>
        /// The CheckCount of the Checkpoint As String
        /// </summary>
        public string SelectedCheckpointCounter
        {
            get => selectedCheckpointCounter;

            set
            {
                selectedCheckpointCounter = value;
                int.TryParse(value, out selectedCheckpointCounterAsInt);
                RaisePropertyChanged("SelectedCheckpointCounter");
            }
        }
        private string selectedCheckpointCounter;

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public int SelectedCheckpointCounterAsInt => selectedCheckpointCounterAsInt;
        private int selectedCheckpointCounterAsInt;

        /// <summary>
        /// 
        /// </summary>
        [XmlIgnore]
        public string[] AvailableCounterTrigger { get; set; }


        /// <summary>
        /// 
        /// </summary>
        public EQUALITY_OPERATOR SelectedCounterTrigger { get; set; }

        #endregion

        #endregion

        #region Logic Task Editor

        /// <summary>
        /// The LOGIC Condition that will be applied between every added (RFID)Task e.g. "MifareDesfireTask" Object
        /// </summary>
        public LOGIC_STATE SelectedLogicCondition
        {
            get => selectedLogicCondition;
            set
            {
                selectedLogicCondition = value;

                switch (selectedLogicCondition)
                {
                    case LOGIC_STATE.AND:
                    case LOGIC_STATE.OR:
                    case LOGIC_STATE.NAND:
                    case LOGIC_STATE.NOR:
                    case LOGIC_STATE.NOT:
                        IsLogicFuncTaskLogicFuncEnabled = true;
                        IsLogicFuncTaskCompareWithEnabled = false;
                        IsLogicFuncTaskCompareWithEnabled = false;
                        break;

                    case LOGIC_STATE.COUNT:
                        IsLogicFuncTaskLogicFuncEnabled = true;
                        IsLogicFuncTaskCountFuncEnabled = true;
                        IsLogicFuncTaskCompareWithEnabled = false;
                        break;

                    case LOGIC_STATE.COMPARE:
                        IsLogicFuncTaskLogicFuncEnabled = false;
                        IsLogicFuncTaskCountFuncEnabled = false;
                        IsLogicFuncTaskCompareWithEnabled = true;
                        break;
                }
                RaisePropertyChanged("SelectedLogicCondition");
            }
        }
        private LOGIC_STATE selectedLogicCondition;

        /// <summary>
        /// 
        /// </summary>
        [XmlIgnore]
        public bool IsLogicFuncTaskCompareWithEnabled
        {
            get => isLogicFuncTaskCompareWithEnabled;
            set
            {
                isLogicFuncTaskCompareWithEnabled = value;
                RaisePropertyChanged("isLogicFuncTaskCompareWithEnabled");
            }
        }
        private bool isLogicFuncTaskCompareWithEnabled;

        /// <summary>
        /// 
        /// </summary>
        [XmlIgnore]
        public bool IsLogicFuncTaskCountFuncEnabled
        {
            get => isLogicFuncTaskCountFuncEnabled;
            set
            {
                isLogicFuncTaskCountFuncEnabled = value;
                RaisePropertyChanged("IsLogicFuncTaskCountFuncEnabled");
            }
        }
        private bool isLogicFuncTaskCountFuncEnabled;

        /// <summary>
        /// 
        /// </summary>
        [XmlIgnore]
        public bool IsLogicFuncTaskLogicFuncEnabled
        {
            get => isLogicFuncTaskLogicFuncEnabled;
            set
            {
                isLogicFuncTaskLogicFuncEnabled = value;
                RaisePropertyChanged("IsLogicFuncTaskLogicFuncEnabled");
            }
        }
        private bool isLogicFuncTaskLogicFuncEnabled;
        /// <summary>
        /// 
        /// </summary>
        public string CompareValue
        {
            get => compareValue;
            set
            {
                compareValue = value;
                RaisePropertyChanged("CompareValue");
            }
        }
        private string compareValue;


        #endregion

        #region Report Task Editor

        /// <summary>
        /// 
        /// </summary>
        public string ReportTemplatePath
        {
            get => reportTemplatePath;
            set
            {
                reportTemplatePath = value;
                RaisePropertyChanged("ReportTemplatePath");
            }
        }
        private string reportTemplatePath;

        /// <summary>
        /// Available Fields in the Report PDF
        /// </summary>
        [XmlIgnore]
        public ObservableCollection<string> TemplateFields
        {
            get => templateFields;

            set
            {
                templateFields = value;
                RaisePropertyChanged("TemplateFields");
            }
        }
        private ObservableCollection<string> templateFields;

        /// <summary>
        /// 
        /// </summary>
        public string SelectedTemplateField
        {
            get => selectedTemplateField;

            set
            {
                selectedTemplateField = value;
                RaisePropertyChanged("SelectedTemplateField");
            }
        }
        private string selectedTemplateField;

        /// <summary>
        /// 
        /// </summary>
        public string Content
        {
            get => content;

            set
            {
                content = value;
                RaisePropertyChanged("Content");
            }
        }
        private string content;


        #endregion

        #endregion General Properties

        #region Commands

        /// <summary>
        /// 
        /// </summary>
        public ICommand CommandDelegator => new RelayCommand<TaskType_CommonTask>((x) => OnNewCommandDelegatorCall(x));
        private void OnNewCommandDelegatorCall(TaskType_CommonTask classicTaskType)
        {
            switch (classicTaskType)
            {
                case TaskType_CommonTask.CreateReport:
                    OnNewWriteReportCommand(ReportReaderWriterToUse);
                    break;
                case TaskType_CommonTask.CheckLogicCondition:
                    OnNewCheckLogicConditionCommand();
                    break;
                default:
                    break;
            }
        }
        
        /// <summary>
        /// 
        /// </summary>
        public ICommand OpenReportTemplateCommand => new RelayCommand(OnNewOpenReportTemplateCommand);
        private void OnNewOpenReportTemplateCommand()
        {

            CurrentTaskErrorLevel = ERROR.Empty;

            try
            {
                var dlg = new OpenFileDialogViewModel
                {
                    Title = ResourceLoader.GetResource("windowCaptionSaveTasks"),
                    Filter = ResourceLoader.GetResource("filterStringSaveReport"),
                    Multiselect = false
                };


                if (dlg.Show(Dialogs) && dlg.FileName != null)
                {
                    Mouse.OverrideCursor = Cursors.AppStarting;

                    string path = dlg.FileName;

                    if (!String.IsNullOrWhiteSpace(path))
                    {
                        ReportTemplatePath = path;
                        reportReaderWriter = new ReportReaderWriter
                        {
                            ReportTemplatePath = ReportTemplatePath
                        };

                        TemplateFields = new ObservableCollection<string>(reportReaderWriter.GetReportFields().OrderBy(x => x));
                    }

                    Mouse.OverrideCursor = null;

                    RaisePropertyChanged("TemplateFields");
                }

                else
                {
                    ReportTemplatePath = string.Empty;
                }
            }


            catch { }
        }

        /// <summary>
        /// 
        /// </summary>
        public ICommand WriteReportCommand => new RelayCommand<ReportReaderWriter>(OnNewWriteReportCommand);
        private void OnNewWriteReportCommand(ReportReaderWriter _reportReaderWriter)
        {
            CurrentTaskErrorLevel = ERROR.Empty;

            Task commonTask = new Task(() =>
            {
                if (_reportReaderWriter != null)
                {
                    Dictionary<string, int> checkpointDictionary = new Dictionary<string, int>();

                    // create a new key,value pair of taskpositions (int) <-> taskindex (string)
                    // (they could be different as because item at array position 0 can have index "100")
                    foreach (object rfidTaskObject in AvailableTasks)
                    {
                        checkpointDictionary.Add((rfidTaskObject as IGenericTaskModel).CurrentTaskIndex, AvailableTasks.IndexOf(rfidTaskObject));
                    }

                    try
                    {
                        reportReaderWriter = _reportReaderWriter;
                        if (string.IsNullOrEmpty(reportReaderWriter.ReportTemplatePath))
                        {
                            reportReaderWriter.ReportTemplatePath = ReportTemplatePath;
                        }

                        if (!String.IsNullOrWhiteSpace(reportReaderWriter.ReportTemplatePath))
                        {

                            foreach (Checkpoint checkpoint in Checkpoints)
                            {

                                bool hasVariable = false;
                                bool concatenate = false;

                                string temporaryContent = checkpoint.Content;

                                if (temporaryContent.Contains("%UID"))
                                {
                                    temporaryContent = temporaryContent.Replace("%UID", GenericChip?.UID ?? "");
                                    hasVariable = true;
                                }

                                if (temporaryContent.Contains("%CHIPTYPE"))
                                {
                                    temporaryContent = temporaryContent.Replace("%CHIPTYPE", Enum.GetName(typeof(CARD_TYPE), GenericChip?.CardType) ?? "");
                                    hasVariable = true;
                                }

                                if (temporaryContent.Contains("%DATETIME"))
                                {
                                    temporaryContent = temporaryContent.Replace("%DATETIME", DateTime.Now.ToString() ?? "");
                                    hasVariable = true;
                                }

                                if (temporaryContent.Contains("%DATE"))
                                {
                                    temporaryContent = temporaryContent.Replace("%DATE", DateTime.Now.ToString("dd/MM/yyyy") ?? "");
                                    hasVariable = true;
                                }

                                if (temporaryContent.Contains("%FREEMEM"))
                                {
                                    temporaryContent = temporaryContent.Replace("%FREEMEM", DesfireChip?.FreeMemory.ToString() ?? "");
                                    hasVariable = true;
                                }

                                if (temporaryContent.Contains("%LISTAPPS"))
                                {
                                    temporaryContent = temporaryContent.Replace("%LISTAPPS", string.Join(", ", DesfireChip?.AppList.Select(x => x.appID)) ?? "");
                                    hasVariable = true;
                                }

                                if (temporaryContent.Contains("%COUNTAPPS"))
                                {
                                    temporaryContent = temporaryContent.Replace("%COUNTAPPS", DesfireChip?.AppList?.Count.ToString());
                                    hasVariable = true;
                                }

                                if (temporaryContent.Contains("%CONCAT"))
                                {
                                    temporaryContent = temporaryContent.Replace("%CONCAT ", string.Empty);
                                    temporaryContent = temporaryContent.Replace("%CONCAT", string.Empty);
                                    concatenate = true;
                                }

                                if (temporaryContent.Contains("%NEWLINE"))
                                {
                                    temporaryContent = temporaryContent.Replace("%NEWLINE ", "\n");
                                    temporaryContent = temporaryContent.Replace("%NEWLINE", "\n");
                                    concatenate = true;
                                }

                                // the "dollar" indicates an external variable that should be replaced
                                if(temporaryContent.Contains("$"))
                                {
                                    foreach(KeyValuePair<string,string> kvArg in Args)
                                    {
                                        temporaryContent = temporaryContent.Replace(kvArg.Key, kvArg.Value);
                                    }
                                    
                                    concatenate = true;
                                }

                                // Does the targeted Task Equals the selected TaskResult ?
                                // targeted Taskindex ist not "null" so check if targeted ERRORLEVEL fits current ERRORLEVEL
                                if (checkpointDictionary.TryGetValue(checkpoint.TaskIndex ?? "-1", out int targetIndex))
                                {
                                    if ((AvailableTasks[targetIndex] as IGenericTaskModel).CurrentTaskErrorLevel == checkpoint.ErrorLevel)
                                    {
                                        if (concatenate)
                                        {
                                            reportReaderWriter.ConcatReportField(checkpoint.TemplateField, temporaryContent);
                                        }
                                        else
                                        {
                                            reportReaderWriter.SetReportField(checkpoint.TemplateField, hasVariable ? temporaryContent : checkpoint.Content);
                                        }
                                    }
                                    else
                                    {
                                        targetIndex++;
                                    }
                                }

                                // The targeted Task does not Exist: Continue Execution anyway...
                                else
                                {
                                    if (concatenate)
                                    {
                                        reportReaderWriter.ConcatReportField(checkpoint.TemplateField, temporaryContent);
                                    }
                                    else
                                    {
                                        reportReaderWriter.SetReportField(checkpoint.TemplateField, hasVariable ? temporaryContent : checkpoint.Content);
                                    }
                                }
                            }
                        }
                    }

                    catch (Exception e)
                    {
                        CurrentTaskErrorLevel = ERROR.IOError;
                        IsTaskCompletedSuccessfully = false;
                        RaisePropertyChanged("TemplateFields");

                        LogWriter.CreateLogEntry(e, FacilityName);

                        return;
                    }

                    CurrentTaskErrorLevel = ERROR.NoError;
                    IsTaskCompletedSuccessfully = true;

                    RaisePropertyChanged("TemplateFields");

                }
            });

            if (CurrentTaskErrorLevel == ERROR.Empty)
            {
                commonTask.ContinueWith((x) =>
                {
                    if (CurrentTaskErrorLevel == ERROR.NoError)
                    {
                        IsTaskCompletedSuccessfully = true;
                    }
                    else
                    {
                        IsTaskCompletedSuccessfully = false;
                    }
                });

                commonTask.RunSynchronously();
            }

            return;
        }

        /// <summary>
        /// 
        /// </summary>
        public ICommand AddEditCheckpointCommand => new RelayCommand(OnNewAddEditCheckpointCommand);
        private void OnNewAddEditCheckpointCommand()
        {
            try
            {
                checkpoint = new Checkpoint
                {
                    ErrorLevel = ERROR.Empty
                };

                if (SelectedTaskType == TaskType_CommonTask.CreateReport)
                {
                    checkpoint.TaskIndex = SelectedTaskIndexFromAvailableTasks;
                    checkpoint.ErrorLevel = SelectedErrorLevel;
                    checkpoint.TemplateField = SelectedTemplateField;
                    checkpoint.Content = Content;

                    Checkpoints.Add(checkpoint);
                }

                else if (SelectedTaskType == TaskType_CommonTask.CheckLogicCondition)
                {
                    checkpoint.TaskIndex = SelectedTaskIndexFromAvailableTasks;
                    checkpoint.ErrorLevel = SelectedErrorLevel;
                    checkpoint.Content = "";
                    checkpoint.CompareValue = CompareValue;

                    Checkpoints.Add(checkpoint);
                }

                RaisePropertyChanged("Checkpoints");
            }

            catch (Exception e)
            {
                LogWriter.CreateLogEntry(e, FacilityName);
            }

            return;
        }

        /// <summary>
        /// 
        /// </summary>
        public ICommand CheckLogicCondition => new RelayCommand<ObservableCollection<object>>(OnNewCheckLogicConditionCommand);
        private void OnNewCheckLogicConditionCommand(ObservableCollection<object> _tasks = null)
        {
            CurrentTaskErrorLevel = ERROR.Empty;

            Task commonTask =
                new Task(() =>
                {
                    try
                    {
                        ERROR result = ERROR.Empty;
                        CurrentTaskErrorLevel = result;

                        // here we are about to compare the results of the added "Checkpoints" in the "Check Condition" Task with the actual 
                        // conditions from the live tasks

                        // lets fill a new vector with the results of all so far executed tasks... We will re-use the checkpoint objects for this
                        ObservableCollection<Checkpoint> results = new ObservableCollection<Checkpoint>();

                        foreach (object task in _tasks)
                        {
                            switch (task)
                            {
                                case CommonTaskViewModel ssVM:
                                    results.Add(new Checkpoint() { ErrorLevel = ssVM.CurrentTaskErrorLevel, TaskIndex = ssVM.CurrentTaskIndex, Content = ssVM.Content, CompareValue = ssVM.CompareValue });
                                    break;
                                case GenericChipTaskViewModel ssVM:
                                    results.Add(new Checkpoint() { ErrorLevel = ssVM.CurrentTaskErrorLevel, TaskIndex = ssVM.CurrentTaskIndex });
                                    break;
                                case MifareClassicSetupViewModel ssVM:
                                    results.Add(new Checkpoint() { ErrorLevel = ssVM.CurrentTaskErrorLevel, TaskIndex = ssVM.CurrentTaskIndex });
                                    break;
                                case MifareDesfireSetupViewModel ssVM:
                                    results.Add(new Checkpoint() { ErrorLevel = ssVM.CurrentTaskErrorLevel, TaskIndex = ssVM.CurrentTaskIndex });
                                    break;
                                case MifareUltralightSetupViewModel ssVM:
                                    results.Add(new Checkpoint() { ErrorLevel = ssVM.CurrentTaskErrorLevel, TaskIndex = ssVM.CurrentTaskIndex });
                                    break;
                                default:
                                    break;
                            }
                        }

                        switch (SelectedLogicCondition)
                        {
                            case LOGIC_STATE.AND:

                                foreach (Checkpoint cp in Checkpoints)
                                {
                                    if (cp.ErrorLevel == results.Where<Checkpoint>(x => x.TaskIndex == cp.TaskIndex).Single().ErrorLevel)
                                    {
                                        continue;
                                    }
                                    else
                                    {
                                        CurrentTaskErrorLevel = ERROR.IsNotTrue;
                                        return;
                                    }

                                }

                                result = ERROR.NoError;
                                break;


                            case LOGIC_STATE.NAND:

                                foreach (Checkpoint cp in Checkpoints)
                                {
                                    for (int i = 0; i < Checkpoints.Count(); i++)
                                    {
                                        if (cp.ErrorLevel == Checkpoints[i].ErrorLevel)
                                        {
                                            continue;
                                        }
                                        else
                                        {
                                            result = ERROR.NoError;
                                            break;
                                        }
                                    }
                                }

                                result = ERROR.IsNotTrue;
                                break;

                            case LOGIC_STATE.NOR:

                                foreach (Checkpoint cp in Checkpoints)
                                {
                                    for (int i = 0; i < Checkpoints.Count(); i++)
                                    {
                                        if (cp.ErrorLevel == Checkpoints[i].ErrorLevel)
                                        {
                                            result = ERROR.IsNotTrue;
                                            break;
                                        }
                                    }
                                }

                                result = ERROR.NoError;
                                break;

                            case LOGIC_STATE.NOT:

                                break;

                            case LOGIC_STATE.OR:

                                foreach (Checkpoint outerCP in Checkpoints)
                                {
                                    foreach (Checkpoint resultCP in results)
                                    {
                                        if (resultCP.TaskIndex == outerCP.TaskIndex && resultCP.ErrorLevel == outerCP.ErrorLevel)
                                        {
                                            CurrentTaskErrorLevel = ERROR.NoError;
                                            return;
                                        }
                                        else
                                        {
                                            continue;
                                        }
                                    }
                                }

                                result = ERROR.IsNotTrue;
                                break;

                            case LOGIC_STATE.COUNT:

                                int loops = 0;

                                foreach (Checkpoint outerCP in Checkpoints)
                                {
                                    foreach (Checkpoint resultCP in results)
                                    {
                                        if (resultCP.TaskIndex == outerCP.TaskIndex && resultCP.ErrorLevel == outerCP.ErrorLevel)
                                        {
                                            loops++;
                                            continue;
                                        }
                                        else
                                        {
                                            continue;
                                        }
                                    }
                                }

                                switch (SelectedCounterTrigger)
                                {
                                    case EQUALITY_OPERATOR.EQUAL:
                                        if (loops == SelectedCheckpointCounterAsInt)
                                        {
                                            CurrentTaskErrorLevel = ERROR.NoError;
                                            return;
                                        }

                                        break;

                                    case EQUALITY_OPERATOR.LESS_OR_EQUAL:
                                        if (loops <= SelectedCheckpointCounterAsInt)
                                        {
                                            CurrentTaskErrorLevel = ERROR.NoError;
                                            return;
                                        }

                                        break;

                                    case EQUALITY_OPERATOR.LESS_THAN:
                                        if (loops < SelectedCheckpointCounterAsInt)
                                        {
                                            CurrentTaskErrorLevel = ERROR.NoError;
                                            return;
                                        }

                                        break;

                                    case EQUALITY_OPERATOR.MORE_OR_EQUAL:
                                        if (loops >= SelectedCheckpointCounterAsInt)
                                        {
                                            CurrentTaskErrorLevel = ERROR.NoError;
                                            return;
                                        }

                                        break;

                                    case EQUALITY_OPERATOR.MORE_THAN:
                                        if (loops > SelectedCheckpointCounterAsInt)
                                        {
                                            CurrentTaskErrorLevel = ERROR.NoError;
                                            return;
                                        }

                                        break;

                                    default:
                                        break;
                                }


                                result = ERROR.IsNotTrue;
                                break;


                            case LOGIC_STATE.COMPARE: //Compare 'TaskResult Content'

                                try
                                {
                                    if (CompareValue.Contains(">="))
                                    {
                                        string[] comparetemp = CompareValue.Replace(" ", string.Empty).Replace(">", string.Empty).Split('=');

                                        //assume 2 values to compare
                                        if (comparetemp.Length == 2)
                                        {
                                            switch (comparetemp[0])
                                            {
                                                case "%FREEMEM":
                                                    uint.TryParse(new string(comparetemp[1].Where(c => Char.IsDigit(c)).ToArray()), out uint compareValueAsUInt);

                                                    if (DesfireChip?.FreeMemory >= compareValueAsUInt)
                                                    {
                                                        CurrentTaskErrorLevel = ERROR.NoError;
                                                        return;
                                                    }

                                                    else
                                                    {
                                                        CurrentTaskErrorLevel = ERROR.IsNotTrue;
                                                    }

                                                    break;

                                                case "%COUNTAPPS":
                                                    uint.TryParse(new string(comparetemp[1].Where(c => Char.IsDigit(c)).ToArray()), out compareValueAsUInt);

                                                    if (DesfireChip?.AppIDs.Count() >= compareValueAsUInt)
                                                    {
                                                        CurrentTaskErrorLevel = ERROR.NoError;
                                                        return;
                                                    }

                                                    else
                                                    {
                                                        CurrentTaskErrorLevel = ERROR.IsNotTrue;
                                                    }

                                                    break;

                                                default:
                                                    break;
                                            }
                                        }
                                    }

                                    else if (CompareValue.Contains("<="))
                                    {
                                        string[] comparetemp = CompareValue.Replace(" ", string.Empty).Replace("<", string.Empty).Split('=');

                                        //assume 2 values to compare
                                        if (comparetemp.Length == 2)
                                        {
                                            switch (comparetemp[0])
                                            {
                                                case "%FREEMEM":
                                                    uint compareValueAsUInt;
                                                    uint.TryParse(new string(comparetemp[1].Where(c => Char.IsDigit(c)).ToArray()), out compareValueAsUInt);

                                                    if (DesfireChip?.FreeMemory <= compareValueAsUInt)
                                                    {
                                                        CurrentTaskErrorLevel = ERROR.NoError;
                                                        return;
                                                    }

                                                    else
                                                    {
                                                        CurrentTaskErrorLevel = ERROR.IsNotTrue;
                                                    }

                                                    break;

                                                default:
                                                    break;
                                            }
                                        }
                                    }

                                    else if (CompareValue.Contains("="))
                                    {
                                        string[] comparetemp = CompareValue.Replace(" ", string.Empty).Split('=');

                                        //assume 2 values to compare
                                        if (comparetemp.Length == 2)
                                        {
                                            switch (comparetemp[0])
                                            {
                                                case "%FREEMEM":
                                                    uint compareValueAsUInt;
                                                    uint.TryParse(new string(comparetemp[1].Where(c => Char.IsDigit(c)).ToArray()), out compareValueAsUInt);

                                                    if (compareValueAsUInt == DesfireChip?.FreeMemory)
                                                    {
                                                        CurrentTaskErrorLevel = ERROR.NoError;
                                                        return;
                                                    }

                                                    else
                                                    {
                                                        CurrentTaskErrorLevel = ERROR.IsNotTrue;
                                                    }

                                                    break;

                                                case "%COUNTAPPS":
                                                    uint.TryParse(new string(comparetemp[1].Where(c => Char.IsDigit(c)).ToArray()), out compareValueAsUInt);

                                                    if (DesfireChip?.AppIDs.Count() >= compareValueAsUInt)
                                                    {
                                                        CurrentTaskErrorLevel = ERROR.NoError;
                                                        return;
                                                    }

                                                    break;

                                                default:
                                                    break;
                                            }
                                        }
                                    }
                                }

                                catch (Exception e)
                                {
                                    LogWriter.CreateLogEntry(e, FacilityName);
                                }

                                result = ERROR.IsNotTrue;

                                break;

                            default:
                                break;
                        }

                        CurrentTaskErrorLevel = result;
                    }
                    catch (Exception e)
                    {
                        LogWriter.CreateLogEntry(e, FacilityName);
                    }

                });

            if (CurrentTaskErrorLevel == ERROR.Empty)
            {
                commonTask.ContinueWith((x) =>
                {
                    if (CurrentTaskErrorLevel == ERROR.NoError)
                    {
                        IsTaskCompletedSuccessfully = true;
                    }
                    else
                    {
                        IsTaskCompletedSuccessfully = false;
                    }
                });

                commonTask.RunSynchronously();
            }

            return;
        }

        /// <summary>
        /// 
        /// </summary>
        public ICommand RemoveCheckpointCommand => new RelayCommand(OnNewRemoveCheckpointCommand);
        private void OnNewRemoveCheckpointCommand()
        {
            Checkpoints.Remove(SelectedCheckpoint);
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
        public Action<CommonTaskViewModel> OnOk { get; set; }

        [XmlIgnore]
        public Action<CommonTaskViewModel> OnCancel { get; set; }

        [XmlIgnore]
        public Action<CommonTaskViewModel> OnAuth { get; set; }

        [XmlIgnore]
        public Action<CommonTaskViewModel> OnCloseRequest { get; set; }

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