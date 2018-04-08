using GalaSoft.MvvmLight;
using System.Xml.Serialization;

namespace RFiDGear.ViewModel
{
    /// <summary>
    /// Description of RFiDChipGrandChildLayerViewModel.
    /// </summary>
    [XmlRootAttribute("TreeViewGrandGrandChildNode", IsNullable = false)]
    public class RFiDChipGrandGrandChildLayerViewModel : ViewModelBase
    {
        #region Constructors

        public RFiDChipGrandGrandChildLayerViewModel()
        {
        }

        public RFiDChipGrandGrandChildLayerViewModel(string _displayItem, RFiDChipGrandChildLayerViewModel _parent)
        {
            grandGrandChildNodeHeader = _displayItem;
			Parent = _parent;
        }

        #endregion Constructors

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

        public RFiDChipGrandChildLayerViewModel Parent
        {
            get { return parent; }
            set
            {
				parent = value;
				RaisePropertyChanged("Parent");
            }
        } private RFiDChipGrandChildLayerViewModel parent;

        #endregion Parent

        #region (Dependency) Properties

        [XmlIgnore]
        public string GrandGrandChildNodeHeader
        {
            get
            {
                return grandGrandChildNodeHeader;
            }
        }

        private string grandGrandChildNodeHeader;

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

        #endregion View Switches
    }
}