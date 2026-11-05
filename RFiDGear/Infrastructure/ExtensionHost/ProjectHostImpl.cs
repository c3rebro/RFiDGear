#nullable enable
using RFiDGear.Contracts;
using RFiDGear.Infrastructure.FileAccess;
using RFiDGear.Models;
using RFiDGear.ViewModel;

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
        // Receives the newly deserialized task model and tree collection.
        // null in headless mode (we just update _dbRW directly).
        private readonly Action<ChipTaskHandlerModel, ObservableCollection<RFiDChipParentLayerViewModel>>? _applyToLiveApp;

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
        /// <see cref="ChipTaskHandlerModel"/> and tree collection to live application state
        /// (task collection, <c>TreeViewParentNodes</c>, property notifications, etc.).
        /// </param>
        public ProjectHostImpl(
            DatabaseReaderWriter dbRW,
            ExtensionOperationCoordinator coordinator,
            Func<ChipTaskHandlerModel> liveModelAccessor,
            Func<Action, Task> dispatch,
            Action<ChipTaskHandlerModel, ObservableCollection<RFiDChipParentLayerViewModel>> applyToLiveApp)
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
                // Read into a temporary DatabaseReaderWriter so that a parsing failure leaves
                // the current project (SetupModel, TreeViewModel, _currentPath) fully intact.
                var tempDbRW = new DatabaseReaderWriter();
                DatabaseReaderWriter.DatabaseReadResult result;

                try
                {
                    result = await tempDbRW.ReadDatabase(path).ConfigureAwait(false);
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

                // Check version before success: a newer-manifest project may parse without error
                // but still be incompatible. These are distinct conditions.
                if (!result.IsSupportedVersion)
                    return Contracts.ProjectOpenResult.Fail(Contracts.ProjectOpenError.UnsupportedFormat);

                if (!result.Success)
                    return Contracts.ProjectOpenResult.Fail(Contracts.ProjectOpenError.Unknown);

                var canonicalPath = Path.GetFullPath(path);

                // Commit atomically on the correct thread. In main-app mode this is the WPF
                // dispatcher, keeping ObservableCollection mutations on the UI thread.
                await _dispatch(() =>
                {
                    // Keep the backing store in sync regardless of mode.
                    _dbRW.SetupModel = tempDbRW.SetupModel;
                    _dbRW.TreeViewModel = tempDbRW.TreeViewModel;
                    _currentPath = canonicalPath;

                    // In main-app mode, push task collection and tree into bound properties.
                    _applyToLiveApp?.Invoke(tempDbRW.SetupModel, tempDbRW.TreeViewModel);

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

            // Blocks synchronously. On a WPF STA thread this can deadlock if ReadDatabase
            // dispatches back to the UI thread. Use LoadAsync() from WPF code instead.
            Task.Run(() => _dbRW.ReadDatabase(path)).GetAwaiter().GetResult();
            _currentPath = Path.GetFullPath(path);
            ProjectChanged?.Invoke(this, EventArgs.Empty);
        }
#pragma warning restore CS0618

        /// <inheritdoc/>
        public void New()
        {
            _dbRW.SetupModel = new ChipTaskHandlerModel();
            _dbRW.TreeViewModel = new ObservableCollection<RFiDChipParentLayerViewModel>();
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
