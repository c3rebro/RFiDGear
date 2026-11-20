#nullable enable
using RFiDGear.Contracts;

namespace RFiDGear.Services.TaskExecution
{
    /// <summary>
    /// A task that compares values and can say, for a program that has to explain a failed check, what it compared: the expected
    /// and the actual settings of a DESFire file, for instance (see <see cref="VerificationDetails"/>). The task chooses what it
    /// reports; the answer holds numbers and fixed tokens only, never a key, a payload or text read from a card. The interface is
    /// internal: the public contract is the <see cref="TaskOutcome.VerificationDetails"/> it ends up in.
    /// </summary>
    internal interface ITaskVerificationSource
    {
        /// <summary>Gets the details of the last attempt of the task.</summary>
        /// <returns>
        /// The details, or <see langword="null"/> when the last attempt compared nothing, or when there was no attempt in this run
        /// (the task forgets an earlier one when a run starts, see <see cref="ITaskOutcomeDetails.ClearOutcomeDetails"/>).
        /// </returns>
        VerificationDetails? GetVerificationDetails();
    }
}
