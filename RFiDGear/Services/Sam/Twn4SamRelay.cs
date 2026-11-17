#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RFiDGear.Services.Sam
{
    /// <summary>The relay between a SAM AV3 and a DESFire card on one TWN4: what a TWN4 gives <see cref="SamRelay"/>.</summary>
    internal static class Twn4SamRelay
    {
        /// <summary>Makes the four things that <see cref="SamRelay.RunAsync"/> needs of a reader.</summary>
        /// <param name="ports">The reader.</param>
        /// <param name="gate">The gate that serializes everything that talks to the reader (its COM port), held for the whole relay.</param>
        /// <returns>The parts of a relay.</returns>
        internal static SamRelayParts Parts(ITwn4SamRelayPorts ports, SemaphoreSlim gate)
        {
            ArgumentNullException.ThrowIfNull(ports);
            ArgumentNullException.ThrowIfNull(gate);

            // The SAM is opened first (and found in whichever socket it is), the card is searched second, once. Nothing of the card has to be
            // let go of: the antenna stays as it is, and the next operation of the reader searches the card itself.
            return new SamRelayParts("TWN4", gate, new Twn4SamConnector(ports).OpenAsync, () => Twn4CardSide.OpenAsync(ports), () => Task.CompletedTask);
        }
    }
}
