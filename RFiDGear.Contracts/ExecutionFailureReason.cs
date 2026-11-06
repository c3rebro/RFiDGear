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
        /// The caller's cancellation token was honoured at a task boundary. Tasks that had already started
        /// completed first; the remaining tasks are reported with <see cref="TaskSkipReasons.NotReached"/>.
        /// </summary>
        Cancelled,

        /// <summary>
        /// A stage of the run exceeded its configured timeout, or a <see cref="System.TimeoutException"/> escaped the
        /// run (for example from a reader driver).
        /// <para>
        /// In this release the host configures no stage timeouts, so every <c>TimedOut</c> result comes from an
        /// escaped <see cref="System.TimeoutException"/>; the run has then already ended, no further task is started
        /// and nothing keeps running in the background. If a task-loop stage itself ever timed out, that stage would
        /// be abandoned, not cancelled: the loop would keep running in the background after the result is returned,
        /// the running task could still finish its card operation, and later tasks could still be started, possibly
        /// alongside a new run. In that case <see cref="ExecResult.TaskOutcomes"/> is only a snapshot. Either way,
        /// treat the card state as unknown.
        /// </para>
        /// <para>
        /// In the main application a per-task watchdog (Release builds only, about 4 seconds) flags the running task
        /// as failed and stops the loop from starting further tasks. It does not interrupt the running operation: the
        /// run ends only once that operation returns, and the task's own result replaces the flag. This is never
        /// reported as <c>TimedOut</c>. The run normally ends with <see cref="None"/> (in rare timing cases
        /// <see cref="Faulted"/>), and the tasks that did not get to run are reported as
        /// <see cref="TaskSkipReasons.NotReached"/>. A run that stopped before its last task is evaluated as
        /// <see cref="ExecutionOutcome.Failure"/>, so <see cref="ExecResult.Outcome"/> can be trusted here; the
        /// unreached tasks are not counted in <see cref="ExecResult.FailedTasks"/>. If the stopped task's own
        /// operation then completes successfully and it was the last task, nothing was left unreached and the
        /// run can still end as <see cref="ExecutionOutcome.Success"/>.
        /// </para>
        /// </summary>
        TimedOut,

        /// <summary>
        /// The run ended unexpectedly (for example no reader was available, a task threw, or an internal error
        /// occurred) or finished without a terminal evaluation. No further task is started after the failure. If a
        /// task threw, its card operation may be incomplete, so treat the card state as unknown; for other causes
        /// (such as no reader, or an error after the last task) no task may have failed at all. See the application
        /// log for the <see cref="ExecResult.RunId"/>.
        /// </summary>
        Faulted
    }
}
