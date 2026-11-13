#nullable enable
using System;
using System.Security;
using System.Security.Cryptography;

namespace RFiDGear.Services.SecureKeys
{
    /// <summary>
    /// Stores a key that a person entered in the Windows Credential Manager. This is the one place where the digits of an entry
    /// become a key: the editors of the task dialogs and the key manager of the menu both come here, so the care taken is the same.
    /// </summary>
    internal static class KeyStorage
    {
        /// <summary>
        /// Stores the key under a reference. The key is read from the entry without becoming a string and is erased afterwards;
        /// nothing here keeps it, and nothing here logs it. The caller owns the entry and disposes and clears it.
        /// </summary>
        /// <param name="keyStore">The Credential Manager provider.</param>
        /// <param name="reference">The reference, which has to be valid.</param>
        /// <param name="hexLength">How many hexadecimal digits the key has.</param>
        /// <param name="entered">The digits as they were entered.</param>
        /// <returns>What came of it.</returns>
        internal static KeySlotStoreResult Store(WindowsCredentialKeyProvider keyStore, string reference, int hexLength, SecureString entered)
        {
            ArgumentNullException.ThrowIfNull(keyStore);
            ArgumentNullException.ThrowIfNull(reference);
            ArgumentNullException.ThrowIfNull(entered);

            Span<byte> key = stackalloc byte[WindowsCredentialKeyProvider.MaxKeyBytes];

            try
            {
                var length = KeyEntry.Read(entered, hexLength, key);

                if (length == 0)
                {
                    return new KeySlotStoreResult(KeySlotMessage.KeyInvalid, hexLength);
                }

                var error = keyStore.Store(reference, key.Slice(0, length));

                return error == 0
                    ? new KeySlotStoreResult(KeySlotMessage.KeyStored, 0)
                    : new KeySlotStoreResult(KeySlotMessage.KeyNotStored, error);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
            }
        }
    }
}
