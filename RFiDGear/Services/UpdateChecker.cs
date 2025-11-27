using System.Threading.Tasks;

namespace RFiDGear.Services
{
    public class UpdateChecker : IUpdateChecker
    {
        private readonly IUpdaterAdapter updater;

        public UpdateChecker()
            : this(new UpdaterAdapter())
        {
        }

        public UpdateChecker(IUpdaterAdapter updaterAdapter)
        {
            updater = updaterAdapter;
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
            return updater.ApplyUpdateAsync();
        }

        public Task StartMonitoringAsync()
        {
            return updater.StartMonitoringAsync();
        }

        public Task StopMonitoringAsync()
        {
            return updater.StopMonitoringAsync();
        }
    }
}
