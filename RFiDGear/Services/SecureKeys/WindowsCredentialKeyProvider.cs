#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
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
    /// <see cref="CredentialBlob"/>); <c>cmdkey /generic:RFiDGear/key/&lt;reference&gt; /user:RFiDGear /pass</c> stores one and
    /// asks for the digits at a prompt. The digits do not belong on the command line (<c>/pass:&lt;digits&gt;</c>): other
    /// programs can read a command line, and shell histories and process logs keep it.
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

        /// <summary>
        /// Lists the references that have a key in the Credential Manager: the names below the prefix of this provider, that this
        /// provider would accept as references. Only the names are asked for; no key is read.
        /// </summary>
        /// <returns>The error, and the references in alphabetical order; none on an error.</returns>
        internal StoredKeyList List()
        {
            CredentialList listed;

            try
            {
                listed = _store.Enumerate(TargetPrefix);
            }
            catch (Exception e)
            {
                LogStoreFailure(e);
                return new StoredKeyList(CredentialErrors.Failed, Array.Empty<string>());
            }

            if (listed.Error != 0)
            {
                return new StoredKeyList(listed.Error, Array.Empty<string>());
            }

            // The vault is asked for the names below the prefix, and the answer is checked all the same: a name of another
            // application, or one that this provider would refuse as a reference, is not ours to offer.
            var references = listed.TargetNames
                .Where(name => name.StartsWith(TargetPrefix, StringComparison.OrdinalIgnoreCase))
                .Select(name => name.Substring(TargetPrefix.Length))
                .Where(SecureKeySyntax.IsValidReference)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(reference => reference, StringComparer.OrdinalIgnoreCase)
                .ThenBy(reference => reference, StringComparer.Ordinal)
                .ToList();

            return new StoredKeyList(0, references);
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

                // On the stack: the key never exists as an array that waits for the garbage collector.
                Span<byte> scratch = stackalloc byte[MaxKeyBytes];
                var lease = blob == null ? null : CredentialBlob.ToLease(blob, scratch);

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

    /// <summary>The answer to a listing of the stored keys: an error code, or the references that have a key.</summary>
    /// <param name="Error">The Win32 error code; <c>0</c> when the keys were listed.</param>
    /// <param name="References">The references, in alphabetical order; empty on an error.</param>
    internal readonly record struct StoredKeyList(int Error, IReadOnlyList<string> References);
}
