#nullable enable
using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using RFiDGear.Bridge;
using RFiDGear.Contracts;
using RFiDGear.Services.AutoMode;
using RFiDGear.Services.ExternalControl;
using RFiDGear.Tests.CardCheckAssistant;

using StreamJsonRpc;

using Xunit;

namespace RFiDGear.Tests.ExternalControl
{
    /// <summary>
    /// Automatic mode over the bridge, on a real pipe with a client written like an external program, against a host whose mode a
    /// test can switch and whose events it can raise: who may start it, what a refusal says, the state a client reads, the
    /// notices, the numbered results and how a client catches up on them, and what happens to the mode when the client goes.
    /// </summary>
    public sealed class BridgeAutoModeTests : IDisposable
    {
        private readonly List<IDisposable> _disposables = new List<IDisposable>();

        public void Dispose()
        {
            foreach (var disposable in Enumerable.Reverse(_disposables))
            {
                disposable.Dispose();
            }
        }

        private static readonly Func<Action, Task> RunAtOnce = action =>
        {
            action();

            return Task.CompletedTask;
        };

        private (ExternalControlService Service, string PipeName, FakeExtensionHost Host) Start(Func<string?>? busyActivity = null)
        {
            var name = "RFiDGear.Bridge.test-" + Guid.NewGuid().ToString("N");
            var host = new FakeExtensionHost();
            host.ProjectHost.CurrentPath = @"C:\projects\one.rfPrj";
            host.ProjectHost.TaskCount = 2;
            var service = new ExternalControlService(host, RunAtOnce, () => false, "2.0.10", name, busyActivity: busyActivity);
            _disposables.Add(service);
            Assert.True(service.Start());
            service.MarkStartupComplete();

            return (service, name, host);
        }

        private async Task<BridgeTestClient> ConnectAsync(string pipeName, params string[] events)
        {
            var client = await BridgeTestClient.ConnectAsync(pipeName, 5000);
            _disposables.Add(client);
            await client.HelloAsync(events);

            return client;
        }

        private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 5000)
        {
            var until = DateTime.UtcNow.AddMilliseconds(timeoutMs);

            while (!condition())
            {
                Assert.True(DateTime.UtcNow < until, "The condition did not become true in time.");
                await Task.Delay(10);
            }
        }

        private static ExecResult AResult(string runId, string? cardUid = "04A1B2C3D4E5F6", int tasks = 2) =>
            FakeResults.Success(tasks) with { RunId = runId, CardUid = cardUid };

        private static string Json(object value) => System.Text.Json.JsonSerializer.Serialize(value, value.GetType(), BridgeJson.CreateClientOptions());

        private static (int Code, BridgeErrorData Data) ErrorOf(RemoteInvocationException failure) => ((int)failure.ErrorCode, Assert.IsType<BridgeErrorData>(BridgeTestClient.DataOf(failure)));

        // ── What is offered ──

        [Fact]
        public async Task TheMode_IsOffered_WithItsResultLogAndItsEvents_AndTheStatusSaysWhatItIsDoing()
        {
            var (_, pipe, _) = Start();
            var client = await ConnectAsync(pipe);

            var status = await client.Service.GetStatus(new GetStatusRequest());

            Assert.NotNull(status.AutoMode);
            Assert.False(status.AutoMode!.Enabled);
            Assert.Null(status.AutoMode.StartedBy);
            Assert.False(status.AutoMode.StopOnDisconnect);
            Assert.Equal(AutoModeStopReasons.None, status.AutoMode.LastStopReason);
        }

