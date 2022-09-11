using CommunityToolkit.Mvvm.ComponentModel;
using System.Xml.Serialization;

namespace RFiDGear.ViewModel
{
    /// <summary>
    /// Description of RFiDChipGrandChildLayerViewModel.
    /// </summary>
    [XmlRootAttribute("TreeViewGrandGrandChildNode", IsNullable = false)]
    public class RFiDChipGrandGrandChildLayerViewModel : ObservableObject
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
            get => selectedItem;
            set => selectedItem = value;
        }

        private object selectedItem;

        #endregion SelectedItem

        #region Parent

        public RFiDChipGrandChildLayerViewModel Parent
        {
            get => parent;
            set
            {
                parent = value;
                OnPropertyChanged(nameof(Parent));
            }
        }
        private RFiDChipGrandChildLayerViewModel parent;

        #endregion Parent

        #region (Dependency) Properties

        [XmlIgnore]
        public string GrandGrandChildNodeHeader => grandGrandChildNodeHeader;

        private readonly string grandGrandChildNodeHeader;

        #endregion (Dependency) Properties

        #region View Switches

        public bool IsExpanded
        {
            get => isExpanded;
            set
            {
                if (value != isExpanded)
                {
                    isExpanded = value;
                    OnPropertyChanged(nameof(IsExpanded));
                }

                // Expand all the way up to the root.
                if (parent != null)
                {
                    parent.IsExpanded = true;
                }
            }
        }

        private bool isExpanded;

        public bool IsSelected
        {
            get => isSelected;
            set
            {
                if (value != isSelected)
                {
                    isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        private bool isSelected;

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

        public bool? IsVisible
        {
            get => isVisible;
            set
            {
                isVisible = value;
                OnPropertyChanged(nameof(IsVisible));
            }
        }

        private bool? isVisible;

        #endregion View Switches
    }
}