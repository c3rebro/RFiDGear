#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using RFiDGear.Bridge;
using RFiDGear.Contracts;
using RFiDGear.Services.ExternalControl;
using RFiDGear.Tests.CardCheckAssistant;

using Xunit;

namespace RFiDGear.Tests.ExternalControl
{
    /// <summary>
    /// The rules for starting, cancelling and looking up runs, against a scripted host: a request id starts at most one run, a
    /// refusal is only ever "not started" when that is certain, a run has one final result, and nothing depends on which
    /// connection asked.
    /// </summary>
    public sealed class BridgeRunSupervisorTests
    {
        private const string ProjectPath = @"C:\projects\one.rfPrj";

        /// <summary>A window whose work is done by the test: the actions wait until the test lets the window run them.</summary>
        private sealed class ManualWindow
        {
            private readonly List<(Action Action, TaskCompletionSource<bool> Done)> _waiting = new List<(Action, TaskCompletionSource<bool>)>();

            public int Waiting
            {
                get
                {
                    lock (_waiting)
                    {
                        return _waiting.Count;
                    }
                }
            }

            public Task Dispatch(Action action)
            {
                var done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                lock (_waiting)
                {
                    _waiting.Add((action, done));
                }

                return done.Task;
            }

            /// <summary>The window gets to the work that was waiting.</summary>
            public void RunAll()
            {
                (Action Action, TaskCompletionSource<bool> Done)[] work;

                lock (_waiting)
                {
                    work = _waiting.ToArray();
                    _waiting.Clear();
                }

                foreach (var (action, done) in work)
                {
                    action();
                    done.SetResult(true);
                }
            }
        }

        private sealed class Harness
        {
            private readonly List<BridgeEvent> _events = new List<BridgeEvent>();

            public Harness(TimeSpan? admissionTimeout = null, RunRegistry? registry = null)
            {
                Host.ProjectHost.CurrentPath = ProjectPath;
                Host.ProjectHost.TaskCount = 2;
                Registry = registry ?? new RunRegistry();
                Supervisor = new RunSupervisor(
                    Host,
                    Host.ExecutionHost,
                    Registry,
                    action => Dispatch(action),
                    Publish,
                    () => ShuttingDown,
                    () => Busy,
                    admissionTimeout);
            }

            public FakeExtensionHost Host { get; } = new FakeExtensionHost();

            public RunRegistry Registry { get; }

            public RunSupervisor Supervisor { get; }

            public Func<Action, Task> Dispatch { get; set; } = action =>
            {
                action();

                return Task.CompletedTask;
            };

            public bool ShuttingDown { get; set; }

            public string? Busy { get; set; }

            public IReadOnlyList<BridgeEvent> Events
            {
                get
                {
                    lock (_events)
                    {
                        return _events.ToList();
                    }
                }
            }

            private void Publish(string name, bool durable, BridgeEventFactory create)
            {
                var evt = create(_events.Count + 1, "session", DateTimeOffset.UtcNow);

                lock (_events)
                {
                    _events.Add(evt);
                }

                Assert.Equal(name == BridgeEvents.RunCompleted || name == BridgeEvents.RunStarted, durable);
            }
        }

        private static StartRunRequest Request(string id, params (string Name, string Value)[] variables) => new StartRunRequest
        {
            RequestId = id,
            Variables = variables.Select(v => new VariablePair { Name = v.Name, Value = v.Value }).ToList(),
        };

        private static async Task<BridgeFault> RefusalOf(Func<Task> call)
        {
            try
            {
                await call();
            }
            catch (BridgeFault fault)
            {
                return fault;
            }

            throw new Xunit.Sdk.XunitException("The call was not refused.");
        }

        private static async Task<GetRunResultResponse> ResultOf(Harness h, string requestId, int waitMs = 5000) =>
            await h.Supervisor.GetResultAsync(new GetRunResultRequest { RequestId = requestId, WaitMs = waitMs });

        // ── Starting ──

