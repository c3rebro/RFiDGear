using System;
using System.Globalization;
using System.Threading.Tasks;
using RFiDGear.Models;
using RFiDGear.Services.Interfaces;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.ReaderProviders;
using RFiDGear.Infrastructure.FileAccess;
using Serilog;

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

                var configuredProvider = settings.DefaultSpecification.DefaultReaderProvider;
                bool isRdpSession = false;

                if (configuredProvider == ReaderTypes.PCSC && !RdpSessionDetector.CanEstablishPcscContext())
                {
                    Log.ForContext<SettingsBootstrapper>().Warning(
                        "PC/SC smart card subsystem is not accessible in this session (SCardEstablishContext failed) — " +
                        "switching reader provider to None. " +
                        "If running under RDP, enable smart card redirection (redirectsmartcards:i:1 in your .rdp file) " +
                        "to restore PC/SC access.");
                    configuredProvider = ReaderTypes.None;
                    isRdpSession = RdpSessionDetector.IsRemoteDesktopSession;
                }

                var readerName = string.IsNullOrWhiteSpace(settings.DefaultSpecification.DefaultReaderName)
                    ? Enum.GetName(typeof(ReaderTypes), configuredProvider)
                    : settings.DefaultSpecification.DefaultReaderName;

                ReaderDevice.Reader = configuredProvider;

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
                    DefaultReaderProvider = configuredProvider,
                    PortNumber = ReaderDevice.PortNumber,
                    AutoLoadLastUsedProject = settings.DefaultSpecification.AutoLoadProjectOnStart,
                    LastUsedProjectPath = settings.DefaultSpecification.LastUsedProjectPath,
                    Culture = settings.DefaultSpecification.DefaultLanguage == "german" ? new CultureInfo("de-DE") : new CultureInfo("en-US"),
                    DefaultSpecification = settings.DefaultSpecification,
                    IsRdpSession = isRdpSession
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
