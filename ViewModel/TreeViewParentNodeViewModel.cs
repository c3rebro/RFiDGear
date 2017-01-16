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
		public MifareDesfireUidTreeViewModel mifareDesfireUidModel { get; set; }
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
			                     	Header = "Read Card with default Keys",
			                     	Command = _cmdReadAllSectorsWithDefaultKeys
			                     });
			
			ContextMenuItems.Add(new MenuItem() {
			                     	Header = "Read Card with custom Keys...",
			                     	Command = _cmdEditAuthInfoAndReadAllSectors
			                     });

			ContextMenuItems.Add(new MenuItem() {
			                     	Header = "Format Card...",
			                     	Command = _cmdEditAuthInfoAndReadAllSectors
			                     });
			
			ContextMenuItems.Add(new MenuItem() {
			                     	Header = "Read Card and Dump to File...",
			                     	Command = _cmdEditAuthInfoAndReadAllSectors
			                     });
			
			ContextMenuItems.Add(new MenuItem() {
			                     	Header = "Delete this Node",
			                     	Command = _cmdDeleteThisNode
			                     });
			
			_children = new ObservableCollection<TreeViewChildNodeViewModel>();
			
			LoadChildren();
		}
		
		public TreeViewParentNodeViewModel(Model.MifareDesfireUidTreeViewModel uid, CARD_TYPE cardType)
		{
			mifareDesfireUidModel = uid;
			_cardType = cardType;
			
			RelayCommand _cmd = new RelayCommand(EditAuthInfoAndReadAllSectors);
			
			ContextMenuItems = new List<MenuItem>();
			ContextMenuItems.Add(new MenuItem() {
			                     	Header = "Read Application IDs",
			                     	Command = _cmd
			                     });

			ContextMenuItems.Add(new MenuItem() {
			                     	Header = "Create a new Application...",
			                     	Command = _cmd
			                     });
			
			ContextMenuItems.Add(new MenuItem() {
			                     	Header = "Authenticate with Card Master Key...",
			                     	Command = _cmd
			                     });
			
			_children = new ObservableCollection<TreeViewChildNodeViewModel>();
			
			LoadChildren();
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
			get { if(mifareClassicUidModel != null)
					return String.Format("[{0} Type: {1}]", mifareClassicUidModel.UidNumber, _constCardType[(int)_cardType]);
				else
					return String.Format("[{0} Type: {1}]",mifareDesfireUidModel.uidNumber, _constCardType[(int)_cardType]);
			}
		}
		
		public string UidNumber {
			get { if(mifareClassicUidModel != null)
					return mifareClassicUidModel.UidNumber;
				else
					return mifareDesfireUidModel.uidNumber;
			}
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
							_children.Add(new TreeViewChildNodeViewModel(new MifareClassicSectorTreeViewModel(i), this, _cardType, i));
						}

					}
					break;
					
				case CARD_TYPE.CT_CLASSIC_2K:
					{
						for (int i = 0; i < 32; i++) {
							_children.Add(new TreeViewChildNodeViewModel(new MifareClassicSectorTreeViewModel(i), this, _cardType, i));
						}

					}
					break;
					
				case CARD_TYPE.CT_CLASSIC_4K:
					{
						for (int i = 0; i < 40; i++) {
							_children.Add(new TreeViewChildNodeViewModel(new MifareClassicSectorTreeViewModel(i), this, _cardType, i));
						}

					}
					break;
					
				case CARD_TYPE.CT_DESFIRE_EV1:
					{
						_children.Add(new TreeViewChildNodeViewModel(new MifareDesfireAppIdTreeViewModel(new string[1] {"00001"}),this, _cardType));
					}
					break;
			}
			
			foreach(TreeViewChildNodeViewModel item in _children){
				if(mifareClassicUidModel != null)
					mifareClassicUidModel.SectorList.Add(item._sectorModel);
			}
		}
	}
}
