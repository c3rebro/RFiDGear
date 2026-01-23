using System;
using System.Threading.Tasks;
using RFiDGear.UI.UIExtensions.Interfaces;
using RFiDGear.ViewModel.TaskSetupViewModels;
using Xunit;

namespace RFiDGear.Tests
{
    public class PluginSelectionViewModelTests
    {
        [Fact]
        public async Task SelectedPlugin_ForMifareClassic_ExposesSelectedMetadataUri()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var viewModel = new MifareClassicSetupViewModel();
                var plugin = CreatePlugin("classic://plugin");

                viewModel.Items = new[] { plugin };
                viewModel.SelectedPlugin = plugin;

                var selected = Assert.IsType<Lazy<IUIExtension, IUIExtensionDetails>>(viewModel.SelectedPlugin);
                Assert.Equal("classic://plugin", selected.Metadata.Uri);
            });
        }

        [Fact]
        public async Task SelectedPlugin_ForMifareUltralight_ExposesSelectedMetadataUri()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var viewModel = new MifareUltralightSetupViewModel();
                var plugin = CreatePlugin("ultralight://plugin");

                viewModel.Items = new[] { plugin };
                viewModel.SelectedPlugin = plugin;

                var selected = Assert.IsType<Lazy<IUIExtension, IUIExtensionDetails>>(viewModel.SelectedPlugin);
                Assert.Equal("ultralight://plugin", selected.Metadata.Uri);
            });
        }

        [Fact]
        public async Task SelectedPlugin_ForMifareDesfire_ExposesSelectedMetadataUri()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var viewModel = new MifareDesfireSetupViewModel();
                var plugin = CreatePlugin("desfire://plugin");

                viewModel.Items = new[] { plugin };
                viewModel.SelectedPlugin = plugin;

                var selected = Assert.IsType<Lazy<IUIExtension, IUIExtensionDetails>>(viewModel.SelectedPlugin);
                Assert.Equal("desfire://plugin", selected.Metadata.Uri);
            });
        }

        private static Lazy<IUIExtension, IUIExtensionDetails> CreatePlugin(string uri)
        {
            return new Lazy<IUIExtension, IUIExtensionDetails>(
                () => new TestExtension(),
                new TestExtensionDetails(uri));
        }

        private sealed class TestExtension : IUIExtension
        {
        }

        private sealed class TestExtensionDetails : IUIExtensionDetails
        {
            public TestExtensionDetails(string uri)
            {
                Uri = uri;
            }

            public string Category => "Test";

            public string IconUri => "";

            public string Name => "Test Extension";

            public string Uri { get; }

            public int SortOrder => 0;
        }
    }
}
