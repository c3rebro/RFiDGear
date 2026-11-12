using RFiDGear.Contracts;
using RFiDGear.ExtensionSdk;
using RFiDGear.Services.SecureKeys;

using Serilog.Events;

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Hosting;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Xunit;

namespace RFiDGear.Tests.SecureKeys
{
    /// <summary>
    /// The registry is the one door between the host and the providers of extensions, which are not trusted to behave. These
    /// tests make a provider throw, hang, answer nothing and answer with something unknown, and check that the host only ever
    /// sees a failed answer, never an exception, a hang or a key that nobody disposes.
    /// </summary>
    public class SecureKeyRegistryTests
    {
        private const string Id = "test.vault";

        private static readonly Lazy<ISecureKeyProvider, ISecureKeyProviderMetadata>[] NoExtensions =
            Array.Empty<Lazy<ISecureKeyProvider, ISecureKeyProviderMetadata>>();

        // ── Resolving ──

        [Fact]
        public async Task A_registered_provider_hands_out_its_key_for_the_request_it_was_given()
        {
            var provider = ScriptedSecureKeyProvider.Returning(SecureKeyTestData.SixteenBytes());
            var registry = RegistryOf(provider);
            var request = SecureKeyTestData.Request();

            using var answer = await registry.ResolveAsync(Id, request, CancellationToken.None);

            Assert.True(answer.IsResolved);
            Assert.Equal(Convert.ToHexString(SecureKeyTestData.SixteenBytes()), Assert.IsType<SecureKeyLease>(answer.Key).ToHexString());
            Assert.Same(request, Assert.Single(provider.Requests));
        }

        [Theory]
        [InlineData("nobody.here")]
        [InlineData("")]
        [InlineData(null)]
        public async Task A_provider_id_nobody_registered_is_unavailable_and_nothing_is_called(string? providerId)
        {
            var provider = ScriptedSecureKeyProvider.Returning(SecureKeyTestData.SixteenBytes());
            var registry = RegistryOf(provider);

            using var answer = await registry.ResolveAsync(providerId, SecureKeyTestData.Request(), CancellationToken.None);

            Assert.Equal(SecureKeyFailureReason.ProviderUnavailable, answer.FailureReason);
            Assert.Empty(provider.Requests);
        }

