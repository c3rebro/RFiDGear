using RFiDGear.Contracts;
using RFiDGear.Infrastructure.FileAccess;
using RFiDGear.Infrastructure.Tasks.Interfaces;
using RFiDGear.Services.TaskExecution;

using System;
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
            _project = new ProjectHostImpl(dbRW);

            var timerReadChip = new NullDispatcherTimerAdapter();
            var timerTimeout = new NullDispatcherTimerAdapter();
            ITaskExecutionService execService = new TaskExecutionService(
                new ReaderDeviceProvider(),
                timerReadChip,
                timerTimeout);

            _execution = new TaskExecHostImpl(execService, _project);
            _reader = new ReaderHostImpl(readerName);
            _autoMode = new NullAutoModeHost();
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
            _project = new ProjectHostImpl(dbRW ?? throw new ArgumentNullException(nameof(dbRW)));
            _execution = new TaskExecHostImpl(
                execService ?? throw new ArgumentNullException(nameof(execService)),
                _project);
            _reader = new ReaderHostImpl(readerName ?? string.Empty);
            _autoMode = new NullAutoModeHost();
        }

        /// <summary>
        /// Creates a host wired to the main application's live view-model state.
        /// The <paramref name="liveModelAccessor"/> delegate always returns the current
        /// <c>taskHandler</c>, keeping the host in sync even when the task collection is replaced.
        /// </summary>
        /// <param name="dbRW">Used for load/save operations.</param>
        /// <param name="execService">The application's running task execution service.</param>
        /// <param name="liveModelAccessor">Always returns the authoritative <c>ChipTaskHandlerModel</c>.</param>
        /// <param name="notifyCollectionReplaced">Called when <see cref="IProjectHost.ReplaceTaskCollection"/> changes the collection.</param>
        /// <param name="readerName">Display name of the configured reader.</param>
        public HeadlessExtensionHost(
            DatabaseReaderWriter dbRW,
            ITaskExecutionService execService,
            Func<Models.ChipTaskHandlerModel> liveModelAccessor,
            Action notifyCollectionReplaced,
            string readerName)
        {
            _project = new ProjectHostImpl(
                dbRW ?? throw new ArgumentNullException(nameof(dbRW)),
                liveModelAccessor ?? throw new ArgumentNullException(nameof(liveModelAccessor)),
                notifyCollectionReplaced ?? (() => { }));
            _execution = new TaskExecHostImpl(
                execService ?? throw new ArgumentNullException(nameof(execService)),
                _project);
            _reader = new ReaderHostImpl(readerName ?? string.Empty);
            _autoMode = new NullAutoModeHost();
        }

        /// <inheritdoc/>
        public IProjectHost Project => _project;

        /// <inheritdoc/>
        public ITaskExecHost Execution => _execution;

        /// <inheritdoc/>
        public IReaderHost Reader => _reader;

        /// <inheritdoc/>
        public IAutoModeHost AutoMode => _autoMode;

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
