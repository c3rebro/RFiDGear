#nullable enable
using RFiDGear.Contracts;
using RFiDGear.Infrastructure.FileAccess;
using RFiDGear.Infrastructure.ReaderProviders;
using RFiDGear.Infrastructure.Tasks.Interfaces;
using RFiDGear.Services.SecureKeys;
using RFiDGear.Services.TaskExecution;

using System;
using System.Text;

namespace RFiDGear.Infrastructure.ExtensionHost
{
    /// <summary>
    /// <see cref="IRFiDGearExtensionHost"/> implementation that runs without a WPF window.
    /// Used directly by the HIL test project; the main application creates one and passes it to an application
    /// extension when the user opens it (<c>IApplicationUIExtension.CreateContent</c>).
    /// </summary>
    public sealed class HeadlessExtensionHost : IRFiDGearExtensionHost, IDisposable
    {
        private readonly ProjectHostImpl _project;
        private readonly TaskExecHostImpl _execution;
        private readonly ReaderHostImpl _reader;
        private readonly IAutoModeHost _autoMode;
        private readonly AppContextImpl _appContext;
        private bool _disposed;

        /// <summary>
        /// Creates a headless host wired to a fresh <see cref="DatabaseReaderWriter"/> and a new
        /// <see cref="TaskExecutionService"/> backed by no-op timers. Suitable for tests and
        /// non-UI automation scenarios where no WPF dispatcher is present.
        /// </summary>
        /// <param name="readerName">Display name of the configured reader (informational only).</param>
        public HeadlessExtensionHost(string readerName = "")
        {
            var dbRW = new DatabaseReaderWriter();
            var coordinator = new ExtensionOperationCoordinator();

            _project = new ProjectHostImpl(dbRW, coordinator);

            var timerReadChip = new NullDispatcherTimerAdapter();
            var timerTimeout = new NullDispatcherTimerAdapter();
            ITaskExecutionService execService = new TaskExecutionService(
                new ReaderDeviceProvider(),
                timerReadChip,
                timerTimeout,
                null,
                SecureKeyRegistry.FromApplicationExtensions());

            _execution = new TaskExecHostImpl(execService, _project, coordinator);
            _reader = new ReaderHostImpl(readerName, coordinator);
            _autoMode = new NullAutoModeHost();
            _appContext = new AppContextImpl();
        }

        /// <summary>
        /// Creates a headless host using caller-supplied services. Suitable for non-UI automation
        /// where the live task model IS the database reader/writer's setup model.
        /// </summary>
        /// <param name="dbRW">Initialized database reader/writer.</param>
        /// <param name="execService">Running task execution service.</param>
        /// <param name="readerName">Display name of the configured reader.</param>
        public HeadlessExtensionHost(
            DatabaseReaderWriter dbRW,
            ITaskExecutionService execService,
            string readerName)
        {
            var coordinator = new ExtensionOperationCoordinator();
            _project = new ProjectHostImpl(
                dbRW ?? throw new ArgumentNullException(nameof(dbRW)),
                coordinator);
            _execution = new TaskExecHostImpl(
                execService ?? throw new ArgumentNullException(nameof(execService)),
                _project,
                coordinator);
            _reader = new ReaderHostImpl(readerName ?? string.Empty, coordinator);
            _autoMode = new NullAutoModeHost();
            _appContext = new AppContextImpl();
        }

