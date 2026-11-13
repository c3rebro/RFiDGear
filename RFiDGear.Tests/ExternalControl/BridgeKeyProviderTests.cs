#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using RFiDGear.Bridge;
using RFiDGear.Contracts;
using RFiDGear.ExtensionSdk;
using RFiDGear.Models;
using RFiDGear.Services.ExternalControl;
using RFiDGear.Services.SecureKeys;
using RFiDGear.Tests.SecureKeys;
using RFiDGear.Tests.CardCheckAssistant;

using StreamJsonRpc;

using Xunit;

namespace RFiDGear.Tests.ExternalControl
{
    /// <summary>
    /// The program that is connected to the bridge as a supplier of card keys: the provider that stands for it, and the whole way
    /// from a task that names a key by reference, through the registry and the pipe, to the vault of a client that is written like
    /// an external program. The rules that matter: no client, no key (and the task ends as it does for any missing provider);
    /// the client is asked only when it registered; a client that is slow, gone or wrong costs the key and nothing else; and
    /// neither a reference nor a key nor a text of the client reaches the log.
    /// </summary>
    public sealed class BridgeKeyProviderTests : IDisposable
    {
        // Text that must not reach a log, and bytes that must not either (as base64 and as hex).
        private const string Reference = "SENTINEL/ref-7f3a9c";
        private const string ExceptionText = "SENTINEL-EXCEPTION-TEXT-91c2";
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

        private static SecureKeyRequest Request(bool interactive = false) => new SecureKeyRequest
        {
            SlotId = "desfire.app.current",
            Reference = Reference,
            Algorithm = SecureKeyAlgorithm.Aes128,
            Usage = SecureKeyUsage.ChangeKeyNew,
            Scope = new SecureKeyScope { ApplicationId = 0x00ABCDEF, KeyNumber = 2 },
            RunId = "run-9",
            TaskIndex = "4",
            CardUid = "04A1B2C3D4E5F6",
            Interactive = interactive,
        };

        private static ResolveKeyResponse Answer(byte[] key) => new ResolveKeyResponse { Result = KeyResolveResults.Resolved, Key = Convert.ToBase64String(key) };

        private static ResolveKeyResponse Answer(string result) => new ResolveKeyResponse { Result = result };

        // ── The provider on its own, with a channel the test controls ──

        private sealed class FakeChannel : IBridgeKeyChannel
        {
            private readonly Func<ResolveKeyRequest, CancellationToken, Task<ResolveKeyResponse?>> _handler;
            private readonly List<ResolveKeyRequest> _requests = new List<ResolveKeyRequest>();

            internal FakeChannel(Func<ResolveKeyRequest, CancellationToken, Task<ResolveKeyResponse?>> handler)
            {
                _handler = handler;
            }

            internal IReadOnlyList<ResolveKeyRequest> Requests
            {
                get
                {
                    lock (_requests)
                    {
                        return _requests.ToList();
                    }
                }
            }

            public Task<ResolveKeyResponse?> RequestKeyAsync(ResolveKeyRequest request, CancellationToken cancellationToken)
            {
                lock (_requests)
                {
                    _requests.Add(request);
                }

                return _handler(request, cancellationToken);
            }
        }

        private static (BridgeKeyProvider Provider, FakeChannel Channel) Bound(Func<ResolveKeyRequest, CancellationToken, Task<ResolveKeyResponse?>> handler)
        {
            var provider = new BridgeKeyProvider();
            var channel = new FakeChannel(handler);
            provider.Attach(channel);

            return (provider, channel);
        }

        private static byte[] BytesOf(SecureKeyResolution resolution)
        {
            var lease = Assert.IsType<SecureKeyLease>(resolution.Key);

            return lease.Key.ToArray();
        }

        [Fact]
        public async Task WithoutAChannel_TheProviderIsUnavailable_AndSoItIsAfterTheChannelWasLetGo()
        {
            var provider = new BridgeKeyProvider();
            var channel = new FakeChannel((_, _) => Task.FromResult<ResolveKeyResponse?>(Answer(KeyBytes)));

            using var before = await provider.ResolveAsync(Request(), CancellationToken.None);
            provider.Attach(channel);
            using var bound = await provider.ResolveAsync(Request(), CancellationToken.None);
            provider.Detach(channel);
            using var after = await provider.ResolveAsync(Request(), CancellationToken.None);

            Assert.Equal(SecureKeyFailureReason.ProviderUnavailable, before.FailureReason);
            Assert.True(bound.IsResolved);
            Assert.Equal(SecureKeyFailureReason.ProviderUnavailable, after.FailureReason);
        }

