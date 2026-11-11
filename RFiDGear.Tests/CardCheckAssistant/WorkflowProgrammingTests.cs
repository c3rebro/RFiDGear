using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using RFiDGear.ExtensionLibraries.CardCheck;
using RFiDGear.Extensions.CardCheckAssistant.Workflow;

using Xunit;

namespace RFiDGear.Tests.CardCheckAssistant
{
    /// <summary>The programming leg of the guided check, as far as it goes: the chip is taken off the reader and put back.</summary>
    public sealed class WorkflowProgrammingTests
    {
        private const string Uid = FakeCardWaiter.DefaultUid;

        // The state and the stage are passed by name in the theories: the test class is public, the enumerations are not.
        private static CardMoveStage Stage(string name) => Enum.Parse<CardMoveStage>(name);

        [Fact]
        public void TheProgrammingStep_HasItsEdges_ItsControls_AndItsCloseAdvice()
        {
            Assert.Equal(
                new[] { WorkflowState.Completing, WorkflowState.Programming, WorkflowState.Cancelled, WorkflowState.Faulted }.OrderBy(s => s),
                WorkflowTables.Next(WorkflowState.Assessing).OrderBy(s => s));
            Assert.Equal(
                new[] { WorkflowState.Cancelled, WorkflowState.Faulted }.OrderBy(s => s),
                WorkflowTables.Next(WorkflowState.Programming).OrderBy(s => s));
            Assert.Equal(new[] { WorkflowState.Assessing }, WorkflowTables.States.Where(s => WorkflowTables.IsAllowed(s, WorkflowState.Programming)));
            Assert.Equal(WorkflowControls.Finish | WorkflowControls.BeginProgramming | WorkflowControls.Cancel, WorkflowTables.ControlsOf(WorkflowState.Assessing));
            Assert.Equal(WorkflowControls.Retry | WorkflowControls.Cancel, WorkflowTables.ControlsOf(WorkflowState.Programming));
            Assert.Equal(CloseAdvice.Confirm, WorkflowTables.CloseAdviceOf(WorkflowState.Programming));
        }

        [Theory]
        [InlineData(ChipVerdict.NotSupported, false, false)]
        [InlineData(ChipVerdict.SupportedOnly, false, false)]
        [InlineData(ChipVerdict.Indeterminate, false, false)]
        [InlineData(ChipVerdict.Programmable, false, true)]
        [InlineData(ChipVerdict.Programmable, true, false)]
        [InlineData(ChipVerdict.NotSupported, true, false)]
        public Task Programming_IsOffered_OnlyForAProgrammableChip_WhoseReadOutProjectCanBeUsed(ChipVerdict verdict, bool projectProblem, bool offered) => WorkflowEngineTests.Sta(async rig =>
        {
            var problem = new Failure(FailureKind.NotFound, WorkflowFailureCodes.MemoryProjectInvalid, "No project.");
            rig.UseSettings(s => s with { MemoryProjectProblem = projectProblem ? problem : null });

            await rig.AssessedAsync(verdict);

            // A chip that is not programmed is finished, and one that is, is not.
            Assert.Equal(offered, rig.Now.Controls.HasFlag(WorkflowControls.BeginProgramming));
            Assert.Equal(verdict != ChipVerdict.Programmable, rig.Now.Controls.HasFlag(WorkflowControls.Finish));

            // For a chip that could be programmed, the page is told why it is not; nothing went wrong, so it is no failure.
            Assert.Same(verdict == ChipVerdict.Programmable && projectProblem ? problem : null, rig.Now.ProgrammingProblem);
            Assert.Null(rig.Now.Failure);
            if (!offered)
            {
                Assert.False(rig.Engine.BeginProgramming());
                Assert.Equal(WorkflowState.Assessing, rig.Now.State);
                Assert.Empty(rig.Waiter.Moves);
            }
        });

