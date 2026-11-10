using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using RFiDGear.Contracts;
using RFiDGear.ExtensionLibraries.CardCheck;
using RFiDGear.Extensions.CardCheckAssistant.Services;

namespace RFiDGear.Extensions.CardCheckAssistant.Workflow
{
    /// <summary>Where a check run is. The workflow shows this while it waits.</summary>
    internal enum CheckPhase
    {
        /// <summary>The paths are being worked out and checked (disk work).</summary>
        Preparing,

        /// <summary>RFiDGear is loading the project.</summary>
        LoadingProject,

        /// <summary>RFiDGear is running the project.</summary>
        Running,

        /// <summary>The run is over and the report it wrote is being read.</summary>
        ReadingReport
    }

    /// <summary>How a check run ended.</summary>
    internal enum CheckRunStatus
    {
        /// <summary>The run ended well and its report was read.</summary>
        Finished,

        /// <summary>Nothing came of it; <see cref="CheckRunResult.Failure"/> says why.</summary>
        Failed,

        /// <summary>The caller cancelled it.</summary>
        Cancelled
    }

    /// <summary>
    /// The stable codes of the failures a check run can end with, for the workflow to turn into texts. A failure of
    /// the library (a number that cannot name a file, an unreadable report) keeps the code the library gave it.
    /// </summary>
    internal static class CheckFailureCodes
    {
        /// <summary>The report to write or to continue from is not a step report, or both are the same.</summary>
        public const string RequestInvalid = "check.request.invalid";

        /// <summary>No project file was given.</summary>
        public const string ProjectRequired = "check.project.required";

        /// <summary>The project file is not a full path.</summary>
        public const string ProjectNotAbsolute = "check.project.notAbsolute";

        /// <summary>The project file does not exist.</summary>
        public const string ProjectNotFound = "check.project.notFound";

        /// <summary>The project in RFiDGear was replaced after the runner had loaded it, so it was not run.</summary>
        public const string ProjectChanged = "check.project.changed";

        /// <summary>The report the run is to continue from does not exist.</summary>
        public const string TemplateMissing = "check.template.missing";

        /// <summary>RFiDGear could not find the project.</summary>
        public const string LoadPathNotFound = "check.load.pathNotFound";

        /// <summary>RFiDGear could not read the file as a project.</summary>
        public const string LoadUnsupportedFormat = "check.load.unsupportedFormat";

        /// <summary>RFiDGear is busy with something else and did not load the project.</summary>
        public const string LoadBusy = "check.load.busy";

        /// <summary>RFiDGear did not load the project and did not say why.</summary>
        public const string LoadUnknown = "check.load.unknown";

        /// <summary>RFiDGear is busy with something else and did not start the run.</summary>
        public const string RunBusy = "check.run.busy";

        /// <summary>RFiDGear refused the run, because of the report path or the report to continue from.</summary>
        public const string RunRefused = "check.run.refused";

        /// <summary>RFiDGear ran the project and the run failed.</summary>
        public const string RunFailed = "check.run.failed";

        /// <summary>Something went wrong that nobody expected; the message names only the type of the exception.</summary>
        public const string RunUnexpected = "check.run.unexpected";

        /// <summary>The run ended well but did not write its report.</summary>
        public const string ReportMissing = "check.report.missing";
    }

    /// <summary>What a check run is to do.</summary>
    /// <param name="Location">Where the reports are kept.</param>
    /// <param name="JobNumber">The job number of the order; it names the report files and goes to the project as <c>$JOBNUMBER</c>.</param>
    /// <param name="ChipNumber">The chip number of the order; it names the report files and goes to the project as <c>$CHIPNUMBER</c>.</param>
    /// <param name="ProjectPath">The project file to run: a full path to a file that exists.</param>
    /// <param name="ProjectLanguage">
    /// The language of the report: if a project of the same name with <c>.deutsch</c> or <c>.englisch</c> before the
    /// extension is beside <paramref name="ProjectPath"/>, that one is run. <c>null</c> runs the project as given,
    /// which is what the run that reads the memory out wants: the old assistant looked for a variant only for the
    /// default project.
    /// </param>
    /// <param name="Report">The report this run writes: a step report, usually <see cref="ReportFile.Step1"/> or <see cref="ReportFile.Step3"/>.</param>
    /// <param name="TemplateReport">
    /// A step report the run is to continue from (it goes to the project as <c>REPORTTEMPLATEFILE</c>), for example
    /// <see cref="ReportFile.Step2"/> for the run that reads the memory out; it must exist and differ from
    /// <paramref name="Report"/>. <c>null</c> for none.
    /// </param>
    internal sealed record CheckRunRequest(
        ReportLocation Location,
        string JobNumber,
        string ChipNumber,
        string ProjectPath,
        ReportLanguage? ProjectLanguage,
        ReportFile Report,
        ReportFile? TemplateReport);

