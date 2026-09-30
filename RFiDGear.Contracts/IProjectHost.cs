using System;
using System.Collections.Generic;

namespace RFiDGear.Contracts
{
    /// <summary>
    /// Provides access to the active task project from an extension or test harness.
    /// </summary>
    public interface IProjectHost
    {
        /// <summary>Path of the currently loaded .rfPrj file, or <c>null</c> if unsaved.</summary>
        string? CurrentPath { get; }

        /// <summary>Number of tasks in the current project.</summary>
        int TaskCount { get; }

        /// <summary>Creates a new empty project, discarding any unsaved changes.</summary>
        void New();

        /// <summary>Loads a project from <paramref name="path"/>.</summary>
        /// <param name="path">Absolute path to the .rfPrj file.</param>
        void Load(string path);

        /// <summary>Saves the current project to <see cref="CurrentPath"/>.</summary>
        void Save();

        /// <summary>Saves the current project to <paramref name="path"/>.</summary>
        /// <param name="path">Absolute path for the output .rfPrj file.</param>
        void SaveAs(string path);

        /// <summary>
        /// Replaces the entire task collection with <paramref name="tasks"/>.
        /// Task objects must be valid task view-model instances (e.g. <c>MifareDesfireSetupViewModel</c>).
        /// </summary>
        /// <param name="tasks">New ordered task collection.</param>
        void ReplaceTaskCollection(IEnumerable<object> tasks);

        /// <summary>Raised after the project is loaded, replaced, or reset.</summary>
        event EventHandler ProjectChanged;
    }
}
