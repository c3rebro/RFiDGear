#nullable enable
using System;

namespace RFiDGear.Services.Sam
{
    /// <summary>The kind of key that a SAM key entry holds for a DESFire card.</summary>
    internal enum SamEntryKind
    {
        /// <summary>An AES-128 key (SET key type <c>100b</c>, with Keep IV): a DESFire card whose key is AES.</summary>
        Aes128,

        /// <summary>
        /// A DESFire native TDEA key (SET key type <c>000b</c>, no Keep IV): 16 bytes, DES (two equal halves) or 2K3DES. The factory key of a new
        /// DESFire card is one.
        /// </summary>
        NativeTdea,
    }

    /// <summary>
    /// The data field of SAM_ChangeKeyEntry for the entries that Elatec.NET cannot write: its <c>ChangeKeyEntryAsync</c> throws
    /// <see cref="NotSupportedException"/> for everything but an AES-128 entry. The layout was compared with the frame of the library for an AES entry
    /// on 2026-11-15 (same session keys, same counter): equal byte for byte. It is <b>not</b> the order of the answer to SAM_GetKeyEntry.
    /// </summary>
    internal static class SamKeyEntryFrame
    {
        /// <summary>The length of the data field: three keys of 16 bytes and 13 bytes of settings.</summary>
        internal const int Length = 61;

        // SET key type 000b (DESFire native TDEA): DES and 2K3DES, 16 bytes, no Keep IV (NXP AN12696, table 1). Nothing else is set:
        // the entry is enabled (bit 9), not locked (bit 10), not a host key (bit 8), and SAM_ChangeKeyPICC is allowed (bit 11).
        private const ushort NativeTdeaSet = 0x0000;

        // ExtSET: key class PICC (b2..b0 = 001b).
        private const byte PiccExtSet = 0x01;

        // Key usage counter: none.
        private const byte NoKeyUsageCounter = 0xFF;

        /// <summary>Builds the data field for a native TDEA key of the PICC key class: SET <c>0x0000</c>, ExtSET <c>0x01</c>, no key usage counter.</summary>
        /// <param name="key">The 16 bytes of the key.</param>
        /// <param name="version">The key version (of all three slots, as the library does).</param>
        /// <returns>The 61 bytes, which hold the key three times: the caller erases them.</returns>
        internal static byte[] NativeTdeaPiccKey(byte[] key, byte version) => Build(key, version, NativeTdeaSet, PiccExtSet);

        /// <summary>Builds the data field for any SET and ExtSET (for the test that compares it with the library's frame).</summary>
        /// <param name="key">The 16 bytes of the key.</param>
        /// <param name="version">The key version.</param>
        /// <param name="set">The SET word.</param>
        /// <param name="extSet">The ExtSET byte.</param>
        /// <returns>The 61 bytes, which hold the key three times: the caller erases them.</returns>
        internal static byte[] Build(byte[] key, byte version, ushort set, byte extSet)
        {
            if (key == null || key.Length != 16)
            {
                throw new ArgumentException("The key has 16 bytes.", nameof(key));
            }

            var data = new byte[Length];

            // Key A, key B, key C: the library puts a copy of key A into B and C.
            Buffer.BlockCopy(key, 0, data, 0, 16);
            Buffer.BlockCopy(key, 0, data, 16, 16);
            Buffer.BlockCopy(key, 0, data, 32, 16);

            // DF_AID (3), DF_KeyNo, KeyNoCEK, KeyVCEK: not used (0). KeyNoCEK 0 = the master key changes the entry.
            data[54] = NoKeyUsageCounter;
            data[55] = (byte)(set & 0xFF);
            data[56] = (byte)(set >> 8);
            // The versions of A, B and C: the library gives all three the version of the key, as it gives all three slots the key.
            data[57] = version;
            data[58] = version;
            data[59] = version;

            data[60] = extSet;

            return data;
        }
    }
}