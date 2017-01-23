using RFiDGear.DataSource;
using RFiDGear.Model;

using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Messaging;

using MvvmDialogs.ViewModels;

using RedCell.Diagnostics.Update;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Linq;

namespace RFiDGear.ViewModel
{
	/// <summary>
	/// Description of MainWindowViewModel.
	/// </summary>
	public class MainWindowViewModel : ViewModelBase
	{
		private ReaderSetupModel readerSetupModel;
		private RFiDDevice rfidDevice;
		private Updater updater = new Updater();
		private DatabaseReaderWriter databaseReaderWriter;

		private ObservableCollection<MifareClassicDataBlockDataGridModel> DataSourceForMifareClassicDataBlock = new ObservableCollection<MifareClassicDataBlockDataGridModel>();

		private List<MifareClassicUidTreeViewModel> mifareClassicUidModels = new List<MifareClassicUidTreeViewModel>();
		private List<MifareDesfireUidTreeViewModel> mifareDesfireViewModels = new List<MifareDesfireUidTreeViewModel>();
		
		#region Constructors
		
		public MainWindowViewModel()
		{
			readerSetupModel = new ReaderSetupModel("PCSC");
			rfidDevice = new RFiDDevice("PCSC");
			databaseReaderWriter = new DatabaseReaderWriter();

			Messenger.Default.Register<NotificationMessage<string>>(
				this, nm => {
					// Processing the Message
					switch (nm.Content) {
							
						case "TreeViewParentNodes":
							switch (nm.Notification) {
								case "ReadAllSectors":
									ReadSectorsWithDefaultConfig((TreeViewParentNodeViewModel)nm.Sender, nm.Content);
									break;
								case "DeleteMe":
									databaseReaderWriter.databaseUIDs.Remove(nm.Content);
									treeViewParentNodes.Remove((TreeViewParentNodeViewModel)nm.Sender);
									break;
								case "EditDefaultKeys":
									NewSectorTrailerEditDialog((TreeViewParentNodeViewModel)nm.Sender, nm.Notification);
									break;
									
									// Desfire Massaging goes here
								case "ReadAppIDs":
									ReadAppIDs((TreeViewParentNodeViewModel)nm.Sender, nm.Notification);
									break;
								case "CreateAppID":
									CreateApp((TreeViewParentNodeViewModel)nm.Sender, nm.Notification);
									break;
							}
							break;
							
						case "TreeViewChildNode":
							switch (nm.Notification) {
								case "EditAuthAndModifySector":
									NewSectorTrailerEditDialog((TreeViewChildNodeViewModel)nm.Sender, nm.Notification);
									break;
								case "EditAccessBits":
									NewSectorTrailerEditDialog((TreeViewChildNodeViewModel)nm.Sender, nm.Notification);
									break;
								case "ReadSectorWithDefaults":
									ReadSectorsWithDefaultConfig((TreeViewChildNodeViewModel)nm.Sender, nm.Notification);
									break;
							}
							break;
					}
				});
			
			//Messenger.Default.Register<TreeViewChildNodeViewModel>(this, NewSectorTrailerEditDialog);
			Messenger.Default.Register<TreeViewGrandChildNodeViewModel>(this, ShowDataBlockInDataGrid);
			//any dialog boxes added in the constructor won't appear until DialogBehavior.DialogViewModels gets bound to the Dialogs collection.
		}
		
		#endregion
		
		#region Dialogs
		
		private ObservableCollection<IDialogViewModel> dialogs = new ObservableCollection<IDialogViewModel>();
		public ObservableCollection<IDialogViewModel> Dialogs { get { return dialogs; } }
		
		#endregion
		
		#region Selected Items
		
		private MifareClassicDataBlockDataGridModel selectedDataGridItem;
		public MifareClassicDataBlockDataGridModel SelectedDataGridItem {
			get { return selectedDataGridItem; }
			set { selectedDataGridItem = value; RaisePropertyChanged("SelectedDataGridItem"); }
		}
		
