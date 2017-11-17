using RFiDGear;
using RFiDGear.DataAccessLayer;
using RFiDGear.Model;

using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;

using MvvmDialogs.ViewModels;

using RedCell.Diagnostics.Update;

using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Linq;
using System.Windows;

namespace RFiDGear.ViewModel
{
	/// <summary>
	/// Description of MainWindowViewModel.
	/// </summary>
	public class MainWindowViewModel : ViewModelBase
	{
		//private ReaderModel readerModel;
		private ResourceLoader resLoader;
		private SettingsReaderWriter settings;
		//private RFiDDevice device;
		private Updater updater = new Updater();
		private DatabaseReaderWriter databaseReaderWriter;
		private DispatcherTimer triggerReadChip;
		private DispatcherTimer taskTimeout;

		private ChipTaskHandlerModel taskHandler;
		private List<MifareClassicChipModel> mifareClassicUidModels = new List<MifareClassicChipModel>();
		private List<MifareDesfireChipModel> mifareDesfireViewModels = new List<MifareDesfireChipModel>();
		
		private MifareClassicSetupViewModel defaultTask;
		
		int taskIndex = 0; //if programming takes too long; quit the process
		
		#region Constructors
		
		public MainWindowViewModel()
		{			
			triggerReadChip = new DispatcherTimer();
			triggerReadChip.Interval = new TimeSpan(0,0,0,0,500);
			
			triggerReadChip.Tick += UpdateChip;
			
			triggerReadChip.Start();
			triggerReadChip.IsEnabled = false;
			
			taskTimeout = new DispatcherTimer();
			taskTimeout.Interval = new TimeSpan(0,0,0,10,0);
			
			taskTimeout.Tick += TaskTimeout;
			taskTimeout.Start();
			taskTimeout.IsEnabled = false;
			
			treeViewParentNodes = new ObservableCollection<TreeViewParentNodeViewModel>();
			//treeViewParentTaskNodes = new ObservableCollection<TreeViewParentNodeViewModel>();
			
			defaultTask = new MifareClassicSetupViewModel(new TreeViewParentNodeViewModel(new MifareClassicChipModel("no Task", CARD_TYPE.Unspecified), dialogs, true),dialogs);
			//defaultTask.IsSelected = false;
			
			taskHandler = new ChipTaskHandlerModel();
			
			TreeViewParentTaskNodes.TaskCollection.Add(defaultTask);
			
			//readerModel = new ReaderModel();
			settings = new SettingsReaderWriter();
			//device = new RFiDDevice(settings.DefaultSpecification);
			databaseReaderWriter = new DatabaseReaderWriter();
			resLoader = new ResourceLoader();

			progressBarValue = 50;
			
			//any dialog boxes added in the constructor won't appear until DialogBehavior.DialogViewModels gets bound to the Dialogs collection.
		}
		
		#endregion
		
		#region Dialogs
		
		private ObservableCollection<IDialogViewModel> dialogs = new ObservableCollection<IDialogViewModel>();
		public ObservableCollection<IDialogViewModel> Dialogs { get { return dialogs; } }
		
		#endregion
		
		
		#region Items Sources
		public bool? TaskCompletedSuccessfully
		{
			get { return null; }
		}
		
		public string RowHeaderImageVisibility
		{
			get { return "Visible"; }
		}
		
		private int progressBarValue;
		public int ProgressBarValue {
			get { return progressBarValue; }
			set { progressBarValue = value;
				RaisePropertyChanged("ProgressBarValue");
			}
		}
		
		private ObservableCollection<TreeViewParentNodeViewModel> treeViewParentNodes;
		public ObservableCollection<TreeViewParentNodeViewModel> TreeViewParentNodes {
			get {
				return treeViewParentNodes;
			}
			
			set {
				treeViewParentNodes = value;
				RaisePropertyChanged("TreeViewParentNodes");
			}
		}

		
		public ChipTaskHandlerModel TreeViewParentTaskNodes {
			get {
				return taskHandler;
			}
			
			set {
				taskHandler = value;
				RaisePropertyChanged("TreeViewParentTaskNodes");
			}
		} //private ObservableCollection<TreeViewParentNodeViewModel> treeViewParentTaskNodes;
		
		#endregion
		
		#region MifareDesFIRE Communication
		
		private void ReadAppIDs(TreeViewParentNodeViewModel selectedPNVM, string content) {
			using (RFiDDevice device = new RFiDDevice(settings.DefaultSpecification.DefaultReaderProvider))
			{
				foreach(uint appID in device.GetAppIDList){
					selectedPNVM.Children.Add(
						new TreeViewChildNodeViewModel(
							new MifareDesfireAppIdTreeViewModel(appID),selectedPNVM, device.CardInfo.cardType));
					//String.Format("{0:d2}",)),, ));
				}
			}
			
		}
		
