using System;
using System.Collections.Generic;
using System.Linq;

using RFiDGear.Contracts;
using RFiDGear.ExtensionLibraries.CardCheck;
using RFiDGear.Extensions.CardCheckAssistant.Localization;
using RFiDGear.Extensions.CardCheckAssistant.Workflow;

using Keys = RFiDGear.Extensions.CardCheckAssistant.Localization.ExtensionStrings.Keys;

namespace RFiDGear.Extensions.CardCheckAssistant.ViewModels
{
    /// <summary>
    /// The sentences of the guided page that follow from a snapshot of the workflow, kept apart from the view model so
    /// that each is a plain function of what it is shown.
    /// </summary>
    internal static class GuidedTexts
    {
        /// <summary>The chip on the reader: its type and UID, and how many more credentials came with it.</summary>
        /// <param name="reader">What the reader showed; may be <c>null</c>.</param>
        /// <param name="strings">The page's texts.</param>
        /// <returns>The description, or empty if there is no chip in <paramref name="reader"/>.</returns>
        public static string Card(ReaderSnapshot? reader, LocalizedStrings strings)
        {
            if (reader == null || !reader.HasCard || reader.Credentials.Count == 0)
            {
                return string.Empty;
            }

            var main = reader.Credentials[0];
            var text = strings.Format(Keys.CardFormat, main.DisplayName, main.Uid);

            return reader.Credentials.Count > 1 ? strings.Format(Keys.CardMoreFormat, text, reader.Credentials.Count - 1) : text;
        }

        /// <summary>
        /// What the card step asks of the operator. A missing reader comes first, then RFiDGear running tasks (the
        /// reader is not available then), then a chip that was seen; otherwise the chip is asked for.
        /// </summary>
        /// <param name="snapshot">The workflow's picture.</param>
        /// <param name="hostCard">The host's own picture of the reader; may be <c>null</c>.</param>
        /// <param name="hostRunning">Whether RFiDGear is running tasks.</param>
        /// <param name="strings">The page's texts.</param>
        /// <returns>The sentence.</returns>
        public static string Prompt(WorkflowSnapshot snapshot, ReaderSnapshot? hostCard, bool hostRunning, LocalizedStrings strings)
        {
            if (hostCard != null && hostCard.State == ReaderScanState.NoReader)
            {
                return strings[Keys.GuidedPromptNoReader];
            }

            if (hostRunning)
            {
                return strings[Keys.GuidedPromptHostRunning];
            }

            var found = Card(snapshot.Card, strings);

            return found.Length > 0 ? strings.Format(Keys.GuidedPromptChipFound, found) : strings[Keys.GuidedPromptPlaceChip];
        }

        /// <summary>The line for the phase the check run is in.</summary>
        /// <param name="current">The phase; <c>null</c> when none is running.</param>
        /// <param name="strings">The page's texts.</param>
        /// <returns>The line, or empty.</returns>
        public static string Phase(CheckPhase? current, LocalizedStrings strings) => current switch
        {
            CheckPhase.Preparing => strings[Keys.GuidedPreparing],
            CheckPhase.LoadingProject => strings[Keys.GuidedPhaseLoading],
            CheckPhase.Running => strings[Keys.GuidedPhaseRunning],
            CheckPhase.ReadingReport => strings[Keys.GuidedPhaseReading],
            _ => string.Empty
        };

