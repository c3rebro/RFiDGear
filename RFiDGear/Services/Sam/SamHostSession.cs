#nullable enable
using System;
using System.Threading.Tasks;

using Elatec.NET.Interfaces;
using Elatec.NET.Sam;

namespace RFiDGear.Services.Sam
{
    /// <summary>
    /// The host session of a SAM as the setup uses it: what <see cref="SamAv3HostSession"/> offers, without the safeguard
    /// flags, so that the setup can never lift them. The tests replace it with a script; the real one is <see cref="Av3HostSession"/>.
    /// </summary>
    internal interface ISamHostSession : IDisposable
    {
        /// <summary>Whether a host authentication succeeded and nothing has failed since.</summary>
        bool IsAuthenticated { get; }

        /// <summary>Authenticates as host (SAM_AuthenticateHost) with an AES-128 key.</summary>
        /// <param name="keyNo">The key entry of the key.</param>
        /// <param name="keyVersion">The version of the key.</param>
        /// <param name="key">The 16 bytes of the key. The caller erases them.</param>
        /// <returns>A task that ends when the three passes are done.</returns>
        Task AuthenticateHostAsync(byte keyNo, byte keyVersion, byte[] key);

        /// <summary>Activates a new SAM (SAM_LockUnlock, activation). Irreversible.</summary>
        /// <param name="keyNo">The key entry of the key.</param>
        /// <param name="keyVersion">The version of the key.</param>
        /// <param name="key">The 16 bytes of the key. The caller erases them.</param>
        /// <returns>A task that ends when the three passes are done.</returns>
        Task ActivateSamAsync(byte keyNo, byte keyVersion, byte[] key);

        /// <summary>Writes a key entry (SAM_ChangeKeyEntry). The master key entry and the irreversible lock are never allowed.</summary>
        /// <param name="keyEntryNo">The key entry.</param>
        /// <param name="entry">The new content.</param>
        /// <returns>A task that ends when the SAM has answered.</returns>
        Task ChangeKeyEntryAsync(byte keyEntryNo, SamAv3KeyEntry entry);
    }

    /// <summary>The real host session, over <see cref="SamAv3HostSession"/> of Elatec.NET.</summary>
    internal sealed class Av3HostSession : ISamHostSession
    {
        private readonly SamAv3HostSession _session;

        /// <summary>Creates a session on a channel to a SAM.</summary>
        /// <param name="channel">The channel.</param>
        internal Av3HostSession(ISamApduChannel channel)
        {
            _session = new SamAv3HostSession(channel);
        }

        /// <inheritdoc />
        public bool IsAuthenticated => _session.IsAuthenticated;

        /// <inheritdoc />
        public Task AuthenticateHostAsync(byte keyNo, byte keyVersion, byte[] key) =>
            _session.AuthenticateHostAsync(keyNo, keyVersion, key);

        /// <inheritdoc />
        public Task ActivateSamAsync(byte keyNo, byte keyVersion, byte[] key) =>
            _session.ActivateSamAsync(keyNo, keyVersion, key);

        /// <inheritdoc />
        public Task ChangeKeyEntryAsync(byte keyEntryNo, SamAv3KeyEntry entry) =>
            _session.ChangeKeyEntryAsync(keyEntryNo, entry, allowMasterKeyChange: false, allowIrreversibleLock: false);

        /// <inheritdoc />
        public void Dispose() => _session.Dispose();
    }
}
