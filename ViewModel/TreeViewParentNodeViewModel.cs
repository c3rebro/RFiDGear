using RFiDGear.Model;
using RFiDGear.DataAccessLayer;

using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;

using MvvmDialogs.ViewModels;

using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;

using System.Xml.Serialization;

namespace RFiDGear.ViewModel
{
	/// <summary>
	/// Description of MifareClassic1KContentViewModel.
	/// </summary>
	[XmlRootAttribute("TreeViewParentNode", IsNullable = false)]
	public class TreeViewParentNodeViewModel : ViewModelBase
	{
		
		private SettingsReaderWriter settings;
		
		private static object _selectedItem;
		
		private ObservableCollection<IDialogViewModel> dialogs;
		
		//private readonly CARD_TYPE _CardType;
		private readonly List<MenuItem> ContextMenuItems;
		private readonly RelayCommand _cmdReadAllSectorsWithDefaultKeys;
		private readonly RelayCommand _cmdDeleteThisNode;
		private readonly RelayCommand _cmdEditDefaultKeys;
		
		
		private readonly RelayCommand _cmdCreateApp;
		private readonly RelayCommand _cmdEraseDesfireCard;
		private readonly string[] _constCardType = { "Mifare1K", "Mifare2K", "Mifare4K", "DESFireEV1" };
		
		private MifareClassicChipModel mifareClassicUidModel { get; set; }
		private MifareDesfireChipModel mifareDesfireUidModel { get; set; }
		
		#region Constructors

		public TreeViewParentNodeViewModel()
		{
			ID = new Random().Next();
			
			mifareClassicUidModel = new MifareClassicChipModel();
			mifareDesfireUidModel = new MifareDesfireChipModel();
			
//			if(mifareClassicUidModel != null )
//				ParentNodeHeader = String.Format("[{0} Type: {1}]", mifareClassicUidModel.UidNumber, Enum.GetName(typeof(CARD_TYPE),CardType));
//			else
//				ParentNodeHeader = String.Format("[{0} Type: {1}]", mifareDesfireUidModel.uidNumber, Enum.GetName(typeof(CARD_TYPE),CardType));
		}
		
		public TreeViewParentNodeViewModel(MifareClassicChipModel _uidModel, ObservableCollection<IDialogViewModel> _dialogs, bool _isTask = false)
		{
			ID = new Random().Next();
			
			if(_dialogs != null)
				dialogs = _dialogs;
			
			isTask = _isTask;
			settings = new SettingsReaderWriter();
			mifareClassicUidModel = _uidModel;
			CardType = mifareClassicUidModel.CardType;
			
			_cmdReadAllSectorsWithDefaultKeys = new RelayCommand(ReadSectorsWithDefaultConfig);
			_cmdDeleteThisNode = new RelayCommand(DeleteMeCommand);
			_cmdEditDefaultKeys = new RelayCommand(EditDefaultKeys);
			
			ContextMenuItems = new List<MenuItem>();
			ContextMenuItems.Add(new MenuItem() {
			                     	Header = "QuickCheck",
			                     	Command = _cmdReadAllSectorsWithDefaultKeys,
			                     	ToolTip= new ToolTip() {
			                     		Content="Try to get read access to all DataBlocks on the Card\n" +
			                     			"Place the Result in a Database including SectorTrailer (Block 3)\n" +
			                     			"Use a csv file to acuire default keys for authentication"
			                     	}
			                     });
			
			ContextMenuItems.Add(new MenuItem() {
			                     	Header = "Delete Node",
			                     	Command = _cmdDeleteThisNode
			                     });
			
			_children = new ObservableCollection<TreeViewChildNodeViewModel>();
			
			if(!isTask)
				LoadChildren();
			
			IsSelected = true;
			
			if(mifareClassicUidModel != null)
				ParentNodeHeader = String.Format("TaskType: {0}\nChipType: {1}", mifareClassicUidModel.UidNumber, Enum.GetName(typeof(CARD_TYPE),CardType));
			else
				ParentNodeHeader = String.Format("TaskType: {0}\nChipType: {1}", mifareDesfireUidModel.uidNumber, Enum.GetName(typeof(CARD_TYPE),CardType));
		}
		
