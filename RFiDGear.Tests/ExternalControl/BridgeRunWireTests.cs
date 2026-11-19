#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using RFiDGear.Bridge;
using RFiDGear.Contracts;
using RFiDGear.Infrastructure.ExtensionHost;
using RFiDGear.Infrastructure.FileAccess;
using RFiDGear.Models;
using RFiDGear.Services.ExternalControl;
using RFiDGear.Tests.CardCheckAssistant;
using RFiDGear.ViewModel.TaskSetupViewModels;

using StreamJsonRpc;

using Xunit;

namespace RFiDGear.Tests.ExternalControl
{
    /// <summary>
    /// Runs as a program outside RFiDGear sees them, on a real pipe: the answers, the events, a connection that is lost in the
    /// middle of a run, and what the log must not hold. Also against the real host, where the id of the run is the one the
    /// bridge chose.
    /// </summary>
    [Collection("AppDataPath")]
    public sealed class BridgeRunWireTests : IDisposable
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

        private static StartRunRequest Request(string id, params (string Name, string Value)[] variables) => new StartRunRequest
        {
            RequestId = id,
            Variables = variables.Select(v => new VariablePair { Name = v.Name, Value = v.Value }).ToList(),
        };

        private static string Json(RunResultDto result) => System.Text.Json.JsonSerializer.Serialize(result, BridgeJson.CreateClientOptions());

        private static async Task WaitUntilAsync(Func<bool> condition)
        {
            var until = DateTime.UtcNow.AddSeconds(5);

            while (!condition())
            {
                Assert.True(DateTime.UtcNow < until, "The condition did not become true in time.");
                await Task.Delay(10);
            }
        }

        // ── A run, as a client sees it ──

        [Fact]
        public async Task ARun_IsAnsweredAtOnce_ThenToldAsEvents_AndTheResultIsTheOneALookupGives()
        {
            var (_, pipe, host) = Start();
            host.ExecutionHost.RunGate = new TaskCompletionSource<bool>();
            var client = await ConnectAsync(pipe, BridgeEvents.RunStarted, BridgeEvents.RunCompleted);

            var response = await client.Service.StartRun(Request("r-1", ("$JOBNUMBER", "J-42")));
            var started = Assert.IsType<RunStartedEvent>((await client.WaitForEventsAsync(1))[0]);
            host.ExecutionHost.RunGate.SetResult(true);
            var events = await client.WaitForEventsAsync(2);
            await Task.Delay(200);
            var lookup = await client.Service.GetRunResult(new GetRunResultRequest { RequestId = "r-1" });

            Assert.Equal(RunStates.Running, response.State);
            Assert.Equal(response.RunId, started.RunId);
            Assert.Equal(2, client.Events.Count);
            var completed = Assert.IsType<RunCompletedEvent>(events[1]);
            Assert.Equal(RunStates.Completed, lookup.State);

            // The same result, written the same way (the records hold lists, which are compared by reference).
            Assert.Equal(Json(lookup.Result!), Json(completed.Result));
            Assert.Equal(response.RunId, completed.Result.RunId);
            Assert.Equal(2, completed.Result.TaskOutcomes.Count);
        }

        [Fact]
        public async Task AConnectionThatIsLostInTheMiddleOfARun_ComesBackToTheSameRun_AndItsResult()
        {
            var (_, pipe, host) = Start();
            host.ExecutionHost.RunGate = new TaskCompletionSource<bool>();
            var first = await ConnectAsync(pipe);
            var response = await first.Service.StartRun(Request("r-1", ("$JOBNUMBER", "J-42")));
            var instanceId = (await first.Service.GetStatus(new GetStatusRequest())).InstanceId;
            first.Dispose();

            // The run goes on without anybody connected, and ends.
            host.ExecutionHost.RunGate.SetResult(true);
            var second = await ConnectAsync(pipe);
            var status = await second.Service.GetStatus(new GetStatusRequest());
            var lookup = await second.Service.GetRunResult(new GetRunResultRequest { RequestId = "r-1", WaitMs = 5000 });
            var again = await second.Service.StartRun(Request("r-1", ("$JOBNUMBER", "J-42")));

            Assert.Equal(instanceId, status.InstanceId);
            Assert.Equal(RunStates.Completed, lookup.State);
            Assert.Equal(response.RunId, lookup.Result!.RunId);
            Assert.True(again.Duplicate);
            Assert.Equal(response.RunId, again.RunId);
            Assert.Equal(RunStates.Completed, again.State);
            Assert.Single(host.ExecutionHost.Requests);
            Assert.Equal(1, (await second.Service.GetStatus(new GetStatusRequest())).LastResultSeq);
        }

