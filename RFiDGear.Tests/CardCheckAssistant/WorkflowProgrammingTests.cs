using System;
using System.Collections.Generic;
using System.IO;
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
        public void TheProgrammingStates_HaveTheirEdges_TheirControls_AndTheirCloseAdvice()
        {
            // Written out on purpose: the one test that catches an edge or a control that is too many.
            var rows = new (WorkflowState State, WorkflowState[] Next, WorkflowControls Controls, CloseAdvice Close)[]
            {
                (WorkflowState.Assessing, new[] { WorkflowState.Completing, WorkflowState.Programming, WorkflowState.Cancelled, WorkflowState.Faulted }, WorkflowControls.Finish | WorkflowControls.BeginProgramming | WorkflowControls.Cancel, CloseAdvice.Confirm),
                (WorkflowState.Programming, new[] { WorkflowState.WritingReport, WorkflowState.Cancelled, WorkflowState.Faulted }, WorkflowControls.ContinueProgramming | WorkflowControls.Retry | WorkflowControls.Cancel, CloseAdvice.Confirm),
                (WorkflowState.WritingReport, new[] { WorkflowState.ReadingOut, WorkflowState.ProgrammingFailed, WorkflowState.Cancelled, WorkflowState.Faulted }, WorkflowControls.Cancel, CloseAdvice.Refuse),
                (WorkflowState.ProgrammingFailed, new[] { WorkflowState.WritingReport, WorkflowState.Cancelled, WorkflowState.Faulted }, WorkflowControls.Retry | WorkflowControls.Cancel, CloseAdvice.Confirm),
                (WorkflowState.ReadingOut, new[] { WorkflowState.Reviewing, WorkflowState.ReadOutFailed, WorkflowState.Cancelled, WorkflowState.Faulted }, WorkflowControls.Cancel, CloseAdvice.Refuse),
                (WorkflowState.ReadOutFailed, new[] { WorkflowState.ReadingOut, WorkflowState.Completing, WorkflowState.Cancelled, WorkflowState.Faulted }, WorkflowControls.Retry | WorkflowControls.FinishWithoutReadOut | WorkflowControls.Cancel, CloseAdvice.Confirm),
                (WorkflowState.Reviewing, new[] { WorkflowState.Completing, WorkflowState.Cancelled, WorkflowState.Faulted }, WorkflowControls.Finish | WorkflowControls.Cancel, CloseAdvice.Confirm),
            };

            foreach (var row in rows)
            {
                Assert.Equal(row.Next.OrderBy(s => s), WorkflowTables.Next(row.State).OrderBy(s => s));
                Assert.Equal(row.Controls, WorkflowTables.ControlsOf(row.State));
                Assert.Equal(row.Close, WorkflowTables.CloseAdviceOf(row.State));
            }

            // Who can lead where: the finish is reached from the verdict, the review, the failed read-out and the failed upload.
            Assert.Equal(new[] { WorkflowState.Assessing }, WorkflowTables.States.Where(s => WorkflowTables.IsAllowed(s, WorkflowState.Programming)));
            Assert.Equal(new[] { WorkflowState.ReadingOut }, WorkflowTables.States.Where(s => WorkflowTables.IsAllowed(s, WorkflowState.Reviewing)));
            Assert.Equal(
                new[] { WorkflowState.Assessing, WorkflowState.CompletionFailed, WorkflowState.ReadOutFailed, WorkflowState.Reviewing }.OrderBy(s => s),
                WorkflowTables.States.Where(s => WorkflowTables.IsAllowed(s, WorkflowState.Completing)).OrderBy(s => s));
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
            Assert.Equal(WorkflowControls.ContinueProgramming | WorkflowControls.Cancel, rig.Now.Controls);
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

            Assert.Equal(WorkflowControls.ContinueProgramming | WorkflowControls.Cancel, rig.Now.Controls);
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
            Assert.Equal(WorkflowControls.ContinueProgramming | WorkflowControls.Cancel, rig.Now.Controls);
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
                new[] { "CompletionFailed", "Programming", "ProgrammingFailed", "ReadOutFailed", "RunFailed" },
                WorkflowTables.States.Where(s => WorkflowTables.ControlsOf(s).HasFlag(WorkflowControls.Retry)).Select(s => s.ToString()).OrderBy(n => n, StringComparer.Ordinal));
        }

        [Theory]
        [InlineData("RunFailed")]
        [InlineData("CompletionFailed")]
        [InlineData("Programming")]
        [InlineData("ProgrammingFailed")]
        [InlineData("ReadOutFailed")]
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
        public Task TheAnswers_AreWrittenIntoTheSecondReport_ThenTheDateIsSet_ThenTheMemoryIsReadOut_AndTheReadOutReportIsFinished() => WorkflowEngineTests.Sta(async rig =>
        {
            await rig.ReachAsync(WorkflowState.Programming);
            Assert.Equal(WorkflowControls.ContinueProgramming | WorkflowControls.Cancel, rig.Now.Controls);
            rig.Calls.Clear();
            rig.Waiter.Timeouts.Clear();
            rig.Events.Clear();
            var template = rig.Template;
            var answers = WorkflowEngineTests.Rig.GoodAnswers;

            Assert.True(rig.Engine.ContinueProgramming(template, answers));

            // The first picture says that the report is being written, and that only Cancel is left.
            Assert.Equal(WorkflowState.WritingReport, rig.Events[0].State);
            Assert.True(rig.Events[0].IsBusy);
            Assert.Equal(WorkflowControls.Cancel, rig.Events[0].Controls);

            await rig.Settled();

            Assert.Equal(new[] { WorkflowState.WritingReport, WorkflowState.ReadingOut, WorkflowState.Reviewing }, rig.Events.Select(e => e.State).Distinct());
            Assert.Equal(WorkflowState.Reviewing, rig.Now.State);
            Assert.False(rig.Now.IsBusy);
            Assert.Equal(WorkflowControls.Finish | WorkflowControls.Cancel, rig.Now.Controls);
            Assert.Null(rig.Now.Failure);
            Assert.Equal(CheckRunStatus.Finished, rig.Now.ReadOut!.Status);

            // The second report is the first with the fields of the answers; then the date; then the run, in that order.
            Assert.Equal(new[] { "write", "touch", "run" }, rig.Calls);
            var write = rig.Reports.Writes.Single();
            Assert.Equal(rig.Paths.PathOf(ReportFile.Step1), write.Source);
            Assert.Equal(rig.Paths.PathOf(ReportFile.Step2), write.Target);
            Assert.Equal(answers.ToFields(template).Value.OrderBy(p => p.Key), write.Fields.OrderBy(p => p.Key));
            Assert.Equal(WorkflowEngineTests.Rig.OrderId, rig.Store.Touched.Single());

            // The run continues from the second report, with the project that reads the memory out, as it is (no language variant).
            var request = rig.Runner.Requests.Last();
            Assert.Equal(ReportFile.Step3, request.Report);
            Assert.Equal(ReportFile.Step2, request.TemplateReport);
            Assert.Equal(rig.Settings.MemoryProjectPath, request.ProjectPath);
            Assert.NotEqual(rig.Settings.DefaultProjectPath, request.ProjectPath);   // not the project of the check
            Assert.Null(request.ProjectLanguage);
            Assert.Equal(WorkflowEngineTests.Rig.Job, request.JobNumber);
            Assert.Equal(WorkflowEngineTests.Rig.Chip, request.ChipNumber);

            // The chip was looked at once before the run, not waited for.
            Assert.Equal(new TimeSpan?[] { TimeSpan.Zero }, rig.Waiter.Timeouts);

            // The session knows what was chosen and answered.
            Assert.Equal(template, rig.Session.Current.ChosenTemplate);
            Assert.Equal(answers, rig.Session.Current.Answers);

            // "Report checked": the finished report is made from the report of the read-out.
            Assert.True(rig.Engine.Finish());
            await rig.Settled();

            Assert.Equal(WorkflowState.Completed, rig.Now.State);
            Assert.Equal(rig.Paths.PathOf(ReportFile.Step3), rig.Reports.Locked.Single().Source);
            Assert.Equal(WorkflowEngineTests.Rig.OrderId, rig.Store.Finalized.Single().Id);
        });

        [Fact]
        public Task TheWorkingReports_AreRemovedAfterAProgrammedFinish_WhenTheSettingSaysSo() => WorkflowEngineTests.Sta(
            async rig =>
            {
                await rig.ReachAsync(WorkflowState.Reviewing);
                Assert.True(File.Exists(rig.Paths.PathOf(ReportFile.Step1)));
                Assert.True(File.Exists(rig.Paths.PathOf(ReportFile.Step2)));
                Assert.True(File.Exists(rig.Paths.PathOf(ReportFile.Step3)));

                Assert.True(rig.Engine.Finish());
                await rig.Settled();

                Assert.Equal(WorkflowState.Completed, rig.Now.State);
                Assert.False(File.Exists(rig.Paths.PathOf(ReportFile.Step1)));
                Assert.False(File.Exists(rig.Paths.PathOf(ReportFile.Step2)));
                Assert.False(File.Exists(rig.Paths.PathOf(ReportFile.Step3)));
                Assert.True(File.Exists(rig.Paths.PathOf(ReportFile.Final)));
            },
            removeTemporary: true);

        [Fact]
        public Task WhenTheSecondReportCannotBeWritten_NothingElseHappens_AndRetryWritesItAgain() => WorkflowEngineTests.Sta(async rig =>
        {
            await rig.ReachAsync(WorkflowState.Programming);
            var failure = new Failure(FailureKind.Io, "report.unreadable", "The report cannot be read.");
            rig.Reports.WriteAnswer = Result.Fail(failure);
            rig.Calls.Clear();
            rig.Runner.Requests.Clear();

            await rig.ContinueAsync();

            Assert.Equal(WorkflowState.ProgrammingFailed, rig.Now.State);
            Assert.False(rig.Now.IsBusy);
            Assert.Equal(WorkflowControls.Retry | WorkflowControls.Cancel, rig.Now.Controls);
            Assert.Same(failure, rig.Now.Failure);
            Assert.Equal(new[] { "write" }, rig.Calls);   // the date is not set, the memory is not read
            Assert.Empty(rig.Runner.Requests);

            rig.Reports.WriteAnswer = null;
            Assert.True(rig.Engine.Retry());
            await rig.Settled();

            Assert.Equal(WorkflowState.Reviewing, rig.Now.State);
            Assert.Null(rig.Now.Failure);
            Assert.Equal(new[] { "write", "write", "touch", "run" }, rig.Calls);
        });

        [Fact]
        public Task WhenTheDateCannotBeSet_TheReadOutDoesNotRun_AndRetryDoesBothAgain() => WorkflowEngineTests.Sta(async rig =>
        {
            await rig.ReachAsync(WorkflowState.ProgrammingFailed);

            Assert.Equal("orders.unreachable", rig.Now.Failure!.Code);
            Assert.Equal(WorkflowControls.Retry | WorkflowControls.Cancel, rig.Now.Controls);
            Assert.True(File.Exists(rig.Paths.PathOf(ReportFile.Step2)));   // the report was written; the order system did not answer
            Assert.Equal(new[] { "write", "touch" }, rig.Calls.Skip(rig.Calls.Count - 2));
            var runs = rig.Runner.Requests.Count;

            rig.Store.TouchAnswer = null;
            Assert.True(rig.Engine.Retry());
            await rig.Settled();

            Assert.Equal(WorkflowState.Reviewing, rig.Now.State);
            Assert.Equal(runs + 1, rig.Runner.Requests.Count);
            Assert.Equal(new[] { "write", "touch", "write", "touch", "run" }, rig.Calls.Skip(rig.Calls.Count - 5));
        });

        [Fact]
        public Task AReadOutThatFails_LeavesTheOrderOpen_AndRetryRunsOnlyTheReadOutAgain() => WorkflowEngineTests.Sta(async rig =>
        {
            await rig.ReachAsync(WorkflowState.ReadOutFailed);

            Assert.False(rig.Now.IsBusy);
            Assert.Equal(WorkflowControls.Retry | WorkflowControls.FinishWithoutReadOut | WorkflowControls.Cancel, rig.Now.Controls);
            Assert.Equal(CheckFailureCodes.ReportMissing, rig.Now.Failure!.Code);
            Assert.Equal(CheckRunStatus.Failed, rig.Now.ReadOut!.Status);
            rig.Calls.Clear();
            rig.RunnerWritesFirstReport(true, true, "1760");
            var gate = new TaskCompletionSource<bool>();
            rig.Runner.Gate = gate;

            Assert.True(rig.Engine.Retry());

            // While it runs again: busy, and the old result and failure are gone.
            Assert.True(rig.Now.IsBusy);
            Assert.Equal(WorkflowState.ReadingOut, rig.Now.State);
            Assert.Null(rig.Now.ReadOut);
            Assert.Null(rig.Now.Failure);

            gate.SetResult(true);
            await rig.Settled();

            Assert.Equal(WorkflowState.Reviewing, rig.Now.State);
            Assert.Equal(CheckRunStatus.Finished, rig.Now.ReadOut!.Status);
            Assert.Equal(new[] { "run" }, rig.Calls);   // the report and the date are not done a second time
        });

        [Fact]
        public Task WithoutAChipOnTheReader_TheReadOutDoesNotRun_AndTheReasonIsTold() => WorkflowEngineTests.Sta(async rig =>
        {
            await rig.ReachAsync(WorkflowState.Programming);
            rig.Runner.Requests.Clear();
            rig.Waiter.Timeouts.Clear();
            rig.Waiter.ThenFindsTheReaderEmpty();

            await rig.ContinueAsync();

            Assert.Equal(WorkflowState.ReadOutFailed, rig.Now.State);
            Assert.Equal(WorkflowFailureCodes.CardMissing, rig.Now.Failure!.Code);
            Assert.Empty(rig.Runner.Requests);
            Assert.Equal(new TimeSpan?[] { TimeSpan.Zero }, rig.Waiter.Timeouts);

            // A reader that gives no clear answer is told apart from an empty one.
            rig.Waiter.ThenGetsNoClearAnswer();
            Assert.True(rig.Engine.Retry());
            await rig.Settled();
            Assert.Equal(WorkflowState.ReadOutFailed, rig.Now.State);
            Assert.Equal(WorkflowFailureCodes.CardUnclear, rig.Now.Failure!.Code);
            Assert.Empty(rig.Runner.Requests);

            // With the chip back, the same retry runs the project.
            Assert.True(rig.Engine.Retry());
            await rig.Settled();
            Assert.Equal(WorkflowState.Reviewing, rig.Now.State);
            Assert.Single(rig.Runner.Requests);
        });

        [Fact]
        public Task AReadOutThatCouldNotStart_CanAlsoBeFinishedWithTheSecondReport() => WorkflowEngineTests.Sta(async rig =>
        {
            await rig.ReachAsync(WorkflowState.Programming);
            rig.Waiter.ThenFindsTheReaderEmpty();
            await rig.ContinueAsync();
            Assert.Equal(WorkflowState.ReadOutFailed, rig.Now.State);

            Assert.True(rig.Engine.FinishWithoutReadOut());
            await rig.Settled();

            Assert.Equal(WorkflowState.Completed, rig.Now.State);
            Assert.Equal(rig.Paths.PathOf(ReportFile.Step2), rig.Reports.Locked.Single().Source);
        });

        [Fact]
        public Task AReadOutThatSaysItWasCancelled_EndsCancelled_WhereItCame() => WorkflowEngineTests.Sta(async rig =>
        {
            await rig.ReachAsync(WorkflowState.Programming);
            rig.Runner.Answer = _ => new CheckRunResult(CheckRunStatus.Cancelled, null, null, null, null);

            await rig.ContinueAsync();

            Assert.Equal(WorkflowState.Cancelled, rig.Now.State);
            Assert.Equal(WorkflowState.ReadingOut, rig.Now.CancelledAt);
            Assert.Null(rig.Now.ReadOut);
        });

        [Fact]
        public Task FinishWithoutReadOut_IsAChoice_AndFinishesWithTheSecondReport() => WorkflowEngineTests.Sta(async rig =>
        {
            await rig.ReachAsync(WorkflowState.ReadOutFailed);
            Assert.False(rig.Engine.Finish());   // the review's button is not the failed read-out's

            Assert.True(rig.Engine.FinishWithoutReadOut());
            await rig.Settled();

            Assert.Equal(WorkflowState.Completed, rig.Now.State);
            Assert.Equal(rig.Paths.PathOf(ReportFile.Step2), rig.Reports.Locked.Single().Source);
            Assert.Equal(WorkflowEngineTests.Rig.OrderId, rig.Store.Finalized.Single().Id);
        });

        [Fact]
        public Task TheFinishWithoutReadOut_IsNotOfferedAtTheReview() => WorkflowEngineTests.Sta(async rig =>
        {
            await rig.ReachAsync(WorkflowState.Reviewing);

            Assert.False(rig.Engine.FinishWithoutReadOut());
            Assert.Equal(WorkflowState.Reviewing, rig.Now.State);
        });

        [Fact]
        public Task Answers_ThatAreNotGood_AreRefusedBeforeAnythingIsWritten() => WorkflowEngineTests.Sta(async rig =>
        {
            await rig.ReachAsync(WorkflowState.Programming);
            var before = rig.Calls.Count;
            var template = rig.Template;

            Assert.Throws<ArgumentNullException>(() => rig.Engine.ContinueProgramming(null!, WorkflowEngineTests.Rig.GoodAnswers));
            Assert.Throws<ArgumentNullException>(() => rig.Engine.ContinueProgramming(template, null!));

            // A question that is not answered.
            Assert.False(rig.Engine.ContinueProgramming(template, new LsmAnswers { ProgrammingSucceeded = true }));
            Assert.Equal(WorkflowState.Programming, rig.Now.State);
            Assert.False(rig.Now.IsBusy);
            Assert.Equal("answers.functionTest.missing", rig.Now.Failure!.Code);

            // A template that does not fit the chip.
            var foreign = new LsmTemplate("Foreign", "Foreign", TemplateFamily.Classic, 1, 1);
            Assert.False(rig.Engine.ContinueProgramming(foreign, WorkflowEngineTests.Rig.GoodAnswers));
            Assert.Equal(WorkflowState.Programming, rig.Now.State);
            Assert.NotNull(rig.Now.Failure);
            Assert.NotEqual("answers.functionTest.missing", rig.Now.Failure!.Code);

            Assert.Equal(before, rig.Calls.Count);   // nothing was written, nothing was touched, nothing was run
            Assert.Empty(rig.Reports.Writes);

            // The good answers go through, and the note of the refusal is gone.
            Assert.True(rig.Engine.ContinueProgramming(template, WorkflowEngineTests.Rig.GoodAnswers));
            Assert.Null(rig.Now.Failure);
            await rig.Settled();
            Assert.Equal(WorkflowState.Reviewing, rig.Now.State);
        });

        [Fact]
        public Task ATemplate_ThatOnlyHasTheNameOfAnAvailableOne_WritesTheFieldsOfTheAvailableOne() => WorkflowEngineTests.Sta(async rig =>
        {
            await rig.ReachAsync(WorkflowState.Programming);
            var available = rig.Template;
            var other = available.Family == TemplateFamily.Classic ? TemplateFamily.Desfire : TemplateFamily.Classic;
            var lookalike = new LsmTemplate(available.Name, "Another text", other, 1, 1);
            var answers = WorkflowEngineTests.Rig.GoodAnswers with { SectorsUsed = "1-3" };

            // The two would write different fields, or this test proves nothing.
            Assert.NotEqual(answers.ToFields(available).Value.OrderBy(p => p.Key), answers.ToFields(lookalike).Value.OrderBy(p => p.Key));

            Assert.True(rig.Engine.ContinueProgramming(lookalike, answers));
            await rig.Settled();

            Assert.Equal(answers.ToFields(available).Value.OrderBy(p => p.Key), rig.Reports.Writes.Single().Fields.OrderBy(p => p.Key));
        });

        [Fact]
        public Task Continuing_IsRefused_UntilTheChipWasPutBack() => WorkflowEngineTests.Sta(async rig =>
        {
            await rig.AssessedAsync(ChipVerdict.Programmable);
            rig.Waiter.ThenTheMoveTimesOut(CardMoveStage.WaitingForRemoval);
            Assert.True(rig.Engine.BeginProgramming());
            await rig.Settled();

            Assert.False(rig.Now.Controls.HasFlag(WorkflowControls.ContinueProgramming));
            Assert.False(rig.Engine.ContinueProgramming(rig.Template, WorkflowEngineTests.Rig.GoodAnswers));
            Assert.Equal(WorkflowState.Programming, rig.Now.State);
            Assert.Empty(rig.Reports.Writes);
        });

        [Fact]
        public Task ACancel_WhileTheReportIsWritten_IsLookedAtBeforeTheDateIsSet() => WorkflowEngineTests.Sta(async rig =>
        {
            await rig.ReachAsync(WorkflowState.Programming);
            var gate = new TaskCompletionSource<bool>();
            rig.Reports.WriteGate = gate;
            rig.Calls.Clear();
            rig.Runner.Requests.Clear();
            Assert.True(rig.Engine.ContinueProgramming(rig.Template, WorkflowEngineTests.Rig.GoodAnswers));

            try
            {
                await rig.UntilAsync(() => rig.Calls.Contains("write"));

                // While the second report is written only Cancel is accepted.
                Assert.Equal(WorkflowState.WritingReport, rig.Now.State);
                Assert.Equal(WorkflowControls.Cancel, rig.Now.Controls);
                Assert.False(rig.Engine.Retry());
                Assert.False(rig.Engine.Finish());
                Assert.False(rig.Engine.FinishWithoutReadOut());
                Assert.False(rig.Engine.BeginProgramming());
                Assert.False(rig.Engine.ContinueProgramming(rig.Template, WorkflowEngineTests.Rig.GoodAnswers));
                Assert.True(rig.Engine.Cancel());
            }
            finally
            {
                gate.SetResult(true);   // the write that had begun ends all the same
            }

            await rig.Settled();

            Assert.Equal(WorkflowState.Cancelled, rig.Now.State);
            Assert.Equal(WorkflowState.WritingReport, rig.Now.CancelledAt);
            Assert.Empty(rig.Store.Touched);
            Assert.Empty(rig.Runner.Requests);
        });

        [Fact]
        public Task ACancel_WinsOverAFailureThatTheWriteBringsBack() => WorkflowEngineTests.Sta(async rig =>
        {
            await rig.ReachAsync(WorkflowState.Programming);
            var gate = new TaskCompletionSource<bool>();
            rig.Reports.WriteGate = gate;
            rig.Reports.WriteAnswer = Result.Fail(new Failure(FailureKind.Io, "report.unreadable", "The report cannot be read."));
            rig.Calls.Clear();
            Assert.True(rig.Engine.ContinueProgramming(rig.Template, WorkflowEngineTests.Rig.GoodAnswers));
            await rig.UntilAsync(() => rig.Calls.Contains("write"));

            Assert.True(rig.Engine.Cancel());
            gate.SetResult(true);
            await rig.Settled();

            // The cancel was asked for first: the state is not a failed one, and the failure is not shown.
            Assert.Equal(WorkflowState.Cancelled, rig.Now.State);
            Assert.Equal(WorkflowState.WritingReport, rig.Now.CancelledAt);
            Assert.Null(rig.Now.Failure);
            Assert.Empty(rig.Store.Touched);
        });

        [Fact]
        public Task ACancel_WinsOverAFailureThatTheReadOutBringsBack() => WorkflowEngineTests.Sta(async rig =>
        {
            await rig.ReachAsync(WorkflowState.Programming);
            var gate = new TaskCompletionSource<bool>();
            rig.Runner.Gate = gate;
            rig.Runner.Answer = _ => new CheckRunResult(CheckRunStatus.Failed, new Failure(FailureKind.Unexpected, CheckFailureCodes.ReportMissing, "No report."), null, null, null);
            rig.Runner.Requests.Clear();
            Assert.True(rig.Engine.ContinueProgramming(rig.Template, WorkflowEngineTests.Rig.GoodAnswers));
            await rig.UntilAsync(() => rig.Runner.Requests.Count == 1);

            Assert.True(rig.Engine.Cancel());
            gate.SetResult(true);
            await rig.Settled();

            Assert.Equal(WorkflowState.Cancelled, rig.Now.State);
            Assert.Equal(WorkflowState.ReadingOut, rig.Now.CancelledAt);
            Assert.Null(rig.Now.Failure);
            Assert.Null(rig.Now.ReadOut);
        });

        [Fact]
        public Task ACancel_WhileTheDateIsSet_EndsCancelled_AndTheReadOutDoesNotRun() => WorkflowEngineTests.Sta(async rig =>
        {
            await rig.ReachAsync(WorkflowState.Programming);
            var gate = new TaskCompletionSource<bool>();
            rig.Store.TouchGate = gate;
            rig.Runner.Requests.Clear();
            Assert.True(rig.Engine.ContinueProgramming(rig.Template, WorkflowEngineTests.Rig.GoodAnswers));
            await rig.UntilAsync(() => rig.Store.Touched.Count == 1);
            Assert.Equal(WorkflowState.WritingReport, rig.Now.State);

            Assert.True(rig.Engine.Cancel());
            gate.SetResult(true);   // the call that had begun answers all the same, and the answer is thrown away
            await rig.Settled();

            Assert.Equal(WorkflowState.Cancelled, rig.Now.State);
            Assert.Equal(WorkflowState.WritingReport, rig.Now.CancelledAt);
            Assert.Empty(rig.Runner.Requests);
        });

        [Fact]
        public Task ACancel_WhileTheMemoryIsRead_NeverEndsInTheReview() => WorkflowEngineTests.Sta(async rig =>
        {
            await rig.ReachAsync(WorkflowState.Programming);
            var gate = new TaskCompletionSource<bool>();
            rig.Runner.Gate = gate;
            rig.Runner.Requests.Clear();
            Assert.True(rig.Engine.ContinueProgramming(rig.Template, WorkflowEngineTests.Rig.GoodAnswers));
            await rig.UntilAsync(() => rig.Runner.Requests.Count == 1);
            Assert.Equal(WorkflowState.ReadingOut, rig.Now.State);
            Assert.Equal(WorkflowControls.Cancel, rig.Now.Controls);

            // The host stops a run only between two tasks: a held run ignores the token, and what it brings back is thrown away.
            Assert.True(rig.Engine.Cancel());
            gate.SetResult(true);
            await rig.Settled();

            Assert.Equal(WorkflowState.Cancelled, rig.Now.State);
            Assert.Equal(WorkflowState.ReadingOut, rig.Now.CancelledAt);
            Assert.Null(rig.Now.ReadOut);
        });

        [Theory]
        [InlineData("Programming", false)]
        [InlineData("ProgrammingFailed", true)]
        [InlineData("ReadOutFailed", true)]
        [InlineData("Reviewing", false)]
        public Task ACancel_WhereNothingRuns_EndsCancelled_AndKeepsTheFailureOfAFailedStep(string name, bool keepsFailure) => WorkflowEngineTests.Sta(async rig =>
        {
            var state = Enum.Parse<WorkflowState>(name);
            await rig.ReachAsync(state);
            var failure = rig.Now.Failure;
            Assert.Equal(keepsFailure, failure != null);

            Assert.True(rig.Engine.Cancel());

            Assert.Equal(WorkflowState.Cancelled, rig.Now.State);
            Assert.Equal(state, rig.Now.CancelledAt);
            Assert.Same(failure, rig.Now.Failure);
        });

        [Fact]
        public Task Back_AfterTheReview_LeavesNothingOfTheOrderBehind() => WorkflowEngineTests.Sta(async rig =>
        {
            await rig.ReachAsync(WorkflowState.Reviewing);
            Assert.NotNull(rig.Now.ReadOut);
            Assert.NotNull(rig.Now.Move);

            Assert.True(rig.Engine.Cancel());
            Assert.True(rig.Engine.Back());

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
