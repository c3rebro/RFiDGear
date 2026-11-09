using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using RFiDGear.Contracts;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.ExtensionHost;
using RFiDGear.Infrastructure.FileAccess;
using RFiDGear.Infrastructure.Tasks;
using RFiDGear.Models;
using RFiDGear.Services.TaskExecution;
using RFiDGear.ViewModel.TaskSetupViewModels;

using Serilog.Events;
using Xunit;

namespace RFiDGear.Tests
{
    /// <summary>
    /// The project task <c>%EXIT</c> as a run meets it: the real task view models, the real task execution service and the
    /// extension host, with only the reader and the call that closes the application replaced. A project of the old
    /// assistant ends with this task. Started from the main window it closes RFiDGear, as it always did; started from an
    /// extension it ends the run, so that the page which started the run can show the result.
    /// </summary>
    public class ProjectExitTaskTests
    {
        // Has no program in it: the task cannot parse the text and fails, without starting anything. A task of this kind
        // that is reached when it should not be shows itself as a failed task.
        private const string NoProgram = "no program here";

        [Fact]
        public async Task AProjectEndingWithExit_RunForAnExtension_IsASuccess_AndRFiDGearStaysOpen()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var exits = new RecordedExits();

                var result = await RunForAnExtensionAsync(ExitTask("0"));