        [Fact]
        public async Task AnAcceptedRun_IsAnsweredWhenAdmitted_NotWhenItEnds_AndTheHostGotTheIdAndTheVariables()
        {
            var h = new Harness();
            h.Host.ExecutionHost.RunGate = new TaskCompletionSource<bool>();

            var response = await h.Supervisor.StartAsync(Request("r-1", ("$JOBNUMBER", "J-42"), ("REPORTTARGETPATH", @"C:\reports\x.pdf")));

            Assert.Equal("r-1", response.RequestId);
            Assert.Matches("^[0-9a-f]{32}$", response.RunId);
            Assert.Equal(RunStates.Running, response.State);
            Assert.False(response.Duplicate);
            Assert.Equal(response.RunId, Assert.Single(h.Host.ExecutionHost.RunIds));
            var received = Assert.Single(h.Host.ExecutionHost.Requests).Variables;
            Assert.Equal("J-42", received["$jobnumber"]);
            Assert.Equal(@"C:\reports\x.pdf", received["REPORTTARGETPATH"]);
            Assert.True(h.Supervisor.HasActiveRun);

            // Not finished: the lookup says so.
            Assert.Equal(RunStates.Running, (await ResultOf(h, "r-1", 0)).State);

            h.Host.ExecutionHost.RunGate.SetResult(true);
            var done = await ResultOf(h, "r-1");

            Assert.Equal(RunStates.Completed, done.State);
            Assert.Equal(response.RunId, done.Result!.RunId);
            Assert.Equal("r-1", done.Result.RequestId);
            Assert.Equal(RunSources.Bridge, done.Result.Source);
            Assert.Equal(1, done.Result.ResultSeq);
            Assert.False(h.Supervisor.HasActiveRun);
        }

        [Fact]
        public async Task TheSameRequestAgain_WhileTheRunGoesOn_AnswersWithThatRun_AndStartsNothing()
        {
            var h = new Harness();
            h.Host.ExecutionHost.RunGate = new TaskCompletionSource<bool>();
            var first = await h.Supervisor.StartAsync(Request("r-1", ("$JOBNUMBER", "J-42")));

            // Same content, another order and case of the names does not matter either.
            var second = await h.Supervisor.StartAsync(Request("r-1", ("$jobnumber", "J-42")));

            Assert.True(second.Duplicate);
            Assert.Equal(first.RunId, second.RunId);
            Assert.Equal(RunStates.Running, second.State);
            Assert.Single(h.Host.ExecutionHost.Requests);
            h.Host.ExecutionHost.RunGate.SetResult(true);
            await ResultOf(h, "r-1");
        }

        [Fact]
        public async Task TheSameRequestAgain_AfterTheRunEnded_AnswersCompleted_AndStartsNothing()
        {
            var h = new Harness();
            var first = await h.Supervisor.StartAsync(Request("r-1"));
            await ResultOf(h, "r-1");

            var second = await h.Supervisor.StartAsync(Request("r-1"));

            Assert.True(second.Duplicate);
            Assert.Equal(first.RunId, second.RunId);
            Assert.Equal(RunStates.Completed, second.State);
            Assert.Single(h.Host.ExecutionHost.Requests);
        }

        [Fact]
        public async Task TheSameIdWithOtherContent_IsAConflict_AndStartsNothing()
        {
            var h = new Harness();
            h.Host.ExecutionHost.RunGate = new TaskCompletionSource<bool>();
            await h.Supervisor.StartAsync(Request("r-1", ("$JOBNUMBER", "J-42")));

            var differentValue = await RefusalOf(() => h.Supervisor.StartAsync(Request("r-1", ("$JOBNUMBER", "J-43"))));
            var differentExpectation = await RefusalOf(() => h.Supervisor.StartAsync(new StartRunRequest { RequestId = "r-1", Variables = new[] { new VariablePair { Name = "$JOBNUMBER", Value = "J-42" } }, ExpectTaskCount = 2 }));

            Assert.Equal(BridgeErrorCodes.RequestIdConflict, differentValue.Code);
            Assert.Equal(BridgeErrorCodes.RequestIdConflict, differentExpectation.Code);
            Assert.True(differentValue.NeverStarted);
            Assert.Single(h.Host.ExecutionHost.Requests);
            h.Host.ExecutionHost.RunGate.SetResult(true);
            await ResultOf(h, "r-1");
        }

