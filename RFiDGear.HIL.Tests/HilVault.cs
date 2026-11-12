#nullable enable
using RFiDGear.Services.SecureKeys;

using Serilog;

using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace RFiDGear.HIL.Tests
{
    /// <summary>
    /// The Windows Credential Manager, as a secure-key hardware case needs it. A key is stored with the code the application
    /// uses to store one (<see cref="WindowsCredentialKeyProvider.Store"/>), under a reference that belongs to this object
    /// (<c>hil/&lt;run&gt;/&lt;name&gt;</c>, so the credential is <c>RFiDGear/key/hil/...</c>), and every credential that this object
    /// stored is removed when it is disposed, also when the case failed. A credential that it did not store is never touched.
    /// The keys are test keys for a test application on a card that may be spared; they are not secrets.
    /// </summary>
    internal sealed class HilVault : IDisposable
    {
        private readonly WindowsCredentialKeyProvider _provider;
        private readonly List<string> _stored = new List<string>();
        private readonly string _run = Guid.NewGuid().ToString("N").Substring(0, 12);

        /// <summary>Creates the vault.</summary>
        /// <param name="store">The store to use; the Windows Credential Manager when there is none. Only a self-check gives one.</param>
        public HilVault(ICredentialStore? store = null)
        {
            _provider = new WindowsCredentialKeyProvider(store ?? new NativeCredentialStore());
        }

        /// <summary>Stores a key under a new reference.</summary>
        /// <param name="name">What the key is for; part of the reference.</param>
        /// <param name="keyHex">The key as hexadecimal digits.</param>
        /// <returns>The reference.</returns>
        /// <exception cref="InvalidOperationException">The Credential Manager refused the key.</exception>
        public string Store(string name, string keyHex)
        {
            var reference = ReferenceOf(name);
            var key = GC.AllocateArray<byte>(keyHex.Length / 2, pinned: true);

            try
            {
                Convert.FromHexString(keyHex, key, out _, out _);
                var error = _provider.Store(reference, key);

                if (error != 0)
                {
                    throw new InvalidOperationException($"The Credential Manager refused the test key '{name}' (Win32 error {error}).");
                }

                _stored.Add(reference);

                return reference;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
            }
        }

        /// <summary>Makes a reference for a credential that does not exist: nothing is stored under it.</summary>
        /// <param name="name">What the reference is for.</param>
        /// <returns>The reference.</returns>
        public string NeverStored(string name) => ReferenceOf(name);

        /// <inheritdoc />
        public void Dispose()
        {
            var notRemoved = new List<string>();

            foreach (var reference in _stored)
            {
                var error = _provider.Remove(reference);

                if (error != 0 && error != CredentialErrors.NotFound)
                {
                    notRemoved.Add(WindowsCredentialKeyProvider.TargetPrefix + reference);
                }
            }

            _stored.Clear();

            if (notRemoved.Count > 0)
            {
                // A test must not throw from Dispose; the log of the run names what is left, for removing by hand.
                Log.Warning("Test credentials could not be removed from the Credential Manager (cmdkey /delete:<name>): {Names}", string.Join(", ", notRemoved));
            }
        }

        private string ReferenceOf(string name) => $"hil/{_run}/{name}";
    }
}
