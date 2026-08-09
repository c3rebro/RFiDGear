namespace RFiDGear.Infrastructure.Tasks
{
    /// <summary>
    /// Tracks the execution lifecycle of a single task within an automated run.
    /// Replaces the <see cref="ERROR.TransportError"/> in-flight sentinel previously used by
    /// <c>TaskExecutionService</c> to signal that a command is in progress.
    /// </summary>
    public enum TaskExecutionState
    {
        /// <summary>Task has not been invoked in the current execution pass.</summary>
        NotStarted,
        /// <summary>Task command has been dispatched and has not yet returned.</summary>
        Running,
        /// <summary>Task command has returned; <see cref="RFiDGear.Infrastructure.IGenericTask.CurrentTaskErrorLevel"/> holds the final result.</summary>
        Completed
    }
}
