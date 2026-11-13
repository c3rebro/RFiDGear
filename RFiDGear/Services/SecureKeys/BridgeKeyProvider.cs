#nullable enable
using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

using RFiDGear.Bridge;
using RFiDGear.Contracts;

using Serilog;

namespace RFiDGear.Services.SecureKeys
{
    /// <summary>The way to the client that supplies keys over the external control bridge.</summary>
    internal interface IBridgeKeyChannel
    {
        /// <summary>Asks the client for a key.</summary>
        /// <param name="request">Which key, and what for.</param>
        /// <param name="cancellationToken">Cancelled when the host stops waiting; the call to the client is cancelled with it.</param>
        /// <returns>The answer of the client, or <see langword="null"/> when there is no client that supplies keys.</returns>
        Task<ResolveKeyResponse?> RequestKeyAsync(ResolveKeyRequest request, CancellationToken cancellationToken);
    }

    /// <summary>
    /// The secure key provider that stands for the program connected to the external control bridge: a project names a key as
    /// this provider and a reference, and the program, which keeps its keys in a vault of its own, is asked at the moment the task
    /// needs the key. The provider is a built-in one, so no extension can take its id. It exists from the start (the registry is
    /// built before the bridge is) and is bound to the connection later: without a client that registered as supplier it answers
    /// <see cref="SecureKeyFailureReason.ProviderUnavailable"/> and the task ends with <c>ERROR.KeyUnavailable</c>, as for any
    /// provider that is not there.
    /// <para>
    /// What it never does: it never writes a key, a reference or an answer of the client to the log (only the type of an
    /// exception), never keeps a key (the lease it hands out is the caller's), and zeroes the buffer it decoded the key into. What
    /// it cannot do: the base64 text of the key lives in the message the JSON library read, which neither side can erase.
    /// </para>
    /// </summary>
    internal sealed class BridgeKeyProvider : ISecureKeyProvider
    {
        /// <summary>The id of the provider, as a project names it. Reserved: the registry accepts no extension with this id.</summary>
        internal const string ProviderId = "bridge-client";

        /// <summary>The name a person sees.</summary>
        internal const string DisplayName = "Connected external application";


        private IBridgeKeyChannel? _channel;

        /// <summary>Gets the provider of this process, which the registry holds and the bridge binds to its connection.</summary>
        internal static BridgeKeyProvider Shared { get; } = new BridgeKeyProvider();

        /// <summary>Gets whether the provider is bound to a way to a client, which is the case while the bridge listens.</summary>
        internal bool IsBound => Volatile.Read(ref _channel) != null;

        /// <summary>Makes the registration the registry takes in as a built-in provider.</summary>
        /// <returns>The registration of the shared provider.</returns>
        internal static SecureKeyProviderRegistration Registration() => new SecureKeyProviderRegistration(ProviderId, DisplayName, Shared);

        /// <summary>Binds the provider to the way to the client. A channel that is bound later replaces this one.</summary>
        /// <param name="channel">The channel.</param>
        internal void Attach(IBridgeKeyChannel channel) => Volatile.Write(ref _channel, channel ?? throw new ArgumentNullException(nameof(channel)));

        /// <summary>Unbinds the provider, but only from this channel: a channel that replaced it stays.</summary>
        /// <param name="channel">The channel to let go of.</param>
        internal void Detach(IBridgeKeyChannel channel) => Interlocked.CompareExchange(ref _channel, null, channel);

