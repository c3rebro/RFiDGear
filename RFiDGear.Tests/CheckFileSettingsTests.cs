using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.Tasks;
using RFiDGear.Services.TaskExecution;
using RFiDGear.ViewModel.TaskSetupViewModels;
using System.Threading.Tasks;
using Xunit;

namespace RFiDGear.Tests
{
    /// <summary>
    /// The read-only file-settings check: what it compares, what it returns for each outcome, and that a configuration it
    /// cannot check is refused before the card is asked.
    /// </summary>
    public class CheckFileSettingsTests
    {
        private const string ZeroAesKey = "00000000000000000000000000000000";

        [Theory]
        [InlineData(FileType_MifareDesfireFileType.StdDataFile)]
        [InlineData(FileType_MifareDesfireFileType.BackupFile)]
        public async Task ACompleteMatch_IsNoError(FileType_MifareDesfireFileType fileType)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var viewModel = CreateViewModel(fileType);
                var reader = new FakeFileSettingsProvider { FileSettings = Settings((int)fileType) };

                await Run(viewModel, reader);

                Assert.Equal(ERROR.NoError, viewModel.CurrentTaskErrorLevel);
                Assert.True(viewModel.LastFileSettingsComparison.Matches);
                Assert.Equal(1, reader.AuthCalls);
                Assert.Equal(1, reader.GetFileSettingsCalls);
                Assert.Equal((1, 2), (reader.LastFileSettingsAppId, reader.LastFileSettingsFileNo));
            });
        }

        // The expected file is a standard data file of 16 bytes, encrypted, and the access rights are read 0, write 1,
        // read-write 2 and change 3. Each row differs from that in exactly one field.
        [Theory]
        [InlineData(1, 16, 3, 0, 1, 2, 3)] // another file type
        [InlineData(0, 15, 3, 0, 1, 2, 3)] // another size
        [InlineData(0, 17, 3, 0, 1, 2, 3)]
        [InlineData(0, 16, 1, 0, 1, 2, 3)] // another communication mode
        [InlineData(0, 16, 0, 0, 1, 2, 3)]
        [InlineData(0, 16, 3, 1, 1, 2, 3)] // read
        [InlineData(0, 16, 3, 0, 2, 2, 3)] // write
        [InlineData(0, 16, 3, 0, 1, 3, 3)] // read-write
        [InlineData(0, 16, 3, 0, 1, 2, 4)] // change
        [InlineData(0, 16, 3, 1, 0, 2, 3)] // read and write swapped: the nibble order counts
        [InlineData(0, 16, 3, 0, 1, 3, 2)] // read-write and change swapped
        [InlineData(0, 16, 3, 1, 1, 1, 3)] // read one higher and read-write one lower: the two bytes still add up
        public async Task AnyOneDifference_IsNotTrue(
            int fileType,
            uint size,
            int mode,
            int read,
            int write,
            int readWrite,
            int change)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var viewModel = CreateViewModel();
                var reader = new FakeFileSettingsProvider { FileSettings = Settings(fileType, size, mode, read, write, readWrite, change) };

                await Run(viewModel, reader);

                Assert.Equal(ERROR.IsNotTrue, viewModel.CurrentTaskErrorLevel);
                Assert.False(viewModel.LastFileSettingsComparison.Matches);
            });
        }

        [Theory]
        [InlineData(ERROR.AuthFailure)]
        [InlineData(ERROR.TransportError)]
        [InlineData(ERROR.PermissionDenied)]
        public async Task AFailedAuthentication_PassesItsErrorOn_AndTheSettingsAreNotAskedFor(ERROR authenticationResult)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var viewModel = CreateViewModel();
                var reader = new FakeFileSettingsProvider { FileSettings = Settings(), AuthenticationResult = authenticationResult };

                await Run(viewModel, reader);

                Assert.Equal(authenticationResult, viewModel.CurrentTaskErrorLevel);
                Assert.Equal(0, reader.GetFileSettingsCalls);
                Assert.Null(viewModel.LastFileSettingsComparison);
            });
        }

        [Theory]
        [InlineData(ERROR.TransportError)]
        [InlineData(ERROR.ProtocolConstraint)]
        [InlineData(ERROR.PermissionDenied)]
        public async Task AFailedRead_PassesItsErrorOn(ERROR readResult)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var viewModel = CreateViewModel();
                var reader = new FakeFileSettingsProvider { FileSettings = Settings(), FileSettingsResult = readResult };

                await Run(viewModel, reader);

                Assert.Equal(readResult, viewModel.CurrentTaskErrorLevel);
                Assert.Null(viewModel.LastFileSettingsComparison);
            });
        }

        [Fact]
        public async Task WithoutAReader_TheResultIsATransportError()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var viewModel = CreateViewModel();

                await Run(viewModel, null);

                Assert.Equal(ERROR.TransportError, viewModel.CurrentTaskErrorLevel);
                Assert.False(viewModel.IsTaskCompletedSuccessfully);
            });
        }

        [Theory]
        [InlineData(FileType_MifareDesfireFileType.ValueFile)]
        [InlineData(FileType_MifareDesfireFileType.LinearRecordFile)]
        [InlineData(FileType_MifareDesfireFileType.CyclicRecordFile)]
        public async Task ValueAndRecordFiles_AreRefused_BeforeAnyReaderAccess(FileType_MifareDesfireFileType fileType)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var withReader = CreateViewModel(fileType);
                var reader = new FakeFileSettingsProvider { FileSettings = Settings((int)fileType) };
                await Run(withReader, reader);

                Assert.Equal(ERROR.ProtocolConstraint, withReader.CurrentTaskErrorLevel);
                Assert.Equal(0, reader.AuthCalls);
                Assert.Equal(0, reader.GetFileSettingsCalls);

                // The reason is the configuration, so it is the same without a reader: not "no reader".
                var withoutReader = CreateViewModel(fileType);
                await Run(withoutReader, null);

                Assert.Equal(ERROR.ProtocolConstraint, withoutReader.CurrentTaskErrorLevel);
                var reason = ResourceLoader.GetResource("textBoxStatusFileSettingsUnsupportedFileType").Replace("{0}: ", string.Empty);
                Assert.False(string.IsNullOrWhiteSpace(reason));
                Assert.Contains(reason, withoutReader.StatusText);
            });
        }

        [Theory]
        [InlineData(nameof(MifareDesfireSetupViewModel.AppNumberCurrent), "abc")]
        [InlineData(nameof(MifareDesfireSetupViewModel.AppNumberCurrent), "0")]
        [InlineData(nameof(MifareDesfireSetupViewModel.FileNumberCurrent), "abc")]
        [InlineData(nameof(MifareDesfireSetupViewModel.FileNumberCurrent), "256")]
        [InlineData(nameof(MifareDesfireSetupViewModel.FileSizeCurrent), "abc")]
        [InlineData(nameof(MifareDesfireSetupViewModel.FileSizeCurrent), "0")]
        [InlineData(nameof(MifareDesfireSetupViewModel.FileSizeCurrent), "9000")]
        [InlineData(nameof(MifareDesfireSetupViewModel.SelectedDesfireAppKeyNumberCurrent), "14")]
        [InlineData(nameof(MifareDesfireSetupViewModel.DesfireAppKeyCurrent), "00")]
        public async Task AnInvalidConfiguration_IsRefused_BeforeAnyReaderAccess(string property, string value)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var viewModel = CreateViewModel();
                typeof(MifareDesfireSetupViewModel).GetProperty(property)!.SetValue(viewModel, value);
                var reader = new FakeFileSettingsProvider { FileSettings = Settings() };

                await Run(viewModel, reader);

                Assert.Equal(ERROR.ProtocolConstraint, viewModel.CurrentTaskErrorLevel);
                Assert.Equal(0, reader.AuthCalls);
                Assert.Equal(0, reader.GetFileSettingsCalls);
            });
        }

        [Fact]
        public async Task AnUnknownCommunicationMode_IsRefused_BeforeAnyReaderAccess()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var viewModel = CreateViewModel();
                viewModel.SelectedDesfireFileCryptoMode = EncryptionMode.CM_UNKNOWN;
                var reader = new FakeFileSettingsProvider { FileSettings = Settings() };

                await Run(viewModel, reader);

                Assert.Equal(ERROR.ProtocolConstraint, viewModel.CurrentTaskErrorLevel);
                Assert.Equal(0, reader.AuthCalls);
            });
        }

        // What the reader returned cannot be compared: that is a failed read, not a card that is wrong.
        [Theory]
        [InlineData("noSettings")]
        [InlineData("noAccessRights")]
        [InlineData("shortAccessRights")]
        [InlineData("sizeNotRead")]
        public async Task SettingsThatCannotBeCompared_AreATransportError(string problem)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var settings = Settings();
                var reader = new FakeFileSettingsProvider { FileSettings = settings };
                switch (problem)
                {
                    case "noSettings":
                        reader.FileSettings = null;
                        break;
                    case "noAccessRights":
                        settings.accessRights = null!;
                        break;
                    case "shortAccessRights":
                        settings.accessRights = new byte[] { 0x10 };
                        break;
                    default:
                        // A data file has at least one byte; 0 is how a provider says it could not read the size.
                        settings.dataFile = new DataFileSetting { fileSize = 0 };
                        break;
                }

                var viewModel = CreateViewModel();
                await Run(viewModel, reader);

                Assert.Equal(ERROR.TransportError, viewModel.CurrentTaskErrorLevel);
                Assert.Null(viewModel.LastFileSettingsComparison);
            });
        }

        [Fact]
        public async Task AFileOfAnotherType_IsAMismatch_EvenWhenItsSizeReadsAsZero()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                // A value file has no data size; the type alone says it is not the file that was expected.
                var viewModel = CreateViewModel();
                var reader = new FakeFileSettingsProvider { FileSettings = Settings((int)FileType_MifareDesfireFileType.ValueFile, size: 0) };

                await Run(viewModel, reader);

                Assert.Equal(ERROR.IsNotTrue, viewModel.CurrentTaskErrorLevel);
            });
        }

        [Fact]
        public async Task TheLargestSize_IsComparedWithoutWrappingAround()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var viewModel = CreateViewModel();
                var reader = new FakeFileSettingsProvider { FileSettings = Settings(size: uint.MaxValue) };

                await Run(viewModel, reader);

                Assert.Equal(ERROR.IsNotTrue, viewModel.CurrentTaskErrorLevel);
                Assert.Equal(uint.MaxValue, viewModel.LastFileSettingsComparison.ActualFileSize);
            });
        }

        [Fact]
        public async Task SettingsOfAnEarlierRead_AreNotReportedForThisOne()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                // The provider answers success but stores nothing; it still holds settings that would match.
                var viewModel = CreateViewModel();
                var reader = new FakeFileSettingsProvider { LeaveSettingsUnset = true, DesfireFileSettings = Settings() };

                await Run(viewModel, reader);

                Assert.Equal(ERROR.TransportError, viewModel.CurrentTaskErrorLevel);
                Assert.Null(viewModel.LastFileSettingsComparison);
            });
        }

        [Fact]
        public async Task TheTaskReportsItsComparisonToTheLogAsPlainNumbers_AndForgetsItOnRequest()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var viewModel = CreateViewModel();
                var details = (ITaskOutcomeDetails)viewModel;
                Assert.Null(details.GetOutcomeDetails());

                await Run(viewModel, new FakeFileSettingsProvider { FileSettings = Settings() });

                var reported = details.GetOutcomeDetails()!;
                Assert.Equal("FileSettingsComparison", details.OutcomeDetailsSection);
                Assert.Equal(true, reported["Matches"]);
                Assert.Equal(16L, reported["ActualFileSize"]);
                Assert.Equal(0x32, reported["ActualAccessRights1"]);
                Assert.All(reported.Values, value => Assert.True(value is bool || value is int || value is long));

                details.ClearOutcomeDetails();

                Assert.Null(details.GetOutcomeDetails());
                Assert.Null(viewModel.LastFileSettingsComparison);
            });
        }

        [Fact]
        public async Task TheComparisonOfOneRun_DoesNotSurviveIntoTheNext()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var viewModel = CreateViewModel();
                var reader = new FakeFileSettingsProvider { FileSettings = Settings() };

                await Run(viewModel, reader);
                Assert.NotNull(viewModel.LastFileSettingsComparison);

                reader.AuthenticationResult = ERROR.AuthFailure;
                await Run(viewModel, reader);

                Assert.Equal(ERROR.AuthFailure, viewModel.CurrentTaskErrorLevel);
                Assert.Null(viewModel.LastFileSettingsComparison);
            });
        }

        private static async Task Run(MifareDesfireSetupViewModel viewModel, FakeFileSettingsProvider? reader)
        {
            using (ReaderDeviceScope.Use(reader))
            {
                await viewModel.CheckFileSettingsCommand();
            }
        }

        // A file as the task below expects it, unless a row says otherwise.
        private static DESFireFileSettings Settings(
            int fileType = 0,
            uint size = 16,
            int mode = 3,
            int read = 0,
            int write = 1,
            int readWrite = 2,
            int change = 3)
        {
            return new DESFireFileSettings
            {
                FileType = (byte)fileType,
                comSett = (byte)mode,
                accessRights = new[] { (byte)(read | (write << 4)), (byte)(readWrite | (change << 4)) },
                dataFile = new DataFileSetting { fileSize = size }
            };
        }

        private static MifareDesfireSetupViewModel CreateViewModel(FileType_MifareDesfireFileType fileType = FileType_MifareDesfireFileType.StdDataFile)
        {
            return new MifareDesfireSetupViewModel
            {
                SelectedTaskType = TaskType_MifareDesfireTask.CheckFileSettings,
                AppNumberCurrent = "1",
                FileNumberCurrent = "2",
                FileSizeCurrent = "16",
                DesfireAppKeyCurrent = ZeroAesKey,
                SelectedDesfireAppKeyEncryptionTypeCurrent = DESFireKeyType.DF_KEY_AES,
                SelectedDesfireAppKeyNumberCurrent = "0",
                SelectedDesfireFileType = fileType,
                SelectedDesfireFileCryptoMode = EncryptionMode.CM_ENCRYPT,
                SelectedDesfireFileAccessRightRead = TaskAccessRights.AR_KEY0,
                SelectedDesfireFileAccessRightWrite = TaskAccessRights.AR_KEY1,
                SelectedDesfireFileAccessRightReadWrite = TaskAccessRights.AR_KEY2,
                SelectedDesfireFileAccessRightChange = TaskAccessRights.AR_KEY3
            };
        }
    }
}
