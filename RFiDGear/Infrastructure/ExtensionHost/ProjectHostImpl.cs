#nullable enable
using RFiDGear.Contracts;
using RFiDGear.Infrastructure.FileAccess;
using RFiDGear.Models;

using Serilog;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RFiDGear.Infrastructure.ExtensionHost
{
    /// <summary>
    /// <see cref="IProjectHost"/> implementation backed by <see cref="DatabaseReaderWriter"/>.
    /// </summary>
    internal sealed class ProjectHostImpl : IProjectHost
    {
        private static readonly ILogger Logger = Log.ForContext<ProjectHostImpl>();

        private readonly DatabaseReaderWriter _dbRW;
        private readonly ExtensionOperationCoordinator _coordinator;
        private readonly Func<ChipTaskHandlerModel>? _liveModelAccessor;

        // Dispatches an Action on the thread that owns bound UI collections
        // (WPF dispatcher in main-app mode, inline in headless mode).
        private readonly Func<Action, Task> _dispatch;

        // Called to apply a project model to the live app state (task collection and property
        // notifications). null in headless mode (we just update _dbRW directly).
        private readonly Action<ChipTaskHandlerModel>? _applyToLiveApp;

        // Load and save both stage files in the shared application data folder, so they must never
        // overlap. Unlike the coordinator this is not blocked by a task run: saving while a run is
        // in progress has always been allowed. 0 = free, 1 = a load or save owns the staging files.
        private int _fileOperationActive;

        private string? _currentPath;

        /// <summary>
        /// Headless constructor: the live model is always <see cref="DatabaseReaderWriter.SetupModel"/>.
        /// Suitable for HIL tests and non-UI scenarios.
        /// </summary>
        /// <param name="dbRW">Initialized database reader/writer.</param>
        /// <param name="coordinator">Shared admission gate; prevents load/execute races.</param>
        public ProjectHostImpl(DatabaseReaderWriter dbRW, ExtensionOperationCoordinator coordinator)
        {
            _dbRW = dbRW ?? throw new ArgumentNullException(nameof(dbRW));
            _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            _dispatch = action => { action(); return Task.CompletedTask; };
        }

        /// <summary>
        /// Main-app constructor: the live model is the <paramref name="liveModelAccessor"/> delegate's result.
        /// Loaded projects are committed through <paramref name="dispatch"/> so WPF-bound collections are
        /// updated on the UI thread.
        /// </summary>
        /// <param name="dbRW">Used for load and save operations.</param>
        /// <param name="coordinator">Shared admission gate; prevents load/execute races.</param>
        /// <param name="liveModelAccessor">Always returns the authoritative live <see cref="ChipTaskHandlerModel"/>.</param>
        /// <param name="dispatch">
        /// Invokes the supplied <see cref="Action"/> on the WPF dispatcher thread and returns a <see cref="Task"/>
        /// that completes when the action has finished. In the main application, pass
        /// <c>action => Application.Current.Dispatcher.InvokeAsync(action).Task</c>.
        /// </param>
        /// <param name="applyToLiveApp">
        /// Applies a project model to live application state (task collection and property
        /// notifications). <see cref="LoadAsync"/> calls it inside <paramref name="dispatch"/>; <see cref="New"/>
        /// and <see cref="ReplaceTaskCollection"/> call it inline on the caller's thread, which must therefore be
        /// the UI thread. It must not touch state that is not part of the project, such as the chip tree that
        /// shows cards detected by the reader.
        /// </param>
        public ProjectHostImpl(
            DatabaseReaderWriter dbRW,
            ExtensionOperationCoordinator coordinator,
            Func<ChipTaskHandlerModel> liveModelAccessor,
            Func<Action, Task> dispatch,
            Action<ChipTaskHandlerModel> applyToLiveApp)
        {
            _dbRW = dbRW ?? throw new ArgumentNullException(nameof(dbRW));
            _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            _liveModelAccessor = liveModelAccessor ?? throw new ArgumentNullException(nameof(liveModelAccessor));
            _dispatch = dispatch ?? throw new ArgumentNullException(nameof(dispatch));
            _applyToLiveApp = applyToLiveApp ?? throw new ArgumentNullException(nameof(applyToLiveApp));
        }

        /// <inheritdoc/>
        public string? CurrentPath => _currentPath;

        /// <inheritdoc/>
        public int TaskCount => SetupModel?.TaskCollection?.Count ?? 0;

        /// <summary>Returns the live <see cref="ChipTaskHandlerModel"/> used by the task execution host.</summary>
        internal ChipTaskHandlerModel SetupModel =>
            _liveModelAccessor != null ? _liveModelAccessor() : _dbRW.SetupModel;

        /// <inheritdoc/>
        public event EventHandler? ProjectChanged;

        /// <inheritdoc/>
        public async Task<Contracts.ProjectOpenResult> LoadAsync(string path, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(path))
                return Contracts.ProjectOpenResult.Fail(Contracts.ProjectOpenError.PathNotFound);

            if (!File.Exists(path))
                return Contracts.ProjectOpenResult.Fail(Contracts.ProjectOpenError.PathNotFound);

            ct.ThrowIfCancellationRequested();

            // Atomically reserve the load slot. Fails immediately if load, execution, or edit is in progress.
            if (!_coordinator.TryBeginLoad())
                return Contracts.ProjectOpenResult.Fail(Contracts.ProjectOpenError.Busy);

            var fileGateHeld = false;
            try
            {
                // A save in progress owns the shared staging files; wait for the caller to retry.
                fileGateHeld = TryEnterFileGate();
                if (!fileGateHeld)
                    return Contracts.ProjectOpenResult.Fail(Contracts.ProjectOpenError.Busy);

                // ReadProjectAsync returns the model without touching _dbRW, so a parsing failure
                // leaves the current project (SetupModel, _currentPath) fully intact, and no
                // throwaway reader runs its destructive constructor on every load.
                DatabaseReaderWriter.DatabaseReadResult result;
                ChipTaskHandlerModel? model;

                try
                {
                    (result, model) = await _dbRW.ReadProjectAsync(path).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (NotSupportedException)
                {
                    return Contracts.ProjectOpenResult.Fail(Contracts.ProjectOpenError.UnsupportedFormat);
                }
                catch
                {
                    return Contracts.ProjectOpenResult.Fail(Contracts.ProjectOpenError.Unknown);
                }

                ct.ThrowIfCancellationRequested();

                // Check success first: Failed() now carries an explicit FailureReason so we can
                // distinguish I/O and deserialization failures from version-mismatch cases.
                // A version-too-new result has Success=true and IsSupportedVersion=false.
                if (!result.Success)
                {
                    var openError = result.FailureReason switch
                    {
                        DatabaseReaderWriter.DatabaseReadFailureReason.UnsupportedVersion =>
                            Contracts.ProjectOpenError.UnsupportedFormat,
                        DatabaseReaderWriter.DatabaseReadFailureReason.UnsupportedFormat =>
                            Contracts.ProjectOpenError.UnsupportedFormat,
                        DatabaseReaderWriter.DatabaseReadFailureReason.DeserializationFailed =>
                            Contracts.ProjectOpenError.UnsupportedFormat,
                        _ => Contracts.ProjectOpenError.Unknown,
                    };
                    return Contracts.ProjectOpenResult.Fail(openError);
                }

                if (!result.IsSupportedVersion || model == null)
                    return Contracts.ProjectOpenResult.Fail(Contracts.ProjectOpenError.UnsupportedFormat);

                var canonicalPath = Path.GetFullPath(path);

                // Commit atomically on the correct thread. In main-app mode this is the WPF
                // dispatcher, keeping ObservableCollection mutations on the UI thread.
                await _dispatch(() =>
                {
                    ApplyModel(model);
                    _currentPath = canonicalPath;
                    RaiseProjectChanged();
                }).ConfigureAwait(false);

                return Contracts.ProjectOpenResult.Ok(canonicalPath, TaskCount);
            }
            finally
            {
                if (fileGateHeld)
                    ExitFileGate();

                _coordinator.EndLoad();
            }
        }

        /// <inheritdoc/>
#pragma warning disable CS0618
        public void Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path must not be empty.", nameof(path));

            // Delegate to LoadAsync so all safety checks (coordinator, detached read,
            // dispatcher commit) apply. Blocks synchronously — avoid calling from a WPF
            // STA thread that owns the dispatcher (deadlock risk); use LoadAsync() instead.
            var result = Task.Run(() => LoadAsync(path)).GetAwaiter().GetResult();
            if (!result.Success)
                throw new InvalidOperationException(
                    $"Failed to load project '{path}': {result.Error}");
        }
#pragma warning restore CS0618

        /// <inheritdoc/>
        public void New() =>
            RunProjectOperation(() =>
            {
                ApplyModel(new ChipTaskHandlerModel());
                _currentPath = null;
            });

        /// <inheritdoc/>
        public void Save()
        {
            var path = _currentPath
                ?? throw new InvalidOperationException("No path set. Use SaveAs(path) for unsaved projects.");
            WriteProject(path);
        }

        /// <inheritdoc/>
        public void SaveAs(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path must not be empty.", nameof(path));

            WriteProject(path);
        }

        /// <inheritdoc/>
        public void ReplaceTaskCollection(IEnumerable<object> tasks)
        {
            if (tasks == null)
                throw new ArgumentNullException(nameof(tasks));

            var collection = new ObservableCollection<object>(tasks);

            RunProjectOperation(() =>
            {
                if (_applyToLiveApp != null)
                {
                    // Same instance-preserving apply as a load, so bound views are notified identically.
                    _applyToLiveApp(new ChipTaskHandlerModel { TaskCollection = collection });
                    return;
                }

                // Headless: keep the model instance and swap only its task collection.
                var model = _dbRW.SetupModel ?? new ChipTaskHandlerModel();
                model.TaskCollection = collection;
                _dbRW.SetupModel = model;
            });
        }

        // ── private helpers ──────────────────────────────────────────────────

        /// <summary>
        /// Makes <paramref name="model"/> the live project: headless hosts adopt it as their model,
        /// the main application copies it into its bound live model.
        /// </summary>
        private void ApplyModel(ChipTaskHandlerModel model)
        {
            if (_applyToLiveApp != null)
            {
                _applyToLiveApp(model);
            }
            else
            {
                _dbRW.SetupModel = model;
            }
        }

        /// <summary>
        /// Runs a project edit under the coordinator so it can never overlap a load or a task run, then
        /// raises <see cref="ProjectChanged"/> while the slot is still held (as a load does).
        /// </summary>
        private void RunProjectOperation(Action edit)
        {
            if (!_coordinator.TryBeginProjectOperation())
                throw new InvalidOperationException(
                    _coordinator.IsLoading
                        ? "A project load is in progress. Wait for LoadAsync to complete."
                        : _coordinator.IsExecuting
                            ? "A task run is in progress. Wait for it to complete."
                            : "Another project edit is in progress.");

            try
            {
                edit();
                RaiseProjectChanged();
            }
            finally
            {
                _coordinator.EndProjectOperation();
            }
        }

        /// <summary>
        /// Writes the live project and records the file that was actually written (canonical, with any
        /// extension the writer appended) as <see cref="CurrentPath"/>. Allowed while a task run is in
        /// progress; refused while a load or a project edit is.
        /// </summary>
        private void WriteProject(string path)
        {
            if (_coordinator.IsMutating)
                throw new InvalidOperationException("A project edit is in progress. Wait for it to complete.");

            if (!TryEnterFileGate())
                throw new InvalidOperationException("A project load or save is in progress. Wait for it to complete.");

            try
            {
                var written = _dbRW.WriteDatabase(SetupModel, path)
                    ?? throw new IOException($"The project could not be written to '{path}'.");

                _currentPath = Path.GetFullPath(written);
            }
            finally
            {
                ExitFileGate();
            }
        }

        private bool TryEnterFileGate() =>
            Interlocked.CompareExchange(ref _fileOperationActive, 1, 0) == 0;

        private void ExitFileGate() =>
            Volatile.Write(ref _fileOperationActive, 0);

        /// <summary>
        /// Raises <see cref="ProjectChanged"/> to each subscriber in isolation: by then the project has already
        /// been replaced, so a throwing subscriber is logged and must not turn a completed load or edit into
        /// an exception, nor starve the subscribers after it.
        /// </summary>
        private void RaiseProjectChanged()
        {
            var handlers = ProjectChanged;
            if (handlers == null)
                return;

            foreach (var handler in handlers.GetInvocationList().Cast<EventHandler>())
            {
                try
                {
                    handler(this, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "ProjectChanged subscriber threw; the exception was not propagated");
                }
            }
        }
    }
}