		#endregion
		
		#region Items Sources
		
		private ObservableCollection<MifareClassicDataBlockDataGridModel> dataGridSource;
		public ObservableCollection<MifareClassicDataBlockDataGridModel> DataGridSource {
			get { return dataGridSource; }
		}
		


		private ObservableCollection<TreeViewParentNodeViewModel> treeViewParentNodes = new ObservableCollection<TreeViewParentNodeViewModel>();
		public ObservableCollection<TreeViewParentNodeViewModel> TreeViewParentNodes {
			get {
				CustomConverter converter = new CustomConverter();
				if (!String.IsNullOrEmpty(readerSetupModel.GetChipUID)) {
					
					//add chip to database if it is a new uid

					databaseReaderWriter.ReadDatabase();
					
					if ((!databaseReaderWriter.databaseUIDs.Contains(readerSetupModel.GetChipUID)))
						databaseReaderWriter.databaseUIDs.Add(readerSetupModel.GetChipUID);
					
					// fill treeview with dummy models and viewmodels
					switch (readerSetupModel.GetChipType) {
						case "Mifare1K":
							treeViewParentNodes.Add(new TreeViewParentNodeViewModel(new MifareClassicUidTreeViewModel(readerSetupModel.GetChipUID, CARD_TYPE.CT_CLASSIC_1K), CARD_TYPE.CT_CLASSIC_1K));
							break;
							
						case "Mifare2K":
							treeViewParentNodes.Add(new TreeViewParentNodeViewModel(new MifareClassicUidTreeViewModel(readerSetupModel.GetChipUID, CARD_TYPE.CT_CLASSIC_2K), CARD_TYPE.CT_CLASSIC_2K));
							break;
							
						case "Mifare4K":
							treeViewParentNodes.Add(new TreeViewParentNodeViewModel(new MifareClassicUidTreeViewModel(readerSetupModel.GetChipUID, CARD_TYPE.CT_CLASSIC_4K), CARD_TYPE.CT_CLASSIC_4K));
							break;
							
						case "DESFireEV1":
							treeViewParentNodes.Add(new TreeViewParentNodeViewModel(new MifareDesfireUidTreeViewModel(readerSetupModel.GetChipUID), CARD_TYPE.CT_DESFIRE_EV1));
							break;
					}
					
					//fill the models with data from db
					foreach (TreeViewParentNodeViewModel pnVM in treeViewParentNodes) {
						if (pnVM.mifareClassicUidModel != null)
							databaseReaderWriter.WriteDatabase(pnVM.mifareClassicUidModel);
						//else
						//databaseReaderWriter.WriteDatabase(pnVM.mifareDesfireUidModel);
					}
					
					return treeViewParentNodes;
				}
				return null;
			}
		}
		
		#endregion
		
		#region MifareDesFIRE Communication
		
		private void ReadAppIDs(TreeViewParentNodeViewModel selectedPNVM, string content) {
			foreach(uint appID in rfidDevice.GetAppIDList){
				selectedPNVM.Children.Add(new TreeViewChildNodeViewModel(new MifareDesfireAppIdTreeViewModel(String.Format("{0:d2}",appID)),selectedPNVM, CARD_TYPE.CT_DESFIRE_EV1));
			}

		}
		
		private void CreateApp(TreeViewParentNodeViewModel selectedPNVM, string content) {
			
		}
		#endregion
		
		#region MifareClassic Communication

