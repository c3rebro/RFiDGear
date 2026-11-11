using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using RFiDGear.Contracts;
using RFiDGear.Infrastructure;
using RFiDGear.Models;
using RFiDGear.Services.TaskExecution;
using RFiDGear.ViewModel.TaskSetupViewModels;

using Xunit;

using static RFiDGear.Tests.AutoMode.AutoModeHostTests;
using static RFiDGear.Tests.AutoMode.AutoModeMainWindowTests;

namespace RFiDGear.Tests.AutoMode
{
    /// <summary>
    /// What an extension learns about each card that automatic mode processed, and why the mode ended: the result of
    /// every run under its <c>RunId</c>, the order of the events, and the reason of a stop. These tests need the same
    /// things as the other tests that build a main window (a reachable update server).
    /// </summary>
    [Collection("SettingsFileAccess")]
    public class AutoModeRunResultTests
    {
        private const string CardA = "0123456789";
        private const string CardB = "9876543210";

        // ── 1 and 2: one correlated, evaluable result per run ──

        [Fact]
        public async Task EveryRunOfTheMode_IsReportedOnce_UnderTheRunIdOfItsRequest_WithTheOrderedOutcomesOfItsTasks()
        {
            await WithHost(async (viewModel, host, provider, exec, _) =>
            {
                var first = (CommonTaskViewModel)viewModel.ChipTasks.TaskCollection[0];
                first.CurrentTaskIndex = "1";
                var second = new CommonTaskViewModel { CurrentTaskIndex = "2" };
                viewModel.ChipTasks.TaskCollection.Add(second);
                exec.OnExecute = request =>
                {
                    request.Observations.Add(new TaskExecutionObservation(0, "1", first, TaskLoopTerminalEvaluator.ExecutedDecision, true, ERROR.NoError));
                    request.Observations.Add(new TaskExecutionObservation(
                        1, "2", second, TaskLoopTerminalEvaluator.SkippedDecision, null, ERROR.Empty, TaskSkipReasons.ExecuteConditionNotMet));

                    return new TaskExecutionResult { TerminalStatus = TaskLoopTerminalStatus.Success };
                };
                var results = new List<ExecResult>();
                host.AutoModeRunCompleted += (_, result) => results.Add(result);

                host.Start();
                provider.Card = Card(CardA);
                await viewModel.AutoMode.TickAsync();
                await viewModel.AutoMode.TickAsync();

                var result = Assert.Single(results);
                Assert.Equal(exec.Requests.Single().RunId, result.RunId);
                Assert.Equal(ExecutionOutcome.Success, result.Outcome);
                Assert.Equal(ExecutionFailureReason.None, result.FailureReason);
                Assert.Equal(2, result.TotalTasks);
                Assert.Equal(new[] { "1", "2" }, result.TaskOutcomes.Select(t => t.ProjectTaskIndex));
                Assert.True(result.TaskOutcomes[0].IsSuccess);
                Assert.Null(result.TaskOutcomes[1].IsSuccess);
                Assert.Equal(TaskSkipReasons.ExecuteConditionNotMet, result.TaskOutcomes[1].SkipReason);
            });
        }

        [Fact]
        public async Task ACardTakenAwayAndPutBack_GivesANewRun_WithItsOwnRunId_AndSoDoesAnotherCard()
        {
            await WithHost(async (viewModel, host, provider, exec, _) =>
            {
                var results = new List<ExecResult>();
                host.AutoModeRunCompleted += (_, result) => results.Add(result);
                host.Start();

                provider.Card = Card(CardA);
                await viewModel.AutoMode.TickAsync();
                provider.Card = new GenericChipModel();
                await viewModel.AutoMode.TickAsync();
                provider.Card = Card(CardA);
                await viewModel.AutoMode.TickAsync();
                provider.Card = Card(CardB);
                await viewModel.AutoMode.TickAsync();

                Assert.Equal(3, results.Count);
                Assert.Equal(3, results.Select(r => r.RunId).Distinct().Count());
                Assert.Equal(exec.Requests.Select(r => r.RunId), results.Select(r => r.RunId));
            });
        }

