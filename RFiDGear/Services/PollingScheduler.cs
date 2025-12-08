using System.Threading;

namespace RFiDGear.Services
{
    public class PollingScheduler : IPollingScheduler
    {
        private readonly ITimerAdapter updateTimer;
        private readonly ITimerAdapter readerTimer;

        public PollingScheduler()
            : this(new SystemTimerFactory())
        {
        }

        public PollingScheduler(ISystemTimerFactory timerFactory)
        {
            updateTimer = timerFactory.Create(CheckUpdate, 100, 5000);
            readerTimer = timerFactory.Create(CheckReader, 5000, 3000);
        }

        public event TimerCallback OnUpdateRequested;
        public event TimerCallback OnReaderRequested;

        public void PauseReader()
        {
            readerTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        }

        public void ResumeReader()
        {
            readerTimer?.Change(2000, 2000);
        }

        public void Dispose()
        {
            updateTimer?.Dispose();
            readerTimer?.Dispose();
        }

        private void CheckUpdate(object state)
        {
            OnUpdateRequested?.Invoke(state);
        }

        private void CheckReader(object state)
        {
            OnReaderRequested?.Invoke(state);
        }
    }
}
