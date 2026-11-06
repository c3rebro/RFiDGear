#nullable enable
using RFiDGear.Contracts;
using RFiDGear.Infrastructure.FileAccess;
using RFiDGear.Models;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace RFiDGear.Infrastructure.ExtensionHost
{
    /// <summary>
    /// <see cref="IProjectHost"/> implementation backed by <see cref="DatabaseReaderWriter"/>.
    /// </summary>
    internal sealed class ProjectHostImpl : IProjectHost
    {
        private readonly DatabaseReaderWriter _dbRW;
        private readonly ExtensionOperationCoordinator _coordinator;
        private readonly Func<ChipTaskHandlerModel>? _liveModelAccessor;

        // Dispatches an Action on the thread that owns bound UI collections
        // (WPF dispatcher in main-app mode, inline in headless mode).
        private readonly Func<Action, Task> _dispatch;

        // Called inside the dispatched commit to sync the loaded model into the live app state.
        // Receives the newly deserialized task model.
        // null in headless mode (we just update _dbRW directly).
        private readonly Action<ChipTaskHandlerModel>? _applyToLiveApp;

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
        /// Called inside <paramref name="dispatch"/> after a successful read to apply the loaded
        /// <see cref="ChipTaskHandlerModel"/> to live application state (task collection and property
        /// notifications). It must not touch state that is not part of the project, such as the
        /// chip tree that shows cards detected by the reader.
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

            // Atomically reserve the load slot. Fails immediately if load or execution is in progress.
            if (!_coordinator.TryBeginLoad())
                return Contracts.ProjectOpenResult.Fail(Contracts.ProjectOpenError.Busy);

            try
            {
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
                    if (_applyToLiveApp == null)
                    {
                        // Headless: the database reader's model is the live model.
                        _dbRW.SetupModel = model;
                    }

                    _currentPath = canonicalPath;

                    // Main-app mode: push the task collection into the bound live model.
                    _applyToLiveApp?.Invoke(model);

                    ProjectChanged?.Invoke(this, EventArgs.Empty);
                }).ConfigureAwait(false);

                return Contracts.ProjectOpenResult.Ok(canonicalPath, TaskCount);
            }
            finally
            {
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
        public void New()
        {
            _dbRW.SetupModel = new ChipTaskHandlerModel();
            _currentPath = null;
            ProjectChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <inheritdoc/>
        public void Save()
        {
            if (_currentPath == null)
                throw new InvalidOperationException("No path set. Use SaveAs(path) for unsaved projects.");
            _dbRW.WriteDatabase(SetupModel, _currentPath);
        }

        /// <inheritdoc/>
        public void SaveAs(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path must not be empty.", nameof(path));
            _dbRW.WriteDatabase(SetupModel, path);
            _currentPath = path;
        }

        /// <inheritdoc/>
        public void ReplaceTaskCollection(IEnumerable<object> tasks)
        {
            if (tasks == null)
                throw new ArgumentNullException(nameof(tasks));

            var collection = new ObservableCollection<object>(tasks);
            var model = SetupModel ?? new ChipTaskHandlerModel();
            model.TaskCollection = collection;

            if (_liveModelAccessor == null)
                _dbRW.SetupModel = model;

            ProjectChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
