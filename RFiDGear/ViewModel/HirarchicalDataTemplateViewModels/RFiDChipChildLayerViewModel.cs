using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using RFiDGear.DataAccessLayer.Remote.FromIO;
using RFiDGear.DataAccessLayer;
using RFiDGear.Model;

using MVVMDialogs.ViewModels;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Xml.Serialization;
using System.Windows.Navigation;

namespace RFiDGear.ViewModel
{
    /// <summary>
    /// Description of RFiDChipChildLayerViewModel.
    /// </summary>
    [XmlRootAttribute("TreeViewChildNode", IsNullable = false)]
    public class RFiDChipChildLayerViewModel : ObservableObject, IUserDialogViewModel
    {
        private readonly ResourceLoader resLoader = new ResourceLoader();
        private readonly RFiDChipParentLayerViewModel _parent;
        private readonly CARD_TYPE _cardType;
        private readonly RelayCommand _cmdReadSectorWithCustoms;
        private readonly AsyncRelayCommand _cmdReadSectorWithDefaults;
        private readonly RelayCommand _cmdEditAuthAndModifySector;
        private readonly string _parentUid;

        //private MifareClassicSetupViewModel setupViewModel;
        private readonly MifareClassicSectorModel sectorModel;
        private readonly MifareDesfireAppModel appModel;
        private readonly MifareUltralightPageModel pageModel;

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
            ObservableCollection<IDialogViewModel> _dialogs) : this(_sectorModel, parent, cardType, _dialogs, false)
        {

        }

        public RFiDChipChildLayerViewModel(
            MifareDesfireAppModel appID,
            RFiDChipParentLayerViewModel parent,
            CARD_TYPE cardType,
            ObservableCollection<IDialogViewModel> _dialogs) : this(appID, parent, cardType, _dialogs, false)
        {

        }

        public RFiDChipChildLayerViewModel(
            MifareUltralightPageModel _pageModel,
            RFiDChipParentLayerViewModel parentUID,
            CARD_TYPE cardType,
            ObservableCollection<IDialogViewModel> _dialogs) : this(_pageModel, parentUID, cardType, _dialogs, false)
        {

        }
        public RFiDChipChildLayerViewModel(
            MifareClassicSectorModel _sectorModel,
            RFiDChipParentLayerViewModel parent,
            CARD_TYPE cardType,
            ObservableCollection<IDialogViewModel> _dialogs,
            bool? _isTask)
        {
            if (_dialogs != null)
            {
                dialogs = _dialogs;
            }

            isTask = _isTask;
            sectorModel = _sectorModel;
            _cardType = cardType;
            _parent = parent;

            _cmdReadSectorWithDefaults = new AsyncRelayCommand(ReadSectorWithDefaults);
            _cmdEditAuthAndModifySector = new RelayCommand(ReadSectorWithCustoms);
            _cmdReadSectorWithCustoms = new RelayCommand(ReadSectorWithCustoms);

            Application.Current.Dispatcher.BeginInvoke((Action)(() => {
                ContextMenuItems = new List<MenuItem>
                {
                    new MenuItem()
                    {
                        Header = "Read Sector using default Configuration",
                        Command = _cmdReadSectorWithDefaults,
                        IsEnabled = false
                    },

                    new MenuItem()
                    {
                        Header = "Edit Authentication Settings and Modify Sector",
                        Command = _cmdEditAuthAndModifySector,
                        IsEnabled = false
                    }
                };
            }));
            children = new ObservableCollection<RFiDChipGrandChildLayerViewModel>();

            LoadChildren();
        }

