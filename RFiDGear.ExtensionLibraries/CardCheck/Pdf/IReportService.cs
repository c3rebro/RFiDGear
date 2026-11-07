using System.Collections.Generic;

namespace RFiDGear.ExtensionLibraries.CardCheck
{
    /// <summary>
    /// Reads and writes the form fields of the check reports (PDF files). It is the only part of the library that
    /// knows the PDF component. Every call loads the file once and saves it at most once; a file is written to a
    /// temporary name and swapped in, so a report is never left half written. Nothing here throws because of a file:
    /// a missing, damaged or locked file is a <see cref="Failure"/>, and no field value or file content is ever put
    /// into a failure message or the log.
    /// </summary>
    public interface IReportService
    {
        /// <summary>
        /// Reads what the first report says about the chip, from the fields the check project fills in. A check box
        /// counts as ticked only when its value is exactly <c>Yes</c>; a field the report does not have reads as empty.
        /// </summary>
        /// <param name="reportPath">The report of the first run.</param>
        /// <returns>
        /// The analysis, or why the file cannot be read: it does not exist, it is not a PDF, or it has no form fields
        /// and so cannot be a report of the check.
        /// </returns>
        Result<ReportAnalysis> Analyze(string reportPath);

        /// <summary>
        /// Copies a report and sets some of its fields in the copy. A field that is written is made visible and
        /// editable first. All or nothing: if the report lacks any of the fields, nothing is written.
        /// </summary>
        /// <param name="sourcePath">The report to start from; it is not changed unless it is also the target.</param>
        /// <param name="targetPath">Where the changed report is written; an existing file is replaced.</param>
        /// <param name="fields">Field name and the new value; check boxes take <c>Yes</c> or <c>Off</c>.</param>
        /// <returns>Success, or why nothing was written (the file is missing, damaged or locked, or a field is missing).</returns>
        Result WriteFields(string sourcePath, string targetPath, IReadOnlyDictionary<string, string> fields);

        /// <summary>
        /// Copies a report with all its fields locked (the finished report) or unlocked (a report fetched for editing).
        /// </summary>
        /// <param name="sourcePath">The report to start from.</param>
        /// <param name="targetPath">Where the copy is written; an existing file is replaced.</param>
        /// <param name="readOnly"><c>true</c> to lock every field, <c>false</c> to unlock every field.</param>
        /// <returns>Success, or why nothing was written.</returns>
        Result SetFieldsReadOnly(string sourcePath, string targetPath, bool readOnly);
    }
}
