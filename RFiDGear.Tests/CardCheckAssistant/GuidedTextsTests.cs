using System;
using System.Linq;

using RFiDGear.Contracts;
using RFiDGear.ExtensionLibraries.CardCheck;
using RFiDGear.Extensions.CardCheckAssistant.Localization;
using RFiDGear.Extensions.CardCheckAssistant.ViewModels;
using RFiDGear.Extensions.CardCheckAssistant.Workflow;

using Xunit;

using Keys = RFiDGear.Extensions.CardCheckAssistant.Localization.ExtensionStrings.Keys;

namespace RFiDGear.Tests.CardCheckAssistant
{
    /// <summary>The sentences of the guided page that are a plain function of what the workflow shows.</summary>
    public sealed class GuidedTextsTests
    {
        private static readonly LocalizedStrings En = new LocalizedStrings("en-US");

        // The states are passed by name: the test class is public, the enumeration is not.
        private static WorkflowSnapshot Snapshot(string state, Func<WorkflowSnapshot, WorkflowSnapshot>? edit = null)
        {
            var snapshot = new WorkflowSnapshot { State = Enum.Parse<WorkflowState>(state) };

            return edit == null ? snapshot : edit(snapshot);
        }

        private static TemplateSelection SelectionOf(ChipVerdict verdict) => new TemplateSelection(verdict, FreeMemory.Unknown, Array.Empty<LsmTemplate>(), null);

        [Theory]
        [InlineData("ReadingOut", "GuidedReadOutRunning")]
        [InlineData("ReadOutFailed", "GuidedReadOutFailedHint")]
        [InlineData("RunFailed", "GuidedRunFailedHint")]
        public void TheCheckStep_SaysWhatRuns_OrWhatToDoAfterAFailure(string state, string key)
        {
            // The first check's phase must not speak for the read-out, and the hint of a failed check must not for a failed read-out.
            var snapshot = Snapshot(state, s => s with { Phase = CheckPhase.Running });

            Assert.Equal(En[key], GuidedTexts.CheckText(snapshot, En));
        }

        [Fact]
        public void TheCheckStep_ShowsThePhase_WhileTheFirstCheckRuns()
        {
            Assert.Equal(En[Keys.GuidedPhaseRunning], GuidedTexts.CheckText(Snapshot("Checking", s => s with { Phase = CheckPhase.Running }), En));
            Assert.Equal(string.Empty, GuidedTexts.CheckText(Snapshot("Checking"), En));
        }

        [Theory]
        [InlineData("ReadOutFailed", "GuidedRetryReadOutButton")]
        [InlineData("RunFailed", "GuidedRetryButton")]
        [InlineData("CompletionFailed", "GuidedRetryButton")]
        public void TheRetryButton_SaysWhatIsRepeated(string state, string key)
        {
            Assert.Equal(En[key], GuidedTexts.RetryText(Snapshot(state), En));
        }

        [Theory]
        [InlineData("Assessing", true, "GuidedLeaveOpenButton")]
        [InlineData("Assessing", false, "CancelButton")]
        [InlineData("Programming", false, "GuidedLeaveOpenButton")]
        [InlineData("ProgrammingFailed", false, "GuidedLeaveOpenButton")]
        [InlineData("ReadOutFailed", false, "GuidedLeaveOpenButton")]
        [InlineData("Reviewing", false, "GuidedLeaveOpenButton")]
        [InlineData("WritingReport", true, "CancelButton")]
        [InlineData("ReadingOut", true, "CancelButton")]
        [InlineData("CompletionFailed", true, "CancelButton")]
        [InlineData("WaitingForCard", true, "CancelButton")]
        public void TheButtonAtTheFootOfThePage_SaysLeaveOpen_WhereTheOrderStaysOpen(string state, bool programmable, string key)
        {
            var snapshot = Snapshot(state, s => s with { Selection = SelectionOf(programmable ? ChipVerdict.Programmable : ChipVerdict.NotSupported) });

            Assert.Equal(En[key], GuidedTexts.CancelText(snapshot, En));
        }

