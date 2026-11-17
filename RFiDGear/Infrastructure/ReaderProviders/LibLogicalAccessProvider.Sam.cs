#nullable enable
using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

using RFiDGear.Services.Sam;

using Serilog;

namespace RFiDGear.Infrastructure.ReaderProviders
{
    [SuppressMessage("Minor Code Smell", "S2333:Partial is gratuitous in this context", Justification = "The other half is LibLogicalAccessProvider.cs; the analysis reads one file at a time and cannot see it.")]
    public partial class LibLogicalAccessProvider
    {
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
            if (!await ConnectToAFreshChipAsync(rethrowFaults: true).ConfigureAwait(false))
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

        // One relay is one boundary, under the session gate of the provider (see SamRelay): the PC/SC reader opens the SAM in the reader that is
        // set for it, and the card in the reader of the application.
        private Task<SamPiccResult> RunWithSamAsync(string operation, Func<SamPiccSession, Task<SamPiccResult>> run) =>
            SamRelay.RunAsync(new SamRelayParts("PC/SC", pcscSessionGate, OpenSamForRelay, OpenCardLinkForSamAsync, ReleaseCardLinkForSamAsync), operation, run);

        // Opening the SAM reads the readers and waits for a card (the insertion wait is 600 ms): off the thread of the caller, which may be a
        // window. The names are read here, once the gate is held, and the opening runs on a thread of the pool.
        private Task<SamPiccOpen> OpenSamForRelay()
        {
            var samReader = samReaderName;
            var cardReader = ReaderUnitName;

            return Task.Run(() => CreateSamPiccConnector().Open(samReader, cardReader));
        }
    }
}