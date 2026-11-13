#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using RFiDGear.Bridge;
using RFiDGear.Contracts;
using RFiDGear.Services.ExternalControl;
using RFiDGear.Tests.CardCheckAssistant;

using StreamJsonRpc;

using Xunit;

namespace RFiDGear.Tests.ExternalControl
{
    /// <summary>
    /// The bridge as the application has it, on a real named pipe with a client written like an external program: what it
    /// offers, what <c>getStatus</c> reports (also when the main window does not answer), the events it gives, and how it
    /// shuts down.
    /// </summary>
    public sealed class ExternalControlServiceTests : IDisposable
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

        private (ExternalControlService Service, string Name, FakeExtensionHost Host) Start(
            Func<Action, Task>? dispatch = null,
            Func<bool>? readerInitializing = null,
            TimeSpan? uiTimeout = null,
            string? name = null)
        {
            name ??= "RFiDGear.Bridge.test-" + Guid.NewGuid().ToString("N");
            var host = new FakeExtensionHost();
            var service = new ExternalControlService(host, dispatch ?? RunAtOnce, readerInitializing ?? (() => false), "2.0.10", name, uiTimeout: uiTimeout);
            _disposables.Add(service);
            Assert.True(service.Start());

            return (service, name, host);
        }

        private async Task<BridgeTestClient> ConnectAsync(string name)
        {
            var client = await BridgeTestClient.ConnectAsync(name, 5000);
            _disposables.Add(client);

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

        // ── What it offers ──

        [Fact]
        public async Task Hello_OffersExactlyWhatThisVersionOfTheBridgeCanDo()
        {
            var (service, name, _) = Start();
            var client = await ConnectAsync(name);

            var hello = await client.HelloAsync(BridgeEvents.RunningChanged, BridgeEvents.ShuttingDown, BridgeEvents.RunCompleted, BridgeEvents.ReaderChanged, BridgeEvents.ProjectChanged, BridgeEvents.RunStarted, BridgeEvents.AutoModeChanged, BridgeEvents.AutoRunCompleted);

            // Everything of the contract as it stands: status, project and reader, runs and their lookup and log, automatic mode, and
            // the events about them. (The journal needs a folder, which this service has none of.)
            var offered = new[]
            {
                BridgeCapabilities.Session, BridgeCapabilities.Status, BridgeCapabilities.ProjectLoad, BridgeCapabilities.ReaderScan,
                BridgeCapabilities.ExecutionRun, BridgeCapabilities.ExecutionCancel, BridgeCapabilities.ExecutionResultLookup,
                BridgeCapabilities.ExecutionResultLog, BridgeCapabilities.AutoMode,
                BridgeCapabilities.EventsExecution, BridgeCapabilities.EventsRunning, BridgeCapabilities.EventsProject,
                BridgeCapabilities.EventsReader, BridgeCapabilities.EventsAutoMode, BridgeCapabilities.EventsLifecycle,
            };
            Assert.Equal(BridgeCapabilities.All.Where(offered.Contains).ToList(), hello.Capabilities);
            Assert.Equal(
                new[] { BridgeEvents.RunningChanged, BridgeEvents.ShuttingDown, BridgeEvents.RunCompleted, BridgeEvents.RunStarted, BridgeEvents.ReaderChanged, BridgeEvents.ProjectChanged, BridgeEvents.AutoModeChanged, BridgeEvents.AutoRunCompleted }.OrderBy(n => n),
                hello.Events.OrderBy(n => n));
            Assert.Equal("2.0.10", hello.AppVersion);
            Assert.Equal(service.InstanceId, hello.InstanceId);
        }

        /// <summary>A host whose execution cannot start a run under an id the caller chose; everything else is the fake's.</summary>
        private sealed class HostWithoutPreassignedRuns : IRFiDGearExtensionHost
        {
            private readonly FakeExtensionHost _inner = new FakeExtensionHost();

            public IProjectHost Project => _inner.Project;

            public ITaskExecHost Execution { get; } = new PlainExecution();

            public IReaderHost Reader => _inner.Reader;

            public IAutoModeHost AutoMode => _inner.AutoMode;

            public IAppContext AppContext => _inner.AppContext;

            private sealed class PlainExecution : ITaskExecHost
            {
                public bool IsRunning => false;

                public event EventHandler? IsRunningChanged
                {
                    add { _ = value; }
                    remove { _ = value; }
                }

                public event EventHandler<ExecResult>? ExecutionCompleted
                {
                    add { _ = value; }
                    remove { _ = value; }
                }

                public Task<ExecResult> ExecuteAllAsync(ExecutionRequest request, CancellationToken ct = default)
                {
                    ArgumentNullException.ThrowIfNull(request);
                    ct.ThrowIfCancellationRequested();

                    throw new NotSupportedException();
                }

#pragma warning disable CS0618
                public Task<ExecResult> ExecuteAllAsync(CancellationToken ct = default)
                {
                    ct.ThrowIfCancellationRequested();

                    throw new NotSupportedException();
                }
#pragma warning restore CS0618

                public void ResetStatus() => throw new NotSupportedException();
            }
        }

        [Fact]
        public async Task AHostThatCannotStartARunUnderAnId_DoesNotOfferRuns_ItIsRefusedAsNotOffered()
        {
            var name = "RFiDGear.Bridge.test-" + Guid.NewGuid().ToString("N");
            var service = new ExternalControlService(new HostWithoutPreassignedRuns(), RunAtOnce, () => false, "2.0.10", name);
            _disposables.Add(service);
            Assert.True(service.Start());
            var client = await ConnectAsync(name);

            var hello = await client.HelloAsync();
            var failure = await Assert.ThrowsAsync<RemoteInvocationException>(() => client.Service.StartRun(new StartRunRequest { RequestId = "r-1" }));

            Assert.DoesNotContain(BridgeCapabilities.ExecutionRun, hello.Capabilities);
            Assert.DoesNotContain(BridgeCapabilities.EventsExecution, hello.Capabilities);
            Assert.Equal(BridgeErrorCodes.CapabilityNotSupported, failure.ErrorCode);
            Assert.Equal(BridgeCapabilities.ExecutionRun, BridgeTestClient.DataOf(failure)!.Details!["capability"]);
        }

        // ── getStatus ──

        [Fact]
        public async Task GetStatus_ReportsWhatTheHostKnows()
        {
            var (service, name, host) = Start(readerInitializing: () => false);
            host.ProjectHost.CurrentPath = @"C:\projects\one.rfPrj";
            host.ProjectHost.TaskCount = 3;
            host.ExecutionHost.SetRunningQuietly(true);
            host.ReaderHost.Show(new ReaderSnapshot("Reader 1", ReaderScanState.Ready, new[] { new CredentialInfo("04AABBCC", 7, "MIFARE DESFire", true) }));
            service.MarkStartupComplete();
            var client = await ConnectAsync(name);
            var hello = await client.HelloAsync();

            var status = await client.Service.GetStatus(new GetStatusRequest());

            Assert.Equal(hello.InstanceId, status.InstanceId);
            Assert.True(status.StartupComplete);
            Assert.False(status.ReaderInitializing);
            Assert.True(status.UiResponsive);
            Assert.False(status.Stale);
            Assert.Equal(new[] { "True", @"C:\projects\one.rfPrj", "3" }, new[] { status.Project.Loaded.ToString(), status.Project.Path!, status.Project.TaskCount.ToString() });
            Assert.True(status.IsRunning);
            Assert.Null(status.ActiveRun);
            Assert.Equal(0, status.LastResultSeq);
            Assert.Equal(ReaderStates.Ready, status.Reader.State);
            Assert.Equal("Reader 1", status.Reader.Name);
            Assert.True(status.Reader.HasCard);
            var card = Assert.Single(status.Reader.Credentials);
            Assert.Equal(new object[] { "04AABBCC", 7, "MIFARE DESFire", true }, new object[] { card.Uid, card.TypeCode, card.DisplayName, card.IsPrimary });
        }

        [Fact]
        public async Task GetStatus_AnswersBeforeStartupIsComplete_AndSaysSo()
        {
            var (service, name, _) = Start(readerInitializing: () => true);
            var client = await ConnectAsync(name);
            await client.HelloAsync();

            var before = await client.Service.GetStatus(new GetStatusRequest());
            service.MarkStartupComplete();
            var after = await client.Service.GetStatus(new GetStatusRequest());

            Assert.False(before.StartupComplete);
            Assert.True(before.ReaderInitializing);
            Assert.True(after.StartupComplete);
        }

        [Fact]
        public async Task GetStatus_WhenTheMainWindowDoesNotAnswer_IsAnsweredInTime_WithTheLastValuesMarkedStale()
        {
            var waiting = new List<(Action Action, TaskCompletionSource<bool> Done)>();
            Task Stuck(Action action)
            {
                var done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                lock (waiting)
                {
                    waiting.Add((action, done));
                }

                return done.Task;
            }

            var (_, name, host) = Start(dispatch: Stuck, uiTimeout: TimeSpan.FromMilliseconds(200));
            host.ProjectHost.CurrentPath = @"C:\projects\late.rfPrj";
            host.ProjectHost.TaskCount = 5;
            host.ExecutionHost.SetRunningQuietly(true);
            var client = await ConnectAsync(name);
            await client.HelloAsync();
            var watch = Stopwatch.StartNew();

            var first = await client.Service.GetStatus(new GetStatusRequest());

            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(3), "getStatus waited for a window that does not answer.");
            Assert.False(first.UiResponsive);
            Assert.True(first.Stale);

            // What does not need the window is still fresh; what does is unknown, not invented.
            Assert.True(first.IsRunning);
            Assert.False(first.Project.Loaded);
            Assert.True(first.ReaderInitializing);

            // The window recovers and runs the work that was waiting; the next answer has its values, still marked stale
            // because the window did not answer that very question.
            (Action Action, TaskCompletionSource<bool> Done) late;
            lock (waiting)
            {
                late = waiting[0];
            }

            late.Action();
            late.Done.SetResult(true);
            var second = await client.Service.GetStatus(new GetStatusRequest());

            Assert.True(second.Stale);
            Assert.Equal(5, second.Project.TaskCount);
            Assert.Equal(@"C:\projects\late.rfPrj", second.Project.Path);
        }

