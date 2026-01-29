using System.Runtime.Versioning;
using System.Windows.Controls;
using RFiDGear.Extensions.DesfirePluginSample.ViewModel;

namespace RFiDGear.Extensions.DesfirePluginSample.View
{
    /// <summary>
    /// Mock UI for the DESFire sample plugin.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public partial class DesfireSampleView : UserControl
    {
        public DesfireSampleView()
        {
            InitializeComponent();
            DataContext = new DesfireSampleViewModel();
        }
    }
}
