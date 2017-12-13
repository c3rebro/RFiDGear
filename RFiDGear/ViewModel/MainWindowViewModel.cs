using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using MvvmDialogs.ViewModels;
using RedCell.Diagnostics.Update;
using RFiDGear.DataAccessLayer;
using RFiDGear.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace RFiDGear.ViewModel
{
	/// <summary>
	/// Description of MainWindowViewModel.
	/// </summary>
	public class MainWindowViewModel : ViewModelBase
	{
		private ResourceLoader resLoader;
		private MainWindow mw;
		private Updater updater;
		private DatabaseReaderWriter databaseReaderWriter;
		private DispatcherTimer triggerReadChip;
		private DispatcherTimer taskTimeout;

		private ChipTaskHandlerModel taskHandler;
		private List<MifareClassicChipModel> mifareClassicUidModels = new List<MifareClassicChipModel>();
		private List<MifareDesfireChipModel> mifareDesfireViewModels = new List<MifareDesfireChipModel>();

		private readonly Version Version = Assembly.GetExecutingAssembly().GetName().Version;

		private int taskIndex = 0; //if programming takes too long; quit the process
		private bool firstRun = true;
		private Mutex mutex;

		#region events / delegates

		/// <summary>
		/// will raise notifier to inform user about available updates
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		public delegate void updateReady(object sender, EventArgs e);
		#endregion

		#region Constructors

		public MainWindowViewModel()
		{
			RunMutex(this, null);

			using (SettingsReaderWriter settings = new SettingsReaderWriter())
			{
				CurrentReader = string.IsNullOrWhiteSpace(settings.DefaultSpecification.DefaultReaderName)
					? Enum.GetName(typeof(ReaderTypes), settings.DefaultSpecification.DefaultReaderProvider)
					: settings.DefaultSpecification.DefaultReaderName;
			}

			updater = new Updater();

			triggerReadChip = new DispatcherTimer();
			triggerReadChip.Interval = new TimeSpan(0, 0, 0, 0, 500);

			triggerReadChip.Tick += UpdateChip;

			triggerReadChip.Start();
			triggerReadChip.IsEnabled = false;
			triggerReadChip.Tag = triggerReadChip.IsEnabled;

			taskTimeout = new DispatcherTimer();
			taskTimeout.Interval = new TimeSpan(0, 0, 0, 10, 0);

			taskTimeout.Tick += TaskTimeout;
			taskTimeout.Start();
			taskTimeout.IsEnabled = false;

			treeViewParentNodes = new ObservableCollection<TreeViewParentNodeViewModel>();

			taskHandler = new ChipTaskHandlerModel();

			ReaderStatus = CurrentReader == "None" ? "" : "ready";
			databaseReaderWriter = new DatabaseReaderWriter();
			resLoader = new ResourceLoader();

			Application.Current.MainWindow.Activated += new EventHandler(LoadCompleted);
			updater.newVersionAvailable += new EventHandler(AskForUpdateNow);


			//any dialog boxes added in the constructor won't appear until DialogBehavior.DialogViewModels gets bound to the Dialogs collection.
		}

		#endregion Constructors

		#region Dialogs

		private ObservableCollection<IDialogViewModel> dialogs = new ObservableCollection<IDialogViewModel>();
		public ObservableCollection<IDialogViewModel> Dialogs { get { return dialogs; } }

		#endregion Dialogs

		#region Localization

		/// <summary>
		/// Expose translated strings from ResourceLoader
		/// </summary>
		public string LocalizationResourceSet { get; set; }

		#endregion Localization

		#region Menu Commands

		/// <summary>
		/// here we perform all tasks on cards with a periodic check for new cards to work with
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void UpdateChip(object sender, EventArgs e)
		{
			CARD_INFO card;

			try
			{
				//try to get singleton instance
				using (RFiDDevice device = RFiDDevice.Instance)
				{
					//reader was ready - proceed
					if (device != null)
					{
						device.ReadChipPublic();

						card = device.CardInfo;
					}
					else
					{
						return;
					}
				}
				//proceed to create dummy only when uid is yet unknown
				if (!string.IsNullOrWhiteSpace(card.uid) &&
				    !treeViewParentNodes.Any(x => (x.UidNumber == card.uid)))
				{
					foreach (TreeViewParentNodeViewModel item in treeViewParentNodes)
					{
						item.IsExpanded = false;
					}

					// fill treeview with dummy models and viewmodels
					switch (card.cardType)
					{
						case CARD_TYPE.Mifare1K:
							treeViewParentNodes.Add(new TreeViewParentNodeViewModel(new MifareClassicChipModel(card.uid, CARD_TYPE.Mifare1K), Dialogs));
							break;

						case CARD_TYPE.Mifare2K:
							treeViewParentNodes.Add(new TreeViewParentNodeViewModel(new MifareClassicChipModel(card.uid, CARD_TYPE.Mifare2K), Dialogs));
							break;

						case CARD_TYPE.Mifare4K:
							treeViewParentNodes.Add(new TreeViewParentNodeViewModel(new MifareClassicChipModel(card.uid, CARD_TYPE.Mifare4K), Dialogs));
							break;

						case CARD_TYPE.DESFire:
						case CARD_TYPE.DESFireEV1:
						case CARD_TYPE.DESFireEV2:
							treeViewParentNodes.Add(new TreeViewParentNodeViewModel(new MifareDesfireChipModel(card.uid, card.cardType), Dialogs));
							break;
					}
					OnNewResetTaskStatusCommand();
					OnNewWriteToChipOnceCommand();
				}
			}
			catch (Exception ex)
			{
				LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, ex.Message, ex.InnerException != null ? ex.InnerException.Message : ""));
			}
		}

		private void TaskTimeout(object sender, EventArgs e)
		{
			taskTimeout.IsEnabled = false;
			taskTimeout.Stop();
			if (taskHandler.GetTaskType == typeof(MifareDesfireSetupViewModel))
				(taskHandler.TaskCollection[(int)taskTimeout.Tag] as MifareDesfireSetupViewModel).IsTaskCompletedSuccessfully = false;
			else if (taskHandler.GetTaskType == typeof(MifareClassicSetupViewModel))
				(taskHandler.TaskCollection[(int)taskTimeout.Tag] as MifareClassicSetupViewModel).IsTaskCompletedSuccessfully = false;
			taskIndex = int.MaxValue;
		}
		
		public ICommand CreateClassicTaskCommand { get { return new RelayCommand(OnNewCreateClassicTaskCommand); } }
		private void OnNewCreateClassicTaskCommand()
		{
			bool timerState = triggerReadChip.IsEnabled;

			triggerReadChip.IsEnabled = false;

			try
			{
				using (RFiDDevice device = RFiDDevice.Instance)
				{
					// only call dialog if device is ready
					if (device != null)
					{
						this.dialogs.Add(new MifareClassicSetupViewModel(SelectedSetupViewModel, dialogs)
						                 {
						                 	Caption = String.Format("{0} UID:[{1}] Type:[{2}]",
						                 	                        ResourceLoader.getResource("mifareAuthSettingsDialogCaption"),
						                 	                        "empty",
						                 	                        "none"),
						                 	IsClassicAuthInfoEnabled = true, //content.Contains("EditAccessBits"),

						                 	OnOk = (sender) =>
						                 	{
						                 		if (sender.SelectedTaskType == TaskType_MifareClassicTask.ChangeDefault)
						                 			sender.Settings.SaveSettings();

						                 		if (sender.SelectedTaskType == TaskType_MifareClassicTask.WriteData ||
						                 		    sender.SelectedTaskType == TaskType_MifareClassicTask.ReadData)
						                 		{
						                 			ChipTasks.TaskCollection.Add(sender);

						                 			ChipTasks.TaskCollection = new ObservableCollection<object>(ChipTasks.TaskCollection.OrderBy(x => (x as MifareClassicSetupViewModel).SelectedTaskIndexAsInt));

						                 			RaisePropertyChanged("ChipTasks");
						                 		}
						                 		sender.Close();
						                 	},

						                 	OnCancel = (sender) =>
						                 	{
						                 		sender.Close();
						                 	},

						                 	OnAuth = (sender) =>
						                 	{
						                 	},

						                 	OnCloseRequest = (sender) =>
						                 	{
						                 		sender.Close();
						                 	}
						                 });
					}
				}
			}
			catch
			{
				dialogs.Clear();
			}

			triggerReadChip.IsEnabled = timerState;
		}

		public ICommand CreateDesfireTaskCommand { get { return new RelayCommand(OnNewCreateDesfireTaskCommand); } }
		private void OnNewCreateDesfireTaskCommand()
		{
			bool timerState = triggerReadChip.IsEnabled;

			triggerReadChip.IsEnabled = false;

			using (RFiDDevice device = RFiDDevice.Instance)
			{
				if (device != null)
				{
					this.dialogs.Add(new MifareDesfireSetupViewModel(SelectedSetupViewModel, dialogs)
					                 {
					                 	Caption = String.Format("{0} UID:[{1}] Type:[{2}]",
					                 	                        ResourceLoader.getResource("mifareAuthSettingsDialogCaption"),
					                 	                        "empty",
					                 	                        "none"),

					                 	OnOk = (sender) =>
					                 	{
					                 		if (sender.SelectedTaskType == TaskType_MifareDesfireTask.ChangeDefault)
					                 			sender.Settings.SaveSettings();

					                 		if (sender.SelectedTaskType == TaskType_MifareDesfireTask.FormatDesfireCard ||
					                 		    sender.SelectedTaskType == TaskType_MifareDesfireTask.CreateApplication ||
					                 		    sender.SelectedTaskType == TaskType_MifareDesfireTask.PICCMasterKeyChangeover ||
					                 		    sender.SelectedTaskType == TaskType_MifareDesfireTask.ApplicationKeyChangeover ||
					                 		    sender.SelectedTaskType == TaskType_MifareDesfireTask.DeleteApplication ||
					                 		    sender.SelectedTaskType == TaskType_MifareDesfireTask.CreateFile)
					                 		{
					                 			ChipTasks.TaskCollection.Add(sender);

					                 			ChipTasks.TaskCollection = new ObservableCollection<object>(ChipTasks.TaskCollection.OrderBy(x => (x as MifareDesfireSetupViewModel).SelectedTaskIndexAsInt));

					                 			RaisePropertyChanged("ChipTasks");

					                 			sender.Close();
					                 		}
					                 	},

					                 	OnCancel = (sender) =>
					                 	{
					                 		sender.Close();
					                 	},

					                 	OnCloseRequest = (sender) =>
					                 	{
					                 		sender.Close();
					                 	}
					                 });
				}
			}

			triggerReadChip.IsEnabled = timerState;
		}

		public ICommand ReadChipCommand { get { return new RelayCommand(OnNewReadChipCommand); } }
		private void OnNewReadChipCommand()
		{
			bool timerState = triggerReadChip.IsEnabled;

			triggerReadChip.IsEnabled = false;

			using (RFiDDevice device = RFiDDevice.Instance)
			{
				foreach (TreeViewParentNodeViewModel item in treeViewParentNodes)
				{
					item.IsExpanded = false;
				}

				device.ReadChipPublic();

				if (device != null &&
				    !string.IsNullOrWhiteSpace(device.CardInfo.uid) &&
				    !treeViewParentNodes.Where(x => x.UidNumber == device.CardInfo.uid).Any())
				{
					// fill treeview with dummy models and viewmodels
					switch (device.CardInfo.cardType)
					{
						case CARD_TYPE.Mifare1K:
							treeViewParentNodes.Add(new TreeViewParentNodeViewModel(new MifareClassicChipModel(device.CardInfo.uid, CARD_TYPE.Mifare1K), Dialogs));
							break;

						case CARD_TYPE.Mifare2K:
							treeViewParentNodes.Add(new TreeViewParentNodeViewModel(new MifareClassicChipModel(device.CardInfo.uid, CARD_TYPE.Mifare2K), Dialogs));
							break;

						case CARD_TYPE.Mifare4K:
							treeViewParentNodes.Add(new TreeViewParentNodeViewModel(new MifareClassicChipModel(device.CardInfo.uid, CARD_TYPE.Mifare4K), Dialogs));
							break;

						case CARD_TYPE.DESFire:
						case CARD_TYPE.DESFireEV1:
						case CARD_TYPE.DESFireEV2:
							treeViewParentNodes.Add(new TreeViewParentNodeViewModel(new MifareDesfireChipModel(device.CardInfo.uid, device.CardInfo.cardType), Dialogs));
							break;
					}

					//fill the models with data from db
					foreach (TreeViewParentNodeViewModel pnVM in treeViewParentNodes)
					{
						//if (pnVM.mifareClassicUidModel != null)
						//databaseReaderWriter.WriteDatabase(pnVM.mifareClassicUidModel);
						//else
						//databaseReaderWriter.WriteDatabase(pnVM.mifareDesfireUidModel);
					}
				}
				else if (treeViewParentNodes.Any(x => x.UidNumber == device.CardInfo.uid))
				{
					treeViewParentNodes.First(x => x.UidNumber == device.CardInfo.uid).IsSelected = true;
				}
			}

			triggerReadChip.IsEnabled = timerState;
		}

		public ICommand ResetTaskStatusCommand { get { return new RelayCommand(OnNewResetTaskStatusCommand); } }
		private void OnNewResetTaskStatusCommand()
		{
			foreach (object chipTask in taskHandler.TaskCollection)
			{
				if (chipTask is MifareClassicSetupViewModel)
				{
					(chipTask as MifareClassicSetupViewModel).IsTaskCompletedSuccessfully = null;
					(chipTask as MifareClassicSetupViewModel).TaskErr = ERROR.Empty;
				}
				else if (chipTask is MifareDesfireSetupViewModel)
				{
					(chipTask as MifareDesfireSetupViewModel).IsTaskCompletedSuccessfully = null;
					(chipTask as MifareDesfireSetupViewModel).TaskErr = ERROR.Empty;
				}
			}
		}

		public ICommand WriteToAllChipAutoCommand { get { return new RelayCommand(OnNewWriteToAllChipAutoCommand); } }
		private void OnNewWriteToAllChipAutoCommand()
		{
			if (!triggerReadChip.IsEnabled)
				triggerReadChip.IsEnabled = true;
			else
				triggerReadChip.IsEnabled = false;
		}

		public ICommand WriteToChipOnceCommand { get { return new RelayCommand(OnNewWriteToChipOnceCommand); } }
		private void OnNewWriteToChipOnceCommand()
		{
			taskIndex = 0;

			taskTimeout.IsEnabled = true;
			taskTimeout.Start();

			triggerReadChip.Tag = triggerReadChip.IsEnabled;
			triggerReadChip.IsEnabled = false;

			Task thread = new Task(() =>
			                       {
			                       	CARD_INFO card;

			                       	try
			                       	{
			                       		//try to get singleton instance
			                       		using (RFiDDevice device = RFiDDevice.Instance)
			                       		{
			                       			//reader was ready - proceed
			                       			if (device != null)
			                       			{
			                       				device.ReadChipPublic();

			                       				card = device.CardInfo;
			                       			}
			                       			else
			                       				card = new CARD_INFO(CARD_TYPE.Unspecified, "");
			                       		}

			                       		//only run if theres a card on the reader and its uid was previously added
			                       		if (
			                       			!string.IsNullOrWhiteSpace(card.uid) &&
			                       			treeViewParentNodes.Any(x => (x.UidNumber == card.uid)))
			                       		{
			                       			//select current parentnode (card) on reader
			                       			treeViewParentNodes.First(x => (x.UidNumber == card.uid)).IsSelected = true;
			                       			treeViewParentNodes.First(x => x.IsSelected).IsBeingProgrammed = true;

			                       			//are there tasks present to process?
			                       			while (taskIndex < taskHandler.TaskCollection.Count)
			                       			{
			                       				Thread.Sleep(100);

			                       				taskTimeout.Tag = taskIndex;

			                       				//decide what type of card to process
			                       				if (taskHandler.GetTaskType == typeof(MifareClassicSetupViewModel))
			                       				{
			                       					switch ((taskHandler.TaskCollection[taskIndex] as MifareClassicSetupViewModel).SelectedTaskType)
			                       					{
			                       						case TaskType_MifareClassicTask.ReadData:
			                       							switch ((taskHandler.TaskCollection[taskIndex] as MifareClassicSetupViewModel).TaskErr)
			                       							{
			                       								case ERROR.AuthenticationError:
			                       									//FIXME (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).IsTaskCompletedSuccessfully = false;
			                       									(taskHandler.TaskCollection[taskIndex] as MifareClassicSetupViewModel).TaskErr = ERROR.Empty;
			                       									break;

			                       								case ERROR.DeviceNotReadyError:
			                       									break;

			                       								case ERROR.IOError:
			                       									break;

			                       								case ERROR.NoError:
			                       									(taskHandler.TaskCollection[taskIndex] as MifareClassicSetupViewModel).IsTaskCompletedSuccessfully = true;
			                       									taskIndex++;
			                       									//taskTimeout.IsEnabled = false;
			                       									taskTimeout.Start();
			                       									break;

			                       								case ERROR.Empty:
			                       									taskTimeout.Start();
			                       									(taskHandler.TaskCollection[taskIndex] as MifareClassicSetupViewModel).ReadDataCommand.Execute(null);
			                       									break;
			                       							}
			                       							break;

			                       						case TaskType_MifareClassicTask.WriteData:

			                       							switch ((taskHandler.TaskCollection[taskIndex] as MifareClassicSetupViewModel).TaskErr)
			                       							{
			                       								case ERROR.AuthenticationError:
			                       									//FIXME (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).IsTaskCompletedSuccessfully = false;
			                       									(taskHandler.TaskCollection[taskIndex] as MifareClassicSetupViewModel).TaskErr = ERROR.Empty;
			                       									break;

			                       								case ERROR.DeviceNotReadyError:
			                       									break;

			                       								case ERROR.IOError:
			                       									break;

			                       								case ERROR.NoError:
			                       									(taskHandler.TaskCollection[taskIndex] as MifareClassicSetupViewModel).IsTaskCompletedSuccessfully = true;
			                       									taskIndex++;
			                       									//taskTimeout.IsEnabled = false;
			                       									taskTimeout.Start();
			                       									break;

			                       								case ERROR.Empty:
			                       									taskTimeout.Start();
			                       									(taskHandler.TaskCollection[taskIndex] as MifareClassicSetupViewModel).WriteDataCommand.Execute(null);
			                       									break;
			                       							}
			                       							break;
			                       					}
			                       				}
			                       				if (taskHandler.GetTaskType == typeof(MifareDesfireSetupViewModel))
			                       				{
			                       					switch ((taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedTaskType)
			                       					{
			                       						case TaskType_MifareDesfireTask.FormatDesfireCard:
			                       							switch ((taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).TaskErr)
			                       							{
			                       								case ERROR.AuthenticationError:
			                       									//FIXME (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).IsTaskCompletedSuccessfully = false;
			                       									(taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).TaskErr = ERROR.Empty;
			                       									break;

			                       								case ERROR.DeviceNotReadyError:
			                       									break;

			                       								case ERROR.IOError:
			                       									break;

			                       								case ERROR.NoError:
			                       									(taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).IsTaskCompletedSuccessfully = true;
			                       									taskIndex++;
			                       									//taskTimeout.IsEnabled = false;
			                       									taskTimeout.Start();
			                       									break;

			                       								case ERROR.Empty:
			                       									taskTimeout.Start();
			                       									(taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).FormatDesfireCardCommand.Execute(null);
			                       									break;
			                       							}
			                       							break;

			                       						case TaskType_MifareDesfireTask.CreateApplication:
			                       							switch ((taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).TaskErr)
			                       							{
			                       								case ERROR.AuthenticationError:
			                       									//FIXME (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).IsTaskCompletedSuccessfully = false;
			                       									(taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).TaskErr = ERROR.Empty;
			                       									break;

			                       								case ERROR.DeviceNotReadyError:
			                       									break;

			                       								case ERROR.IOError:
			                       									break;

			                       								case ERROR.NoError:
			                       									(taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).IsTaskCompletedSuccessfully = true;
			                       									taskIndex++;
			                       									//taskTimeout.IsEnabled = false;
			                       									taskTimeout.Start();
			                       									break;

			                       								case ERROR.Empty:
			                       									taskTimeout.Start();
			                       									(taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).CreateAppCommand.Execute(null);
			                       									break;
			                       							}
			                       							break;

			                       						case TaskType_MifareDesfireTask.DeleteApplication:
			                       							switch ((taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).TaskErr)
			                       							{
			                       								case ERROR.AuthenticationError:
			                       									//FIXME (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).IsTaskCompletedSuccessfully = false;
			                       									(taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).TaskErr = ERROR.Empty;
			                       									break;

			                       								case ERROR.DeviceNotReadyError:
			                       									break;

			                       								case ERROR.IOError:
			                       									break;

			                       								case ERROR.NoError:
			                       									(taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).IsTaskCompletedSuccessfully = true;
			                       									taskIndex++;
			                       									//taskTimeout.IsEnabled = false;
			                       									taskTimeout.Start();
			                       									break;

			                       								case ERROR.Empty:
			                       									taskTimeout.Start();
			                       									(taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).DeleteSignleCardApplicationCommand.Execute(null);
			                       									break;
			                       							}
			                       							break;

			                       						case TaskType_MifareDesfireTask.PICCMasterKeyChangeover:
			                       							switch ((taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).TaskErr)
			                       							{
			                       								case ERROR.AuthenticationError:
			                       									//FIXME (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).IsTaskCompletedSuccessfully = false;
			                       									(taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).TaskErr = ERROR.Empty;
			                       									break;

			                       								case ERROR.DeviceNotReadyError:
			                       									break;

			                       								case ERROR.IOError:
			                       									break;

			                       								case ERROR.NoError:
			                       									(taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).IsTaskCompletedSuccessfully = true;
			                       									taskIndex++;
			                       									//taskTimeout.IsEnabled = false;
			                       									taskTimeout.Start();
			                       									break;

			                       								case ERROR.Empty:
			                       									taskTimeout.Start();
			                       									(taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).ChangeMasterCardKeyCommand.Execute(null);
			                       									break;
			                       							}
			                       							break;

			                       						case TaskType_MifareDesfireTask.ApplicationKeyChangeover:
			                       							switch ((taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).TaskErr)
			                       							{
			                       								case ERROR.AuthenticationError:
			                       									//FIXME (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).IsTaskCompletedSuccessfully = false;
			                       									(taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).TaskErr = ERROR.Empty;
			                       									break;

			                       								case ERROR.DeviceNotReadyError:
			                       									break;

			                       								case ERROR.IOError:
			                       									break;

			                       								case ERROR.NoError:
			                       									(taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).IsTaskCompletedSuccessfully = true;
			                       									taskIndex++;
			                       									//taskTimeout.IsEnabled = false;
			                       									taskTimeout.Start();
			                       									break;

			                       								case ERROR.Empty:
			                       									taskTimeout.Start();
			                       									(taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).ChangeAppKeyCommand.Execute(null);
			                       									break;
			                       							}
			                       							break;

			                       						case TaskType_MifareDesfireTask.CreateFile:
			                       							switch ((taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).TaskErr)
			                       							{
			                       								case ERROR.AuthenticationError:
			                       									//FIXME (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).IsTaskCompletedSuccessfully = false;
			                       									(taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).TaskErr = ERROR.Empty;
			                       									break;

			                       								case ERROR.DeviceNotReadyError:
			                       									break;

			                       								case ERROR.IOError:
			                       									break;

			                       								case ERROR.NoError:
			                       									(taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).IsTaskCompletedSuccessfully = true;
			                       									taskIndex++;
			                       									//taskTimeout.IsEnabled = false;
			                       									taskTimeout.Start();
			                       									break;

			                       								case ERROR.Empty:
			                       									taskTimeout.Start();
			                       									(taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).CreateFileCommand.Execute(null);
			                       									break;
			                       							}
			                       							break;
			                       					}
			                       				}
			                       			}
			                       		}
			                       		else
			                       		{
			                       			if (treeViewParentNodes.Any(x => x.IsSelected))
			                       				treeViewParentNodes.First(x => x.IsSelected).IsSelected = false;
			                       		}

			                       		RaisePropertyChanged("TreeViewParentNodes");

			                       		taskTimeout.Stop();
			                       	}
			                       	catch (Exception e)
			                       	{
			                       		LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""));
			                       	}
			                       });

			thread.ContinueWith((x) =>
			                    {
			                    	treeViewParentNodes.First(y => y.IsSelected).IsBeingProgrammed = null;
			                    	triggerReadChip.IsEnabled = (bool)triggerReadChip.Tag;
			                    });
			thread.Start();
		}

		public ICommand CloseAllCommand { get { return new RelayCommand(OnCloseAll); } }
		private void OnCloseAll()
		{
			this.Dialogs.Clear();
		}

		public ICommand SwitchLanguageToGerman { get { return new RelayCommand(SetGermanLanguage); } }
		private void SetGermanLanguage()
		{
			using (SettingsReaderWriter settings = new SettingsReaderWriter())
			{
				if (settings.DefaultSpecification.DefaultLanguage != "german")
				{
					settings.DefaultSpecification.DefaultLanguage = "german";
					settings.SaveSettings();
					this.OnNewLanguageChangedDialog();
				}
			}
		}

		public ICommand SwitchLanguageToEnglish { get { return new RelayCommand(SetEnglishLanguage); } }
		private void SetEnglishLanguage()
		{
			using (SettingsReaderWriter settings = new SettingsReaderWriter())
			{
				if (settings.DefaultSpecification.DefaultLanguage != "english")
				{
					settings.DefaultSpecification.DefaultLanguage = "english";
					settings.SaveSettings();
					this.OnNewLanguageChangedDialog();
				}
			}
		}

		private void OnNewLanguageChangedDialog()
		{
			this.Dialogs.Add(new CustomDialogViewModel
			                 {
			                 	Message = ResourceLoader.getResource("messageBoxRestartRequiredMessage"),
			                 	Caption = ResourceLoader.getResource("messageBoxRestartRequiredCaption"),

			                 	OnOk = (sender) =>
			                 	{
			                 		sender.Close();
			                 		App.Current.Shutdown();
			                 	},

			                 	OnCancel = (sender) =>
			                 	{
			                 		sender.Close();
			                 	},

			                 	OnCloseRequest = (sender) =>
			                 	{
			                 		sender.Close();
			                 	}
			                 });
		}

		/// <summary>
		/// 
		/// </summary>
		public ICommand NewReaderSetupDialogCommand { get { return new RelayCommand(OnNewReaderSetupDialog); } }
		private void OnNewReaderSetupDialog()
		{
			using (RFiDDevice device = RFiDDevice.Instance)
			{
				this.Dialogs.Add(new SetupViewModel(device)
				                 {
				                 	Caption = "RFiDGear Reader Setup",

				                 	OnOk = (sender) =>
				                 	{
				                 		using (SettingsReaderWriter settings = new SettingsReaderWriter())
				                 		{
				                 			DefaultSpecification currentSettings = settings.DefaultSpecification;

				                 			currentSettings.DefaultReaderProvider = sender.SelectedReader;

				                 			settings.DefaultSpecification = currentSettings;

				                 			sender.Close();
				                 		}
				                 	},

				                 	OnConnect = (sender) =>
				                 	{
				                 	},

				                 	OnCancel = (sender) =>
				                 	{
				                 		sender.Close();
				                 	},

				                 	OnCloseRequest = (sender) =>
				                 	{
				                 		sender.Close();
				                 	}
				                 });
			}
		}

		/// <summary>
		/// 
		/// </summary>
		public ICommand NewOpenFileDialogCommand { get { return new RelayCommand(OnNewOpenFileDialog); } }
		private void OnNewOpenFileDialog()
		{
			var dlg = new OpenFileDialogViewModel
			{
				Title = ResourceLoader.getResource("windowTitleOpenProject"),
				Filter = ResourceLoader.getResource("filterStringSaveTasks"),
				Multiselect = false
			};

			if (dlg.Show(this.Dialogs) && dlg.FileName != null)
			{
				if (ChipTasks.TaskCollection != null && ChipTasks.TaskCollection.Count > 0)
					ChipTasks.TaskCollection.Clear();

				databaseReaderWriter.ReadDatabase(dlg.FileName);

				foreach (TreeViewParentNodeViewModel vm in databaseReaderWriter.treeViewModel)
				{
					TreeViewParentNodes.Add(vm);
				}

				foreach (object setup in databaseReaderWriter.setupModel.TaskCollection)
				{
					ChipTasks.TaskCollection.Add(setup);
				}
			}

			RaisePropertyChanged("ChipTasks");
		}

		/// <summary>
		/// Expose Command to Save As Menu Item
		/// </summary>
		public ICommand SaveTaskDialogCommand { get { return new RelayCommand(OnNewSaveTaskDialogCommand); } }
		private void OnNewSaveTaskDialogCommand()
		{
			var dlg = new SaveFileDialogViewModel
			{
				Title = ResourceLoader.getResource("windowTitleSaveTasks"),
				Filter = ResourceLoader.getResource("filterStringSaveTasks")
			};

			if (dlg.Show(this.Dialogs) && dlg.FileName != null)
			{
				databaseReaderWriter.WriteDatabase(ChipTasks, dlg.FileName);
			}
		}

		/// <summary>
		/// Expose Command to Save Menu Item
		/// </summary>
		public ICommand SaveChipDialogCommand { get { return new RelayCommand(OnNewSaveChipDialogCommand); } }
		private void OnNewSaveChipDialogCommand()
		{
			databaseReaderWriter.WriteDatabase(TreeViewParentNodes);
		}

		/// <summary>
		/// 
		/// </summary>
		public ICommand CloseApplication { get { return new RelayCommand(OnCloseRequest); } }
		private void OnCloseRequest()
		{
			App.Current.Shutdown();
		}

		#endregion Menu Commands

		#region Dependency Properties

