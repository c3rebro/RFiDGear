using System;
using System.Threading;

namespace RFiDGear.Services.Interfaces
{
    public interface IPollingScheduler : IDisposable
    {
        event TimerCallback OnUpdateRequested;
        event TimerCallback OnReaderRequested;
        void PauseReader();
        void ResumeReader();
    }
}
