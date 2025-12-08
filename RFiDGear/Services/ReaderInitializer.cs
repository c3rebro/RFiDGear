using System.Globalization;
using System.Threading.Tasks;
using RFiDGear.DataAccessLayer;
using RFiDGear.DataAccessLayer.Remote.FromIO;
using RFiDGear.Model;
using RFiDGear.Services.Interfaces;

namespace RFiDGear.Services
{
    public class ReaderInitializer : IReaderInitializer
    {
        public ReaderSetup ApplySettings(SettingsReaderWriter settings)
        {
            var currentReader = string.IsNullOrWhiteSpace(settings.DefaultSpecification.DefaultReaderName)
                ? System.Enum.GetName(typeof(ReaderTypes), settings.DefaultSpecification.DefaultReaderProvider)
                : settings.DefaultSpecification.DefaultReaderName;

            if (int.TryParse(settings.DefaultSpecification.LastUsedComPort, out var portNumber))
            {
                ReaderDevice.PortNumber = portNumber;
            }
            else
            {
                ReaderDevice.PortNumber = 0;
            }

            ReaderDevice.Reader = settings.DefaultSpecification.DefaultReaderProvider;

            var culture = settings.DefaultSpecification.DefaultLanguage == "german"
                ? new CultureInfo("de-DE")
                : new CultureInfo("en-US");

            return new ReaderSetup(currentReader, culture);
        }

        public async Task<ReaderStatus> RefreshReaderStatusAsync(bool? isReaderBusy)
        {
            using (var settings = new SettingsReaderWriter())
            {
                if (ReaderDevice.Instance != null && isReaderBusy != true)
                {
                    try
                    {
                        if (!ReaderDevice.Instance.IsConnected)
                        {
                            var result = await ReaderDevice.Instance.ConnectAsync().ConfigureAwait(false);

                            if (result == ERROR.NoError)
                            {
                                var description = BuildReaderDescription(settings);
                                return new ReaderStatus(description, false);
                            }

                            return new ReaderStatus(BuildReaderFallback(settings), null);
                        }

                        return new ReaderStatus(BuildReaderDescription(settings), isReaderBusy);
                    }
                    catch
                    {
                        return new ReaderStatus(BuildReaderFallback(settings), null);
                    }
                }
                else if (ReaderDevice.Instance != null && isReaderBusy != true)
                {
                    var description = BuildReaderFallback(settings);
                    await ReaderDevice.Instance.ConnectAsync().ConfigureAwait(false);

                    return ReaderDevice.Instance.IsConnected
                        ? new ReaderStatus(description, false)
                        : new ReaderStatus(description, null);
                }

                return new ReaderStatus(BuildReaderFallback(settings), isReaderBusy);
            }
        }

        private static string BuildReaderDescription(SettingsReaderWriter settings)
        {
            return settings.DefaultSpecification.DefaultReaderProvider == ReaderTypes.None
                ? "N/A"
                : settings.DefaultSpecification.DefaultReaderProvider + " " +
                  ReaderDevice.Instance.ReaderUnitName +
                  ReaderDevice.Instance.ReaderUnitVersion;
        }

        private static string BuildReaderFallback(SettingsReaderWriter settings)
        {
            return settings.DefaultSpecification.DefaultReaderProvider == ReaderTypes.None
                ? "N/A"
                : settings.DefaultSpecification.DefaultReaderProvider.ToString();
        }
    }
}
