using System;
using RFiDGear.Models;

namespace RFiDGear.Infrastructure.Tasks
{
    /// <summary>
    /// The settings of a DESFire file that the file-settings check can compare, as bit flags. The numbers are logged
    /// with the outcome of the task, so a value is never renumbered.
    /// </summary>
    [Flags]
    internal enum DesfireFileSettingKinds
    {
        /// <summary>No setting.</summary>
        None = 0,

        /// <summary>The file type.</summary>
        FileType = 1,

        /// <summary>The file size.</summary>
        FileSize = 2,

        /// <summary>The communication mode.</summary>
        CommunicationMode = 4,

        /// <summary>The key that may read.</summary>
        Read = 8,

        /// <summary>The key that may write.</summary>
        Write = 16,

        /// <summary>The key that may read and write.</summary>
        ReadWrite = 32,

        /// <summary>The key that may change the access rights.</summary>
        Change = 64,

        /// <summary>All four access rights.</summary>
        AccessRights = Read | Write | ReadWrite | Change,

        /// <summary>Every setting.</summary>
        All = FileType | FileSize | CommunicationMode | AccessRights,
    }

    /// <summary>Which settings the file-settings check compares, and how.</summary>
    /// <param name="Kinds">The settings that are compared; the others are ignored.</param>
    /// <param name="Mode">Whether all or any of the selected settings have to match.</param>
    /// <param name="SizeRelation">How the size read from the file is compared with the expected one.</param>
    internal sealed record DesfireFileSettingsSelection(
        DesfireFileSettingKinds Kinds,
        DesfireFileSettingsCompareMode Mode,
        NumericRelation SizeRelation)
    {
        /// <summary>Determines whether a setting is selected.</summary>
        /// <param name="kind">The setting.</param>
        /// <returns><see langword="true"/> when the check compares it.</returns>
        internal bool Includes(DesfireFileSettingKinds kind) => (Kinds & kind) != 0;
    }

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
    /// <param name="Selection">Which of these settings are compared, and how.</param>
    internal sealed record DesfireFileSettingsExpectation(
        int ApplicationId,
        int FileNumber,
        FileType_MifareDesfireFileType FileType,
        long FileSize,
        EncryptionMode CommunicationMode,
        TaskAccessRights Read,
        TaskAccessRights Write,
        TaskAccessRights ReadWrite,
        TaskAccessRights Change,
        DesfireFileSettingsSelection Selection);

    /// <summary>What the file-settings check found.</summary>
    /// <param name="ErrorLevel">
    /// <see cref="ERROR.NoError"/> for a match, <see cref="ERROR.IsNotTrue"/> for a complete read that does not match,
    /// <see cref="ERROR.TransportError"/> when what the reader returned cannot be compared.
    /// </param>
    /// <param name="Comparison">The settings side by side; <see langword="null"/> when nothing could be compared.</param>
    internal sealed record DesfireFileSettingsCheckResult(ERROR ErrorLevel, DesfireFileSettingsComparison Comparison);

    /// <summary>
    /// Compares the settings read from a DESFire file with the expected ones, as far as the task selects them: a file
    /// type, a size, a communication mode and each of the four access rights is a value of its own, so each is compared
    /// on its own and none stands in for another. A file type, a communication mode and an access right (a key number,
    /// "free" or "never"; it is not a set of bits) are compared for equality; the size by the relation the task states.
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
        /// <param name="expected">The expected settings and which of them are compared.</param>
        /// <param name="actual">The settings as the reader provider returned them.</param>
        /// <returns>
        /// The verdict. Settings that cannot be compared are not a mismatch but a failed read
        /// (<see cref="ERROR.TransportError"/>): access rights that are missing or too short while one is compared, and
        /// a size of 0 for a data file, which no data file has and which is how a provider says it could not read the
        /// size; that size is unknown, and decides the verdict only when no other selected setting does.
        /// </returns>
        internal static DesfireFileSettingsCheckResult Compare(DesfireFileSettingsExpectation expected, DESFireFileSettings actual)
        {
            var selection = expected.Selection;
            var hasRights = actual.accessRights != null && actual.accessRights.Length >= 2;

            if (selection.Includes(DesfireFileSettingKinds.AccessRights) && !hasRights)
            {
                return new DesfireFileSettingsCheckResult(ERROR.TransportError, null);
            }

            var actualRights0 = hasRights ? actual.accessRights[0] : 0;
            var actualRights1 = hasRights ? actual.accessRights[1] : 0;
            var actualSize = (long)actual.dataFile.fileSize;
            var actualIsDataFile = IsDataFile(actual.FileType);
            var sizeIsUnknown = actualIsDataFile && actualSize == 0;

            var expectedRights0 = (int)expected.Read | ((int)expected.Write << 4);
            var expectedRights1 = (int)expected.ReadWrite | ((int)expected.Change << 4);

            // A value or record file has no data size, so it can only differ in size, whatever the relation says. The
            // relation is only looked at when the size is compared.
            var sizeMatches = selection.Includes(DesfireFileSettingKinds.FileSize) && actualIsDataFile && !sizeIsUnknown
                              && DesfireValueComparison.MatchesNumber(actualSize, expected.FileSize, selection.SizeRelation);

            var matched =
                Flag(actual.FileType == (int)expected.FileType, DesfireFileSettingKinds.FileType) |
                Flag(sizeMatches, DesfireFileSettingKinds.FileSize) |
                Flag(actual.comSett == (int)expected.CommunicationMode, DesfireFileSettingKinds.CommunicationMode) |
                Flag(hasRights && (actualRights0 & 0x0F) == (int)expected.Read, DesfireFileSettingKinds.Read) |
                Flag(hasRights && (actualRights0 >> 4) == (int)expected.Write, DesfireFileSettingKinds.Write) |
                Flag(hasRights && (actualRights1 & 0x0F) == (int)expected.ReadWrite, DesfireFileSettingKinds.ReadWrite) |
                Flag(hasRights && (actualRights1 >> 4) == (int)expected.Change, DesfireFileSettingKinds.Change);
            var unknown = Flag(sizeIsUnknown, DesfireFileSettingKinds.FileSize);

            var verdict = DesfireValueComparison.JudgeSelected((int)selection.Kinds, (int)matched, (int)unknown, selection.Mode);
            if (verdict == SelectionVerdict.Undecided)
            {
                return new DesfireFileSettingsCheckResult(ERROR.TransportError, null);
            }

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
                ActualAccessRights0 = actualRights0,
                ExpectedAccessRights1 = expectedRights1,
                ActualAccessRights1 = actualRights1,
                SelectedSettings = (int)selection.Kinds,
                MatchedSettings = (int)(matched & selection.Kinds),
                UnreadSettings = (int)(unknown & selection.Kinds),
                CompareModeAny = selection.Mode == DesfireFileSettingsCompareMode.AnySelected,
                FileSizeRelation = (int)selection.SizeRelation,
                Matches = verdict == SelectionVerdict.Matches,
            };

            return new DesfireFileSettingsCheckResult(comparison.Matches ? ERROR.NoError : ERROR.IsNotTrue, comparison);
        }

        private static DesfireFileSettingKinds Flag(bool condition, DesfireFileSettingKinds kind) =>
            condition ? kind : DesfireFileSettingKinds.None;

        private static bool IsDataFile(int fileType) =>
            fileType == (int)FileType_MifareDesfireFileType.StdDataFile || fileType == (int)FileType_MifareDesfireFileType.BackupFile;
    }
}