        [Fact]
        public Task BeginProgramming_WaitsForTheChipToBeTakenOffAndPutBack_AndSettlesInProgramming() => WorkflowEngineTests.Sta(async rig =>
        {
            await rig.AssessedAsync(ChipVerdict.Programmable);
            rig.Waiter.ThenTheCardIsMoved(Uid, Uid);
            rig.Events.Clear();

            Assert.True(rig.Engine.BeginProgramming());

            // The first picture already says that the workflow waits, and that only Cancel is left.
            var first = rig.Events[0];
            Assert.Equal(WorkflowState.Programming, first.State);
            Assert.True(first.IsBusy);
            Assert.Equal(WorkflowControls.Cancel, first.Controls);
            Assert.Null(first.Move);
            Assert.Null(first.Failure);

            await rig.Settled();

            Assert.Equal(new CardMoveStage?[] { null, CardMoveStage.WaitingForRemoval, CardMoveStage.WaitingForReinsertion, null }, rig.Events.Select(e => e.MoveStage));
            Assert.Equal(WorkflowState.Programming, rig.Now.State);
            Assert.False(rig.Now.IsBusy);
            Assert.Equal(WorkflowControls.Cancel, rig.Now.Controls);
            Assert.Null(rig.Now.Failure);
            Assert.Equal(CardWaitOutcome.Done, rig.Now.Move!.Outcome);
            Assert.True(rig.Now.Move.SameCard);

            // The chip that was seen on the reader is the one that is waited for to be taken off, for at most 30 minutes.
            Assert.Equal((CardCheckWorkflow.WaitCeiling, Uid), rig.Waiter.Moves.Single());
        });

        [Fact]
        public Task AnotherChipPutBack_IsAccepted_AndTheSnapshotSaysItIsAnotherOne() => WorkflowEngineTests.Sta(async rig =>
        {
            await rig.AssessedAsync(ChipVerdict.Programmable);
            rig.Waiter.ThenTheCardIsMoved(Uid, "04112233");

            Assert.True(rig.Engine.BeginProgramming());
            await rig.Settled();

            Assert.Equal(WorkflowState.Programming, rig.Now.State);
            Assert.Equal(CardWaitOutcome.Done, rig.Now.Move!.Outcome);
            Assert.False(rig.Now.Move.SameCard);
            Assert.Equal("04112233", rig.Now.Move.UidAfter);
        });

        [Theory]
        [InlineData("WaitingForRemoval")]
        [InlineData("WaitingForReinsertion")]
        public Task ACancel_WhileTheChipIsAwaited_EndsTheProgramming_InEitherStage(string stageName) => WorkflowEngineTests.Sta(async rig =>
        {
            var stage = Stage(stageName);
            await rig.AssessedAsync(ChipVerdict.Programmable);
            rig.Waiter.ThenTheMoveWaitsInStageUntilCancelled(stage);

            Assert.True(rig.Engine.BeginProgramming());
            Assert.True(rig.Now.IsBusy);
            Assert.Equal(stage, rig.Now.MoveStage);

            Assert.True(rig.Engine.Cancel());
            await rig.Settled();

            Assert.Contains(rig.Events, e => e.State == WorkflowState.Programming && e.IsCancelling);
            Assert.Equal(WorkflowState.Cancelled, rig.Now.State);
            Assert.Equal(WorkflowState.Programming, rig.Now.CancelledAt);
            Assert.Null(rig.Now.MoveStage);
            Assert.Null(rig.Now.Failure);
            Assert.Empty(rig.Store.Finalized);
        });

        [Theory]
        [InlineData("WaitingForRemoval")]
        [InlineData("WaitingForReinsertion")]
        public Task AWaitThatRunsOut_LeavesTheProgrammingStepOpen_WithANote_AndOffersToWaitAgain(string stageName) => WorkflowEngineTests.Sta(async rig =>
        {
            var stage = Stage(stageName);
            await rig.AssessedAsync(ChipVerdict.Programmable);
            rig.Waiter.ThenTheMoveTimesOut(stage);

            Assert.True(rig.Engine.BeginProgramming());
            await rig.Settled();

            // Nobody is cancelled by a clock: the chip may have been programmed meanwhile.
            Assert.Equal(WorkflowState.Programming, rig.Now.State);
            Assert.False(rig.Now.IsBusy);
            Assert.Equal(WorkflowControls.Retry | WorkflowControls.Cancel, rig.Now.Controls);
            Assert.Equal(WorkflowFailureCodes.CardMoveTimedOut, rig.Now.Failure!.Code);
            Assert.Equal(CardWaitOutcome.TimedOut, rig.Now.Move!.Outcome);
            Assert.Equal(stage, rig.Now.Move.Stage);
            Assert.Equal(Uid, rig.Now.Move.UidBefore);

            // Cancelling from here drops the note and says where the cancel came.
            Assert.True(rig.Engine.Cancel());
            Assert.Equal(WorkflowState.Cancelled, rig.Now.State);
            Assert.Equal(WorkflowState.Programming, rig.Now.CancelledAt);
            Assert.Null(rig.Now.Failure);
        });

