#nullable enable
using System;
using System.Linq;
using System.Threading.Tasks;

using RFiDGear.Bridge;
using RFiDGear.Contracts;
using RFiDGear.Services.ExternalControl;
using RFiDGear.Services.TaskExecution;
using RFiDGear.Tests.AutoMode;

using Xunit;

using static RFiDGear.Tests.AutoMode.AutoModeMainWindowTests;

namespace RFiDGear.Tests.ExternalControl
{
    /// <summary>
    /// The bridge on the host of the real main window: the real state machine of automatic mode, with a scripted reader and a
    /// scripted execution service, and the clock driven by hand. What the fake host promises about the order of the notices is
    /// shown here to be what the mode does. These tests need the same things as the other tests that build a main window.
    /// </summary>
    [Collection("SettingsFileAccess")]
    public class BridgeAutoModeRealHostTests
    {
        private const string CardA = "0123456789";

        private static readonly Func<Action, Task> RunAtOnce = action =>
        {
            action();

            return Task.CompletedTask;
        };

        private static async Task WaitUntilAsync(Func<bool> condition)
        {
            var until = DateTime.UtcNow.AddSeconds(5);

            while (!condition())
            {
                Assert.True(DateTime.UtcNow < until, "The condition did not become true in time.");
                await Task.Delay(10);
            }
        }

        [Fact]
        public async Task ACardInAModeAClientStarted_IsReportedBeforeTheModeStops_AndTheFailureOfOneRunStopsNothingBeforeThat()
        {
            await AutoModeHostTests.WithHost(async (viewModel, host, provider, exec, _) =>
            {
                var pipe = "RFiDGear.Bridge.test-" + Guid.NewGuid().ToString("N");
                using var service = new ExternalControlService(viewModel.ExtensionHost, RunAtOnce, () => false, "2.0.10", pipe);
                Assert.True(service.Start());
                service.MarkStartupComplete();
                using var client = await BridgeTestClient.ConnectAsync(pipe, 5000);
                await client.HelloAsync(BridgeEvents.AutoModeChanged, BridgeEvents.AutoRunCompleted);

                // The first card ends with an ordinary failure of a task: the mode goes on. The second one faults: the mode stops after it.
                var started = await client.Service.StartAutoMode(new StartAutoModeRequest());
                exec.OnExecute = _ => new TaskExecutionResult { TerminalStatus = TaskLoopTerminalStatus.Failure };
                provider.Card = Card(CardA);
                await viewModel.AutoMode.TickAsync();
                await client.WaitForEventsAsync(2);
                var stillOn = host.IsAutoModeEnabled;

                provider.Card = new RFiDGear.Models.GenericChipModel();
                await viewModel.AutoMode.TickAsync();
                exec.OnExecute = _ => throw new InvalidOperationException("The reader failed.");
                provider.Card = Card(CardA);
                await viewModel.AutoMode.TickAsync();
                var events = await client.WaitForEventsAsync(4);
                await Task.Delay(200);
                var list = await client.Service.ListRunResults(new ListRunResultsRequest());

                Assert.True(started.Enabled);
                Assert.True(stillOn);
                Assert.False(host.IsAutoModeEnabled);
                Assert.Equal(4, client.Events.Count);

                // On, first run (not stopped by a failure of a task), second run, off, in that order: the result before the notice.
                Assert.IsType<AutoModeChangedEvent>(events[0]);
                var first = Assert.IsType<AutoRunCompletedEvent>(events[1]);
                var second = Assert.IsType<AutoRunCompletedEvent>(events[2]);
                var off = Assert.IsType<AutoModeChangedEvent>(events[3]);
                Assert.Equal(new long[] { 1, 2, 3, 4 }, events.Select(e => e.Seq));
                Assert.Equal(new long[] { 1, 2 }, new[] { first.Result.ResultSeq, second.Result.ResultSeq });
                Assert.All(new[] { first.Result, second.Result }, r => Assert.Equal(RunSources.AutoMode, r.Source));
                Assert.Equal(FailureReasons.Faulted, second.Result.FailureReason);
                Assert.False(off.Enabled);
                Assert.Equal(AutoModeStopReasons.RunAborted, off.LastStopReason);
                Assert.Equal(2, off.LastResultSeq);
                Assert.Equal(new long[] { 1, 2 }, list.Results.Select(r => r.ResultSeq));
                Assert.Equal(RunSources.AutoMode, list.Results[1].Source);
            });
        }

        [Fact]
        public async Task AModeAClientStarted_EndsWhenItsConnectionEnds_AndTheMenuAndTheModeAgree()
        {
            await AutoModeHostTests.WithHost(async (viewModel, host, _, _, _) =>
            {
                var pipe = "RFiDGear.Bridge.test-" + Guid.NewGuid().ToString("N");
                using var service = new ExternalControlService(viewModel.ExtensionHost, RunAtOnce, () => false, "2.0.10", pipe);
                Assert.True(service.Start());
                service.MarkStartupComplete();
                var client = await BridgeTestClient.ConnectAsync(pipe, 5000);
                await client.HelloAsync();

                await client.Service.StartAutoMode(new StartAutoModeRequest());
                Assert.True(viewModel.IsWriteToAllChipAutoChecked);
                client.Dispose();
                await WaitUntilAsync(() => !host.IsAutoModeEnabled);

                Assert.Equal(AutoModeStopReason.StopRequested, host.LastStopReason);
                Assert.False(viewModel.IsWriteToAllChipAutoChecked);
            });
        }

        [Fact]
        public async Task StartsAndRefusals_AreTheOnesOfTheRealMode_ForAMainWindowWithoutATask()
        {
            await WithViewModel(async (viewModel, _, _, _) =>
            {
                var pipe = "RFiDGear.Bridge.test-" + Guid.NewGuid().ToString("N");
                using var service = new ExternalControlService(viewModel.ExtensionHost, RunAtOnce, () => false, "2.0.10", pipe);
                Assert.True(service.Start());
                service.MarkStartupComplete();
                using var client = await BridgeTestClient.ConnectAsync(pipe, 5000);
                await client.HelloAsync();

                var failure = await Assert.ThrowsAsync<StreamJsonRpc.RemoteInvocationException>(() => client.Service.StartAutoMode(new StartAutoModeRequest()));

                Assert.Equal(BridgeErrorCodes.NoProjectLoaded, (int)failure.ErrorCode);
                Assert.False(viewModel.ExtensionHost.AutoMode.IsAutoModeEnabled);
            });
        }
    }
}
