using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using RFiDGear.Contracts;
using RFiDGear.Infrastructure.ExtensionHost;
using RFiDGear.Models;
using RFiDGear.Services.TaskExecution;
using RFiDGear.Tests.Reader;
using RFiDGear.ViewModel;
using RFiDGear.ViewModel.TaskSetupViewModels;

using Xunit;

using static RFiDGear.Tests.AutoMode.AutoModeMainWindowTests;
using static RFiDGear.Tests.CardCheckAssistant.ViewModelTestSupport;

namespace RFiDGear.Tests.AutoMode
{
    /// <summary>
    /// <see cref="IAutoModeHost"/> as an extension meets it: the host of the real main window, with a scripted reader and
    /// a scripted execution service, and the clock driven by hand. The extension never touches a view model; the tests
    /// reach the machine only to make the clock tick (the application's timer does that in real life). The cases follow
    /// the acceptance cases of issue #233. These tests need the same things as the other tests that build a main window
    /// (a reachable update server).
    /// </summary>
    [Collection("SettingsFileAccess")]
    public class AutoModeHostTests
    {
        private const string CardA = "0123456789";

        /// <summary>A main window whose project has one task, which is what a start of the host needs.</summary>
        private static Task WithHost(
            Func<MainWindowViewModel, IAutoModeHost, ScriptedReadProvider, FakeTaskExecutionService, ExtensionOperationCoordinator, Task> test) =>
            WithViewModel(async (viewModel, provider, exec, coordinator) =>
            {
                viewModel.ChipTasks.TaskCollection.Add(new CommonTaskViewModel());
                await test(viewModel, viewModel.ExtensionHost.AutoMode, provider, exec, coordinator);
            });

        // ── without an explicit start nothing is encoded ──

        [Fact]
        public async Task WithoutAStart_NoCardIsProcessed_NoMatterHowOftenTheClockTicks()
        {
            await WithHost(async (viewModel, host, provider, exec, _) =>
            {
                provider.Card = Card(CardA);

                await viewModel.AutoMode.TickAsync();
                await viewModel.AutoMode.TickAsync();

                Assert.False(host.IsAutoModeEnabled);
                Assert.Empty(exec.Requests);
            });
        }

        // ── start and stop, repeated, and seen by the menu ──

        [Fact]
        public async Task Start_TurnsTheModeOn_ForTheMenuAndTheClock_AndStopTurnsItOff_EachWithOneEvent()
        {
            await WithHost(async (viewModel, host, _, _, _) =>
            {
                var events = new List<bool>();
                host.AutoModeStateChanged += (_, _) => events.Add(host.IsAutoModeEnabled);

                host.Start();
                host.Start();

                Assert.True(host.IsAutoModeEnabled);
                Assert.True(viewModel.IsWriteToAllChipAutoChecked);
                Assert.True(Clock(viewModel).IsEnabled);

                host.Stop();
                host.Stop();

                Assert.False(host.IsAutoModeEnabled);
                Assert.False(viewModel.IsWriteToAllChipAutoChecked);
                Assert.False(Clock(viewModel).IsEnabled);
                Assert.Equal(new[] { true, false }, events);
                await Task.CompletedTask;
            });
        }

        [Fact]
        public async Task TheMenuAndTheHost_ControlTheSameMode()
        {
            await WithHost(async (viewModel, host, _, _, _) =>
            {
                var events = 0;
                host.AutoModeStateChanged += (_, _) => events++;

                viewModel.WriteToAllChipAutoCommand.Execute(null);
                Assert.True(host.IsAutoModeEnabled);
                Assert.Equal(1, events);

                host.Start();   // already on: nothing happens
                Assert.Equal(1, events);

                host.Stop();
                Assert.False(viewModel.IsWriteToAllChipAutoChecked);

                host.Start();
                Assert.True(viewModel.IsWriteToAllChipAutoChecked);
                viewModel.WriteToAllChipAutoCommand.Execute(null);
                Assert.False(host.IsAutoModeEnabled);
                Assert.Equal(4, events);
                await Task.CompletedTask;
            });
        }

        [Fact]
        public async Task Start_AndStop_WorkFromAnyThread_AndTheEventIsRaisedOnTheThreadThatChangedTheState()
        {
            await WithHost(async (_, host, _, _, _) =>
            {
                var raisedOn = new List<int>();
                host.AutoModeStateChanged += (_, _) => raisedOn.Add(Environment.CurrentManagedThreadId);
                int starter = 0, stopper = 0;

                await Task.Run(() => { starter = Environment.CurrentManagedThreadId; host.Start(); });
                await Task.Run(() => { stopper = Environment.CurrentManagedThreadId; host.Stop(); });

                Assert.Equal(new[] { starter, stopper }, raisedOn);
                Assert.NotEqual(Environment.CurrentManagedThreadId, starter);
            });
        }

