using System;
using RFiDGear.Model;

namespace RFiDGear.Services
{
    public class StartupConfigurator : IStartupConfigurator
    {
        private readonly IReaderInitializer readerInitializer;
        private readonly Func<SettingsReaderWriter> settingsFactory;

        public StartupConfigurator(IReaderInitializer readerInitializer)
            : this(readerInitializer, () => new SettingsReaderWriter())
        {
        }

        public StartupConfigurator(IReaderInitializer readerInitializer, Func<SettingsReaderWriter> settingsFactory)
        {
            this.readerInitializer = readerInitializer;
            this.settingsFactory = settingsFactory;
        }

        public StartupConfiguration Configure()
        {
            using (var settings = settingsFactory())
            {
                var setup = readerInitializer.ApplySettings(settings);

                return new StartupConfiguration(setup.ReaderName, setup.Culture, settings.DefaultSpecification.AutoLoadProjectOnStart);
            }
        }
    }
}
