using System;

using RFiDGear.ExtensionLibraries.CardCheck;

namespace RFiDGear.Extensions.CardCheckAssistant.Workflow
{
    /// <summary>
    /// The guided workflow as the page uses it: the commands, the current picture and the event that announces a new
    /// one. <see cref="CardCheckWorkflow"/> is the implementation. The page depends on this interface, not on the
    /// class, so a test can hand it any snapshot, in any order, without the engine's own rules in the way.
    /// </summary>
    internal interface IGuidedWorkflow : IDisposable
    {
        /// <summary>Raised after every change, with the new snapshot, in the order of the versions; on whatever thread made the change.</summary>
        event EventHandler<WorkflowSnapshot>? Changed;

        /// <summary>Gets the current picture; never <c>null</c>.</summary>
        WorkflowSnapshot Current { get; }

        /// <summary>Reads the list of orders again.</summary>
        /// <returns><c>true</c> if the state offers it and the read has started.</returns>
        bool RefreshOrders();

        /// <summary>Starts the check of an order.</summary>
        /// <param name="orderId">The id of the order.</param>
        /// <param name="language">The language of the report.</param>
        /// <returns><c>true</c> if the state offers it and the start has begun.</returns>
        bool Start(string orderId, ReportLanguage language);

        /// <summary>Answers the question whether the reports of an earlier attempt are replaced.</summary>
        /// <param name="replace"><c>true</c> to replace them, <c>false</c> to keep them and go back to the list.</param>
        /// <returns><c>true</c> if the state asked the question.</returns>
        bool ConfirmReplace(bool replace);

        /// <summary>Runs the check with the card that was seen.</summary>
        /// <returns><c>true</c> if the state offers it and the check has begun.</returns>
        bool StartCheck();

        /// <summary>Makes the finished report and uploads it.</summary>
        /// <returns><c>true</c> if the state offers it and the upload has begun.</returns>
        bool Finish();

        /// <summary>
        /// Begins to program the chip: the workflow waits for it to be taken off the reader and put back, while the
        /// operator programs it elsewhere.
        /// </summary>
        /// <returns><c>true</c> if the verdict leads to programming and the wait has begun.</returns>
        bool BeginProgramming();

        /// <summary>
        /// Goes on after the chip was put back: checks the answers, writes the second report and sets the change date of
        /// the order, reads the memory out. The answers are refused, with the reason in <see cref="WorkflowSnapshot.Failure"/>,
        /// if they are incomplete or the template does not fit; nothing is written then.
        /// </summary>
        /// <param name="template">The template of the programming, one of those that fit the chip.</param>
        /// <param name="answers">What the operator answered about the programming.</param>
        /// <returns><c>true</c> if the state offers it, the answers are good and the work has begun.</returns>
        bool ContinueProgramming(LsmTemplate template, LsmAnswers answers);

        /// <summary>Finishes the order with the report made before programming, because the memory could not be read out.</summary>
        /// <returns><c>true</c> if the state offers it and the finish has begun.</returns>
        bool FinishWithoutReadOut();

        /// <summary>Tries the failed step again.</summary>
        /// <returns><c>true</c> if the state offers it.</returns>
        bool Retry();

        /// <summary>Cancels what is going on.</summary>
        /// <returns><c>true</c> if the cancel was accepted.</returns>
        bool Cancel();

        /// <summary>Goes back to the list of orders after the end of a check.</summary>
        /// <returns><c>true</c> if the check had ended.</returns>
        bool Back();
    }
}
