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
		#region Fields

        private protected ReportReaderWriter reportReaderWriter;
        private protected Checkpoint checkpoint;

		[XmlIgnore]
        public ObservableCollection<object> AvailableTasks { get; set; }

		[XmlIgnore]
        public GenericChipModel GenericChip { get; set; }
		//public CARD_INFO CardInfo { get; set; }

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

                    }
				}

                AvailableTasks = _tasks;

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
        [XmlIgnore]
		public ObservableCollection<string> TemplateFields
        {
			get
			{
				return templateFields;
			}

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


        #endregion

        #endregion General Properties

        #region Commands

        /// <summary>
        /// 
        /// </summary>
        public ICommand OpenReportTemplateCommand { get { return new RelayCommand(OnNewOpenReportTemplateCommand); } }
		private void OnNewOpenReportTemplateCommand()
        {

			TaskErr = ERROR.Empty;

			try
            {
				var dlg = new OpenFileDialogViewModel
				{
					Title = ResourceLoader.getResource("windowCaptionSaveTasks"),
					Filter = ResourceLoader.getResource("filterStringSaveReport"),
					Multiselect = false
				};


				if (dlg.Show(this.Dialogs) && dlg.FileName != null)
				{
					Mouse.OverrideCursor = Cursors.AppStarting;

					string path = dlg.FileName;

					if (!String.IsNullOrWhiteSpace(path))
					{
						ReportTemplatePath = path;
						reportReaderWriter = new ReportReaderWriter();
						reportReaderWriter.ReportTemplatePath = ReportTemplatePath;
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
        public ICommand WriteReportCommand { get { return new RelayCommand<ReportReaderWriter>(OnNewWriteReportCommand); } }
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
								taskIndices.Add(ssVM.SelectedTaskIndex, AvailableTasks.IndexOf(ssVM));
							break;
						case CommonTaskViewModel ssVM:
							if (ssVM.IsValidSelectedTaskIndex != false)
								taskIndices.Add(ssVM.SelectedTaskIndex, AvailableTasks.IndexOf(ssVM));
							break;
						case MifareClassicSetupViewModel ssVM:
							if (ssVM.IsValidSelectedTaskIndex != false)
								taskIndices.Add(ssVM.SelectedTaskIndex, AvailableTasks.IndexOf(ssVM));
							break;
						case MifareDesfireSetupViewModel ssVM:
							if (ssVM.IsValidSelectedTaskIndex != false)
								taskIndices.Add(ssVM.SelectedTaskIndex, AvailableTasks.IndexOf(ssVM));
							break;
						case MifareUltralightSetupViewModel ssVM:
							if (ssVM.IsValidSelectedTaskIndex != false)
								taskIndices.Add(ssVM.SelectedTaskIndex, AvailableTasks.IndexOf(ssVM));
							break;
					}
				}

				try
                {
                    reportReaderWriter = _reportReaderWriter;
					if (string.IsNullOrEmpty(reportReaderWriter.ReportTemplatePath))
						reportReaderWriter.ReportTemplatePath = this.ReportTemplatePath;

					if (!String.IsNullOrWhiteSpace(reportReaderWriter.ReportTemplatePath))
                    {
						reportReaderWriter.OpenReport();

						foreach (Checkpoint checkpoint in this.Checkpoints)
                        {
							int targetIndex;

							bool hasVariable = false;
							string temporaryContent = "";

							if(checkpoint.Content.Contains("%UID"))
                            {
								temporaryContent = checkpoint.Content.Replace("%UID", GenericChip.UID);
								hasVariable = true;
                            }

							if (checkpoint.Content.Contains("%DATETIME"))
							{
								temporaryContent = checkpoint.Content.Replace("%DATETIME", DateTime.Now.ToString());
								hasVariable = true;
							}

                            if (checkpoint.Content.Contains("%FREEMEM"))
                            {
								temporaryContent = checkpoint.Content.Replace("%FREEMEM", GenericChip.FreeMemory.ToString());
								hasVariable = true;
							}

                            if (taskIndices.TryGetValue(checkpoint.TaskIndex, out targetIndex))
							{
								switch (AvailableTasks[targetIndex])
								{
									case GenericChipTaskViewModel tsVM:
										if (tsVM.TaskErr == checkpoint.ErrorLevel)
											reportReaderWriter.SetReportField(checkpoint.TemplateField, hasVariable ? temporaryContent : checkpoint.Content);
										break;
									case CommonTaskViewModel tsVM:
										if (tsVM.TaskErr == checkpoint.ErrorLevel)
											reportReaderWriter.SetReportField(checkpoint.TemplateField, hasVariable ? temporaryContent : checkpoint.Content);
										break;
									case MifareClassicSetupViewModel tsVM:
										if (tsVM.TaskErr == checkpoint.ErrorLevel)
											reportReaderWriter.SetReportField(checkpoint.TemplateField, hasVariable ? temporaryContent : checkpoint.Content);
										break;
									case MifareDesfireSetupViewModel tsVM:
										if (tsVM.TaskErr == checkpoint.ErrorLevel)
											reportReaderWriter.SetReportField(checkpoint.TemplateField, hasVariable ? temporaryContent : checkpoint.Content);
										break;
									case MifareUltralightSetupViewModel tsVM:
										if (tsVM.TaskErr == checkpoint.ErrorLevel)
											reportReaderWriter.SetReportField(checkpoint.TemplateField, hasVariable ? temporaryContent : checkpoint.Content);
										break;
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

                    return;
                }

				TaskErr = ERROR.NoError;
				IsTaskCompletedSuccessfully = true;

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
					checkpoint.ErrorLevel = SelectedErrorLevel;

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
										result = ERROR.IsNotTrue;
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
										if ((cp.ErrorLevel == Checkpoints[i].ErrorLevel))
											continue;
										else
										{
											TaskErr = ERROR.NoError;
											return;
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
										if ((cp.ErrorLevel == Checkpoints[i].ErrorLevel))
										{
											TaskErr = ERROR.IsNotTrue;
											return;
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
											continue;
									}
								}

								result = ERROR.IsNotTrue;
								break;
						}

						TaskErr = result;
					}

					
					catch (Exception e)
					{
						LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""));
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
		public ICommand RemoveCheckpointCommand { get { return new RelayCommand(OnNewRemoveCheckpointCommand); } }
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