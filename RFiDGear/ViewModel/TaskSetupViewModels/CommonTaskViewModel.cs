/*
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
		#region fields

		private protected SettingsReaderWriter settings = new SettingsReaderWriter();
        private protected ReportReaderWriter reportReaderWriter;
        private protected Checkpoint checkpoint;
        private protected readonly ObservableCollection<object> _availableTasks;

		#endregion fields

		#region constructors
		
		/// <summary>
		///
		/// </summary>
		public CommonTaskViewModel()
		{
			checkpoint = new Checkpoint();
			Checkpoints = new ObservableCollection<Checkpoint>();

		}

		/// <summary>
		///
		/// </summary>
		/// <param name="_selectedSetupViewModel"></param>
		/// <param name="_dialogs"></param>
		public CommonTaskViewModel(object _selectedSetupViewModel, ObservableCollection<object> _tasks, ObservableCollection<IDialogViewModel> _dialogs)
		{
			try
			{

				_availableTasks = _tasks;
				checkpoint = new Checkpoint();

				Checkpoints = new ObservableCollection<Checkpoint>();

				if(_selectedSetupViewModel is CommonTaskViewModel)
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
				}
				
				else
				{
					SelectedTaskIndex = "0";
					SelectedTaskDescription = "Enter a Description";
                    SelectedExecuteConditionErrorLevel = ERROR.Empty;
                    SelectedExecuteConditionTaskIndex = "0";
				}
				
			}
			catch (Exception e)
			{
				LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""));
			}
			
		}

		#endregion

		#region Dialogs
		[XmlIgnore]
		public ObservableCollection<IDialogViewModel> Dialogs { get { return dialogs; } }
		private ObservableCollection<IDialogViewModel> dialogs = new ObservableCollection<IDialogViewModel>();

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
			get
			{
				return isFocused;
			}
			set
			{
				isFocused = value;
			}
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
            get
            {
                return selectedExecuteConditionTaskIndex;
            }

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
            get
            {
                return isValidSelectedExecuteConditionTaskIndex;
            }
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
        public int SelectedExecuteConditionTaskIndexAsInt
        { get { return selectedExecuteConditionTaskIndexAsInt; } }
        private int selectedExecuteConditionTaskIndexAsInt;

        /// <summary>
        /// 
        /// </summary>
        public ERROR SelectedExecuteConditionErrorLevel
        {
            get
            {
                return selectedExecuteConditionErrorLevel;
            }

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
			get { return isTaskCompletedSuccessfully; }
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
        public int SelectedTaskIndexAsInt
		{ get { return selectedTaskIndexAsInt; } }
		private int selectedTaskIndexAsInt;

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public bool? IsValidSelectedTaskIndex
		{
			get
			{
				return isValidSelectedTaskIndex;
			}
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
			get
			{
				return selectedTaskType;
			}
			set
			{
				selectedTaskType = value;
				switch (value)
				{
					case TaskType_CommonTask.None:

						break;

					case TaskType_CommonTask.CreateReport:

						break;

					case TaskType_CommonTask.ChangeDefault:

						break;
				}
				RaisePropertyChanged("SelectedTaskType");
			}
		}
		private TaskType_CommonTask selectedTaskType;

		/// <summary>
		///
		/// </summary>
		public string SelectedTaskDescription
		{
			get
			{
				return selectedTaskDescription;
			}
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
			get
			{
				return selectedTaskIndex;
			}

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
		public ObservableCollection<Checkpoint> Checkpoints
		{
			get
			{
				return checkpoints;
			}
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
			get
			{
				return selectedCheckpoint;
			}
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
                foreach (object ssVMO in _availableTasks)
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
                return availableTaskIndices;
			}
		}

        /// <summary>
        /// The Selected Error Level that Trigger the Field in the PDF to be filled
        /// </summary>
        public ERROR SelectedErrorLevel
		{
			get { return selectedErrorLevel; }
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
			get
			{
				return selectedTaskIndexFromAvailableTasks;
			}

			set
			{
				selectedTaskIndexFromAvailableTasks = value;
				RaisePropertyChanged("SelectedTaskIndexFromAvailableTasks");
			}
		}
		private string selectedTaskIndexFromAvailableTasks;

		#endregion

		#endregion

		#region Logic Task Editor

		/// <summary>
		/// The LOGIC Condition that will be applied between every added (RFID)Task e.g. "MifareDesfireTask" Object
		/// </summary>
		public LOGIC_STATE SelectedLogicCondition
		{
			get
			{
				return selectedLogicCondition;
			}
			set
			{
				selectedLogicCondition = value;
				RaisePropertyChanged("SelectedLogicCondition");
			}
		}
		private LOGIC_STATE selectedLogicCondition;

        #endregion

        #region Report Task Editor

        /// <summary>
        /// 
        /// </summary>
        public string ReportTemplatePath
        {
            get { return reportTemplatePath; }
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
        public ObservableCollection<string> TemplateFields
        {
            get { return templateFields; }
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
            get
            {
                return selectedTemplateField;
            }

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
            get
            {
                return content;
            }

            set
            {
                content = value;
                RaisePropertyChanged("Content");
            }
        }
        private string content;

		/// <summary>
		/// 
		/// </summary>
		[XmlIgnore]
		public SettingsReaderWriter Settings
		{
			get { return settings; }
		}


        #endregion

        #endregion General Properties

        #region Commands

        /// <summary>
        /// 
        /// </summary>
        public ICommand OpenReportTemplateCommand { get { return new RelayCommand(OnNewOpenReportTemplateCommand); } }
		private void OnNewOpenReportTemplateCommand()
        {
			var dlg = new OpenFileDialogViewModel
			{
				Title = ResourceLoader.getResource("windowCaptionSaveTasks"),
				Filter = ResourceLoader.getResource("filterStringSaveReport"),
				Multiselect = false
			};

			
			if (dlg.Show(this.Dialogs) && dlg.FileName != null)
			{
				CARD_INFO card;

				Mouse.OverrideCursor = Cursors.AppStarting;

                ReportTemplatePath = dlg.FileName;

                if(!String.IsNullOrWhiteSpace(ReportTemplatePath))
                {
                    reportReaderWriter = new ReportReaderWriter(ReportTemplatePath);
                }


				try
				{
					/*
					//try to get singleton instance
					using (RFiDDevice device = RFiDDevice.Instance)
					{
						//reader was ready - proceed
						if (device != null)
						{
							device.ReadChipPublic();

							card = device.CardInfo;

							reportReaderWriter.CreateReport(device, dlg.FileName);
						}
						else
							card = new CARD_INFO(CARD_TYPE.Unspecified, "");
					}
					*/

					TemplateFields = reportReaderWriter.GetReportFields();
				}

				catch { }



				//IRandomAccessSource source = new RandomAccessSourceFactory().CreateSource(new byte[1] { 3});
				//PdfDocument pdfDoc = new PdfDocument(new PdfReader(source, new ReaderProperties()), new PdfWriter(dlg.FileName));
				//PdfAcroForm form = PdfAcroForm.GetAcroForm(pdfDoc, true);

				//form.GetField("test1")
				//    .SetValue("A B C D E F\nG H I J K L M N\nO P Q R S T U\r\nV W X Y Z\n\nAlphabet street");

				// If no fields have been explicitly included, then all fields are flattened.
				// Otherwise only the included fields are flattened.
				//form.FlattenFields();

				//pdfDoc.Close();

				Mouse.OverrideCursor = null;

                RaisePropertyChanged("TemplateFields");
            }
			
		}

        /// <summary>
        /// 
        /// </summary>
        public ICommand WriteReportCommand { get { return new RelayCommand<string>(OnNewWriteReportCommand); } }
        private void OnNewWriteReportCommand(string _path)
        {

            if (!String.IsNullOrWhiteSpace(_path))
            {
                CARD_INFO card;

                //Mouse.OverrideCursor = Cursors.AppStarting;

                try
                {
                    if (reportReaderWriter != null)
                    {
                        reportReaderWriter.ReportOutputPath = _path;
                    }
                    else
                        reportReaderWriter = new ReportReaderWriter(ReportTemplatePath);

                    reportReaderWriter.ReportOutputPath = _path;

                    if (!String.IsNullOrWhiteSpace(reportReaderWriter.ReportOutputPath))
                    {
                        foreach (Checkpoint checkpoint in this.Checkpoints)
                        {
                            reportReaderWriter.SetReportField(checkpoint.TemplateField, checkpoint.Content);
                        }
                    }


                    
                }

                catch { }



                //IRandomAccessSource source = new RandomAccessSourceFactory().CreateSource(new byte[1] { 3});
                //PdfDocument pdfDoc = new PdfDocument(new PdfReader(source, new ReaderProperties()), new PdfWriter(dlg.FileName));
                //PdfAcroForm form = PdfAcroForm.GetAcroForm(pdfDoc, true);

                //form.GetField("test1")
                //    .SetValue("A B C D E F\nG H I J K L M N\nO P Q R S T U\r\nV W X Y Z\n\nAlphabet street");

                // If no fields have been explicitly included, then all fields are flattened.
                // Otherwise only the included fields are flattened.
                //form.FlattenFields();

                //pdfDoc.Close();

                //Mouse.OverrideCursor = null;

                RaisePropertyChanged("TemplateFields");
            }

        }

        /// <summary>
        /// 
        /// </summary>
        public ICommand AddEditCheckpointCommand { get { return new RelayCommand(OnNewAddEditCheckpointCommand); } }
		private void OnNewAddEditCheckpointCommand()
        {
            try
            {
                checkpoint = new Checkpoint();

                checkpoint.ErrorLevel = ERROR.Empty;

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

                    Checkpoints.Add(checkpoint);
                }

                RaisePropertyChanged("Checkpoints");

                //SelectedCheckpoint = checkpoint;

            }

            catch (Exception e)
            {
                LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""));
            }

            return;
        }

        /// <summary>
        /// 
        /// </summary>
        public ICommand CheckLogicCondition { get { return new RelayCommand<ObservableCollection<object>>(OnNewCheckLogicConditionCommand); } }
        private void OnNewCheckLogicConditionCommand(ObservableCollection<object> _tasks = null)
        {
            try
            {
                TaskErr = ERROR.Empty;

                // here we are about to compare the results of the added "Checkpoints" in the "Check Condition" Task with the actual 
                // conditions from the live tasks

                //lets fill a new vector with the results of all so far executed tasks... We will re-use the checkpoint objects for this
                ObservableCollection<Checkpoint> results = new ObservableCollection<Checkpoint>();

                foreach (object task in _tasks)
                {
                    switch (task)
                    {
                        case CommonTaskViewModel ssVM:
                            results.Add(new Checkpoint() { ErrorLevel = ssVM.TaskErr, TaskIndex = ssVM.SelectedTaskIndex });
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
                                continue;
                            else
                            {
                                TaskErr = ERROR.IsNotTrue;
                                return;
                            }
                            
                        }

                        TaskErr = ERROR.NoError;
                        break;


                    case LOGIC_STATE.NAND:

                        foreach (Checkpoint cp in Checkpoints)
                        {
                            for (int i = 0; i < Checkpoints.Count(); i++)
                            {
                                if ((cp.ErrorLevel == Checkpoints[i].ErrorLevel))
                                    continue;
                                else
                                {
                                    TaskErr = ERROR.NoError;
                                    return;
                                }
                            }

                        }

                        TaskErr = ERROR.IsNotTrue;

                        break;

                    case LOGIC_STATE.NOR:

                        foreach (Checkpoint cp in Checkpoints)
                        {
                            for (int i = 0; i < Checkpoints.Count(); i++)
                            {
                                if ((cp.ErrorLevel == Checkpoints[i].ErrorLevel))
                                {
                                    TaskErr = ERROR.IsNotTrue;
                                    return;
                                }
                            }
                        }

                        TaskErr = ERROR.NoError;

                        break;

                    case LOGIC_STATE.NOT:

                        break;

                    case LOGIC_STATE.OR:

                        foreach (Checkpoint cp in Checkpoints)
                        {
                            if (cp.ErrorLevel == results.Where<Checkpoint>(x => x.TaskIndex == cp.TaskIndex).Single().ErrorLevel)
                            {
                                TaskErr = ERROR.NoError;
                                return;
                            }
                        }

                        TaskErr = ERROR.IsNotTrue;

                        break;
                }
            }

            catch (Exception e)
            {
                LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""));
            }

            return;
        }

        #endregion Commands

        #region IUserDialogViewModel Implementation

        [XmlIgnore]
		public bool IsModal { get; private set; }
		
		public virtual void RequestClose()
		{
			if (this.OnCloseRequest != null)
				OnCloseRequest(this);
			else
				Close();
		}

		public event EventHandler DialogClosing;

		public ICommand OKCommand { get { return new RelayCommand(Ok); } }

		protected virtual void Ok()
		{
			if (this.OnOk != null)
				this.OnOk(this);
			else
				Close();
		}

		public ICommand CancelCommand { get { return new RelayCommand(Cancel); } }

		protected virtual void Cancel()
		{
			if (this.OnCancel != null)
				this.OnCancel(this);
			else
				Close();
		}

		public ICommand AuthCommand { get { return new RelayCommand(Auth); } }

		protected virtual void Auth()
		{
			if (this.OnAuth != null)
				this.OnAuth(this);
			else
				Close();
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
			if (this.DialogClosing != null)
				this.DialogClosing(this, new EventArgs());
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
			get { return _Caption; }
			set
			{
				_Caption = value;
				RaisePropertyChanged("Caption");
			}
		} private string _Caption;

		#endregion Localization
	}
}