        [Fact]
        public async Task WhileARunGoesOn_TheStatusAndTheNoticesSayWhoStartedIt()
        {
            var (service, pipe, host) = Start();
            host.ExecutionHost.RunGate = new TaskCompletionSource<bool>();
            host.ExecutionHost.SimulateRunningState = true;
            var client = await ConnectAsync(pipe, BridgeEvents.RunningChanged);

            var response = await client.Service.StartRun(Request("r-1"));
            var status = await client.Service.GetStatus(new GetStatusRequest());
            var running = Assert.IsType<RunningChangedEvent>((await client.WaitForEventsAsync(1))[0]);

            // What the application asks before it closes, and what the shutdown notice says.
            Assert.True(service.RunInFlight());
            Assert.True(status.IsRunning);
            Assert.Equal("r-1", status.ActiveRun!.RequestId);
            Assert.Equal(response.RunId, status.ActiveRun.RunId);
            Assert.Equal(RunSources.Bridge, status.ActiveRun.Source);
            Assert.True(running.IsRunning);
            Assert.Equal("r-1", running.OwnRequestId);
            Assert.Equal(response.RunId, running.OwnRunId);
            host.ExecutionHost.RunGate.SetResult(true);
            await client.Service.GetRunResult(new GetRunResultRequest { RequestId = "r-1", WaitMs = 5000 });
            Assert.Null((await client.Service.GetStatus(new GetStatusRequest())).ActiveRun);
        }

        [Fact]
        public async Task WhenTheHostHasGivenUpItsGate_ANewRunOfSomeoneElse_IsNotLabelledWithTheRunThatJustEnded()
        {
            var (_, pipe, host) = Start();
            host.ExecutionHost.RunGate = new TaskCompletionSource<bool>();
            host.ExecutionHost.SimulateRunningState = true;
            var client = await ConnectAsync(pipe, BridgeEvents.RunningChanged, BridgeEvents.RunCompleted);
            var response = await client.Service.StartRun(Request("r-1"));
            var first = Assert.IsType<RunningChangedEvent>((await client.WaitForEventsAsync(1))[0]);
            Assert.Equal("r-1", first.OwnRequestId);

            // The host gives up its gate and reports the run as ended; the task of the run has not completed yet. Somebody else
            // takes the gate in between.
            host.ExecutionHost.SetRunning(false);

            // Gate given up, completion not reported yet: nothing is running, so no run is shown as going on.
            Assert.Null((await client.Service.GetStatus(new GetStatusRequest())).ActiveRun);
            host.ExecutionHost.RaiseExecutionCompleted(FakeResults.Success(2) with { RunId = response.RunId });
            host.ExecutionHost.SetRunning(true);
            await WaitUntilAsync(() => client.Events.Skip(1).OfType<RunningChangedEvent>().Any(e => e.IsRunning));
            var status = await client.Service.GetStatus(new GetStatusRequest());

            Assert.True(status.IsRunning);
            Assert.Null(status.ActiveRun);
            Assert.All(client.Events.Skip(1).OfType<RunningChangedEvent>(), e => Assert.Null(e.OwnRequestId));

            // The run of the bridge still ends with its one result.
            host.ExecutionHost.RunGate.SetResult(true);
            var lookup = await client.Service.GetRunResult(new GetRunResultRequest { RequestId = "r-1", WaitMs = 5000 });
            Assert.Equal(RunStates.Completed, lookup.State);
        }

