#nullable enable
using System.Threading.Tasks;

using Elatec.NET.Interfaces;

namespace RFiDGear.Services.Sam
{
    /// <summary>The SAM sockets of a TWN4 of hardware revision G (the readers of the revisions D and E have none that sees a SAM AV3).</summary>
    internal enum Twn4SamSlot
    {
        /// <summary>The first socket.</summary>
        Sam1 = 1,

        /// <summary>The second socket.</summary>
        Sam2 = 2,
    }

    /// <summary>What a search for a card on the antenna of the reader found.</summary>
    internal enum Twn4CardSighting
    {
        /// <summary>No card.</summary>
        None,

        /// <summary>A card, and it is no DESFire card (or no card that talks ISO 14443-4).</summary>
        NotDesfire,

        /// <summary>A DESFire card.</summary>
        Desfire,
    }

    /// <summary>
    /// The settings of the contact interface for T=1 that come from the ATR of the SAM. The speed is not in it: no PPS is sent, so the SAM
    /// works with the defaults that its ATR announces (F=372, D=1).
    /// </summary>
    /// <param name="GuardTime">The extra guard time (TC1); <c>FF</c> for the SAM AV3.</param>
    /// <param name="WaitingIntegers">BWI in the high nibble and CWI in the low one (TB3); <c>43</c> for the SAM AV3.</param>
    /// <param name="Ifsc">The size of the information field of the card (TA3); <c>FE</c> for the SAM AV3.</param>
    internal readonly record struct Twn4SamSettings(byte GuardTime, byte WaitingIntegers, byte Ifsc);

    /// <summary>
    /// What a relay needs of a TWN4: its SAM sockets and the card on its antenna. The real one is <see cref="ElatecTwn4Ports"/>, which holds no
    /// logic; the tests use a script. Every call is one call of the reader, and a fault of the reader is an exception.
    /// </summary>
    internal interface ITwn4SamRelayPorts
    {
        /// <summary>Asks whether a card is in the socket. Sends nothing to the card.</summary>
        /// <param name="slot">The socket.</param>
        /// <returns><see langword="true"/> when there is a card (active or not).</returns>
        Task<bool> SamSlotHasCardAsync(Twn4SamSlot slot);

        /// <summary>Powers the card in the socket up (a warm reset when the reader had done it already) and reads its ATR.</summary>
        /// <param name="slot">The socket.</param>
        /// <returns>The ATR, or <see langword="null"/> when the card did not answer.</returns>
        Task<byte[]?> SamSlotPowerOnAsync(Twn4SamSlot slot);

        /// <summary>Sets the contact interface of the socket up for T=1 with these values and the default speed.</summary>
        /// <param name="slot">The socket.</param>
        /// <param name="settings">The values from the ATR.</param>
        /// <returns>A task that ends when the reader has taken the settings.</returns>
        Task SamSlotSetT1Async(Twn4SamSlot slot, Twn4SamSettings settings);

        /// <summary>Powers the card in the socket down, which resets whatever state it had.</summary>
        /// <param name="slot">The socket.</param>
        /// <returns>A task that ends when the card is powered down.</returns>
        Task SamSlotPowerOffAsync(Twn4SamSlot slot);

        /// <summary>Gives the channel that carries APDUs to the SAM in the socket. The socket must be powered up and set up for T=1.</summary>
        /// <param name="slot">The socket.</param>
        /// <returns>The channel.</returns>
        ISamApduChannel SamChannel(Twn4SamSlot slot);

        /// <summary>Searches the antenna once for a card and says whether it is a DESFire card. One search, nothing else is selected.</summary>
        /// <returns>What was found.</returns>
        Task<Twn4CardSighting> SearchCardAsync();

        /// <summary>Sends an APDU to the card that the search found (the reader does the framing of ISO 14443-4) and returns its answer.</summary>
        /// <param name="apdu">The command, as for the PC/SC reader: <c>90 xx 00 00 ...</c>.</param>
        /// <returns>The answer with its status word at the end, or <see langword="null"/> when the reader gave none.</returns>
        Task<byte[]?> ExchangeWithCardAsync(byte[] apdu);
    }
}