        /// <summary>
        /// Creates a host wired to the main application's live view-model state.
        /// </summary>
        /// <param name="dbRW">Used for load/save operations.</param>
        /// <param name="execService">The application's running task execution service.</param>
        /// <param name="liveModelAccessor">Always returns the authoritative live <c>ChipTaskHandlerModel</c>.</param>
        /// <param name="dispatch">
        /// Invokes an <see cref="Action"/> on the WPF dispatcher thread and returns a <see cref="System.Threading.Tasks.Task"/>
        /// that completes when the action has run.
        /// Typically: <c>action => Application.Current.Dispatcher.InvokeAsync(action).Task</c>.
        /// </param>
        /// <param name="applyToLiveApp">
        /// Called on the dispatcher thread after a successful load to push the loaded task model
        /// into WPF-bound properties. It must leave state that is not part of the project untouched,
        /// such as the chip tree that shows cards detected by the reader.
        /// </param>
        /// <param name="readerName">Display name of the configured reader.</param>
        /// <param name="coordinator">
        /// Application-scoped admission gate shared with the host UI's own load and execute operations.
        /// Passing a coordinator owned by <c>MainWindowViewModel</c> ensures that extension operations
        /// and direct UI calls cannot interleave.
        /// </param>
        /// <param name="getConfiguredLanguage">
        /// Optional delegate returning <c>"german"</c> or <c>"english"</c> from application settings.
        /// When provided, <see cref="IAppContext.UICultureName"/> reflects the user's language choice.
        /// The delegate is called once, on the constructing thread, while the host is being created, to record the
        /// language that <see cref="IAppContext.CultureChanged"/> is measured against; it must be safe to call then.
        /// </param>
        /// <param name="isOnUiThread">
        /// Optional delegate returning <c>true</c> when the calling thread is the UI thread. When provided, the obsolete
        /// synchronous <c>Project.Load</c> throws on that thread instead of deadlocking.
        /// </param>
        /// <param name="getReaderName">
        /// Optional delegate returning the display name of the reader the user has selected. When provided it is read
        /// each time <see cref="IReaderHost.ReaderName"/> is, and <paramref name="readerName"/> is ignored, so the name
        /// follows the reader the user selects while the application runs. Pair it with
        /// <see cref="NotifyReaderStateChanged"/> to tell extensions when it changes.
        /// </param>
        /// <param name="readerGate">
        /// Optional gate that lets one party at a time read a card. A reader scan of an extension takes it, and the
        /// application's own reads should take it too; a scan answers busy while it is held. When omitted a scan only
        /// excludes loads, runs and project edits.
        /// </param>
        /// <param name="autoMode">
        /// Optional host of automatic mode, backed by the application's state machine for continuous card processing.
        /// When omitted, <see cref="IRFiDGearExtensionHost.AutoMode"/> is the stand-in of a host without a reader loop, on
        /// which <see cref="IAutoModeHost.Start"/> throws <see cref="NotSupportedException"/>. The application's own host
        /// is also told about the runs started through <see cref="IRFiDGearExtensionHost.Execution"/>: such a run counts
        /// as processing the card on the reader, and one that ends in a fault, a timeout or a cancellation stops a mode
        /// that is on.
        /// </param>
        public HeadlessExtensionHost(
            DatabaseReaderWriter dbRW,
            ITaskExecutionService execService,
            Func<Models.ChipTaskHandlerModel> liveModelAccessor,
            Func<Action, System.Threading.Tasks.Task> dispatch,
            Action<Models.ChipTaskHandlerModel> applyToLiveApp,
            string readerName,
            ExtensionOperationCoordinator coordinator,
            Func<string?>? getConfiguredLanguage = null,
            Func<bool>? isOnUiThread = null,
            Func<string?>? getReaderName = null,
            ReaderAccessGate? readerGate = null,
            IAutoModeHost? autoMode = null)
        {
            _project = new ProjectHostImpl(
                dbRW ?? throw new ArgumentNullException(nameof(dbRW)),
                coordinator ?? throw new ArgumentNullException(nameof(coordinator)),
                liveModelAccessor ?? throw new ArgumentNullException(nameof(liveModelAccessor)),
                dispatch ?? throw new ArgumentNullException(nameof(dispatch)),
                applyToLiveApp ?? throw new ArgumentNullException(nameof(applyToLiveApp)),
                isOnUiThread);
            _execution = new TaskExecHostImpl(
                execService ?? throw new ArgumentNullException(nameof(execService)),
                _project,
                coordinator,
                // The application's automatic mode also hears of the runs an extension starts here: they work on the card
                // on the reader too.
                autoMode as IRunObserver);
            _reader = getReaderName != null
                ? new ReaderHostImpl(getReaderName, coordinator, readerGate)
                : new ReaderHostImpl(readerName ?? string.Empty, coordinator, readerGate);
            _autoMode = autoMode ?? new NullAutoModeHost();
            _appContext = getConfiguredLanguage != null
                ? new AppContextImpl(getConfiguredLanguage)
                : new AppContextImpl();
        }

