using System.Globalization;

namespace RFiDGear.Services
{
    public interface IStartupConfigurator
    {
        StartupConfiguration Configure();
    }

    public class StartupConfiguration
    {
        public StartupConfiguration(string readerName, CultureInfo culture, bool autoLoadLastUsedDatabase)
        {
            ReaderName = readerName;
            Culture = culture;
            AutoLoadLastUsedDatabase = autoLoadLastUsedDatabase;
        }

        public string ReaderName { get; }

        public CultureInfo Culture { get; }

        public bool AutoLoadLastUsedDatabase { get; }
    }
}
