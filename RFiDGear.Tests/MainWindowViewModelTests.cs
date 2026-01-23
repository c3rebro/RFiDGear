using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Controls;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.ReaderProviders;
using RFiDGear.Models;
using RFiDGear.Services;
using RFiDGear.Services.Factories;
using RFiDGear.Services.Interfaces;
using RFiDGear.Services.TaskExecution;
using RFiDGear.ViewModel;
using RFiDGear.ViewModel.DialogFactories;
using Xunit;

namespace RFiDGear.Tests
{
    public class MainWindowViewModelTests
    {
        [Fact]
        public async Task InitializeAsync_UsesAsyncSettingsBootstrapperOnUiThread()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var bootstrapper = new DeferredSettingsBootstrapper();
                var viewModel = new MainWindowViewModel(
                    bootstrapper,
                    new NoopUpdateNotifier(),
                    new FakeContextMenuBuilder(),
                    new FakeAppStartupInitializer(),
                    new FakeTimerFactory(),
                    new FakeTaskServiceInitializer(),
                    new FakeMenuInitializer(),
                    new FakeStartupArgumentProcessor(),
                    new NoopUpdateScheduler(),
                    new FakeReaderMonitor(),
                    new FakeProjectBootstrapper());

                var uiThreadId = Environment.CurrentManagedThreadId;

                var initializationTask = viewModel.InitializeAsync();

                Assert.False(initializationTask.IsCompleted);

                bootstrapper.Complete(new SettingsBootstrapResult
                {
                    CurrentReaderName = "Reader",
                    DefaultReaderProvider = ReaderTypes.None,
                    Culture = CultureInfo.InvariantCulture
                });

                await initializationTask;

