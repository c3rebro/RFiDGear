#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;

using RFiDGear.Infrastructure;

using Serilog;

namespace RFiDGear.Services.Sam
{
    /// <summary>The card of a relay: the way to send it APDUs, or why there is none.</summary>
    /// <param name="Failure">Why there is no transport; <see cref="SamPiccFailure.None"/> when there is one.</param>
    /// <param name="Transport">The transport to the card, or <see langword="null"/>.</param>
    internal readonly record struct SamCardLink(SamPiccFailure Failure, ISamTransport? Transport);

    /// <summary>What a reader gives a relay: the gate that one relay holds, and how to open the SAM, open the card and let go of the card.</summary>
    /// <param name="Label">The name of the reader kind for the log (<c>PC/SC</c>, <c>TWN4</c>); no reader name, no secret.</param>
    /// <param name="Gate">The gate of the reader's session, held for the whole relay.</param>
    /// <param name="OpenSam">Opens the SAM for this operation. Called once the gate is held; it takes the work off the thread of the caller itself.</param>
    /// <param name="OpenCard">Connects to the card and checks that it is a DESFire card; called only when the SAM is open.</param>
    /// <param name="ReleaseCard">Lets go of the card; best effort, called whenever the card was asked for.</param>
    internal readonly record struct SamRelayParts(
        string Label,
        SemaphoreSlim Gate,
        Func<Task<SamPiccOpen>> OpenSam,
        Func<Task<SamCardLink>> OpenCard,
        Func<Task> ReleaseCard);

    /// <summary>
    /// One relay between a MIFARE SAM AV3 and a DESFire card, the same for every reader that can do it. One relay is one boundary, under the
    /// gate of the session of the reader (a read of the chip disconnects first, which would take the card away from a relay that is running).
    /// The SAM is opened first and checked to be a SAM AV3, and until then nothing is sent to the card. Nothing is repeated: the session makes
    /// one attempt per step. No key, reference, reader name or text of an exception reaches the log.
    /// </summary>
    internal static class SamRelay
    {
        /// <summary>Runs one operation of the session between the SAM and the card, and lets go of both whatever happens.</summary>
        /// <param name="parts">What the reader gives the relay.</param>
        /// <param name="operation">The name of the operation, for the log.</param>
        /// <param name="run">The operation, on a session between the open SAM and the open card.</param>
        /// <returns>The answer of the session, or why the relay did not get that far.</returns>
        internal static async Task<SamPiccResult> RunAsync(SamRelayParts parts, string operation, Func<SamPiccSession, Task<SamPiccResult>> run)
        {
            await parts.Gate.WaitAsync().ConfigureAwait(false);

            try
            {
                var open = await parts.OpenSam().ConfigureAwait(false);

                if (open.Connection == null)
                {
                    return SamPiccResult.OfOpenFailure(open.Failure);
                }

                using (open.Connection)
                {
                    try
                    {
                        var link = await parts.OpenCard().ConfigureAwait(false);

                        if (link.Transport == null)
                        {
                            return SamPiccResult.OfCardFailure(link.Failure);
                        }

                        return await run(new SamPiccSession(open.Connection.Channel, link.Transport)).ConfigureAwait(false);
                    }
                    finally
                    {
                        // Once the card was asked for it is let go of, however far it got: a card that was connected and is no DESFire card, or
                        // whose chip could not be looked at, must not stay connected.
                        await parts.ReleaseCard().ConfigureAwait(false);
                    }
                }
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                Log.ForContext(typeof(SamRelay)).Warning("{Label}: the SAM operation {Operation} failed ({ExceptionType}).", parts.Label, operation, e.GetType().Name);

                return new SamPiccResult(ERROR.TransportError, SamPiccFailure.ReaderFailure);
            }
            finally
            {
                parts.Gate.Release();
            }
        }
    }
}