        [Fact]
        public async Task AHostWithoutTheMode_DoesNotOfferIt_AndSaysSoInsteadOfDoingNothing()
        {
            var name = "RFiDGear.Bridge.test-" + Guid.NewGuid().ToString("N");
            var service = new ExternalControlService(new HostWithPlainAutoMode(), RunAtOnce, () => false, "2.0.10", name);
            _disposables.Add(service);
            Assert.True(service.Start());
            service.MarkStartupComplete();
            var client = await BridgeTestClient.ConnectAsync(name, 5000);
            _disposables.Add(client);

            var hello = await client.HelloAsync(BridgeEvents.AutoModeChanged);
            var start = await Assert.ThrowsAsync<RemoteInvocationException>(() => client.Service.StartAutoMode(new StartAutoModeRequest()));
            var stop = await Assert.ThrowsAsync<RemoteInvocationException>(() => client.Service.StopAutoMode(new StopAutoModeRequest()));
            var status = await client.Service.GetStatus(new GetStatusRequest());

            Assert.DoesNotContain(BridgeCapabilities.AutoMode, hello.Capabilities);
            Assert.DoesNotContain(BridgeCapabilities.EventsAutoMode, hello.Capabilities);
            Assert.DoesNotContain(BridgeEvents.AutoModeChanged, hello.Events);
            Assert.Equal(BridgeErrorCodes.CapabilityNotSupported, ErrorOf(start).Code);
            Assert.Equal(BridgeErrorCodes.CapabilityNotSupported, ErrorOf(stop).Code);
            Assert.Null(status.AutoMode);
        }

        // ── Start ──

        [Fact]
        public async Task AStart_SwitchesTheModeOn_AgreesToEndItWithTheConnection_AndTellsWhoStartedIt()
        {
            var (_, pipe, host) = Start();
            var client = await ConnectAsync(pipe, BridgeEvents.AutoModeChanged);

            var response = await client.Service.StartAutoMode(new StartAutoModeRequest());
            var events = await client.WaitForEventsAsync(1);
            var status = await client.Service.GetStatus(new GetStatusRequest());

            Assert.True(response.Enabled);
            Assert.False(response.AlreadyEnabled);
            Assert.True(response.StopOnDisconnect);
            Assert.True(host.AutoModeHost.IsAutoModeEnabled);
            var changed = Assert.IsType<AutoModeChangedEvent>(events[0]);
            Assert.True(changed.Enabled);
            Assert.Equal(RunSources.Bridge, changed.StartedBy);
            Assert.Equal(AutoModeStopReasons.None, changed.LastStopReason);
            Assert.True(status.AutoMode!.Enabled);
            Assert.Equal(RunSources.Bridge, status.AutoMode.StartedBy);
            Assert.True(status.AutoMode.StopOnDisconnect);
        }

        [Fact]
        public async Task AStartWhileTheModeIsOnFromThisBridge_ChangesOnlyTheAgreement_AndSendsNoSecondNotice()
        {
            var (_, pipe, host) = Start();
            var client = await ConnectAsync(pipe, BridgeEvents.AutoModeChanged);
            await client.Service.StartAutoMode(new StartAutoModeRequest());

            var again = await client.Service.StartAutoMode(new StartAutoModeRequest { StopOnDisconnect = false });
            var status = await client.Service.GetStatus(new GetStatusRequest());
            await Task.Delay(200);

            Assert.True(again.Enabled);
            Assert.True(again.AlreadyEnabled);
            Assert.False(again.StopOnDisconnect);
            Assert.False(status.AutoMode!.StopOnDisconnect);
            Assert.Single(client.Events);
            Assert.True(host.AutoModeHost.IsAutoModeEnabled);
        }

        [Fact]
        public async Task AStartWhileTheUserHasTheModeOn_IsRefused_AndTheModeStaysTheUsers()
        {
            var (_, pipe, host) = Start();
            var client = await ConnectAsync(pipe, BridgeEvents.AutoModeChanged);
            Assert.Equal(AutoModeStartResult.Started, host.AutoModeHost.TryStart());
            await client.WaitForEventsAsync(1);

            var failure = await Assert.ThrowsAsync<RemoteInvocationException>(() => client.Service.StartAutoMode(new StartAutoModeRequest()));
            var status = await client.Service.GetStatus(new GetStatusRequest());

            var (code, data) = ErrorOf(failure);
            Assert.Equal(BridgeErrorCodes.AutoModeActive, code);
            Assert.Equal(BridgeErrorKinds.AutoModeActive, data.Kind);
            Assert.True(data.NeverStarted);
            var changed = Assert.IsType<AutoModeChangedEvent>(client.Events[0]);
            Assert.Equal(RunSources.Other, changed.StartedBy);
            Assert.Equal(RunSources.Other, status.AutoMode!.StartedBy);
            Assert.False(status.AutoMode.StopOnDisconnect);
            Assert.True(host.AutoModeHost.IsAutoModeEnabled);
        }

