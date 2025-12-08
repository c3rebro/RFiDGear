using System;

namespace RFiDGear.Services
{
    public interface ITimerAdapter : IDisposable
    {
        void Change(int dueTime, int period);
    }

    public interface ISystemTimerFactory
    {
        ITimerAdapter Create(System.Threading.TimerCallback callback, int dueTime, int period);
    }
}
