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
    /// Per-task results in project order, one entry per task. Always non-null and never stale: task
    /// state is reset when a run is accepted, so tasks that were not executed in <em>this</em> run
    /// report <c>IsSuccess == null</c> with a <c>SkipReason</c>. May be empty when the project has no tasks.
    /// </param>
    /// <param name="FailureReason">
    /// Why the run ended without a terminal evaluation of its task loop, or
    /// <see cref="ExecutionFailureReason.None"/> when it reached one. When this is not
    /// <see cref="ExecutionFailureReason.None"/>, <paramref name="Outcome"/> is always
    /// <see cref="ExecutionOutcome.Failure"/> and <paramref name="TaskOutcomes"/> shows how far the run got.
    /// </param>
    public record ExecResult(
        string RunId,
        ExecutionOutcome Outcome,
        int TotalTasks,
        int FailedTasks,
        DateTimeOffset StartedAt,
        DateTimeOffset CompletedAt,
        IReadOnlyList<TaskOutcome> TaskOutcomes,
        ExecutionFailureReason FailureReason = ExecutionFailureReason.None);
}
