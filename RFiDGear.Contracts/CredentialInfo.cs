namespace RFiDGear.Contracts
{
    /// <summary>
    /// One credential the reader sees: the card on the reader, or an LF or LEGIC credential that was read together with
    /// it. It holds no keys and no card data.
    /// </summary>
    /// <param name="Uid">The identifier as the reader reports it, trimmed and in upper case. Never blank.</param>
    /// <param name="TypeCode">
    /// RFiDGear's numeric code for the card type. The numbers are fixed values, not positions in a list, so a code means
    /// the same in every release. Compare this, not <paramref name="DisplayName"/>. This assembly does not list the
    /// codes: they are those of RFiDGear's card type list and, for the LF and HF families, the type numbers of the reader
    /// SDK. To show a type to people, use <paramref name="DisplayName"/>.
    /// </param>
    /// <param name="DisplayName">
    /// The name of the card type for people to read, for example <c>DESFireEV1_8K</c>, or <c>Unknown</c> if the code has no
    /// name. Several names can share one code, so this is not for comparing.
    /// </param>
    /// <param name="IsPrimary">
    /// <c>true</c> for the first credential of a snapshot, the main one; <c>false</c> for the ones read together with it.
    /// </param>
    public sealed record CredentialInfo(string Uid, int TypeCode, string DisplayName, bool IsPrimary)
    {
        /// <summary>A short description for logs: the type and whether it is primary, but not the identifier, like <see cref="ReaderSnapshot"/>.</summary>
        /// <returns>The description.</returns>
        public override string ToString() => $"CredentialInfo {{ TypeCode = {TypeCode}, DisplayName = {DisplayName}, IsPrimary = {IsPrimary} }}";
    }
}