        public RFiDChipChildLayerViewModel(
            MifareDesfireAppModel appID,
            RFiDChipParentLayerViewModel parentUID,
            CARD_TYPE cardType,
            ObservableCollection<IDialogViewModel> _dialogs,
            bool? _isTask)
        {
            if (_dialogs != null)
            {
                dialogs = _dialogs;
            }

            isTask = _isTask;

            appModel = appID;
            _cardType = cardType;
            _parentUid = parentUID?.UID;

            _cmdReadSectorWithDefaults = new AsyncRelayCommand(ReadSectorWithDefaults);
            _cmdEditAuthAndModifySector = new RelayCommand(ReadSectorWithCustoms);

            Application.Current.Dispatcher.BeginInvoke((Action)(() =>
            {
                ContextMenuItems = new List<MenuItem>
                {
                    new MenuItem()
                    {
                        Header = "Read Sector using default Configuration",
                        Command = null,
                        Visibility = Visibility.Hidden
                    },

                    new MenuItem()
                    {
                        Header = "Edit Authentication Settings and Modify Sector",
                        Command = null,
                        Visibility = Visibility.Hidden
                    }
                };
            }));

            children = new ObservableCollection<RFiDChipGrandChildLayerViewModel>();

            //LoadChildren();
        }

        public RFiDChipChildLayerViewModel(
            MifareUltralightPageModel _pageModel,
            RFiDChipParentLayerViewModel parentUID,
            CARD_TYPE cardType,
            ObservableCollection<IDialogViewModel> _dialogs,
            bool? _isTask)
        {
            if (_dialogs != null)
            {
                dialogs = _dialogs;
            }

            isTask = _isTask;

            //device = _device;
            pageModel = _pageModel;
            _cardType = cardType;
            _parentUid = parentUID?.UID;

            _cmdReadSectorWithDefaults = new AsyncRelayCommand(ReadSectorWithDefaults);
            _cmdEditAuthAndModifySector = new RelayCommand(ReadSectorWithCustoms);

            Application.Current.Dispatcher.BeginInvoke((Action)(() =>
            {
                ContextMenuItems = new List<MenuItem>
                {
                    new MenuItem()
                    {
                        Header = "Read Sector using default Configuration",
                        Command = _cmdReadSectorWithDefaults,
                        IsEnabled = false
                    },

                    new MenuItem()
                    {
                        Header = "Edit Authentication Settings and Modify Sector",
                        Command = _cmdEditAuthAndModifySector,
                        IsEnabled = false
                    }
                };
            }));
            children = new ObservableCollection<RFiDChipGrandChildLayerViewModel>();

            LoadChildren();
        }

        public RFiDChipChildLayerViewModel(string _childNodeHeader)
        {
            ChildNodeHeader = _childNodeHeader;
        }

        #endregion

        #region Dialogs

        private readonly ObservableCollection<IDialogViewModel> dialogs;

        #endregion Dialogs

        #region Context Menu Items

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public List<MenuItem> ContextMenu => ContextMenuItems;

        private List<MenuItem> ContextMenuItems;

        public async Task ReadSectorWithDefaults()
        {
            await Parent.ExecuteClassicQuickCheckCommand.ExecuteAsync(this);
        }

