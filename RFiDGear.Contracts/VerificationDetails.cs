using System.Collections.Generic;

namespace RFiDGear.Contracts
{
    /// <summary>
    /// What a task that compares values found, side by side: which settings it compared, what it expected, what it read and
    /// whether each one matched. It is the detail behind <see cref="TaskOutcome.IsSuccess"/> of a check, for a program that has to
    /// say why a check failed (an acceptance record, for instance) and not only that it did.
    /// <para>
    /// It holds numbers and fixed tokens only: never a key, never text that a card or a person wrote, never the content of a file.
    /// A reader treats everything it does not know as absent: another <see cref="Kind"/> or a later <see cref="SchemaVersion"/> may
    /// carry more, and <see cref="Checks"/> can always be read without knowing the kind.
    /// </para>
    /// </summary>
    /// <param name="SchemaVersion">The version of this layout (<c>1</c>). A later version only adds; a reader that does not know it reads <paramref name="Checks"/> and ignores the rest.</param>
    /// <param name="Kind">What was verified, one of <see cref="VerificationKinds"/>; a reader that does not know it ignores the kind-specific members.</param>
    /// <param name="Checks">One entry per setting the task knows how to compare, in a fixed order, whether the task compared it or not.</param>
    public sealed record VerificationDetails(
        int SchemaVersion,
        string Kind,
        IReadOnlyList<VerificationCheck> Checks)
    {
        /// <summary>Gets whether the settings that were compared matched as a whole, as the task decided (see <see cref="CompareMode"/>); <see langword="null"/> when the task did not decide.</summary>
        public bool? OverallMatch { get; init; }

        /// <summary>Gets how the single results make the whole, one of <see cref="VerificationCompareModes"/>; <see langword="null"/> when that does not apply.</summary>
        public string? CompareMode { get; init; }

        /// <summary>Gets the DESFire application the file is in, for <see cref="VerificationKinds.DesfireFileSettings"/>.</summary>
        public int? ApplicationId { get; init; }

        /// <summary>Gets the number of the file, for <see cref="VerificationKinds.DesfireFileSettings"/>.</summary>
        public int? FileNumber { get; init; }

        /// <summary>Gets a fixed token that says why something could not be checked, one of <see cref="VerificationErrors"/>; <see langword="null"/> when everything that was selected was checked.</summary>
        public string? ErrorCode { get; init; }
    }

    /// <summary>
    /// One setting a task compared, or could have compared.
    /// </summary>
    /// <param name="Name">The setting, one of <see cref="VerificationCheckNames"/> for the kinds this version knows.</param>
    /// <param name="Compared">Whether the task compared this setting. When it did not, nothing else is reported for it.</param>
    /// <param name="Result">How it ended, one of <see cref="VerificationResults"/>.</param>
    public sealed record VerificationCheck(string Name, bool Compared, string Result)
    {
        /// <summary>Gets how the numbers were compared, one of <see cref="VerificationRules"/>; <see langword="null"/> when they were compared for equality or the setting is no number to compare with a rule.</summary>
        public string? Rule { get; init; }

        /// <summary>Gets the expected value (a number, an enumeration value or a nibble of access rights as the task writes it); <see langword="null"/> when the setting was not compared.</summary>
        public long? Expected { get; init; }

        /// <summary>Gets the value that was read; <see langword="null"/> when the setting was not compared or could not be read.</summary>
        public long? Actual { get; init; }
    }

    /// <summary>The values of <see cref="VerificationDetails.Kind"/>.</summary>
    public static class VerificationKinds
    {
        /// <summary>The settings of a DESFire file (the task "Check: File Settings"): file type, size, communication mode and the four access rights.</summary>
        public static string DesfireFileSettings { get; } = "desfire.fileSettings";
    }

    /// <summary>The values of <see cref="VerificationCheck.Name"/> for <see cref="VerificationKinds.DesfireFileSettings"/>, in the order a result lists them.</summary>
    public static class VerificationCheckNames
    {
        /// <summary>The type of the file (standard data, backup, value, record, ...), as the number DESFire gives it.</summary>
        public static string FileType { get; } = "fileType";

        /// <summary>The size of a data file in bytes, compared by <see cref="VerificationCheck.Rule"/>.</summary>
        public static string FileSize { get; } = "fileSize";

        /// <summary>The communication mode (plain, MACed, enciphered), as the number DESFire gives it.</summary>
        public static string CommunicationMode { get; } = "communicationMode";

        /// <summary>The key number that may read the file (a nibble: 0 to 13 a key, 14 free, 15 never).</summary>
        public static string AccessRead { get; } = "accessRead";

        /// <summary>The key number that may write to the file (a nibble).</summary>
        public static string AccessWrite { get; } = "accessWrite";

        /// <summary>The key number that may read and write (a nibble).</summary>
        public static string AccessReadWrite { get; } = "accessReadWrite";

        /// <summary>The key number that may change the access rights (a nibble).</summary>
        public static string AccessChange { get; } = "accessChange";
    }

    /// <summary>The values of <see cref="VerificationCheck.Result"/>.</summary>
    public static class VerificationResults
    {
        /// <summary>The setting was compared and is as expected.</summary>
        public static string Match { get; } = "match";

        /// <summary>The setting was compared and is not as expected.</summary>
        public static string Mismatch { get; } = "mismatch";

        /// <summary>The setting was to be compared, but its value could not be read (a data file that reports a size of 0, for instance).</summary>
        public static string NotRead { get; } = "notRead";

        /// <summary>The task was not set to compare this setting.</summary>
        public static string NotCompared { get; } = "notCompared";
    }

    /// <summary>The values of <see cref="VerificationCheck.Rule"/>: how the value that was read relates to the expected one.</summary>
    public static class VerificationRules
    {
        /// <summary>The value that was read equals the expected one.</summary>
        public static string Exact { get; } = "exact";

        /// <summary>The value that was read is at least the expected one.</summary>
        public static string AtLeast { get; } = "atLeast";

        /// <summary>The value that was read is at most the expected one.</summary>
        public static string AtMost { get; } = "atMost";
    }

    /// <summary>The values of <see cref="VerificationDetails.CompareMode"/>.</summary>
    public static class VerificationCompareModes
    {
        /// <summary>Every setting that was compared had to match.</summary>
        public static string All { get; } = "all";

        /// <summary>One setting that matched was enough.</summary>
        public static string Any { get; } = "any";
    }

    /// <summary>The values of <see cref="VerificationDetails.ErrorCode"/>.</summary>
    public static class VerificationErrors
    {
        /// <summary>At least one setting that was selected could not be read, so the card did not give the task what it needed to decide on it.</summary>
        public static string NotRead { get; } = "notRead";
    }
}
