/// <summary>
/// Main application view model that orchestrates startup, UI binding, reader monitoring,
/// and task execution lifecycles. Instances are exported via MEF under the name
/// <c>"MainWin"</c> so XAML bindings can resolve this view model from the shared locator.
/// </summary>

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Serilog;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using RFiDGear.Infrastructure.DI;

using RedCell.Diagnostics.Update;
using RFiDGear.Models;
using RFiDGear.Services;
using RFiDGear.Services.AutoMode;
using RFiDGear.Bridge;
using RFiDGear.Services.ExternalControl;
using RFiDGear.Services.Sam;
using RFiDGear.Services.SecureKeys;
using RFiDGear.Services.TaskExecution;
using RFiDGear.ViewModel.DialogFactories;
using RFiDGear.ViewModel.TaskSetupViewModels;
using RFiDGear.Services.Interfaces;
using RFiDGear.Services.Factories;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.Tasks;
using RFiDGear.Infrastructure.ReaderProviders;
using RFiDGear.Infrastructure.Ultralight;
using RFiDGear.Infrastructure.FileAccess;
using RFiDGear.Infrastructure.ExtensionHost;
using RFiDGear.Infrastructure.ExtensionPackages;
using RFiDGear.UI.MVVMDialogs.ViewModels;
using RFiDGear.UI.MVVMDialogs.ViewModels.Interfaces;
using RFiDGear.Infrastructure.Tasks.Interfaces;
using RFiDGear.Contracts;
using RFiDGear.UI.UIExtensions;
using RFiDGear.UI.UIExtensions.Interfaces;
using System.ComponentModel.Composition;

namespace RFiDGear.ViewModel
{
    /// <summary>
    /// Description of MainWindowViewModel.
    /// </summary>
    [ExportViewModel("MainWin")]
    public class MainWindowViewModel : ObservableObject
    {
        private readonly Version Version = Assembly.GetExecutingAssembly().GetName().Version;
#nullable enable
#nullable disable
        private readonly string[] args;
        private readonly Dictionary<string, string> variablesFromArgs = new Dictionary<string, string>();
        private readonly Updater updater;
        private TaskDialogFactory taskDialogFactory;
        private ITaskListEditGate taskListEditGate;
        private ITaskExecutionService taskExecutionService;
        private readonly ISettingsBootstrapper settingsBootstrapper;
        private readonly Func<SettingsReaderWriter> settingsReaderWriterFactory;
        private readonly Func<DatabaseReaderWriter> databaseReaderWriterFactory;
        private readonly IUpdateNotifier updateNotifier;
        private readonly IContextMenuBuilder contextMenuBuilder;
        private readonly IStartupArgumentProcessor startupArgumentProcessor;
        private readonly IUpdateScheduler updateScheduler;
        private readonly IReaderMonitor readerMonitor;
        private readonly IProjectBootstrapper projectBootstrapper;
        private readonly ITimerFactory timerFactory;
        private readonly ITaskServiceInitializer taskServiceInitializer;
        private readonly IMenuInitializer menuInitializer;

        private HeadlessExtensionHost extensionHost;
        private ExternalControlService externalControl;

        // Which run the window is running and who asked for it (by hand, or automatic mode): a supplier of keys is told.
        private readonly WindowRunMarker _windowRuns = new WindowRunMarker();

        // What the window knows about a run when it takes it (the revision of the project), for the result that is made later.
        private readonly RunFacts _runFacts = new RunFacts();

        /// <summary>Gets what the window notes about its runs, for the bridge that reports their results.</summary>
        internal RunFacts RunFacts => _runFacts;
        private readonly string externalControlPipeName;
        private readonly string externalControlJournalDirectory;
        private readonly OnceGate startupOnce = new OnceGate();
        private ExtensionOperationCoordinator _coordinator;
        private AutoModeController _autoMode;
        private Dispatcher uiDispatcher;
        private readonly ReaderAccessGate _readerGate = new ReaderAccessGate();

        /// <summary>The gate that lets one party at a time read a card: the reads of this window and the scans of extensions.</summary>
        internal ReaderAccessGate ReaderGate => _readerGate;

        /// <summary>The external control bridge, or <see langword="null"/> when it is off (the setting and the start-up argument both say no, or the pipe could not be opened).</summary>
        internal ExternalControlService ExternalControl => externalControl;

        /// <summary>The state machine of automatic mode: the menu toggle, the start-up argument and the host all drive it.</summary>
        internal AutoModeController AutoMode => _autoMode;
        private readonly IApplicationExtensionLauncher applicationExtensionLauncher;

        private protected MainWindow mw;
        private protected DatabaseReaderWriter databaseReaderWriter;
        private protected ReportReaderWriter reportReaderWriter;
        private protected DispatcherTimer triggerReadChip;
        private protected DispatcherTimer taskTimeout;
        private protected string reportOutputPath;

        // The report target of the start-up arguments as it was typed ("C:\Reports\card-???.pdf"). The number of a pattern is looked up
        // for each run, so that a series of cards does not write one file again and again. reportOutputPath is only what a person chose in the
        // window: it stays what it is until the report target paths are reset.
        private string reportOutputPattern;
        private protected string reportTemplateFile;
        private protected ChipTaskHandlerModel taskHandler;
        private protected List<MifareClassicChipModel> mifareClassicUidModels = new List<MifareClassicChipModel>();
        private protected List<MifareDesfireChipModel> mifareDesfireViewModels = new List<MifareDesfireChipModel>();
        private protected bool _runSelectedOnly;

        // set if task was completed; indicates greenlight to continue execution
        // if programming takes too long; quit the process
        private bool userIsNotifiedForAvailableUpdate = false;
        private protected Mutex mutex;
        private Task initializationTask;

        // one reader, one instance - only

        #region Constructors

        public MainWindowViewModel()
            : this(
                  new SettingsBootstrapper(),
                  new UpdateNotifier(),
                  new ContextMenuBuilder(),
                  new AppStartupInitializer(),
                  new MainWindowTimerFactory(),
                  new TaskServiceInitializer(),
                  new MenuInitializer(),
                  new StartupArgumentProcessor(),
                  new UpdateScheduler(),
                  new ReaderMonitor(),
                  new ProjectBootstrapper())
        {
        }

        public MainWindowViewModel(
            ISettingsBootstrapper settingsBootstrapper,
            IUpdateNotifier updateNotifier,
            IContextMenuBuilder contextMenuBuilder,
            IAppStartupInitializer appStartupInitializer,
            ITimerFactory timerFactory,
            ITaskServiceInitializer taskServiceInitializer,
            IMenuInitializer menuInitializer,
            IStartupArgumentProcessor startupArgumentProcessor,
            IUpdateScheduler updateScheduler,
            IReaderMonitor readerMonitor,
            IProjectBootstrapper projectBootstrapper,
            Func<SettingsReaderWriter> settingsReaderWriterFactory = null,
            Func<DatabaseReaderWriter> databaseReaderWriterFactory = null,
            IApplicationExtensionLauncher applicationExtensionLauncher = null,
            string externalControlPipeName = null,
            string externalControlJournalDirectory = null)
        {
            // Tests that switch the bridge on give it a pipe and a journal folder of their own; the application uses the pipe of
            // the user and the folder below %LocalAppData%.
            this.externalControlPipeName = externalControlPipeName;
            this.externalControlJournalDirectory = externalControlJournalDirectory;
            this.settingsBootstrapper = settingsBootstrapper ?? throw new ArgumentNullException(nameof(settingsBootstrapper));
            this.updateNotifier = updateNotifier ?? throw new ArgumentNullException(nameof(updateNotifier));
            this.contextMenuBuilder = contextMenuBuilder ?? throw new ArgumentNullException(nameof(contextMenuBuilder));
            this.timerFactory = timerFactory ?? throw new ArgumentNullException(nameof(timerFactory));
            this.taskServiceInitializer = taskServiceInitializer ?? throw new ArgumentNullException(nameof(taskServiceInitializer));
            this.menuInitializer = menuInitializer ?? throw new ArgumentNullException(nameof(menuInitializer));
            this.startupArgumentProcessor = startupArgumentProcessor ?? throw new ArgumentNullException(nameof(startupArgumentProcessor));
            this.updateScheduler = updateScheduler ?? throw new ArgumentNullException(nameof(updateScheduler));
            this.readerMonitor = readerMonitor ?? throw new ArgumentNullException(nameof(readerMonitor));
            this.projectBootstrapper = projectBootstrapper ?? throw new ArgumentNullException(nameof(projectBootstrapper));
            this.settingsReaderWriterFactory = settingsReaderWriterFactory ?? (() => new SettingsReaderWriter());
            this.databaseReaderWriterFactory = databaseReaderWriterFactory ?? (() => new DatabaseReaderWriter());
            this.applicationExtensionLauncher = applicationExtensionLauncher ?? new ApplicationExtensionLauncher(ShowExtensionError);

            if (appStartupInitializer == null)
            {
                throw new ArgumentNullException(nameof(appStartupInitializer));
            }

            IsReaderBusy = false;

            var startupContext = appStartupInitializer.Initialize();
            mutex = startupContext.Mutex;
            args = startupContext.Arguments;

            updater = new Updater();
        }

        /// <summary>
        /// Starts asynchronous initialization, ensuring the sequence only runs once per
        /// application lifetime even if invoked multiple times by the view layer.
        /// </summary>
        /// <returns>A task that completes when startup dependencies and timers are ready.</returns>
        public Task InitializeAsync()
        {
            initializationTask ??= InitializeInternalAsync();
            return initializationTask;
        }

        /// <summary>
        /// Performs the full startup sequence: loads persisted settings, configures reader
        /// timers, initializes task services, and wires UI collections for data binding.
        /// </summary>
        private async Task InitializeInternalAsync()
        {
            var bootstrapResult = await settingsBootstrapper.LoadAsync();

            CurrentReader = bootstrapResult.CurrentReaderName;
            ReaderDevice.Reader = bootstrapResult.DefaultReaderProvider;
            culture = bootstrapResult.Culture;

            if (bootstrapResult.IsRdpSession)
            {
                IsRdpWarningVisible = true;
                RdpWarningMessage = ResourceLoader.GetResource("labelRdpStatusBar");
            }

            var timerInitialization = timerFactory.CreateTimers(UpdateChip, TaskTimeout);
            triggerReadChip = timerInitialization.TriggerReadTimer;
            taskTimeout = timerInitialization.TaskTimeoutTimer;

            TreeViewParentNodes = new ObservableCollection<RFiDChipParentLayerViewModel>();

            ChipTasks = new ChipTaskHandlerModel();

            ReaderStatus = "";
            DateTimeStatusBar = "";
            databaseReaderWriter = databaseReaderWriterFactory();
            resLoader = new ResourceLoader();

            _coordinator = new ExtensionOperationCoordinator();
            taskListEditGate = new CoordinatorTaskListEditGate(_coordinator, ShowProjectBusy);

            var taskServices = taskServiceInitializer.Initialize(
                () => OnPropertyChanged(nameof(ChipTasks)),
                () => mw?.Activate(),
                status => IsReaderBusy = status,
                TaskExecutionService_ExecutionCompleted,
                triggerReadChip,
                taskTimeout,
                taskListEditGate);

            taskDialogFactory = taskServices.TaskDialogFactory;
            taskExecutionService = taskServices.TaskExecutionService;

            uiDispatcher = Dispatcher.CurrentDispatcher;
            var owningDispatcher = uiDispatcher;

            // Before the host, which hands its state to extensions. What the machine needs of the host (to store what
            // the reader showed) it reads when it is called, not now.
            _autoMode = new AutoModeController(
                _coordinator,
                poll: PollReaderForAutoModeAsync,
                // The run reads the card itself (the card may have been swapped since the poll), so it needs no UID.
                runCard: (_, keepApplicationOpen, isStillWanted) => RunCardForAutoModeAsync(keepApplicationOpen, isStillWanted),
                setClock: enabled => triggerReadChip.IsEnabled = enabled,
                post: action =>
                {
                    if (owningDispatcher.CheckAccess())
                    {
                        action();
                    }
                    else
                    {
                        owningDispatcher.InvokeAsync(action);
                    }
                },
                isReaderSelected: () => ReaderDevice.Instance != null,
                hasTasks: () => (taskHandler?.TaskCollection?.Count ?? 0) > 0,
                runApprovedCard: (_, keepApplicationOpen, isStillWanted, approval) => RunCardForAutoModeAsync(keepApplicationOpen, isStillWanted, approval));

            // The menu item shows the state of the machine, whoever changed it (the click, the start-up argument, or an
            // extension through the host). The state can change on any thread; the binding is raised on the UI thread.
            _autoMode.StateChanged += (_, _) => owningDispatcher.InvokeAsync(() =>
            {
                OnPropertyChanged(nameof(IsWriteToAllChipAutoChecked));
                OnPropertyChanged(nameof(IsWriteSelectedToChipAutoChecked));
            });

            extensionHost = new HeadlessExtensionHost(
                databaseReaderWriter,
                taskExecutionService,
                liveModelAccessor: () => taskHandler,
                dispatch: action => owningDispatcher.InvokeAsync(action).Task,
                applyToLiveApp: loadedModel =>
                {
                    // Runs on the WPF dispatcher thread — safe to mutate bound collections.
                    // TreeViewParentNodes is deliberately left alone: it shows the cards detected by
                    // the reader, which are not part of a project, and must survive opening one.
                    if (taskHandler.TaskCollection != null)
                    {
                        taskHandler.TaskCollection.Clear();
                    }
                    else
                    {
                        taskHandler.TaskCollection = new System.Collections.ObjectModel.ObservableCollection<object>();
                    }

                    foreach (var task in loadedModel?.TaskCollection ?? System.Linq.Enumerable.Empty<object>())
                    {
                        taskHandler.TaskCollection.Add(task);
                    }

                    OnPropertyChanged(nameof(ChipTasks));
                },
                readerName: string.Empty,
                coordinator: _coordinator,
                getConfiguredLanguage: () => culture?.Name == "de-DE" ? "german" : "english",
                isOnUiThread: () => owningDispatcher.CheckAccess(),
                getReaderName: () => CurrentReader,
                readerGate: _readerGate,
                autoMode: new AutoModeHostImpl(_autoMode, _coordinator));

            // The Extensions menu entries were built when MEF composed this view model, before the host existed, and
            // stay disabled until now.
            NotifyApplicationExtensionCommandsCanExecuteChanged();

            // As soon as the host exists: a program that waits for RFiDGear can connect and ask how far start-up is.
            StartExternalControl(bootstrapResult);

            var deleteSelectedCommand = new RelayCommand(() => TryDeleteSelectedTask());

            var menuInitialization = menuInitializer.Initialize(
                contextMenuBuilder,
                GetAddEditCommand,
                deleteSelectedCommand,
                WriteSelectedTaskToChipOnceCommand,
                ResetSelectedTaskStatusCommand,
                WriteToChipOnceCommand,
                ResetTaskStatusCommand,
                ResetReportTaskDirectoryCommand,
                ReadChipCommand,
                CreateGenericTaskCommand,
                CreateGenericChipTaskCommand,
                CreateClassicTaskCommand,
                CreateDesfireTaskCommand,
                CreateUltralightTaskCommand);

            rowContextMenuItems = menuInitialization.RowContextMenuItems;
            emptySpaceContextMenuItems = menuInitialization.EmptySpaceContextMenuItems;
            emptySpaceTreeViewContextMenu = menuInitialization.EmptySpaceTreeViewContextMenu;

            var appDispatcher = Application.Current?.Dispatcher;
            var mainWindow = (appDispatcher != null && appDispatcher.CheckAccess())
                ? Application.Current.MainWindow
                : null;
            if (mainWindow != null)
            {
                mainWindow.Closing += new CancelEventHandler(CloseThreads);
                mainWindow.Activated += new EventHandler(LoadCompleted);

                if (mainWindow.IsActive)
                {
                    LoadCompleted(mainWindow, EventArgs.Empty);
                }
                else
                {
                    // Does nothing unless the bridge is on.
                    ArmStartupFallback(
                        () => mainWindow.IsVisible,
                        handler => mainWindow.ContentRendered += handler,
                        handler => mainWindow.ContentRendered -= handler,
                        () => LoadCompleted(mainWindow, EventArgs.Empty));
                }
            }
        }

