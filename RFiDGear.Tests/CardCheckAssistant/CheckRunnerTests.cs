using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using RFiDGear.Contracts;
using RFiDGear.ExtensionLibraries.CardCheck;
using RFiDGear.Extensions.CardCheckAssistant.Workflow;
using RFiDGear.Tests.CardCheck;

using Xunit;

namespace RFiDGear.Tests.CardCheckAssistant
{
    public sealed class CheckRunnerTests : IDisposable
    {
        private const string Job = "KP-000005";
        private const string Chip = "5";

        private readonly TempArea _area = new TempArea();
        private readonly FakeProjectHost _project = new FakeProjectHost();
        private readonly FakeExecHost _execution = new FakeExecHost();
        private readonly RecordingCardCheckLog _log = new RecordingCardCheckLog();

        public CheckRunnerTests()
        {
            // What RFiDGear does with a good run: the project writes the report it was given.
            _execution.OnExecute = request => ReportFixtures.WriteFirstReport(request.Variables["REPORTTARGETPATH"]);
        }

        public void Dispose() => _area.Dispose();

        private IReportFiles Files => CardCheckServices.CreateReportFiles(_log);

        private ReportLocation Location => new ReportLocation(_area.Directory, true);

        private OrderReportPaths Paths => Files.Resolve(Location, Job, Chip).Value;

        private CheckRunner Runner(IReportFiles? files = null, IReportService? reports = null) =>
            new CheckRunner(_project, _execution, files ?? Files, reports ?? CardCheckServices.CreateReportService(_log), null, _log);

        private CheckRunRequest Request(
            ReportFile report = ReportFile.Step1,
            ReportFile? template = null,
            ReportLanguage? language = null,
            string? project = null,
            string job = Job) =>
            new CheckRunRequest(Location, job, Chip, project ?? _area.ProjectPath, language, report, template);

        private static void AssertVariables(IReadOnlyDictionary<string, string> actual, params (string Name, string Value)[] expected) =>
            Assert.Equal(
                expected.OrderBy(p => p.Name, StringComparer.Ordinal).Select(p => p.Name + "=" + p.Value),
                actual.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + "=" + p.Value));

        private void WriteStaleReport(ReportFile file)
        {
            var paths = Paths;
            Assert.True(Files.EnsureOrderFolder(paths).IsSuccess);
            ReportFixtures.WriteFirstReport(paths.PathOf(file), supported: "No", programmable: "No");
        }

        [Fact]
        public async Task AGoodRun_ReadsTheReportItWrote_AndSendsTheJobTheChipAndTheReportPath()
        {
            var phases = new List<CheckPhase>();
            using var cts = new CancellationTokenSource();

            var result = await Runner().RunAsync(Request(), phases.Add, cts.Token);

            var report = Path.Combine(_area.Directory, Job, Job + "-" + Chip + ".step1.pdf");
            Assert.Equal(CheckRunStatus.Finished, result.Status);
            Assert.Null(result.Failure);
            Assert.Equal(report, result.ReportPath);
            Assert.True(result.Findings!.Supported);
            Assert.True(result.Findings.Programmable);
            Assert.Equal("1760", result.Findings.FreeMemoryText);
            Assert.Equal("MIFARE DESFire EV2", result.Findings.ChipType);
            Assert.Same(_execution.Result, result.Execution);
            AssertVariables(
                Assert.Single(_execution.Requests).Variables,
                ("$JOBNUMBER", Job),
                ("$CHIPNUMBER", Chip),
                ("REPORTTARGETPATH", report));
            Assert.Equal(new[] { _area.ProjectPath }, _project.LoadedPaths);
            Assert.Equal(new[] { CheckPhase.Preparing, CheckPhase.LoadingProject, CheckPhase.Running, CheckPhase.ReadingReport }, phases);

            // The caller's token reaches the host, so a cancel can stop the load and the run.
            Assert.Equal(cts.Token, Assert.Single(_project.LoadTokens));
            Assert.Equal(cts.Token, Assert.Single(_execution.RunTokens));
        }

