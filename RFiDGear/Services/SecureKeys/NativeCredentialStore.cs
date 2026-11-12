#nullable enable
using System;
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

        [StructLayout(LayoutKind.Sequential)]
        private struct Credential
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
                var blob = new byte[size];

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
    }
}