		public TreeViewParentNodeViewModel(MifareDesfireChipModel _uidModel, ObservableCollection<IDialogViewModel> _dialogs, bool _isTask = false)
		{
			ID = new Random().Next();
			
			if(_dialogs != null)
				dialogs = _dialogs;
			
			isTask = _isTask;
			settings = new SettingsReaderWriter();
			
			mifareDesfireUidModel = _uidModel;
			CardType = mifareDesfireUidModel.CardType;
			
			RelayCommand _cmdEditDefaultKeys = new RelayCommand(EditDefaultKeys);
			RelayCommand _cmdReadAppIds = new RelayCommand(ReadAppIDs);
			_cmdCreateApp = new RelayCommand(CreateApp);
			_cmdEraseDesfireCard = new RelayCommand(EraseDesfireCard);
			
			ContextMenuItems = new List<MenuItem>();

			ContextMenuItems.Add(new MenuItem() {
			                     	Header = "QuickCheck",
			                     	Command = _cmdReadAppIds,
			                     	ToolTip = new ToolTip() {
			                     		Content="Try to get all Application IDs on the Card"
			                     	}});
			


			_children = new ObservableCollection<TreeViewChildNodeViewModel>();
			
			if(!isTask)
				LoadChildren();
			

			
			IsSelected = true;
			
			if(mifareDesfireUidModel != null)
				ParentNodeHeader = String.Format("TaskType: {0}\nChipType: {1}", mifareDesfireUidModel.uidNumber, Enum.GetName(typeof(CARD_TYPE),CardType));
		}

		#endregion
		
		#region Context Menu Items
		
		/// <summary>
		/// 
		/// </summary>
		[XmlIgnore]
		public List<MenuItem> ContextMenu {
			get { return ContextMenuItems; }
		}

		
		private void ReadSectorsWithDefaultConfig() {
			if(!isTask)
			{
				using ( RFiDDevice device = new RFiDDevice(settings.DefaultSpecification.DefaultReaderProvider))
				{
					//private void ReadSectorsWithDefaultConfig(TreeViewParentNodeViewModel selectedPnVM, string content)
					
					
					Mouse.OverrideCursor = Cursors.Wait;
					
					//TreeViewParentNodeViewModel vm = this;

					foreach (TreeViewChildNodeViewModel cnVM in this.Children) {
						
						foreach(string keys in settings.DefaultSpecification.MifareClassicDefaultQuickCheckKeys) {
							//TODO Try all Keys and add the result somewhere in the treeview
							//rfidDevice.ReadMiFareClassicSingleSector(cnVM.SectorNumber, keys, keys);
							device.ReadMiFareClassicSingleSector(cnVM.SectorNumber, keys, keys);
							
							if(device.SectorSuccesfullyAuth) {
								cnVM.Children.Add(new TreeViewGrandChildNodeViewModel(string.Format("Key: {0}",keys)));
								break;
							}
						}
						cnVM.IsAuthenticated = device.SectorSuccesfullyAuth;
						foreach (TreeViewGrandChildNodeViewModel gcVM in cnVM.Children.Where(x => x.IsDataBlock)) {
							if(gcVM.DataBlockContent != null) {
								if(cnVM.SectorNumber <= 31)
									gcVM.IsAuthenticated = device.DataBlockSuccesfullyAuth[(((cnVM.SectorNumber + 1) * cnVM.BlockCount) - (cnVM.BlockCount - gcVM.DataBlockNumber))];
								else
									gcVM.IsAuthenticated = device.DataBlockSuccesfullyAuth[((128 + (cnVM.SectorNumber - 31) * cnVM.BlockCount) - (cnVM.BlockCount - gcVM.DataBlockNumber))];
								
								gcVM.DataBlockContent = device.currentSector[gcVM.DataBlockNumber];
							}
						}
					}
					
					this.IsExpanded = true;
					
					Mouse.OverrideCursor = null;
				}
			}
			
		}

