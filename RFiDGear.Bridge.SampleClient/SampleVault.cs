#nullable enable
using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace RFiDGear.Bridge.SampleClient
{
    /// <summary>
    /// The smallest vault that keeps the rules of the client guide: it serves <b>one</b> reference with <b>one</b> key, answers
    /// every other reference with "not authorized" (any program of the user can name any reference in a project, so the vault
    /// checks), and erases its copy of the key when it is disposed. A real vault looks up the key of the reference in its store; the
    /// shape of the answers is the same.
    /// </summary>
    public sealed class SampleVault : IDisposable
    {
        private readonly string _reference;
        private readonly byte[] _key;
        private readonly object _gate = new object();
        private bool _disposed;
        private int _served;

        /// <summary>Creates a vault that serves a key. It keeps a copy of <paramref name="key"/>; erase your own.</summary>
        /// <param name="reference">The reference a project names for the key.</param>
        /// <param name="key">The key, 1 to <see cref="BridgeProtocol.MaxKeyBytes"/> bytes.</param>
        public SampleVault(string reference, ReadOnlySpan<byte> key)
        {
            if (string.IsNullOrEmpty(reference))
            {
                throw new ArgumentException("A reference is needed.", nameof(reference));
            }

            if (key.IsEmpty || key.Length > BridgeProtocol.MaxKeyBytes)
            {
                throw new ArgumentOutOfRangeException(nameof(key), "A key has 1 to " + BridgeProtocol.MaxKeyBytes + " bytes.");
            }

            _reference = reference;
            _key = key.ToArray();
        }

        /// <summary>Gets how many keys this vault handed out; for a program that wants to tell its operator.</summary>
        public int Served
        {
            get
            {
                lock (_gate)
                {
                    return _served;
                }
            }
        }

        /// <summary>Answers the host's request for a key. Assign it to <see cref="BridgeSampleClient.KeyVault"/>.</summary>
        /// <param name="request">Which key, and what for.</param>
        /// <param name="cancellationToken">Cancelled when the host stops waiting.</param>
        /// <returns>The key when the reference is the one this vault serves, "not authorized" for every other.</returns>
        public Task<ResolveKeyResponse> ResolveAsync(ResolveKeyRequest request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();

            // The check, the encoding and the erasing share one lock: a request gets the whole key or is refused, never a key that
            // is being erased.
            lock (_gate)
            {
                if (_disposed || !string.Equals(request.Reference, _reference, StringComparison.Ordinal))
                {
                    return Task.FromResult(new ResolveKeyResponse { Result = KeyResolveResults.NotAuthorized });
                }

                _served++;

                return Task.FromResult(new ResolveKeyResponse { Result = KeyResolveResults.Resolved, Key = Convert.ToBase64String(_key) });
            }
        }

        /// <summary>Erases the copy of the key; the vault then refuses everything.</summary>
        public void Dispose()
        {
            lock (_gate)
            {
                if (!_disposed)
                {
                    _disposed = true;
                    CryptographicOperations.ZeroMemory(_key);
                }
            }
        }
    }
}
