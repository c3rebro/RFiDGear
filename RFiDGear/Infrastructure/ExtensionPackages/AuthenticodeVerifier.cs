#nullable enable

using System;
using System.Runtime.InteropServices;

namespace RFiDGear.Infrastructure.ExtensionPackages
{
    /// <summary>What the check of the Authenticode signature of a file found.</summary>
    internal enum SignatureCheck
    {
        /// <summary>The file has no signature (or its type has none).</summary>
        Unsigned,

        /// <summary>The file has a signature that belongs to its content and chains to a root that this PC trusts.</summary>
        Valid,

        /// <summary>The file has a signature that does not hold: it does not belong to the content (the file was changed), its certificate is not trusted, or it is expired or revoked.</summary>
        Invalid,
    }

    /// <summary>
    /// Checks the Authenticode signature of a file with the operating system (<c>WinVerifyTrust</c>), the way Windows itself does
    /// for a signed program: the signature over the file, and the chain of the certificate. It never shows a window of its own and
    /// never goes to the network for a revocation list (only what the PC has cached is used), so it cannot hang.
    /// </summary>
    internal static class AuthenticodeVerifier
    {
        // WINTRUST_ACTION_GENERIC_VERIFY_V2: the check that a signed program gets.
        private static readonly Guid GenericVerifyAction = new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

        private const uint UiNone = 2;
        private const uint RevokeNone = 0;
        private const uint ChoiceFile = 1;
        private const uint StateActionIgnore = 0;
        private const uint CacheOnlyUrlRetrieval = 0x1000;

        // The answers that mean "there is nothing to verify" and not "there is something wrong": no signature, a provider or a
        // type of file that does not know signatures.
        private const int NoSignature = unchecked((int)0x800B0100);
        private const int ProviderUnknown = unchecked((int)0x800B0001);
        private const int SubjectFormUnknown = unchecked((int)0x800B0003);

        /// <summary>Checks the signature of a file.</summary>
        /// <param name="path">The full path of the file.</param>
        /// <returns>What was found; a file that cannot be checked is not <see cref="SignatureCheck.Valid"/>.</returns>
        internal static SignatureCheck Check(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("There is no file to check.", nameof(path));
            }

            var pathText = IntPtr.Zero;
            var fileInfoBlock = IntPtr.Zero;
            var dataBlock = IntPtr.Zero;
            try
            {
                pathText = Marshal.StringToHGlobalUni(path);
                var fileInfo = new FileInfoBlock
                {
                    Size = (uint)Marshal.SizeOf<FileInfoBlock>(),
                    FilePath = pathText,
                    FileHandle = IntPtr.Zero,
                    KnownSubject = IntPtr.Zero,
                };
                fileInfoBlock = Marshal.AllocHGlobal(Marshal.SizeOf<FileInfoBlock>());
                Marshal.StructureToPtr(fileInfo, fileInfoBlock, false);

                var data = new DataBlock
                {
                    Size = (uint)Marshal.SizeOf<DataBlock>(),
                    PolicyCallbackData = IntPtr.Zero,
                    SipClientData = IntPtr.Zero,
                    UiChoice = UiNone,
                    RevocationChecks = RevokeNone,
                    UnionChoice = ChoiceFile,
                    File = fileInfoBlock,
                    StateAction = StateActionIgnore,
                    StateData = IntPtr.Zero,
                    UrlReference = IntPtr.Zero,
                    ProviderFlags = CacheOnlyUrlRetrieval,
                    UiContext = 0,
                    SignatureSettings = IntPtr.Zero,
                };
                dataBlock = Marshal.AllocHGlobal(Marshal.SizeOf<DataBlock>());
                Marshal.StructureToPtr(data, dataBlock, false);

                // A window handle of -1 says "no window".
                var action = GenericVerifyAction;
                var answer = WinVerifyTrust(new IntPtr(-1), ref action, dataBlock);
                return Classify(answer);
            }
            finally
            {
                FreeBlock(dataBlock);
                FreeBlock(fileInfoBlock);
                FreeBlock(pathText);
            }
        }

        // 0 is "valid and trusted"; the few answers that mean "nothing to verify" are unsigned; anything else is a signature that
        // does not hold (the digest of the content does not match, the chain is not trusted, the certificate expired).
        internal static SignatureCheck Classify(int answer) => answer switch
        {
            0 => SignatureCheck.Valid,
            NoSignature or ProviderUnknown or SubjectFormUnknown => SignatureCheck.Unsigned,
            _ => SignatureCheck.Invalid,
        };

        private static void FreeBlock(IntPtr block)
        {
            if (block != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(block);
            }
        }

        // The operating system's own check, called once above and nowhere else.
        [DllImport("wintrust.dll", ExactSpelling = true, SetLastError = false)]
        private static extern int WinVerifyTrust(IntPtr window, ref Guid action, IntPtr data);

        // WINTRUST_FILE_INFO (a formatted class: the layout is that of the structure, and a class needs no equality of its own)
        [StructLayout(LayoutKind.Sequential)]
        private sealed class FileInfoBlock
        {
            internal uint Size;
            internal IntPtr FilePath;
            internal IntPtr FileHandle;
            internal IntPtr KnownSubject;
        }

        // WINTRUST_DATA (as above)
        [StructLayout(LayoutKind.Sequential)]
        private sealed class DataBlock
        {
            internal uint Size;
            internal IntPtr PolicyCallbackData;
            internal IntPtr SipClientData;
            internal uint UiChoice;
            internal uint RevocationChecks;
            internal uint UnionChoice;
            internal IntPtr File;
            internal uint StateAction;
            internal IntPtr StateData;
            internal IntPtr UrlReference;
            internal uint ProviderFlags;
            internal uint UiContext;
            internal IntPtr SignatureSettings;
        }
    }
}
