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
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Xml.Serialization;
using Org.BouncyCastle.Asn1.X509;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.Tasks;
using RFiDGear.Infrastructure.FileAccess;
using RFiDGear.UI.MVVMDialogs.ViewModels;
using RFiDGear.UI.MVVMDialogs.ViewModels.Interfaces;

namespace RFiDGear.ViewModel.TaskSetupViewModels
{
    /// <summary>
    /// Description of ReportTaskViewModel.
    /// </summary>
    public class CommonTaskViewModel : ObservableObject, IUserDialogViewModel, IGenericTaskModel
    {
        #region Fields
        private static int IterCounter = 1; //Initial Value of Counter: How often have "this" been called (+1 per "run all tasks")
        private readonly EventLog eventLog = new EventLog("Application", ".", Assembly.GetEntryAssembly().GetName().Name);
        // The Counter could be replaced in an pdf by %n; %nn or %nnn. increased once per run all tasks: %n -> 1 on first execution

        private protected ReportReaderWriter reportReaderWriter;
        private protected Checkpoint checkpoint;
        private protected string lastUsedReportPath;

        [XmlIgnore]
        public ReportReaderWriter ReportReaderWriterToUse { get; set; }

        [XmlIgnore]
        public ObservableCollection<object> AvailableTasks { get; set; }

        [XmlIgnore]
        public GenericChipModel GenericChip { get; set; }

        [XmlIgnore]
        public ObservableCollection<TaskAttemptResult> AttemptResults { get; } = new ObservableCollection<TaskAttemptResult>();

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
            eventLog.Source = Assembly.GetEntryAssembly().GetName().Name;

            CurrentTaskErrorLevel = ERROR.Empty;

            checkpoint = new Checkpoint();
            SelectedCheckpoint = checkpoint;
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
                    var properties = typeof(CommonTaskViewModel).GetProperties(BindingFlags.Public | BindingFlags.Instance);

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

                    var cpCopy = Checkpoints;

                    if (cpCopy.Any(x => string.IsNullOrEmpty(x.CheckpointIndex)))
                    {
                        for (int i = 1; i <= cpCopy.Count; i++)
                        {
                            cpCopy[i - 1].CheckpointIndex = string.Format("{0:D3}", i);
                        }
                    }

                    Checkpoints = new ObservableCollection<Checkpoint>(cpCopy);

                    using (var reader = new ReportReaderWriter())
                    {
                        if (!string.IsNullOrEmpty(reportTemplatePath))
                        {
                            reader.ReportTemplateFile = reportTemplatePath;

                            TemplateFields = new ObservableCollection<string>(reader.GetReportFields().OrderBy(x => x));
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
                        eventLog.WriteEntry(e.Message, EventLogEntryType.Error);
                    }
                }

                AvailableTasks = _tasks;
                NumberOfCheckpoints = CustomConverter.GenerateStringSequence(0, 60).ToArray();

                rowContextMenuItems = new ObservableCollection<MenuItem>
                {
                    new MenuItem()
                    {
                        Header = ResourceLoader.GetResource("contextMenuItemCopy"),
                        HorizontalContentAlignment = HorizontalAlignment.Center,
                        VerticalContentAlignment = VerticalAlignment.Center,
                        Command = CopySelectedCheckpoint,
                        IsEnabled = true
                    },

                    new MenuItem()
                    {
                        Header = ResourceLoader.GetResource("contextMenuItemPaste"),
                        HorizontalContentAlignment = HorizontalAlignment.Center,
                        VerticalContentAlignment = VerticalAlignment.Center,
                        Command = PasteSelectedCheckpoint,
                        IsEnabled = true
                    }
                };

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

        /// <summary>
        /// expose contextmenu on row click
        /// </summary>
        [XmlIgnore]
        public ObservableCollection<MenuItem> RowContextMenu => rowContextMenuItems;
        private readonly ObservableCollection<MenuItem> rowContextMenuItems;

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
                        IsTabPageExecuteProgramViewEnabled = false;
                        IsProgramExecuterTabEnabled = false;
                        IsTabPageReportSettingsViewEnabled = false;
                        break;

                    case TaskType_CommonTask.CreateReport:
                        IsLogicCheckerTabEnabled = false;
                        IsReportSetupTabEnabled = true;
                        IsTabPageExecuteProgramViewEnabled = false;
                        IsProgramExecuterTabEnabled = false;
                        SelectedTabIndex = 1;
                        IsTabPageLogicTaskSettingsViewEnabled = false;
                        IsTabPageReportSettingsViewEnabled = true;
                        break;