		private void ReadSectorsWithDefaultConfig(TreeViewParentNodeViewModel selectedPnVM, string content)
		{
			
			Mouse.OverrideCursor = Cursors.Wait;
			
			TreeViewParentNodeViewModel vm = selectedPnVM;
			
			foreach (TreeViewParentNodeViewModel treeViewPnVM in treeViewParentNodes) {
				treeViewPnVM.IsExpanded = false;
				if (treeViewPnVM.UidNumber == selectedPnVM.UidNumber) {
					selectedPnVM.IsExpanded = true;
					foreach (TreeViewChildNodeViewModel cnVM in treeViewPnVM.Children) {
						rfidDevice.ReadMiFareClassicSingleSector(cnVM.SectorNumber, cnVM.SectorNumber);
						cnVM.IsAuthenticated = rfidDevice.SectorSuccesfullyAuth;
						foreach (TreeViewGrandChildNodeViewModel gcVM in cnVM.Children) {
							gcVM.IsAuthenticated = rfidDevice.DataBlockSuccesfullyAuth[(((cnVM.SectorNumber + 1) * cnVM.BlockCount) - (cnVM.BlockCount - gcVM.DataBlockNumber))];
							gcVM.DataBlockContent = rfidDevice.currentSector[gcVM.DataBlockNumber];
						}
					}
				}
			}
			
			Mouse.OverrideCursor = Cursors.Arrow;
		}
		
		private void ReadSectorsWithDefaultConfig(TreeViewChildNodeViewModel selectedCnVM, string content)
		{
			
			Mouse.OverrideCursor = Cursors.Wait;
			
			TreeViewChildNodeViewModel vm = selectedCnVM;

			rfidDevice.ReadMiFareClassicSingleSector(selectedCnVM.SectorNumber, selectedCnVM.SectorNumber);
			selectedCnVM.IsAuthenticated = rfidDevice.SectorSuccesfullyAuth;
			foreach (TreeViewGrandChildNodeViewModel gcVM in selectedCnVM.Children) {
				gcVM.IsAuthenticated = rfidDevice.DataBlockSuccesfullyAuth[(((selectedCnVM.SectorNumber + 1) * selectedCnVM.BlockCount) - (selectedCnVM.BlockCount - gcVM.DataBlockNumber))];
				gcVM.DataBlockContent = rfidDevice.currentSector[gcVM.DataBlockNumber];
			}
			
			Mouse.OverrideCursor = Cursors.Arrow;
		}
		
		private void ShowDataBlockInDataGrid(TreeViewGrandChildNodeViewModel selectedGCN)
		{
			DataSourceForMifareClassicDataBlock.Clear();
			for (int i = 0; i < selectedGCN.DataBlockContent.Length; i++) {
				DataSourceForMifareClassicDataBlock.Add(new MifareClassicDataBlockDataGridModel(selectedGCN.DataBlockContent, i));
			}
			dataGridSource = new ObservableCollection<MifareClassicDataBlockDataGridModel>(DataSourceForMifareClassicDataBlock);
			RaisePropertyChanged("DataGridSource");
		}
		
		public void NewSectorTrailerEditDialog(TreeViewParentNodeViewModel uidVM, string content)
		{
			bool isClassicCard;
			
			if (uidVM.CardType == CARD_TYPE.CT_CLASSIC_1K || uidVM.CardType == CARD_TYPE.CT_CLASSIC_2K || uidVM.CardType == CARD_TYPE.CT_CLASSIC_4K)
				isClassicCard = true;
			else
				isClassicCard = false;
			
			this.Dialogs.Add(new MifareAuthSettingsDialogViewModel(uidVM) {
			                 	
			                 	Caption = String.Format("{0}{1}{2}", ResourceLoader.getResource("mifareAuthSettingsDialogCaption"), uidVM.CardType, uidVM.UidNumber),
			                 	IsClassicAuthInfoEnabled = isClassicCard,
			                 	IsAccessBitsEditTabEnabled = false, //content.Contains("EditAccessBits"),
			                 	
			                 	OnOk = (sender) => {
			                 		databaseReaderWriter.WriteDatabase((sender.ViewModelContext as TreeViewChildNodeViewModel)._sectorModel);
			                 	},

			                 	OnCancel = (sender) => {
			                 		
			                 		sender.Close();

			                 	},
			                 	
			                 	OnAuth = (sender) => {
			                 		foreach (TreeViewChildNodeViewModel cnVM in uidVM.Children) {
			                 			rfidDevice.ReadMiFareClassicSingleSector(cnVM.SectorNumber, cnVM.SectorNumber);
			                 			cnVM.IsAuthenticated = rfidDevice.SectorSuccesfullyAuth;
			                 			foreach (TreeViewGrandChildNodeViewModel gcVM in cnVM.Children) {
			                 				gcVM.IsAuthenticated = rfidDevice.DataBlockSuccesfullyAuth[(((cnVM.SectorNumber + 1) * cnVM.BlockCount) - (cnVM.BlockCount - gcVM.DataBlockNumber))];
			                 				gcVM.DataBlockContent = rfidDevice.currentSector[gcVM.DataBlockNumber];
			                 			}
			                 		}
			                 	},

			                 	OnCloseRequest = (sender) => {
			                 		sender.Close();
			                 	}
			                 });
		}
		