        /// <summary>
        /// What the programming step asks of the operator. While the chip is awaited a missing reader comes first, then
        /// RFiDGear running tasks (the reader is not available then); then the stage of the chip's way off the reader and
        /// back on; after that what is being written or what failed.
        /// </summary>
        /// <param name="snapshot">The workflow's picture.</param>
        /// <param name="hostCard">The host's own picture of the reader; may be <c>null</c>.</param>
        /// <param name="hostRunning">Whether RFiDGear is running tasks.</param>
        /// <param name="strings">The page's texts.</param>
        /// <returns>The sentence, or empty outside the programming states.</returns>
        public static string ProgramPrompt(WorkflowSnapshot snapshot, ReaderSnapshot? hostCard, bool hostRunning, LocalizedStrings strings)
        {
            var awaiting = snapshot.State == WorkflowState.Programming && snapshot.Move?.Outcome != CardWaitOutcome.Done;
            if (awaiting && hostCard != null && hostCard.State == ReaderScanState.NoReader)
            {
                return strings[Keys.GuidedPromptNoReader];
            }

            if (awaiting && hostRunning)
            {
                return strings[Keys.GuidedPromptHostRunning];
            }

            return snapshot.State switch
            {
                WorkflowState.Programming => snapshot.Move?.Outcome switch
                {
                    CardWaitOutcome.Done => strings[Keys.GuidedProgramBack],
                    CardWaitOutcome.TimedOut => strings[Keys.GuidedProgramTimedOut],
                    _ => snapshot.MoveStage == CardMoveStage.WaitingForReinsertion ? strings[Keys.GuidedProgramReinsert] : strings[Keys.GuidedProgramRemove]
                },
                WorkflowState.WritingReport => strings[Keys.GuidedProgramWriting],
                WorkflowState.ProgrammingFailed => strings[Keys.GuidedProgramFailed],
                _ => string.Empty
            };
        }

        /// <summary>What is still to do before the programming can go on.</summary>
        /// <param name="snapshot">The workflow's picture.</param>
        /// <param name="missing">The names of what is still unanswered in the form.</param>
        /// <param name="strings">The page's texts.</param>
        /// <returns>What is still to answer, or that Continue writes and reads out, or empty.</returns>
        public static string ProgramStatus(WorkflowSnapshot snapshot, IReadOnlyList<string> missing, LocalizedStrings strings)
        {
            if (snapshot.State != WorkflowState.Programming)
            {
                return string.Empty;
            }

            if (missing.Count > 0)
            {
                return strings.Format(Keys.GuidedStillToAnswerFormat, string.Join(", ", missing));
            }

            return snapshot.Move?.Outcome == CardWaitOutcome.Done ? strings[Keys.GuidedContinueHint] : string.Empty;
        }

        /// <summary>The line of the check step: what runs, or what to do after a run failed.</summary>
        /// <param name="snapshot">The workflow's picture.</param>
        /// <param name="strings">The page's texts.</param>
        /// <returns>The line, or empty.</returns>
        public static string CheckText(WorkflowSnapshot snapshot, LocalizedStrings strings) => snapshot.State switch
        {
            WorkflowState.ReadingOut => strings[Keys.GuidedReadOutRunning],
            WorkflowState.ReadOutFailed => strings[Keys.GuidedReadOutFailedHint],
            WorkflowState.RunFailed => strings[Keys.GuidedRunFailedHint],
            _ => Phase(snapshot.Phase, strings)
        };

        /// <summary>The label of the button that repeats a failed step.</summary>
        /// <param name="snapshot">The workflow's picture.</param>
        /// <param name="strings">The page's texts.</param>
        /// <returns>The label.</returns>
        public static string RetryText(WorkflowSnapshot snapshot, LocalizedStrings strings) => snapshot.State switch
        {
            WorkflowState.ReadOutFailed => strings[Keys.GuidedRetryReadOutButton],
            WorkflowState.Programming => strings[Keys.GuidedRetryWaitButton],
            WorkflowState.ProgrammingFailed => strings[Keys.GuidedRetryWriteButton],
            _ => strings[Keys.GuidedRetryButton]
        };

        /// <summary>
        /// The label of the button at the foot of the page. Where the order stays open and the chip may have been programmed
        /// it says so; it is plain Cancel where something is being made or uploaded.
        /// </summary>
        /// <param name="snapshot">The workflow's picture.</param>
        /// <param name="strings">The page's texts.</param>
        /// <returns>The label.</returns>
        public static string CancelText(WorkflowSnapshot snapshot, LocalizedStrings strings) => snapshot.State switch
        {
            WorkflowState.Assessing when snapshot.Selection?.Verdict == ChipVerdict.Programmable => strings[Keys.GuidedLeaveOpenButton],
            WorkflowState.Programming or WorkflowState.ProgrammingFailed or WorkflowState.ReadOutFailed or WorkflowState.Reviewing => strings[Keys.GuidedLeaveOpenButton],
            _ => strings[Keys.CancelButton]
        };

