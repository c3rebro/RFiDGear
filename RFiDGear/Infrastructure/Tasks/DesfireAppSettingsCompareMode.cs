namespace RFiDGear.Infrastructure.Tasks
{
    /// <summary>
    /// How the key-settings byte read from a card application is compared with the settings the Check: App Config task
    /// expects. The four lowest bits of the byte (0x01 to 0x08) are independent flags; the upper nibble is a change-key
    /// policy, which is a value and not a set of bits, so only <see cref="Exact"/> takes it into account.
    /// The names are saved in project files: add members at the end, never rename or remove one.
    /// </summary>
    public enum DesfireAppSettingsCompareMode
    {
        /// <summary>Every flag that is ticked must be set on the card; further flags on the card are allowed.</summary>
        AllSelected = 0,

        /// <summary>At least one of the flags that are ticked must be set on the card.</summary>
        AnySelected = 1,

        /// <summary>The whole byte must equal the expected one: the ticked flags, no other flag, and the change-key policy.</summary>
        Exact = 2,
    }
}