        [Fact]
        public async Task ALaterChannel_ReplacesTheEarlierOne_AndLettingGoOfTheEarlierOneLeavesTheLaterBound()
        {
            var provider = new BridgeKeyProvider();
            var first = new FakeChannel((_, _) => Task.FromResult<ResolveKeyResponse?>(Answer(KeyBytes)));
            var second = new FakeChannel((_, _) => Task.FromResult<ResolveKeyResponse?>(Answer(KeyBytes)));
            provider.Attach(first);
            provider.Attach(second);

            provider.Detach(first);
            using var answer = await provider.ResolveAsync(Request(), CancellationToken.None);

            Assert.True(answer.IsResolved);
            Assert.Empty(first.Requests);
            Assert.Single(second.Requests);
            Assert.Throws<ArgumentNullException>(() => provider.Attach(null!));
        }

        [Fact]
        public async Task AKey_ArrivesAsALease_WithTheBytesOfTheClient_AndTheRequestCarriesEverythingTheVaultNeeds()
        {
            var (provider, channel) = Bound((_, _) => Task.FromResult<ResolveKeyResponse?>(Answer(KeyBytes)));

            using var answer = await provider.ResolveAsync(Request(interactive: true), CancellationToken.None);

            Assert.True(answer.IsResolved);
            Assert.Equal(KeyBytes, BytesOf(answer));
            var wire = Assert.Single(channel.Requests);
            Assert.Equal("desfire.app.current", wire.SlotId);
            Assert.Equal(Reference, wire.Reference);
            Assert.Equal(KeyAlgorithms.Aes128, wire.Algorithm);
            Assert.Equal(KeyUsages.ChangeKeyNew, wire.Usage);
            Assert.Equal((long?)0x00ABCDEF, wire.ApplicationId);
            Assert.Equal((int?)2, wire.KeyNumber);
            Assert.Equal("run-9", wire.RunId);
            Assert.Equal("4", wire.TaskIndex);
            Assert.Equal("04A1B2C3D4E5F6", wire.CardUid);
            Assert.True(wire.Interactive);
        }

        [Theory]
        [InlineData(SecureKeyAlgorithm.Des, KeyAlgorithms.Des)]
        [InlineData(SecureKeyAlgorithm.TripleDes2K, KeyAlgorithms.TripleDes2K)]
        [InlineData(SecureKeyAlgorithm.TripleDes3K, KeyAlgorithms.TripleDes3K)]
        [InlineData(SecureKeyAlgorithm.Aes128, KeyAlgorithms.Aes128)]
        [InlineData(SecureKeyAlgorithm.Classic6Byte, KeyAlgorithms.Classic6Byte)]
        public async Task EveryKindOfKey_HasANameOnTheWire(SecureKeyAlgorithm algorithm, string expected)
        {
            var (provider, channel) = Bound((_, _) => Task.FromResult<ResolveKeyResponse?>(Answer(KeyBytes)));
            var request = new SecureKeyRequest { SlotId = "s", Reference = Reference, Algorithm = algorithm, Usage = SecureKeyUsage.Authenticate };

            using var answer = await provider.ResolveAsync(request, CancellationToken.None);

            Assert.Equal(expected, Assert.Single(channel.Requests).Algorithm);
            Assert.Null(channel.Requests[0].ApplicationId);
            Assert.Null(channel.Requests[0].KeyNumber);
        }

