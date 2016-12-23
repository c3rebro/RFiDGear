/*
 * Created by SharpDevelop.
 * User: rotts
 * Date: 26.11.2016
 * Time: 23:05
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using GalaSoft.MvvmLight.Messaging;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Input;
using RFiDGear;

namespace RFiDGear.ViewModel
{
	/// <summary>
	/// Description of TreeViewGrandChildNodeViewModel.
	/// </summary>
	public class TreeViewGrandChildNodeViewModel : INotifyPropertyChanged
	{
		
		#region Data

		readonly TreeViewChildNodeViewModel _parent;
		
		static object _selectedItem;

		bool _isExpanded;
		bool _isSelected;
		
		bool? isAuth;

		#endregion // Data
		
		readonly Model.chipMifareClassicDataBlock _dataBlock;
		readonly string _tag;
		

		public TreeViewGrandChildNodeViewModel(Model.chipMifareClassicDataBlock dataBlock, TreeViewChildNodeViewModel parentSector, CARD_TYPE cardType, int sectorNumber)
		{
			_dataBlock = dataBlock;
			_dataBlock.dataBlockNumber = dataBlock.dataBlockNumber;
			_dataBlock.dataBlockContent = new byte[16];
			
			_tag = String.Format("{0}:{1}",parentSector.ParentUid,parentSector.SectorNumber);
			
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
					Messenger.Default.Send(this);
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

		public TreeViewChildNodeViewModel Parent
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
		
		public string DataBlockDisplayItem {
			get { return String.Format("Block: [{0}]", _dataBlock.dataBlockNumber); }
		}
		
		public int DataBlockNumber {
			get { return _dataBlock.dataBlockNumber; }
			set { _dataBlock.dataBlockNumber = value; }
		}
		
		public bool? IsAuthenticated {
			get {return isAuth;}
			set { isAuth = value; OnPropertyChanged("IsAuthenticated");}
		}
		
		public byte[] DataBlockContent {
			get { return _dataBlock.dataBlockContent; }
			set { _dataBlock.dataBlockContent = value; }
		}
		
		public string Tag {
			get {return _tag;}
		}
	}
}
