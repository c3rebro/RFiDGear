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

        /// <summary>Activates a new SAM (SAM_LockUnlock, activation). Persistent: it survived a removal of the SAM, and no way back is known.</summary>
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

        /// <summary>
        /// Writes a DESFire native TDEA key (DES or 2K3DES, 16 bytes) into a key entry as a key of the PICC key class (SAM_ChangeKeyEntry, SET
        /// <c>0x0000</c>): the library only writes AES-128 entries. Only the entries 1 to 127; the settings are fixed.
        /// </summary>
        /// <param name="keyEntryNo">The key entry, 1 to 127.</param>
        /// <param name="key">The 16 bytes of the key. The caller erases them.</param>
        /// <param name="version">The key version.</param>
        /// <returns>A task that ends when the SAM has answered.</returns>
        Task WriteNativeKeyEntryAsync(byte keyEntryNo, byte[] key, byte version);

        /// <summary>
        /// Reads the settings of a key entry back (SAM_GetKeyEntry under the secure session): what the SAM holds for it, never a key.
        /// Needs a host authentication. An answer that is not what a key entry looks like is <c>null</c>.
        /// </summary>
        /// <param name="keyEntryNo">The key entry.</param>
        /// <returns>The settings, or <c>null</c> for an answer of another length.</returns>
        Task<SamKeyEntryInfo?> ReadKeyEntryAsync(byte keyEntryNo);
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
        public async Task WriteNativeKeyEntryAsync(byte keyEntryNo, byte[] key, byte version)
        {
            // ExecuteSecureAsync has none of the guards of ChangeKeyEntryAsync (the master entry, the lock): they are here. The settings are
            // constants of SamKeyEntryFrame, never a caller's, so no lock bit and no host key can be written through this path.
            if (keyEntryNo < 1 || keyEntryNo > 127)
            {
                throw new ArgumentOutOfRangeException(nameof(keyEntryNo), "Only the key entries 1 to 127 are written.");
            }

            var data = SamKeyEntryFrame.NativeTdeaPiccKey(key, version);

            try
            {
                await _session.ExecuteSecureAsync(0xC1, keyEntryNo, 0xFF, data, null).ConfigureAwait(false);
            }
            finally
            {
                Array.Clear(data, 0, data.Length);
            }
        }

        /// <inheritdoc />
        public async Task<SamKeyEntryInfo?> ReadKeyEntryAsync(byte keyEntryNo)
        {
            // SAM_GetKeyEntry: INS 0x64, P1 = the entry, P2 = 0, Le = 0 (read back from a real MIFARE SAM AV3: 13 bytes and 9000).
            var data = await _session.ExecuteSecureAsync(0x64, keyEntryNo, 0x00, null, 0x00).ConfigureAwait(false);

            return SamKeyEntryInfo.Parse(data);
        }

        /// <inheritdoc />
        public void Dispose() => _session.Dispose();
    }
}