        [Theory]
        [InlineData(SecureKeyUsage.Authenticate, KeyUsages.Authenticate)]
        [InlineData(SecureKeyUsage.ChangeKeyCurrent, KeyUsages.ChangeKeyCurrent)]
        [InlineData(SecureKeyUsage.ChangeKeyNew, KeyUsages.ChangeKeyNew)]
        public async Task EveryUseOfAKey_HasANameOnTheWire(SecureKeyUsage usage, string expected)
        {
            var (provider, channel) = Bound((_, _) => Task.FromResult<ResolveKeyResponse?>(Answer(KeyBytes)));
            var request = new SecureKeyRequest { SlotId = "s", Reference = Reference, Algorithm = SecureKeyAlgorithm.Aes128, Usage = usage };

            using var answer = await provider.ResolveAsync(request, CancellationToken.None);

            Assert.Equal(expected, Assert.Single(channel.Requests).Usage);
        }

        [Fact]
        public async Task AKindOfKeyOrUseThatTheProtocolHasNoNameFor_IsUnsupported_AndTheClientIsNotAsked()
        {
            var (provider, channel) = Bound((_, _) => Task.FromResult<ResolveKeyResponse?>(Answer(KeyBytes)));
            var unknownAlgorithm = new SecureKeyRequest { SlotId = "s", Reference = Reference, Algorithm = (SecureKeyAlgorithm)99, Usage = SecureKeyUsage.Authenticate };
            var unknownUsage = new SecureKeyRequest { SlotId = "s", Reference = Reference, Algorithm = SecureKeyAlgorithm.Aes128, Usage = (SecureKeyUsage)99 };

            using var first = await provider.ResolveAsync(unknownAlgorithm, CancellationToken.None);
            using var second = await provider.ResolveAsync(unknownUsage, CancellationToken.None);

            Assert.Equal(SecureKeyFailureReason.Unsupported, first.FailureReason);
            Assert.Equal(SecureKeyFailureReason.Unsupported, second.FailureReason);
            Assert.Empty(channel.Requests);
        }

        [Theory]
        [InlineData(KeyResolveResults.NotFound, SecureKeyFailureReason.NotFound)]
        [InlineData(KeyResolveResults.NotAuthorized, SecureKeyFailureReason.NotAuthorized)]
        [InlineData(KeyResolveResults.InvalidKey, SecureKeyFailureReason.InvalidKey)]
        [InlineData(KeyResolveResults.Unsupported, SecureKeyFailureReason.Unsupported)]
        [InlineData("somethingOfALaterVersion", SecureKeyFailureReason.ProviderUnavailable)]
        [InlineData("", SecureKeyFailureReason.ProviderUnavailable)]
        public async Task WhatTheClientAnswers_IsAReasonOfTheRegistry_AndAnUnknownAnswerIsAFailureNotAKey(string result, SecureKeyFailureReason expected)
        {
            var (provider, _) = Bound((_, _) => Task.FromResult<ResolveKeyResponse?>(new ResolveKeyResponse { Result = result, Key = Convert.ToBase64String(KeyBytes) }));

            using var answer = await provider.ResolveAsync(Request(), CancellationToken.None);

            Assert.False(answer.IsResolved);
            Assert.Equal(expected, answer.FailureReason);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("!!!not base64!!!")]
        [InlineData("QUJD=====")]
        [InlineData("   ")]
        public async Task AKeyThatIsNoKey_IsInvalid(string? key)
        {
            var (provider, _) = Bound((_, _) => Task.FromResult<ResolveKeyResponse?>(new ResolveKeyResponse { Result = KeyResolveResults.Resolved, Key = key }));

            using var answer = await provider.ResolveAsync(Request(), CancellationToken.None);

            Assert.Equal(SecureKeyFailureReason.InvalidKey, answer.FailureReason);
        }

        [Fact]
        public async Task TheLongestKeyIsTaken_AndOneByteMoreIsInvalid_WithoutDecodingAnythingLonger()
        {
            var longest = Enumerable.Repeat((byte)0x5A, BridgeProtocol.MaxKeyBytes).ToArray();
            var tooLong = Enumerable.Repeat((byte)0x5A, BridgeProtocol.MaxKeyBytes + 1).ToArray();
            var huge = new string('A', 100_000);
            var (provider, _) = Bound((request, _) => Task.FromResult<ResolveKeyResponse?>(
                request.SlotId switch
                {
                    "longest" => Answer(longest),
                    "tooLong" => Answer(tooLong),
                    "one" => Answer(new byte[] { 7 }),
                    _ => new ResolveKeyResponse { Result = KeyResolveResults.Resolved, Key = huge },
                }));

            using var atTheLimit = await provider.ResolveAsync(Request("longest"), CancellationToken.None);
            using var oneMore = await provider.ResolveAsync(Request("tooLong"), CancellationToken.None);
            using var single = await provider.ResolveAsync(Request("one"), CancellationToken.None);
            using var enormous = await provider.ResolveAsync(Request("huge"), CancellationToken.None);

            Assert.Equal(longest, BytesOf(atTheLimit));
            Assert.Equal(SecureKeyFailureReason.InvalidKey, oneMore.FailureReason);
            Assert.Equal(new byte[] { 7 }, BytesOf(single));
            Assert.Equal(SecureKeyFailureReason.InvalidKey, enormous.FailureReason);
        }

