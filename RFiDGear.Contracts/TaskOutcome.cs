namespace RFiDGear.Contracts
{
    /// <summary>
    /// Immutable result record for a single task within a completed execution run.
    /// Does not contain key values, payload bytes, or arbitrary task descriptions.
    /// <para>
    /// The list in <see cref="ExecResult.TaskOutcomes"/> first holds every task the run reached, in the order
    /// it reached them, followed by the tasks it never reached (see <see cref="TaskSkipReasons.NotReached"/>).
    /// </para>
    /// </summary>
    /// <param name="Position">Zero-based index of this entry in <see cref="ExecResult.TaskOutcomes"/>: the order the run reached the task, continuing in project order for tasks it never reached.</param>
    /// <param name="ProjectTaskIndex">
    /// Task index string exactly as stored in the project file (matches <c>IGenericTask.CurrentTaskIndex</c>). It is normally
    /// a small integer, but it is free text that is neither validated nor filtered, so do not put secrets in task indexes.
    /// </param>
    /// <param name="TaskTypeName">Short type name of the task view model (e.g. <c>MifareDesfireSetupViewModel</c>), or <c>Unknown</c> when the task model was unavailable.</param>
    /// <param name="IsSuccess">
    /// <c>true</c> = the task ran and succeeded; <c>false</c> = it ran and failed (including by throwing);
    /// <c>null</c> = it did not run (see <paramref name="SkipReason"/>), or it ran but its result could not be
    /// determined (<paramref name="SkipReason"/> is also <c>null</c>); the run is then reported as a failure.
    /// </param>
    /// <param name="NormalizedErrorCode">
    /// String representation of the task's normalized error level as recorded when the task was reached
    /// (e.g. <c>NoError</c>, <c>AuthFailure</c>, or <c>KeyUnavailable</c> when a key slot is bound to a secure key
    /// provider and the key could not be obtained, so the task made no card call), or <c>Empty</c> for a task that was
    /// never reached. Never contains key material or payload data.
    /// </param>
    /// <param name="SkipReason">
    /// Why the task did not run, or <c>null</c> when it ran. One of the fixed tokens in <see cref="TaskSkipReasons"/>.
    /// </param>
    public record TaskOutcome(
        int Position,
        string ProjectTaskIndex,
        string TaskTypeName,
        bool? IsSuccess,
        string NormalizedErrorCode,
        string? SkipReason = null);
}