        /// <summary>
        /// Opens the pipe of the external control bridge when the settings or the start-up argument ask for it. A bridge that
        /// cannot start (the pipe name is taken, access is denied) is logged and left off: it never keeps RFiDGear from starting.
        /// </summary>
        /// <param name="bootstrapResult">What the settings said.</param>
        private void StartExternalControl(SettingsBootstrapResult bootstrapResult)
        {
            if (!bootstrapResult.EnableExternalControl && !StartupArgumentProcessor.IsExternalControlRequested(args))
            {
                return;
            }

            var dispatcher = uiDispatcher;

            try
            {
                var service = new ExternalControlService(
                    extensionHost,
                    action => dispatcher.InvokeAsync(action).Task,
                    () => IsReaderInitializing,
                    string.Format(CultureInfo.InvariantCulture, "{0}.{1}.{2}", Version.Major, Version.Minor, Version.Build),
                    externalControlPipeName ?? PipeNames.ForCurrentUser(),
                    busyActivity: () => ExternalControlService.ActivityOf(_coordinator),
                    journalDirectory: externalControlJournalDirectory ?? RunJournal.DefaultDirectory,
                    keyProvider: BridgeKeyProvider.Shared,
                    windowRunSource: _windowRuns.SourceOf,
                    runFacts: _runFacts);

                // The status bar shows whether a program is connected; the server raises the change on its own threads.
                service.ConnectionChanged += (_, _) =>
                {
                    if (!dispatcher.HasShutdownStarted)
                    {
                        dispatcher.InvokeAsync(() => IsExternalControlConnected = service.IsConnected);
                    }
                };

                if (service.Start())
                {
                    externalControl = service;
                }
                else
                {
                    service.Dispose();
                }
            }
            catch (Exception e)
            {
                Log.ForContext<MainWindowViewModel>().Warning("External control could not be started and stays off ({ExceptionType})", e.GetType().FullName);
            }
        }

        /// <summary>
        /// A window that is started in the background may never be activated, and the whole start-up (the reader monitor, the
        /// project that is loaded at start) waits for that. A program that waits for RFiDGear would wait with it, so when the
        /// bridge is on the start-up also runs a moment after the window has been drawn. The first of the two triggers wins.
        /// </summary>
        /// <param name="isDrawn">Says whether the window has been drawn already.</param>
        /// <param name="subscribeDrawn">Subscribes to the event that is raised when the window has been drawn.</param>
        /// <param name="unsubscribeDrawn">Takes the subscription back.</param>
        /// <param name="run">Starts the start-up; it goes through the same gate as the activation, so it runs only if nothing else did.</param>
        /// <param name="delay">How long after the window was drawn to wait for the activation; 1.5 seconds when <see langword="null"/>.</param>
        /// <returns><see langword="true"/> when the fallback was armed; <see langword="false"/> when the bridge is off (nothing waits for the start-up, so nothing changes).</returns>
        internal bool ArmStartupFallback(Func<bool> isDrawn, Action<EventHandler> subscribeDrawn, Action<EventHandler> unsubscribeDrawn, Action run, TimeSpan? delay = null)
        {
            if (externalControl == null)
            {
                return false;
            }

            var wait = delay ?? StartupFallbackDelay;

            void Arm()
            {
                Task.Delay(wait).ContinueWith(
                    _ => uiDispatcher.InvokeAsync(() =>
                    {
                        if (startupOnce.HasEntered)
                        {
                            return;
                        }

                        Log.ForContext<MainWindowViewModel>().Information("Start-up was run by the fallback: the main window was drawn but never activated");
                        run();
                    }),
                    TaskScheduler.Default);
            }

            if (isDrawn())
            {
                Arm();

                return true;
            }

            EventHandler drawn = null;
            drawn = (_, _) =>
            {
                unsubscribeDrawn(drawn);
                Arm();
            };
            subscribeDrawn(drawn);

            return true;
        }

        private static readonly TimeSpan StartupFallbackDelay = TimeSpan.FromMilliseconds(1500);

        #endregion Constructors

        #region Dialogs

        /// <summary>
        /// 
        /// </summary>
        private protected ObservableCollection<IDialogViewModel> dialogs = new ObservableCollection<IDialogViewModel>();
        public ObservableCollection<IDialogViewModel> Dialogs => dialogs;

        #endregion Dialogs

        #region Extension host

        /// <summary>
        /// Stable host service for extensions. Provides project, execution, reader, and auto-mode access without
        /// requiring a direct reference to this view model. It is <c>null</c> until start-up has built it.
        /// </summary>
        /// <remarks>
        /// Deliberately not a MEF export: it did not exist yet when MEF composed the extensions, so an import was
        /// empty. Application extensions receive it as the argument of
        /// <c>IApplicationUIExtension.CreateContent</c> when the user opens them.
        /// </remarks>
        public IRFiDGearExtensionHost ExtensionHost => extensionHost;

        #endregion Extension host

        #region Application-level UI extensions

        /// <summary>
        /// Receives all <see cref="IUIExtension"/> exports from the MEF container.
        /// The setter filters to <see cref="HostPlacement.Application"/> and builds
        /// <see cref="ApplicationExtensionMenuItems"/>.
        /// </summary>
        [ImportMany()]
        public Lazy<IUIExtension, IUIExtensionDetails>[] ApplicationExtensionItems
        {
            get => _applicationExtensionItems;
            set
            {
                _applicationExtensionItems = (from g in value
                                              where g.Metadata.HostPlacement == HostPlacement.Application
                                              orderby g.Metadata.SortOrder, g.Metadata.Name
                                              select g).ToArray();

                _applicationExtensionMenuItems.Clear();
                _applicationExtensionCommands.Clear();
                foreach (var ext in _applicationExtensionItems)
                {
                    // Only the metadata is read here. The exported class is created when the user opens the entry.
                    var extension = ext;
                    var command = new RelayCommand(() => OpenApplicationExtension(extension), () => extensionHost != null);
                    _applicationExtensionCommands.Add(command);
                    _applicationExtensionMenuItems.Add(new ExtensionMenuItem(ext.Metadata.Name, command));
                }

                OnPropertyChanged(nameof(ApplicationExtensionItems));
                OnPropertyChanged(nameof(HasApplicationExtensions));
            }
        }
        private Lazy<IUIExtension, IUIExtensionDetails>[] _applicationExtensionItems = Array.Empty<Lazy<IUIExtension, IUIExtensionDetails>>();

        private readonly ObservableCollection<ExtensionMenuItem> _applicationExtensionMenuItems = new ObservableCollection<ExtensionMenuItem>();
        private readonly List<RelayCommand> _applicationExtensionCommands = new List<RelayCommand>();

        /// <summary>
        /// Menu items for application-level extensions; bound to the Extensions menu in the main window.
        /// </summary>
        public ObservableCollection<ExtensionMenuItem> ApplicationExtensionMenuItems => _applicationExtensionMenuItems;

        /// <summary>
        /// True when at least one application-level extension is loaded; controls Extensions menu visibility.
        /// </summary>
        public bool HasApplicationExtensions => _applicationExtensionMenuItems.Count > 0;

        /// <summary>
        /// Opens one application extension. The menu entry is disabled until the host service exists, but a command
        /// can still be executed directly, so this checks it too: an extension must never be handed a missing host.
        /// </summary>
        private void OpenApplicationExtension(Lazy<IUIExtension, IUIExtensionDetails> extension)
        {
            var host = extensionHost;
            if (host == null)
            {
                Log.ForContext<MainWindowViewModel>().Warning(
                    "Application extension {ExtensionName} was opened before the host service was ready",
                    extension.Metadata.Name);
                ShowExtensionError(extension.Metadata.Name);
                return;
            }

            applicationExtensionLauncher.Open(extension, host);
        }

        /// <summary>
        /// Re-evaluates whether the Extensions menu entries can be used. Called once the host service exists.
        /// </summary>
        private void NotifyApplicationExtensionCommandsCanExecuteChanged()
        {
            foreach (var command in _applicationExtensionCommands)
            {
                command.NotifyCanExecuteChanged();
            }
        }

        /// <summary>Resource key of the caption of the "extension could not be opened" dialog.</summary>
        internal const string ExtensionOpenFailedCaptionKey = "messageBoxExtensionOpenFailedCaption";

        /// <summary>Resource key of the message of that dialog; its <c>{0}</c> is the extension's name.</summary>
        internal const string ExtensionOpenFailedMessageKey = "messageBoxExtensionOpenFailedMessage";

        /// <summary>
        /// Tells the user that an application extension could not be opened. The details are in the log.
        /// </summary>
        /// <param name="extensionName">The extension's display name.</param>
        private void ShowExtensionError(string extensionName)
        {
            const string fallbackMessage = "The extension '{0}' could not be opened. Details are in the application log.";

            var template = ResourceLoader.GetResource(ExtensionOpenFailedMessageKey);
            Dialogs.Add(new CustomDialogViewModel
            {
                Caption = ResourceLoader.GetResource(ExtensionOpenFailedCaptionKey),
                Message = string.Format(
                    CultureInfo.CurrentCulture,
                    string.IsNullOrWhiteSpace(template) ? fallbackMessage : template,
                    extensionName),
                OnOk = sender => sender.Close()
            });
        }

        /// <summary>
        /// Simple data object representing one application-level extension in the Extensions menu.
        /// </summary>
        public sealed class ExtensionMenuItem
        {
            /// <summary>Gets the display name shown in the menu.</summary>
            public string Header { get; }
            /// <summary>Gets the command that opens the extension view.</summary>
            public ICommand Command { get; }

            internal ExtensionMenuItem(string header, ICommand command)
            {
                Header = header;
                Command = command;
            }
        }

        #endregion Application-level UI extensions

        #region Localization
        [ExportViewModel("Culture")]
        private protected CultureInfo culture;

        /// <summary>
        /// Records the language the application now works with and lets extensions know if it differs from before
        /// (<c>IAppContext.CultureChanged</c>). Runs on the thread that changes the language, normally the UI thread.
        /// Before start-up has built the extension host there is nobody to tell, so the language is just stored.
        /// </summary>
        /// <param name="value">The culture that matches the language read from the settings.</param>
        private void ApplyCulture(CultureInfo value)
        {
            culture = value;
            extensionHost?.NotifyCultureMayHaveChanged();
        }

        private protected ResourceLoader resLoader;

        /// <summary>
        /// Expose translated strings from ResourceLoader
        /// </summary>
        public string LocalizationResourceSet
        {
            get; set;
        }

        #endregion Localization

        #region Local Commands

