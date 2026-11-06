using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using RFiDGear.Contracts;
using RFiDGear.Extensions.CardCheckAssistant.ViewModels;

using Xunit;

using static RFiDGear.Tests.CardCheckAssistant.ViewModelTestSupport;

namespace RFiDGear.Tests.CardCheckAssistant
{
    /// <summary>
    /// The run of the page against a fake host: what is asked of the host, what the page shows while it waits, and what it
    /// shows when the host answers or refuses.
    /// </summary>
    public class CardCheckAssistantRunTests
    {
        private const string Secret = "00112233445566778899AABBCCDDEEFF";

        [Fact]
        public async Task Run_LoadsTheTrimmedPath_ThenRunsWithTheVariables_AndShowsTheResult()
        {
            using var area = new TempArea();
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var host = new FakeExtensionHost();
                host.ExecutionHost.Result = FakeResults.WithOutcomes(
                    ExecutionOutcome.Success,
                    ExecutionFailureReason.None,
                    new TaskOutcome(0, "1", "Classic", true, "NoError"),
                    new TaskOutcome(1, "2", "Desfire", true, "NoError"),
                    new TaskOutcome(2, "3", "Ultralight", null, "Empty", TaskSkipReasons.NotReached));
                using var viewModel = Create(host);
                viewModel.ProjectPath = "  " + area.ProjectPath + "  ";
                viewModel.JobNumber = " J-42 ";
                viewModel.ChipNumber = "C-7";
                viewModel.ReportTargetPath = area.ReportPath;

                await viewModel.RunCommand.ExecuteAsync();

                Assert.Equal(new[] { area.ProjectPath }, host.ProjectHost.LoadedPaths);
                var request = Assert.Single(host.ExecutionHost.Requests);
                Assert.Equal("J-42", request.Variables["$JOBNUMBER"]);
                Assert.Equal("C-7", request.Variables["$CHIPNUMBER"]);
                Assert.Equal(area.ReportPath, request.Variables["REPORTTARGETPATH"]);
                Assert.Equal(3, request.Variables.Count);

                Assert.True(viewModel.HasResult);
                Assert.Equal("Success", viewModel.OutcomeText);
                Assert.Null(viewModel.ReasonText);
                Assert.False(viewModel.HasReason);
                Assert.Equal("0 of 3 tasks failed", viewModel.SummaryText);
                Assert.Equal(new[] { 1, 2, 3 }, viewModel.Rows.Select(row => row.Position));
                Assert.Equal(new[] { "1", "2", "3" }, viewModel.Rows.Select(row => row.TaskIndex));
                Assert.Equal(
                    new[] { TaskRowKind.Success, TaskRowKind.Success, TaskRowKind.Skipped },
                    viewModel.Rows.Select(row => row.Kind));
                Assert.Equal("Not reached", viewModel.Rows[2].ResultText);
                Assert.Equal("OK", viewModel.Rows[0].ResultText);
                Assert.False(viewModel.IsBusy);
                Assert.True(viewModel.CanRun);
            });
        }

        [Fact]
        public async Task Run_LeavesBlankVariablesOut_SoTheyAreNotReadAsEmptyOnPurpose()
        {
            using var area = new TempArea();
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var host = new FakeExtensionHost();
                using var viewModel = Create(host);
                viewModel.ProjectPath = area.ProjectPath;
                viewModel.JobNumber = "   ";
                viewModel.ChipNumber = string.Empty;
                viewModel.ReportTargetPath = string.Empty;

                await viewModel.RunCommand.ExecuteAsync();

                var request = Assert.Single(host.ExecutionHost.Requests);
                Assert.Empty(request.Variables);
            });
        }

        [Fact]
        public async Task Browse_PutsThePickedPathIntoTheField_AndStartsFromTheCurrentOne()
        {
            using var area = new TempArea();
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var dialogs = new FakeFileDialogService { Answer = area.ProjectPath };
                var host = new FakeExtensionHost("de-DE");
                using var viewModel = Create(host, dialogs);
                viewModel.ProjectPath = "C:\\old\\project.rfPrj";

                viewModel.BrowseCommand.Execute(null);

                Assert.Equal(area.ProjectPath, viewModel.ProjectPath);
                var request = Assert.Single(dialogs.Requests);
                Assert.Equal("C:\\old\\project.rfPrj", request.Current);
                Assert.Equal("Projektdatei auswählen", request.Title);
                Assert.Contains("*.rfPrj", request.Filter, StringComparison.Ordinal);

                dialogs.Answer = null;   // the user cancelled
                viewModel.BrowseCommand.Execute(null);
                Assert.Equal(area.ProjectPath, viewModel.ProjectPath);
            });
        }

        [Fact]
        public async Task WhileLoadingAndRunning_ThePageIsBusyAndItsControlsAreDisabled()
        {
            using var area = new TempArea();
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var host = new FakeExtensionHost();
                host.ProjectHost.LoadGate = NewGate();
                host.ExecutionHost.RunGate = NewGate();
                using var viewModel = Create(host);
                viewModel.ProjectPath = area.ProjectPath;

                var run = viewModel.RunCommand.ExecuteAsync();

                // Loading: the host has not been asked to run anything yet.
                Assert.True(viewModel.IsBusy);
                Assert.False(viewModel.InputsEnabled);
                Assert.False(viewModel.CanRun);
                Assert.False(viewModel.RunCommand.CanExecute(null));
                Assert.False(viewModel.BrowseCommand.CanExecute(null));
                Assert.True(viewModel.ShowProgress);
                Assert.Equal("Loading the project...", viewModel.StatusText);
                Assert.Empty(host.ExecutionHost.Requests);

                viewModel.RunCommand.Execute(null);   // pressing it again changes nothing
                Assert.Single(host.ProjectHost.LoadedPaths);

                host.ProjectHost.LoadGate.SetResult(true);
                await Flush();

                // Running.
                Assert.True(viewModel.IsBusy);
                Assert.False(viewModel.InputsEnabled);
                Assert.Equal("Running the tasks...", viewModel.StatusText);
                Assert.Single(host.ExecutionHost.Requests);

                host.ExecutionHost.RunGate.SetResult(true);
                await run;

                Assert.False(viewModel.IsBusy);
                Assert.True(viewModel.InputsEnabled);
                Assert.True(viewModel.RunCommand.CanExecute(null));
                Assert.False(viewModel.ShowProgress);
                Assert.Equal(string.Empty, viewModel.StatusText);
                Assert.True(viewModel.HasResult);
            });
        }

        [Fact]
        public async Task WhenTheHostIsRunningForSomeoneElse_TheControlsAreDisabledUntilItStops()
        {
            using var area = new TempArea();
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var host = new FakeExtensionHost();
                using var viewModel = Create(host);
                viewModel.ProjectPath = area.ProjectPath;

                host.ExecutionHost.SetRunning(true);
                await Flush();

                Assert.True(viewModel.IsExecutionRunning);
                Assert.False(viewModel.InputsEnabled);
                Assert.False(viewModel.RunCommand.CanExecute(null));
                Assert.Equal("RFiDGear is running tasks. Wait until it has finished.", viewModel.StatusText);
                viewModel.RunCommand.Execute(null);
                Assert.Empty(host.ProjectHost.LoadedPaths);

                host.ExecutionHost.SetRunning(false);
                await Flush();

                Assert.False(viewModel.IsExecutionRunning);
                Assert.True(viewModel.InputsEnabled);
                Assert.True(viewModel.RunCommand.CanExecute(null));
            });
        }

        [Fact]
        public async Task AHostThatIsAlreadyRunningWhenThePageOpens_IsSeenFromTheStart()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var host = new FakeExtensionHost();
                host.ExecutionHost.SetRunningQuietly(true);

                using var viewModel = Create(host);

                Assert.True(viewModel.IsExecutionRunning);
                Assert.False(viewModel.InputsEnabled);
            });
        }

        [Theory]
        [InlineData("", "Select a project file.")]
        [InlineData("relative\\project.rfPrj", "Enter the full path of the project file.")]
        public async Task Run_WithAnUnusableProjectPath_ShowsWhyNextToTheField_AndNeverAsksTheHost(string path, string expected)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var host = new FakeExtensionHost();
                using var viewModel = Create(host);
                viewModel.ProjectPath = path;

                await viewModel.RunCommand.ExecuteAsync();

                Assert.True(viewModel.HasProjectError);
                Assert.Equal(expected, viewModel.ProjectError);
                Assert.Empty(host.ProjectHost.LoadedPaths);
                Assert.Empty(host.ExecutionHost.Requests);
                Assert.False(viewModel.IsBusy);
            });
        }

        [Fact]
        public async Task Run_WithAProjectFileThatDoesNotExist_SaysSo()
        {
            using var area = new TempArea();
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var host = new FakeExtensionHost();
                using var viewModel = Create(host);
                viewModel.ProjectPath = area.ProjectPath + ".missing";

                await viewModel.RunCommand.ExecuteAsync();

                Assert.Equal("The project file does not exist.", viewModel.ProjectError);
                Assert.Empty(host.ProjectHost.LoadedPaths);
            });
        }

        [Fact]
        public async Task EditingAField_ClearsItsOwnError()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var host = new FakeExtensionHost();
                using var viewModel = Create(host);
                await viewModel.RunCommand.ExecuteAsync();
                Assert.True(viewModel.HasProjectError);

                viewModel.ProjectPath = "C:\\something.rfPrj";

                Assert.False(viewModel.HasProjectError);
                Assert.Null(viewModel.ProjectError);
            });
        }

        [Fact]
        public async Task Run_WithAnUnusableReportTarget_ShowsEachReasonNextToTheField_AndNeverAsksTheHost()
        {
            using var area = new TempArea();
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var expectations = new (string Target, string Message)[]
                {
                    ("report.pdf", "The report file must be a full path, for example C:\\Reports\\report.pdf."),
                    (System.IO.Path.Combine(area.Directory, "missing", "report.pdf"), "The folder of the report file does not exist."),
                    (area.Directory, "The report target must be a file, not a folder."),
                    (area.Directory + System.IO.Path.DirectorySeparatorChar, "The report target must be a file, not a folder."),
                };

                foreach (var (target, message) in expectations)
                {
                    var host = new FakeExtensionHost();
                    using var viewModel = Create(host);
                    viewModel.ProjectPath = area.ProjectPath;
                    viewModel.ReportTargetPath = target;

                    await viewModel.RunCommand.ExecuteAsync();

                    Assert.True(viewModel.HasReportTargetError, target);
                    Assert.Equal(message, viewModel.ReportTargetError);
                    Assert.Empty(host.ProjectHost.LoadedPaths);
                    Assert.Empty(host.ExecutionHost.Requests);
                }
            });
        }

        [Theory]
        [InlineData(ProjectOpenError.PathNotFound, "RFiDGear could not find the project file.")]
        [InlineData(ProjectOpenError.UnsupportedFormat, "RFiDGear cannot read this file as a project.")]
        [InlineData(ProjectOpenError.Busy, "RFiDGear is busy. Wait until the current operation has finished and try again.")]
        [InlineData(ProjectOpenError.Unknown, "The project could not be loaded (see the application log).")]
        public async Task WhenTheHostCannotLoadTheProject_TheReasonIsShownAndNothingRuns(ProjectOpenError error, string expected)
        {
            using var area = new TempArea();
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var host = new FakeExtensionHost();
                host.ProjectHost.LoadAnswer = _ => ProjectOpenResult.Fail(error);
                using var viewModel = Create(host);
                viewModel.ProjectPath = area.ProjectPath;

                await viewModel.RunCommand.ExecuteAsync();

                Assert.Equal(expected, viewModel.ProjectError);
                Assert.Empty(host.ExecutionHost.Requests);
                Assert.False(viewModel.HasResult);
                Assert.False(viewModel.IsBusy);
                Assert.True(viewModel.CanRun);
            });
        }

        [Fact]
        public async Task WhenTheHostIsBusy_TheRunSaysSo_AndTheNextTryIsPossible()
        {
            using var area = new TempArea();
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var host = new FakeExtensionHost();
                host.ExecutionHost.ThrowOnExecute = new InvalidOperationException("A run is already in progress.");
                using var viewModel = Create(host);
                viewModel.ProjectPath = area.ProjectPath;

                await viewModel.RunCommand.ExecuteAsync();

                Assert.True(viewModel.HasRunError);
                Assert.Equal("RFiDGear is busy. Wait until the current operation has finished and try again.", viewModel.RunError);
                Assert.False(viewModel.IsBusy);

                host.ExecutionHost.ThrowOnExecute = null;
                await viewModel.RunCommand.ExecuteAsync();
                Assert.False(viewModel.HasRunError, "a new run starts without the old error");
                Assert.True(viewModel.HasResult);
            });
        }

        [Fact]
        public async Task WhenTheHostRefusesTheReportTarget_ItIsShownNextToThatField()
        {
            using var area = new TempArea();
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var host = new FakeExtensionHost();
                host.ExecutionHost.ThrowOnExecute = new ArgumentException("Variable REPORTTARGETPATH cannot be used.");
                using var viewModel = Create(host);
                viewModel.ProjectPath = area.ProjectPath;
                viewModel.ReportTargetPath = area.ReportPath;

                await viewModel.RunCommand.ExecuteAsync();

                Assert.Equal("This path cannot be used as a report file.", viewModel.ReportTargetError);
                Assert.False(viewModel.HasRunError);
                Assert.False(viewModel.IsBusy);
            });
        }

        [Fact]
        public async Task AnUnexpectedFailure_ShowsOnlyTheTypeName_NeverTheMessage()
        {
            using var area = new TempArea();
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var host = new FakeExtensionHost();
                host.ExecutionHost.ThrowOnExecute = new FormatException("Key=" + Secret);
                using var viewModel = Create(host);
                viewModel.ProjectPath = area.ProjectPath;

                await viewModel.RunCommand.ExecuteAsync();

                Assert.Equal("The run could not be completed (FormatException). See the application log.", viewModel.RunError);
                Assert.DoesNotContain(Secret, viewModel.RunError, StringComparison.OrdinalIgnoreCase);
                Assert.False(viewModel.IsBusy);
                Assert.True(viewModel.CanRun);
            });
        }

        [Theory]
        [InlineData(ExecutionOutcome.Success, "Success")]
        [InlineData(ExecutionOutcome.CompletedWithIntentionalFailures, "Completed with intentional failures")]
        [InlineData(ExecutionOutcome.Failure, "Failed")]
        public async Task EachTerminalOutcomeHasItsOwnText(ExecutionOutcome outcome, string expected)
        {
            using var area = new TempArea();
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var host = new FakeExtensionHost();
                host.ExecutionHost.Result = FakeResults.WithOutcomes(outcome, ExecutionFailureReason.None);
                using var viewModel = Create(host);
                viewModel.ProjectPath = area.ProjectPath;

                await viewModel.RunCommand.ExecuteAsync();

                Assert.Equal(expected, viewModel.OutcomeText);
            });
        }

        [Theory]
        [InlineData(ExecutionFailureReason.Cancelled, "Reason: the run was cancelled")]
        [InlineData(ExecutionFailureReason.TimedOut, "Reason: the run timed out")]
        [InlineData(ExecutionFailureReason.Faulted, "Reason: the run stopped because of an error (see the application log)")]
        public async Task AFailureReason_IsShownOnlyWhenTheHostGivesOne(ExecutionFailureReason reason, string expected)
        {
            using var area = new TempArea();
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var host = new FakeExtensionHost();
                host.ExecutionHost.Result = FakeResults.WithOutcomes(ExecutionOutcome.Failure, reason);
                using var viewModel = Create(host);
                viewModel.ProjectPath = area.ProjectPath;

                await viewModel.RunCommand.ExecuteAsync();

                Assert.True(viewModel.HasReason);
                Assert.Equal(expected, viewModel.ReasonText);
            });
        }

        [Fact]
        public async Task EveryKindOfRow_IsShownWithItsOwnTextAndKind()
        {
            using var area = new TempArea();
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var host = new FakeExtensionHost();
                host.ExecutionHost.Result = FakeResults.WithOutcomes(
                    ExecutionOutcome.Failure,
                    ExecutionFailureReason.None,
                    new TaskOutcome(0, "1", "A", true, "NoError"),
                    new TaskOutcome(1, "2", "B", false, "AuthFailure"),
                    new TaskOutcome(2, "3", "C", null, "Empty"),
                    new TaskOutcome(3, "4", "D", null, "Empty", TaskSkipReasons.ExecuteConditionNotMet),
                    new TaskOutcome(4, "5", "E", null, "Empty", TaskSkipReasons.NotExecuted),
                    new TaskOutcome(5, "6", "F", null, "Empty", TaskSkipReasons.TaskModelUnavailable),
                    new TaskOutcome(6, "7", "G", null, "Empty", TaskSkipReasons.NotReached),
                    new TaskOutcome(7, "8", "H", null, "Empty", "SomethingNewer"));
                using var viewModel = Create(host);
                viewModel.ProjectPath = area.ProjectPath;

                await viewModel.RunCommand.ExecuteAsync();

                Assert.Equal(
                    new[]
                    {
                        "OK", "Failed", "-", "Skipped: condition not met", "Not executed", "Task unavailable",
                        "Not reached", "SomethingNewer"
                    },
                    viewModel.Rows.Select(row => row.ResultText));
                Assert.Equal(
                    new[]
                    {
                        TaskRowKind.Success, TaskRowKind.Failure, TaskRowKind.Unknown, TaskRowKind.Skipped,
                        TaskRowKind.Skipped, TaskRowKind.Skipped, TaskRowKind.Skipped, TaskRowKind.Skipped
                    },
                    viewModel.Rows.Select(row => row.Kind));
                Assert.Equal("AuthFailure", viewModel.Rows[1].ErrorCode);
                Assert.Equal("1 of 8 tasks failed", viewModel.SummaryText);
            });
        }

        [Fact]
        public async Task ANewRun_ClearsTheResultOfTheLastOneBeforeItStarts()
        {
            using var area = new TempArea();
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var host = new FakeExtensionHost();
                using var viewModel = Create(host);
                viewModel.ProjectPath = area.ProjectPath;
                await viewModel.RunCommand.ExecuteAsync();
                Assert.True(viewModel.HasResult);

                host.ProjectHost.LoadGate = NewGate();
                var second = viewModel.RunCommand.ExecuteAsync();

                Assert.False(viewModel.HasResult, "the old result must not sit next to a run that has just started");
                Assert.Empty(viewModel.Rows);

                host.ProjectHost.LoadGate.SetResult(true);
                await second;
                Assert.True(viewModel.HasResult);
            });
        }

        [Fact]
        public async Task TheReaderLine_ShowsTheNameAndConnection_AndSurvivesAFaultyReader()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var host = new FakeExtensionHost();
                using var viewModel = Create(host);
                Assert.Equal("Reader: Test reader (connected)", viewModel.ReaderText);

                host.ReaderHost.ReaderName = "Other reader";
                host.ReaderHost.IsConnected = false;
                host.ReaderHost.RaiseReaderStateChanged();
                await Flush();
                Assert.Equal("Reader: Other reader (not connected)", viewModel.ReaderText);

                host.ReaderHost.ThrowOnRead = true;
                host.ReaderHost.RaiseReaderStateChanged();
                await Flush();
                Assert.Equal("Reader: unknown (not connected)", viewModel.ReaderText);
            });
        }

        [Fact]
        public async Task TheOpenProjectLine_FollowsTheProjectOfTheApplication()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var host = new FakeExtensionHost();
                using var viewModel = Create(host);
                Assert.Equal("Open in RFiDGear: no project", viewModel.OpenProjectText);

                host.ProjectHost.CurrentPath = "C:\\jobs\\check.rfPrj";
                host.ProjectHost.TaskCount = 4;
                host.ProjectHost.RaiseProjectChanged();
                await Flush();

                Assert.Equal("Open in RFiDGear: C:\\jobs\\check.rfPrj (4 tasks)", viewModel.OpenProjectText);
            });
        }

        [Fact]
        public async Task Constructor_RejectsMissingArguments()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var host = new FakeExtensionHost();
                var dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
                var dialogs = new FakeFileDialogService();

                Assert.Throws<ArgumentNullException>(() => new CardCheckAssistantViewModel(null!, dispatcher, dialogs));
                Assert.Throws<ArgumentNullException>(() => new CardCheckAssistantViewModel(host, null!, dialogs));
                Assert.Throws<ArgumentNullException>(() => new CardCheckAssistantViewModel(host, dispatcher, null!));
                Assert.Equal(0, host.PageSubscriberCount);   // a rejected construction leaves nothing subscribed
            });
        }
    }
}