        private static SecureKeyRequest Request(string slot) => new SecureKeyRequest
        {
            SlotId = slot,
            Reference = Reference,
            Algorithm = SecureKeyAlgorithm.Aes128,
            Usage = SecureKeyUsage.Authenticate,
        };

        [Fact]
        public async Task NoAnswer_AndAClientThatFails_AreUnavailable_AndTheTextOfTheFailureIsNotLogged()
        {
            using var logs = CapturedLogs.Install();
            var (nothing, _) = Bound((_, _) => Task.FromResult<ResolveKeyResponse?>(null));
            var (failing, _) = Bound((_, _) => throw new InvalidOperationException(ExceptionText));

            using var none = await nothing.ResolveAsync(Request(), CancellationToken.None);
            using var failed = await failing.ResolveAsync(Request(), CancellationToken.None);

            Assert.Equal(SecureKeyFailureReason.ProviderUnavailable, none.FailureReason);
            Assert.Equal(SecureKeyFailureReason.ProviderUnavailable, failed.FailureReason);
            var written = string.Join("\n", logs.All().Select(CapturedLogs.EverythingWrittenFor));
            Assert.Contains(nameof(InvalidOperationException), written, StringComparison.Ordinal);
            Assert.DoesNotContain(ExceptionText, written, StringComparison.Ordinal);
            Assert.DoesNotContain(Reference, written, StringComparison.Ordinal);
        }

        [Fact]
        public async Task ACancelledRequest_IsCancelled_AndTheChannelSawTheToken()
        {
            CancellationToken seen = default;
            var (provider, _) = Bound(async (_, token) =>
            {
                seen = token;
                await Task.Delay(Timeout.Infinite, token);

                return null;
            });
            using var source = new CancellationTokenSource();

            var pending = provider.ResolveAsync(Request(), source.Token).AsTask();
            source.Cancel();
            using var answer = await pending;

            Assert.Equal(SecureKeyFailureReason.Cancelled, answer.FailureReason);
            Assert.True(seen.IsCancellationRequested);
        }

        // ── The provider among the others ──

        [Fact]
        public void TheProvider_IsABuiltInOne_WithAValidIdNoExtensionCanTake()
        {
            var builtIns = SecureKeyRegistry.BuiltInProviders();
            var registry = SecureKeyRegistry.Create(builtIns, Array.Empty<Lazy<ISecureKeyProvider, ISecureKeyProviderMetadata>>());

            Assert.Contains(builtIns, registration => registration.Id == BridgeKeyProvider.ProviderId);
            Assert.True(SecureKeySyntax.IsValidProviderId(BridgeKeyProvider.ProviderId));
            Assert.True(registry.Contains(BridgeKeyProvider.ProviderId));
            Assert.Contains(registry.Providers, info => info.Id == BridgeKeyProvider.ProviderId && info.DisplayName == BridgeKeyProvider.DisplayName);
        }

        [Fact]
        public async Task AnExtensionThatTakesTheIdOfTheProvider_IsIgnored_AndTheBuiltInOneStays()
        {
            var impostor = ScriptedSecureKeyProvider.Returning(KeyBytes);
            var export = new Lazy<ISecureKeyProvider, ISecureKeyProviderMetadata>(() => impostor, new FakeProviderMetadata(BridgeKeyProvider.ProviderId, "Impostor"));
            var registry = SecureKeyRegistry.Create(new[] { new SecureKeyProviderRegistration(BridgeKeyProvider.ProviderId, BridgeKeyProvider.DisplayName, new BridgeKeyProvider()) }, new[] { export });

            using var answer = await registry.ResolveAsync(BridgeKeyProvider.ProviderId, Request(), CancellationToken.None);

            Assert.Equal(SecureKeyFailureReason.ProviderUnavailable, answer.FailureReason);
            Assert.Empty(impostor.Requests);
        }

