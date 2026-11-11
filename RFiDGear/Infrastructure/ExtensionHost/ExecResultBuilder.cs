#nullable enable
using RFiDGear.Contracts;
using RFiDGear.Services.TaskExecution;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace RFiDGear.Infrastructure.ExtensionHost
{
    /// <summary>
    /// Builds the <see cref="ExecResult"/> of a run. A run that an extension starts with
    /// <see cref="ITaskExecHost.ExecuteAllAsync(ExecutionRequest, CancellationToken)"/> and a run that automatic mode
    /// starts for a card are judged by the same rules, so both use this one place.
    /// </summary>
    internal static class ExecResultBuilder
    {
        /// <summary>
        /// Builds the result of a run whose execution service returned (it may still have failed a task, or
        /// have returned without evaluating the task loop).
        /// </summary>
        /// <param name="runId">The identity shared by the result and the log lines of the run.</param>
        /// <param name="startedAt">When the run was accepted.</param>
        /// <param name="terminalStatus">The terminal classification the execution service returned, or <c>null</c>.</param>
        /// <param name="snapshot">The task collection as it was when the run started.</param>
        /// <param name="observations">The observations the run recorded.</param>
        /// <returns>The result. A <c>null</c> <paramref name="terminalStatus"/> is never reported as a clean run.</returns>
        internal static ExecResult FromCompletion(
            string runId,
            DateTimeOffset startedAt,
            TaskLoopTerminalStatus? terminalStatus,
            IReadOnlyList<object?> snapshot,
            IReadOnlyList<TaskExecutionObservation> observations)
        {
            var failureReason = terminalStatus == null ? ExecutionFailureReason.Faulted : ExecutionFailureReason.None;
            var taskOutcomes = TaskOutcomeMapper.Build(snapshot, observations);

            return new ExecResult(
                runId,
                MapOutcome(terminalStatus),
                snapshot.Count,
                taskOutcomes.Count(t => t.IsSuccess == false),
                startedAt,
                DateTimeOffset.UtcNow,
                taskOutcomes,
                failureReason);
        }

        /// <summary>
        /// Builds the result of a run that ended with an exception. It keeps the outcomes of the tasks that were
        /// observed before the exception, so a caller can still tell which tasks completed.
        /// </summary>
        /// <param name="runId">The identity shared by the result and the log lines of the run.</param>
        /// <param name="startedAt">When the run was accepted.</param>
        /// <param name="failureReason">Why the run ended; see <see cref="Classify"/>.</param>
        /// <param name="snapshot">The task collection as it was when the run started.</param>
        /// <param name="observations">The observations the run recorded.</param>
        /// <returns>A <see cref="ExecutionOutcome.Failure"/> result.</returns>
        internal static ExecResult FromFailure(
            string runId,
            DateTimeOffset startedAt,
            ExecutionFailureReason failureReason,
            IReadOnlyList<object?> snapshot,
            IReadOnlyList<TaskExecutionObservation> observations)
        {
            var taskOutcomes = TaskOutcomeMapper.Build(snapshot, observations);

            return new ExecResult(
                runId,
                ExecutionOutcome.Failure,
                snapshot.Count,
                taskOutcomes.Count(t => t.IsSuccess == false),
                startedAt,
                DateTimeOffset.UtcNow,
                taskOutcomes,
                failureReason);
        }

        /// <summary>
        /// Maps an exception from the execution service to a non-sensitive reason. A bare
        /// <see cref="OperationCanceledException"/> only counts as caller cancellation when the
        /// caller's token was actually cancelled; anything else is an unexpected fault.
        /// </summary>
        /// <param name="ex">The exception that ended the run.</param>
        /// <param name="ct">The caller's cancellation token.</param>
        /// <returns>The reason.</returns>
        internal static ExecutionFailureReason Classify(Exception ex, CancellationToken ct) => ex switch
        {
            OperationCanceledException when ct.IsCancellationRequested => ExecutionFailureReason.Cancelled,
            TimeoutException                                            => ExecutionFailureReason.TimedOut,
            _                                                           => ExecutionFailureReason.Faulted,
        };

        private static ExecutionOutcome MapOutcome(TaskLoopTerminalStatus? status) => status switch
        {
            TaskLoopTerminalStatus.Success                          => ExecutionOutcome.Success,
            TaskLoopTerminalStatus.CompletedWithIntentionalFailures => ExecutionOutcome.CompletedWithIntentionalFailures,
            _                                                       => ExecutionOutcome.Failure,
        };
    }
}
