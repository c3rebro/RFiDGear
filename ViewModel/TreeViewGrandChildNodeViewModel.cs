using System;
using GalaSoft.MvvmLight.Messaging;
using System.Linq;
using System.ComponentModel;
using RFiDGear;

namespace RFiDGear.ViewModel
{
	/// <summary>
	/// Description of TreeViewGrandChildNodeViewModel.
	/// </summary>
	public class TreeViewGrandChildNodeViewModel : INotifyPropertyChanged
	{
		public Model.MifareClassicDataBlockTreeViewModel _dataBlock {get; set;}

		#region Constructors
		
		public TreeViewGrandChildNodeViewModel(Model.MifareClassicDataBlockTreeViewModel dataBlock, TreeViewChildNodeViewModel parentSector, CARD_TYPE cardType, int sectorNumber)
		{
			_dataBlock = dataBlock;
			_parent = parentSector;
			_dataBlock.dataBlockNumber = dataBlock.dataBlockNumber;
			_dataBlock.dataBlockContent = new byte[16];
			
			_tag = String.Format("{0}:{1}",parentSector.ParentUid,parentSector.SectorNumber);
			
		}
		
		public TreeViewGrandChildNodeViewModel(string displayItem) {
			_displayItem = displayItem;
			isVisible=false;
		}
		
		#endregion
		
		#region SelectedItem
		
		private object _selectedItem;
		public object SelectedItem
		{
			get { return _selectedItem; }
			private set
			{
				if (_selectedItem != value)
				{
					_selectedItem = value;
					Messenger.Default.Send(this);
				}
			}
		}
		
		#endregion

		#region Parent

		private readonly TreeViewChildNodeViewModel _parent;
		public TreeViewChildNodeViewModel Parent
		{
			get { return _parent; }
		}

		#endregion

		#region Properties
		
		public byte[] DataBlockContent {
			get { return _dataBlock.dataBlockContent; }
			set { _dataBlock.dataBlockContent = value; }
		}
		
		private readonly string _tag;
		public string Tag {
			get {return _tag;}
		}
		
		private string _displayItem;
		public string GrandChildNodeDisplayItem {
			get { if(_dataBlock != null)
					return String.Format("Block: [{0}]", _dataBlock.dataBlockNumber);
				return _displayItem;
			}
		}
		
		public int DataBlockNumber {
			get { return _dataBlock.dataBlockNumber; }
			set { _dataBlock.dataBlockNumber = value; }
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
		
		#region View Switches
		
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
		
		private bool? isAuth;
		public bool? IsAuthenticated {
			get {return isAuth;}
			set { isAuth = value; OnPropertyChanged("IsAuthenticated");}
		}
		
		private bool isVisible = true;
		public bool IsVisible {
			get { return isVisible; }
			set { isVisible = value;
				OnPropertyChanged("IsVisible"); }
		}
		
		#endregion
	}
}
