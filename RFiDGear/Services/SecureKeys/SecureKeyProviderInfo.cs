#nullable enable
namespace RFiDGear.Services.SecureKeys
{
    /// <summary>A secure key provider as the user interface lists it.</summary>
    /// <param name="Id">The provider id stored in projects.</param>
    /// <param name="DisplayName">The name shown to the user.</param>
    internal sealed record SecureKeyProviderInfo(string Id, string DisplayName);
}