        [Theory]
        [InlineData((int)AutoModeStartResult.NoReader, BridgeErrorCodes.NoReader, BridgeErrorKinds.NoReader)]
        [InlineData((int)AutoModeStartResult.NoTasks, BridgeErrorCodes.NoProjectLoaded, BridgeErrorKinds.NoProjectLoaded)]
        [InlineData((int)AutoModeStartResult.Busy, BridgeErrorCodes.Busy, BridgeErrorKinds.Busy)]
        public async Task AStartThatTheHostRefuses_SaysWhy_AsAValueAndNotAsAText_AndChangesNothing(int refusalValue, int expectedCode, string expectedKind)
        {
            var refusal = (AutoModeStartResult)refusalValue;
            var (_, pipe, host) = Start(busyActivity: () => BusyActivities.Scan);
            host.AutoModeHost.RefuseWith = refusal;
            var client = await ConnectAsync(pipe, BridgeEvents.AutoModeChanged);

            var failure = await Assert.ThrowsAsync<RemoteInvocationException>(() => client.Service.StartAutoMode(new StartAutoModeRequest()));
            await Task.Delay(200);

            var (code, data) = ErrorOf(failure);
            Assert.Equal(expectedCode, code);
            Assert.Equal(expectedKind, data.Kind);
            Assert.True(data.NeverStarted);
            Assert.False(host.AutoModeHost.IsAutoModeEnabled);
            Assert.Empty(client.Events);

            if (refusal == AutoModeStartResult.Busy)
            {
                Assert.Equal(BusyActivities.Scan, data.Details!["activity"]);
            }
        }

        // ── Stop ──

        [Fact]
        public async Task AStop_SwitchesTheModeOffWhoeverStartedIt_AndSaysWhetherItWasOn()
        {
            var (_, pipe, host) = Start();
            var client = await ConnectAsync(pipe, BridgeEvents.AutoModeChanged);
            host.AutoModeHost.TryStart();
            await client.WaitForEventsAsync(1);

            var first = await client.Service.StopAutoMode(new StopAutoModeRequest());
            var events = await client.WaitForEventsAsync(2);
            var second = await client.Service.StopAutoMode(new StopAutoModeRequest());
            await Task.Delay(200);

            Assert.True(first.WasEnabled);
            Assert.False(second.WasEnabled);
            Assert.False(host.AutoModeHost.IsAutoModeEnabled);
            var changed = Assert.IsType<AutoModeChangedEvent>(events[1]);
            Assert.False(changed.Enabled);
            Assert.Null(changed.StartedBy);
            Assert.Equal(AutoModeStopReasons.StopRequested, changed.LastStopReason);
            Assert.Equal(2, client.Events.Count);
        }

        [Theory]
        [InlineData(AutoModeStopReason.ReaderLost, AutoModeStopReasons.ReaderLost)]
        [InlineData(AutoModeStopReason.ReaderChanged, AutoModeStopReasons.ReaderChanged)]
        [InlineData(AutoModeStopReason.RunAborted, AutoModeStopReasons.RunAborted)]
        [InlineData(AutoModeStopReason.RunTimedOut, AutoModeStopReasons.RunTimedOut)]
        [InlineData((AutoModeStopReason)99, AutoModeStopReasons.Unknown)]
        public async Task AModeThatStopsByItself_SaysWhy_AndAReasonOfALaterVersionIsUnknownNotAnError(AutoModeStopReason reason, string expected)
        {
            var (_, pipe, host) = Start();
            var client = await ConnectAsync(pipe, BridgeEvents.AutoModeChanged);
            await client.Service.StartAutoMode(new StartAutoModeRequest());

            host.AutoModeHost.StopFor(reason);
            var events = await client.WaitForEventsAsync(2);
            var status = await client.Service.GetStatus(new GetStatusRequest());

            Assert.Equal(expected, Assert.IsType<AutoModeChangedEvent>(events[1]).LastStopReason);
            Assert.Equal(expected, status.AutoMode!.LastStopReason);
            Assert.False(status.AutoMode.Enabled);
        }

        // ── Results ──