        [Fact]
        public async Task ANoticeIsMadeWhenItIsSent_AndNamesTheOwnerOnlyWhileTheHostStillRunsThatRun()
        {
            var (service, pipe, host) = Start();
            host.ExecutionHost.RunGate = new TaskCompletionSource<bool>();
            host.ExecutionHost.SimulateRunningState = true;
            var client = await ConnectAsync(pipe);
            var response = await client.Service.StartRun(Request("r-1"));
            var ownWhenItChanged = (await client.Service.GetStatus(new GetStatusRequest())).ActiveRun;

            var whileItRuns = service.MakeRunningChanged(1, "session", DateTimeOffset.UtcNow, ownWhenItChanged);
            var nobody = service.MakeRunningChanged(2, "session", DateTimeOffset.UtcNow, null);

            // The host has given up its gate and not reported the end of the run yet: nothing runs, so nobody owns it.
            host.ExecutionHost.SetRunningQuietly(false);
            var idle = service.MakeRunningChanged(4, "session", DateTimeOffset.UtcNow, ownWhenItChanged);
            host.ExecutionHost.SetRunningQuietly(true);
            Assert.False(idle.IsRunning);
            Assert.Null(idle.OwnRequestId);

            // The host gives up the run, and somebody else is running when the notice that was made for the start is sent.
            host.ExecutionHost.RaiseExecutionCompleted(FakeResults.Success(2) with { RunId = response.RunId });
            host.ExecutionHost.SetRunningQuietly(true);
            var afterwards = service.MakeRunningChanged(3, "session", DateTimeOffset.UtcNow, ownWhenItChanged);

            Assert.Equal(("r-1", response.RunId, true), (whileItRuns.OwnRequestId, whileItRuns.OwnRunId, whileItRuns.IsRunning));
            Assert.Null(nobody.OwnRequestId);
            Assert.True(afterwards.IsRunning);
            Assert.Null(afterwards.OwnRequestId);
            Assert.Null(afterwards.OwnRunId);
            host.ExecutionHost.RunGate.SetResult(true);
        }

        [Fact]
        public async Task ARunOfSomeoneElse_IsShownAsRunning_ButHasNoOwnerHere_AndNeverANotice()
        {
            var (_, pipe, host) = Start();
            var client = await ConnectAsync(pipe, BridgeEvents.RunningChanged, BridgeEvents.RunCompleted);

            host.ExecutionHost.SetRunning(true);
            var on = Assert.IsType<RunningChangedEvent>((await client.WaitForEventsAsync(1))[0]);
            var status = await client.Service.GetStatus(new GetStatusRequest());
            host.ExecutionHost.SetRunning(false);
            await client.WaitForEventsAsync(2);
            await Task.Delay(200);

            Assert.True(on.IsRunning);
            Assert.Null(on.OwnRequestId);
            Assert.True(status.IsRunning);
            Assert.Null(status.ActiveRun);
            Assert.All(client.Events, e => Assert.IsType<RunningChangedEvent>(e));
        }

        [Fact]
        public async Task ALongPollingLookup_DoesNotBlockTheOtherCalls_AndEndsWhenTheRunEnds()
        {
            var (_, pipe, host) = Start();
            host.ExecutionHost.RunGate = new TaskCompletionSource<bool>();
            host.ExecutionHost.SimulateRunningState = true;
            var client = await ConnectAsync(pipe);
            await client.Service.StartRun(Request("r-1"));

            var waiting = client.Service.GetRunResult(new GetRunResultRequest { RequestId = "r-1", WaitMs = 20_000 });
            await Task.Delay(100);
            var status = await client.Service.GetStatus(new GetStatusRequest());
            var cancel = await client.Service.CancelRun(new CancelRunRequest { RequestId = "r-1" });
            var result = await waiting.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.False(waiting.IsFaulted);
            Assert.True(status.IsRunning || status.ActiveRun != null);
            Assert.True(cancel.CancelRequested);
            Assert.Equal(RunStates.Completed, result.State);
            Assert.Equal(RunStatuses.Cancelled, result.Result!.Status);
        }

        // ── Errors ──

        [Fact]
        public async Task TheRefusalsOfARun_HaveCodesOfTheirOwn_AndSayThatNothingWasStarted()
        {
            var (_, pipe, host) = Start(() => BusyActivities.Scan);
            var client = await ConnectAsync(pipe);

            host.ExecutionHost.ThrowOnExecute = new InvalidOperationException("A reader scan is in progress.");
            var busy = await Assert.ThrowsAsync<RemoteInvocationException>(() => client.Service.StartRun(Request("r-1")));
            host.ExecutionHost.ThrowOnExecute = null;
            host.ProjectHost.TaskCount = 0;
            var empty = await Assert.ThrowsAsync<RemoteInvocationException>(() => client.Service.StartRun(Request("r-2")));
            host.ProjectHost.TaskCount = 2;
            var invalid = await Assert.ThrowsAsync<RemoteInvocationException>(() => client.Service.StartRun(Request("not valid")));

            Assert.Equal(BridgeErrorCodes.Busy, busy.ErrorCode);
            Assert.Equal(BusyActivities.Scan, BridgeTestClient.DataOf(busy)!.Details!["activity"]);
            Assert.True(BridgeTestClient.DataOf(busy)!.NeverStarted);
            Assert.Equal(BridgeErrorCodes.NoProjectLoaded, empty.ErrorCode);
            Assert.True(BridgeTestClient.DataOf(empty)!.NeverStarted);
            Assert.Equal(BridgeErrorCodes.InvalidRequest, invalid.ErrorCode);
        }