		private void EditDefaultKeys() {

		}
		
		private void ReadAppIDs() {
			if(!isTask)
			{
				using ( RFiDDevice device = RFiDDevice.Instance)
				{
					//private void ReadSectorsWithDefaultConfig(TreeViewParentNodeViewModel selectedPnVM, string content)
					
					if(device != null)
					{
						Mouse.OverrideCursor = Cursors.Wait;
						
						//TreeViewParentNodeViewModel vm = this;
						//"ca be 09 11 20 16 ca 22 01 19 90 be 01 09 19 92 " roche alt
						//"00 11 22 33 44 55 66 77 88 99 AA BB CC DD EE FF" test
						//ff ff ff ff ff ff ff ff ff ff ff ff ff ff ff ff
						//00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
						//var success = device.FormatDesFireCard("ca be 09 11 20 16 ca 22 01 19 90 be 01 09 19 92",LibLogicalAccess.DESFireKeyType.DF_KEY_AES);
						
						var appIDs = device.GetAppIDList;
						
						Children.Clear();
						
						Children.Add(
							new TreeViewChildNodeViewModel(
								string.Format("Available Space: {0}Byte",device.FreeMemory)));
						
						Children.Add(
							new TreeViewChildNodeViewModel(
								new MifareDesfireAppIdTreeViewModel(0),this, CardType, dialogs));
						
						foreach(uint appID in appIDs)
						{
							Children.Add(new TreeViewChildNodeViewModel(new MifareDesfireAppIdTreeViewModel(appID), this, device.CardInfo.cardType, dialogs));
						}
						
						if(device.AuthToMifareDesfireApplication("00000000000000000000000000000000",LibLogicalAccess.DESFireKeyType.DF_KEY_AES, MifareDesfireKeyNumber.MifareDesfireKey00) == ERROR.NoError)
						{
							Children.First(x => x.AppID == 0).Children.Add(new TreeViewGrandChildNodeViewModel("PICC MasterKey NOT Set"));
						}
						
						IsExpanded = true;
						
						Mouse.OverrideCursor = null;
						
					}
					
				}
			}
		}
		
		private void EraseDesfireCard() {
			if(!isTask)
			{
				using ( RFiDDevice device = new RFiDDevice(settings.DefaultSpecification.DefaultReaderProvider))
				{
					//private void ReadSectorsWithDefaultConfig(TreeViewParentNodeViewModel selectedPnVM, string content)
					
					
					Mouse.OverrideCursor = Cursors.Wait;
					
					//TreeViewParentNodeViewModel vm = this;
					//"cabe09112016ca22011990be01091992 " roche alt
					//"00 11 22 33 44 55 66 77 88 99 AA BB CC DD EE FF" test
					//ff ff ff ff ff ff ff ff ff ff ff ff ff ff ff ff
					//00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
					//var success = device.FormatDesFireCard("ca be 09 11 20 16 ca 22 01 19 90 be 01 09 19 92",LibLogicalAccess.DESFireKeyType.DF_KEY_AES);
					
					this.IsExpanded = true;
					
					Mouse.OverrideCursor = null;
				}
			}
		}
		
		private void CreateApp() {

		}
		
		private void DeleteMeCommand() {

		}
		
		#endregion
		
		#region Items Sources
		
		/// <summary>
		/// 
		/// </summary>
		public int ID { get; set; }
		
		/// <summary>
		/// 
		/// </summary>
		public ObservableCollection<TreeViewChildNodeViewModel> Children
		{
			get { return _children; }
			set {
				_children = value;
				RaisePropertyChanged("Children");
			}
		} private ObservableCollection<TreeViewChildNodeViewModel> _children;
		
		#endregion
		
		#region Selected Items
		[XmlIgnore]
		public object SelectedItem
		{
			get { return _selectedItem; }
			set
			{
				if (_selectedItem != value)
				{
					_selectedItem = value;
					RaisePropertyChanged("SelectedItem");
				}
			}
		}

		#endregion
		
