#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RFiDGear.Services.Sam
{
    /// <summary>The card side of a relay on a TWN4: the card on the antenna of the same reader as the SAM.</summary>
    internal static class Twn4CardSide
    {
        /// <summary>The first frame of GetVersion, as the PC/SC reader sends it: it ends with <c>91AF</c> and the card answers 7 bytes.</summary>
        internal static readonly byte[] GetVersionApdu = { 0x90, 0x60, 0x00, 0x00, 0x00 };

        /// <summary>Tells from the answer to GetVersion whether the card is a DESFire card (the vendor is NXP, <c>04</c>, and the type is <c>01</c>).</summary>
        /// <param name="answer">The answer with its status word.</param>
        /// <returns><see langword="true"/> for a DESFire card.</returns>
        internal static bool IsDesfireVersion(byte[]? answer) =>
            answer != null && answer.Length >= 4 && answer[0] == 0x04 && answer[1] == 0x01
            && answer[^2] == 0x91 && (answer[^1] == 0xAF || answer[^1] == 0x00);

        /// <summary>Says from the answer to GetVersion what the card is. No answer is a fault of the reader and not a card that is no DESFire card.</summary>
        /// <param name="answer">The answer with its status word, or <see langword="null"/> when the reader gave none.</param>
        /// <returns>A DESFire card, or not.</returns>
        internal static Twn4CardSighting SightingOfVersion(byte[]? answer)
        {
            if (answer == null)
            {
                throw new SamReaderException("The reader gave no answer from the card.");
            }

            return IsDesfireVersion(answer) ? Twn4CardSighting.Desfire : Twn4CardSighting.NotDesfire;
        }

        /// <summary>Looks for the card (one search) and says what it is and how to reach it.</summary>
        /// <param name="ports">The reader.</param>
        /// <returns>The transport to the card, or why there is none.</returns>
        internal static async Task<SamCardLink> OpenAsync(ITwn4SamRelayPorts ports)
        {
            var sighting = await ports.SearchCardAsync().ConfigureAwait(false);

            return sighting switch
            {
                Twn4CardSighting.Desfire => new SamCardLink(SamPiccFailure.None, new Twn4CardTransport(ports)),
                Twn4CardSighting.NotDesfire => new SamCardLink(SamPiccFailure.NotDesfire, null),
                _ => new SamCardLink(SamPiccFailure.NoCard, null),
            };
        }
    }

    /// <summary>
    /// The way to send APDUs to the card on the antenna of the TWN4: the frames of the PC/SC reader, handed over as they are (the reader does the
    /// framing of ISO 14443-4). The session runs it on a thread of the pool, so waiting for the reader here blocks nothing else.
    /// </summary>
    internal sealed class Twn4CardTransport : ISamTransport
    {
        private readonly ITwn4SamRelayPorts _ports;

        /// <summary>Creates the transport.</summary>
        /// <param name="ports">The reader.</param>
        internal Twn4CardTransport(ITwn4SamRelayPorts ports)
        {
            _ports = ports ?? throw new ArgumentNullException(nameof(ports));
        }

        /// <inheritdoc />
        public byte[] Exchange(byte[] apdu)
        {
            ArgumentNullException.ThrowIfNull(apdu);

            byte[]? answer;

            try
            {
                answer = Task.Run(() => _ports.ExchangeWithCardAsync(apdu), CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                // The text of the reader library is not passed on.
                throw new SamReaderException("The reader could not carry the command to the card.");
            }

            return answer ?? throw new SamReaderException("The reader gave no answer from the card.");
        }
    }
}
