/*
 * Created by SharpDevelop.
 * User: rotts
 * Date: 26.11.2016
 * Time: 22:56
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using GalaSoft.MvvmLight;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace RFiDGear.ViewModel
{
	/// <summary>
	/// Description of TreeViewModelBase.
	/// </summary>
	public class TreeViewModelBase : INotifyPropertyChanged
	{
		public delegate void SelectedTreeViewItemChanged(object sender, EventArgs e);
		public event SelectedTreeViewItemChanged itemSelectedEvent;
		
		#region Data

		static readonly TreeViewModelBase DummyChild = new TreeViewModelBase();

		readonly ObservableCollection<TreeViewModelBase> _children;
		readonly TreeViewModelBase _parent;
		
		static object _selectedItem;

		bool _isExpanded;
		bool _isSelected;

		#endregion // Data

		#region Constructors

		protected TreeViewModelBase(TreeViewModelBase parent, bool lazyLoadChildren)
		{
			_parent = parent;

			_children = new ObservableCollection<TreeViewModelBase>();

			if (lazyLoadChildren)
				_children.Add(DummyChild);
		}

		// This is used to create the DummyChild instance.
		TreeViewModelBase()
		{
		}

		#endregion // Constructors

		#region Presentation Members

		#region Children

		/// <summary>
		/// Returns the logical child items of this object.
		/// </summary>
		public ObservableCollection<TreeViewModelBase> Children
		{
			get { return _children; }
		}

		#endregion // Children
		
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

		
		void OnSelectedItemChanged()
		{
			itemSelectedEvent(_selectedItem, EventArgs.Empty);
			// Raise event / do other things
		}
		
		#region HasLoadedChildren

		/// <summary>
		/// Returns true if this object's Children have not yet been populated.
		/// </summary>
		public bool HasDummyChild
		{
			get { return this.Children.Count == 1 && this.Children[0] == DummyChild; }
		}

		#endregion // HasLoadedChildren

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

				// Lazy load the child items, if necessary.
				if (this.HasDummyChild)
				{
					this.Children.Remove(DummyChild);
					this.LoadChildren();
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

		#region LoadChildren

		/// <summary>
		/// Invoked when the child items need to be loaded on demand.
		/// Subclasses can override this to populate the Children collection.
		/// </summary>
		protected virtual void LoadChildren()
		{
		}

		#endregion // LoadChildren

		#region Parent

		public TreeViewModelBase Parent
		{
			get { return _parent; }
		}

		#endregion // Parent

		#endregion // Presentation Members

		#region INotifyPropertyChanged Members

		public event PropertyChangedEventHandler PropertyChanged;

		protected virtual void OnPropertyChanged(string propertyName)
		{
			if (this.PropertyChanged != null)
				this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
		}

		#endregion // INotifyPropertyChanged Members
	}
}
