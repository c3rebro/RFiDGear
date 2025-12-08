using System;
using System.Threading.Tasks;

namespace RFiDGear.Services.Interfaces
{
    /// <summary>
    /// Schedules update checks and informs listeners when an update is available.
    /// </summary>
    public interface IUpdateNotifier : IDisposable
    {
        /// <summary>
        /// Starts monitoring for updates and invokes the callback when an update is available.
        /// </summary>
        /// <param name="onUpdateAvailable">Callback invoked when an update is detected.</param>
        void StartUpdateCheck(Func<Task> onUpdateAvailable);

        /// <summary>
        /// Executes a manual check for updates.
        /// </summary>
        /// <param name="onUpdateAvailable">Callback invoked when an update is detected.</param>
        Task TriggerUpdateCheckAsync(Func<Task> onUpdateAvailable);
    }
}
