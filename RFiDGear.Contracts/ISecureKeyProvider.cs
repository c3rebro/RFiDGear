using System.Threading;
using System.Threading.Tasks;

namespace RFiDGear.Contracts
{
    /// <summary>
    /// Hands out card keys that a project refers to instead of containing them. A project slot stores a provider id and an
    /// opaque reference; when a task needs the key, the host asks the provider for it, uses it for that one card
    /// operation and disposes it. The key is never written to the project, to a log or to a result.
    /// <para>
    /// A provider is exported from an extension with <c>[SecureKeyProvider("id", "Display name")]</c> (see the extension
    /// SDK). The rules a provider has to keep:
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// <b>Thread safety.</b> The host creates one instance per provider and calls it from worker threads, possibly several
    /// at once. It never calls from the user-interface thread and never with a synchronization context.
    /// </description></item>
    /// <item><description>
    /// <b>Same request, same key.</b> Within one run (<see cref="SecureKeyRequest.RunId"/>) the same request must give the
    /// same key. A provider that rotates keys may do so between runs, never in the middle of one.
    /// </description></item>
    /// <item><description>
    /// <b>No secrets outside the answer.</b> Do not log, cache on disk, or put a key into an exception message. The host
    /// catches everything a provider throws and logs only the exception type and the provider id, never the message,
    /// because the messages of vault libraries are not under anyone's control.
    /// </description></item>
    /// <item><description>
    /// <b>Time.</b> Honour <paramref name="cancellationToken"/>. The host stops waiting after a limit (ten seconds, longer
    /// for an <see cref="SecureKeyRequest.Interactive"/> request) and then disposes a key that arrives late.
    /// </description></item>
    /// <item><description>
    /// <b>Failure is an answer.</b> Report what is wrong with a <see cref="SecureKeyResolution.Failed"/> and the matching
    /// <see cref="SecureKeyFailureReason"/>. Throwing is treated as <see cref="SecureKeyFailureReason.ProviderUnavailable"/>.
    /// </description></item>
    /// </list>
    /// Extensions run inside the application with its full rights; a provider is trusted as much as the application is.
    /// </summary>
    public interface ISecureKeyProvider
    {
        /// <summary>Looks up the key for <paramref name="request"/>.</summary>
        /// <param name="request">What to look up and where it is used.</param>
        /// <param name="cancellationToken">Cancelled when the caller gives up or the time limit is reached.</param>
        /// <returns>The key, or the reason there is none. The host owns a returned key and disposes it.</returns>
        ValueTask<SecureKeyResolution> ResolveAsync(SecureKeyRequest request, CancellationToken cancellationToken);
    }
}
