namespace RFiDGear.Contracts
{
    /// <summary>
    /// Terminal outcome of a completed task-loop run, mirroring <c>TaskLoopTerminalStatus</c>.
    /// </summary>
    public enum ExecutionOutcome
    {
        /// <summary>All executed tasks completed successfully.</summary>
        Success,

        /// <summary>Every failure had a downstream handler that explicitly targets it — intentional by design.</summary>
        CompletedWithIntentionalFailures,

        /// <summary>At least one task failed without a downstream handler. Operator intervention required.</summary>
        Failure
    }
}