        [Fact]
        public async Task ADuplicateThatArrivesWhileTheFirstIsStillBeingAdmitted_GetsTheAnswerTheFirstGets()
        {
            var h = new Harness();
            var window = new ManualWindow();
            h.Dispatch = window.Dispatch;
            h.Host.ExecutionHost.ThrowOnExecute = new InvalidOperationException("An execution is already in progress.");
            h.Busy = BusyActivities.Run;

            var first = h.Supervisor.StartAsync(Request("r-1"));
            var second = h.Supervisor.StartAsync(Request("r-1"));
            await Task.Delay(100);

            // Neither is told anything before the host has decided.
            Assert.False(first.IsCompleted);
            Assert.False(second.IsCompleted);

            window.RunAll();
            var firstFault = await RefusalOf(() => first);
            var secondFault = await RefusalOf(() => second);

            Assert.Equal(BridgeErrorCodes.Busy, firstFault.Code);
            Assert.Equal(BridgeErrorCodes.Busy, secondFault.Code);
            Assert.Equal(BusyActivities.Run, secondFault.Details!["activity"]);
            Assert.True(secondFault.NeverStarted);
            Assert.Single(h.Host.ExecutionHost.Requests);
        }

        [Fact]
        public async Task ARequestThatWasRefused_CanBeSentAgainWithTheSameId()
        {
            var h = new Harness();
            h.Host.ExecutionHost.ThrowOnExecute = new InvalidOperationException("An execution is already in progress.");
            var refused = await RefusalOf(() => h.Supervisor.StartAsync(Request("r-1")));

            // A run that was refused is not a run in flight: the application may close, and the status shows none.
            Assert.False(h.Supervisor.HasActiveRun);
            Assert.Null(h.Supervisor.ActiveRun);

            h.Host.ExecutionHost.ThrowOnExecute = null;
            var retry = await h.Supervisor.StartAsync(Request("r-1"));

            Assert.Equal(BridgeErrorCodes.Busy, refused.Code);
            Assert.False(retry.Duplicate);
            Assert.Equal(2, h.Host.ExecutionHost.Requests.Count);
            await ResultOf(h, "r-1");
        }

        [Fact]
        public async Task AReportVariableThatCannotProduceItsReport_IsRefusedAsInvalid_BeforeAnyCardWork()
        {
            var h = new Harness();
            h.Host.ExecutionHost.ThrowOnExecute = new ArgumentException("The directory does not exist.");

            var fault = await RefusalOf(() => h.Supervisor.StartAsync(Request("r-1", ("REPORTTARGETPATH", @"C:\nowhere\x.pdf"))));

            Assert.Equal(BridgeErrorCodes.InvalidRequest, fault.Code);
            Assert.Equal("variables", fault.Details!["field"]);
        }

        [Fact]
        public async Task AFaultOfTheHostOfAnotherKind_IsNotNothingHappened_ItIsARunThatFailed()
        {
            var h = new Harness();
            h.Host.ExecutionHost.ThrowOnExecute = new IOException("The disk is gone.");

            var response = await h.Supervisor.StartAsync(Request("r-1"));
            var done = await ResultOf(h, "r-1");

            // The card may have been touched, so the client is not told that nothing was started.
            Assert.Equal(RunStates.Running, response.State);
            Assert.Equal(RunStates.Completed, done.State);
            Assert.Equal(RunStatuses.Failure, done.Result!.Status);
            Assert.Equal(FailureReasons.BridgeFault, done.Result.FailureReason);
            Assert.Empty(done.Result.TaskOutcomes);
        }

        [Fact]
        public async Task NoTasks_ADifferentProject_AndAutomaticMode_AreRefusedBeforeTheHostIsAsked()
        {
            var h = new Harness();

            h.Host.ProjectHost.TaskCount = 0;
            var empty = await RefusalOf(() => h.Supervisor.StartAsync(Request("r-1")));
            h.Host.ProjectHost.TaskCount = 2;
            var otherPath = await RefusalOf(() => h.Supervisor.StartAsync(new StartRunRequest { RequestId = "r-2", ExpectProjectPath = @"C:\projects\other.rfPrj" }));
            var otherCount = await RefusalOf(() => h.Supervisor.StartAsync(new StartRunRequest { RequestId = "r-3", ExpectTaskCount = 5 }));
            h.Host.AutoModeHost.IsAutoModeEnabled = true;
            var autoMode = await RefusalOf(() => h.Supervisor.StartAsync(Request("r-4")));

            Assert.Equal(BridgeErrorCodes.NoProjectLoaded, empty.Code);
            Assert.Equal(BridgeErrorCodes.ProjectMismatch, otherPath.Code);
            Assert.Equal("expectProjectPath", otherPath.Details!["field"]);
            Assert.Equal("expectTaskCount", otherCount.Details!["field"]);
            Assert.Equal(BridgeErrorCodes.AutoModeActive, autoMode.Code);
            Assert.All(new[] { empty, otherPath, otherCount, autoMode }, fault => Assert.True(fault.NeverStarted));
            Assert.Empty(h.Host.ExecutionHost.Requests);
        }