        [Fact]
        public async Task NumbersWithBlanksAround_AreTrimmed_ForTheFilesAndForTheProject()
        {
            var request = new CheckRunRequest(Location, "  " + Job + " ", " " + Chip + "  ", _area.ProjectPath, null, ReportFile.Step1, null);

            var result = await Runner().RunAsync(request, null, CancellationToken.None);

            var report = Path.Combine(_area.Directory, Job, Job + "-" + Chip + ".step1.pdf");
            Assert.Equal(CheckRunStatus.Finished, result.Status);
            Assert.Equal(report, result.ReportPath);
            AssertVariables(
                Assert.Single(_execution.Requests).Variables,
                ("$JOBNUMBER", Job),
                ("$CHIPNUMBER", Chip),
                ("REPORTTARGETPATH", report));
        }

        [Fact]
        public async Task ARunThatContinuesFromAReport_SendsItsPath_AndWritesTheThirdReport()
        {
            var paths = Paths;
            Assert.True(Files.EnsureOrderFolder(paths).IsSuccess);
            ReportFixtures.WriteFirstReport(paths.Step2);

            var result = await Runner().RunAsync(Request(ReportFile.Step3, ReportFile.Step2), null, CancellationToken.None);

            Assert.Equal(CheckRunStatus.Finished, result.Status);
            Assert.Equal(paths.Step3, result.ReportPath);
            AssertVariables(
                Assert.Single(_execution.Requests).Variables,
                ("$JOBNUMBER", Job),
                ("$CHIPNUMBER", Chip),
                ("REPORTTARGETPATH", paths.Step3),
                ("REPORTTEMPLATEFILE", paths.Step2));
        }

        [Fact]
        public async Task AReportToContinueFromThatIsNotThere_IsAFailure_BeforeAnythingIsLoaded()
        {
            var result = await Runner().RunAsync(Request(ReportFile.Step3, ReportFile.Step2), null, CancellationToken.None);

            Assert.Equal(CheckRunStatus.Failed, result.Status);
            Assert.Equal(CheckFailureCodes.TemplateMissing, result.Failure!.Code);
            Assert.Empty(_project.LoadedPaths);
            Assert.Empty(_execution.Requests);
            Assert.False(Directory.Exists(Paths.Folder));
        }

        [Theory]
        [InlineData(ReportFile.Final, null)]
        [InlineData(ReportFile.Step3, ReportFile.Step3)]
        [InlineData(ReportFile.Step3, ReportFile.Final)]
        public async Task AReportThatIsNotAStepReport_OrTheSameTwice_IsRefused_BeforeAnythingIsDone(ReportFile report, ReportFile? template)
        {
            var result = await Runner().RunAsync(Request(report, template), null, CancellationToken.None);

            Assert.Equal(CheckRunStatus.Failed, result.Status);
            Assert.Equal(CheckFailureCodes.RequestInvalid, result.Failure!.Code);
            Assert.Empty(_project.LoadedPaths);
            Assert.False(Directory.Exists(Paths.Folder));
        }

        [Theory]
        [InlineData(ReportLanguage.German, "check.deutsch.rfPrj")]
        [InlineData(ReportLanguage.English, "check.englisch.rfPrj")]
        [InlineData(null, "check.rfPrj")]
        public async Task TheProjectOfTheReportLanguage_IsRunInsteadOfTheOneGiven_WhenItIsBesideIt(ReportLanguage? language, string loaded)
        {
            File.WriteAllText(Path.Combine(_area.Directory, "check.deutsch.rfPrj"), "not read by the fake host");
            File.WriteAllText(Path.Combine(_area.Directory, "check.englisch.rfPrj"), "not read by the fake host");

            await Runner().RunAsync(Request(language: language), null, CancellationToken.None);

            Assert.Equal(new[] { Path.Combine(_area.Directory, loaded) }, _project.LoadedPaths);
        }