                    case TaskType_CommonTask.CheckLogicCondition:
                        IsLogicCheckerTabEnabled = true;
                        IsReportSetupTabEnabled = false;
                        IsTabPageExecuteProgramViewEnabled = false;
                        IsProgramExecuterTabEnabled = false;
                        SelectedTabIndex = 0;
                        IsTabPageLogicTaskSettingsViewEnabled = true;
                        IsTabPageReportSettingsViewEnabled = false;
                        break;

                    case TaskType_CommonTask.ExecuteProgram:
                        IsLogicCheckerTabEnabled = false;
                        IsTabPageReportSettingsViewEnabled = false;
                        IsReportSetupTabEnabled = false;
                        IsTabPageLogicTaskSettingsViewEnabled = false;
                        IsTabPageExecuteProgramViewEnabled = true;
                        IsProgramExecuterTabEnabled = true;
                        break;

                    case TaskType_CommonTask.ChangeDefault:
                        IsLogicCheckerTabEnabled = true;
                        IsTabPageReportSettingsViewEnabled = true;
                        IsReportSetupTabEnabled = true;
                        IsTabPageLogicTaskSettingsViewEnabled = true;
                        break;

                    default:
                        break;
                }
                OnPropertyChanged(nameof(SelectedTaskType));
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
                OnPropertyChanged(nameof(SelectedTabIndex));
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
                OnPropertyChanged(nameof(IsTabPageLogicTaskSettingsViewEnabled));
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
                OnPropertyChanged(nameof(IsTabPageReportSettingsViewEnabled));
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
                OnPropertyChanged(nameof(SelectedTaskDescription));
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
                OnPropertyChanged(nameof(IsReportSetupTabEnabled));
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
                OnPropertyChanged(nameof(IsLogicCheckerTabEnabled));
            }
        }
        private bool isLogicCheckerTabEnabled;

        /// <summary>
        /// 
        /// </summary>
        public bool IsTabPageExecuteProgramViewEnabled
        {
            set
            {
                isTabPageExecuteProgramViewEnabled = value;
                OnPropertyChanged(nameof(IsTabPageExecuteProgramViewEnabled));
            }
            get => isTabPageExecuteProgramViewEnabled;
        }
        private bool isTabPageExecuteProgramViewEnabled;

        /// <summary>
        /// 
        /// </summary>
        public bool IsProgramExecuterTabEnabled
        {
            get => isProgramExecuterTabEnabled;
            set
            {
                isProgramExecuterTabEnabled = value;
                OnPropertyChanged(nameof(IsProgramExecuterTabEnabled));
            }
        }
        private bool isProgramExecuterTabEnabled;

        /// <summary>
        /// 
        /// </summary>
        public ObservableCollection<Checkpoint> Checkpoints
        {
            get => checkpoints;
            set
            {
                checkpoints = value;
                OnPropertyChanged(nameof(Checkpoints));
            }
        }
        private ObservableCollection<Checkpoint> checkpoints;

        /// <summary>
        /// 
        /// </summary>
        [XmlIgnore]
        public Checkpoint SelectedCheckpoint
        {
            get => selectedCheckpoint;
            set
            {
                selectedCheckpoint = value;
                if (selectedCheckpoint != null)
                {
                    Content = selectedCheckpoint.Content;
                }
                OnPropertyChanged(nameof(SelectedCheckpoint));
            }
        }
        private Checkpoint selectedCheckpoint;
        private Checkpoint previouslySelectedCheckpoint;

        /// <summary>
        /// Collection of Tasks that were created. Select One to add a report entry. Called "Checkpoint"
        /// </summary>
        [XmlIgnore]
        public ObservableCollection<string> AvailableTaskIndices
        {
            get
            {
                var availableTaskIndices = new ObservableCollection<string>();
                foreach (var ssVMO in AvailableTasks)
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
                OnPropertyChanged(nameof(SelectedCheckpointCounter));
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
                OnPropertyChanged(nameof(SelectedLogicCondition));
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
                OnPropertyChanged(nameof(isLogicFuncTaskCompareWithEnabled));
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
                OnPropertyChanged(nameof(IsLogicFuncTaskCountFuncEnabled));
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
                OnPropertyChanged(nameof(IsLogicFuncTaskLogicFuncEnabled));
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
                OnPropertyChanged(nameof(CompareValue));
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
                OnPropertyChanged(nameof(ReportTemplatePath));
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
                OnPropertyChanged(nameof(TemplateFields));
            }
        }
        private ObservableCollection<string> templateFields;

        /// <summary>
        /// 
        /// </summary>
        public string Content
        {
            get => content;

            set
            {
                content = value;
                OnPropertyChanged(nameof(Content));
            }
        }
        private string content;


        #endregion

        #region ExecuteApplication

        /// <summary>
        /// 
        /// </summary>
        public string ProgramToExecute
        {
            get => programToExecute;
            set
            {
                programToExecute = value;
                OnPropertyChanged(nameof(ProgramToExecute));
            }
        }
        private string programToExecute;

        #endregion

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

        [XmlIgnore]
        public IAsyncRelayCommand CopySelectedCheckpoint => new AsyncRelayCommand(OnNewCopyCheckpointCommand);
        private Task OnNewCopyCheckpointCommand()
        {
            previouslySelectedCheckpoint = SelectedCheckpoint;

            return Task.CompletedTask;
        }

        [XmlIgnore]
        public IAsyncRelayCommand PasteSelectedCheckpoint => new AsyncRelayCommand(OnNewPasteSelectedCheckpointCommand);
        private async Task OnNewPasteSelectedCheckpointCommand()
        {
            await OnNewAddEditCheckpointCommand(previouslySelectedCheckpoint);
        }

        /// <summary>
        /// 
        /// </summary>
        [XmlIgnore]
        public IAsyncRelayCommand CommandDelegator => new AsyncRelayCommand<TaskType_CommonTask>((x) => OnNewCommandDelegatorCall(x));
        private async Task OnNewCommandDelegatorCall(TaskType_CommonTask commonTaskType)
        {
            switch (commonTaskType)
            {
                case TaskType_CommonTask.CreateReport:
                    await OnNewWriteReportCommand(ReportReaderWriterToUse);
                    break;
                case TaskType_CommonTask.CheckLogicCondition:
                    await OnNewCheckLogicConditionCommand();
                    break;
                case TaskType_CommonTask.ExecuteProgram:
                    await OnNewExecuteProgramCommand();
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        [XmlIgnore]
        public ICommand OpenReportTemplateCommand => new RelayCommand(OnNewOpenReportTemplateCommand);
        private void OnNewOpenReportTemplateCommand()
        {
            
            CurrentTaskErrorLevel = ERROR.Empty;

            try
            {
                var dlg = new OpenFileDialogViewModel
                {
                    Title = ResourceLoader.GetResource("windowCaptionOpenReport"),
                    Filter = ResourceLoader.GetResource("filterStringSaveReport"),
                    Multiselect = false
                };


                if (dlg.Show(Dialogs) && dlg.FileName != null)
                {
                    Mouse.OverrideCursor = Cursors.AppStarting;

                    var path = dlg.FileName;
                    
                    if (!String.IsNullOrWhiteSpace(path))
                    {
                        ReportTemplatePath = path;
                        reportReaderWriter = new ReportReaderWriter
                        {
                            ReportTemplateFile = ReportTemplatePath
                        };

                        TemplateFields = new ObservableCollection<string>(reportReaderWriter.GetReportFields().OrderBy(x => x));
                    }

                    Mouse.OverrideCursor = null;

                    OnPropertyChanged(nameof(TemplateFields));
                }

                else
                {
                    //ReportTemplateFile = string.Empty;
                }
            }


            catch(Exception e)
            {
                eventLog.WriteEntry(e.Message, EventLogEntryType.Information);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public IAsyncRelayCommand WriteReportCommand => new AsyncRelayCommand<ReportReaderWriter>(OnNewWriteReportCommand);
        private async Task OnNewWriteReportCommand(ReportReaderWriter _reportReaderWriter)
        {
            CurrentTaskErrorLevel = ERROR.Empty;

            if (_reportReaderWriter != null)
            {
                var checkpointDictionary = new Dictionary<string, int>();

                // create a new key,value pair of taskpositions (int) <-> taskindex (string)
                // (they could be different as because item at array position 0 can have index "100")
                foreach (var rfidTaskObject in AvailableTasks)
                {
                    checkpointDictionary.Add((rfidTaskObject as IGenericTaskModel).CurrentTaskIndex, AvailableTasks.IndexOf(rfidTaskObject));
                }

                try
                {
                    reportReaderWriter = _reportReaderWriter;
                    if (string.IsNullOrEmpty(reportReaderWriter.ReportTemplateFile))
                    {
                        reportReaderWriter.ReportTemplateFile = ReportTemplatePath;
                    }

                    if (!String.IsNullOrWhiteSpace(reportReaderWriter.ReportTemplateFile))
                    {

                        foreach (var checkpoint in Checkpoints)
                        {
                            var hasVariable = false;
                            var concatenate = false;

                            var temporaryContent = checkpoint.Content;

                            if (temporaryContent.Contains("%UID"))
                            {
                                temporaryContent = temporaryContent.Replace("%UID", GenericChip?.UID ?? "");
                                hasVariable = true;
                            }

                            if (temporaryContent.Contains("%CHIPTYPE"))
                            {
                                temporaryContent = temporaryContent.Replace("%CHIPTYPE", ResourceLoader.GetResource(
                                string.Format("ENUM.CARD_TYPE.{0}", Enum.GetName(typeof(CARD_TYPE), GenericChip?.CardType))) ?? "");
                                hasVariable = true;
                            }
                            
                            if (temporaryContent.Contains("%SLAVECHIPTYPE"))
                            {
                                if (GenericChip != null && GenericChip.HasChilds == true)
                                {
                                    temporaryContent = temporaryContent.Replace("%SLAVECHIPTYPE", ResourceLoader.GetResource(
                                        string.Format("ENUM.CARD_TYPE.{0}", Enum.GetName(typeof(CARD_TYPE), GenericChip?.Childs[0]?.CardType))) ?? "");
                                    hasVariable = true;
                                }
                            }

                            if (temporaryContent.Contains("%SLAVEUID"))
                            {
                                if (GenericChip != null && GenericChip.HasChilds == true)
                                {
                                    temporaryContent = temporaryContent.Replace("%SLAVEUID", GenericChip?.Childs[0]?.UID ?? "");
                                    hasVariable = true;
                                }
                            }

                            if (temporaryContent.Contains("%DATETIME"))
                            {
                                temporaryContent = temporaryContent.Replace("%DATETIME", DateTime.Now.ToString(CultureInfo.CurrentCulture) ?? "");
                                hasVariable = true;
                            }

                            if (temporaryContent.Contains("%DATE"))
                            {
                                temporaryContent = temporaryContent.Replace("%DATE", DateTime.Now.ToString("dd/MM/yyyy") ?? "");
                                hasVariable = true;
                            }

                            if (temporaryContent.Contains("%FREEMEM"))
                            {
                                temporaryContent = temporaryContent.Replace("%FREEMEM", DesfireChip?.FreeMemory.ToString(CultureInfo.CurrentCulture) ?? "");
                                hasVariable = true;
                            }

                            if (temporaryContent.Contains("%FREECLASSICMEM"))
                            {
                                temporaryContent = temporaryContent.Replace("%FREECLASSICMEM", ClassicChip?.FreeMemory.ToString(CultureInfo.CurrentCulture) ?? "");
                                hasVariable = true;
                            }

                            if (temporaryContent.Contains("%LISTAPPS"))
                            {
                                temporaryContent = temporaryContent.Replace("%LISTAPPS", string.Join(", ", DesfireChip?.AppList.Select(x => x.appID)) ?? "");
                                hasVariable = true;
                            }

                            if (temporaryContent.Contains("%COUNTAPPS"))
                            {
                                temporaryContent = temporaryContent.Replace("%COUNTAPPS", DesfireChip?.AppList?.Count.ToString(CultureInfo.CurrentCulture));
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

                            if (temporaryContent.Contains("%nnn"))
                            {
                                temporaryContent = temporaryContent.Replace("%nnn", IterCounter.ToString("D3"));
                                hasVariable = true;
                                IterCounter++;
                            }

                            if (temporaryContent.Contains("%nn"))
                            {
                                temporaryContent = temporaryContent.Replace("%nn", IterCounter.ToString("D2"));
                                hasVariable = true;
                                IterCounter++;
                            }

                            if (temporaryContent.Contains("%n"))
                            {
                                temporaryContent = temporaryContent.Replace("%n", IterCounter.ToString("D1"));
                                hasVariable = true;
                                IterCounter++;
                            }

                            // the "dollar" indicates an external variable that should be replaced
                            if (temporaryContent.Contains('$'))
                            {
                                foreach (var kvArg in Args)
                                {
                                    temporaryContent = temporaryContent.Replace(kvArg.Key, kvArg.Value);
                                }

                                concatenate = true;
                            }

                            // Does the targeted Task Equals the selected TaskResult ?
                            // targeted Taskindex ist not "null" so check if targeted ERRORLEVEL fits current ERRORLEVEL
                            // .ConfigureAwait(false) due to fileAccess
                            if (checkpointDictionary.TryGetValue(checkpoint.TaskIndex ?? "-1", out var targetIndex))
                            {
                                if ((AvailableTasks[targetIndex] as IGenericTaskModel).CurrentTaskErrorLevel == checkpoint.ErrorLevel)
                                {
                                    if (concatenate)
                                    {
                                        await reportReaderWriter.ConcatReportField(checkpoint.TemplateField, temporaryContent);
                                    }
                                    else
                                    {
                                        await reportReaderWriter.SetReportField(checkpoint.TemplateField, hasVariable ? temporaryContent : checkpoint.Content);
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
                                    await reportReaderWriter.ConcatReportField(checkpoint.TemplateField, temporaryContent);
                                }
                                else
                                {
                                    await reportReaderWriter.SetReportField(checkpoint.TemplateField, hasVariable ? temporaryContent : checkpoint.Content).ConfigureAwait(false);
                                }
                            }
                        }
                    }
                }

                catch (Exception e)
                {
                    CurrentTaskErrorLevel = ERROR.TransportError;
                    IsTaskCompletedSuccessfully = false;
                    OnPropertyChanged(nameof(TemplateFields));

                    eventLog.WriteEntry(e.Message, EventLogEntryType.Error);

                    return;
                }

                CurrentTaskErrorLevel = ERROR.NoError;
                IsTaskCompletedSuccessfully = true;

                OnPropertyChanged(nameof(TemplateFields));

            }

            if (CurrentTaskErrorLevel == ERROR.NoError)
            {
                IsTaskCompletedSuccessfully = true;
            }
            else
            {
                IsTaskCompletedSuccessfully = false;
            }

            return;
        }

        /// <summary>
        /// 
        /// </summary>
        public IAsyncRelayCommand CheckLogicCondition => new AsyncRelayCommand<ObservableCollection<object>>(OnNewCheckLogicConditionCommand);
        private async Task<ERROR> OnNewCheckLogicConditionCommand(ObservableCollection<object> _tasks = null)
        {
            CurrentTaskErrorLevel = ERROR.Empty;

            CurrentTaskErrorLevel = await Task.Run(async () =>
            {
                try
                {
                    // here we are about to compare the results of the added "Checkpoints" in the "Check Condition" Task with the actual 
                    // conditions from the live tasks

                    // lets fill a new vector with the results of all so far executed tasks... We will re-use the checkpoint objects for this
                    var results = new ObservableCollection<Checkpoint>();

                    foreach (var task in _tasks)
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

                            foreach (var cp in Checkpoints)
                            {
                                if (cp.ErrorLevel == results.Where<Checkpoint>(x => x.TaskIndex == cp.TaskIndex).Single().ErrorLevel)
                                {
                                    continue;
                                }
                                else
                                {
                                    return ERROR.IsNotTrue;
                                }

                            }

                            CurrentTaskErrorLevel = ERROR.NoError;
                            break;

                        case LOGIC_STATE.NAND:

                            foreach (var cp in Checkpoints)
                            {
                                for (var i = 0; i < Checkpoints.Count; i++)
                                {
                                    if (cp.ErrorLevel == Checkpoints[i].ErrorLevel)
                                    {
                                        continue;
                                    }
                                    else
                                    {
                                        return ERROR.NoError;
                                    }
                                }
                            }

                            CurrentTaskErrorLevel = ERROR.IsNotTrue;
                            break;

                        case LOGIC_STATE.NOR:

                            foreach (var cp in Checkpoints)
                            {
                                for (var i = 0; i < Checkpoints.Count; i++)
                                {
                                    if (cp.ErrorLevel == Checkpoints[i].ErrorLevel)
                                    {
                                        return ERROR.IsNotTrue;
                                    }
                                }
                            }

                            CurrentTaskErrorLevel = ERROR.NoError;
                            break;

                        case LOGIC_STATE.NOT:

                            break;

                        case LOGIC_STATE.OR:

                            foreach (var outerCP in Checkpoints)
                            {
                                foreach (var resultCP in results)
                                {
                                    if (resultCP.TaskIndex == outerCP.TaskIndex && resultCP.ErrorLevel == outerCP.ErrorLevel)
                                    {
                                        return ERROR.NoError;
                                    }
                                    else
                                    {
                                        continue;
                                    }
                                }
                            }

                            CurrentTaskErrorLevel = ERROR.IsNotTrue;
                            break;

                        case LOGIC_STATE.COUNT:

                            var loops = 0;

                            foreach (var outerCP in Checkpoints)
                            {
                                foreach (var resultCP in results)
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
                                        break;
                                    }
                                    CurrentTaskErrorLevel = ERROR.IsNotTrue;
                                    break;

                                case EQUALITY_OPERATOR.LESS_OR_EQUAL:
                                    if (loops <= SelectedCheckpointCounterAsInt)
                                    {
                                        CurrentTaskErrorLevel = ERROR.NoError;
                                        break;
                                    }
                                    CurrentTaskErrorLevel = ERROR.IsNotTrue;
                                    break;

                                case EQUALITY_OPERATOR.LESS_THAN:
                                    if (loops < SelectedCheckpointCounterAsInt)
                                    {
                                        CurrentTaskErrorLevel = ERROR.NoError;
                                        break;
                                    }
                                    CurrentTaskErrorLevel = ERROR.IsNotTrue;
                                    break;

                                case EQUALITY_OPERATOR.MORE_OR_EQUAL:
                                    if (loops >= SelectedCheckpointCounterAsInt)
                                    {
                                        CurrentTaskErrorLevel = ERROR.NoError;
                                        break;
                                    }
                                    CurrentTaskErrorLevel = ERROR.IsNotTrue;
                                    break;

                                case EQUALITY_OPERATOR.MORE_THAN:
                                    if (loops > SelectedCheckpointCounterAsInt)
                                    {
                                        CurrentTaskErrorLevel = ERROR.NoError;
                                        break;
                                    }
                                    CurrentTaskErrorLevel = ERROR.IsNotTrue;
                                    break;

                                default:
                                    CurrentTaskErrorLevel = ERROR.IsNotTrue;
                                    break;
                            }

                            break;


                        case LOGIC_STATE.COMPARE: //Compare 'TaskResult Content'

                            try
                            {
                                if (CompareValue.Contains(">="))
                                {
                                    var comparetemp = CompareValue.Replace(" ", string.Empty).Replace(">", string.Empty).Split('=');

                                    //assume 2 values to compare
                                    if (comparetemp.Length == 2)
                                    {
                                        switch (comparetemp[0])
                                        {
                                            case "%FREEMEM":
                                                uint.TryParse(new string(comparetemp[1].Where(c => Char.IsDigit(c)).ToArray()), out var compareValueAsUInt);

                                                if (DesfireChip?.FreeMemory >= compareValueAsUInt)
                                                {
                                                    CurrentTaskErrorLevel = ERROR.NoError;
                                                    break;
                                                }

                                                else
                                                {
                                                    CurrentTaskErrorLevel = ERROR.IsNotTrue;
                                                }

                                                break;

                                            case "%COUNTAPPS":
                                                uint.TryParse(new string(comparetemp[1].Where(c => Char.IsDigit(c)).ToArray()), out compareValueAsUInt);

                                                if (DesfireChip?.AppIDs.Length >= compareValueAsUInt)
                                                {
                                                    CurrentTaskErrorLevel = ERROR.NoError;
                                                    break;
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
                                    var comparetemp = CompareValue.Replace(" ", string.Empty).Replace("<", string.Empty).Split('=');

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
                                                    break;
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

                                else if (CompareValue.Contains("!="))
                                {
                                    var comparetemp = CompareValue.Split(new string[] { "!=" }, 2, StringSplitOptions.None);

                                    //assume 2 values to compare
                                    if (comparetemp.Length == 2)
                                    {
                                        // the "dollar" indicates an external variable that should be replaced
                                        if (comparetemp[0].Contains('$'))
                                        {
                                            foreach (var kvArg in Args)
                                            {
                                                if (Args.ContainsKey(comparetemp[0]) && Args[comparetemp[0]] != comparetemp[1])
                                                {
                                                    return ERROR.NoError;
                                                }
                                            }
                                            CurrentTaskErrorLevel = ERROR.IsNotTrue;
                                        }

                                        switch (comparetemp[0])
                                        {
                                            case "%FREEMEM":
                                                uint compareValueAsUInt;
                                                uint.TryParse(new string(comparetemp[1].Where(c => Char.IsDigit(c)).ToArray()), out compareValueAsUInt);

                                                if (compareValueAsUInt == DesfireChip?.FreeMemory)
                                                {
                                                    CurrentTaskErrorLevel = ERROR.NoError;
                                                    break;
                                                }

                                                else
                                                {
                                                    CurrentTaskErrorLevel = ERROR.IsNotTrue;
                                                }

                                                break;

                                            case "%COUNTAPPS":
                                                uint.TryParse(new string(comparetemp[1].Where(c => Char.IsDigit(c)).ToArray()), out compareValueAsUInt);

                                                if (DesfireChip?.AppIDs.Length >= compareValueAsUInt)
                                                {
                                                    CurrentTaskErrorLevel = ERROR.NoError;
                                                    break;
                                                }

                                                break;

                                            default:
                                                break;
                                        }


                                    }
                                }

                                else if (CompareValue.Contains("=="))
                                {
                                    var comparetemp = CompareValue.Split(new string[] { "==" }, 2, StringSplitOptions.None);

                                    //assume 2 values to compare
                                    if (comparetemp.Length == 2)
                                    {
                                        // the "dollar" indicates an external variable that should be replaced
                                        if (comparetemp[0].Contains('$'))
                                        {
                                            foreach (var kvArg in Args)
                                            {
                                                if (Args.ContainsKey(comparetemp[0]) && Args[comparetemp[0]] == comparetemp[1])
                                                {
                                                    return ERROR.NoError;
                                                }
                                            }
                                            CurrentTaskErrorLevel = ERROR.IsNotTrue;
                                        }

                                        switch (comparetemp[0])
                                        {
                                            case "%FREEMEM":
                                                uint compareValueAsUInt;
                                                uint.TryParse(new string(comparetemp[1].Where(c => Char.IsDigit(c)).ToArray()), out compareValueAsUInt);

                                                if (compareValueAsUInt == DesfireChip?.FreeMemory)
                                                {
                                                    CurrentTaskErrorLevel = ERROR.NoError;
                                                    break;
                                                }

                                                else
                                                {
                                                    CurrentTaskErrorLevel = ERROR.IsNotTrue;
                                                }

                                                break;

                                            case "%COUNTAPPS":
                                                uint.TryParse(new string(comparetemp[1].Where(c => Char.IsDigit(c)).ToArray()), out compareValueAsUInt);

                                                if (DesfireChip?.AppIDs.Length >= compareValueAsUInt)
                                                {
                                                    CurrentTaskErrorLevel = ERROR.NoError;
                                                    break;
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
                                eventLog.WriteEntry(e.Message, EventLogEntryType.Error);
                            }

                            break;

                        default:
                            break;
                    }
                }
                catch (Exception e)
                {
                    eventLog.WriteEntry(e.Message, EventLogEntryType.Error);
                }

                return CurrentTaskErrorLevel;
            });

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
        public IAsyncRelayCommand ExecuteProgramCommand => new AsyncRelayCommand(OnNewExecuteProgramCommand);
        private Task OnNewExecuteProgramCommand()
        {
            try
            {
                var p = new Process();
                ProcessStartInfo info;

                //InitOnFirstRun Program from RFiDGear Argument 
                if (ProgramToExecute.Contains('$'))
                {
                    var argArr = ProgramToExecute.Split('\"');

                    info = new ProcessStartInfo()
                    {
                        FileName = argArr[1],
                        UseShellExecute =
                        ProgramToExecute.Contains("bat") ||
                        ProgramToExecute.Contains("exe") ||
                        ProgramToExecute.Contains("msi") ? false : true
                    };

                    argArr = argArr.Where(arg => !string.IsNullOrWhiteSpace(arg))
                                 .Skip(1) // Skip the first argument (position of the running executable)
                                 .ToArray();

                    var joinedArgs = string.Join(" ", argArr);

                    foreach(KeyValuePair<string,string> argToReplace in Args.Where(arg => arg.Key.Contains('$')))
                    {
                        joinedArgs = joinedArgs.Replace(argToReplace.Key, argToReplace.Value);
                    }

                    info.Arguments = joinedArgs;
                }

                else if (ProgramToExecute.ToLower() == @"%exit")
                {
                    info = null;

                    Environment.Exit(0);
                }

                else
                {
                    var fileName = ProgramToExecute.Split('\"')[1];
                    var args = ProgramToExecute.Split('\"')[2];

                    if (ProgramToExecute.Contains("%UID"))
                    {
                        ProgramToExecute = ProgramToExecute.Replace("%UID", GenericChip?.UID ?? "");
                    }

                    if (ProgramToExecute.Contains("%CHIPTYPE"))
                    {
                        ProgramToExecute = ProgramToExecute.Replace("%CHIPTYPE", ResourceLoader.GetResource(
                        string.Format("ENUM.CARD_TYPE.{0}", Enum.GetName(typeof(CARD_TYPE), GenericChip.CardType))) ?? "");
                    }

                    if (ProgramToExecute.Contains("%DATETIME"))
                    {
                        ProgramToExecute = ProgramToExecute.Replace("%DATETIME", DateTime.Now.ToString(CultureInfo.CurrentCulture) ?? "");
                    }

                    if (ProgramToExecute.Contains("%DATE"))
                    {
                        ProgramToExecute = ProgramToExecute.Replace("%DATE", DateTime.Now.ToString("dd/MM/yyyy") ?? "");
                    }

                    if (ProgramToExecute.Contains("%FREEMEM"))
                    {
                        ProgramToExecute = ProgramToExecute.Replace("%FREEMEM", DesfireChip?.FreeMemory.ToString(CultureInfo.CurrentCulture) ?? "");
                    }

                    if (ProgramToExecute.Contains("%LISTAPPS"))
                    {
                        ProgramToExecute = ProgramToExecute.Replace("%LISTAPPS", string.Join(", ", DesfireChip?.AppList.Select(x => x.appID)) ?? "");
                    }

                    if (ProgramToExecute.Contains("%COUNTAPPS"))
                    {
                        ProgramToExecute = ProgramToExecute.Replace("%COUNTAPPS", DesfireChip?.AppList?.Count.ToString(CultureInfo.CurrentCulture));
                    }

                    info = new ProcessStartInfo()
                    {
                        FileName = string.IsNullOrWhiteSpace(fileName) ? ProgramToExecute : fileName,
                        Arguments = string.IsNullOrWhiteSpace(args) ? "" : args,
                        UseShellExecute = (fileName != null) ? (
                        fileName.Contains("bat") ||
                        fileName.Contains("exe") ||
                        fileName.Contains("msi") ? false : true) : false,
                    };
                }

                p.StartInfo = info;

                if(p.Start())
                {
                    CurrentTaskErrorLevel = ERROR.NoError;
                    IsTaskCompletedSuccessfully = true;
                }
            }

            catch(Exception e)
            {
                CurrentTaskErrorLevel = ERROR.IsNotTrue;
                IsTaskCompletedSuccessfully = false;
                eventLog.WriteEntry(e.Message, EventLogEntryType.Error);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// 
        /// </summary>
        public IAsyncRelayCommand AddEditCheckpointCommand => new AsyncRelayCommand(OnNewAddEditCheckpointCommand);
        private async Task OnNewAddEditCheckpointCommand()
        {
            await OnNewAddEditCheckpointCommand(SelectedCheckpoint);
        }

        private Task OnNewAddEditCheckpointCommand(Checkpoint cpTarget)
        {
            try
            {
                checkpoint = new Checkpoint
                {
                    ErrorLevel = ERROR.Empty,
                    CheckpointIndex = "",
                    TemplateField = "",
                    UUID = new Random().Next(),
                    Content = "",
                    CompareValue = "",
                    TaskIndex = "",
                };

                if (SelectedTaskType == TaskType_CommonTask.CreateReport)
                {
                    if (cpTarget != null)
                    {
                        var currIndex = Checkpoints.IndexOf(SelectedCheckpoint);

                        checkpoint = new Checkpoint
                        {
                            ErrorLevel = cpTarget.ErrorLevel,
                            CheckpointIndex = cpTarget.CheckpointIndex,
                            TemplateField = cpTarget.TemplateField,
                            UUID = new Random().Next(),
                            Content = cpTarget.Content,
                            CompareValue = cpTarget.CompareValue,
                            TaskIndex = cpTarget.TaskIndex,
                        };

                        checkpoint.CheckpointIndex = string.Format("{0:D3}", currIndex + 1);

                        for (var i = currIndex; i < Checkpoints.Count; i++)
                        {
                            Checkpoints[i].CheckpointIndex = string.Format("{0:D3}", Checkpoints[i].CheckpointIndexAsInt + 1);
                        }
                    }

                    Checkpoints.Add(checkpoint);
                    Checkpoints = new ObservableCollection<Checkpoint>(Checkpoints.OrderBy(x => x.CheckpointIndexAsInt));
                    SelectedCheckpoint = checkpoint;
                }

                else if (SelectedTaskType == TaskType_CommonTask.CheckLogicCondition)
                {
                    checkpoint.TaskIndex = "";
                    checkpoint.ErrorLevel = ERROR.Empty;
                    checkpoint.Content = "";
                    checkpoint.CompareValue = CompareValue;

                    Checkpoints.Add(checkpoint);
                }

                OnPropertyChanged(nameof(Checkpoints));
            }

            catch (Exception e)
            {
                eventLog.WriteEntry(e.Message, EventLogEntryType.Error);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// 
        /// </summary>
        public ICommand RemoveCheckpointCommand => new RelayCommand(OnNewRemoveCheckpointCommand);
        private void OnNewRemoveCheckpointCommand()
        {
            if (SelectedCheckpoint != null)
            {
                var currIndex = Checkpoints.IndexOf(SelectedCheckpoint);

                Checkpoints.Remove(SelectedCheckpoint);

                for (int i = currIndex; i < Checkpoints.Count - 1; i++)
                {
                    Checkpoints[i].CheckpointIndex = string.Format("{0:D3}", i + 1);
                }
            }
            Checkpoints = new ObservableCollection<Checkpoint>(Checkpoints.OrderBy(x => x.CheckpointIndexAsInt));
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
                OnPropertyChanged(nameof(Caption));
            }
        }
        private string _Caption;

        #endregion Localization
    }
}
