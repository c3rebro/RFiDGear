using System;
using System.Collections.Generic;
using System.Linq;

using RFiDGear.ExtensionLibraries.CardCheck;
using RFiDGear.Extensions.CardCheckAssistant.Localization;
using RFiDGear.Extensions.CardCheckAssistant.ViewModels;
using RFiDGear.Extensions.CardCheckAssistant.Workflow;

using Xunit;

using Keys = RFiDGear.Extensions.CardCheckAssistant.Localization.ExtensionStrings.Keys;

namespace RFiDGear.Tests.CardCheckAssistant
{
    /// <summary>The operator's answers about the programming: plain objects, no window.</summary>
    public sealed class ProgrammingFormTests
    {
        private static readonly LocalizedStrings En = new LocalizedStrings("en-US");
        private static readonly LsmTemplate Desfire = new LsmTemplate("MD4000L_AV", "DESFire 4K", TemplateFamily.Desfire, 4000, 0);
        private static readonly LsmTemplate Classic = new LsmTemplate("MC1000L", "Classic 1K", TemplateFamily.Classic, 0, 4);

        private static ProgrammingFormViewModel NewForm(Action? changed = null) => new ProgrammingFormViewModel(En, changed ?? (() => { }));

        private static WorkflowSnapshot Snapshot(WorkflowState state, string? orderId = "A", string notes = "First notes", params TextTemplate[] blocks) => new WorkflowSnapshot
        {
            State = state,
            Order = orderId == null ? null : new WorkflowOrder(orderId, "J" + orderId, "1"),
            Selection = new TemplateSelection(ChipVerdict.Programmable, FreeMemory.Unknown, new[] { Desfire, Classic }, Desfire),
            Findings = new ReportAnalysis { Notes = notes },
            TextBlocks = blocks,
        };

        private static ProgrammingFormViewModel Seeded(params TextTemplate[] blocks)
        {
            var form = NewForm();
            form.Bind(Snapshot(WorkflowState.Programming, "A", "First notes", blocks));

            return form;
        }

        private static void AnswerAll(ProgrammingFormViewModel form, bool programming = true, bool functionTest = true, bool restricted = false)
        {
            form.Programming.IsYes = programming;
            form.Programming.IsNo = !programming;
            form.FunctionTest.IsYes = functionTest;
            form.FunctionTest.IsNo = !functionTest;
            form.Restricted.IsYes = restricted;
            form.Restricted.IsNo = !restricted;
        }

        [Fact]
        public void TheQuestions_StartUnanswered_AndNoIsAnAnswerOfItsOwn()
        {
            var form = Seeded();

            Assert.Null(form.Programming.Value);
            Assert.Null(form.FunctionTest.Value);
            Assert.Null(form.Restricted.Value);
            Assert.False(form.IsComplete);

            form.Programming.IsNo = true;
            Assert.False(form.Programming.Value);                // an answer, not "unanswered"
            Assert.True(form.Programming.IsNo);

            // What a radio button writes when the other one of its group is chosen is no answer.
            form.Programming.IsYes = false;
            form.Programming.IsNo = false;
            Assert.False(form.Programming.Value);

            form.Programming.IsYes = true;
            Assert.True(form.Programming.Value);
            Assert.False(form.Programming.IsNo);

            // The same for yes: the radio button of "yes" writes false when "no" is chosen, and a yes stays a yes until no is chosen.
            form.Programming.IsYes = false;
            Assert.True(form.Programming.Value);
        }

        [Fact]
        public void TheForm_IsCompleteOnlyWhenWhatIsNeededIsThere_InTheOrderOfTheForm()
        {
            var form = Seeded();
            Assert.Equal(new[] { En[Keys.GuidedQuestionProgramming], En[Keys.GuidedQuestionFunctionTest], En[Keys.GuidedQuestionRestricted] }, form.Missing());

            AnswerAll(form);
            Assert.True(form.IsComplete);

            // A restriction needs its text: a blank one would clear the notes of the first report.
            form.Restricted.IsYes = true;
            Assert.True(form.IsComplete);                         // the notes of the first report are the start of it
            form.RestrictionText = "   ";
            Assert.Equal(new[] { En[Keys.GuidedFormRestriction] }, form.Missing());

            // A Classic chip needs its sectors.
            form.RestrictionText = "No reader in the building.";
            form.SelectedTemplate = Classic;
            Assert.True(form.IsClassic);
            Assert.Equal(new[] { En[Keys.GuidedFormSectors] }, form.Missing());
            form.SectorsUsed = "1-3";
            Assert.True(form.IsComplete);

            form.SelectedTemplate = Desfire;
            Assert.False(form.IsClassic);
            form.SectorsUsed = string.Empty;
            Assert.True(form.IsComplete);

            // A list that is replaced writes null into the choice: it stays.
            form.SelectedTemplate = null;
            Assert.Same(Desfire, form.SelectedTemplate);
        }

        [Fact]
        public void TheAnswers_GoToTheWorkflow_AsTheReportNeedsThem()
        {
            var form = Seeded();
            AnswerAll(form, functionTest: false);

            // Not restricted: the text of the first report's notes is not sent (null keeps them), nothing for a DESFire chip.
            Assert.Equal(
                new LsmAnswers { ProgrammingSucceeded = true, FunctionTestPassed = false, HasRestrictions = false, RestrictionText = null, SectorsUsed = string.Empty },
                form.ToAnswers());

            form.Restricted.IsYes = true;
            form.RestrictionText = "Only the first sector.";
            form.SelectedTemplate = Classic;
            form.SectorsUsed = " 1-3 ";
            Assert.Equal(
                new LsmAnswers { ProgrammingSucceeded = true, FunctionTestPassed = false, HasRestrictions = true, RestrictionText = "Only the first sector.", SectorsUsed = "1-3" },
                form.ToAnswers());

            // Sectors that were typed for a Classic template are not sent for a DESFire one.
            form.SelectedTemplate = Desfire;
            Assert.Equal(string.Empty, form.ToAnswers().SectorsUsed);
            form.SelectedTemplate = Classic;

            // The text is kept when the question is answered no and yes again.
            form.Restricted.IsNo = true;
            form.Restricted.IsYes = true;
            Assert.Equal("Only the first sector.", form.RestrictionText);
        }

