using System.Globalization;
using System.Threading.Tasks;
using RFiDGear.DataAccessLayer;
using RFiDGear.Model;

namespace RFiDGear.Services.Interfaces
{
    public interface IReaderInitializer
    {
        ReaderSetup ApplySettings(SettingsReaderWriter settings);
        Task<ReaderStatus> RefreshReaderStatusAsync(bool? isReaderBusy);
    }

    public class ReaderSetup
    {
        public ReaderSetup(string readerName, CultureInfo culture)
        {
            ReaderName = readerName;
            Culture = culture;
        }

        public string ReaderName { get; }
        public CultureInfo Culture { get; }
    }

    public class ReaderStatus
    {
        public ReaderStatus(string currentReader, bool? isBusy)
        {
            CurrentReader = currentReader;
            IsBusy = isBusy;
        }

        public string CurrentReader { get; }
        public bool? IsBusy { get; }
    }
}