        [Fact]
        public async Task TheExpectedProject_IsComparedByItsFullPath_WithoutRegardToCase()
        {
            var h = new Harness();

            var response = await h.Supervisor.StartAsync(new StartRunRequest { RequestId = "r-1", ExpectProjectPath = @"c:\PROJECTS\sub\..\ONE.rfprj", ExpectTaskCount = 2 });

            Assert.Equal(RunStates.Running, response.State);
            await ResultOf(h, "r-1");
        }

        [Fact]
        public async Task WhenRFiDGearIsClosing_NothingIsStarted()
        {
            var h = new Harness { ShuttingDown = true };

            var fault = await RefusalOf(() => h.Supervisor.StartAsync(Request("r-1")));

            Assert.Equal(BridgeErrorCodes.ShuttingDown, fault.Code);
            Assert.True(fault.NeverStarted);
            Assert.Empty(h.Host.ExecutionHost.Requests);
        }

        // ── A window that does not answer ──

        [Fact]
        public async Task AWindowThatDoesNotTakeTheRequestInTime_IsAnsweredAsNotStarted_AndWhenItWakesUpItStartsNothing()
        {
            var h = new Harness(TimeSpan.FromMilliseconds(150));
            var window = new ManualWindow();
            h.Dispatch = window.Dispatch;

            var fault = await RefusalOf(() => h.Supervisor.StartAsync(Request("r-1")));

            Assert.Equal(BridgeErrorCodes.UiUnresponsive, fault.Code);
            Assert.True(fault.NeverStarted);

            // The window comes back and finds the work that was waiting for it. It must not run it: the client was told that
            // nothing was started.
            window.RunAll();

            Assert.Empty(h.Host.ExecutionHost.Requests);
            Assert.False(h.Supervisor.HasActiveRun);
            Assert.Equal(RunStates.Unknown, (await ResultOf(h, "r-1", 0)).State);

            // And the id is free again.
            h.Dispatch = action =>
            {
                action();

                return Task.CompletedTask;
            };
            var retry = await h.Supervisor.StartAsync(Request("r-1"));
            Assert.False(retry.Duplicate);
            await ResultOf(h, "r-1");
        }

        [Fact]
        public async Task AWindowThatHasTakenTheRequest_IsWaitedFor_NotGivenUpOn()
        {
            var h = new Harness(TimeSpan.FromMilliseconds(100));
            var entered = new ManualResetEventSlim();
            var release = new ManualResetEventSlim();

            // The window is inside the host's admission when the time is up.
            h.Host.ExecutionHost.OnExecute = _ =>
            {
                entered.Set();
                release.Wait(TimeSpan.FromSeconds(10));
            };
            h.Dispatch = action => Task.Run(action);

            var start = h.Supervisor.StartAsync(Request("r-1"));
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
            await Task.Delay(400);

            // Longer than the limit, and still no answer that says "refused" or "unresponsive".
            Assert.False(start.IsCompleted);

            release.Set();
            var response = await start;

            Assert.Equal(RunStates.Running, response.State);
            Assert.Single(h.Host.ExecutionHost.Requests);
            await ResultOf(h, "r-1");
        }

        // ── Cancelling ──

        [Fact]
        public async Task ACancelWhileTheRunGoesOn_EndsItAtTheNextBoundary_WithOneResult()
        {
            var h = new Harness();
            h.Host.ExecutionHost.RunGate = new TaskCompletionSource<bool>();
            await h.Supervisor.StartAsync(Request("r-1"));

            var cancel = h.Supervisor.Cancel(new CancelRunRequest { RequestId = "r-1" });
            var again = h.Supervisor.Cancel(new CancelRunRequest { RequestId = "r-1" });
            var done = await ResultOf(h, "r-1");

            Assert.True(cancel.CancelRequested);
            Assert.Equal(RunStates.Running, cancel.State);
            Assert.True(again.CancelRequested);
            Assert.Equal(RunStatuses.Cancelled, done.Result!.Status);
            Assert.Equal(FailureReasons.Cancelled, done.Result.FailureReason);
            Assert.Equal(1, h.Registry.LastResultSeq);
        }

