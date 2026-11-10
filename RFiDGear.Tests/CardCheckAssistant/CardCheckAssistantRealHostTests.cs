using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using RFiDGear.Contracts;
using RFiDGear.ExtensionLibraries.CardCheck;
using RFiDGear.Extensions.CardCheckAssistant.Workflow;
using RFiDGear.Infrastructure.ExtensionHost;
using RFiDGear.Infrastructure.FileAccess;
using RFiDGear.Tests.CardCheck;

using Xunit;

using static RFiDGear.Tests.CardCheckAssistant.ViewModelTestSupport;

namespace RFiDGear.Tests.CardCheckAssistant
{
    /// <summary>The check runner against the real host (headless), with a real execution service that records what it receives.</summary>
    [Collection("AppDataPath")]
    public class CardCheckAssistantRealHostTests
    {
        private readonly RecordingCardCheckLog _log = new RecordingCardCheckLog();

        private CheckRunner Runner(IRFiDGearExtensionHost host, IReportFiles files) =>
            new CheckRunner(host.Project, host.Execution, files, CardCheckServices.CreateReportService(_log), null, _log);

        [Fact]
        public async Task ACheckRunThroughTheRealHost_LoadsTheProject_AndHandsTheVariablesToTheExecutionService()
        {
            var projectPath = ProjectFixtures.CreateProject(2);
            using var area = new TempArea();
            try
            {
                var service = new CapturingExecService();
                using var host = new HeadlessExtensionHost(new DatabaseReaderWriter(), service, "Test reader");
                var files = CardCheckServices.CreateReportFiles(_log);
                var location = new ReportLocation(area.Directory, true);
                var paths = files.Resolve(location, "J-42", "C-7").Value;

                var result = await Runner(host, files).RunAsync(
                    new CheckRunRequest(location, "J-42", "C-7", projectPath, null, ReportFile.Step1, null), null, CancellationToken.None);

                // The tasks of the fixture write no report, so the run ends without one: but it ran, on the real service.
                Assert.Equal(CheckFailureCodes.ReportMissing, result.Failure!.Code);
                Assert.Equal(ExecutionOutcome.Success, result.Execution!.Outcome);

                var received = Assert.IsAssignableFrom<IReadOnlyDictionary<string, string>>(service.CapturedRequest!.VariablesFromArgs);
                Assert.Equal("J-42", received["$JOBNUMBER"]);
                Assert.Equal("C-7", received["$CHIPNUMBER"]);
                Assert.Equal(paths.Step1, received["REPORTTARGETPATH"]);
                Assert.Equal(paths.Step1, service.CapturedRequest.ReportOutputPath);
                Assert.Equal(2, host.Project.TaskCount);
                Assert.Equal(Path.GetFullPath(projectPath), host.Project.CurrentPath);
            }
            finally
            {
                TryDelete(projectPath);
            }
        }

        [Fact]
        public async Task AFileThatIsNotAProject_IsReportedByTheHost_AndNothingRuns()
        {
            var notAProject = ProjectFixtures.CreateNotAnArchive();
            using var area = new TempArea();
            try
            {
                var service = new CapturingExecService();
                using var host = new HeadlessExtensionHost(new DatabaseReaderWriter(), service, string.Empty);
                var files = CardCheckServices.CreateReportFiles(_log);

                var result = await Runner(host, files).RunAsync(
                    new CheckRunRequest(new ReportLocation(area.Directory, true), "J-1", "1", notAProject, null, ReportFile.Step1, null), null, CancellationToken.None);

                Assert.Equal(CheckRunStatus.Failed, result.Status);
                Assert.Equal(CheckFailureCodes.LoadUnsupportedFormat, result.Failure!.Code);
                Assert.Null(service.CapturedRequest);
                Assert.Null(result.Execution);
            }
            finally
            {
                TryDelete(notAProject);
            }
        }
    }
}
