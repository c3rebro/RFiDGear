using System;
using System.Linq;
using System.Threading.Tasks;

using RFiDGear.ExtensionLibraries.CardCheck;
using RFiDGear.Extensions.CardCheckAssistant.ViewModels;
using RFiDGear.Extensions.CardCheckAssistant.Workflow;

using Xunit;

using Keys = RFiDGear.Extensions.CardCheckAssistant.Localization.ExtensionStrings.Keys;

namespace RFiDGear.Tests.CardCheckAssistant
{
    /// <summary>The way into the programming: the verdict of a chip that can be programmed, and the texts around it.</summary>
    public sealed class GuidedBeginTests
    {
        private static Func<WorkflowSnapshot, WorkflowSnapshot> Verdict(ChipVerdict verdict, Failure? problem = null) => s => s with
        {
            Order = new WorkflowOrder("A", "JA", "1"),
            Selection = new TemplateSelection(verdict, FreeMemory.Unknown, Array.Empty<LsmTemplate>(), null),
            ProgrammingProblem = problem,
        };

        [Fact]
        public Task ForAChipThatCanBeProgrammed_BeginReplacesFinish_AndIsAskedForNoQuestion() => GuidedPage.Run(async page =>
        {
            var vm = page.Vm;
            await page.PushAsync(WorkflowState.Assessing, Verdict(ChipVerdict.Programmable));

            Assert.True(vm.ShowBegin);
            Assert.False(vm.ShowFinish);
            Assert.True(vm.BeginCommand.CanExecute(null));
            Assert.Equal(vm.Strings[Keys.GuidedVerdictProgrammable], vm.VerdictText);
            Assert.Empty(vm.Messages);

            vm.BeginCommand.Execute(null);

            Assert.Contains("BeginProgramming", page.Workflow.Calls);
            Assert.Empty(page.Close.Asked);                                       // it only starts to wait for the chip
        });

        [Theory]
        [InlineData(ChipVerdict.NotSupported)]
        [InlineData(ChipVerdict.SupportedOnly)]
        [InlineData(ChipVerdict.Indeterminate)]
        public Task ForAnyOtherVerdict_FinishIsThereAndBeginIsNot(ChipVerdict verdict) => GuidedPage.Run(async page =>
        {
            await page.PushAsync(WorkflowState.Assessing, Verdict(verdict));

            Assert.False(page.Vm.ShowBegin);
            Assert.True(page.Vm.ShowFinish);
            Assert.True(page.Vm.FinishCommand.CanExecute(null));
            Assert.False(page.Vm.BeginCommand.CanExecute(null));
        });

        [Fact]
        public Task WhenTheReadOutProjectCannotBeUsed_TheVerdictSaysSo_AndTheReasonIsGiven_AndBeginIsNotOffered() => GuidedPage.Run(async page =>
        {
            var vm = page.Vm;
            var problem = new Failure(FailureKind.NotConfigured, WorkflowFailureCodes.MemoryProjectInvalid, "No project.");
            await page.PushAsync(WorkflowState.Assessing, Verdict(ChipVerdict.Programmable, problem));

            Assert.False(vm.BeginCommand.CanExecute(null));
            Assert.False(vm.FinishCommand.CanExecute(null));                      // a chip that can be programmed is not finished here
            Assert.Equal(vm.Strings[Keys.GuidedVerdictProgrammableBlocked], vm.VerdictText);

            // Nothing went wrong, but the operator has to act: the reason is told with its text and code.
            var message = Assert.Single(vm.Messages);
            Assert.Equal(WorkflowFailureCodes.MemoryProjectInvalid, message.Code);
            Assert.Equal(vm.Strings[Keys.FailMemoryProjectInvalid], message.Text);

            // The reason belongs to the verdict: it is not shown on the steps after.
            await page.PushAsync(WorkflowState.Cancelled, s => Verdict(ChipVerdict.Programmable, problem)(s) with { CancelledAt = WorkflowState.Assessing });
            Assert.Empty(vm.Messages);
        });

        [Fact]
        public Task TheQuestionAboutEarlierReports_SaysTheChipMayBeProgrammedAlready_WhenTheyAreThoseOfAProgrammedChip() => GuidedPage.Run(async page =>
        {
            var vm = page.Vm;
            await page.PushAsync(WorkflowState.ConfirmingReplace, s => s with { ExistingReports = new[] { ReportFile.Step1, ReportFile.Final } });
            Assert.DoesNotContain(vm.Strings[Keys.GuidedReplaceProgrammed], vm.ReplaceText, StringComparison.Ordinal);

            foreach (var programmed in new[] { ReportFile.Step2, ReportFile.Step3 })
            {
                await page.PushAsync(WorkflowState.ConfirmingReplace, s => s with { ExistingReports = new[] { ReportFile.Step1, programmed } });
                Assert.EndsWith(vm.Strings[Keys.GuidedReplaceProgrammed], vm.ReplaceText, StringComparison.Ordinal);
            }
        });

