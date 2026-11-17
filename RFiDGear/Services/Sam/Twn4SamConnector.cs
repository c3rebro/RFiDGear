#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Serilog;

namespace RFiDGear.Services.Sam
{
    /// <summary>
    /// Opens the MIFARE SAM AV3 in a SAM socket of a TWN4 for one operation. There is no setting for the socket: both are looked at, and the
    /// card in a socket is the SAM only when its ATR says <c>MIFARE SAM AV3</c> (<see cref="SamDetection.IsSamAv3"/>). Nothing but the power-up
    /// is sent to another card, and it is powered down again; a SAM in each socket is no choice that this makes for the person
    /// (<see cref="SamPiccFailure.SamAmbiguous"/>), because their keys differ. The SAM stays powered up until the connection is disposed, which
    /// powers it down and so resets whatever state it had. The gate of the SAM (<see cref="SamPortGate"/>) is held meanwhile. No reader name,
    /// serial number or text of an exception reaches the log.
    /// </summary>
    internal sealed class Twn4SamConnector
    {
        private static readonly Twn4SamSlot[] Slots = { Twn4SamSlot.Sam1, Twn4SamSlot.Sam2 };

        private readonly ITwn4SamRelayPorts _ports;

        /// <summary>Creates the connector.</summary>
        /// <param name="ports">The reader.</param>
        internal Twn4SamConnector(ITwn4SamRelayPorts ports)
        {
            _ports = ports ?? throw new ArgumentNullException(nameof(ports));
        }

        /// <summary>Finds the SAM AV3 in the sockets and opens it for one operation.</summary>
        /// <returns>The open SAM, or why it is not.</returns>
        internal async Task<SamPiccOpen> OpenAsync()
        {
            if (!SamPortGate.TryEnter())
            {
                return new SamPiccOpen(null, SamPiccFailure.SamBusy);
            }

            var handedOver = false;
            var poweredUp = new List<Twn4SamSlot>();

            try
            {
                var found = new List<(Twn4SamSlot Slot, byte[] Atr)>();

                foreach (var slot in Slots)
                {
                    if (!await _ports.SamSlotHasCardAsync(slot).ConfigureAwait(false))
                    {
                        continue;
                    }

                    poweredUp.Add(slot);
                    var atr = await _ports.SamSlotPowerOnAsync(slot).ConfigureAwait(false);

                    if (atr != null && SamDetection.IsSamAv3(atr))
                    {
                        found.Add((slot, atr));
                    }
                    else
                    {
                        // Not ours: it only had its power-up. Let go of it again.
                        await PowerOffQuietlyAsync(slot).ConfigureAwait(false);
                        poweredUp.Remove(slot);
                    }
                }

                if (found.Count != 1)
                {
                    await PowerOffAllQuietlyAsync(poweredUp).ConfigureAwait(false);

                    return new SamPiccOpen(null, found.Count == 0 ? SamPiccFailure.SamNotFound : SamPiccFailure.SamAmbiguous);
                }

                var (samSlot, samAtr) = found[0];
                var settings = Twn4AtrReader.SettingsOf(samAtr);

                if (settings == null)
                {
                    await PowerOffAllQuietlyAsync(poweredUp).ConfigureAwait(false);

                    return new SamPiccOpen(null, SamPiccFailure.SamRefused);
                }

                await _ports.SamSlotSetT1Async(samSlot, settings.Value).ConfigureAwait(false);
                var channel = _ports.SamChannel(samSlot);
                handedOver = true;

                return new SamPiccOpen(new SamPiccConnection(channel, () => PowerOffNow(samSlot)), SamPiccFailure.None);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                Log.ForContext<Twn4SamConnector>().Warning("TWN4: the SAM could not be opened ({ExceptionType}).", e.GetType().Name);
                await PowerOffAllQuietlyAsync(poweredUp).ConfigureAwait(false);

                return new SamPiccOpen(null, SamPiccFailure.ReaderFailure);
            }
            finally
            {
                if (!handedOver)
                {
                    SamPortGate.Exit();
                }
            }
        }

        // The disposal of the connection is synchronous; the reader is asynchronous. It runs on a thread of the pool so that a caller with a
        // context cannot deadlock on it.
        private void PowerOffNow(Twn4SamSlot slot) =>
            Task.Run(() => PowerOffQuietlyAsync(slot)).GetAwaiter().GetResult();

        private async Task PowerOffAllQuietlyAsync(IEnumerable<Twn4SamSlot> slots)
        {
            foreach (var slot in slots)
            {
                await PowerOffQuietlyAsync(slot).ConfigureAwait(false);
            }
        }

        private async Task PowerOffQuietlyAsync(Twn4SamSlot slot)
        {
            try
            {
                await _ports.SamSlotPowerOffAsync(slot).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Log.ForContext<Twn4SamConnector>().Debug("TWN4: powering a SAM socket down failed ({ExceptionType}).", e.GetType().Name);
            }
        }
    }
}
