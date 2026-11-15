#nullable enable
using System;
using System.Security;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

using RFiDGear.Contracts;
using RFiDGear.Services.SecureKeys;

using Serilog;

namespace RFiDGear.Services.Sam
{
    /// <summary>Where a key of the SAM setup comes from. It describes the source and never holds a key.</summary>
    internal sealed class SamKeySource
    {
        private readonly SecureString? _typed;
        private readonly string? _reference;

        private SamKeySource(Kind kind, SecureString? typed, string? reference)
        {
            SourceKind = kind;
            _typed = typed;
            _reference = reference;
        }

        /// <summary>The ways to name a key.</summary>
        internal enum Kind
        {
            /// <summary>The key of a new SAM: sixteen bytes of zero.</summary>
            Factory,

            /// <summary>Typed into a key box; the caller owns the secure string and disposes it after the step.</summary>
            Typed,

            /// <summary>Stored in the Credential Manager under a reference.</summary>
            Stored,
        }

        /// <summary>The way this key is named.</summary>
        internal Kind SourceKind { get; }

        /// <summary>The typed digits, for <see cref="Kind.Typed"/>.</summary>
        internal SecureString? Typed => _typed;

        /// <summary>The reference of the stored key, for <see cref="Kind.Stored"/>.</summary>
        internal string? Reference => _reference;

        /// <summary>The key a new SAM has, sixteen bytes of zero.</summary>
        internal static SamKeySource Factory() => new SamKeySource(Kind.Factory, null, null);

        /// <summary>A key that was typed.</summary>
        /// <param name="entered">What was typed; the caller disposes it after the step.</param>
        /// <returns>The source.</returns>
        internal static SamKeySource FromTyped(SecureString entered) => new SamKeySource(Kind.Typed, entered, null);

        /// <summary>A key that is stored in the Credential Manager.</summary>
        /// <param name="reference">The reference it is stored under.</param>
        /// <returns>The source.</returns>
        internal static SamKeySource FromStored(string reference) => new SamKeySource(Kind.Stored, null, reference);

        /// <inheritdoc />
        public override string ToString() => "SamKeySource [" + SourceKind + "]";
    }

    /// <summary>Sixteen bytes of a key in memory that is pinned and erased when the buffer is disposed. Never logged, never shown.</summary>
    internal sealed class SamKeyBuffer : IDisposable
    {
        /// <summary>The length of an AES-128 key.</summary>
        internal const int Length = 16;

        private readonly byte[] _bytes = GC.AllocateArray<byte>(Length, pinned: true);

        /// <summary>The key. Do not copy it into memory that outlives the step.</summary>
        internal byte[] Bytes => _bytes;

        /// <summary>Copies a key into the buffer.</summary>
        /// <param name="key">Exactly <see cref="Length"/> bytes.</param>
        internal void Fill(ReadOnlySpan<byte> key) => key.CopyTo(_bytes);

        /// <inheritdoc />
        public void Dispose() => CryptographicOperations.ZeroMemory(_bytes);

        /// <inheritdoc />
        public override string ToString() => "SamKeyBuffer [redacted]";
    }

    /// <summary>What reading a key came to: the key, or the reason there is none. Dispose it to erase the key.</summary>
    internal sealed class SamKeyRead : IDisposable
    {
        private SamKeyRead(SamKeyBuffer? key, SamStepResult? failure)
        {
            Key = key;
            Failure = failure;
        }

        /// <summary>The key, or <c>null</c> when there is none.</summary>
        internal SamKeyBuffer? Key { get; }

        /// <summary>Why there is no key, or <c>null</c> when there is one.</summary>
        internal SamStepResult? Failure { get; }

        internal static SamKeyRead Of(SamKeyBuffer key) => new SamKeyRead(key, null);

        internal static SamKeyRead Failed(SamStepResult failure) => new SamKeyRead(null, failure);

        /// <inheritdoc />
        public void Dispose() => Key?.Dispose();

        /// <inheritdoc />
        public override string ToString() => "SamKeyRead [redacted]";
    }

    /// <summary>
    /// Turns a <see cref="SamKeySource"/> into the sixteen bytes a SAM step needs, in a buffer the caller erases. A key
    /// that is not there, or is not an AES-128 key, ends the step before the card is touched.
    /// </summary>
    internal sealed class SamKeyReader
    {
        private readonly ISecureKeyProvider _provider;

        /// <summary>Creates a reader that takes stored keys from a provider.</summary>
        /// <param name="provider">The provider of the stored keys (the Credential Manager).</param>
        internal SamKeyReader(ISecureKeyProvider provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        /// <summary>Reads the key of a source.</summary>
        /// <param name="source">Where the key comes from.</param>
        /// <param name="usage">What the key is for; the provider sees it.</param>
        /// <param name="slotId">The slot of the key, <c>sam.master</c> or <c>sam.newkey</c>; the provider sees it.</param>
        /// <returns>The key, or the reason there is none. The caller disposes it.</returns>
        internal async Task<SamKeyRead> ReadAsync(SamKeySource source, SecureKeyUsage usage, string slotId)
        {
            switch (source.SourceKind)
            {
                case SamKeySource.Kind.Factory:
                    return SamKeyRead.Of(new SamKeyBuffer());

                case SamKeySource.Kind.Typed:
                    return ReadTyped(source.Typed);

                case SamKeySource.Kind.Stored:
                    return await ReadStoredAsync(source.Reference, usage, slotId).ConfigureAwait(false);

                default:
                    return SamKeyRead.Failed(SamStepResult.KeyUnavailable);
            }
        }

        private static SamKeyRead ReadTyped(SecureString? entered)
        {
            Span<byte> scratch = stackalloc byte[SamKeyBuffer.Length];

            try
            {
                if (KeyEntry.Read(entered, 2 * SamKeyBuffer.Length, scratch) != SamKeyBuffer.Length)
                {
                    return SamKeyRead.Failed(SamStepResult.InvalidKey);
                }

                var key = new SamKeyBuffer();
                key.Fill(scratch);

                return SamKeyRead.Of(key);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(scratch);
            }
        }

        private async Task<SamKeyRead> ReadStoredAsync(string? reference, SecureKeyUsage usage, string slotId)
        {
            if (string.IsNullOrEmpty(reference))
            {
                return SamKeyRead.Failed(SamStepResult.KeyUnavailable);
            }

            SecureKeyResolution resolution;

            try
            {
                resolution = await _provider.ResolveAsync(
                    new SecureKeyRequest
                    {
                        SlotId = slotId,
                        Reference = reference,
                        Algorithm = SecureKeyAlgorithm.Aes128,
                        Usage = usage,
                        Interactive = true,
                    },
                    CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                // A provider that throws is a provider that cannot answer. Only the type is logged, never its text.
                Log.ForContext<SamKeyReader>().Warning("SAM key: the provider failed ({ExceptionType}).", e.GetType().Name);
                return SamKeyRead.Failed(SamStepResult.KeyUnavailable);
            }

            using (resolution)
            {
                if (!(resolution.Key is SecureKeyLease lease))
                {
                    Log.ForContext<SamKeyReader>().Information("SAM key: no key ({Reason}).", resolution.FailureReason);
                    return SamKeyRead.Failed(SamStepResult.KeyUnavailable);
                }

                if (lease.Length != SamKeyBuffer.Length)
                {
                    return SamKeyRead.Failed(SamStepResult.InvalidKey);
                }

                var key = new SamKeyBuffer();
                key.Fill(lease.Key);

                return SamKeyRead.Of(key);
            }
        }
    }
}
