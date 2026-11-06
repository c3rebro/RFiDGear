namespace RFiDGear.ExtensionLibraries.CardCheck
{
    /// <summary>What kind of free-memory figure a report carries.</summary>
    public enum MemoryKind
    {
        /// <summary>The report says nothing usable about free memory (missing, <c>N/A</c>, text without a number).</summary>
        Unknown,

        /// <summary>A DESFire chip: <see cref="FreeMemory.Amount"/> is the number of free bytes.</summary>
        Desfire,

        /// <summary>A Classic chip: <see cref="FreeMemory.Amount"/> is the number of free sectors.</summary>
        Classic,
    }

    /// <summary>
    /// The free memory of the chip as the report states it: bytes on a DESFire chip, sectors on a Classic chip.
    /// </summary>
    /// <param name="Kind">Which of the two it is, or <see cref="MemoryKind.Unknown"/>.</param>
    /// <param name="Amount">Free bytes or free sectors; zero when the kind is unknown.</param>
    public sealed record FreeMemory(MemoryKind Kind, int Amount)
    {
        /// <summary>The figure for a report that says nothing usable.</summary>
        public static FreeMemory Unknown { get; } = new FreeMemory(MemoryKind.Unknown, 0);
    }
}