        [Fact]
        public async Task TheSameIdWithOtherContent_IsAConflictOverThePipe()
        {
            var (_, pipe, host) = Start();
            host.ExecutionHost.RunGate = new TaskCompletionSource<bool>();
            var client = await ConnectAsync(pipe);
            await client.Service.StartRun(Request("r-1", ("$JOBNUMBER", "J-42")));

            var conflict = await Assert.ThrowsAsync<RemoteInvocationException>(() => client.Service.StartRun(Request("r-1", ("$JOBNUMBER", "J-43"))));

            Assert.Equal(BridgeErrorCodes.RequestIdConflict, conflict.ErrorCode);
            Assert.Single(host.ExecutionHost.Requests);
            host.ExecutionHost.RunGate.SetResult(true);
        }

        // ── What must not be in the log ──

        [Fact]
        public async Task NeitherTheValuesOfTheVariablesNorTheReportPathReachTheLog()
        {
            using var logs = CapturedLogs.Install();
            var (_, pipe, host) = Start();
            var client = await ConnectAsync(pipe);

            await client.Service.StartRun(Request("r-1", ("REPORTTARGETPATH", @"C:\SECRET-reports\SECRET-card.pdf"), ("$JOBNUMBER", "SECRET-job")));
            await client.Service.GetRunResult(new GetRunResultRequest { RequestId = "r-1", WaitMs = 5000 });
            host.ExecutionHost.ThrowOnExecute = new InvalidOperationException("SECRET-text of the host");
            await Assert.ThrowsAsync<RemoteInvocationException>(() => client.Service.StartRun(Request("r-2", ("$JOBNUMBER", "SECRET-job"))));
            await Task.Delay(100);

            var text = string.Join("\n", logs.All().Select(CapturedLogs.EverythingWrittenFor));
            Assert.Contains("r-1", text, StringComparison.Ordinal);
            Assert.DoesNotContain("SECRET", text, StringComparison.Ordinal);
        }

        // ── Against the real host ──

        private sealed class RealHost : IDisposable
        {
            public RealHost()
            {
                var live = Live;
                Coordinator = new ExtensionOperationCoordinator();
                Host = new HeadlessExtensionHost(
                    new DatabaseReaderWriter(),
                    Exec,
                    () => live,
                    action =>
                    {
                        action();
                        AfterCommit?.Invoke();

                        return Task.CompletedTask;
                    },
                    loaded =>
                    {
                        live.TaskCollection!.Clear();

                        foreach (var task in loaded?.TaskCollection ?? Enumerable.Empty<object>())
                        {
                            live.TaskCollection.Add(task);
                        }
                    },
                    "Test reader",
                    Coordinator);
            }

            public HeadlessExtensionHost Host { get; }

            /// <summary>The live project, which the window would edit.</summary>
            public ChipTaskHandlerModel Live { get; } = new ChipTaskHandlerModel { TaskCollection = new ObservableCollection<object>() };

            /// <summary>The service the host's runs go through; it has been asked only for runs that were admitted.</summary>
            public CapturingExecService Exec { get; } = new CapturingExecService();

            public ExtensionOperationCoordinator Coordinator { get; }

            /// <summary>What happens right after the window has done what the host asked it to: the moment an edit can get in.</summary>
            public Action? AfterCommit { get; set; }

            public void Dispose() => Host.Dispose();
        }

        private async Task<(BridgeTestClient Client, ExternalControlService Service, RealHost Real)> ConnectToRealHostAsync(params string[] events)
        {
            var real = new RealHost();
            _disposables.Add(real);
            var name = "RFiDGear.Bridge.test-" + Guid.NewGuid().ToString("N");
            var service = new ExternalControlService(real.Host, RunAtOnce, () => false, "2.0.10", name, busyActivity: () => ExternalControlService.ActivityOf(real.Coordinator));
            _disposables.Add(service);
            Assert.True(service.Start());
            service.MarkStartupComplete();

            return (await ConnectAsync(name, events), service, real);
        }