        [Theory]
        [InlineData("", CheckFailureCodes.ProjectRequired)]
        [InlineData("check.rfPrj", CheckFailureCodes.ProjectNotAbsolute)]
        [InlineData("<missing>", CheckFailureCodes.ProjectNotFound)]
        public async Task AProjectThatCannotBeRun_IsAFailure_BeforeAnythingIsLoaded(string project, string code)
        {
            var path = project == "<missing>" ? Path.Combine(_area.Directory, "nothere.rfPrj") : project;

            var result = await Runner().RunAsync(Request(project: path), null, CancellationToken.None);

            Assert.Equal(CheckRunStatus.Failed, result.Status);
            Assert.Equal(code, result.Failure!.Code);
            Assert.Empty(_project.LoadedPaths);
        }

        [Fact]
        public async Task ANumberThatCannotNameAFile_IsTheLibrarysFailure_BeforeAnythingIsLoaded()
        {
            var result = await Runner().RunAsync(Request(job: "  "), null, CancellationToken.None);

            Assert.Equal(CheckRunStatus.Failed, result.Status);
            Assert.StartsWith("report.", result.Failure!.Code, StringComparison.Ordinal);
            Assert.Empty(_project.LoadedPaths);
            Assert.Empty(_execution.Requests);
        }

        [Fact]
        public async Task AReportAnEarlierAttemptLeft_IsRemovedBeforeTheRun_AndNeverReadAsItsResult()
        {
            WriteStaleReport(ReportFile.Step1);
            _execution.OnExecute = null;   // this run writes nothing

            var result = await Runner().RunAsync(Request(), null, CancellationToken.None);

            Assert.Equal(CheckRunStatus.Failed, result.Status);
            Assert.Equal(CheckFailureCodes.ReportMissing, result.Failure!.Code);
            Assert.NotNull(result.Execution);
            Assert.False(File.Exists(Paths.Step1));
        }

        [Theory]
        [InlineData(ProjectOpenError.PathNotFound, CheckFailureCodes.LoadPathNotFound)]
        [InlineData(ProjectOpenError.UnsupportedFormat, CheckFailureCodes.LoadUnsupportedFormat)]
        [InlineData(ProjectOpenError.Busy, CheckFailureCodes.LoadBusy)]
        [InlineData(ProjectOpenError.Unknown, CheckFailureCodes.LoadUnknown)]
        public async Task AProjectThatDoesNotLoad_IsAFailure_AndNothingIsRun_AndTheEarlierReportStays(ProjectOpenError error, string code)
        {
            WriteStaleReport(ReportFile.Step1);
            _project.LoadAnswer = _ => ProjectOpenResult.Fail(error);

            var result = await Runner().RunAsync(Request(), null, CancellationToken.None);

            Assert.Equal(CheckRunStatus.Failed, result.Status);
            Assert.Equal(code, result.Failure!.Code);
            Assert.Single(_project.LoadedPaths);   // one attempt: the caller decides whether to try again
            Assert.Empty(_execution.Requests);
            Assert.True(File.Exists(Paths.Step1));
        }

        [Fact]
        public async Task ARunThatFails_IsAFailureWithTheTaskOutcomes_AndItsReportIsNotRead()
        {
            _execution.Result = FakeResults.WithOutcomes(
                ExecutionOutcome.Failure,
                ExecutionFailureReason.Faulted,
                new TaskOutcome(0, "1", "CommonTaskViewModel", false, "Unknown"));

            var result = await Runner().RunAsync(Request(), null, CancellationToken.None);

            // The good report that the fake host wrote is there; it is shown, but a failed run is still a failure.
            Assert.True(File.Exists(result.ReportPath!));
            Assert.Equal(CheckRunStatus.Failed, result.Status);
            Assert.Equal(CheckFailureCodes.RunFailed, result.Failure!.Code);
            Assert.Same(_execution.Result, result.Execution);
            Assert.NotNull(result.Findings);
        }

