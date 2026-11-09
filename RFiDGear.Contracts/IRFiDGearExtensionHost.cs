namespace RFiDGear.Contracts
{
    /// <summary>
    /// Top-level host service that RFiDGear offers to extensions.
    /// Provides stable, supported access to project lifecycle, task execution, reader state,
    /// and automatic mode — without requiring a direct reference to internal view models.
    /// <para>
    /// Obtain this service in an application extension from the argument of <c>CreateContent</c>, the method of
    /// <c>IApplicationUIExtension</c> (in the <c>RFiDGear.ExtensionSdk</c> assembly) that RFiDGear calls each time the
    /// user opens the extension.
    /// </para>
    /// <para>
    /// Availability: the service is created while the main window initializes, which can be after MEF has composed
    /// the extensions, and an extension can only be opened after that. <c>CreateContent</c> therefore always receives
    /// an initialized service, never <c>null</c>, and the same instance for the whole session. Do not declare a MEF
    /// <c>[Import]</c> for this interface: it is not exported, and MEF leaves out, without an error, an extension
    /// with an import it cannot satisfy.
    /// </para>
    /// <para>
    /// Threading: once the host exists, each sub-host property below returns the same instance for the lifetime
    /// of the host and may be read from any thread. The host does not marshal events to the UI thread on your
    /// behalf: <c>ExecutionCompleted</c> is raised on the thread that completes the run (typically a thread-pool
    /// thread), <c>IsRunningChanged</c> on the thread that starts or ends a run (the WPF dispatcher thread for a run
    /// started from the main window), <c>ReaderStateChanged</c> on the thread that made the change (the thread that changes
    /// the reader name, a thread-pool thread for the periodic reader check, or the thread on which a reader scan finishes), and <c>ProjectChanged</c> on the thread that commits the
    /// project, which is the WPF dispatcher thread in the main application. Each member documents whether it must be
    /// called on the UI thread.
    /// </para>
    /// </summary>
    public interface IRFiDGearExtensionHost
    {
        /// <summary>Project load, save, and task-collection management.</summary>
        IProjectHost Project { get; }

        /// <summary>Task execution control and result observation.</summary>
        ITaskExecHost Execution { get; }

        /// <summary>Reader and card state, and an on-demand reader scan.</summary>
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
