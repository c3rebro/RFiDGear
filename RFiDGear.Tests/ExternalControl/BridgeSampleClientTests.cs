#nullable enable
using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using RFiDGear.Bridge;
using RFiDGear.Bridge.SampleClient;
using RFiDGear.Contracts;
using RFiDGear.ExtensionSdk;
using RFiDGear.Services.AutoMode;
using RFiDGear.Services.ExternalControl;
using RFiDGear.Services.SecureKeys;
using RFiDGear.Tests.CardCheckAssistant;

using Xunit;

namespace RFiDGear.Tests.ExternalControl
{
    /// <summary>
    /// The sample client, run against the real bridge on a real pipe. It is the client guide in code, so these tests are the proof
    /// that what the guide tells a client to do works: the handshake, a run that is never sent twice, the way back after a lost
    /// connection, the catch-up on missed results and the vault of a client.
    /// </summary>
    public sealed class BridgeSampleClientTests : IAsyncDisposable
    {
        private readonly List<IDisposable> _disposables = new List<IDisposable>();
        private readonly List<BridgeSampleClient> _clients = new List<BridgeSampleClient>();

        public async ValueTask DisposeAsync()
        {
            foreach (var client in _clients)
            {
                await client.DisposeAsync();
            }

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

        private (ExternalControlService Service, string Pipe, FakeExtensionHost Host, BridgeKeyProvider Keys) Start(Func<Action, Task>? dispatch = null, Func<string?, string?>? windowRunSource = null)
        {
            var name = "RFiDGear.Bridge.test-" + Guid.NewGuid().ToString("N");
            var host = new FakeExtensionHost();
            host.ProjectHost.CurrentPath = @"C:\projects\one.rfPrj";
            host.ProjectHost.TaskCount = 2;
            var keys = new BridgeKeyProvider();
            var service = new ExternalControlService(host, dispatch ?? RunAtOnce, () => false, "2.0.10", name, keyProvider: keys, windowRunSource: windowRunSource);
            _disposables.Add(service);
            Assert.True(service.Start());
            service.MarkStartupComplete();

            return (service, name, host, keys);
        }

        private async Task<BridgeSampleClient> ConnectAsync(string pipe, params string[] events)
        {
            var client = await BridgeSampleClient.ConnectAsync(pipe, events);
            _clients.Add(client);

            return client;
        }

        private static readonly Dictionary<string, string> NoVariables = new Dictionary<string, string>();

        [Fact]
        public async Task TheDefaultPipeName_IsTheOneTheHostListensOn()
        {
            Assert.Equal(PipeNames.ForCurrentUser(), BridgeSampleClient.DefaultPipeName());
            await Task.CompletedTask;
        }

        [Fact]
        public async Task TheEndOfTheConnection_IsSeenByTheClient_WhenRFiDGearCloses()
        {
            var (service, pipe, _, _) = Start();
            var client = await ConnectAsync(pipe);
            Assert.False(client.Completion.IsCompleted);

            service.Dispose();

            Assert.Same(client.Completion, await Task.WhenAny(client.Completion, Task.Delay(5000)));
        }

        [Fact]
        public async Task Connecting_MakesTheHandshake_AndTheClientDecidesFromTheCapabilities()
        {
            var (service, pipe, _, _) = Start();

            var client = await ConnectAsync(pipe);

            Assert.Equal(service.InstanceId, client.Hello.InstanceId);
            Assert.True(client.Offers(BridgeCapabilities.ExecutionRun));
            Assert.True(client.Offers(BridgeCapabilities.KeysProvider));
            Assert.False(client.Offers("somethingOfALaterVersion"));
        }

        [Fact]
        public async Task ARun_ReturnsItsResult_AndTheRequestIdIsTheOneTheClientChose()
        {
            var (_, pipe, host, _) = Start();
            var client = await ConnectAsync(pipe, BridgeEvents.RunCompleted);
            var seen = new List<BridgeEvent>();
            client.EventReceived += seen.Add;

            var outcome = await client.RunOnceAsync("job-1", new Dictionary<string, string> { ["$JOBNUMBER"] = "J-42" });

            Assert.Equal(RunOutcomeKind.Completed, outcome.Kind);
            Assert.Equal("job-1", outcome.RequestId);
            Assert.Equal("job-1", outcome.Result!.RequestId);
            Assert.Equal(RunStatuses.Success, outcome.Result.Status);
            Assert.Single(host.ExecutionHost.Requests);
            await WaitUntilAsync(() => seen.Count == 1);
            Assert.IsType<RunCompletedEvent>(seen[0]);
        }

        [Fact]
        public async Task ARefusalThatSaysNothingWasStarted_IsNotStarted_AndTheHostWasNeverAsked()
        {
            var (_, pipe, host, _) = Start();
            host.ProjectHost.TaskCount = 0;
            var client = await ConnectAsync(pipe);

            var outcome = await client.RunOnceAsync("job-1", NoVariables);

            Assert.Equal(RunOutcomeKind.NotStarted, outcome.Kind);
            Assert.Contains(BridgeErrorKinds.NoProjectLoaded, outcome.Detail, StringComparison.Ordinal);
            Assert.Empty(host.ExecutionHost.Requests);
        }

        [Fact]
        public async Task ARunThatExpectsAnotherProject_IsRefused_AndTheHostIsNeverAsked()
        {
            // The project was replaced between the load and the start: the run says which project it is for.
            var (_, pipe, host, _) = Start();
            var client = await ConnectAsync(pipe);

            var otherPath = await client.RunOnceAsync("job-1", NoVariables, expectProjectPath: @"C:\projects\other.rfPrj");
            var otherCount = await client.RunOnceAsync("job-2", NoVariables, expectProjectPath: @"C:\projects\one.rfPrj", expectTaskCount: 7);
            var matching = await client.RunOnceAsync("job-3", NoVariables, expectProjectPath: @"C:\projects\one.rfPrj", expectTaskCount: 2);

            Assert.Equal(RunOutcomeKind.NotStarted, otherPath.Kind);
            Assert.Contains(BridgeErrorKinds.ProjectMismatch, otherPath.Detail, StringComparison.Ordinal);
            Assert.Equal(RunOutcomeKind.NotStarted, otherCount.Kind);
            Assert.Equal(RunOutcomeKind.Completed, matching.Kind);
            Assert.Single(host.ExecutionHost.Requests);
        }

        [Fact]
        public async Task ARunThatExpectsTheStateItWasApprovedWith_IsRefusedWhenATaskWasChanged_AndTheHostIsNeverAsked()
        {
            // The case that the path and the count cannot see: the same project, and a task changed in the window after the client
            // looked. A run that expects the revision it approved is refused before the host is asked; one with the current revision runs.
            var (_, pipe, host, _) = Start();
            host.ProjectHost.Revision = "aaaa1111";
            var client = await ConnectAsync(pipe);
            var approved = (await client.Service.GetStatus(new GetStatusRequest())).Project.Revision;
            host.ProjectHost.Revision = "bbbb2222";

            var stale = await client.RunOnceAsync("job-1", NoVariables, expectProjectPath: @"C:\projects\one.rfPrj", expectTaskCount: 2, expectProjectRevision: approved);
            var current = await client.RunOnceAsync("job-2", NoVariables, expectProjectPath: @"C:\projects\one.rfPrj", expectTaskCount: 2, expectProjectRevision: "bbbb2222");

            Assert.True(client.Offers(BridgeCapabilities.ProjectRevision));
            Assert.Equal("aaaa1111", approved);
            Assert.Equal(RunOutcomeKind.NotStarted, stale.Kind);
            Assert.Contains(BridgeErrorKinds.ProjectMismatch, stale.Detail, StringComparison.Ordinal);
            Assert.Equal(RunOutcomeKind.Completed, current.Kind);
            Assert.Equal("bbbb2222", current.Result!.ProjectRevision);
            Assert.Single(host.ExecutionHost.Requests);
        }

        [Fact]
        public async Task TheRevisionToExpect_IsTheOneOfTheLoad_AndAMissingOneIsRefusedWhenTheServerOffersRevisions()
        {
            var (_, pipe, host, _) = Start();
            var client = await ConnectAsync(pipe);
            host.ProjectHost.Revision = "aaaa1111";
            var loaded = await client.Service.LoadProject(new LoadProjectRequest { Path = @"C:\projects\two.rfPrj" });

            Assert.Equal("aaaa1111", client.RevisionToExpect(loaded));

            // The server offers the capability and could not make a revision for this project: a client that went on would run
            // whatever is loaded, with only the path and the count to hold it back.
            host.ProjectHost.Revision = null;
            var withoutRevision = await client.Service.LoadProject(new LoadProjectRequest { Path = @"C:\projects\two.rfPrj" });

            Assert.Null(withoutRevision.Revision);
            Assert.Throws<InvalidOperationException>(() => client.RevisionToExpect(withoutRevision));
        }

        [Fact]
        public async Task AServerThatNeverAnswersTheHandshake_EndsInATimeout_AndInACancellationWhenTheCallerGivesUp()
        {
            var name = "RFiDGear.Bridge.test-" + Guid.NewGuid().ToString("N");
            using var silent = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            var accepted = silent.WaitForConnectionAsync();

            // Bounded: a client that waits for ever must fail this test, not hang it.
            var timedOut = Assert.ThrowsAsync<TimeoutException>(() => BridgeSampleClient.ConnectAsync(name, connectTimeout: TimeSpan.FromMilliseconds(300)));
            Assert.Same(timedOut, await Task.WhenAny(timedOut, Task.Delay(10_000)));
            await timedOut;
            await accepted;

            // Another silent server: the pipe connects at once, and it is the handshake that the caller gives up on.
            var otherName = "RFiDGear.Bridge.test-" + Guid.NewGuid().ToString("N");
            using var otherSilent = new NamedPipeServerStream(otherName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            _ = otherSilent.WaitForConnectionAsync();
            using var giveUp = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

            var cancelled = Assert.ThrowsAnyAsync<OperationCanceledException>(() => BridgeSampleClient.ConnectAsync(otherName, connectTimeout: TimeSpan.FromSeconds(20), cancellationToken: giveUp.Token));
            Assert.Same(cancelled, await Task.WhenAny(cancelled, Task.Delay(10_000)));
            await cancelled;
        }

        [Fact]
        public async Task AStartThatIsNotAnsweredInTime_IsAskedAboutAndNeverSentAgain()
        {
            // The window of the application does not take the request for a moment: the client times out, and must not send it again.
            var held = new TaskCompletionSource<Action>(TaskCreationOptions.RunContinuationsAsynchronously);
            var done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var (_, pipe, host, _) = Start(action =>
            {
                held.TrySetResult(() =>
                {
                    action();
                    done.TrySetResult(true);
                });

                return done.Task;
            });
            var client = await ConnectAsync(pipe);
            client.StartTimeout = TimeSpan.FromMilliseconds(300);

            var pending = client.RunOnceAsync("job-1", NoVariables);
            var release = await held.Task;
            await Task.Delay(600);
            Assert.False(pending.IsCompleted);
            release();
            var outcome = await pending.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Equal(RunOutcomeKind.Completed, outcome.Kind);
            Assert.Single(host.ExecutionHost.Requests);
        }

        [Fact]
        public async Task ARunThatTakesLongerThanOneQuestionWaits_IsAskedAboutAgain_UntilItEnds()
        {
            var (_, pipe, host, _) = Start();
            host.ExecutionHost.RunGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var client = await ConnectAsync(pipe);
            client.LongPoll = TimeSpan.FromMilliseconds(150);

            var pending = client.RunOnceAsync("job-1", NoVariables);
            await Task.Delay(700);
            var stillGoingOn = !pending.IsCompleted;
            host.ExecutionHost.RunGate.SetResult(true);
            var outcome = await pending.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.True(stillGoingOn, "The client gave up while the run was still going on.");
            Assert.Equal(RunOutcomeKind.Completed, outcome.Kind);
            Assert.Single(host.ExecutionHost.Requests);
        }

        [Fact]
        public async Task TheLongPoll_HasToBeAPositiveTimeUpToTheLimitOfTheHost()
        {
            var (_, pipe, _, _) = Start();
            var client = await ConnectAsync(pipe);

            Assert.Throws<ArgumentOutOfRangeException>(() => client.LongPoll = TimeSpan.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() => client.LongPoll = TimeSpan.FromMilliseconds(BridgeProtocol.MaxWaitMs + 1));
            client.LongPoll = TimeSpan.FromMilliseconds(BridgeProtocol.MaxWaitMs);
            Assert.Equal(TimeSpan.FromMilliseconds(BridgeProtocol.MaxWaitMs), client.LongPoll);
        }

        [Fact]
        public async Task AnErrorThatDoesNotSayNothingWasStarted_IsDoubt_NotARefusal()
        {
            // The window of the application fails in a way nobody foresaw: the answer is an internal error, which does not promise that
            // the card was left alone. The client must not take it for "never started".
            var (_, pipe, host, _) = Start(_ => throw new FormatException("the window failed"));
            var client = await ConnectAsync(pipe);

            var outcome = await client.RunOnceAsync("job-1", NoVariables);

            Assert.Equal(RunOutcomeKind.Unknown, outcome.Kind);
            Assert.Empty(host.ExecutionHost.Requests);
        }

        [Fact]
        public async Task ALostConnection_IsAnsweredWithUnknown_AndANewConnectionFindsTheSameRun()
        {
            var (_, pipe, host, _) = Start();
            host.ExecutionHost.RunGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var first = await ConnectAsync(pipe);

            var pending = first.RunOnceAsync("job-1", NoVariables);
            await WaitUntilAsync(() => host.ExecutionHost.Requests.Count == 1);
            await first.DisposeAsync();
            var lost = await pending.WaitAsync(TimeSpan.FromSeconds(10));
            var second = await ConnectAsync(pipe);
            var resumed = second.ResumeAsync("job-1");
            host.ExecutionHost.RunGate.SetResult(true);
            var outcome = await resumed.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Equal(RunOutcomeKind.Unknown, lost.Kind);
            Assert.Contains("do not send the job again", lost.Detail, StringComparison.Ordinal);
            Assert.Equal(RunOutcomeKind.Completed, outcome.Kind);
            Assert.Equal("job-1", outcome.Result!.RequestId);
            Assert.Single(host.ExecutionHost.Requests);
        }

        [Fact]
        public async Task AnIdTheHostDoesNotKnow_IsUnknown_NotACompletedRun()
        {
            var (_, pipe, _, _) = Start();
            var client = await ConnectAsync(pipe);

            var outcome = await client.ResumeAsync("never-sent");

            Assert.Equal(RunOutcomeKind.Unknown, outcome.Kind);
            Assert.Null(outcome.Result);
            Assert.Contains("Look at the card", outcome.Detail, StringComparison.Ordinal);
        }

        [Fact]
        public async Task TheCatchUp_ReturnsWhatWasMissed_InOrder_AndTellsWhichProcessItIs()
        {
            var (service, pipe, host, _) = Start();
            var client = await ConnectAsync(pipe);
            host.AutoModeHost.RaiseRunCompleted(FakeResults.Success(1) with { RunId = "auto-a", CardUid = "04AA" });
            host.AutoModeHost.RaiseRunCompleted(FakeResults.Success(1) with { RunId = "auto-b", CardUid = "04BB" });

            var all = await client.CatchUpAsync(0);
            var rest = await client.CatchUpAsync(1);

            Assert.Equal(service.InstanceId, all.InstanceId);
            Assert.Equal(new[] { "auto-a", "auto-b" }, all.Results.Select(r => r.RunId));
            Assert.Equal(new[] { "auto-b" }, rest.Results.Select(r => r.RunId));
            Assert.False(all.Gap);
            Assert.Equal(2, rest.LastResultSeq);
        }

        [Fact]
        public async Task TheVaultOfTheClient_AnswersTheHost_AndARequestForAReferenceItDoesNotServeIsRefused()
        {
            var (_, pipe, _, keys) = Start();
            var registry = SecureKeyRegistry.Create(
                new[] { new SecureKeyProviderRegistration(BridgeKeyProvider.ProviderId, BridgeKeyProvider.DisplayName, keys) },
                Array.Empty<Lazy<ISecureKeyProvider, ISecureKeyProviderMetadata>>());
            var client = await ConnectAsync(pipe);
            var key = Enumerable.Range(1, 16).Select(i => (byte)i).ToArray();

            // The client serves its own reference, and says "not authorized" for everything else.
            using var vault = new SampleVault("orders/4711", key);
            client.KeyVault = vault.ResolveAsync;
            var registered = await client.RegisterKeyProviderAsync();
            using var served = await registry.ResolveAsync(registered.ProviderId, Request("orders/4711"), CancellationToken.None);
            using var refused = await registry.ResolveAsync(registered.ProviderId, Request("somebody/elses"), CancellationToken.None);

            Assert.Equal(BridgeKeyProvider.ProviderId, registered.ProviderId);
            Assert.Equal(key, Assert.IsType<SecureKeyLease>(served.Key).Key.ToArray());
            Assert.Equal(SecureKeyFailureReason.NotAuthorized, refused.FailureReason);
            Assert.Equal(1, vault.Served);
        }

        [Fact]
        public async Task AClientThatAsksForItsOwnRunsOnly_IsAskedForTheRunItStarted_AndNeverForAnother()
        {
            var (_, pipe, host, keys) = Start();
            var registry = SecureKeyRegistry.Create(
                new[] { new SecureKeyProviderRegistration(BridgeKeyProvider.ProviderId, BridgeKeyProvider.DisplayName, keys) },
                Array.Empty<Lazy<ISecureKeyProvider, ISecureKeyProviderMetadata>>());
            var client = await ConnectAsync(pipe);
            var key = Enumerable.Range(1, 16).Select(i => (byte)i).ToArray();
            using var vault = new SampleVault("orders/4711", key);
            client.KeyVault = vault.ResolveAsync;

            var registered = await client.RegisterKeyProviderAsync(ownRunsOnly: true);

            // A run of the client, held in the host while keys are asked for.
            host.ExecutionHost.RunGate = new TaskCompletionSource<bool>();
            var run = client.RunOnceAsync("job-1", NoVariables);
            await WaitUntilAsync(() => host.ExecutionHost.RunIds.Count == 1);
            var ownRunId = host.ExecutionHost.RunIds[0];

            using var own = await registry.ResolveAsync(registered.ProviderId, RequestOfRun("orders/4711", ownRunId), CancellationToken.None);
            using var another = await registry.ResolveAsync(registered.ProviderId, RequestOfRun("orders/4711", "a-run-of-the-window"), CancellationToken.None);
            host.ExecutionHost.RunGate.SetResult(true);
            var outcome = await run;

            Assert.True(registered.OwnRunsOnly);
            Assert.True(client.ServeOwnRunsOnly);
            Assert.Equal(key, Assert.IsType<SecureKeyLease>(own.Key).Key.ToArray());
            Assert.Equal(SecureKeyFailureReason.NotAuthorized, another.FailureReason);
            Assert.Equal(1, vault.Served);
            Assert.Equal(RunOutcomeKind.Completed, outcome.Kind);
        }

        [Fact]
        public async Task AClientThatAsksForItsOwnRunsOnly_IsAskedForTheRunOfACardItApproved_AndNeverForARunOfTheModeNobodyApproved()
        {
            var window = new WindowRunMarker();
            var (_, pipe, host, keys) = Start(windowRunSource: window.SourceOf);
            var registry = SecureKeyRegistry.Create(
                new[] { new SecureKeyProviderRegistration(BridgeKeyProvider.ProviderId, BridgeKeyProvider.DisplayName, keys) },
                Array.Empty<Lazy<ISecureKeyProvider, ISecureKeyProviderMetadata>>());
            // The caller asks for the cards only: the sample asks for the notice that names the run of an approved card itself.
            var client = await ConnectAsync(pipe, BridgeEvents.CardPresented);
            var key = Enumerable.Range(1, 16).Select(i => (byte)i).ToArray();
            using var vault = new SampleVault("orders/4711", key);
            client.KeyVault = vault.ResolveAsync;
            var registered = await client.RegisterKeyProviderAsync(ownRunsOnly: true);
            var presented = new TaskCompletionSource<CardPresentedEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
            client.EventReceived += e =>
            {
                if (e is CardPresentedEvent shownCard)
                {
                    presented.TrySetResult(shownCard);
                }
            };
            await client.Service.StartAutoMode(new StartAutoModeRequest { StopOnDisconnect = true, RequireApproval = true });

            // The machine shows a card, this client approves it, and the window runs it as a run of the mode.
            var waiting = Task.Run(() => host.AutoModeHost.Approval!.RequestAsync(new AutoModePresence("p-1", "04AA"), CancellationToken.None));
            var shown = await presented.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await client.Service.ApproveCard(new ApproveCardRequest { PresenceId = shown.PresenceId });
            var approval = (await waiting)!;
            window.Begin("w-1", RunSources.AutoMode);
            approval.NotifyRunAdmitted("w-1");

            // The request for the key can be here before the notice that names the run: the client waits for it a little.
            using var approved = await registry.ResolveAsync(registered.ProviderId, RequestOfRun("orders/4711", "w-1"), CancellationToken.None);
            window.End("w-1");
            window.Begin("w-2", RunSources.AutoMode);
            using var notApproved = await registry.ResolveAsync(registered.ProviderId, RequestOfRun("orders/4711", "w-2"), CancellationToken.None);

            Assert.Equal(key, Assert.IsType<SecureKeyLease>(approved.Key).Key.ToArray());
            Assert.Equal(SecureKeyFailureReason.NotAuthorized, notApproved.FailureReason);
            Assert.Equal(1, vault.Served);
        }

        [Fact]
        public async Task TheClientsOwnCheck_ServesARunOfAnApprovedCardOnlyWhenANoticeNamedIt_AndWaitsForOneThatIsLate()
        {
            var (_, pipe, _, _) = Start();
            var client = await ConnectAsync(pipe);
            using var vault = new SampleVault("orders/4711", Enumerable.Range(1, 16).Select(i => (byte)i).ToArray());
            client.KeyVault = vault.ResolveAsync;
            await client.RegisterKeyProviderAsync(ownRunsOnly: true);
            client.ApprovedRunWait = TimeSpan.FromMilliseconds(100);

            Task<ResolveKeyResponse> Ask(string runId, string source) =>
                client.ResolveKey(new ResolveKeyRequest { Reference = "orders/4711", RunId = runId, Source = source }, CancellationToken.None);

            var noNotice = await Ask("w-1", RunSources.AutoMode);
            client.CardPresentationEnded(new CardPresentationEndedEvent { PresenceId = "p-1", Reason = PresentationEndReasons.Approved, RunId = "w-1" });
            var named = await Ask("w-1", RunSources.AutoMode);
            var namedButNotOfTheMode = await Ask("w-1", RunSources.Gui);
            client.CardPresentationEnded(new CardPresentationEndedEvent { PresenceId = "p-2", Reason = PresentationEndReasons.Declined, RunId = "w-2" });
            var noRunWasAdmitted = await Ask("w-2", RunSources.AutoMode);

            // The check on its own (the one the guide tells an author to make): the run of an approved card, and nothing else.
            var checkNamed = client.StartedByThisClient(new ResolveKeyRequest { Source = RunSources.AutoMode, RunId = "w-1" });
            var checkOther = client.StartedByThisClient(new ResolveKeyRequest { Source = RunSources.AutoMode, RunId = "w-9" });
            var checkWrongSource = client.StartedByThisClient(new ResolveKeyRequest { Source = RunSources.Gui, RunId = "w-1" });
            var checkWithoutRun = client.StartedByThisClient(new ResolveKeyRequest { Source = RunSources.AutoMode });

            // The notice that is late: the request waits for it and is served when it comes.
            client.ApprovedRunWait = TimeSpan.FromSeconds(5);
            var late = Ask("w-3", RunSources.AutoMode);
            await Task.Delay(150);
            client.CardPresentationEnded(new CardPresentationEndedEvent { PresenceId = "p-3", Reason = PresentationEndReasons.Approved, RunId = "w-3" });
            var lateAnswer = await late.WaitAsync(TimeSpan.FromSeconds(5));

            // Only the latest runs are remembered: a station that goes on for days does not collect every run it ever approved.
            client.ApprovedRunWait = TimeSpan.FromMilliseconds(100);

            for (var i = 0; i < 70; i++)
            {
                client.CardPresentationEnded(new CardPresentationEndedEvent { PresenceId = "q-" + i, Reason = PresentationEndReasons.Approved, RunId = "x-" + i });
            }

            var forgotten = await Ask("x-0", RunSources.AutoMode);
            var remembered = await Ask("x-69", RunSources.AutoMode);

            Assert.Equal(KeyResolveResults.NotAuthorized, noNotice.Result);
            Assert.Equal(KeyResolveResults.Resolved, named.Result);
            Assert.Equal(KeyResolveResults.NotAuthorized, namedButNotOfTheMode.Result);
            Assert.Equal(KeyResolveResults.NotAuthorized, noRunWasAdmitted.Result);
            Assert.True(checkNamed);
            Assert.False(checkOther);
            Assert.False(checkWrongSource);
            Assert.False(checkWithoutRun);
            Assert.Equal(KeyResolveResults.Resolved, lateAnswer.Result);
            Assert.Equal(KeyResolveResults.NotAuthorized, forgotten.Result);
            Assert.Equal(KeyResolveResults.Resolved, remembered.Result);
            Assert.Equal(3, vault.Served);
        }

        [Fact]
        public async Task TheClientsOwnCheck_ServesOnlyWhatItStartedOverTheBridge_AndRefusesARequestWithoutContext()
        {
            var (_, pipe, _, _) = Start();
            var client = await ConnectAsync(pipe);
            var key = Enumerable.Range(1, 16).Select(i => (byte)i).ToArray();
            using var vault = new SampleVault("orders/4711", key);
            client.KeyVault = vault.ResolveAsync;
            await client.RegisterKeyProviderAsync(ownRunsOnly: true);
            await client.RunOnceAsync("job-1", NoVariables);

            Task<ResolveKeyResponse> Ask(string? requestId, string source) =>
                client.ResolveKey(new ResolveKeyRequest { Reference = "orders/4711", RequestId = requestId, Source = source }, CancellationToken.None);

            var own = await Ask("job-1", RunSources.Bridge);
            var anotherRequest = await Ask("job-2", RunSources.Bridge);
            var sameIdButNotAClientsRun = await Ask("job-1", RunSources.Gui);
            var withoutContext = await client.ResolveKey(new ResolveKeyRequest { Reference = "orders/4711" }, CancellationToken.None);

            Assert.Equal(KeyResolveResults.Resolved, own.Result);
            Assert.Equal(KeyResolveResults.NotAuthorized, anotherRequest.Result);
            Assert.Equal(KeyResolveResults.NotAuthorized, sameIdButNotAClientsRun.Result);
            Assert.Equal(KeyResolveResults.NotAuthorized, withoutContext.Result);
            Assert.Equal(1, vault.Served);
        }

        [Fact]
        public async Task WithoutTheRestriction_TheClientServesAsBefore_AndTheFlagIsOff()
        {
            var (_, pipe, _, _) = Start();
            var client = await ConnectAsync(pipe);
            using var vault = new SampleVault("orders/4711", new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 });
            client.KeyVault = vault.ResolveAsync;

            var registered = await client.RegisterKeyProviderAsync();
            var answer = await client.ResolveKey(new ResolveKeyRequest { Reference = "orders/4711" }, CancellationToken.None);

            Assert.False(registered.OwnRunsOnly);
            Assert.False(client.ServeOwnRunsOnly);
            Assert.Equal(KeyResolveResults.Resolved, answer.Result);
        }

        private static SecureKeyRequest RequestOfRun(string reference, string runId) => new SecureKeyRequest
        {
            SlotId = "desfire.app.current",
            Reference = reference,
            Algorithm = SecureKeyAlgorithm.Aes128,
            Usage = SecureKeyUsage.Authenticate,
            RunId = runId,
        };

        // ── The reference flow: one card, one approval, one run ──

        private static ReaderSnapshot Reader(string state, params (string Uid, bool Primary)[] cards) =>
            new ReaderSnapshot(
                "Reader 1",
                state == "ready" ? ReaderScanState.Ready : ReaderScanState.Busy,
                cards.Select(card => new CredentialInfo(card.Uid, 7, "MIFARE DESFire", card.Primary)).ToList());

        private static Task<bool> Yes(ApprovedCard card, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(card);
            token.ThrowIfCancellationRequested();

            return Task.FromResult(true);
        }

        [Fact]
        public async Task AnApprovedCard_IsRunOnceWithItsUidInTheVariable_AndConfirmedByWhatTheResultSays()
        {
            var (_, pipe, host, _) = Start();
            host.ProjectHost.Revision = "rev-0";
            host.ReaderHost.ScriptScan(Reader("ready", ("04AABB", true)));
            host.ExecutionHost.Result = FakeResults.Success(2) with { CardUid = "04AABB" };
            var client = await ConnectAsync(pipe);
            var asked = new List<ApprovedCard>();

            var outcome = await client.RunApprovedCardAsync(
                "job-7",
                "$EXPECTEDUID",
                (card, _) =>
                {
                    asked.Add(card);

                    return Task.FromResult(true);
                },
                new Dictionary<string, string> { ["$JOBNUMBER"] = "J-7" },
                expectProjectRevision: "rev-0");

            // The program is asked once, about the card that is on the reader, before anything is started.
            Assert.Equal(new ApprovedCard("04AABB", 7, "MIFARE DESFire"), Assert.Single(asked));
            var started = Assert.Single(host.ExecutionHost.Requests);
            Assert.Equal("04AABB", started.Variables["$EXPECTEDUID"]);
            Assert.Equal("J-7", started.Variables["$JOBNUMBER"]);
            Assert.Equal(ApprovedRunKind.Ran, outcome.Kind);
            Assert.Equal("04AABB", outcome.ApprovedUid);
            Assert.True(outcome.CardUidMatches);
            Assert.True(outcome.IsConfirmed);
            Assert.Equal("rev-0", outcome.Run!.Result!.ProjectRevision);
        }

        [Fact]
        public async Task ASeriesOfCards_EachConfirmed_EvenThoughTheRunsChangeSettingsOfTheirOwnTasks()
        {
            var (_, pipe, host, _) = Start();
            host.ProjectHost.Revision = "rev-0";

            // Each run leaves the project in another state (the tasks write some of their own settings), as a DESFire write does.
            var runs = 0;
            host.ExecutionHost.OnExecute = _ => host.ProjectHost.Revision = "rev-" + (++runs);
            host.ReaderHost.ScriptScan(Reader("ready", ("04AABB", true)));
            host.ReaderHost.ScriptScan(Reader("ready", ("04CCDD", true)));
            var client = await ConnectAsync(pipe);

            host.ExecutionHost.Result = FakeResults.Success(2) with { CardUid = "04AABB" };
            var first = await client.RunApprovedCardAsync("job-7-card-1", "$EXPECTEDUID", Yes, expectProjectRevision: "rev-0");
            host.ExecutionHost.Result = FakeResults.Success(2) with { CardUid = "04CCDD" };
            var second = await client.RunApprovedCardAsync("job-7-card-2", "$EXPECTEDUID", Yes, expectProjectRevision: "rev-0");

            // The second run started with the state the first one left, which RFiDGear knows as the approved one: it ran, and it is confirmed.
            Assert.True(first.IsConfirmed);
            Assert.True(second.IsConfirmed);
            Assert.Equal("rev-0", first.Run!.Result!.ProjectRevision);
            Assert.Equal("rev-1", second.Run!.Result!.ProjectRevision);
            Assert.Equal(2, host.ExecutionHost.Requests.Count);
        }

        [Fact]
        public async Task ACardTheProgramDoesNotApprove_StartsNothing()
        {
            var (_, pipe, host, _) = Start();
            host.ReaderHost.ScriptScan(Reader("ready", ("04AABB", true)));
            var client = await ConnectAsync(pipe);

            var outcome = await client.RunApprovedCardAsync("job-7", "$EXPECTEDUID", (_, _) => Task.FromResult(false));

            Assert.Equal(ApprovedRunKind.Declined, outcome.Kind);
            Assert.Equal("04AABB", outcome.ApprovedUid);
            Assert.Null(outcome.Run);
            Assert.False(outcome.IsConfirmed);
            Assert.Empty(host.ExecutionHost.Requests);
        }

        [Theory]
        [InlineData("none", ApprovedRunKind.NoCard)]
        [InlineData("two", ApprovedRunKind.AmbiguousCard)]
        [InlineData("notPrimary", ApprovedRunKind.AmbiguousCard)]
        [InlineData("busy", ApprovedRunKind.ReaderNotReady)]
        public async Task AReaderThatDoesNotShowExactlyOneCard_IsNeverApprovedAndNeverRun(string scenario, ApprovedRunKind expected)
        {
            var (_, pipe, host, _) = Start();
            host.ReaderHost.ScriptScan(scenario switch
            {
                "none" => Reader("ready"),
                "two" => Reader("ready", ("04AABB", true), ("04CCDD", false)),
                "notPrimary" => Reader("ready", ("04AABB", false)),
                _ => Reader("busy"),
            });
            var client = await ConnectAsync(pipe);
            var calls = 0;

            var outcome = await client.RunApprovedCardAsync(
                "job-7",
                "$EXPECTEDUID",
                (_, _) =>
                {
                    calls++;

                    return Task.FromResult(true);
                });

            Assert.Equal(expected, outcome.Kind);
            Assert.Equal(0, calls);
            Assert.Null(outcome.ApprovedUid);
            Assert.Empty(host.ExecutionHost.Requests);
        }

        [Fact]
        public async Task AReaderThatCannotBeLookedAt_IsNotReady_AndNothingIsStarted()
        {
            var (_, pipe, host, _) = Start();
            host.ReaderHost.ScriptScanFailure(new InvalidOperationException("the reader does not answer"));
            var client = await ConnectAsync(pipe);

            var outcome = await client.RunApprovedCardAsync("job-7", "$EXPECTEDUID", Yes);

            Assert.Equal(ApprovedRunKind.ReaderNotReady, outcome.Kind);
            Assert.Empty(host.ExecutionHost.Requests);
        }

        [Fact]
        public async Task ACardOtherThanTheApprovedOne_IsNotConfirmed_EvenWhenTheRunSucceeded()
        {
            var (_, pipe, host, _) = Start();
            host.ReaderHost.ScriptScan(Reader("ready", ("04AABB", true)));

            // The card was swapped after the approval: the run read another one.
            host.ExecutionHost.Result = FakeResults.Success(2) with { CardUid = "04FFFF" };
            var client = await ConnectAsync(pipe);

            var outcome = await client.RunApprovedCardAsync("job-7", "$EXPECTEDUID", Yes);

            Assert.Equal(ApprovedRunKind.Ran, outcome.Kind);
            Assert.Equal(RunStatuses.Success, outcome.Run!.Result!.Status);
            Assert.False(outcome.CardUidMatches);
            Assert.False(outcome.IsConfirmed);
        }

        [Fact]
        public async Task ARunThatFailed_IsNotConfirmed_EvenWhenItReadTheApprovedCard()
        {
            var (_, pipe, host, _) = Start();
            host.ReaderHost.ScriptScan(Reader("ready", ("04AABB", true)));
            host.ExecutionHost.Result = FakeResults.Success(2) with { CardUid = "04AABB", Outcome = ExecutionOutcome.Failure };
            var client = await ConnectAsync(pipe);

            var outcome = await client.RunApprovedCardAsync("job-7", "$EXPECTEDUID", Yes);

            Assert.True(outcome.CardUidMatches);
            Assert.Equal(RunStatuses.Failure, outcome.Run!.Result!.Status);
            Assert.False(outcome.IsConfirmed);
        }

        [Fact]
        public async Task AProjectThatChangedAfterTheApproval_IsRefusedBeforeAnythingIsTouched_AndNotConfirmed()
        {
            var (_, pipe, host, _) = Start();
            host.ProjectHost.Revision = "rev-9";
            host.ReaderHost.ScriptScan(Reader("ready", ("04AABB", true)));
            var client = await ConnectAsync(pipe);

            var outcome = await client.RunApprovedCardAsync("job-7", "$EXPECTEDUID", Yes, expectProjectRevision: "rev-0");

            Assert.Equal(ApprovedRunKind.Ran, outcome.Kind);
            Assert.Equal(RunOutcomeKind.NotStarted, outcome.Run!.Kind);
            Assert.False(outcome.IsConfirmed);
            Assert.Empty(host.ExecutionHost.Requests);
        }

        [Theory]
        [InlineData("EXPECTEDUID")]
        [InlineData("$")]
        [InlineData("$A B")]
        [InlineData("")]
        public async Task AVariableThatIsNoVariable_IsRefusedBeforeTheReaderIsLookedAt(string variable)
        {
            var (_, pipe, host, _) = Start();
            var client = await ConnectAsync(pipe);

            await Assert.ThrowsAsync<ArgumentException>(() => client.RunApprovedCardAsync("job-7", variable, Yes));
            Assert.Equal(0, host.ReaderHost.ScanCount);
        }

        [Fact]
        public async Task AnApprovalThatComesWhileTheCallerIsCancelling_StartsNoRun()
        {
            var (_, pipe, host, _) = Start();
            host.ReaderHost.ScriptScan(Reader("ready", ("04AABB", true)));
            var client = await ConnectAsync(pipe);
            using var cancelling = new CancellationTokenSource();

            // A prompt that does not look at the token: Ctrl+C cancelled, and the operator typed yes afterwards.
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.RunApprovedCardAsync(
                "job-7",
                "$EXPECTEDUID",
                (_, _) =>
                {
                    cancelling.Cancel();

                    return Task.FromResult(true);
                },
                cancellationToken: cancelling.Token));

            // A request that was sent is admitted by the bridge a moment later: give it the time, so that a run that was started shows.
            await Task.Delay(400);

            Assert.Empty(host.ExecutionHost.Requests);
        }

