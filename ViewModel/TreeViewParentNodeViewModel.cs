using RFiDGear.Model;

using GalaSoft.MvvmLight.Messaging;

using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Controls;

namespace RFiDGear.ViewModel
{
	/// <summary>
	/// Description of MifareClassic1KContentViewModel.
	/// </summary>
	public class TreeViewParentNodeViewModel : INotifyPropertyChanged
	{
		#region Data

		readonly ObservableCollection<TreeViewChildNodeViewModel> _children;
		
		static object _selectedItem;

		bool _isExpanded;
		bool _isSelected;

		#endregion // Data
		
		public MifareClassicUidTreeViewModel mifareClassicUidModel { get; set; } 
		readonly MifareDesfireUidTreeViewModel _chipMifareDesfireUid;
		readonly CARD_TYPE _cardType;
		readonly List<MenuItem> ContextMenuItems;
		readonly RelayCommand _cmdReadAllSectorsWithDefaultKeys;
		readonly RelayCommand _cmdDeleteThisNode;
		readonly RelayCommand _cmdEditAuthInfoAndReadAllSectors;
		readonly string[] _constCardType = { "Mifare1K", "Mifare2K", "Mifare4K", "DESFireEV1" };
		
		public TreeViewParentNodeViewModel(MifareClassicUidTreeViewModel uidModel, CARD_TYPE cardType)
		{
			mifareClassicUidModel = uidModel;
			_cardType = cardType;
			
			_cmdReadAllSectorsWithDefaultKeys = new ViewModel.RelayCommand(ReadSectorsWithDefaultConfig);
			_cmdDeleteThisNode = new ViewModel.RelayCommand(DeleteMeCommand);
			_cmdEditAuthInfoAndReadAllSectors = new RelayCommand(EditAuthInfoAndReadAllSectors);
			
			ContextMenuItems = new List<MenuItem>();
			ContextMenuItems.Add(new MenuItem() {
			                     	Header = "Read all Sectors using default Configuration",
			                     	Command = _cmdReadAllSectorsWithDefaultKeys
			                     });
			
			ContextMenuItems.Add(new MenuItem() {
			                     	Header = "Edit Authentication Info and Read all Sectors",
			                     	Command = _cmdEditAuthInfoAndReadAllSectors
			                     });
			
			ContextMenuItems.Add(new MenuItem() {
			                     	Header = "Delete Node",
			                     	Command = _cmdDeleteThisNode
			                     });
			
			_children = new ObservableCollection<TreeViewChildNodeViewModel>();
			
			LoadChildren();
		}
		
		public TreeViewParentNodeViewModel(Model.MifareDesfireUidTreeViewModel uid, CARD_TYPE cardType)
		{
			_chipMifareDesfireUid = uid;
			_cardType = cardType;
			/*
			_cmd = new ViewModel.RelayCommand(RFiDAccess.authToMifareDesfireCard);
			
			ContextMenuItems = new List<MenuItem>();
			ContextMenuItems.Add(new MenuItem() {
			                     	Header = "auth to DesfireCard",
			                     	Command = _cmd
			                     });
			 */
		}
		
		void ReadSectorsWithDefaultConfig() {
			Messenger.Default.Send<NotificationMessage<string>>(
				new NotificationMessage<string>(this, "TreeViewParentNode", "ReadAllSectors")
			);
		}

		void EditAuthInfoAndReadAllSectors() {
			Messenger.Default.Send<NotificationMessage<string>>(
				new NotificationMessage<string>(this, "TreeViewParentNode", "EditAuthInfoAndReadAllSectors")
			);
		}
		
		void DeleteMeCommand() {
			Messenger.Default.Send<NotificationMessage<string>>(
				new NotificationMessage<string>(this, mifareClassicUidModel.UidNumber, "DeleteMe")
			);
		}
		
		public ObservableCollection<TreeViewChildNodeViewModel> Children
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
					OnSelectedItemChanged();
				}
			}
		}
		
		#endregion //SelectedItem

		
		void OnSelectedItemChanged(){
			//Messenger.Default.Send(this);
		}
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

		#region INotifyPropertyChanged Members

		public event PropertyChangedEventHandler PropertyChanged;

		protected virtual void OnPropertyChanged(string propertyName)
		{
			if (this.PropertyChanged != null)
				this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
		}

		#endregion // INotifyPropertyChanged Members
		
		public List<MenuItem> ContextMenu {
			get { return ContextMenuItems; }
		}
		
		public string ParentNodeDisplayItem {
			get { return String.Format("[{0} Type: {1}]", mifareClassicUidModel.UidNumber, _constCardType[(int)_cardType]); }
		}
		
		public string UidNumber {
			get { return mifareClassicUidModel.UidNumber; }
		}
		
		public CARD_TYPE CardType {
			get { return _cardType; }
		}
		
		void LoadChildren()
		{

			switch (_cardType) {
				case CARD_TYPE.CT_CLASSIC_1K:
					{
						for (int i = 0; i < 16; i++) {
							_children.Add(new ViewModel.TreeViewChildNodeViewModel(new Model.MifareClassicSectorTreeViewModel(i), this, _cardType, i));
						}

					}
					break;
					
				case CARD_TYPE.CT_CLASSIC_2K:
					{
						for (int i = 0; i < 32; i++) {
							_children.Add(new ViewModel.TreeViewChildNodeViewModel(new Model.MifareClassicSectorTreeViewModel(i), this, _cardType, i));
						}

					}
					break;
					
				case CARD_TYPE.CT_CLASSIC_4K:
					{
						for (int i = 0; i < 40; i++) {
							_children.Add(new ViewModel.TreeViewChildNodeViewModel(new Model.MifareClassicSectorTreeViewModel(i), this, _cardType, i));
						}

					}
					break;
					
				case CARD_TYPE.CT_DESFIRE_EV1:
					{
						_children.Add(new ViewModel.TreeViewChildNodeViewModel(new Model.MifareDesfireAppIdTreeViewModel(),this, _cardType));
					}
					break;
			}
			
			foreach(TreeViewChildNodeViewModel item in _children){
				mifareClassicUidModel.SectorList.Add(item._sectorModel);
			}
		}
	}
}
