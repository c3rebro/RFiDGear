namespace RFiDGear.Services.TaskExecution
{
    /// <summary>
    /// Classifies the terminal outcome of a completed task-loop run.
    /// Derived at runtime from collected task outcomes and the task graph's execute-condition
    /// relationships — no project file format change is required.
    /// </summary>
    public enum TaskLoopTerminalStatus
    {
        /// <summary>
        /// All executed tasks completed successfully; no failures of any kind, and the loop got to the end of the
        /// task list (tasks that error routing jumped over on purpose do not count against it). A run of the
        /// selected task only is judged on that task alone.
        /// </summary>
        Success,

        /// <summary>
        /// The run completed and every failure has a downstream execute-condition handler that
        /// explicitly targets that descriptor and error level — the failures were intentional by design.
        /// Safe to repeat without operator intervention.
        /// </summary>
        CompletedWithIntentionalFailures,

        /// <summary>
        /// At least one executed task failed without a downstream handler, or produced an unknown
        /// outcome, or the loop ended before reaching every task (tasks after the last one it reached were
        /// never started; not applied to a run of the selected task only). Operator intervention is required
        /// before repeating.
        /// </summary>
        Failure
    }
}
