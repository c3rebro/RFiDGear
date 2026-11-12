#nullable enable
using RFiDGear.Contracts;

namespace RFiDGear.Services.SecureKeys
{
    /// <summary>
    /// A provider that is part of RFiDGear itself and is handed to the registry directly, not found through MEF. Its id is
    /// reserved: no extension can take it over.
    /// </summary>
    /// <param name="Id">The provider id stored in projects; see <see cref="SecureKeySyntax.IsValidProviderId"/>.</param>
    /// <param name="DisplayName">The name shown to the user.</param>
    /// <param name="Provider">The provider.</param>
    internal sealed record SecureKeyProviderRegistration(string Id, string DisplayName, ISecureKeyProvider Provider);
}
