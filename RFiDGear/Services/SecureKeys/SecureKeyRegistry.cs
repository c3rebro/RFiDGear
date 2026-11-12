#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Hosting;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RFiDGear.Contracts;
using RFiDGear.ExtensionSdk;
using Serilog;

namespace RFiDGear.Services.SecureKeys
{
    /// <summary>
    /// The secure key providers of this installation, and the one way to ask them for a key. Built once, on the
    /// user-interface thread: it creates every provider it accepts right away (the MEF container is not thread-safe, and a
    /// provider that cannot be created should show up at start-up, not in the middle of a card operation). After that it
    /// holds instances only, and <see cref="ResolveAsync"/> may be called from any thread.
    /// <para>
    /// Everything that can go wrong with a provider stays inside: a provider that throws, hangs, answers with nothing or
    /// with a kind of key the host does not know turns into a <see cref="SecureKeyResolution.Failed"/>, never into an
    /// exception, and a key that arrives after the host gave up is disposed. Nothing that a provider throws is logged except
    /// the type of the exception and the id of the provider: the messages of vault libraries are not under our control and
    /// may contain what they were asked for.
    /// </para>
    /// </summary>
    internal sealed class SecureKeyRegistry
    {
        /// <summary>How long the host waits for a provider that cannot ask a person.</summary>
        internal static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

        /// <summary>How long the host waits for a provider that may ask a person (a prompt can stay open).</summary>
        internal static readonly TimeSpan DefaultInteractiveTimeout = TimeSpan.FromMinutes(5);

        private readonly IReadOnlyDictionary<string, Entry> _entries;
        private readonly TimeSpan _timeout;
        private readonly TimeSpan _interactiveTimeout;

        private SecureKeyRegistry(IReadOnlyDictionary<string, Entry> entries, TimeSpan timeout, TimeSpan interactiveTimeout)
        {
            _entries = entries;
            _timeout = timeout;
            _interactiveTimeout = interactiveTimeout;
        }

        /// <summary>Gets a registry without providers: every project that names one gets <see cref="SecureKeyFailureReason.ProviderUnavailable"/>.</summary>
        internal static SecureKeyRegistry Empty { get; } =
            new SecureKeyRegistry(new Dictionary<string, Entry>(), DefaultTimeout, DefaultInteractiveTimeout);

        /// <summary>Gets the accepted providers, ordered by id, for the user interface.</summary>
        internal IReadOnlyList<SecureKeyProviderInfo> Providers =>
            _entries.Values
                .OrderBy(entry => entry.Id, StringComparer.Ordinal)
                .Select(entry => new SecureKeyProviderInfo(entry.Id, entry.DisplayName))
                .ToList();

        /// <summary>
        /// Builds the registry from the providers that MEF found in the container. A container that fails the query is
        /// logged, and the registry then holds the built-in providers only. Call it on the user-interface thread.
        /// </summary>
        /// <param name="container">The composed container; may be <c>null</c> when MEF is not composed.</param>
        /// <param name="builtIns">The providers that belong to RFiDGear itself.</param>
        /// <returns>The registry.</returns>
        internal static SecureKeyRegistry FromContainer(CompositionContainer? container, IEnumerable<SecureKeyProviderRegistration> builtIns)
        {
            IReadOnlyList<Lazy<ISecureKeyProvider, ISecureKeyProviderMetadata>> exports =
                Array.Empty<Lazy<ISecureKeyProvider, ISecureKeyProviderMetadata>>();

            if (container != null)
            {
                try
                {
                    exports = container.GetExports<ISecureKeyProvider, ISecureKeyProviderMetadata>().ToList();
                }
                catch (Exception e)
                {
                    // Only the type: what an extension throws while its provider export is evaluated is not ours to print.
                    Log.ForContext<SecureKeyRegistry>().Error(
                        "The extensions' secure key providers could not be listed ({ExceptionType}); continuing with the built-in ones",
                        e.GetType().FullName);
                }
            }

            return Create(builtIns, exports);
        }