        [Fact]
        public async Task ARunThatFailsAndLeavesNoReport_IsTheFailureOfTheRun_NotAMissingReport()
        {
            _execution.OnExecute = null;
            _execution.Result = FakeResults.WithOutcomes(
                ExecutionOutcome.Failure,
                ExecutionFailureReason.Faulted,
                new TaskOutcome(0, "1", "CommonTaskViewModel", false, "Unknown"));

            var result = await Runner().RunAsync(Request(), null, CancellationToken.None);

            Assert.Equal(CheckFailureCodes.RunFailed, result.Failure!.Code);
            Assert.Null(result.Findings);
        }

        [Fact]
        public async Task ACancelDuringTheLastTask_DoesNotThrowAwayARunThatEndedWell()
        {
            using var cts = new CancellationTokenSource();
            _execution.OnExecute = request =>
            {
                ReportFixtures.WriteFirstReport(request.Variables["REPORTTARGETPATH"]);
                cts.Cancel();   // the task that writes the report cannot be interrupted, so the run ends well
            };

            var result = await Runner().RunAsync(Request(), null, cts.Token);

            Assert.Equal(CheckRunStatus.Finished, result.Status);
            Assert.NotNull(result.Findings);
        }

        [Fact]
        public async Task AReportThatCannotBeCleared_IsAFailure_AndNothingIsRun()
        {
            var paths = Paths;
            Assert.True(Files.EnsureOrderFolder(paths).IsSuccess);
            Directory.CreateDirectory(paths.Step1);   // a folder where the report belongs cannot be deleted as a file

            var result = await Runner().RunAsync(Request(), null, CancellationToken.None);

            Assert.Equal(CheckRunStatus.Failed, result.Status);
            Assert.StartsWith("report.", result.Failure!.Code, StringComparison.Ordinal);
            Assert.Empty(_execution.Requests);
        }

        [Fact]
        public async Task AFolderThatCannotBeMade_IsAFailure_BeforeAnythingIsLoaded()
        {
            File.WriteAllText(Paths.Folder, "a file where the order's folder belongs");

            var result = await Runner().RunAsync(Request(), null, CancellationToken.None);

            Assert.Equal(CheckRunStatus.Failed, result.Status);
            Assert.StartsWith("report.", result.Failure!.Code, StringComparison.Ordinal);
            Assert.Empty(_project.LoadedPaths);
        }

        [Fact]
        public async Task AProjectReplacedBetweenTheLoadAndTheRun_IsNotRun()
        {
            var files = new SpyReportFiles(Files) { OnPrepareStep = () => _project.CurrentPath = Path.Combine(_area.Directory, "another.rfPrj") };

            var result = await Runner(files).RunAsync(Request(), null, CancellationToken.None);

            Assert.Equal(CheckRunStatus.Failed, result.Status);
            Assert.Equal(CheckFailureCodes.ProjectChanged, result.Failure!.Code);
            Assert.Empty(_execution.Requests);
        }

        [Fact]
        public async Task ARunWithIntentionalFailures_IsFinished_BecauseTheProjectFailsSomeChecksOnPurpose()
        {
            _execution.Result = FakeResults.WithOutcomes(
                ExecutionOutcome.CompletedWithIntentionalFailures,
                ExecutionFailureReason.None,
                new TaskOutcome(0, "1", "CommonTaskViewModel", false, "ProtocolConstraint"),
                new TaskOutcome(1, "2", "CommonTaskViewModel", true, "NoError"));

            var result = await Runner().RunAsync(Request(), null, CancellationToken.None);

            Assert.Equal(CheckRunStatus.Finished, result.Status);
            Assert.NotNull(result.Findings);
        }

        [Fact]
        public async Task AGoodRunThatLeavesNoReport_IsAFailure_NotAChipThatIsNotSupported()
        {
            _execution.OnExecute = null;

            var result = await Runner().RunAsync(Request(), null, CancellationToken.None);

            Assert.Equal(CheckRunStatus.Failed, result.Status);
            Assert.Equal(CheckFailureCodes.ReportMissing, result.Failure!.Code);
            Assert.Null(result.Findings);
        }

