using System.Threading;
using RFiDGear.Services.Interfaces;

namespace RFiDGear.Services
{
    public class TimerAdapter : ITimerAdapter
    {
        private readonly Timer timer;

        public TimerAdapter(TimerCallback callback, int dueTime, int period)
        {
            timer = new Timer(callback, null, dueTime, period);
        }

        public void Change(int dueTime, int period)
        {
            timer.Change(dueTime, period);
        }

        public void Dispose()
        {
            timer.Dispose();
        }
    }

    public class SystemTimerFactory : ISystemTimerFactory
    {
        public ITimerAdapter Create(TimerCallback callback, int dueTime, int period)
        {
            return new TimerAdapter(callback, dueTime, period);
        }
    }
}
