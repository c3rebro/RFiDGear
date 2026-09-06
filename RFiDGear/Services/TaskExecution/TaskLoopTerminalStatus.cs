namespace RFiDGear.Services.TaskExecution
{
    /// <summary>
    /// Classifies the terminal outcome of a completed task-loop run.
    /// Derived at runtime from collected task outcomes and the task graph's execute-condition
    /// relationships — no project file format change is required.
    /// </summary>
    public enum TaskLoopTerminalStatus
    {
        /// <summary>All executed tasks completed successfully; no failures of any kind.</summary>
        Success,

        /// <summary>
        /// The run completed and every failure has a downstream execute-condition handler that
        /// explicitly targets that descriptor and error level — the failures were intentional by design.
        /// Safe to repeat without operator intervention.
        /// </summary>
        CompletedWithIntentionalFailures,

        /// <summary>
        /// At least one executed task failed without a downstream handler, or produced an unknown
        /// outcome. Operator intervention is required before repeating.
        /// </summary>
        Failure
    }
}