        /// <summary>
        /// Whether leaving in a state needs a question first. After the chip was taken off the reader nothing resumes the
        /// work (a new start begins at the check and asks about the reports), so a click on the button at the foot of the
        /// page must not end it by mistake.
        /// </summary>
        /// <param name="state">The state of the workflow.</param>
        /// <returns><c>true</c> from the programming on.</returns>
        public static bool CancelNeedsQuestion(WorkflowState state) =>
            state is WorkflowState.Programming or WorkflowState.WritingReport or WorkflowState.ProgrammingFailed
                or WorkflowState.ReadingOut or WorkflowState.ReadOutFailed or WorkflowState.Reviewing;

        /// <summary>What may be wrong with the chip or its report that the operator should know before the order is finished.</summary>
        /// <param name="snapshot">The workflow's picture.</param>
        /// <param name="strings">The page's texts.</param>
        /// <returns>The warnings, none if nothing is odd. They never block anything.</returns>
        public static IReadOnlyList<string> ReviewWarnings(WorkflowSnapshot snapshot, LocalizedStrings strings)
        {
            var warnings = new List<string>(3);

            var uid = UidWarning(snapshot.Move, strings);
            if (uid.Length > 0)
            {
                warnings.Add(uid);
            }

            // The two reports come from two different projects: the chip type is compared loosely, and both are shown.
            var first = snapshot.Findings?.ChipType?.Trim();
            var second = snapshot.ReadOut?.Findings?.ChipType?.Trim();
            if (!string.IsNullOrEmpty(first) && !string.IsNullOrEmpty(second) && !string.Equals(first, second, StringComparison.OrdinalIgnoreCase))
            {
                warnings.Add(strings.Format(Keys.GuidedWarnChipTypeFormat, first!, second!));
            }

            if (snapshot.ReadOut?.Findings is { Supported: false })
            {
                warnings.Add(strings[Keys.GuidedWarnUnsupported]);
            }

            return warnings;
        }

        /// <summary>The warning that the chip put back is not the one taken off, with both UIDs.</summary>
        /// <param name="move">How the chip's way off the reader and back on ended; may be <c>null</c>.</param>
        /// <param name="strings">The page's texts.</param>
        /// <returns>The sentence, or empty if the chip is the same or one of the UIDs is not known.</returns>
        public static string UidWarning(CardMoveResult? move, LocalizedStrings strings) =>
            move?.SameCard == false ? strings.Format(Keys.GuidedWarnUidFormat, move.UidBefore ?? string.Empty, move.UidAfter ?? string.Empty) : string.Empty;

        /// <summary>What the verdict means for the order.</summary>
        /// <param name="selection">What the rules made of the findings; may be <c>null</c>.</param>
        /// <param name="strings">The page's texts.</param>
        /// <returns>The sentence, or empty if there is no verdict.</returns>
        public static string Verdict(TemplateSelection? selection, LocalizedStrings strings)
        {
            if (selection == null)
            {
                return string.Empty;
            }

            return selection.Verdict switch
            {
                ChipVerdict.NotSupported => strings[Keys.GuidedVerdictNotSupported],
                ChipVerdict.SupportedOnly => strings[Keys.GuidedVerdictSupportedOnly],
                ChipVerdict.Programmable => strings[Keys.GuidedVerdictProgrammable],
                _ => strings[Keys.GuidedVerdictIndeterminate]
            };
        }

        /// <summary>One line of the findings, or empty if the report said nothing.</summary>
        /// <param name="key">The text with one placeholder.</param>
        /// <param name="value">What goes into it.</param>
        /// <param name="strings">The page's texts.</param>
        /// <returns>The line, or empty.</returns>
        public static string Line(string key, string? value, LocalizedStrings strings) =>
            string.IsNullOrWhiteSpace(value) ? string.Empty : strings.Format(key, value!.Trim());

