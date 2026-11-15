#nullable enable
using System;
using System.Threading.Tasks;

using Elatec.NET.Interfaces;
using Elatec.NET.Sam;

namespace RFiDGear.Services.Sam
{
    /// <summary>
    /// What is known about a MIFARE SAM AV3 from the answer of SAM_GetVersion. Only the hardware and the software
    /// information (7 bytes each) are kept; the rest of that answer holds the serial number of the SAM, which this type
    /// never receives.
    /// </summary>
    /// <param name="HardwareInfo">The hardware information as hexadecimal digits.</param>
    /// <param name="SoftwareInfo">The software information as hexadecimal digits.</param>
    internal sealed record SamVersionInfo(string HardwareInfo, string SoftwareInfo);

    /// <summary>Recognising a MIFARE SAM AV3 by its ATR, and the one command that needs no key: SAM_GetVersion.</summary>
    internal static class SamDetection
    {
        private const byte Cla = 0x80;
        private const byte InsGetVersion = 0x60;
        private const int InfoLength = 7;

        // "MIFARE SAM AV3": the historical bytes of the ATR of the SAM (they go on with the revision of the SAM, e.g. "0").
        private static readonly byte[] SamAv3Marker =
        {
            0x4D, 0x49, 0x46, 0x41, 0x52, 0x45, 0x20, 0x53, 0x41, 0x4D, 0x20, 0x41, 0x56, 0x33
        };

        /// <summary>Tells whether an ATR carries the historical bytes <c>MIFARE SAM AV3</c>.</summary>
        /// <param name="atr">The answer to reset of a card.</param>
        /// <returns><c>true</c> for a MIFARE SAM AV3.</returns>
        internal static bool IsSamAv3(byte[]? atr)
        {
            return atr != null && ((ReadOnlySpan<byte>)atr).IndexOf(SamAv3Marker) >= 0;
        }

        /// <summary>
        /// Sends SAM_GetVersion (<c>80 60 00 00 00</c>), which needs no authentication and changes nothing.
        /// </summary>
        /// <param name="channel">The channel to a SAM.</param>
        /// <returns>The hardware and software information, or <c>null</c> when the SAM did not answer with success and enough bytes.</returns>
        /// <exception cref="SamException">The reader could not exchange the command.</exception>
        internal static async Task<SamVersionInfo?> ReadVersionAsync(ISamApduChannel channel)
        {
            var response = await channel.TransmitAsync(new SamApdu(Cla, InsGetVersion, 0x00, 0x00, null, 0x00)).ConfigureAwait(false);

            return ParseVersion(response);
        }

        /// <summary>Takes the information out of the answer to SAM_GetVersion, and nothing else of it.</summary>
        /// <param name="response">The answer.</param>
        /// <returns>The information, or <c>null</c> for a status word other than success or a too short answer.</returns>
        internal static SamVersionInfo? ParseVersion(SamApduResponse response)
        {
            if (response.StatusWord != 0x9000 || response.Data.Length < 2 * InfoLength)
            {
                return null;
            }

            return new SamVersionInfo(
                Convert.ToHexString(response.Data, 0, InfoLength),
                Convert.ToHexString(response.Data, InfoLength, InfoLength));
        }
    }
}