        [Fact]
        public async Task AClientThatPollsWhileTheWindowIsStuck_DoesNotLeaveABacklogForTheWindow()
        {
            var waiting = new List<(Action Action, TaskCompletionSource<bool> Done)>();
            Task Stuck(Action action)
            {
                var done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                lock (waiting)
                {
                    waiting.Add((action, done));
                }

                return done.Task;
            }

            var (_, name, _) = Start(dispatch: Stuck, uiTimeout: TimeSpan.FromMilliseconds(100));
            var client = await ConnectAsync(name);
            await client.HelloAsync();

            for (var i = 0; i < 5; i++)
            {
                Assert.False((await client.Service.GetStatus(new GetStatusRequest())).UiResponsive);
            }

            // One read waits for the window, however often the client asked.
            int queued;
            lock (waiting)
            {
                queued = waiting.Count;
            }

            Assert.Equal(1, queued);

            // When that read has been done, the next question makes a new one.
            (Action Action, TaskCompletionSource<bool> Done) first;
            lock (waiting)
            {
                first = waiting[0];
            }

            first.Action();
            first.Done.SetResult(true);
            Assert.True((await client.Service.GetStatus(new GetStatusRequest())).Stale);
            lock (waiting)
            {
                Assert.Equal(2, waiting.Count);
            }
        }

