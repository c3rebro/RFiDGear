namespace RFiDGear.ExtensionLibraries.CardCheck
{
    /// <summary>
    /// What the first run of the check says about a chip. It decides what the operator is told and whether the
    /// programming step follows.
    /// </summary>
    public enum ChipVerdict
    {
        /// <summary>The chip cannot be programmed (whatever else the report says about it).</summary>
        NotSupported,

        /// <summary>The chip is recognised and can be programmed, but there is not enough free memory for any template.</summary>
        SupportedOnly,

        /// <summary>The chip is recognised, can be programmed and has memory for at least one template.</summary>
        Programmable,

        /// <summary>
        /// The report contradicts itself: the chip is marked programmable but not supported. No outcome is
        /// assumed; the operator is told to look at the report.
        /// </summary>
        Indeterminate,
    }
}