    /// <summary>What a check run came to.</summary>
    /// <param name="Status">How it ended.</param>
    /// <param name="Failure">Why it failed; <c>null</c> unless <paramref name="Status"/> is <see cref="CheckRunStatus.Failed"/>.</param>
    /// <param name="Execution">
    /// What RFiDGear answered for the run, with the outcome of each task; <c>null</c> if the run never started. It
    /// is there also when the run failed or was cancelled.
    /// </param>
    /// <param name="Findings">
    /// What the report says. There when <paramref name="Status"/> is <see cref="CheckRunStatus.Finished"/>. It is also
    /// there when the run itself failed but left a readable report, so that the workflow can show it; it is then not
    /// the verdict of the check, because a run that failed can leave a report whose flags were never set.
    /// </param>
    /// <param name="ReportPath">The path of the report the run writes; <c>null</c> if the run ended while it was preparing, before the project was loaded.</param>
    internal sealed record CheckRunResult(
        CheckRunStatus Status,
        Failure? Failure,
        ExecResult? Execution,
        ReportAnalysis? Findings,
        string? ReportPath);

    /// <summary>
    /// Runs one check project for one order and reads the report it wrote: the first run of the check (default project,
    /// writes the first report) and the run that reads the memory out after programming (custom project, continues from
    /// the second report and writes the third) are the same job with other arguments.
    /// <para>
    /// The steps: check the request and the project, work out the paths of the order's files
    /// (<see cref="IReportFiles"/>) and make its folder, have RFiDGear load the project, delete a report an earlier
    /// attempt left (so an old file is never read as the result of this run), run the project with the job number, the
    /// chip number, the report path and, if there is one, the report to continue from, and then read the report.
    /// Nothing is retried: a failed run is a result for the operator to act on (the one-attempt rule). A missing or
    /// unreadable report after a good run is a failure, never "the chip is not supported". Once the run has ended its
    /// report is read even if a cancel came meanwhile, because the work is done.
    /// </para>
    /// <para>
    /// Loading a project replaces the project in RFiDGear's main window; if it is replaced again before the run starts,
    /// the run is not started. The disk work runs on a worker thread, and the runner continues on the context of the
    /// caller (it never uses <c>ConfigureAwait(false)</c>), so the phase callback runs there. A callback that throws
    /// ends the run as an unexpected failure. A failure is a result; only a null argument throws.
    /// </para>
    /// </summary>
    internal sealed class CheckRunner
    {
        /// <summary>The variable that carries the job number into the project.</summary>
        internal const string JobNumberVariable = "$JOBNUMBER";

        /// <summary>The variable that carries the chip number into the project.</summary>
        internal const string ChipNumberVariable = "$CHIPNUMBER";

        /// <summary>The variable that carries the path of the report the project writes.</summary>
        internal const string ReportTargetVariable = "REPORTTARGETPATH";

        /// <summary>The variable that carries the path of the report the project continues from.</summary>
        internal const string ReportTemplateVariable = "REPORTTEMPLATEFILE";

        private readonly IProjectHost _project;
        private readonly ITaskExecHost _execution;
        private readonly IReportFiles _files;
        private readonly IReportService _reports;
        private readonly IPathChecker _paths;
        private readonly ICardCheckLog? _log;

        /// <summary>Creates a runner.</summary>
        /// <param name="project">RFiDGear's project service.</param>
        /// <param name="execution">RFiDGear's execution service.</param>
        /// <param name="files">The paths and the folder of an order's reports.</param>
        /// <param name="reports">Reads a report.</param>
        /// <param name="paths">Checks the project path; the real check against the file system if <c>null</c>.</param>
        /// <param name="log">Where an unexpected fault is noted (by type only); <c>null</c> for none.</param>
        /// <exception cref="ArgumentNullException">One of the services is null.</exception>
        public CheckRunner(
            IProjectHost project,
            ITaskExecHost execution,
            IReportFiles files,
            IReportService reports,
            IPathChecker? paths = null,
            ICardCheckLog? log = null)
        {
            _project = project ?? throw new ArgumentNullException(nameof(project));
            _execution = execution ?? throw new ArgumentNullException(nameof(execution));
            _files = files ?? throw new ArgumentNullException(nameof(files));
            _reports = reports ?? throw new ArgumentNullException(nameof(reports));
            _paths = paths ?? new FileSystemPathChecker();
            _log = log;
        }