        [Theory]
        [InlineData("$EXPECTEDUID")]
        [InlineData("$expecteduid")]
        [InlineData("$ExpectedUid")]
        public async Task TheVariableOfTheApprovedUid_IsNotSetByTheCaller_UnderAnyCaseOfItsName(string callersName)
        {
            var (_, pipe, host, _) = Start();
            var client = await ConnectAsync(pipe);

            await Assert.ThrowsAsync<ArgumentException>(() => client.RunApprovedCardAsync(
                "job-7",
                "$EXPECTEDUID",
                Yes,
                new Dictionary<string, string> { [callersName] = "04FFFF" }));
            Assert.Equal(0, host.ReaderHost.ScanCount);
        }

        [Fact]
        public async Task TheVariableOfTheApprovedUid_IsNotSetByTheCaller_NobodyElseMayWriteIt()
        {
            var (_, pipe, host, _) = Start();
            var client = await ConnectAsync(pipe);

            await Assert.ThrowsAsync<ArgumentException>(() => client.RunApprovedCardAsync(
                "job-7",
                "$EXPECTEDUID",
                Yes,
                new Dictionary<string, string> { ["$EXPECTEDUID"] = "04FFFF" }));
            Assert.Equal(0, host.ReaderHost.ScanCount);
        }

        [Fact]
        public void TheVerificationText_SaysWhatWasComparedAndWhatDiffered_AndLeavesOutWhatWasNot()
        {
            var sample = (TaskOutcomeDto)BridgeSamples.All["TaskOutcomeDto"];

            var lines = VerificationText.Describe(sample.VerificationDetails!);

            Assert.Equal(
                new[]
                {
                    "desfire.fileSettings (application 3, file 1): DOES NOT MATCH, all of the compared settings, 2 compared, 1 not",
                    "fileType: match, expected 0, read 0",
                    "fileSize: mismatch, expected 16, read 8 (exact)",
                },
                lines);
        }

