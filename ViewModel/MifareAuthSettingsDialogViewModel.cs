using GalaSoft.MvvmLight;
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
		private DatabaseReaderWriter databaseReaderWriter;
		
		
		private readonly string[] datablockSelectorSeparatedCmbbxItems = {
			"DataBlock 0",
			"DataBlock 1",
			"DataBlock 2",
		};
		
		private readonly string[] keyCmbbxItems = {
			"No. 0",
			"No. 1",
			"No. 2",
			"No. 3",
			"No. 4",
			"No. 5",
			"No. 6",
			"No. 7",
			"No. 8",
			"No. 9",
			"No. 10",
			"No. 11",
			"No. 12",
			"No. 13",
			"No. 14",
			"No. 15"
		};
		
		private int selectionIndex;
		
		private bool isClassicAuthInfo = true;
		
		private readonly ObservableCollection<MifareClassicAccessBitsSectorTrailerDataGridModel> displaySourceForSectorTrailerDataGrid;
		private readonly ObservableCollection<MifareClassicAccessBitsLongDataBlockDataGridModel> displaySourceForLongDataBlockDataGrid;
		private readonly ObservableCollection<MifareClassicAccessBitsShortDataBlockDataGridModel> displaySourceForShortDataBlockDataGrid;
		
		private ObservableCollection<object> displaySourceForCombinedDataBlockDataGrid;
		
		private MifareClassicAccessBitsSectorTrailerDataGridModel selectedSectorTrailerAccessBitsItem;
		private object selectedCombinedDataBlockAccessBitsDataGridItem;
		private object selectedDataBlock0AccessBitsDataGridItem;
		private object selectedDataBlock1AccessBitsDataGridItem;
		private object selectedDataBlock2AccessBitsDataGridItem;
		
		public object ViewModelContext { get; set; }
		
		public MifareAuthSettingsDialogViewModel(object treeViewViewModel, bool isModal = true)
		{
			_dataBlockIsCombinedToggleButtonIsChecked = true;
			databaseReaderWriter = new DatabaseReaderWriter();
			ViewModelContext = treeViewViewModel;
			
			displaySourceForSectorTrailerDataGrid = new ObservableCollection<MifareClassicAccessBitsSectorTrailerDataGridModel>();
			displaySourceForLongDataBlockDataGrid = new ObservableCollection<MifareClassicAccessBitsLongDataBlockDataGridModel>();
			displaySourceForShortDataBlockDataGrid = new ObservableCollection<MifareClassicAccessBitsShortDataBlockDataGridModel>();
			
			foreach (string accessConditions in MifareClassicAccessBitsBaseModel.sectorTrailerAB) {
				displaySourceForSectorTrailerDataGrid.Add(new MifareClassicAccessBitsSectorTrailerDataGridModel(accessConditions));
			}

			foreach (string accessConditions in MifareClassicAccessBitsBaseModel.dataBlockAB) {
				displaySourceForLongDataBlockDataGrid.Add(new MifareClassicAccessBitsLongDataBlockDataGridModel(accessConditions));
			}
			

			foreach (string accessConditions in MifareClassicAccessBitsBaseModel.dataBlockABs) {
				if (!String.IsNullOrEmpty(accessConditions))
					displaySourceForShortDataBlockDataGrid.Add(new MifareClassicAccessBitsShortDataBlockDataGridModel(accessConditions));
			}
			
			// select the default items from xml-file based Database
			displaySourceForCombinedDataBlockDataGrid = new ObservableCollection<object>(displaySourceForShortDataBlockDataGrid);
			
			foreach (MifareClassicAccessBitsSectorTrailerDataGridModel item in displaySourceForSectorTrailerDataGrid) {
				if (item.GetSectorAccessBitsFromHumanReadableFormat() ==
				    (ViewModelContext as TreeViewChildNodeViewModel)._sectorModel.sectorAccessBits.DecodedSectorTrailerAccessBits) //"N,A,A,A,A,A"
					selectedSectorTrailerAccessBitsItem = item;
				
			}
			
			foreach (MifareClassicAccessBitsShortDataBlockDataGridModel item in displaySourceForCombinedDataBlockDataGrid) {
				if (item.GetShortDataBlockAccessBitsFromHumanReadableFormat() ==
				    (ViewModelContext as TreeViewChildNodeViewModel)._sectorModel.sectorAccessBits.DecodedDataBlock0AccessBits) //"A,A,A,A"
				{
					selectedCombinedDataBlockAccessBitsDataGridItem = item;
					selectedDataBlock0AccessBitsDataGridItem = item;
					selectedDataBlock1AccessBitsDataGridItem = item;
					selectedDataBlock2AccessBitsDataGridItem = item;
				}

			}
			this.IsModal = isModal;
		}

		#region SelectedItem
		
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
				if(_dataBlockIsCombinedToggleButtonIsChecked)
					return selectedCombinedDataBlockAccessBitsDataGridItem;
				
				switch (selectionIndex) {
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
				if(_dataBlockIsCombinedToggleButtonIsChecked)
					selectedCombinedDataBlockAccessBitsDataGridItem = value;
				
				else
				{
					switch (selectionIndex) {
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
			get { return _dataBlockIsCombinedToggleButtonIsChecked ? "Combined" : datablockSelectorSeparatedCmbbxItems[selectionIndex]; }
			set {
				if(value != "Combined")
					selectionIndex = Array.IndexOf(datablockSelectorSeparatedCmbbxItems, value);
				RaisePropertyChanged("DataBlockSource");
				RaisePropertyChanged("SelectedDataBlockAccessBitsRow");
				RaisePropertyChanged("SectorTrailerTextBoxText");
			}
		}
		
		#endregion //SelectedItem
		
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
		public Action<MifareAuthSettingsDialogViewModel> OnAuth {get; set; }
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

		#endregion // INotifyPropertyChanged Members
		
		private string _Caption;
		public string Caption {
			get { return _Caption; }
			set {
				_Caption = value;
				RaisePropertyChanged("Caption");
			}
		}
		
		public ObservableCollection<MifareClassicAccessBitsSectorTrailerDataGridModel> SectorTrailerSource {
			get { return displaySourceForSectorTrailerDataGrid; }
		}
		
		public bool IsClassicAuthInfoEnabled {
			get { return isClassicAuthInfo; }
			set {
				isClassicAuthInfo = value;
				RaisePropertyChanged("IsClassicAuthInfoEnabled");
			}
		}
		
		private bool _dataBlockIsCombinedToggleButtonIsChecked;
		public bool DataBlockIsCombinedToggleButtonIsChecked{
			get { return _dataBlockIsCombinedToggleButtonIsChecked; }
			set { _dataBlockIsCombinedToggleButtonIsChecked = value; selectionIndex=0; RaisePropertyChanged("SelectedDataBlockItem"); RaisePropertyChanged("DataBlockSelectionComboBoxIsEnabled"); RaisePropertyChanged("DataBlockSelection");}
		}
		
		public bool DataBlockSelectionComboBoxIsEnabled{
			get { return !_dataBlockIsCombinedToggleButtonIsChecked; }
		}
		
		public bool IsDesfireAuthInfoEnabled {
			get { return !isClassicAuthInfo; }
			set {
				isClassicAuthInfo = !value;
				RaisePropertyChanged("IsDesfireAuthInfoEnabled");
			}
		}
		
		public ObservableCollection<object> DataBlockSource {
			get {
				
				if(_dataBlockIsCombinedToggleButtonIsChecked)
				{
					if(ViewModelContext is TreeViewChildNodeViewModel){
						if(selectedCombinedDataBlockAccessBitsDataGridItem is MifareClassicAccessBitsShortDataBlockDataGridModel){
							(ViewModelContext as TreeViewChildNodeViewModel)._sectorModel.sectorAccessBits.SectorTrailerAccessBits = (selectedSectorTrailerAccessBitsItem as MifareClassicAccessBitsSectorTrailerDataGridModel)
								.ProcessSectorTrailerEncoding((selectedCombinedDataBlockAccessBitsDataGridItem as MifareClassicAccessBitsShortDataBlockDataGridModel).GetShortDataBlockAccessBitsFromHumanReadableFormat()
								                              , (selectedCombinedDataBlockAccessBitsDataGridItem as MifareClassicAccessBitsShortDataBlockDataGridModel).GetShortDataBlockAccessBitsFromHumanReadableFormat()
								                              , (selectedCombinedDataBlockAccessBitsDataGridItem as MifareClassicAccessBitsShortDataBlockDataGridModel).GetShortDataBlockAccessBitsFromHumanReadableFormat()).SectorTrailerAccessBits;
							RaisePropertyChanged("SectorTrailerTextBoxText");
						}
						else if(selectedCombinedDataBlockAccessBitsDataGridItem is MifareClassicAccessBitsLongDataBlockDataGridModel){
							(ViewModelContext as TreeViewChildNodeViewModel)._sectorModel.sectorAccessBits.SectorTrailerAccessBits = (selectedSectorTrailerAccessBitsItem as MifareClassicAccessBitsSectorTrailerDataGridModel)
								.ProcessSectorTrailerEncoding((selectedCombinedDataBlockAccessBitsDataGridItem as MifareClassicAccessBitsLongDataBlockDataGridModel).GetLongDataBlockAccessBitsFromHumanReadableFormat()
								                              , (selectedCombinedDataBlockAccessBitsDataGridItem as MifareClassicAccessBitsLongDataBlockDataGridModel).GetLongDataBlockAccessBitsFromHumanReadableFormat()
								                              , (selectedCombinedDataBlockAccessBitsDataGridItem as MifareClassicAccessBitsLongDataBlockDataGridModel).GetLongDataBlockAccessBitsFromHumanReadableFormat()).SectorTrailerAccessBits;
							RaisePropertyChanged("SectorTrailerTextBoxText");
						}

					}

					return displaySourceForCombinedDataBlockDataGrid;
				}
				else
				{
					switch (selectionIndex) {

						case 2:
							if(selectedDataBlock0AccessBitsDataGridItem != null && selectedDataBlock1AccessBitsDataGridItem != null && selectedDataBlock2AccessBitsDataGridItem != null)
							{
								if(selectedDataBlock2AccessBitsDataGridItem is MifareClassicAccessBitsShortDataBlockDataGridModel)
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
							if(selectedDataBlock0AccessBitsDataGridItem != null && selectedDataBlock1AccessBitsDataGridItem != null && selectedDataBlock2AccessBitsDataGridItem != null)
							{
								if(selectedDataBlock1AccessBitsDataGridItem is MifareClassicAccessBitsShortDataBlockDataGridModel)
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
							if(selectedDataBlock0AccessBitsDataGridItem != null && selectedDataBlock1AccessBitsDataGridItem != null && selectedDataBlock2AccessBitsDataGridItem != null)
							{
								if(selectedDataBlock0AccessBitsDataGridItem is MifareClassicAccessBitsShortDataBlockDataGridModel)
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
		
		public ObservableCollection<string> DataBlockSelection {
			get { return _dataBlockIsCombinedToggleButtonIsChecked ? new ObservableCollection<string>(new string[1] {"Combined"}) : new ObservableCollection<string>(datablockSelectorSeparatedCmbbxItems); }
		}
		
		public ObservableCollection<string> KeySelection {
			get { return new ObservableCollection<string>(keyCmbbxItems); }
		}
		
		public int KeySelectionComboboxIndex {
			get {
				if (ViewModelContext is TreeViewChildNodeViewModel)
					return (ViewModelContext as TreeViewChildNodeViewModel)._sectorModel.mifareClassicSectorNumber;
				else
					return 0;
			}
		}
		
		public bool? IsFixedKeyNumber {
			get {
				if (ViewModelContext is TreeViewChildNodeViewModel)
					return false;
				else
					return true;
			}
		}
		
		public string SectorTrailerTextBoxText {
			get { return String.Format("{0},{1},{2}"
			                           ,(ViewModelContext as TreeViewChildNodeViewModel)._sectorModel.sectorAccessBits.sectorKeyAKey
			                           ,(ViewModelContext as TreeViewChildNodeViewModel)._sectorModel.sectorAccessBits.SectorTrailerAccessBits
			                           ,(ViewModelContext as TreeViewChildNodeViewModel)._sectorModel.sectorAccessBits.sectorKeyBKey);}
		}
		// TODO Add Keys to KeySetup Dialog. Keys are published as follows: 1. add sector trailer to db. 2. add st to model in databasereaderwriter class
		public string selectedClassicKeyAKey {
			get { return (ViewModelContext as TreeViewChildNodeViewModel)._sectorModel.sectorAccessBits.sectorKeyAKey.Replace(" ",""); }
			set { CustomConverter conv = new CustomConverter();
				conv.classicKeyToEdit = value;
				if(conv.FormatMifareClassicKeyStringWithSpacesEachByte(value) == KEY_ERROR.NO_ERROR)
					(ViewModelContext as TreeViewChildNodeViewModel)._sectorModel.sectorAccessBits.sectorKeyAKey = conv.classicKeyToEdit; }
		}
		
		public string selectedClassicKeyBKey {
			get { return (ViewModelContext as TreeViewChildNodeViewModel)._sectorModel.sectorAccessBits.sectorKeyBKey.Replace(" ",""); }
			set { CustomConverter conv = new CustomConverter();
				conv.classicKeyToEdit = value;
				if(conv.FormatMifareClassicKeyStringWithSpacesEachByte(value) == KEY_ERROR.NO_ERROR)
					(ViewModelContext as TreeViewChildNodeViewModel)._sectorModel.sectorAccessBits.sectorKeyBKey = conv.classicKeyToEdit; }
		}
		
		public MifareClassicAccessBitsSectorTrailerDataGridModel SelectedSectorTrailerAccessBitsItem {
			get { return selectedSectorTrailerAccessBitsItem; }
			set { selectedSectorTrailerAccessBitsItem = value; }
		}
	}
}