		public void NewSectorTrailerEditDialog(TreeViewChildNodeViewModel sectorVM, string content)
		{
			bool isClassicCard;
			
			if (sectorVM.Parent.CardType == CARD_TYPE.CT_CLASSIC_1K || sectorVM.Parent.CardType == CARD_TYPE.CT_CLASSIC_2K || sectorVM.Parent.CardType == CARD_TYPE.CT_CLASSIC_4K)
				isClassicCard = true;
			else
				isClassicCard = false;
			
			this.Dialogs.Add(new MifareAuthSettingsDialogViewModel(sectorVM) {
			                 	
			                 	Caption = String.Format("{0} UID:[{1}] Type:[{2}]", ResourceLoader.getResource("mifareAuthSettingsDialogCaption"), sectorVM.Parent.UidNumber, sectorVM.Parent.CardType),
			                 	ViewModelContext = sectorVM,
			                 	IsClassicAuthInfoEnabled = isClassicCard,
			                 	IsAccessBitsEditTabEnabled = content.Contains("EditAccessBits"),

			                 	OnOk = (sender) => {
			                 		databaseReaderWriter.WriteDatabase((sender.ViewModelContext as TreeViewChildNodeViewModel)._sectorModel);
			                 		Messenger.Default.Send<NotificationMessage<string>>(
			                 			new NotificationMessage<string>(this, "TreeViewChildNodeHasChanged", "ReadSectorWithDefaults"));
			                 		},

			                 	OnCancel = (sender) => {
			                 		sender.Close();
			                 	},

			                 	OnAuth = (sender) => {

			                 		rfidDevice.ReadMiFareClassicSingleSector(sectorVM.SectorNumber, sender.selectedClassicKeyAKey, sender.selectedClassicKeyBKey);
			                 		sectorVM.IsAuthenticated = rfidDevice.SectorSuccesfullyAuth;
			                 		foreach (TreeViewGrandChildNodeViewModel gcVM in sectorVM.Children) {
			                 			gcVM.IsAuthenticated = rfidDevice.DataBlockSuccesfullyAuth[(((sectorVM.SectorNumber + 1) * sectorVM.BlockCount) - (sectorVM.BlockCount - gcVM.DataBlockNumber))];
			                 			gcVM.DataBlockContent = rfidDevice.currentSector[gcVM.DataBlockNumber];
			                 		}

			                 	},
			                 	
			                 	OnCloseRequest = (sender) => {
			                 		sender.Close();
			                 	}
			                 });
		}
		
		#endregion
		
		#region Localization
		
		public string MenuItem_FileHeader {
			get { return ResourceLoader.getResource("menuItemFileHeader"); }
		}
		
		public string MenuItem_ExitHeader {
			get { return ResourceLoader.getResource("menuItemExitHeader"); }
		}
		
		public string MenuItem_EditHeader {
			get { return ResourceLoader.getResource("menuItemEditHeader"); }
		}
		
		public string MenuItem_OptionsHeader {
			get { return ResourceLoader.getResource("menuItemOptionsHeader"); }
		}
		