        [Fact]
        public void TheVerificationText_CopesWithAKindItDoesNotKnow_ByItsChecksAlone()
        {
            var unknown = new VerificationDetailsDto
            {
                SchemaVersion = 7,
                Kind = "something.else",
                Checks = new[] { new VerificationCheckDto { Name = "novel", Compared = true, Result = "a-token-of-a-later-version", Expected = 1 } },
            };

            var lines = VerificationText.Describe(unknown);

            Assert.Equal(new[] { "something.else: no verdict, 1 compared, 0 not", "novel: a-token-of-a-later-version, expected 1" }, lines);
        }

        [Fact]
        public async Task TheSampleVault_ServesOneReference_RefusesTheRest_AndForgetsTheKeyWhenDisposed()
        {
            var key = new byte[] { 1, 2, 3, 4, 5, 6 };
            var vault = new SampleVault("orders/4711", key);

            var served = await vault.ResolveAsync(new ResolveKeyRequest { Reference = "orders/4711" }, CancellationToken.None);
            var other = await vault.ResolveAsync(new ResolveKeyRequest { Reference = "orders/4712" }, CancellationToken.None);
            var differentCase = await vault.ResolveAsync(new ResolveKeyRequest { Reference = "ORDERS/4711" }, CancellationToken.None);
            vault.Dispose();
            vault.Dispose();
            var afterwards = await vault.ResolveAsync(new ResolveKeyRequest { Reference = "orders/4711" }, CancellationToken.None);

            Assert.Equal(KeyResolveResults.Resolved, served.Result);
            Assert.Equal(key, Convert.FromBase64String(served.Key!));
            Assert.Equal(KeyResolveResults.NotAuthorized, other.Result);
            Assert.Null(other.Key);
            Assert.Equal(KeyResolveResults.NotAuthorized, differentCase.Result);
            Assert.Equal(KeyResolveResults.NotAuthorized, afterwards.Result);
            Assert.Null(afterwards.Key);
            Assert.Equal(1, vault.Served);
            await Assert.ThrowsAsync<OperationCanceledException>(() => new SampleVault("r", key).ResolveAsync(new ResolveKeyRequest { Reference = "r" }, new CancellationToken(true)));
        }

