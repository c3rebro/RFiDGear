using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using MvvmDialogs.ViewModels;
using RFiDGear.DataAccessLayer;
using RFiDGear.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        private readonly ResourceLoader resLoader = new ResourceLoader();
        private readonly TreeViewParentNodeViewModel _parent;
        private readonly CARD_TYPE _cardType;
        private readonly RelayCommand _cmdReadSectorWithCustoms;
        private readonly RelayCommand _cmdReadSectorWithDefaults;
        private readonly RelayCommand _cmdEditAuthAndModifySector;
        private readonly string _parentUid;

        private MifareClassicSetupViewModel setupViewModel;
        private MifareClassicSectorModel sectorModel;
        private MifareDesfireAppModel appModel;

        #region Constructors

        public TreeViewChildNodeViewModel()
        {
            sectorModel = new MifareClassicSectorModel(0);
            appModel = new MifareDesfireAppModel();

            children = new ObservableCollection<TreeViewGrandChildNodeViewModel>();
        }

        public TreeViewChildNodeViewModel(
            MifareClassicSectorModel _sectorModel,
            MifareClassicSetupViewModel _setupViewModel)
        {
            sectorModel = _sectorModel;
            setupViewModel = _setupViewModel;

            isTask = true;
            children = new ObservableCollection<TreeViewGrandChildNodeViewModel>();

            LoadChildren();
        }

        public TreeViewChildNodeViewModel(
            MifareClassicSectorModel _sectorModel,
            TreeViewParentNodeViewModel parent,
            CARD_TYPE cardType,
            int _sectorNumber,
            ObservableCollection<IDialogViewModel> _dialogs = null,
            bool? _isTask = null)
        {
            if (_dialogs != null)
                dialogs = _dialogs;

            isTask = _isTask;
            //device = _device;
            sectorModel = _sectorModel;
            sectorModel.SectorNumber = _sectorNumber;
            _cardType = cardType;
            _parent = parent;

            _cmdReadSectorWithDefaults = new RelayCommand(ReadSectorWithDefaults);
            _cmdEditAuthAndModifySector = new RelayCommand(ReadSectorWithCustoms);
            _cmdReadSectorWithCustoms = new RelayCommand(ReadSectorWithCustoms);

            ContextMenuItems = new List<MenuItem>();
            ContextMenuItems.Add(new MenuItem()
            {
                Header = "Read Sector with default Keys",
                Command = _cmdReadSectorWithDefaults
            });

            ContextMenuItems.Add(new MenuItem()
            {
                Header = "Read Sector with custom Keys",
                Command = _cmdReadSectorWithCustoms
            });

            children = new ObservableCollection<TreeViewGrandChildNodeViewModel>();

            if (_cardType == CARD_TYPE.Mifare1K || _cardType == CARD_TYPE.Mifare2K || _cardType == CARD_TYPE.Mifare4K)
            {
                if (isTask == true)
                    ChildNodeHeader = String.Format("*Sector: [{0}]", sectorModel.SectorNumber);
                else
                    ChildNodeHeader = String.Format("Sector: [{0}]", sectorModel.SectorNumber);
            }
            else
            {
                if (isTask == true)
                    ChildNodeHeader = String.Format("*AppID: {0}", appModel.appID);
                else
                    ChildNodeHeader = String.Format("AppID: {0}", appModel.appID);
            }

            LoadChildren();
        }

        public TreeViewChildNodeViewModel(
            MifareDesfireAppModel appID,
            TreeViewParentNodeViewModel parentUID,
            CARD_TYPE cardType,
            ObservableCollection<IDialogViewModel> _dialogs = null,
            bool? _isTask = null)
        {
            if (_dialogs != null)
                dialogs = _dialogs;

            isTask = _isTask;

            //device = _device;
            appModel = appID;
            _cardType = cardType;
            _parentUid = parentUID.UidNumber;

            _cmdReadSectorWithDefaults = new RelayCommand(ReadSectorWithDefaults);
            _cmdEditAuthAndModifySector = new RelayCommand(ReadSectorWithCustoms);

            ContextMenuItems = new List<MenuItem>();
            ContextMenuItems.Add(new MenuItem()
            {
                Header = "Read Sector using default Configuration",
                Command = _cmdReadSectorWithDefaults
            });

            ContextMenuItems.Add(new MenuItem()
            {
                Header = "Edit Authentication Settings and Modify Sector",
                Command = _cmdEditAuthAndModifySector
            });

            children = new ObservableCollection<TreeViewGrandChildNodeViewModel>();

            if (_cardType == CARD_TYPE.Mifare1K || _cardType == CARD_TYPE.Mifare2K || _cardType == CARD_TYPE.Mifare4K)
            {
                if (isTask == true)
                    ChildNodeHeader = String.Format("*Sector: [{0}]", sectorModel.SectorNumber);
                else
                    ChildNodeHeader = String.Format("Sector: [{0}]", sectorModel.SectorNumber);
            }
            else
            {
                if (isTask == true)
                    ChildNodeHeader = String.Format("*AppID: {0}", appModel.appID);
                else
                    ChildNodeHeader = String.Format("AppID: {0}", appModel.appID);
            }

            LoadChildren();
        }

        public TreeViewChildNodeViewModel(string _childNodeHeader)
        {
            ChildNodeHeader = _childNodeHeader;
        }

        #endregion Constructors

        #region Dialogs

        private ObservableCollection<IDialogViewModel> dialogs;

        #endregion Dialogs

        #region Context Menu Items

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public List<MenuItem> ContextMenu
        {
            get { return ContextMenuItems; }
        }

        private readonly List<MenuItem> ContextMenuItems;

        public void ReadSectorWithDefaults()
        {
        }

        public void ReadSectorWithCustoms()
        {
            using (RFiDDevice device = new RFiDDevice())
            {
                IsSelected = true;

                this.dialogs.Add(new MifareClassicSetupViewModel(this, dialogs)
                {
                    Caption = String.Format("{0} UID:[{1}] Type:[{2}]",
                                                             ResourceLoader.getResource("mifareAuthSettingsDialogCaption"),
                                                             this.Parent.UidNumber,
                                                             this.Parent.CardType),
                    //ViewModelContext = this,
                    IsClassicAuthInfoEnabled = true, //content.Contains("EditAccessBits"),

                    OnOk = (sender) =>
                    {
                        //databaseReaderWriter.WriteDatabase((sender.ViewModelContext as TreeViewChildNodeViewModel)._sectorModel);
                    },

                    OnCancel = (sender) =>
                    {
                        sender.Close();
                    },

                    OnAuth = (sender) =>
                    {
                        //readerModel.ReadMiFareClassicSingleSector(sectorVM.SectorNumber, sender.selectedClassicKeyAKey, sender.selectedClassicKeyBKey);
                        this.IsAuthenticated = device.SectorSuccesfullyAuth;
                        foreach (TreeViewGrandChildNodeViewModel gcVM in this.Children)
                        {
                            gcVM.IsAuthenticated = device.DataBlockSuccesfullyAuth[
                                (((this.SectorNumber + 1) * this.BlockCount) - (this.BlockCount - gcVM.DataBlockNumber))];
                            gcVM.DataBlockContent = device.currentSector[gcVM.DataBlockNumber];
                        }
                    },

                    OnCloseRequest = (sender) =>
                    {
                        sender.Close();
                    }
                });
            }
        }

        public void EditAccessBits()
        {
            IsSelected = true;
        }

        #endregion Context Menu Items



        #region Selected Items

        /// <summary>
        ///
        /// </summary>
        public object SelectedItem
        {
            get { return selectedItem; }
            set
            {
                if (selectedItem != value)
                {
                    selectedItem = value;
                }
            }
        }

        private object selectedItem;

        #endregion Selected Items

        #region Items Sources

        /// <summary>
        ///
        /// </summary>
        public ObservableCollection<TreeViewGrandChildNodeViewModel> Children
        {
            get { return children; }
            set
            {
                children = value;
                RaisePropertyChanged("Children");
            }
        }

        private ObservableCollection<TreeViewGrandChildNodeViewModel> children;

        #endregion Items Sources

        #region Parent

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public TreeViewParentNodeViewModel Parent
        {
            get { return _parent; }
        }

        #endregion Parent

        #region View Switchers

        /// <summary>
        ///
        /// </summary>
        public bool IsSelected
        {
            get { return isSelected; }
            set
            {
                if (value != isSelected)
                {
                    isSelected = value;
                    RaisePropertyChanged("IsSelected");

                    SelectedItem = this;
                }
            }
        }

        private bool isSelected;

        /// <summary>
        ///
        /// </summary>
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
                if (isExpanded && _parent != null)
                    _parent.IsExpanded = true;
            }
        }

        private bool isExpanded;

        /// <summary>
        ///
        /// </summary>
        public bool? HasChanged
        {
            get { return hasChanged; }
            set
            {
                hasChanged = value;
                RaisePropertyChanged("HasChanged");
            }
        }

        private bool? hasChanged;

        /// <summary>
        ///
        /// </summary>
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

        /// <summary>
        ///
        /// </summary>
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

        #endregion View Switchers

        #region Properties

        /// <summary>
        ///
        /// </summary>
        public int SectorNumber
        {
            get { return sectorModel != null ? sectorModel.SectorNumber : -1; }
            set { sectorModel.SectorNumber = value; }
        }

        /// <summary>
        ///
        /// </summary>
        public int BlockCount
        {
            get { return sectorModel.SectorNumber > 31 ? 16 : 4; }
        }

        /// <summary>
        ///
        /// </summary>
        public uint? AppID
        {
            get
            {
                try
                {
                    return appModel.appID;
                }
                catch
                {
                    return null;
                }
            }
        }

        /// <summary>
        ///
        /// </summary>
        public string ParentUid
        {
            get { return _parentUid; }
        }

        /// <summary>
        ///
        /// </summary>
        public string ChildNodeHeader
        {
            get
            {
                if (_cardType == CARD_TYPE.Mifare1K || _cardType == CARD_TYPE.Mifare2K || _cardType == CARD_TYPE.Mifare4K)
                {
                    if (isTask == true)
                        childNodeHeader = String.Format("*Sector: [{0}]", sectorModel.SectorNumber);
                    else
                        childNodeHeader = String.Format("Sector: [{0}]", sectorModel.SectorNumber);
                }
                else if (_cardType == CARD_TYPE.DESFire || _cardType == CARD_TYPE.DESFireEV1 || _cardType == CARD_TYPE.DESFireEV2)
                {
                    if (isTask == true)
                        childNodeHeader = String.Format("*AppID: {0}", appModel.appID);
                    else
                        childNodeHeader = String.Format("AppID: {0}", appModel.appID);
                }

                return childNodeHeader;
            }
            set
            {
                childNodeHeader = value;
                RaisePropertyChanged("ChildNodeHeader");
            }
        }

        private string childNodeHeader;

        #endregion Properties

        private void LoadChildren()
        {
            switch (_cardType)
            {
                case CARD_TYPE.Mifare1K:
                case CARD_TYPE.Mifare2K:
                    {
                        for (int i = 0; i <= 3; i++)
                        {
                            children.Add(new TreeViewGrandChildNodeViewModel(new MifareClassicDataBlockModel(i), this, _cardType, sectorModel.SectorNumber, true));
                        }
                    }
                    break;

                case CARD_TYPE.Mifare4K:
                    {
                        if (SectorNumber < 32)
                        {
                            for (int i = 0; i <= 3; i++)
                            {
                                children.Add(new TreeViewGrandChildNodeViewModel(new MifareClassicDataBlockModel(i), this, _cardType, sectorModel.SectorNumber, true));
                            }
                        }
                        else
                        {
                            for (int i = 0; i <= 15; i++)
                            {
                                children.Add(new TreeViewGrandChildNodeViewModel(new MifareClassicDataBlockModel(i), this, _cardType, sectorModel.SectorNumber, true));
                            }
                        }
                    }
                    break;

                case CARD_TYPE.Unspecified:
                    for (int i = 0; i <= 3; i++)
                    {
                        children.Add(new TreeViewGrandChildNodeViewModel(new MifareClassicDataBlockModel(i), setupViewModel));
                    }
                    break;
            }
            //foreach (TreeViewGrandChildNodeViewModel item in _children) {
            //	sectorModel.DataBlock.Add(item._dataBlock);
            //}
        }

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
        public Action<TreeViewChildNodeViewModel> OnOk { get; set; }

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public Action<TreeViewChildNodeViewModel> OnCancel { get; set; }

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
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