        /// <inheritdoc />
        public async ValueTask<SecureKeyResolution> ResolveAsync(SecureKeyRequest request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var channel = Volatile.Read(ref _channel);

            if (channel == null)
            {
                return SecureKeyResolution.Failed(SecureKeyFailureReason.ProviderUnavailable);
            }

            if (!TryMap(request, out var wire))
            {
                // A kind of key the protocol of this version has no name for: the client is not asked for what it cannot name.
                return SecureKeyResolution.Failed(SecureKeyFailureReason.Unsupported);
            }

            ResolveKeyResponse? answer;

            try
            {
                answer = await channel.RequestKeyAsync(wire, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return SecureKeyResolution.Failed(SecureKeyFailureReason.Cancelled);
            }
            catch (Exception e)
            {
                // The client went away, answered with an error or sent something unreadable. Only the type: what the library says
                // about it can hold what was sent.
                Log.ForContext<BridgeKeyProvider>().Warning("The connected application could not supply a key ({ExceptionType})", e.GetType().FullName);

                return SecureKeyResolution.Failed(SecureKeyFailureReason.ProviderUnavailable);
            }

            return Interpret(answer);
        }

        private static SecureKeyResolution Interpret(ResolveKeyResponse? answer)
        {
            if (answer == null)
            {
                return SecureKeyResolution.Failed(SecureKeyFailureReason.ProviderUnavailable);
            }

            switch (answer.Result)
            {
                case KeyResolveResults.Resolved:
                    return ToLease(answer.Key);
                case KeyResolveResults.NotFound:
                    return SecureKeyResolution.Failed(SecureKeyFailureReason.NotFound);
                case KeyResolveResults.NotAuthorized:
                    return SecureKeyResolution.Failed(SecureKeyFailureReason.NotAuthorized);
                case KeyResolveResults.InvalidKey:
                    return SecureKeyResolution.Failed(SecureKeyFailureReason.InvalidKey);
                case KeyResolveResults.Unsupported:
                    return SecureKeyResolution.Failed(SecureKeyFailureReason.Unsupported);
                default:
                    // Not the value: it is text of the client's.
                    Log.ForContext<BridgeKeyProvider>().Warning("The connected application answered with a result this version does not know");

                    return SecureKeyResolution.Failed(SecureKeyFailureReason.ProviderUnavailable);
            }
        }

        // Decodes into a buffer on the stack, copies into the pinned lease, and zeroes the buffer. The buffer holds the longest key
        // there is: a text that decodes to more does not fit, and the decoding stops at once.
        private static SecureKeyResolution ToLease(string? text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return SecureKeyResolution.Failed(SecureKeyFailureReason.InvalidKey);
            }

            Span<byte> buffer = stackalloc byte[BridgeProtocol.MaxKeyBytes];

            try
            {
                if (!Convert.TryFromBase64String(text, buffer, out var written) || written == 0)
                {
                    return SecureKeyResolution.Failed(SecureKeyFailureReason.InvalidKey);
                }

                return SecureKeyResolution.Resolved(new SecureKeyLease(buffer[..written]));
            }
            finally
            {
                CryptographicOperations.ZeroMemory(buffer);
            }
        }

        private static bool TryMap(SecureKeyRequest request, out ResolveKeyRequest wire)
        {
            wire = null!;

            var algorithm = request.Algorithm switch
            {
                SecureKeyAlgorithm.Des => KeyAlgorithms.Des,
                SecureKeyAlgorithm.TripleDes2K => KeyAlgorithms.TripleDes2K,
                SecureKeyAlgorithm.TripleDes3K => KeyAlgorithms.TripleDes3K,
                SecureKeyAlgorithm.Aes128 => KeyAlgorithms.Aes128,
                SecureKeyAlgorithm.Classic6Byte => KeyAlgorithms.Classic6Byte,
                _ => null,
            };

            var usage = request.Usage switch
            {
                SecureKeyUsage.Authenticate => KeyUsages.Authenticate,
                SecureKeyUsage.ChangeKeyCurrent => KeyUsages.ChangeKeyCurrent,
                SecureKeyUsage.ChangeKeyNew => KeyUsages.ChangeKeyNew,
                _ => null,
            };

            if (algorithm == null || usage == null)
            {
                return false;
            }

            wire = new ResolveKeyRequest
            {
                SlotId = request.SlotId,
                Reference = request.Reference,
                Algorithm = algorithm,
                Usage = usage,
                ApplicationId = request.Scope?.ApplicationId,
                KeyNumber = request.Scope?.KeyNumber,
                RunId = request.RunId,
                TaskIndex = request.TaskIndex,
                CardUid = request.CardUid,
                Interactive = request.Interactive,
            };

            return true;
        }
    }
}
