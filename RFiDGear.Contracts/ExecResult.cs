namespace RFiDGear.Contracts
{
    /// <summary>
    /// Summary of a completed task-loop execution returned by <see cref="ITaskExecHost"/>.
    /// </summary>
    /// <param name="Outcome">Terminal classification of the run.</param>
    /// <param name="TotalTasks">Number of tasks that were executed.</param>
    /// <param name="FailedTasks">Number of tasks that did not complete successfully.</param>
    public record ExecResult(ExecutionOutcome Outcome, int TotalTasks, int FailedTasks);
}
