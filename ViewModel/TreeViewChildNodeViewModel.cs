using System;
using GalaSoft.MvvmLight.Messaging;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Input;
using RFiDGear.Model;

namespace RFiDGear.ViewModel
{
	/// <summary>
	/// Description of TreeViewChildNodeViewModel.
	/// </summary>
	public class TreeViewChildNodeViewModel : INotifyPropertyChanged
	{
		
		#region Data

		private readonly ObservableCollection<TreeViewGrandChildNodeViewModel> _children;
		private readonly TreeViewParentNodeViewModel _parent;
		
		private static object _selectedItem;

		private bool _isExpanded;
		private bool _isSelected;

		#endregion // Data
		
		public MifareClassicSectorTreeViewModel _sectorModel { get; set; }
		private readonly MifareDesfireAppIdTreeViewModel _appID;
		private readonly CARD_TYPE _cardType;
		private readonly RelayCommand _cmdReadSectorWithDefaults;
		private readonly RelayCommand _cmdEditAuthAndModifySector;
		
		private readonly string _parentUid;
		
		private readonly List<MenuItem> ContextMenuItems;

		private bool? isAuth;
		
		public TreeViewChildNodeViewModel(MifareClassicSectorTreeViewModel sectorModel, TreeViewParentNodeViewModel parent, CARD_TYPE cardType, int sectorNumber)
		{
			_sectorModel = sectorModel;
			_sectorModel.mifareClassicSectorNumber = sectorNumber;
			_cardType = cardType;
			_parent = parent;
			
			_cmdReadSectorWithDefaults = new RelayCommand(ReadSectorWithDefaults);
			_cmdEditAuthAndModifySector = new RelayCommand(EditAuthAndModifySector);
			
			ContextMenuItems = new List<MenuItem>();
			ContextMenuItems.Add(new MenuItem() {
			                     	Header = "Read Sector with default Keys",
			                     	Command = _cmdReadSectorWithDefaults
			                     });
			
			ContextMenuItems.Add(new MenuItem() {
			                     	Header = "Read Sector with custom Keys...",
			                     	Command = _cmdEditAuthAndModifySector
			                     });
			
			ContextMenuItems.Add(new MenuItem() {
			                     	Header = "Copy Sector to Clipboard",
			                     	Command = _cmdEditAuthAndModifySector
			                     });

			ContextMenuItems.Add(new MenuItem() {
			                     	Header = "Paste from Clipboard",
			                     	Command = _cmdEditAuthAndModifySector
			                     });
			
			ContextMenuItems.Add(new MenuItem() {
			                     	Header = "Modify Sector Access Rights...",
			                     	Command = _cmdEditAuthAndModifySector
			                     });
			
			_children = new ObservableCollection<TreeViewGrandChildNodeViewModel>();
			
			LoadChildren();
		}

		public TreeViewChildNodeViewModel(MifareDesfireAppIdTreeViewModel appID, TreeViewParentNodeViewModel parentUID, CARD_TYPE cardType)
		{
			_appID = appID;
			_cardType = cardType;
			_parentUid = parentUID.UidNumber;
			
			_cmdReadSectorWithDefaults = new RelayCommand(ReadSectorWithDefaults);
			_cmdEditAuthAndModifySector = new RelayCommand(EditAuthAndModifySector);
			
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

		
		public ObservableCollection<TreeViewGrandChildNodeViewModel> Children
		{
			get { return _children; }
		}
		
		#region SelectedItem
		
		public object SelectedItem
		{
			get { return _selectedItem; }
			private set
			{
				if (_selectedItem != value)
				{
					_selectedItem = value;
				}
			}
		}
		
		#endregion //SelectedItem
		
		#region IsExpanded

		/// <summary>
		/// Gets/sets whether the TreeViewItem
		/// associated with this object is expanded.
		/// </summary>
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

		#endregion // IsExpanded

		#region IsSelected

		/// <summary>
		/// Gets/sets whether the TreeViewItem
		/// associated with this object is selected.
		/// </summary>
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

		#endregion // IsSelected

		#region Parent

		public TreeViewParentNodeViewModel Parent
		{
			get { return _parent; }
		}

		#endregion // Parent


		#region INotifyPropertyChanged Members

		public event PropertyChangedEventHandler PropertyChanged;

		protected virtual void OnPropertyChanged(string propertyName)
		{
			if (this.PropertyChanged != null)
				this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
		}

		#endregion // INotifyPropertyChanged Members
		
		public ICommand ReadSectorCommand {get { return new RelayCommand(ReadSectorWithDefaults); }}
		public void ReadSectorWithDefaults() {
			Messenger.Default.Send<NotificationMessage<string>>(
				new NotificationMessage<string>(this, "TreeViewChildNode", "ReadSectorWithDefaults")
			);
		}

		public void EditAuthAndModifySector() {
			Messenger.Default.Send<NotificationMessage<string>>(
				new NotificationMessage<string>(this, "TreeViewChildNode", "EditAuthAndModifySector")
			);
		}
		
		public List<MenuItem> ContextMenu {
			get { return ContextMenuItems; }
		}
		
		public string SectorNumberDisplayItem {
			get { return String.Format("Sector: [{0}]", _sectorModel.mifareClassicSectorNumber); }
		}
		
		public int SectorNumber {
			get { return _sectorModel.mifareClassicSectorNumber; }
			set { _sectorModel.mifareClassicSectorNumber = value; }
		}
		
		public int BlockCount {
			get { return _sectorModel.mifareClassicSectorNumber > 31 ? 16 : 4; }
		}

		public bool? IsAuthenticated {
			get {return isAuth;}
			set { isAuth = value; OnPropertyChanged("IsAuthenticated");}
		}
		
		public string ParentUid {
			get { return _parentUid; }
		}

		public void LoadChildren()
		{
			switch (_cardType) {
				case CARD_TYPE.CT_CLASSIC_1K:
					{
						for (int i = 0; i < 4; i++) {
							_children.Add(new TreeViewGrandChildNodeViewModel(new MifareClassicDataBlockTreeViewModel(i), this, _cardType, _sectorModel.mifareClassicSectorNumber));
						}
					}
					break;
					
				case CARD_TYPE.CT_CLASSIC_2K:
					{
						for (int i = 0; i < 4; i++) {
							_children.Add(new TreeViewGrandChildNodeViewModel(new MifareClassicDataBlockTreeViewModel(i), this, _cardType, _sectorModel.mifareClassicSectorNumber));
						}
					}
					break;
					
				case CARD_TYPE.CT_CLASSIC_4K:
					{
						if (SectorNumber < 32) {
							for (int i = 0; i < 4; i++) {
								_children.Add(new TreeViewGrandChildNodeViewModel(new MifareClassicDataBlockTreeViewModel(i), this, _cardType, _sectorModel.mifareClassicSectorNumber));
							}
						} else {
							
							for (int i = 0; i < 16; i++) {
								_children.Add(new TreeViewGrandChildNodeViewModel(new MifareClassicDataBlockTreeViewModel(i), this, _cardType, _sectorModel.mifareClassicSectorNumber));
							}
						}
					}
					break;
			}
			foreach(TreeViewGrandChildNodeViewModel item in _children){
				_sectorModel.dataBlock.Add(item._dataBlock);
			}
		}
	}
}
