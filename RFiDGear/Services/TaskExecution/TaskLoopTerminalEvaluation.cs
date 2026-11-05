using System;
using System.Collections.Generic;
using System.Linq;

using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.Tasks.Interfaces;

namespace RFiDGear.Services.TaskExecution
{
    /// <summary>
    /// Captures the key-free execution facts needed to evaluate the terminal state of a task loop.
    /// </summary>
    internal sealed class TaskExecutionObservation
    {
        /// <summary>
        /// Initializes a new observation record for one task execution decision.
        /// </summary>
        /// <param name="taskPosition">Zero-based position of the task in the descriptor list.</param>
        /// <param name="taskId">Task identifier used in dependency expressions.</param>
        /// <param name="task">The task model, for reading execute-condition metadata.</param>
        /// <param name="decision">The execution decision: "Executed" or "Skipped".</param>
        /// <param name="wasSuccessful">Outcome of an executed task, or <see langword="null"/> when skipped or unknown.</param>
        /// <param name="errorLevel">Error level recorded on the task after execution.</param>
        /// <param name="skipReason">Reason token for a skipped task, or <see langword="null"/>.</param>
        public TaskExecutionObservation(
            int taskPosition,
            string taskId,
            IGenericTask task,
            string decision,
            bool? wasSuccessful,
            ERROR errorLevel,
            string skipReason = null)
        {
            TaskPosition = taskPosition;
            TaskId = taskId;
            Task = task;
            Decision = decision;
            WasSuccessful = wasSuccessful;
            ErrorLevel = errorLevel;
            SkipReason = skipReason;
        }

        /// <summary>Gets the zero-based position of the task in the descriptor list.</summary>
        public int TaskPosition { get; }

        /// <summary>Gets the task identifier used in execute-condition expressions.</summary>
        public string TaskId { get; }

        /// <summary>Gets the task model, used for reading execute-condition metadata.</summary>
        public IGenericTask Task { get; }

        /// <summary>Gets the execution decision: "Executed" or "Skipped".</summary>
        public string Decision { get; }

        /// <summary>Gets the outcome for an executed task, or <see langword="null"/> when skipped or outcome is unknown.</summary>
        public bool? WasSuccessful { get; }

        /// <summary>Gets the error level recorded on the task after execution.</summary>
        public ERROR ErrorLevel { get; }

        /// <summary>Gets the reason token for a skipped task, or <see langword="null"/> for executed tasks.</summary>
        public string SkipReason { get; }
    }

    /// <summary>
    /// Contains the normalized task-loop result and its non-secret aggregate counts.
    /// </summary>
    internal sealed class TaskLoopRunResult
    {
        /// <summary>Gets or sets the normalized terminal classification of this run.</summary>
        public TaskLoopTerminalStatus TerminalStatus { get; set; }

        /// <summary>Gets or sets the total number of tasks in the descriptor list.</summary>
        public int TotalTasks { get; set; }

        /// <summary>Gets or sets the number of tasks that were executed.</summary>
        public int Executed { get; set; }

        /// <summary>Gets or sets the number of executed tasks that completed successfully.</summary>
        public int Successful { get; set; }

        /// <summary>Gets or sets the number of executed tasks that failed.</summary>
        public int Failed { get; set; }

        /// <summary>
        /// Gets or sets the number of failed tasks whose failure was consumed by a downstream
        /// condition branch or error-routing entry that was itself executed.
        /// </summary>
        public int AcceptedFailures { get; set; }

        /// <summary>Gets or sets the number of failed tasks that were not consumed by any handler.</summary>
        public int UnhandledFailures { get; set; }

        /// <summary>Gets or sets the number of tasks that were skipped.</summary>
        public int Skipped { get; set; }

        /// <summary>Gets or sets the number of executed tasks whose outcome could not be determined.</summary>
        public int Unknown { get; set; }

        /// <summary>
        /// Gets or sets the number of tasks that were skipped without a valid conditional basis
        /// (no matching executed source task with a non-matching error level).
        /// </summary>
        public int InvalidSkips { get; set; }
    }

    /// <summary>
    /// Derives an authoritative terminal state from the collected task observations of one execution pass.
    /// </summary>
    internal static class TaskLoopTerminalEvaluator
    {
        private const string ExecutedDecision = "Executed";
        private const string SkippedDecision = "Skipped";
        private const string ConditionalSkipReason = "ExecuteConditionNotMet";

