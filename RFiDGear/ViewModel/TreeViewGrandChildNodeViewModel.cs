using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using MvvmDialogs.ViewModels;
using RFiDGear.DataAccessLayer;
using RFiDGear.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Xml.Serialization;

namespace RFiDGear.ViewModel
{
    /// <summary>
    /// Description of TreeViewGrandChildNodeViewModel.
    /// </summary>
    [XmlRootAttribute("TreeViewGrandChildNode", IsNullable = false)]
    public class TreeViewGrandChildNodeViewModel : ViewModelBase, IUserDialogViewModel
    {
        private MifareClassicSetupViewModel setupViewModel;

        #region Constructors

        public TreeViewGrandChildNodeViewModel()
        {
            dataBlockContent = new MifareClassicDataBlockModel();
            dataBlockContent.Data = new byte[16];

            children = new ObservableCollection<TreeViewGrandGrandChildNodeViewModel>();
        }

        public TreeViewGrandChildNodeViewModel(MifareClassicDataBlockModel dataBlock, MifareClassicSetupViewModel _setupViewModel)
        {
            if (dataBlock != null)
                dataBlockContent = dataBlock;
            else
            {
                dataBlockContent = new MifareClassicDataBlockModel();
                dataBlockContent.Data = new byte[16];
            }

            setupViewModel = _setupViewModel;

            IsVisible = true;

            dataBlockContent.dataBlockNumber = dataBlock.dataBlockNumber;

            DataBlockAsHexString = "00000000000000000000000000000000";
            DataBlockAsCharString = "\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0";

            IsValidDataBlockContent = null;

            children = new ObservableCollection<TreeViewGrandGrandChildNodeViewModel>();
        }

        public TreeViewGrandChildNodeViewModel(MifareClassicDataBlockModel dataBlock, TreeViewChildNodeViewModel parentSector, CARD_TYPE cardType, int sectorNumber, bool _isDataBlock)
        {
            if (dataBlock != null)
                dataBlockContent = dataBlock;
            else
            {
                dataBlockContent = new MifareClassicDataBlockModel();
                dataBlockContent.Data = new byte[16];
            }

            isDataBlock = _isDataBlock;
            IsVisible = true;

            parent = parentSector;
            dataBlockContent.dataBlockNumber = dataBlock.dataBlockNumber;

            DataBlockAsHexString = "00000000000000000000000000000000";
            DataBlockAsCharString = "\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0";

            IsValidDataBlockContent = null;

            tag = String.Format("{0}:{1}", parentSector.ParentUid, parentSector.SectorNumber);

            children = new ObservableCollection<TreeViewGrandGrandChildNodeViewModel>();
        }

        public TreeViewGrandChildNodeViewModel(MifareDesfireFileModel _desfireFile)
        {
            desfireFile = _desfireFile;
            children = new ObservableCollection<TreeViewGrandGrandChildNodeViewModel>();
        }

        public TreeViewGrandChildNodeViewModel(string _displayItem)
        {
            children = new ObservableCollection<TreeViewGrandGrandChildNodeViewModel>();
            grandChildNodeHeader = _displayItem;
            IsDataBlock = false;
            isVisible = false;
        }

        #endregion Constructors

        #region Dialogs

        private ObservableCollection<IDialogViewModel> dialogs;

        #endregion Dialogs

        #region SelectedItem

        [XmlIgnore]
        public object SelectedItem
        {
            get { return selectedItem; }
            set
            {
                selectedItem = value;
            }
        }

        private object selectedItem;

        #endregion SelectedItem

        #region Parent

        public TreeViewChildNodeViewModel Parent
        {
            get { return parent; }
        }

        private readonly TreeViewChildNodeViewModel parent;

        #endregion Parent

        /// <summary>
        ///
        /// </summary>
        public ObservableCollection<TreeViewGrandGrandChildNodeViewModel> Children
        {
            get { return children; }
            set
            {
                children = value;
                RaisePropertyChanged("Children");
            }
        }

        private ObservableCollection<TreeViewGrandGrandChildNodeViewModel> children;