		private void CreateApp(TreeViewParentNodeViewModel selectedPNVM, string content) {
			
			this.Dialogs.Add(new MifareDesfireSetupViewModel(selectedPNVM, dialogs) {
			                 	
			                 	
			                 	OnOk = (sender) => {
			                 		
			                 	},

			                 	OnCancel = (sender) => {
			                 		
			                 		sender.Close();

			                 	},

			                 	OnCloseRequest = (sender) => {
			                 		sender.Close();
			                 	}
			                 });
		}
		#endregion
		
		#region MifareClassic Communication

		
		
		private void ReadSectorsWithDefaultConfig(TreeViewChildNodeViewModel selectedCnVM, string content)
		{
			using (RFiDDevice device = new RFiDDevice(settings.DefaultSpecification.DefaultReaderProvider))
			{
				selectedCnVM.IsSelected = true;
				
				Mouse.OverrideCursor = Cursors.Wait;
				
				TreeViewChildNodeViewModel vm = selectedCnVM;

				//readerModel.ReadMiFareClassicSingleSector(selectedCnVM.SectorNumber, selectedCnVM.SectorNumber);
				selectedCnVM.IsAuthenticated = device.SectorSuccesfullyAuth;
				foreach (TreeViewGrandChildNodeViewModel gcVM in selectedCnVM.Children) {
					if(selectedCnVM.SectorNumber <= 31)
						gcVM.IsAuthenticated = device.DataBlockSuccesfullyAuth[(((selectedCnVM.SectorNumber + 1) * selectedCnVM.BlockCount) - (selectedCnVM.BlockCount - gcVM.DataBlockNumber))];
					else
						gcVM.IsAuthenticated = device.DataBlockSuccesfullyAuth[((128 + (selectedCnVM.SectorNumber - 31) * selectedCnVM.BlockCount) - (selectedCnVM.BlockCount - gcVM.DataBlockNumber))];
					
					gcVM.DataBlockContent = device.currentSector[gcVM.DataBlockNumber];
				}
				Mouse.OverrideCursor = null;
			}
		}
		
		public void NewSectorTrailerEditDialog(TreeViewParentNodeViewModel uidVM, string content)
		{
			using (RFiDDevice device = new RFiDDevice(settings.DefaultSpecification.DefaultReaderProvider))
			{
				bool isClassicCard;
				
				if (uidVM.CardType == CARD_TYPE.Mifare1K || uidVM.CardType == CARD_TYPE.Mifare2K || uidVM.CardType == CARD_TYPE.Mifare4K)
					isClassicCard = true;
				else
					isClassicCard = false;
				
				this.Dialogs.Add(new MifareClassicSetupViewModel(uidVM, dialogs) {
				                 	
				                 	Caption = String.Format("{0}{1}{2}", resLoader.getResource("mifareAuthSettingsDialogCaption"), uidVM.CardType, uidVM.UidNumber),
				                 	IsClassicAuthInfoEnabled = isClassicCard,
				                 	
				                 	OnOk = (sender) => {
				                 		//databaseReaderWriter.WriteDatabase((sender.ViewModelContext as TreeViewChildNodeViewModel)._sectorModel);
				                 	},

				                 	OnCancel = (sender) => {
				                 		
				                 		sender.Close();

				                 	},
				                 	
				                 	OnAuth = (sender) => {
				                 		foreach (TreeViewChildNodeViewModel cnVM in uidVM.Children) {
				                 			//readerModel.ReadMiFareClassicSingleSector(cnVM.SectorNumber, cnVM.SectorNumber);
				                 			cnVM.IsAuthenticated = device.SectorSuccesfullyAuth;
				                 			foreach (TreeViewGrandChildNodeViewModel gcVM in cnVM.Children) {
				                 				gcVM.IsAuthenticated = device.DataBlockSuccesfullyAuth[(((cnVM.SectorNumber + 1) * cnVM.BlockCount) - (cnVM.BlockCount - gcVM.DataBlockNumber))];
				                 				gcVM.DataBlockContent = device.currentSector[gcVM.DataBlockNumber];
				                 			}
				                 		}
				                 	},

				                 	OnCloseRequest = (sender) => {
				                 		sender.Close();
				                 	}
				                 });
			}

		}
		
		public void NewSectorTrailerEditDialog(TreeViewChildNodeViewModel sectorVM, string content)
		{
			
		}
		
		#endregion
		
		#region Localization
		
		public string MenuItem_FileHeader {
			get { return resLoader.getResource("menuItemFileHeader"); }
		}
		