                Assert.Equal("Reader", viewModel.CurrentReader);
                Assert.Equal(uiThreadId, Environment.CurrentManagedThreadId);
            });
        }

        [Fact]
        public async Task SaveProject_UsesProjectPathFromStartupArguments()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var bootstrapper = new DeferredSettingsBootstrapper();
                var viewModel = new MainWindowViewModel(
                    bootstrapper,
                    new NoopUpdateNotifier(),
                    new FakeContextMenuBuilder(),
                    new FakeAppStartupInitializer(),
                    new FakeTimerFactory(),
                    new FakeTaskServiceInitializer(),
                    new FakeMenuInitializer(),
                    new FakeStartupArgumentProcessor(),
                    new NoopUpdateScheduler(),
                    new FakeReaderMonitor(),
                    new FakeProjectBootstrapper());

                var initializationTask = viewModel.InitializeAsync();

                bootstrapper.Complete(new SettingsBootstrapResult
                {
                    CurrentReaderName = "Reader",
                    DefaultReaderProvider = ReaderTypes.None,
                    Culture = CultureInfo.InvariantCulture
                });

                await initializationTask;

                var tempRoot = Path.Combine(Path.GetTempPath(), "RFiDGear.Tests", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempRoot);

                var oldProjectPath = Path.Combine(tempRoot, "old.xml");
                var newProjectPath = Path.Combine(tempRoot, "new.xml");
                var originalOldProjectContent = "<Project><ManifestVersion>1.0.0</ManifestVersion></Project>";
                var originalNewProjectContent = "<Project><ManifestVersion>1.0.0</ManifestVersion></Project>";
                File.WriteAllText(oldProjectPath, originalOldProjectContent);
                File.WriteAllText(newProjectPath, originalNewProjectContent);

                var projectManager = new ProjectManager();
                var originalLastUsedPath = string.Empty;

                using (var settings = new SettingsReaderWriter(projectManager.AppDataPath))
                {
                    originalLastUsedPath = settings.DefaultSpecification.LastUsedProjectPath;
                    settings.DefaultSpecification.LastUsedProjectPath = oldProjectPath;
                    await settings.SaveSettings();
                }

                try
                {
                    var method = typeof(MainWindowViewModel).GetMethod(
                        "OpenLastProjectFile",
                        BindingFlags.Instance | BindingFlags.NonPublic,
                        null,
                        new[] { typeof(string) },
                        null);
                    Assert.NotNull(method);

                    var openTask = (Task)method.Invoke(viewModel, new object[] { newProjectPath });
                    await openTask;

                    await viewModel.SaveTaskDialogCommand.ExecuteAsync(null);

                    var updatedOldProjectContent = File.ReadAllText(oldProjectPath);
                    var updatedNewProjectContent = File.ReadAllText(newProjectPath);

                    Assert.Equal(originalOldProjectContent, updatedOldProjectContent);
                    Assert.NotEqual(originalNewProjectContent, updatedNewProjectContent);

                    using (var settings = new SettingsReaderWriter(projectManager.AppDataPath))
                    {
                        Assert.Equal(newProjectPath, settings.DefaultSpecification.LastUsedProjectPath);
                    }
                }
                finally
                {
                    using (var settings = new SettingsReaderWriter(projectManager.AppDataPath))
                    {
                        settings.DefaultSpecification.LastUsedProjectPath = originalLastUsedPath;
                        await settings.SaveSettings();
                    }

                    if (Directory.Exists(tempRoot))
                    {
                        Directory.Delete(tempRoot, recursive: true);
                    }
                }
            });
        }

    }

    internal class DeferredSettingsBootstrapper : ISettingsBootstrapper
    {
        private readonly TaskCompletionSource<SettingsBootstrapResult> completionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<SettingsBootstrapResult> LoadAsync() => completionSource.Task;

        public Task SaveAsync(Action<DefaultSpecification> updateAction) => Task.CompletedTask;

        public void Complete(SettingsBootstrapResult result) => completionSource.TrySetResult(result);
    }

    internal class NoopUpdateNotifier : IUpdateNotifier
    {
        public void Dispose()
        {
        }

        public void StartUpdateCheck(Func<Task> onUpdateAvailable)
        {
        }

        public Task TriggerUpdateCheckAsync(Func<Task> onUpdateAvailable) => Task.CompletedTask;
    }

    internal class FakeContextMenuBuilder : IContextMenuBuilder
    {
        public ObservableCollection<MenuItem> BuildEmptySpaceMenu(
            System.Windows.Input.ICommand createGenericTaskCommand,
            System.Windows.Input.ICommand createGenericChipTaskCommand,
            System.Windows.Input.ICommand createClassicTaskCommand,
            System.Windows.Input.ICommand createDesfireTaskCommand,
            System.Windows.Input.ICommand createUltralightTaskCommand) => new() { new MenuItem() };

        public ObservableCollection<MenuItem> BuildEmptyTreeMenu(System.Windows.Input.ICommand readChipCommand) => new() { new MenuItem() };

        public ObservableCollection<MenuItem> BuildNodeMenu(
            System.Windows.Input.ICommand addNewTaskCommand,
            System.Windows.Input.ICommand addOrEditCommand,
            System.Windows.Input.ICommand deleteCommand,
            System.Windows.Input.ICommand executeSelectedCommand,
            System.Windows.Input.ICommand resetSelectedCommand,
            System.Windows.Input.ICommand executeAllCommand,
            System.Windows.Input.ICommand resetAllCommand,
            System.Windows.Input.ICommand resetReportPathCommand) => new() { new MenuItem() };
    }

    internal class FakeAppStartupInitializer : IAppStartupInitializer
    {
        public AppStartupContext Initialize() => new AppStartupContext(null, new Mutex(), Array.Empty<string>());
    }

    internal class FakeTimerFactory : ITimerFactory
    {
        public TimerInitializationResult CreateTimers(EventHandler triggerReadHandler, EventHandler taskTimeoutHandler)
        {
            return new TimerInitializationResult(CreateTriggerReadTimer(triggerReadHandler), CreateTaskTimeoutTimer(taskTimeoutHandler));
        }

        public DispatcherTimer CreateTaskTimeoutTimer(EventHandler tickHandler)
        {
            var timer = new DispatcherTimer();
            timer.Tick += tickHandler;
            return timer;
        }

        public DispatcherTimer CreateTriggerReadTimer(EventHandler tickHandler)
        {
            var timer = new DispatcherTimer();
            timer.Tick += tickHandler;
            return timer;
        }
    }

    internal class FakeTaskServiceInitializer : ITaskServiceInitializer
    {
        public TaskServiceInitialization Initialize(
            Action notifyChipTasksChanged,
            Action activateMainWindow,
            Action<bool> updateReaderBusy,
            EventHandler<TaskExecutionCompletedEventArgs> executionCompletedHandler,
            DispatcherTimer triggerReadChip,
            DispatcherTimer taskTimeout)
        {
            var taskExecutionService = new FakeTaskExecutionService();
            if (executionCompletedHandler != null)
            {
                taskExecutionService.ExecutionCompleted += executionCompletedHandler;
            }

            return new TaskServiceInitialization(new TaskDialogFactory(notifyChipTasksChanged, activateMainWindow, updateReaderBusy), taskExecutionService);
        }
    }

    internal class FakeTaskExecutionService : ITaskExecutionService
    {
        public event EventHandler<TaskExecutionCompletedEventArgs>? ExecutionCompleted;

        public int CurrentTaskIndex { get; private set; }

        public Task<TaskExecutionResult> ExecuteOnceAsync(TaskExecutionRequest request, CancellationToken cancellationToken = default)
        {
            CurrentTaskIndex++;
            return Task.FromResult(new TaskExecutionResult());
        }

        public void HandleTaskTimeout()
        {
            ExecutionCompleted?.Invoke(this, new TaskExecutionCompletedEventArgs(new TaskExecutionResult()));
        }
    }

    internal class FakeMenuInitializer : IMenuInitializer
    {
        public MenuInitializationResult Initialize(
            IContextMenuBuilder contextMenuBuilder,
            System.Windows.Input.ICommand addEditCommand,
            System.Windows.Input.ICommand deleteSelectedCommand,
            System.Windows.Input.ICommand writeSelectedOnceCommand,
            System.Windows.Input.ICommand resetSelectedStatusCommand,
            System.Windows.Input.ICommand writeToChipOnceCommand,
            System.Windows.Input.ICommand resetAllStatusCommand,
            System.Windows.Input.ICommand resetReportTaskDirectoryCommand,
            System.Windows.Input.ICommand readChipCommand,
            System.Windows.Input.ICommand createGenericTaskCommand,
            System.Windows.Input.ICommand createGenericChipTaskCommand,
            System.Windows.Input.ICommand createClassicTaskCommand,
            System.Windows.Input.ICommand createDesfireTaskCommand,
            System.Windows.Input.ICommand createUltralightTaskCommand)
        {
            var rowMenu = contextMenuBuilder.BuildNodeMenu(
                addEditCommand,
                addEditCommand,
                deleteSelectedCommand,
                writeSelectedOnceCommand,
                resetSelectedStatusCommand,
                writeToChipOnceCommand,
                resetAllStatusCommand,
                resetReportTaskDirectoryCommand);
            var emptySpaceMenu = contextMenuBuilder.BuildEmptySpaceMenu(
                createGenericTaskCommand,
                createGenericChipTaskCommand,
                createClassicTaskCommand,
                createDesfireTaskCommand,
                createUltralightTaskCommand);
            var emptyTreeMenu = contextMenuBuilder.BuildEmptyTreeMenu(readChipCommand);
            return new MenuInitializationResult(rowMenu, emptySpaceMenu, emptyTreeMenu);
        }
    }

    internal class FakeStartupArgumentProcessor : IStartupArgumentProcessor
    {
        public StartupArgumentResult Process(string[] args) => new StartupArgumentResult();
    }

    internal class NoopUpdateScheduler : IUpdateScheduler
    {
        public void Begin(Func<Task> onUpdateAvailable)
        {
        }

        public void Dispose()
        {
        }
    }

    internal class FakeReaderMonitor : IReaderMonitor
    {
        public void Dispose()
        {
        }

        public void Pause()
        {
        }

        public void Resume()
        {
        }

        public void StartMonitoring(TimerCallback callback)
        {
        }
    }

    internal class FakeProjectBootstrapper : IProjectBootstrapper
    {
        public Task BootstrapAsync(ProjectBootstrapRequest request) => Task.CompletedTask;
    }
}