		#region Properties
		/// <summary>
		/// 
		/// </summary>
		public string ParentNodeHeader {
			get { return parentNodeHeader; }
			set {
				parentNodeHeader = value;
				RaisePropertyChanged("ParentNodeHeader");
			}
		} private string parentNodeHeader;
		
		/// <summary>
		/// 
		/// </summary>
		public int TaskIndex { get; set; }
		
		/// <summary>
		/// 
		/// </summary>
		public string UidNumber {
			get { if(mifareClassicUidModel != null)
					return mifareClassicUidModel.UidNumber;
				else
					return mifareDesfireUidModel.uidNumber;
			}
		}
		
		/// <summary>
		/// 
		/// </summary>
		
		public CARD_TYPE CardType {
			get { return mifareClassicUidModel != null ? mifareClassicUidModel.CardType : (mifareDesfireUidModel != null ? mifareDesfireUidModel.CardType : CARD_TYPE.Unspecified); }
			set {
				if(mifareClassicUidModel != null)
					mifareClassicUidModel.CardType = value;
				else if(mifareDesfireUidModel != null)
					mifareDesfireUidModel.CardType = value;
				RaisePropertyChanged("CardType");
			}
		}
		
		
		#endregion
		
		#region View Switches
		
		/// <summary>
		/// 
		/// </summary>
		public bool IsExpanded
		{
			get { return _isExpanded; }
			set
			{
				_isExpanded = value;
				RaisePropertyChanged("IsExpanded");
			}
		} private bool _isExpanded;

		/// <summary>
		/// 
		/// </summary>
		public bool IsSelected
		{
			get { return _isSelected; }
			set
			{
				_isSelected = value;
				RaisePropertyChanged("IsSelected");
			}
		} private bool _isSelected;
		
		/// <summary>
		/// 
		/// </summary>
		public bool IsTask
		{
			get { return isTask; }
			set { isTask = value; }
		} private bool isTask;
		
		/// <summary>
		/// 
		/// </summary>
		public bool? IsProgrammingCompletedSuccessfully
		{
			get { return isProgrammingCompletedSuccessfully; }
			set {
				isProgrammingCompletedSuccessfully = value;
				RaisePropertyChanged("IsProgrammingCompletedSuccessfully");
			}
		} private bool? isProgrammingCompletedSuccessfully;
		
		/// <summary>
		/// 
		/// </summary>
		public bool? IsBeingProgrammed
		{
			get { return isBeingProgrammed; }
			set {
				isBeingProgrammed = value;
				RaisePropertyChanged("IsBeingProgrammed");
			}
		} private bool? isBeingProgrammed;
		
		#endregion
		
		private void LoadChildren()
		{

			switch (CardType) {
				case CARD_TYPE.Mifare1K:
					{
						for (int i = 0; i < 16; i++) {
							_children.Add(
								new TreeViewChildNodeViewModel(
									new MifareClassicSectorModel(i), this, CardType, i, dialogs));
						}

					}
					break;
					
				case CARD_TYPE.Mifare2K:
					{
						for (int i = 0; i < 32; i++) {
							_children.Add(
								new TreeViewChildNodeViewModel(
									new MifareClassicSectorModel(i), this, CardType, i, dialogs));
						}

					}
					break;
					
				case CARD_TYPE.Mifare4K:
					{
						for (int i = 0; i < 40; i++) {
							_children.Add(
								new TreeViewChildNodeViewModel(
									new MifareClassicSectorModel(i), this, CardType, i, dialogs));
						}

					}
					break;
					
				case CARD_TYPE.DESFire:
				case CARD_TYPE.DESFireEV1:
				case CARD_TYPE.DESFireEV2:
					{
						_children.Add(
							new TreeViewChildNodeViewModel(
								new MifareDesfireAppIdTreeViewModel(0),this, CardType, dialogs));
					}
					break;
			}
			
			//foreach(TreeViewChildNodeViewModel item in _children){
			//	if(mifareClassicUidModel != null)
			//		mifareClassicUidModel.SectorList.Add(item.sectorModel);
			//}
		}
	}
}
