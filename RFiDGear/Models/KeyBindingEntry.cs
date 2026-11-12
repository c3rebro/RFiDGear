namespace RFiDGear.Models
{
    /// <summary>
    /// States that one key slot of a task gets its key from a secure key provider, not from the project. It is stored in the
    /// project file in place of the key: it names the slot, the provider, and a reference that only the provider
    /// understands. It contains no secret.
    /// <para>
    /// A task that has an entry for a slot never uses the embedded key of that slot, even when the entry turns out to be
    /// unusable: a slot is bound or it is not, and a broken binding fails the task. It is not a fallback.
    /// </para>
    /// </summary>
    public class KeyBindingEntry
    {
        /// <summary>
        /// Gets or sets the stable identifier of the slot, for example <c>desfire.app.current</c>. The ids are fixed in
        /// <c>SecureKeySlots</c> and never change, because they are stored in project files.
        /// </summary>
        public string Slot { get; set; } = string.Empty;

        /// <summary>Gets or sets the id of the provider, as in <c>[SecureKeyProvider]</c>.</summary>
        public string ProviderId { get; set; } = string.Empty;

        /// <summary>Gets or sets the reference the provider resolves to a key. Not a secret, and not shown in logs.</summary>
        public string Reference { get; set; } = string.Empty;

        /// <summary>Creates an independent copy.</summary>
        /// <returns>A new entry with the same values.</returns>
        public KeyBindingEntry Clone() => new KeyBindingEntry { Slot = Slot, ProviderId = ProviderId, Reference = Reference };

        /// <inheritdoc />
        public override string ToString() => $"KeyBindingEntry {{ Slot = {Slot}, ProviderId = {ProviderId} }}";
    }
}
