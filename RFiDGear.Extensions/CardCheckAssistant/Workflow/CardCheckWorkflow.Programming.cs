using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

using RFiDGear.ExtensionLibraries.CardCheck;

namespace RFiDGear.Extensions.CardCheckAssistant.Workflow
{
    // The programming leg of the guided check. The chip is taken off the reader and put back while the operator programs
    // it elsewhere (in the LSM); the workflow only watches the reader. Everything here follows the rules of the engine in
    // CardCheckWorkflow.cs: a command is a synchronous request that starts at most one operation, and the operation ends in
    // a state in which nothing runs.
    [SuppressMessage("Minor Code Smell", "S2333:Partial is gratuitous in this context", Justification = "The class is split over two files to keep each of them short; the analysis reads one file at a time.")]
    internal sealed partial class CardCheckWorkflow
    {
        /// <summary>
        /// Begins to program the chip: the workflow waits for it to be taken off the reader and put back. It is offered
        /// only for a chip whose verdict leads to programming and when the project that reads out its memory can be used.
        /// The wait does not end by itself before <see cref="WaitCeiling"/>; then the operator can wait again or cancel.
        /// </summary>
        /// <returns><c>true</c> if the state offers it and the wait has begun.</returns>
        public bool BeginProgramming()
        {
            if (!Offers(WorkflowControls.BeginProgramming))
            {
                return false;
            }

            // The card that was seen is on the reader: the look before the check made sure of it.
            var seen = Current.Card;
            var uidBefore = seen == null ? null : CardWaiter.PrimaryUid(seen);
            Run(WorkflowState.Programming, ct => MoveAsync(uidBefore, ct), null);

            return true;
        }

        // The wait ran out: it is made again, from where it was. A chip that was seen taken off is not waited for to be
        // taken off a second time. Looking at the reader changes nothing on the chip, so this is no second attempt at
        // anything that was written.
        private void WaitForMoveAgain()
        {
            // The retry control is offered only for a wait that ran out, and that leaves its result.
            var kept = Current.Move!;
            var reinsertionOnly = kept.Stage == CardMoveStage.WaitingForReinsertion;

            Run(
                WorkflowState.Programming,
                ct => reinsertionOnly ? ReinsertionAsync(kept, ct) : MoveAsync(kept.UidBefore, ct),
                s => s with { Move = null, MoveStage = reinsertionOnly ? CardMoveStage.WaitingForReinsertion : (CardMoveStage?)null });
        }

        private async Task MoveAsync(string? uidBefore, CancellationToken ct)
        {
            var moved = await _waiter.WaitForRemovalAndReinsertionAsync(WaitCeiling, uidBefore, stage => Change(s => s with { MoveStage = stage }), ct);
            SettleMove(moved);
        }

        private async Task ReinsertionAsync(CardMoveResult kept, CancellationToken ct)
        {
            var waited = await _waiter.WaitForCardAsync(WaitCeiling, ct);
            SettleMove(new CardMoveResult(
                waited.Outcome,
                CardMoveStage.WaitingForReinsertion,
                kept.UidBefore,
                waited.Outcome == CardWaitOutcome.Done ? CardWaiter.PrimaryUid(waited.Last!) : null,
                waited.Last ?? kept.Last));
        }

        // A wait that ran out does not cancel anything: the operator may have programmed the chip meanwhile, so the workflow
        // stays in Programming with the note and offers to wait again (or to cancel).
        private void SettleMove(CardMoveResult moved)
        {
            if (StopIfCancelled())
            {
                return;
            }

            switch (moved.Outcome)
            {
                case CardWaitOutcome.Done:
                    Settle(WorkflowState.Programming, s => s with { Move = moved, MoveStage = null });
                    break;
                case CardWaitOutcome.TimedOut:
                    Settle(
                        WorkflowState.Programming,
                        s => s with
                        {
                            Move = moved,
                            MoveStage = null,
                            Failure = new Failure(FailureKind.Timeout, WorkflowFailureCodes.CardMoveTimedOut, "The chip was not taken off the reader and put back."),
                        });
                    break;
                default:
                    EndCancelled();
                    break;
            }
        }
    }
}
