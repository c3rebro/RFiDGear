using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RFiDGear.Contracts
{
    /// <summary>
    /// Provides access to the active task project from an extension or test harness.
    /// <para>
    /// Project operations never overlap: a load, a project edit (<see cref="New"/>, <see cref="ReplaceTaskCollection"/>),
    /// and a task run exclude one another, and a save is excluded by a load or an edit but, unlike them, is allowed
    /// while a task run is in progress. A task-status reset (<see cref="ITaskExecHost.ResetStatus"/>) counts as a
    /// project edit here, for as long as it runs. Each member below says how a rejected call is reported.
    /// </para>
    /// <para>
    /// Sensitive data: a project file holds whatever key values the tasks contain, and
    /// <see cref="Save"/> / <see cref="SaveAs"/> write them to the target file as they are, unencrypted, so choose
    /// the location accordingly. <see cref="ProjectOpenResult"/> carries only a path, a count, and a fixed error code.
    /// The <c>Message</c> of the exceptions these members throw themselves, and of the file and serialization
    /// exceptions they let through, consists of file paths and fixed texts, never key values. An exception raised by
    /// code the host calls (a handler of the live task collection, or the sequence you pass to
    /// <see cref="ReplaceTaskCollection"/>) is passed on unchanged. The <c>InnerException</c> is not filtered
    /// either: when serializing the project fails, <see cref="Save"/> and <see cref="SaveAs"/> throw an
    /// <see cref="InvalidOperationException"/> from the XML serializer whose inner exception is the underlying cause
    /// and may contain project content, so do not show or log it unfiltered.
    /// </para>
    /// </summary>
    public interface IProjectHost
    {
        /// <summary>
        /// Absolute path of the project file that was last loaded or saved (including when the main window did it),
        /// or <c>null</c> for a new project that has not been saved yet. For a save this is the file that was
        /// actually written, so it includes an extension the writer appended.
        /// </summary>
        /// <remarks>
        /// Threading: may be read from any thread without locking. The value is not synchronized with a commit and
        /// can be out of date while a load or edit is being committed on the UI thread.
        /// </remarks>
        string? CurrentPath { get; }

        /// <summary>
        /// Number of tasks in the current project.
        /// </summary>
        /// <remarks>
        /// Threading: may be read from any thread without locking. The value is not synchronized with a commit: while
        /// a load or edit is being committed it can be out of date and can pass through intermediate values (the main
        /// application clears the live collection and adds the tasks one at a time). It also follows tasks added or
        /// removed in the main window at any time.
        /// </remarks>
        int TaskCount { get; }

        /// <summary>
        /// Asynchronously loads a project from <paramref name="path"/> and synchronizes the live application state.
        /// <para>
        /// Threading: safe to await from any thread including the WPF STA thread. The project is committed to the
        /// live application state on the WPF dispatcher thread in the main application. The returned task can
        /// complete on any thread, so do not assume where the code after the <c>await</c> runs unless you capture the
        /// context; marshal to the dispatcher before updating bound UI properties.
        /// </para>
        /// <para>
        /// Busy: if a task execution, another load, a project edit, or a save is in progress, the call returns
        /// immediately with <see cref="ProjectOpenError.Busy"/>. The prior project is never unloaded when the load fails.
        /// </para>
        /// <para>
        /// Formats: <c>.rfPrj</c> archives and <c>.xml</c> task projects are supported. A file that is not a task
        /// project (for example a chip-tree export) returns <see cref="ProjectOpenError.UnsupportedFormat"/>.
        /// </para>
        /// <para>
        /// Cancellation: the token is not observed while the file is being read. It is checked when the call
        /// starts, after the path has been validated (an empty or missing path returns
        /// <see cref="ProjectOpenError.PathNotFound"/> even if the token is already cancelled), and once more when
        /// the read has finished. If it is cancelled at either check the returned task is cancelled, so awaiting it
        /// throws <see cref="OperationCanceledException"/> instead of returning a failed result, and the prior project
        /// is left as it was. After the second check the load can no longer be cancelled and runs to completion, even
        /// if cancellation is requested while the commit is still waiting for the UI thread.
        /// </para>
        /// <para>
        /// Event ordering: <see cref="ProjectChanged"/> is raised during the commit, before the returned task
        /// completes.
        /// </para>
        /// </summary>
        /// <param name="path">Absolute path to the .rfPrj or .xml project file.</param>
        /// <param name="ct">Cancellation token; see the cancellation remarks above for when it is observed.</param>
        /// <returns>Load result containing success state, canonical path, task count, and error classification.</returns>
        Task<ProjectOpenResult> LoadAsync(string path, CancellationToken ct = default);

        /// <summary>
        /// Synchronously loads a project from <paramref name="path"/>.
        /// <para>
        /// Delegates to <see cref="LoadAsync(string, CancellationToken)"/>, so it has the same safety guarantees
        /// (busy rejection, prior project kept on failure). Unlike earlier versions it no longer ignores failures:
        /// for a non-empty path it throws <see cref="InvalidOperationException"/> whenever
        /// <see cref="LoadAsync(string, CancellationToken)"/> would return a failed result (an empty path throws
        /// <see cref="ArgumentException"/>), and <see cref="CurrentPath"/> becomes the canonical (absolute) path.
        /// </para>
        /// <para>
        /// Threading: the call blocks the calling thread until the load has finished, so prefer
        /// <see cref="LoadAsync(string, CancellationToken)"/>. In the main application it refuses to run on the UI
        /// thread: it throws <see cref="InvalidOperationException"/> straight away, before it looks at the file or at
        /// whether the host is busy, so nothing is loaded and the host is left as it was. Blocking the UI thread there
        /// could never work: reading a project parses it on that same thread (the project's view models must be
        /// created there), so the call would never return and the host would stay busy. Called from a thread other
        /// than the UI thread, it blocks until the load has finished and then returns or throws as described above.
        /// That holds only while the UI thread is not itself waiting for this call: in the main application the load
        /// needs the UI thread to parse and commit the project, so if the UI thread is blocked on the calling thread
        /// (for example <c>Task.Run(() =&gt; host.Load(path)).Wait()</c> on the UI thread) the call never returns and
        /// the host stays busy. Code on the UI thread must use
        /// <see cref="LoadAsync(string, CancellationToken)"/>.
        /// </para>
        /// </summary>
        /// <param name="path">Absolute path to the .rfPrj or .xml project file.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is null, empty, or whitespace.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the load fails or is rejected (for example because a run is in progress), and in the main
        /// application when the call is made on the UI thread.
        /// </exception>
        [Obsolete("Load() blocks the calling thread and throws on the main application's UI thread. Use LoadAsync() instead.")]
        void Load(string path);

        /// <summary>
        /// Replaces the project with a new empty one and clears <see cref="CurrentPath"/>. Cards detected by the
        /// reader are not part of a project and are left alone.
        /// <para>
        /// Threading: in the main application this updates bound UI collections and must be called on the UI
        /// thread. <see cref="ProjectChanged"/> is raised before this method returns.
        /// </para>
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when a load, a task run, a save, or another project edit is in progress.</exception>
        void New();

        /// <summary>
        /// Saves the current project to <see cref="CurrentPath"/>.
        /// <para>
        /// Threading: blocks the calling thread while the file is written. In the main application it reads the
        /// live, UI-bound task collection, so call it on the UI thread.
        /// </para>
        /// </summary>
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
        /// <para>
        /// Threading: blocks the calling thread while the file is written. In the main application it reads the
        /// live, UI-bound task collection, so call it on the UI thread.
        /// </para>
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
        /// <exception cref="InvalidOperationException">Thrown when a load, a task run, a save, or another project edit is in progress.</exception>
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
        /// rejected as busy. An exception thrown synchronously by a subscriber is logged (type name, redacted message,
        /// and the start of the redacted stack trace) and is not propagated to the caller or to the other subscribers.
        /// </para>
        /// </summary>
        event EventHandler ProjectChanged;
    }
}