        [Fact]
        public async Task AReportThatCannotBeRead_IsTheLibrarysFailure_WithTheRunAttached()
        {
            _execution.OnExecute = request => File.WriteAllText(request.Variables["REPORTTARGETPATH"], "this is not a PDF");

            var result = await Runner().RunAsync(Request(), null, CancellationToken.None);

            Assert.Equal(CheckRunStatus.Failed, result.Status);
            Assert.StartsWith("report.", result.Failure!.Code, StringComparison.Ordinal);
            Assert.NotNull(result.Execution);
            Assert.Null(result.Findings);
        }

        [Fact]
        public async Task ARunTheHostCancelled_IsCancelled_AndKeepsWhatTheHostAnswered()
        {
            _execution.Result = FakeResults.WithOutcomes(
                ExecutionOutcome.Failure,
                ExecutionFailureReason.Cancelled,
                new TaskOutcome(0, "1", "CommonTaskViewModel", true, "NoError"));

            var result = await Runner().RunAsync(Request(), null, CancellationToken.None);

            Assert.Equal(CheckRunStatus.Cancelled, result.Status);
            Assert.Null(result.Failure);
            Assert.Same(_execution.Result, result.Execution);
        }

        [Fact]
        public async Task ACancelBeforeTheStart_RunsNothing()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var result = await Runner().RunAsync(Request(), null, cts.Token);

