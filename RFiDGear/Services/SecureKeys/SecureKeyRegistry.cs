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
                    Log.ForContext<SecureKeyRegistry>().Error(e, "The extensions' secure key providers could not be listed; continuing with the built-in ones");
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

            // The token the provider sees is cancelled by the caller and by the time limit alike.
            var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linked.CancelAfter(request.Interactive ? _interactiveTimeout : _timeout);
            var token = linked.Token;

            var call = Task.Run(() => entry.Provider.ResolveAsync(request, token).AsTask(), CancellationToken.None);
            var stopWaiting = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Task first;

            using (token.Register(() => stopWaiting.TrySetResult(true)))
            {
                first = await Task.WhenAny(call, stopWaiting.Task).ConfigureAwait(false);
            }

            if (first != call)
            {
                // The provider is still busy. Whatever it hands over later has nobody to receive it.
                Abandon(call, linked, entry.Id);

                return SecureKeyResolution.Failed(
                    cancellationToken.IsCancellationRequested ? SecureKeyFailureReason.Cancelled : SecureKeyFailureReason.Timeout);
            }

            linked.Dispose();

            return await Settle(call, entry.Id, cancellationToken, token).ConfigureAwait(false);
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
            string providerId,
            CancellationToken callerToken,
            CancellationToken linkedToken)
        {
            SecureKeyResolution? answer;

            try
            {
                answer = await call.ConfigureAwait(false);
            }
            catch (OperationCanceledException e)
            {
                if (callerToken.IsCancellationRequested)
                {
                    return SecureKeyResolution.Failed(SecureKeyFailureReason.Cancelled);
                }

                if (linkedToken.IsCancellationRequested)
                {
                    return SecureKeyResolution.Failed(SecureKeyFailureReason.Timeout);
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

            if (!answer.IsResolved || answer.Key is SecureKeyLease)
            {
                return answer;
            }

            Log.ForContext<SecureKeyRegistry>().Warning(
                "The secure key provider {ProviderId} answered with a kind of key this version cannot use ({KeyType})",
                providerId,
                answer.Key?.GetType().FullName);
            answer.Dispose();

            return SecureKeyResolution.Failed(SecureKeyFailureReason.Unsupported);
        }

        private static void Abandon(Task<SecureKeyResolution> call, CancellationTokenSource linked, string providerId)
        {
            _ = call.ContinueWith(
                finished =>
                {
                    try
                    {
                        if (finished.IsFaulted)
                        {
                            LogProviderFailure(providerId, finished.Exception?.GetBaseException());
                        }
                        else if (finished.Status == TaskStatus.RanToCompletion)
                        {
                            finished.Result?.Dispose();
                        }
                    }
                    finally
                    {
                        linked.Dispose();
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
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
