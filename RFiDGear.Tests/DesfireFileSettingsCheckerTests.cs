using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.Tasks;
using Xunit;

namespace RFiDGear.Tests
{
    /// <summary>
    /// What the file-settings check does with the settings it was told to compare. The flags of the settings are
    /// written as numbers because the test methods of a public class cannot take an internal enum: 1 file type, 2 size,
    /// 4 communication mode, 8 read, 16 write, 32 read-write, 64 change. The mode is 0 for all, 1 for any, and the size
    /// relation 0 for equal, 1 for at least, 2 for at most.
    /// </summary>
    public class DesfireFileSettingsCheckerTests
    {
        private const int Everything = (int)DesfireFileSettingKinds.All;
        private const int FileType = 1;
        private const int Size = 2;
        private const int Mode = 4;
        private const int Read = 8;
        private const int Write = 16;
        private const int ReadWrite = 32;
        private const int Change = 64;
        private const int AllMode = 0;
        private const int AnyMode = 1;

        // Everything selected and everything equal is how the check behaved before it could choose.
        [Theory]
        [InlineData(0, ERROR.NoError)]
        [InlineData(FileType, ERROR.IsNotTrue)]
        [InlineData(Size, ERROR.IsNotTrue)]
        [InlineData(Mode, ERROR.IsNotTrue)]
        [InlineData(Read, ERROR.IsNotTrue)]
        [InlineData(Write, ERROR.IsNotTrue)]
        [InlineData(ReadWrite, ERROR.IsNotTrue)]
        [InlineData(Change, ERROR.IsNotTrue)]
        public void WithEverythingSelected_EachSettingCountsOnItsOwn(int differing, ERROR expected)
        {
            var result = DesfireFileSettingsChecker.Compare(Expected(Everything, AllMode), Actual(differing));

            Assert.Equal(expected, result.ErrorLevel);
            Assert.Equal(expected == ERROR.NoError, result.Comparison!.Matches);
        }

        [Theory]
        // A setting that is not selected is ignored, whatever it says.
        [InlineData(Everything & ~Size, AllMode, Size, ERROR.NoError)]
        [InlineData(FileType, AllMode, Size | Mode | Read | Write | ReadWrite | Change, ERROR.NoError)]
        // All: one selected setting that differs is enough to fail.
        [InlineData(Read | Change, AllMode, Change, ERROR.IsNotTrue)]
        [InlineData(Write, AllMode, Write, ERROR.IsNotTrue)]
        // Any: one selected setting that matches is enough to pass.
        [InlineData(Read | Change, AnyMode, Change, ERROR.NoError)]
        [InlineData(Size | Mode, AnyMode, Size, ERROR.NoError)]
        [InlineData(Read | Change, AnyMode, Read | Change, ERROR.IsNotTrue)]
        [InlineData(Everything, AnyMode, Everything, ERROR.IsNotTrue)]
        // The rights are told apart: the nibbles of read and write are not interchangeable.
        [InlineData(Read, AllMode, Write, ERROR.NoError)]
        [InlineData(Write, AllMode, Read, ERROR.NoError)]
        public void OnlyTheSelectedSettings_DecideTheVerdict_AsTheModeSays(int selected, int mode, int differing, ERROR expected)
        {
            var result = DesfireFileSettingsChecker.Compare(Expected(selected, mode), Actual(differing));

            Assert.Equal(expected, result.ErrorLevel);
        }

        [Theory]
        [InlineData(0, 15u, ERROR.IsNotTrue)]
        [InlineData(0, 16u, ERROR.NoError)]
        [InlineData(0, 17u, ERROR.IsNotTrue)]
        [InlineData(1, 15u, ERROR.IsNotTrue)]
        [InlineData(1, 16u, ERROR.NoError)]
        [InlineData(1, 17u, ERROR.NoError)]
        [InlineData(2, 15u, ERROR.NoError)]
        [InlineData(2, 16u, ERROR.NoError)]
        [InlineData(2, 17u, ERROR.IsNotTrue)]
        public void TheSize_IsComparedAsTheRelationSays(int relation, uint sizeOnTheCard, ERROR expected)
        {
            var result = DesfireFileSettingsChecker.Compare(Expected(Size, AllMode, relation), Actual(size: sizeOnTheCard));

            Assert.Equal(expected, result.ErrorLevel);
        }

        [Fact]
        public void TheLargestSize_IsComparedWithoutWrappingAround()
        {
            var result = DesfireFileSettingsChecker.Compare(Expected(Size, AllMode, relation: 1, expectedSize: 8000), Actual(size: uint.MaxValue));

            Assert.Equal(ERROR.NoError, result.ErrorLevel);
        }

