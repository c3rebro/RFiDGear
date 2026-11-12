#nullable enable
using System;
using System.Security.Cryptography;
using RFiDGear.Contracts;

namespace RFiDGear.Services.SecureKeys
{
    /// <summary>
    /// How a key is kept in a generic credential: the hexadecimal digits of the key as UTF-16 text, two bytes per digit,
    /// the way <c>cmdkey /generic:... /pass:...</c> stores a password. The conversion works on the bytes and never makes a
    /// string, because a string cannot be erased.
    /// </summary>
    internal static class CredentialBlob
    {
        private const string UpperHex = "0123456789ABCDEF";

        /// <summary>
        /// Turns the blob into a key. Anything that is not an even number of hexadecimal digits in UTF-16 is refused, and so
        /// is a blob that would make a key longer than <paramref name="scratch"/>; what the refused blob contained is not
        /// reported. The key is put together in <paramref name="scratch"/> and erased there before the method returns, whether
        /// it is a key or not.
        /// </summary>
        /// <param name="blob">The secret as the vault holds it. The caller erases it.</param>
        /// <param name="scratch">Room for the key; its length is the longest key that is accepted.</param>
        /// <returns>A lease over the key, or <c>null</c> when the blob is not a key.</returns>
        internal static SecureKeyLease? ToLease(ReadOnlySpan<byte> blob, Span<byte> scratch)
        {
            // Four bytes of blob make one byte of key: two digits of two bytes.
            if (blob.Length == 0 || blob.Length % 4 != 0 || blob.Length / 4 > scratch.Length)
            {
                return null;
            }

            var key = scratch.Slice(0, blob.Length / 4);

            try
            {
                for (var i = 0; i < key.Length; i++)
                {
                    var high = HexValue(blob[4 * i], blob[4 * i + 1]);
                    var low = HexValue(blob[4 * i + 2], blob[4 * i + 3]);

                    if (high < 0 || low < 0)
                    {
                        return null;
                    }

                    key[i] = (byte)((high << 4) | low);
                }

                return new SecureKeyLease(key);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
            }
        }

        /// <summary>Makes the blob of a key. The caller erases it after use.</summary>
        /// <param name="key">The key.</param>
        /// <returns>The upper-case hexadecimal digits of the key as UTF-16 text.</returns>
        internal static byte[] FromKey(ReadOnlySpan<byte> key)
        {
            var blob = new byte[key.Length * 4];

            for (var i = 0; i < key.Length; i++)
            {
                blob[4 * i] = (byte)UpperHex[key[i] >> 4];
                blob[4 * i + 2] = (byte)UpperHex[key[i] & 0x0F];
            }

            return blob;
        }

        // A character is two bytes, low first. A hexadecimal digit has a high byte of 0.
        private static int HexValue(byte low, byte high)
        {
            if (high != 0)
            {
                return -1;
            }

            return low switch
            {
                >= (byte)'0' and <= (byte)'9' => low - '0',
                >= (byte)'a' and <= (byte)'f' => low - 'a' + 10,
                >= (byte)'A' and <= (byte)'F' => low - 'A' + 10,
                _ => -1,
            };
        }
    }
}
