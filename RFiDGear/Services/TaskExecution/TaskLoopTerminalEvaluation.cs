using System;
using System.Collections.Generic;
using System.Linq;

using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.Tasks.Interfaces;

namespace RFiDGear.Services.TaskExecution
{
    /// <summary>
    /// Describes the normalized terminal state of one task execution pass.
    /// </summary>
    public enum TaskExecutionTerminalStatus
    {
        /// <summary>
        /// No task loop has completed yet.
        /// </summary>
        NotRun,

        /// <summary>
        /// Every task outcome was successful or explicitly handled by the project workflow.
        /// </summary>
        Succeeded,

        /// <summary>
        /// At least one task outcome was unknown, unhandled, or could not be executed safely.
        /// </summary>
        Failed
    }

    /// <summary>
    /// Captures the key-free execution facts needed to evaluate a task loop.
    /// </summary>
    internal sealed class TaskExecutionObservation
    {
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

        public int TaskPosition { get; }

        public string TaskId { get; }

        public IGenericTask Task { get; }

        public string Decision { get; }

        public bool? WasSuccessful { get; }

        public ERROR ErrorLevel { get; }

        public string SkipReason { get; }
    }

    /// <summary>
    /// Contains the normalized task-loop result and its non-secret aggregate counts.
    /// </summary>
    internal sealed class TaskLoopRunResult
    {
        public TaskExecutionTerminalStatus TerminalStatus { get; set; }

        public int TotalTasks { get; set; }

        public int Executed { get; set; }

        public int Successful { get; set; }

        public int Failed { get; set; }

        public int AcceptedFailures { get; set; }

        public int UnhandledFailures { get; set; }

        public int Skipped { get; set; }

        public int Unknown { get; set; }

        public int InvalidSkips { get; set; }
    }

    /// <summary>
    /// Derives an authoritative terminal state from the collected task outcomes.
    /// </summary>
    internal static class TaskLoopTerminalEvaluator
    {
        private const string ExecutedDecision = "Executed";
        private const string SkippedDecision = "Skipped";
        private const string ConditionalSkipReason = "ExecuteConditionNotMet";

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

            return new TaskLoopRunResult
            {
                TerminalStatus = unhandledFailures == 0 && unknown == 0 && invalidSkips == 0 && !noObservedTask
                    ? TaskExecutionTerminalStatus.Succeeded
                    : TaskExecutionTerminalStatus.Failed,
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
