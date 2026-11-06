namespace RFiDGear.Contracts
{
    /// <summary>
    /// The closed set of values <see cref="TaskOutcome.SkipReason"/> can take. They are fixed tokens, never
    /// free text, so they are safe to log and to compare. Compare with <c>==</c> against these properties
    /// rather than hard-coding the strings.
    /// <para>
    /// The members are read-only static properties, <b>not compile-time constants</b>. They cannot be used as
    /// <c>case</c> labels, in constant patterns, as attribute arguments, as default parameter values, or in
    /// <c>const</c> initializers; use <c>==</c>, an if-chain, or a dictionary keyed by the property instead.
    /// <see cref="TaskOutcome.SkipReason"/> is a plain string and a future version may add tokens, so treat a
    /// value you do not recognize as non-fatal.
    /// </para>
    /// </summary>
    public static class TaskSkipReasons
    {
        /// <summary>
        /// The task was reached but its execute condition (another task's expected error level) was not met,
        /// so it was skipped on purpose. This is normal control flow, not a failure.
        /// </summary>
        public static string ExecuteConditionNotMet { get; } = "ExecuteConditionNotMet";

        /// <summary>The task was reached and skipped without an execute condition explaining why.</summary>
        public static string NotExecuted { get; } = "NotExecuted";

        /// <summary>The task could not be evaluated because its model was unavailable.</summary>
        public static string TaskModelUnavailable { get; } = "TaskModelUnavailable";

        /// <summary>
        /// The run never reached the task: it was cancelled or faulted before getting there, it ended early (for
        /// example the per-task watchdog in the main application stopped the loop), or error routing bypassed it.
        /// Alone this does not mean failure; check <see cref="ExecResult.Outcome"/> and
        /// <see cref="ExecResult.FailureReason"/>, and with <see cref="ExecutionFailureReason.None"/> also check
        /// the results of the other tasks.
        /// </summary>
        public static string NotReached { get; } = "NotReached";
    }
}