        [Fact]
        public async Task ARealRun_CarriesTheIdTheBridgeGave_FromTheStartToTheResult()
        {
            var path = ProjectFixtures.CreateProject(2);

            try
            {
                var (client, service, _) = await ConnectToRealHostAsync(BridgeEvents.RunCompleted);
                await client.Service.LoadProject(new LoadProjectRequest { Path = path });

                var response = await client.Service.StartRun(Request("r-1", ("$JOBNUMBER", "J-42")));
                var lookup = await client.Service.GetRunResult(new GetRunResultRequest { RequestId = "r-1", WaitMs = 10_000 });
                var completed = Assert.IsType<RunCompletedEvent>((await client.WaitForEventsAsync(1))[0]);

                // The host used the id the bridge chose: the result, the notice and the answer to the start agree.
                Assert.Equal(response.RunId, lookup.Result!.RunId);
                Assert.Equal(response.RunId, completed.Result.RunId);
                Assert.Equal(RunStatuses.Success, lookup.Result.Status);
                Assert.Equal(2, lookup.Result.TotalTasks);
                Assert.Equal(2, lookup.Result.TaskOutcomes.Count);
                Assert.False(service.RunInFlight());
            }
            finally
            {
                TryDelete(path);
            }
        }

        [Fact]
        public async Task TheRealHost_RunsUnderTheIdItIsGiven_AndTheIdsOfItsOwnRunsAreStillItsOwn()
        {
            var path = ProjectFixtures.CreateProject(2);

            try
            {
                using var real = new RealHost();
                Assert.True((await real.Host.Project.LoadAsync(path)).Success);

                var chosen = await ((IPreassignedRunExecution)real.Host.Execution).ExecuteAllAsync(ExecutionRequest.Empty, "chosen-by-the-caller", default);
                var own = await real.Host.Execution.ExecuteAllAsync(ExecutionRequest.Empty);

                Assert.Equal("chosen-by-the-caller", chosen.RunId);
                Assert.Matches("^[0-9a-f]{32}$", own.RunId);
                await Assert.ThrowsAsync<ArgumentException>(() => ((IPreassignedRunExecution)real.Host.Execution).ExecuteAllAsync(ExecutionRequest.Empty, " ", default));
            }
            finally
            {
                TryDelete(path);
            }
        }

        [Fact]
        public async Task ATaskChangedInTheWindowAfterTheClientLooked_RefusesTheRunBeforeTheHost_ButTheRevisionAsItIsNowIsAdmitted()
        {
            var path = ProjectFixtures.CreateProject(2);

            try
            {
                var (client, _, real) = await ConnectToRealHostAsync();
                var loaded = await client.Service.LoadProject(new LoadProjectRequest { Path = path });

                // What the client sees after the load: the revision of the load and the one of the status are the same.
                Assert.Matches("^[0-9a-f]{32}$", loaded.Revision);
                Assert.Equal(loaded.Revision, (await client.Service.GetStatus(new GetStatusRequest())).Project.Revision);

                // The client approved that state. Then a task is changed in the window: the path and the count are as they were.
                ((CommonTaskViewModel)real.Live.TaskCollection[0]).SelectedTaskDescription = "changed after the approval";
                var changed = (await client.Service.GetStatus(new GetStatusRequest())).Project.Revision;

                var refused = await Assert.ThrowsAsync<RemoteInvocationException>(() => client.Service.StartRun(new StartRunRequest
                {
                    RequestId = "r-1",
                    ExpectProjectPath = loaded.Path,
                    ExpectTaskCount = loaded.TaskCount,
                    ExpectProjectRevision = loaded.Revision,
                }));

                Assert.NotEqual(loaded.Revision, changed);
                Assert.Equal(BridgeErrorCodes.ProjectMismatch, refused.ErrorCode);
                Assert.True(BridgeTestClient.DataOf(refused)!.NeverStarted);
                Assert.Equal("expectProjectRevision", BridgeTestClient.DataOf(refused)!.Details!["field"]);
                Assert.Null(real.Exec.CapturedRequest);

                // The same job with the revision as it is now is admitted, and its result says what it started with. What the run
                // leaves on the tasks is no change of the project.
                await client.Service.StartRun(new StartRunRequest { RequestId = "r-2", ExpectProjectRevision = changed });
                var done = await client.Service.GetRunResult(new GetRunResultRequest { RequestId = "r-2", WaitMs = 10_000 });

                Assert.Equal(RunStatuses.Success, done.Result!.Status);
                Assert.Equal(changed, done.Result.ProjectRevision);
                Assert.NotNull(real.Exec.CapturedRequest);
                Assert.Equal(changed, (await client.Service.GetStatus(new GetStatusRequest())).Project.Revision);
            }
            finally
            {
                TryDelete(path);
            }
        }