        [Fact]
        public async Task A_failure_the_provider_reports_comes_through_unchanged()
        {
            var registry = RegistryOf(ScriptedSecureKeyProvider.Failing(SecureKeyFailureReason.NotFound));

            using var answer = await registry.ResolveAsync(Id, SecureKeyTestData.Request(), CancellationToken.None);

            Assert.Equal(SecureKeyFailureReason.NotFound, answer.FailureReason);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task A_provider_that_throws_is_unavailable_and_nothing_it_says_is_logged(bool throwsAfterItsFirstAwait)
        {
            using var logs = CapturedLogs.Install();
            var message = $"the vault answered: {SecureKeyTestData.Secret}";
            Func<SecureKeyRequest, CancellationToken, ValueTask<SecureKeyResolution>> behaviour = throwsAfterItsFirstAwait
                ? async (_, _) =>
                {
                    await Task.Yield();
                    throw new InvalidOperationException(message);
                }
                : (_, _) => throw new InvalidOperationException(message);
            var registry = RegistryOf(new ScriptedSecureKeyProvider(behaviour));

            using var answer = await registry.ResolveAsync(Id, SecureKeyTestData.Request(), CancellationToken.None);

            Assert.Equal(SecureKeyFailureReason.ProviderUnavailable, answer.FailureReason);
            var written = logs.From(nameof(SecureKeyRegistry)).Select(CapturedLogs.EverythingWrittenFor).ToList();
            Assert.NotEmpty(written);
            Assert.All(written, text => Assert.DoesNotContain(SecureKeyTestData.Secret, text, StringComparison.Ordinal));
            Assert.Contains(written, text => text.Contains(nameof(InvalidOperationException), StringComparison.Ordinal) && text.Contains(Id, StringComparison.Ordinal));
        }

        [Fact]
        public async Task A_provider_that_answers_nothing_is_unavailable()
        {
            var registry = RegistryOf(new ScriptedSecureKeyProvider((_, _) => new ValueTask<SecureKeyResolution>((SecureKeyResolution)null!)));

            using var answer = await registry.ResolveAsync(Id, SecureKeyTestData.Request(), CancellationToken.None);

            Assert.Equal(SecureKeyFailureReason.ProviderUnavailable, answer.FailureReason);
        }

        [Fact]
        public async Task A_provider_that_cancels_for_no_reason_is_unavailable_and_not_timed_out()
        {
            var registry = RegistryOf(new ScriptedSecureKeyProvider((_, _) => throw new OperationCanceledException()));

            using var answer = await registry.ResolveAsync(Id, SecureKeyTestData.Request(), CancellationToken.None);

            Assert.Equal(SecureKeyFailureReason.ProviderUnavailable, answer.FailureReason);
        }

        [Fact]
        public async Task A_kind_of_key_the_host_does_not_know_is_unsupported_and_disposed()
        {
            var unknown = new UnknownKindOfKey();
            var registry = RegistryOf(new ScriptedSecureKeyProvider((_, _) => new ValueTask<SecureKeyResolution>(SecureKeyResolution.Resolved(unknown))));

            using var answer = await registry.ResolveAsync(Id, SecureKeyTestData.Request(), CancellationToken.None);

            Assert.Equal(SecureKeyFailureReason.Unsupported, answer.FailureReason);
            Assert.Equal(1, unknown.Disposals);
        }

        [Fact]
        public async Task A_key_whose_disposal_throws_is_still_unsupported_and_nothing_it_says_is_logged()
        {
            using var logs = CapturedLogs.Install();
            var registry = RegistryOf(new ScriptedSecureKeyProvider((_, _) =>
                new ValueTask<SecureKeyResolution>(SecureKeyResolution.Resolved(new KeyThatCannotBeDisposed()))));

            using var answer = await registry.ResolveAsync(Id, SecureKeyTestData.Request(), CancellationToken.None);

            Assert.Equal(SecureKeyFailureReason.Unsupported, answer.FailureReason);
            var written = logs.From(nameof(SecureKeyRegistry)).Select(CapturedLogs.EverythingWrittenFor).ToList();
            Assert.Contains(written, text => text.Contains(nameof(InvalidOperationException), StringComparison.Ordinal));
            Assert.All(written, text => Assert.DoesNotContain(SecureKeyTestData.Secret, text, StringComparison.Ordinal));
        }

        [Fact]
        public async Task A_late_key_whose_disposal_throws_is_logged_by_type_only()
        {
            using var logs = CapturedLogs.Install();
            var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var registry = RegistryOf(
                new ScriptedSecureKeyProvider(async (_, _) =>
                {
                    await release.Task;
                    return SecureKeyResolution.Resolved(new KeyThatCannotBeDisposed());
                }),
                timeout: TimeSpan.FromMilliseconds(150));

            using var answer = await registry.ResolveAsync(Id, SecureKeyTestData.Request(), CancellationToken.None);
            release.SetResult(true);

            Assert.Equal(SecureKeyFailureReason.Timeout, answer.FailureReason);
            Assert.True(
                await WaitFor(() => logs.From(nameof(SecureKeyRegistry)).Any(e => e.RenderMessage().Contains("disposed", StringComparison.Ordinal))),
                "the failed disposal was not logged");
            Assert.All(
                logs.From(nameof(SecureKeyRegistry)).Select(CapturedLogs.EverythingWrittenFor),
                text => Assert.DoesNotContain(SecureKeyTestData.Secret, text, StringComparison.Ordinal));
        }

        [Fact]
        public async Task The_provider_runs_without_the_synchronization_context_of_the_caller()
        {
            SynchronizationContext? seen = new SynchronizationContext();
            var isPoolThread = false;
            var registry = RegistryOf(new ScriptedSecureKeyProvider((_, _) =>
            {
                seen = SynchronizationContext.Current;
                isPoolThread = Thread.CurrentThread.IsThreadPoolThread;
                return new ValueTask<SecureKeyResolution>(SecureKeyResolution.Failed(SecureKeyFailureReason.NotFound));
            }));

            Task<SecureKeyResolution> pending;
            var previous = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(new MarkerContext());

            try
            {
                pending = registry.ResolveAsync(Id, SecureKeyTestData.Request(), CancellationToken.None);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previous);
            }

            using var answer = await pending;

            Assert.Null(seen);
            Assert.True(isPoolThread);
        }

