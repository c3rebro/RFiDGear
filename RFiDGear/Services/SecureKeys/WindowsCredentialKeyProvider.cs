#nullable enable
using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using RFiDGear.Contracts;
using Serilog;

namespace RFiDGear.Services.SecureKeys
{
    /// <summary>
    /// The provider that is part of RFiDGear: it reads a key from the Windows Credential Manager. A reference is the name of
    /// the credential below a fixed prefix, so a project can never name a credential of another application, whatever the
    /// reference says. The credential holds the hexadecimal digits of the key as UTF-16 text (see
    /// <see cref="CredentialBlob"/>); <c>cmdkey /generic:RFiDGear/key/&lt;reference&gt; /user:RFiDGear /pass:&lt;digits&gt;</c>
    /// stores one.
    /// <para>
    /// What this protects against is a key that ends up in a project file, a backup or an e-mail. It does not protect against
    /// a program that runs as the same user: the Credential Manager gives the secret to every process of that user.
    /// </para>
    /// </summary>
    internal sealed class WindowsCredentialKeyProvider : ISecureKeyProvider
    {
        /// <summary>The id that projects store for this provider. It never changes.</summary>
        internal const string ProviderId = "windows-credential-manager";

        /// <summary>The name shown to the user.</summary>
        internal const string DisplayName = "Windows Credential Manager";

        /// <summary>What every target name starts with. A reference is appended, nothing else is ever read.</summary>
        internal const string TargetPrefix = "RFiDGear/key/";

        /// <summary>The longest key in bytes: a 3K3DES key has 24.</summary>
        internal const int MaxKeyBytes = 32;

        private readonly ICredentialStore _store;

        /// <summary>Creates the provider.</summary>
        /// <param name="store">The vault.</param>
        internal WindowsCredentialKeyProvider(ICredentialStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        /// <summary>Creates the registration of the provider that works with the real Credential Manager.</summary>
        /// <returns>The registration.</returns>
        internal static SecureKeyProviderRegistration Registration() =>
            new SecureKeyProviderRegistration(ProviderId, DisplayName, new WindowsCredentialKeyProvider(new NativeCredentialStore()));

        /// <inheritdoc />
        public ValueTask<SecureKeyResolution> ResolveAsync(SecureKeyRequest request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            return new ValueTask<SecureKeyResolution>(Resolve(request.Reference, cancellationToken));
        }

        /// <summary>
        /// Stores a key under a reference, replacing the one that is there. A reference or a key that this provider would not
        /// read back is refused before the vault is asked.
        /// </summary>
        /// <param name="reference">The reference.</param>
        /// <param name="key">The key. The caller erases it.</param>
        /// <returns>The Win32 error code; <c>0</c> when the key was stored.</returns>
        internal int Store(string reference, ReadOnlySpan<byte> key)
        {
            if (!SecureKeySyntax.IsValidReference(reference) || key.Length == 0 || key.Length > MaxKeyBytes)
            {
                return CredentialErrors.InvalidParameter;
            }

            var blob = CredentialBlob.FromKey(key);

            try
            {
                return _store.Write(TargetPrefix + reference, blob);
            }
            catch (Exception e)
            {
                LogStoreFailure(e);
                return CredentialErrors.Failed;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(blob);
            }
        }

        /// <summary>Deletes the key under a reference.</summary>
        /// <param name="reference">The reference.</param>
        /// <returns>The Win32 error code; <c>0</c> when the key was deleted.</returns>
        internal int Remove(string reference)
        {
            if (!SecureKeySyntax.IsValidReference(reference))
            {
                return CredentialErrors.InvalidParameter;
            }

            try
            {
                return _store.Delete(TargetPrefix + reference);
            }
            catch (Exception e)
            {
                LogStoreFailure(e);
                return CredentialErrors.Failed;
            }
        }

        private SecureKeyResolution Resolve(string reference, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return SecureKeyResolution.Failed(SecureKeyFailureReason.Cancelled);
            }

            // The shape is checked here as well: this provider must not depend on who calls it.
            if (!SecureKeySyntax.IsValidReference(reference))
            {
                return SecureKeyResolution.Failed(SecureKeyFailureReason.NotFound);
            }

            CredentialRead read;

            try
            {
                read = _store.Read(TargetPrefix + reference);
            }
            catch (Exception e)
            {
                LogStoreFailure(e);
                return SecureKeyResolution.Failed(SecureKeyFailureReason.ProviderUnavailable);
            }

            var blob = read.Blob;

            try
            {
                if (read.Error != 0)
                {
                    return SecureKeyResolution.Failed(FailureOf(read.Error));
                }

                var lease = blob == null ? null : CredentialBlob.ToLease(blob, MaxKeyBytes);

                return lease == null
                    ? SecureKeyResolution.Failed(SecureKeyFailureReason.InvalidKey)
                    : SecureKeyResolution.Resolved(lease);
            }
            finally
            {
                if (blob != null)
                {
                    CryptographicOperations.ZeroMemory(blob);
                }
            }
        }

        private static SecureKeyFailureReason FailureOf(int error)
        {
            switch (error)
            {
                case CredentialErrors.NotFound:
                    return SecureKeyFailureReason.NotFound;

                case CredentialErrors.AccessDenied:
                    Log.ForContext<WindowsCredentialKeyProvider>().Warning("The Windows Credential Manager refused access (error {ErrorCode})", error);
                    return SecureKeyFailureReason.NotAuthorized;

                default:
                    Log.ForContext<WindowsCredentialKeyProvider>().Warning("The Windows Credential Manager could not be read (error {ErrorCode})", error);
                    return SecureKeyFailureReason.ProviderUnavailable;
            }
        }

        // The type only: what a vault throws may contain the name it was asked for.
        private static void LogStoreFailure(Exception e) =>
            Log.ForContext<WindowsCredentialKeyProvider>().Warning("The Windows Credential Manager failed ({ExceptionType})", e.GetType().FullName);
    }
}