        [Fact]
        public Task AChipPutBack_AfterTheCancelWasAskedFor_DoesNotEndTheCancel() => WorkflowEngineTests.Sta(async rig =>
        {
            await rig.AssessedAsync(ChipVerdict.Programmable);
            var gate = new TaskCompletionSource<bool>();
            rig.Waiter.ThenTheMoveHoldsUntil(gate, Uid);
            Assert.True(rig.Engine.BeginProgramming());

            // A look that has begun brings its answer all the same; the cancel wins.
            Assert.True(rig.Engine.Cancel());
            gate.SetResult(true);
            await rig.Settled();

            Assert.Equal(WorkflowState.Cancelled, rig.Now.State);
            Assert.Equal(WorkflowState.Programming, rig.Now.CancelledAt);
            Assert.Null(rig.Now.Move);
        });

        [Fact]
        public Task ACancel_WhileWaitingAgainForAChip_EndsTheProgramming() => WorkflowEngineTests.Sta(async rig =>
        {
            await rig.AssessedAsync(ChipVerdict.Programmable);
            rig.Waiter.ThenTheMoveTimesOut(CardMoveStage.WaitingForReinsertion);
            Assert.True(rig.Engine.BeginProgramming());
            await rig.Settled();
            rig.Waiter.ThenWaitsUntilCancelled();
            Assert.True(rig.Engine.Retry());
            Assert.True(rig.Now.IsBusy);

            Assert.True(rig.Engine.Cancel());
            await rig.Settled();

            Assert.Equal(WorkflowState.Cancelled, rig.Now.State);
            Assert.Equal(WorkflowState.Programming, rig.Now.CancelledAt);
        });

        [Fact]
        public Task AWaitThatFails_EndsInFaulted_WithoutTheTextOfTheException_AndWithoutAStage() => WorkflowEngineTests.Sta(async rig =>
        {
            await rig.AssessedAsync(ChipVerdict.Programmable);
            rig.Waiter.ThenTheMoveThrows(new InvalidOperationException("secret"));

            Assert.True(rig.Engine.BeginProgramming());
            await rig.Settled();

            Assert.Equal(WorkflowState.Faulted, rig.Now.State);
            Assert.Equal(WorkflowFailureCodes.Faulted, rig.Now.Failure!.Code);
            Assert.Null(rig.Now.MoveStage);
            Assert.Contains("InvalidOperationException", rig.Log.Everything, StringComparison.Ordinal);
            Assert.DoesNotContain("secret", rig.Log.Everything, StringComparison.Ordinal);
        });

        [Fact]
        public Task Back_ForgetsWhyProgrammingWasNotOffered() => WorkflowEngineTests.Sta(async rig =>
        {
            var problem = new Failure(FailureKind.NotFound, WorkflowFailureCodes.MemoryProjectInvalid, "No project.");
            rig.UseSettings(s => s with { MemoryProjectProblem = problem });
            await rig.AssessedAsync(ChipVerdict.Programmable);
            Assert.Same(problem, rig.Now.ProgrammingProblem);

            Assert.True(rig.Engine.Cancel());
            Assert.True(rig.Engine.Back());

            Assert.Null(rig.Now.ProgrammingProblem);
        });