        [Fact]
        public void AValueFile_HasNoSize_AndIsNeverSmallerThanOne()
        {
            // "At most" must not read the size of 0 that a value file reports as 0 <= 16.
            var valueFile = Actual(fileType: 2, size: 0);

            Assert.Equal(ERROR.IsNotTrue, DesfireFileSettingsChecker.Compare(Expected(Size, AllMode, relation: 2), valueFile).ErrorLevel);
            Assert.Equal(ERROR.IsNotTrue, DesfireFileSettingsChecker.Compare(Expected(Size, AllMode, relation: 1), valueFile).ErrorLevel);
        }

        [Fact]
        public void WithTheFileTypeLeftOut_AValueFileCanBeChecked_OnItsModeAndRights()
        {
            var result = DesfireFileSettingsChecker.Compare(Expected(Mode | Read | Write, AllMode), Actual(fileType: 2, size: 0));

            Assert.Equal(ERROR.NoError, result.ErrorLevel);
        }

        // A size of 0 is how a provider says that it could not read the size of a data file. That setting is unknown, and
        // it decides the verdict only when nothing else does.
        [Theory]
        [InlineData(Size, AllMode, 0, ERROR.TransportError)]
        [InlineData(Size, AnyMode, 0, ERROR.TransportError)]
        [InlineData(Size | Mode, AllMode, 0, ERROR.TransportError)]
        [InlineData(Size | Mode, AllMode, Mode, ERROR.IsNotTrue)]
        // With everything selected too: this is the one verdict that differs from the check before it could choose, which
        // was a TransportError here although the file certainly differed.
        [InlineData(Everything, AllMode, Mode, ERROR.IsNotTrue)]
        [InlineData(Size | Mode, AnyMode, 0, ERROR.NoError)]
        [InlineData(Size | Mode, AnyMode, Mode, ERROR.TransportError)]
        [InlineData(Everything, AllMode, 0, ERROR.TransportError)]
        [InlineData(Everything & ~Size, AllMode, 0, ERROR.NoError)]
        public void ASizeThatCouldNotBeRead_DecidesTheVerdictOnlyWhenNothingElseDoes(int selected, int mode, int differing, ERROR expected)
        {
            var result = DesfireFileSettingsChecker.Compare(Expected(selected, mode), Actual(differing, size: 0));

            Assert.Equal(expected, result.ErrorLevel);
            Assert.Equal(expected == ERROR.TransportError, result.Comparison == null);
        }

        // The relation of the size is only looked at when the size is compared.
        [Fact]
        public void TheSizeRelation_IsNotLookedAt_WhenTheSizeIsNotCompared()
        {
            var result = DesfireFileSettingsChecker.Compare(Expected(Everything & ~Size, AllMode, relation: 99), Actual());

            Assert.Equal(ERROR.NoError, result.ErrorLevel);
        }

        [Fact]
        public void ADataFileOfAnotherType_IsAMismatch_EvenWhenItsSizeReadsAsZero()
        {
            // A backup file is not the standard file that was expected, whatever its size says (a PC/SC provider may not
            // read the size of a backup file).
            var result = DesfireFileSettingsChecker.Compare(Expected(Everything, AllMode), Actual(fileType: 1, size: 0));

            Assert.Equal(ERROR.IsNotTrue, result.ErrorLevel);
            Assert.Equal(DesfireFileSettingKinds.FileSize, (DesfireFileSettingKinds)result.Comparison!.UnreadSettings);
        }

        [Theory]
        [InlineData(Read, AllMode)]
        [InlineData(Read | Size, AnyMode)]
        [InlineData(Change | Mode, AllMode)]
        public void RightsThatAreMissing_AreAFailedRead_WhenOneOfThemIsCompared(int selected, int mode)
        {
            var settings = Actual();
            settings.accessRights = null;

            Assert.Equal(ERROR.TransportError, DesfireFileSettingsChecker.Compare(Expected(selected, mode), settings).ErrorLevel);

            settings.accessRights = new byte[1];
            Assert.Equal(ERROR.TransportError, DesfireFileSettingsChecker.Compare(Expected(selected, mode), settings).ErrorLevel);
        }

        [Fact]
        public void RightsThatAreMissing_DoNotMatter_WhenNoneOfThemIsCompared()
        {
            var settings = Actual();
            settings.accessRights = null;

            Assert.Equal(ERROR.NoError, DesfireFileSettingsChecker.Compare(Expected(Size | Mode, AllMode), settings).ErrorLevel);
        }

