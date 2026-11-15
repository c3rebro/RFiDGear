using RFiDGear.Contracts;
using RFiDGear.ExtensionSdk;
using RFiDGear.Services.Sam;
using RFiDGear.Services.SecureKeys;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Elatec.NET.Sam;

namespace RFiDGear.Tests.Sam
{
    /// <summary>A host session whose outcome a test scripts; it records what it was asked and the keys it was given.</summary>
    internal sealed class FakeHostSession : ISamHostSession
    {
        /// <summary>What the session was asked, in order, e.g. <c>authenticate 0 0</c>.</summary>
        internal List<string> Calls { get; } = new List<string>();

        /// <summary>A copy of each key at the moment it was passed, in order.</summary>
        internal List<byte[]> KeyCopies { get; } = new List<byte[]>();

        /// <summary>The key arrays themselves, as passed: they must be erased when the step is over.</summary>
        internal List<byte[]> KeyArrays { get; } = new List<byte[]>();

        internal Exception? AuthenticateFails { get; set; }

        internal Exception? ActivateFails { get; set; }

        internal Exception? ChangeFails { get; set; }

        internal byte LastEntryNumber { get; private set; }

        internal SamAv3KeyEntry? LastEntry { get; private set; }

        internal bool Disposed { get; private set; }

        public bool IsAuthenticated { get; set; }

        public Task AuthenticateHostAsync(byte keyNo, byte keyVersion, byte[] key)
        {
            Calls.Add("authenticate " + keyNo + " " + keyVersion);
            Remember(key);

            if (AuthenticateFails != null)
            {
                IsAuthenticated = false;
                throw AuthenticateFails;
            }

            IsAuthenticated = true;
            return Task.CompletedTask;
        }

        public Task ActivateSamAsync(byte keyNo, byte keyVersion, byte[] key)
        {
            Calls.Add("activate " + keyNo + " " + keyVersion);
            Remember(key);
            IsAuthenticated = false;

            if (ActivateFails != null)
            {
                throw ActivateFails;
            }

            return Task.CompletedTask;
        }

        public Task ChangeKeyEntryAsync(byte keyEntryNo, SamAv3KeyEntry entry)
        {
            Calls.Add("change " + keyEntryNo);
            Remember(entry.KeyA);
            LastEntryNumber = keyEntryNo;
            LastEntry = entry;

            if (ChangeFails != null)
            {
                IsAuthenticated = false;
                throw ChangeFails;
            }

            return Task.CompletedTask;
        }

        public void Dispose() => Disposed = true;

        private void Remember(byte[] key)
        {
            KeyArrays.Add(key);
            KeyCopies.Add((byte[])key.Clone());
        }
    }

    /// <summary>A registry that holds one provider under the id of the Credential Manager provider, as the window's registry does.</summary>
    internal static class SamTestKeys
    {
        internal static SecureKeyRegistry RegistryOf(ISecureKeyProvider provider) =>
            SecureKeyRegistry.Create(
                new[] { new SecureKeyProviderRegistration(WindowsCredentialKeyProvider.ProviderId, WindowsCredentialKeyProvider.DisplayName, provider) },
                Array.Empty<Lazy<ISecureKeyProvider, ISecureKeyProviderMetadata>>());
    }

    /// <summary>The questions of the setup, answered by a test.</summary>
    internal sealed class ScriptedSamDialogs : ISamSetupDialogs
    {
        /// <summary>The answer to the activation question.</summary>
        internal bool Agree { get; set; }

        /// <summary>How often the activation was asked about.</summary>
        internal int ActivationAsked { get; private set; }

        public bool ConfirmActivate()
        {
            ActivationAsked++;

            return Agree;
        }
    }
}
