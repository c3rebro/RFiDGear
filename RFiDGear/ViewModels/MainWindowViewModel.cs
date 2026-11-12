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
using RFiDGear.Services.SecureKeys;
using RFiDGear.Services.TaskExecution;
using RFiDGear.ViewModel.DialogFactories;
using RFiDGear.ViewModel.TaskSetupViewModels;
using RFiDGear.Services.Interfaces;
using RFiDGear.Services.Factories;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.Tasks;
using RFiDGear.Infrastructure.ReaderProviders;
using RFiDGear.Infrastructure.FileAccess;
using RFiDGear.Infrastructure.ExtensionHost;
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
        private ExtensionOperationCoordinator _coordinator;
        private AutoModeController _autoMode;
        private Dispatcher uiDispatcher;
        private readonly ReaderAccessGate _readerGate = new ReaderAccessGate();

        /// <summary>The gate that lets one party at a time read a card: the reads of this window and the scans of extensions.</summary>
        internal ReaderAccessGate ReaderGate => _readerGate;

        /// <summary>The state machine of automatic mode: the menu toggle, the start-up argument and the host all drive it.</summary>
        internal AutoModeController AutoMode => _autoMode;
        private readonly IApplicationExtensionLauncher applicationExtensionLauncher;

        private protected MainWindow mw;
        private protected DatabaseReaderWriter databaseReaderWriter;
        private protected ReportReaderWriter reportReaderWriter;
        private protected DispatcherTimer triggerReadChip;
        private protected DispatcherTimer taskTimeout;
        private protected string reportOutputPath;
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
            IApplicationExtensionLauncher applicationExtensionLauncher = null)
        {
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
                hasTasks: () => (taskHandler?.TaskCollection?.Count ?? 0) > 0);

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
            }
        }

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
        private async Task<AutoModeRunReport> RunCardForAutoModeAsync(bool keepApplicationOpen, Func<bool> isStillWanted)
        {
            foreach (var item in treeViewParentNodes)
            {
                item.IsExpanded = false;
            }

            return await RunAllOnceCoreAsync(keepApplicationOpen, isStillWanted);
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

            using (var device = ReaderDevice.Instance)
            {
                if (device != null)
                {
                    Dialogs.Add(taskDialogFactory.CreateUltralightTaskDialog(SelectedSetupViewModel, ChipTasks, dialogs));
                }
            }

            Mouse.OverrideCursor = null;

            triggerReadChip.IsEnabled = timerState;

            return Task.CompletedTask;
        }

        /// <summary>
        /// 
        /// </summary>
        public IAsyncRelayCommand ExecuteQuickCheckCommand => new AsyncRelayCommand(OnNewExecuteQuickCheckCommand);
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

                    if (((int)treeViewParentNodes.Single(x => x.IsSelected == true).CardType & 0xF000) == (int)CARD_TYPE.MifareClassic)
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
                        switch ((CARD_TYPE)((short)chip.CardType & 0xF000))
                            {
                                case CARD_TYPE.MifareClassic:
                                    treeViewParentNodes.Add(
                                        new RFiDChipParentLayerViewModel(
                                            new MifareClassicChipModel(chip as MifareClassicChipModel), Dialogs, false));
                                    break;

                                case CARD_TYPE.DESFireEV0:
                                case CARD_TYPE.DESFireEV1:
                                case CARD_TYPE.DESFireEV2:
                                case CARD_TYPE.DESFireEV3:
                                    treeViewParentNodes.Add(
                                        new RFiDChipParentLayerViewModel(
                                            new MifareDesfireChipModel(chip as MifareDesfireChipModel), Dialogs, false));
                                    break;

                                case CARD_TYPE.MifarePlus:
                                    treeViewParentNodes.Add(
                                        new RFiDChipParentLayerViewModel(
                                            new MifareClassicChipModel(chip as MifareClassicChipModel), Dialogs, false));
                                    break;

                                case CARD_TYPE.MifareUltralight:
                                    treeViewParentNodes.Add(
                                        new RFiDChipParentLayerViewModel(
                                            new MifareUltralightChipModel(chip), Dialogs, false));
                                    break;

                                default:
                                    if(chip?.CardType != CARD_TYPE.NOTAG)
                                    {
                                        treeViewParentNodes.Add(
                                            new RFiDChipParentLayerViewModel(
                                                new GenericChipModel(chip), Dialogs, false));
                                    }
                                    break;
                            }
                        // fill treeview with dummy models and viewmodels
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
        /// <returns><c>true</c> when the task was removed.</returns>
        internal bool TryDeleteSelectedTask() =>
            taskListEditGate.TryEdit(() =>
            {
                taskHandler.TaskCollection.Remove(SelectedSetupViewModel);
                return true;
            });

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
            try
            {
                foreach (var chipTask in taskHandler.TaskCollection)
                {
                    switch (chipTask)
                    {
                        case CommonTaskViewModel ssVM:
                            ssVM.IsTaskCompletedSuccessfully = null;
                            reportOutputPath = null;
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
        /// <returns>What came of it; <see cref="AutoModeRunReport.NotAdmitted"/> when nothing ran.</returns>
        private async Task<AutoModeRunReport> RunAllOnceCoreAsync(bool keepApplicationOpen, Func<bool> isStillWanted = null)
        {
            if (!await TryBeginExecutionAfterAScanAsync(isStillWanted))
            {
                Mouse.OverrideCursor = null;
                return AutoModeRunReport.NotAdmitted;
            }

            var runId = Guid.NewGuid().ToString("N");
            var startedAt = DateTimeOffset.UtcNow;
            var observations = new List<TaskExecutionObservation>();
            IReadOnlyList<object> snapshot = Array.Empty<object>();
            ExecResult execResult;
            var abandoned = false;

            // The UID of the card the service's own read found; it stays null for a run that never gets that far.
            string? cardUid = null;

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
                    TaskHandler = taskHandler,
                    TreeViewParentNodes = treeViewParentNodes,
                    VariablesFromArgs = variablesFromArgs,
                    Dialogs = Dialogs,
                    ReportReaderWriter = reportReaderWriter,
                    ReportOutputPath = reportOutputPath,
                    ReportTemplateFile = reportTemplateFile,
                    SelectedSetupViewModel = SelectedSetupViewModel,
                    RunSelectedOnly = _runSelectedOnly,
                    UpdateSelectedSetupViewModel = vm => SelectedSetupViewModel = vm,
                    UpdateReaderBusy = status => IsReaderBusy = status,
                    NotifyTreeViewChanged = () => OnPropertyChanged(nameof(TreeViewParentNodes)),
                    NotifyTasksChanged = () => OnPropertyChanged(nameof(ChipTasks)),
                };

                var result = await taskExecutionService.ExecuteOnceAsync(request, CancellationToken.None);

                reportReaderWriter = result.ReportReaderWriter;
                reportOutputPath = result.ReportOutputPath;
                reportTemplateFile = result.ReportTemplateFile;
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
                _coordinator.EndExecution();
                Mouse.OverrideCursor = null;
            }

            return new AutoModeRunReport(admitted: true, execResult, abandoned);
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

                        currentSettings.DefaultReaderProvider = sender.SelectedReader;
                        currentSettings.DefaultReaderName = sender.SelectedReader == ReaderTypes.PCSC
                            ? sender.SelectedReaderName
                            : string.Empty;
                        currentSettings.AutoLoadProjectOnStart = sender.LoadOnStart;
                        currentSettings.LastUsedComPort = sender.ComPort;
                        currentSettings.AutoCheckForUpdates = sender.CheckOnStart;
                        currentSettings.LastUsedBaudRate = sender.SelectedBaudRate;

                        settings.DefaultSpecification = currentSettings;

                        sender.SaveSettings.ExecuteAsync(null);

                        // Switch the provider first: setting the name announces the change to extensions, and they read
                        // the connection and card state of whichever provider is current when that happens.
                        ReaderDevice.Reader = sender.SelectedReader;

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
        /// 
        /// </summary>
        public ICommand ShowHelpCommand => new RelayCommand(OnNewShowHelpCommand);
        private void OnNewShowHelpCommand()
        {
            var q = from p in Process.GetProcesses() where ContainsAny(p.MainWindowTitle, new string[] { "Help", "Hilfe" }) select p;

            using (var settingsReaderWriter = new SettingsReaderWriter())
            {
                if (!q.Any())
                {
                    var pathToHelpFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, settingsReaderWriter.DefaultSpecification.DefaultLanguage == "german" ? "RFiDGear_de.chm" : "RFiDGear_en.chm");
                    if (File.Exists(pathToHelpFile))
                    {
                        var startInfo = new ProcessStartInfo(pathToHelpFile);
                        Process.Start(startInfo);
                    }

                }
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

            if (!updater.UpdateAvailable)
            {
                await Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
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
            if (!userIsNotifiedForAvailableUpdate || updateDisabled)
            {
                await Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
                {
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
            Environment.Exit(0);
        }

        private async void LoadCompleted(object sender, EventArgs e)
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

        private void ApplyStartupArguments(StartupArgumentResult startupArguments)
        {
            if (startupArguments == null)
            {
                return;
            }

            reportOutputPath = startupArguments.ReportOutputPath ?? reportOutputPath;
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
                        if (result.ChangesBusy)
                        {
                            IsReaderBusy = result.Busy;
                        }

                        CurrentReader = result.Label;
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

        private bool ContainsAny(string haystack, params string[] needles)
        {
            return needles.Any(haystack.Contains);
        }

        #endregion
    }
}
