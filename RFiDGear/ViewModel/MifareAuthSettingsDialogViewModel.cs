using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;

using MvvmDialogs.ViewModels;

using RFiDGear.DataSource;

using System;
using System.Security;
using System.ComponentModel;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace RFiDGear.ViewModel
{
	/// <summary>
	/// Description of KeySettingsMifareClassicDialogViewModel.
	/// </summary>
	public class MifareAuthSettingsDialogViewModel : ViewModelBase, IUserDialogViewModel
	{

		
		#region Constructors

		public MifareAuthSettingsDialogViewModel(object treeViewViewModel, bool isModal = true)
		{

		}
		
		#endregion
		
		#region Text Boxes
		
		public string SectorTrailerTextBoxText {
			get {
				return String.Format("{0},{1},{2}"
				                     , (ViewModelContext as TreeViewChildNodeViewModel)._sectorModel.sectorAccessBits.sectorKeyAKey.Replace(" ", "").ToUpper()
				                     , (ViewModelContext as TreeViewChildNodeViewModel)._sectorModel.sectorAccessBits.SectorTrailerAccessBits
				                     , (ViewModelContext as TreeViewChildNodeViewModel)._sectorModel.sectorAccessBits.sectorKeyBKey.Replace(" ", "").ToUpper());
			}
		}

		public string selectedClassicKeyAKey {
			get {
				if (ViewModelContext is TreeViewChildNodeViewModel)
					return (ViewModelContext as TreeViewChildNodeViewModel)._sectorModel.sectorAccessBits.sectorKeyAKey.Replace(" ", "");
				else {
					return (ViewModelContext as TreeViewParentNodeViewModel).Children[selectedKeyNumber]._sectorModel.sectorAccessBits.sectorKeyAKey.Replace(" ", "");
				}
			}
			
			set {
				CustomConverter conv = new CustomConverter();
				conv.classicKeyToEdit = value;
				if (ViewModelContext is TreeViewChildNodeViewModel && conv.FormatMifareClassicKeyStringWithSpacesEachByte(value) == KEY_ERROR.NO_ERROR)
					(ViewModelContext as TreeViewChildNodeViewModel)._sectorModel.sectorAccessBits.sectorKeyAKey = conv.classicKeyToEdit;
				else if (ViewModelContext is TreeViewParentNodeViewModel && conv.FormatMifareClassicKeyStringWithSpacesEachByte(value) == KEY_ERROR.NO_ERROR)
					(ViewModelContext as TreeViewParentNodeViewModel).Children[selectedKeyNumber]._sectorModel.sectorAccessBits.sectorKeyAKey = conv.classicKeyToEdit;
				
				RaisePropertyChanged("SectorTrailerTextBoxText");
			}
		}
		
		public string selectedClassicKeyBKey {
			get {
				if (ViewModelContext is TreeViewChildNodeViewModel)
					return (ViewModelContext as TreeViewChildNodeViewModel)._sectorModel.sectorAccessBits.sectorKeyBKey.Replace(" ", "");
				else {
					return (ViewModelContext as TreeViewParentNodeViewModel).Children[selectedKeyNumber]._sectorModel.sectorAccessBits.sectorKeyBKey.Replace(" ", "");
				}
			}
			
			set {
				CustomConverter conv = new CustomConverter();
				conv.classicKeyToEdit = value;
				if (ViewModelContext is TreeViewChildNodeViewModel && conv.FormatMifareClassicKeyStringWithSpacesEachByte(value) == KEY_ERROR.NO_ERROR)
					(ViewModelContext as TreeViewChildNodeViewModel)._sectorModel.sectorAccessBits.sectorKeyBKey = conv.classicKeyToEdit;
				else if (ViewModelContext is TreeViewParentNodeViewModel && conv.FormatMifareClassicKeyStringWithSpacesEachByte(value) == KEY_ERROR.NO_ERROR)
					(ViewModelContext as TreeViewParentNodeViewModel).Children[selectedKeyNumber]._sectorModel.sectorAccessBits.sectorKeyBKey = conv.classicKeyToEdit;
				
				RaisePropertyChanged("SectorTrailerTextBoxText");
			}
		}
		
		#endregion
		
		#region Switches and Toggles
		
		private bool _dataBlockIsCombinedToggleButtonIsChecked;
		public bool DataBlockIsCombinedToggleButtonIsChecked {
			get { return _dataBlockIsCombinedToggleButtonIsChecked; }
			set {
				_dataBlockIsCombinedToggleButtonIsChecked = value;
				selectedDataBlockRow = 0;
				RaisePropertyChanged("SelectedDataBlockItem");
				RaisePropertyChanged("DataBlockSelectionComboBoxIsEnabled");
				RaisePropertyChanged("DataBlockSelection");
			}
		}
		
		#endregion

		#region Selected Items
		
		public MifareClassicAccessBitsSectorTrailerDataGridModel SelectedSectorTrailerAccessBitsItem {
			get { return selectedSectorTrailerAccessBitsItem; }
			set { selectedSectorTrailerAccessBitsItem = value; }
			
		}
		
		public MifareClassicAccessBitsSectorTrailerDataGridModel SelectedSectorTrailerAccessBitsRow {
			get {
				if (selectedSectorTrailerAccessBitsItem.getReadAccessCond == "Key A or B") {
					displaySourceForCombinedDataBlockDataGrid = null;
					displaySourceForCombinedDataBlockDataGrid = new ObservableCollection<object>(displaySourceForLongDataBlockDataGrid);
				} else {
					displaySourceForCombinedDataBlockDataGrid = null;
					displaySourceForCombinedDataBlockDataGrid = new ObservableCollection<object>(displaySourceForShortDataBlockDataGrid);
				}
				return selectedSectorTrailerAccessBitsItem;
			}
			
			set {
				selectedSectorTrailerAccessBitsItem = value;
				if (selectedSectorTrailerAccessBitsItem.getReadAccessCond == "Key A or B") {
					displaySourceForCombinedDataBlockDataGrid = null;
					displaySourceForCombinedDataBlockDataGrid = new ObservableCollection<object>(displaySourceForLongDataBlockDataGrid);
				} else {
					displaySourceForCombinedDataBlockDataGrid = null;
					displaySourceForCombinedDataBlockDataGrid = new ObservableCollection<object>(displaySourceForShortDataBlockDataGrid);
				}

				RaisePropertyChanged("DataBlockSource");
				RaisePropertyChanged("SectorTrailerTextBoxText");
			}
		}
		
		public object SelectedDataBlockAccessBitsRow {
			get {
				if (_dataBlockIsCombinedToggleButtonIsChecked)
					return selectedCombinedDataBlockAccessBitsDataGridItem;
				
				switch (selectedDataBlockRow) {
					case 0:
						return selectedDataBlock0AccessBitsDataGridItem;
					case 1:
						return selectedDataBlock1AccessBitsDataGridItem;
					case 2:
						return selectedDataBlock2AccessBitsDataGridItem;
				}
				return null;
			}
			set {
				if (_dataBlockIsCombinedToggleButtonIsChecked)
					selectedCombinedDataBlockAccessBitsDataGridItem = value;
				else {
					switch (selectedDataBlockRow) {
						case 0:
							selectedDataBlock0AccessBitsDataGridItem = value;
							break;
						case 1:
							selectedDataBlock1AccessBitsDataGridItem = value;
							break;
						case 2:
							selectedDataBlock2AccessBitsDataGridItem = value;
							break;
					}
				}
				
				RaisePropertyChanged("DataBlockSource");
				RaisePropertyChanged("SectorTrailerTextBoxText");
			}
		}
		
		public string SelectedDataBlockItem {
			get { return _dataBlockIsCombinedToggleButtonIsChecked ? "Combined" : datablockSelectorSeparatedCmbbxItems[selectedDataBlockRow]; }
			set {
				if (value != "Combined")
					selectedDataBlockRow = Array.IndexOf(datablockSelectorSeparatedCmbbxItems, value);
				RaisePropertyChanged("DataBlockSource");
				RaisePropertyChanged("SelectedDataBlockAccessBitsRow");
				RaisePropertyChanged("SectorTrailerTextBoxText");
			}
		}
		
		private int selectedKeyNumber;
		public int KeySelectionComboboxIndex {
			get {
				if (ViewModelContext is TreeViewChildNodeViewModel)
					return (ViewModelContext as TreeViewChildNodeViewModel)._sectorModel.mifareClassicSectorNumber;
				else
					return selectedKeyNumber;
			}
			set {
				selectedKeyNumber = value;
				RaisePropertyChanged("KeySelectionComboboxIndex");
				RaisePropertyChanged("selectedClassicKeyAKey");
				RaisePropertyChanged("selectedClassicKeyBKey");
			}
		}
		
		#endregion
		
		#region Items Sources
		
		private readonly string[] datablockSelectorSeparatedCmbbxItems = {
			"DataBlock 0",
			"DataBlock 1",
			"DataBlock 2",
		};
		
		public ObservableCollection<string> DataBlockSelection {
			get { return _dataBlockIsCombinedToggleButtonIsChecked ? new ObservableCollection<string>(new string[1] { "Combined" }) : new ObservableCollection<string>(datablockSelectorSeparatedCmbbxItems); }
		}
		
		private readonly string[] keyIndexComboBoxItems = {
			"No. 00",
			"No. 01",
			"No. 02",
			"No. 03",
			"No. 04",
			"No. 05",
			"No. 06",
			"No. 07",
			"No. 08",
			"No. 09",
			"No. 10",
			"No. 11",
			"No. 12",
			"No. 13",
			"No. 14",
			"No. 15",
			"No. 16",
			"No. 17",
			"No. 18",
			"No. 19",
			"No. 20",
			"No. 21",
			"No. 22",
			"No. 23",
			"No. 24",
			"No. 25",
			"No. 26",
			"No. 27",
			"No. 28",
			"No. 29",
			"No. 30",
			"No. 31"
		};
		
		public ObservableCollection<string> KeySelection {
			get { return new ObservableCollection<string>(keyIndexComboBoxItems); }
		}
		
		public ObservableCollection<MifareClassicAccessBitsSectorTrailerDataGridModel> SectorTrailerSource {
			get { return displaySourceForSectorTrailerDataGrid; }
		}
		
		public ObservableCollection<object> DataBlockSource {
			get {
				
				if (_dataBlockIsCombinedToggleButtonIsChecked) {
					if (ViewModelContext is TreeViewChildNodeViewModel) {
						if (selectedCombinedDataBlockAccessBitsDataGridItem is MifareClassicAccessBitsShortDataBlockDataGridModel) {
							(ViewModelContext as TreeViewChildNodeViewModel)._sectorModel.sectorAccessBits.SectorTrailerAccessBits = (selectedSectorTrailerAccessBitsItem as MifareClassicAccessBitsSectorTrailerDataGridModel)
								.ProcessSectorTrailerEncoding((selectedCombinedDataBlockAccessBitsDataGridItem as MifareClassicAccessBitsShortDataBlockDataGridModel).GetShortDataBlockAccessBitsFromHumanReadableFormat()
								                              , (selectedCombinedDataBlockAccessBitsDataGridItem as MifareClassicAccessBitsShortDataBlockDataGridModel).GetShortDataBlockAccessBitsFromHumanReadableFormat()
								                              , (selectedCombinedDataBlockAccessBitsDataGridItem as MifareClassicAccessBitsShortDataBlockDataGridModel).GetShortDataBlockAccessBitsFromHumanReadableFormat()).SectorTrailerAccessBits;
							RaisePropertyChanged("SectorTrailerTextBoxText");
						} else if (selectedCombinedDataBlockAccessBitsDataGridItem is MifareClassicAccessBitsLongDataBlockDataGridModel) {
							(ViewModelContext as TreeViewChildNodeViewModel)._sectorModel.sectorAccessBits.SectorTrailerAccessBits = (selectedSectorTrailerAccessBitsItem as MifareClassicAccessBitsSectorTrailerDataGridModel)
								.ProcessSectorTrailerEncoding((selectedCombinedDataBlockAccessBitsDataGridItem as MifareClassicAccessBitsLongDataBlockDataGridModel).GetLongDataBlockAccessBitsFromHumanReadableFormat()
								                              , (selectedCombinedDataBlockAccessBitsDataGridItem as MifareClassicAccessBitsLongDataBlockDataGridModel).GetLongDataBlockAccessBitsFromHumanReadableFormat()
								                              , (selectedCombinedDataBlockAccessBitsDataGridItem as MifareClassicAccessBitsLongDataBlockDataGridModel).GetLongDataBlockAccessBitsFromHumanReadableFormat()).SectorTrailerAccessBits;
							RaisePropertyChanged("SectorTrailerTextBoxText");
						}

					}

					return displaySourceForCombinedDataBlockDataGrid;
				} else {
					switch (selectedDataBlockRow) {

						case 2:
							if (selectedDataBlock0AccessBitsDataGridItem != null && selectedDataBlock1AccessBitsDataGridItem != null && selectedDataBlock2AccessBitsDataGridItem != null) {
								if (selectedDataBlock2AccessBitsDataGridItem is MifareClassicAccessBitsShortDataBlockDataGridModel)
									(ViewModelContext as TreeViewChildNodeViewModel)._sectorModel.sectorAccessBits.SectorTrailerAccessBits = (selectedSectorTrailerAccessBitsItem as MifareClassicAccessBitsSectorTrailerDataGridModel)
										.ProcessSectorTrailerEncoding((selectedDataBlock0AccessBitsDataGridItem as MifareClassicAccessBitsShortDataBlockDataGridModel).GetShortDataBlockAccessBitsFromHumanReadableFormat()
										                              , (selectedDataBlock1AccessBitsDataGridItem as MifareClassicAccessBitsShortDataBlockDataGridModel).GetShortDataBlockAccessBitsFromHumanReadableFormat()
										                              , (selectedDataBlock2AccessBitsDataGridItem as MifareClassicAccessBitsShortDataBlockDataGridModel).GetShortDataBlockAccessBitsFromHumanReadableFormat()).SectorTrailerAccessBits;
								else
									(ViewModelContext as TreeViewChildNodeViewModel)._sectorModel.sectorAccessBits.SectorTrailerAccessBits = (selectedSectorTrailerAccessBitsItem as MifareClassicAccessBitsSectorTrailerDataGridModel)
										.ProcessSectorTrailerEncoding((selectedDataBlock0AccessBitsDataGridItem as MifareClassicAccessBitsLongDataBlockDataGridModel).GetLongDataBlockAccessBitsFromHumanReadableFormat()
										                              , (selectedDataBlock1AccessBitsDataGridItem as MifareClassicAccessBitsLongDataBlockDataGridModel).GetLongDataBlockAccessBitsFromHumanReadableFormat()
										                              , (selectedDataBlock2AccessBitsDataGridItem as MifareClassicAccessBitsLongDataBlockDataGridModel).GetLongDataBlockAccessBitsFromHumanReadableFormat()).SectorTrailerAccessBits;
							}

							RaisePropertyChanged("SectorTrailerTextBoxText");
							return displaySourceForCombinedDataBlockDataGrid;
							
						case 1:
							if (selectedDataBlock0AccessBitsDataGridItem != null && selectedDataBlock1AccessBitsDataGridItem != null && selectedDataBlock2AccessBitsDataGridItem != null) {
								if (selectedDataBlock1AccessBitsDataGridItem is MifareClassicAccessBitsShortDataBlockDataGridModel)
									(ViewModelContext as TreeViewChildNodeViewModel)._sectorModel.sectorAccessBits.SectorTrailerAccessBits = (selectedSectorTrailerAccessBitsItem as MifareClassicAccessBitsSectorTrailerDataGridModel)
										.ProcessSectorTrailerEncoding((selectedDataBlock0AccessBitsDataGridItem as MifareClassicAccessBitsShortDataBlockDataGridModel).GetShortDataBlockAccessBitsFromHumanReadableFormat()
										                              , (selectedDataBlock1AccessBitsDataGridItem as MifareClassicAccessBitsShortDataBlockDataGridModel).GetShortDataBlockAccessBitsFromHumanReadableFormat()
										                              , (selectedDataBlock2AccessBitsDataGridItem as MifareClassicAccessBitsShortDataBlockDataGridModel).GetShortDataBlockAccessBitsFromHumanReadableFormat()).SectorTrailerAccessBits;
								else
									(ViewModelContext as TreeViewChildNodeViewModel)._sectorModel.sectorAccessBits.SectorTrailerAccessBits = (selectedSectorTrailerAccessBitsItem as MifareClassicAccessBitsSectorTrailerDataGridModel)
										.ProcessSectorTrailerEncoding((selectedDataBlock0AccessBitsDataGridItem as MifareClassicAccessBitsLongDataBlockDataGridModel).GetLongDataBlockAccessBitsFromHumanReadableFormat()
										                              , (selectedDataBlock1AccessBitsDataGridItem as MifareClassicAccessBitsLongDataBlockDataGridModel).GetLongDataBlockAccessBitsFromHumanReadableFormat()
										                              , (selectedDataBlock2AccessBitsDataGridItem as MifareClassicAccessBitsLongDataBlockDataGridModel).GetLongDataBlockAccessBitsFromHumanReadableFormat()).SectorTrailerAccessBits;
							}
							RaisePropertyChanged("SectorTrailerTextBoxText");
							return displaySourceForCombinedDataBlockDataGrid;
							
						case 0:
							if (selectedDataBlock0AccessBitsDataGridItem != null && selectedDataBlock1AccessBitsDataGridItem != null && selectedDataBlock2AccessBitsDataGridItem != null) {
								if (selectedDataBlock0AccessBitsDataGridItem is MifareClassicAccessBitsShortDataBlockDataGridModel)
									(ViewModelContext as TreeViewChildNodeViewModel)._sectorModel.sectorAccessBits.SectorTrailerAccessBits = (selectedSectorTrailerAccessBitsItem as MifareClassicAccessBitsSectorTrailerDataGridModel)
										.ProcessSectorTrailerEncoding((selectedDataBlock0AccessBitsDataGridItem as MifareClassicAccessBitsShortDataBlockDataGridModel).GetShortDataBlockAccessBitsFromHumanReadableFormat()
										                              , (selectedDataBlock1AccessBitsDataGridItem as MifareClassicAccessBitsShortDataBlockDataGridModel).GetShortDataBlockAccessBitsFromHumanReadableFormat()
										                              , (selectedDataBlock2AccessBitsDataGridItem as MifareClassicAccessBitsShortDataBlockDataGridModel).GetShortDataBlockAccessBitsFromHumanReadableFormat()).SectorTrailerAccessBits;
								else
									(ViewModelContext as TreeViewChildNodeViewModel)._sectorModel.sectorAccessBits.SectorTrailerAccessBits = (selectedSectorTrailerAccessBitsItem as MifareClassicAccessBitsSectorTrailerDataGridModel)
										.ProcessSectorTrailerEncoding((selectedDataBlock0AccessBitsDataGridItem as MifareClassicAccessBitsLongDataBlockDataGridModel).GetLongDataBlockAccessBitsFromHumanReadableFormat()
										                              , (selectedDataBlock1AccessBitsDataGridItem as MifareClassicAccessBitsLongDataBlockDataGridModel).GetLongDataBlockAccessBitsFromHumanReadableFormat()
										                              , (selectedDataBlock2AccessBitsDataGridItem as MifareClassicAccessBitsLongDataBlockDataGridModel).GetLongDataBlockAccessBitsFromHumanReadableFormat()).SectorTrailerAccessBits;
							}
							RaisePropertyChanged("SectorTrailerTextBoxText");
							return displaySourceForCombinedDataBlockDataGrid;
							
						default:
							return null;
					}
				}
			}
		}
		
		#endregion
		
		#region View Switchers
		
		public bool DataBlockSelectionComboBoxIsEnabled {
			get { return !_dataBlockIsCombinedToggleButtonIsChecked; }
		}
		
		public bool? IsFixedKeyNumber {
			get {
				if (ViewModelContext is TreeViewChildNodeViewModel)
					return false;
				else
					return true;
			}
		}

		private bool isClassicAuthInfo = true;
		public bool IsClassicAuthInfoEnabled {
			get { return isClassicAuthInfo; }
			set {
				isClassicAuthInfo = value;
				RaisePropertyChanged("IsClassicAuthInfoEnabled");
			}
		}
		
		private bool isAccessBitsEnabled = false;
		public bool IsAccessBitsEditTabEnabled {
			get { return isAccessBitsEnabled; }
			set {
				isAccessBitsEnabled = value;
				RaisePropertyChanged("IsAccessBitsEditTabEnabled");
			}
		}
		
		public bool IsDesfireAuthInfoEnabled {
			get { return !isClassicAuthInfo; }
			set {
				isClassicAuthInfo = !value;
				RaisePropertyChanged("IsDesfireAuthInfoEnabled");
			}
		}
		
		#endregion
		
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

		public ICommand ApplyCommand { get { return new RelayCommand(Ok); } }
		protected virtual void Ok()
		{
			if (this.OnOk != null)
				this.OnOk(this);
			else
				Close();
		}
		
		public ICommand ExitCommand { get { return new RelayCommand(Cancel); } }
		protected virtual void Cancel()
		{
			if (this.OnCancel != null)
				this.OnCancel(this);
			else
				Close();
		}
		
		public ICommand AuthCommand { get { return new RelayCommand(Auth); } }
		protected virtual void Auth()
		{
			if (this.OnAuth != null)
				this.OnAuth(this);
			else
				Close();
		}
		
		public Action<MifareAuthSettingsDialogViewModel> OnOk { get; set; }
		public Action<MifareAuthSettingsDialogViewModel> OnCancel { get; set; }
		public Action<MifareAuthSettingsDialogViewModel> OnAuth { get; set; }
		public Action<MifareAuthSettingsDialogViewModel> OnCloseRequest { get; set; }

		public void Close()
		{
			if (this.DialogClosing != null)
				this.DialogClosing(this, new EventArgs());
		}

		public void Show(IList<IDialogViewModel> collection)
		{
			collection.Add(this);
		}
		
		#endregion IUserDialogViewModel Implementation
		
		#region INotifyPropertyChanged Members

		public event PropertyChangedEventHandler KeySettingsPropertyChanged;

		protected virtual void OnPropertyChanged(string propertyName)
		{
			if (this.KeySettingsPropertyChanged != null)
				this.KeySettingsPropertyChanged(this, new PropertyChangedEventArgs(propertyName));
		}

		#endregion
		
		#region Localization

		private string _Caption;
		public string Caption {
			get { return _Caption; }
			set {
				_Caption = value;
				RaisePropertyChanged("Caption");
			}
		}
		
		#endregion
	}
}
