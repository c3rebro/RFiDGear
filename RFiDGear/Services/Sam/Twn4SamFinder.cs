#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Serilog;

namespace RFiDGear.Services.Sam
{
    /// <summary>
    /// The SAM sockets of a TWN4 (hardware revision G) in the list of the SAM setup. A SAM is listed by the ATR of the card in a socket, which
    /// needs the socket to be powered up and nothing else: a card that is no SAM is powered down again, and the SAM too (the window opens it
    /// afterwards). <see cref="Open"/> sets the socket up for T=1 from the ATR and gives the channel of the reader; letting go of the connection
    /// powers the socket down. Unlike <see cref="Twn4SamConnector"/> it does not take <see cref="SamPortGate"/>: the window holds it already. No
    /// serial number, reader name or text of an exception reaches the log.
    /// </summary>
    internal sealed class Twn4SamFinder : ISamFinder
    {
        private const string SocketNamePrefix = "TWN4 SAM socket ";

        private static readonly Twn4SamSlot[] Slots = { Twn4SamSlot.Sam1, Twn4SamSlot.Sam2 };

        private readonly ITwn4SamRelayPorts _ports;

        /// <summary>Creates the finder.</summary>
        /// <param name="ports">The connected TWN4.</param>
        internal Twn4SamFinder(ITwn4SamRelayPorts ports)
        {
            _ports = ports ?? throw new ArgumentNullException(nameof(ports));
        }

        /// <inheritdoc />
        public SamSearchResult Find()
        {
            var found = new List<SamReaderInfo>();

            foreach (var slot in Slots)
            {
                try
                {
                    if (!Wait(() => _ports.SamSlotHasCardAsync(slot)))
                    {
                        continue;
                    }

                    var atr = Wait(() => _ports.SamSlotPowerOnAsync(slot));

                    // Only the ATR was wanted: the socket is let go of again whatever the card is.
                    PowerOffQuietly(slot);

                    if (atr != null && SamDetection.IsSamAv3(atr))
                    {
                        found.Add(new SamReaderInfo(NameOf(slot), atr));
                    }
                }
                catch (OperationCanceledException)
                {
                    // Passed on, but not before the socket is let go of: no connection is handed out to do that.
                    PowerOffQuietly(slot);

                    throw;
                }
                catch (Exception e)
                {
                    // This socket is skipped; the other one is still looked at.
                    Log.ForContext<Twn4SamFinder>().Warning("SAM search: a SAM socket of the TWN4 could not be looked at ({ExceptionType}).", e.GetType().Name);
                    PowerOffQuietly(slot);
                }
            }

            return new SamSearchResult(found, false);
        }

        /// <inheritdoc />
        public SamConnection? Open(string readerName)
        {
            if (!TryGetSlot(readerName, out var slot))
            {
                return null;
            }

            try
            {
                if (!Wait(() => _ports.SamSlotHasCardAsync(slot)))
                {
                    return null;
                }

                var atr = Wait(() => _ports.SamSlotPowerOnAsync(slot));
                var settings = atr != null && SamDetection.IsSamAv3(atr) ? Twn4AtrReader.SettingsOf(atr) : null;

                // No command goes to a card that is not a SAM, and a SAM that does not offer T=1 cannot be talked to.
                if (atr == null || settings == null)
                {
                    PowerOffQuietly(slot);

                    return null;
                }

                Wait(() => _ports.SamSlotSetT1Async(slot, settings.Value));

                return new SamConnection(new SamReaderInfo(readerName, atr), _ports.SamChannel(slot), () => PowerOffQuietly(slot));
            }
            catch (OperationCanceledException)
            {
                // Passed on, but not before the socket is let go of: no connection is handed out to do that.
                PowerOffQuietly(slot);

                throw;
            }
            catch (Exception e)
            {
                // The socket is not left powered; the fault goes on to the setup, which reports it as a fault of the reader.
                PowerOffQuietly(slot);
                Log.ForContext<Twn4SamFinder>().Debug("SAM setup: a SAM socket of the TWN4 could not be opened ({ExceptionType}).", e.GetType().Name);

                throw;
            }
        }

        private static string NameOf(Twn4SamSlot slot) => SocketNamePrefix + (int)slot;

        private static bool TryGetSlot(string name, out Twn4SamSlot slot)
        {
            foreach (var candidate in Slots)
            {
                if (string.Equals(name, NameOf(candidate), StringComparison.Ordinal))
                {
                    slot = candidate;

                    return true;
                }
            }

            slot = default;

            return false;
        }

        // The setup calls the finder from a thread of the pool, so waiting for the reader here blocks nothing else.
        private static T Wait<T>(Func<Task<T>> call) => Task.Run(call).GetAwaiter().GetResult();

        private static void Wait(Func<Task> call) => Task.Run(call).GetAwaiter().GetResult();

        private void PowerOffQuietly(Twn4SamSlot slot)
        {
            try
            {
                Wait(() => _ports.SamSlotPowerOffAsync(slot));
            }
            catch (Exception e)
            {
                Log.ForContext<Twn4SamFinder>().Debug("TWN4: powering a SAM socket down failed ({ExceptionType}).", e.GetType().Name);
            }
        }
    }

    /// <summary>Makes the finder of the SAM setup window.</summary>
    internal static class SamFinders
    {
        /// <summary>The PC/SC readers, and the SAM sockets of a TWN4 after them when one is connected.</summary>
        /// <param name="pcsc">The PC/SC readers.</param>
        /// <param name="twn4">The ports of the connected TWN4, or <see langword="null"/> when the Elatec reader is not the one in use or not connected.</param>
        /// <returns>The finder of the window.</returns>
        internal static ISamFinder Create(ISamReaderSource pcsc, ITwn4SamRelayPorts? twn4)
        {
            ArgumentNullException.ThrowIfNull(pcsc);

            var pcscFinder = new SamReaderFinder(pcsc);

            return twn4 == null ? pcscFinder : new CompositeSamFinder(pcscFinder, new Twn4SamFinder(twn4));
        }
    }
}