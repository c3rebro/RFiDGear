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
using System.Reflection;
using System.Xml.Serialization;

namespace RFiDGear.ViewModel
{
	/// <summary>
	/// Description of MifareClassic1KContentViewModel.
	/// </summary>
	[XmlRootAttribute("TreeViewParentNode", IsNullable = false)]
	public class TreeViewParentNodeViewModel : ViewModelBase
	{
		
		private Version Version = Assembly.GetExecutingAssembly().GetName().Version;
		
		private SettingsReaderWriter settings;
		
		private static object _selectedItem;
		
		private ObservableCollection<IDialogViewModel> dialogs;
		
		private readonly CARD_TYPE _cardType;
		private readonly List<MenuItem> ContextMenuItems;
		private readonly RelayCommand _cmdReadAllSectorsWithDefaultKeys;
		private readonly RelayCommand _cmdDeleteThisNode;
		private readonly RelayCommand _cmdEditDefaultKeys;
		
		
		private readonly RelayCommand _cmdCreateApp;
		private readonly RelayCommand _cmdEraseDesfireCard;
		private readonly string[] _constCardType = { "Mifare1K", "Mifare2K", "Mifare4K", "DESFireEV1" };
		
		private MifareClassicUidTreeViewModel mifareClassicUidModel { get; set; }
		private MifareDesfireUidTreeViewModel mifareDesfireUidModel { get; set; }
		
		#region Constructors

		public TreeViewParentNodeViewModel()
		{

		}
		
		public TreeViewParentNodeViewModel(MifareClassicUidTreeViewModel uidModel, ObservableCollection<IDialogViewModel> _dialogs)
		{
			if(_dialogs != null)
				dialogs = _dialogs;
			
			settings = new SettingsReaderWriter();
			mifareClassicUidModel = uidModel;
			_cardType = mifareClassicUidModel.CardType;
			
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
			
			LoadChildren();
		}
		
		public TreeViewParentNodeViewModel(Model.MifareDesfireUidTreeViewModel uid, ObservableCollection<IDialogViewModel> _dialogs)
		{
			if(_dialogs != null)
				dialogs = _dialogs;
			
			settings = new SettingsReaderWriter();
			
			mifareDesfireUidModel = uid;
			_cardType = mifareDesfireUidModel.CardType;
			
			//device = _device;
			
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
			
			_children.Add(
				new TreeViewChildNodeViewModel(
					new MifareDesfireAppIdTreeViewModel("[0] (Master App)"),this, _cardType));
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
					foreach (TreeViewGrandChildNodeViewModel gcVM in cnVM.Children) {
						if(gcVM._dataBlock != null) {
							if(cnVM.SectorNumber <= 31)
								gcVM.IsAuthenticated = device.DataBlockSuccesfullyAuth[(((cnVM.SectorNumber + 1) * cnVM.BlockCount) - (cnVM.BlockCount - gcVM.DataBlockNumber))];
							else
								gcVM.IsAuthenticated = device.DataBlockSuccesfullyAuth[((128 + (cnVM.SectorNumber - 31) * cnVM.BlockCount) - (cnVM.BlockCount - gcVM.DataBlockNumber))];
							
							gcVM.DataBlockContent = device.currentSector[gcVM.DataBlockNumber];
						}
					}
				}
				
				this.IsExpanded = true;
				
				Mouse.OverrideCursor = Cursors.Arrow;
			}			
		}

		private void EditDefaultKeys() {

		}
		
		private void ReadAppIDs() {

		}
		
		private void EraseDesfireCard() {

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
		public string ManifestVersion
		{
			get {
				return string.Format("{0}.{1}.{2}",Version.Major,Version.Minor,Version.Build);
			}
		}
		
		/// <summary>
		/// 
		/// </summary>
		public ObservableCollection<TreeViewChildNodeViewModel> Children
		{
			get { return _children; }
			set { _children = value; }
		} private ObservableCollection<TreeViewChildNodeViewModel> _children;
		
		#endregion
		
		#region Selected Items
		
		public object SelectedItem
		{
			get { return _selectedItem; }
			set
			{
				if (_selectedItem != value)
				{
					_selectedItem = value;
					OnPropertyChanged("SelectedItem");
				}
			}
		}

		#endregion
		
		#region Properties
		/// <summary>
		/// 
		/// </summary>
		[XmlIgnore]
		public string ParentNodeDisplayItem {
			get { if(mifareClassicUidModel != null)
					return String.Format("[{0} Type: {1}]", mifareClassicUidModel.UidNumber, Enum.GetName(typeof(CARD_TYPE),_cardType));
				else
					return String.Format("[{0} Type: {1}]", mifareDesfireUidModel.uidNumber, Enum.GetName(typeof(CARD_TYPE),_cardType));
			}
		}
		
		/// <summary>
		/// 
		/// </summary>
		[XmlIgnore]
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
		[XmlIgnore]
		public CARD_TYPE CardType {
			get { return _cardType; }
		}
		
		#endregion
		
		#region View Switches
		
		private bool _isExpanded;
		public bool IsExpanded
		{
			get { return _isExpanded; }
			set
			{
				_isExpanded = value;
				RaisePropertyChanged("IsExpanded");
			}
		}

		private bool _isSelected;
		public bool IsSelected
		{
			get { return _isSelected; }
			set
			{
				if (value != _isSelected)
				{
					_isSelected = value;
					OnPropertyChanged("IsSelected");
					
					SelectedItem = this;
				}
			}
		}
		
		#endregion

		#region INotifyPropertyChanged Members

		public event PropertyChangedEventHandler PropertyChanged;

		protected virtual void OnPropertyChanged(string propertyName)
		{
			if (this.PropertyChanged != null)
				this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
		}

		#endregion
		
		private void LoadChildren()
		{

			switch (_cardType) {
				case CARD_TYPE.Mifare1K:
					{
						for (int i = 0; i < 16; i++) {
							_children.Add(
								new TreeViewChildNodeViewModel(
									new MifareClassicSectorTreeViewModel(i), this, _cardType, i, dialogs));
						}

					}
					break;
					
				case CARD_TYPE.Mifare2K:
					{
						for (int i = 0; i < 32; i++) {
							_children.Add(
								new TreeViewChildNodeViewModel(
									new MifareClassicSectorTreeViewModel(i), this, _cardType, i, dialogs));
						}

					}
					break;
					
				case CARD_TYPE.Mifare4K:
					{
						for (int i = 0; i < 40; i++) {
							_children.Add(
								new TreeViewChildNodeViewModel(
									new MifareClassicSectorTreeViewModel(i), this, _cardType, i, dialogs));
						}

					}
					break;
					
				case CARD_TYPE.DESFire:
				case CARD_TYPE.DESFireEV1:
				case CARD_TYPE.DESFireEV2:
					{
						_children.Add(
							new TreeViewChildNodeViewModel(
								new MifareDesfireAppIdTreeViewModel("00001"),this, _cardType, dialogs));
					}
					break;
			}
			
			foreach(TreeViewChildNodeViewModel item in _children){
				if(mifareClassicUidModel != null)
					mifareClassicUidModel.SectorList.Add(item.sectorModel);
			}
		}
	}
}
