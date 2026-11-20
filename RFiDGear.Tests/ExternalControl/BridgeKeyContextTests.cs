#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using RFiDGear.Bridge;
using RFiDGear.Contracts;
using RFiDGear.ExtensionSdk;
using RFiDGear.Services.ExternalControl;
using RFiDGear.Services.SecureKeys;
using RFiDGear.Tests.CardCheckAssistant;

using Xunit;

namespace RFiDGear.Tests.ExternalControl
{
    /// <summary>
    /// What the supplier of keys is told about the run a key is for, and the restriction it can ask for: only the runs it started.
    /// The rules that matter: a run of the client says so with the id of its own request, anything else never does; a client that
    /// asked for its own runs only is not called for the others, and the host answers "not authorized" for it; and a client can
    /// see whether the restriction is in force.
    /// </summary>
    public sealed class BridgeKeyContextTests : IDisposable
    {
        private const string Reference = "vault/order-4711";
        private static readonly byte[] KeyBytes = Enumerable.Range(0x41, 16).Select(i => (byte)i).ToArray();

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

        private sealed record Rig(ExternalControlService Service, string PipeName, FakeExtensionHost Host, SecureKeyRegistry Registry, WindowRunMarker Window);

        private Rig Start(bool withProvider = true)
        {
            var name = "RFiDGear.Bridge.test-" + Guid.NewGuid().ToString("N");
            var provider = new BridgeKeyProvider();
            var host = new FakeExtensionHost();
            host.ProjectHost.CurrentPath = @"C:\projects\one.rfPrj";
            host.ProjectHost.TaskCount = 2;
            host.ProjectHost.Revision = "rev-0";
            var window = new WindowRunMarker();
            var service = new ExternalControlService(
                host,
                RunAtOnce,
                () => false,
                "2.0.10",
                name,
                keyProvider: withProvider ? provider : null,
                windowRunSource: window.SourceOf);
            _disposables.Add(service);
            Assert.True(service.Start());
            service.MarkStartupComplete();
            var registry = SecureKeyRegistry.Create(
                new[] { new SecureKeyProviderRegistration(BridgeKeyProvider.ProviderId, BridgeKeyProvider.DisplayName, provider) },
                Array.Empty<Lazy<ISecureKeyProvider, ISecureKeyProviderMetadata>>());

            return new Rig(service, name, host, registry, window);
        }

        private async Task<BridgeTestClient> ConnectAsync(Rig rig, bool? ownRunsOnly)
        {
            var client = await BridgeTestClient.ConnectAsync(rig.PipeName, 5000);
            _disposables.Add(client);
            await client.HelloAsync();
            client.KeyHandler = (_, _) => Task.FromResult(new ResolveKeyResponse { Result = KeyResolveResults.Resolved, Key = Convert.ToBase64String(KeyBytes) });

            if (ownRunsOnly != null)
            {
                await client.Service.RegisterKeyProvider(new RegisterKeyProviderRequest { OwnRunsOnly = ownRunsOnly.Value });
            }

            return client;
        }

        private static Task<SecureKeyResolution> Ask(Rig rig, string? runId, bool interactive = false) =>
            rig.Registry.ResolveAsync(
                BridgeKeyProvider.ProviderId,
                new SecureKeyRequest
                {
                    SlotId = "desfire.app.current",
                    Reference = Reference,
                    Algorithm = SecureKeyAlgorithm.Aes128,
                    Usage = SecureKeyUsage.Authenticate,
                    RunId = runId,
                    TaskIndex = "4",
                    CardUid = "04A1B2C3D4E5F6",
                    Interactive = interactive,
                },
                CancellationToken.None);

        // A run of the client that is held in the host until the test lets it go; its id is the one the host chose.
        private static async Task<string> StartHeldRunAsync(Rig rig, BridgeTestClient client, string requestId)
        {
            rig.Host.ExecutionHost.RunGate = new TaskCompletionSource<bool>();
            await client.Service.StartRun(new StartRunRequest { RequestId = requestId });

            return rig.Host.ExecutionHost.RunIds.Last();
        }

        [Fact]
        public void ARequestOfAnOlderHost_ThatSendsNoContext_ReadsAsAnotherRun_NeverAsTheClientsOwn()
        {
            const string older = """{"slotId":"s","reference":"r","algorithm":"aes128","usage":"authenticate","taskIndex":"1","interactive":false}""";

            var request = System.Text.Json.JsonSerializer.Deserialize<ResolveKeyRequest>(older, BridgeJson.CreateClientOptions());

            Assert.NotNull(request);
            Assert.Equal(RunSources.Other, request!.Source);
            Assert.Null(request.RequestId);
            Assert.Null(request.ProjectRevision);
        }

        [Fact]
        public async Task TheCapability_IsOfferedWithAProvider_AndWithoutOneItIsNot()
        {
            var with = Start();
            var without = Start(withProvider: false);
            var withClient = await BridgeTestClient.ConnectAsync(with.PipeName, 5000);
            var withoutClient = await BridgeTestClient.ConnectAsync(without.PipeName, 5000);
            _disposables.Add(withClient);
            _disposables.Add(withoutClient);

            var offered = await withClient.HelloAsync();
            var notOffered = await withoutClient.HelloAsync();

            Assert.Contains(BridgeCapabilities.KeysContext, offered.Capabilities);
            Assert.DoesNotContain(BridgeCapabilities.KeysContext, notOffered.Capabilities);
        }

