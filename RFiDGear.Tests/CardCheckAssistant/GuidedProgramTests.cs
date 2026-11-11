using System;
using System.Linq;
using System.Threading.Tasks;

using RFiDGear.Contracts;
using RFiDGear.ExtensionLibraries.CardCheck;
using RFiDGear.Extensions.CardCheckAssistant.Workflow;

using Xunit;

using Keys = RFiDGear.Extensions.CardCheckAssistant.Localization.ExtensionStrings.Keys;

namespace RFiDGear.Tests.CardCheckAssistant
{
    /// <summary>The page while the chip is programmed elsewhere: the form, Continue, and what is locked once the work has begun.</summary>
    public sealed class GuidedProgramTests
    {
        private static readonly LsmTemplate Desfire = new LsmTemplate("MD4000L_AV", "DESFire 4K", TemplateFamily.Desfire, 4000, 0);
        private static readonly LsmTemplate Classic = new LsmTemplate("MC1000L", "Classic 1K", TemplateFamily.Classic, 0, 4);

        private static Func<WorkflowSnapshot, WorkflowSnapshot> Programming(bool chipBack, string orderId = "A") => s => s with
        {
            Order = new WorkflowOrder(orderId, "J" + orderId, "1"),
            Selection = new TemplateSelection(ChipVerdict.Programmable, FreeMemory.Unknown, new[] { Desfire, Classic }, Desfire),
            Findings = new ReportAnalysis { ChipType = "DESFire EV1", Notes = "First notes" },
            Move = chipBack ? new CardMoveResult(CardWaitOutcome.Done, CardMoveStage.WaitingForReinsertion, "04AA", "04AA", null) : null,
        };

        private static void Answer(GuidedPage page, bool programming = true, bool functionTest = true, bool restricted = false)
        {
            var form = page.Vm.Form;
            form.Programming.IsYes = programming;
            form.Programming.IsNo = !programming;
            form.FunctionTest.IsYes = functionTest;
            form.FunctionTest.IsNo = !functionTest;
            form.Restricted.IsYes = restricted;
            form.Restricted.IsNo = !restricted;
        }

        [Fact]
        public Task Continue_SendsTheExactTemplateAndAnswers_WhenTheChipIsBack_AndTheFormIsComplete() => GuidedPage.Run(async page =>
        {
            var vm = page.Vm;
            await page.PushAsync(WorkflowState.Programming, Programming(chipBack: true));

            Assert.True(vm.IsProgramStep);
            Assert.False(vm.ContinueCommand.CanExecute(null));                 // nothing answered yet
            Assert.Equal(vm.Strings.Format(Keys.GuidedStillToAnswerFormat, string.Join(", ", vm.Form.Missing())), vm.ProgramStatusText);

            // The button is told when the last answer is given, not only asked.
            var announced = 0;
            vm.ContinueCommand.CanExecuteChanged += (_, _) => announced++;
            Answer(page, restricted: true);
            vm.Form.RestrictionText = "Only sector 1.";
            vm.Form.SelectedTemplate = Classic;
            Assert.False(vm.ContinueCommand.CanExecute(null));                 // the sectors are still open
            announced = 0;
            vm.Form.SectorsUsed = "1";

            Assert.True(announced > 0, "The Continue button was not told.");
            Assert.True(vm.ContinueCommand.CanExecute(null));
            Assert.Equal(vm.Strings[Keys.GuidedContinueHint], vm.ProgramStatusText);
            vm.ContinueCommand.Execute(null);

            Assert.Same(Classic, page.Workflow.LastTemplate);
            Assert.Equal(
                new LsmAnswers { ProgrammingSucceeded = true, FunctionTestPassed = true, HasRestrictions = true, RestrictionText = "Only sector 1.", SectorsUsed = "1" },
                page.Workflow.LastAnswers);
            Assert.Empty(page.Close.Asked);                                    // yes and a restriction are no reason to ask
        });

        [Fact]
        public Task Continue_WaitsForTheChip_AndTheOperatorCanAnswerMeanwhile() => GuidedPage.Run(async page =>
        {
            var vm = page.Vm;
            await page.PushAsync(WorkflowState.Programming, Programming(chipBack: false), busy: true);
            Assert.Equal(vm.Strings.Format(Keys.GuidedStillToAnswerFormat, string.Join(", ", vm.Form.Missing())), vm.ProgramStatusText);
            Answer(page);

            Assert.Equal(string.Empty, vm.ProgramStatusText);                   // answered, and nothing more to say until the chip is back
            Assert.True(vm.Form.IsEditable);                                    // the questions can be answered while the chip is away
            Assert.True(vm.Form.IsComplete);
            Assert.False(vm.ContinueCommand.CanExecute(null));
            Assert.Equal(vm.Strings[Keys.GuidedProgramRemove], vm.ProgramPromptText);

            await page.PushAsync(WorkflowState.Programming, Programming(chipBack: true));
            Assert.True(vm.ContinueCommand.CanExecute(null));                   // the answers are still there
            Assert.Equal(vm.Strings[Keys.GuidedProgramBack], vm.ProgramPromptText);
        });

