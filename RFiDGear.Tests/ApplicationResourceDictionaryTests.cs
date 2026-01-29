using System;
using System.Threading.Tasks;
using System.Windows;
using RFiDGear;
using RFiDGear.Extensions.DesfirePluginSample.ViewModel;
using Xunit;

namespace RFiDGear.Tests
{
    public class ApplicationResourceDictionaryTests
    {
        [Fact]
        public async Task DesfireSampleResourceDictionary_RegistersDesfireSampleViewModel()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var app = Application.Current ?? new Application();
                var dictionary = new ResourceDictionary
                {
                    Source = new Uri("/RFiDGear.Extensions.DesfirePluginSample;component/ResourceDictionary.xaml", UriKind.RelativeOrAbsolute)
                };

                app.Resources.MergedDictionaries.Add(dictionary);

                try
                {
                    var resource = app.TryFindResource(typeof(DesfireSampleViewModel));
                    Assert.NotNull(resource);
                }
                finally
                {
                    app.Resources.MergedDictionaries.Remove(dictionary);
                }
            });
        }

        [Fact]
        public async Task TryMergeExtensionResourceDictionary_AddsDesfireSampleResourcesWhenAssemblyAvailable()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var resources = new ResourceDictionary();
                var merged = App.TryMergeExtensionResourceDictionary(
                    resources,
                    "RFiDGear.Extensions.DesfirePluginSample",
                    "/RFiDGear.Extensions.DesfirePluginSample;component/ResourceDictionary.xaml");

                Assert.True(merged);
                Assert.Single(resources.MergedDictionaries);
            });
        }
    }
}
