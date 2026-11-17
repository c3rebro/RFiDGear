#nullable enable
using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

using RFiDGear.Services.Sam;

namespace RFiDGear.Infrastructure.ReaderProviders
{
    [SuppressMessage("Minor Code Smell", "S2333:Partial is gratuitous in this context", Justification = "The other half is ElatecNetProvider.cs; the analysis reads one file at a time and cannot see it.")]
    public partial class ElatecNetProvider
    {
        /// <inheritdoc />
        /// <remarks>
        /// A TWN4 can: the SAM sits in one of its SAM sockets, which only the hardware revision G has. A reader of an older revision, or a socket
        /// with no SAM in it, makes the call answer <see cref="ERROR.KeyUnavailable"/>, which says what is missing.
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

        /// <summary>Gives the SAM sockets and the antenna of the reader that is connected. Overridden by the tests, which have no reader.</summary>
        /// <returns>The ports, or <see langword="null"/> when no reader is connected.</returns>
        internal virtual ITwn4SamRelayPorts? CreateTwn4Ports() =>
            readerDevice is { IsConnected: true } device ? new ElatecTwn4Ports(device) : null;

        // One relay is one boundary, under the lock of the COM port (see SamRelay). Everything else that talks to the reader waits for it,
        // and it waits for them, because the SAM and the card are reached through the same port. The SAM is opened first and found in
        // whichever socket it is in, and the card is searched once, afterwards. The library's own DESFire calls are not used, because they
        // repeat steps. The reader is taken before the lock is waited for, which is safe because the monitor replaces it only when it is
        // not connected, and the ports ask the reader on every call.
        private Task<SamPiccResult> RunWithSamAsync(string operation, Func<SamPiccSession, Task<SamPiccResult>> run) =>
            CreateTwn4Ports() is { } reader
                ? SamRelay.RunAsync(Twn4SamRelay.Parts(reader, _comPortLock), operation, run)
                : Task.FromResult(new SamPiccResult(ERROR.TransportError, SamPiccFailure.ReaderFailure));
    }
}