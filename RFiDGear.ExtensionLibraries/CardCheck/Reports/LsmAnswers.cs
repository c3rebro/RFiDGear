using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace RFiDGear.ExtensionLibraries.CardCheck
{
    /// <summary>
    /// What the operator answers about the programming of a chip, and how the answers go into the report. There are
    /// three yes-or-no questions (did the programming succeed, did the function test pass, is the function
    /// restricted); a question that has not been answered is <c>null</c>, which is different from "no". The field names
    /// and the way each answer ticks a pair of check boxes are the ones the old assistant wrote, so a report made here
    /// reads like one made there.
    /// </summary>
    public sealed record LsmAnswers
    {
        // Each question has two check boxes in the report, one ticked for "yes" and one for "no"; the other is "Off".
        private const string ProgrammingYesField = "CheckBox_Detail_Reserved_2_1";
        private const string ProgrammingNoField = "CheckBox_Detail_Reserved_2_2";
        private const string FunctionTestPassedField = "CheckBox_Detail_Reserved_3_1";
        private const string FunctionTestFailedField = "CheckBox_Detail_Reserved_3_2";
        private const string RestrictedYesField = "CheckBox_Detail_Reserved_4_1";
        private const string RestrictedNoField = "CheckBox_Detail_Reserved_4_2";
        private const string TemplateField = "ComboBox_UsedTemplate";
        private const string SectorsField = "TextBox_Detail_Mem_3";

        /// <summary>Whether the programming succeeded; <c>null</c> if not answered yet.</summary>
        public bool? ProgrammingSucceeded { get; init; }

        /// <summary>Whether the function test passed; <c>null</c> if not answered yet.</summary>
        public bool? FunctionTestPassed { get; init; }

        /// <summary>Whether the function is restricted; <c>null</c> if not answered yet.</summary>
        public bool? HasRestrictions { get; init; }

        /// <summary>
        /// The text that goes into the notes of the report when the function is restricted. It replaces the notes of
        /// the first report, so a caller that shows it to the operator starts it with them. <c>null</c> (not given)
        /// leaves the notes of the first report as they are; an empty text clears them. Ignored when there are no
        /// restrictions.
        /// </summary>
        public string? RestrictionText { get; init; }

        /// <summary>
        /// The sectors used on a Classic chip, as the operator wrote them; ignored for a DESFire template. It is free
        /// text and is not checked, as in the old assistant.
        /// </summary>
        public string SectorsUsed { get; init; } = string.Empty;

        /// <summary>
        /// Turns the answers into the report fields to write. Check boxes take <c>Yes</c> or <c>Off</c>. The notes are
        /// written only when the function is restricted and a text was given (an empty text clears them), and the
        /// sectors only for a Classic template.
        /// </summary>
        /// <param name="template">The template chosen for the report; its text goes into the report.</param>
        /// <returns>
        /// The field names with their new values, or why they cannot be made: the first question that has not been
        /// answered. Nothing is guessed for a question that was not answered.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="template"/> is null.</exception>
        public Result<IReadOnlyDictionary<string, string>> ToFields(LsmTemplate template)
        {
            if (template == null)
            {
                throw new ArgumentNullException(nameof(template));
            }

            if (ProgrammingSucceeded == null)
            {
                return NotAnswered("answers.programming.missing", "Say whether the programming succeeded.");
            }

            if (FunctionTestPassed == null)
            {
                return NotAnswered("answers.functionTest.missing", "Say whether the function test passed.");
            }

            if (HasRestrictions == null)
            {
                return NotAnswered("answers.restrictions.missing", "Say whether the function is restricted.");
            }

            var fields = new Dictionary<string, string>(StringComparer.Ordinal);

            Tick(fields, ProgrammingYesField, ProgrammingNoField, ProgrammingSucceeded.Value);
            Tick(fields, FunctionTestPassedField, FunctionTestFailedField, FunctionTestPassed.Value);
            Tick(fields, RestrictedYesField, RestrictedNoField, HasRestrictions.Value);

            if (HasRestrictions.Value && RestrictionText != null)
            {
                fields[ReportService.NotesField] = RestrictionText;
            }

            fields[TemplateField] = template.DisplayText;

            if (template.Family == TemplateFamily.Classic)
            {
                fields[SectorsField] = SectorsUsed ?? string.Empty;
            }

            return Result<IReadOnlyDictionary<string, string>>.Ok(new ReadOnlyDictionary<string, string>(fields));
        }

        private static void Tick(IDictionary<string, string> fields, string yesField, string noField, bool yes)
        {
            fields[yesField] = yes ? ReportService.Ticked : "Off";
            fields[noField] = yes ? "Off" : ReportService.Ticked;
        }

        private static Result<IReadOnlyDictionary<string, string>> NotAnswered(string code, string message) =>
            Result<IReadOnlyDictionary<string, string>>.Fail(new Failure(FailureKind.InvalidInput, code, message));
    }
}
