#nullable enable
using RFiDGear.Contracts;
using RFiDGear.Infrastructure.Tasks.Interfaces;
using RFiDGear.Services.TaskExecution;

using System;
using System.Collections.Generic;

namespace RFiDGear.Infrastructure.ExtensionHost
{
    /// <summary>
    /// Turns what the task loop actually did into the extension-facing <see cref="TaskOutcome"/> list.
    /// <para>
    /// The loop's observations are the single source of truth: they are already what the terminal
    /// evaluation and the structured logs use, and they alone know the real execution order and why a
    /// task was skipped. Task state fields cannot answer that (handlers mark a skipped task completed,
    /// and some task kinds never update their execution state at all).
    /// </para>
    /// </summary>
    internal static class TaskOutcomeMapper
    {
        private const string UnknownTypeName = "Unknown";

        /// <summary>
        /// Builds the outcome list: first every task the loop reached, in the order it reached them,
        /// then every other task of the project as <see cref="TaskSkipReasons.NotReached"/>, in project order.
        /// The result therefore always has at least one entry per project task.
        /// </summary>
        /// <param name="snapshot">
        /// The project's tasks as they were when the run started. Observation positions index into it, so
        /// it must not be the live collection, which the user can edit while a run is in progress.
        /// </param>
        /// <param name="observations">The loop's observations for the run, in the order they were recorded.</param>
        /// <returns>The ordered outcomes. Contains no key material, payload bytes, or free text.</returns>
        public static IReadOnlyList<TaskOutcome> Build(
            IReadOnlyList<object?> snapshot,
            IReadOnlyList<TaskExecutionObservation> observations)
        {
            var outcomes = new List<TaskOutcome>(Math.Max(snapshot.Count, observations.Count));
            var reached = new HashSet<int>();

            foreach (var observation in observations)
            {
                reached.Add(observation.TaskPosition);

                var skipped = string.Equals(
                    observation.Decision,
                    TaskLoopTerminalEvaluator.SkippedDecision,
                    StringComparison.Ordinal);

                outcomes.Add(new TaskOutcome(
                    outcomes.Count,
                    observation.TaskId ?? observation.TaskPosition.ToString(),
                    observation.Task?.GetType().Name ?? UnknownTypeName,
                    // An executed task whose success could not be determined stays (null, null).
                    skipped ? null : observation.WasSuccessful,
                    observation.ErrorLevel.ToString(),
                    skipped ? observation.SkipReason : null));
            }

            for (var position = 0; position < snapshot.Count; position++)
            {
                if (reached.Contains(position))
                {
                    continue;
                }

                var task = snapshot[position];
                outcomes.Add(new TaskOutcome(
                    outcomes.Count,
                    (task as IGenericTask)?.CurrentTaskIndex ?? position.ToString(),
                    task?.GetType().Name ?? UnknownTypeName,
                    null,
                    ERROR.Empty.ToString(),
                    TaskSkipReasons.NotReached));
            }

            return outcomes;
        }
    }
}
