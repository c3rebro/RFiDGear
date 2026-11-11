using System;
using System.Linq;
using System.Threading.Tasks;

using RFiDGear.Contracts;
using RFiDGear.ExtensionLibraries.CardCheck;
using RFiDGear.Extensions.CardCheckAssistant.Services;
using RFiDGear.Extensions.CardCheckAssistant.Workflow;

using Xunit;

using Keys = RFiDGear.Extensions.CardCheckAssistant.Localization.ExtensionStrings.Keys;

namespace RFiDGear.Tests.CardCheckAssistant
{
    /// <summary>The page from the read-out on: its rows, the questions it asks, and the viewer it opens.</summary>
    public sealed class GuidedReviewTests
    {
        private const string ReportPath = @"C:\reports\J1\J1-1.step3.pdf";

        private static ExecResult Rows(int count)
        {
            var now = DateTimeOffset.UtcNow;

            return new ExecResult(
                "run",
                ExecutionOutcome.Failure,
                count,
                1,
                now,
                now,
                Enumerable.Range(0, count).Select(i => new TaskOutcome(i, (i + 1).ToString(), "Classic", i != 1, i != 1 ? "NoError" : "AuthFailure")).ToList());
        }

        private static CheckRunResult ReadOut(int rows = 2, string path = ReportPath) =>
            new CheckRunResult(CheckRunStatus.Finished, null, Rows(rows), new ReportAnalysis { ChipType = "DESFire EV1" }, path);

        private static Func<WorkflowSnapshot, WorkflowSnapshot> InReview(string path = ReportPath) =>
            s => s with { Order = new WorkflowOrder("A", "JA", "1"), ReadOut = ReadOut(path: path) };

        [Fact]
        public Task TheTaskRows_FollowTheReadOut_FromTheReadOutOn_AndTheFirstChecksNeverShowUnderIt() => GuidedPage.Run(async page =>
        {
            var vm = page.Vm;
            var check = new CheckRunResult(CheckRunStatus.Finished, null, Rows(3), null, "first.pdf");

            await page.PushAsync(WorkflowState.Assessing, s => s with { Check = check });
            Assert.Equal(3, vm.Rows.Count);
            Assert.Equal(vm.Strings.Format(Keys.SummaryFormat, 1, 3), vm.SummaryText);

            // The read-out has not run yet (or failed before it could): no rows, not those of the first check.
            await page.PushAsync(WorkflowState.ReadingOut, s => s with { Check = check, ReadOut = null }, busy: true);
            Assert.Empty(vm.Rows);
            Assert.False(vm.HasRows);
            Assert.Equal(string.Empty, vm.SummaryText);

            await page.PushAsync(WorkflowState.ReadOutFailed, s => s with { Check = check, ReadOut = ReadOut(5) });
            Assert.Equal(5, vm.Rows.Count);
            Assert.Equal(vm.Strings.Format(Keys.SummaryFormat, 1, 5), vm.SummaryText);

            await page.PushAsync(WorkflowState.Assessing, s => s with { Check = check });
            Assert.Equal(3, vm.Rows.Count);
        });

        [Fact]
        public Task FinishWithoutReadOut_AsksFirst_AndOnlyYesFinishes() => GuidedPage.Run(async page =>
        {
            var vm = page.Vm;
            await page.PushAsync(WorkflowState.ReadOutFailed);
            Assert.True(vm.ShowFinishWithoutReadOut);
            Assert.True(vm.FinishWithoutReadOutCommand.CanExecute(null));

            page.Close.Answer = false;
            vm.FinishWithoutReadOutCommand.Execute(null);

            Assert.DoesNotContain("FinishWithoutReadOut", page.Workflow.Calls);
            var asked = Assert.Single(page.Close.Asked);
            Assert.Equal(vm.Strings[Keys.GuidedAskFinishWithoutTitle], asked.Title);
            Assert.Equal(vm.Strings[Keys.GuidedAskFinishWithout], asked.Text);

            page.Close.Answer = true;
            vm.FinishWithoutReadOutCommand.Execute(null);
            Assert.Contains("FinishWithoutReadOut", page.Workflow.Calls);

            // Nowhere else is the button shown.
            await page.PushAsync(WorkflowState.Reviewing);
            Assert.False(vm.ShowFinishWithoutReadOut);
            Assert.False(vm.FinishWithoutReadOutCommand.CanExecute(null));
        });

