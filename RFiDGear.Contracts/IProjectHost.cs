using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

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

        /// <summary>
        /// Asynchronously loads a project from <paramref name="path"/> and synchronizes the live application state.
        /// <para>
        /// Threading: safe to await from any thread including the WPF STA thread. The result is
        /// returned on a thread-pool thread; marshal to the dispatcher before updating bound UI properties.
        /// </para>
        /// <para>
        /// Busy: if a task execution is in progress, the call returns immediately with
        /// <see cref="ProjectLoadError.Busy"/>. The prior project is never unloaded when the load fails.
        /// </para>
        /// </summary>
        /// <param name="path">Absolute path to the .rfPrj file.</param>
        /// <param name="ct">Cancellation token. Cancellation before the file is read leaves the prior project intact.</param>
        /// <returns>Load result containing success state, canonical path, task count, and error classification.</returns>
        Task<ProjectOpenResult> LoadAsync(string path, CancellationToken ct = default);

        /// <summary>
        /// Synchronously loads a project from <paramref name="path"/>.
        /// <para><strong>Warning:</strong> calling this method from a WPF STA thread can deadlock
        /// if the underlying file reader dispatches back to the UI thread.
        /// Use <see cref="LoadAsync(string, CancellationToken)"/> instead.</para>
        /// </summary>
        /// <param name="path">Absolute path to the .rfPrj file.</param>
        [Obsolete("Calling Load() from the WPF STA thread can deadlock. Use LoadAsync() instead.")]
        void Load(string path);

        /// <summary>Creates a new empty project, discarding any unsaved changes.</summary>
        void New();

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
