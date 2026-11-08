using System.Collections.Generic;
using RFiDGear.Infrastructure.Tasks;

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

        /// <summary>
        /// Gets the settings the check compared, as <c>DesfireFileSettingKinds</c> bit flags: 1 file type, 2 size,
        /// 4 communication mode, 8 read, 16 write, 32 read-write, 64 change.
        /// </summary>
        public int SelectedSettings { get; init; }

        /// <summary>Gets the selected settings that matched, as the same bit flags.</summary>
        public int MatchedSettings { get; init; }

        /// <summary>Gets the selected settings that could not be read (a size of 0 for a data file), as the same bit flags.</summary>
        public int UnreadSettings { get; init; }

        /// <summary>Gets a value indicating whether one matching selected setting was enough (otherwise all had to match).</summary>
        public bool CompareModeAny { get; init; }

        /// <summary>Gets how the size was compared: 0 equal to, 1 at least, 2 at most.</summary>
        public int FileSizeRelation { get; init; }

        /// <summary>Gets a value indicating whether the file type was selected and matched.</summary>
        public bool FileTypeMatches => Has(MatchedSettings, DesfireFileSettingKinds.FileType);

        /// <summary>Gets a value indicating whether the file size was selected and matched.</summary>
        public bool FileSizeMatches => Has(MatchedSettings, DesfireFileSettingKinds.FileSize);

        /// <summary>Gets a value indicating whether the communication mode was selected and matched.</summary>
        public bool CommunicationModeMatches => Has(MatchedSettings, DesfireFileSettingKinds.CommunicationMode);

        /// <summary>Gets a value indicating whether the read access right was selected and matched.</summary>
        public bool ReadMatches => Has(MatchedSettings, DesfireFileSettingKinds.Read);

        /// <summary>Gets a value indicating whether the write access right was selected and matched.</summary>
        public bool WriteMatches => Has(MatchedSettings, DesfireFileSettingKinds.Write);

        /// <summary>Gets a value indicating whether the read-write access right was selected and matched.</summary>
        public bool ReadWriteMatches => Has(MatchedSettings, DesfireFileSettingKinds.ReadWrite);

        /// <summary>Gets a value indicating whether the change access right was selected and matched.</summary>
        public bool ChangeMatches => Has(MatchedSettings, DesfireFileSettingKinds.Change);

        /// <summary>Gets a value indicating whether the selected settings matched, as the compare mode asks.</summary>
        public bool Matches { get; init; }

        private static bool Has(int flags, DesfireFileSettingKinds kind) => (flags & (int)kind) != 0;

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
            [nameof(SelectedSettings)] = SelectedSettings,
            [nameof(MatchedSettings)] = MatchedSettings,
            [nameof(UnreadSettings)] = UnreadSettings,
            [nameof(CompareModeAny)] = CompareModeAny,
            [nameof(FileSizeRelation)] = FileSizeRelation,
            [nameof(FileTypeMatches)] = FileTypeMatches,
            [nameof(FileSizeMatches)] = FileSizeMatches,
            [nameof(CommunicationModeMatches)] = CommunicationModeMatches,
            [nameof(ReadMatches)] = ReadMatches,
            [nameof(WriteMatches)] = WriteMatches,
            [nameof(ReadWriteMatches)] = ReadWriteMatches,
            [nameof(ChangeMatches)] = ChangeMatches,
            [nameof(Matches)] = Matches,
        };
    }
}