        [Theory]
        [InlineData("Programming", false, true)]
        [InlineData("ProgrammingFailed", false, true)]
        [InlineData("ReadOutFailed", false, true)]
        [InlineData("Reviewing", false, true)]
        [InlineData("WritingReport", true, true)]
        [InlineData("ReadingOut", true, true)]
        [InlineData("Assessing", false, false)]
        [InlineData("WaitingForCard", false, false)]
        [InlineData("Checking", true, false)]
        public Task LeavingTheOrderOpen_IsAskedAbout_FromTheProgrammingOn_AndNotBefore(string name, bool busy, bool asks) => GuidedPage.Run(async page =>
        {
            var state = Enum.Parse<WorkflowState>(name);
            await page.PushAsync(state, null, busy);
            var snapshot = page.Workflow.Current;

            page.Close.Answer = false;
            page.Vm.CancelCommand.Execute(null);

            Assert.Equal(asks, page.Close.Asked.Count == 1);
            Assert.Equal(!asks, page.Workflow.Calls.Contains("Cancel"));
            if (asks)
            {
                Assert.Equal((page.Vm.Strings[Keys.GuidedCancelTitle], GuidedTextsOf(page, snapshot)), page.Close.Asked.Single());

                // Yes leaves it.
                page.Close.Answer = true;
                page.Vm.CancelCommand.Execute(null);
                Assert.Contains("Cancel", page.Workflow.Calls);
            }
        });

        private static string GuidedTextsOf(GuidedPage page, WorkflowSnapshot snapshot) =>
            RFiDGear.Extensions.CardCheckAssistant.ViewModels.GuidedTexts.Close(snapshot, page.Vm.Strings);

        [Fact]
        public Task TheReviewShowsThePath_AndTheViewerIsAskedForTheReportAndForItsFolder() => GuidedPage.Run(async page =>
        {
            var vm = page.Vm;
            await page.PushAsync(WorkflowState.Reviewing, InReview());

            Assert.True(vm.IsReviewStep);
            Assert.Equal(vm.Strings.Format(Keys.GuidedReadOutPathFormat, ReportPath), vm.ReadOutPathText);
            Assert.Equal(vm.Strings[Keys.GuidedReviewCommit], vm.ReviewCommitText);
            Assert.True(vm.OpenReportCommand.CanExecute(null));
            Assert.True(vm.OpenFolderCommand.CanExecute(null));

            await vm.OpenReportCommand.ExecuteAsync();
            await vm.OpenFolderCommand.ExecuteAsync();

            Assert.Equal(new[] { ReportPath, @"C:\reports\J1" }, page.Close.Opened);
            Assert.Empty(vm.Messages);
        }, WorkflowState.Idle);

        [Fact]
        public Task WithoutAReport_OrOutsideTheReview_NothingCanBeOpened() => GuidedPage.Run(async page =>
        {
            var vm = page.Vm;
            await page.PushAsync(WorkflowState.Reviewing, s => s with { ReadOut = null });
            Assert.False(vm.OpenReportCommand.CanExecute(null));
            Assert.False(vm.OpenFolderCommand.CanExecute(null));

            await page.PushAsync(WorkflowState.Completing, InReview(), busy: true);
            Assert.False(vm.OpenReportCommand.CanExecute(null));
            Assert.False(vm.OpenFolderCommand.CanExecute(null));
        });