        /// <summary>
        /// Builds the registry. Built-in providers come first and keep their ids. An extension provider is left out, with a
        /// log entry, when its id is not valid, when its metadata cannot be read, when the id belongs to a built-in
        /// provider, when another extension uses the same id (neither is used: there is no safe way to tell which one the
        /// project meant) or when it cannot be created.
        /// </summary>
        /// <param name="builtIns">The providers that belong to RFiDGear itself.</param>
        /// <param name="extensions">The exports found in extensions.</param>
        /// <param name="timeout">How long to wait for a provider that cannot ask a person; defaults to <see cref="DefaultTimeout"/>.</param>
        /// <param name="interactiveTimeout">How long to wait for one that may; defaults to <see cref="DefaultInteractiveTimeout"/>.</param>
        /// <returns>The registry.</returns>
        /// <exception cref="ArgumentException">A built-in provider has an invalid or a duplicate id: a defect of the application.</exception>
        internal static SecureKeyRegistry Create(
            IEnumerable<SecureKeyProviderRegistration> builtIns,
            IEnumerable<Lazy<ISecureKeyProvider, ISecureKeyProviderMetadata>> extensions,
            TimeSpan? timeout = null,
            TimeSpan? interactiveTimeout = null)
        {
            ArgumentNullException.ThrowIfNull(builtIns);
            ArgumentNullException.ThrowIfNull(extensions);

            var entries = new Dictionary<string, Entry>(StringComparer.Ordinal);

            foreach (var builtIn in builtIns)
            {
                if (!SecureKeySyntax.IsValidProviderId(builtIn.Id) || !entries.TryAdd(builtIn.Id, new Entry(builtIn.Id, builtIn.DisplayName, builtIn.Provider)))
                {
                    throw new ArgumentException($"The built-in secure key provider id '{builtIn.Id}' is not valid or is used twice.", nameof(builtIns));
                }
            }

            var candidates = ReadMetadata(extensions);
            var usedByMoreThanOne = candidates
                .GroupBy(candidate => candidate.Id, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var candidate in candidates.OrderBy(candidate => candidate.Id, StringComparer.Ordinal))
            {
                if (entries.ContainsKey(candidate.Id))
                {
                    Log.ForContext<SecureKeyRegistry>().Warning("The secure key provider {ProviderId} of an extension was ignored: the id belongs to a built-in provider", candidate.Id);
                }
                else if (usedByMoreThanOne.Contains(candidate.Id))
                {
                    Log.ForContext<SecureKeyRegistry>().Warning("The secure key provider {ProviderId} was ignored: more than one extension uses this id", candidate.Id);
                }
                else if (TryCreate(candidate, out var provider))
                {
                    entries.Add(candidate.Id, new Entry(candidate.Id, candidate.DisplayName, provider));
                    Log.ForContext<SecureKeyRegistry>().Information(
                        "Secure key provider {ProviderId} ({DisplayName}) from {ProviderAssembly}",
                        candidate.Id,
                        candidate.DisplayName,
                        provider.GetType().Assembly.GetName().Name);
                }
            }

            return new SecureKeyRegistry(entries, timeout ?? DefaultTimeout, interactiveTimeout ?? DefaultInteractiveTimeout);
        }

        /// <summary>Returns whether a provider with this id is registered.</summary>
        /// <param name="providerId">The provider id from a project.</param>
        /// <returns><c>true</c> when it is registered.</returns>
        internal bool Contains(string? providerId) => providerId != null && _entries.ContainsKey(providerId);

