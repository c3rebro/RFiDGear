namespace RFiDGear.ExtensionLibraries.CardCheck
{
    /// <summary>
    /// What the first report says about the chip, read from its form fields. The report analyser fills this in;
    /// the template rules and the session work from it.
    /// </summary>
    public sealed record ReportAnalysis
    {
        /// <summary>Whether the report marks the chip as supported (the field is exactly <c>Yes</c>).</summary>
        public bool Supported { get; init; }

        /// <summary>Whether the report marks the chip as usable for programming (the field is exactly <c>Yes</c>).</summary>
        public bool Programmable { get; init; }

        /// <summary>The chip type as the report names it; empty if it does not.</summary>
        public string ChipType { get; init; } = string.Empty;

        /// <summary>The free-memory field as written; see <see cref="TemplateRules.ParseFreeMemory"/>.</summary>
        public string FreeMemoryText { get; init; } = string.Empty;

        /// <summary>The notes of the report; empty if there are none.</summary>
        public string Notes { get; init; } = string.Empty;
    }
}
