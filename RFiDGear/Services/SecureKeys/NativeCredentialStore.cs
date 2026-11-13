#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace RFiDGear.Services.SecureKeys
{
    /// <summary>
    /// The Windows Credential Manager itself (<c>advapi32</c>). Generic credentials only, kept on this computer. The copy of
    /// a secret that the vault hands out is erased before it is given back.
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal sealed class NativeCredentialStore : ICredentialStore
    {
        private const uint CredTypeGeneric = 1;
        private const uint CredPersistLocalMachine = 2;

        // What the Credential Manager shows as the user name of the entry.
        private const string StoredUserName = "RFiDGear";

        // The layout of CREDENTIALW. It is only ever copied to and from the vault, never compared, so IEquatable would be dead code.
        [StructLayout(LayoutKind.Sequential)]
        [SuppressMessage("Major Code Smell", "S3898:Value types should implement IEquatable<T>", Justification = "Interop layout of CREDENTIALW; it is never compared.")]
        internal struct Credential
        {
            public uint Flags;
            public uint Type;
            public IntPtr TargetName;
            public IntPtr Comment;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
            public uint CredentialBlobSize;
            public IntPtr CredentialBlob;
            public uint Persist;
            public uint AttributeCount;
            public IntPtr Attributes;
            public IntPtr TargetAlias;
            public IntPtr UserName;
        }

        [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
        private static extern bool CredRead(string target, uint type, uint flags, out IntPtr credential);

        [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
        private static extern bool CredWrite(ref Credential credential, uint flags);

        [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
        private static extern bool CredDelete(string target, uint type, uint flags);

        [DllImport("advapi32.dll", EntryPoint = "CredEnumerateW", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
        private static extern bool CredEnumerate(string filter, uint flags, out uint count, out IntPtr credentials);

        [DllImport("advapi32.dll", EntryPoint = "CredFree", ExactSpelling = true)]
        private static extern void CredFree(IntPtr buffer);

        /// <inheritdoc />
        public CredentialRead Read(string targetName)
        {
            if (!CredRead(targetName, CredTypeGeneric, 0, out var buffer))
            {
                return new CredentialRead(Marshal.GetLastWin32Error(), null);
            }

            try
            {
                var credential = Marshal.PtrToStructure<Credential>(buffer);
                var size = (int)credential.CredentialBlobSize;

                // On the pinned object heap, like the lease: the garbage collector never moves it, so the caller's erasure
                // reaches the only copy there is.
                var blob = GC.AllocateArray<byte>(size, pinned: true);

                if (size > 0 && credential.CredentialBlob != IntPtr.Zero)
                {
                    Marshal.Copy(credential.CredentialBlob, blob, 0, size);

                    // The vault gave this memory to the caller; the secret does not stay in it after it was freed.
                    Marshal.Copy(new byte[size], 0, credential.CredentialBlob, size);
                }

                return new CredentialRead(0, blob);
            }
            finally
            {
                CredFree(buffer);
            }
        }

        /// <inheritdoc />
        public int Write(string targetName, byte[] blob)
        {
            var target = Marshal.StringToCoTaskMemUni(targetName);
            var user = Marshal.StringToCoTaskMemUni(StoredUserName);
            var pinned = GCHandle.Alloc(blob, GCHandleType.Pinned);

            try
            {
                var credential = new Credential
                {
                    Type = CredTypeGeneric,
                    TargetName = target,
                    CredentialBlobSize = (uint)blob.Length,
                    CredentialBlob = pinned.AddrOfPinnedObject(),
                    Persist = CredPersistLocalMachine,
                    UserName = user,
                };

                return CredWrite(ref credential, 0) ? 0 : Marshal.GetLastWin32Error();
            }
            finally
            {
                pinned.Free();
                Marshal.FreeCoTaskMem(target);
                Marshal.FreeCoTaskMem(user);
            }
        }

        /// <inheritdoc />
        public int Delete(string targetName) =>
            CredDelete(targetName, CredTypeGeneric, 0) ? 0 : Marshal.GetLastWin32Error();

        /// <inheritdoc />
        public CredentialList Enumerate(string targetPrefix)
        {
            // The filter takes a wildcard at its end only. The vault answers NOT_FOUND for no match, which is an empty list here.
            if (!CredEnumerate(targetPrefix + "*", 0, out var count, out var buffer))
            {
                var error = Marshal.GetLastWin32Error();

                return new CredentialList(error == CredentialErrors.NotFound ? 0 : error, Array.Empty<string>());
            }

            try
            {
                var names = new List<string>((int)count);

                for (var index = 0; index < count; index++)
                {
                    // Only the target name is copied out: the secret in the structure is never read.
                    var credential = Marshal.PtrToStructure<Credential>(Marshal.ReadIntPtr(buffer, index * IntPtr.Size));

                    if (credential.Type == CredTypeGeneric && Marshal.PtrToStringUni(credential.TargetName) is { } name)
                    {
                        names.Add(name);
                    }
                }

                return new CredentialList(0, names);
            }
            finally
            {
                // The vault hands out whole credentials, secrets included, in memory that CredFree gives back without erasing it.
                // The secrets are erased here, also when reading a name failed.
                ZeroBlobs(buffer, count);
                CredFree(buffer);
            }
        }

        /// <summary>
        /// Erases the secret of every credential in a listing that the vault handed out, and nothing else: the array of pointers to
        /// the credentials, and the credentials themselves, are left as they are for <c>CredFree</c>.
        /// </summary>
        /// <param name="credentials">The array of pointers to credentials that <c>CredEnumerate</c> returned.</param>
        /// <param name="count">How many credentials there are.</param>
        internal static void ZeroBlobs(IntPtr credentials, uint count)
        {
            for (var index = 0; index < count; index++)
            {
                var credential = Marshal.PtrToStructure<Credential>(Marshal.ReadIntPtr(credentials, index * IntPtr.Size));
                var size = (int)credential.CredentialBlobSize;

                if (size > 0 && credential.CredentialBlob != IntPtr.Zero)
                {
                    Marshal.Copy(new byte[size], 0, credential.CredentialBlob, size);
                }
            }
        }
    }
}
