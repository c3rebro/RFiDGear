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

        /// <summary>
        /// Gets or sets the number of tasks after the last task the loop reached: the loop ended before it got to
        /// them. Tasks that error routing jumped over on purpose lie before a task the loop did reach and are not
        /// counted. Always 0 when only the selected task was run.
        /// </summary>
        public int NotReached { get; set; }
    }

    /// <summary>
    /// Derives an authoritative terminal state from the collected task observations of one execution pass.
    /// </summary>
    internal static class TaskLoopTerminalEvaluator
    {
        /// <summary>The decision recorded for a task the loop ran.</summary>
        internal const string ExecutedDecision = "Executed";

        /// <summary>The name of the task type that starts a program; its failure is never an answer.</summary>
        private const string ExecuteProgramTaskType = "ExecuteProgram";

        /// <summary>The decision recorded for a task the loop deliberately skipped.</summary>
        internal const string SkippedDecision = "Skipped";

        /// <summary>
        /// Evaluates the terminal state of a task loop from its collected observations.
        /// </summary>
        /// <param name="totalTasks">Total number of tasks in the descriptor list.</param>
        /// <param name="observations">Per-task execution observations collected during the run.</param>
        /// <param name="errorRouting">Optional map of error levels to recovery task identifiers.</param>
        /// <param name="selectedTaskOnly">
        /// <see langword="true"/> when the run was asked to execute only the selected task, so the other tasks are
        /// not expected to be reached.
        /// </param>
        /// <param name="abandonedAt">
        /// The position of the task the per-task watchdog gave up, or <see langword="null"/> when none was. Its failure is
        /// counted as unhandled, whatever error level it ended with.
        /// </param>
        /// <returns>
        /// A <see cref="TaskLoopRunResult"/> with the derived <see cref="TaskLoopTerminalStatus"/>
        /// and non-secret aggregate counts.
        /// </returns>
        /// <remarks>
        /// A run is a failure when the loop stopped before reaching every task. The loop visits the list in order,
        /// and the only way it passes a task without an observation is a forward jump by error routing, which always
        /// lands on a task the loop then reaches. So every task after the last one with an observation was never
        /// started because the run ended early, for example when the per-task watchdog (Release builds) stopped it.
        /// Such a run must not read as <see cref="TaskLoopTerminalStatus.Success"/> just because the tasks that did
        /// run were fine.
        /// </remarks>
        public static TaskLoopRunResult Evaluate(
            int totalTasks,
            IReadOnlyCollection<TaskExecutionObservation> observations,
            IDictionary<ERROR, string> errorRouting = null,
            bool selectedTaskOnly = false,
            int? abandonedAt = null)
        {
            var items = observations?.ToList() ?? new List<TaskExecutionObservation>();
            var executed = items.Where(item => string.Equals(item.Decision, ExecutedDecision, StringComparison.Ordinal)).ToList();
            var failed = executed.Where(item => item.WasSuccessful == false).ToList();

            // The task the watchdog gave up has given no answer, and no downstream task handled it: its failure is never accepted.
            var acceptedFailures = failed.Count(item =>
                item.TaskPosition != abandonedAt && (IsLogicAnswer(item) || IsFailureConsumed(item, executed, errorRouting)));
            var skipped = items.Where(item => string.Equals(item.Decision, SkippedDecision, StringComparison.Ordinal)).ToList();
            var unknown = executed.Count(item => !item.WasSuccessful.HasValue);
            var invalidSkips = skipped.Count(item => !IsPermittedConditionalSkip(item, items));
            var unhandledFailures = failed.Count - acceptedFailures;
            var noObservedTask = totalTasks > 0 && items.Count == 0;
            var lastReached = items.Count == 0 ? -1 : items.Max(item => item.TaskPosition);
            var notReached = selectedTaskOnly ? 0 : Math.Max(0, totalTasks - 1 - lastReached);

            TaskLoopTerminalStatus status;
            if (unhandledFailures > 0 || unknown > 0 || invalidSkips > 0 || noObservedTask || notReached > 0)
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
                InvalidSkips = invalidSkips,
                NotReached = notReached
            };
        }

        /// <summary>
        /// Whether a failed task only gave the answer "no" to a question: it ended with
        /// <see cref="ERROR.IsNotTrue"/> or <see cref="ERROR.IsNotFalse"/>, which a check task reports when its
        /// condition does not hold. That is the result of the check, not a fault, so it does not need a handler
        /// downstream. A program start (<c>ExecuteProgram</c>) is not a check: when it fails, the run failed.
        /// </summary>
        private static bool IsLogicAnswer(TaskExecutionObservation failedTask)
        {
            if (failedTask.ErrorLevel != ERROR.IsNotTrue && failedTask.ErrorLevel != ERROR.IsNotFalse)
            {
                return false;
            }

            return !string.Equals(TaskTypeOf(failedTask.Task), ExecuteProgramTaskType, StringComparison.Ordinal);
        }

        private static string TaskTypeOf(IGenericTask task)
        {
            try
            {
                return task?.GetType().GetProperty("SelectedTaskType")?.GetValue(task)?.ToString();
            }
            catch (Exception ex) when (ex is System.Reflection.TargetInvocationException || ex is System.Reflection.AmbiguousMatchException)
            {
                // A task whose type cannot be read is not known to be a check: its failure stays a failure.
                return ExecuteProgramTaskType;
            }
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
            if (!string.Equals(
                    skippedTask.SkipReason,
                    Contracts.TaskSkipReasons.ExecuteConditionNotMet,
                    StringComparison.Ordinal) ||
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
