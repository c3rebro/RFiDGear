using System.Diagnostics.CodeAnalysis;
using System.Windows.Controls;

namespace RFiDGear.View
{
    /// <summary>
    /// The chip a task of the Ultralight family expects, how it authenticates, and the opt-in for writing protected pages.
    /// </summary>
    [SuppressMessage("Minor Code Smell", "S2333:Partial is gratuitous in this context", Justification = "The other half of the class is generated from the XAML, which the analyser cannot see.")]
    public partial class TabPageMifareUltralightChipView : UserControl
    {
        /// <summary>Creates the tab.</summary>
        public TabPageMifareUltralightChipView()
        {
            InitializeComponent();
        }
    }
}
