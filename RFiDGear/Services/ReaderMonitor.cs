using System;
using System.Threading;

namespace RFiDGear.Services
{
    public class ReaderMonitor : IReaderMonitor
    {
        private Timer readerTimer;

        public void StartMonitoring(TimerCallback callback)
        {
            Dispose();
            readerTimer = new Timer(callback, null, 5000, 3000);
        }

        public void Pause()
        {
            readerTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        }

        public void Resume()
        {
            readerTimer?.Change(2000, 2000);
        }

        public void Dispose()
        {
            readerTimer?.Dispose();
            readerTimer = null;
        }
    }
}
