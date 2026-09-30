using RFiDGear.Contracts;
using RFiDGear.Infrastructure.FileAccess;
using RFiDGear.Models;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace RFiDGear.Infrastructure.ExtensionHost
{
    /// <summary>
    /// <see cref="IProjectHost"/> implementation backed by <see cref="DatabaseReaderWriter"/>.
    /// </summary>
    internal sealed class ProjectHostImpl : IProjectHost
    {
        private readonly DatabaseReaderWriter _dbRW;

        // When set, this overrides dbRW.SetupModel as the live task collection source.
        // Used in the main-app context where ChipTasks (taskHandler) is the authoritative model.
        private readonly Func<ChipTaskHandlerModel>? _liveModelAccessor;

        // Optional notification callback for when ReplaceTaskCollection is called from outside.
        private readonly Action? _notifyCollectionReplaced;

        private string? _currentPath;

        /// <summary>
        /// Headless constructor: the live model is always <see cref="DatabaseReaderWriter.SetupModel"/>.
        /// Suitable for HIL tests and non-UI scenarios.
        /// </summary>
        public ProjectHostImpl(DatabaseReaderWriter dbRW)
        {
            _dbRW = dbRW ?? throw new ArgumentNullException(nameof(dbRW));
        }

        /// <summary>
        /// Main-app constructor: <paramref name="liveModelAccessor"/> always returns the current
        /// <c>taskHandler</c> field, independent of any <c>DatabaseReaderWriter.SetupModel</c> state.
        /// </summary>
        /// <param name="dbRW">Used for load and save operations.</param>
        /// <param name="liveModelAccessor">Returns the authoritative live <see cref="ChipTaskHandlerModel"/>.</param>
        /// <param name="notifyCollectionReplaced">
        /// Called after <see cref="ReplaceTaskCollection"/> so the host VM can propagate the change to the UI.
        /// </param>
        public ProjectHostImpl(
            DatabaseReaderWriter dbRW,
            Func<ChipTaskHandlerModel> liveModelAccessor,
            Action notifyCollectionReplaced)
        {
            _dbRW = dbRW ?? throw new ArgumentNullException(nameof(dbRW));
            _liveModelAccessor = liveModelAccessor ?? throw new ArgumentNullException(nameof(liveModelAccessor));
            _notifyCollectionReplaced = notifyCollectionReplaced;
        }

        /// <inheritdoc/>
        public string? CurrentPath => _currentPath;

        /// <inheritdoc/>
        public int TaskCount => SetupModel?.TaskCollection?.Count ?? 0;

        /// <summary>Returns the live <see cref="ChipTaskHandlerModel"/> used by the task execution host.</summary>
        internal ChipTaskHandlerModel SetupModel =>
            _liveModelAccessor != null ? _liveModelAccessor() : _dbRW.SetupModel;

        /// <inheritdoc/>
        public event EventHandler ProjectChanged;

        /// <inheritdoc/>
        public void New()
        {
            _dbRW.SetupModel = new ChipTaskHandlerModel();
            _dbRW.TreeViewModel = new ObservableCollection<ViewModel.RFiDChipParentLayerViewModel>();
            _currentPath = null;
            ProjectChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <inheritdoc/>
        public void Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path must not be empty.", nameof(path));

            // ReadDatabase is async but the headless host blocks synchronously via GetAwaiter().
            // Intentional: extensions and tests call Load() from non-UI threads where
            // blocking is acceptable and a WPF SynchronizationContext is not present.
            Task.Run(() => _dbRW.ReadDatabase(path)).GetAwaiter().GetResult();
            _currentPath = path;
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

            // In headless mode, also keep dbRW.SetupModel in sync.
            if (_liveModelAccessor == null)
                _dbRW.SetupModel = model;

            _notifyCollectionReplaced?.Invoke();
            ProjectChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