        [Fact]
        public async Task The_same_provider_can_be_asked_by_many_callers_at_once()
        {
            var registry = RegistryOf(ScriptedSecureKeyProvider.Returning(SecureKeyTestData.SixteenBytes()));

            var answers = await Task.WhenAll(
                Enumerable.Range(0, 20).Select(_ => registry.ResolveAsync(Id, SecureKeyTestData.Request(), CancellationToken.None)));

            try
            {
                Assert.All(answers, answer => Assert.True(answer.IsResolved));
                Assert.Equal(20, answers.Select(answer => answer.Key).Distinct().Count());
            }
            finally
            {
                foreach (var answer in answers)
                {
                    answer.Dispose();
                }
            }
        }

        // ── Time ──

        [Fact]
        public async Task A_provider_that_hangs_times_out_and_the_key_it_hands_over_late_is_disposed()
        {
            var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            SecureKeyLease? late = null;
            var providerToken = CancellationToken.None;
            var registry = RegistryOf(
                new ScriptedSecureKeyProvider(async (_, token) =>
                {
                    // Does not look at the token, as a provider that ignores the contract would.
                    providerToken = token;
                    await release.Task;
                    late = new SecureKeyLease(SecureKeyTestData.SixteenBytes());
                    return SecureKeyResolution.Resolved(late);
                }),
                timeout: TimeSpan.FromMilliseconds(150));

            using var answer = await registry.ResolveAsync(Id, SecureKeyTestData.Request(), CancellationToken.None);

            Assert.Equal(SecureKeyFailureReason.Timeout, answer.FailureReason);
            Assert.True(await WaitFor(() => providerToken.IsCancellationRequested), "the provider was not told to stop");
            release.SetResult(true);
            Assert.True(await WaitFor(() => late != null && IsDisposed(late)), "the late key was not disposed");
        }

        [Fact]
        public async Task A_key_that_arrives_after_the_caller_gave_up_is_not_handed_out()
        {
            // The caller cancels while the provider is answering, so that both are done when the host looks: the answer
            // must lose, and the key must be disposed.
            SecureKeyLease? issued = null;
            using var cancel = new CancellationTokenSource();
            var registry = RegistryOf(new ScriptedSecureKeyProvider((_, _) =>
            {
                cancel.Cancel();
                issued = new SecureKeyLease(SecureKeyTestData.SixteenBytes());
                return new ValueTask<SecureKeyResolution>(SecureKeyResolution.Resolved(issued));
            }));

            using var answer = await registry.ResolveAsync(Id, SecureKeyTestData.Request(), cancel.Token);

            Assert.Equal(SecureKeyFailureReason.Cancelled, answer.FailureReason);
            Assert.NotNull(issued);
            Assert.True(IsDisposed(issued!), "the key was handed out although the caller had given up");
        }

