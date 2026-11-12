using RFiDGear.Contracts;
using RFiDGear.Services.SecureKeys;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RFiDGear.Tests.SecureKeys
{
    /// <summary>
    /// A vault: a provider that answers from a table of keys by slot id, and notes what it was asked. A slot the table does
    /// not know is answered with <see cref="SecureKeyFailureReason.NotFound"/>. The suites of the key-bound tasks use it with
    /// their own tables, so the way a failing or misbehaving provider is simulated is the same everywhere.
    /// </summary>
    internal class SlotVault
    {
        private readonly string _providerId;
        private readonly Func<string, string?> _vaultKeyOf;
        private readonly List<SecureKeyRequest> _requests = new List<SecureKeyRequest>();
        private readonly List<string> _returnedKeys = new List<string>();

        /// <summary>Creates a vault.</summary>
        /// <param name="providerId">The id the vault is registered under.</param>
        /// <param name="vaultKeyOf">Returns the key the vault holds for a slot as hexadecimal text, or <c>null</c> for none.</param>
        /// <param name="keyLength">The length of the keys in bytes. A table entry of another length is repeated or cut to it.</param>
        public SlotVault(string providerId, Func<string, string?> vaultKeyOf, int keyLength)
        {
            _providerId = providerId;
            _vaultKeyOf = vaultKeyOf;
            KeyLength = keyLength;
        }

        /// <summary>Gets or sets a failure every request is answered with; <c>null</c> for none.</summary>
        public SecureKeyFailureReason? Failure { get; set; }

        /// <summary>Gets the slots that are answered with a failure.</summary>
        public HashSet<string> FailSlots { get; } = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>Gets the references that are answered with a failure.</summary>
        public HashSet<string> FailReferences { get; } = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>Gets or sets the length of the keys in bytes.</summary>
        public int KeyLength { get; set; }

        /// <summary>Gets or sets a text that the vault throws instead of answering; <c>null</c> for none.</summary>
        public string? ThrowText { get; set; }

        /// <summary>Gets or sets whether the vault hands out a lease that is disposed already, as a provider with a defect might.</summary>
        public bool ReturnDisposedLease { get; set; }

        /// <summary>Gets or sets what happens when a request arrives.</summary>
        public Action<SecureKeyRequest>? OnRequest { get; set; }

        /// <summary>Gets the requests, in order.</summary>
        public IReadOnlyList<SecureKeyRequest> Requests
        {
            get
            {
                lock (_requests)
                {
                    return _requests.ToList();
                }
            }
        }

        /// <summary>Gets the keys it handed out, as hexadecimal text.</summary>
        public IReadOnlyList<string> ReturnedKeys
        {
            get
            {
                lock (_requests)
                {
                    return _returnedKeys.ToList();
                }
            }
        }

        /// <summary>Creates a registry that holds only this vault.</summary>
        /// <returns>The registry.</returns>
        public SecureKeyRegistry Registry() => SecureKeyRegistry.Create(
            new[] { new SecureKeyProviderRegistration(_providerId, "Test vault", new ScriptedSecureKeyProvider((request, _) => new ValueTask<SecureKeyResolution>(Answer(request)))) },
            Array.Empty<Lazy<ISecureKeyProvider, RFiDGear.ExtensionSdk.ISecureKeyProviderMetadata>>());

        /// <summary>Makes this vault the one that commands without a run ask.</summary>
        /// <param name="runId">The run id of the context.</param>
        /// <param name="taskIndex">The task index of the context.</param>
        /// <param name="cardUid">The card UID of the context.</param>
        /// <param name="resolutionStarted">Called when the first provider is about to be asked.</param>
        /// <param name="resolutionFinished">Called when the last answer is in.</param>
        /// <param name="cancellationToken">The cancellation token of the run.</param>
        public void Install(string? runId = "run-1", string taskIndex = "0", string? cardUid = null, Action? resolutionStarted = null, Action? resolutionFinished = null, CancellationToken cancellationToken = default) =>
            KeyResolutionScope.SetDefault(new KeyResolutionContext(Registry(), runId, taskIndex, cardUid, interactive: false, resolutionStarted, resolutionFinished, cancellationToken));

        private SecureKeyResolution Answer(SecureKeyRequest request)
        {
            lock (_requests)
            {
                _requests.Add(request);
            }

            OnRequest?.Invoke(request);

            if (ThrowText != null)
            {
                throw new InvalidOperationException(ThrowText);
            }

            if (Failure != null)
            {
                return SecureKeyResolution.Failed(Failure.Value);
            }

            var vaultKey = _vaultKeyOf(request.SlotId);

            if (FailSlots.Contains(request.SlotId) || FailReferences.Contains(request.Reference) || vaultKey == null)
            {
                return SecureKeyResolution.Failed(SecureKeyFailureReason.NotFound);
            }

            var bytes = Convert.FromHexString(vaultKey);
            var key = KeyLength == bytes.Length ? bytes : Enumerable.Range(0, KeyLength).Select(i => bytes[i % bytes.Length]).ToArray();

            lock (_requests)
            {
                _returnedKeys.Add(Convert.ToHexString(key));
            }

            var lease = new SecureKeyLease(key);

            if (ReturnDisposedLease)
            {
                lease.Dispose();
            }

            return SecureKeyResolution.Resolved(lease);
        }
    }
}
