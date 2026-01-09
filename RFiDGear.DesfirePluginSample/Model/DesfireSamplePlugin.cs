using RFiDGear.UI.UIExtensions;
using RFiDGear.UI.UIExtensions.Interfaces;

namespace RFiDGear.DesfirePluginSample.Model
{
    /// <summary>
    /// Sample MEF-exported plugin for the Mifare DESFire setup view.
    /// </summary>
    /// <remarks>
    /// Load mechanism:
    /// 1) Build this project to produce RFiDGear.DesfirePluginSample.dll.
    /// 2) Copy the DLL (and any dependencies) into the application's Extensions folder
    ///    (%ProgramData%\RFiDGear\Extensions by default), or any folder assigned to
    ///    <see cref="RFiDGear.Infrastructure.MefHelper.ExtensionsPath"/>.
    /// 3) Restart the app; the plugin will appear in the DESFire Plugins tab and load
    ///    <see cref="View.DesfireSampleView"/> via the pack URI below.
    /// </remarks>
    [UiExtension(
        Name = "DESFire Sample Plugin",
        Category = "Mifare DESFire",
        SortOrder = 100,
        Uri = "pack://application:,,,/RFiDGear.DesfirePluginSample;component/View/DesfireSampleView.xaml")]
    public class DesfireSamplePlugin : IUIExtension
    {
    }
}