        [Fact]
        public async Task CardAfterCard_TheApprovedRevisionStaysTheProject_AfterARunThatRewroteSettingsOfItsTasks_ButNotAfterAnEdit()
        {
            var (_, pipe, host) = Start();
            host.ProjectHost.Revision = "r0";
            host.ExecutionHost.RunGate = new TaskCompletionSource<bool>();
            var client = await ConnectAsync(pipe);

            // The first card: the run writes what it found (the application it created, the key version) into its tasks.
            await client.Service.StartRun(new StartRunRequest { RequestId = "card-1", ExpectProjectRevision = "r0" });
            host.ProjectHost.Revision = "r1";
            host.ExecutionHost.RunGate.SetResult(true);
            await client.Service.GetRunResult(new GetRunResultRequest { RequestId = "card-1", WaitMs = 10_000 });
            host.ExecutionHost.RunGate = null;

            // The second card, with the revision that was approved before the first: admitted, and it ran what the first left.
            await client.Service.StartRun(new StartRunRequest { RequestId = "card-2", ExpectProjectRevision = "r0" });
            var second = await client.Service.GetRunResult(new GetRunResultRequest { RequestId = "card-2", WaitMs = 10_000 });

            // A person changes a task in the window: this is no state that a run led to.
            host.ProjectHost.Revision = "r9";
            var refused = await Assert.ThrowsAsync<RemoteInvocationException>(() => client.Service.StartRun(new StartRunRequest { RequestId = "card-3", ExpectProjectRevision = "r0" }));

            Assert.Equal("r1", second.Result!.ProjectRevision);
            Assert.Equal(BridgeErrorCodes.ProjectMismatch, refused.ErrorCode);
            Assert.True(BridgeTestClient.DataOf(refused)!.NeverStarted);
            Assert.Equal(2, host.ExecutionHost.Requests.Count);
        }

        [Fact]
        public async Task TheHostTellsTheRunWhatItLeft_WhileItStillHoldsTheGate_SoNoEditOrLoadCanBeTakenForTheRunsDoing()
        {
            var path = ProjectFixtures.CreateProject(2);

            try
            {
                using var real = new RealHost();
                Assert.True((await real.Host.Project.LoadAsync(path)).Success);
                var before = ((IProjectRevisionSource)real.Host.Project).GetProjectRevision();

                string? told = null;
                var holdsTheGate = false;
                var editRefused = false;
                var loadRefused = false;

                await ((IPreassignedRunExecution)real.Host.Execution).ExecuteAllAsync(ExecutionRequest.Empty, "ends-run", default, revision =>
                {
                    told = revision;
                    holdsTheGate = real.Coordinator.IsExecuting;

                    // What a person or another program would do at this moment, on the thread that told the run: refused.
                    try
                    {
                        real.Host.Project.ReplaceTaskCollection(Array.Empty<object>());
                    }
                    catch (InvalidOperationException)
                    {
                        editRefused = true;
                    }

                    loadRefused = real.Host.Project.LoadAsync(path).GetAwaiter().GetResult().Error == ProjectOpenError.Busy;
                });

                Assert.True(holdsTheGate);
                Assert.True(editRefused);
                Assert.True(loadRefused);
                Assert.Equal(before, told);
                Assert.False(real.Coordinator.IsExecuting);
            }
            finally
            {
                TryDelete(path);
            }
        }

        [Fact]
        public async Task TheRevisionOfALoad_IsTheStateThatWasCommitted_NotWhatAnEditLeftAMomentLater()
        {
            var path = ProjectFixtures.CreateProject(2);

            try
            {
                var (client, _, real) = await ConnectToRealHostAsync();
                var edited = false;

                // A task is changed in the window right after the project was put in place, before the answer is made.
                real.AfterCommit = () =>
                {
                    if (!edited && real.Live.TaskCollection!.Count > 0)
                    {
                        edited = true;
                        ((CommonTaskViewModel)real.Live.TaskCollection[0]).SelectedTaskDescription = "changed right after the commit";
                    }
                };

                var loaded = await client.Service.LoadProject(new LoadProjectRequest { Path = path });
                var now = (await client.Service.GetStatus(new GetStatusRequest())).Project.Revision;

                // The load answers with the project as it was loaded; the edit is a change of it, which a run that expects the
                // answer of the load is refused for. Were the revision read a moment after the commit, the edit would be inside it.
                Assert.True(edited);
                Assert.NotEqual(loaded.Revision, now);

                var refused = await Assert.ThrowsAsync<RemoteInvocationException>(() => client.Service.StartRun(new StartRunRequest { RequestId = "r-1", ExpectProjectRevision = loaded.Revision }));
                Assert.Equal(BridgeErrorCodes.ProjectMismatch, refused.ErrorCode);
            }
            finally
            {
                TryDelete(path);
            }
        }