        [Fact]
        public async Task ATaskThatNamesTheProviderWithoutAClient_GetsNoKey_ForTheReasonAnyMissingProviderGives()
        {
            var registry = SecureKeyRegistry.Create(
                new[] { new SecureKeyProviderRegistration(BridgeKeyProvider.ProviderId, BridgeKeyProvider.DisplayName, new BridgeKeyProvider()) },
                Array.Empty<Lazy<ISecureKeyProvider, ISecureKeyProviderMetadata>>());
            var context = new KeyResolutionContext(registry, "run-9", "4", "04A1B2C3D4E5F6", interactive: false);
            var needs = new[] { new KeyNeed("desfire.app.current", SecureKeyAlgorithm.Aes128, SecureKeyUsage.Authenticate, 1, 0) };
            var bindings = new[] { new KeyBindingEntry { Slot = "desfire.app.current", ProviderId = BridgeKeyProvider.ProviderId, Reference = Reference } };

            var keys = await KeyAcquisition.AcquireAsync(needs, bindings, new[] { "desfire.app.current" }, _ => "EMBEDDED-KEY-NEVER-USED", context);

            // The command ends with ERROR.KeyUnavailable on this and does not touch the card; the embedded key is not a fallback.
            Assert.True(keys.Failed);
            Assert.Equal(BridgeKeyProvider.ProviderId, keys.Failure!.ProviderId);
            Assert.Equal(SecureKeyFailureReason.ProviderUnavailable.ToString(), keys.Failure.Cause);
        }

        // ── Over the pipe, to a client that keeps its keys in a vault of its own ──

        private (ExternalControlService Service, string PipeName, BridgeKeyProvider Provider, SecureKeyRegistry Registry) Start(
            TimeSpan? timeout = null,
            bool withProvider = true)
        {
            var name = "RFiDGear.Bridge.test-" + Guid.NewGuid().ToString("N");
            var provider = new BridgeKeyProvider();
            var host = new FakeExtensionHost();
            var service = new ExternalControlService(host, RunAtOnce, () => false, "2.0.10", name, keyProvider: withProvider ? provider : null);
            _disposables.Add(service);
            Assert.True(service.Start());
            service.MarkStartupComplete();
            var registry = SecureKeyRegistry.Create(
                new[] { new SecureKeyProviderRegistration(BridgeKeyProvider.ProviderId, BridgeKeyProvider.DisplayName, provider) },
                Array.Empty<Lazy<ISecureKeyProvider, ISecureKeyProviderMetadata>>(),
                timeout);

            return (service, name, provider, registry);
        }

        private async Task<BridgeTestClient> ConnectAsync(string pipeName, bool register = true)
        {
            var client = await BridgeTestClient.ConnectAsync(pipeName, 5000);
            _disposables.Add(client);
            await client.HelloAsync();

            if (register)
            {
                await client.Service.RegisterKeyProvider(new RegisterKeyProviderRequest());
            }

            return client;
        }

        private static Task<SecureKeyResolution> Ask(SecureKeyRegistry registry, CancellationToken cancellationToken = default, bool interactive = false) =>
            registry.ResolveAsync(BridgeKeyProvider.ProviderId, Request(interactive), cancellationToken);

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
        public async Task TheCapability_IsOfferedWithAProvider_AndWithoutOneTheMethodSaysItIsNot()
        {
            var (_, withPipe, _, _) = Start();
            var (_, withoutPipe, _, _) = Start(withProvider: false);
            var with = await BridgeTestClient.ConnectAsync(withPipe, 5000);
            var without = await BridgeTestClient.ConnectAsync(withoutPipe, 5000);
            _disposables.Add(with);
            _disposables.Add(without);

            var offered = await with.HelloAsync();
            var notOffered = await without.HelloAsync();
            var failure = await Assert.ThrowsAsync<RemoteInvocationException>(() => without.Service.RegisterKeyProvider(new RegisterKeyProviderRequest()));

            Assert.Contains(BridgeCapabilities.KeysProvider, offered.Capabilities);
            Assert.DoesNotContain(BridgeCapabilities.KeysProvider, notOffered.Capabilities);
            Assert.Equal(BridgeErrorCodes.CapabilityNotSupported, (int)failure.ErrorCode);
        }

