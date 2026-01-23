using System.Reflection;
using RFiDGear.UI.UIExtensions;
using RFiDGear.UI.UIExtensions.Interfaces;
using RFiDGear.Extensions.VCNEditor.Model;
using Xunit;

namespace RFiDGear.Tests
{
    public class VCNEditorUiExtensionTests
    {
        [Fact]
        public void VCNEditorExtensionIsConfigured()
        {
            var extensionType = typeof(VCNEditor);
            var attribute = extensionType.GetCustomAttribute<UiExtensionAttribute>();

            Assert.NotNull(attribute);
            Assert.Equal("VCNEditor", attribute.Name);
            Assert.Equal("pack://application:,,,/VCNEditor;component/View/VCNEditorView.xaml", attribute.Uri);
            Assert.Equal("VCNEditor", attribute.Category);
            Assert.True(typeof(IUIExtension).IsAssignableFrom(extensionType));
        }
    }
}
