#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;

using RFiDGear.Contracts;
using RFiDGear.Services.Sam;

namespace RFiDGear.Services.SecureKeys
{
    /// <summary>
    /// The provider id of the keys that stay in a MIFARE SAM AV3. It is a built-in provider only so that the id is reserved (no extension can
    /// take it) and the key manager lists it. A key of the SAM is never bytes, so this provider never hands one out: it answers
    /// <see cref="SecureKeyFailureReason.Unsupported"/> to everything, and the commands that can use a key of the SAM read the binding
    /// themselves (<see cref="KeyAcquisition"/>) and give the reference to the SAM, never to this provider.
    /// </summary>
    internal sealed class SamKeyProvider : ISecureKeyProvider
    {
        /// <summary>The name a person sees.</summary>
        internal const string DisplayName = "MIFARE SAM AV3";

        /// <summary>Gets the id of the provider, as a project names it.</summary>
        internal static string ProviderId => SamKeyReference.ProviderId;

        /// <summary>Makes the registration the registry takes in as a built-in provider.</summary>
        /// <returns>The registration.</returns>
        internal static SecureKeyProviderRegistration Registration() => new SecureKeyProviderRegistration(ProviderId, DisplayName, new SamKeyProvider());

        /// <inheritdoc />
        public ValueTask<SecureKeyResolution> ResolveAsync(SecureKeyRequest request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();

            return new ValueTask<SecureKeyResolution>(SecureKeyResolution.Failed(SecureKeyFailureReason.Unsupported));
        }
    }
}