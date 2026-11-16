#nullable enable
using System;
using System.Linq;

using Elatec.NET.Sam;

using RFiDGear.Infrastructure;

namespace RFiDGear.Services.Sam
{
    /// <summary>The kind of key that authenticates a DESFire card through the SAM: the card command, the length of its random numbers.</summary>
    internal enum SamPiccKeyType
    {
        /// <summary>DESFire native TDEA (DES and 2K3DES): the card command <c>0A</c>, random numbers of 8 bytes.</summary>
        NativeTdea,

        /// <summary>AES-128: the card command <c>AA</c>, random numbers of 16 bytes.</summary>
        Aes128,
    }

    /// <summary>
    /// The frames of a PICC authentication and of a key change through a SAM AV3 in S mode, with the PC between the SAM and the card. SAM
    /// frames: NXP AN12696 and the reader library for the SAM AV2 (INS <c>0A</c> authenticate, <c>C4</c> change key, <c>CA</c> kill); all
    /// of them were seen on a real SAM AV3 with a DESFire EV3 (<c>SAM-PICC-FIRST-CONTACT.md</c> and <c>SAM-AV3-FIRST-CONTACT.md</c>).
    /// </summary>
    internal static class SamPiccFrames
    {
        /// <summary>The card command of the native authentication with a TDEA key.</summary>
        internal const byte CardAuthenticateNative = 0x0A;

        /// <summary>The card command of the authentication with an AES key.</summary>
        internal const byte CardAuthenticateAes = 0xAA;

        /// <summary>The status byte of the card for "more frames".</summary>
        internal const ushort CardMoreFrames = 0x91AF;

        /// <summary>The status word of the card for success.</summary>
        internal const ushort CardSuccess = 0x9100;

        /// <summary>The status word of the card that says that it dropped an unfinished authentication (it answers the next command with it).</summary>
        internal const ushort CardAborted = 0x91CA;

        /// <summary>The status word of a SAM for success.</summary>
        internal const ushort SamSuccess = 0x9000;

        /// <summary>The status word of a SAM for "the next part of the authentication".</summary>
        internal const ushort SamMoreParts = 0x90AF;

        /// <summary>Gives the card command of an authentication.</summary>
        /// <param name="type">The kind of key.</param>
        /// <returns><c>0A</c> or <c>AA</c>.</returns>
        internal static byte CardAuthenticateCommand(SamPiccKeyType type) => type == SamPiccKeyType.Aes128 ? CardAuthenticateAes : CardAuthenticateNative;

        /// <summary>Gives the length of the random numbers of an authentication.</summary>
        /// <param name="type">The kind of key.</param>
        /// <returns>8 or 16.</returns>
        internal static int RandomLength(SamPiccKeyType type) => type == SamPiccKeyType.Aes128 ? 16 : 8;

        /// <summary>Gives the key number byte of a card command ChangeKey for the card master key: key 0, and the type of the new key in the top bits.</summary>
        /// <param name="newType">The kind of the new key.</param>
        /// <returns><c>00</c> for DES, <c>80</c> for AES (<c>40</c> is 3K3DES, which is not offered).</returns>
        internal static byte MasterKeyNumberByte(SamPiccKeyType newType) => newType == SamPiccKeyType.Aes128 ? (byte)0x80 : (byte)0x00;

        /// <summary>Builds the card command SelectApplication.</summary>
        /// <param name="appId">The application, 0 for the card itself.</param>
        /// <returns>The APDU: the three bytes of the AID, low byte first.</returns>
        internal static byte[] CardSelectApplication(uint appId) =>
            new byte[] { 0x90, 0x5A, 0x00, 0x00, 0x03, (byte)(appId & 0xFF), (byte)((appId >> 8) & 0xFF), (byte)((appId >> 16) & 0xFF), 0x00 };

        /// <summary>Builds the first part of the authentication on the card.</summary>
        /// <param name="type">The kind of key.</param>
        /// <param name="cardKeyNo">The key number on the card.</param>
        /// <returns>The APDU.</returns>
        internal static byte[] CardAuthenticatePart1(SamPiccKeyType type, byte cardKeyNo) =>
            new byte[] { 0x90, CardAuthenticateCommand(type), 0x00, 0x00, 0x01, cardKeyNo, 0x00 };