        /// <summary>Runs the check and reads its report.</summary>
        /// <param name="request">What to run.</param>
        /// <param name="phaseChanged">Called when the run enters a phase; may be <c>null</c>.</param>
        /// <param name="ct">Cancels the run: before it starts, at the next task, or while it loads.</param>
        /// <returns>How the run ended. Never <c>null</c>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
        public Task<CheckRunResult> RunAsync(CheckRunRequest request, Action<CheckPhase>? phaseChanged, CancellationToken ct)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            return RunCoreAsync(request, phaseChanged, ct);
        }

        private static CheckRunResult Failed(Failure failure, Progress progress) =>
            new CheckRunResult(CheckRunStatus.Failed, failure, progress.Execution, null, progress.ReportPath);

        private static ExecutionRequest BuildRequest(CheckRunRequest request, OrderReportPaths paths, string reportPath)
        {
            var variables = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>(JobNumberVariable, request.JobNumber.Trim()),
                new KeyValuePair<string, string>(ChipNumberVariable, request.ChipNumber.Trim()),
                new KeyValuePair<string, string>(ReportTargetVariable, reportPath),
            };

            if (request.TemplateReport.HasValue)
            {
                variables.Add(new KeyValuePair<string, string>(ReportTemplateVariable, paths.PathOf(request.TemplateReport.Value)));
            }

