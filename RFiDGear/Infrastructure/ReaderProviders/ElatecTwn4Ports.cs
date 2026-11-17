#nullable enable
using System;
using System.Threading.Tasks;

using Elatec.NET;
using Elatec.NET.Interfaces;
using Elatec.NET.Sam;

using RFiDGear.Services.Sam;

namespace RFiDGear.Infrastructure.ReaderProviders
{
    /// <summary>
    /// The SAM sockets and the antenna of one TWN4, as <see cref="ITwn4SamRelayPorts"/>: each call is one call of Elatec.NET. It holds no logic
    /// beyond the names of the sockets, the default speed (no PPS is sent) and the look at the card (one search, the SAK, and the first frame of
    /// GetVersion). Whoever uses it holds the lock of the COM port of the reader.
    /// </summary>
    internal sealed class ElatecTwn4Ports : ITwn4SamRelayPorts
    {
        // The most that a SAM answers to one command (a cryptogram, a version): the same limit as the PC/SC channel.
        private const ushort MaxSamAnswer = 261;

        // F index 1 (372) and D index 1 (1): what the ATR announces as the defaults, and what is used while no PPS is sent.
        private const byte DefaultFindexDindex = 0x11;

        private const byte SakIso14443Part4 = 0x20;

        private readonly TWN4ReaderDevice _reader;

        /// <summary>Creates the ports of a reader.</summary>
        /// <param name="reader">The connected reader.</param>
        internal ElatecTwn4Ports(TWN4ReaderDevice reader)
        {
            _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        }

        /// <inheritdoc />
        public async Task<bool> SamSlotHasCardAsync(Twn4SamSlot slot)
        {
            var status = await _reader.ISO7816_GetSlotStatusAsync(ChannelOf(slot)).ConfigureAwait(false);

            return status != null && status.IccStatus != Iso7816IccStatus.NoIccPresent;
        }

        /// <inheritdoc />
        public async Task<byte[]?> SamSlotPowerOnAsync(Twn4SamSlot slot)
        {
            var result = await _reader.ISO7816_IccPowerOnAsync(ChannelOf(slot), Iso7816PowerSelect.Auto, 64).ConfigureAwait(false);

            return result?.Atr is { Length: > 0 } atr ? atr : null;
        }

        /// <inheritdoc />
        public Task SamSlotSetT1Async(Twn4SamSlot slot, Twn4SamSettings settings) =>
            _reader.ISO7816_SetCommSettingsAsync(
                ChannelOf(slot),
                new TWN4ReaderDevice.Iso7816CommSettings
                {
                    Protocol = Iso7816Protocol.T1,
                    Frequency = Iso7816Frequency.Hz3750000,
                    F = 372,
                    D = 1,
                    T1 = new TWN4ReaderDevice.Iso7816ProtocolDataT1
                    {
                        FindexDindex = DefaultFindexDindex,
                        Tccks = 0,
                        GuardTime = settings.GuardTime,
                        WaitingIntegers = settings.WaitingIntegers,
                        ClockStop = 0,
                        Ifsc = settings.Ifsc,
                        NadValue = 0,
                        Wtx = 0,
                    },
                });

        /// <inheritdoc />
        public async Task SamSlotPowerOffAsync(Twn4SamSlot slot) =>
            await _reader.ISO7816_IccPowerOffAsync(ChannelOf(slot)).ConfigureAwait(false);

        /// <inheritdoc />
        public ISamApduChannel SamChannel(Twn4SamSlot slot) => new Twn4SamApduChannel(_reader, ChannelOf(slot), MaxSamAnswer);

        /// <inheritdoc />
        public async Task<Twn4CardSighting> SearchCardAsync()
        {
            var tag = await _reader.SearchTagAsync().ConfigureAwait(false);

            if (tag == null)
            {
                return Twn4CardSighting.None;
            }

            // A card that talks ISO 14443-4 (SAK bit 5) and answers the first frame of GetVersion like a DESFire card. Ultralight, NTAG and
            // Classic cards stop at the SAK, and nothing is sent to them.
            var sak = await _reader.ISO14443A_GetSakAsync().ConfigureAwait(false);

            if (sak == null || (sak.Value & SakIso14443Part4) == 0)
            {
                return Twn4CardSighting.NotDesfire;
            }

            var version = await _reader.ISO14443_4_TdxAsync(Twn4CardSide.GetVersionApdu).ConfigureAwait(false);

            return Twn4CardSide.IsDesfireVersion(version) ? Twn4CardSighting.Desfire : Twn4CardSighting.NotDesfire;
        }

        /// <inheritdoc />
        public async Task<byte[]?> ExchangeWithCardAsync(byte[] apdu)
        {
            ArgumentNullException.ThrowIfNull(apdu);

            // The reader takes one byte for the length of the frame.
            if (apdu.Length > byte.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(apdu), "The frame is longer than the reader takes.");
            }

            return await _reader.ISO14443_4_TdxAsync(apdu).ConfigureAwait(false);
        }

        private static Iso7816Channel ChannelOf(Twn4SamSlot slot) =>
            slot == Twn4SamSlot.Sam1 ? Iso7816Channel.Sam1 : Iso7816Channel.Sam2;
    }
}
