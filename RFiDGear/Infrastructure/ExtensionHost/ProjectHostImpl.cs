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
        private readonly Func<ChipTaskHandlerModel>? _liveModelAccessor;
        private readonly Action? _notifyCollectionReplaced;
        private readonly Func<bool>? _isExecutionRunning;
        private string? _currentPath;

        /// <summary>
        /// Headless constructor: the live model is always <see cref="DatabaseReaderWriter.SetupModel"/>.
        /// Suitable for HIL tests and non-UI scenarios.
        /// </summary>
        /// <param name="dbRW">Initialized database reader/writer.</param>
        /// <param name="isExecutionRunning">Optional delegate that returns <c>true</c> when a run is in progress.</param>
        public ProjectHostImpl(DatabaseReaderWriter dbRW, Func<bool>? isExecutionRunning = null)
        {
            _dbRW = dbRW ?? throw new ArgumentNullException(nameof(dbRW));
            _isExecutionRunning = isExecutionRunning;
        }

        /// <summary>
        /// Main-app constructor: <paramref name="liveModelAccessor"/> always returns the current
        /// <c>taskHandler</c> field, independent of any <c>DatabaseReaderWriter.SetupModel</c> state.
        /// After a successful <see cref="LoadAsync"/>, the task collection from the newly read
        /// <see cref="DatabaseReaderWriter.SetupModel"/> is copied into the live model so that the
        /// task execution host immediately sees the updated tasks.
        /// </summary>
        /// <param name="dbRW">Used for load and save operations.</param>
        /// <param name="liveModelAccessor">Returns the authoritative live <see cref="ChipTaskHandlerModel"/>.</param>
        /// <param name="notifyCollectionReplaced">Called after task-collection sync so the host VM can propagate the change to the UI.</param>
        /// <param name="isExecutionRunning">Delegate that returns <c>true</c> when a run is in progress; used to reject load-during-run.</param>
        public ProjectHostImpl(
            DatabaseReaderWriter dbRW,
            Func<ChipTaskHandlerModel> liveModelAccessor,
            Action notifyCollectionReplaced,
            Func<bool>? isExecutionRunning = null)
        {
            _dbRW = dbRW ?? throw new ArgumentNullException(nameof(dbRW));
            _liveModelAccessor = liveModelAccessor ?? throw new ArgumentNullException(nameof(liveModelAccessor));
            _notifyCollectionReplaced = notifyCollectionReplaced;
            _isExecutionRunning = isExecutionRunning;
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

            if (_isExecutionRunning?.Invoke() == true)
                return Contracts.ProjectOpenResult.Fail(Contracts.ProjectOpenError.Busy);

            if (!File.Exists(path))
                return Contracts.ProjectOpenResult.Fail(Contracts.ProjectOpenError.PathNotFound);

            ct.ThrowIfCancellationRequested();

            DatabaseReaderWriter.DatabaseReadResult result;
            try
            {
                result = await _dbRW.ReadDatabase(path).ConfigureAwait(false);
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

            if (!result.Success)
            {
                // IsSupportedVersion=false means the manifest version is newer than supported.
                return !result.IsSupportedVersion
                    ? Contracts.ProjectOpenResult.Fail(Contracts.ProjectOpenError.UnsupportedFormat)
                    : Contracts.ProjectOpenResult.Fail(Contracts.ProjectOpenError.Unknown);
            }

            var canonicalPath = Path.GetFullPath(path);
            _currentPath = canonicalPath;

            // In main-app mode, copy the loaded tasks into the live model so that the execution
            // host sees the updated collection. The normal MWViewModel load flow does this copy
            // explicitly; we replicate it here so extension-triggered loads are consistent.
            if (_liveModelAccessor != null)
            {
                var liveModel = _liveModelAccessor();
                if (liveModel != null)
                {
                    var loaded = _dbRW.SetupModel?.TaskCollection;
                    liveModel.TaskCollection = loaded != null
                        ? new ObservableCollection<object>(loaded)
                        : new ObservableCollection<object>();
                }
            }

            _notifyCollectionReplaced?.Invoke();
            ProjectChanged?.Invoke(this, EventArgs.Empty);
            return Contracts.ProjectOpenResult.Ok(canonicalPath, TaskCount);
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
            _dbRW.TreeViewModel = new ObservableCollection<ViewModel.RFiDChipParentLayerViewModel>();
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

            _notifyCollectionReplaced?.Invoke();
            ProjectChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
