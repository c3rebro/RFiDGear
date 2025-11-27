using System;
using System.Threading;

namespace RFiDGear.Services
{
    public interface IPollingScheduler : IDisposable
    {
        event TimerCallback OnUpdateRequested;
        event TimerCallback OnReaderRequested;
        void PauseReader();
        void ResumeReader();
    }
}
