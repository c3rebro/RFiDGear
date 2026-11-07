using RFiDGear.Models;

namespace RFiDGear.Infrastructure.Tasks
{
    /// <summary>The settings a DESFire file is expected to have, as the task is configured.</summary>
    /// <param name="ApplicationId">The application the file is in.</param>
    /// <param name="FileNumber">The file number.</param>
    /// <param name="FileType">The expected file type.</param>
    /// <param name="FileSize">The expected size in bytes.</param>
    /// <param name="CommunicationMode">The expected communication mode.</param>
    /// <param name="Read">The key, or "free"/"never", that may read.</param>
    /// <param name="Write">The key, or "free"/"never", that may write.</param>
    /// <param name="ReadWrite">The key, or "free"/"never", that may read and write.</param>
    /// <param name="Change">The key, or "free"/"never", that may change the access rights.</param>
    internal sealed record DesfireFileSettingsExpectation(
        int ApplicationId,
        int FileNumber,
        FileType_MifareDesfireFileType FileType,
        long FileSize,
        EncryptionMode CommunicationMode,
        TaskAccessRights Read,
        TaskAccessRights Write,
        TaskAccessRights ReadWrite,
        TaskAccessRights Change);

    /// <summary>What the file-settings check found.</summary>
    /// <param name="ErrorLevel">
    /// <see cref="ERROR.NoError"/> for a complete match, <see cref="ERROR.IsNotTrue"/> for a complete read with any
    /// difference, <see cref="ERROR.TransportError"/> when what the reader returned cannot be compared.
    /// </param>
    /// <param name="Comparison">The settings side by side; <see langword="null"/> when nothing could be compared.</param>
    internal sealed record DesfireFileSettingsCheckResult(ERROR ErrorLevel, DesfireFileSettingsComparison Comparison);

    /// <summary>
    /// Compares the settings read from a DESFire file with the expected ones. Every field has to match: a file type, a
    /// size, a communication mode and each of the four access rights is a value of its own, so each is compared for
    /// equality and none stands in for another. (An access right is a key number, "free" or "never"; it is not a set of
    /// bits.)
    /// </summary>
    internal static class DesfireFileSettingsChecker
    {
        /// <summary>
        /// Determines whether a file type can be checked. A value or record file has settings of its own (limits, record
        /// counts) that the task cannot express yet, so a task for one is not accepted instead of half checked.
        /// </summary>
        /// <param name="fileType">The file type the task is configured for.</param>
        /// <returns><see langword="true"/> for standard and backup data files.</returns>
        internal static bool IsSupportedFileType(FileType_MifareDesfireFileType fileType) =>
            fileType == FileType_MifareDesfireFileType.StdDataFile || fileType == FileType_MifareDesfireFileType.BackupFile;

        /// <summary>Compares the settings read from a file with the expected ones.</summary>
        /// <param name="expected">The expected settings.</param>
        /// <param name="actual">The settings as the reader provider returned them.</param>
        /// <returns>
        /// The verdict. Settings that cannot be compared are not a mismatch but a failed read
        /// (<see cref="ERROR.TransportError"/>): access rights that are missing or too short, and a data file whose size
        /// the provider reports as 0, which no data file has and which is how a provider says it could not read the size.
        /// </returns>
        internal static DesfireFileSettingsCheckResult Compare(DesfireFileSettingsExpectation expected, DESFireFileSettings actual)
        {
            if (actual.accessRights == null || actual.accessRights.Length < 2)
            {
                return new DesfireFileSettingsCheckResult(ERROR.TransportError, null);
            }

            var actualSize = (long)actual.dataFile.fileSize;
            var typeMatches = actual.FileType == (int)expected.FileType;

            // A file of another type is a mismatch whatever its size says; the size of the file that was expected is
            // only unreadable when the file is of that type.
            if (typeMatches && actualSize == 0)
            {
                return new DesfireFileSettingsCheckResult(ERROR.TransportError, null);
            }

            var expectedRights0 = (int)expected.Read | ((int)expected.Write << 4);
            var expectedRights1 = (int)expected.ReadWrite | ((int)expected.Change << 4);

            var matches = typeMatches &&
                          actualSize == expected.FileSize &&
                          actual.comSett == (int)expected.CommunicationMode &&
                          actual.accessRights[0] == expectedRights0 &&
                          actual.accessRights[1] == expectedRights1;

            var comparison = new DesfireFileSettingsComparison
            {
                ApplicationId = expected.ApplicationId,
                FileNumber = expected.FileNumber,
                ExpectedFileType = (int)expected.FileType,
                ActualFileType = actual.FileType,
                ExpectedFileSize = expected.FileSize,
                ActualFileSize = actualSize,
                ExpectedCommunicationMode = (int)expected.CommunicationMode,
                ActualCommunicationMode = actual.comSett,
                ExpectedAccessRights0 = expectedRights0,
                ActualAccessRights0 = actual.accessRights[0],
                ExpectedAccessRights1 = expectedRights1,
                ActualAccessRights1 = actual.accessRights[1],
                Matches = matches,
            };

            return new DesfireFileSettingsCheckResult(matches ? ERROR.NoError : ERROR.IsNotTrue, comparison);
        }
    }
}