        [Fact]
        public async Task ARevisionIsOfferedAsACapability_OnlyByAHostThatCanTellOne_AndWithoutItARunThatExpectsOneIsRefused()
        {
            var withRevisions = Start();
            var withoutRevisions = new FakeExtensionHost();
            withoutRevisions.ProjectHost.CurrentPath = @"C:\projects\one.rfPrj";
            withoutRevisions.ProjectHost.TaskCount = 2;
            var name = "RFiDGear.Bridge.test-" + Guid.NewGuid().ToString("N");
            var service = new ExternalControlService(new HostWithoutRevisions(withoutRevisions), RunAtOnce, () => false, "2.0.10", name);
            _disposables.Add(service);
            Assert.True(service.Start());
            service.MarkStartupComplete();

            var offered = await BridgeTestClient.ConnectAsync(withRevisions.PipeName, 5000);
            _disposables.Add(offered);
            var notOffered = await BridgeTestClient.ConnectAsync(name, 5000);
            _disposables.Add(notOffered);

            Assert.Contains(BridgeCapabilities.ProjectRevision, (await offered.HelloAsync()).Capabilities);
            Assert.DoesNotContain(BridgeCapabilities.ProjectRevision, (await notOffered.HelloAsync()).Capabilities);

            // A client that asks for a revision never gets a run without one: it is refused, not ignored.
            var refused = await Assert.ThrowsAsync<RemoteInvocationException>(() => notOffered.Service.StartRun(new StartRunRequest { RequestId = "r-1", ExpectProjectRevision = "aaaa1111" }));

            Assert.Equal(BridgeErrorCodes.ProjectMismatch, refused.ErrorCode);
            Assert.True(BridgeTestClient.DataOf(refused)!.NeverStarted);
            Assert.Empty(withoutRevisions.ExecutionHost.Requests);
            Assert.Null((await notOffered.Service.GetStatus(new GetStatusRequest())).Project.Revision);
        }

        [Fact]
        public async Task TheRevisionOfTheHost_IsReportedByTheLoadAndByTheStatus()
        {
            var (_, pipe, host) = Start();
            host.ProjectHost.Revision = "aaaa1111";
            var client = await ConnectAsync(pipe);

            var loaded = await client.Service.LoadProject(new LoadProjectRequest { Path = @"C:\projects\two.rfPrj" });
            var status = await client.Service.GetStatus(new GetStatusRequest());

            Assert.Equal("aaaa1111", loaded.Revision);
            Assert.Equal("aaaa1111", status.Project.Revision);
        }

        // A host whose project cannot tell a revision: everything else is the fake's.
        private sealed class HostWithoutRevisions : IRFiDGearExtensionHost
        {
            private readonly FakeExtensionHost _inner;

            public HostWithoutRevisions(FakeExtensionHost inner)
            {
                _inner = inner;
                Project = new ProjectWithoutRevisions(inner.ProjectHost);
            }

            public IProjectHost Project { get; }

            public ITaskExecHost Execution => _inner.Execution;

            public IReaderHost Reader => _inner.Reader;

            public IAutoModeHost AutoMode => _inner.AutoMode;

            public IAppContext AppContext => _inner.AppContext;
        }

        private sealed class ProjectWithoutRevisions : IProjectHost
        {
            private readonly IProjectHost _inner;

            public ProjectWithoutRevisions(IProjectHost inner) => _inner = inner;

            public string? CurrentPath => _inner.CurrentPath;

            public int TaskCount => _inner.TaskCount;

            public event EventHandler ProjectChanged
            {
                add => _inner.ProjectChanged += value;
                remove => _inner.ProjectChanged -= value;
            }

            public Task<ProjectOpenResult> LoadAsync(string path, System.Threading.CancellationToken ct = default) => _inner.LoadAsync(path, ct);

            [Obsolete("The synchronous load is not part of what the bridge uses.")]
            public void Load(string path) => throw Unsupported(path);

