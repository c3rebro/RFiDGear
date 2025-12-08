using System;
using System.Windows.Threading;

namespace RFiDGear.Services
{
    public interface ITimerFactory
    {
        TimerInitializationResult CreateTimers(EventHandler triggerReadHandler, EventHandler taskTimeoutHandler);
        DispatcherTimer CreateTriggerReadTimer(EventHandler tickHandler);
        DispatcherTimer CreateTaskTimeoutTimer(EventHandler tickHandler);
    }

    public class MainWindowTimerFactory : ITimerFactory
    {
        public TimerInitializationResult CreateTimers(EventHandler triggerReadHandler, EventHandler taskTimeoutHandler)
        {
            if (triggerReadHandler == null)
            {
                throw new ArgumentNullException(nameof(triggerReadHandler));
            }

            if (taskTimeoutHandler == null)
            {
                throw new ArgumentNullException(nameof(taskTimeoutHandler));
            }

            var triggerReadChip = CreateTriggerReadTimer(triggerReadHandler);
            var taskTimeout = CreateTaskTimeoutTimer(taskTimeoutHandler);

            return new TimerInitializationResult(triggerReadChip, taskTimeout);
        }

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

    public class TimerInitializationResult
    {
        public TimerInitializationResult(DispatcherTimer triggerReadTimer, DispatcherTimer taskTimeoutTimer)
        {
            TriggerReadTimer = triggerReadTimer ?? throw new ArgumentNullException(nameof(triggerReadTimer));
            TaskTimeoutTimer = taskTimeoutTimer ?? throw new ArgumentNullException(nameof(taskTimeoutTimer));
        }

        public DispatcherTimer TriggerReadTimer { get; }

        public DispatcherTimer TaskTimeoutTimer { get; }
    }
}