//		/// <summary>
//		/// 
//		/// </summary>
//		public bool? TaskCompletedSuccessfully
//		{
//			get { return null; }
//		}
//
//		/// <summary>
//		/// 
//		/// </summary>
//		public string RowHeaderImageVisibility
//		{
//			get { return "Visible"; }
//		}

		/// <summary>
		/// 
		/// </summary>
		public object SelectedSetupViewModel
		{
			get { return selectedSetupViewModel; }
			set {
				selectedSetupViewModel = value;
				RaisePropertyChanged("SelectedSetupViewModel");
			}
			
		} private object selectedSetupViewModel;

		/// <summary>
		/// 
		/// </summary>
		public ObservableCollection<TreeViewParentNodeViewModel> TreeViewParentNodes
		{
			get
			{
				return treeViewParentNodes;
			}

			set
			{
				treeViewParentNodes = value;
				RaisePropertyChanged("TreeViewParentNodes");
			}
		} private ObservableCollection<TreeViewParentNodeViewModel> treeViewParentNodes;
		
		/// <summary>
		/// 
		/// </summary>
		public ChipTaskHandlerModel ChipTasks
		{
			get
			{
				return taskHandler;
			}

			set
			{
				taskHandler = value;
				RaisePropertyChanged("ChipTasks");
			}
		}

		/// <summary>
		/// 
		/// </summary>
		public bool IsUpdateChipsAutomaticallyChecked
		{
			get { return triggerReadChip.IsEnabled; }
		}

		/// <summary>
		///
		/// </summary>
		public string CurrentReader
		{
			get { return currentReader; }
			set
			{
				currentReader = value;
				RaisePropertyChanged("CurrentReader");
			}
		} private string currentReader;

		/// <summary>
		///
		/// </summary>
		public string ReaderStatus
		{
			get { return readerStatus; }
			set
			{
				readerStatus = value;
				RaisePropertyChanged("ReaderStatus");
			}
		} private string readerStatus;
		
		/// <summary>
		/// 
		/// </summary>
		public bool IsSelected { get; set; }

		/// <summary>
		/// 
		/// </summary>
		public bool IsCheckForUpdatesChecked
		{
			get { using (SettingsReaderWriter settings = new SettingsReaderWriter()) { return settings.DefaultSpecification != null ? settings.DefaultSpecification.AutoCheckForUpdates : false; } }
			set
			{
				using (SettingsReaderWriter settings = new SettingsReaderWriter())
				{
					if (value)
						updater.StartMonitoring();

					else
						updater.StopMonitoring();

					settings.DefaultSpecification.AutoCheckForUpdates = value;
					settings.SaveSettings();
				}
				RaisePropertyChanged("IsCheckForUpdatesChecked");

			}
		}

		public bool RadioButtonGermanLanguageSelectedState
		{
			get
			{
				using (SettingsReaderWriter settings = new SettingsReaderWriter())
				{
					if (settings.DefaultSpecification.DefaultLanguage == "german")
						return true;
					else
						return false;
				}
			}
			set
			{
				using (SettingsReaderWriter settings = new SettingsReaderWriter())
				{
					if (settings.DefaultSpecification.DefaultLanguage == "english")
						value = false;
					RaisePropertyChanged("RadioButtonGermanLanguageSelectedState");
				}
			}
		}

		public bool RadioButtonEnglishLanguageSelectedState
		{
			get
			{
				using (SettingsReaderWriter settings = new SettingsReaderWriter())
				{
					if (settings.DefaultSpecification.DefaultLanguage == "german")
						return false;
					else
						return true;
				}
			}
			set
			{
				using (SettingsReaderWriter settings = new SettingsReaderWriter())
				{
					if (settings.DefaultSpecification.DefaultLanguage == "german")
						value = false;
					RaisePropertyChanged("RadioButtonEnglishLanguageSelectedState");
				}
			}
		}

		#endregion Dependency Properties

		#region Extensions

		private void AskForUpdateNow(object sender, EventArgs e)
		{
			if (new MessageBoxViewModel
			    {
			    	Caption = ResourceLoader.getResource("messageBoxUpdateAvailableCaption"),
			    	Message = ResourceLoader.getResource("messageBoxUpdateAvailableMessage"),
			    	Buttons = MessageBoxButton.YesNo,
			    	Image = MessageBoxImage.Question
			    }.Show(this.Dialogs) == MessageBoxResult.Yes)
				(sender as Updater).allowUpdate = true;
			else
			{
				(sender as Updater).allowUpdate = false;
			}

		}

		//Only one instance is allowed due to the pipeserver listening for event_cmd.exe
		private void RunMutex(object sender, StartupEventArgs e)
		{
			bool aIsNewInstance = false;
			mutex = new Mutex(true, "App", out aIsNewInstance);

			if (!aIsNewInstance)
			{
				Environment.Exit(0);
			}

		}
		private void LoadCompleted(object sender, EventArgs e)
		{
			mw = (MainWindow)Application.Current.MainWindow;
			mw.Title = string.Format("RFiDGear {0}.{1}.{2} {3}", Version.Major, Version.Minor, Version.Build, Version.Major == 0 ? "DEVELOPER PREVIEW" : "");

			if (firstRun)
			{
				firstRun = false;
				using (SettingsReaderWriter settings = new SettingsReaderWriter())
				{
					if (settings.DefaultSpecification.AutoCheckForUpdates)
						updater.StartMonitoring();
				}

			}

			#endregion
		}
	}
}