        [Fact]
        public async Task A_callback_a_provider_registers_on_its_token_cannot_hold_the_host_up()
        {
            using var unblock = new ManualResetEventSlim();
            var registry = RegistryOf(
                new ScriptedSecureKeyProvider(async (_, token) =>
                {
                    // Registers after the host has started waiting, and blocks whoever cancels the token.
                    await Task.Delay(100);
                    token.Register(() => unblock.Wait());
                    await Task.Delay(Timeout.Infinite);
                    throw new InvalidOperationException("unreachable");
                }),
                timeout: TimeSpan.FromMilliseconds(500));

            try
            {
                var pending = registry.ResolveAsync(Id, SecureKeyTestData.Request(), CancellationToken.None);
                var finished = await Task.WhenAny(pending, Task.Delay(TimeSpan.FromSeconds(10)));

                Assert.Same(pending, finished);
                using var answer = await pending;
                Assert.Equal(SecureKeyFailureReason.Timeout, answer.FailureReason);
            }
            finally
            {
                unblock.Set();
            }
        }

        [Fact]
        public async Task A_callback_on_the_providers_token_that_throws_is_logged_by_type_only()
        {
            using var logs = CapturedLogs.Install();
            var registry = RegistryOf(
                new ScriptedSecureKeyProvider(async (_, token) =>
                {
                    await Task.Delay(100);
                    token.Register(() => throw new InvalidOperationException($"the callback failed for {SecureKeyTestData.Secret}"));
                    await Task.Delay(Timeout.Infinite);
                    throw new InvalidOperationException("unreachable");
                }),
                timeout: TimeSpan.FromMilliseconds(500));

            using var answer = await registry.ResolveAsync(Id, SecureKeyTestData.Request(), CancellationToken.None);

            Assert.Equal(SecureKeyFailureReason.Timeout, answer.FailureReason);
            Assert.True(
                await WaitFor(() => logs.From(nameof(SecureKeyRegistry)).Any(e => e.Level == LogEventLevel.Warning)),
                "the failure of the callback was not logged");
            Assert.All(
                logs.From(nameof(SecureKeyRegistry)).Select(CapturedLogs.EverythingWrittenFor),
                text => Assert.DoesNotContain(SecureKeyTestData.Secret, text, StringComparison.Ordinal));
        }

        [Fact]
        public async Task A_provider_that_honours_the_token_is_a_timeout_too()
        {
            var registry = RegistryOf(
                new ScriptedSecureKeyProvider(async (_, token) =>
                {
                    await Task.Delay(Timeout.Infinite, token);
                    throw new InvalidOperationException("unreachable");
                }),
                timeout: TimeSpan.FromMilliseconds(150));

            using var answer = await registry.ResolveAsync(Id, SecureKeyTestData.Request(), CancellationToken.None);

            Assert.Equal(SecureKeyFailureReason.Timeout, answer.FailureReason);
        }

        [Fact]
        public async Task A_caller_that_gives_up_gets_cancelled_and_the_provider_is_told()
        {
            var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var providerToken = CancellationToken.None;
            var registry = RegistryOf(new ScriptedSecureKeyProvider(async (_, token) =>
            {
                providerToken = token;
                started.SetResult(true);
                await Task.Delay(Timeout.Infinite, token);
                throw new InvalidOperationException("unreachable");
            }));
            using var cancel = new CancellationTokenSource();

            var pending = registry.ResolveAsync(Id, SecureKeyTestData.Request(), cancel.Token);
            await started.Task;
            await cancel.CancelAsync();
            using var answer = await pending;

            Assert.Equal(SecureKeyFailureReason.Cancelled, answer.FailureReason);
            Assert.True(await WaitFor(() => providerToken.IsCancellationRequested), "the provider was not told to stop");
        }

        [Fact]
        public async Task A_caller_that_has_already_given_up_never_reaches_the_provider()
        {
            var provider = ScriptedSecureKeyProvider.Returning(SecureKeyTestData.SixteenBytes());
            var registry = RegistryOf(provider);
            using var cancel = new CancellationTokenSource();
            await cancel.CancelAsync();

            using var answer = await registry.ResolveAsync(Id, SecureKeyTestData.Request(), cancel.Token);

            Assert.Equal(SecureKeyFailureReason.Cancelled, answer.FailureReason);
            Assert.Empty(provider.Requests);
        }

