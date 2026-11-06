using System;
using System.Collections.Generic;

namespace RFiDGear.ExtensionLibraries.CardCheck
{
    /// <summary>What <see cref="IReportFiles.Cleanup"/> did.</summary>
    /// <param name="Removed">The files that existed and were deleted.</param>
    /// <param name="Failed">The files that exist but could not be deleted (open in a viewer, no permission).</param>
    public sealed record CleanupReport(IReadOnlyList<ReportFile> Removed, IReadOnlyList<ReportFile> Failed)
    {
        /// <summary>A report for a cleanup that had nothing to do.</summary>
        public static CleanupReport Nothing { get; } = new CleanupReport(Array.Empty<ReportFile>(), Array.Empty<ReportFile>());
    }

    /// <summary>
    /// The files of one order's check: where they are, making their folder, clearing a file before a run that is
    /// to write it, and removing working files afterwards.
    /// </summary>
    public interface IReportFiles
    {
        /// <summary>
        /// Builds every path of an order's check. This is the only place paths are built, so the rules for names
        /// live here: the job and chip number become file names, a name Windows would refuse or misread is
        /// rejected, and a character it forbids is replaced by <c>_</c> with a short hash of the original added so
        /// that two different numbers never end up with the same name. Nothing is touched on disk.
        /// </summary>
        /// <param name="location">Where reports are kept.</param>
        /// <param name="jobNumber">The job number of the order; becomes the folder name and the start of each file name.</param>
        /// <param name="chipNumber">The chip number of the order; becomes the end of each file name.</param>
        /// <returns>The paths, or why they cannot be built.</returns>
        Result<OrderReportPaths> Resolve(ReportLocation location, string jobNumber, string chipNumber);

        /// <summary>
        /// Makes sure the folder for the order's files exists. The report output folder itself must already
        /// exist; only the order's own subfolder is created.
        /// </summary>
        /// <param name="paths">The paths from <see cref="Resolve"/>.</param>
        /// <returns>Success, or why the folder is not there.</returns>
        Result EnsureOrderFolder(OrderReportPaths paths);

        /// <summary>
        /// Deletes a working report that an earlier attempt left behind, before the run that is to write it. A file
        /// that is not replaced would otherwise be read as the result of the new run. Only step reports can be
        /// prepared; the final report is never deleted here.
        /// </summary>
        /// <param name="paths">The paths from <see cref="Resolve"/>.</param>
        /// <param name="file">Which step report: <see cref="ReportFile.Step1"/>, <see cref="ReportFile.Step2"/> or <see cref="ReportFile.Step3"/>.</param>
        /// <returns>Success (also when there was nothing to delete), or why the file could not be removed.</returns>
        Result PrepareStep(OrderReportPaths paths, ReportFile file);

        /// <summary>
        /// Removes the working reports a finished check no longer needs. Never throws because of a file: a file that
        /// cannot be deleted is reported and logged and the rest is still tried. The final report is never removed.
        /// <list type="bullet">
        /// <item><description><see cref="CleanupScope.NotSupported"/>: the first and second reports, if <paramref name="removeTemporaryReports"/>.</description></item>
        /// <item><description><see cref="CleanupScope.SupportedOnly"/>: nothing.</description></item>
        /// <item><description><see cref="CleanupScope.Programmed"/>: the first, second and third reports, if <paramref name="removeTemporaryReports"/>.</description></item>
        /// <item><description><see cref="CleanupScope.ReEdit"/>: the downloaded report and its working copy, always.</description></item>
        /// </list>
        /// </summary>
        /// <param name="paths">The paths from <see cref="Resolve"/>.</param>
        /// <param name="scope">How the check ended.</param>
        /// <param name="removeTemporaryReports">The operator's setting for removing working reports.</param>
        /// <returns>What was removed and what could not be.</returns>
        CleanupReport Cleanup(OrderReportPaths paths, CleanupScope scope, bool removeTemporaryReports);
    }
}
