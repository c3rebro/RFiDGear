using System;
using System.Globalization;
using System.Threading.Tasks;
using RFiDGear.DataAccessLayer;
using RFiDGear.DataAccessLayer.Remote.FromIO;
using RFiDGear;
using RFiDGear.Model;

namespace RFiDGear.Services
{
    /// <summary>
    /// Default implementation that loads and saves persisted application settings.
    /// </summary>
    public class SettingsBootstrapper : ISettingsBootstrapper
    {
        private readonly Func<SettingsReaderWriter> settingsFactory;

        public SettingsBootstrapper()
            : this(() => new SettingsReaderWriter())
        {
        }

        public SettingsBootstrapper(Func<SettingsReaderWriter> settingsFactory)
        {
            this.settingsFactory = settingsFactory ?? throw new ArgumentNullException(nameof(settingsFactory));
        }

        public async Task<SettingsBootstrapResult> LoadAsync()
        {
            using (var settings = settingsFactory())
            {
                await settings.ReadSettingsAsync().ConfigureAwait(false);
                settings.InitUpdateFile();

                var readerName = string.IsNullOrWhiteSpace(settings.DefaultSpecification.DefaultReaderName)
                    ? Enum.GetName(typeof(ReaderTypes), settings.DefaultSpecification.DefaultReaderProvider)
                    : settings.DefaultSpecification.DefaultReaderName;

                ReaderDevice.Reader = settings.DefaultSpecification.DefaultReaderProvider;

                if (int.TryParse(settings.DefaultSpecification.LastUsedComPort, out var portNumber))
                {
                    ReaderDevice.PortNumber = portNumber;
                }
                else
                {
                    ReaderDevice.PortNumber = 0;
                }

                return new SettingsBootstrapResult
                {
                    CurrentReaderName = readerName,
                    DefaultReaderProvider = settings.DefaultSpecification.DefaultReaderProvider,
                    PortNumber = ReaderDevice.PortNumber,
                    AutoLoadLastUsedProject = settings.DefaultSpecification.AutoLoadProjectOnStart,
                    LastUsedProjectPath = settings.DefaultSpecification.LastUsedProjectPath,
                    Culture = settings.DefaultSpecification.DefaultLanguage == "german" ? new CultureInfo("de-DE") : new CultureInfo("en-US"),
                    DefaultSpecification = settings.DefaultSpecification
                };
            }
        }

        public async Task SaveAsync(Action<DefaultSpecification> updateAction)
        {
            using (var settings = settingsFactory())
            {
                updateAction?.Invoke(settings.DefaultSpecification);
                await settings.SaveSettings().ConfigureAwait(false);
            }
        }
    }
}
