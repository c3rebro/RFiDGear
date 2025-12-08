using System;
using System.Windows.Threading;
using RFiDGear.Services.TaskExecution;
using RFiDGear.ViewModel.DialogFactories;

namespace RFiDGear.Services
{
    public interface ITaskServiceInitializer
    {
        TaskServiceInitialization Initialize(
            Action notifyChipTasksChanged,
            Action activateMainWindow,
            Action<bool> updateReaderBusy,
            EventHandler<TaskExecutionCompletedEventArgs> executionCompletedHandler,
            DispatcherTimer triggerReadChip,
            DispatcherTimer taskTimeout);
    }

    public class TaskServiceInitialization
    {
        public TaskServiceInitialization(TaskDialogFactory taskDialogFactory, ITaskExecutionService taskExecutionService)
        {
            TaskDialogFactory = taskDialogFactory ?? throw new ArgumentNullException(nameof(taskDialogFactory));
            TaskExecutionService = taskExecutionService ?? throw new ArgumentNullException(nameof(taskExecutionService));
        }

        public TaskDialogFactory TaskDialogFactory { get; }

        public ITaskExecutionService TaskExecutionService { get; }
    }

    public class TaskServiceInitializer : ITaskServiceInitializer
    {
        public TaskServiceInitialization Initialize(
            Action notifyChipTasksChanged,
            Action activateMainWindow,
            Action<bool> updateReaderBusy,
            EventHandler<TaskExecutionCompletedEventArgs> executionCompletedHandler,
            DispatcherTimer triggerReadChip,
            DispatcherTimer taskTimeout)
        {
            if (triggerReadChip == null)
            {
                throw new ArgumentNullException(nameof(triggerReadChip));
            }

            if (taskTimeout == null)
            {
                throw new ArgumentNullException(nameof(taskTimeout));
            }

            var taskDialogFactory = new TaskDialogFactory(
                notifyChipTasksChanged,
                activateMainWindow,
                updateReaderBusy);

            var triggerReadChipAdapter = new DispatcherTimerAdapter(triggerReadChip);
            var taskTimeoutAdapter = new DispatcherTimerAdapter(taskTimeout);
            var taskExecutionService = new TaskExecutionService(new ReaderDeviceProvider(), triggerReadChipAdapter, taskTimeoutAdapter);

            if (executionCompletedHandler != null)
            {
                taskExecutionService.ExecutionCompleted += executionCompletedHandler;
            }

            return new TaskServiceInitialization(taskDialogFactory, taskExecutionService);
        }
    }
}
