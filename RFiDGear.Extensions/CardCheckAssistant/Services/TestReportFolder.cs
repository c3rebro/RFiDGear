using System.IO;

using RFiDGear.ExtensionLibraries.CardCheck;

namespace RFiDGear.Extensions.CardCheckAssistant.Services
{
    /// <summary>
    /// Where the reports of test runs go: a <c>Test</c> folder of their own, below the report folder of the settings if
    /// it is a full path to a folder that exists, otherwise below a fallback folder, so a test never needs a report folder to have been set up
    /// and never mixes with the reports of real jobs.
    /// </summary>
    internal static class TestReportFolder
    {
        /// <summary>The name of the folder the reports of test runs are kept in.</summary>
        internal const string FolderName = "Test";

        /// <summary>The folder the reports of a test run are kept in. Nothing is made on the disk.</summary>
        /// <param name="report">The report settings.</param>
        /// <param name="fallbackFolder">The folder the <c>Test</c> folder is made in when the settings name no usable report folder.</param>
        /// <returns>The path of the <c>Test</c> folder.</returns>
        internal static string Root(ReportSettings report, string fallbackFolder) =>
            Path.Combine(UsableReportFolder(report.OutputPath) ?? fallbackFolder, FolderName);

        // The report folder of the settings, if it is a full path to a folder that is there; a network share that is not
        // reachable just now, or a path that was never right, does not stop a test.
        private static string? UsableReportFolder(string path) =>
            !string.IsNullOrWhiteSpace(path) && Path.IsPathFullyQualified(path) && Directory.Exists(path) ? path : null;
    }
}
