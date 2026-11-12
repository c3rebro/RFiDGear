#nullable enable
using System;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;

namespace RFiDGear.Services.SecureKeys
{
    /// <summary>
    /// Turns what a person typed into a password box into a key. The text is read from the secure string and never becomes
    /// a managed string, because a string cannot be erased.
    /// </summary>
    internal static class KeyEntry
    {
        /// <summary>
        /// Reads a key from the hexadecimal digits that were entered. Only exactly <paramref name="hexLength"/> digits
        /// (0-9, A-F, either case) are a key: no spaces, no separators. When the entry is not a key nothing stays in
        /// <paramref name="key"/>.
        /// </summary>
        /// <param name="entered">What was entered; may be <c>null</c>.</param>
        /// <param name="hexLength">How many digits the key has; even.</param>
        /// <param name="key">Room for the key; at least <c>hexLength / 2</c> bytes. The caller erases it.</param>
        /// <returns>The length of the key in bytes, or <c>0</c> when the entry is not a key.</returns>
        internal static int Read(SecureString? entered, int hexLength, Span<byte> key)
        {
            if (entered == null || hexLength <= 0 || hexLength % 2 != 0 || entered.Length != hexLength || key.Length < hexLength / 2)
            {
                return 0;
            }

            var text = IntPtr.Zero;

            try
            {
                text = Marshal.SecureStringToGlobalAllocUnicode(entered);

                for (var i = 0; i < hexLength / 2; i++)
                {
                    var high = HexValue((char)Marshal.ReadInt16(text, 4 * i));
                    var low = HexValue((char)Marshal.ReadInt16(text, 4 * i + 2));

                    if (high < 0 || low < 0)
                    {
                        CryptographicOperations.ZeroMemory(key);
                        return 0;
                    }

                    key[i] = (byte)((high << 4) | low);
                }

                return hexLength / 2;
            }
            finally
            {
                if (text != IntPtr.Zero)
                {
                    Marshal.ZeroFreeGlobalAllocUnicode(text);
                }
            }
        }

        private static int HexValue(char c) => c switch
        {
            >= '0' and <= '9' => c - '0',
            >= 'a' and <= 'f' => c - 'a' + 10,
            >= 'A' and <= 'F' => c - 'A' + 10,
            _ => -1,
        };
    }
}
