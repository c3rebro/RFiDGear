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
        private string? _currentPath;

        public ProjectHostImpl(DatabaseReaderWriter dbRW)
        {
            _dbRW = dbRW ?? throw new ArgumentNullException(nameof(dbRW));
        }

        /// <inheritdoc/>
        public string? CurrentPath => _currentPath;

        /// <inheritdoc/>
        public int TaskCount => _dbRW.SetupModel?.TaskCollection?.Count ?? 0;

        /// <summary>Provides direct access to the underlying model for the task execution host.</summary>
        internal ChipTaskHandlerModel SetupModel => _dbRW.SetupModel;

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
            // This is intentional: extensions and tests call Load() from non-UI threads where
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
            _dbRW.WriteDatabase(_dbRW.SetupModel, _currentPath);
        }

        /// <inheritdoc/>
        public void SaveAs(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path must not be empty.", nameof(path));
            _dbRW.WriteDatabase(_dbRW.SetupModel, path);
            _currentPath = path;
        }

        /// <inheritdoc/>
        public void ReplaceTaskCollection(IEnumerable<object> tasks)
        {
            if (tasks == null)
                throw new ArgumentNullException(nameof(tasks));

            var collection = new ObservableCollection<object>(tasks);
            _dbRW.SetupModel ??= new ChipTaskHandlerModel();
            _dbRW.SetupModel.TaskCollection = collection;
            ProjectChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