        [Fact]
        public Task WhileTheViewerIsAsked_BothButtonsWait_AndASecondClickOnEitherDoesNothing() => GuidedPage.Run(async page =>
        {
            var vm = page.Vm;
            await page.PushAsync(WorkflowState.Reviewing, InReview());
            var answer = new TaskCompletionSource<Failure?>();
            page.Close.NextOpen = answer;

            var announced = 0;
            vm.OpenFolderCommand.CanExecuteChanged += (_, _) => announced++;
            var first = vm.OpenReportCommand.ExecuteAsync();
            Assert.False(vm.OpenReportCommand.CanExecute(null));
            Assert.False(vm.OpenFolderCommand.CanExecute(null));
            Assert.True(announced > 0, "The other button was not told.");
            await vm.OpenReportCommand.ExecuteAsync();
            await vm.OpenFolderCommand.ExecuteAsync();

            Assert.Single(page.Close.Opened);

            // The report can still be checked while the viewer starts.
            Assert.True(vm.FinishCommand.CanExecute(null));

            answer.SetResult(null);
            await first;
            Assert.True(vm.OpenReportCommand.CanExecute(null));
            Assert.True(vm.OpenFolderCommand.CanExecute(null));
        });

        [Fact]
        public Task AViewerThatRefuses_IsToldWithItsTextAndCode_AndItGoesWhenTheReviewIsLeft() => GuidedPage.Run(async page =>
        {
            var vm = page.Vm;
            await page.PushAsync(WorkflowState.Reviewing, InReview());
            page.Close.NextOpen = new TaskCompletionSource<Failure?>();
            page.Close.NextOpen.SetResult(new Failure(FailureKind.NotFound, OpenerFailureCodes.NoViewer, "No program."));

            await vm.OpenReportCommand.ExecuteAsync();

            var message = Assert.Single(vm.Messages);
            Assert.Equal(OpenerFailureCodes.NoViewer, message.Code);
            Assert.Equal(vm.Strings[Keys.FailOpenNoViewer], message.Text);

            // The next click starts clean.
            page.Close.NextOpen = null;
            await vm.OpenFolderCommand.ExecuteAsync();
            Assert.Empty(vm.Messages);

            // And a refusal of an earlier click does not outlive the review.
            page.Close.NextOpen = new TaskCompletionSource<Failure?>();
            page.Close.NextOpen.SetResult(new Failure(FailureKind.NotFound, OpenerFailureCodes.NotFound, "Gone."));
            await vm.OpenReportCommand.ExecuteAsync();
            Assert.Single(vm.Messages);
            await page.PushAsync(WorkflowState.Completing, InReview(), busy: true);
            Assert.Empty(vm.Messages);
            await page.PushAsync(WorkflowState.Reviewing, InReview());
            Assert.Empty(vm.Messages);
        });

        [Fact]
        public Task AnAnswerOfTheViewer_ThatComesAfterTheReviewWasLeft_ShowsNothing() => GuidedPage.Run(async page =>
        {
            var vm = page.Vm;
            await page.PushAsync(WorkflowState.Reviewing, InReview());
            var answer = new TaskCompletionSource<Failure?>();
            page.Close.NextOpen = answer;
            var pending = vm.OpenReportCommand.ExecuteAsync();

            await page.PushAsync(WorkflowState.Completing, InReview(), busy: true);
            answer.SetResult(new Failure(FailureKind.Io, OpenerFailureCodes.StartFailed, "Failed."));
            await pending;

            Assert.Empty(vm.Messages);
            Assert.True(vm.IsFinishStep);

            // Had the refusal been kept, it would be shown whenever the review is shown again.
            await page.PushAsync(WorkflowState.Reviewing, InReview());
            Assert.Empty(vm.Messages);
        });

