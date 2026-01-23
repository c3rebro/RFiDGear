using System.Reflection;
using RFiDGear.Extensions.DesfirePluginSample.Model;
using RFiDGear.UI.UIExtensions;
using RFiDGear.UI.UIExtensions.Interfaces;
using Xunit;

namespace RFiDGear.Tests
{
    public class DesfireSamplePluginTests
    {
        [Fact]
        public void DesfireSamplePlugin_IsConfiguredForMefLoading()
        {
            var pluginType = typeof(DesfireSamplePlugin);
            var attribute = pluginType.GetCustomAttribute<UiExtensionAttribute>();

            Assert.NotNull(attribute);
            Assert.Equal("DESFire Sample Plugin", attribute.Name);
            Assert.Equal("Mifare DESFire", attribute.Category);
            Assert.Equal("pack://application:,,,/RFiDGear.Extensions.DesfirePluginSample;component/View/DesfireSampleView.xaml", attribute.Uri);
            Assert.True(typeof(IUIExtension).IsAssignableFrom(pluginType));
        }
    }
}
