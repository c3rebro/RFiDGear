namespace RFiDGear.Contracts
{
    /// <summary>
    /// Immutable result record for a single task within a completed execution run.
    /// Does not contain key values, payload bytes, or arbitrary task descriptions.
    /// </summary>
    /// <param name="Position">Zero-based position in execution order within the run.</param>
    /// <param name="ProjectTaskIndex">Task index string as stored in the project file (matches <c>IGenericTask.CurrentTaskIndex</c>).</param>
    /// <param name="TaskTypeName">Short type name of the task view model (e.g. <c>MifareDesfireSetupViewModel</c>).</param>
    /// <param name="IsSuccess">
    /// <c>true</c> = completed successfully; <c>false</c> = failed;
    /// <c>null</c> = not executed (skipped or execution did not reach this task).
    /// </param>
    /// <param name="NormalizedErrorCode">
    /// String representation of the task's normalized error level (e.g. <c>NoError</c>, <c>AuthFailure</c>).
    /// Never contains key material or payload data.
    /// </param>
    public record TaskOutcome(
        int Position,
        string ProjectTaskIndex,
        string TaskTypeName,
        bool? IsSuccess,
        string NormalizedErrorCode);
}