        [Fact]
        public async Task TheResultOfARunOfTheMode_IsNumbered_KeptAndSentOnce_BeforeTheNoticeThatTheModeStopped()
        {
            var (_, pipe, host) = Start();
            var client = await ConnectAsync(pipe, BridgeEvents.AutoModeChanged, BridgeEvents.AutoRunCompleted);
            await client.Service.StartAutoMode(new StartAutoModeRequest());

            // The mode raises the result of the run, and then stops because of that run: in that order, on one thread.
            host.AutoModeHost.RaiseRunCompleted(AResult("run-a"));
            host.AutoModeHost.StopFor(AutoModeStopReason.RunAborted);
            var events = await client.WaitForEventsAsync(3);
            await Task.Delay(200);
            var list = await client.Service.ListRunResults(new ListRunResultsRequest());

            Assert.Equal(3, client.Events.Count);
            Assert.IsType<AutoModeChangedEvent>(events[0]);
            var completed = Assert.IsType<AutoRunCompletedEvent>(events[1]);
            var stopped = Assert.IsType<AutoModeChangedEvent>(events[2]);
            Assert.Equal(new long[] { 1, 2, 3 }, events.Select(e => e.Seq));
            Assert.Equal(RunSources.AutoMode, completed.Result.Source);
            Assert.Null(completed.Result.RequestId);
            Assert.Equal("run-a", completed.Result.RunId);
            Assert.Equal("04A1B2C3D4E5F6", completed.Result.CardUid);
            Assert.Equal(1, completed.Result.ResultSeq);
            Assert.Equal(2, completed.Result.TaskOutcomes.Count);
            Assert.False(stopped.Enabled);
            Assert.Equal(1, stopped.LastResultSeq);

            // What the log keeps is what the event said.
            Assert.Single(list.Results);
            Assert.Equal(Json(completed.Result), Json(list.Results[0]));
            Assert.Equal(1, list.LastResultSeq);
        }

        [Fact]
        public async Task TheResultsOfClientsAndOfTheMode_ShareOneSequence_AndTheLogListsThemInTheOrderTheyEnded()
        {
            var (_, pipe, host) = Start();
            var client = await ConnectAsync(pipe, BridgeEvents.RunCompleted);

            await client.Service.StartRun(new StartRunRequest { RequestId = "r-1" });
            await client.WaitForEventsAsync(1);
            host.AutoModeHost.RaiseRunCompleted(AResult("run-a"));
            var status = await client.Service.GetStatus(new GetStatusRequest());
            var all = await client.Service.ListRunResults(new ListRunResultsRequest());
            var after = await client.Service.ListRunResults(new ListRunResultsRequest { AfterResultSeq = 1 });
            var cut = await client.Service.ListRunResults(new ListRunResultsRequest { Limit = 1 });

            Assert.Equal(new[] { RunSources.Bridge, RunSources.AutoMode }, all.Results.Select(r => r.Source));
            Assert.Equal(new long[] { 1, 2 }, all.Results.Select(r => r.ResultSeq));
            Assert.Equal("r-1", all.Results[0].RequestId);
            Assert.False(all.Gap);
            Assert.Equal(2, all.LastResultSeq);
            Assert.Equal(2, status.LastResultSeq);
            Assert.Equal(new long[] { 2 }, after.Results.Select(r => r.ResultSeq));
            Assert.Equal(new long[] { 1 }, cut.Results.Select(r => r.ResultSeq));
            Assert.Equal(2, cut.LastResultSeq);
            Assert.Equal(all.InstanceId, status.InstanceId);
        }

        [Fact]
        public async Task AClientThatAskedForNoNotices_GetsNone_AndStillFindsTheResultInTheLog()
        {
            var (_, pipe, host) = Start();
            var client = await ConnectAsync(pipe);
            await client.Service.StartAutoMode(new StartAutoModeRequest());

            host.AutoModeHost.RaiseRunCompleted(AResult("run-a"));
            host.AutoModeHost.StopFor(AutoModeStopReason.RunAborted);
            await Task.Delay(200);
            var list = await client.Service.ListRunResults(new ListRunResultsRequest());

            Assert.Empty(client.Events);
            Assert.Equal("run-a", Assert.Single(list.Results).RunId);
        }

