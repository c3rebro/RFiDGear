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
    /// Description of RFiDChipChildLayerViewModel.
    /// </summary>
    [XmlRootAttribute("TreeViewChildNode", IsNullable = false)]
    public class RFiDChipChildLayerViewModel : ViewModelBase, IUserDialogViewModel
    {
        private readonly ResourceLoader resLoader = new ResourceLoader();
        private readonly RFiDChipParentLayerViewModel _parent;
        private readonly CARD_TYPE _cardType;
        private readonly RelayCommand _cmdReadSectorWithCustoms;
        private readonly RelayCommand _cmdReadSectorWithDefaults;
        private readonly RelayCommand _cmdEditAuthAndModifySector;
        private readonly string _parentUid;

        //private MifareClassicSetupViewModel setupViewModel;
        private MifareClassicSectorModel sectorModel;
        private MifareDesfireAppModel appModel;
        private MifareUltralightPageModel pageModel;

        #region Constructors

        public RFiDChipChildLayerViewModel()
        {
            sectorModel = new MifareClassicSectorModel(0);
            appModel = new MifareDesfireAppModel();

            children = new ObservableCollection<RFiDChipGrandChildLayerViewModel>();
        }

        public RFiDChipChildLayerViewModel(
            MifareClassicSectorModel _sectorModel,
            RFiDChipParentLayerViewModel parent,
            CARD_TYPE cardType,
            ObservableCollection<IDialogViewModel> _dialogs = null,
            bool? _isTask = null)
        {
            if (_dialogs != null)
            {
                dialogs = _dialogs;
            }

            isTask = _isTask;
            sectorModel = _sectorModel;
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

            children = new ObservableCollection<RFiDChipGrandChildLayerViewModel>();

            LoadChildren();
        }

        public RFiDChipChildLayerViewModel(
            MifareDesfireAppModel appID,
            RFiDChipParentLayerViewModel parentUID,
            CARD_TYPE cardType,
            ObservableCollection<IDialogViewModel> _dialogs = null,
            bool? _isTask = null)
        {
            if (_dialogs != null)
            {
                dialogs = _dialogs;
            }

            isTask = _isTask;

            //device = _device;
            appModel = appID;
            _cardType = cardType;
            _parentUid = parentUID != null ? parentUID.UidNumber : null;

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

            children = new ObservableCollection<RFiDChipGrandChildLayerViewModel>();

            LoadChildren();
        }

        public RFiDChipChildLayerViewModel(
            MifareUltralightPageModel _pageModel,
            RFiDChipParentLayerViewModel parentUID,
            CARD_TYPE cardType,
            ObservableCollection<IDialogViewModel> _dialogs = null,
            bool? _isTask = null)
        {
            if (_dialogs != null)
            {
                dialogs = _dialogs;
            }

            isTask = _isTask;

            //device = _device;
            pageModel = _pageModel;
            _cardType = cardType;
            _parentUid = parentUID != null ? parentUID.UidNumber : null;

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

            children = new ObservableCollection<RFiDChipGrandChildLayerViewModel>();

            LoadChildren();
        }

        public RFiDChipChildLayerViewModel(string _childNodeHeader)
        {
            ChildNodeHeader = _childNodeHeader;
        }

        #endregion

        #region Dialogs

        private ObservableCollection<IDialogViewModel> dialogs;

        #endregion Dialogs

        #region Context Menu Items

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public List<MenuItem> ContextMenu => ContextMenuItems;

        private readonly List<MenuItem> ContextMenuItems;

        public void ReadSectorWithDefaults()
        {
        }

        public void ReadSectorWithCustoms()
        {
            using (RFiDDevice device = new RFiDDevice())
            {
                IsSelected = true;

                dialogs.Add(new MifareClassicSetupViewModel(this, dialogs)
                {
                    Caption = String.Format("{0} UID:[{1}] Type:[{2}]",
                                                             ResourceLoader.GetResource("mifareAuthSettingsDialogCaption"),
                                                             Parent.UidNumber,
                                                             Parent.CardType),

                    IsClassicAuthInfoEnabled = true,

                    OnOk = (sender) =>
                    {
                                         //databaseReaderWriter.WriteDatabase((sender.ViewModelContext as RFiDChipChildLayerViewModel)._sectorModel);
                                     },

                    OnCancel = (sender) =>
                    {
                        sender.Close();
                    },

                    OnAuth = (sender) =>
                    {
                                         //readerModel.ReadMiFareClassicSingleSector(sectorVM.SectorNumber, sender.selectedClassicKeyAKey, sender.selectedClassicKeyBKey);
                                         IsAuthenticated = device.SectorSuccesfullyAuth;
                        foreach (RFiDChipGrandChildLayerViewModel gcVM in Children)
                        {
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

        #region Dependency Properties

        /// <summary>
        ///
        /// </summary>
        public object SelectedItem
        {
            get => selectedItem;
            set
            {
                selectedItem = value;
                RaisePropertyChanged("SelectedItem");
            }
        }
        private object selectedItem;

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public RFiDChipParentLayerViewModel Parent => _parent;

        /// <summary>
        ///
        /// </summary>
        public ObservableCollection<RFiDChipGrandChildLayerViewModel> Children
        {
            get => children;
            set
            {
                children = value;
                RaisePropertyChanged("Children");
            }
        }
        private ObservableCollection<RFiDChipGrandChildLayerViewModel> children;

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public bool IsSelected
        {
            get => isSelected;
            set
            {
                isSelected = value;
                RaisePropertyChanged("IsSelected");
            }
        }
        private bool isSelected;

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public bool IsExpanded
        {
            get => isExpanded;
            set
            {
                isExpanded = value;
                RaisePropertyChanged("IsExpanded");

                // Expand all the way up to the root.
                if (isExpanded && _parent != null)
                {
                    _parent.IsExpanded = true;
                }
            }
        }
        private bool isExpanded;

        /// <summary>
        ///
        /// </summary>
        public bool? HasChanged
        {
            get => hasChanged;
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
            get => isTask;
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
            get => isAuth;
            set
            {
                isAuth = value;
                RaisePropertyChanged("IsAuthenticated");
            }
        }
        private bool? isAuth;

        /// <summary>
        ///
        /// </summary>
        public int SectorNumber
        {
            get => sectorModel != null ? sectorModel.SectorNumber : -1;
            set
            {
                if (value >= 0)
                {
                    sectorModel.SectorNumber = value;
                }
            }
        }

        /// <summary>
        ///
        /// </summary>
        public int PageNumber
        {
            get => pageModel != null ? pageModel.PageNumber : -1;
            set
            {
                if (value >= 0)
                {
                    pageModel.PageNumber = value;
                }
            }
        }

        /// <summary>
        ///
        /// </summary>
        public int BlockCount => sectorModel != null ? sectorModel.SectorNumber > 31 ? 16 : 4 : -1;

        /// <summary>
        ///
        /// </summary>
        public uint? AppID
        {
            get
            {
                try
                {
                    return appModel != null ? appModel.appID : 999999;
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
        public string ParentUid => _parentUid;

        /// <summary>
        ///
        /// </summary>
        public string ChildNodeHeader
        {
            get
            {
                if (_cardType == CARD_TYPE.Mifare1K || _cardType == CARD_TYPE.Mifare2K || _cardType == CARD_TYPE.Mifare4K)
                {
                    childNodeHeader = string.Format("Sector: [{0}]", sectorModel.SectorNumber);
                }
                else if (_cardType == CARD_TYPE.DESFire || _cardType == CARD_TYPE.DESFireEV1 || _cardType == CARD_TYPE.DESFireEV2)
                {
                    childNodeHeader = string.Format("AppID: {0}", appModel.appID);
                }
                else if (_cardType == CARD_TYPE.MifareUltralight)
                {
                    childNodeHeader = string.Format("Page: {0}", pageModel.PageNumber);
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

        #endregion

        private void LoadChildren()
        {
            switch (_cardType)
            {
                case CARD_TYPE.Mifare1K:
                case CARD_TYPE.Mifare2K:
                    {
                        for (int i = 0; i <= 3; i++)
                        {
                            children.Add(new RFiDChipGrandChildLayerViewModel(new MifareClassicDataBlockModel(0, i), this));
                        }
                    }
                    break;

                case CARD_TYPE.Mifare4K:
                    {
                        if (SectorNumber < 32)
                        {
                            for (int i = 0; i <= 3; i++)
                            {
                                children.Add(new RFiDChipGrandChildLayerViewModel(new MifareClassicDataBlockModel(0, i), this));
                            }
                        }
                        else
                        {
                            for (int i = 0; i <= 15; i++)
                            {
                                children.Add(new RFiDChipGrandChildLayerViewModel(new MifareClassicDataBlockModel(0, i), this));
                            }
                        }
                    }
                    break;

                case CARD_TYPE.DESFire:
                case CARD_TYPE.DESFireEV1:
                case CARD_TYPE.DESFireEV2:
                    {
                        //children.Add(new RFiDChipGrandChildLayerViewModel(new MifareDesfireFileModel(), this));
                    }
                    break;

                case CARD_TYPE.Unspecified: //TODO: Add Card Type "TASK_MF_Classic" for every type
                    for (int i = 0; i <= 3; i++)
                    {
                        children.Add(new RFiDChipGrandChildLayerViewModel(new MifareClassicDataBlockModel(0, i), new MifareClassicSetupViewModel()));
                    }
                    break;
            }
        }

        #region IUserDialogViewModel Implementation

        [XmlIgnore]
        public bool IsModal { get; private set; }

        public virtual void RequestClose()
        {
            if (OnCloseRequest != null)
            {
                OnCloseRequest(this);
            }
            else
            {
                Close();
            }
        }

        public event EventHandler DialogClosing;

        public ICommand OKCommand => new RelayCommand(Ok);

        protected virtual void Ok()
        {
            if (OnOk != null)
            {
                OnOk(this);
            }
            else
            {
                Close();
            }
        }

        /// <summary>
        ///
        /// </summary>
        public ICommand CancelCommand => new RelayCommand(Cancel);

        protected virtual void Cancel()
        {
            if (OnCancel != null)
            {
                OnCancel(this);
            }
            else
            {
                Close();
            }
        }

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public Action<RFiDChipChildLayerViewModel> OnOk { get; set; }

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public Action<RFiDChipChildLayerViewModel> OnCancel { get; set; }

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public Action<RFiDChipChildLayerViewModel> OnCloseRequest { get; set; }

        /// <summary>
        ///
        /// </summary>
        public void Close()
        {
            if (DialogClosing != null)
            {
                DialogClosing(this, new EventArgs());
            }
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