        [Fact]
        public async Task AVaultThatIsDisposedWhileItIsAsked_GivesTheWholeKeyOrNothing_NeverAKeyThatIsBeingErased()
        {
            var key = Enumerable.Range(1, 16).Select(i => (byte)(i * 7)).ToArray();

            for (var round = 0; round < 200; round++)
            {
                var vault = new SampleVault("r", key);
                var gate = new ManualResetEventSlim(false);
                var asking = Task.Run(() =>
                {
                    gate.Wait();

                    return vault.ResolveAsync(new ResolveKeyRequest { Reference = "r" }, CancellationToken.None);
                });
                var disposing = Task.Run(() =>
                {
                    gate.Wait();
                    vault.Dispose();
                });

                gate.Set();
                var answer = await asking;
                await disposing;

                if (answer.Result == KeyResolveResults.Resolved)
                {
                    Assert.Equal(key, Convert.FromBase64String(answer.Key!));
                }
                else
                {
                    Assert.Equal(KeyResolveResults.NotAuthorized, answer.Result);
                    Assert.Null(answer.Key);
                }
            }
        }

        [Fact]
        public void TheSampleVault_RefusesWhatIsNoKeyOrNoReference()
        {
            Assert.Throws<ArgumentException>(() => new SampleVault(string.Empty, new byte[] { 1 }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SampleVault("r", ReadOnlySpan<byte>.Empty));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SampleVault("r", new byte[BridgeProtocol.MaxKeyBytes + 1]));
            using var longest = new SampleVault("r", new byte[BridgeProtocol.MaxKeyBytes]);
            Assert.NotNull(longest);
        }