        [Fact]
        public Task WaitingAgain_AfterTheRemovalRanOut_LooksForTheRemovalAgain_WithTheChipThatWasKnown() => WorkflowEngineTests.Sta(async rig =>
        {
            await rig.AssessedAsync(ChipVerdict.Programmable);
            rig.Waiter.ThenTheMoveTimesOut(CardMoveStage.WaitingForRemoval);
            rig.Waiter.ThenTheMoveTimesOut(CardMoveStage.WaitingForRemoval);
            rig.Waiter.Timeouts.Clear();

            Assert.True(rig.Engine.BeginProgramming());
            await rig.Settled();
            Assert.True(rig.Engine.Retry());
            await rig.Settled();

            // A second wait that runs out is the same picture again, with the note again.
            Assert.Equal(WorkflowState.Programming, rig.Now.State);
            Assert.Equal(WorkflowControls.Retry | WorkflowControls.Cancel, rig.Now.Controls);
            Assert.Equal(WorkflowFailureCodes.CardMoveTimedOut, rig.Now.Failure!.Code);

            // The third is not scripted: the chip is taken off and put back.
            Assert.True(rig.Engine.Retry());
            await rig.Settled();

            Assert.Equal(WorkflowControls.Cancel, rig.Now.Controls);
            Assert.Null(rig.Now.Failure);
            Assert.Equal(CardWaitOutcome.Done, rig.Now.Move!.Outcome);
            Assert.Equal(new (TimeSpan?, string?)[] { (CardCheckWorkflow.WaitCeiling, Uid), (CardCheckWorkflow.WaitCeiling, Uid), (CardCheckWorkflow.WaitCeiling, Uid) }, rig.Waiter.Moves);
            Assert.Empty(rig.Waiter.Timeouts);   // the reader was not looked at in any other way
        });

        [Fact]
        public Task WaitingAgain_AfterTheReinsertionRanOut_OnlyWaitsForAChip_AndKeepsWhatWasSeenBefore() => WorkflowEngineTests.Sta(async rig =>
        {
            await rig.AssessedAsync(ChipVerdict.Programmable);
            rig.Waiter.ThenTheMoveTimesOut(CardMoveStage.WaitingForReinsertion);
            Assert.True(rig.Engine.BeginProgramming());
            await rig.Settled();
            Assert.Equal(WorkflowControls.Retry | WorkflowControls.Cancel, rig.Now.Controls);

            var gate = new TaskCompletionSource<bool>();
            rig.Waiter.ThenHoldsUntil(gate, Snapshots.Card("04112233"));
            rig.Waiter.Timeouts.Clear();

            Assert.True(rig.Engine.Retry());

            // While it waits: busy, the note and the old result are gone, and the stage is already known.
            Assert.True(rig.Now.IsBusy);
            Assert.Equal(WorkflowControls.Cancel, rig.Now.Controls);
            Assert.Null(rig.Now.Failure);
            Assert.Null(rig.Now.Move);
            Assert.Equal(CardMoveStage.WaitingForReinsertion, rig.Now.MoveStage);

            gate.SetResult(true);
            await rig.Settled();

            Assert.Single(rig.Waiter.Moves);   // the removal was seen, so it is not waited for a second time
            Assert.Equal(new TimeSpan?[] { CardCheckWorkflow.WaitCeiling }, rig.Waiter.Timeouts);
            Assert.Equal(WorkflowState.Programming, rig.Now.State);
            Assert.False(rig.Now.IsBusy);
            Assert.Equal(WorkflowControls.Cancel, rig.Now.Controls);
            Assert.Null(rig.Now.MoveStage);
            var move = rig.Now.Move!;
            Assert.Equal(CardWaitOutcome.Done, move.Outcome);
            Assert.Equal(CardMoveStage.WaitingForReinsertion, move.Stage);
            Assert.Equal(Uid, move.UidBefore);
            Assert.Equal("04112233", move.UidAfter);
            Assert.False(move.SameCard);
        });