        [Theory]
        [InlineData(false, true, false, true)]
        [InlineData(true, false, false, true)]
        [InlineData(false, false, false, true)]
        [InlineData(true, true, true, false)]
        [InlineData(true, true, false, false)]
        public Task ANoToTheProgrammingOrTheFunctionTest_IsAskedAbout_AndAnythingElseIsNot(bool programming, bool functionTest, bool restricted, bool asks) => GuidedPage.Run(async page =>
        {
            var vm = page.Vm;
            await page.PushAsync(WorkflowState.Programming, Programming(chipBack: true));
            Answer(page, programming, functionTest, restricted);
            page.Close.Answer = false;

            vm.ContinueCommand.Execute(null);

            Assert.Equal(asks, page.Close.Asked.Count == 1);
            Assert.Null(asks ? page.Workflow.LastTemplate : null);              // a no on the question: nothing is sent
            if (!asks)
            {
                Assert.Same(Desfire, page.Workflow.LastTemplate);
                return;
            }

            // The question names what was answered no, and yes sends it.
            Assert.Contains(!programming ? vm.Strings[Keys.GuidedQuestionProgramming] : vm.Strings[Keys.GuidedQuestionFunctionTest], page.Close.Asked.Single().Text, StringComparison.Ordinal);
            page.Close.Answer = true;
            vm.ContinueCommand.Execute(null);
            Assert.Same(Desfire, page.Workflow.LastTemplate);
            Assert.False(page.Workflow.LastAnswers!.ProgrammingSucceeded == true && page.Workflow.LastAnswers.FunctionTestPassed == true);
        });

        [Fact]
        public Task OnceTheWorkHasBegun_TheAnswersAreShownLocked_AndKept_UntilTheOrderIsLeft() => GuidedPage.Run(async page =>
        {
            var vm = page.Vm;
            await page.PushAsync(WorkflowState.Programming, Programming(chipBack: true));
            Answer(page);
            vm.Form.RestrictionText = "Typed.";
            Assert.Equal(string.Empty, vm.LockedNoteText);

            await page.PushAsync(WorkflowState.WritingReport, Programming(chipBack: true), busy: true);
            Assert.False(vm.Form.IsEditable);
            Assert.Equal(vm.Strings[Keys.GuidedAnswersLocked], vm.LockedNoteText);
            Assert.Equal(vm.Strings[Keys.GuidedProgramWriting], vm.ProgramPromptText);

            await page.PushAsync(WorkflowState.ProgrammingFailed, Programming(chipBack: true));
            Assert.False(vm.Form.IsEditable);
            Assert.True(vm.Form.Programming.Value);
            Assert.Equal("Typed.", vm.Form.RestrictionText);
            Assert.Equal(vm.Strings[Keys.GuidedProgramFailed], vm.ProgramPromptText);
            Assert.True(vm.CanRetry);
            Assert.Equal(vm.Strings[Keys.GuidedRetryWriteButton], vm.RetryText);

            // Back passes through the list (no order): the form is empty for the next one.
            await page.PushAsync(WorkflowState.Idle);
            Assert.False(vm.Form.IsSeeded);
            Assert.Null(vm.Form.Programming.Value);
            Assert.Equal(string.Empty, vm.LockedNoteText);
        });

        [Fact]
        public Task WhatIsTyped_SurvivesWhatTheHostAnnounces() => GuidedPage.Run(async page =>
        {
            var vm = page.Vm;
            await page.PushAsync(WorkflowState.Programming, Programming(chipBack: false), busy: true);
            vm.Form.Restricted.IsYes = true;
            vm.Form.RestrictionText = "Typed by the operator.";
            var template = vm.Form.SelectedTemplate;

            // The chip is taken off and put back, and RFiDGear runs a task meanwhile: the host says so, and the page shows it.
            page.Host.ReaderHost.Show(Snapshots.NoCard());
            page.Host.ExecutionHost.SetRunning(true);
            await ViewModelTestSupport.Flush();
            page.Host.ReaderHost.Show(Snapshots.Card("04AA"));
            await ViewModelTestSupport.Flush();

            Assert.Equal("Typed by the operator.", vm.Form.RestrictionText);
            Assert.True(vm.Form.Restricted.Value);
            Assert.Same(template, vm.Form.SelectedTemplate);
        });

        [Fact]
        public Task TheFormOfOneOrder_IsNotTheFormOfTheNext() => GuidedPage.Run(async page =>
        {
            var vm = page.Vm;
            await page.PushAsync(WorkflowState.Programming, Programming(chipBack: true));
            Answer(page);
            await page.PushAsync(WorkflowState.Cancelled, Programming(chipBack: true));
            Assert.True(vm.Form.Programming.Value);                              // the end shows what was answered until Back
            Assert.False(vm.Form.IsEditable);

            await page.PushAsync(WorkflowState.Programming, Programming(chipBack: true, "B"));
            Assert.Null(vm.Form.Programming.Value);
            Assert.True(vm.Form.IsEditable);
            Assert.Equal("First notes", vm.Form.RestrictionText);
        });
    }
}
