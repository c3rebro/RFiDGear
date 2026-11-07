namespace RFiDGear.Models
{
    /// <summary>
    /// The expected and the actual settings of one DESFire file, side by side, as one run of the file-settings check
    /// found them. It is the result of that attempt and cannot be changed afterwards. It holds numbers and a verdict
    /// only: no key, and nothing that was read from the file.
    /// </summary>
    public sealed record DesfireFileSettingsComparison
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
    }
}