        [Fact]
        public async Task WithoutAVault_EveryRequestIsRefused_AndRegisteringWithoutTheCapabilityIsSaidSo()
        {
            var (_, pipe, _, keys) = Start();
            var registry = SecureKeyRegistry.Create(
                new[] { new SecureKeyProviderRegistration(BridgeKeyProvider.ProviderId, BridgeKeyProvider.DisplayName, keys) },
                Array.Empty<Lazy<ISecureKeyProvider, ISecureKeyProviderMetadata>>());
            var client = await ConnectAsync(pipe);
            await client.RegisterKeyProviderAsync();

            using var answer = await registry.ResolveAsync(BridgeKeyProvider.ProviderId, Request("orders/4711"), CancellationToken.None);

            Assert.Equal(SecureKeyFailureReason.NotAuthorized, answer.FailureReason);

            var withoutKeys = new FakeExtensionHost();
            var name = "RFiDGear.Bridge.test-" + Guid.NewGuid().ToString("N");
            var plain = new ExternalControlService(withoutKeys, RunAtOnce, () => false, "2.0.10", name);
            _disposables.Add(plain);
            Assert.True(plain.Start());
            plain.MarkStartupComplete();
            var other = await ConnectAsync(name);

            await Assert.ThrowsAsync<NotSupportedException>(() => other.RegisterKeyProviderAsync());
        }