        #region (Dependency) Properties

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public string DataBlockAsCharString
        {
            get
            {
                if (DataBlockContent.Length == 16 && dataBlockAsCharString.Length == 16)
                {
                    char[] tempString = new char[DataBlockContent.Length];
                    for (int i = 0; i < DataBlockContent.Length; i++)
                    {
                        if (DataBlockContent[i] < 27 | DataBlockContent[i] > 127)
                            tempString[i] = (char)248;
                        else
                            tempString[i] = (char)DataBlockContent[i];
                    }

                    dataBlockAsCharString = new string(tempString);
                }
                return dataBlockAsCharString;
            }
            set
            {
                dataBlockAsCharString = value;

                if (dataBlockAsCharString.Length == 16)
                {
                    char[] tempString = value.ToCharArray();

                    try
                    {
                        for (int i = 0; i < DataBlockContent.Length; i++)
                        {
                            if (
                                ((char)DataBlockContent[i] != value[i])
                                && (
                                    (!((char)DataBlockContent[i] < 27 | (char)DataBlockContent[i] > 127))//do not perform overwrite datablockat position 'i' if non printable character...
                                    || (value[i] > 27 && value[i] < 127) //..except if a printable character was entered at the same position
                                ))
                            {
                                DataBlockContent[i] = (byte)value[i];
                                //tempString[i] = (char)DataBlockContent[i];
                            }
                        }
                        //dataBlockAsCharString = new string(tempString);
                        //DataBlockContent = Encoding.UTF8.GetBytes(dataBlockAsCharString);
                    }
                    catch
                    {
                        IsValidDataBlockContent = false;
                        IsTask = false;
                        return;
                    }
                    IsValidDataBlockContent = null;
                    IsTask = true;
                }
                else
                {
                    IsTask = false;
                    IsValidDataBlockContent = false;
                }

                RaisePropertyChanged("DataBlockAsCharString");
                RaisePropertyChanged("DataBlockAsHexString");
                RaisePropertyChanged("DataBlockContent");
            }
        }

        private string dataBlockAsCharString;

        /// <summary>
        /// DependencyProperty
        /// </summary>

        public string DataBlockAsHexString
        {
            get
            {
                if (DataBlockContent.Length == 16)
                {
                    dataBlockAsHexString = CustomConverter.HexToString(DataBlockContent);
                }

                return dataBlockAsHexString;
            }
            set
            {
                int discardedChars = 0;
                DataBlockContent = CustomConverter.GetBytes(value, out discardedChars);

                if (discardedChars == 0 && value.Length == 32)
                {
                    IsValidDataBlockContent = null;
                    IsTask = true;
                }
                else
                {
                    IsValidDataBlockContent = false;
                    IsTask = false;
                }

                dataBlockAsHexString = value;

                RaisePropertyChanged("DataBlockAsHexString");
                RaisePropertyChanged("DataBlockAsCharString");
            }
        }

        private string dataBlockAsHexString;

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public bool? IsValidDataBlockContent
        {
            get { return isValidDataBlockContent; }
            set
            {
                isValidDataBlockContent = value;

                RaisePropertyChanged("IsValidDataBlockContent");
            }
        }

        private bool? isValidDataBlockContent;

        [XmlIgnore]
        public byte[] DataBlockContent
        {
            get { return dataBlockContent.Data; }       //_dataBlock != null ? _dataBlock.Data : new byte[16]; }
            set
            {
                dataBlockContent.Data = value;
                RaisePropertyChanged("DataBlockContent");
                RaisePropertyChanged("DataBlockAsHexString");
                RaisePropertyChanged("DataBlockAsCharString");
            }
        }

        private MifareClassicDataBlockModel dataBlockContent;

        public MifareDesfireFileModel DesfireFile
        {
            get { return desfireFile; }
            set
            {
                desfireFile = value;
                RaisePropertyChanged("DesfireFile");
            }
        }

        private MifareDesfireFileModel desfireFile;

        [XmlIgnore]
        public string Tag
        {
            get { return tag; }
        }

        private readonly string tag;

        [XmlIgnore]
        public string GrandChildNodeHeader
        {
            get
            {
                if (dataBlockContent != null)
                    return String.Format("Block: [{0}]", dataBlockContent.dataBlockNumber);
                else if (desfireFile != null)
                    return string.Format("FileNo: [{0}]", desfireFile.FileID.ToString("D3"));
                return grandChildNodeHeader;
            }
        }

        private string grandChildNodeHeader;