        /// <summary>The reports of an earlier check, named for the operator.</summary>
        /// <param name="existing">The files that exist.</param>
        /// <param name="strings">The page's texts.</param>
        /// <returns>The sentence, or empty if there are none.</returns>
        public static string Replace(IReadOnlyList<ReportFile> existing, LocalizedStrings strings) =>
            existing.Count == 0
                ? string.Empty
                : strings.Format(Keys.GuidedReplaceFormat, string.Join(", ", existing.Select(file => FileName(file, strings))));

        /// <summary>What the end of the check came to, and what the operator should do about it.</summary>
        /// <param name="snapshot">The workflow's picture.</param>
        /// <param name="strings">The page's texts.</param>
        /// <returns>The sentence, or empty outside the end states.</returns>
        public static string End(WorkflowSnapshot snapshot, LocalizedStrings strings)
        {
            switch (snapshot.State)
            {
                case WorkflowState.Completed:
                    return snapshot.CleanupFailed
                        ? strings[Keys.GuidedEndCompleted] + " " + strings[Keys.GuidedEndCleanupFailed]
                        : strings[Keys.GuidedEndCompleted];
                case WorkflowState.Faulted:
                    return strings[Keys.GuidedEndFaulted];
                case WorkflowState.Cancelled:
                    return Cancelled(snapshot, strings);
                default:
                    return string.Empty;
            }
        }

        /// <summary>What closing the window means in the state the workflow is in.</summary>
        /// <param name="snapshot">The workflow's picture.</param>
        /// <param name="strings">The page's texts.</param>
        /// <returns>The sentence for the question.</returns>
        public static string Close(WorkflowSnapshot snapshot, LocalizedStrings strings) => snapshot.State switch
        {
            WorkflowState.CompletionFailed => strings[Keys.GuidedCloseUpload],
            WorkflowState.Assessing => strings[Keys.GuidedCloseVerdict],
            WorkflowState.RunFailed => strings[Keys.GuidedCloseVerdict],
            WorkflowState.Programming or WorkflowState.WritingReport or WorkflowState.ProgrammingFailed or WorkflowState.ReadingOut
                or WorkflowState.ReadOutFailed or WorkflowState.Reviewing => strings[Keys.GuidedCloseProgrammed],
            _ => strings[Keys.GuidedCloseNothing]
        };

        private static string Cancelled(WorkflowSnapshot snapshot, LocalizedStrings strings)
        {
            // A cancel while the chip was being programmed, or after: it may be programmed all the same, and the order is not
            // finished.
            if (snapshot.CancelledAt is WorkflowState.Programming or WorkflowState.WritingReport or WorkflowState.ProgrammingFailed
                or WorkflowState.ReadingOut or WorkflowState.ReadOutFailed or WorkflowState.Reviewing)
            {
                return strings[Keys.GuidedEndCancelledProgrammed];
            }

            // A cancel after the report was being made or stored: the order may or may not have been finished. For a chip that
            // was programmed, a new check is not "safe": it would replace the reports made so far.
            if (snapshot.CancelledAt == WorkflowState.CompletionFailed || snapshot.CancelledAt == WorkflowState.Completing)
            {
                return strings[snapshot.Selection?.Verdict == ChipVerdict.Programmable ? Keys.GuidedEndCancelledMaybeProgrammed : Keys.GuidedEndCancelledMaybe];
            }

            var path = snapshot.Check?.ReportPath;
            if (string.IsNullOrWhiteSpace(path))
            {
                return strings[Keys.GuidedEndCancelledNothing];
            }

            return snapshot.Selection?.Verdict == ChipVerdict.Programmable
                ? strings.Format(Keys.GuidedEndCancelledProgrammableFormat, path!)
                : strings.Format(Keys.GuidedEndCancelledKeptFormat, path!);
        }

        private static string FileName(ReportFile file, LocalizedStrings strings) => file switch
        {
            ReportFile.Step1 => strings[Keys.GuidedFileStep1],
            ReportFile.Step2 => strings[Keys.GuidedFileStep2],
            ReportFile.Step3 => strings[Keys.GuidedFileStep3],
            ReportFile.Final => strings[Keys.GuidedFileFinal],
            _ => file.ToString()
        };
    }
}
