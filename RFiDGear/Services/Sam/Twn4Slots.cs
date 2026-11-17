#nullable enable
using Elatec.NET;

namespace RFiDGear.Services.Sam
{
    /// <summary>What the reader says about its SAM sockets.</summary>
    internal static class Twn4Slots
    {
        /// <summary>
        /// Tells from the status of a socket whether there is a card in it. A reader that gave no status (Elatec.NET answers <see langword="null"/>
        /// when the command fails) is a fault and not an empty socket: a failed look at the second socket would let a second SAM go unseen.
        /// </summary>
        /// <param name="status">The status of the socket, or <see langword="null"/> when the reader gave none.</param>
        /// <returns><see langword="true"/> when a card is in the socket, active or not.</returns>
        internal static bool HasCard(Iso7816IccStatus? status) =>
            status switch
            {
                null => throw new SamReaderException("The reader gave no status of the SAM socket."),
                Iso7816IccStatus.NoIccPresent => false,
                _ => true,
            };
    }
}