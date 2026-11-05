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