        [Fact]
        public Task AnAnswerOfAnEarlierReview_DoesNotShowInTheNextOne_AndDoesNotHoldTheOtherButtonUp() => GuidedPage.Run(async page =>
        {
            var vm = page.Vm;
            await page.PushAsync(WorkflowState.Reviewing, InReview());
            var answer = new TaskCompletionSource<Failure?>();
            page.Close.NextOpen = answer;
            var pending = vm.OpenReportCommand.ExecuteAsync();

            // The order is finished and the next one reaches its review while the first viewer is still being asked.
            await page.PushAsync(WorkflowState.Completing, InReview(), busy: true);
            await page.PushAsync(WorkflowState.Reviewing, InReview(@"C:\reports\J2\J2-1.step3.pdf"));

            // The other button is free at once; the one whose request is still waiting is free when that request ends.
            Assert.True(vm.OpenFolderCommand.CanExecute(null));
            Assert.False(vm.OpenReportCommand.CanExecute(null));

            answer.SetResult(new Failure(FailureKind.Io, OpenerFailureCodes.StartFailed, "Failed."));
            await pending;

            Assert.Empty(vm.Messages);
            Assert.True(vm.OpenReportCommand.CanExecute(null));
        });

        [Fact]
        public Task AViewerThatFailsInAWayNobodyExpected_IsToldByTheCodeThatHasAText() => GuidedPage.Run(async page =>
        {
            var vm = page.Vm;
            await page.PushAsync(WorkflowState.Reviewing, InReview());
            var answer = new TaskCompletionSource<Failure?>();
            answer.SetException(new InvalidOperationException("a secret path of a share"));
            page.Close.NextOpen = answer;

            await vm.OpenReportCommand.ExecuteAsync();

            var message = Assert.Single(vm.Messages);
            Assert.Equal(OpenerFailureCodes.StartFailed, message.Code);
            Assert.Equal(vm.Strings[Keys.FailOpenFailed], message.Text);
            Assert.DoesNotContain("secret", message.Text, StringComparison.Ordinal);
            Assert.True(vm.OpenReportCommand.CanExecute(null));
        });

        [Fact]
        public Task DisposingThePage_WhileTheViewerIsAsked_EndsQuietly() => GuidedPage.Run(async page =>
        {
            var vm = page.Vm;
            await page.PushAsync(WorkflowState.Reviewing, InReview());
            page.Close.NextOpen = new TaskCompletionSource<Failure?>();
            var pending = vm.OpenReportCommand.ExecuteAsync();

            vm.Dispose();
            await pending;

            Assert.Empty(vm.Messages);
        });

        [Fact]
        public Task EveryCommand_FollowsItsControl_AndTellsItWhenItChanges() => GuidedPage.Run(async page =>
        {
            var vm = page.Vm;
            var cases = new (WorkflowState State, WorkflowControls Flag, System.Windows.Input.ICommand Command)[]
            {
                (WorkflowState.Idle, WorkflowControls.Refresh, vm.RefreshCommand),
                (WorkflowState.ConfirmingReplace, WorkflowControls.ConfirmReplace, vm.ConfirmReplaceCommand),
                (WorkflowState.ConfirmingReplace, WorkflowControls.DeclineReplace, vm.KeepReportsCommand),
                (WorkflowState.WaitingForCard, WorkflowControls.StartCheck, vm.StartCheckCommand),
                (WorkflowState.Reviewing, WorkflowControls.Finish, vm.FinishCommand),
                (WorkflowState.Assessing, WorkflowControls.BeginProgramming, vm.BeginCommand),
                (WorkflowState.ReadOutFailed, WorkflowControls.FinishWithoutReadOut, vm.FinishWithoutReadOutCommand),
                (WorkflowState.RunFailed, WorkflowControls.Retry, vm.RetryCommand),
                (WorkflowState.Reviewing, WorkflowControls.Cancel, vm.CancelCommand),
                (WorkflowState.Completed, WorkflowControls.Back, vm.BackCommand),
            };

            foreach (var (state, flag, command) in cases)
            {
                var changed = 0;
                command.CanExecuteChanged += (_, _) => changed++;

                await page.PushAsync(state, s => s with { Controls = flag });
                Assert.True(command.CanExecute(null), flag + " in " + state);
                Assert.True(changed > 0, flag + " was not announced");

                changed = 0;
                await page.PushAsync(state, s => s with { Controls = WorkflowControls.None });
                Assert.False(command.CanExecute(null), flag + " in " + state);
                Assert.True(changed > 0, flag + " was not announced when taken away");
            }
        });
    }
}
