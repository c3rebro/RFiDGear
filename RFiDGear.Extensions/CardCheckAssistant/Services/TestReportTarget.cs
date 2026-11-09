using System;
using System.IO;

using RFiDGear.ExtensionLibraries.CardCheck;

namespace RFiDGear.Extensions.CardCheckAssistant.Services
{
    /// <summary>
    /// Puts the reports of a test run in a <c>Test</c> folder of their own: below the report folder of the settings if it
    /// is set and exists, otherwise below a fallback folder, so a test never needs a report folder to have been set up and
    /// never mixes with the reports of real jobs. The folder of the job inside it follows the setting "one folder for each
    /// job", and the names of the files follow the rules of the support library.
    /// </summary>
    internal sealed class TestReportTarget : ITestReportTarget
    {
        /// <summary>The name of the folder the reports of test runs are kept in.</summary>
        internal const string FolderName = "Test";

        private readonly ISettingsStore _settings;
        private readonly IReportFiles _files;
        private readonly string _fallbackFolder;

        /// <summary>Creates the target.</summary>
        /// <param name="settings">Where the report folder is read from.</param>
        /// <param name="files">Builds the paths of a job's files and makes the job's folder.</param>
        /// <param name="fallbackFolder">The folder the <c>Test</c> folder is made in when the settings name no usable report folder.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public TestReportTarget(ISettingsStore settings, IReportFiles files, string fallbackFolder)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _files = files ?? throw new ArgumentNullException(nameof(files));
            _fallbackFolder = fallbackFolder ?? throw new ArgumentNullException(nameof(fallbackFolder));
        }

        /// <inheritdoc/>
        public string Prepare(JobEntry job)
        {
            if (job == null)
            {
                throw new ArgumentNullException(nameof(job));
            }

            try
            {
                var report = _settings.Load().Settings.Report;
                var root = Path.Combine(UsableReportFolder(report.OutputPath) ?? _fallbackFolder, FolderName);

                // Resolve checks the root (it must be a full path) and the numbers (they must be usable as file names)
                // before anything is made, so none of them leaves a folder behind.
                var paths = _files.Resolve(new ReportLocation(root, report.CreateSubdirectory), job.JobNumber, job.ChipNumber);
                if (!paths.IsSuccess)
                {
                    return string.Empty;
                }

                Directory.CreateDirectory(root);

                return _files.EnsureOrderFolder(paths.Value).IsSuccess ? paths.Value.Final : string.Empty;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is NotSupportedException)
            {
                // A folder that cannot be made is no reason to stop the page: the report field is simply left alone.
                return string.Empty;
            }
        }

        // The report folder of the settings, if it is a full path to a folder that is there; a network share that is not
        // reachable just now, or a path that was never right, does not stop a test.
        private static string? UsableReportFolder(string path) =>
            !string.IsNullOrWhiteSpace(path) && Path.IsPathFullyQualified(path) && Directory.Exists(path) ? path : null;
    }
}
