using RFiDGear;
using RFiDGear.DataAccessLayer;
using RFiDGear.Model;

using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;

using MvvmDialogs.ViewModels;

using RedCell.Diagnostics.Update;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Input;
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

		private List<MifareClassicUidTreeViewModel> mifareClassicUidModels = new List<MifareClassicUidTreeViewModel>();
		private List<MifareDesfireUidTreeViewModel> mifareDesfireViewModels = new List<MifareDesfireUidTreeViewModel>();
		
		#region Constructors
		
		public MainWindowViewModel()
		{
			treeViewParentNodes = new ObservableCollection<TreeViewParentNodeViewModel>();
			
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
		
		#endregion
		
		#region MifareDesFIRE Communication
		
		private void ReadAppIDs(TreeViewParentNodeViewModel selectedPNVM, string content) {
			using (RFiDDevice device = new RFiDDevice(settings.DefaultSpecification.DefaultReaderProvider))
			{
				foreach(uint appID in device.GetAppIDList){
					selectedPNVM.Children.Add(
						new TreeViewChildNodeViewModel(
							new MifareDesfireAppIdTreeViewModel(
								String.Format("{0:d2}",appID)),selectedPNVM, device.CardInfo.cardType));
				}
			}
			
		}
		
		private void EraseDesfireCard(TreeViewParentNodeViewModel selectedPNVM, string content) {
			using (RFiDDevice device = new RFiDDevice(settings.DefaultSpecification.DefaultReaderProvider))
			{
				if(!device.EraseDesfireCard)
				{
					selectedPNVM.Children.Clear();
					foreach(uint appID in device.GetAppIDList){
						selectedPNVM.Children.Add(
							new TreeViewChildNodeViewModel(
								new MifareDesfireAppIdTreeViewModel(
									String.Format("{0:d2}",appID)),selectedPNVM, device.CardInfo.cardType));
					}
				}
			}
			
		}
		
		private void CreateApp(TreeViewParentNodeViewModel selectedPNVM, string content) {
			
			this.Dialogs.Add(new MifareDesfireSetupViewModel(selectedPNVM) {
			                 	
			                 	
			                 	OnOk = (sender) => {
			                 		
			                 	},

			                 	OnCancel = (sender) => {
			                 		
			                 		sender.Close();

			                 	},
			                 	
			                 	OnAuth = (sender) => {
//			                 		foreach (TreeViewChildNodeViewModel cnVM in selectedPNVM.Children) {
//			                 			readerModel.ReadMiFareClassicSingleSector(cnVM.SectorNumber, cnVM.SectorNumber);
//			                 			cnVM.IsAuthenticated = rfidDevice.SectorSuccesfullyAuth;
//			                 			foreach (TreeViewGrandChildNodeViewModel gcVM in cnVM.Children) {
//			                 				gcVM.IsAuthenticated = rfidDevice.DataBlockSuccesfullyAuth[(((cnVM.SectorNumber + 1) * cnVM.BlockCount) - (cnVM.BlockCount - gcVM.DataBlockNumber))];
//			                 				gcVM.DataBlockContent = rfidDevice.currentSector[gcVM.DataBlockNumber];
//			                 			}
//			                 		}
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
				Mouse.OverrideCursor = Cursors.Arrow;
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
				
				this.Dialogs.Add(new MifareClassicSetupViewModel(uidVM) {
				                 	
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
		public void OnNewCreateClassicTaskCommand()
		{
			using (RFiDDevice device = new RFiDDevice(settings.DefaultSpecification.DefaultReaderProvider))
			{
				TreeViewParentNodeViewModel treeViewModel = new TreeViewParentNodeViewModel(new MifareClassicUidTreeViewModel(device.CardInfo.cardType.ToString(),device.CardInfo.cardType),dialogs);
				
				this.dialogs.Add(new MifareClassicSetupViewModel(treeViewModel) {
				                 	
				                 	Caption = String.Format("{0} UID:[{1}] Type:[{2}]",
				                 	                        resLoader.getResource("mifareAuthSettingsDialogCaption"),
				                 	                        "empty",
				                 	                        "none"),
				                 	IsClassicAuthInfoEnabled = true, //content.Contains("EditAccessBits"),

				                 	OnOk = (sender) => {
				                 		
				                 		if(sender.SelectedAccessBitsTaskType == Task_Type.ChangeDefault)
				                 			sender.Settings.SaveSettings();
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

		public ICommand CreateDesfireTaskCommand { get { return new RelayCommand(OnNewCreateDesfireTaskCommand); } }
		public void OnNewCreateDesfireTaskCommand()
		{
			using (RFiDDevice device = new RFiDDevice(settings.DefaultSpecification.DefaultReaderProvider))
			{
				TreeViewParentNodeViewModel treeViewModel = new TreeViewParentNodeViewModel(new MifareClassicUidTreeViewModel(device.CardInfo.cardType.ToString(),device.CardInfo.cardType),dialogs);
				
				this.dialogs.Add(new MifareDesfireSetupViewModel(treeViewModel) {
				                 	
				                 	Caption = String.Format("{0} UID:[{1}] Type:[{2}]",
				                 	                        resLoader.getResource("mifareAuthSettingsDialogCaption"),
				                 	                        "empty",
				                 	                        "none"),
				                 	//IsClassicAuthInfoEnabled = true, //content.Contains("EditAccessBits"),

				                 	OnOk = (sender) => {
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
		
		public ICommand ReadChipCommand { get { return new RelayCommand(OnNewReadChipCommand); } }
		public void OnNewReadChipCommand()
		{
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
							treeViewParentNodes.Add(new TreeViewParentNodeViewModel(new MifareClassicUidTreeViewModel(device.CardInfo.uid, CARD_TYPE.Mifare1K),  Dialogs));
							break;

						case CARD_TYPE.Mifare2K:
							treeViewParentNodes.Add(new TreeViewParentNodeViewModel(new MifareClassicUidTreeViewModel(device.CardInfo.uid, CARD_TYPE.Mifare2K),  Dialogs));
							break;

						case CARD_TYPE.Mifare4K:
							treeViewParentNodes.Add(new TreeViewParentNodeViewModel(new MifareClassicUidTreeViewModel(device.CardInfo.uid, CARD_TYPE.Mifare4K),  Dialogs));
							break;

						case CARD_TYPE.DESFire:
						case CARD_TYPE.DESFireEV1:
						case CARD_TYPE.DESFireEV2:
							treeViewParentNodes.Add(new TreeViewParentNodeViewModel(new MifareDesfireUidTreeViewModel(device.CardInfo.uid, device.CardInfo.cardType),  Dialogs));
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
			}

		}
		
		public ICommand WriteChipCommand { get { return new RelayCommand(OnWriteChipCommand); } }
		public void OnWriteChipCommand()
		{
			
		}
		
		
		public ICommand CloseAllCommand { get { return new RelayCommand(OnCloseAll); } }
		public void OnCloseAll()
		{
			this.Dialogs.Clear();
		}
		
		public ICommand SwitchLanguageToGerman { get { return new RelayCommand(SetGermanLanguage); } }
		public void SetGermanLanguage()
		{
			if (settings.DefaultSpecification.DefaultLanguage != "german") {
				settings.DefaultSpecification.DefaultLanguage = "german";
				this.OnNewLanguageChangedDialog();
			}

		}
		
		public ICommand SwitchLanguageToEnglish { get { return new RelayCommand(SetEnglishLanguage); } }
		public void SetEnglishLanguage()
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
		public void OnNewReaderSetupDialog()
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
			
			if (dlg.Show(this.Dialogs))
				new MessageBoxViewModel { Message = "You selected the following file: " + dlg.FileName + "." }.Show(this.Dialogs);
			else
				new MessageBoxViewModel { Message = "You didn't select a file." }.Show(this.Dialogs);
		}

		/// <summary>
		/// Expose Command to Save As Menu Item
		/// </summary>
		public ICommand SaveToProjectFileDialogCommand { get { return new RelayCommand(OnNewSaveToProjectFileCommand); } }
		private void OnNewSaveToProjectFileCommand()
		{
			var dlg = new SaveFileDialogViewModel {
				Title = "Select a file (I won't actually do anything with it)",
				Filter = "All files (*.xml)|*.xml",
			};
			
			if (dlg.Show(this.Dialogs) && dlg.FileName != null)
			{
				databaseReaderWriter.WriteDatabase(treeViewParentNodes, dlg.FileName);
			}
		}
		
		/// <summary>
		/// Expose Command to Save Menu Item
		/// </summary>
		public ICommand SaveToDatabaseCommand { get { return new RelayCommand(OnSaveToDatabaseCommand); } }
		private void OnSaveToDatabaseCommand()
		{
			databaseReaderWriter.WriteDatabase(treeViewParentNodes);
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
