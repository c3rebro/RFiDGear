using System;
using System.Threading.Tasks;
using System.Windows;
using RFiDGear;
using RFiDGear.Extensions.VCNEditor.ViewModel;
using Xunit;

namespace RFiDGear.Tests
{
    public class ApplicationResourceDictionaryTests
    {
        [Fact]
        public async Task VcnEditorResourceDictionary_RegistersProfileEditorViewModel()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var app = Application.Current ?? new Application();
                var dictionary = new ResourceDictionary
                {
                    Source = new Uri("/RFiDGear.Extensions.VCNEditor;component/ResourceDictionary.xaml", UriKind.RelativeOrAbsolute)
                };

                app.Resources.MergedDictionaries.Add(dictionary);

                try
                {
                    var resource = app.TryFindResource(typeof(ProfileEditorViewModel));
                    Assert.NotNull(resource);
                }
                finally
                {
                    app.Resources.MergedDictionaries.Remove(dictionary);
                }
            });
        }

        [Fact]
        public async Task TryMergeExtensionResourceDictionary_AddsVcnEditorResourcesWhenAssemblyAvailable()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var resources = new ResourceDictionary();
                var merged = App.TryMergeExtensionResourceDictionary(
                    resources,
                    "RFiDGear.Extensions.VCNEditor",
                    "/RFiDGear.Extensions.VCNEditor;component/ResourceDictionary.xaml");

                Assert.True(merged);
                Assert.Single(resources.MergedDictionaries);
            });
        }
    }
}