        /// <summary>
        /// Evaluates the terminal state of a task loop from its collected observations.
        /// </summary>
        /// <param name="totalTasks">Total number of tasks in the descriptor list.</param>
        /// <param name="observations">Per-task execution observations collected during the run.</param>
        /// <param name="errorRouting">Optional map of error levels to recovery task identifiers.</param>
        /// <returns>
        /// A <see cref="TaskLoopRunResult"/> with the derived <see cref="TaskLoopTerminalStatus"/>
        /// and non-secret aggregate counts.
        /// </returns>
        public static TaskLoopRunResult Evaluate(
            int totalTasks,
            IReadOnlyCollection<TaskExecutionObservation> observations,
            IDictionary<ERROR, string> errorRouting = null)
        {
            var items = observations?.ToList() ?? new List<TaskExecutionObservation>();
            var executed = items.Where(item => string.Equals(item.Decision, ExecutedDecision, StringComparison.Ordinal)).ToList();
            var failed = executed.Where(item => item.WasSuccessful == false).ToList();
            var acceptedFailures = failed.Count(item => IsFailureConsumed(item, executed, errorRouting));
            var skipped = items.Where(item => string.Equals(item.Decision, SkippedDecision, StringComparison.Ordinal)).ToList();
            var unknown = executed.Count(item => !item.WasSuccessful.HasValue);
            var invalidSkips = skipped.Count(item => !IsPermittedConditionalSkip(item, items));
            var unhandledFailures = failed.Count - acceptedFailures;
            var noObservedTask = totalTasks > 0 && items.Count == 0;

            TaskLoopTerminalStatus status;
            if (unhandledFailures > 0 || unknown > 0 || invalidSkips > 0 || noObservedTask)
            {
                status = TaskLoopTerminalStatus.Failure;
            }
            else if (acceptedFailures > 0)
            {
                status = TaskLoopTerminalStatus.CompletedWithIntentionalFailures;
            }
            else
            {
                status = TaskLoopTerminalStatus.Success;
            }

            return new TaskLoopRunResult
            {
                TerminalStatus = status,
                TotalTasks = totalTasks,
                Executed = executed.Count,
                Successful = executed.Count(item => item.WasSuccessful == true),
                Failed = failed.Count,
                AcceptedFailures = acceptedFailures,
                UnhandledFailures = unhandledFailures,
                Skipped = skipped.Count,
                Unknown = unknown,
                InvalidSkips = invalidSkips
            };
        }

        private static bool IsFailureConsumed(
            TaskExecutionObservation failedTask,
            IReadOnlyCollection<TaskExecutionObservation> executedTasks,
            IDictionary<ERROR, string> errorRouting)
        {
            if (failedTask.ErrorLevel == ERROR.Empty || failedTask.ErrorLevel == ERROR.NoError)
            {
                return false;
            }

            var conditionBranchExecuted = executedTasks.Any(candidate =>
                candidate.TaskPosition > failedTask.TaskPosition &&
                candidate.Task != null &&
                candidate.Task.SelectedExecuteConditionErrorLevel == failedTask.ErrorLevel &&
                string.Equals(
                    candidate.Task.SelectedExecuteConditionTaskIndex,
                    failedTask.TaskId,
                    StringComparison.Ordinal));

            if (conditionBranchExecuted)
            {
                return true;
            }

            return errorRouting != null &&
                errorRouting.TryGetValue(failedTask.ErrorLevel, out var routedTaskId) &&
                executedTasks.Any(candidate =>
                    candidate.TaskPosition > failedTask.TaskPosition &&
                    string.Equals(candidate.TaskId, routedTaskId, StringComparison.Ordinal));
        }

        private static bool IsPermittedConditionalSkip(
            TaskExecutionObservation skippedTask,
            IReadOnlyCollection<TaskExecutionObservation> observations)
        {
            if (!string.Equals(skippedTask.SkipReason, ConditionalSkipReason, StringComparison.Ordinal) ||
                skippedTask.Task == null ||
                skippedTask.Task.SelectedExecuteConditionErrorLevel == ERROR.Empty)
            {
                return false;
            }

            var conditionSource = observations.FirstOrDefault(candidate =>
                string.Equals(candidate.TaskId, skippedTask.Task.SelectedExecuteConditionTaskIndex, StringComparison.Ordinal));

            return conditionSource != null &&
                string.Equals(conditionSource.Decision, ExecutedDecision, StringComparison.Ordinal) &&
                conditionSource.ErrorLevel != ERROR.Empty &&
                conditionSource.ErrorLevel != skippedTask.Task.SelectedExecuteConditionErrorLevel;
        }
    }
}
