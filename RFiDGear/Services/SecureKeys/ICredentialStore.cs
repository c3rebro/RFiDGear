#nullable enable
namespace RFiDGear.Services.SecureKeys
{
    /// <summary>
    /// The part of the Windows Credential Manager that the key provider needs: generic credentials, read and written by their
    /// target name. It is a seam, so that the tests of the provider never touch the vault of the person who runs them.
    /// Nothing here throws because the vault refuses: an error is a Win32 error code, <c>0</c> for none.
    /// </summary>
    internal interface ICredentialStore
    {
        /// <summary>Reads the secret of a generic credential.</summary>
        /// <param name="targetName">The target name of the credential.</param>
        /// <returns>The error, or the secret; the caller owns the array and erases it.</returns>
        CredentialRead Read(string targetName);

        /// <summary>Writes a generic credential that lives on this computer, replacing one with the same target name.</summary>
        /// <param name="targetName">The target name of the credential.</param>
        /// <param name="blob">The secret. The caller erases the array afterwards.</param>
        /// <returns>The Win32 error code, <c>0</c> when the credential was written.</returns>
        int Write(string targetName, byte[] blob);

        /// <summary>Deletes a generic credential.</summary>
        /// <param name="targetName">The target name of the credential.</param>
        /// <returns>The Win32 error code, <c>0</c> when the credential was deleted.</returns>
        int Delete(string targetName);
    }

    /// <summary>The answer to a read: an error code, or the secret.</summary>
    /// <param name="Error">The Win32 error code; <c>0</c> when the credential was read.</param>
    /// <param name="Blob">The secret as the vault holds it; <c>null</c> on an error.</param>
    internal readonly record struct CredentialRead(int Error, byte[]? Blob)
    {
        /// <inheritdoc />
        public override string ToString() => $"CredentialRead [error {Error}, redacted]";
    }

    /// <summary>The Win32 error codes of the Credential Manager that the provider tells apart.</summary>
    internal static class CredentialErrors
    {
        /// <summary>ERROR_ACCESS_DENIED: the vault refuses the caller.</summary>
        internal const int AccessDenied = 5;

        /// <summary>ERROR_INVALID_PARAMETER: used for input the provider refuses before it asks the vault.</summary>
        internal const int InvalidParameter = 87;

        /// <summary>ERROR_NOT_FOUND: there is no credential with that target name.</summary>
        internal const int NotFound = 1168;

        /// <summary>Not a Win32 error: the store threw instead of answering.</summary>
        internal const int Failed = -1;
    }
}
