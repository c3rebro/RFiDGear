#nullable enable
using System;

namespace RFiDGear.Services.Sam
{
    /// <summary>What reading a key entry back from the SAM said about the entry it was asked to write.</summary>
    internal enum SamKeyEntryCheck
    {
        /// <summary>The entry was not read back (the step does not write a key entry, or it did not get that far).</summary>
        NotChecked,

        /// <summary>The entry is enabled, an AES-128 key, and of the PICC key class: what was written.</summary>
        Matches,

        /// <summary>The entry is still disabled (SET bit 9): the key cannot be used, whatever else it holds.</summary>
        Disabled,

        /// <summary>The entry is enabled, but it is not an AES-128 key of the PICC key class.</summary>
        WrongKind,

        /// <summary>
        /// The entry is an enabled AES-128 key of the PICC key class, but SET bit 2 (Keep IV) is clear: a DESFire card cannot be authenticated
        /// with it (SAM_AuthenticatePICC part 2 answers <c>901E</c>). Write the key again.
        /// </summary>
        MissingKeepIv,

        /// <summary>The key entry was written, but reading it back failed, so it is not verified.</summary>
        Unreadable,
    }

    /// <summary>
    /// What SAM_GetKeyEntry (<c>80 64 entry 00 00</c>, under the secure session) says about a key entry: its settings, and no key.
    /// </summary>
    /// <remarks>
    /// The 13 bytes of the answer were read back from a real MIFARE SAM AV3 and match what was written to it: key versions A, B and
    /// C (3 bytes), DF_AID (3), DF_KeyNo, KeyNoCEK, KeyVCEK, RefNoKUC, SET (2 bytes, low byte first) and ExtSET. The order is
    /// inferred from the values that were written (RefNoKUC <c>FF</c>, SET <c>0x0020</c>, ExtSET <c>0x01</c>), not from a
    /// published table of the answer. The meaning of the bits is from NXP's AN12701, table 2: SET bits 3 to 5 are the key type
    /// (<c>100b</c> is AES-128), <b>SET bit 9 (<c>0x0200</c>) disables the key entry</b> (a blank entry has it set, and a
    /// SAM_ChangeKeyEntry that clears it enables the entry), and the low bits of ExtSET are the key class (<c>001b</c> is a PICC key,
    /// <c>000b</c> a host key). SET <c>0x0020</c> says AES-128 and nothing about the class: that is ExtSET.
    /// </remarks>
    internal sealed class SamKeyEntryInfo
    {
        /// <summary>The length of the answer of SAM_GetKeyEntry.</summary>
        internal const int AnswerLength = 13;

        private const ushort DisableKeyEntryBit = 0x0200;
        private const ushort KeyTypeMask = 0x0038;
        private const ushort KeyTypeAes128 = 0x0020;
        private const byte KeyClassMask = 0x07;
        private const byte KeyClassPicc = 0x01;

        private SamKeyEntryInfo(byte versionA, byte versionB, byte versionC, byte keyNoCek, byte keyVersionCek, byte refNoKuc, ushort set, byte extSet)
        {
            VersionA = versionA;
            VersionB = versionB;
            VersionC = versionC;
            KeyNoCek = keyNoCek;
            KeyVersionCek = keyVersionCek;
            RefNoKuc = refNoKuc;
            Set = set;
            ExtSet = extSet;
        }

        /// <summary>Gets the version of the key in position A.</summary>
        internal byte VersionA { get; }

        /// <summary>Gets the version of the key in position B.</summary>
        internal byte VersionB { get; }

        /// <summary>Gets the version of the key in position C.</summary>
        internal byte VersionC { get; }

        /// <summary>Gets the key entry that must be used to change this entry.</summary>
        internal byte KeyNoCek { get; }

        /// <summary>Gets the version of that key.</summary>
        internal byte KeyVersionCek { get; }

        /// <summary>Gets the key usage counter that is linked to the entry, <c>0xFF</c> for none.</summary>
        internal byte RefNoKuc { get; }

        /// <summary>Gets the SET word of the entry.</summary>
        internal ushort Set { get; }

        /// <summary>Gets the ExtSET byte of the entry.</summary>
        internal byte ExtSet { get; }

        /// <summary>Gets whether the entry is enabled: SET bit 9 is clear.</summary>
        internal bool IsEnabled => (Set & DisableKeyEntryBit) == 0;

        /// <summary>Gets whether the entry is an AES-128 key: SET bits 3 to 5 are <c>100b</c>.</summary>
        internal bool IsAes128 => (Set & KeyTypeMask) == KeyTypeAes128;

        /// <summary>Gets whether SET bit 2 (Keep IV) is set, which a DESFire AES key needs (NXP AN12696, table 1).</summary>
        internal bool HasKeepIv => (Set & SamKeyEntries.KeepIvBit) != 0;

        /// <summary>Gets the key class: the low three bits of ExtSET.</summary>
        internal int KeyClass => ExtSet & KeyClassMask;

        /// <summary>Gets whether the entry is of the PICC key class.</summary>
        internal bool IsPiccKey => KeyClass == KeyClassPicc;

        /// <summary>Gets what to say about the entry that was written: it is usable for authenticating a DESFire PICC, or why not.</summary>
        internal SamKeyEntryCheck Verdict =>
            !IsEnabled ? SamKeyEntryCheck.Disabled
            : IsAes128 && IsPiccKey ? (HasKeepIv ? SamKeyEntryCheck.Matches : SamKeyEntryCheck.MissingKeepIv)
            : SamKeyEntryCheck.WrongKind;

        /// <summary>Reads the answer of SAM_GetKeyEntry.</summary>
        /// <param name="data">The decrypted data of the answer.</param>
        /// <returns>What the SAM said, or <c>null</c> when the answer is not 13 bytes long.</returns>
        internal static SamKeyEntryInfo? Parse(ReadOnlySpan<byte> data)
        {
            if (data.Length != AnswerLength)
            {
                return null;
            }

            return new SamKeyEntryInfo(
                data[0],
                data[1],
                data[2],
                data[7],
                data[8],
                data[9],
                (ushort)(data[10] | (data[11] << 8)),
                data[12]);
        }
    }
}
