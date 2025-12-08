using System;
using System.Threading;

namespace RFiDGear.Services
{
    public interface IReaderMonitor : IDisposable
    {
        void StartMonitoring(TimerCallback callback);

        void Pause();

        void Resume();
    }
}