        // ── The loop of a program that decides about every card (autoMode.approval) ──

        // Starts the loop against the real bridge and waits until the mode is on; the test plays the machine against the approval the
        // host was given (what the main window does when a card is put on the reader).
        private async Task<(BridgeSampleClient Client, FakeExtensionHost Host, Task<IReadOnlyList<CardOutcome>> Loop, CancellationTokenSource Stop, List<CardOutcome> Seen)> StartLoopAsync(
            Func<CardPresentedEvent, CancellationToken, Task<CardDecision>> decide,
            string? revision = "rev-1")
        {
            var (_, pipe, host, _) = Start();
            host.ProjectHost.Revision = revision;
            var client = await ConnectAsync(pipe);
            var stop = new CancellationTokenSource();
            _disposables.Add(stop);
            var seen = new List<CardOutcome>();
            var loop = new ApprovalModeLoop(client).RunAsync(decide, onOutcome: outcome => { lock (seen) { seen.Add(outcome); } }, cancellationToken: stop.Token);
            await WaitUntilAsync(() => host.AutoModeHost.Approval != null);

            return (client, host, loop, stop, seen);
        }

        private static Task<AutoModeApproval?> Present(FakeExtensionHost host, string presenceId, string uid) =>
            Task.Run(() => host.AutoModeHost.Approval!.RequestAsync(new AutoModePresence(presenceId, uid), CancellationToken.None));