            public void New() => throw Unsupported();

            public void Save() => throw Unsupported();

            public void SaveAs(string path) => throw Unsupported(path);

            public void ReplaceTaskCollection(IEnumerable<object> tasks) => throw Unsupported(tasks);

            private static NotSupportedException Unsupported(params object[] arguments) =>
                new NotSupportedException("The bridge does not use this (" + arguments.Length + " arguments).");
        }

        [Fact]
        public async Task ARealHostThatIsBusy_RefusesTheRun_AndRetryingTheSameIdLaterStartsIt()
        {
            var path = ProjectFixtures.CreateProject(1);

            try
            {
                var (client, _, real) = await ConnectToRealHostAsync();
                await client.Service.LoadProject(new LoadProjectRequest { Path = path });
                Assert.True(real.Coordinator.TryBeginScan());

                var busy = await Assert.ThrowsAsync<RemoteInvocationException>(() => client.Service.StartRun(Request("r-1")));
                real.Coordinator.EndScan();
                var retry = await client.Service.StartRun(Request("r-1"));

                Assert.Equal(BridgeErrorCodes.Busy, busy.ErrorCode);
                Assert.Equal(BusyActivities.Scan, BridgeTestClient.DataOf(busy)!.Details!["activity"]);
                Assert.True(BridgeTestClient.DataOf(busy)!.NeverStarted);
                Assert.False(retry.Duplicate);
                Assert.Equal(RunStates.Completed, (await client.Service.GetRunResult(new GetRunResultRequest { RequestId = "r-1", WaitMs = 10_000 })).State);
            }
            finally
            {
                TryDelete(path);
            }
        }

        [Fact]
        public async Task ARealReportVariableThatCannotProduceItsReport_IsRefusedBeforeAnyCardWork()
        {
            var path = ProjectFixtures.CreateProject(1);

            try
            {
                var (client, _, _) = await ConnectToRealHostAsync();
                await client.Service.LoadProject(new LoadProjectRequest { Path = path });
                var nowhere = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "no", "report.pdf");

                var failure = await Assert.ThrowsAsync<RemoteInvocationException>(() => client.Service.StartRun(Request("r-1", ("REPORTTARGETPATH", nowhere))));

                Assert.Equal(BridgeErrorCodes.InvalidRequest, failure.ErrorCode);
                Assert.Equal("variables", BridgeTestClient.DataOf(failure)!.Details!["field"]);
                Assert.DoesNotContain(nowhere, failure.Message, StringComparison.Ordinal);
                Assert.Equal(RunStates.Unknown, (await client.Service.GetRunResult(new GetRunResultRequest { RequestId = "r-1" })).State);
            }
            finally
            {
                TryDelete(path);
            }
        }

        // ── Closing the application ──

        [Fact]
        public async Task TheApplicationAsksBeforeItClosesDuringARunOfTheBridge_AndOnlyThen()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var viewModel = await ExternalControlWiringTests.InitializeAsync("RFiDGear.Bridge.test-" + Guid.NewGuid().ToString("N"), setting: true);

                try
                {
                    var service = viewModel.ExternalControl!;
                    var runInFlight = false;
                    service.RunInFlight = () => runInFlight;
                    var asked = 0;

                    // No run: it closes without asking.
                    Assert.True(viewModel.ShouldClose(() =>
                    {
                        asked++;

                        return false;
                    }));
                    Assert.Equal(0, asked);

                    // A run of the bridge: the answer of the user decides.
                    runInFlight = true;
                    Assert.False(viewModel.ShouldClose(() =>
                    {
                        asked++;

                        return false;
                    }));
                    Assert.True(viewModel.ShouldClose(() =>
                    {
                        asked++;

                        return true;
                    }));
                    Assert.Equal(2, asked);
                }
                finally
                {
                    viewModel.ExternalControl?.Dispose();
                }
            });
        }

        [Fact]
        public async Task WithTheBridgeOff_TheApplicationClosesWithoutAsking()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var viewModel = await ExternalControlWiringTests.InitializeAsync("RFiDGear.Bridge.test-" + Guid.NewGuid().ToString("N"), setting: false);
                var asked = 0;

                Assert.True(viewModel.ShouldClose(() =>
                {
                    asked++;

                    return false;
                }));
                Assert.Equal(0, asked);
            });
        }

        private static void TryDelete(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
                // A temporary file that stays behind is not a failure of the test.
            }
        }
    }
}
