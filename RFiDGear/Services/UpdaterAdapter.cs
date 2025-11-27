using System.Threading.Tasks;
using RedCell.Diagnostics.Update;

namespace RFiDGear.Services
{
    public interface IUpdaterAdapter
    {
        bool UpdateAvailable { get; }
        string UpdateInfoText { get; }
        bool AllowUpdate { get; set; }
        Task StartMonitoringAsync();
        Task StopMonitoringAsync();
        Task ApplyUpdateAsync();
    }

    public class UpdaterAdapter : IUpdaterAdapter
    {
        private readonly Updater updater;

        public UpdaterAdapter()
        {
            updater = new Updater();
        }

        public bool UpdateAvailable => updater.UpdateAvailable;
        public string UpdateInfoText => updater.UpdateInfoText;
        public bool AllowUpdate
        {
            get => updater.AllowUpdate;
            set => updater.AllowUpdate = value;
        }

        public Task ApplyUpdateAsync()
        {
            return updater.Update();
        }

        public Task StartMonitoringAsync()
        {
            return updater.StartMonitoring();
        }

        public Task StopMonitoringAsync()
        {
            return updater.StopMonitoring();
        }
    }
}
