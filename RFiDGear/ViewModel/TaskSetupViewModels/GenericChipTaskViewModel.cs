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
	/// Description of CommonTaskViewModel.
	/// </summary>
	public class GenericChipTaskViewModel : ViewModelBase, IUserDialogViewModel
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
				if(_selectedSetupViewModel is GenericChipTaskViewModel)
				{
					PropertyInfo[] properties = typeof(GenericChipTaskViewModel).GetProperties(BindingFlags.Public | BindingFlags.Instance);

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
        public CARD_TYPE SelectedChipType
		{
            get { return selectedChipType; }
            set
            {
				selectedChipType = value;
                RaisePropertyChanged("SelectedChipType");
            }
        }
        private CARD_TYPE selectedChipType;

        /// <summary>
        /// 
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
        /// 
        /// </summary>
        [XmlIgnore]
		public ERROR TaskErr { get; set; }

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
		public TaskType_GenericChipTask SelectedTaskType
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
					case TaskType_GenericChipTask.None:

						break;

					case TaskType_GenericChipTask.ChipIsOfType:

						break;

					case TaskType_GenericChipTask.ChangeDefault:

						break;
				}
				RaisePropertyChanged("SelectedTaskType");
			}
		}
		private TaskType_GenericChipTask selectedTaskType;

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
		/// 
		/// </summary>
		[XmlIgnore]
		public SettingsReaderWriter Settings
		{
			get { return settings; }
		}

        #endregion General Properties

        #region Commands

		public ICommand CheckChipType { get { return new RelayCommand(OnNewCheckChipTypeCommand); } }
		private void OnNewCheckChipTypeCommand()
        {
			TaskErr = ERROR.Empty;

			Task desfireTask =
				new Task(() =>
				{
					using (RFiDDevice device = RFiDDevice.Instance)
					{
						if (device != null)
						{
							//StatusText = string.Format("{0}: {1}\n", DateTime.Now, ResourceLoader.getResource("textBoxStatusTextBoxDllLoaded"));

							//Mouse.OverrideCursor = Cursors.Wait;

							if (device.ReadChipPublic() == ERROR.NoError)
							{
								//StatusText += string.Format("{0}: Successfully Authenticated to App 0\n", DateTime.Now);

								if (device.CardInfo.CardType == SelectedChipType)
									TaskErr = ERROR.NoError;
								else
									TaskErr = ERROR.IsNotTrue;

								return;

								//StatusText += string.Format("{0}: Successfully Created AppID {1}\n", DateTime.Now, AppNumberNewAsInt);
							}

							//RaisePropertyChanged("DataAsByteArray");

							//Mouse.OverrideCursor = null;
						}
						else
						{
							TaskErr = ERROR.DeviceNotReadyError;
							return;
						}
					}
				});

			if (TaskErr == ERROR.Empty)
			{
				TaskErr = ERROR.DeviceNotReadyError;

				desfireTask.ContinueWith((x) =>
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

				desfireTask.Start();
			}

			return;
		}

		#endregion

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
		public Action<GenericChipTaskViewModel> OnOk { get; set; }

		[XmlIgnore]
		public Action<GenericChipTaskViewModel> OnCancel { get; set; }

		[XmlIgnore]
		public Action<GenericChipTaskViewModel> OnAuth { get; set; }

		[XmlIgnore]
		public Action<GenericChipTaskViewModel> OnCloseRequest { get; set; }

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