        /// <summary>
        /// Asks a provider for a key. The call runs on a worker thread without the caller's synchronization context, and
        /// the host stops waiting after the time limit or when <paramref name="cancellationToken"/> is cancelled. The caller
        /// owns a resolved answer and disposes it.
        /// </summary>
        /// <param name="providerId">The provider id from a project. An id nobody registered is
        /// <see cref="SecureKeyFailureReason.ProviderUnavailable"/>: the project asks for a provider this installation does not have.</param>
        /// <param name="request">What to look up.</param>
        /// <param name="cancellationToken">Cancelled when the caller gives up.</param>
        /// <returns>
        /// The answer; never <c>null</c> and never an exception for anything a provider does. A resolved answer always
        /// holds a <see cref="SecureKeyLease"/>.
        /// </returns>
        internal async Task<SecureKeyResolution> ResolveAsync(string? providerId, SecureKeyRequest request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (providerId == null || !_entries.TryGetValue(providerId, out var entry))
            {
                return SecureKeyResolution.Failed(SecureKeyFailureReason.ProviderUnavailable);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return SecureKeyResolution.Failed(SecureKeyFailureReason.Cancelled);
            }

            // Three signals that never share a token. The provider gets its own, which only the host cancels (and only on
            // a worker thread, see Abandon): a callback a provider registers on it can block or throw, and must not be
            // able to delay the host or take down the timer thread. The host waits on its own deadline and on the
            // caller's token, whose callbacks are trivial.
            var providerStop = new CancellationTokenSource();
            var providerToken = providerStop.Token;
            var call = Task.Run(() => entry.Provider.ResolveAsync(request, providerToken).AsTask(), CancellationToken.None);

            var hostStop = new TaskCompletionSource<SecureKeyFailureReason>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var deadline = new CancellationTokenSource(request.Interactive ? _interactiveTimeout : _timeout);
            using var onDeadline = deadline.Token.Register(() => hostStop.TrySetResult(SecureKeyFailureReason.Timeout));
            using var onCaller = cancellationToken.Register(() => hostStop.TrySetResult(SecureKeyFailureReason.Cancelled));

            var first = await Task.WhenAny(call, hostStop.Task).ConfigureAwait(false);

            if (first != call)
            {
                // The provider is still busy. Whatever it hands over later has nobody to receive it.
                Abandon(call, providerStop, entry.Id);

                return SecureKeyResolution.Failed(await hostStop.Task.ConfigureAwait(false));
            }

            providerStop.Dispose();

            return await Settle(call, hostStop.Task, entry.Id).ConfigureAwait(false);
        }

        private static List<Candidate> ReadMetadata(IEnumerable<Lazy<ISecureKeyProvider, ISecureKeyProviderMetadata>> extensions)
        {
            var candidates = new List<Candidate>();

            foreach (var export in extensions)
            {
                string id;
                string displayName;

                try
                {
                    id = export.Metadata.Id;
                    displayName = export.Metadata.DisplayName;
                }
                catch (Exception e)
                {
                    Log.ForContext<SecureKeyRegistry>().Warning(
                        "A secure key provider of an extension was ignored: its metadata cannot be read ({ExceptionType})",
                        e.GetType().FullName);
                    continue;
                }

                if (!SecureKeySyntax.IsValidProviderId(id))
                {
                    Log.ForContext<SecureKeyRegistry>().Warning(
                        "A secure key provider of an extension was ignored: its id is not 1 to {MaxLength} of a-z, 0-9, '.', '_' or '-'",
                        SecureKeySyntax.MaxProviderIdLength);
                    continue;
                }

                candidates.Add(new Candidate(id, string.IsNullOrWhiteSpace(displayName) ? id : displayName, export));
            }

            return candidates;
        }

        private static bool TryCreate(Candidate candidate, out ISecureKeyProvider provider)
        {
            try
            {
                provider = candidate.Export.Value;

                if (provider != null)
                {
                    return true;
                }

                Log.ForContext<SecureKeyRegistry>().Warning("The secure key provider {ProviderId} was ignored: the extension created nothing", candidate.Id);
            }
            catch (Exception e)
            {
                Log.ForContext<SecureKeyRegistry>().Warning(
                    "The secure key provider {ProviderId} was ignored: it could not be created ({ExceptionType})",
                    candidate.Id,
                    e.GetType().FullName);
            }

            provider = null!;
            return false;
        }