        // ── a start that cannot be honoured ──

        [Fact]
        public async Task Start_WithoutATask_IsRefused_AndChangesNothing()
        {
            await WithViewModel(async (viewModel, _, _, _) =>
            {
                var host = viewModel.ExtensionHost.AutoMode;
                var events = 0;
                host.AutoModeStateChanged += (_, _) => events++;

                var error = Assert.Throws<InvalidOperationException>(() => host.Start());

                Assert.Contains("no task", error.Message, StringComparison.OrdinalIgnoreCase);
                Assert.False(host.IsAutoModeEnabled);
                Assert.Equal(0, events);
                await Task.CompletedTask;
            });
        }

        [Fact]
        public async Task Start_WithoutAReader_IsRefused_AndChangesNothing()
        {
            await WithHost(async (_, host, _, _, _) =>
            {
                using (ReaderDeviceScope.Use(null))
                {
                    var error = Assert.Throws<InvalidOperationException>(() => host.Start());

                    Assert.Contains("reader", error.Message, StringComparison.OrdinalIgnoreCase);
                    Assert.False(host.IsAutoModeEnabled);
                }

                await Task.CompletedTask;
            });
        }

        [Theory]
        [InlineData("load")]
        [InlineData("run")]
        [InlineData("edit")]
        [InlineData("scan")]
        public async Task Start_WhileSomethingIsInProgress_IsRefused_WithAMessageThatNamesIt_AndChangesNothing(string busyWith)
        {
            await WithHost(async (_, host, _, _, coordinator) =>
            {
                var events = 0;
                host.AutoModeStateChanged += (_, _) => events++;
                Assert.True(busyWith switch
                {
                    "load" => coordinator.TryBeginLoad(),
                    "run" => coordinator.TryBeginExecution(),
                    "edit" => coordinator.TryBeginProjectOperation(),
                    _ => coordinator.TryBeginScan(),
                });

                var error = Assert.Throws<InvalidOperationException>(() => host.Start());

                Assert.Contains(busyWith, error.Message, StringComparison.OrdinalIgnoreCase);
                Assert.False(host.IsAutoModeEnabled);
                Assert.Equal(0, events);
                await Task.CompletedTask;
            });
        }

        [Fact]
        public void AHostWithoutAReaderLoop_CannotRunTheMode()
        {
            using var host = new HeadlessExtensionHost();

            Assert.False(host.AutoMode.IsAutoModeEnabled);
            Assert.Throws<NotSupportedException>(() => host.AutoMode.Start());
            host.AutoMode.Stop();
            Assert.False(host.AutoMode.IsAutoModeEnabled);
        }

        // ── the cards ──

        [Fact]
        public async Task AHostStartedMode_RunsANewCardOnce_AndKeepsTheApplicationOpenForAExitTask()
        {
            await WithHost(async (viewModel, host, provider, exec, _) =>
            {
                host.Start();
                provider.Card = Card(CardA);

                await viewModel.AutoMode.TickAsync();
                await viewModel.AutoMode.TickAsync();

                var request = Assert.Single(exec.Requests);
                Assert.True(request.KeepApplicationOpen, "a program task %EXIT must not close the application of the extension");
                Assert.True(host.IsAutoModeEnabled);
            });
        }

        [Fact]
        public async Task AnExtension_FollowsACardThroughTheHostAlone_InTheDocumentedOrder()
        {
            await WithHost(async (viewModel, host, provider, _, _) =>
            {
                var events = new List<string>();
                host.AutoModeStateChanged += (_, _) => events.Add("mode:" + host.IsAutoModeEnabled);
                viewModel.ExtensionHost.Execution.IsRunningChanged += (_, _) => events.Add("running:" + viewModel.ExtensionHost.Execution.IsRunning);
                viewModel.ExtensionHost.Reader.ReaderStateChanged += (_, _) => events.Add("reader:" + viewModel.ExtensionHost.Reader.CurrentSnapshot.HasCard);

                host.Start();
                provider.Card = Card(CardA);
                await viewModel.AutoMode.TickAsync();
                provider.Card = new GenericChipModel();
                await viewModel.AutoMode.TickAsync();
                host.Stop();

                Assert.Equal(
                    new[] { "mode:True", "reader:True", "running:True", "running:False", "reader:False", "mode:False" },
                    events);
            });
        }