		public string MenuItem_ReaderSettingsHeader {
			get { return ResourceLoader.getResource("menuItemReaderSettingsHeader"); }
		}
		#endregion
		
		#region Menu Commands
		public ICommand ReadChipCommand { get { return new RelayCommand(OnNewReadChipCommand); } }
		public void OnNewReadChipCommand()
		{
			if (!String.IsNullOrEmpty(new ReaderSetupModel(new SettingsReaderWriter()._defaultReaderProvider).GetChipUID)) {
				if (readerSetupModel.SelectedReader != "N/A") {
					if (!databaseReaderWriter.databaseUIDs.Contains(readerSetupModel.GetChipUID) || (treeViewParentNodes.Count == 0)) {
						//databaseReaderWriter.databaseUIDs.Add(readerSetupModel.GetChipUID);
						RaisePropertyChanged("TreeViewParentNodes");
					}
				}
			} else {
				if (readerSetupModel.SelectedReader != "N/A") {
					if (!databaseReaderWriter.databaseUIDs.Contains(readerSetupModel.GetChipUID)) {
						databaseReaderWriter.databaseUIDs.Add(readerSetupModel.GetChipUID);
						RaisePropertyChanged("TreeViewParentNodes");
					}
				}
			}
		}
		
		public ICommand CloseAllCommand { get { return new RelayCommand(OnCloseAll); } }
		public void OnCloseAll()
		{
			this.Dialogs.Clear();
		}
		
		public ICommand SwitchLanguageToGerman { get { return new RelayCommand(SetGermanLanguage); } }
		public void SetGermanLanguage()
		{
			if (ResourceLoader.getLanguage() != "german") {
				ResourceLoader.setLanguage("german");
				this.OnNewLanguageChangedDialog();
			}

		}
		
		public ICommand SwitchLanguageToEnglish { get { return new RelayCommand(SetEnglishLanguage); } }
		public void SetEnglishLanguage()
		{
			if (ResourceLoader.getLanguage() != "english") {
				ResourceLoader.setLanguage("english");
				this.OnNewLanguageChangedDialog();
			}

		}
		
		public void OnNewLanguageChangedDialog()
		{
			this.Dialogs.Add(new CustomDialogViewModel {
			                 	Message = ResourceLoader.getResource("messageBoxRestartRequiredMessage"),
			                 	Caption = ResourceLoader.getResource("messageBoxRestartRequiredCaption"),

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
		public void OnNewReaderSetupDialog()
		{
			this.Dialogs.Add(new ReaderSetupDialogViewModel {
			                 	Caption = "RFiDGear Reader Setup",
			                 	
			                 	OnOk = (sender) => {
			                 		
			                 		//currentReaderSetup = new RFiDReaderSetup(sender.SelectedReader);
			                 		
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
			
			if (dlg.Show(this.Dialogs))
				new MessageBoxViewModel { Message = "You selected the following file: " + dlg.FileName + "." }.Show(this.Dialogs);
			else
				new MessageBoxViewModel { Message = "You didn't select a file." }.Show(this.Dialogs);
		}

		public ICommand CloseApplication { get { return new RelayCommand(OnCloseRequest); } }
		public void OnCloseRequest()
		{
			App.Current.Shutdown();
		}
		
		#endregion
		
		#region View Switchers
		
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
				if (ResourceLoader.getLanguage() == "german")
					return true;
				else
					return false;
			}
			set {
				if (ResourceLoader.getLanguage() == "english")
					value = false;
				RaisePropertyChanged("RadioButtonGermanLanguageSelectedState");
			}
		}
		
		public bool RadioButtonEnglishLanguageSelectedState {
			get {
				if (ResourceLoader.getLanguage() == "german")
					return false;
				else
					return true;
			}
			set {
				if (ResourceLoader.getLanguage() == "german")
					value = false;
				RaisePropertyChanged("RadioButtonEnglishLanguageSelectedState");
			}
		}
		
		#endregion
	}
}
