using System;

namespace RFiDGear.ExtensionLibraries.CardCheck
{
    /// <summary>What a check does when it ends: which report becomes the final one and which working files may go.</summary>
    /// <param name="Source">The report the final (locked) report is made from.</param>
    /// <param name="Cleanup">How the check ended, which decides what <see cref="IReportFiles.Cleanup"/> removes.</param>
    public sealed record FinishPlan(ReportFile Source, CleanupScope Cleanup);

    /// <summary>
    /// How a check ends, as a function of what the checks found. The old assistant spread these choices over its
    /// pages; here they are in one place, so the workflow asks instead of deciding.
    /// </summary>
    public static class FinishRules
    {
        /// <summary>
        /// How a check ends right after the first run, or <c>null</c> when it does not end there. A chip that is not
        /// supported, one that is recognised but has too little memory for any template, and a report that
        /// contradicts itself all finish with the first report. The first two follow the old assistant: nothing is
        /// removed for a recognised chip, the working reports of an unsupported chip may be. A contradicting report
        /// keeps its files, because the operator is asked to look at them (the old assistant offered no way to finish
        /// such a report; the workflow lets the operator decide).
        /// </summary>
        /// <param name="verdict">What the first run found.</param>
        /// <returns>The plan, or <c>null</c> for <see cref="ChipVerdict.Programmable"/>: programming follows.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="verdict"/> is not a defined value.</exception>
        public static FinishPlan? AfterFirstRun(ChipVerdict verdict) => verdict switch
        {
            ChipVerdict.NotSupported => new FinishPlan(ReportFile.Step1, CleanupScope.NotSupported),
            ChipVerdict.SupportedOnly => new FinishPlan(ReportFile.Step1, CleanupScope.SupportedOnly),
            ChipVerdict.Indeterminate => new FinishPlan(ReportFile.Step1, CleanupScope.SupportedOnly),
            ChipVerdict.Programmable => null,
            _ => throw new ArgumentOutOfRangeException(nameof(verdict), verdict, "Unknown verdict."),
        };

        /// <summary>
        /// How a check ends after the chip was programmed and read out again: with the report of that last run, or,
        /// if that run left none, with the report made before programming (as the old assistant did).
        /// </summary>
        /// <param name="readOutReportExists">Whether the report of the last run (<see cref="ReportFile.Step3"/>) exists.</param>
        /// <returns>The plan.</returns>
        public static FinishPlan AfterProgramming(bool readOutReportExists) =>
            new FinishPlan(readOutReportExists ? ReportFile.Step3 : ReportFile.Step2, CleanupScope.Programmed);
    }
}
