using System.IO;
using System.Linq;
using System.Threading.Tasks;

using RFiDGear.Infrastructure.ExtensionHost;
using RFiDGear.Infrastructure.FileAccess;

using Xunit;

using static RFiDGear.Tests.CardCheckAssistant.ViewModelTestSupport;

namespace RFiDGear.Tests.CardCheckAssistant
{
    /// <summary>
    /// The page against the real headless host and a generated project (never a customer's): the project is loaded by the
    /// real project service, and the variables arrive at the execution service the way a run in RFiDGear receives them.
    /// </summary>
    [Collection("AppDataPath")]
    public class CardCheckAssistantRealHostTests
    {
        [Fact]
        public async Task ARunThroughTheRealHost_LoadsTheProject_AndHandsTheVariablesToTheExecutionService()
        {
            var projectPath = ProjectFixtures.CreateProject(2);
            using var area = new TempArea();
            try
            {
                var service = new CapturingExecService();
                using var host = new HeadlessExtensionHost(new DatabaseReaderWriter(), service, "Test reader");

                await StaTestRunner.RunOnStaThreadAsync(async () =>
                {
                    using var viewModel = Create(host);
                    viewModel.Strings.Refresh("en-US");   // the real host reports the machine's own language
                    viewModel.ProjectPath = projectPath;
                    viewModel.JobNumber = "J-42";
                    viewModel.ChipNumber = "C-7";
                    viewModel.ReportTargetPath = area.ReportPath;

                    await viewModel.RunCommand.ExecuteAsync();

                    Assert.False(viewModel.HasProjectError, viewModel.ProjectError);
                    Assert.False(viewModel.HasReportTargetError, viewModel.ReportTargetError);
                    Assert.False(viewModel.HasRunError, viewModel.RunError);

                    // What the real execution service received.
                    var received = Assert.IsAssignableFrom<System.Collections.Generic.IReadOnlyDictionary<string, string>>(
                        service.CapturedRequest!.VariablesFromArgs);
                    Assert.Equal("J-42", received["$JOBNUMBER"]);
                    Assert.Equal("C-7", received["$CHIPNUMBER"]);
                    Assert.Equal(area.ReportPath, received["REPORTTARGETPATH"]);
                    Assert.Equal(area.ReportPath, service.CapturedRequest.ReportOutputPath);

                    // What the page shows of it.
                    Assert.Equal(2, host.Project.TaskCount);
                    Assert.Equal(Path.GetFullPath(projectPath), host.Project.CurrentPath);
                    Assert.True(viewModel.HasResult);
                    Assert.Equal("Success", viewModel.OutcomeText);
                    Assert.Equal(2, viewModel.Rows.Count);
                    Assert.All(viewModel.Rows, row => Assert.Equal("OK", row.ResultText));
                    Assert.Equal("Reader: Test reader (not connected)", viewModel.ReaderText);
                    Assert.Contains("(2 tasks)", viewModel.OpenProjectText, System.StringComparison.Ordinal);
                });
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
            try
            {
                var service = new CapturingExecService();
                using var host = new HeadlessExtensionHost(new DatabaseReaderWriter(), service, string.Empty);

                await StaTestRunner.RunOnStaThreadAsync(async () =>
                {
                    using var viewModel = Create(host);
                    viewModel.Strings.Refresh("en-US");   // the real host reports the machine's own language
                    viewModel.ProjectPath = notAProject;

                    await viewModel.RunCommand.ExecuteAsync();

                    Assert.True(viewModel.HasProjectError);
                    Assert.Null(service.CapturedRequest);
                    Assert.False(viewModel.HasResult);
                    Assert.False(viewModel.IsBusy);
                });
            }
            finally
            {
                TryDelete(notAProject);
            }
        }

        [Fact]
        public async Task AProjectWithNoTasks_RunsAndShowsAnEmptyList()
        {
            var projectPath = ProjectFixtures.CreateProject(0);
            try
            {
                using var host = new HeadlessExtensionHost(new DatabaseReaderWriter(), new CapturingExecService(), string.Empty);

                await StaTestRunner.RunOnStaThreadAsync(async () =>
                {
                    using var viewModel = Create(host);
                    viewModel.Strings.Refresh("en-US");   // the real host reports the machine's own language
                    viewModel.ProjectPath = projectPath;

                    await viewModel.RunCommand.ExecuteAsync();

                    Assert.True(viewModel.HasResult);
                    Assert.Empty(viewModel.Rows);
                    Assert.Equal("0 of 0 tasks failed", viewModel.SummaryText);
                });
            }
            finally
            {
                TryDelete(projectPath);
            }
        }

        [Fact]
        public async Task ThePageSeesTheRealHostsRunState_WhileItsOwnRunIsInProgress()
        {
            var projectPath = ProjectFixtures.CreateProject(1);
            try
            {
                var service = new BlockingExecService();
                using var host = new HeadlessExtensionHost(new DatabaseReaderWriter(), service, string.Empty);

                await StaTestRunner.RunOnStaThreadAsync(async () =>
                {
                    using var viewModel = Create(host);
                    viewModel.Strings.Refresh("en-US");   // the real host reports the machine's own language
                    viewModel.ProjectPath = projectPath;

                    var run = viewModel.RunCommand.ExecuteAsync();
                    await WaitUntil(() => host.Execution.IsRunning, "the real host to start the run");
                    await Flush();

                    // The real coordinator announced the start, and the page reads the state itself.
                    Assert.True(host.Execution.IsRunning);
                    Assert.True(viewModel.IsExecutionRunning);
                    Assert.False(viewModel.InputsEnabled);

                    service.Unblock();
                    await run;
                    await Flush();

                    Assert.False(host.Execution.IsRunning);
                    Assert.False(viewModel.IsExecutionRunning);
                    Assert.True(viewModel.InputsEnabled);
                    Assert.True(viewModel.HasResult);
                });
            }
            finally
            {
                TryDelete(projectPath);
            }
        }
    }
}