        [Fact]
        public async Task ARunThatFaults_StopsTheMode_AfterTheRunHasEnded_AndTheEventComesOnTheUiThread()
        {
            await WithHost(async (viewModel, host, provider, exec, _) =>
            {
                exec.OnExecute = _ => throw new InvalidOperationException("The reader failed.");
                var events = new List<string>();
                var stoppedOn = 0;
                host.AutoModeStateChanged += (_, _) =>
                {
                    events.Add("mode:" + host.IsAutoModeEnabled);
                    stoppedOn = Environment.CurrentManagedThreadId;
                };
                viewModel.ExtensionHost.Execution.IsRunningChanged += (_, _) => events.Add("running:" + viewModel.ExtensionHost.Execution.IsRunning);

                host.Start();
                provider.Card = Card(CardA);
                await viewModel.AutoMode.TickAsync();

                Assert.Equal(new[] { "mode:True", "running:True", "running:False", "mode:False" }, events);
                Assert.Equal(Environment.CurrentManagedThreadId, stoppedOn);
                Assert.False(host.IsAutoModeEnabled);
            });
        }

        [Fact]
        public async Task ARunByHandThatFaults_StopsAHostStartedMode_AndTheHostSaysSo()
        {
            await WithHost(async (viewModel, host, provider, exec, _) =>
            {
                var events = 0;
                host.AutoModeStateChanged += (_, _) => events++;
                host.Start();
                provider.Card = Card(CardA);
                exec.OnExecute = _ => throw new InvalidOperationException("The reader failed.");

                await viewModel.WriteToChipOnceCommand.ExecuteAsync(null);

                Assert.False(host.IsAutoModeEnabled);
                Assert.Equal(2, events);   // on, off
            });
        }

        [Fact]
        public async Task StopWhileARunWaitsForAScanOfAnExtension_WithdrawsIt_SoItDoesNotStartWhenTheScanEnds()
        {
            await WithHost(async (viewModel, host, provider, exec, coordinator) =>
            {
                // An extension that scans the reader as soon as it hears of the card.
                var scanStarted = false;
                viewModel.ExtensionHost.Reader.ReaderStateChanged += (_, _) => scanStarted = scanStarted || coordinator.TryBeginScan();
                host.Start();
                provider.Card = Card(CardA);

                var tick = viewModel.AutoMode.TickAsync();
                Assert.True(scanStarted);
                Assert.False(tick.IsCompleted, "the run waits for the scan");

                await Task.Run(() => host.Stop());
                coordinator.EndScan();
                await Within(tick);

                Assert.Empty(exec.Requests);
                Assert.False(viewModel.ExtensionHost.Execution.IsRunning);
            });
        }

        [Fact]
        public async Task StopDuringARun_FromAnotherThread_LetsTheRunEnd_AndNothingFollows()
        {
            await WithHost(async (viewModel, host, provider, exec, _) =>
            {
                var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                exec.Gate = gate.Task;
                host.Start();
                provider.Card = Card(CardA);

                var tick = viewModel.AutoMode.TickAsync();
                await WaitUntil(() => viewModel.ExtensionHost.Execution.IsRunning, "the run to start");

                await Task.Run(() => host.Stop());
                var startWhileRunning = Assert.Throws<InvalidOperationException>(() => host.Start());
                Assert.Contains("run", startWhileRunning.Message, StringComparison.OrdinalIgnoreCase);
                Assert.False(host.IsAutoModeEnabled);
                Assert.False(tick.IsCompleted, "the card operation is not interrupted");

                gate.SetResult(true);
                await Within(tick);

                Assert.False(viewModel.ExtensionHost.Execution.IsRunning);
                Assert.Single(exec.Requests);

                provider.Card = new GenericChipModel();
                await viewModel.AutoMode.TickAsync();
                provider.Card = Card(CardA);
                await viewModel.AutoMode.TickAsync();
                Assert.Single(exec.Requests);
            });
        }

        // ── subscribers ──

        [Fact]
        public async Task ASubscriberThatThrows_DoesNotTurnAStartOrStopIntoAnException_NorStarveTheNext()
        {
            await WithHost(async (_, host, _, _, _) =>
            {
                var later = 0;
                host.AutoModeStateChanged += (_, _) => throw new InvalidOperationException("Key=0011223344556677");
                host.AutoModeStateChanged += (_, _) => later++;

                host.Start();
                host.Stop();

                Assert.Equal(2, later);
                Assert.False(host.IsAutoModeEnabled);
                await Task.CompletedTask;
            });
        }

        [Fact]
        public async Task TheSenderOfTheEvent_IsTheHost_NotAnInternalPart()
        {
            await WithHost(async (_, host, _, _, _) =>
            {
                object? sender = null;
                host.AutoModeStateChanged += (s, _) => sender = s;

                host.Start();

                Assert.Same(host, sender);
                await Task.CompletedTask;
            });
        }
    }
}
