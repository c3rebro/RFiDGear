using System;
using System.Threading.Tasks;
using RFiDGear.Services.Interfaces;

namespace RFiDGear.Services
{
    /// <summary>
    /// An update notifier that never looks for an update. The Store edition uses it: the Store delivers the updates, and the
    /// program must not reach out to GitHub for one.
    /// </summary>
    public sealed class NoUpdateNotifier : IUpdateNotifier
    {
        /// <inheritdoc />
        public void StartUpdateCheck(Func<Task> onUpdateAvailable)
        {
            // Nothing to watch: there is no updater behind this notifier.
        }

        /// <inheritdoc />
        public Task TriggerUpdateCheckAsync(Func<Task> onUpdateAvailable)
        {
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            // Holds nothing that needs to be let go of.
        }
    }
}
