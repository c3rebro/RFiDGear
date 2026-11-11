using System;
using System.Collections.Generic;
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

        /// <summary>
        /// Goes on after the chip was put back. The answers are checked first (the template must fit the chip, every
        /// question must be answered); if they are not good, nothing happens but the failure in the snapshot, and the state
        /// stays. Then, as one operation that only <see cref="Cancel"/> can interrupt: the answers are written into the
        /// second report (a copy of the first), the change date of the order is set, and the memory is read out. It ends
        /// in <see cref="WorkflowState.Reviewing"/>, or in a failed state from which the operator can try again.
        /// </summary>
        /// <param name="template">The template of the programming, one of those that fit the chip.</param>
        /// <param name="answers">What the operator answered about the programming.</param>
        /// <returns><c>true</c> if the state offers it, the answers are good and the work has begun.</returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public bool ContinueProgramming(LsmTemplate template, LsmAnswers answers)
        {
            if (template == null)
            {
                throw new ArgumentNullException(nameof(template));
            }

            if (answers == null)
            {
                throw new ArgumentNullException(nameof(answers));
            }

            if (!Offers(WorkflowControls.ContinueProgramming))
            {
                return false;
            }

            var job = _job!;
            var fields = FieldsOf(job, template, answers);
            if (!fields.IsSuccess)
            {
                Change(s => s with { Failure = fields.Failure });

                return false;
            }

            job.Fields = fields.Value;
            Run(WorkflowState.WritingReport, ProgramAsync, null);

            return true;
        }

        /// <summary>
        /// Finishes the order with the report made before programming, because the memory could not be read out. The
        /// operator chooses this; the workflow never takes a report for the read-out that is not one.
        /// </summary>
        /// <returns><c>true</c> if the state offers it and the finish has begun.</returns>
        public bool FinishWithoutReadOut()
        {
            if (!Offers(WorkflowControls.FinishWithoutReadOut))
            {
                return false;
            }

            Run(WorkflowState.Completing, _ => CompleteAsync(), null);

            return true;
        }

        // The template is chosen and the answers are recorded in the session, which refuses what does not fit; the fields
        // are what the report gets. The session finds the template of the chip by its name: the fields are made from the one
        // it chose, not from what the caller passed under that name (another family or text would write other fields).
        private Result<IReadOnlyDictionary<string, string>> FieldsOf(Job job, LsmTemplate template, LsmAnswers answers)
        {
            var chosen = _session.ChooseTemplate(job.Order.Id, template);
            if (!chosen.IsSuccess)
            {
                return Result<IReadOnlyDictionary<string, string>>.Fail(chosen.Failure!);
            }

            var recorded = _session.RecordAnswers(job.Order.Id, answers);

            return recorded.IsSuccess ? answers.ToFields(_session.Current.ChosenTemplate!) : Result<IReadOnlyDictionary<string, string>>.Fail(recorded.Failure!);
        }

        // The second report is the first with the operator's answers in it, and the change date tells the order system that
        // the chip was handed on. Both can be done again without harm (the report is replaced, the date set to now), which
        // is what Retry does after a failure; a failure stops here, before the read-out. A cancel is looked at before any
        // failure is read, so it is never turned into a failed state.
        private async Task ProgramAsync(CancellationToken ct)
        {
            var job = _job!;

            var written = await Task.Run(() => _reports.WriteFields(job.Paths.PathOf(ReportFile.Step1), job.Paths.PathOf(ReportFile.Step2), job.Fields!), ct);
            if (StopIfCancelled())
            {
                return;
            }

            if (!written.IsSuccess)
            {
                Settle(WorkflowState.ProgrammingFailed, s => s with { Failure = written.Failure });

                return;
            }

            var touched = await job.Store.TouchChangedDateAsync(job.Order.Id, ct);
            if (StopIfCancelled())
            {
                return;
            }

            if (!touched.IsSuccess)
            {
                Settle(WorkflowState.ProgrammingFailed, s => s with { Failure = touched.Failure });

                return;
            }

            Move(WorkflowState.ReadingOut, null);
            await ReadOutAsync(ct);
        }

        // The chip is looked at once first: it may have been taken off again since it was put back, or this is a repeat. The
        // look is of presence only (it is no check of the UID: another chip may have been put back, and the page has told
        // the operator). Then the project that reads the memory out is run, continuing from the second report.
        private async Task ReadOutAsync(CancellationToken ct)
        {
            var job = _job!;

            var look = await _waiter.WaitForCardAsync(TimeSpan.Zero, ct);
            if (StopIfCancelled())
            {
                return;
            }

            if (look.Outcome == CardWaitOutcome.Cancelled)
            {
                EndCancelled();

                return;
            }

            if (look.Outcome != CardWaitOutcome.Done)
            {
                job.Plan = FinishRules.AfterProgramming(false);
                var failure = ReaderIsEmpty(look)
                    ? new Failure(FailureKind.NotFound, WorkflowFailureCodes.CardMissing, "No chip is on the reader.")
                    : CardUnclear();
                Settle(WorkflowState.ReadOutFailed, s => s with { Failure = failure });

                return;
            }

            var request = new CheckRunRequest(
                job.Settings.Location,
                job.Order.JobNumber,
                job.Order.ChipNumber,
                job.Settings.MemoryProjectPath,
                null,
                ReportFile.Step3,
                ReportFile.Step2);
            var result = await _runner.RunAsync(request, phase => Change(s => s with { Phase = phase }), ct);
            if (StopIfCancelled())
            {
                return;
            }

            if (result.Status == CheckRunStatus.Cancelled)
            {
                EndCancelled();

                return;
            }

            // The report of the read-out becomes the final one; if the run left none, the operator may choose to finish with
            // the second report (FinishWithoutReadOut).
            job.Plan = FinishRules.AfterProgramming(result.Status == CheckRunStatus.Finished);
            if (result.Status == CheckRunStatus.Failed)
            {
                Settle(WorkflowState.ReadOutFailed, s => s with { ReadOut = result, Failure = result.Failure, Phase = null });
            }
            else
            {
                Settle(WorkflowState.Reviewing, s => s with { ReadOut = result, Phase = null });
            }
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
