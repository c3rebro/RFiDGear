using System;
using System.Threading;

namespace RFiDGear.Services.Interfaces
{
    public interface IReaderMonitor : IDisposable
    {
        void StartMonitoring(TimerCallback callback);

        void Pause();

        void Resume();
    }
}