        [Fact]
        public async Task A_request_that_may_ask_a_person_gets_the_longer_limit()
        {
            var registry = RegistryOf(
                new ScriptedSecureKeyProvider(async (_, _) =>
                {
                    await Task.Delay(400);
                    return SecureKeyResolution.Resolved(new SecureKeyLease(SecureKeyTestData.SixteenBytes()));
                }),
                timeout: TimeSpan.FromMilliseconds(100),
                interactiveTimeout: TimeSpan.FromSeconds(30));

            using var silent = await registry.ResolveAsync(Id, SecureKeyTestData.Request(interactive: false), CancellationToken.None);
            using var prompting = await registry.ResolveAsync(Id, SecureKeyTestData.Request(interactive: true), CancellationToken.None);

            Assert.Equal(SecureKeyFailureReason.Timeout, silent.FailureReason);
            Assert.True(prompting.IsResolved);
        }

        // ── Building the registry ──

        [Fact]
        public void A_built_in_provider_needs_a_valid_id_and_a_unique_one()
        {
            var provider = ScriptedSecureKeyProvider.Returning(SecureKeyTestData.SixteenBytes());

            Assert.Throws<ArgumentException>(() => SecureKeyRegistry.Create(
                new[] { new SecureKeyProviderRegistration("Not Valid", "x", provider) }, NoExtensions));
            Assert.Throws<ArgumentException>(() => SecureKeyRegistry.Create(
                new[] { new SecureKeyProviderRegistration(Id, "one", provider), new SecureKeyProviderRegistration(Id, "two", provider) }, NoExtensions));
        }

        [Fact]
        public async Task An_extension_cannot_take_over_the_id_of_a_built_in_provider()
        {
            using var logs = CapturedLogs.Install();
            var builtIn = ScriptedSecureKeyProvider.Returning(Enumerable.Repeat((byte)0x01, 16).ToArray());
            var intruder = ScriptedSecureKeyProvider.Returning(Enumerable.Repeat((byte)0x02, 16).ToArray());

            var registry = SecureKeyRegistry.Create(
                new[] { new SecureKeyProviderRegistration(Id, "Built in", builtIn) },
                new[] { Export(Id, intruder) });

            using var answer = await registry.ResolveAsync(Id, SecureKeyTestData.Request(), CancellationToken.None);
            Assert.Equal(string.Concat(Enumerable.Repeat("01", 16)), Assert.IsType<SecureKeyLease>(answer.Key).ToHexString());
            Assert.Empty(intruder.Requests);
            Assert.Equal("Built in", Assert.Single(registry.Providers).DisplayName);
            Assert.Contains(logs.From(nameof(SecureKeyRegistry)), e => e.Level == LogEventLevel.Warning && e.RenderMessage().Contains("built-in", StringComparison.Ordinal));
        }

        [Fact]
        public void Two_extensions_that_use_one_id_are_both_left_out()
        {
            using var logs = CapturedLogs.Install();
            var any = ScriptedSecureKeyProvider.Failing(SecureKeyFailureReason.NotFound);

            var registry = SecureKeyRegistry.Create(
                Array.Empty<SecureKeyProviderRegistration>(),
                new[] { Export("shared.id", any), Export("unique.id", any), Export("shared.id", any) });

            Assert.Equal(new[] { "unique.id" }, registry.Providers.Select(p => p.Id));
            Assert.False(registry.Contains("shared.id"));
            Assert.Contains(logs.From(nameof(SecureKeyRegistry)), e => e.Level == LogEventLevel.Warning && e.RenderMessage().Contains("more than one", StringComparison.Ordinal));
        }