        [Fact]
        public async Task ACancelThatArrivesBeforeTheRunWasAdmitted_IsNotLost()
        {
            var h = new Harness();
            var window = new ManualWindow();
            h.Dispatch = window.Dispatch;
            h.Host.ExecutionHost.RunGate = new TaskCompletionSource<bool>();
            var start = h.Supervisor.StartAsync(Request("r-1"));
            await Task.Delay(50);

            var cancel = h.Supervisor.Cancel(new CancelRunRequest { RequestId = "r-1" });
            window.RunAll();
            await start;
            var done = await ResultOf(h, "r-1");

            Assert.True(cancel.CancelRequested);
            Assert.True(h.Host.ExecutionHost.RunTokens.Single().IsCancellationRequested);
            Assert.Equal(RunStatuses.Cancelled, done.Result!.Status);
        }

        [Fact]
        public async Task ACancelOfAnIdThatIsUnknown_OrOfARunThatHasEnded_IsAnAnswerNotAnError()
        {
            var h = new Harness();
            await h.Supervisor.StartAsync(Request("r-1"));
            await ResultOf(h, "r-1");

            var unknown = h.Supervisor.Cancel(new CancelRunRequest { RequestId = "nobody" });
            var ended = h.Supervisor.Cancel(new CancelRunRequest { RequestId = "r-1" });

            Assert.Equal(RunStates.Unknown, unknown.State);
            Assert.False(unknown.CancelRequested);
            Assert.Equal(RunStates.Completed, ended.State);
            Assert.False(ended.CancelRequested);
            Assert.Throws<BridgeFault>(() => h.Supervisor.Cancel(new CancelRunRequest { RequestId = "not valid" }));
        }

        // ── Results ──

        [Fact]
        public async Task ARun_HasOneStartedAndOneCompletedEvent_InThatOrder_AndNoMore()
        {
            var h = new Harness();
            h.Host.ExecutionHost.RunGate = new TaskCompletionSource<bool>();
            var response = await h.Supervisor.StartAsync(Request("r-1"));

            // A duplicate and a lookup are not new events.
            await h.Supervisor.StartAsync(Request("r-1"));
            h.Host.ExecutionHost.RunGate.SetResult(true);
            var done = await ResultOf(h, "r-1");
            await ResultOf(h, "r-1");
            await Task.Delay(200);

            Assert.Equal(2, h.Events.Count);
            var started = Assert.IsType<RunStartedEvent>(h.Events[0]);
            var completed = Assert.IsType<RunCompletedEvent>(h.Events[1]);
            Assert.Equal(response.RunId, started.RunId);
            Assert.Equal("r-1", started.RequestId);
            Assert.Equal(done.Result, completed.Result);
        }

        [Fact]
        public async Task ALookupByRunId_FindsTheSameRun()
        {
            var h = new Harness();
            var response = await h.Supervisor.StartAsync(Request("r-1"));
            await ResultOf(h, "r-1");

            var byRunId = await h.Supervisor.GetResultAsync(new GetRunResultRequest { RunId = response.RunId });

            Assert.Equal(RunStates.Completed, byRunId.State);
            Assert.Equal("r-1", byRunId.Result!.RequestId);
        }

        [Fact]
        public async Task ALookupThatWaits_ReturnsAsSoonAsTheRunEnds_OrSaysRunningWhenTheTimeIsUp()
        {
            var h = new Harness();
            h.Host.ExecutionHost.RunGate = new TaskCompletionSource<bool>();
            await h.Supervisor.StartAsync(Request("r-1"));

            var watch = System.Diagnostics.Stopwatch.StartNew();
            var timedOut = await ResultOf(h, "r-1", 150);
            var shortWait = watch.Elapsed;

            var waiting = ResultOf(h, "r-1", 10_000);
            await Task.Delay(100);
            Assert.False(waiting.IsCompleted);
            h.Host.ExecutionHost.RunGate.SetResult(true);
            var completed = await waiting.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(RunStates.Running, timedOut.State);
            Assert.True(shortWait >= TimeSpan.FromMilliseconds(100));
            Assert.Equal(RunStates.Completed, completed.State);
        }

        [Fact]
        public async Task ALookupOfAnIdNobodyKnows_IsUnknown_NotAnError()
        {
            var h = new Harness();

            Assert.Equal(RunStates.Unknown, (await h.Supervisor.GetResultAsync(new GetRunResultRequest { RequestId = "nobody" })).State);
            Assert.Equal(RunStates.Unknown, (await h.Supervisor.GetResultAsync(new GetRunResultRequest { RunId = "nobody" })).State);
        }

