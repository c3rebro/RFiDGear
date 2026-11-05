#nullable enable
using RFiDGear.Contracts;
using RFiDGear.Infrastructure.FileAccess;
using RFiDGear.Infrastructure.Tasks.Interfaces;
using RFiDGear.Services.TaskExecution;
using RFiDGear.ViewModel;

using System;
using System.Collections.ObjectModel;
using System.Text;

namespace RFiDGear.Infrastructure.ExtensionHost
{
    /// <summary>
    /// <see cref="IRFiDGearExtensionHost"/> implementation that runs without a WPF window.
    /// Used directly by the HIL test project and exported via MEF for UI extensions.
    /// </summary>
    public sealed class HeadlessExtensionHost : IRFiDGearExtensionHost, IDisposable
    {
        private readonly ProjectHostImpl _project;
        private readonly TaskExecHostImpl _execution;
        private readonly ReaderHostImpl _reader;
        private readonly NullAutoModeHost _autoMode;
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
                timerTimeout);

            _execution = new TaskExecHostImpl(execService, _project, coordinator);
            _reader = new ReaderHostImpl(readerName);
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
            _reader = new ReaderHostImpl(readerName ?? string.Empty);
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
        /// and tree collection into WPF-bound properties.
        /// </param>
        /// <param name="readerName">Display name of the configured reader.</param>
        /// <param name="getConfiguredLanguage">
        /// Optional delegate returning <c>"german"</c> or <c>"english"</c> from application settings.
        /// When provided, <see cref="IAppContext.UICultureName"/> reflects the user's language choice.
        /// </param>
        public HeadlessExtensionHost(
            DatabaseReaderWriter dbRW,
            ITaskExecutionService execService,
            Func<Models.ChipTaskHandlerModel> liveModelAccessor,
            Func<Action, System.Threading.Tasks.Task> dispatch,
            Action<Models.ChipTaskHandlerModel, ObservableCollection<RFiDChipParentLayerViewModel>> applyToLiveApp,
            string readerName,
            Func<string?>? getConfiguredLanguage = null)
        {
            var coordinator = new ExtensionOperationCoordinator();
            _project = new ProjectHostImpl(
                dbRW ?? throw new ArgumentNullException(nameof(dbRW)),
                coordinator,
                liveModelAccessor ?? throw new ArgumentNullException(nameof(liveModelAccessor)),
                dispatch ?? throw new ArgumentNullException(nameof(dispatch)),
                applyToLiveApp ?? throw new ArgumentNullException(nameof(applyToLiveApp)));
            _execution = new TaskExecHostImpl(
                execService ?? throw new ArgumentNullException(nameof(execService)),
                _project,
                coordinator);
            _reader = new ReaderHostImpl(readerName ?? string.Empty);
            _autoMode = new NullAutoModeHost();
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
        /// Notifies the reader host that connection or card state may have changed.
        /// Call this from the main application's reader polling loop.
        /// </summary>
        public void NotifyReaderStateChanged() => _reader.RaiseStateChanged();

        /// <summary>
        /// Returns a one-line-per-task summary of the last run's error levels and success flags.
        /// Useful for diagnostic messages in test assertions.
        /// </summary>
        public string GetTaskErrorSummary()
        {
            var tasks = _project.SetupModel?.TaskCollection;
            if (tasks == null || tasks.Count == 0)
                return "(no tasks in collection)";

            var sb = new StringBuilder();
            int i = 0;
            foreach (var item in tasks)
            {
                if (item is IGenericTask t)
                    sb.AppendLine($"  task[{i}] index={t.CurrentTaskIndex} error={t.CurrentTaskErrorLevel} success={t.IsTaskCompletedSuccessfully}");
                i++;
            }
            return sb.ToString();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
        }
    }
}
