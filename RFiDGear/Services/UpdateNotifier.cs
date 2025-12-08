using System;
using System.Threading.Tasks;

namespace RFiDGear.Services
{
    /// <summary>
    /// Polls for application updates using a timer and raises notifications when available.
    /// </summary>
    public class UpdateNotifier : IUpdateNotifier
    {
        private readonly IUpdaterAdapter updater;
        private readonly ISystemTimerFactory timerFactory;
        private ITimerAdapter updateTimer;

        public UpdateNotifier()
            : this(new UpdaterAdapter(), new SystemTimerFactory())
        {
        }

        public UpdateNotifier(IUpdaterAdapter updater, ISystemTimerFactory timerFactory)
        {
            this.updater = updater ?? throw new ArgumentNullException(nameof(updater));
            this.timerFactory = timerFactory ?? throw new ArgumentNullException(nameof(timerFactory));
        }

        public void StartUpdateCheck(Func<Task> onUpdateAvailable)
        {
            DisposeTimer();
            updateTimer = timerFactory.Create(async _ => await CheckForUpdatesAsync(onUpdateAvailable).ConfigureAwait(false), 100, 5000);
        }

        public Task TriggerUpdateCheckAsync(Func<Task> onUpdateAvailable)
        {
            return CheckForUpdatesAsync(onUpdateAvailable);
        }

        private async Task CheckForUpdatesAsync(Func<Task> onUpdateAvailable)
        {
            if (onUpdateAvailable == null)
            {
                return;
            }

            updateTimer?.Change(System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);

            if (updater.UpdateAvailable)
            {
                await onUpdateAvailable().ConfigureAwait(false);
            }

            updateTimer?.Change(5000, 5000);
        }

        public void Dispose()
        {
            DisposeTimer();
        }

        private void DisposeTimer()
        {
            updateTimer?.Dispose();
            updateTimer = null;
        }
    }
}
