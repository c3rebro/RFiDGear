using System;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Threading;
using RFiDGear.Contracts;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.ExtensionHost;
using RFiDGear.Infrastructure.FileAccess;
using RFiDGear.Models;
using RFiDGear.Services;
using RFiDGear.Services.Factories;
using RFiDGear.Services.Interfaces;
using RFiDGear.Services.TaskExecution;
using RFiDGear.ViewModel;
using RFiDGear.ViewModel.DialogFactories;
using RFiDGear.ViewModel.TaskSetupViewModels;
using Xunit;

namespace RFiDGear.Tests
{
    [Collection("SettingsFileAccess")]
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
                var tempRoot = Path.Combine(Path.GetTempPath(), "RFiDGear.Tests", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempRoot);
                var oldProjectPath = Path.Combine(tempRoot, "old.xml");
                var newProjectPath = Path.Combine(tempRoot, "new.xml");
                var settingsDirectory = Path.Combine(tempRoot, "settings");
                var settingsPath = Path.Combine(settingsDirectory, "settings.xml");
                const string validProjectContent = "<?xml version=\"1.0\" encoding=\"utf-8\"?>"
                    + "<ChipTaskHandlerModel xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" "
                    + "xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\">"
                    + "<ManifestVersion>1.0.0</ManifestVersion><TaskCollection />"
                    + "<!-- original fixture marker --></ChipTaskHandlerModel>";
                var originalOldProjectContent = validProjectContent;
                var originalNewProjectContent = validProjectContent;
                File.WriteAllText(oldProjectPath, originalOldProjectContent);
                File.WriteAllText(newProjectPath, originalNewProjectContent);
                Directory.CreateDirectory(settingsDirectory);

                using (var settings = new SettingsReaderWriter(settingsDirectory, loadSettings: false))
                {
                    settings.DefaultSpecification = new DefaultSpecification(true);
                    settings.DefaultSpecification.LastUsedProjectPath = oldProjectPath;
                    settings.SaveSettings(settings.DefaultSpecification, settingsPath);
                }

