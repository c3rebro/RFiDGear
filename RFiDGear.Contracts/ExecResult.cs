using System;
using System.Collections.Generic;

namespace RFiDGear.Contracts
{
    /// <summary>
    /// Immutable summary of a completed task-loop execution returned by <see cref="ITaskExecHost"/>.
    /// Does not contain key material, payload bytes, or card data. The one free-text field in it is
    /// <see cref="TaskOutcome.ProjectTaskIndex"/>, which is the task index as stored in the project.
    /// </summary>
    /// <param name="RunId">
    /// Unique identifier for this execution pass. Correlates all <see cref="TaskOutcomes"/> entries
    /// and matches the <c>RunId</c> field in structured log events.
    /// </param>
    /// <param name="Outcome">Terminal classification of the run.</param>
    /// <param name="TotalTasks">Number of tasks that were present in the project during this run.</param>
    /// <param name="FailedTasks">
    /// Number of tasks that ran and did not complete successfully (i.e. <c>IsSuccess == false</c>). Tasks the run
    /// never reached are not counted, so an <see cref="ExecutionOutcome.Failure"/> with no failed task can mean the
    /// run stopped before its last task.
    /// </param>
    /// <param name="StartedAt">UTC timestamp when execution was accepted and began.</param>
    /// <param name="CompletedAt">UTC timestamp when the terminal result was produced.</param>
    /// <param name="TaskOutcomes">
    /// One entry per project task: first the tasks the run reached, in the order it reached them (each with
    /// its real result or skip reason), then the tasks it never reached in project order, marked
    /// <see cref="TaskSkipReasons.NotReached"/>. A <c>NotReached</c> entry does not say why: error routing, where it
    /// is in use, can jump over tasks on purpose, whereas a run that stops before its last task is reported as
    /// <see cref="ExecutionOutcome.Failure"/>; check <paramref name="Outcome"/> and
    /// <paramref name="FailureReason"/>. Always non-null and never stale: it is built from what <em>this</em>
    /// run did, even when the run was cancelled or faulted. Empty when the project has no tasks.
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