        /// <summary>Builds the second part of the authentication on the card: the additional frame with what the SAM answered.</summary>
        /// <param name="fromSam">The SAM's answer to its first part.</param>
        /// <returns>The APDU.</returns>
        internal static byte[] CardAdditionalFrame(byte[] fromSam) =>
            new byte[] { 0x90, 0xAF, 0x00, 0x00, (byte)fromSam.Length }.Concat(fromSam).Concat(new byte[] { 0x00 }).ToArray();

        /// <summary>Builds the card command ChangeKey.</summary>
        /// <param name="keyNumberByte">The key number, or for the card master key the type of the new key (<see cref="MasterKeyNumberByte"/>).</param>
        /// <param name="cryptogram">What the SAM built.</param>
        /// <returns>The APDU.</returns>
        internal static byte[] CardChangeKey(byte keyNumberByte, byte[] cryptogram) =>
            new byte[] { 0x90, 0xC4, 0x00, 0x00, (byte)(1 + cryptogram.Length), keyNumberByte }.Concat(cryptogram).Concat(new byte[] { 0x00 }).ToArray();

        /// <summary>Builds the first part of SAM_AuthenticatePICC: no key diversification, the key chosen by the key entry number.</summary>
        /// <param name="key">The key entry and the version.</param>
        /// <param name="encRndB">What the card answered to its first part.</param>
        /// <returns>The command.</returns>
        internal static SamApdu SamAuthenticatePart1(SamKeyReference key, byte[] encRndB) =>
            new SamApdu(0x80, 0x0A, 0x00, 0x00, new byte[] { key.Entry, key.Version }.Concat(encRndB).ToArray(), 0x00);

        /// <summary>Builds the second part of SAM_AuthenticatePICC: what the card answered to the SAM's first part, and no Le (the SAM accepts both).</summary>
        /// <param name="encRndA">The card's answer.</param>
        /// <returns>The command.</returns>
        internal static SamApdu SamAuthenticatePart2(byte[] encRndA) => new SamApdu(0x80, 0x0A, 0x00, 0x00, encRndA, null);

        /// <summary>
        /// Builds SAM_ChangeKeyPICC. <c>P1</c> bit 0 says that the key that is changed is the one that was authenticated with; <c>P2</c> is the
        /// number of the key that is changed, and bit 4 says that it is the card master key. The data are the current key and the new key.
        /// </summary>
        /// <param name="sameAsAuthenticated">Whether the key that is changed is the one that was authenticated with.</param>
        /// <param name="piccMasterKey">Whether it is the card master key (application 0, key 0).</param>
        /// <param name="targetKeyNo">The number of the key that is changed.</param>
        /// <param name="current">The key that is replaced.</param>
        /// <param name="next">The key that replaces it.</param>
        /// <returns>The command.</returns>
        internal static SamApdu SamChangeKeyPicc(bool sameAsAuthenticated, bool piccMasterKey, byte targetKeyNo, SamKeyReference current, SamKeyReference next) =>
            new SamApdu(
                0x80,
                0xC4,
                sameAsAuthenticated ? (byte)0x01 : (byte)0x00,
                piccMasterKey ? (byte)0x10 : (byte)(targetKeyNo & 0x0F),
                new byte[] { current.Entry, current.Version, next.Entry, next.Version },
                0x00);

        /// <summary>
        /// Builds SAM_KillAuthentication for the PICC authentication only (<c>P1</c> 01). <b>No Le</b>: with one the SAM answers <c>6700</c>
        /// (or <c>6883</c> while an authentication is half done). It clears an unfinished part 1; closing the connection does the same.
        /// </summary>
        /// <returns>The command.</returns>
        internal static SamApdu SamKillPiccAuthentication() => new SamApdu(0x80, 0xCA, 0x01, 0x00, null, null);

        /// <summary>Maps a status word of the card to the error of a task.</summary>
        /// <param name="statusWord">The card's answer, <c>91xx</c> or another.</param>
        /// <returns>The error level.</returns>
        internal static ERROR ErrorOfCard(ushort statusWord)
        {
            switch (statusWord)
            {
                case 0x91AE:
                    return ERROR.AuthFailure;
                case 0x919D:
                    return ERROR.PermissionDenied;
                case 0x91A0:
                case 0x9140:
                case 0x917E:
                case 0x911C:
                case 0x919E:
                    return ERROR.ProtocolConstraint;
                default:
                    return ERROR.Unknown;
            }
        }
    }
}