using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RFiDGear.Contracts
{
    /// <summary>
    /// Provides access to the active task project from an extension or test harness.
    /// <para>
    /// Project operations never overlap: a load, a task run, and a project edit (<see cref="New"/>,
    /// <see cref="ReplaceTaskCollection"/>) exclude one another, and a load also excludes a save. Each member below
    /// says how a rejected call is reported.
    /// </para>
    /// </summary>
    public interface IProjectHost
    {
        /// <summary>
        /// Absolute path of the project file that was last loaded or saved (including when the main window did it),
        /// or <c>null</c> for a new project that has not been saved yet. For a save this is the file that was
        /// actually written, so it includes an extension the writer appended.
        /// </summary>
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
        /// Busy: if a task execution, another load, a project edit, or a save is in progress, the call returns
        /// immediately with <see cref="ProjectOpenError.Busy"/>. The prior project is never unloaded when the load fails.
        /// </para>
        /// <para>
        /// Formats: <c>.rfPrj</c> archives and <c>.xml</c> task projects are supported. A file that is not a task
        /// project (for example a chip-tree export) returns <see cref="ProjectOpenError.UnsupportedFormat"/>.
        /// </para>
        /// </summary>
        /// <param name="path">Absolute path to the .rfPrj or .xml project file.</param>
        /// <param name="ct">Cancellation token. Cancellation before the file is read leaves the prior project intact.</param>
        /// <returns>Load result containing success state, canonical path, task count, and error classification.</returns>
        Task<ProjectOpenResult> LoadAsync(string path, CancellationToken ct = default);

        /// <summary>
        /// Synchronously loads a project from <paramref name="path"/>.
        /// <para>
        /// Delegates to <see cref="LoadAsync(string, CancellationToken)"/>, so it has the same safety guarantees
        /// (busy rejection, prior project kept on failure). Unlike earlier versions it no longer ignores failures:
        /// it throws <see cref="InvalidOperationException"/> whenever <see cref="LoadAsync(string, CancellationToken)"/>
        /// would return a failed result, and <see cref="CurrentPath"/> becomes the canonical (absolute) path.
        /// </para>
        /// <para><strong>Warning:</strong> calling this method from a WPF STA thread can deadlock
        /// if the underlying file reader dispatches back to the UI thread.
        /// Use <see cref="LoadAsync(string, CancellationToken)"/> instead.</para>
        /// </summary>
        /// <param name="path">Absolute path to the .rfPrj or .xml project file.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is null, empty, or whitespace.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the load fails or is rejected (for example because a run is in progress).</exception>
        [Obsolete("Calling Load() from the WPF STA thread can deadlock. Use LoadAsync() instead.")]
        void Load(string path);

        /// <summary>
        /// Replaces the project with a new empty one and clears <see cref="CurrentPath"/>. Cards detected by the
        /// reader are not part of a project and are left alone.
        /// <para>
        /// Threading: in the main application this updates bound UI collections and must be called on the UI
        /// thread. <see cref="ProjectChanged"/> is raised before this method returns.
        /// </para>
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when a load, a task run, or another project edit is in progress.</exception>
        void New();

        /// <summary>Saves the current project to <see cref="CurrentPath"/>.</summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when <see cref="CurrentPath"/> is <c>null</c> (use <see cref="SaveAs"/>), or when a load, a save,
        /// or a project edit is in progress. Saving while a task run is in progress is allowed.
        /// </exception>
        /// <exception cref="System.IO.IOException">Thrown when the project could not be written.</exception>
        void Save();

        /// <summary>
        /// Saves the current project to <paramref name="path"/> and makes the file that was written the
        /// <see cref="CurrentPath"/>. A <c>.xml</c> path is written as plain XML; any other path is written as a
        /// project archive, with <c>.rfPrj</c> appended when the name has no extension.
        /// </summary>
        /// <param name="path">Output path for the project file.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is null, empty, or whitespace.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when a load, a save, or a project edit is in progress. Saving while a task run is in progress is allowed.
        /// </exception>
        /// <exception cref="System.IO.IOException">Thrown when the project could not be written; <see cref="CurrentPath"/> is then unchanged.</exception>
        void SaveAs(string path);

        /// <summary>
        /// Replaces the entire task collection with <paramref name="tasks"/>.
        /// Task objects must be valid task view-model instances (e.g. <c>MifareDesfireSetupViewModel</c>).
        /// <para>
        /// Threading: in the main application this updates bound UI collections and must be called on the UI
        /// thread. <see cref="ProjectChanged"/> is raised before this method returns.
        /// </para>
        /// </summary>
        /// <param name="tasks">New ordered task collection.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="tasks"/> is <c>null</c>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when a load, a task run, or another project edit is in progress.</exception>
        void ReplaceTaskCollection(IEnumerable<object> tasks);

        /// <summary>
        /// Raised after the project is loaded, replaced, or reset.
        /// <para>
        /// Ordering: the event is raised only after the new project has been committed to the live application
        /// state, so <see cref="TaskCount"/> and <see cref="CurrentPath"/> already reflect it. It is not raised
        /// for a failed, rejected, or cancelled operation.
        /// </para>
        /// <para>
        /// Threading: raised on the thread that commits the project, which is the WPF dispatcher thread in
        /// the main application. Subscribers must not block on a long operation. They run while the operation
        /// still owns the project, so calling a project-changing member or starting a task run from a handler is
        /// rejected as busy. An exception thrown by a subscriber is logged and is not propagated to the caller
        /// or to the other subscribers.
        /// </para>
        /// </summary>
        event EventHandler ProjectChanged;
    }
}