        /// <summary>
        ///
        /// </summary>
        public int SelectedDataLength
        {
            get { return selectedDataLength; }
            set
            {
                selectedDataLength = value;

                if (value % 2 == 0 && value < 100 && selectedDataIndexStart % 2 == 0)
                {
                    IsValidSelectedDataIndexAndLength = true;
                }
                else if (value % 2 == 0 && value >= 100)
                {
                    selectedDataLength -= 100;
                    IsValidSelectedDataIndexAndLength = true;
                }
                else if (value % 2 == 1)
                {
                    //selectedDataLength = value - 1;
                    IsValidSelectedDataIndexAndLength = false;
                }
                RaisePropertyChanged("SelectedDataLength");
            }
        }

        private int selectedDataLength;

        /// <summary>
        ///
        /// </summary>
        public int SelectedDataIndexStart
        {
            get { return selectedDataIndexStart; }
            set
            {
                selectedDataIndexStart = value;

                if (value % 2 == 0 && value < 100)
                {
                    IsValidSelectedDataIndexAndLength = true;
                }
                else if (value % 2 == 0 && value >= 100)
                {
                    selectedDataIndexStart -= 100;
                    IsValidSelectedDataIndexAndLength = true;
                }
                else if (value % 2 == 1)
                {
                    //selectedDataIndexStart = 0;
                    IsValidSelectedDataIndexAndLength = false;
                }

                RaisePropertyChanged("SelectedDataIndexStart");
            }
        }

        private int selectedDataIndexStart;

        /// <summary>
        ///
        /// </summary>
        public int DataBlockNumber
        {
            get { return dataBlockContent != null ? dataBlockContent.dataBlockNumber : 0; }
            set
            {
                dataBlockContent.dataBlockNumber = value;
                RaisePropertyChanged("DataBlockNumber");
                RaisePropertyChanged("GrandChildNodeHeader");
            }
        }

        #endregion (Dependency) Properties

        #region View Switches

        public bool IsExpanded
        {
            get { return isExpanded; }
            set
            {
                if (value != isExpanded)
                {
                    isExpanded = value;
                    this.RaisePropertyChanged("IsExpanded");
                }

                // Expand all the way up to the root.
                if (parent != null)
                    parent.IsExpanded = true;
            }
        }

        private bool isExpanded;

        public bool IsSelected
        {
            get { return isSelected; }
            set
            {
                if (value != isSelected)
                {
                    isSelected = value;
                    RaisePropertyChanged("IsSelected");
                }
            }
        }

        private bool isSelected;

        public bool? IsValidSelectedDataIndexAndLength
        {
            get { return isValidSelectedDataIndexAndLength; }
            set
            {
                isValidSelectedDataIndexAndLength = value;
                RaisePropertyChanged("IsValidSelectedDataIndexAndLength");
            }
        }

        private bool? isValidSelectedDataIndexAndLength;

        public bool IsFocused
        {
            get { return isFocused; }
            set
            {
                if (value != isFocused)
                {
                    isFocused = value;
                    RaisePropertyChanged("IsFocused");
                }
            }
        }

        private bool isFocused;

        public bool? IsAuthenticated
        {
            get { return isAuth; }
            set
            {
                isAuth = value;
                RaisePropertyChanged("IsAuthenticated");
            }
        }

        private bool? isAuth;

        public bool? IsTask
        {
            get { return isTask; }
            set
            {
                isTask = value;
                RaisePropertyChanged("IsTask");
            }
        }

        private bool? isTask;

        public bool? IsVisible
        {
            get { return isVisible; }
            set
            {
                isVisible = value;
                RaisePropertyChanged("IsVisible");
            }
        }

        private bool? isVisible;

        public bool IsDataBlock
        {
            get { return isDataBlock; }
            set
            {
                isDataBlock = value;
                RaisePropertyChanged("IsDataBlock");
            }
        }

        private bool isDataBlock;

        #endregion View Switches

        #region IUserDialogViewModel Implementation

        [XmlIgnore]
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
        [XmlIgnore]
        public Action<TreeViewGrandChildNodeViewModel> OnOk { get; set; }

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public Action<TreeViewGrandChildNodeViewModel> OnCancel { get; set; }

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public Action<TreeViewGrandChildNodeViewModel> OnCloseRequest { get; set; }

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