                try
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
                        new FakeProjectBootstrapper(),
                        () => new SettingsReaderWriter(settingsDirectory));

                    var initializationTask = viewModel.InitializeAsync();
                    bootstrapper.Complete(new SettingsBootstrapResult
                    {
                        CurrentReaderName = "Reader",
                        DefaultReaderProvider = ReaderTypes.None,
                        Culture = CultureInfo.InvariantCulture
                    });
                    await initializationTask;

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

                    using (var settings = new SettingsReaderWriter(settingsDirectory))
                    {
                        Assert.Equal(newProjectPath, settings.DefaultSpecification.LastUsedProjectPath);
                    }
                }
                finally
                {
                    if (Directory.Exists(tempRoot))
                    {
                        Directory.Delete(tempRoot, recursive: true);
                    }
                }
            });
        }

        [Fact]
        public async Task OpenProject_LoadsIntoLiveTaskModel_KeepsChipTree_AndRejectedOpenKeepsIt()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var tempRoot = Path.Combine(Path.GetTempPath(), "RFiDGear.Tests", Guid.NewGuid().ToString("N"));
                var settingsDirectory = Path.Combine(tempRoot, "settings");
                var settingsPath = Path.Combine(settingsDirectory, "settings.xml");
                Directory.CreateDirectory(settingsDirectory);

                var projectPath = ProjectFixtures.CreateProject(2);
                var corruptPath = ProjectFixtures.CreateNotAnArchive();

                using (var settings = new SettingsReaderWriter(settingsDirectory, loadSettings: false))
                {
                    settings.DefaultSpecification = new DefaultSpecification(true);
                    settings.SaveSettings(settings.DefaultSpecification, settingsPath);
                }

                try
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
                        new FakeProjectBootstrapper(),
                        () => new SettingsReaderWriter(settingsDirectory),
                        // Hermetic: project extraction and staging stay in this test's folder.
                        () => new DatabaseReaderWriter(new ProjectManager(Path.Combine(tempRoot, "appdata"))));

                    var initializationTask = viewModel.InitializeAsync();
                    bootstrapper.Complete(new SettingsBootstrapResult
                    {
                        CurrentReaderName = "Reader",
                        DefaultReaderProvider = ReaderTypes.None,
                        Culture = CultureInfo.InvariantCulture
                    });
                    await initializationTask;

                    // A card detected by the reader: not part of any project, so opening one must not drop it.
                    var detectedChip = new RFiDChipParentLayerViewModel("detected chip");
                    viewModel.TreeViewParentNodes.Add(detectedChip);

                    var openProject = typeof(MainWindowViewModel).GetMethod(
                        "OpenLastProjectFile",
                        BindingFlags.Instance | BindingFlags.NonPublic,
                        null,
                        new[] { typeof(string) },
                        null);
                    Assert.NotNull(openProject);

                    await (Task)openProject!.Invoke(viewModel, new object[] { projectPath })!;

                    // The live, bound task model and the extension host facade both show the project.
                    Assert.Equal(2, viewModel.ChipTasks.TaskCollection.Count);
                    Assert.All(viewModel.ChipTasks.TaskCollection, t => Assert.IsType<CommonTaskViewModel>(t));
                    Assert.Equal(2, viewModel.ExtensionHost.Project.TaskCount);
                    Assert.Equal(Path.GetFullPath(projectPath), viewModel.ExtensionHost.Project.CurrentPath);
                    Assert.Same(detectedChip, Assert.Single(viewModel.TreeViewParentNodes));

                    // A rejected open leaves the live project and the remembered path untouched.
                    await (Task)openProject.Invoke(viewModel, new object[] { corruptPath })!;

                    Assert.Same(detectedChip, Assert.Single(viewModel.TreeViewParentNodes));
                    Assert.Equal(2, viewModel.ChipTasks.TaskCollection.Count);
                    Assert.Equal(2, viewModel.ExtensionHost.Project.TaskCount);
                    Assert.Equal(Path.GetFullPath(projectPath), viewModel.ExtensionHost.Project.CurrentPath);

                    using (var settings = new SettingsReaderWriter(settingsDirectory))
                    {
                        Assert.Equal(Path.GetFullPath(projectPath), settings.DefaultSpecification.LastUsedProjectPath);
                    }
                }
                finally
                {
                    File.Delete(projectPath);
                    File.Delete(corruptPath);
                    if (Directory.Exists(tempRoot))
                    {
                        Directory.Delete(tempRoot, recursive: true);
                    }
                }
            });
        }
        [Fact]
        public async Task ExtensionHost_SynchronousLoad_IsToldWhichThreadIsTheUiThread()
        {
            // Project.Load would deadlock on the UI thread, so the host must know which thread that is. Calling
            // Load here would hang the test if the wiring were missing, so the probe is checked directly.
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var tempRoot = Path.Combine(Path.GetTempPath(), "RFiDGear.Tests", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempRoot);
                try
                {
                    var viewModel = await CreateInitializedViewModelAsync(tempRoot);
                    const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
                    var project = typeof(HeadlessExtensionHost).GetField("_project", privateInstance)!.GetValue(viewModel.ExtensionHost)!;
                    var probe = (Func<bool>?)project.GetType().GetField("_isOnUiThread", privateInstance)!.GetValue(project);

                    Assert.NotNull(probe);
                    Assert.True(probe!(), "The thread the view model lives on is the UI thread.");
                    Assert.False(await Task.Run(probe), "Any other thread is not.");
                }
                finally
                {
                    Directory.Delete(tempRoot, recursive: true);
                }
            });
        }

        // ── Save / Save As ──

        /// <summary>
        /// Creates and initializes a view model whose settings and project staging live below <paramref name="tempRoot"/>.
        /// </summary>
        private static async Task<MainWindowViewModel> CreateInitializedViewModelAsync(string tempRoot, CultureInfo? culture = null)
        {
            var settingsDirectory = Path.Combine(tempRoot, "settings");
            Directory.CreateDirectory(settingsDirectory);

            using (var settings = new SettingsReaderWriter(settingsDirectory, loadSettings: false))
            {
                settings.DefaultSpecification = new DefaultSpecification(true);
                settings.SaveSettings(settings.DefaultSpecification, Path.Combine(settingsDirectory, "settings.xml"));
            }

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
                new FakeProjectBootstrapper(),
                () => new SettingsReaderWriter(settingsDirectory),
                () => new DatabaseReaderWriter(new ProjectManager(Path.Combine(tempRoot, "appdata"))));

            var initializationTask = viewModel.InitializeAsync();
            bootstrapper.Complete(new SettingsBootstrapResult
            {
                CurrentReaderName = "Reader",
                DefaultReaderProvider = ReaderTypes.None,
                Culture = culture ?? CultureInfo.InvariantCulture
            });
            await initializationTask;

            return viewModel;
        }

        private static async Task OpenProjectAsync(MainWindowViewModel viewModel, string path)
        {
            var openProject = typeof(MainWindowViewModel).GetMethod(
                "OpenLastProjectFile",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(string) },
                null);
            Assert.NotNull(openProject);

            await (Task)openProject!.Invoke(viewModel, new object[] { path })!;
        }

        private static string RememberedProjectPath(string tempRoot)
        {
            using (var settings = new SettingsReaderWriter(Path.Combine(tempRoot, "settings")))
            {
                return settings.DefaultSpecification.LastUsedProjectPath;
            }
        }

        private static string NewTempRoot()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "RFiDGear.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            return tempRoot;
        }

        [Fact]
        public async Task SaveProjectAs_WithoutExtension_RemembersTheFileThatWasActuallyWritten()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var tempRoot = NewTempRoot();
                var projectPath = ProjectFixtures.CreateProject(2);
                try
                {
                    var viewModel = await CreateInitializedViewModelAsync(tempRoot);
                    await OpenProjectAsync(viewModel, projectPath);
                    var typed = Path.Combine(tempRoot, "saved");   // no extension, as typed in the dialog

                    await viewModel.SaveProjectAsAsync(typed, setAsDefault: true);

                    var written = Path.GetFullPath(typed + ".rfPrj");
                    Assert.True(File.Exists(written));
                    Assert.Equal(written, viewModel.ExtensionHost.Project.CurrentPath);
                    // The typed name would never be a valid save path; the written file is remembered instead.
                    Assert.Equal(written, RememberedProjectPath(tempRoot));
                }
                finally
                {
                    File.Delete(projectPath);
                    Directory.Delete(tempRoot, recursive: true);
                }
            });
        }

        [Fact]
        public async Task SaveProjectAs_WithoutSettingDefault_MovesTheOpenProjectButKeepsTheRememberedOne()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var tempRoot = NewTempRoot();
                var projectPath = ProjectFixtures.CreateProject(2);
                try
                {
                    var viewModel = await CreateInitializedViewModelAsync(tempRoot);
                    await OpenProjectAsync(viewModel, projectPath);
                    var target = Path.Combine(tempRoot, "copy.rfPrj");

                    await viewModel.SaveProjectAsAsync(target, setAsDefault: false);

                    Assert.True(File.Exists(target));
                    Assert.Equal(Path.GetFullPath(target), viewModel.ExtensionHost.Project.CurrentPath);
                    Assert.Equal(Path.GetFullPath(projectPath), RememberedProjectPath(tempRoot));
                }
                finally
                {
                    File.Delete(projectPath);
                    Directory.Delete(tempRoot, recursive: true);
                }
            });
        }

        [Fact]
        public async Task SaveProjectAs_WhenTheWriteFails_ChangesNothingAndShowsAMessage()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var tempRoot = NewTempRoot();
                var projectPath = ProjectFixtures.CreateProject(2);
                try
                {
                    var viewModel = await CreateInitializedViewModelAsync(tempRoot);
                    await OpenProjectAsync(viewModel, projectPath);
                    var unwritable = Path.Combine(tempRoot, "no-such-directory", "p.rfPrj");

                    await viewModel.SaveProjectAsAsync(unwritable, setAsDefault: true);   // must not throw

                    Assert.Equal(Path.GetFullPath(projectPath), viewModel.ExtensionHost.Project.CurrentPath);
                    Assert.Equal(Path.GetFullPath(projectPath), RememberedProjectPath(tempRoot));
                    Assert.Single(viewModel.Dialogs);
                }
                finally
                {
                    File.Delete(projectPath);
                    Directory.Delete(tempRoot, recursive: true);
                }
            });
        }

        [Fact]
        public async Task Save_AfterSaveAsWithoutSettingDefault_WritesTheOpenProjectAndNotTheOldDefault()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var tempRoot = NewTempRoot();
                var projectPath = ProjectFixtures.CreateProject(2);
                try
                {
                    var viewModel = await CreateInitializedViewModelAsync(tempRoot);
                    await OpenProjectAsync(viewModel, projectPath);
                    var copy = Path.Combine(tempRoot, "copy.rfPrj");
                    await viewModel.SaveProjectAsAsync(copy, setAsDefault: false);
                    var originalBytes = File.ReadAllBytes(projectPath);

                    // Edit the project, then plain Save.
                    viewModel.ChipTasks.TaskCollection.Add(new CommonTaskViewModel { CurrentTaskIndex = "2" });
                    await viewModel.SaveTaskDialogCommand.ExecuteAsync(null);

                    // The old default was not overwritten; the open project (the copy) now has three tasks.
                    Assert.Equal(originalBytes, File.ReadAllBytes(projectPath));
                    var reloaded = await viewModel.ExtensionHost.Project.LoadAsync(copy);
                    Assert.True(reloaded.Success, $"LoadAsync failed: {reloaded.Error}");
                    Assert.Equal(3, reloaded.TaskCount);
                }
                finally
                {
                    File.Delete(projectPath);
                    Directory.Delete(tempRoot, recursive: true);
                }
            });
        }

        [Fact]
        public async Task Save_WhenTheOpenProjectsFileIsGone_DoesNotWriteItIntoTheRememberedProject()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var tempRoot = NewTempRoot();
                var rememberedProject = ProjectFixtures.CreateProject(2);
                try
                {
                    var viewModel = await CreateInitializedViewModelAsync(tempRoot);
                    await OpenProjectAsync(viewModel, rememberedProject);

                    // Project B is now the open one; A stays the remembered default.
                    var openProject = Path.Combine(tempRoot, "b.rfPrj");
                    await viewModel.SaveProjectAsAsync(openProject, setAsDefault: false);
                    var rememberedBytes = File.ReadAllBytes(rememberedProject);
                    File.Delete(openProject);   // B was moved or deleted

                    // Falling back to A would silently overwrite it with B's contents.
                    Assert.Null(viewModel.ResolveProjectSaveTarget());

                    await viewModel.SaveTaskDialogCommand.ExecuteAsync(null);

                    Assert.Equal(rememberedBytes, File.ReadAllBytes(rememberedProject));
                    Assert.False(File.Exists(openProject));
                    Assert.Single(viewModel.Dialogs);   // Save As was offered instead
                }
                finally
                {
                    File.Delete(rememberedProject);
                    Directory.Delete(tempRoot, recursive: true);
                }
            });
        }

        [Fact]
        public async Task ResolveProjectSaveTarget_WithNoProjectOpen_UsesTheRememberedDefault()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var tempRoot = NewTempRoot();
                var rememberedProject = ProjectFixtures.CreateProject(1);
                try
                {
                    var viewModel = await CreateInitializedViewModelAsync(tempRoot);
                    Assert.Null(viewModel.ExtensionHost.Project.CurrentPath);   // nothing is open

                    using (var settings = new SettingsReaderWriter(Path.Combine(tempRoot, "settings")))
                    {
                        settings.DefaultSpecification.LastUsedProjectPath = rememberedProject;
                        await settings.SaveSettings();
                    }

                    Assert.Equal(rememberedProject, viewModel.ResolveProjectSaveTarget());

                    // A remembered path that no longer exists means there is nothing to save to.
                    File.Delete(rememberedProject);
                    Assert.Null(viewModel.ResolveProjectSaveTarget());
                }
                finally
                {
                    File.Delete(rememberedProject);
                    Directory.Delete(tempRoot, recursive: true);
                }
            });
        }
        // ── extension host as an extension sees it ──

        /// <summary>Stands in for an extension: it only declares the import the contract documents.</summary>
        public sealed class HostImporter
        {
            [Import]
            public IRFiDGearExtensionHost? Host { get; set; }
        }

        [Theory]
        [InlineData("de-DE", "de-DE")]
        [InlineData("en-US", "en-US")]
        public async Task ExtensionHost_AppContext_FollowsTheApplicationCulture(string applicationCulture, string expected)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var tempRoot = NewTempRoot();
                try
                {
                    var viewModel = await CreateInitializedViewModelAsync(tempRoot, new CultureInfo(applicationCulture));

                    IRFiDGearExtensionHost api = viewModel.ExtensionHost;

                    Assert.Equal(expected, api.AppContext.UICultureName);
                }
                finally
                {
                    Directory.Delete(tempRoot, recursive: true);
                }
            });
        }

        [Fact]
        public async Task ExtensionHost_CanBeImportedThroughMef_AsAnExtensionDoesIt()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var tempRoot = NewTempRoot();
                try
                {
                    var viewModel = await CreateInitializedViewModelAsync(tempRoot);
                    var extension = new HostImporter();

                    // The host application exports the service from its view model; an extension imports it.
                    using var container = new CompositionContainer();
                    var batch = new CompositionBatch();
                    batch.AddPart(viewModel);
                    batch.AddPart(extension);
                    container.Compose(batch);

                    Assert.Same(viewModel.ExtensionHost, extension.Host);

                    // The first things an extension does with it.
                    Assert.Equal(0, extension.Host!.Project.TaskCount);
                    Assert.Null(extension.Host.Project.CurrentPath);
                    Assert.False(extension.Host.Execution.IsRunning);
                    Assert.False(string.IsNullOrWhiteSpace(extension.Host.AppContext.RFiDGearVersion));
                    Assert.False(extension.Host.AutoMode.IsAutoModeEnabled);
                }
                finally
                {
                    Directory.Delete(tempRoot, recursive: true);
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
        public AppStartupContext Initialize() => new AppStartupContext(new Mutex(), Array.Empty<string>());
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