                Assert.Empty(exits.Codes);
                Assert.Equal(ExecutionOutcome.Success, result.Outcome);
                var outcome = Assert.Single(result.TaskOutcomes);
                Assert.True(outcome.IsSuccess);
                Assert.Null(outcome.SkipReason);
            });
        }

        [Fact]
        public async Task TheTasksAfterExit_AreNotStarted_AreReportedAsNotReached_AndAreNotHeldAgainstTheRun()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var exits = new RecordedExits();
                var after = ProgramTask("1", NoProgram);

                var result = await RunForAnExtensionAsync(ExitTask("0"), after);

                Assert.Empty(exits.Codes);
                Assert.Equal(ExecutionOutcome.Success, result.Outcome);
                Assert.Equal(0, result.FailedTasks);
                Assert.Equal(2, result.TaskOutcomes.Count);
                Assert.True(result.TaskOutcomes[0].IsSuccess);
                Assert.Null(result.TaskOutcomes[1].IsSuccess);
                Assert.Equal(TaskSkipReasons.NotReached, result.TaskOutcomes[1].SkipReason);

                // The second task was never tried: a task that is tried fails to parse its text and says so in its error level.
                Assert.NotEqual(ERROR.IsNotTrue, after.CurrentTaskErrorLevel);
            });
        }

        [Fact]
        public async Task AnExitThatIsNotRun_BecauseItsConditionIsNotMet_DoesNotEndTheRun()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var exits = new RecordedExits();
                var exit = ExitTask("1");
                exit.SelectedExecuteConditionTaskIndex = "0";
                exit.SelectedExecuteConditionErrorLevel = ERROR.NoError;   // only after a first task that went well

                var result = await RunForAnExtensionAsync(ProgramTask("0", NoProgram), exit, ProgramTask("2", NoProgram));

                // The first task fails, so the exit is skipped and the run goes on to the third, which fails as well.
                Assert.Empty(exits.Codes);
                Assert.Equal(ExecutionOutcome.Failure, result.Outcome);
                Assert.Equal(3, result.TaskOutcomes.Count);
                Assert.Null(result.TaskOutcomes[1].IsSuccess);
                Assert.NotEqual(TaskSkipReasons.NotReached, result.TaskOutcomes[1].SkipReason);
                Assert.False(result.TaskOutcomes[2].IsSuccess);
            });
        }

        [Fact]
        public async Task AnExitWhoseConditionIsMet_EndsTheRunToo()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var exits = new RecordedExits();
                var exit = ExitTask("1");
                exit.SelectedExecuteConditionTaskIndex = "0";
                exit.SelectedExecuteConditionErrorLevel = ERROR.IsNotTrue;   // runs after a first task that failed, as its handler
                var after = ProgramTask("2", NoProgram);

                var result = await RunForAnExtensionAsync(ProgramTask("0", NoProgram), exit, after);

                Assert.Empty(exits.Codes);
                Assert.Equal(3, result.TaskOutcomes.Count);
                Assert.False(result.TaskOutcomes[0].IsSuccess);
                Assert.True(result.TaskOutcomes[1].IsSuccess);
                Assert.Equal(TaskSkipReasons.NotReached, result.TaskOutcomes[2].SkipReason);
                Assert.NotEqual(ERROR.IsNotTrue, after.CurrentTaskErrorLevel);
            });
        }

        [Fact]
        public async Task ARunOfTheMainWindow_StillClosesRFiDGearAtExit_AndSaysSoInTheLog()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var logs = CapturedLogs.Install();

                // Nothing can be logged once the application is closed, so the line has to be there when it is asked to close.
                var loggedBeforeTheExit = false;
                using var exits = new RecordedExits(_ => loggedBeforeTheExit = logs
                    .From(nameof(CommonTaskViewModel))
                    .Any(logEvent => logEvent.RenderMessage().Contains("%EXIT", StringComparison.Ordinal)));

                using (ReaderDeviceScope.Use(new FakeAppSettingsProvider()))
                {
                    await RunOnAsync(NewService(), keepApplicationOpen: false, ExitTask("0"));
                }

                Assert.Equal(new[] { 0 }, exits.Codes);
                Assert.True(loggedBeforeTheExit);
            });
        }

        [Fact]
        public async Task TheExitOfARun_IsLogged_AndDoesNotShortenTheNextRunOfTheSameService()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var exits = new RecordedExits();
                using var logs = CapturedLogs.Install();
                using (ReaderDeviceScope.Use(new FakeAppSettingsProvider()))
                {
                    var service = NewService();
                    await RunOnAsync(service, keepApplicationOpen: true, ExitTask("0"), ProgramTask("1", NoProgram));
                    await RunOnAsync(service, keepApplicationOpen: true, ProgramTask("0", NoProgram), ProgramTask("1", NoProgram));
                }

                var entries = TaskLog(logs);
                Assert.Contains(entries, entry => entry.RootElement.GetProperty("Stage").GetString() == "TaskLoop.ProjectExit");

                var summaries = entries
                    .Where(entry => entry.RootElement.GetProperty("Stage").GetString() == "TaskLoop.Summary")
                    .Select(entry => entry.RootElement.GetProperty("Details"))
                    .ToList();
                Assert.Equal(2, summaries.Count);

                // The first run says where the project ended it, and still counts the task after it; the second knows
                // nothing of it and was judged on both of its tasks.
                Assert.Equal(0, summaries[0].GetProperty("EndedByProjectAt").GetInt32());
                Assert.Equal(2, summaries[0].GetProperty("TotalTasks").GetInt32());
                Assert.Equal(1, summaries[0].GetProperty("Executed").GetInt32());
                Assert.Equal(JsonValueKind.Null, summaries[1].GetProperty("EndedByProjectAt").ValueKind);
                Assert.Equal(2, summaries[1].GetProperty("TotalTasks").GetInt32());
                Assert.Equal(2, summaries[1].GetProperty("Executed").GetInt32());
            });
        }

        [Theory]
        [InlineData("%EXIT", true)]
        [InlineData("%exit", true)]
        [InlineData("%Exit", true)]
        [InlineData("%EXIT now", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void OnlyTheTextExit_AsksForRFiDGearToClose_InAnyCase(string? text, bool expected)
        {
            var task = new CommonTaskViewModel { SelectedTaskType = TaskType_CommonTask.ExecuteProgram, ProgramToExecute = text! };

            Assert.Equal(expected, task.IsExitRequest);
        }

        private static CommonTaskViewModel ExitTask(string index) => ProgramTask(index, "%EXIT");

        private static CommonTaskViewModel ProgramTask(string index, string program) => new CommonTaskViewModel
        {
            SelectedTaskType = TaskType_CommonTask.ExecuteProgram,
            CurrentTaskIndex = index,
            ProgramToExecute = program
        };

        // As an extension runs a project: through the host, which asks the execution service to keep the application open.
        private static async Task<ExecResult> RunForAnExtensionAsync(params CommonTaskViewModel[] tasks)
        {
            // Its own folder: opening the project database resets the files of the application's profile, which a test
            // must not do to the one of whoever runs it.
            var root = Directory.CreateTempSubdirectory("ProjectExitRun").FullName;
            try
            {
                using (ReaderDeviceScope.Use(new FakeAppSettingsProvider()))
                {
                    var service = NewService();
                    var coordinator = new ExtensionOperationCoordinator();
                    var project = new ProjectHostImpl(new DatabaseReaderWriter(new ProjectManager(root)), coordinator);
                    var host = new TaskExecHostImpl(service, project, coordinator);
                    project.ReplaceTaskCollection(tasks);

                    // A run that does not end is cancelled instead of hanging the test.
                    using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(30));

                    return await host.ExecuteAllAsync(ExecutionRequest.Empty, limit.Token);
                }
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        private static TaskExecutionService NewService() =>
            new TaskExecutionService(new ReaderDeviceProvider(), new NullDispatcherTimerAdapter(), new NullDispatcherTimerAdapter());

        // As the main window runs a project when the request does not ask to keep the application open, and as the host
        // does when it does.
        private static async Task RunOnAsync(TaskExecutionService service, bool keepApplicationOpen, params CommonTaskViewModel[] tasks)
        {
            var handler = new ChipTaskHandlerModel();
            foreach (var task in tasks)
            {
                handler.TaskCollection.Add(task);
            }

            using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            await service.ExecuteOnceAsync(
                new TaskExecutionRequest
                {
                    TaskHandler = handler,
                    VariablesFromArgs = new Dictionary<string, string>(),
                    Timeouts = TaskExecutionTimeouts.Default,
                    KeepApplicationOpen = keepApplicationOpen
                },
                limit.Token);
        }

        // What the task loop logged, as parsed entries.
        private static List<JsonDocument> TaskLog(CapturedLogs logs) =>
            logs.From(nameof(NullTaskExecutionLogger))
                .Select(logEvent => logEvent.Properties.TryGetValue("SerializedTaskLog", out var value) ? (value as ScalarValue)?.Value as string : null)
                .Where(json => json != null)
                .Select(json => JsonDocument.Parse(json!))
                .ToList();

        /// <summary>Takes the place of the call that closes the application, so that a test does not close the test host.</summary>
        private sealed class RecordedExits : IDisposable
        {
            private readonly Action<int> _previous = CommonTaskViewModel.ApplicationExit;

            public RecordedExits(Action<int>? onExit = null)
            {
                CommonTaskViewModel.ApplicationExit = code =>
                {
                    Codes.Add(code);
                    onExit?.Invoke(code);
                };
            }

            public List<int> Codes { get; } = new List<int>();

            public void Dispose() => CommonTaskViewModel.ApplicationExit = _previous;
        }
    }
}
