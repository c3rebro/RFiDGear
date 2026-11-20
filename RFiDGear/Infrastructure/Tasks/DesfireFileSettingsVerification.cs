#nullable enable
using System.Collections.Generic;

using RFiDGear.Contracts;
using RFiDGear.Models;

namespace RFiDGear.Infrastructure.Tasks
{
    /// <summary>
    /// Writes down what the file-settings check found as <see cref="VerificationDetails"/>: one entry per setting the check can compare,
    /// in a fixed order, with what was expected, what was read and how it ended. It reads the comparison that the check made and
    /// nothing else, and the result holds numbers and fixed tokens only.
    /// </summary>
    internal static class DesfireFileSettingsVerification
    {
        /// <summary>The layout version of the details this class writes.</summary>
        internal const int SchemaVersion = 1;

        /// <summary>Turns the comparison of one attempt of the check into details.</summary>
        /// <param name="comparison">What the check compared.</param>
        /// <returns>The details, one check per setting in the order of <see cref="VerificationCheckNames"/>.</returns>
        internal static VerificationDetails From(DesfireFileSettingsComparison comparison)
        {
            System.ArgumentNullException.ThrowIfNull(comparison);

            var checks = new List<VerificationCheck>
            {
                CheckOf(comparison, VerificationCheckNames.FileType, DesfireFileSettingKinds.FileType, comparison.ExpectedFileType, comparison.ActualFileType, null),
                CheckOf(comparison, VerificationCheckNames.FileSize, DesfireFileSettingKinds.FileSize, comparison.ExpectedFileSize, comparison.ActualFileSize, RuleOf(comparison.FileSizeRelation)),
                CheckOf(comparison, VerificationCheckNames.CommunicationMode, DesfireFileSettingKinds.CommunicationMode, comparison.ExpectedCommunicationMode, comparison.ActualCommunicationMode, null),
                CheckOf(comparison, VerificationCheckNames.AccessRead, DesfireFileSettingKinds.Read, LowNibble(comparison.ExpectedAccessRights0), LowNibble(comparison.ActualAccessRights0), null),
                CheckOf(comparison, VerificationCheckNames.AccessWrite, DesfireFileSettingKinds.Write, HighNibble(comparison.ExpectedAccessRights0), HighNibble(comparison.ActualAccessRights0), null),
                CheckOf(comparison, VerificationCheckNames.AccessReadWrite, DesfireFileSettingKinds.ReadWrite, LowNibble(comparison.ExpectedAccessRights1), LowNibble(comparison.ActualAccessRights1), null),
                CheckOf(comparison, VerificationCheckNames.AccessChange, DesfireFileSettingKinds.Change, HighNibble(comparison.ExpectedAccessRights1), HighNibble(comparison.ActualAccessRights1), null),
            };

            return new VerificationDetails(SchemaVersion, VerificationKinds.DesfireFileSettings, checks)
            {
                OverallMatch = comparison.Matches,
                CompareMode = comparison.CompareModeAny ? VerificationCompareModes.Any : VerificationCompareModes.All,
                ApplicationId = comparison.ApplicationId,
                FileNumber = comparison.FileNumber,
                ErrorCode = comparison.UnreadSettings != 0 ? VerificationErrors.NotRead : null,
            };
        }

        // A setting that was not selected says so and nothing more; a selected one that could not be read has no value read; any
        // other has both values and its verdict.
        private static VerificationCheck CheckOf(DesfireFileSettingsComparison comparison, string name, DesfireFileSettingKinds kind, long expected, long actual, string? rule)
        {
            var flag = (int)kind;

            if ((comparison.SelectedSettings & flag) == 0)
            {
                return new VerificationCheck(name, false, VerificationResults.NotCompared);
            }

            if ((comparison.UnreadSettings & flag) != 0)
            {
                return new VerificationCheck(name, true, VerificationResults.NotRead) { Rule = rule, Expected = expected };
            }

            var result = (comparison.MatchedSettings & flag) != 0 ? VerificationResults.Match : VerificationResults.Mismatch;

            return new VerificationCheck(name, true, result) { Rule = rule, Expected = expected, Actual = actual };
        }

        // 0 equal, 1 at least, 2 at most (NumericRelation); a value that is none of them names no rule rather than a wrong one.
        private static string? RuleOf(int relation) => relation switch
        {
            0 => VerificationRules.Exact,
            1 => VerificationRules.AtLeast,
            2 => VerificationRules.AtMost,
            _ => null,
        };

        private static int LowNibble(int accessRightsByte) => accessRightsByte & 0x0F;

        private static int HighNibble(int accessRightsByte) => (accessRightsByte >> 4) & 0x0F;
    }
}
