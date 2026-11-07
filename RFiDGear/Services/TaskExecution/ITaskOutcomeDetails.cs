using System.Collections.Generic;

namespace RFiDGear.Services.TaskExecution
{
    /// <summary>
    /// A task that has a result worth logging with its outcome, beyond success or error: the expected and the actual
    /// settings of a file it checked, for instance. The task chooses what it reports and states it as named numbers and
    /// flags, so what ends up in the log is the list below and nothing else, and the service neither looks at the task's
    /// properties nor serializes its objects. The interface is internal on purpose: extensions get the success or error
    /// of a task from the public contract, and anything richer is a decision for that contract.
    /// </summary>
    internal interface ITaskOutcomeDetails
    {
        /// <summary>Gets the name under which the details appear in the outcome entry of the log.</summary>
        string OutcomeDetailsSection { get; }

        /// <summary>Gets the details of the last attempt of the task.</summary>
        /// <returns>
        /// The named values: numbers and flags only, never a key, a payload or text read from a card.
        /// Empty when there is nothing to report.
        /// </returns>
        IReadOnlyDictionary<string, object> GetOutcomeDetails();

        /// <summary>
        /// Forgets the details of an earlier attempt. The service calls it before a run starts, so that a task that is
        /// skipped or cannot run in the next one is never logged with what the last one found.
        /// </summary>
        void ClearOutcomeDetails();
    }
}