        [Fact]
        public async Task GetStatus_WhenTheWindowIsClosing_DoesNotFail()
        {
            var (_, name, _) = Start(dispatch: _ => throw new InvalidOperationException("The dispatcher has shut down."));
            var client = await ConnectAsync(name);
            await client.HelloAsync();

            var status = await client.Service.GetStatus(new GetStatusRequest());

            Assert.False(status.UiResponsive);
            Assert.True(status.Stale);
        }

        // ── Events ──

        [Fact]
        public async Task RunningChanged_ReachesASubscribedClient_WithTheStateAtTheTimeOfSending()
        {
            var (_, name, host) = Start();
            var client = await ConnectAsync(name);
            var hello = await client.HelloAsync(BridgeEvents.RunningChanged);

            host.ExecutionHost.SetRunning(true);
            await client.WaitForEventsAsync(1);
            host.ExecutionHost.SetRunning(false);
            var events = await client.WaitForEventsAsync(2);

            var on = Assert.IsType<RunningChangedEvent>(events[0]);
            var off = Assert.IsType<RunningChangedEvent>(events[1]);
            Assert.True(on.IsRunning);
            Assert.False(off.IsRunning);
            Assert.Null(on.OwnRequestId);
            Assert.Equal(new long[] { 1, 2 }, new[] { on.Seq, off.Seq });
            Assert.All(events, e => Assert.Equal(hello.SessionId, e.SessionId));
        }

        [Fact]
        public async Task RunningChanged_DoesNotReachAClientThatDidNotAskForIt()
        {
            var (_, name, host) = Start();
            var client = await ConnectAsync(name);
            await client.HelloAsync();

            host.ExecutionHost.SetRunning(true);
            host.ExecutionHost.SetRunning(false);
            await Task.Delay(200);

            Assert.Empty(client.Events);
        }

        [Fact]
        public async Task ConnectionChanged_IsRaised_WhenAClientConnectsAndWhenItLeaves()
        {
            var (service, name, _) = Start();
            var raised = 0;
            service.ConnectionChanged += (_, _) => Interlocked.Increment(ref raised);

            var client = await ConnectAsync(name);
            await client.HelloAsync();
            await WaitUntilAsync(() => service.IsConnected);
            var afterConnect = Volatile.Read(ref raised);
            client.Dispose();
            await WaitUntilAsync(() => !service.IsConnected && Volatile.Read(ref raised) > afterConnect);

            Assert.True(afterConnect >= 1);
        }

        // ── Shutdown ──

