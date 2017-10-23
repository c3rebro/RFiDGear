using RFiDGear.Model;
using RFiDGear.DataAccessLayer;

using MvvmDialogs.ViewModels;

using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;

using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Input;
using System.Xml.Serialization;

namespace RFiDGear.ViewModel
{
	/// <summary>
	/// Description of TreeViewChildNodeViewModel.
	/// </summary>
	[XmlRootAttribute("TreeViewChildNode", IsNullable = false)]
	public class TreeViewChildNodeViewModel : ViewModelBase, IUserDialogViewModel
	{
		private readonly RFiDDevice device;
		private readonly ResourceLoader resLoader = new ResourceLoader();
		private readonly TreeViewParentNodeViewModel _parent;
		private readonly MifareDesfireAppIdTreeViewModel _appID;
		private readonly CARD_TYPE _cardType;
		private readonly RelayCommand _cmdReadSectorWithCustoms;
		private readonly RelayCommand _cmdReadSectorWithDefaults;
		private readonly RelayCommand _cmdEditAuthAndModifySector;
		private readonly string _parentUid;
		
		public MifareClassicSectorTreeViewModel sectorModel { get; set; }
		
		#region Constructors
		
		public TreeViewChildNodeViewModel()
		{
			
		}
		
		public TreeViewChildNodeViewModel(MifareClassicSectorTreeViewModel _sectorModel, TreeViewParentNodeViewModel parent, CARD_TYPE cardType, int _sectorNumber, ObservableCollection<IDialogViewModel> _dialogs = null)
		{
			if(_dialogs != null)
				dialogs = _dialogs;
			
			//device = _device;
			sectorModel = _sectorModel;
			sectorModel.mifareClassicSectorNumber = _sectorNumber;
			_cardType = cardType;
			_parent = parent;
			
			_cmdReadSectorWithDefaults = new RelayCommand(ReadSectorWithDefaults);
			_cmdEditAuthAndModifySector = new RelayCommand(ReadSectorWithCustoms);
			_cmdReadSectorWithCustoms = new RelayCommand(ReadSectorWithCustoms);
			
			ContextMenuItems = new List<MenuItem>();
			ContextMenuItems.Add(new MenuItem() {
			                     	Header = "Read Sector with default Keys",
			                     	Command = _cmdReadSectorWithDefaults
			                     });

			ContextMenuItems.Add(new MenuItem() {
			                     	Header = "Read Sector with custom Keys",
			                     	Command = _cmdReadSectorWithCustoms
			                     });
			
			_children = new ObservableCollection<TreeViewGrandChildNodeViewModel>();
			
			LoadChildren();
		}

		public TreeViewChildNodeViewModel(MifareDesfireAppIdTreeViewModel appID, TreeViewParentNodeViewModel parentUID, CARD_TYPE cardType, ObservableCollection<IDialogViewModel> _dialogs = null)
		{
			if(_dialogs != null)
				dialogs = _dialogs;
			
			//device = _device;
			_appID = appID;
			_cardType = cardType;
			_parentUid = parentUID.UidNumber;
			
			_cmdReadSectorWithDefaults = new RelayCommand(ReadSectorWithDefaults);
			_cmdEditAuthAndModifySector = new RelayCommand(ReadSectorWithCustoms);
			
			ContextMenuItems = new List<MenuItem>();
			ContextMenuItems.Add(new MenuItem() {
			                     	Header = "Read Sector using default Configuration",
			                     	Command = _cmdReadSectorWithDefaults
			                     });
			
			ContextMenuItems.Add(new MenuItem() {
			                     	Header = "Edit Authentication Settings and Modify Sector",
			                     	Command = _cmdEditAuthAndModifySector
			                     });
			
			_children = new ObservableCollection<TreeViewGrandChildNodeViewModel>();
			
			LoadChildren();
		}
		
		#endregion
		
		
		#region Dialogs
		
		private ObservableCollection<IDialogViewModel> dialogs;
		
		#endregion
		
		#region Context Menu Items

		/// <summary>
		/// 
		/// </summary>
		[XmlIgnore]
		public List<MenuItem> ContextMenu {
			get { return ContextMenuItems; }
		} private readonly List<MenuItem> ContextMenuItems;
		
		public void ReadSectorWithDefaults()
		{
			
		}
		
		public void ReadSectorWithCustoms() {
			IsSelected = true;
			
			bool isClassicCard;
			
			if (this.Parent.CardType == CARD_TYPE.Mifare1K ||
			    this.Parent.CardType == CARD_TYPE.Mifare2K ||
			    this.Parent.CardType == CARD_TYPE.Mifare4K)
				isClassicCard = true;
			else
				isClassicCard = false;
			
			this.dialogs.Add(new MifareClassicSetupViewModel(this) {
			                 	
			                 	Caption = String.Format("{0} UID:[{1}] Type:[{2}]",
			                 	                        resLoader.getResource("mifareAuthSettingsDialogCaption"),
			                 	                        this.Parent.UidNumber,
			                 	                        this.Parent.CardType),
			                 	ViewModelContext = this,
			                 	IsClassicAuthInfoEnabled = true, //content.Contains("EditAccessBits"),

			                 	OnOk = (sender) => {
			                 		//databaseReaderWriter.WriteDatabase((sender.ViewModelContext as TreeViewChildNodeViewModel)._sectorModel);
			                 	},

			                 	OnCancel = (sender) => {
			                 		sender.Close();
			                 	},

			                 	OnAuth = (sender) => {

			                 		//readerModel.ReadMiFareClassicSingleSector(sectorVM.SectorNumber, sender.selectedClassicKeyAKey, sender.selectedClassicKeyBKey);
			                 		this.IsAuthenticated = device.SectorSuccesfullyAuth;
			                 		foreach (TreeViewGrandChildNodeViewModel gcVM in this.Children) {
			                 			gcVM.IsAuthenticated = device.DataBlockSuccesfullyAuth[
			                 				(((this.SectorNumber + 1) * this.BlockCount) - (this.BlockCount - gcVM.DataBlockNumber))];
			                 			gcVM.DataBlockContent = device.currentSector[gcVM.DataBlockNumber];
			                 		}

			                 	},
			                 	
			                 	OnCloseRequest = (sender) => {
			                 		sender.Close();
			                 	}
			                 });
		}
		
