namespace RFiDGear.Contracts
{
    /// <summary>
    /// Non-sensitive classification of a failed <see cref="IProjectHost.LoadAsync"/> call.
    /// </summary>
    public enum ProjectOpenError
    {
        /// <summary>No error; load was successful.</summary>
        None,

        /// <summary>The specified path does not exist or is not accessible.</summary>
        PathNotFound,

        /// <summary>The file exists but is not a supported project format or version.</summary>
        UnsupportedFormat,

        /// <summary>
        /// The project cannot be replaced right now because a task execution, another load, a project edit
        /// (<c>New</c> / <c>ReplaceTaskCollection</c>), or a save is in progress. The prior project is untouched;
        /// retry once the other operation has finished.
        /// </summary>
        Busy,

        /// <summary>An unexpected error occurred. See the application log for details.</summary>
        Unknown
    }

    /// <summary>
    /// Result of an asynchronous project load operation initiated through <see cref="IProjectHost.LoadAsync"/>.
    /// <para>
    /// Sensitive data: it carries only the canonical path of the file the caller named, the task count, and a
    /// fixed error code. It never contains project content, key values, or exception text.
    /// </para>
    /// </summary>
    public sealed class ProjectOpenResult
    {
        /// <summary><c>true</c> when the project was loaded successfully.</summary>
        public bool Success { get; }

        /// <summary>Canonical (absolute) path of the loaded project, or <c>null</c> on failure.</summary>
        public string? CanonicalPath { get; }

        /// <summary>Number of tasks in the loaded project, or 0 on failure.</summary>
        public int TaskCount { get; }

        /// <summary>Error classification when <see cref="Success"/> is <c>false</c>; otherwise <see cref="ProjectOpenError.None"/>.</summary>
        public ProjectOpenError Error { get; }

        private ProjectOpenResult(bool success, string? canonicalPath, int taskCount, ProjectOpenError error)
        {
            Success = success;
            CanonicalPath = canonicalPath;
            TaskCount = taskCount;
            Error = error;
        }

        /// <summary>Creates a successful result.</summary>
        /// <param name="canonicalPath">Absolute path of the loaded file.</param>
        /// <param name="taskCount">Number of tasks in the loaded project.</param>
        public static ProjectOpenResult Ok(string canonicalPath, int taskCount) =>
            new ProjectOpenResult(true, canonicalPath, taskCount, ProjectOpenError.None);

        /// <summary>Creates a failure result.</summary>
        /// <param name="error">Non-sensitive classification of the failure reason.</param>
        public static ProjectOpenResult Fail(ProjectOpenError error) =>
            new ProjectOpenResult(false, null, 0, error);
    }
}