        private static async Task<IReadOnlyList<CardOutcome>> EndTheLoopAsync(Task<IReadOnlyList<CardOutcome>> loop, CancellationTokenSource stop, List<CardOutcome> seen, int outcomes)
        {
            await WaitUntilAsync(() => { lock (seen) { return seen.Count >= outcomes; } });
            stop.Cancel();

            return await loop.WaitAsync(TimeSpan.FromSeconds(5));
        }

        [Fact]
        public async Task TheLoop_ApprovesACardWithItsVariablesAndTheStateItWasShownWith_AndTellsHowItEnded()
        {
            var (_, host, loop, stop, seen) = await StartLoopAsync((shown, _) => Task.FromResult(CardDecision.ApproveWith(new Dictionary<string, string> { ["$EXPECTEDUID"] = shown.Uid })));

            var approval = await Present(host, "p-1", "04AA").WaitAsync(TimeSpan.FromSeconds(5));
            approval!.NotifyRunAdmitted("run-1");
            var outcomes = await EndTheLoopAsync(loop, stop, seen, 1);

            Assert.Equal("04AA", approval.Variables["$EXPECTEDUID"]);
            Assert.Equal("rev-1", approval.ExpectProjectRevision);
            Assert.Equal(new CardOutcome("p-1", "04AA", "approved", PresentationEndReasons.Approved, "run-1"), Assert.Single(outcomes));

            // The mode is stopped when the loop ends: nothing is left running that nobody decides about.
            Assert.False(host.AutoModeHost.IsAutoModeEnabled);
        }

