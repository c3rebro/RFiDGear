namespace RFiDGear.Contracts
{
    /// <summary>
    /// Terminal classification of a task-loop run, mirroring <c>TaskLoopTerminalStatus</c>.
    /// <para>
    /// A run that ended without a terminal evaluation of its task loop (cancelled, timed out, or faulted) is
    /// reported as <see cref="Failure"/>; <see cref="ExecResult.FailureReason"/> tells those cases apart from
    /// a run whose tasks failed.
    /// </para>
    /// </summary>
    public enum ExecutionOutcome
    {
        /// <summary>
        /// All executed tasks completed successfully and the run got to its last task, or to a program task
        /// <c>%EXIT</c> that ended it (see <c>ITaskExecHost.ExecuteAllAsync</c>). Tasks that error routing jumped over
        /// on purpose, where error routing is in use, and the tasks after such a <c>%EXIT</c>, do not count against it.
        /// </summary>
        Success,

        /// <summary>Every failure had a downstream handler that explicitly targets it — intentional by design.</summary>
        CompletedWithIntentionalFailures,

        /// <summary>
        /// At least one task failed without a downstream handler, the run stopped before it reached its last task
        /// other than at a <c>%EXIT</c> task that ended it on purpose (the unreached tasks are reported as
        /// <see cref="TaskSkipReasons.NotReached"/>), or the run ended without a
        /// terminal evaluation (see <see cref="ExecResult.FailureReason"/>). Operator intervention required.
        /// </summary>
        Failure
    }
}