        [Fact]
        public async Task ARunWhoseTaskFailed_IsReportedAsAFailure_TheModeGoesOn_AndTheCardIsNotRetried()
        {
            await WithHost(async (viewModel, host, provider, exec, _) =>
            {
                exec.OnExecute = _ => new TaskExecutionResult { TerminalStatus = TaskLoopTerminalStatus.Failure };
                var results = new List<ExecResult>();
                host.AutoModeRunCompleted += (_, result) => results.Add(result);
                host.Start();
                provider.Card = Card(CardA);

                await viewModel.AutoMode.TickAsync();
                await viewModel.AutoMode.TickAsync();
                await viewModel.AutoMode.TickAsync();

                var result = Assert.Single(results);
                Assert.Equal(ExecutionOutcome.Failure, result.Outcome);
                Assert.Equal(ExecutionFailureReason.None, result.FailureReason);
                Assert.True(host.IsAutoModeEnabled);
                Assert.Single(exec.Requests);
            });
        }

        // ── the order of what an extension sees ──

        [Fact]
        public async Task TheResultComesAfterTheRunHasEnded_AndBeforeTheStopItCauses()
        {
            await WithHost(async (viewModel, host, provider, exec, _) =>
            {
                exec.OnExecute = _ => throw new InvalidOperationException("The reader failed.");
                var events = new List<string>();
                host.AutoModeStateChanged += (_, _) => events.Add("mode:" + host.IsAutoModeEnabled + ":" + host.LastStopReason);
                host.AutoModeRunCompleted += (_, result) => events.Add("completed:" + result.FailureReason + ":" + host.IsAutoModeEnabled);
                viewModel.ExtensionHost.Execution.IsRunningChanged += (_, _) => events.Add("running:" + viewModel.ExtensionHost.Execution.IsRunning);

                host.Start();
                provider.Card = Card(CardA);
                await viewModel.AutoMode.TickAsync();

                Assert.Equal(
                    new[]
                    {
                        "mode:True:None",
                        "running:True",
                        "running:False",
                        "completed:Faulted:True",
                        "mode:False:RunAborted",
                    },
                    events);
            });
        }

        [Fact]
        public async Task TheEventsOfTheMode_NameTheHostAsSender()
        {
            await WithHost(async (viewModel, host, provider, _, _) =>
            {
                object? sender = null;
                host.AutoModeRunCompleted += (s, _) => sender = s;
                host.Start();
                provider.Card = Card(CardA);

                await viewModel.AutoMode.TickAsync();

                Assert.Same(host, sender);
            });
        }

        [Fact]
        public async Task ASubscriberOfTheResult_ThatThrows_DoesNotStopTheMode_NorStarveTheNext()
        {
            await WithHost(async (viewModel, host, provider, _, _) =>
            {
                var later = 0;
                host.AutoModeRunCompleted += (_, _) => throw new InvalidOperationException("Key=0011223344556677");
                host.AutoModeRunCompleted += (_, _) => later++;
                host.Start();
                provider.Card = Card(CardA);

                await viewModel.AutoMode.TickAsync();
                provider.Card = Card(CardB);
                await viewModel.AutoMode.TickAsync();

                Assert.Equal(2, later);
                Assert.True(host.IsAutoModeEnabled);
            });
        }

        // ── runs of the mode are not mixed up with other runs ──

        [Fact]
        public async Task OnlyRunsOfTheMode_AreReportedToTheModesEvent_AndOnlyRunsOfTheCaller_ToExecutionCompleted()
        {
            await WithHost(async (viewModel, host, provider, _, _) =>
            {
                var autoRuns = 0;
                var ownRuns = 0;
                host.AutoModeRunCompleted += (_, _) => autoRuns++;
                viewModel.ExtensionHost.Execution.ExecutionCompleted += (_, _) => ownRuns++;

                // By hand, and by an extension through ExecuteAllAsync: neither is a run of the mode.
                provider.Card = Card(CardA);
                await viewModel.WriteToChipOnceCommand.ExecuteAsync(null);
                await viewModel.ExtensionHost.Execution.ExecuteAllAsync(ExecutionRequest.Empty);
                Assert.Equal(0, autoRuns);
                Assert.Equal(1, ownRuns);

                // A run of the mode: reported to its own event, and not to ExecutionCompleted.
                host.Start();
                provider.Card = Card(CardB);
                await viewModel.AutoMode.TickAsync();

                Assert.Equal(1, autoRuns);
                Assert.Equal(1, ownRuns);
            });
        }

