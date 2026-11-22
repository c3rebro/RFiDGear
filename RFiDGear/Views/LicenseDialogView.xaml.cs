using System.Diagnostics.CodeAnalysis;
using System.Windows;

namespace RFiDGear.View
{
    /// <summary>
    /// The window of <see cref="RFiDGear.ViewModel.LicenseDialogViewModel"/>.
    /// </summary>
    [SuppressMessage("Minor Code Smell", "S2333:Partial is gratuitous in this context", Justification = "The other half of the class is generated from the XAML, which the analyser cannot see.")]
    public partial class LicenseDialogView : Window
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LicenseDialogView"/> class.
        /// </summary>
        public LicenseDialogView()
        {
            InitializeComponent();
        }
    }
}
