using System.Threading;
using System.Threading.Tasks;

namespace RFiDGear.Bridge
{
    /// <summary>
    /// The call the host makes on a client that registered as the supplier of card keys (<see cref="IBridgeService.RegisterKeyProvider"/>).
    /// A client implements this on the object it registers as its local target, next to <see cref="IBridgeClientEvents"/>, and
    /// maps the wire name <see cref="BridgeClientMethods.ResolveKey"/> to the method with the same camel-case rule as for the events.
    /// <para>
    /// Rules for the implementation:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Make it asynchronous. The host's notifications and this call are delivered in the order they arrive, and a handler that blocks holds up the ones behind it.</description></item>
    /// <item><description>Answer only for references you serve, and only when you decide to; <see cref="KeyResolveResults.NotAuthorized"/> is a normal answer. Any local program can name any reference in a project, so the client, not RFiDGear, is the one that checks.</description></item>
    /// <item><description>Honour the token. It is cancelled when the time is up or the run is given up; a key that arrives after that is thrown away.</description></item>
    /// <item><description>Never log the request or the answer, and keep the key only as long as the answer needs it.</description></item>
    /// </list>
    /// </summary>
    public interface IBridgeClientKeys
    {
        /// <summary>Asks the client for a card key.</summary>
        /// <param name="request">Which key, and what for.</param>
        /// <param name="cancellationToken">Cancelled when the host stops waiting.</param>
        /// <returns>How the client answered, and the key when it did.</returns>
        [Capability(BridgeCapabilities.KeysProvider)]
        Task<ResolveKeyResponse> ResolveKey(ResolveKeyRequest request, CancellationToken cancellationToken);
    }
}