        [Fact]
        public async Task Shutdown_TellsASubscribedClient_ThenClosesThePipe()
        {
            var (service, name, _) = Start();
            var client = await ConnectAsync(name);
            await client.HelloAsync(BridgeEvents.ShuttingDown);

            service.Shutdown(ShutdownReasons.UserClose);

            var events = await client.WaitForEventsAsync(1);
            var notice = Assert.IsType<ShuttingDownEvent>(events[0]);
            Assert.Equal(ShutdownReasons.UserClose, notice.Reason);
            Assert.False(notice.RunInFlight);
            Assert.Equal(BridgeServerState.Stopped, service.ServerState);
            await Assert.ThrowsAsync<TimeoutException>(() => BridgeTestClient.ConnectAsync(name, 300));
        }

        [Fact]
        public async Task Shutdown_SaysWhetherARunStartedOverTheBridgeIsInFlight()
        {
            var (service, name, _) = Start();
            service.RunInFlight = () => true;
            var client = await ConnectAsync(name);
            await client.HelloAsync(BridgeEvents.ShuttingDown);

            service.Shutdown(ShutdownReasons.Update);

            var notice = Assert.IsType<ShuttingDownEvent>((await client.WaitForEventsAsync(1))[0]);
            Assert.Equal(ShutdownReasons.Update, notice.Reason);
            Assert.True(notice.RunInFlight);
        }

        [Fact]
        public void Shutdown_IsIdempotent_AndTheSecondCallReturnsAtOnce()
        {
            var (service, _, _) = Start();

            service.Shutdown(ShutdownReasons.UserClose);
            var watch = Stopwatch.StartNew();
            service.Shutdown(ShutdownReasons.ProcessExit);
            service.OnProcessExit(null, EventArgs.Empty);

            Assert.True(watch.Elapsed < TimeSpan.FromMilliseconds(200));
            Assert.Equal(BridgeServerState.Stopped, service.ServerState);
        }

        [Fact]
        public void TheProcessExitHandler_ShutsTheBridgeDown_WithoutAClientToo()
        {
            var (service, _, _) = Start();
            var watch = Stopwatch.StartNew();

            service.OnProcessExit(null, EventArgs.Empty);

            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(2));
            Assert.Equal(BridgeServerState.Stopped, service.ServerState);
        }

        // ── Start and dispose ──

        [Fact]
        public void APipeNameThatIsTaken_LeavesTheBridgeOff_AndTheFirstOneKeepsWorking()
        {
            var (first, name, _) = Start();
            var second = new ExternalControlService(new FakeExtensionHost(), RunAtOnce, () => false, "2.0.10", name);
            _disposables.Add(second);

            Assert.False(second.Start());

            Assert.Equal(BridgeServerState.Failed, second.ServerState);
            Assert.Equal(BridgeServerState.Listening, first.ServerState);
        }

        [Fact]
        public void Disposing_LetsGoOfTheHost_AndOfTheProcessExitHook()
        {
            var (service, _, host) = Start();
            // The run state is listened to twice: for the notices of the bridge, and for the run of a mode that a client started.
            Assert.Equal(new[] { 2, 1, 1, 1, 1, 1 }, Subscribers(host));
            Assert.True(IsHookedToProcessExit(service));

            service.Dispose();
            service.Dispose();

            Assert.Equal(new[] { 0, 0, 0, 0, 0, 0 }, Subscribers(host));
            Assert.False(IsHookedToProcessExit(service));
            Assert.Equal(BridgeServerState.Stopped, service.ServerState);
        }

        // The events of the host the bridge listens to: a run starting or ending, the project, the reader, the end of a run, and the
        // state and the runs of automatic mode.
        private static int[] Subscribers(FakeExtensionHost host) => new[]
        {
            host.ExecutionHost.IsRunningChangedSubscribers,
            host.ProjectHost.ProjectChangedSubscribers,
            host.ReaderHost.ReaderStateChangedSubscribers,
            host.ExecutionHost.ExecutionCompletedSubscribers,
            host.AutoModeHost.StateSubscribers,
            host.AutoModeHost.RunSubscribers,
        };

        [Fact]
        public void TheServiceHooksTheProcessExit_SoEveryWayOutOfTheApplicationTellsTheClient()
        {
            var (service, _, _) = Start();

            Assert.True(IsHookedToProcessExit(service));
        }

        // The event cannot be raised without ending the test process, so the list of its handlers is read from the runtime. If
        // the runtime changes how it keeps them this fails with a message that says so, rather than passing without looking.
        private static bool IsHookedToProcessExit(ExternalControlService service)
        {
            var field = typeof(AppDomain).GetField("ProcessExit", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.True(field != null, "The runtime no longer keeps the handlers of ProcessExit in the field AppDomain.ProcessExit; this test has to look somewhere else.");

            return (field!.GetValue(AppDomain.CurrentDomain) as Delegate)?.GetInvocationList().Any(handler => ReferenceEquals(handler.Target, service)) == true;
        }
    }
}