        [Fact]
        public void TheProvider_IsBoundWhileTheServiceListens_AndNotAfterwards()
        {
            var (service, _, provider, _) = Start();
            var whileListening = provider.IsBound;

            service.Dispose();

            Assert.True(whileListening);
            Assert.False(provider.IsBound);
        }

        [Fact]
        public async Task Registering_SaysWhichProviderToNameInProjects_AndHowLongTheClientHasToAnswer()
        {
            var (_, pipe, _, _) = Start();
            var client = await ConnectAsync(pipe, register: false);

            var first = await client.Service.RegisterKeyProvider(new RegisterKeyProviderRequest());
            var again = await client.Service.RegisterKeyProvider(new RegisterKeyProviderRequest());

            Assert.Equal(BridgeKeyProvider.ProviderId, first.ProviderId);
            Assert.Equal((int)SecureKeyRegistry.DefaultTimeout.TotalMilliseconds, first.TimeoutMs);
            Assert.Equal((int)SecureKeyRegistry.DefaultInteractiveTimeout.TotalMilliseconds, first.InteractiveTimeoutMs);
            Assert.Equal(first, again);
        }

        [Fact]
        public async Task ARegisteredClient_IsAskedForTheKey_AndTheLeaseHoldsWhatItAnswered()
        {
            var (_, pipe, _, registry) = Start();
            var client = await ConnectAsync(pipe);
            client.KeyHandler = (_, _) => Task.FromResult(Answer(KeyBytes));

            using var answer = await Ask(registry);

            Assert.Equal(KeyBytes, BytesOf(answer));
            var asked = Assert.Single(client.KeyRequests);
            Assert.Equal(Reference, asked.Reference);
            Assert.Equal(KeyAlgorithms.Aes128, asked.Algorithm);
            Assert.Equal(KeyUsages.ChangeKeyNew, asked.Usage);
            Assert.Equal((long?)0x00ABCDEF, asked.ApplicationId);
            Assert.Equal((int?)2, asked.KeyNumber);
            Assert.Equal("04A1B2C3D4E5F6", asked.CardUid);
        }

        [Fact]
        public async Task AClientThatDidNotRegister_IsNeverAsked()
        {
            var (_, pipe, _, registry) = Start();
            var client = await ConnectAsync(pipe, register: false);
            client.KeyHandler = (_, _) => Task.FromResult(Answer(KeyBytes));

            using var answer = await Ask(registry);

            Assert.Equal(SecureKeyFailureReason.ProviderUnavailable, answer.FailureReason);
            Assert.Empty(client.KeyRequests);
        }

        [Fact]
        public async Task WithoutAnyClient_TheKeyIsUnavailable()
        {
            var (_, _, _, registry) = Start();

            using var answer = await Ask(registry);

            Assert.Equal(SecureKeyFailureReason.ProviderUnavailable, answer.FailureReason);
        }

        [Fact]
        public async Task TheRegistration_EndsWithTheConnection_ANewConnectionHasToRegisterAgain()
        {
            var (service, pipe, _, registry) = Start();
            var first = await ConnectAsync(pipe);
            first.KeyHandler = (_, _) => Task.FromResult(Answer(KeyBytes));
            using var whileRegistered = await Ask(registry);

            first.Dispose();
            await WaitUntilAsync(() => !service.IsConnected);
            using var afterItLeft = await Ask(registry);
            var second = await ConnectAsync(pipe, register: false);
            second.KeyHandler = (_, _) => Task.FromResult(Answer(KeyBytes));
            using var notRegisteredAgain = await Ask(registry);

            Assert.True(whileRegistered.IsResolved);
            Assert.Equal(SecureKeyFailureReason.ProviderUnavailable, afterItLeft.FailureReason);
            Assert.Equal(SecureKeyFailureReason.ProviderUnavailable, notRegisteredAgain.FailureReason);
            Assert.Empty(second.KeyRequests);
        }

