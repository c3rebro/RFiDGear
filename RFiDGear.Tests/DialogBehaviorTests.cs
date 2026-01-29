using System.Threading.Tasks;
using RFiDGear.UI.MVVMDialogs.Behaviors;
using Xunit;

namespace RFiDGear.Tests
{
    public class DialogBehaviorTests
    {
        [Fact]
        public async Task SetResourceDictionary_IgnoresDuplicateSource()
        {
            const string source = "/RFiDGear.Extensions.DesfirePluginSample;component/ResourceDictionary.xaml";

            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                DialogBehavior.SetResourceDictionary(source);
                DialogBehavior.SetResourceDictionary(source);
            });
        }

        [Fact]
        public async Task SetResourceDictionary_IgnoresEmptySource()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                DialogBehavior.SetResourceDictionary(string.Empty);
            });
        }
    }
}
