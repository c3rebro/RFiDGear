#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using RFiDGear.Bridge;
using RFiDGear.Bridge.SampleClient;
using RFiDGear.Contracts;
using RFiDGear.ExtensionSdk;
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

        private (ExternalControlService Service, string Pipe, FakeExtensionHost Host, BridgeKeyProvider Keys) Start(Func<Action, Task>? dispatch = null)
        {
            var name = "RFiDGear.Bridge.test-" + Guid.NewGuid().ToString("N");
            var host = new FakeExtensionHost();
            host.ProjectHost.CurrentPath = @"C:\projects\one.rfPrj";
            host.ProjectHost.TaskCount = 2;
            var keys = new BridgeKeyProvider();
            var service = new ExternalControlService(host, dispatch ?? RunAtOnce, () => false, "2.0.10", name, keyProvider: keys);
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