        [Theory]
        [InlineData(KeyResolveResults.NotFound, SecureKeyFailureReason.NotFound)]
        [InlineData(KeyResolveResults.NotAuthorized, SecureKeyFailureReason.NotAuthorized)]
        [InlineData(KeyResolveResults.InvalidKey, SecureKeyFailureReason.InvalidKey)]
        public async Task TheAnswersOfAVault_ArriveAsTheirReasons(string result, SecureKeyFailureReason expected)
        {
            var (_, pipe, _, registry) = Start();
            var client = await ConnectAsync(pipe);
            client.KeyHandler = (_, _) => Task.FromResult(Answer(result));

            using var answer = await Ask(registry);

            Assert.Equal(expected, answer.FailureReason);
        }

        [Fact]
        public async Task AClientThatThrows_CostsTheKeyAndNothingElse()
        {
            var (_, pipe, _, registry) = Start();
            var client = await ConnectAsync(pipe);
            client.KeyHandler = (_, _) => throw new InvalidOperationException(ExceptionText);

            using var failed = await Ask(registry);
            client.KeyHandler = (_, _) => Task.FromResult(Answer(KeyBytes));
            using var next = await Ask(registry);

            Assert.Equal(SecureKeyFailureReason.ProviderUnavailable, failed.FailureReason);
            Assert.True(next.IsResolved);
        }

        [Fact]
        public async Task AClientThatIsTooSlow_TimesOut_AndIsToldToStopLooking()
        {
            var (_, pipe, _, registry) = Start(timeout: TimeSpan.FromMilliseconds(300));
            var client = await ConnectAsync(pipe);
            var stopped = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            client.KeyHandler = async (_, token) =>
            {
                token.Register(() => stopped.TrySetResult(true));
                await Task.Delay(Timeout.Infinite, token);

                return Answer(KeyBytes);
            };

            using var answer = await Ask(registry);

            Assert.Equal(SecureKeyFailureReason.Timeout, answer.FailureReason);
            Assert.True(await Task.WhenAny(stopped.Task, Task.Delay(5000)) == stopped.Task, "The client was not told to stop.");
        }

        [Fact]
        public async Task WhenTheRunIsGivenUp_TheClientIsToldToStopLooking()
        {
            var (_, pipe, _, registry) = Start();
            var client = await ConnectAsync(pipe);
            var asked = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var stopped = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            client.KeyHandler = async (_, token) =>
            {
                token.Register(() => stopped.TrySetResult(true));
                asked.TrySetResult(true);
                await Task.Delay(Timeout.Infinite, token);

                return Answer(KeyBytes);
            };
            using var source = new CancellationTokenSource();

            var pending = Ask(registry, source.Token);
            Assert.True(await Task.WhenAny(asked.Task, Task.Delay(5000)) == asked.Task, "The client was not asked.");
            source.Cancel();
            using var answer = await pending;

            Assert.Equal(SecureKeyFailureReason.Cancelled, answer.FailureReason);
            Assert.True(await Task.WhenAny(stopped.Task, Task.Delay(5000)) == stopped.Task, "The client was not told to stop.");
        }

        [Fact]
        public async Task AClientThatLeavesWhileItIsAsked_CostsTheKey_NotTheWholeTimeout()
        {
            var (service, pipe, _, registry) = Start();
            var client = await ConnectAsync(pipe);
            var asked = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            client.KeyHandler = async (_, token) =>
            {
                asked.TrySetResult(true);
                await Task.Delay(Timeout.Infinite, token);

                return Answer(KeyBytes);
            };

            var pending = Ask(registry);
            Assert.True(await Task.WhenAny(asked.Task, Task.Delay(5000)) == asked.Task, "The client was not asked.");
            client.Dispose();
            var finished = await Task.WhenAny(pending, Task.Delay(5000));

            Assert.Same(pending, finished);
            using var answer = await pending;
            Assert.Equal(SecureKeyFailureReason.ProviderUnavailable, answer.FailureReason);
            await WaitUntilAsync(() => !service.IsConnected);
        }