        [Fact]
        public void TheStatesThatOfferRetry_AreTheOnesWhoseRetryIsWrittenOut()
        {
            // A state that is given the retry control must be given its case in Retry(): this list is the reminder.
            Assert.Equal(
                new[] { "CompletionFailed", "Programming", "RunFailed" },
                WorkflowTables.States.Where(s => WorkflowTables.ControlsOf(s).HasFlag(WorkflowControls.Retry)).Select(s => s.ToString()).OrderBy(n => n, StringComparer.Ordinal));
        }

        [Theory]
        [InlineData("RunFailed")]
        [InlineData("CompletionFailed")]
        [InlineData("Programming")]
        public Task EveryStateThatOffersRetry_AcceptsIt(string name) => WorkflowEngineTests.Sta(async rig =>
        {
            var state = Enum.Parse<WorkflowState>(name);
            if (state == WorkflowState.Programming)
            {
                rig.Waiter.ThenTheMoveTimesOut(CardMoveStage.WaitingForRemoval);
            }

            await rig.ReachAsync(state);

            Assert.True(rig.Now.Controls.HasFlag(WorkflowControls.Retry));
            Assert.True(rig.Engine.Retry());

            // What the retry started ends before the rig is torn down (a finished report is made on a worker thread).
            await rig.Settled();
        });

        [Fact]
        public Task WhileTheChipIsAwaited_EveryCommandButCancel_IsRefused() => WorkflowEngineTests.Sta(async rig =>
        {
            await rig.AssessedAsync(ChipVerdict.Programmable);
            rig.Waiter.ThenTheMoveWaitsInStageUntilCancelled(CardMoveStage.WaitingForRemoval);
            Assert.True(rig.Engine.BeginProgramming());
            var before = rig.Now;

            Assert.False(rig.Engine.RefreshOrders());
            Assert.False(rig.Engine.Start(WorkflowEngineTests.Rig.OrderId, ReportLanguage.German));
            Assert.False(rig.Engine.ConfirmReplace(true));
            Assert.False(rig.Engine.ConfirmReplace(false));
            Assert.False(rig.Engine.StartCheck());
            Assert.False(rig.Engine.Finish());
            Assert.False(rig.Engine.BeginProgramming());
            Assert.False(rig.Engine.Retry());
            Assert.False(rig.Engine.Back());
            Assert.Same(before, rig.Now);

            Assert.True(rig.Engine.Cancel());
            await rig.Settled();
            Assert.Equal(WorkflowState.Cancelled, rig.Now.State);
        });

        [Fact]
        public Task Back_AfterTheProgrammingStep_LeavesNothingOfTheOrderBehind() => WorkflowEngineTests.Sta(async rig =>
        {
            await rig.AssessedAsync(ChipVerdict.Programmable);
            Assert.True(rig.Engine.BeginProgramming());
            await rig.Settled();
            Assert.NotNull(rig.Now.Move);

            Assert.True(rig.Engine.Cancel());
            Assert.True(rig.Engine.Back());

            // Only what belongs to the window, not to the order, stays: the list, the mode, the language, the text blocks.
            var now = rig.Now;
            Assert.Equal(
                new WorkflowSnapshot
                {
                    State = WorkflowState.Idle,
                    Controls = WorkflowControls.Refresh | WorkflowControls.Start,
                    Close = CloseAdvice.Allow,
                    Orders = now.Orders,
                    IsTestMode = now.IsTestMode,
                    DefaultLanguage = now.DefaultLanguage,
                    TextBlocks = now.TextBlocks,
                    Version = now.Version,
                },
                now);
        });

        [Fact]
        public Task TheTextBlocks_AreTakenFromTheSettings_WhenTheListIsReadAndWhenAnOrderStarts() => WorkflowEngineTests.Sta(async rig =>
        {
            var first = new[] { new TextTemplate { Name = "A", Content = "a" } };
            var second = new[] { new TextTemplate { Name = "B", Content = "b" } };

            rig.UseSettings(s => s with { TextBlocks = first });
            Assert.True(rig.Engine.RefreshOrders());
            await rig.Settled();
            Assert.Same(first, rig.Now.TextBlocks);

            rig.UseSettings(s => s with { TextBlocks = second });
            await rig.WaitingForCardAsync();
            Assert.Same(second, rig.Now.TextBlocks);
        });
    }
}