        /// <inheritdoc/>
        public IProjectHost Project => _project;

        /// <inheritdoc/>
        public ITaskExecHost Execution => _execution;

        /// <inheritdoc/>
        public IReaderHost Reader => _reader;

        /// <inheritdoc/>
        public IAutoModeHost AutoMode => _autoMode;

        /// <inheritdoc/>
        public IAppContext AppContext => _appContext;

        /// <summary>
        /// Tells the reader host that the reader the user selected has changed (its display name is a different one).
        /// What the old reader showed says nothing about the new one, so the card picture the host kept is forgotten
        /// and <see cref="IReaderHost.ReaderStateChanged"/> is raised once. Call it from the place that changes the
        /// name, and only then.
        /// </summary>
        public void NotifyReaderStateChanged() => _reader.NotifyReaderNameChanged();

        /// <summary>The number of reader changes so far; take it before a read and hand it to <see cref="PublishRead"/>.</summary>
        internal int ReaderGeneration => _reader.Generation;

        /// <summary>Says what a read that the main window made means; call it while the reader gate is still held.</summary>
        /// <param name="device">The device the read was made with.</param>
        /// <param name="result">What the read returned.</param>
        /// <returns>The snapshot, or <c>null</c> if the device could not be asked.</returns>
        internal ReaderSnapshot? InterpretRead(ReaderDevice? device, ERROR result) => _reader.InterpretRead(device, result);

        /// <summary>
        /// Stores the picture of a read that the main window made; call it while the reader gate is still held, then
        /// announce the change with <see cref="AnnounceReaderChange"/> when this returns <c>true</c>.
        /// </summary>
        /// <param name="snapshot">What <see cref="InterpretRead"/> returned.</param>
        /// <param name="generation">The value of <see cref="ReaderGeneration"/> from before the read.</param>
        /// <returns><c>true</c> if the picture is a change to announce.</returns>
        internal bool StoreRead(ReaderSnapshot snapshot, int generation) => _reader.StoreRead(snapshot, generation);

        /// <summary>
        /// Raises <see cref="IReaderHost.ReaderStateChanged"/> for a change that <see cref="StoreRead"/> reported, unless the
        /// reader has changed since the read began: the change of the reader announced itself.
        /// </summary>
        /// <param name="changed">What <see cref="StoreRead"/> returned.</param>
        /// <param name="generation">The value of <see cref="ReaderGeneration"/> from before the read.</param>
        internal void AnnounceReaderChange(bool changed, int generation) => _reader.AnnounceIfCurrent(changed, generation);

        /// <summary>
        /// Tells the host that the application has just changed the language it works with. If
        /// <see cref="IAppContext.UICultureName"/> now reports a different language than before,
        /// <see cref="IAppContext.CultureChanged"/> is raised once, synchronously on the calling thread; otherwise
        /// nothing happens. Call this on the UI thread right after assigning the language.
        /// </summary>
        internal void NotifyCultureMayHaveChanged() => _appContext.NotifyCultureMayHaveChanged();

        /// <summary>
        /// Returns a one-line-per-task summary of the last run's error levels and success flags.
        /// Useful for diagnostic messages in test assertions.
        /// </summary>
        public string GetTaskErrorSummary()
        {
            var tasks = _project.SetupModel?.TaskCollection;
            if (tasks == null || tasks.Count == 0)
            {
                return "(no tasks in collection)";
            }

            var sb = new StringBuilder();
            int i = 0;
            foreach (var item in tasks)
            {
                if (item is IGenericTask t)
                {
                    sb.AppendLine($"  task[{i}] index={t.CurrentTaskIndex} error={t.CurrentTaskErrorLevel} success={t.IsTaskCompletedSuccessfully}");
                }
                i++;
            }
            return sb.ToString();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
        }
    }
}