        public void ReadSectorWithCustoms()
        {
            using (var device = ReaderDevice.Instance)
            {
                IsSelected = true;

                dialogs.Add(new MifareClassicSetupViewModel(this, dialogs)
                {
                    Caption = String.Format("{0} UID:[{1}] Type:[{2}]",
                                                             ResourceLoader.GetResource("mifareAuthSettingsDialogCaption"),
                                                             Parent.UID,
                                                             Parent.CardType),

                    IsClassicAuthInfoEnabled = true,

                    OnOk = (sender) =>
                    {
                    },

                    OnCancel = (sender) =>
                    {
                        sender.Close();
                    },

                    OnAuth = (sender) =>
                    {
                        IsAuthenticated = device.SectorSuccessfullyAuth;

                        foreach (var gcVM in Children)
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
        [XmlIgnore]
        public object SelectedItem
        {
            get => selectedItem;
            set
            {
                selectedItem = value;

                OnPropertyChanged(nameof(SelectedItem));
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
                OnPropertyChanged(nameof(Children));
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

                if (ContextMenuItems != null)
                {
                    foreach (MenuItem item in ContextMenuItems)
                    {
                        item.IsEnabled = value;
                    }
                }

                OnPropertyChanged(nameof(IsSelected));
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
                OnPropertyChanged(nameof(IsExpanded));

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
        [XmlIgnore]
        public bool? HasChanged
        {
            get => hasChanged;
            set
            {
                hasChanged = value;
                OnPropertyChanged(nameof(HasChanged));
            }
        }
        private bool? hasChanged;

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public bool? IsTask
        {
            get => isTask;
            set
            {
                isTask = value;
                OnPropertyChanged(nameof(IsTask));
            }
        }
        private bool? isTask;

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public bool? IsAuthenticated
        {
            get => isAuth;
            set
            {
                isAuth = value;
                OnPropertyChanged(nameof(IsAuthenticated));
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
                switch ((CARD_TYPE)((short)_cardType & 0xF000))
                {
                    case CARD_TYPE.MifareClassic:
                        childNodeHeader = string.Format("Sector: [{0}]", sectorModel.SectorNumber);
                        break;
                    case CARD_TYPE.MifareUltralight:
                        childNodeHeader = string.Format("Page: {0}", pageModel.PageNumber);
                        break;

                    case CARD_TYPE.DESFireEV0:
                    case CARD_TYPE.DESFireEV1:
                    case CARD_TYPE.DESFireEV2:
                    case CARD_TYPE.DESFireEV3:
                        childNodeHeader = string.Format("AppID: {0} (0x{1})", appModel.appID, appModel.appID.ToString("X8"));
                        break;

                    default:
                        return childNodeHeader;
                }
                return childNodeHeader;
            }
            set
            {
                childNodeHeader = value;
                OnPropertyChanged(nameof(ChildNodeHeader));
            }
        }
        private string childNodeHeader;

        #endregion

        #region ChipProperties

        /// <summary>
        ///
        /// </summary>
        public MifareClassicSectorModel SectorModel => sectorModel;

        /// <summary>
        ///
        /// </summary>
        public MifareDesfireAppModel AppModel => appModel;

        /// <summary>
        ///
        /// </summary>
        public MifareUltralightPageModel PageModel => pageModel;

        #endregion

        public void LoadChildren()
        {
            switch (_cardType)
            {
                case CARD_TYPE.Mifare1K:
                case CARD_TYPE.Mifare2K:
                case CARD_TYPE.MifarePlus_SL1_1K:
                case CARD_TYPE.MifarePlus_SL1_2K:
                    {
                        for (var i = 0; i <= 3; i++)
                        {
                            children.Add(new RFiDChipGrandChildLayerViewModel(new MifareClassicDataBlockModel(0, i), this));
                        }
                    }
                    break;

                case CARD_TYPE.Mifare4K:
                case CARD_TYPE.MifarePlus_SL1_4K:
                    {
                        if (SectorNumber < 32)
                        {
                            for (var i = 0; i <= 3; i++)
                            {
                                children.Add(new RFiDChipGrandChildLayerViewModel(new MifareClassicDataBlockModel(0, i), this));
                            }
                        }
                        else
                        {
                            for (var i = 0; i <= 15; i++)
                            {
                                children.Add(new RFiDChipGrandChildLayerViewModel(new MifareClassicDataBlockModel(0, i), this));
                            }
                        }
                    }
                    break;

                case CARD_TYPE.DESFireEV0:
                case CARD_TYPE.DESFireEV1:
                case CARD_TYPE.DESFireEV2:
                case CARD_TYPE.DESFireEV3:
                    {
                        children.Add(new RFiDChipGrandChildLayerViewModel(new MifareDesfireFileModel(), this));
                    }
                    break;

                default:
                    break;
            }
        }

        #region IUserDialogViewModel Implementation

        [XmlIgnore]
        public bool IsModal { get; private set; }

        public virtual void RequestClose()
        {
            if (this.OnCloseRequest != null)
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
            if (this.OnOk != null)
            {
                this.OnOk(this);
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
            if (this.OnCancel != null)
            {
                this.OnCancel(this);
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
            if (this.DialogClosing != null)
            {
                this.DialogClosing(this, new EventArgs());
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