        [Theory]
        [InlineData(AllMode)]
        [InlineData(AnyMode)]
        public void NothingSelected_NeverMatches(int mode)
        {
            var result = DesfireFileSettingsChecker.Compare(Expected(0, mode), Actual());

            Assert.Equal(ERROR.IsNotTrue, result.ErrorLevel);
        }

        [Fact]
        public void TheComparison_SaysWhatWasSelected_WhatMatched_AndWhatWasNotRead()
        {
            var selection = Expected(Size | Read, AnyMode, relation: 1);

            var result = DesfireFileSettingsChecker.Compare(selection, Actual(Read, size: 17));
            var comparison = result.Comparison!;

            Assert.Equal(ERROR.NoError, result.ErrorLevel);
            Assert.Equal(Size | Read, comparison.SelectedSettings);
            Assert.Equal(Size, comparison.MatchedSettings);
            Assert.Equal(0, comparison.UnreadSettings);
            Assert.True(comparison.CompareModeAny);
            Assert.Equal(1, comparison.FileSizeRelation);
            Assert.True(comparison.FileSizeMatches);
            Assert.False(comparison.ReadMatches);
            Assert.False(comparison.WriteMatches);
            Assert.True(comparison.Matches);
        }

        [Fact]
        public void TheLoggedValues_AreNumbersAndFlagsOnly_AndNameNoKey()
        {
            var comparison = DesfireFileSettingsChecker.Compare(Expected(Everything, AllMode), Actual()).Comparison!;

            var values = comparison.ToLogValues();

            Assert.All(values.Values, value => Assert.True(value is bool || value is int || value is long, value.GetType().Name));
            Assert.DoesNotContain(values.Keys, name => name.Contains("key", System.StringComparison.OrdinalIgnoreCase)
                                                       || name.Contains("payload", System.StringComparison.OrdinalIgnoreCase));
            Assert.All(
                new[] { "SelectedSettings", "MatchedSettings", "UnreadSettings", "CompareModeAny", "FileSizeRelation", "FileTypeMatches", "FileSizeMatches", "CommunicationModeMatches", "ReadMatches", "WriteMatches", "ReadWriteMatches", "ChangeMatches" },
                name => Assert.Contains(name, values.Keys));
        }

        [Fact]
        public void TheSavedNames_OfTheRelationAndTheMode_AreUnchanged()
        {
            // Project files hold these names: they are appended to, never renamed or removed.
            Assert.Equal(new[] { "Equal", "ActualAtLeastExpected", "ActualAtMostExpected" }, System.Enum.GetNames(typeof(NumericRelation)));
            Assert.Equal(new[] { "AllSelected", "AnySelected" }, System.Enum.GetNames(typeof(DesfireFileSettingsCompareMode)));
        }

        private static DesfireFileSettingsExpectation Expected(int selected, int mode, int relation = 0, long expectedSize = 16)
        {
            var selection = new DesfireFileSettingsSelection(
                (DesfireFileSettingKinds)selected,
                mode == AnyMode ? DesfireFileSettingsCompareMode.AnySelected : DesfireFileSettingsCompareMode.AllSelected,
                (NumericRelation)relation);

            return new DesfireFileSettingsExpectation(
                1,
                2,
                FileType_MifareDesfireFileType.StdDataFile,
                expectedSize,
                EncryptionMode.CM_ENCRYPT,
                TaskAccessRights.AR_KEY0,
                TaskAccessRights.AR_KEY1,
                TaskAccessRights.AR_KEY2,
                TaskAccessRights.AR_KEY3,
                selection);
        }

        // The file as the expectation above describes it, with the settings named in `differing` changed.
        private static DESFireFileSettings Actual(int differing = 0, int fileType = 0, uint size = 16)
        {
            return new DESFireFileSettings
            {
                FileType = (byte)((differing & FileType) != 0 ? 1 : fileType),
                comSett = (byte)((differing & Mode) != 0 ? 1 : 3),
                accessRights = new[]
                {
                    (byte)(((differing & Read) != 0 ? 5 : 0) | (((differing & Write) != 0 ? 6 : 1) << 4)),
                    (byte)(((differing & ReadWrite) != 0 ? 7 : 2) | (((differing & Change) != 0 ? 8 : 3) << 4)),
                },
                dataFile = new DataFileSetting { fileSize = (differing & Size) != 0 ? size + 1 : size }
            };
        }
    }
}
