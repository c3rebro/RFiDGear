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
        /// <param name="card">What the reader showed; may be <c>null</c>.</param>
        /// <param name="strings">The page's texts.</param>
        /// <returns>The description, or empty if there is no chip in <paramref name="card"/>.</returns>
        public static string Card(ReaderSnapshot? card, LocalizedStrings strings)
        {
            if (card == null || !card.HasCard || card.Credentials.Count == 0)
            {
                return string.Empty;
            }

            var main = card.Credentials[0];
            var text = strings.Format(Keys.CardFormat, main.DisplayName, main.Uid);

            return card.Credentials.Count > 1 ? strings.Format(Keys.CardMoreFormat, text, card.Credentials.Count - 1) : text;
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
        /// <param name="phase">The phase; <c>null</c> when none is running.</param>
        /// <param name="strings">The page's texts.</param>
        /// <returns>The line, or empty.</returns>
        public static string Phase(CheckPhase? phase, LocalizedStrings strings) => phase switch
        {
            CheckPhase.Preparing => strings[Keys.GuidedPreparing],
            CheckPhase.LoadingProject => strings[Keys.GuidedPhaseLoading],
            CheckPhase.Running => strings[Keys.GuidedPhaseRunning],
            CheckPhase.ReadingReport => strings[Keys.GuidedPhaseReading],
            _ => string.Empty
        };

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
            _ => strings[Keys.GuidedCloseNothing]
        };

        private static string Cancelled(WorkflowSnapshot snapshot, LocalizedStrings strings)
        {
            // A cancel after the report was being made or stored: the order may or may not have been finished.
            if (snapshot.CancelledAt == WorkflowState.CompletionFailed || snapshot.CancelledAt == WorkflowState.Completing)
            {
                return strings[Keys.GuidedEndCancelledMaybe];
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