        [Fact]
        public async Task TheLoop_DeclinesACardTheProgramDoesNotApprove_AndANotApprovedCardRunsNothing()
        {
            var (_, host, loop, stop, seen) = await StartLoopAsync((_, _) => Task.FromResult(CardDecision.Decline));

            var approval = await Present(host, "p-1", "04AA").WaitAsync(TimeSpan.FromSeconds(5));
            var outcomes = await EndTheLoopAsync(loop, stop, seen, 1);

            Assert.Null(approval);
            Assert.Equal(("declined", PresentationEndReasons.Declined), (outcomes[0].Answer, outcomes[0].Reason));
        }

        [Fact]
        public async Task TheLoop_DeclinesACardWhenTheDecisionThrows_ThatIsNoApproval()
        {
            var (_, host, loop, stop, seen) = await StartLoopAsync((_, _) => throw new InvalidOperationException("The list of written cards could not be read."));

            var approval = await Present(host, "p-1", "04AA").WaitAsync(TimeSpan.FromSeconds(5));
            var outcomes = await EndTheLoopAsync(loop, stop, seen, 1);

            Assert.Null(approval);
            Assert.Equal("declined", outcomes[0].Answer);
        }

        [Fact]
        public async Task TheLoop_DeclinesACardWhoseProjectStateCannotBeHeldTo_AndApprovesItWhenTheProgramDoesNotAskForThat()
        {
            var (_, host, loop, stop, seen) = await StartLoopAsync(
                (shown, _) => Task.FromResult(shown.Uid == "04AA" ? CardDecision.ApproveWith() : CardDecision.ApproveWith(pinRevision: false)),
                revision: null);

            var pinned = await Present(host, "p-1", "04AA").WaitAsync(TimeSpan.FromSeconds(5));
            var notPinned = await Present(host, "p-2", "04BB").WaitAsync(TimeSpan.FromSeconds(5));
            notPinned!.NotifyRunAdmitted("run-2");
            var outcomes = await EndTheLoopAsync(loop, stop, seen, 2);

            // The state of the project cannot be told, so an approval that insists on it is no approval; one that does not is taken as it is.
            Assert.Null(pinned);
            Assert.Null(notPinned.ExpectProjectRevision);
            Assert.Equal(new[] { "declined", "approved" }, outcomes.Select(o => o.Answer));
        }

        [Fact]
        public async Task TheLoop_DeclinesACardWhoseApprovalRFiDGearRefusesForWhatItSays_InsteadOfLeavingItToExpire()
        {
            // A report target that is no full path: the host refuses the approval, and the card is still waited for.
            var (_, host, loop, stop, seen) = await StartLoopAsync((_, _) => Task.FromResult(CardDecision.ApproveWith(new Dictionary<string, string> { ["REPORTTARGETPATH"] = "report.pdf" })));

            var approval = await Present(host, "p-1", "04AA").WaitAsync(TimeSpan.FromSeconds(5));
            var outcomes = await EndTheLoopAsync(loop, stop, seen, 1);

            Assert.Null(approval);
            Assert.Equal(("declined", PresentationEndReasons.Declined), (outcomes[0].Answer, outcomes[0].Reason));
        }

        [Fact]
        public async Task TheLoop_ToleratesALateAnswer_AndGoesOnWithTheNextCard()
        {
            CardPresentedEvent? first = null;
            BridgeSampleClient? clientOfTheTest = null;
            var (client, host, loop, stop, seen) = await StartLoopAsync(async (shown, _) =>
            {
                if (first == null)
                {
                    // The first card is given up (declined from elsewhere) while the program is still deciding; its answer comes late.
                    first = shown;
                    await clientOfTheTest!.Service.DeclineCard(new DeclineCardRequest { PresenceId = shown.PresenceId });
                }

                return CardDecision.ApproveWith();
            });
            clientOfTheTest = client;

            var late = await Present(host, "p-1", "04AA").WaitAsync(TimeSpan.FromSeconds(5));
            var next = await Present(host, "p-2", "04BB").WaitAsync(TimeSpan.FromSeconds(5));
            next!.NotifyRunAdmitted("run-2");
            var outcomes = await EndTheLoopAsync(loop, stop, seen, 2);

            Assert.Null(late);
            Assert.Equal(("p-1", "late", PresentationEndReasons.Declined), (outcomes[0].PresenceId, outcomes[0].Answer, outcomes[0].Reason));
            Assert.Equal(("p-2", "approved", PresentationEndReasons.Approved), (outcomes[1].PresenceId, outcomes[1].Answer, outcomes[1].Reason));
        }

        [Fact]
        public async Task TheLoop_EndsWhenRFiDGearStopsTheMode_AndWhenTheConnectionEnds()
        {
            var (_, host, loop, _, _) = await StartLoopAsync((_, _) => Task.FromResult(CardDecision.ApproveWith()));

            host.AutoModeHost.StopFor(AutoModeStopReason.RunAborted);
            var endedByTheMode = await loop.WaitAsync(TimeSpan.FromSeconds(5));

            var (client, _, secondLoop, _, _) = await StartLoopAsync((_, _) => Task.FromResult(CardDecision.ApproveWith()));
            await client.DisposeAsync();
            var endedByTheConnection = await secondLoop.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Empty(endedByTheMode);
            Assert.Empty(endedByTheConnection);
        }

        private static SecureKeyRequest Request(string reference) => new SecureKeyRequest
        {
            SlotId = "desfire.app.current",
            Reference = reference,
            Algorithm = SecureKeyAlgorithm.Aes128,
            Usage = SecureKeyUsage.Authenticate,
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
    }
}