        // ── 3. why the mode ended ──

        [Fact]
        public async Task TheStopReason_IsNone_UntilTheModeEnds_AndAgainOnceItIsStartedAnew()
        {
            await WithHost(async (_, host, _, _, _) =>
            {
                Assert.Equal(AutoModeStopReason.None, host.LastStopReason);

                host.Start();
                Assert.Equal(AutoModeStopReason.None, host.LastStopReason);

                host.Stop();
                Assert.Equal(AutoModeStopReason.StopRequested, host.LastStopReason);

                host.Start();
                Assert.Equal(AutoModeStopReason.None, host.LastStopReason);
                await Task.CompletedTask;
            });
        }

        [Fact]
        public async Task ARunTheExtensionStarted_ThatFaults_StopsTheMode_AsAnAbortedRun_WithoutBeingReportedAsARunOfTheMode()
        {
            await WithHost(async (viewModel, host, _, exec, _) =>
            {
                var reported = 0;
                host.AutoModeRunCompleted += (_, _) => reported++;
                host.Start();
                exec.OnExecute = _ => throw new InvalidOperationException("The reader failed.");

                var result = await viewModel.ExtensionHost.Execution.ExecuteAllAsync(ExecutionRequest.Empty);

                // The extension has the result of its own run; the mode reports only the runs it started itself.
                Assert.Equal(ExecutionFailureReason.Faulted, result.FailureReason);
                Assert.False(host.IsAutoModeEnabled);
                Assert.Equal(AutoModeStopReason.RunAborted, host.LastStopReason);
                Assert.Equal(0, reported);
                await Task.CompletedTask;
            });
        }

        [Fact]
        public async Task ARunByHandThatFaults_StopsTheMode_AsAnAbortedRun_WithoutBeingReportedAsARunOfTheMode()
        {
            await WithHost(async (viewModel, host, provider, exec, _) =>
            {
                var reported = 0;
                host.AutoModeRunCompleted += (_, _) => reported++;
                host.Start();
                provider.Card = Card(CardA);
                exec.OnExecute = _ => throw new InvalidOperationException("The reader failed.");

                await viewModel.WriteToChipOnceCommand.ExecuteAsync(null);

                Assert.False(host.IsAutoModeEnabled);
                Assert.Equal(AutoModeStopReason.RunAborted, host.LastStopReason);
                Assert.Equal(0, reported);
            });
        }

        [Fact]
        public async Task ARunTheWatchdogGaveUp_StopsTheMode_AsATimeout()
        {
            await WithHost(async (viewModel, host, provider, exec, _) =>
            {
                exec.OnExecute = _ => new TaskExecutionResult { TerminalStatus = TaskLoopTerminalStatus.Failure, Abandoned = true };
                host.Start();
                provider.Card = Card(CardA);

                await viewModel.AutoMode.TickAsync();

                Assert.False(host.IsAutoModeEnabled);
                Assert.Equal(AutoModeStopReason.RunTimedOut, host.LastStopReason);
            });
        }

        [Fact]
        public async Task AReaderThatIsTakenAway_StopsTheMode_WithoutAnyRun()
        {
            await WithHost(async (viewModel, host, provider, exec, _) =>
            {
                host.Start();
                await viewModel.AutoMode.TickAsync();   // the reader answers, without a card

                provider.Connected = false;
                await viewModel.AutoMode.TickAsync();
                await viewModel.AutoMode.TickAsync();
                Assert.True(host.IsAutoModeEnabled);
                await viewModel.AutoMode.TickAsync();

                Assert.False(host.IsAutoModeEnabled);
                Assert.Equal(AutoModeStopReason.ReaderLost, host.LastStopReason);
                Assert.Empty(exec.Requests);
            });
        }
    }
}