        [Fact]
        public void LeavingIsAskedAbout_FromTheProgrammingOn_AndNotBefore()
        {
            var asked = WorkflowTables.States.Where(GuidedTexts.CancelNeedsQuestion).Select(s => s.ToString()).OrderBy(n => n, StringComparer.Ordinal);

            Assert.Equal(
                new[] { "Programming", "ProgrammingFailed", "ReadOutFailed", "ReadingOut", "Reviewing", "WritingReport" },
                asked);
        }

        [Fact]
        public void TheCloseQuestion_SaysTheChipMayBeProgrammed_InEveryStateFromTheProgrammingOn()
        {
            foreach (var state in new[] { "Programming", "WritingReport", "ProgrammingFailed", "ReadingOut", "ReadOutFailed", "Reviewing" })
            {
                Assert.Equal(En[Keys.GuidedCloseProgrammed], GuidedTexts.Close(Snapshot(state), En));
            }

            Assert.Equal(En[Keys.GuidedCloseNothing], GuidedTexts.Close(Snapshot("WaitingForCard"), En));
        }

        [Fact]
        public void TheWarningAboutAnotherChip_NamesBothUids_AndOnlyWhenTheyAreKnownToDiffer()
        {
            var another = new CardMoveResult(CardWaitOutcome.Done, CardMoveStage.WaitingForReinsertion, "04AA", "04BB", null);
            var same = another with { UidAfter = "04AA" };
            var unknown = another with { UidAfter = null };

            var warning = GuidedTexts.UidWarning(another, En);
            Assert.Contains("04AA", warning, StringComparison.Ordinal);
            Assert.Contains("04BB", warning, StringComparison.Ordinal);
            Assert.Equal(string.Empty, GuidedTexts.UidWarning(same, En));
            Assert.Equal(string.Empty, GuidedTexts.UidWarning(unknown, En));
            Assert.Equal(string.Empty, GuidedTexts.UidWarning(null, En));
        }

        [Fact]
        public void TheReviewWarnings_ArePlainFacts_BothValuesShown_AndNoneWhenNothingIsOdd()
        {
            var move = new CardMoveResult(CardWaitOutcome.Done, CardMoveStage.WaitingForReinsertion, "04AA", "04AA", null);
            WorkflowSnapshot ReadOutWith(string? first, string? second, bool supported, CardMoveResult? moved) => Snapshot("Reviewing", s => s with
            {
                Findings = new ReportAnalysis { ChipType = first ?? string.Empty },
                ReadOut = new CheckRunResult(CheckRunStatus.Finished, null, null, new ReportAnalysis { ChipType = second ?? string.Empty, Supported = supported }, "x.pdf"),
                Move = moved,
            });

            Assert.Empty(GuidedTexts.ReviewWarnings(ReadOutWith("DESFire EV1", "DESFire EV1", true, move), En));

            // The chip type is compared loosely (case, blanks), the two projects may not spell it alike.
            Assert.Empty(GuidedTexts.ReviewWarnings(ReadOutWith("DESFire EV1", " desfire ev1 ", true, move), En));

            // A report that does not name the chip type says nothing to compare.
            Assert.Empty(GuidedTexts.ReviewWarnings(ReadOutWith("DESFire EV1", null, true, move), En));
            Assert.Empty(GuidedTexts.ReviewWarnings(ReadOutWith(null, "DESFire EV1", true, null), En));

            var odd = GuidedTexts.ReviewWarnings(ReadOutWith("DESFire EV1", "DESFire EV2", false, move with { UidAfter = "04BB" }), En);
            Assert.Equal(3, odd.Count);
            Assert.Contains(odd, line => line.Contains("04AA", StringComparison.Ordinal) && line.Contains("04BB", StringComparison.Ordinal));
            Assert.Contains(odd, line => line.Contains("DESFire EV1", StringComparison.Ordinal) && line.Contains("DESFire EV2", StringComparison.Ordinal));
            Assert.Contains(En[Keys.GuidedWarnUnsupported], odd);
        }
    }
}
