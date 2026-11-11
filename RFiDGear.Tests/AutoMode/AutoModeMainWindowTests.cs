using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Threading;

using RFiDGear.Contracts;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.ExtensionHost;
using RFiDGear.Models;
using RFiDGear.Services.AutoMode;
using RFiDGear.Services.TaskExecution;
using RFiDGear.Tests.Reader;
using RFiDGear.ViewModel;

using Xunit;

using static RFiDGear.Tests.CardCheckAssistant.ViewModelTestSupport;

namespace RFiDGear.Tests.AutoMode
{
    /// <summary>
    /// Automatic mode in the real main window: a scripted reader, a scripted execution service, and the clock driven by
    /// hand (<see cref="AutoModeController.TickAsync"/>). These tests need the same things as the other tests that build a
    /// main window (a reachable update server).
    /// </summary>
    [Collection("SettingsFileAccess")]
    public class AutoModeMainWindowTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const string CardA = "0123456789";

        internal static GenericChipModel Card(string uid) => new GenericChipModel(uid, CARD_TYPE.EM4102);

        internal static async Task Within(Task task)
        {
            var finished = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(20)));
            Assert.Same(task, finished);
            await task;
        }

        internal static DispatcherTimer Clock(MainWindowViewModel viewModel) =>
            (DispatcherTimer)typeof(MainWindowViewModel).GetField("triggerReadChip", Private)!.GetValue(viewModel)!;

        /// <summary>
        /// Runs a test on an STA thread with the real view model, a scripted reader that is the application's reader, and
        /// a scripted execution service whose runs end with success unless the test says otherwise.
        /// </summary>
        /// <param name="test">The test.</param>
        /// <returns>A task that ends when the test has ended and its temporary files are removed.</returns>
        internal static async Task WithViewModel(
            Func<MainWindowViewModel, ScriptedReadProvider, FakeTaskExecutionService, ExtensionOperationCoordinator, Task> test)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var tempRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "RFiDGear.Tests", Guid.NewGuid().ToString("N"));
                System.IO.Directory.CreateDirectory(tempRoot);
                try
                {
                    var viewModel = await MainWindowViewModelTests.CreateInitializedViewModelAsync(tempRoot);
                    var provider = new ScriptedReadProvider();
                    var exec = (FakeTaskExecutionService)typeof(MainWindowViewModel).GetField("taskExecutionService", Private)!.GetValue(viewModel)!;
                    var coordinator = (ExtensionOperationCoordinator)typeof(MainWindowViewModel).GetField("_coordinator", Private)!.GetValue(viewModel)!;
                    exec.OnExecute = _ => new TaskExecutionResult { TerminalStatus = TaskLoopTerminalStatus.Success };

                    using (ReaderDeviceScope.Use(provider))
                    {
                        await test(viewModel, provider, exec, coordinator);
                    }
                }
                finally
                {
                    System.IO.Directory.Delete(tempRoot, recursive: true);
                }
            });
        }

        private static AutoModeController Start(MainWindowViewModel viewModel)
        {
            Assert.Equal(AutoModeStartResult.Started, viewModel.AutoMode.TryStart(requireReady: false, keepApplicationOpen: false));

            return viewModel.AutoMode;
        }

        // ── the menu and the machine are one thing ──

        [Fact]
        public async Task TheMenuToggle_StartsAndStopsAutomaticMode_AndTheCheckFollowsTheMachineWhoeverChangesIt()
        {
            await WithViewModel(async (viewModel, _, _, _) =>
            {
                var changed = new List<string>();
                viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
                Assert.False(viewModel.IsWriteToAllChipAutoChecked);
                Assert.False(Clock(viewModel).IsEnabled);

                viewModel.WriteToAllChipAutoCommand.Execute(null);

                Assert.True(viewModel.IsWriteToAllChipAutoChecked);
                Assert.True(viewModel.AutoMode.IsEnabled);
                Assert.True(Clock(viewModel).IsEnabled);
                await WaitUntil(() => changed.Contains(nameof(MainWindowViewModel.IsWriteToAllChipAutoChecked)), "the menu to be told");

                // What an extension does through the host: from anywhere, not from the menu.
                changed.Clear();
                await Task.Run(() => viewModel.AutoMode.Stop());

                Assert.False(viewModel.IsWriteToAllChipAutoChecked);
                await WaitUntil(() => changed.Contains(nameof(MainWindowViewModel.IsWriteToAllChipAutoChecked)), "the menu to be told of the stop");
                Assert.False(Clock(viewModel).IsEnabled);
            });
        }

        [Fact]
        public async Task TheMenuToggle_IsRefusedWhileARunIsInProgress_AndTellsTheUser()
        {
            await WithViewModel(async (viewModel, _, _, coordinator) =>
            {
                Assert.True(coordinator.TryBeginExecution());
                var dialogsBefore = viewModel.Dialogs.Count;

                viewModel.WriteToAllChipAutoCommand.Execute(null);

                Assert.False(viewModel.AutoMode.IsEnabled);
                Assert.Equal(dialogsBefore + 1, viewModel.Dialogs.Count);
                coordinator.EndExecution();
                await Task.CompletedTask;
            });
        }

        // ── one run per card, correlated through its RunId ──

        [Fact]
        public async Task ANewCard_IsRunOnce_WithTheRunIdOfTheRequestInTheResult_AndNotAgainWhileItStays()
        {
            await WithViewModel(async (viewModel, provider, exec, coordinator) =>
            {
                var results = new List<ExecResult>();
                var machine = Start(viewModel);
                machine.RunCompleted += (_, result) => results.Add(result);
                provider.Card = Card(CardA);

                await machine.TickAsync();
                await machine.TickAsync();
                await machine.TickAsync();

                var request = Assert.Single(exec.Requests);
                var result = Assert.Single(results);
                Assert.False(string.IsNullOrEmpty(request.RunId));
                Assert.Equal(request.RunId, result.RunId);
                Assert.Equal(ExecutionOutcome.Success, result.Outcome);
                Assert.False(request.KeepApplicationOpen, "the menu keeps the old behaviour of %EXIT");
                Assert.False(coordinator.IsExecuting);
                Assert.False(viewModel.ReaderGate.IsHeld);
                Assert.Contains(viewModel.TreeViewParentNodes, node => node.UID == CardA);
            });
        }

        [Fact]
        public async Task TheSameCard_TakenAwayAndPutBack_IsRunAgain_UnderAnotherRunId_WithoutASecondNodeInTheTree()
        {
            await WithViewModel(async (viewModel, provider, exec, _) =>
            {
                var machine = Start(viewModel);
                provider.Card = Card(CardA);
                await machine.TickAsync();

                provider.Card = new GenericChipModel();
                await machine.TickAsync();
                provider.Card = Card(CardA);
                await machine.TickAsync();

                Assert.Equal(2, exec.Requests.Count);
                Assert.NotEqual(exec.Requests[0].RunId, exec.Requests[1].RunId);
                Assert.Single(viewModel.TreeViewParentNodes, node => node.UID == CardA);
            });
        }

        [Fact]
        public async Task ARunByHand_CountsAsProcessingTheCard_SoAutomaticModeStartedAfterwardsDoesNotRepeatIt()
        {
            await WithViewModel(async (viewModel, provider, exec, _) =>
            {
                // AUTORUN=1 followed by AUTOMODE=1 is this: the run of the start-up, then the machine.
                provider.Card = Card(CardA);
                await viewModel.WriteToChipOnceCommand.ExecuteAsync(null);
                Assert.Single(exec.Requests);

                var machine = Start(viewModel);
                await machine.TickAsync();
                await machine.TickAsync();

                Assert.Single(exec.Requests);

                provider.Card = new GenericChipModel();
                await machine.TickAsync();
                provider.Card = Card(CardA);
                await machine.TickAsync();
                Assert.Equal(2, exec.Requests.Count);
            });
        }

        [Fact]
        public async Task AnExtension_SeesTheRunAndItsResultInOrder_ThroughTheHostAlone()
        {
            await WithViewModel(async (viewModel, provider, _, _) =>
            {
                var events = new List<string>();
                var execution = viewModel.ExtensionHost.Execution;
                execution.IsRunningChanged += (_, _) => events.Add("running:" + execution.IsRunning);
                var machine = Start(viewModel);
                machine.RunCompleted += (_, result) => events.Add("completed:" + result.Outcome);
                provider.Card = Card(CardA);

                await machine.TickAsync();

                Assert.Equal(new[] { "running:True", "running:False", "completed:Success" }, events);
            });
        }

        [Fact]
        public async Task TheExtensions_AreToldWhenACardComesAndGoes_WhileAutomaticModeIsOn()
        {
            await WithViewModel(async (viewModel, provider, _, _) =>
            {
                var events = 0;
                viewModel.ExtensionHost.Reader.ReaderStateChanged += (_, _) => events++;
                var machine = Start(viewModel);

                provider.Card = Card(CardA);
                await machine.TickAsync();
                Assert.Equal(1, events);
                Assert.Equal(CardA, Assert.Single(viewModel.ExtensionHost.Reader.CurrentSnapshot.Credentials).Uid);

                provider.Card = new GenericChipModel();
                await machine.TickAsync();
                Assert.Equal(2, events);
                Assert.False(viewModel.ExtensionHost.Reader.CurrentSnapshot.HasCard);
            });
        }

        // ── stop, busy, and the runs that end badly ──

        [Fact]
        public async Task StopDuringARun_LetsItEnd_ReportsIt_LeavesTheClockOff_AndRefusesAStartUntilItIsOver()
        {
            await WithViewModel(async (viewModel, provider, exec, coordinator) =>
            {
                var results = new List<ExecResult>();
                var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                exec.Gate = gate.Task;
                var machine = Start(viewModel);
                machine.RunCompleted += (_, result) => results.Add(result);
                provider.Card = Card(CardA);

                var tick = machine.TickAsync();
                await WaitUntil(() => exec.Requests.Count == 1, "the run to start");
                Assert.True(coordinator.IsExecuting);

                machine.Stop();
                Assert.Equal(AutoModeStartResult.Busy, machine.TryStart(requireReady: false, keepApplicationOpen: false));
                Assert.False(tick.IsCompleted);

                gate.SetResult(true);
                await Within(tick);

                Assert.Single(results);
                Assert.False(coordinator.IsExecuting);
                Assert.False(machine.IsEnabled);
                Assert.False(Clock(viewModel).IsEnabled);

                provider.Card = new GenericChipModel();
                await machine.TickAsync();
                provider.Card = Card(CardA);
                await machine.TickAsync();
                Assert.Single(exec.Requests);
            });
        }

        [Fact]
        public async Task ARunThatThrows_IsReportedAsAFault_AndStopsAutomaticMode_ButTheSlotIsFree()
        {
            await WithViewModel(async (viewModel, provider, exec, coordinator) =>
            {
                var results = new List<ExecResult>();
                exec.OnExecute = _ => throw new InvalidOperationException("The reader failed.");
                var machine = Start(viewModel);
                machine.RunCompleted += (_, result) => results.Add(result);
                provider.Card = Card(CardA);

                await machine.TickAsync();

                var result = Assert.Single(results);
                Assert.Equal(ExecutionOutcome.Failure, result.Outcome);
                Assert.Equal(ExecutionFailureReason.Faulted, result.FailureReason);
                Assert.False(machine.IsEnabled);
                Assert.Equal("RunAborted", machine.LastStopReason.ToString());
                Assert.False(coordinator.IsExecuting);
                Assert.False(Clock(viewModel).IsEnabled);
            });
        }

        [Fact]
        public async Task ARunTheWatchdogGaveUp_StopsAutomaticMode()
        {
            await WithViewModel(async (viewModel, provider, exec, _) =>
            {
                exec.OnExecute = _ => new TaskExecutionResult { TerminalStatus = TaskLoopTerminalStatus.Failure, Abandoned = true };
                var machine = Start(viewModel);
                provider.Card = Card(CardA);

                await machine.TickAsync();

                Assert.False(machine.IsEnabled);
                Assert.Equal("RunTimedOut", machine.LastStopReason.ToString());
            });
        }

        [Fact]
        public async Task ARunWhoseTasksFailed_DoesNotStopAutomaticMode()
        {
            await WithViewModel(async (viewModel, provider, exec, _) =>
            {
                exec.OnExecute = _ => new TaskExecutionResult { TerminalStatus = TaskLoopTerminalStatus.Failure };
                var machine = Start(viewModel);
                provider.Card = Card(CardA);

                await machine.TickAsync();

                Assert.True(machine.IsEnabled);
                Assert.Single(exec.Requests);
            });
        }

        [Fact]
        public async Task ATickThatMeetsAScanOfAnExtension_WaitsForItsTurn_AndTheCardIsTakenOnceTheReaderIsFree()
        {
            await WithViewModel(async (viewModel, provider, exec, coordinator) =>
            {
                var machine = Start(viewModel);
                provider.Card = Card(CardA);
                Assert.True(coordinator.TryBeginScan());

                await machine.TickAsync();
                Assert.Empty(exec.Requests);

                coordinator.EndScan();
                await machine.TickAsync();

                Assert.Single(exec.Requests);
            });
        }

        // ── findings of the review of the pull request ──

        [Fact]
        public async Task AStopWhileTheRunWaitsForAScanThatAnExtensionStartedFromTheNotification_MeansNoRunAfterIt()
        {
            await WithViewModel(async (viewModel, provider, exec, coordinator) =>
            {
                var results = new List<ExecResult>();
                var machine = Start(viewModel);
                machine.RunCompleted += (_, result) => results.Add(result);

                // A subscriber that scans the reader as soon as it hears of the card, from inside the poll's notification.
                var scanStarted = false;
                viewModel.ExtensionHost.Reader.ReaderStateChanged += (_, _) => scanStarted = scanStarted || coordinator.TryBeginScan();
                provider.Card = Card(CardA);

                var tick = machine.TickAsync();
                Assert.True(scanStarted);
                Assert.False(tick.IsCompleted, "the run waits for the scan");

                machine.Stop();
                coordinator.EndScan();
                await Within(tick);

                Assert.Empty(exec.Requests);
                Assert.Empty(results);
                Assert.False(coordinator.IsExecuting);
                Assert.False(machine.IsEnabled);
            });
        }

        [Fact]
        public async Task ARunByHandThatFaults_StopsAModeThatIsOn_ButAFailedTaskDoesNot()
        {
            await WithViewModel(async (viewModel, provider, exec, _) =>
            {
                var machine = Start(viewModel);
                provider.Card = Card(CardA);

                exec.OnExecute = _ => new TaskExecutionResult { TerminalStatus = TaskLoopTerminalStatus.Failure };
                await viewModel.WriteToChipOnceCommand.ExecuteAsync(null);
                Assert.True(machine.IsEnabled);

                exec.OnExecute = _ => throw new InvalidOperationException("The reader failed.");
                await viewModel.WriteToChipOnceCommand.ExecuteAsync(null);

                Assert.False(machine.IsEnabled);
                Assert.Equal("RunAborted", machine.LastStopReason.ToString());
            });
        }

        [Fact]
        public async Task TheCardThatIsRemembered_IsTheOneTheExecutionServiceRead_NotTheOneTheWindowReadBefore()
        {
            await WithViewModel(async (viewModel, provider, exec, _) =>
            {
                var results = new List<ExecResult>();
                var machine = Start(viewModel);
                machine.RunCompleted += (_, result) => results.Add(result);
                provider.Card = Card(CardA);

                // The service reads the card once more before its first task, and the card was swapped meanwhile.
                exec.OnExecute = request =>
                {
                    Assert.NotNull(request.ChipHydrated);
                    request.ChipHydrated(Card("9876543210"));

                    return new TaskExecutionResult { TerminalStatus = TaskLoopTerminalStatus.Success };
                };
                await machine.TickAsync();

                provider.Card = Card("9876543210");
                await machine.TickAsync();
                await machine.TickAsync();

                // A single, successful run, and the mode is still on: not a run that fell over and stopped it.
                Assert.Equal(ExecutionOutcome.Success, Assert.Single(results).Outcome);
                Assert.True(machine.IsEnabled);
                Assert.Single(exec.Requests);
            });
        }

        [Fact]
        public async Task ThePoll_EmptiesTheSharedChipBeforeItReads_SoATagThatWasTakenAwayDoesNotStayInThePicture()
        {
            await WithViewModel(async (viewModel, provider, _, _) =>
            {
                var machine = Start(viewModel);
                provider.Card = Card(CardA);

                await machine.TickAsync();
                var readsSoFar = provider.ReadCount;
                await machine.TickAsync();
                await machine.TickAsync();

                // A provider that finds the same card again returns without touching the chip. Only a chip that was
                // emptied first has no leftover children of the read before.
                Assert.True(provider.ReadCount > readsSoFar);
                Assert.All(provider.ChipWasEmptyAtStart, empty => Assert.True(empty));
            });
        }
    }
}