            return new ExecutionRequest(variables);
        }

        private static Failure LoadFailure(ProjectOpenError error) => error switch
        {
            ProjectOpenError.PathNotFound => new Failure(FailureKind.NotFound, CheckFailureCodes.LoadPathNotFound, "RFiDGear did not find the project."),
            ProjectOpenError.UnsupportedFormat => new Failure(FailureKind.InvalidInput, CheckFailureCodes.LoadUnsupportedFormat, "The file is not a project RFiDGear can read."),
            ProjectOpenError.Busy => new Failure(FailureKind.Unreachable, CheckFailureCodes.LoadBusy, "RFiDGear is busy with something else."),
            _ => new Failure(FailureKind.Unexpected, CheckFailureCodes.LoadUnknown, "RFiDGear did not load the project."),
        };

        private static Failure? ProjectFailure(ProjectPathCheck check) => check switch
        {
            ProjectPathCheck.Valid => null,
            ProjectPathCheck.Required => new Failure(FailureKind.InvalidInput, CheckFailureCodes.ProjectRequired, "No project was given."),
            ProjectPathCheck.NotFullyQualified => new Failure(FailureKind.InvalidInput, CheckFailureCodes.ProjectNotAbsolute, "The project is not a full path."),
            _ => new Failure(FailureKind.NotFound, CheckFailureCodes.ProjectNotFound, "The project does not exist."),
        };

        private static bool IsStepReport(ReportFile file) =>
            file == ReportFile.Step1 || file == ReportFile.Step2 || file == ReportFile.Step3;

        private async Task<CheckRunResult> RunCoreAsync(CheckRunRequest request, Action<CheckPhase>? phaseChanged, CancellationToken ct)
        {
            var progress = new Progress();

            try
            {
                return await RunStepsAsync(request, phaseChanged, ct, progress);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return new CheckRunResult(CheckRunStatus.Cancelled, null, progress.Execution, null, progress.ReportPath);
            }
            catch (Exception ex)
            {
                // Only the type name goes on screen and into the log: the message may carry text nobody must see.
                _log?.Unexpected("check.run", UnexpectedFault.From(ex));

                return Failed(new Failure(FailureKind.Unexpected, CheckFailureCodes.RunUnexpected, "The check failed unexpectedly (" + ex.GetType().Name + ")."), progress);
            }
        }

        private async Task<CheckRunResult> RunStepsAsync(CheckRunRequest request, Action<CheckPhase>? phaseChanged, CancellationToken ct, Progress progress)
        {
            phaseChanged?.Invoke(CheckPhase.Preparing);
            var prepared = await Task.Run(() => Prepare(request), ct);
            if (prepared.Failure != null)
            {
                return Failed(prepared.Failure, progress);
            }

            var paths = prepared.Paths!;
            var reportPath = paths.PathOf(request.Report);
            progress.ReportPath = reportPath;

            phaseChanged?.Invoke(CheckPhase.LoadingProject);
            var load = await _project.LoadAsync(prepared.ProjectPath!, ct);
            if (!load.Success)
            {
                return Failed(LoadFailure(load.Error), progress);
            }

            var cleared = await Task.Run(() => _files.PrepareStep(paths, request.Report), ct);
            if (!cleared.IsSuccess)
            {
                return Failed(cleared.Failure!, progress);
            }

            // The host is free between the load and the run, and someone may have opened another project meanwhile.
            if (!string.Equals(_project.CurrentPath, load.CanonicalPath, StringComparison.OrdinalIgnoreCase))
            {
                return Failed(new Failure(FailureKind.Unreachable, CheckFailureCodes.ProjectChanged, "The project in RFiDGear was replaced before the run."), progress);
            }

            phaseChanged?.Invoke(CheckPhase.Running);
            var refusal = await ExecuteAsync(BuildRequest(request, paths, reportPath), ct, progress);
            if (refusal != null)
            {
                return Failed(refusal, progress);
            }

            var execution = progress.Execution!;
            if (execution.FailureReason == ExecutionFailureReason.Cancelled)
            {
                return new CheckRunResult(CheckRunStatus.Cancelled, null, execution, null, reportPath);
            }

            phaseChanged?.Invoke(CheckPhase.ReadingReport);

            // The run is over, so its report is read even if a cancel came meanwhile: the work is done and its answer
            // is not thrown away. The caller sees its own token.
            var read = await Task.Run(() => ReadReport(reportPath));

            if (execution.Outcome == ExecutionOutcome.Failure)
            {
                return new CheckRunResult(CheckRunStatus.Failed, new Failure(FailureKind.Unexpected, CheckFailureCodes.RunFailed, "The run failed."), execution, read.Findings, reportPath);
            }

            return read.Failure != null
                ? Failed(read.Failure, progress)
                : new CheckRunResult(CheckRunStatus.Finished, null, execution, read.Findings, reportPath);
        }

        // Asks RFiDGear to run the project. Returns why it refused, or null when it ran; the answer is in progress.
        private async Task<Failure?> ExecuteAsync(ExecutionRequest request, CancellationToken ct, Progress progress)
        {
            try
            {
                progress.Execution = await _execution.ExecuteAllAsync(request, ct);

                return null;
            }
            catch (ArgumentException)
            {
                return new Failure(FailureKind.InvalidInput, CheckFailureCodes.RunRefused, "RFiDGear refused the run.");
            }
            catch (InvalidOperationException ex) when (ex is not ObjectDisposedException)
            {
                return new Failure(FailureKind.Unreachable, CheckFailureCodes.RunBusy, "RFiDGear is busy with something else.");
            }
        }

        // All the disk work before the project is loaded; the caller runs it on a worker thread. What can be refused
        // without touching the disk is refused first, and the order's folder is made last.
        private (Failure? Failure, string? ProjectPath, OrderReportPaths? Paths) Prepare(CheckRunRequest request)
        {
            if (!IsStepReport(request.Report) || (request.TemplateReport.HasValue && (!IsStepReport(request.TemplateReport.Value) || request.TemplateReport.Value == request.Report)))
            {
                return (new Failure(FailureKind.InvalidInput, CheckFailureCodes.RequestInvalid, "The report to write and the report to continue from must be different step reports."), null, null);
            }

            var project = request.ProjectPath.Trim();
            var projectFailure = ProjectFailure(_paths.CheckProject(project));
            if (projectFailure != null)
            {
                return (projectFailure, null, null);
            }

            if (request.ProjectLanguage.HasValue)
            {
                project = ProjectVariants.Resolve(project, request.ProjectLanguage.Value, File.Exists);
            }

            // The library trims the numbers for the names; the project gets them trimmed in BuildRequest.
            var resolved = _files.Resolve(request.Location, request.JobNumber, request.ChipNumber);
            if (!resolved.IsSuccess)
            {
                return (resolved.Failure, null, null);
            }

            var paths = resolved.Value;
            if (request.TemplateReport.HasValue && !File.Exists(paths.PathOf(request.TemplateReport.Value)))
            {
                return (new Failure(FailureKind.NotFound, CheckFailureCodes.TemplateMissing, "The report to continue from does not exist."), null, null);
            }

            var folder = _files.EnsureOrderFolder(paths);

            return folder.IsSuccess ? (null, project, paths) : (folder.Failure, null, null);
        }

        // The report the run wrote, on a worker thread. A file that is not there is a failure of the run, whatever the
        // run said: PrepareStep removed the old one, so a file here was written by this run.
        private (Failure? Failure, ReportAnalysis? Findings) ReadReport(string reportPath)
        {
            if (!File.Exists(reportPath))
            {
                return (new Failure(FailureKind.NotFound, CheckFailureCodes.ReportMissing, "The run did not write the report."), null);
            }

            var analysis = _reports.Analyze(reportPath);

            return analysis.IsSuccess ? (null, analysis.Value) : (analysis.Failure, null);
        }

        // What a run has done so far, for the result of a run that ends early.
        private sealed class Progress
        {
            public ExecResult? Execution { get; set; }

            public string? ReportPath { get; set; }
        }
    }
}
