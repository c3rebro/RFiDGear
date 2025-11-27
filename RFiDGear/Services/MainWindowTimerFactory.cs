using System;
using System.Windows.Threading;

namespace RFiDGear.Services
{
    public interface IMainWindowTimerFactory
    {
        DispatcherTimer CreateTriggerReadTimer(EventHandler tickHandler);
        DispatcherTimer CreateTaskTimeoutTimer(EventHandler tickHandler);
    }

    public class MainWindowTimerFactory : IMainWindowTimerFactory
    {
        public DispatcherTimer CreateTriggerReadTimer(EventHandler tickHandler)
        {
            var triggerReadChip = new DispatcherTimer
            {
                Interval = new TimeSpan(0, 0, 0, 2, 500)
            };

            triggerReadChip.Tick += tickHandler;
            triggerReadChip.Start();
            triggerReadChip.IsEnabled = false;
            triggerReadChip.Tag = triggerReadChip.IsEnabled;

            return triggerReadChip;
        }

        public DispatcherTimer CreateTaskTimeoutTimer(EventHandler tickHandler)
        {
#if DEBUG
            var taskTimeout = new DispatcherTimer
            {
                Interval = new TimeSpan(0, 1, 0, 0, 0)
            };
#else
            var taskTimeout = new DispatcherTimer
            {
                Interval = new TimeSpan(0, 0, 0, 4, 0)
            };
#endif
            taskTimeout.Tick += tickHandler;
            taskTimeout.Start();
            taskTimeout.IsEnabled = false;

            return taskTimeout;
        }
    }
}
