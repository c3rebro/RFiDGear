#nullable enable
using RFiDGear.Services.Sam;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Elatec.NET.Interfaces;

namespace RFiDGear.Tests.Sam
{
    /// <summary>
    /// A TWN4 as the relay sees it, in memory: a card in a socket is a key of <see cref="Cards"/> (its value is the ATR that the power-up gives,
    /// <c>null</c> for a card that does not answer), the card on the antenna is a script, and every call is recorded in order.
    /// </summary>
    internal sealed class FakeTwn4Ports : ITwn4SamRelayPorts
    {
        internal static readonly byte[] GsmAtr = Convert.FromHexString("3B9F95801FC78031E073FE2113574A33052C323400BD");

        /// <summary>A SAM AV3 whose ATR offers T=0 only (a made-up card; the real one offers T=1).</summary>
        internal static readonly byte[] SamAtrWithoutT1 =
            new byte[] { 0x3B, 0x0E, 0x4D, 0x49, 0x46, 0x41, 0x52, 0x45, 0x20, 0x53, 0x41, 0x4D, 0x20, 0x41, 0x56, 0x33 };

        /// <summary>Gets the cards in the sockets, with the ATR that each gives.</summary>
        internal Dictionary<Twn4SamSlot, byte[]?> Cards { get; } = new Dictionary<Twn4SamSlot, byte[]?>();

        /// <summary>Gets every call, in order: <c>has Sam1</c>, <c>power on Sam1</c>, <c>set t1 Sam1</c>, <c>power off Sam1</c>, <c>channel Sam1</c>, <c>search card</c>, <c>card exchange</c>.</summary>
        internal List<string> Events { get; } = new List<string>();

        /// <summary>Gets or sets what the search for a card on the antenna finds.</summary>
        internal Twn4CardSighting Sighting { get; set; } = Twn4CardSighting.Desfire;

        /// <summary>Gets or sets what the card answers to a command; <c>null</c> is a reader that gives no answer.</summary>
        internal Func<byte[], byte[]?> Card { get; set; } = _ => new byte[] { 0x91, 0x00 };

        /// <summary>Gets or sets the channel that is handed out for a SAM.</summary>
        internal ISamApduChannel Channel { get; set; } = new ScriptedSamChannel(_ => new Elatec.NET.Sam.SamApduResponse(Array.Empty<byte>(), 0x9000));

        /// <summary>Gets or sets what the search for the card throws (a reader that fails).</summary>
        internal Exception? SearchThrows { get; set; }

        /// <summary>Gets or sets what setting the socket up for T=1 throws.</summary>
        internal Exception? SetT1Throws { get; set; }

        /// <summary>Gets or sets what powering a socket down throws.</summary>
        internal Exception? PowerOffThrows { get; set; }

        /// <summary>Gets the settings of the last call to set a socket up.</summary>
        internal Twn4SamSettings? LastSettings { get; private set; }

        public Task<bool> SamSlotHasCardAsync(Twn4SamSlot slot)
        {
            Events.Add("has " + slot);

            return Task.FromResult(Cards.ContainsKey(slot));
        }

        public Task<byte[]?> SamSlotPowerOnAsync(Twn4SamSlot slot)
        {
            Events.Add("power on " + slot);

            return Task.FromResult(Cards[slot]);
        }

        public Task SamSlotSetT1Async(Twn4SamSlot slot, Twn4SamSettings settings)
        {
            Events.Add("set t1 " + slot);
            LastSettings = settings;

            return SetT1Throws == null ? Task.CompletedTask : Task.FromException(SetT1Throws);
        }

        public Task SamSlotPowerOffAsync(Twn4SamSlot slot)
        {
            Events.Add("power off " + slot);

            return PowerOffThrows == null ? Task.CompletedTask : Task.FromException(PowerOffThrows);
        }

        public ISamApduChannel SamChannel(Twn4SamSlot slot)
        {
            Events.Add("channel " + slot);

            return Channel;
        }

        public Task<Twn4CardSighting> SearchCardAsync()
        {
            Events.Add("search card");

            return SearchThrows == null ? Task.FromResult(Sighting) : Task.FromException<Twn4CardSighting>(SearchThrows);
        }

        public Task<byte[]?> ExchangeWithCardAsync(byte[] apdu)
        {
            Events.Add("card exchange");

            return Task.FromResult(Card(apdu));
        }
    }
}