        [Theory]
        [InlineData(-1L, 0, "afterResultSeq")]
        [InlineData(0L, -1, "limit")]
        [InlineData(5L, 0, "afterResultSeq")]
        public async Task AListWithANumberThatMakesNoSense_IsRefusedByNamingTheField_NotAnsweredWithNothing(long after, int limit, string field)
        {
            var (_, pipe, host) = Start();
            var client = await ConnectAsync(pipe);
            host.AutoModeHost.RaiseRunCompleted(AResult("run-a"));

            var failure = await Assert.ThrowsAsync<RemoteInvocationException>(() => client.Service.ListRunResults(new ListRunResultsRequest { AfterResultSeq = after, Limit = limit }));

            var (code, data) = ErrorOf(failure);
            Assert.Equal(BridgeErrorCodes.InvalidRequest, code);
            Assert.Equal(field, data.Details!["field"]);
        }

        [Fact]
        public async Task AClientThatLetTheNoticesPileUp_IsDisconnected_AndFindsEveryResultOfTheHistoryInTheLog()
        {
            var (service, pipe, host) = Start();

            // A client that said hello and never reads again: the pipe fills, the notices wait, and past the limit it is let go.
            using var raw = new NamedPipeClientStream(".", pipe, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            await raw.ConnectAsync(5000);
            var hello = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"hello\",\"params\":[{\"clientName\":\"t\",\"clientVersion\":\"1\",\"protocolMajorMin\":1,\"protocolMajorMax\":1,\"events\":[\"autoRunCompleted\"]}]}";
            await raw.WriteAsync(Encoding.UTF8.GetBytes($"Content-Length: {Encoding.UTF8.GetByteCount(hello)}\r\n\r\n{hello}"));
            await WaitUntilAsync(() => service.IsConnected);
            await Task.Delay(300);

            var many = BridgeProtocol.ResultHistory + 6;

            for (var i = 0; i < many; i++)
            {
                host.AutoModeHost.RaiseRunCompleted(AResult("run-" + i, tasks: 400));
            }

            await WaitUntilAsync(() => !service.IsConnected, 15_000);
            var client = await ConnectAsync(pipe);
            var status = await client.Service.GetStatus(new GetStatusRequest());
            var all = await client.Service.ListRunResults(new ListRunResultsRequest { Limit = 3 });
            var fromTheEdge = await client.Service.ListRunResults(new ListRunResultsRequest { AfterResultSeq = many - BridgeProtocol.ResultHistory, Limit = 3 });

            // Every number was given out, and the log holds the last ones: asking from the start says that the first were dropped by
            // the history, asking from the edge says nothing was lost.
            Assert.Equal(many, status.LastResultSeq);
            Assert.True(all.Gap);
            Assert.Equal(many - BridgeProtocol.ResultHistory + 1, all.Results[0].ResultSeq);
            Assert.False(fromTheEdge.Gap);
            Assert.Equal(all.Results.Select(r => r.ResultSeq), fromTheEdge.Results.Select(r => r.ResultSeq));
            Assert.Equal(many, fromTheEdge.LastResultSeq);
        }

        // ── The end of the connection ──

        [Fact]
        public async Task AModeThatTheClientStartedWithTheDefault_EndsWithItsConnection()
        {
            var (service, pipe, host) = Start();
            var client = await ConnectAsync(pipe);
            await client.Service.StartAutoMode(new StartAutoModeRequest());

            client.Dispose();
            await WaitUntilAsync(() => !host.AutoModeHost.IsAutoModeEnabled);

            Assert.Equal(AutoModeStopReason.StopRequested, host.AutoModeHost.LastStopReason);
            await WaitUntilAsync(() => !service.IsConnected);
        }

        [Fact]
        public async Task AModeThatTheClientLeftRunningOnPurpose_GoesOn_AndAnotherConnectionSeesItAndCanStopIt()
        {
            var (service, pipe, host) = Start();
            var first = await ConnectAsync(pipe);
            await first.Service.StartAutoMode(new StartAutoModeRequest { StopOnDisconnect = false });

            first.Dispose();
            await WaitUntilAsync(() => !service.IsConnected);
            await Task.Delay(200);
            var second = await ConnectAsync(pipe);
            var status = await second.Service.GetStatus(new GetStatusRequest());
            var stop = await second.Service.StopAutoMode(new StopAutoModeRequest());

            Assert.True(status.AutoMode!.Enabled);
            Assert.Equal(RunSources.Bridge, status.AutoMode.StartedBy);
            Assert.False(status.AutoMode.StopOnDisconnect);
            Assert.True(stop.WasEnabled);
            Assert.False(host.AutoModeHost.IsAutoModeEnabled);
        }

        [Fact]
        public async Task AClientThatTakesTheAgreementOver_EndsTheModeWhenItLeavesNotWhenTheFirstOneDoes()
        {
            var (service, pipe, host) = Start();
            var first = await ConnectAsync(pipe);
            await first.Service.StartAutoMode(new StartAutoModeRequest { StopOnDisconnect = false });
            first.Dispose();
            await WaitUntilAsync(() => !service.IsConnected);
            await Task.Delay(200);

            var second = await ConnectAsync(pipe);
            var again = await second.Service.StartAutoMode(new StartAutoModeRequest());
            Assert.True(again.AlreadyEnabled);
            Assert.True(host.AutoModeHost.IsAutoModeEnabled);
            second.Dispose();
            await WaitUntilAsync(() => !host.AutoModeHost.IsAutoModeEnabled);

            Assert.Equal(AutoModeStopReason.StopRequested, host.AutoModeHost.LastStopReason);
        }

        [Fact]
        public async Task AModeTheUserSwitchedOn_IsNotStopped_WhenAClientLeaves()
        {
            var (service, pipe, host) = Start();
            var client = await ConnectAsync(pipe);
            host.AutoModeHost.TryStart();

            client.Dispose();
            await WaitUntilAsync(() => !service.IsConnected);
            await Task.Delay(200);

            Assert.True(host.AutoModeHost.IsAutoModeEnabled);
            Assert.Equal(0, host.AutoModeHost.StopCalls);
        }

        [Fact]
        public async Task AModeTheUserRestartedAfterTheClientsStop_IsNotStopped_WhenTheClientLeaves()
        {
            var (service, pipe, host) = Start();
            var client = await ConnectAsync(pipe, BridgeEvents.AutoModeChanged);
            await client.Service.StartAutoMode(new StartAutoModeRequest());
            host.AutoModeHost.StopFor(AutoModeStopReason.StopRequested);
            host.AutoModeHost.TryStart();
            await client.WaitForEventsAsync(3);
            var stopsBefore = host.AutoModeHost.StopCalls;

            client.Dispose();
            await WaitUntilAsync(() => !service.IsConnected);
            await Task.Delay(200);

            Assert.True(host.AutoModeHost.IsAutoModeEnabled);
            Assert.Equal(stopsBefore, host.AutoModeHost.StopCalls);
        }

        [Fact]
        public async Task ARunInAModeAClientStarted_CountsAsARunInFlight_ForTheShutdownNoticeAndTheCloseCheck()
        {
            var (service, pipe, host) = Start();
            Assert.False(service.RunInFlight());
            var client = await ConnectAsync(pipe);
            await client.Service.StartAutoMode(new StartAutoModeRequest());

            // A run of the mode is going on: the application asks before it closes, and the shutdown notice says so.
            host.ExecutionHost.SetRunning(true);

            Assert.True(service.RunInFlight());
            host.ExecutionHost.SetRunning(false);
            Assert.False(service.RunInFlight());
        }

        /// <summary>A host whose automatic mode is the plain public one: it cannot say why a start was refused.</summary>
        private sealed class HostWithPlainAutoMode : IRFiDGearExtensionHost
        {
            private readonly FakeExtensionHost _inner = new FakeExtensionHost();

            public IProjectHost Project => _inner.Project;

            public ITaskExecHost Execution => _inner.Execution;

            public IReaderHost Reader => _inner.Reader;

            public IAutoModeHost AutoMode { get; } = new PlainAutoMode();

            public IAppContext AppContext => _inner.AppContext;

            private sealed class PlainAutoMode : IAutoModeHost
            {
                public bool IsAutoModeEnabled => false;

                public AutoModeStopReason LastStopReason => AutoModeStopReason.None;

                public event EventHandler? AutoModeStateChanged
                {
                    add { }
                    remove { }
                }

                public event EventHandler<ExecResult>? AutoModeRunCompleted
                {
                    add { }
                    remove { }
                }

                public void Start() => throw new NotSupportedException();

                public void Stop() => throw new NotSupportedException();
            }
        }
    }
}
