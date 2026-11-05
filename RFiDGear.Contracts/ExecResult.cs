using System;
using System.Collections.Generic;

namespace RFiDGear.Contracts
{
    /// <summary>
    /// Immutable summary of a completed task-loop execution returned by <see cref="ITaskExecHost"/>.
    /// Does not contain key material, payload bytes, or card data.
    /// </summary>
    /// <param name="RunId">
    /// Unique identifier for this execution pass. Correlates all <see cref="TaskOutcomes"/> entries
    /// and matches the <c>RunId</c> field in structured log events.
    /// </param>
    /// <param name="Outcome">Terminal classification of the run.</param>
    /// <param name="TotalTasks">Number of tasks that were present in the project during this run.</param>
    /// <param name="FailedTasks">Number of tasks that did not complete successfully (i.e. <c>IsSuccess == false</c>).</param>
    /// <param name="StartedAt">UTC timestamp when execution was accepted and began.</param>
    /// <param name="CompletedAt">UTC timestamp when the terminal result was produced.</param>
    /// <param name="TaskOutcomes">
    /// Ordered per-task results, one entry per task in execution order.
    /// Always non-null; may be empty when execution failed before the task loop started.
    /// </param>
    public record ExecResult(
        string RunId,
        ExecutionOutcome Outcome,
        int TotalTasks,
        int FailedTasks,
        DateTimeOffset StartedAt,
        DateTimeOffset CompletedAt,
        IReadOnlyList<TaskOutcome> TaskOutcomes);
}
