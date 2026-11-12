using RFiDGear.Contracts;
using RFiDGear.ExtensionSdk;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RFiDGear.Tests.SecureKeys
{
    /// <summary>A provider whose answers a test scripts, and that remembers what it was asked.</summary>
    internal sealed class ScriptedSecureKeyProvider : ISecureKeyProvider
    {
        private readonly Func<SecureKeyRequest, CancellationToken, ValueTask<SecureKeyResolution>> _answer;
        private readonly List<SecureKeyRequest> _requests = new List<SecureKeyRequest>();

        public ScriptedSecureKeyProvider(Func<SecureKeyRequest, CancellationToken, ValueTask<SecureKeyResolution>> answer)
        {
            _answer = answer ?? throw new ArgumentNullException(nameof(answer));
        }

        /// <summary>Gets the requests the provider has received, in order.</summary>
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

        /// <summary>A provider that hands out a new lease over <paramref name="key"/> for every request.</summary>
        public static ScriptedSecureKeyProvider Returning(byte[] key) =>
            new ScriptedSecureKeyProvider((_, _) => new ValueTask<SecureKeyResolution>(SecureKeyResolution.Resolved(new SecureKeyLease(key))));

        /// <summary>A provider that always answers with the failure <paramref name="reason"/>.</summary>
        public static ScriptedSecureKeyProvider Failing(SecureKeyFailureReason reason) =>
            new ScriptedSecureKeyProvider((_, _) => new ValueTask<SecureKeyResolution>(SecureKeyResolution.Failed(reason)));

        public ValueTask<SecureKeyResolution> ResolveAsync(SecureKeyRequest request, CancellationToken cancellationToken)
        {
            lock (_requests)
            {
                _requests.Add(request);
            }

            return _answer(request, cancellationToken);
        }
    }

    /// <summary>The metadata a registry reads from an export.</summary>
    internal sealed class FakeProviderMetadata : ISecureKeyProviderMetadata
    {
        public FakeProviderMetadata(string id, string displayName)
        {
            Id = id;
            DisplayName = displayName;
        }

        public string Id { get; }

        public string DisplayName { get; }
    }

    /// <summary>Metadata that cannot be read, and says what it was asked to protect.</summary>
    internal sealed class UnreadableProviderMetadata : ISecureKeyProviderMetadata
    {
        public string Id => throw new InvalidOperationException(SecureKeyTestData.Secret);

        public string DisplayName => "Unreadable";
    }

    /// <summary>A kind of key this version of the host does not know.</summary>
    internal sealed class UnknownKindOfKey : ResolvedSecureKey
    {
        private int _disposals;

        /// <summary>Gets how often the key was disposed.</summary>
        public int Disposals => Volatile.Read(ref _disposals);

        protected override void Dispose(bool disposing) => Interlocked.Increment(ref _disposals);
    }

    internal static class SecureKeyTestData
    {
        /// <summary>A text that must never reach a log: tests put it into exception messages.</summary>
        internal const string Secret = "CANARY-SECRET-b2f1c0d9";

        /// <summary>Sixteen different bytes, the length of an AES-128 key.</summary>
        internal static byte[] SixteenBytes() => Enumerable.Range(1, 16).Select(i => (byte)(i * 11)).ToArray();

        internal static SecureKeyRequest Request(bool interactive = false) => new SecureKeyRequest
        {
            SlotId = "desfire.app.current",
            Reference = "cards/main",
            Algorithm = SecureKeyAlgorithm.Aes128,
            Usage = SecureKeyUsage.Authenticate,
            Scope = new SecureKeyScope { ApplicationId = 1, KeyNumber = 0 },
            RunId = "run-1",
            TaskIndex = "3",
            CardUid = "04AABBCCDD",
            Interactive = interactive,
        };
    }
}
