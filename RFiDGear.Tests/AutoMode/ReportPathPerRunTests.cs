using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using RFiDGear.Services;
using RFiDGear.Services.TaskExecution;
using RFiDGear.Tests.Reader;
using RFiDGear.ViewModel;
using RFiDGear.ViewModel.TaskSetupViewModels;

using Xunit;

namespace RFiDGear.Tests.AutoMode
{
    /// <summary>
    /// The report target of the main window's own runs. A target of the start-up arguments with a <c>?</c>, <c>??</c> or <c>???</c>
    /// (<c>REPORTTARGETPATH=C:\Reports\card-???.pdf</c>) promises the next free number, and was resolved once, at start-up, and kept
    /// for every later run: in automatic mode every card overwrote <c>card-001.pdf</c>. The pattern is now resolved for each run; a
    /// fixed path stays a fixed path; and a path a person chose in the window when a run had none is kept until the report target
    /// paths are reset.
    /// </summary>
    [Collection("SettingsFileAccess")]
    public class ReportPathPerRunTests
    {
        // Runs a test in the real main window with a card on the reader and a folder of its own. The execution service does what the real
        // one does with a report path: the report is "written" to the path of the request, so the next free number moves on.
        private static Task WithWindowAsync(Func<MainWindowViewModel, FakeTaskExecutionService, string, Task> test) =>
            AutoModeMainWindowTests.WithViewModel(async (viewModel, provider, exec, _) =>
            {
                var folder = Path.Combine(Path.GetTempPath(), "RFiDGear.Tests", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(folder);

                try
                {
                    viewModel.ChipTasks.TaskCollection.Add(new CommonTaskViewModel());
                    provider.Card = AutoModeMainWindowTests.Card("0123456789");
                    exec.OnExecute = request =>
                    {
                        if (request.ReportOutputPath != null)
                        {
                            File.WriteAllText(request.ReportOutputPath, "a report");
                        }

                        return new TaskExecutionResult { TerminalStatus = TaskLoopTerminalStatus.Success, ReportOutputPath = request.ReportOutputPath };
                    };

                    await test(viewModel, exec, folder);
                }
                finally
                {
                    Directory.Delete(folder, recursive: true);
                }
            });

        private static void StartWith(MainWindowViewModel viewModel, string reportTarget) =>
            viewModel.ApplyStartupArguments(new StartupArgumentProcessor().Process(new[] { "REPORTTARGETPATH=" + reportTarget }));

        private static async Task RunAsync(MainWindowViewModel viewModel, int times)
        {
            for (var run = 0; run < times; run++)
            {
                await viewModel.WriteToChipOnceCommand.ExecuteAsync(null);
            }
        }

        private static List<string?> PathsOf(FakeTaskExecutionService exec) => exec.Requests.Select(request => request.ReportOutputPath).ToList();

        [Fact]
        public void TheStartUpArguments_KeepTheReportTargetAsTyped_BesideTheNumberOfTheMoment()
        {
            var folder = Path.Combine(Path.GetTempPath(), "RFiDGear.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);

            try
            {
                var pattern = Path.Combine(folder, "card-??.pdf");

                var result = new StartupArgumentProcessor().Process(new[] { "REPORTTARGETPATH=" + pattern });

                Assert.Equal(pattern, result.ReportOutputPattern);
                Assert.Equal(Path.Combine(folder, "card-01.pdf"), result.ReportOutputPath);
            }
            finally
            {
                Directory.Delete(folder, recursive: true);
            }
        }

        [Fact]
        public async Task APatternOfTheStartUpArguments_GivesTheNextFreeNumberForEveryRun()
        {
            await WithWindowAsync(async (viewModel, exec, folder) =>
            {
                StartWith(viewModel, Path.Combine(folder, "card-???.pdf"));

                await RunAsync(viewModel, 3);

                Assert.Equal(
                    new[] { "card-001.pdf", "card-002.pdf", "card-003.pdf" }.Select(name => Path.Combine(folder, name)),
                    PathsOf(exec));
            });
        }

        [Fact]
        public async Task APatternWithATextAfterTheQuestionMarks_GivesTheNextFreeNumberForEveryRun()
        {
            await WithWindowAsync(async (viewModel, exec, folder) =>
            {
                StartWith(viewModel, Path.Combine(folder, "card-???-result.pdf"));

                await RunAsync(viewModel, 3);

                Assert.Equal(
                    new[] { "card-001-result.pdf", "card-002-result.pdf", "card-003-result.pdf" }.Select(name => Path.Combine(folder, name)),
                    PathsOf(exec));
            });
        }

        [Fact]
        public void ThePatternIsMatchedByWhatStandsBeforeAndAfterTheQuestionMarks()
        {
            var folder = Path.Combine(Path.GetTempPath(), "RFiDGear.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);

            try
            {
                foreach (var name in new[] { "card-001-result.pdf", "card-007-result.pdf", "card-abc-result.pdf", "card-009-other.pdf", "card-010.pdf", "x-card-020-result.pdf" })
                {
                    File.WriteAllText(Path.Combine(folder, name), "x");
                }

                // Only the files of the shape card-<digits>-result.pdf count: the highest is 7.
                Assert.Equal(Path.Combine(folder, "card-008-result.pdf"), StartupArgumentProcessor.NormalizeReportTargetPath(Path.Combine(folder, "card-???-result.pdf")));
                Assert.Equal(Path.Combine(folder, "card-11.pdf"), StartupArgumentProcessor.NormalizeReportTargetPath(Path.Combine(folder, "card-??.pdf")));
            }
            finally
            {
                Directory.Delete(folder, recursive: true);
            }
        }

        [Fact]
        public async Task ResettingTheReportTargetPaths_ForgetsThePatternAlsoInAProjectWithoutAReportTask()
        {
            await WithWindowAsync(async (viewModel, exec, folder) =>
            {
                StartWith(viewModel, Path.Combine(folder, "card-???.pdf"));

                await RunAsync(viewModel, 1);

                // The project has no task of the kind that makes a report while the paths are reset.
                viewModel.ChipTasks.TaskCollection.Clear();
                await viewModel.ResetReportTaskDirectoryCommand.ExecuteAsync(null);
                viewModel.ChipTasks.TaskCollection.Add(new CommonTaskViewModel());
                await RunAsync(viewModel, 1);

                Assert.Equal(new string?[] { Path.Combine(folder, "card-001.pdf"), null }, PathsOf(exec));
            });
        }

        [Fact]
        public async Task AFixedPathOfTheStartUpArguments_IsTheSameForEveryRun()
        {
            await WithWindowAsync(async (viewModel, exec, folder) =>
            {
                var fixedPath = Path.Combine(folder, "report.pdf");
                StartWith(viewModel, fixedPath);

                await RunAsync(viewModel, 2);

                Assert.Equal(new[] { fixedPath, fixedPath }, PathsOf(exec));
            });
        }

        [Fact]
        public async Task APathThatAPersonChoseInTheWindow_IsKeptForTheRunsAfterIt()
        {
            await WithWindowAsync(async (viewModel, exec, folder) =>
            {
                var chosen = Path.Combine(folder, "chosen.pdf");

                // The first run has no path: the real service asks, and hands back what the person chose.
                exec.OnExecute = request => new TaskExecutionResult
                {
                    TerminalStatus = TaskLoopTerminalStatus.Success,
                    ReportOutputPath = request.ReportOutputPath ?? chosen
                };

                await RunAsync(viewModel, 3);

                Assert.Equal(new string?[] { null, chosen, chosen }, PathsOf(exec));
            });
        }

        [Fact]
        public async Task APatternWhoseFolderIsGone_LeavesTheChoiceToThePerson_AndWhatWasChosenIsKept()
        {
            await WithWindowAsync(async (viewModel, exec, folder) =>
            {
                var chosen = Path.Combine(folder, "chosen.pdf");
                StartWith(viewModel, Path.Combine(folder, "missing-folder", "card-???.pdf"));
                exec.OnExecute = request => new TaskExecutionResult
                {
                    TerminalStatus = TaskLoopTerminalStatus.Success,
                    ReportOutputPath = request.ReportOutputPath ?? chosen
                };

                await RunAsync(viewModel, 2);

                Assert.Equal(new string?[] { null, chosen }, PathsOf(exec));
            });
        }

        [Fact]
        public async Task ResettingTheReportTargetPaths_ForgetsThePatternOfTheStartUpArguments_AsItForgotItsPath()
        {
            await WithWindowAsync(async (viewModel, exec, folder) =>
            {
                StartWith(viewModel, Path.Combine(folder, "card-???.pdf"));

                await RunAsync(viewModel, 1);
                await viewModel.ResetReportTaskDirectoryCommand.ExecuteAsync(null);
                await RunAsync(viewModel, 1);

                Assert.Equal(new string?[] { Path.Combine(folder, "card-001.pdf"), null }, PathsOf(exec));
            });
        }
    }
}