        private static async Task<SecureKeyResolution> Settle(
            Task<SecureKeyResolution> call,
            Task<SecureKeyFailureReason> hostStop,
            string providerId)
        {
            SecureKeyResolution? answer;

            try
            {
                answer = await call.ConfigureAwait(false);
            }
            catch (OperationCanceledException e)
            {
                if (hostStop.IsCompleted)
                {
                    return SecureKeyResolution.Failed(hostStop.Result);
                }

                LogProviderFailure(providerId, e);
                return SecureKeyResolution.Failed(SecureKeyFailureReason.ProviderUnavailable);
            }
            catch (Exception e)
            {
                LogProviderFailure(providerId, e);
                return SecureKeyResolution.Failed(SecureKeyFailureReason.ProviderUnavailable);
            }

            if (answer == null)
            {
                Log.ForContext<SecureKeyRegistry>().Warning("The secure key provider {ProviderId} answered with nothing", providerId);
                return SecureKeyResolution.Failed(SecureKeyFailureReason.ProviderUnavailable);
            }

            // The caller gave up, or the deadline passed, while the answer was on its way (both can be true before the
            // host looks): nobody is waiting for it any more, so it is not handed out.
            if (hostStop.IsCompleted)
            {
                DisposeQuietly(answer, providerId);
                return SecureKeyResolution.Failed(hostStop.Result);
            }

            if (!answer.IsResolved || answer.Key is SecureKeyLease)
            {
                return answer;
            }

            Log.ForContext<SecureKeyRegistry>().Warning(
                "The secure key provider {ProviderId} answered with a kind of key this version cannot use ({KeyType})",
                providerId,
                answer.Key?.GetType().FullName);
            DisposeQuietly(answer, providerId);

            return SecureKeyResolution.Failed(SecureKeyFailureReason.Unsupported);
        }

        /// <summary>
        /// The provider has not answered yet and the host stops waiting. It is told to stop, and whatever it hands over
        /// later is disposed. The provider's token is cancelled on a worker thread of its own, and every exception is
        /// caught: a callback the provider registered on that token can block or throw.
        /// </summary>
        private static void Abandon(Task<SecureKeyResolution> call, CancellationTokenSource providerStop, string providerId)
        {
            var stopped = Task.Run(() =>
            {
                try
                {
                    providerStop.Cancel();
                }
                catch (Exception e)
                {
                    LogProviderFailure(providerId, e);
                }
            });

            var late = call.ContinueWith(
                finished => HandleLateAnswer(finished, providerId),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);

            // The source can only be disposed when nothing is going to cancel it any more.
            _ = Task.WhenAll(stopped, late).ContinueWith(
                _ => providerStop.Dispose(),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        private static void HandleLateAnswer(Task<SecureKeyResolution> finished, string providerId)
        {
            if (finished.IsFaulted)
            {
                LogProviderFailure(providerId, finished.Exception?.GetBaseException());
            }
            else if (finished.Status == TaskStatus.RanToCompletion && finished.Result != null)
            {
                DisposeQuietly(finished.Result, providerId);
            }
        }

        /// <summary>
        /// Disposes an answer the host does not hand out. The key may be of a kind written by the provider, whose
        /// <c>Dispose</c> can throw; that must not escape from the registry.
        /// </summary>
        private static void DisposeQuietly(SecureKeyResolution answer, string providerId)
        {
            try
            {
                answer.Dispose();
            }
            catch (Exception e)
            {
                Log.ForContext<SecureKeyRegistry>().Warning(
                    "The secure key provider {ProviderId} threw {ExceptionType} while its key was disposed",
                    providerId,
                    e.GetType().FullName);
            }
        }

        private static void LogProviderFailure(string providerId, Exception? exception) =>
            Log.ForContext<SecureKeyRegistry>().Warning(
                "The secure key provider {ProviderId} failed with {ExceptionType}",
                providerId,
                exception?.GetType().FullName);

        private sealed record Entry(string Id, string DisplayName, ISecureKeyProvider Provider);

        private sealed record Candidate(string Id, string DisplayName, Lazy<ISecureKeyProvider, ISecureKeyProviderMetadata> Export);
    }
}