		public void EditAccessBits() {
			IsSelected = true;
		}
		
		#endregion
		
		#region Commands
		
		
		#endregion
		
		#region Selected Items

		private object _selectedItem;
		public object SelectedItem
		{
			get { return _selectedItem; }
			set
			{
				if (_selectedItem != value)
				{
					_selectedItem = value;
				}
			}
		}
		
		#endregion
		
		#region Items Sources
		
		private readonly ObservableCollection<TreeViewGrandChildNodeViewModel> _children;
		public ObservableCollection<TreeViewGrandChildNodeViewModel> Children
		{
			get { return _children; }
			set { Children = value; OnPropertyChanged("Children"); }
		}
		
		#endregion

		#region Parent

		public TreeViewParentNodeViewModel Parent
		{
			get { return _parent; }
		}

		#endregion

		#region View Switchers

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
		
		private bool _isExpanded;
		public bool IsExpanded
		{
			get { return _isExpanded; }
			set
			{
				if (value != _isExpanded)
				{
					_isExpanded = value;
					this.OnPropertyChanged("IsExpanded");
				}

				// Expand all the way up to the root.
				if (_isExpanded && _parent != null)
					_parent.IsExpanded = true;
			}
		}
		
		private bool? hasChanged;
		public bool? HasChanged{
			get { return hasChanged; }
			set { hasChanged = value; OnPropertyChanged("HasChanged"); }
		}

		private bool? isAuth;
		public bool? IsAuthenticated {
			get {return isAuth;}
			set { isAuth = value; OnPropertyChanged("IsAuthenticated");}
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
		
		#region Properties

		public int SectorNumber {
			get { return sectorModel.mifareClassicSectorNumber; }
			set { sectorModel.mifareClassicSectorNumber = value; }
		}
		
		public int BlockCount {
			get { return sectorModel.mifareClassicSectorNumber > 31 ? 16 : 4; }
		}

		public string ParentUid {
			get { return _parentUid; }
		}
		
		public string ChildNodeDisplayItem {
			get {
				if(_cardType == CARD_TYPE.Mifare1K || _cardType == CARD_TYPE.Mifare2K || _cardType == CARD_TYPE.Mifare4K){
					if(hasChanged == true)
						return String.Format("*Sector: [{0}]", sectorModel.mifareClassicSectorNumber);
					else
						return String.Format("Sector: [{0}]", sectorModel.mifareClassicSectorNumber);
				}

				else{
					if(hasChanged == true)
						return String.Format("*AppID: {0}", _appID.appID);
					else
						return String.Format("AppID: {0}", _appID.appID);
				}
			}
		}

		#endregion
		
		private void LoadChildren()
		{
			switch (_cardType) {
				case CARD_TYPE.Mifare1K:
					{
						for (int i = 0; i < 4; i++) {
							_children.Add(new TreeViewGrandChildNodeViewModel(new MifareClassicDataBlockTreeViewModel(i), this, _cardType, sectorModel.mifareClassicSectorNumber));
						}
					}
					break;
					
				case CARD_TYPE.Mifare2K:
					{
						for (int i = 0; i < 4; i++) {
							_children.Add(new TreeViewGrandChildNodeViewModel(new MifareClassicDataBlockTreeViewModel(i), this, _cardType, sectorModel.mifareClassicSectorNumber));
						}
					}
					break;
					
				case CARD_TYPE.Mifare4K:
					{
						if (SectorNumber < 32) {
							for (int i = 0; i < 4; i++) {
								_children.Add(new TreeViewGrandChildNodeViewModel(new MifareClassicDataBlockTreeViewModel(i), this, _cardType, sectorModel.mifareClassicSectorNumber));
							}
						} else {
							
							for (int i = 0; i < 16; i++) {
								_children.Add(new TreeViewGrandChildNodeViewModel(new MifareClassicDataBlockTreeViewModel(i), this, _cardType, sectorModel.mifareClassicSectorNumber));
							}
						}
					}
					break;
			}
			foreach(TreeViewGrandChildNodeViewModel item in _children){
				sectorModel.DataBlock.Add(item._dataBlock);
			}
		}
		
		
		#region IUserDialogViewModel Implementation

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
		
		/// <summary>
		/// 
		/// </summary>
		public ICommand CancelCommand { get { return new RelayCommand(Cancel); } }
		protected virtual void Cancel()
		{
			if (this.OnCancel != null)
				this.OnCancel(this);
			else
				Close();
		}
		
		/// <summary>
		/// 
		/// </summary>
		public Action<TreeViewChildNodeViewModel> OnOk { get; set; }
		
		/// <summary>
		/// 
		/// </summary>
		public Action<TreeViewChildNodeViewModel> OnCancel { get; set; }
		
		/// <summary>
		/// 
		/// </summary>
		public Action<TreeViewChildNodeViewModel> OnCloseRequest { get; set; }

		/// <summary>
		/// 
		/// </summary>
		public void Close()
		{
			if (this.DialogClosing != null)
				this.DialogClosing(this, new EventArgs());
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="collection"></param>
		public void Show(IList<IDialogViewModel> collection)
		{
			collection.Add(this);
		}
		
		#endregion IUserDialogViewModel Implementation
	}
}