        [Fact]
        public void OnlyTheProgrammingAndTheFunctionTest_AreWorthASecondLook_WhenAnsweredNo()
        {
            var form = Seeded();
            AnswerAll(form, programming: false, functionTest: false);

            Assert.Equal(new[] { En[Keys.GuidedQuestionProgramming], En[Keys.GuidedQuestionFunctionTest] }, form.NoAnswers());

            AnswerAll(form);
            Assert.Empty(form.NoAnswers());                       // "restricted: no" is the normal case
        }

        [Fact]
        public void TheForm_IsFilledOnce_FromTheFindings_AndLaterPicturesChangeNothingOfWhatWasEntered()
        {
            var announced = 0;
            var form = new ProgrammingFormViewModel(En, () => announced++);

            // Before the programming begins there is nothing to fill it from.
            form.Bind(Snapshot(WorkflowState.Assessing));
            Assert.False(form.IsSeeded);

            form.Bind(Snapshot(WorkflowState.Programming, "A", "First notes"));
            Assert.True(form.IsSeeded);
            Assert.True(form.IsEditable);
            Assert.Same(Desfire, form.SelectedTemplate);
            Assert.Equal("First notes", form.RestrictionText);

            form.RestrictionText = "My text";
            form.SelectedTemplate = Classic;
            form.Programming.IsYes = true;

            // The same picture again, or one with other notes: not a thing is announced or changed.
            var before = announced;
            form.Bind(Snapshot(WorkflowState.Programming, "A", "First notes"));
            form.Bind(Snapshot(WorkflowState.Programming, "A", "Other notes"));
            Assert.Equal(before, announced);
            Assert.Equal("My text", form.RestrictionText);
            Assert.Same(Classic, form.SelectedTemplate);
            Assert.True(form.Programming.Value);
        }

        [Fact]
        public void TheForm_BelongsToTheOrder_AndIsLockedOnceTheWorkflowHasTakenTheAnswers()
        {
            var form = Seeded();
            AnswerAll(form);
            form.RestrictionText = "My text";

            // From the writing on, the answers are the engine's: shown, not editable, and kept.
            foreach (var state in new[] { WorkflowState.WritingReport, WorkflowState.ProgrammingFailed, WorkflowState.ReadingOut, WorkflowState.Cancelled })
            {
                form.Bind(Snapshot(state, "A", "Other notes"));
                Assert.False(form.IsEditable, state.ToString());
                Assert.True(form.IsReadOnly, state.ToString());
                Assert.True(form.IsSeeded, state.ToString());
                Assert.Equal("My text", form.RestrictionText);
                Assert.True(form.Programming.Value);
            }

            // Back passes through the list, where there is no order: the form is empty again.
            form.Bind(Snapshot(WorkflowState.Idle, null));
            Assert.False(form.IsSeeded);
            Assert.Null(form.Programming.Value);
            Assert.Equal(string.Empty, form.RestrictionText);
            Assert.Null(form.SelectedTemplate);

            // Another order starts clean, whatever the one before had.
            form.Bind(Snapshot(WorkflowState.Programming, "B", "Notes of B"));
            AnswerAll(form);
            form.Bind(Snapshot(WorkflowState.Programming, "C", "Notes of C"));
            Assert.Null(form.Programming.Value);
            Assert.Equal("Notes of C", form.RestrictionText);
        }

        [Fact]
        public void ATextBlock_IsAppendedAfterABlankLine_AndNeverReplacesWhatWasTyped()
        {
            var block = new TextTemplate { Name = "Standard", Content = "Block text." };
            var form = Seeded(block);
            Assert.True(form.HasTextBlocks);

            // Nothing to insert before the function is restricted and a block is chosen: either alone is not enough.
            Assert.False(form.InsertTextBlockCommand.CanExecute(null));
            form.SelectedTextBlock = block;
            Assert.False(form.InsertTextBlockCommand.CanExecute(null));
            form.SelectedTextBlock = null;
            form.Restricted.IsYes = true;
            Assert.False(form.InsertTextBlockCommand.CanExecute(null));
            form.SelectedTextBlock = block;
            Assert.True(form.InsertTextBlockCommand.CanExecute(null));

            form.InsertTextBlockCommand.Execute(null);
            Assert.Equal("First notes" + Environment.NewLine + Environment.NewLine + "Block text.", form.RestrictionText);

            form.RestrictionText = string.Empty;
            form.InsertTextBlockCommand.Execute(null);
            Assert.Equal("Block text.", form.RestrictionText);

            // Not once the answers are the engine's.
            form.Bind(Snapshot(WorkflowState.WritingReport, "A", "x", block));
            Assert.False(form.InsertTextBlockCommand.CanExecute(null));
        }

        [Fact]
        public void TheForm_NeedsItsTextsAndItsCallback()
        {
            Assert.Throws<ArgumentNullException>(() => new ProgrammingFormViewModel(null!, () => { }));
            Assert.Throws<ArgumentNullException>(() => new ProgrammingFormViewModel(En, null!));
        }
    }
}
