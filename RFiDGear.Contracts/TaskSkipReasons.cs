namespace RFiDGear.Contracts
{
    /// <summary>
    /// The closed set of values <see cref="TaskOutcome.SkipReason"/> can take. They are fixed tokens, never
    /// free text, so they are safe to log and to compare. Compare with <c>==</c> against these constants
    /// rather than hard-coding the strings.
    /// </summary>
    public static class TaskSkipReasons
    {
        /// <summary>
        /// The task was reached but its execute condition (another task's expected error level) was not met,
        /// so it was skipped on purpose. This is normal control flow, not a failure.
        /// </summary>
        public const string ExecuteConditionNotMet = "ExecuteConditionNotMet";

        /// <summary>The task was reached and skipped without an execute condition explaining why.</summary>
        public const string NotExecuted = "NotExecuted";

        /// <summary>The task could not be evaluated because its model was unavailable.</summary>
        public const string TaskModelUnavailable = "TaskModelUnavailable";

        /// <summary>
        /// The run ended before the task was reached (cancelled, faulted, or bypassed by error routing).
        /// Alone this does not mean failure; check <see cref="ExecResult.Outcome"/> and
        /// <see cref="ExecResult.FailureReason"/>.
        /// </summary>
        public const string NotReached = "NotReached";
    }
}
