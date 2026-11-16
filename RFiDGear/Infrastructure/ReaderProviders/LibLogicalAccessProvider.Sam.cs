#nullable enable
using System;
using System.Threading.Tasks;

using RFiDGear.Services.Sam;

using Serilog;

namespace RFiDGear.Infrastructure.ReaderProviders
{
    public partial class LibLogicalAccessProvider
    {
        /// <summary>The card of a relay: the way to send it APDUs, or why there is none.</summary>
        /// <param name="Failure">Why there is no transport; <see cref="SamPiccFailure.None"/> when there is one.</param>
        /// <param name="Transport">The transport to the card, or <see langword="null"/>.</param>
        internal readonly record struct SamCardLink(SamPiccFailure Failure, ISamTransport? Transport);

        /// <inheritdoc />
        /// <remarks>
        /// A PC/SC reader can, and when no reader is set for the SAM the call answers <see cref="ERROR.KeyUnavailable"/>, which says what is missing.
        /// </remarks>
        public override bool SupportsDesfireSamKeys => true;

        /// <inheritdoc />
        public override Task<SamPiccResult> AuthenticateMifareDesfireWithSamAsync(DesfireSamAccess access)
        {
            if (!DesfireSamRequests.TryAuthenticate(access, out var kind))
            {
                return Task.FromResult(SamPiccResult.Refused(ERROR.ProtocolConstraint, SamPiccFailure.InvalidRequest));
            }

            return RunWithSamAsync("authenticate", session => session.AuthenticateAsync(access.AppId, access.KeyNumber, kind, access.Key));
        }

        /// <inheritdoc />
        public override Task<SamPiccResult> ChangeMifareDesfireKeyWithSamAsync(DesfireSamKeyChange change)
        {
            if (!DesfireSamRequests.TryChange(change, out var request))
            {
                return Task.FromResult(SamPiccResult.Refused(ERROR.ProtocolConstraint, SamPiccFailure.InvalidRequest));
            }

            return RunWithSamAsync("change key", session => session.ChangeKeyAsync(request));
        }

        /// <summary>Tells the provider which reader holds the SAM without choosing a reader unit, so that the tests (which have no readers) can say it.</summary>
        /// <param name="name">The reader of the SAM; empty for none.</param>
        internal void AssumeSamReader(string name) => samReaderName = name ?? string.Empty;

        /// <summary>Makes the connector that opens the SAM for an operation. Overridden by the tests.</summary>
        /// <returns>The connector.</returns>
        internal virtual ISamPiccConnector CreateSamPiccConnector() => new SamPiccConnector(new SamReaderFinder(new LlaSamReaderSource()));

        /// <summary>
        /// Connects to the card on the reader and checks that it is a DESFire card; the second step of every relay, after the SAM is open. A fresh
        /// session (the same steps as a page operation), and the way to send APDUs is the data transport, which keeps every status word. Overridden by
        /// the tests.
        /// </summary>
        /// <returns>The transport, or why there is none.</returns>
        internal virtual async Task<SamCardLink> OpenCardLinkForSamAsync()
        {
            if (!await ConnectToAFreshChipAsync().ConfigureAwait(false))
            {
                return new SamCardLink(SamPiccFailure.NoCard, null);
            }

            return await Task.Run(() =>
            {
                var chip = readerUnit.getSingleChip();

                if (chip == null)
                {
                    return new SamCardLink(SamPiccFailure.NoCard, null);
                }

                // "DESFire", "DESFireEV1", "DESFireEV2", "DESFireEV3": the names that LibLogicalAccess gives (seen: the card is "DESFire" before it is
                // connected and "DESFireEV3" after).
                var cardType = chip.getCardType();

                if (!DesfireSamRequests.IsDesfireCardType(cardType))
                {
                    return new SamCardLink(SamPiccFailure.NotDesfire, null);
                }

                var transport = readerUnit.getDefaultReaderCardAdapter()?.getDataTransport();

                return transport == null
                    ? new SamCardLink(SamPiccFailure.ReaderFailure, null)
                    : new SamCardLink(SamPiccFailure.None, new LlaApduTransport(transport));
            }).ConfigureAwait(false);
        }

        /// <summary>Lets go of the card after a relay, so that the next operation starts from a card session that it makes itself. Best effort. Overridden by the tests.</summary>
        /// <returns>A task that ends when the card is let go of.</returns>
        internal virtual Task ReleaseCardLinkForSamAsync() =>
            Task.Run(() =>
            {
                try
                {
                    readerUnit?.disconnect();
                }
                catch (Exception e)
                {
                    Log.ForContext<LibLogicalAccessProvider>().Debug("PC/SC disconnect after a SAM operation failed ({ExceptionType}).", e.GetType().Name);
                }
            });

        // One relay is one boundary, under the session gate of the provider (ReadChipPublic disconnects first, which would take the card away
        // from a relay that is running). The SAM is opened first and checked to be a SAM AV3, and until then nothing is sent to the card. Nothing is
        // repeated: the session makes one attempt per step. No key, reference, reader name or text of an exception reaches the log.
        private async Task<SamPiccResult> RunWithSamAsync(string operation, Func<SamPiccSession, Task<SamPiccResult>> run)
        {
            await pcscSessionGate.WaitAsync().ConfigureAwait(false);

            try
            {
                var open = CreateSamPiccConnector().Open(samReaderName, ReaderUnitName);

                if (open.Connection == null)
                {
                    return SamPiccResult.OfOpenFailure(open.Failure);
                }

                using (open.Connection)
                {
                    var link = await OpenCardLinkForSamAsync().ConfigureAwait(false);

                    if (link.Transport == null)
                    {
                        return SamPiccResult.OfCardFailure(link.Failure);
                    }

                    try
                    {
                        return await run(new SamPiccSession(open.Connection.Channel, link.Transport)).ConfigureAwait(false);
                    }
                    finally
                    {
                        await ReleaseCardLinkForSamAsync().ConfigureAwait(false);
                    }
                }
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                Log.ForContext<LibLogicalAccessProvider>().Warning("PC/SC: the SAM operation {Operation} failed ({ExceptionType}).", operation, e.GetType().Name);

                return new SamPiccResult(ERROR.TransportError, SamPiccFailure.ReaderFailure);
            }
            finally
            {
                pcscSessionGate.Release();
            }
        }
    }
}