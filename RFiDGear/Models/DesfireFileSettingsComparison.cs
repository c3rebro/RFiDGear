namespace RFiDGear.Models
{
    /// <summary>
    /// Contains the key-free expected and actual values from a DESFire file-settings comparison.
    /// </summary>
    public sealed class DesfireFileSettingsComparison
    {
        /// <summary>Gets or sets the application identifier.</summary>
        public int ApplicationId { get; set; }

        /// <summary>Gets or sets the file number.</summary>
        public int FileNumber { get; set; }

        /// <summary>Gets or sets the expected file type.</summary>
        public int ExpectedFileType { get; set; }

        /// <summary>Gets or sets the actual file type.</summary>
        public int ActualFileType { get; set; }

        /// <summary>Gets or sets the expected file size in bytes.</summary>
        public int ExpectedFileSize { get; set; }

        /// <summary>Gets or sets the actual file size in bytes.</summary>
        public long ActualFileSize { get; set; }

        /// <summary>Gets or sets the expected communication mode.</summary>
        public int ExpectedCommunicationMode { get; set; }

        /// <summary>Gets or sets the actual communication mode.</summary>
        public int ActualCommunicationMode { get; set; }

        /// <summary>Gets or sets the packed expected read/write access-right byte.</summary>
        public int ExpectedAccessRights0 { get; set; }

        /// <summary>Gets or sets the packed actual read/write access-right byte.</summary>
        public int ActualAccessRights0 { get; set; }

        /// <summary>Gets or sets the packed expected read-write/change access-right byte.</summary>
        public int ExpectedAccessRights1 { get; set; }

        /// <summary>Gets or sets the packed actual read-write/change access-right byte.</summary>
        public int ActualAccessRights1 { get; set; }

        /// <summary>Gets or sets a value indicating whether every expected field matches.</summary>
        public bool Matches { get; set; }
    }
}
