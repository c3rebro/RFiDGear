using System;
using System.Windows.Threading;
using RFiDGear.Services.SecureKeys;
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
            DispatcherTimer taskTimeout,
            ITaskListEditGate taskListEdit);
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
            DispatcherTimer taskTimeout,
            ITaskListEditGate taskListEdit)
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
                updateReaderBusy,
                taskListEdit);

            var triggerReadChipAdapter = new DispatcherTimerAdapter(triggerReadChip);
            var taskTimeoutAdapter = new DispatcherTimerAdapter(taskTimeout);
            // The providers are created once, here, on the user-interface thread. A command that no run started (a button of
            // a task dialog) asks them with the default context, and a person is there to be asked.
            var secureKeys = SecureKeyRegistry.FromApplicationExtensions();
            KeyResolutionScope.SetDefault(new KeyResolutionContext(secureKeys, null, string.Empty, null, interactive: true));

            var taskExecutionService = new TaskExecutionService(new ReaderDeviceProvider(), triggerReadChipAdapter, taskTimeoutAdapter, null, secureKeys);

            if (executionCompletedHandler != null)
            {
                taskExecutionService.ExecutionCompleted += executionCompletedHandler;
            }

            return new TaskServiceInitialization(taskDialogFactory, taskExecutionService);
        }
    }
}
