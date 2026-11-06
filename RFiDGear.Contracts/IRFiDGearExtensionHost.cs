namespace RFiDGear.Contracts
{
    /// <summary>
    /// Top-level host service exported by RFiDGear and importable by extensions via MEF.
    /// Provides stable, supported access to project lifecycle, task execution, reader state,
    /// and automatic mode — without requiring a direct reference to internal view models.
    /// <para>
    /// Obtain this service in an extension by declaring a MEF import:
    /// <code>[Import] public IRFiDGearExtensionHost Host { get; set; }</code>
    /// </para>
    /// <para>
    /// Availability: the host is created while the main window initializes. Until then, and for good if start-up
    /// fails, the MEF export is <c>null</c>, so an extension must not import it before start-up has completed.
    /// </para>
    /// <para>
    /// Threading: once the host exists, each sub-host property below returns the same instance for the lifetime
    /// of the host and may be read from any thread. The host does not marshal events to the UI thread on your
    /// behalf: <c>ExecutionCompleted</c> is raised on the thread that completes the run (typically a thread-pool
    /// thread), <c>IsRunningChanged</c> on the thread that starts or ends a run (the WPF dispatcher thread for a run
    /// started from the main window), and <c>ProjectChanged</c> on the thread that commits the project, which is the
    /// WPF dispatcher thread in the main application. Each member documents whether it must be called on the UI
    /// thread.
    /// </para>
    /// </summary>
    public interface IRFiDGearExtensionHost
    {
        /// <summary>Project load, save, and task-collection management.</summary>
        IProjectHost Project { get; }

        /// <summary>Task execution control and result observation.</summary>
        ITaskExecHost Execution { get; }

        /// <summary>Read-only reader and card state.</summary>
        IReaderHost Reader { get; }

        /// <summary>
        /// Continuous automatic card-processing mode control.
        /// <para>
        /// <strong>Note:</strong> <see cref="IAutoModeHost.Start"/> throws <see cref="System.NotSupportedException"/>
        /// in this release. Full auto-mode wiring is tracked in issue #192.
        /// </para>
        /// </summary>
        IAutoModeHost AutoMode { get; }

        /// <summary>
        /// Read-only application context: RFiDGear version and active UI culture.
        /// Use this to select language-specific default project files and format UI strings.
        /// </summary>
        IAppContext AppContext { get; }
    }
}
