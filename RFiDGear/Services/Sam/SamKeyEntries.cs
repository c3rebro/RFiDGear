#nullable enable
using Elatec.NET.Sam;

namespace RFiDGear.Services.Sam
{
    /// <summary>The key entries that the setup writes into a SAM.</summary>
    internal static class SamKeyEntries
    {
        /// <summary>
        /// SET bit 2, "Keep IV". NXP AN12696, table 1, asks for it in the entry of every DESFire key that is not a native TDEA key (AES-128,
        /// 3TDEA, TDEA ISO 10116) and says that "the incorrect setting will result to authentication error". Without it the SAM accepts
        /// the key and the card accepts the SAM's first message, and SAM_AuthenticatePICC part 2 then answers <c>901E</c>
        /// (seen on a real SAM AV3 with a DESFire EV3, 2026-11-16).
        /// </summary>
        internal const ushort KeepIvBit = 0x0004;

        /// <summary>
        /// An AES-128 key of the PICC key class as a DESFire card needs it: SET <c>0x0024</c> (AES-128 and Keep IV), ExtSET <c>0x01</c>,
        /// no key usage counter. The helper of Elatec.NET writes <c>0x0020</c>, which a DESFire card cannot be authenticated with.
        /// </summary>
        /// <param name="key">The 16 bytes of the key.</param>
        /// <param name="version">The key version.</param>
        /// <returns>The entry to write.</returns>
        internal static SamAv3KeyEntry Aes128PiccKey(byte[] key, byte version)
        {
            var entry = SamAv3KeyEntry.CreateAes128PiccKey(key, version);
            entry.Set = (ushort)(entry.Set | KeepIvBit);

            return entry;
        }
    }
}