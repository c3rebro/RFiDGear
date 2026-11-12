namespace RFiDGear.Contracts
{
    /// <summary>
    /// Where on the card a key is used. A provider can use it to derive a key (diversification) or to select a key on
    /// a secure element; a plain vault ignores it. Immutable; carries no secret.
    /// </summary>
    public sealed class SecureKeyScope
    {
        /// <summary>A scope that says nothing: the slot is not tied to an application or a key number.</summary>
        public static SecureKeyScope None { get; } = new SecureKeyScope();

        /// <summary>
        /// Gets the DESFire application the key belongs to. <c>0</c> is the card itself (the PICC master application).
        /// <c>null</c> when the slot is not tied to an application, as for a MIFARE Classic key.
        /// </summary>
        public uint? ApplicationId { get; init; }

        /// <summary>Gets the number of the key within its application, or <c>null</c> when it has none.</summary>
        public int? KeyNumber { get; init; }

        /// <inheritdoc />
        public override string ToString() =>
            $"SecureKeyScope {{ ApplicationId = {ApplicationId?.ToString() ?? "-"}, KeyNumber = {KeyNumber?.ToString() ?? "-"} }}";
    }
}
