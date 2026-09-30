namespace RFiDGear.Contracts
{
    /// <summary>
    /// Top-level host service exported by RFiDGear and importable by extensions via MEF.
    /// Provides stable, supported access to project lifecycle, task execution, reader state,
    /// and automatic mode — without requiring a direct reference to internal view models.
    /// </summary>
    public interface IRFiDGearExtensionHost
    {
        /// <summary>Project load, save, and task-collection management.</summary>
        IProjectHost Project { get; }

        /// <summary>Task execution control and result observation.</summary>
        ITaskExecHost Execution { get; }

        /// <summary>Read-only reader and card state.</summary>
        IReaderHost Reader { get; }

        /// <summary>Continuous automatic card-processing mode control.</summary>
        IAutoModeHost AutoMode { get; }
    }
}
