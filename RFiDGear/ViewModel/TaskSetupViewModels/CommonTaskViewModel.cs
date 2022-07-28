﻿/*
 * Created by SharpDevelop.
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
    /// Description of ReportTaskViewModel.
    /// </summary>
    public class CommonTaskViewModel : ViewModelBase, IUserDialogViewModel
    {
        #region Fields

        private protected ReportReaderWriter reportReaderWriter;
        private protected Checkpoint checkpoint;

        [XmlIgnore]
        public ObservableCollection<object> AvailableTasks { get; set; }

        [XmlIgnore]
        public GenericChipModel GenericChip { get; set; }

        [XmlIgnore]
        public MifareDesfireChipModel DesfireChip { get; set; }

        #endregion

        #region Constructors

        /// <summary>
        ///
        /// </summary>
        public CommonTaskViewModel()
        {
            TaskErr = ERROR.Empty;

            checkpoint = new Checkpoint();
            Checkpoints = new ObservableCollection<Checkpoint>();

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
                TaskErr = ERROR.Empty;

                checkpoint = new Checkpoint();
                Checkpoints = new ObservableCollection<Checkpoint>();

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
                            reader.OpenReport();
                            TemplateFields = reader.GetReportFields();

                            RaisePropertyChanged("SelectedTemplateField");
                        }
                    }
                }

                else
                {
                    SelectedTaskIndex = "0";
                    SelectedTaskDescription = "Enter a Description";
                    SelectedExecuteConditionErrorLevel = ERROR.Empty;
                    SelectedExecuteConditionTaskIndex = "0";
                    SelectedCounterTrigger = EQUALITY_OPERATOR.EQUAL;
                    SelectedCheckpointCounter = "0";

                    try
                    {
                        //string templatePath = _tasks.OfType<CommonTaskViewModel>()
                        //    .Where(x => x.ReportTemplatePath != null)
                        //    .Where(y => y.IsFocused)
                        //    .Select(x => x.ReportTemplatePath).Single();

                        reportReaderWriter = new ReportReaderWriter();
                        //reportReaderWriter.ReportTemplatePath = templatePath;
                        //reportReaderWriter.OpenReport();

                        //TemplateFields = reportReaderWriter.GetReportFields();
                    }

                    catch (Exception e)
                    {
                        LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""), FacilityName);
                    }
                }

                AvailableTasks = _tasks;
                NumberOfCheckpoints = CustomConverter.GenerateStringSequence(0, 60).ToArray();

            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""), FacilityName);
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
        public string SelectedTaskIndex
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
        public ERROR TaskErr { get; set; }

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
                            availableTaskIndices.Add(ssVM.SelectedTaskIndex);
                            break;
                        case GenericChipTaskViewModel ssVM:
                            availableTaskIndices.Add(ssVM.SelectedTaskIndex);
                            break;
                        case MifareClassicSetupViewModel ssVM:
                            availableTaskIndices.Add(ssVM.SelectedTaskIndex);
                            break;
                        case MifareDesfireSetupViewModel ssVM:
                            availableTaskIndices.Add(ssVM.SelectedTaskIndex);
                            break;
                        case MifareUltralightSetupViewModel ssVM:
                            availableTaskIndices.Add(ssVM.SelectedTaskIndex);
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
                        IsLogicFuncTaskLogicFuncEnabled = false;
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
        public ICommand OpenReportTemplateCommand => new RelayCommand(OnNewOpenReportTemplateCommand);
        private void OnNewOpenReportTemplateCommand()
        {

            TaskErr = ERROR.Empty;

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
                        reportReaderWriter.OpenReport();

                        TemplateFields = reportReaderWriter.GetReportFields();
                    }

                    Mouse.OverrideCursor = null;

                    RaisePropertyChanged("TemplateFields");
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
            TaskErr = ERROR.Empty;

            if (_reportReaderWriter != null)
            {

                Dictionary<string, int> taskIndices = new Dictionary<string, int>();

                // create a new key,value pair of taskpositions <-> taskindex 
                // (they could be different as because item at array position 0 can have index "100")
                foreach (object o in AvailableTasks)
                {
                    switch (o)
                    {
                        case GenericChipTaskViewModel ssVM:
                            if (ssVM.IsValidSelectedTaskIndex != false)
                            {
                                taskIndices.Add(ssVM.SelectedTaskIndex, AvailableTasks.IndexOf(ssVM));
                            }

                            break;
                        case CommonTaskViewModel ssVM:
                            if (ssVM.IsValidSelectedTaskIndex != false)
                            {
                                taskIndices.Add(ssVM.SelectedTaskIndex, AvailableTasks.IndexOf(ssVM));
                            }

                            break;
                        case MifareClassicSetupViewModel ssVM:
                            if (ssVM.IsValidSelectedTaskIndex != false)
                            {
                                taskIndices.Add(ssVM.SelectedTaskIndex, AvailableTasks.IndexOf(ssVM));
                            }

                            break;
                        case MifareDesfireSetupViewModel ssVM:
                            if (ssVM.IsValidSelectedTaskIndex != false)
                            {
                                taskIndices.Add(ssVM.SelectedTaskIndex, AvailableTasks.IndexOf(ssVM));
                            }

                            break;
                        case MifareUltralightSetupViewModel ssVM:
                            if (ssVM.IsValidSelectedTaskIndex != false)
                            {
                                taskIndices.Add(ssVM.SelectedTaskIndex, AvailableTasks.IndexOf(ssVM));
                            }

                            break;
                        default:
                            taskIndices.Add(null, 0);
                            break;
                    }
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
                        reportReaderWriter.OpenReport();

                        foreach (Checkpoint checkpoint in Checkpoints)
                        {

                            bool hasVariable = false;
                            bool concatenate = false;

                            string temporaryContent = checkpoint.Content;

                            if (temporaryContent.Contains("%UID"))
                            {
                                temporaryContent = temporaryContent.Replace("%UID", GenericChip.UID ?? "");
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
                                temporaryContent = temporaryContent.Replace("%FREEMEM", GenericChip.FreeMemory.ToString() ?? "");
                                hasVariable = true;
                            }

                            if (temporaryContent.Contains("%LISTAPPS"))
                            {
                                temporaryContent = temporaryContent.Replace("%LISTAPPS", string.Join(", ", DesfireChip?.AppList.Select(x => x.appID)) ?? "");
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

                            // Does the targeted Task Equals the selected TaskResult ?
                            try
                            {
                                if (taskIndices.TryGetValue(checkpoint.TaskIndex, out int targetIndex))
                                {
                                    switch (AvailableTasks[targetIndex])
                                    {
                                        case GenericChipTaskViewModel tsVM:
                                            if (tsVM.TaskErr == checkpoint.ErrorLevel)
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

                                            break;
                                        case CommonTaskViewModel tsVM:
                                            if (tsVM.TaskErr == checkpoint.ErrorLevel)
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

                                            break;
                                        case MifareClassicSetupViewModel tsVM:
                                            if (tsVM.TaskErr == checkpoint.ErrorLevel)
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

                                            break;
                                        case MifareDesfireSetupViewModel tsVM:
                                            if (tsVM.TaskErr == checkpoint.ErrorLevel)
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

                                            break;
                                        case MifareUltralightSetupViewModel tsVM:
                                            if (tsVM.TaskErr == checkpoint.ErrorLevel)
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

                                            break;
                                    }
                                }
                                // The targeted Task is not of any vaild type. E.g. a "string"
                                if (targetIndex == 0)
                                {
                                    throw new Exception();
                                }
                            }

                            // The targeted Task does not Exist: Continue Execution anyway...
                            catch
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

                        reportReaderWriter.CloseReport();
                    }
                }

                catch (Exception e)
                {
                    TaskErr = ERROR.IOError;
                    IsTaskCompletedSuccessfully = false;
                    RaisePropertyChanged("TemplateFields");

                    LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""), FacilityName);

                    return;
                }

                TaskErr = ERROR.NoError;
                IsTaskCompletedSuccessfully = true;

                RaisePropertyChanged("TemplateFields");

                //_reportReaderWriter.CloseReport();
            }

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

                //SelectedCheckpoint = checkpoint;

            }

            catch (Exception e)
            {
                LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""), FacilityName);
            }

            return;
        }

        /// <summary>
        /// 
        /// </summary>
        public ICommand CheckLogicCondition => new RelayCommand<ObservableCollection<object>>(OnNewCheckLogicConditionCommand);
        private void OnNewCheckLogicConditionCommand(ObservableCollection<object> _tasks = null)
        {
            Task commonTask =
                new Task(() =>
                {
                    try
                    {
                        ERROR result = ERROR.Empty;
                        TaskErr = result;

                        // here we are about to compare the results of the added "Checkpoints" in the "Check Condition" Task with the actual 
                        // conditions from the live tasks

                        //lets fill a new vector with the results of all so far executed tasks... We will re-use the checkpoint objects for this
                        ObservableCollection<Checkpoint> results = new ObservableCollection<Checkpoint>();

                        foreach (object task in _tasks)
                        {
                            switch (task)
                            {
                                case CommonTaskViewModel ssVM:
                                    results.Add(new Checkpoint() { ErrorLevel = ssVM.TaskErr, TaskIndex = ssVM.SelectedTaskIndex, Content = ssVM.Content, CompareValue = ssVM.CompareValue });
                                    break;
                                case GenericChipTaskViewModel ssVM:
                                    results.Add(new Checkpoint() { ErrorLevel = ssVM.TaskErr, TaskIndex = ssVM.SelectedTaskIndex });
                                    break;
                                case MifareClassicSetupViewModel ssVM:
                                    results.Add(new Checkpoint() { ErrorLevel = ssVM.TaskErr, TaskIndex = ssVM.SelectedTaskIndex });
                                    break;
                                case MifareDesfireSetupViewModel ssVM:
                                    results.Add(new Checkpoint() { ErrorLevel = ssVM.TaskErr, TaskIndex = ssVM.SelectedTaskIndex });
                                    break;
                                case MifareUltralightSetupViewModel ssVM:
                                    results.Add(new Checkpoint() { ErrorLevel = ssVM.TaskErr, TaskIndex = ssVM.SelectedTaskIndex });
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
                                        TaskErr = ERROR.IsNotTrue;
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
                                            TaskErr = ERROR.NoError;
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
                                            TaskErr = ERROR.NoError;
                                            return;
                                        }

                                        break;

                                    case EQUALITY_OPERATOR.LESS_OR_EQUAL:
                                        if (loops <= SelectedCheckpointCounterAsInt)
                                        {
                                            TaskErr = ERROR.NoError;
                                            return;
                                        }

                                        break;

                                    case EQUALITY_OPERATOR.LESS_THAN:
                                        if (loops < SelectedCheckpointCounterAsInt)
                                        {
                                            TaskErr = ERROR.NoError;
                                            return;
                                        }

                                        break;

                                    case EQUALITY_OPERATOR.MORE_OR_EQUAL:
                                        if (loops >= SelectedCheckpointCounterAsInt)
                                        {
                                            TaskErr = ERROR.NoError;
                                            return;
                                        }

                                        break;

                                    case EQUALITY_OPERATOR.MORE_THAN:
                                        if (loops > SelectedCheckpointCounterAsInt)
                                        {
                                            TaskErr = ERROR.NoError;
                                            return;
                                        }

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

                                                    if (GenericChip.FreeMemory >= compareValueAsUInt)
                                                    {
                                                        TaskErr = ERROR.NoError;
                                                        return;
                                                    }

                                                    break;

                                                case "%APPSCOUNT":
                                                    uint.TryParse(new string(comparetemp[1].Where(c => Char.IsDigit(c)).ToArray()), out compareValueAsUInt);

                                                    if (DesfireChip?.AppList.Count >= compareValueAsUInt)
                                                    {
                                                        TaskErr = ERROR.NoError;
                                                        return;
                                                    }

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

                                                    if (GenericChip.FreeMemory <= compareValueAsUInt)
                                                    {
                                                        TaskErr = ERROR.NoError;
                                                        return;
                                                    }


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

                                                    if (compareValueAsUInt == GenericChip.FreeMemory)
                                                    {
                                                        TaskErr = ERROR.NoError;
                                                        return;
                                                    }

                                                    else
                                                    {
                                                        result = ERROR.IsNotTrue;
                                                    }

                                                    break;
                                            }
                                        }
                                    }
                                }

                                catch (Exception e)
                                {
                                    LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""), FacilityName);
                                }

                                result = ERROR.IsNotTrue;

                                break;
                        }

                        TaskErr = result;
                    }


                    catch (Exception e)
                    {
                        LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""), FacilityName);
                    }

                });

            if (TaskErr == ERROR.Empty)
            {
                TaskErr = ERROR.DeviceNotReadyError;

                commonTask.ContinueWith((x) =>
                {
                    if (TaskErr == ERROR.NoError)
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