            Assert.Equal(CheckRunStatus.Cancelled, result.Status);
            Assert.Empty(_project.LoadedPaths);
            Assert.Empty(_execution.Requests);
        }

        [Fact]
        public async Task ACancelWhileTheProjectLoads_IsCancelled_AndNothingIsRun()
        {
            using var cts = new CancellationTokenSource();
            _project.LoadAnswer = _ =>
            {
                cts.Cancel();
                throw new OperationCanceledException(cts.Token);
            };

            var result = await Runner().RunAsync(Request(), null, cts.Token);

            Assert.Equal(CheckRunStatus.Cancelled, result.Status);
            Assert.Empty(_execution.Requests);
        }

        [Fact]
        public async Task AHostThatIsBusy_IsAFailure_AndTheRunIsNotRepeated()
        {
            _execution.ThrowOnExecute = new InvalidOperationException("busy");

            var result = await Runner().RunAsync(Request(), null, CancellationToken.None);

            Assert.Equal(CheckRunStatus.Failed, result.Status);
            Assert.Equal(CheckFailureCodes.RunBusy, result.Failure!.Code);
            Assert.Null(result.Execution);
            Assert.Single(_execution.Requests);
        }

        [Fact]
        public async Task AHostThatIsShutDown_IsNotCalledBusy()
        {
            _execution.ThrowOnExecute = new ObjectDisposedException("host");

            var result = await Runner().RunAsync(Request(), null, CancellationToken.None);

            Assert.Equal(CheckRunStatus.Failed, result.Status);
            Assert.Equal(CheckFailureCodes.RunUnexpected, result.Failure!.Code);
        }

        [Fact]
        public async Task ARunTheHostRefuses_IsAFailure()
        {
            _execution.ThrowOnExecute = new ArgumentException("the report target");

            var result = await Runner().RunAsync(Request(), null, CancellationToken.None);

            Assert.Equal(CheckRunStatus.Failed, result.Status);
            Assert.Equal(CheckFailureCodes.RunRefused, result.Failure!.Code);
            Assert.Single(_execution.Requests);
        }

        [Fact]
        public async Task AnUnexpectedFault_IsAFailure_ThatNamesOnlyTheTypeOfTheException()
        {
            _execution.ThrowOnExecute = new FormatException("secret text from the host");

            var result = await Runner().RunAsync(Request(), null, CancellationToken.None);

            Assert.Equal(CheckRunStatus.Failed, result.Status);
            Assert.Equal(CheckFailureCodes.RunUnexpected, result.Failure!.Code);
            Assert.Contains("FormatException", result.Failure.SafeMessage, StringComparison.Ordinal);
            Assert.DoesNotContain("secret", result.Failure.SafeMessage, StringComparison.Ordinal);
            Assert.Single(_execution.Requests);

            // The fault is logged by type, never by message.
            Assert.Contains("FormatException", _log.Everything, StringComparison.Ordinal);
            Assert.DoesNotContain("secret", _log.Everything, StringComparison.Ordinal);
        }

        [Fact]
        public async Task TheDiskWork_RunsOffTheCallersThread()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var caller = Environment.CurrentManagedThreadId;
                var files = new SpyReportFiles(Files);
                var reports = new SpyReportService(CardCheckServices.CreateReportService(_log));
                var phaseThreads = new List<int>();

                var result = await Runner(files, reports).RunAsync(Request(), _ => phaseThreads.Add(Environment.CurrentManagedThreadId), CancellationToken.None);

                Assert.Equal(CheckRunStatus.Finished, result.Status);
                Assert.Equal(4, files.Threads.Count + reports.Threads.Count);
                Assert.DoesNotContain(caller, files.Threads.Concat(reports.Threads));

                // The phases are announced where the caller awaits.
                Assert.Equal(4, phaseThreads.Count);
                Assert.All(phaseThreads, id => Assert.Equal(caller, id));
            });
        }

        [Fact]
        public void Arguments_AreChecked()
        {
            var reports = CardCheckServices.CreateReportService(_log);

            Assert.Throws<ArgumentNullException>(() => new CheckRunner(null!, _execution, Files, reports));
            Assert.Throws<ArgumentNullException>(() => new CheckRunner(_project, null!, Files, reports));
            Assert.Throws<ArgumentNullException>(() => new CheckRunner(_project, _execution, null!, reports));
            Assert.Throws<ArgumentNullException>(() => new CheckRunner(_project, _execution, Files, null!));
            Assert.Throws<ArgumentNullException>(() => { _ = Runner().RunAsync(null!, null, CancellationToken.None); });
        }

        // Records the thread each call arrives on: Resolve, EnsureOrderFolder and PrepareStep are disk work or may be.
        private sealed class SpyReportFiles : IReportFiles
        {
            private readonly IReportFiles _inner;

            public SpyReportFiles(IReportFiles inner) => _inner = inner;

            public List<int> Threads { get; } = new List<int>();

            public Action? OnPrepareStep { get; set; }

            public Result<OrderReportPaths> Resolve(ReportLocation location, string jobNumber, string chipNumber)
            {
                Threads.Add(Environment.CurrentManagedThreadId);
                return _inner.Resolve(location, jobNumber, chipNumber);
            }

            public Result EnsureOrderFolder(OrderReportPaths paths)
            {
                Threads.Add(Environment.CurrentManagedThreadId);
                return _inner.EnsureOrderFolder(paths);
            }

            public Result PrepareStep(OrderReportPaths paths, ReportFile file)
            {
                Threads.Add(Environment.CurrentManagedThreadId);
                var result = _inner.PrepareStep(paths, file);
                OnPrepareStep?.Invoke();

                return result;
            }

            public CleanupReport Cleanup(OrderReportPaths paths, CleanupScope scope, bool removeTemporaryReports) =>
                _inner.Cleanup(paths, scope, removeTemporaryReports);
        }

        private sealed class SpyReportService : IReportService
        {
            private readonly IReportService _inner;

            public SpyReportService(IReportService inner) => _inner = inner;

            public List<int> Threads { get; } = new List<int>();

            public Result<ReportAnalysis> Analyze(string reportPath)
            {
                Threads.Add(Environment.CurrentManagedThreadId);
                return _inner.Analyze(reportPath);
            }

            public Result WriteFields(string sourcePath, string targetPath, IReadOnlyDictionary<string, string> fields) =>
                _inner.WriteFields(sourcePath, targetPath, fields);

            public Result SetFieldsReadOnly(string sourcePath, string targetPath, bool readOnly) =>
                _inner.SetFieldsReadOnly(sourcePath, targetPath, readOnly);
        }
    }
}
