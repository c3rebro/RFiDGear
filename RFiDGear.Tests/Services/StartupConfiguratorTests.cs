using System.Globalization;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RFiDGear.Model;
using RFiDGear.Services;

namespace RFiDGear.Tests.Services
{
    [TestClass]
    public class StartupConfiguratorTests
    {
        [TestMethod]
        public void Configure_UsesInitializerAndSettings()
        {
            var fakeInitializer = new FakeReaderInitializer();
            var settings = CreateSettings(autoLoadLastUsedDatabase: true);
            var configurator = new StartupConfigurator(fakeInitializer, () => settings);

            var configuration = configurator.Configure();

            Assert.AreEqual("Reader-One", configuration.ReaderName);
            Assert.AreEqual(new CultureInfo("en-US"), configuration.Culture);
            Assert.IsTrue(configuration.AutoLoadLastUsedDatabase);
            Assert.AreSame(settings, fakeInitializer.LastSettings);
        }

        private static SettingsReaderWriter CreateSettings(bool autoLoadLastUsedDatabase)
        {
            var settings = new SettingsReaderWriter();
            settings.DefaultSpecification.AutoLoadProjectOnStart = autoLoadLastUsedDatabase;
            return settings;
        }

        private class FakeReaderInitializer : IReaderInitializer
        {
            public SettingsReaderWriter LastSettings { get; private set; }

            public ReaderSetup ApplySettings(SettingsReaderWriter settings)
            {
                LastSettings = settings;
                return new ReaderSetup("Reader-One", new CultureInfo("en-US"));
            }

            public Task<ReaderStatus> RefreshReaderStatusAsync(bool? isReaderBusy)
            {
                return Task.FromResult(new ReaderStatus("Reader", isReaderBusy));
            }
        }
    }
}