        [Theory]
        [InlineData("Upper")]
        [InlineData("has space")]
        [InlineData("")]
        [InlineData("ends.with.newline\n")]
        [InlineData("ümlaut")]
        [InlineData("slash/not/allowed")]
        public void An_extension_with_an_invalid_id_is_left_out(string id)
        {
            using var logs = CapturedLogs.Install();

            var registry = SecureKeyRegistry.Create(
                Array.Empty<SecureKeyProviderRegistration>(),
                new[] { Export(id, ScriptedSecureKeyProvider.Failing(SecureKeyFailureReason.NotFound)) });

            Assert.Empty(registry.Providers);
            Assert.Contains(logs.From(nameof(SecureKeyRegistry)), e => e.Level == LogEventLevel.Warning);
        }

        [Fact]
        public void An_id_of_64_characters_is_the_longest_that_is_accepted()
        {
            var provider = ScriptedSecureKeyProvider.Failing(SecureKeyFailureReason.NotFound);

            var registry = SecureKeyRegistry.Create(
                Array.Empty<SecureKeyProviderRegistration>(),
                new[] { Export(new string('a', 64), provider), Export(new string('b', 65), provider) });

            Assert.Equal(new[] { new string('a', 64) }, registry.Providers.Select(p => p.Id));
        }

        [Fact]
        public void An_extension_whose_metadata_cannot_be_read_is_left_out_and_the_others_stay()
        {
            using var logs = CapturedLogs.Install();
            var broken = new Lazy<ISecureKeyProvider, ISecureKeyProviderMetadata>(
                () => ScriptedSecureKeyProvider.Failing(SecureKeyFailureReason.NotFound),
                new UnreadableProviderMetadata());

            var registry = SecureKeyRegistry.Create(
                Array.Empty<SecureKeyProviderRegistration>(),
                new[] { broken, Export("good.vault", ScriptedSecureKeyProvider.Failing(SecureKeyFailureReason.NotFound)) });

            Assert.Equal(new[] { "good.vault" }, registry.Providers.Select(p => p.Id));
            var written = logs.From(nameof(SecureKeyRegistry)).Select(CapturedLogs.EverythingWrittenFor).ToList();
            Assert.Contains(written, text => text.Contains(nameof(InvalidOperationException), StringComparison.Ordinal));
            Assert.All(written, text => Assert.DoesNotContain(SecureKeyTestData.Secret, text, StringComparison.Ordinal));
        }

        [Fact]
        public void An_extension_that_cannot_be_created_is_left_out_and_the_others_stay()
        {
            using var logs = CapturedLogs.Install();
            var cannotBeCreated = new Lazy<ISecureKeyProvider, ISecureKeyProviderMetadata>(
                () => throw new InvalidOperationException($"the constructor failed for {SecureKeyTestData.Secret}"),
                new FakeProviderMetadata("broken.vault", "Broken"));

            var registry = SecureKeyRegistry.Create(
                Array.Empty<SecureKeyProviderRegistration>(),
                new[] { cannotBeCreated, Export("good.vault", ScriptedSecureKeyProvider.Failing(SecureKeyFailureReason.NotFound)) });

            Assert.Equal(new[] { "good.vault" }, registry.Providers.Select(p => p.Id));
            var written = logs.From(nameof(SecureKeyRegistry)).Select(CapturedLogs.EverythingWrittenFor).ToList();
            Assert.Contains(written, text => text.Contains("broken.vault", StringComparison.Ordinal) && text.Contains(nameof(InvalidOperationException), StringComparison.Ordinal));
            Assert.All(written, text => Assert.DoesNotContain(SecureKeyTestData.Secret, text, StringComparison.Ordinal));
        }

        [Fact]
        public void Every_accepted_provider_is_created_while_the_registry_is_built()
        {
            var created = 0;
            var export = new Lazy<ISecureKeyProvider, ISecureKeyProviderMetadata>(
                () =>
                {
                    created++;
                    return ScriptedSecureKeyProvider.Failing(SecureKeyFailureReason.NotFound);
                },
                new FakeProviderMetadata("eager.vault", "Eager"));

            _ = SecureKeyRegistry.Create(Array.Empty<SecureKeyProviderRegistration>(), new[] { export });

            Assert.Equal(1, created);
        }