        [Fact]
        public void TheEndAfterALeaveOpen_NamesTheNewestReportOnlyWhereItIsKnown_AndSaysWhenTheDateMayHaveBeenSet()
        {
            var strings = new RFiDGear.Extensions.CardCheckAssistant.Localization.LocalizedStrings("en-US");
            var first = new CheckRunResult(CheckRunStatus.Finished, null, null, null, @"C:\r\J1-1.step1.pdf");
            var readOut = new CheckRunResult(CheckRunStatus.Finished, null, null, null, @"C:\r\J1-1.step3.pdf");
            var failedRun = new CheckRunResult(CheckRunStatus.Failed, null, null, null, @"C:\r\J1-1.step3.pdf");   // the path the run was going to write
            string End(WorkflowState at, CheckRunResult? readOutResult, CheckRunResult? check = null) => GuidedTexts.End(
                new WorkflowSnapshot { State = WorkflowState.Cancelled, CancelledAt = at, Check = check ?? first, ReadOut = readOutResult },
                strings);
            string Folder() => strings.Format(Keys.GuidedEndReportsFolderFormat, @"C:\r");

            // While the chip is away nothing of the order was touched but the reports of the first check: the first one is the newest.
            var waiting = End(WorkflowState.Programming, null);
            Assert.StartsWith(strings[Keys.GuidedEndCancelledProgrammed], waiting, StringComparison.Ordinal);
            Assert.DoesNotContain(strings[Keys.GuidedEndDateTouched], waiting, StringComparison.Ordinal);
            Assert.EndsWith(strings.Format(Keys.GuidedEndNewestReportFormat, first.ReportPath!), waiting, StringComparison.Ordinal);

            // At the review the read-out has finished: its report is the newest, the date may have been set.
            var reviewing = End(WorkflowState.Reviewing, readOut);
            Assert.StartsWith(strings[Keys.GuidedEndCancelledProgrammed], reviewing, StringComparison.Ordinal);
            Assert.Contains(strings[Keys.GuidedEndDateTouched], reviewing, StringComparison.Ordinal);
            Assert.EndsWith(strings.Format(Keys.GuidedEndNewestReportFormat, readOut.ReportPath!), reviewing, StringComparison.Ordinal);

            // In between the engine has no read-out result (writing, a failed write or touch, the read-out under way or the chip
            // gone) or a failed run's: the second report may or may not be there and the third may never have been written. The
            // first report is not the newest then and the third is not known to exist, so only the folder is told.
            var between = new[]
            {
                (WorkflowState.WritingReport, (CheckRunResult?)null),
                (WorkflowState.ProgrammingFailed, null),
                (WorkflowState.ReadingOut, null),
                (WorkflowState.ReadOutFailed, null),
                (WorkflowState.ReadOutFailed, failedRun),
            };
            foreach (var (at, result) in between)
            {
                var text = End(at, result);
                Assert.StartsWith(strings[Keys.GuidedEndCancelledProgrammed], text, StringComparison.Ordinal);
                Assert.Contains(strings[Keys.GuidedEndDateTouched], text, StringComparison.Ordinal);
                Assert.EndsWith(Folder(), text, StringComparison.Ordinal);
                Assert.DoesNotContain(".step1.pdf", text, StringComparison.Ordinal);
                Assert.DoesNotContain(".step3.pdf", text, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void TheEndAfterALeaveOpen_NamesNothingWhenNoReportIsKnown()
        {
            var strings = new RFiDGear.Extensions.CardCheckAssistant.Localization.LocalizedStrings("en-US");
            var readOut = new CheckRunResult(CheckRunStatus.Finished, null, null, null, @"C:\r\J1-1.step3.pdf");
            var withoutPath = new CheckRunResult(CheckRunStatus.Finished, null, null, null, "  ");
            string End(WorkflowState at, CheckRunResult? check, CheckRunResult? readOutResult = null) => GuidedTexts.End(
                new WorkflowSnapshot { State = WorkflowState.Cancelled, CancelledAt = at, Check = check, ReadOut = readOutResult },
                strings);
            var programmed = strings[Keys.GuidedEndCancelledProgrammed];
            var touched = programmed + " " + strings[Keys.GuidedEndDateTouched];

            // No first report (or one without a path): nothing is named, and nothing is half formatted.
            Assert.Equal(programmed, End(WorkflowState.Programming, null));
            Assert.Equal(programmed, End(WorkflowState.Programming, withoutPath));
            Assert.Equal(touched, End(WorkflowState.ReadingOut, null));
            Assert.Equal(touched, End(WorkflowState.ReadingOut, withoutPath));

            // A review whose read-out has no path names no file; the folder of the first report is told instead. Without a first
            // report either, nothing is, and a read-out with a path is named.
            var first = new CheckRunResult(CheckRunStatus.Finished, null, null, null, @"C:\r\J1-1.step1.pdf");
            var noPath = new CheckRunResult(CheckRunStatus.Finished, null, null, null, null);
            Assert.EndsWith(strings.Format(Keys.GuidedEndReportsFolderFormat, @"C:\r"), End(WorkflowState.Reviewing, first, noPath), StringComparison.Ordinal);
            Assert.Equal(touched, End(WorkflowState.Reviewing, null, noPath));
            Assert.EndsWith(strings.Format(Keys.GuidedEndNewestReportFormat, readOut.ReportPath!), End(WorkflowState.Reviewing, null, readOut), StringComparison.Ordinal);
        }
    }
}
