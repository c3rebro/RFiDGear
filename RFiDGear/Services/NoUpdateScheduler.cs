using System;
using System.Threading.Tasks;
using RFiDGear.Services.Interfaces;

namespace RFiDGear.Services
{
    /// <summary>
    /// An update scheduler that never starts a check. The Store edition uses it, see <see cref="NoUpdateNotifier"/>.
    /// </summary>
    public sealed class NoUpdateScheduler : IUpdateScheduler
    {
        /// <inheritdoc />
        public void Begin(Func<Task> onUpdateAvailable)
        {
            // No check is started, so the callback is never invoked.
        }

        /// <inheritdoc />
        public void Dispose()
        {
            // Holds nothing that needs to be let go of.
        }
    }
}