        [Fact]
        public void The_providers_are_listed_by_id_with_the_display_name_or_the_id_when_there_is_none()
        {
            var provider = ScriptedSecureKeyProvider.Failing(SecureKeyFailureReason.NotFound);

            var registry = SecureKeyRegistry.Create(
                Array.Empty<SecureKeyProviderRegistration>(),
                new[] { Export("b.vault", provider, "Beta"), Export("a.vault", provider, "  ") });

            Assert.Equal(
                new[] { new SecureKeyProviderInfo("a.vault", "a.vault"), new SecureKeyProviderInfo("b.vault", "Beta") },
                registry.Providers);
        }

        [Fact]
        public void A_container_that_cannot_list_the_providers_is_logged_by_type_without_the_exception()
        {
            using var logs = CapturedLogs.Install();
            var container = new CompositionContainer();
            container.Dispose();
            var builtIn = ScriptedSecureKeyProvider.Failing(SecureKeyFailureReason.NotFound);

            var registry = SecureKeyRegistry.FromContainer(container, new[] { new SecureKeyProviderRegistration(Id, "Built in", builtIn) });

            Assert.Equal(new[] { Id }, registry.Providers.Select(p => p.Id));
            var logged = Assert.Single(logs.From(nameof(SecureKeyRegistry)), e => e.Level == LogEventLevel.Error);
            Assert.Null(logged.Exception);
            Assert.Contains(nameof(ObjectDisposedException), CapturedLogs.EverythingWrittenFor(logged), StringComparison.Ordinal);
        }

        [Fact]
        public async Task A_registry_without_providers_and_one_built_from_no_container_know_only_the_built_in_ones()
        {
            var builtIn = ScriptedSecureKeyProvider.Failing(SecureKeyFailureReason.NotFound);

            var fromNothing = SecureKeyRegistry.FromContainer(null, new[] { new SecureKeyProviderRegistration(Id, "Built in", builtIn) });
            using var answer = await SecureKeyRegistry.Empty.ResolveAsync(Id, SecureKeyTestData.Request(), CancellationToken.None);

            Assert.Equal(new[] { Id }, fromNothing.Providers.Select(p => p.Id));
            Assert.True(fromNothing.Contains(Id));
            Assert.False(fromNothing.Contains(null));
            Assert.Empty(SecureKeyRegistry.Empty.Providers);
            Assert.Equal(SecureKeyFailureReason.ProviderUnavailable, answer.FailureReason);
        }

        // ── Helpers ──

        private static SecureKeyRegistry RegistryOf(ISecureKeyProvider provider, TimeSpan? timeout = null, TimeSpan? interactiveTimeout = null) =>
            SecureKeyRegistry.Create(
                new[] { new SecureKeyProviderRegistration(Id, "Test vault", provider) },
                NoExtensions,
                timeout,
                interactiveTimeout);

        private static Lazy<ISecureKeyProvider, ISecureKeyProviderMetadata> Export(string id, ISecureKeyProvider provider, string displayName = "Name") =>
            new Lazy<ISecureKeyProvider, ISecureKeyProviderMetadata>(() => provider, new FakeProviderMetadata(id, displayName));

        private static bool IsDisposed(SecureKeyLease lease)
        {
            try
            {
                _ = lease.ToHexString();
                return false;
            }
            catch (ObjectDisposedException)
            {
                return true;
            }
        }

        private static async Task<bool> WaitFor(Func<bool> condition)
        {
            for (var attempt = 0; attempt < 500 && !condition(); attempt++)
            {
                await Task.Delay(10);
            }

            return condition();
        }

        private sealed class MarkerContext : SynchronizationContext
        {
        }
    }
}