        /// <summary>
        /// 
        /// </summary>
        public IAsyncRelayCommand GetAddEditCommand => new AsyncRelayCommand(OnNewGetAddEditCommand);
        private Task OnNewGetAddEditCommand()
        {
            switch (selectedSetupViewModel)
            {
                case CommonTaskViewModel _:
                    OnNewNewCreateReportTaskCommand();
                    break;
                case GenericChipTaskViewModel _:
                    OnNewCreateGenericChipTaskCommand();
                    break;
                case MifareClassicSetupViewModel _:
                    OnNewCreateClassicTaskCommand();
                    break;
                case MifareDesfireSetupViewModel _:
                    OnNewCreateDesfireTaskCommand();
                    break;
                case MifareUltralightSetupViewModel _:
                    OnNewCreateUltralightTaskCommand();
                    break;
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Opens the last-used project file from settings when no explicit path is provided.
        /// </summary>
        /// <returns>A task that completes once the project load finishes.</returns>
        private async Task OpenLastProjectFile()
        {
            await OpenLastProjectFile(string.Empty);
        }

        /// <summary>
        /// Opens the specified project file, persisting it as the last-used path when provided.
        /// </summary>
        /// <param name="projectFileToUse">Optional project path to load instead of the persisted value.</param>
        /// <returns>A task that completes once the project load finishes.</returns>
        private async Task OpenLastProjectFile(string projectFileToUse)
        {
            using (var settings = settingsReaderWriterFactory())
            {
                var projectPath = string.IsNullOrEmpty(projectFileToUse)
                    ? settings.DefaultSpecification.LastUsedProjectPath
                    : projectFileToUse;

                ApplyCulture((settings.DefaultSpecification.DefaultLanguage == "german") ? new CultureInfo("de-DE") : new CultureInfo("en-US"));

                var result = await extensionHost.Project.LoadAsync(projectPath);
                if (!result.Success)
                {
                    LogProjectOpenFailure(projectPath, result.Error);
                    if (Dialogs.Any())
                    {
                        Dialogs.RemoveAt(0);
                    }
                    return;
                }

                if (!string.IsNullOrEmpty(projectFileToUse))
                {
                    settings.DefaultSpecification.LastUsedProjectPath = result.CanonicalPath;
                    await settings.SaveSettings();
                }

                if (Dialogs.Any())
                {
                    Dialogs.RemoveAt(0);
                }
            }
        }
        #endregion

        #region Menu Commands

        /// <summary>
        /// Here we perform all tasks on cards with a periodic check for new cards to work with.
        /// Added to Timer.Tick Event @ "triggerReadChip.Tick += UpdateChip;"
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void UpdateChip(object sender, EventArgs args)
        {
            if (_autoMode == null)
            {
                return;
            }

            try
            {
                await _autoMode.TickAsync();
            }
            catch (Exception e)
            {
                // TickAsync lets nothing escape; this is for an async void that must never end the application.
                Log.ForContext<MainWindowViewModel>().Error(e, "Failed to update chip.");
            }
        }

        /// <summary>
        /// Looks at the reader once for automatic mode, with the reader to itself, and says what the look meant. The
        /// picture the extensions see is stored from the same read, as the Read button does, so they learn of a card
        /// that comes or goes while automatic mode is on.
        /// </summary>
        /// <returns>
        /// What the read meant; <c>null</c> when somebody else holds the reader (an extension's scan or a read of the
        /// Read button), in which case this tick is skipped and the next one tries again.
        /// </returns>
        private async Task<AutoModePoll?> PollReaderForAutoModeAsync()
        {
            Mouse.OverrideCursor = Cursors.AppStarting;

            try
            {
                // The singleton, as every caller takes it: disposing it between reads is how the session is released.
                using (var device = ReaderDevice.Instance)
                {
                    if (device == null)
                    {
                        return new AutoModePoll(ReaderScanState.NoReader, null);
                    }

                    if (!_readerGate.TryEnter())
                    {
                        return null;
                    }

                    ReaderSnapshot snapshot;
                    var generation = extensionHost?.ReaderGeneration ?? 0;
                    var changed = false;
                    try
                    {
                        IsReaderBusy = true;

                        // Emptied first, as the Read button does: a provider that finds the same high-frequency card again
                        // returns without touching the chip, so the low-frequency and LEGIC children of the read before
                        // would stay on it, and the picture would show a tag that was taken away.
                        device.GenericChip = new GenericChipModel();
                        var result = await device.ReadChipPublic();

                        // The one place that says what a read means (a PC/SC reader without a card answers with an error).
                        snapshot = extensionHost != null
                            ? extensionHost.InterpretRead(device, result) ?? ReaderSnapshotBuilder.ReadFailed(CurrentReader)
                            : ReaderSnapshotBuilder.FromRead(CurrentReader, true, device.IsPcscReader, device.IsConnected, result, device.GenericChip);

                        // Stored while the reader is still held, so that pictures are stored in the order of the reads.
                        changed = extensionHost?.StoreRead(snapshot, generation) ?? false;
                    }
                    catch (Exception e)
                    {
                        Log.ForContext<MainWindowViewModel>().Error(e, "Failed to update chip.");
                        snapshot = ReaderSnapshotBuilder.ReadFailed(CurrentReader);
                    }
                    finally
                    {
                        IsReaderBusy = false;
                        _readerGate.Exit();
                    }

                    // After the reader is given back, so that a handler may scan again.
                    extensionHost?.AnnounceReaderChange(changed, generation);

                    return new AutoModePoll(snapshot.State, snapshot.Credentials.FirstOrDefault()?.Uid);
                }
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        /// <summary>
        /// Processes a card that automatic mode found newly presented: the same run as "Run all tasks once", with the
        /// card put in the tree by the run's own read.
        /// </summary>
        /// <param name="keepApplicationOpen">Whether a program task <c>%EXIT</c> ends the run instead of closing the application.</param>
        /// <param name="isStillWanted">
        /// Whether automatic mode, in the session that asked for this run, is still on. The run may wait for a reader scan
        /// of an extension before it is admitted, and a stop meanwhile must not be followed by a run.
        /// </param>
        /// <returns>What came of it; not admitted when something else held the execution slot or the request was withdrawn.</returns>
        private async Task<AutoModeRunReport> RunCardForAutoModeAsync(bool keepApplicationOpen, Func<bool> isStillWanted, AutoModeApproval approval = null)
        {
            foreach (var item in treeViewParentNodes)
            {
                item.IsExpanded = false;
            }

            return await RunAllOnceCoreAsync(keepApplicationOpen, isStillWanted, approval);
        }

        /// <summary>
        /// What to do if timer has ended without success i.e. ErrorLevel != ElatecError.NoError ?
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TaskTimeout(object sender, EventArgs e)
        {
            taskExecutionService?.HandleTaskTimeout();
        }

        /// <summary>
        /// "Remove all listed Chips from listing" was called
        /// </summary>
        public ICommand RemoveChipsFromTreeCommand => new RelayCommand(OnNewRemoveChipsFromTreeCommand);
        private void OnNewRemoveChipsFromTreeCommand()
        {
            TreeViewParentNodes.Clear();
        }

        /// <summary>
        /// Show Detailed Version Info
        /// </summary>
        public IAsyncRelayCommand ShowChangeLogCommand => new AsyncRelayCommand(OnNewShowChangeLogCommand);
        private async Task OnNewShowChangeLogCommand()
        {
            await AskForUpdateNow(true);
        }

        /// <summary>
        /// Show Detailed Version Info
        /// </summary>
        public ICommand NewAboutDialogCommand => new RelayCommand(OnNewNewAboutDialogCommand);
        private void OnNewNewAboutDialogCommand()
        {
            Dialogs.Add(new AboutViewModel()
            {
                Caption = ResourceLoader.GetResource("windowCaptionAboutRFiDGear"),
                AboutText = string.Format("RFiDGear {0}.{1}.{2} {3}\n\n", Version.Major, Version.Minor, Version.Build, Constants.TITLE_SUFFIX)
                + ResourceLoader.GetResource("textBoxTextAboutRFiDGear"),

                OnOk = (sender) =>
                {
                    sender.Close();
                },

                OnCloseRequest = (sender) =>
                {
                    sender.Close();
                    mw.Activate();
                }
            });
        }

        /// <summary>
        /// Create a new "Common" Task of Type "Report Creator"
        /// </summary>
        public IAsyncRelayCommand CreateGenericChipTaskCommand => new AsyncRelayCommand(OnNewCreateGenericChipTaskCommand);
        private Task OnNewCreateGenericChipTaskCommand()
        {

            var timerState = triggerReadChip.IsEnabled;
            triggerReadChip.IsEnabled = false;
            Mouse.OverrideCursor = Cursors.AppStarting;

            try
            {
                using (var device = ReaderDevice.Instance)
                {
                    // only call dialog if device is ready
                    if (device != null)
                    {
                        Dialogs.Add(taskDialogFactory.CreateGenericChipTaskDialog(SelectedSetupViewModel, ChipTasks, dialogs));
                    }

                    else
                    {
                        OnNewNoReaderFoundDialog();
                    }
                }
            }
            catch (Exception e)
            {
                Log.ForContext<MainWindowViewModel>().Error(e, "Failed to update chip.");

                dialogs.Clear();

            }

            Mouse.OverrideCursor = null;

            triggerReadChip.IsEnabled = timerState;

            OnPropertyChanged(nameof(ChipTasks));

            return Task.CompletedTask;
        }

        /// <summary>
        /// Create a new "Common" Task of Type "Report Creator"
        /// </summary>
        public IAsyncRelayCommand CreateGenericTaskCommand => new AsyncRelayCommand(OnNewNewCreateReportTaskCommand);
        private Task OnNewNewCreateReportTaskCommand()
        {


            var timerState = triggerReadChip.IsEnabled;

            triggerReadChip.IsEnabled = false;

            Mouse.OverrideCursor = Cursors.AppStarting;

            try
            {
                dialogs.Add(new CommonTaskViewModel(SelectedSetupViewModel, ChipTasks.TaskCollection, dialogs)
                {
                    Caption = ResourceLoader.GetResource("windowCaptionAddEditGenericTask"),

                    OnOk = (sender) =>
                    {

                        if (sender.SelectedTaskType == TaskType_CommonTask.CreateReport ||
                            sender.SelectedTaskType == TaskType_CommonTask.CheckLogicCondition ||
                            sender.SelectedTaskType == TaskType_CommonTask.ExecuteProgram)
                        {
                            // The list changes only while no run, load or other edit is in progress. Otherwise the user
                            // is told, and the dialog stays open so the confirmation can be repeated.
                            var listChanged = taskListEditGate.TryEdit(() =>
                            {
                                if (!TryValidateTaskIndices(sender.CurrentTaskIndex, sender.SelectedExecuteConditionTaskIndex, sender.SelectedExecuteConditionErrorLevel))
                                {
                                    return false;
                                }

                                if (ChipTasks.TaskCollection.OfType<CommonTaskViewModel>().Where(x => (x as CommonTaskViewModel).SelectedTaskIndexAsInt == sender.SelectedTaskIndexAsInt).Any())
                                {
                                    ChipTasks.TaskCollection.RemoveAt(ChipTasks.TaskCollection.IndexOf(SelectedSetupViewModel));
                                }

                                ChipTasks.TaskCollection.Add(sender);

                                ChipTasks.TaskCollection = new ObservableCollection<object>(ChipTasks.TaskCollection.OrderBy(x => (x as IGenericTask).SelectedTaskIndexAsInt));

                                OnPropertyChanged(nameof(ChipTasks));
                                return true;
                            });

                            if (!listChanged)
                            {
                                return;
                            }
                        }
                        sender.Close();

                        mw?.Activate();
                    },

                    OnCancel = (sender) =>
                    {
                        sender.Close();

                        mw.Activate();
                    },

                    OnAuth = (sender) =>
                    {
                    },

                    OnCloseRequest = (sender) =>
                    {
                        sender.Close();

                        mw.Activate();
                    }
                });
            }
            catch (Exception e)
            {
                Log.ForContext<MainWindowViewModel>().Error(e, "Failed to update chip.");

                dialogs.Clear();


            }

            Mouse.OverrideCursor = null;

            triggerReadChip.IsEnabled = timerState;

            OnPropertyChanged(nameof(ChipTasks));

            return Task.CompletedTask;
        }

        /// <summary>
        /// Validates task indices and shows a dialog when the input is invalid.
        /// </summary>
        /// <param name="taskIndex">The task index assigned to the current task.</param>
        /// <param name="executeConditionTaskIndex">The task index referenced by the execute condition.</param>
        /// <param name="executeConditionErrorLevel">The execute condition error level.</param>
        /// <returns><see langword="true"/> when validation succeeds; otherwise <see langword="false"/>.</returns>
        private bool TryValidateTaskIndices(string taskIndex, string executeConditionTaskIndex, ERROR executeConditionErrorLevel)
        {
            if (!TaskIndexValidation.TryValidateTaskIndex(taskIndex, ChipTasks?.TaskCollection, SelectedSetupViewModel, out var errorMessage) ||
                !TaskIndexValidation.TryValidateExecuteConditionIndex(executeConditionTaskIndex, executeConditionErrorLevel, ChipTasks?.TaskCollection, out errorMessage))
            {
                dialogs.Add(new CustomDialogViewModel
                {
                    Caption = ResourceLoader.GetResource("messageBoxDefaultCaption"),
                    Message = errorMessage,
                    OnOk = sender => sender.Close()
                });
                return false;
            }

            return true;
        }

        /// <summary>
        /// Creates a new Task of Type Mifare Classic Card
        /// </summary>
        public IAsyncRelayCommand CreateClassicTaskCommand => new AsyncRelayCommand(OnNewCreateClassicTaskCommand);
        private Task OnNewCreateClassicTaskCommand()
        {


            var timerState = triggerReadChip.IsEnabled;

            triggerReadChip.IsEnabled = false;

            Mouse.OverrideCursor = Cursors.AppStarting;

            try
            {
                using (var device = ReaderDevice.Instance)
                {
                    // only call dialog if device is ready
                    if (device != null)
                    {
                        Dialogs.Add(taskDialogFactory.CreateClassicTaskDialog(SelectedSetupViewModel, ChipTasks, dialogs));
                    }

                    else
                    {
                        OnNewNoReaderFoundDialog();
                    }
                }
            }
            catch (Exception e)
            {
                Log.ForContext<MainWindowViewModel>().Error(e, "Failed to update chip.");

                dialogs.Clear();


            }

            Mouse.OverrideCursor = null;

            triggerReadChip.IsEnabled = timerState;

            return Task.CompletedTask;
        }

        /// <summary>
        /// 
        /// </summary>
        public IAsyncRelayCommand CreateDesfireTaskCommand => new AsyncRelayCommand(OnNewCreateDesfireTaskCommand);
        private Task OnNewCreateDesfireTaskCommand()
        {
            var timerState = triggerReadChip.IsEnabled;

            triggerReadChip.IsEnabled = false;

            Mouse.OverrideCursor = Cursors.AppStarting;

            using (var device = ReaderDevice.Instance)
            {
                if (device != null)
                {
                    Dialogs.Add(taskDialogFactory.CreateDesfireTaskDialog(SelectedSetupViewModel, ChipTasks, dialogs));
                }

                else
                {
                    OnNewNoReaderFoundDialog();
                }
            }

            Mouse.OverrideCursor = null;

            triggerReadChip.IsEnabled = timerState;

            return Task.CompletedTask;
        }

        /// <summary>
        /// 
        /// </summary>
        public ICommand CreateUltralightTaskCommand => new AsyncRelayCommand(OnNewCreateUltralightTaskCommand);
        private Task OnNewCreateUltralightTaskCommand()
        {
            var timerState = triggerReadChip.IsEnabled;

            triggerReadChip.IsEnabled = false;

            Mouse.OverrideCursor = Cursors.AppStarting;

            try
            {
                using (var device = ReaderDevice.Instance)
                {
                    // only call dialog if device is ready
                    if (device != null)
                    {
                        Dialogs.Add(taskDialogFactory.CreateUltralightTaskDialog(SelectedSetupViewModel, ChipTasks, dialogs));
                    }
                    else
                    {
                        OnNewNoReaderFoundDialog();
                    }
                }
            }
            catch (Exception e)
            {
                Log.ForContext<MainWindowViewModel>().Error(e, "Failed to open the Ultralight task dialog.");
            }
            finally
            {
                Mouse.OverrideCursor = null;

                triggerReadChip.IsEnabled = timerState;
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// 
        /// </summary>
        public IAsyncRelayCommand ExecuteQuickCheckCommand => new AsyncRelayCommand(OnNewExecuteQuickCheckCommand);

        /// <summary>
        /// Tells whether the quick check of the main window is the Classic quick check for a chip of the type: a MIFARE Classic chip (the
        /// SmartMX Classic chips included), and a MIFARE Plus in security level 1, which answers like one and has the sectors of its
        /// size (a TWN4 reported it as a Classic chip until Elatec.NET 0.7.0, and the quick check was for it). A Plus in another level has
        /// no Classic commands.
        /// </summary>
        /// <param name="type">The type of the selected chip.</param>
        /// <returns><c>true</c> when the Classic quick check is the one to run.</returns>
        internal static bool IsClassicQuickCheckable(CARD_TYPE type) =>
            ((int)type & 0xF000) == (int)CARD_TYPE.MifareClassic
            || type == CARD_TYPE.MifarePlus_SL1_1K
            || type == CARD_TYPE.MifarePlus_SL1_2K
            || type == CARD_TYPE.MifarePlus_SL1_4K;

        private async Task OnNewExecuteQuickCheckCommand()
        {
            try
            {
                Mouse.OverrideCursor = Cursors.AppStarting;

                await ReadChipCommand.ExecuteAsync(null);

                IsReaderBusy = true;
                Mouse.OverrideCursor = Cursors.AppStarting;

                if (treeViewParentNodes != null && treeViewParentNodes.Any(x => x.IsSelected))
                {
                    treeViewParentNodes.FirstOrDefault(x => x.IsSelected).IsExpanded = true;

                    if (IsClassicQuickCheckable(treeViewParentNodes.Single(x => x.IsSelected == true).CardType))
                    {
                        await treeViewParentNodes.Single(x => x.IsSelected == true).ExecuteClassicQuickCheckCommand.ExecuteAsync(null);
                    } // Mifare Classic

                    else if (Enum.GetName(typeof(CARD_TYPE), treeViewParentNodes.Single(x => x.IsSelected == true).CardType).ToLower(CultureInfo.CurrentCulture).Contains("desfire"))
                    {
                        await treeViewParentNodes.Single(x => x.IsSelected == true).ExecuteDesfireQuickCheckCommand.ExecuteAsync(null);
                    } // Mifare Desfire
                }

                IsReaderBusy = false;
                Mouse.OverrideCursor = null;
            }
            catch
            {
                Mouse.OverrideCursor = null;
            }


        }

        /// <summary>
        /// 
        /// </summary>
        public IAsyncRelayCommand ReadChipCommand => new AsyncRelayCommand(OnNewReadChipCommand);
        private async Task OnNewReadChipCommand()
        {
            // The tick of automatic mode holds back for as long as this read lasts. A scope, not a save and restore of
            // the timer: this method awaits, so a start or stop of automatic mode can come in meanwhile, and a restore
            // would undo it. (Before, it also left the timer off when there was no reader.)
            using var pause = _autoMode?.PausePolling();

            Mouse.OverrideCursor = Cursors.AppStarting;

            if (ReaderDevice.Instance == null)
            {
                Mouse.OverrideCursor = null;

                return;
            }

            IsReaderBusy = true;
            var pictureChanged = false;
            var pictureGeneration = 0;

            using (var device = ReaderDevice.Instance)
            {
                if(device != null)
                {
                    foreach (var item in treeViewParentNodes)
                    {
                        item.IsExpanded = false;
                        item.IsSelected = false;
                    }

                    // The chip is taken while the reader is still ours: a scan that follows empties the shared one.
                    var (result, chip, changed, generation) = await ReadChipExclusivelyAsync(device);
                    pictureChanged = changed;
                    pictureGeneration = generation;
                    var wellKnownTvNodes = from nodes in treeViewParentNodes where nodes.UID == chip.UID select nodes;

                    if (result == ERROR.NoError && !wellKnownTvNodes.Any())
                    {
                        var node = CreateParentNode(chip, Dialogs);

                        if (node != null)
                        {
                            treeViewParentNodes.Add(node);
                        }
                    }
                    else if (wellKnownTvNodes.Any())
                    {
                        wellKnownTvNodes.FirstOrDefault().IsSelected = true;
                    }
                }

            }

            Mouse.OverrideCursor = null;
            IsReaderBusy = false;

            // Last, and after everything that must be restored: the subscribers of the event run now, on this thread.
            extensionHost?.AnnounceReaderChange(pictureChanged, pictureGeneration);
        }

        /// <summary>
        /// Makes the node of the chip tree for a chip that was read. The kind of node follows the type of the chip; a chip of the
        /// Ultralight family (the Ultralight, the Ultralight C and EV1, the NTAG21x) gets the node with the pages of its layout, and
        /// a chip that has no node (no tag) gets none.
        /// </summary>
        /// <param name="chip">The chip that was read.</param>
        /// <param name="dialogs">The dialogs of the window.</param>
        /// <returns>The node, or <see langword="null"/> when the chip has none.</returns>
        internal static RFiDChipParentLayerViewModel CreateParentNode(GenericChipModel chip, ObservableCollection<IDialogViewModel> dialogs)
        {
            // The chips of the Ultralight family first: an NTAG21x has a type of 0xA5.. and no case below, and has pages like the others.
            if (UltralightChipTypes.IsOfTheFamily(chip.CardType))
            {
                return new RFiDChipParentLayerViewModel(new MifareUltralightChipModel(chip), dialogs, false);
            }

            // The family is the high nibble of the type. It is taken from the int: the application is built with overflow checks, and
            // a type from 0x8000 up (an Ultralight is 0x9000, a chip of the NTAG family 0xA000 and up) does not fit a short.
            switch ((CARD_TYPE)((int)chip.CardType & 0xF000))
            {
                case CARD_TYPE.MifareClassic:
                case CARD_TYPE.MifarePlus:
                    // From the chip itself: a Plus that a reader reported as a plain GenericChipModel was null here (an `as` cast), and the copy threw.
                    return new RFiDChipParentLayerViewModel(new MifareClassicChipModel(chip), dialogs, false);

                case CARD_TYPE.DESFireEV0:
                case CARD_TYPE.DESFireEV1:
                case CARD_TYPE.DESFireEV2:
                case CARD_TYPE.DESFireEV3:
                    return new RFiDChipParentLayerViewModel(new MifareDesfireChipModel(chip as MifareDesfireChipModel), dialogs, false);

                case CARD_TYPE.MifareUltralight:
                    return new RFiDChipParentLayerViewModel(new MifareUltralightChipModel(chip), dialogs, false);

                default:
                    return chip?.CardType != CARD_TYPE.NOTAG ? new RFiDChipParentLayerViewModel(new GenericChipModel(chip), dialogs, false) : null;
            }
        }

        /// <summary>
        /// Reads the chip with the reader to itself: while this runs no reader scan of an extension reads, and the timer
        /// tick skips its read (the reads of tasks, quick checks and setup dialogs are not covered). The shared chip is
        /// emptied first, as this command always did. The picture the extensions see is made and stored right after the
        /// read, while the reader is still held, from the chip as this read left it, so pictures are stored in the order
        /// of the reads; the caller raises the event later.
        /// </summary>
        /// <param name="device">The reader device.</param>
        /// <returns>
        /// What the read returned, the chip it left, whether its picture is a change to announce, and the number of reader
        /// changes from before the read (the announcement is dropped if the reader changed since).
        /// </returns>
        private async Task<(ERROR Result, GenericChipModel Chip, bool Changed, int Generation)> ReadChipExclusivelyAsync(ReaderDevice device)
        {
            var generation = extensionHost?.ReaderGeneration ?? 0;
            await _readerGate.EnterAsync();
            try
            {
                device.GenericChip = new GenericChipModel();
                var result = await device.ReadChipPublic();
                var observed = extensionHost?.InterpretRead(device, result);

                return (result, device.GenericChip, observed != null && extensionHost.StoreRead(observed, generation), generation);
            }
            finally
            {
                _readerGate.Exit();
            }
        }

        /// <summary>
        /// Removes the selected task from the task list, if no run, load or other edit is in progress. Otherwise the
        /// user is told and nothing is removed.
        /// </summary>
        /// <returns><c>true</c> when the task was removed; <c>false</c> when the edit was refused or the selection is not in the list.</returns>
        internal bool TryDeleteSelectedTask() =>
            taskListEditGate.TryEdit(() => taskHandler.TaskCollection.Remove(SelectedSetupViewModel));

        /// <summary>
        /// Runs a change of the task list, or of the state of its tasks, while the coordinator's project-edit slot
        /// is held. Clearing the state under a running task loop lets a finished task run a second time (it breaks
        /// the one-attempt rule), and changing the collection under it is worse, so these changes are admitted
        /// only when no run, load or other edit is in progress (a reader scan is waited for), as for an extension.
        /// </summary>
        /// <remarks>
        /// An edit that meets a reader scan of an extension waits for it, as a run does (see
        /// <see cref="TryBeginAfterAScanAsync"/>): a scan is short, and the reset that starts an automatic run must not
        /// be dropped because one was in progress, since the card is in the tree already and nothing would try it again.
        /// </remarks>
        /// <param name="edit">The change to make.</param>
        /// <returns><c>false</c> when the slot was not free and nothing was changed.</returns>
        private async Task<bool> TryRunProjectEditAsync(Func<Task> edit)
        {
            if (!await TryBeginAfterAScanAsync(_coordinator.TryBeginProjectOperation))
            {
                return false;
            }

            try
            {
                await edit();
                return true;
            }
            finally
            {
                _coordinator.EndProjectOperation();
            }
        }

        /// <summary>Tells the user that the task list cannot be changed right now.</summary>
        private void ShowProjectBusy()
        {
            Dialogs.Add(new CustomDialogViewModel
            {
                Caption = ResourceLoader.GetResource("messageBoxDefaultCaption"),
                Message = ResourceLoader.GetResource("messageBoxProjectBusyMessage"),
                OnOk = sender => sender.Close()
            });
        }

        /// <summary>
        /// Reset all Task status information. Refused, with a message, while a run, a load or another project
        /// edit is in progress.
        /// </summary>
        public IAsyncRelayCommand ResetTaskStatusCommand => new AsyncRelayCommand(OnNewResetTaskStatusCommand);
        private async Task OnNewResetTaskStatusCommand()
        {
            if (!await TryResetTaskStatusAsync())
            {
                ShowProjectBusy();
            }
        }

        /// <summary>
        /// Resets the state of every task if no run, load or other edit is in progress. Quiet when it is refused, for
        /// callers that are not a click (the reader timer, start-up).
        /// </summary>
        /// <returns><c>false</c> when the reset was refused.</returns>
        private Task<bool> TryResetTaskStatusAsync() => TryRunProjectEditAsync(ResetTaskStatusCoreAsync);

        /// <summary>
        /// The reset itself. Callers must hold the coordinator's slot: the main window's own run does, as the
        /// execution slot, and every other caller goes through <see cref="TryResetTaskStatusAsync"/>.
        /// </summary>
        private Task ResetTaskStatusCoreAsync() =>
            Task.Run(() =>
            {
                foreach (IGenericTask chipTask in taskHandler.TaskCollection)
                {
                    chipTask.IsTaskCompletedSuccessfully = null;
                    chipTask.CurrentTaskErrorLevel = ERROR.Empty;
                }
            });

        /// <summary>
        /// Reset all Reporttasks directory information. Refused, with a message, while a run, a load or another
        /// project edit is in progress.
        /// </summary>
        public IAsyncRelayCommand ResetReportTaskDirectoryCommand => new AsyncRelayCommand(OnNewResetReportTaskDirectoryCommand);
        private async Task OnNewResetReportTaskDirectoryCommand()
        {
            var changed = await TryRunProjectEditAsync(() =>
            {
                ResetReportTaskDirectoryCore();
                return Task.CompletedTask;
            });

            if (!changed)
            {
                ShowProjectBusy();
            }
        }

        private void ResetReportTaskDirectoryCore()
        {
            // What the window remembers of the report target is forgotten whether or not the project has a task that makes a report.
            reportOutputPath = null;
            reportOutputPattern = null;

            try
            {
                foreach (var chipTask in taskHandler.TaskCollection)
                {
                    switch (chipTask)
                    {
                        case CommonTaskViewModel ssVM:
                            ssVM.IsTaskCompletedSuccessfully = null;
                            reportReaderWriter.ReportOutputPath = null;
                            reportReaderWriter.ReportTemplateFile = null;
                            ssVM.CurrentTaskErrorLevel = ERROR.Empty;
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                Log.ForContext<MainWindowViewModel>().Error(e, "Failed to update chip.");
            }
        }

        /// <summary>
        /// Reset selected Task status information. Refused, with a message, while a run, a load or another project
        /// edit is in progress.
        /// </summary>
        public IAsyncRelayCommand ResetSelectedTaskStatusCommand => new AsyncRelayCommand(OnNewResetSelectedTaskStatusCommand);
        private async Task OnNewResetSelectedTaskStatusCommand()
        {
            var changed = await TryRunProjectEditAsync(() =>
                Task.Run(() =>
                {
                    if (SelectedSetupViewModel is IGenericTask selectedTask)
                    {
                        selectedTask.IsTaskCompletedSuccessfully = null;
                        selectedTask.CurrentTaskErrorLevel = ERROR.Empty;
                    }
                }));

            if (!changed)
            {
                ShowProjectBusy();
            }
        }

        /// <summary>
        /// Remove all Tasks from DataGrid. Refused, with a message, while a run, a load or another project edit
        /// is in progress.
        /// </summary>
        public IAsyncRelayCommand RemoveAllTasksCommand => new AsyncRelayCommand(OnNewRemoveAllTasksCommand);
        private async Task OnNewRemoveAllTasksCommand()
        {
            var changed = await TryRunProjectEditAsync(async () =>
            {
                // The collection is bound to the grid and may only change on the UI thread.
                if (uiDispatcher.CheckAccess())
                {
                    taskHandler.TaskCollection.Clear();
                }
                else
                {
                    await uiDispatcher.InvokeAsync(() => taskHandler.TaskCollection.Clear());
                }
            });

            if (!changed)
            {
                ShowProjectBusy();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public ICommand WriteSelectedTaskToChipAutoCommand => new RelayCommand(OnNewWriteSelectedTaskToChipAutoCommand);
        private void OnNewWriteSelectedTaskToChipAutoCommand()
        {
            if (!isWriteSelectedToChipAutoCheckedTemp)
            {
                isWriteSelectedToChipAutoCheckedTemp = true;
            }
            else
            {
                isWriteSelectedToChipAutoCheckedTemp = false;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public ICommand WriteToAllChipAutoCommand => new RelayCommand(OnNewWriteToAllChipAutoCommand);
        private void OnNewWriteToAllChipAutoCommand()
        {
            if (_autoMode == null)
            {
                return;
            }

            if (_autoMode.IsEnabled)
            {
                _autoMode.Stop();

                return;
            }

            // The menu does not insist on a reader or a task (the reader may be plugged in later); it only refuses to
            // start in the middle of something that would be disturbed by it.
            if (_autoMode.TryStart(requireReady: false, keepApplicationOpen: false) == AutoModeStartResult.Busy)
            {
                ShowProjectBusy();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public IAsyncRelayCommand WriteSelectedTaskToChipOnceCommand => new AsyncRelayCommand(OnNewWriteSelectedTaskToChipOnceCommand);
        private async Task OnNewWriteSelectedTaskToChipOnceCommand()
        {
            _runSelectedOnly = true;
            await WriteToChipOnceCommand.ExecuteAsync(null);
        }

        /// <summary>
        /// 
        /// </summary>
        public IAsyncRelayCommand WriteToChipOnceCommand => new AsyncRelayCommand(OnNewWriteToChipOnceCommand);
        private async Task OnNewWriteToChipOnceCommand()
        {
            var report = await RunAllOnceCoreAsync(keepApplicationOpen: false);

            // Whoever started a run, one that ended in a way that leaves the card in an unknown state also stops a mode
            // that is on; otherwise the next card would be run on the strength of it.
            _autoMode?.OnOtherRunEnded(report);
        }

        /// <summary>
        /// Runs all tasks once on the card that is on the reader: the run behind "Run all tasks once", behind
        /// <c>AUTORUN=1</c> and behind every card of automatic mode. A run that is not admitted (something else holds
        /// the execution slot) has touched nothing.
        /// </summary>
        /// <remarks>
        /// The run gets a <c>RunId</c> and records its observations, so the result can be judged by the same rules as
        /// the result of a run an extension started. The card the run read is remembered by automatic mode before the
        /// first task starts, so that the machine does not run it a second time, however the run ends.
        /// </remarks>
        /// <param name="keepApplicationOpen">Whether a program task <c>%EXIT</c> ends the run instead of closing the application.</param>
        /// <param name="isStillWanted">
        /// Asked while the run waits for a reader scan of an extension to end: when it returns <c>false</c> the run is
        /// given up (not admitted). <c>null</c> for a run by hand, which is always wanted.
        /// </param>
        /// <param name="approval">
        /// What the program that decides about the cards of automatic mode approved for this card, or <c>null</c>: the variables of this one
        /// run (laid over those of the start-up), and the revision of the project that it expects, which is checked here, with the
        /// execution slot held, before anything is started.
        /// </param>
        /// <returns>What came of it; <see cref="AutoModeRunReport.NotAdmitted"/> when nothing ran.</returns>
        private async Task<AutoModeRunReport> RunAllOnceCoreAsync(bool keepApplicationOpen, Func<bool> isStillWanted = null, AutoModeApproval approval = null)
        {
            // The report variables of an approval (REPORTTARGETPATH, REPORTTEMPLATEFILE) are read as for any run of the host, before
            // anything is admitted (the program was told when it approved if they are not usable), and they are the report of this one card:
            // they do not become the report of the next one.
            string approvedReportPath = null;
            string approvedReportTemplate = null;

            if (approval != null)
            {
                try
                {
                    ReportVariables.Resolve(approval.Variables, out approvedReportPath, out approvedReportTemplate);
                }
                catch (ArgumentException)
                {
                    Log.ForContext<MainWindowViewModel>().Warning("The report variables of an approval cannot be used any more; the card is not run");

                    return AutoModeRunReport.NotAdmitted;
                }
            }

            var reportIsOfThisRun = approvedReportPath != null || approvedReportTemplate != null;

            if (!await TryBeginExecutionAfterAScanAsync(isStillWanted))
            {
                Mouse.OverrideCursor = null;
                return AutoModeRunReport.NotAdmitted;
            }

            // A card that was approved is the card that was shown, and no other: the reader is looked at once more now that the window
            // holds the execution slot, before anything is admitted. A card that cannot be told is not the one that was approved.
            if (approval != null)
            {
                AutoModeRefusal cardRefusal;
                bool stillWanted;

                try
                {
                    cardRefusal = await VerifyTheApprovedCardAsync(approval);

                    // The look took time: a mode that was stopped meanwhile (or whose client left), or a card that another run processed,
                    // ends the request here, before anything is admitted.
                    stillWanted = isStillWanted == null || isStillWanted();
                }
                catch (Exception)
                {
                    // The execution slot is held and the run has not started: whatever went wrong, the slot is let go of, or nothing
                    // could run again until the application is restarted. The machine stops on the exception.
                    _coordinator.EndExecution();

                    throw;
                }

                if (cardRefusal != AutoModeRefusal.None)
                {
                    _coordinator.EndExecution();

                    return cardRefusal == AutoModeRefusal.CardChanged ? AutoModeRunReport.CardChanged : AutoModeRunReport.NotAdmitted;
                }

                if (!stillWanted)
                {
                    _coordinator.EndExecution();

                    return AutoModeRunReport.NotAdmitted;
                }
            }

            // The window holds the execution slot, so nothing can change the project now. An approval that says which state of the
            // project it was given for is held to it, and a state that cannot be told is not the one that was approved.
            var revision = ReadTheProjectRevision(approval);

            if (approval?.ExpectProjectRevision != null && !string.Equals(approval.ExpectProjectRevision, revision, StringComparison.Ordinal))
            {
                _coordinator.EndExecution();

                return AutoModeRunReport.ProjectChanged;
            }

            var runId = Guid.NewGuid().ToString("N");

            // A run by hand has no question to ask the mode ("isStillWanted" is for the requests of automatic mode only).
            _windowRuns.Begin(runId, isStillWanted == null ? RunSources.Gui : RunSources.AutoMode);
            NoteTheProjectOfTheRun(runId, revision, approval?.PresenceId);
            approval?.NotifyRunAdmitted(runId);
            var startedAt = DateTimeOffset.UtcNow;
            var observations = new List<TaskExecutionObservation>();
            IReadOnlyList<object> snapshot = Array.Empty<object>();
            ExecResult execResult;
            var abandoned = false;

            // The UID of the card the service's own read found; it stays null for a run that never gets that far.
            string? cardUid = null;

            // The report of an approval is built in a work folder of its own: the writer keeps the report it is building in a file there,
            // and the writer of the window (which lives from run to run) keeps its own in the folder of the application. Two writers in
            // one folder would hand the fields of one card to the report of the next.
            string reportWorkFolder = null;

            try
            {
                Mouse.OverrideCursor = Cursors.AppStarting;

                if (!_runSelectedOnly)
                {
                    // This run holds the execution slot, so it resets without asking for the project-edit slot.
                    await ResetTaskStatusCoreAsync();
                }

                await OnNewReadChipCommand();
                OnPropertyChanged(nameof(TreeViewParentNodes));
                OnPropertyChanged(nameof(ChipTasks));

                // What the reader showed so far. The run reads the card once more before its first task (see ChipHydrated
                // below), and that read is the one that counts if the card was swapped in between.
                RememberCard(ReaderDevice.Instance?.GenericChip);

                // The positions of the observations index into the list as it is now; the live one may change meanwhile.
                snapshot = taskHandler?.TaskCollection?.ToList() ?? new List<object>();

                // The next free number of the pattern of the start-up arguments, for this run (null without a pattern, or when its folder is gone).
                // It is looked up only when nothing of higher priority names the report: the target of an approval, or the path that a person chose.
                var reportPathOfThePattern = approvedReportPath == null && reportOutputPath == null && reportOutputPattern != null
                    ? StartupArgumentProcessor.NormalizeReportTargetPath(reportOutputPattern)
                    : null;

                var request = new TaskExecutionRequest
                {
                    RunId = runId,
                    Observations = observations,
                    KeepApplicationOpen = keepApplicationOpen,
                    ChipHydrated = chip =>
                    {
                        cardUid = ReaderSnapshotBuilder.PrimaryUidOf(chip);
                        RememberCard(chip);
                    },

                    // The card the tasks work on is the one that was approved: the service looks at it right after its own read, before
                    // anything else is sent to the card, and ends the run before the first task when it is another one.
                    ExpectedCardUid = approval?.Uid,
                    TaskHandler = taskHandler,
                    TreeViewParentNodes = treeViewParentNodes,
                    VariablesFromArgs = VariablesOfTheRun(approval),
                    Dialogs = Dialogs,
                    ReportReaderWriter = reportIsOfThisRun ? MakeTheReportWriterOfThisRun(runId, out reportWorkFolder) : reportReaderWriter,
                    ReportOutputPath = approvedReportPath ?? reportOutputPath ?? reportPathOfThePattern,
                    ReportTemplateFile = approvedReportTemplate ?? reportTemplateFile,
                    SelectedSetupViewModel = SelectedSetupViewModel,
                    RunSelectedOnly = _runSelectedOnly,
                    UpdateSelectedSetupViewModel = vm => SelectedSetupViewModel = vm,
                    UpdateReaderBusy = status => IsReaderBusy = status,
                    NotifyTreeViewChanged = () => OnPropertyChanged(nameof(TreeViewParentNodes)),
                    NotifyTasksChanged = () => OnPropertyChanged(nameof(ChipTasks)),
                };

                var result = await taskExecutionService.ExecuteOnceAsync(request, CancellationToken.None);

                if (!reportIsOfThisRun)
                {
                    reportReaderWriter = result.ReportReaderWriter;

                    // The path that the pattern gave is of this run only. A path that came from elsewhere (a person chose it in the window
                    // when the run had none) is kept for the runs after it.
                    if (!string.Equals(result.ReportOutputPath, reportPathOfThePattern, StringComparison.Ordinal))
                    {
                        reportOutputPath = result.ReportOutputPath;
                    }

                    reportTemplateFile = result.ReportTemplateFile;
                }

                _runSelectedOnly = result.RunSelectedOnly;
                SelectedSetupViewModel = result.SelectedSetupViewModel;

                abandoned = result.Abandoned;
                execResult = ExecResultBuilder.FromCompletion(runId, startedAt, result.TerminalStatus, snapshot, observations, cardUid);
            }
            catch (Exception ex)
            {
                Log.ForContext<MainWindowViewModel>().Error(ex, "Task execution failed. Run {RunId}", runId);
                Dialogs.Add(new CustomDialogViewModel
                {
                    Caption = ResourceLoader.GetResource("messageBoxDefaultCaption"),
                    Message = ex.Message,
                    OnOk = sender => sender.Close()
                });

                execResult = ExecResultBuilder.FromFailure(
                    runId, startedAt, ExecResultBuilder.Classify(ex, CancellationToken.None), snapshot, observations, cardUid);
            }
            finally
            {
                _windowRuns.End(runId);
                _coordinator.EndExecution();
                Mouse.OverrideCursor = null;
                DeleteTheReportWorkFolder(reportWorkFolder);
            }

            return new AutoModeRunReport(admitted: true, execResult, abandoned);
        }

        // A writer for the report of one run, with a folder of its own for the file in which it builds the report.
        private static ReportReaderWriter MakeTheReportWriterOfThisRun(string runId, out string workFolder)
        {
            workFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RFiDGear", "report-work", runId);

            return new ReportReaderWriter(new ProjectManager(workFolder));
        }

        private static void DeleteTheReportWorkFolder(string folder)
        {
            if (folder == null)
            {
                return;
            }

            try
            {
                if (Directory.Exists(folder))
                {
                    Directory.Delete(folder, recursive: true);
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Log.ForContext<MainWindowViewModel>().Debug("The work folder of a report could not be removed ({ExceptionType})", e.GetType().FullName);
            }
        }

        // Looks at the reader once (the same look automatic mode takes) and says whether the card on it is the one an approval was given
        // for. Nothing is changed on the card; a reader that cannot say is a refusal to run, and the card is tried again.
        private async Task<AutoModeRefusal> VerifyTheApprovedCardAsync(AutoModeApproval approval)
        {
            var poll = await PollReaderForAutoModeAsync();

            if (!poll.HasValue || poll.Value.State != ReaderScanState.Ready)
            {
                return AutoModeRefusal.NotAdmitted;
            }

            return string.Equals(poll.Value.Uid, approval.Uid, StringComparison.Ordinal) ? AutoModeRefusal.None : AutoModeRefusal.CardChanged;
        }

        // The revision of the project, read only when somebody needs it: the bridge listens for the results of the runs of the mode, or
        // an approval says which state it was given for. It never affects the run (a state that cannot be told is null).
        private string ReadTheProjectRevision(AutoModeApproval approval)
        {
            if ((!_runFacts.IsWanted && approval?.ExpectProjectRevision == null) || extensionHost?.Project is not IProjectRevisionSource source)
            {
                return null;
            }

            try
            {
                return source.GetProjectRevision();
            }
            catch (Exception e)
            {
                Log.ForContext<MainWindowViewModel>().Debug("The revision of the project could not be read for a run ({ExceptionType})", e.GetType().FullName);

                return null;
            }
        }

        // What the window knows about the run when it takes it: the revision the run starts with, and the presence the run was
        // approved for. The result of the run (made later, by the bridge) says so. Only while the bridge listens for it.
        private void NoteTheProjectOfTheRun(string runId, string revision, string presenceId)
        {
            if (_runFacts.IsWanted)
            {
                _runFacts.Remember(runId, new RunFact(revision, presenceId));
            }
        }

        // The variables of a run: those of the start-up, and for a card that a program approved, the variables of that approval laid
        // over them (the approval wins), in a table of its own so that nothing of it stays for the next card. Names are compared
        // without regard to case, as the bridge does for the variables of startRun.
        private Dictionary<string, string> VariablesOfTheRun(AutoModeApproval approval)
        {
            if (approval == null || approval.Variables.Count == 0)
            {
                return variablesFromArgs;
            }

            var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var pair in variablesFromArgs)
            {
                merged[pair.Key] = pair.Value;
            }

            foreach (var pair in approval.Variables)
            {
                merged[pair.Key] = pair.Value;
            }

            return merged;
        }

        /// <summary>
        /// Tells automatic mode which card a run works on, as the polls would report it, so that automatic mode does not
        /// run that card again while it stays on the reader. Called with what the run's reads found: the preliminary read
        /// of the window first, then the read of the execution service, which is the one the tasks work on.
        /// </summary>
        /// <param name="chip">The chip a read left behind; none or an empty chip changes nothing.</param>
        private void RememberCard(GenericChipModel chip)
        {
            var uid = ReaderSnapshotBuilder.PrimaryUidOf(chip);
            if (uid != null)
            {
                _autoMode?.RememberCard(uid);
            }
        }

        /// <summary>The longest a run waits for a reader scan of an extension to end before it gives up.</summary>
        internal TimeSpan MaxWaitForAScan { get; set; } = TimeSpan.FromSeconds(20);

        // A scan is short. A run that meets one waits for it instead of being dropped, so that a click is not lost and a
        // card of automatic mode does not wait for the next tick. Any other reason to refuse (a run, a load, an edit)
        // still refuses at once. A request of automatic mode can be withdrawn while it waits (isStillWanted).
        internal Task<bool> TryBeginExecutionAfterAScanAsync(Func<bool> isStillWanted = null) =>
            TryBeginAfterAScanAsync(_coordinator.TryBeginExecution, isStillWanted);

        /// <summary>
        /// Admits an operation with <paramref name="tryBegin"/>, waiting up to <see cref="MaxWaitForAScan"/> while the
        /// only thing in the way is a reader scan.
        /// </summary>
        /// <param name="tryBegin">The coordinator's admission method for the operation.</param>
        /// <param name="isStillWanted">
        /// Asked before every attempt, so before the one that succeeds when the scan has ended: when it returns
        /// <c>false</c> the operation is not admitted. <c>null</c> means it is always wanted.
        /// </param>
        /// <returns><c>true</c> when the operation was admitted.</returns>
        private async Task<bool> TryBeginAfterAScanAsync(Func<bool> tryBegin, Func<bool> isStillWanted = null)
        {
            var giveUp = DateTime.UtcNow + MaxWaitForAScan;
            while (isStillWanted == null || isStillWanted())
            {
                if (tryBegin())
                {
                    return true;
                }

                if (!_coordinator.IsScanning)
                {
                    // Not a scan, or a scan that ended since the attempt: one more attempt tells which.
                    return (isStillWanted == null || isStillWanted()) && tryBegin();
                }

                if (DateTime.UtcNow >= giveUp)
                {
                    return false;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(25));
            }

            return false;
        }

        private void TaskExecutionService_ExecutionCompleted(object sender, TaskExecutionCompletedEventArgs e)
        {
            Mouse.OverrideCursor = null;
            OnPropertyChanged(nameof(TreeViewParentNodes));
            OnPropertyChanged(nameof(ChipTasks));
        }
        /// <summary>
        ///
        /// </summary>
        public ICommand CloseAllCommand => new RelayCommand(OnCloseAll);
        private void OnCloseAll()
        {
            Dialogs.Clear();
        }

        /// <summary>
        /// 
        /// </summary>
        public IAsyncRelayCommand SwitchLanguageToGerman => new AsyncRelayCommand(SetGermanLanguage);
        private async Task SetGermanLanguage()
        {
            using (var settings = new SettingsReaderWriter())
            {
                if (settings.DefaultSpecification.DefaultLanguage != "german")
                {
                    settings.DefaultSpecification.DefaultLanguage = "german";
                    await settings.SaveSettings();
                    OnNewLanguageChangedDialog();
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public IAsyncRelayCommand SwitchLanguageToEnglish => new AsyncRelayCommand(SetEnglishLanguage);
        private async Task SetEnglishLanguage()
        {
            using (var settings = new SettingsReaderWriter())
            {
                if (settings.DefaultSpecification.DefaultLanguage != "english")
                {
                    settings.DefaultSpecification.DefaultLanguage = "english";
                    await settings.SaveSettings();
                    OnNewLanguageChangedDialog();
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private void OnNewLanguageChangedDialog()
        {
            Dialogs.Add(new CustomDialogViewModel()
            {
                Message = ResourceLoader.GetResource("messageBoxRestartRequiredMessage"),
                Caption = ResourceLoader.GetResource("messageBoxRestartRequiredCaption"),

                OnCancel = (sender) =>
                {
                    sender.Close();
                },

                OnOk = (sender) =>
                {
                    {
                        Environment.Exit(0);
                    }
                }
            });

        }

        /// <summary>
        /// 
        /// </summary>
        private void OnNewNoReaderFoundDialog()
        {
            Dialogs.Add(new CustomDialogViewModel()
            {
                Message = ResourceLoader.GetResource("messageBoxNoReaderFoundMessage"),
                Caption = ResourceLoader.GetResource("messageBoxNoReaderFoundCaption"),

                OnCancel = (sender) =>
                {
                    sender.Close();
                },

                OnOk = (sender) =>
                {
                    sender.Close();
                }
            });

        }

        /// <summary>
        /// 
        /// </summary>
        public IAsyncRelayCommand NewReaderSetupDialogCommand => new AsyncRelayCommand(OnNewReaderSetupDialog);
        private Task OnNewReaderSetupDialog()
        {
            var settings = new SettingsReaderWriter();

            var currentSettings = settings.DefaultSpecification;

            ReaderDevice.PortNumber = int.TryParse(currentSettings.LastUsedComPort, out var portNumber) ? portNumber : 0;
            ReaderDevice.Reader = currentSettings.DefaultReaderProvider;

            using (var device = ReaderDevice.Instance)
            {
                Dialogs.Add(new SetupViewModel(device, settings, Dialogs)
                {
                    Caption = ResourceLoader.GetResource("windowCaptionReaderSetup"),

                    OnOk = (sender) =>
                    {
                        // Judged before the settings are overwritten: the dialog also confirms changes that have nothing
                        // to do with the reader (the update check), and those must not stop automatic mode.
                        var readerChanged = currentSettings.DefaultReaderProvider != sender.SelectedReader
                            || (sender.SelectedReader == ReaderTypes.PCSC
                                && !string.Equals(currentSettings.DefaultReaderName, sender.SelectedReaderName, StringComparison.Ordinal))
                            || (sender.SelectedReader == ReaderTypes.Elatec
                                && !string.Equals(currentSettings.LastUsedComPort, sender.ComPort, StringComparison.Ordinal));

                        // The reader of the SAM is saved only for the PC/SC provider: the dialog does not show it for another one.
                        var samReaderToSave = PcscReaderChoice.SamReaderToSave(sender.SelectedReader == ReaderTypes.PCSC, sender.SelectedSamReaderName, currentSettings.SamReaderName);
                        var samReaderChanged = !string.Equals(currentSettings.SamReaderName, samReaderToSave, StringComparison.Ordinal);

                        // The pipe is opened or not at start-up only, so a change needs a restart.
                        var externalControlChanged = currentSettings.EnableExternalControl != sender.EnableExternalControl;

                        currentSettings.DefaultReaderProvider = sender.SelectedReader;
                        currentSettings.DefaultReaderName = sender.SelectedReader == ReaderTypes.PCSC
                            ? sender.SelectedReaderName
                            : string.Empty;
                        currentSettings.AutoLoadProjectOnStart = sender.LoadOnStart;
                        currentSettings.LastUsedComPort = sender.ComPort;
                        currentSettings.AutoCheckForUpdates = sender.CheckOnStart;
                        currentSettings.LastUsedBaudRate = sender.SelectedBaudRate;
                        currentSettings.EnableExternalControl = sender.EnableExternalControl;
                        currentSettings.SamReaderName = samReaderToSave;

                        settings.DefaultSpecification = currentSettings;

                        var saveTask = sender.SaveSettings.ExecuteAsync(null);

                        // Switch the provider first: setting the name announces the change to extensions, and they read
                        // the connection and card state of whichever provider is current when that happens.
                        ReaderDevice.Reader = sender.SelectedReader;

                        // The card is never read in the reader of the SAM: a provider that exists learns it now, together with the reader of
                        // the card that was chosen (it may not have been connected: the provider still holds the old name, which can be the
                        // reader that is the SAM's now). Also when only the reader or the provider changed: the file may not be written yet
                        // when a provider that is made now reads its names from it.
                        if (PcscReaderChoice.ProviderLearnsReaders(sender.SelectedReader == ReaderTypes.PCSC, samReaderChanged, readerChanged, sender.IsValidClassicKeysText != false)
                            && ReaderDevice.Instance is LibLogicalAccessProvider pcscProvider)
                        {
                            pcscProvider.UpdateReaders(sender.SelectedReaderName, samReaderToSave);
                        }

                        // Another reader: automatic mode does not carry on with it unasked, and forgets the card of the old
                        // one. (Not in the setter of CurrentReader: the reader monitor keeps assigning a status label to it.)
                        if (readerChanged)
                        {
                            _autoMode?.OnReaderChanged();
                        }

                        CurrentReader = string.IsNullOrWhiteSpace(sender.SelectedReaderName)
                            ? Enum.GetName(typeof(ReaderTypes), sender.SelectedReader)
                            : sender.SelectedReaderName;

                        sender.Close();

                        IsReaderBusy = false;

                        // Not for settings that did not pass the check of the dialog: nothing was saved, so there is nothing to restart for.
                        if (externalControlChanged && sender.IsValidClassicKeysText != false)
                        {
                            // The same notice as for the language: the software must be restarted for the change to apply.
                            // Accepting it ends the process, so it is offered only after the write has finished.
                            _ = RestartNotice.OfferWhenSavedAsync(saveTask, OnNewLanguageChangedDialog);
                        }
                    },

                    OnConnect = (sender) =>
                    {
                        IsReaderBusy = true;
                    },

                    OnUpdateReaderStatus = (sender) =>
                    {
                        IsReaderBusy = sender;
                    },

                    OnBeginUpdateCheck = (sender) =>
                    {
                        if (sender)
                        {
                            updater?.StartMonitoring();
                        }
                    },

                    OnCancel = (sender) =>
                    {
                        sender.Close();
                        IsReaderBusy = false;
                        mw.Activate();
                    },

                    OnCloseRequest = (sender) =>
                    {
                        sender.Close();
                        IsReaderBusy = false;
                        mw.Activate();
                    }
                });
            }

            return Task.CompletedTask;
        }

        private void MainWindowViewModel_ReaderStatusChanged(object sender, EventArgs e) => throw new NotImplementedException();

        /// <summary>
        /// 
        /// </summary>
        public IAsyncRelayCommand NewOpenFileDialogCommand => new AsyncRelayCommand(OnNewOpenFileDialog);
        private async Task OnNewOpenFileDialog()
        {
            using (var settings = settingsReaderWriterFactory())
            {
                CurrentReader = string.IsNullOrWhiteSpace(settings.DefaultSpecification.DefaultReaderName)
                    ? Enum.GetName(typeof(ReaderTypes), settings.DefaultSpecification.DefaultReaderProvider)
                    : settings.DefaultSpecification.DefaultReaderName;

                ApplyCulture((settings.DefaultSpecification.DefaultLanguage == "german") ? new CultureInfo("de-DE") : new CultureInfo("en-US"));


                var dlg = new OpenFileDialogViewModel
                {
                    Title = ResourceLoader.GetResource("windowCaptionOpenProject"),
                    Filter = ResourceLoader.GetResource("filterStringSaveTasks"),
                    ParentWindow = this.mw,
                    Multiselect = false
                };

                if (dlg.Show(Dialogs) && dlg.FileName != null)
                {
                    Mouse.OverrideCursor = Cursors.AppStarting;
                    try
                    {
                        var result = await extensionHost.Project.LoadAsync(dlg.FileName);
                        if (!result.Success)
                        {
                            LogProjectOpenFailure(dlg.FileName, result.Error);
                            return;
                        }

                        settings.DefaultSpecification.LastUsedProjectPath = result.CanonicalPath;
                        await settings.SaveSettings();
                    }
                    finally
                    {
                        Mouse.OverrideCursor = null;
                    }
                }
            }
        }

        /// <summary>
        /// Moves the card keys that are typed into the tasks of a project into the Windows Credential Manager and saves a new
        /// project that refers to them. The project that is open is not touched: the migration reads and writes files of its own.
        /// </summary>
        public IAsyncRelayCommand MigrateProjectKeysCommand => new AsyncRelayCommand(OnMigrateProjectKeys);
        private async Task OnMigrateProjectKeys()
        {
            using (var projects = new DatabaseKeyMigrationProjects())
            {
                var migration = new KeyMigration(projects, new WindowsCredentialKeyProvider(new NativeCredentialStore()));
                var flow = new KeyMigrationFlow(migration, new MvvmKeyMigrationDialogs(Dialogs, mw), ResourceLoader.GetResource);

                await flow.RunAsync();
            }
        }

        /// <summary>
        /// Installs an update of an extension from a package (<c>.rfext</c>) that the person chooses, for this user: the package is
        /// checked, the person is asked, and the extension is put in a folder of the user's own, so neither administrator rights nor
        /// a visit to the extensions folder are needed. A running extension is not touched; the new version is used after a restart.
        /// </summary>
        public IAsyncRelayCommand InstallExtensionCommand => new AsyncRelayCommand(OnInstallExtension);
        private async Task OnInstallExtension()
        {
            var userRoot = ExtensionPackageLayout.DefaultUserRoot();
            if (string.IsNullOrEmpty(userRoot))
            {
                return;
            }

            var installer = new ExtensionPackageInstaller(
                userRoot,
                MefHelper.Instance.ExtensionsPath,
                typeof(MefHelper).Assembly.GetName().Version ?? new Version(0, 0, 0, 0));
            var flow = new ExtensionInstallFlow(installer, new MvvmExtensionInstallDialogs(Dialogs, mw), ResourceLoader.GetResource);

            await flow.RunAsync();
        }

        /// <summary>Gets or sets how the key manager of the menu is shown; the tests replace it.</summary>
        internal IStoredKeysLauncher StoredKeysLauncher { get; set; } = new RFiDGear.View.WpfStoredKeysLauncher();

        /// <summary>
        /// Opens the key manager: the keys that are stored in the Windows Credential Manager, listed by name, with a way to store a key
        /// under a name and to remove one. It needs no project and no task, and it never shows a key.
        /// </summary>
        public IRelayCommand OpenKeyManagerCommand => new RelayCommand(OnOpenKeyManager);
        private void OnOpenKeyManager() => StoredKeysLauncher.Show(mw);

        /// <summary>Gets or sets how the SAM setup of the menu is shown; the tests replace it.</summary>
        internal ISamSetupLauncher SamSetupLauncher { get; set; } = new RFiDGear.View.WpfSamSetupLauncher();

        /// <summary>
        /// Opens the setup of a MIFARE SAM AV3 in a PC/SC reader: find the SAM, activate a new one, authenticate as host and write one
        /// key entry. It needs no project. It holds the gate of the reader and the scan slot of the coordinator while it is open, so
        /// the reader monitor, the automatic mode, the tasks and the reads of the window leave the reader alone; if a run, a load, an
        /// edit or a scan is in progress, somebody holds the gate, or the window says that it uses the reader (the quick check, which
        /// works on the reader without the coordinator and the gate, says so with the busy flag only), the window does not open.
        /// </summary>
        public IRelayCommand OpenSamSetupCommand => new RelayCommand(OnOpenSamSetup);
        private void OnOpenSamSetup() => SamSetupLauncher.Show(mw, _readerGate, _coordinator, () => IsReaderBusy, busy => IsReaderBusy = busy);

        /// <summary>Logs a project-open failure without changing the currently loaded project.</summary>
        private static void LogProjectOpenFailure(string path, ProjectOpenError error) =>
            Log.ForContext<MainWindowViewModel>()
                .Warning("Project open rejected for {ProjectPath} with {ProjectOpenError}", path, error);

        /// <summary>
        /// Exposes the command to save the current project, falling back to Save As
        /// when no valid last-used project path is available.
        /// </summary>
        public IAsyncRelayCommand SaveTaskDialogCommand => new AsyncRelayCommand(OnNewSaveTaskDialogCommand);
        private async Task OnNewSaveTaskDialogCommand()
        {
            var targetPath = ResolveProjectSaveTarget();

            if (targetPath == null)
            {
                await OnNewSaveTaskAsDialogCommand();
                return;
            }

            TrySaveProject(targetPath);
        }

        /// <summary>
        /// Decides which file a plain Save writes to, or <c>null</c> when the user has to choose one (Save As).
        /// <para>
        /// When a project is open, Save only ever writes that project. If its file has since been moved or
        /// deleted the answer is <c>null</c>: falling back to the remembered default here would silently write
        /// this project's contents over a different project. The remembered default is consulted only when no
        /// project is open at all, e.g. right after startup without an auto-loaded project.
        /// </para>
        /// </summary>
        /// <returns>An existing project file path, or <c>null</c> if Save As should be offered.</returns>
        internal string ResolveProjectSaveTarget()
        {
            var openProjectPath = extensionHost.Project.CurrentPath;

            if (openProjectPath != null)
            {
                return IsValidProjectSavePath(openProjectPath) ? openProjectPath : null;
            }

            using (var settings = settingsReaderWriterFactory())
            {
                var rememberedPath = settings.DefaultSpecification.LastUsedProjectPath;
                return IsValidProjectSavePath(rememberedPath) ? rememberedPath : null;
            }
        }

        /// <summary>
        /// Expose Command to Save As Menu Item
        /// </summary>
        public IAsyncRelayCommand SaveTaskAsDialogCommand => new AsyncRelayCommand(OnNewSaveTaskAsDialogCommand);
        private async Task OnNewSaveTaskAsDialogCommand()
        {
            string fileName;

            var dlg = new SaveFileDialogViewModel
            {
                Title = ResourceLoader.GetResource("windowCaptionSaveTasks"),
                Filter = ResourceLoader.GetResource("filterStringSaveTasks"),
            };

            if (dlg.Show(Dialogs) && dlg.FileName != null)
            {
                fileName = dlg.FileName;
            }
            else
            {
                return;
            }

            if (!ConfirmReplacingRenamedProject(fileName))
            {
                return;
            }

            var mbox = new MessageBoxViewModel
            {
                ParentWindow = mw,
                Caption = ResourceLoader.GetResource("windowCaptionAskTaskDefault"),
                Message = ResourceLoader.GetResource("messageBoxMessageSetProjectAsDefault"),
                Buttons = MessageBoxButton.YesNo
            };

            var setAsDefault = mbox.Show(Dialogs) == MessageBoxResult.Yes;

            await SaveProjectAsAsync(fileName, setAsDefault);
        }

        /// <summary>
        /// Saves the project to <paramref name="fileName"/> through the extension host and, only after the file
        /// was written, remembers the file as the default project when asked to. The remembered path is the one
        /// the writer actually produced: a name typed without an extension is saved as <c>name.rfPrj</c>, and
        /// remembering the typed name would leave a path that is never valid. A failed write changes nothing.
        /// </summary>
        /// <param name="fileName">The path chosen in the Save As dialog.</param>
        /// <param name="setAsDefault">Whether to store the written file as <c>LastUsedProjectPath</c>.</param>
        /// <returns>A task that completes once the project is saved and the default path is stored.</returns>
        internal async Task SaveProjectAsAsync(string fileName, bool setAsDefault)
        {
            if (string.IsNullOrEmpty(fileName) || !TrySaveProject(fileName))
            {
                return;
            }

            AnnounceSavedName(fileName);

            if (setAsDefault)
            {
                using (var settings = settingsReaderWriterFactory())
                {
                    settings.DefaultSpecification.LastUsedProjectPath = extensionHost.Project.CurrentPath;
                    await settings.SaveSettings();
                }
            }
        }

        /// <summary>
        /// A project is saved as <c>.xml</c> or <c>.rfPrj</c>; any other name gets <c>.rfPrj</c> appended. The file
        /// dialog only confirmed replacing the name the user typed, so when the saved name is another file that
        /// exists, the user is asked before it is replaced.
        /// </summary>
        /// <param name="chosenPath">The path chosen in the Save As dialog.</param>
        /// <returns><c>false</c> when the user declined to replace the other file.</returns>
        private bool ConfirmReplacingRenamedProject(string chosenPath)
        {
            var target = DatabaseReaderWriter.ProjectSavePath(chosenPath);
            if (string.Equals(target, chosenPath, StringComparison.OrdinalIgnoreCase) || !File.Exists(target))
            {
                return true;
            }

            var mbox = new MessageBoxViewModel
            {
                ParentWindow = mw,
                Caption = ResourceLoader.GetResource("messageBoxDefaultCaption"),
                Message = string.Format(
                    CultureInfo.CurrentCulture,
                    ResourceLoader.GetResource("messageBoxProjectReplaceMessage"),
                    target),
                Buttons = MessageBoxButton.YesNo
            };

            return mbox.Show(Dialogs) == MessageBoxResult.Yes;
        }

        /// <summary>
        /// Tells the user the name the project was saved under when it is not the one typed (an extension was
        /// appended), so it is clear which file to open again.
        /// </summary>
        /// <param name="chosenPath">The path chosen in the Save As dialog.</param>
        private void AnnounceSavedName(string chosenPath)
        {
            var written = extensionHost.Project.CurrentPath;
            if (written == null || string.Equals(written, Path.GetFullPath(chosenPath), StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            Dialogs.Add(new CustomDialogViewModel
            {
                Caption = ResourceLoader.GetResource("messageBoxDefaultCaption"),
                Message = string.Format(
                    CultureInfo.CurrentCulture,
                    ResourceLoader.GetResource("messageBoxProjectSavedAsMessage"),
                    written),
                OnOk = sender => sender.Close()
            });
        }

        /// <summary>
        /// Writes the project through the extension host, which also keeps its current path in step. Failures
        /// (a load or edit in progress, an unwritable location) are logged and shown instead of escaping the command.
        /// </summary>
        private bool TrySaveProject(string path)
        {
            try
            {
                extensionHost.Project.SaveAs(path);
                return true;
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is IOException || ex is UnauthorizedAccessException)
            {
                Log.ForContext<MainWindowViewModel>().Error(ex, "Saving the project to {ProjectPath} failed.", path);
                ShowSaveFailure(ex);
                return false;
            }
        }

        /// <summary>
        /// Exports the chip tree through the database writer. A failure (an unwritable location, a chip tree that
        /// cannot be serialized) is logged and shown; the application keeps running and an existing file is kept.
        /// </summary>
        internal bool TrySaveChipTree(string path)
        {
            try
            {
                databaseReaderWriter.WriteDatabase(TreeViewParentNodes, path);
                return true;
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is IOException || ex is UnauthorizedAccessException)
            {
                Log.ForContext<MainWindowViewModel>().Error(ex, "Saving the chip tree to {ChipPath} failed.", path);
                ShowSaveFailure(ex);
                return false;
            }
        }

        private void ShowSaveFailure(Exception ex)
        {
            Dialogs.Add(new CustomDialogViewModel
            {
                Caption = ResourceLoader.GetResource("messageBoxDefaultCaption"),
                Message = ex.Message,
                OnOk = sender => sender.Close()
            });
        }

        private static bool IsValidProjectSavePath(string projectPath)
        {
            if (string.IsNullOrWhiteSpace(projectPath))
            {
                return false;
            }

            if (!Path.HasExtension(projectPath))
            {
                return false;
            }

            if (Directory.Exists(projectPath))
            {
                return false;
            }

            return File.Exists(projectPath);
        }

        /// <summary>
        /// Expose Command to Save Menu Item
        /// </summary>
        public IAsyncRelayCommand SaveChipDialogCommand => new AsyncRelayCommand(OnNewSaveChipDialogCommand);
        private Task OnNewSaveChipDialogCommand()
        {
            var dlg = new SaveFileDialogViewModel
            {
                Title = ResourceLoader.GetResource("windowCaptionSaveTasks"),
                // A chip export is XML, not a project archive: it must not be offered under a project's extension.
                Filter = ResourceLoader.GetResource("filterStringSaveChipTree")
            };

            if (dlg.Show(Dialogs) && dlg.FileName != null)
            {
                // The file dialog confirmed replacing the name the user typed; the export is saved as .xml, which may
                // be another file that exists. That one is not replaced without asking.
                var target = DatabaseReaderWriter.ChipExportPath(dlg.FileName);
                if (!string.Equals(target, dlg.FileName, StringComparison.OrdinalIgnoreCase) && File.Exists(target))
                {
                    Dialogs.Add(new CustomDialogViewModel
                    {
                        Caption = ResourceLoader.GetResource("messageBoxDefaultCaption"),
                        Message = string.Format(
                            CultureInfo.CurrentCulture,
                            ResourceLoader.GetResource("messageBoxChipExportReplaceMessage"),
                            target),
                        OnOk = sender =>
                        {
                            sender.Close();
                            TrySaveChipTree(dlg.FileName);
                        },
                        OnCancel = sender => sender.Close()
                    });
                }
                else
                {
                    TrySaveChipTree(dlg.FileName);
                }
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Opens the help (F1 and Help > Help): the compiled help in the language of the settings, next to the program. A help window that
        /// is open already is left alone; a help file that is missing or cannot be started is logged and told to the user.
        /// </summary>
        public ICommand ShowHelpCommand => new RelayCommand(OnNewShowHelpCommand);
        private void OnNewShowHelpCommand()
        {
            if (HelpFile.IsOpen())
            {
                return;
            }

            string language;

            using (var settingsReaderWriter = new SettingsReaderWriter())
            {
                language = settingsReaderWriter.DefaultSpecification.DefaultLanguage;
            }

            if (!HelpFile.TryOpen(HelpFile.PathFor(language, AppDomain.CurrentDomain.BaseDirectory)))
            {
                Dialogs.Add(new MessageBoxViewModel
                {
                    ParentWindow = mw,
                    Caption = ResourceLoader.GetResource("messageBoxHelpNotFoundCaption"),
                    Message = string.Format(CultureInfo.CurrentCulture, ResourceLoader.GetResource("messageBoxHelpNotFoundMessage"), HelpFile.FileNameFor(language)),
                    Buttons = MessageBoxButton.OK,
                    Image = MessageBoxImage.Information
                });
            }
        }
        /// <summary>
        ///
        /// </summary>
        public IAsyncRelayCommand CheckUpdateCommand => new AsyncRelayCommand(OnNewCheckUpdateCommand);
        private async Task OnNewCheckUpdateCommand()
        {
            userIsNotifiedForAvailableUpdate = false;

            await updateNotifier.TriggerUpdateCheckAsync(() => AskForUpdateNow());

            // The check of this window's updater runs in the background after the start: what it found is only known once it is
            // through. The notifier has an updater of its own, whose answer may have come later than this one's, in which case
            // the offer above was skipped: it is made here (it is shown once, whoever asks first).
            await updater.InitialCheck;

            if (updater.UpdateAvailable)
            {
                await AskForUpdateNow();
            }
            else
            {
                await InvokeOnUiThread(new Action(() =>
                {
                    Dialogs.Add(new MessageBoxViewModel
                    {
                        ParentWindow = mw,
                        Caption = ResourceLoader.GetResource("messageBoxNoUpdateAvailableCaption"),
                        Message = ResourceLoader.GetResource("messageBoxNoUpdateAvailableMessage"),
                        Buttons = MessageBoxButton.OK,
                        Image = MessageBoxImage.Information
                    });
                }));
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public ICommand CloseApplication => new RelayCommand(OnCloseRequest);
        private void OnCloseRequest()
        {
            if (!ShouldClose(AskToCloseDuringBridgeRun))
            {
                return;
            }

            externalControl?.Shutdown(RFiDGear.Bridge.ShutdownReasons.UserClose);
            Environment.Exit(0);
        }

        #endregion Menu Commands

        #region Dependency Properties

        /// <summary>
        /// expose contextmenu on row click
        /// </summary>
        public ObservableCollection<MenuItem> EmptySpaceTreeViewContextMenu => emptySpaceTreeViewContextMenu;

        private ObservableCollection<MenuItem> emptySpaceTreeViewContextMenu;
        /// <summary>
        /// expose contextmenu on row click
        /// </summary>
        public ObservableCollection<MenuItem> RowContextMenu => rowContextMenuItems;
        private ObservableCollection<MenuItem> rowContextMenuItems;

        /// <summary>
        /// expose contextmenu on empty space click
        /// </summary>
        public ObservableCollection<MenuItem> EmptySpaceContextMenu => emptySpaceContextMenuItems;
        private ObservableCollection<MenuItem> emptySpaceContextMenuItems;


        /// <summary>
        /// 
        /// </summary>
        public object SelectedSetupViewModel
        {
            get => selectedSetupViewModel;
            set
            {
                selectedSetupViewModel = value;
                OnPropertyChanged(nameof(SelectedSetupViewModel));
            }

        }
        private object selectedSetupViewModel;

        /// <summary>
        /// 
        /// </summary>
        public ObservableCollection<RFiDChipParentLayerViewModel> TreeViewParentNodes
        {
            get => treeViewParentNodes;

            set
            {
                treeViewParentNodes = value;
                OnPropertyChanged(nameof(TreeViewParentNodes));
            }
        }
        private ObservableCollection<RFiDChipParentLayerViewModel> treeViewParentNodes;

        /// <summary>
        /// 
        /// </summary>
        public ChipTaskHandlerModel ChipTasks
        {
            get => taskHandler;

            set
            {
                taskHandler = value;
                OnPropertyChanged(nameof(ChipTasks));
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public bool IsWriteToAllChipAutoChecked => _autoMode?.IsEnabled ?? false;

        /// <summary>
        /// 
        /// </summary>
        public bool IsWriteSelectedToChipAutoChecked => _autoMode?.IsEnabled ?? false;
        private bool isWriteSelectedToChipAutoCheckedTemp;

        /// <summary>
        ///
        /// </summary>
        public string CurrentReader
        {
            get => currentReader;
            set
            {
                var changed = !string.Equals(currentReader, value, StringComparison.Ordinal);
                currentReader = value;
                OnPropertyChanged(nameof(CurrentReader));

                // Extensions read the name through IReaderHost; tell them it moved. Before start-up has built the host
                // there is nobody to tell, and the host reads the current name when it is asked.
                if (changed)
                {
                    extensionHost?.NotifyReaderStateChanged();
                }
            }
        }
        private string currentReader;

        /// <summary>
        ///
        /// </summary>
        public string ReaderStatus
        {
            get => readerStatus;
            set
            {
                readerStatus = value;
                OnPropertyChanged(nameof(ReaderStatus));
            }
        }
        private string readerStatus;

        /// <summary>
        ///
        /// </summary>
        public string DateTimeStatusBar
        {
            get => dateTimeStatusBar;
            set
            {
                dateTimeStatusBar = value;
                OnPropertyChanged(nameof(DateTimeStatusBar));
            }
        }
        private string dateTimeStatusBar;

        /// <summary>
        /// Short warning text shown in the status bar when the app is running in an RDP session
        /// with PC/SC unavailable.
        /// </summary>
        public string RdpWarningMessage
        {
            get => rdpWarningMessage;
            set
            {
                rdpWarningMessage = value;
                OnPropertyChanged(nameof(RdpWarningMessage));
            }
        }
        private string rdpWarningMessage;

        /// <summary>
        /// Controls visibility of the RDP/PC/SC warning item in the status bar.
        /// </summary>
        public bool IsRdpWarningVisible
        {
            get => isRdpWarningVisible;
            set
            {
                isRdpWarningVisible = value;
                OnPropertyChanged(nameof(IsRdpWarningVisible));
            }
        }
        private bool isRdpWarningVisible;

        /// <summary>
        /// Gets whether a program is connected to the external control bridge; shows the item in the status bar. Set on the
        /// UI thread.
        /// </summary>
        public bool IsExternalControlConnected
        {
            get => isExternalControlConnected;
            set
            {
                isExternalControlConnected = value;
                OnPropertyChanged(nameof(IsExternalControlConnected));
            }
        }
        private bool isExternalControlConnected;

        /// <summary>
        /// 
        /// </summary>
        public bool IsSelected
        {
            get; set;
        }

        /// <summary>
        /// 
        /// </summary>
        public bool? IsReaderBusy
        {
            get => isReaderBusy;
            set
            {
                isReaderBusy = value;
                if (value == true)
                {
                    readerMonitor?.Pause();
                }
                else
                {
                    readerMonitor?.Resume();
                }
                OnPropertyChanged(nameof(IsReaderBusy));
            }
        }
        private bool? isReaderBusy;

        /// <summary>
        /// Gets a value indicating whether the reader is still being set up: the application has started, but the reader
        /// monitor has not yet had its first look at the reader. The status bar shows INIT instead of IDLE or UNKNOWN while
        /// this is <see langword="true"/>; a busy reader is still shown as busy.
        /// </summary>
        public bool IsReaderInitializing
        {
            get => isReaderInitializing;
            private set
            {
                if (isReaderInitializing == value)
                {
                    return;
                }

                isReaderInitializing = value;
                OnPropertyChanged(nameof(IsReaderInitializing));
            }
        }
        private bool isReaderInitializing = true;

        /// <summary>
        ///
        /// </summary>
        public bool IsCheckForUpdatesChecked
        {
            get
            {
                using (var settings = new SettingsReaderWriter())
                {
                    return settings.DefaultSpecification != null && settings.DefaultSpecification.AutoCheckForUpdates;
                }
            }
            set
            {
                using (var settings = new SettingsReaderWriter())
                {
                    if (value)
                    {
                        updater.StartMonitoring().GetAwaiter().GetResult();
                    }
                    else
                    {
                        updater.StopMonitoring().GetAwaiter().GetResult();
                    }

                    settings.DefaultSpecification.AutoCheckForUpdates = value;
                    settings.SaveSettings().GetAwaiter().GetResult();
                }
                OnPropertyChanged(nameof(IsCheckForUpdatesChecked));

            }
        }

        /// <summary>
        /// 
        /// </summary>
        public bool RadioButtonGermanLanguageSelectedState
        {
            get
            {
                using (var settings = new SettingsReaderWriter())
                {
                    if (settings.DefaultSpecification.DefaultLanguage == "german")
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            set
            {
                using (var settings = new SettingsReaderWriter())
                {
                    if (settings.DefaultSpecification.DefaultLanguage == "english")
                    {
                        value = false;
                    }

                    OnPropertyChanged(nameof(RadioButtonGermanLanguageSelectedState));
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public bool RadioButtonEnglishLanguageSelectedState
        {
            get
            {
                using (var settings = new SettingsReaderWriter())
                {
                    if (settings.DefaultSpecification.DefaultLanguage == "german")
                    {
                        return false;
                    }
                    else
                    {
                        return true;
                    }
                }
            }
            set
            {
                using (var settings = new SettingsReaderWriter())
                {
                    if (settings.DefaultSpecification.DefaultLanguage == "german")
                    {
                        value = false;
                    }

                    OnPropertyChanged(nameof(RadioButtonEnglishLanguageSelectedState));
                }
            }
        }

        #endregion Dependency Properties

        #region Extensions
        private void EnableUpdate(object sender, EventArgs e)
        {
            userIsNotifiedForAvailableUpdate = true;
        }

        /// <summary>
        /// How an action gets onto the thread of the window, where a dialog is added. The tests replace it: the host they run
        /// in has no application of its own, or one whose thread is gone.
        /// </summary>
        internal Func<Action, Task> InvokeOnUiThread { get; set; } =
            action => Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Normal, action).Task;

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        private async Task AskForUpdateNow()
        {
            await AskForUpdateNow(false);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateDisabled"></param>
        /// <returns></returns>
        private async Task AskForUpdateNow(bool updateDisabled)
        {
            // The update notifier has an updater of its own, and this window has another one; both ask the server for the manifest
            // in the background, so the notifier's may be through first. The dialog shows the notes of this window's updater and
            // the download uses its manifest: it waits (without blocking anyone) until that updater has its answer. If that
            // answer is that there is no update, nothing is offered and nothing is consumed; the notifier asks again every few
            // seconds. The notes that are shown on request (updateDisabled) are shown whether there is an update or not.
            await updater.InitialCheck;

            if (!updateDisabled && !updater.UpdateAvailable)
            {
                return;
            }

            if (!userIsNotifiedForAvailableUpdate || updateDisabled)
            {
                await InvokeOnUiThread(new Action(() =>
                {
                    // Decided again here, on the thread of the window: two asks that were let through together (a tick of the
                    // notifier and "Check for updates") must not both put a dialog up.
                    if (userIsNotifiedForAvailableUpdate && !updateDisabled)
                    {
                        return;
                    }

                    userIsNotifiedForAvailableUpdate = true;

                    Dialogs.Add(new UpdateNotifierViewModel(updater.UpdateInfoText)
                    {
                        Caption = updateDisabled ? "Changelog" : "Update Available",

                        OnOk = async (updateAction) =>
                        {
                            updateAction.Close();

                            if (!updateDisabled)
                            {
                                await StartDownloadWithProgressAsync();
                            }
                        },

                        OnCancel = (updateAction) =>
                        {
                            updater.AllowUpdate = false;
                            updateAction.Close();
                            mw.Activate();
                        },

                        OnCloseRequest = (updateAction) =>
                        {
                            updater.AllowUpdate = false;
                            updateAction.Close();
                            mw.Activate();
                        }
                    });
                }));
            }

            userIsNotifiedForAvailableUpdate = true;
        }

        private async Task StartDownloadWithProgressAsync()
        {
            using var cts = new CancellationTokenSource();

            var progressVm = new DownloadProgressViewModel
            {
                Caption = "Downloading Update",
                OnCancel = vm =>
                {
                    cts.Cancel();
                    vm.StatusText = "Cancelling…";
                    vm.IsIndeterminate = true;
                },
                OnCloseRequest = vm => vm.Close()
            };

            Dialogs.Add(progressVm);

            // IProgress<T> marshals Report() back to the UI thread automatically.
            var uiProgress = new Progress<(int fileIndex, int fileCount, string fileName, long bytesReceived, long totalBytes)>(t =>
            {
                if (t.totalBytes > 0)
                {
                    var overall = (t.fileIndex + (double)t.bytesReceived / t.totalBytes) / t.fileCount * 100.0;
                    progressVm.IsIndeterminate = false;
                    progressVm.ProgressValue = overall;
                    var mb = t.bytesReceived / 1_048_576.0;
                    var totalMb = t.totalBytes / 1_048_576.0;
                    progressVm.StatusText = t.fileCount > 1
                        ? $"File {t.fileIndex + 1} of {t.fileCount}: {t.fileName}  {mb:F1} / {totalMb:F1} MB"
                        : $"Downloading {t.fileName}  {mb:F1} / {totalMb:F1} MB";
                }
                else
                {
                    progressVm.IsIndeterminate = true;
                    var mb = t.bytesReceived / 1_048_576.0;
                    progressVm.StatusText = t.fileCount > 1
                        ? $"File {t.fileIndex + 1} of {t.fileCount}: {t.fileName}  {mb:F1} MB received"
                        : $"Downloading {t.fileName}  {mb:F1} MB received";
                }
            });

            Action<int, int, string, long, long> onProgress =
                (fi, fc, fn, br, tb) => ((IProgress<(int, int, string, long, long)>)uiProgress).Report((fi, fc, fn, br, tb));

            try
            {
                await updater.Update(onProgress, cts.Token);
            }
            catch (OperationCanceledException)
            {
                updater.AllowUpdate = false;
                progressVm.Close();
                mw.Activate();
            }
            catch (Exception ex)
            {
                Log.ForContext<MainWindowViewModel>().Error(ex, "Update download failed.");
                progressVm.StatusText = $"Download failed: {ex.Message}";
                progressVm.IsIndeterminate = false;
                // Leave dialog open so the user sees the error message; Cancel button closes it.
            }
        }

        private void CloseThreads(object sender, CancelEventArgs e)
        {
            // Closing the window while another program is running the tasks cuts that run off on the card: the user decides.
            if (!ShouldClose(AskToCloseDuringBridgeRun))
            {
                e.Cancel = true;

                return;
            }

            externalControl?.Shutdown(RFiDGear.Bridge.ShutdownReasons.UserClose);
            Environment.Exit(0);
        }

        /// <summary>
        /// Says whether the application may close now. It may when no run that another program started is in flight; when
        /// one is, the user is asked, and only a yes closes it.
        /// </summary>
        /// <param name="confirm">Asks the user whether to close anyway; called only when a run of the bridge is in flight.</param>
        /// <returns><see langword="true"/> when the application may close.</returns>
        internal bool ShouldClose(Func<bool> confirm) => externalControl == null || !externalControl.RunInFlight() || confirm();

        private bool AskToCloseDuringBridgeRun()
        {
            var caption = ResourceLoader.GetResource("messageBoxCloseDuringBridgeRunCaption");
            var message = ResourceLoader.GetResource("messageBoxCloseDuringBridgeRunMessage");
            var answer = mw != null
                ? MessageBox.Show(mw, message, caption, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No)
                : MessageBox.Show(message, caption, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);

            return answer == MessageBoxResult.Yes;
        }

        /// <summary>
        /// Runs the start-up once, whichever trigger comes first (the window being activated, or the fallback of the external
        /// control bridge), and tells the bridge when it is over, however it ended.
        /// </summary>
        private async void LoadCompleted(object sender, EventArgs e) => await RunStartupOnceAsync(RunStartupAsync);

        /// <summary>
        /// Runs <paramref name="startup"/> unless an earlier trigger already did, and tells the bridge when it is over,
        /// whether it succeeded or threw: a program that waits for RFiDGear must not wait for ever for a start-up that failed.
        /// </summary>
        /// <param name="startup">The start-up.</param>
        /// <returns><see langword="true"/> when this call ran the start-up; <see langword="false"/> when another had.</returns>
        internal async Task<bool> RunStartupOnceAsync(Func<Task> startup)
        {
            if (!startupOnce.TryEnter())
            {
                return false;
            }

            try
            {
                await startup();
            }
            finally
            {
                externalControl?.MarkStartupComplete();
            }

            return true;
        }

        private async Task RunStartupAsync()
        {
            Application.Current.MainWindow.Activated -= new EventHandler(LoadCompleted);

            mw = (MainWindow)Application.Current.MainWindow;
            mw.Title = string.Format("RFiDGear v{0}.{1}.{2}", Version.Major, Version.Minor, Version.Build);

            updateScheduler.Begin(() => AskForUpdateNow());
            // The timer callback is async void; CheckReaderAsync lets nothing escape, so it cannot end the application.
            readerMonitor.StartMonitoring(async _ => await CheckReaderAsync());

            var startupArguments = startupArgumentProcessor.Process(args);
            ApplyStartupArguments(startupArguments);

            var splashScreenType = typeof(SplashScreenViewModel);

            await projectBootstrapper.BootstrapAsync(new ProjectBootstrapRequest
            {
                ProjectFilePath = startupArguments.ProjectFilePath,
                Autorun = startupArguments.Autorun,
                CreateSplashScreen = () => new SplashScreenViewModel(),
                AddDialog = dialog => Dialogs.Add(dialog as IDialogViewModel),
                RemoveSplash = () =>
                {
                    if (Dialogs.FirstOrDefault() != null && Dialogs.FirstOrDefault().GetType() == splashScreenType)
                    {
                        Dialogs.RemoveAt(0);
                    }
                },
                OpenProjectAsync = file => OpenLastProjectFile(file ?? string.Empty),
                ResetTaskStatusAsync = () => TryResetTaskStatusAsync(),
                ReadChipAsync = () => OnNewReadChipCommand(),
                WriteOnceAsync = () => OnNewWriteToChipOnceCommand(),
                UpdateDateTime = value => DateTimeStatusBar = value,
                SetCurrentReader = value => CurrentReader = value,
                SetReaderProvider = value => ReaderDevice.Reader = value,
                SetReaderPort = value => ReaderDevice.PortNumber = value,
                SetCulture = ApplyCulture
            });

            if (startupArguments.AutoMode && !IsWriteToAllChipAutoChecked)
            {
                OnNewWriteToAllChipAutoCommand();
            }
        }

        internal void ApplyStartupArguments(StartupArgumentResult startupArguments)
        {
            if (startupArguments == null)
            {
                return;
            }

            reportOutputPattern = startupArguments.ReportOutputPattern ?? reportOutputPattern;
            reportTemplateFile = startupArguments.ReportTemplateFile ?? reportTemplateFile;

            variablesFromArgs.Clear();

            foreach (var variable in startupArguments.Variables)
            {
                variablesFromArgs[variable.Key] = variable.Value;
            }
        }

        private readonly ReaderConnectionProbe readerProbe = new ReaderConnectionProbe();

        // The probe does nothing while somebody reads the card: the main window (the flag) or a reader scan (the gate).
        internal bool? ReaderBusyForProbe() => _readerGate.IsHeld ? true : IsReaderBusy;

        /// <summary>
        /// One look at the reader, as the reader monitor makes it every few seconds: connects a reader that is not
        /// connected, updates the status bar, and ends <see cref="IsReaderInitializing"/> once the reader has been looked
        /// at. Nothing escapes it.
        /// </summary>
        /// <returns>A task that completes when the look is over.</returns>
        internal async Task CheckReaderAsync()
        {
            readerMonitor?.Pause();

            try
            {
                using (var settings = settingsReaderWriterFactory())
                {
                    var device = ReaderDevice.Instance;
                    var result = await readerProbe.ProbeAsync(
                        device,
                        settings.DefaultSpecification.DefaultReaderProvider,
                        ReaderBusyForProbe());

                    if (result != null)
                    {
                        ApplyProbeResult(result);
                    }

                    // The first look that reached the reader ends the start-up, whatever it found. Without a reader
                    // (none selected) there is nothing to wait for. A look that was skipped because the reader was busy
                    // does not count.
                    if (result != null || device == null)
                    {
                        IsReaderInitializing = false;
                    }
                }
            }
            catch (Exception e)
            {
                // The timer callback is async void: anything that escaped would end the application.
                readerProbe.ReportUnexpectedFailure(e);

                // The look has been made and failed; INIT must not stay on for good.
                IsReaderInitializing = false;
            }
            finally
            {
                readerMonitor?.Resume();
            }
        }

        /// <summary>
        /// Shows what a look at the reader found. A look that ends while somebody else has taken the reader meanwhile (the SAM setup,
        /// a scan, a read of the window) must not say that the reader is free: the busy state is left to the one who holds the gate.
        /// The look itself opens the reader and not the card, so it cannot disturb what a holder of the gate is doing.
        /// </summary>
        /// <param name="result">What the look found.</param>
        internal void ApplyProbeResult(ReaderProbeResult result)
        {
            if (result.ChangesBusy && !_readerGate.IsHeld)
            {
                IsReaderBusy = result.Busy;
            }

            CurrentReader = result.Label;
        }

        #endregion
    }
}