		public string MenuItem_ExitHeader {
			get { return resLoader.getResource("menuItemExitHeader"); }
		}
		
		public string MenuItem_EditHeader {
			get { return resLoader.getResource("menuItemEditHeader"); }
		}
		
		public string MenuItem_OptionsHeader {
			get { return resLoader.getResource("menuItemOptionsHeader"); }
		}
		
		public string MenuItem_ReaderSettingsHeader {
			get { return resLoader.getResource("menuItemReaderSettingsHeader"); }
		}
		#endregion
		
		#region Menu Commands
		
		public ICommand CreateClassicTaskCommand { get { return new RelayCommand(OnNewCreateClassicTaskCommand); } }
		private void OnNewCreateClassicTaskCommand()
		{
			bool timerState = triggerReadChip.IsEnabled;

			triggerReadChip.IsEnabled = false;
			
			try{
				using (RFiDDevice device = RFiDDevice.Instance)
				{
					TreeViewParentNodeViewModel treeViewModel;
					
					
					
					try{
						if(TreeViewParentTaskNodes.GetTaskType == typeof(MifareClassicSetupViewModel))
						{
							var item = TreeViewParentTaskNodes.TaskCollection.Where(x => (x as MifareClassicSetupViewModel).ParentNodeViewModel.IsSelected);
							treeViewModel = item.First(x => (x as TreeViewParentNodeViewModel).IsTask) as TreeViewParentNodeViewModel;
						}
						else
						{
							treeViewModel = new TreeViewParentNodeViewModel(new MifareClassicChipModel("no Task", CARD_TYPE.Mifare1K), dialogs, true);;
						}
						
						
						//.Where(x => x.ParentNodeViewModel.IsTask == true);
						//First(x => (x as TreeViewParentNodeViewModel).IsSelected);
						//treeViewModel = item as TreeViewParentNodeViewModel;
					}
					catch
					{
						treeViewModel = new TreeViewParentNodeViewModel(new MifareClassicChipModel("no Task", CARD_TYPE.Mifare1K), dialogs, true); //new MifareClassicChipModel(device.CardInfo.cardType.ToString(),device.CardInfo.cardType),dialogs);
						
					}
					
					// only call dialog if device is ready
					if(device != null)
					{
						this.dialogs.Add(new MifareClassicSetupViewModel(treeViewModel, dialogs) {
						                 	
						                 	Caption = String.Format("{0} UID:[{1}] Type:[{2}]",
						                 	                        resLoader.getResource("mifareAuthSettingsDialogCaption"),
						                 	                        "empty",
						                 	                        "none"),
						                 	IsClassicAuthInfoEnabled = true, //content.Contains("EditAccessBits"),
						                 	

						                 	OnOk = (sender) => {
						                 		
						                 		if(sender.SelectedTaskType == TaskType_MifareClassicTask.ChangeDefault)
						                 			sender.Settings.SaveSettings();
						                 		
						                 		if(sender.SelectedTaskType == TaskType_MifareClassicTask.WriteData)
						                 		{
						                 			TreeViewParentTaskNodes.TaskCollection.Remove(defaultTask);
						                 			
						                 			TreeViewChildNodeViewModel taskChild = sender.ParentNodeViewModel.Children.First(x => x.IsTask == true);
						                 			
						                 			treeViewModel.Children.Clear();
						                 			treeViewModel.Children.Add(taskChild);
						                 			treeViewModel.ParentNodeHeader = string.Format("Task Order: {0}\nType: {1}\nDescription: {2}"
						                 			                                               ,sender.SelectedTaskIndex
						                 			                                               ,Enum.GetName(typeof(TaskType_MifareClassicTask),sender.SelectedTaskType)
						                 			                                               ,sender.SelectedTaskDescription);
						                 			treeViewModel.TaskIndex = int.Parse(sender.SelectedTaskIndex);
						                 			TreeViewParentTaskNodes.TaskCollection.Add(sender);
						                 			
						                 			TreeViewParentTaskNodes.TaskCollection = new ObservableCollection<object>(
						                 				TreeViewParentTaskNodes.TaskCollection.OrderBy(x => (x as MifareClassicSetupViewModel).ParentNodeViewModel.TaskIndex)
						                 			);
						                 			
						                 			//foreach(TreeViewParentNodeViewModel vm in TreeViewParentTaskNodes.TaskCollection)
						                 			//	vm.IsSelected = false;
						                 			
						                 			RaisePropertyChanged("TreeViewParentTaskNodes");
						                 		}
						                 		sender.Close();
						                 	},

						                 	OnCancel = (sender) => {
						                 		sender.Close();
						                 	},

						                 	OnAuth = (sender) => {

						                 	},
						                 	
						                 	OnCloseRequest = (sender) => {
						                 		sender.Close();
						                 	}
						                 });
					}
					
				}
			}
			catch(Exception e)
			{
				
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
				TreeViewParentNodeViewModel treeViewModel;
				
				try{
					var item = TreeViewParentTaskNodes.TaskCollection.First(x => (x as MifareClassicSetupViewModel).ParentNodeViewModel.IsSelected);
					treeViewModel = (item as MifareClassicSetupViewModel).ParentNodeViewModel;
				}
				catch
				{
					treeViewModel = new TreeViewParentNodeViewModel(new MifareDesfireChipModel("no Task", CARD_TYPE.DESFire), dialogs, true);
					
				}
				
				if(device != null)
				{
					this.dialogs.Add(new MifareDesfireSetupViewModel(treeViewModel, dialogs) {
					                 	
					                 	Caption = String.Format("{0} UID:[{1}] Type:[{2}]",
					                 	                        resLoader.getResource("mifareAuthSettingsDialogCaption"),
					                 	                        "empty",
					                 	                        "none"),

					                 	OnOk = (sender) => {
					                 		
					                 		
					                 		if(sender.SelectedTaskType == TaskType_MifareDesfireTask.ChangeDefault)
					                 			sender.Settings.SaveSettings();
					                 		
					                 		if(sender.SelectedTaskType == TaskType_MifareDesfireTask.FormatDesfireCard)
					                 		{
					                 			TreeViewParentTaskNodes.TaskCollection.Remove(defaultTask);
					                 			
					                 			//TreeViewChildNodeViewModel taskChild = sender.ParentNodeViewModel.Children.First(x => x.IsTask == true);
					                 			
					                 			treeViewModel.Children.Clear();
					                 			//treeViewModel.Children.Add(taskChild);
					                 			treeViewModel.ParentNodeHeader = string.Format("Task Order: {0}\nType: {1}\nDescription: {2}"
					                 			                                               ,sender.SelectedTaskIndex
					                 			                                               ,Enum.GetName(typeof(TaskType_MifareDesfireTask),sender.SelectedTaskType)
					                 			                                               ,sender.SelectedTaskDescription);
					                 			treeViewModel.TaskIndex = int.Parse(sender.SelectedTaskIndex);
					                 			TreeViewParentTaskNodes.TaskCollection.Add(sender);
					                 			
					                 			TreeViewParentTaskNodes.TaskCollection = new ObservableCollection<object>(TreeViewParentTaskNodes.TaskCollection.OrderBy(x => (x as MifareDesfireSetupViewModel).ParentNodeViewModel.TaskIndex));
					                 			
					                 			foreach(MifareDesfireSetupViewModel vm in TreeViewParentTaskNodes.TaskCollection)
					                 				vm.ParentNodeViewModel.IsSelected = false;
					                 			
					                 			RaisePropertyChanged("TreeViewParentTaskNodes");
					                 			
					                 			sender.Close();
					                 		}
					                 	},

					                 	OnCancel = (sender) => {
					                 		sender.Close();
					                 	},
					                 	
					                 	OnCloseRequest = (sender) => {
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
			
			using (RFiDDevice device = new RFiDDevice(settings.DefaultSpecification.DefaultReaderProvider))
			{
				foreach(TreeViewParentNodeViewModel item in treeViewParentNodes)
				{
					item.IsExpanded = false;
				}
				
				device.ReadChipPublic();
				
				if (device != null &&
				    !string.IsNullOrWhiteSpace(device.CardInfo.uid) &&
				    !treeViewParentNodes.Where(x => x.UidNumber == device.CardInfo.uid).Any()) {

					// fill treeview with dummy models and viewmodels
					switch (device.CardInfo.cardType)
					{
						case CARD_TYPE.Mifare1K:
							treeViewParentNodes.Add(new TreeViewParentNodeViewModel(new MifareClassicChipModel(device.CardInfo.uid, CARD_TYPE.Mifare1K),  Dialogs));
							break;

						case CARD_TYPE.Mifare2K:
							treeViewParentNodes.Add(new TreeViewParentNodeViewModel(new MifareClassicChipModel(device.CardInfo.uid, CARD_TYPE.Mifare2K),  Dialogs));
							break;

						case CARD_TYPE.Mifare4K:
							treeViewParentNodes.Add(new TreeViewParentNodeViewModel(new MifareClassicChipModel(device.CardInfo.uid, CARD_TYPE.Mifare4K),  Dialogs));
							break;

						case CARD_TYPE.DESFire:
						case CARD_TYPE.DESFireEV1:
						case CARD_TYPE.DESFireEV2:
							treeViewParentNodes.Add(new TreeViewParentNodeViewModel(new MifareDesfireChipModel(device.CardInfo.uid, device.CardInfo.cardType),  Dialogs));
							break;
					}

					//fill the models with data from db
					foreach (TreeViewParentNodeViewModel pnVM in treeViewParentNodes) {
						//if (pnVM.mifareClassicUidModel != null)
						//databaseReaderWriter.WriteDatabase(pnVM.mifareClassicUidModel);
						//else
						//databaseReaderWriter.WriteDatabase(pnVM.mifareDesfireUidModel);
					}
				}
				
				else if (treeViewParentNodes.Where(x => x.UidNumber == device.CardInfo.uid).Any())
				{
					treeViewParentNodes.Where(x => x.UidNumber == device.CardInfo.uid).First().IsSelected = true;
				}
			}

			triggerReadChip.IsEnabled = timerState;
		}
		
		/// <summary>
		/// here we perform all tasks on cards with a periodic check for new cards to work with
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void UpdateChip(object sender, EventArgs e)
		{
			try{
				//try to get singleton instance
				using (RFiDDevice device = RFiDDevice.Instance)
				{
					//reader was ready - proceed
					if(device != null)
					{
						device.ReadChipPublic();
						
						//proceed to create dummy only when uid is yet unknown
						if (device != null &&
						    !string.IsNullOrWhiteSpace(device.CardInfo.uid) &&
						    !treeViewParentNodes.Where(x => x.UidNumber == device.CardInfo.uid).Any()) {

							foreach(TreeViewParentNodeViewModel item in treeViewParentNodes)
							{
								item.IsExpanded = false;
							}
							
							// fill treeview with dummy models and viewmodels
							switch (device.CardInfo.cardType)
							{
								case CARD_TYPE.Mifare1K:
									treeViewParentNodes.Add(new TreeViewParentNodeViewModel(new MifareClassicChipModel(device.CardInfo.uid, CARD_TYPE.Mifare1K),  Dialogs));
									break;

								case CARD_TYPE.Mifare2K:
									treeViewParentNodes.Add(new TreeViewParentNodeViewModel(new MifareClassicChipModel(device.CardInfo.uid, CARD_TYPE.Mifare2K),  Dialogs));
									break;

								case CARD_TYPE.Mifare4K:
									treeViewParentNodes.Add(new TreeViewParentNodeViewModel(new MifareClassicChipModel(device.CardInfo.uid, CARD_TYPE.Mifare4K),  Dialogs));
									break;

								case CARD_TYPE.DESFire:
								case CARD_TYPE.DESFireEV1:
								case CARD_TYPE.DESFireEV2:
									treeViewParentNodes.Add(new TreeViewParentNodeViewModel(new MifareDesfireChipModel(device.CardInfo.uid, device.CardInfo.cardType),  Dialogs));
									break;
							}

							//fill the models with data from db
							foreach (TreeViewParentNodeViewModel pnVM in treeViewParentNodes) {

							}
						}
						
						//only run if theres a card on the reader and its uid was previously added
						if (device != null &&
						    !string.IsNullOrWhiteSpace(device.CardInfo.uid) &&
						    treeViewParentNodes.Where(x => x.UidNumber == device.CardInfo.uid).Any())
						{
							//only run tasks if there is no child or grandchildnode selected for further edit
							if((treeViewParentNodes.Where(x => x.UidNumber == device.CardInfo.uid).First().Children.Any() && !(treeViewParentNodes.Where(x => x.UidNumber == device.CardInfo.uid).First().Children.Any(x => x.IsSelected))) &&
							   (!(treeViewParentNodes.Where(x => x.UidNumber == device.CardInfo.uid).First().Children.Where(x => x.Children != null && x.Children.Any(y => y.IsSelected)).Any())))
							{
								//select current parentnode (card) on reader
								treeViewParentNodes.Where(x => x.UidNumber == device.CardInfo.uid).First().IsSelected = true;
								
								//only run tasks when the card is yet untouched
								if(treeViewParentNodes.Where(x => x.UidNumber == device.CardInfo.uid).First().IsBeingProgrammed == null)
								{
									//are there tasks present to process?
									for(int i = 0; i < taskHandler.TaskCollection.Count; i++)
									{
										//decide what type of card to process
										if(taskHandler.GetTaskType == typeof(MifareClassicSetupViewModel))
										{
											switch( (taskHandler.TaskCollection[i] as MifareClassicSetupViewModel).SelectedTaskType)
											{
												case TaskType_MifareClassicTask.ChangeSecuritySettings:
													break;
											}

										}
										if(taskHandler.GetTaskType == typeof(MifareDesfireSetupViewModel))
										{
											switch( (taskHandler.TaskCollection[i] as MifareDesfireSetupViewModel).SelectedTaskType)
											{
												case TaskType_MifareDesfireTask.FormatDesfireCard:
													
													treeViewParentNodes.Where(x => x.IsSelected).First().IsBeingProgrammed = true;
													
													(taskHandler.TaskCollection[i] as MifareDesfireSetupViewModel).FormatDesfireCardCommand.Execute(device);
													
													if((taskHandler.TaskCollection[i] as MifareDesfireSetupViewModel).IsTaskCompletedSuccessfully == true)
														treeViewParentNodes.Where(x => x.IsSelected).First().IsBeingProgrammed = null;
													else
													{
														treeViewParentNodes.Where(x => x.IsSelected).First().IsBeingProgrammed = false;
														(taskHandler.TaskCollection[i] as MifareDesfireSetupViewModel).IsTaskCompletedSuccessfully = false;
													}
													
													break;
											}
										}
									}
								}
								
							}
							
							
							
						}
						
						else
						{
							if(treeViewParentNodes.Where(x => x.IsSelected).Any())
								treeViewParentNodes.Where(x => x.IsSelected).First().IsSelected = false;
						}
						
						RaisePropertyChanged("TreeViewParentNodes");
					}
				}
			}
			catch(Exception ex)
			{
				
			}
		}
		
		public ICommand WriteToAllChipAutoCommand { get { return new RelayCommand(OnNewWriteToAllChipAutoCommand); } }
		private void OnNewWriteToAllChipAutoCommand()
		{
			if(!triggerReadChip.IsEnabled)
				triggerReadChip.IsEnabled = true;
			else
				triggerReadChip.IsEnabled = false;
		}
		
		public ICommand WriteToChipOnceCommand { get { return new RelayCommand(OnNewWriteToChipOnceCommand); } }
		private void OnNewWriteToChipOnceCommand()
		{
//			foreach(object chipTask in taskHandler.TaskCollection)
//			{
//				if (chipTask is MifareClassicSetupViewModel)
//					(chipTask as MifareClassicSetupViewModel).IsTaskCompletedSuccessfully = null;
//				else if(chipTask is MifareDesfireSetupViewModel)
//					(chipTask as MifareDesfireSetupViewModel).IsTaskCompletedSuccessfully = null;
//			}
			
			Task thread = new Task(() =>
			                       {
			                       	
			                       	CARD_INFO card;
			                       	
			                       	try{
			                       		//try to get singleton instance
			                       		using (RFiDDevice device = RFiDDevice.Instance)
			                       		{
			                       			//reader was ready - proceed
			                       			if(device != null)
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
			                       			treeViewParentNodes.Where(x => x.UidNumber == card.uid).Any())
			                       		{

			                       			//select current parentnode (card) on reader
			                       			treeViewParentNodes.Where(x => x.UidNumber == card.uid).First().IsSelected = true;
			                       			treeViewParentNodes.Where(x => x.IsSelected).First().IsBeingProgrammed = true;
			                       			
			                       			//are there tasks present to process?
			                       			while(taskIndex < taskHandler.TaskCollection.Count)
			                       			{
			                       				
			                       				Thread.Sleep(100);
			                       				
			                       				taskTimeout.IsEnabled = true;
			                       				taskTimeout.Start();
			                       				
			                       				//decide what type of card to process
			                       				if(taskHandler.GetTaskType == typeof(MifareClassicSetupViewModel))
			                       				{
			                       					switch( (taskHandler.TaskCollection[taskIndex] as MifareClassicSetupViewModel).SelectedTaskType)
			                       					{
			                       						case TaskType_MifareClassicTask.ChangeSecuritySettings:
			                       							break;
			                       					}
			                       				}
			                       				if(taskHandler.GetTaskType == typeof(MifareDesfireSetupViewModel))
			                       				{
			                       					
			                       					switch( (taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).SelectedTaskType)
			                       					{
			                       						case TaskType_MifareDesfireTask.FormatDesfireCard:
			                       							
			                       							switch((taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).taskErr)
			                       							{
			                       								case ERROR.AuthenticationError:
			                       									(taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).IsTaskCompletedSuccessfully = false;
			                       									break;
			                       								case ERROR.DeviceNotReadyError:
			                       									break;
			                       								case ERROR.IOError:
			                       									break;
			                       								case ERROR.NoError:
			                       									(taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).IsTaskCompletedSuccessfully = true;
			                       									taskIndex++;
			                       									taskTimeout.IsEnabled = false;
			                       									taskTimeout.Stop();
			                       									break;
			                       								case ERROR.Empty:
			                       									(taskHandler.TaskCollection[taskIndex] as MifareDesfireSetupViewModel).FormatDesfireCardCommand.Execute(null);
			                       									break;
			                       							}
			                       							
			                       							break;
			                       					}
			                       				}
			                       			}
			                       			
			                       			
			                       			
			                       			
			                       			
			                       			
			                       		}
			                       		
			                       		else
			                       		{
			                       			if(treeViewParentNodes.Where(x => x.IsSelected).Any())
			                       				treeViewParentNodes.Where(x => x.IsSelected).First().IsSelected = false;
			                       		}
			                       		
			                       		RaisePropertyChanged("TreeViewParentNodes");
			                       		
			                       		
			                       	}
			                       	catch(Exception ex)
			                       	{
			                       		
			                       	}
			                       });
			
			thread.ContinueWith((x) => {
			                    	treeViewParentNodes.Where(y => y.IsSelected).First().IsBeingProgrammed = null;
			                    });
			thread.Start();

		}
		
		private void TaskTimeout(object sender, EventArgs e)
		{
			taskIndex = int.MaxValue;
		}
		
		public ICommand CloseAllCommand { get { return new RelayCommand(OnCloseAll); } }
		private void OnCloseAll()
		{
			this.Dialogs.Clear();
		}
		
		public ICommand SwitchLanguageToGerman { get { return new RelayCommand(SetGermanLanguage); } }
		private void SetGermanLanguage()
		{
			if (settings.DefaultSpecification.DefaultLanguage != "german") {
				settings.DefaultSpecification.DefaultLanguage = "german";
				this.OnNewLanguageChangedDialog();
			}

		}
		
		public ICommand SwitchLanguageToEnglish { get { return new RelayCommand(SetEnglishLanguage); } }
		private void SetEnglishLanguage()
		{
			if (settings.DefaultSpecification.DefaultLanguage != "english") {
				settings.DefaultSpecification.DefaultLanguage = "english";
				this.OnNewLanguageChangedDialog();
			}

		}
		
		public void OnNewLanguageChangedDialog()
		{
			this.Dialogs.Add(new CustomDialogViewModel {
			                 	Message = resLoader.getResource("messageBoxRestartRequiredMessage"),
			                 	Caption = resLoader.getResource("messageBoxRestartRequiredCaption"),

			                 	OnOk = (sender) => {
			                 		sender.Close();
			                 		App.Current.Shutdown();
			                 	},

			                 	OnCancel = (sender) => {
			                 		sender.Close();

			                 	},

			                 	OnCloseRequest = (sender) => {
			                 		sender.Close();
			                 	}
			                 });
		}
		
		public ICommand NewReaderSetupDialogCommand { get { return new RelayCommand(OnNewReaderSetupDialog); } }
		private void OnNewReaderSetupDialog()
		{
			using (RFiDDevice device = new RFiDDevice(settings.DefaultSpecification.DefaultReaderProvider))
			{
				this.Dialogs.Add(new SetupViewModel(settings, device) {
				                 	Caption = "RFiDGear Reader Setup",
				                 	
				                 	OnOk = (sender) => {
				                 		
				                 		settings.DefaultSpecification.DefaultReaderProvider = sender.SelectedReader;
				                 		//currentReaderSetup = new RFiDReaderSetup(sender.SelectedReader);
				                 		settings.SaveSettings();
				                 		sender.Close();
				                 		
				                 	},

				                 	OnCancel = (sender) => {
				                 		sender.Close();
				                 	},

				                 	OnCloseRequest = (sender) => {
				                 		sender.Close();
				                 	}
				                 });
			}

		}
		
		/*
		public ICommand NewModalDialogCommand { get { return new RelayCommand(OnNewModalDialog); } }
		public void OnNewModalDialog()
		{
			this.Dialogs.Add(new CustomDialogViewModel {
			                 	Message = "Hello World!",
			                 	Caption = "Modal Dialog Box",

			                 	OnOk = (sender) => {
			                 		sender.Close();
			                 		new MessageBoxViewModel("You pressed ok!", "Bye bye!").Show(this.Dialogs);
			                 	},

			                 	OnCancel = (sender) => {
			                 		sender.Close();
			                 		new MessageBoxViewModel("You pressed cancel!", "Bye bye!").Show(this.Dialogs);
			                 	},

			                 	OnCloseRequest = (sender) => {
			                 		sender.Close();
			                 		new MessageBoxViewModel("You clicked the caption bar close button!", "Bye bye!").Show(this.Dialogs);
			                 	}
			                 });
		}

		 */

		/*
		public ICommand NewModelessDialogCommand { get { return new RelayCommand(OnNewModelessDialog); } }
		public void OnNewModelessDialog()
		{
			var confirmClose = new Action<CustomDialogViewModel>((sender) => {
			                                                     	if (new MessageBoxViewModel {
			                                                     	    	Caption = "Closing",
			                                                     	    	Message = "Are you sure you want to close this window?",
			                                                     	    	Buttons = MessageBoxButton.YesNo,
			                                                     	    	Image = MessageBoxImage.Question
			                                                     	    }
			                                                     	    .Show(this.Dialogs) == MessageBoxResult.Yes)
			                                                     		sender.Close();
			                                                     });

			new CustomDialogViewModel(false) {
				Message = "Hello World!",
				Caption = "Modeless Dialog Box",
				OnOk = confirmClose,
				OnCancel = confirmClose,
				OnCloseRequest = confirmClose
			}.Show(this.Dialogs);
		}

		 */

		/*
		public ICommand NewMessageBoxCommand { get { return new RelayCommand(OnNewMessageBox); } }
		public void OnNewMessageBox()
		{
			new MessageBoxViewModel {
				Caption = "Message Box",
				Message = "This is a message box!",
				Image = MessageBoxImage.Information
			}.Show(this.Dialogs);
		}
		 */

		public ICommand NewOpenFileDialogCommand { get { return new RelayCommand(OnNewOpenFileDialog); } }
		public void OnNewOpenFileDialog()
		{
			var dlg = new OpenFileDialogViewModel {
				Title = "Select a file (I won't actually do anything with it)",
				Filter = "All files (*.*)|*.*",
				Multiselect = false
			};
			
			if (dlg.Show(this.Dialogs) && dlg.FileName != null)
			{
				if(TreeViewParentTaskNodes.TaskCollection != null && TreeViewParentTaskNodes.TaskCollection.Count > 0)
					TreeViewParentTaskNodes.TaskCollection.Clear();
				
				databaseReaderWriter.ReadDatabase(dlg.FileName);
				
				foreach(TreeViewParentNodeViewModel vm in databaseReaderWriter.treeViewModel)
				{
					TreeViewParentNodes.Add(vm);
				}
				
				foreach(object setup in databaseReaderWriter.setupModel.TaskCollection)
				{
					TreeViewParentTaskNodes.TaskCollection.Add(setup);
				}
			}
			
			else
				new MessageBoxViewModel { Message = "You didn't select a file." }.Show(this.Dialogs);
			
			RaisePropertyChanged("TreeViewParentTaskNodes");
		}

		/// <summary>
		/// Expose Command to Save As Menu Item
		/// </summary>
		public ICommand SaveTaskDialogCommand { get { return new RelayCommand(OnNewSaveTaskDialogCommand); } }
		private void OnNewSaveTaskDialogCommand()
		{
			var dlg = new SaveFileDialogViewModel {
				Title = "Select a file (I won't actually do anything with it)",
				Filter = "All files (*.xml)|*.xml"
			};
			
			if (dlg.Show(this.Dialogs) && dlg.FileName != null)
			{
				databaseReaderWriter.WriteDatabase(TreeViewParentTaskNodes, dlg.FileName);
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
		
		public ICommand CloseApplication { get { return new RelayCommand(OnCloseRequest); } }
		public void OnCloseRequest()
		{
			App.Current.Shutdown();
		}
		
		#endregion
		
		#region View Switchers
		
		public bool IsSelected { get; set; }
		
		private bool isCheckedForUpdatesChecked;
		public bool IsCheckForUpdatesChecked {
			get { return isCheckedForUpdatesChecked; }
			set {
				if (value) {
					updater.StartMonitoring();
					isCheckedForUpdatesChecked = value;
					RaisePropertyChanged("IsCheckForUpdatesChecked");
				}
				else
					updater.StopMonitoring();
			}
		}
		
		public bool RadioButtonGermanLanguageSelectedState {
			get {
				if (settings.DefaultSpecification.DefaultLanguage == "german")
					return true;
				else
					return false;
			}
			set {
				if (settings.DefaultSpecification.DefaultLanguage == "english")
					value = false;
				RaisePropertyChanged("RadioButtonGermanLanguageSelectedState");
			}
		}
		
		public bool RadioButtonEnglishLanguageSelectedState {
			get {
				if (settings.DefaultSpecification.DefaultLanguage == "german")
					return false;
				else
					return true;
			}
			set {
				if (settings.DefaultSpecification.DefaultLanguage == "german")
					value = false;
				RaisePropertyChanged("RadioButtonEnglishLanguageSelectedState");
			}
		}
		
		#endregion
		

		
	}
}
