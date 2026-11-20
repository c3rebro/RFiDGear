using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace RFiDGear.Bridge
{
    /// <summary>
    /// What a task that compares values found, side by side: which settings it compared, what it expected, what it read and
    /// whether each one matched. It is the detail behind <see cref="TaskOutcomeDto.IsSuccess"/> of a check, for a client that has
    /// to say why a check failed (an acceptance record, for instance) and not only that it did.
    /// <para>
    /// It holds numbers and fixed tokens only: never a key, never text that a card or a person wrote, never the content of a file.
    /// A client treats what it does not know as absent: another <see cref="Kind"/> or a later <see cref="SchemaVersion"/> may carry
    /// more, and <see cref="Checks"/> can be read without knowing the kind. Which run, request and task it belongs to is the
    /// <see cref="RunResultDto"/> and the <see cref="TaskOutcomeDto.Position"/> around it.
    /// </para>
    /// </summary>
    public sealed record VerificationDetailsDto
    {
        /// <summary>Gets the version of this layout (<c>1</c>). A later version only adds members.</summary>
        [JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; init; }

        /// <summary>Gets what was verified, one of <see cref="DetailKinds"/>. A client that does not know it reads <see cref="Checks"/> and ignores the members that belong to the kind.</summary>
        [JsonPropertyName("kind")]
        public string Kind { get; init; } = string.Empty;

        /// <summary>Gets whether the settings that were compared matched as a whole, as the task decided; nothing when it did not decide.</summary>
        [JsonPropertyName("overallMatch")]
        public bool? OverallMatch { get; init; }

        /// <summary>Gets how the single results make the whole, one of <see cref="CompareModes"/>; nothing when that does not apply.</summary>
        [JsonPropertyName("compareMode")]
        public string? CompareMode { get; init; }

        /// <summary>Gets the DESFire application the file is in (<see cref="DetailKinds.DesfireFileSettings"/>).</summary>
        [JsonPropertyName("applicationId")]
        public int? ApplicationId { get; init; }

        /// <summary>Gets the number of the file (<see cref="DetailKinds.DesfireFileSettings"/>).</summary>
        [JsonPropertyName("fileNumber")]
        public int? FileNumber { get; init; }

        /// <summary>Gets a fixed token that says why something could not be checked, one of <see cref="DetailErrors"/>; nothing when everything that was selected was checked.</summary>
        [JsonPropertyName("errorCode")]
        public string? ErrorCode { get; init; }

        /// <summary>Gets one entry per setting the task knows how to compare, in a fixed order, whether it compared it or not.</summary>
        [JsonPropertyName("checks")]
        public IReadOnlyList<VerificationCheckDto> Checks { get; init; } = Array.Empty<VerificationCheckDto>();
    }

    /// <summary>One setting a task compared, or could have compared.</summary>
    public sealed record VerificationCheckDto
    {
        /// <summary>Gets the setting, one of <see cref="CheckNames"/> for the kinds this version knows.</summary>
        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        /// <summary>Gets whether the task compared this setting. When it did not, nothing else is reported for it.</summary>
        [JsonPropertyName("compared")]
        public bool Compared { get; init; }

        /// <summary>Gets how it ended, one of <see cref="CheckResults"/>.</summary>
        [JsonPropertyName("result")]
        public string Result { get; init; } = string.Empty;

        /// <summary>Gets how a number was compared, one of <see cref="CheckRules"/>; nothing for a setting that is compared for equality only.</summary>
        [JsonPropertyName("rule")]
        public string? Rule { get; init; }

        /// <summary>Gets the expected value (a number, an enumeration value or a nibble of access rights); nothing when the setting was not compared.</summary>
        [JsonPropertyName("expected")]
        public long? Expected { get; init; }

        /// <summary>Gets the value that was read; nothing when the setting was not compared or could not be read.</summary>
        [JsonPropertyName("actual")]
        public long? Actual { get; init; }
    }

    /// <summary>The values of <see cref="VerificationDetailsDto.Kind"/>.</summary>
    [SuppressMessage("Major Code Smell", "S2339:Public constant members should not be used", Justification = WireConstants.Justification)]
    public static class DetailKinds
    {
        /// <summary>The settings of a DESFire file (the task "Check: File Settings"): file type, size, communication mode and the four access rights.</summary>
        public const string DesfireFileSettings = "desfire.fileSettings";
    }

    /// <summary>The values of <see cref="VerificationCheckDto.Name"/> for <see cref="DetailKinds.DesfireFileSettings"/>, in the order a result lists them.</summary>
    [SuppressMessage("Major Code Smell", "S2339:Public constant members should not be used", Justification = WireConstants.Justification)]
    public static class CheckNames
    {
        /// <summary>The type of the file, as the number DESFire gives it.</summary>
        public const string FileType = "fileType";

        /// <summary>The size of a data file in bytes, compared by <see cref="VerificationCheckDto.Rule"/>.</summary>
        public const string FileSize = "fileSize";

        /// <summary>The communication mode, as the number DESFire gives it.</summary>
        public const string CommunicationMode = "communicationMode";

        /// <summary>The key number that may read the file (a nibble: 0 to 13 a key, 14 free, 15 never).</summary>
        public const string AccessRead = "accessRead";

        /// <summary>The key number that may write to the file (a nibble).</summary>
        public const string AccessWrite = "accessWrite";

        /// <summary>The key number that may read and write (a nibble).</summary>
        public const string AccessReadWrite = "accessReadWrite";

        /// <summary>The key number that may change the access rights (a nibble).</summary>
        public const string AccessChange = "accessChange";
    }

    /// <summary>The values of <see cref="VerificationCheckDto.Result"/>.</summary>
    [SuppressMessage("Major Code Smell", "S2339:Public constant members should not be used", Justification = WireConstants.Justification)]
    public static class CheckResults
    {
        /// <summary>The setting was compared and is as expected.</summary>
        public const string Match = "match";

        /// <summary>The setting was compared and is not as expected.</summary>
        public const string Mismatch = "mismatch";

        /// <summary>The setting was to be compared, but its value could not be read.</summary>
        public const string NotRead = "notRead";

        /// <summary>The task was not set to compare this setting.</summary>
        public const string NotCompared = "notCompared";
    }

    /// <summary>The values of <see cref="VerificationCheckDto.Rule"/>.</summary>
    [SuppressMessage("Major Code Smell", "S2339:Public constant members should not be used", Justification = WireConstants.Justification)]
    public static class CheckRules
    {
        /// <summary>The value that was read equals the expected one.</summary>
        public const string Exact = "exact";

        /// <summary>The value that was read is at least the expected one.</summary>
        public const string AtLeast = "atLeast";

        /// <summary>The value that was read is at most the expected one.</summary>
        public const string AtMost = "atMost";
    }

    /// <summary>The values of <see cref="VerificationDetailsDto.CompareMode"/>.</summary>
    [SuppressMessage("Major Code Smell", "S2339:Public constant members should not be used", Justification = WireConstants.Justification)]
    public static class CompareModes
    {
        /// <summary>Every setting that was compared had to match.</summary>
        public const string All = "all";

        /// <summary>One setting that matched was enough.</summary>
        public const string Any = "any";
    }

    /// <summary>The values of <see cref="VerificationDetailsDto.ErrorCode"/>.</summary>
    [SuppressMessage("Major Code Smell", "S2339:Public constant members should not be used", Justification = WireConstants.Justification)]
    public static class DetailErrors
    {
        /// <summary>At least one setting that was selected could not be read, so the card did not give the task what it needed to decide on it.</summary>
        public const string NotRead = "notRead";
    }
}