        [Theory]
        [InlineData(null, null, 0, "requestId")]
        [InlineData("r-1", "run-1", 0, "requestId")]
        [InlineData("not valid", null, 0, "requestId")]
        [InlineData(null, "not valid", 0, "runId")]
        [InlineData("r-1", null, -1, "waitMs")]
        [InlineData("r-1", null, 30001, "waitMs")]
        public async Task ALookupThatNamesTheRunWrongly_OrWaitsTooLong_IsRefused(string? requestId, string? runId, int waitMs, string field)
        {
            var h = new Harness();

            var fault = await RefusalOf(() => h.Supervisor.GetResultAsync(new GetRunResultRequest { RequestId = requestId, RunId = runId, WaitMs = waitMs }));

            Assert.Equal(BridgeErrorCodes.InvalidRequest, fault.Code);
            Assert.Equal(field, fault.Details!["field"]);
        }

        [Fact]
        public async Task ARunOfAnotherStarter_IsNoRunOfTheBridge()
        {
            var h = new Harness();

            h.Host.ExecutionHost.SetRunning(true);
            h.Host.ExecutionHost.SetRunning(false);

            Assert.False(h.Supervisor.HasActiveRun);
            Assert.Null(h.Supervisor.ActiveRun);
            Assert.Empty(h.Events);
            Assert.Equal(0, h.Registry.LastResultSeq);
            await Task.CompletedTask;
        }

        [Fact]
        public async Task TheRunInFlight_IsKnownByItsRequestAndItsId()
        {
            var h = new Harness();
            h.Host.ExecutionHost.RunGate = new TaskCompletionSource<bool>();
            var response = await h.Supervisor.StartAsync(Request("r-1"));

            var active = h.Supervisor.ActiveRun!;

            Assert.Equal("r-1", active.RequestId);
            Assert.Equal(response.RunId, active.RunId);
            Assert.Equal(RunSources.Bridge, active.Source);
            Assert.NotNull(active.StartedAt);
            h.Host.ExecutionHost.RunGate.SetResult(true);
            await ResultOf(h, "r-1");
            Assert.Null(h.Supervisor.ActiveRun);
        }

        // ── What is kept ──

        [Fact]
        public async Task AResultIsKeptForAWhile_ThenItIsUnknown_ButTheIdIsRememberedLongerStill()
        {
            var h = new Harness(registry: new RunRegistry(resultHistory: 2, requestIdHistory: 3));
            var ids = new List<string>();

            for (var i = 1; i <= 4; i++)
            {
                ids.Add((await h.Supervisor.StartAsync(Request("r-" + i))).RunId);
                await ResultOf(h, "r-" + i);
            }

            // Results: the last two are kept. Ids: the last three are remembered.
            Assert.Equal(RunStates.Unknown, (await ResultOf(h, "r-1", 0)).State);
            Assert.Equal(RunStates.Unknown, (await ResultOf(h, "r-2", 0)).State);
            Assert.Equal(RunStates.Completed, (await ResultOf(h, "r-3", 0)).State);
            Assert.Equal(RunStates.Completed, (await ResultOf(h, "r-4", 0)).State);

            // r-2 is still remembered as a request: sending it again does not start a second run.
            var remembered = await h.Supervisor.StartAsync(Request("r-2"));
            Assert.True(remembered.Duplicate);
            Assert.Equal(ids[1], remembered.RunId);
            Assert.Equal(4, h.Host.ExecutionHost.Requests.Count);

            // r-1 is forgotten: the same id is a new request now, and starts a new run.
            var forgotten = await h.Supervisor.StartAsync(Request("r-1"));
            Assert.False(forgotten.Duplicate);
            Assert.NotEqual(ids[0], forgotten.RunId);
            Assert.Equal(5, h.Host.ExecutionHost.Requests.Count);
        }

        [Fact]
        public async Task TheResultsAreNumberedInTheOrderTheyEnded()
        {
            var h = new Harness();

            for (var i = 1; i <= 3; i++)
            {
                await h.Supervisor.StartAsync(Request("r-" + i));
                Assert.Equal(i, (await ResultOf(h, "r-" + i)).Result!.ResultSeq);
            }

            Assert.Equal(3, h.Supervisor.LastResultSeq);
        }
    }
}
