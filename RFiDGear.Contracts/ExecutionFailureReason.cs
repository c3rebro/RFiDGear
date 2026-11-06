namespace RFiDGear.Contracts
{
    /// <summary>
    /// Non-sensitive classification of why a run ended <em>without</em> a normal terminal evaluation
    /// of its task loop. It never carries exception text, key material, or payload data; details
    /// are written to the application log under the run's <see cref="ExecResult.RunId"/>.
    /// </summary>
    public enum ExecutionFailureReason
    {
        /// <summary>
        /// The task loop reached its terminal evaluation. <see cref="ExecResult.Outcome"/> and
        /// <see cref="ExecResult.TaskOutcomes"/> describe the result; any task failures are reported there.
        /// </summary>
        None,

        /// <summary>
        /// The caller's cancellation token was honoured at a task boundary. Tasks that had already
        /// started completed first; the remaining tasks are reported as not executed.
        /// </summary>
        Cancelled,

        /// <summary>A stage of the run exceeded its configured timeout.</summary>
        TimedOut,

        /// <summary>
        /// The run ended unexpectedly (for example no reader was available, or an internal error
        /// occurred) or finished without a terminal evaluation. See the application log for the <see cref="ExecResult.RunId"/>.
        /// </summary>
        Faulted
    }
}
