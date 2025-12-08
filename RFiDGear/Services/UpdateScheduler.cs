using System;
using System.Threading.Tasks;

namespace RFiDGear.Services
{
    public class UpdateScheduler : IUpdateScheduler
    {
        private readonly IUpdateNotifier updateNotifier;

        public UpdateScheduler()
            : this(new UpdateNotifier())
        {
        }

        public UpdateScheduler(IUpdateNotifier updateNotifier)
        {
            this.updateNotifier = updateNotifier ?? throw new ArgumentNullException(nameof(updateNotifier));
        }

        public void Begin(Func<Task> onUpdateAvailable)
        {
            updateNotifier.StartUpdateCheck(onUpdateAvailable);
        }

        public void Dispose()
        {
            updateNotifier?.Dispose();
        }
    }
}