        [Fact]
        public async Task AKeyThatArrivesAfterTheTimeout_IsNotHandedOut()
        {
            var (_, pipe, _, registry) = Start(timeout: TimeSpan.FromMilliseconds(300));
            var client = await ConnectAsync(pipe);
            var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            // A client that does not honour the token and answers late.
            client.KeyHandler = async (_, _) =>
            {
                await release.Task;

                return Answer(KeyBytes);
            };

            using var answer = await Ask(registry);
            release.SetResult(true);
            await Task.Delay(200);

            Assert.Equal(SecureKeyFailureReason.Timeout, answer.FailureReason);
            Assert.Null(answer.Key);
        }

        [Fact]
        public async Task ARequestForAnInteractiveTask_SaysSo_AndIsGivenTheLongTimeLimit()
        {
            var (_, pipe, _, registry) = Start(timeout: TimeSpan.FromMilliseconds(200));
            var client = await ConnectAsync(pipe);
            client.KeyHandler = async (request, _) =>
            {
                // Longer than the limit for a task that nobody watches; a person may take that long to unlock a vault.
                await Task.Delay(600);

                return request.Interactive ? Answer(KeyBytes) : Answer(KeyResolveResults.NotFound);
            };

            using var interactive = await Ask(registry, interactive: true);
            using var unattended = await Ask(registry);

            Assert.True(interactive.IsResolved);
            Assert.Equal(SecureKeyFailureReason.Timeout, unattended.FailureReason);
        }

        [Fact]
        public async Task NeitherTheReferenceNorTheKeyNorTheTextOfAFailure_ReachesTheLog()
        {
            using var logs = CapturedLogs.Install();
            var (_, pipe, _, registry) = Start(timeout: TimeSpan.FromMilliseconds(300));
            var client = await ConnectAsync(pipe);

            client.KeyHandler = (_, _) => Task.FromResult(Answer(KeyBytes));
            using var resolved = await Ask(registry);
            client.KeyHandler = (_, _) => Task.FromResult(Answer(KeyResolveResults.NotAuthorized));
            using var refused = await Ask(registry);
            client.KeyHandler = (_, _) => throw new InvalidOperationException(ExceptionText + Convert.ToBase64String(KeyBytes));
            using var failed = await Ask(registry);
            client.KeyHandler = (_, token) => Task.Delay(Timeout.Infinite, token).ContinueWith(_ => Answer(KeyBytes), TaskScheduler.Default);
            using var late = await Ask(registry);
            client.KeyHandler = (_, _) => Task.FromResult(new ResolveKeyResponse { Result = KeyResolveResults.Resolved, Key = Convert.ToBase64String(new byte[200]) });
            using var wrong = await Ask(registry);
            await Task.Delay(300);

            var written = string.Join("\n", logs.All().Select(CapturedLogs.EverythingWrittenFor));
            Assert.True(resolved.IsResolved);
            Assert.Equal(SecureKeyFailureReason.NotAuthorized, refused.FailureReason);
            Assert.Equal(SecureKeyFailureReason.ProviderUnavailable, failed.FailureReason);
            Assert.Equal(SecureKeyFailureReason.Timeout, late.FailureReason);
            Assert.Equal(SecureKeyFailureReason.InvalidKey, wrong.FailureReason);
            Assert.NotEmpty(logs.All());

            foreach (var secret in new[]
            {
                Reference,
                ExceptionText,
                Convert.ToBase64String(KeyBytes),
                Convert.ToHexString(KeyBytes),
                Convert.ToHexString(KeyBytes).ToLowerInvariant(),
                "04A1B2C3D4E5F6",
            })
            {
                Assert.DoesNotContain(secret, written, StringComparison.Ordinal);
            }
        }

        [Fact]
        public async Task TheRequestOfAKey_IsNotSentWhenTheServiceIsGone()
        {
            var (service, pipe, _, registry) = Start();
            var client = await ConnectAsync(pipe);
            client.KeyHandler = (_, _) => Task.FromResult(Answer(KeyBytes));

            service.Dispose();
            using var answer = await Ask(registry);

            Assert.Equal(SecureKeyFailureReason.ProviderUnavailable, answer.FailureReason);
            Assert.Empty(client.KeyRequests);
        }

    }
}
