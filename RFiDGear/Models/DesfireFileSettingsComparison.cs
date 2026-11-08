using System.Collections.Generic;

namespace RFiDGear.Models
{
    /// <summary>
    /// The expected and the actual settings of one DESFire file, side by side, as one run of the file-settings check
    /// found them. It is the result of that attempt and cannot be changed afterwards. It holds numbers and a verdict
    /// only: no key, and nothing that was read from the file.
    /// </summary>
    internal sealed record DesfireFileSettingsComparison
    {
        /// <summary>Gets the application identifier.</summary>
        public int ApplicationId { get; init; }

        /// <summary>Gets the file number.</summary>
        public int FileNumber { get; init; }

        /// <summary>Gets the expected file type.</summary>
        public int ExpectedFileType { get; init; }

        /// <summary>Gets the actual file type.</summary>
        public int ActualFileType { get; init; }

        /// <summary>Gets the expected file size in bytes.</summary>
        public long ExpectedFileSize { get; init; }

        /// <summary>Gets the actual file size in bytes.</summary>
        public long ActualFileSize { get; init; }

        /// <summary>Gets the expected communication mode.</summary>
        public int ExpectedCommunicationMode { get; init; }

        /// <summary>Gets the actual communication mode.</summary>
        public int ActualCommunicationMode { get; init; }

        /// <summary>Gets the expected read/write access-right byte: read in the low nibble, write in the high one.</summary>
        public int ExpectedAccessRights0 { get; init; }

        /// <summary>Gets the actual read/write access-right byte.</summary>
        public int ActualAccessRights0 { get; init; }

        /// <summary>Gets the expected read-write/change access-right byte: read-write in the low nibble, change in the high one.</summary>
        public int ExpectedAccessRights1 { get; init; }

        /// <summary>Gets the actual read-write/change access-right byte.</summary>
        public int ActualAccessRights1 { get; init; }

        /// <summary>Gets a value indicating whether every expected field matches the actual one.</summary>
        public bool Matches { get; init; }

        /// <summary>
        /// Lists what is logged with the outcome of the task: each field by name, as a number or a flag. This list is the
        /// only thing that reaches the log, so a field added to the record is not logged until it is added here.
        /// </summary>
        /// <returns>The named values.</returns>
        internal IReadOnlyDictionary<string, object> ToLogValues() => new Dictionary<string, object>
        {
            [nameof(ApplicationId)] = ApplicationId,
            [nameof(FileNumber)] = FileNumber,
            [nameof(ExpectedFileType)] = ExpectedFileType,
            [nameof(ActualFileType)] = ActualFileType,
            [nameof(ExpectedFileSize)] = ExpectedFileSize,
            [nameof(ActualFileSize)] = ActualFileSize,
            [nameof(ExpectedCommunicationMode)] = ExpectedCommunicationMode,
            [nameof(ActualCommunicationMode)] = ActualCommunicationMode,
            [nameof(ExpectedAccessRights0)] = ExpectedAccessRights0,
            [nameof(ActualAccessRights0)] = ActualAccessRights0,
            [nameof(ExpectedAccessRights1)] = ExpectedAccessRights1,
            [nameof(ActualAccessRights1)] = ActualAccessRights1,
            [nameof(Matches)] = Matches,
        };
    }
}