        [Fact]
        public async Task Registering_SaysWhetherTheRestrictionIsInForce_AndTheLatestRegistrationCounts()
        {
            var rig = Start();
            var client = await ConnectAsync(rig, ownRunsOnly: null);

            var plain = await client.Service.RegisterKeyProvider(new RegisterKeyProviderRequest());
            var restricted = await client.Service.RegisterKeyProvider(new RegisterKeyProviderRequest { OwnRunsOnly = true });
            var held = await Ask(rig, "run-of-the-window");
            var lifted = await client.Service.RegisterKeyProvider(new RegisterKeyProviderRequest { OwnRunsOnly = false });
            var asked = await Ask(rig, "run-of-the-window");

            Assert.False(plain.OwnRunsOnly);
            Assert.True(restricted.OwnRunsOnly);
            Assert.Equal(SecureKeyFailureReason.NotAuthorized, held.FailureReason);
            Assert.False(lifted.OwnRunsOnly);
            Assert.True(asked.IsResolved);
        }

        [Fact]
        public async Task ARunTheClientStarted_CarriesItsOwnRequestId_ItsSourceAndTheRevisionItStartedWith()
        {
            var rig = Start();
            var client = await ConnectAsync(rig, ownRunsOnly: false);
            var runId = await StartHeldRunAsync(rig, client, "r-1");

            using var answer = await Ask(rig, runId);
            rig.Host.ProjectHost.Revision = "rev-1";
            using var later = await Ask(rig, runId);
            rig.Host.ExecutionHost.RunGate!.SetResult(true);

            Assert.True(answer.IsResolved);
            Assert.True(later.IsResolved);
            Assert.All(client.KeyRequests, asked =>
            {
                Assert.Equal("r-1", asked.RequestId);
                Assert.Equal(RunSources.Bridge, asked.Source);

                // The revision the run started with, not the one of the project at the moment of the request.
                Assert.Equal("rev-0", asked.ProjectRevision);
                Assert.Equal(runId, asked.RunId);
            });
        }

        [Theory]
        [InlineData(null)]
        [InlineData("a-run-nobody-started-over-the-bridge")]
        public async Task ARunOfSomebodyElse_IsNotTakenForTheClients_AndCarriesNoRequestId(string? runId)
        {
            var rig = Start();
            var client = await ConnectAsync(rig, ownRunsOnly: false);

            using var answer = await Ask(rig, runId);

            Assert.True(answer.IsResolved);
            var asked = Assert.Single(client.KeyRequests);
            Assert.Equal(RunSources.Other, asked.Source);
            Assert.Null(asked.RequestId);
            Assert.Null(asked.ProjectRevision);
        }

        [Fact]
        public async Task ARunOfTheWindow_SaysWhetherTheUserOrAutomaticModeStartedIt()
        {
            var rig = Start();
            var client = await ConnectAsync(rig, ownRunsOnly: false);

            rig.Window.Begin("w-1", RunSources.Gui);
            using var byHand = await Ask(rig, "w-1");
            rig.Window.End("w-1");
            rig.Window.Begin("w-2", RunSources.AutoMode);
            using var byTheMode = await Ask(rig, "w-2");

            Assert.Equal(new[] { RunSources.Gui, RunSources.AutoMode }, client.KeyRequests.Select(r => r.Source));
            Assert.All(client.KeyRequests, asked => Assert.Null(asked.RequestId));
        }

        [Fact]
        public async Task WithTheRestriction_OnlyTheClientsOwnRunsAreAsked_ForEveryOtherSourceTheHostAnswersAndTheClientIsNotCalled()
        {
            var rig = Start();
            var client = await ConnectAsync(rig, ownRunsOnly: true);
            var runId = await StartHeldRunAsync(rig, client, "r-1");
            rig.Window.Begin("w-1", RunSources.Gui);

            using var own = await Ask(rig, runId);
            var calledForOwn = client.KeyRequests.Count;
            using var byHand = await Ask(rig, "w-1");
            using var unknown = await Ask(rig, "an-extensions-run");
            using var aButton = await Ask(rig, null, interactive: true);
            rig.Host.ExecutionHost.RunGate!.SetResult(true);

            Assert.True(own.IsResolved);
            Assert.Equal(1, calledForOwn);
            Assert.All(new[] { byHand, unknown, aButton }, refused => Assert.Equal(SecureKeyFailureReason.NotAuthorized, refused.FailureReason));

            // Nothing about the refused runs reached the client: the host answered.
            Assert.Equal(1, client.KeyRequests.Count);
        }

        [Fact]
        public async Task AClientThatEndedItsRun_IsNoLongerAskedForIt_WithTheRestriction()
        {
            var rig = Start();
            var client = await ConnectAsync(rig, ownRunsOnly: true);
            var runId = await StartHeldRunAsync(rig, client, "r-1");
            rig.Host.ExecutionHost.RunGate!.SetResult(true);
            await client.Service.GetRunResult(new GetRunResultRequest { RequestId = "r-1", WaitMs = 5000 });

            using var late = await Ask(rig, runId);

            Assert.Equal(SecureKeyFailureReason.NotAuthorized, late.FailureReason);
            Assert.Empty(client.KeyRequests);
        }

        [Fact]
        public async Task WithoutTheRestriction_EveryRunIsAsked_ThatIsHowItWasBeforeTheFieldsExisted()
        {
            var rig = Start();
            var client = await ConnectAsync(rig, ownRunsOnly: false);
            rig.Window.Begin("w-1", RunSources.Gui);

            using var byHand = await Ask(rig, "w-1");
            using var unknown = await Ask(rig, "an-extensions-run");

            Assert.True(byHand.IsResolved);
            Assert.True(unknown.IsResolved);
            Assert.Equal(2, client.KeyRequests.Count);
        }
    }
}
