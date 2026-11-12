namespace RFiDGear.ExtensionSdk
{
    /// <summary>
    /// The metadata of a secure key provider, as <see cref="SecureKeyProviderAttribute"/> exports it. The host reads the
    /// provider list through this view; an extension has no reason to implement it.
    /// </summary>
    public interface ISecureKeyProviderMetadata
    {
        /// <summary>Gets the provider id stored in projects.</summary>
        string Id { get; }

        /// <summary>Gets the name shown to the user.</summary>
        string DisplayName { get; }
    }
}
