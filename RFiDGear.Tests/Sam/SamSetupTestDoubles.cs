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

        /// <summary>Whether the last write was a native TDEA entry (<see cref="WriteNativeKeyEntryAsync"/>) and not an AES one.</summary>
        internal bool LastWriteWasNative { get; private set; }

        /// <summary>The key version of the last native write.</summary>
        internal byte LastNativeVersion { get; private set; }

        internal bool Disposed { get; private set; }

        public bool IsAuthenticated { get; set; }

        /// <summary>When set, the authentication waits for it before it answers: a step that is still running.</summary>
        internal Task? AuthenticateBlocker { get; set; }

        public async Task AuthenticateHostAsync(byte keyNo, byte keyVersion, byte[] key)
        {
            Calls.Add("authenticate " + keyNo + " " + keyVersion);
            Remember(key);

            if (AuthenticateBlocker != null)
            {
                await AuthenticateBlocker;
            }

            if (AuthenticateFails != null)
            {
                IsAuthenticated = false;
                throw AuthenticateFails;
            }

            IsAuthenticated = true;
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
            LastWriteWasNative = false;

            if (ChangeFails != null)
            {
                IsAuthenticated = false;
                throw ChangeFails;
            }

            return Task.CompletedTask;
        }

        public Task WriteNativeKeyEntryAsync(byte keyEntryNo, byte[] key, byte version)
        {
            Calls.Add("writenative " + keyEntryNo);
            Remember(key);
            LastEntryNumber = keyEntryNo;
            LastNativeVersion = version;
            LastWriteWasNative = true;

            if (ChangeFails != null)
            {
                IsAuthenticated = false;
                throw ChangeFails;
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// What reading a key entry back answers, as the 13 bytes of the answer; <c>null</c> (the default) answers like a SAM that
        /// holds what was written last.
        /// </summary>
        internal byte[]? ReadAnswer { get; set; }

        internal Exception? ReadFails { get; set; }

        public Task<SamKeyEntryInfo?> ReadKeyEntryAsync(byte keyEntryNo)
        {
            Calls.Add("read " + keyEntryNo);

            if (ReadFails != null)
            {
                IsAuthenticated = false;
                throw ReadFails;
            }

            if (ReadAnswer != null)
            {
                return Task.FromResult(SamKeyEntryInfo.Parse(ReadAnswer));
            }

            if (LastWriteWasNative)
            {
                // A SAM that holds a native TDEA entry as SamKeyEntryFrame writes it: SET 0x0000, ExtSET 0x01, no key usage counter.
                return Task.FromResult(SamKeyEntryInfo.Parse(new byte[] { LastNativeVersion, 0, 0, 0, 0, 0, 0, 0, 0, 0xFF, 0x00, 0x00, 0x01 }));
            }

            // A SAM that holds what was written: versions, the entry's own fields and SET and ExtSET as they were written.
            var entry = LastEntry!;
            var answer = new byte[]
            {
                entry.VersionA, entry.VersionA, entry.VersionA, 0, 0, 0, entry.DfKeyNo, entry.KeyNoCek, entry.KeyVersionCek, entry.RefNoKuc,
                (byte)(entry.Set & 0xFF), (byte)(entry.Set >> 8), entry.ExtSet,
            };

            return Task.FromResult(SamKeyEntryInfo.Parse(answer));
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
