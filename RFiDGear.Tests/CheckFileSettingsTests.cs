using RFiDGear.Infrastructure;
using RFiDGear.Models;
using RFiDGear.UI.MVVMDialogs.ViewModels.Interfaces;
using RFiDGear.Infrastructure.Tasks;
using RFiDGear.Services.TaskExecution;
using RFiDGear.ViewModel.TaskSetupViewModels;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Serialization;
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

                var reason = ResourceLoader.GetResource("textBoxStatusFileSettingsNoReader").Replace("{0}: ", string.Empty);
                Assert.False(string.IsNullOrWhiteSpace(reason));
                Assert.Equal(ERROR.TransportError, viewModel.CurrentTaskErrorLevel);
                Assert.False(viewModel.IsTaskCompletedSuccessfully);
                Assert.Contains(reason, viewModel.StatusText);
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

        [Theory]
        [InlineData(DESFireKeyType.DF_KEY_AES)]
        [InlineData(DESFireKeyType.DF_KEY_DES)]
        public async Task TheReaderIsAskedWithTheKeyTheApplicationAndTheFileOfTheTask(DESFireKeyType keyType)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                // Nothing here is a default: a slip to another key, key type, key number, application or file would go
                // unnoticed if the reader accepted whatever it was given.
                const string key = "00112233445566778899AABBCCDDEEFF";
                var viewModel = CreateViewModel();
                viewModel.AppNumberCurrent = "0xF482D0";
                viewModel.FileNumberCurrent = "7";
                viewModel.SelectedDesfireAppKeyEncryptionTypeCurrent = keyType;
                viewModel.DesfireAppKeyCurrent = key;
                viewModel.SelectedDesfireAppKeyNumberCurrent = "2";
                var reader = new FakeFileSettingsProvider { FileSettings = Settings() };

                await Run(viewModel, reader);

                Assert.Equal(ERROR.NoError, viewModel.CurrentTaskErrorLevel);
                Assert.Equal(key, reader.LastAuthKey!.Replace(" ", string.Empty), ignoreCase: true);
                Assert.Equal(key, reader.LastReadKey!.Replace(" ", string.Empty), ignoreCase: true);
                Assert.Equal((keyType, 2, 0xF482D0), (reader.LastAuthKeyType, reader.LastAuthKeyNumber, reader.LastAuthAppId));
                Assert.Equal((keyType, 2), (reader.LastReadKeyType, reader.LastReadKeyNumber));
                Assert.Equal((0xF482D0, 7), (reader.LastFileSettingsAppId, reader.LastFileSettingsFileNo));
            });
        }

        [Fact]
        public async Task ATaskThatWasJustCreatedInTheDialog_RunsWithoutRetypingTheDefaults()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                // A new task holds the file number 0 and the size 16 whose validity flags are still "not yet validated".
                var viewModel = new MifareDesfireSetupViewModel(null, new ObservableCollection<IDialogViewModel>())
                {
                    SelectedTaskType = TaskType_MifareDesfireTask.CheckFileSettings,
                    AppNumberCurrent = "1",
                    DesfireAppKeyCurrent = ZeroAesKey,
                    SelectedDesfireAppKeyEncryptionTypeCurrent = DESFireKeyType.DF_KEY_AES,
                    SelectedDesfireAppKeyNumberCurrent = "0",
                    SelectedDesfireFileType = FileType_MifareDesfireFileType.StdDataFile,
                    SelectedDesfireFileCryptoMode = EncryptionMode.CM_ENCRYPT,
                    SelectedDesfireFileAccessRightRead = TaskAccessRights.AR_KEY0,
                    SelectedDesfireFileAccessRightWrite = TaskAccessRights.AR_KEY1,
                    SelectedDesfireFileAccessRightReadWrite = TaskAccessRights.AR_KEY2,
                    SelectedDesfireFileAccessRightChange = TaskAccessRights.AR_KEY3
                };
                Assert.Equal(("0", "16"), (viewModel.FileNumberCurrent, viewModel.FileSizeCurrent));
                Assert.Null(viewModel.IsValidFileNumberCurrent);
                Assert.Null(viewModel.IsValidFileSizeCurrent);

                await Run(viewModel, new FakeFileSettingsProvider { FileSettings = Settings() });

                Assert.Equal(ERROR.NoError, viewModel.CurrentTaskErrorLevel);
            });
        }

        [Fact]
        public async Task FreeAndNever_AreComparedLikeKeys_InTheHighNibbleToo()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                // 14 and 15 are the largest nibble values, and write and change are packed into the high nibbles.
                var viewModel = CreateViewModel();
                viewModel.SelectedDesfireFileAccessRightRead = TaskAccessRights.AR_FREE;
                viewModel.SelectedDesfireFileAccessRightWrite = TaskAccessRights.AR_NEVER;
                viewModel.SelectedDesfireFileAccessRightReadWrite = TaskAccessRights.AR_KEY13;
                viewModel.SelectedDesfireFileAccessRightChange = TaskAccessRights.AR_FREE;

                await Run(viewModel, new FakeFileSettingsProvider { FileSettings = Settings(read: 14, write: 15, readWrite: 13, change: 14) });
                Assert.Equal(ERROR.NoError, viewModel.CurrentTaskErrorLevel);
                Assert.Equal(0xFE, viewModel.LastFileSettingsComparison.ExpectedAccessRights0);
                Assert.Equal(0xED, viewModel.LastFileSettingsComparison.ExpectedAccessRights1);

                await Run(viewModel, new FakeFileSettingsProvider { FileSettings = Settings(read: 15, write: 14, readWrite: 13, change: 14) });
                Assert.Equal(ERROR.IsNotTrue, viewModel.CurrentTaskErrorLevel);
            });
        }

        [Fact]
        public async Task TheHighestKeyNumber_IsAccepted()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var viewModel = CreateViewModel();
                viewModel.SelectedDesfireAppKeyNumberCurrent = "13";
                var reader = new FakeFileSettingsProvider { FileSettings = Settings() };

                await Run(viewModel, reader);

                Assert.Equal(ERROR.NoError, viewModel.CurrentTaskErrorLevel);
                Assert.Equal(13, reader.LastAuthKeyNumber);
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

        [Theory]
        [InlineData("14", EncryptionMode.CM_ENCRYPT, "textBoxStatusFileSettingsInvalidKeyNumber")]
        [InlineData("0", EncryptionMode.CM_UNKNOWN, "textBoxStatusFileSettingsUnknownCommunicationMode")]
        public async Task TheReasonOfARefusal_IsSaid(string keyNumber, EncryptionMode mode, string reasonKey)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var viewModel = CreateViewModel();
                viewModel.SelectedDesfireAppKeyNumberCurrent = keyNumber;
                viewModel.SelectedDesfireFileCryptoMode = mode;

                await Run(viewModel, new FakeFileSettingsProvider { FileSettings = Settings() });

                // The text follows the language; the template starts with the time, which is not part of the reason.
                var reason = ResourceLoader.GetResource(reasonKey).Replace("{0}: ", string.Empty);
                Assert.False(string.IsNullOrWhiteSpace(reason));
                Assert.Equal(ERROR.ProtocolConstraint, viewModel.CurrentTaskErrorLevel);
                Assert.Contains(reason, viewModel.StatusText);
            });
        }

        [Fact]
        public async Task ATaskThatIsEdited_KeepsEverySettingOfTheCheck()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var source = CreateViewModel(FileType_MifareDesfireFileType.BackupFile);
                source.FileNumberCurrent = "9";
                source.FileSizeCurrent = "300";
                source.SelectedDesfireAppKeyNumberCurrent = "2";
                source.SelectedDesfireFileCryptoMode = EncryptionMode.CM_MAC;
                source.SelectedDesfireFileAccessRightRead = TaskAccessRights.AR_FREE;
                source.SelectedDesfireFileAccessRightWrite = TaskAccessRights.AR_KEY5;
                source.SelectedDesfireFileAccessRightReadWrite = TaskAccessRights.AR_NEVER;
                source.SelectedDesfireFileAccessRightChange = TaskAccessRights.AR_KEY13;

                // The dialog opened for a task that is already in the list copies every property of it.
                var editor = new MifareDesfireSetupViewModel(source, new ObservableCollection<IDialogViewModel>());

                Assert.Equal(TaskType_MifareDesfireTask.CheckFileSettings, editor.SelectedTaskType);
                Assert.Equal(source.AppNumberCurrent, editor.AppNumberCurrent);
                Assert.Equal(source.DesfireAppKeyCurrent, editor.DesfireAppKeyCurrent);
                Assert.Equal(DESFireKeyType.DF_KEY_AES, editor.SelectedDesfireAppKeyEncryptionTypeCurrent);
                Assert.Equal("2", editor.SelectedDesfireAppKeyNumberCurrent);
                Assert.Equal("9", editor.FileNumberCurrent);
                Assert.Equal("300", editor.FileSizeCurrent);
                Assert.Equal(FileType_MifareDesfireFileType.BackupFile, editor.SelectedDesfireFileType);
                Assert.Equal(EncryptionMode.CM_MAC, editor.SelectedDesfireFileCryptoMode);
                Assert.Equal(TaskAccessRights.AR_FREE, editor.SelectedDesfireFileAccessRightRead);
                Assert.Equal(TaskAccessRights.AR_KEY5, editor.SelectedDesfireFileAccessRightWrite);
                Assert.Equal(TaskAccessRights.AR_NEVER, editor.SelectedDesfireFileAccessRightReadWrite);
                Assert.Equal(TaskAccessRights.AR_KEY13, editor.SelectedDesfireFileAccessRightChange);

                // And the tabs and the action of the task are there.
                Assert.True(editor.ShowCheckFileSettingsButton);
                Assert.True(editor.IsDesfireFileAuthoringTabEnabled);
                Assert.True(editor.IsDesfireAppAuthenticationTabEnabled);
                Assert.False(editor.IsDesfirePICCAuthoringTabEnabled);
            });
        }

        [Fact]
        public async Task ANegativeFileSize_IsInvalid_AndNothingThrows()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                // It used to allocate an array of that size.
                var viewModel = new MifareDesfireSetupViewModel { FileSizeCurrent = "-5" };

                Assert.False(viewModel.IsValidFileSizeCurrent);

                // A project that holds one still loads, and the task with it is refused when it runs.
                var project = new ChipTaskHandlerModel();
                project.TaskCollection.Add(CreateViewModel());
                var serializer = new XmlSerializer(typeof(ChipTaskHandlerModel));
                using var writer = new StringWriter();
                serializer.Serialize(writer, project);
                var xml = writer.ToString();
                Assert.Contains("<FileSizeCurrent>16</FileSizeCurrent>", xml);

                using var reader = new StringReader(xml.Replace("<FileSizeCurrent>16</FileSizeCurrent>", "<FileSizeCurrent>-5</FileSizeCurrent>"));
                var loaded = (ChipTaskHandlerModel)serializer.Deserialize(reader)!;

                var task = Assert.IsType<MifareDesfireSetupViewModel>(Assert.Single(loaded.TaskCollection));
                Assert.Equal("-5", task.FileSizeCurrent);
                Assert.False(task.IsValidFileSizeCurrent);
            });
        }

        [Fact]
        public async Task TheButton_FinishesTheTaskOnEveryPress()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var viewModel = CreateViewModel();
                var finished = Observe(viewModel);
                var reader = new FakeFileSettingsProvider { FileSettings = Settings() };

                using (ReaderDeviceScope.Use(reader))
                {
                    await viewModel.CheckFileSettingsRelayCommand.ExecuteAsync(null);
                    Assert.True(viewModel.IsTaskCompletedSuccessfully);
                    Assert.Equal(new[] { false }, finished);

                    // The card now differs; the second press must say so, once.
                    reader.FileSettings = Settings(size: 15);
                    await viewModel.CheckFileSettingsRelayCommand.ExecuteAsync(null);
                }

                Assert.False(viewModel.IsTaskCompletedSuccessfully);
                Assert.Equal(ERROR.IsNotTrue, viewModel.CurrentTaskErrorLevel);
                Assert.Equal(new[] { false, false }, finished);
            });
        }

        [Fact]
        public async Task TheButtonThatThrows_StillFinishesTheTaskOnce()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var viewModel = CreateViewModel();
                var finished = Observe(viewModel);
                var reader = new FakeFileSettingsProvider { FileSettings = Settings(), ThrowOnRead = true };

                using (ReaderDeviceScope.Use(reader))
                {
                    await Assert.ThrowsAsync<System.InvalidOperationException>(() => viewModel.CheckFileSettingsRelayCommand.ExecuteAsync(null));
                }

                // The reader is released, and the task does not count as done.
                Assert.Equal(new[] { false }, finished);
                Assert.False(viewModel.IsTaskCompletedSuccessfully);
            });
        }

        [Fact]
        public async Task TheTaskLoop_FinishesTheTaskOncePerRun()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                // The task loop starts the task through the command delegator, not through the button.
                var viewModel = CreateViewModel();
                var finished = Observe(viewModel);
                var reader = new FakeFileSettingsProvider { FileSettings = Settings() };

                using (ReaderDeviceScope.Use(reader))
                {
                    await viewModel.CommandDelegator.ExecuteAsync(TaskType_MifareDesfireTask.CheckFileSettings);
                    Assert.True(viewModel.IsTaskCompletedSuccessfully);
                    Assert.Equal(new[] { false }, finished);

                    reader.FileSettings = Settings(size: 15);
                    await viewModel.CommandDelegator.ExecuteAsync(TaskType_MifareDesfireTask.CheckFileSettings);
                }

                Assert.False(viewModel.IsTaskCompletedSuccessfully);
                Assert.Equal(new[] { false, false }, finished);
            });
        }

        [Fact]
        public async Task TheTaskReportsItsComparisonToTheLogAsPlainNumbers_AndForgetsItOnRequest()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var viewModel = CreateViewModel();
                var details = (ITaskOutcomeDetails)viewModel;
                Assert.Empty(details.GetOutcomeDetails());

                await Run(viewModel, new FakeFileSettingsProvider { FileSettings = Settings() });

                var reported = details.GetOutcomeDetails()!;
                Assert.Equal("FileSettingsComparison", details.OutcomeDetailsSection);
                Assert.True((bool)reported["Matches"]);
                Assert.Equal(16L, reported["ActualFileSize"]);
                Assert.Equal(0x32, reported["ActualAccessRights1"]);
                Assert.All(reported.Values, value => Assert.True(value is bool || value is int || value is long));

                // The details are those of the check and of nothing else the same view model is switched to.
                viewModel.SelectedTaskType = TaskType_MifareDesfireTask.ReadData;
                Assert.Empty(details.GetOutcomeDetails());
                viewModel.SelectedTaskType = TaskType_MifareDesfireTask.CheckFileSettings;
                Assert.NotEmpty(details.GetOutcomeDetails());

                details.ClearOutcomeDetails();

                Assert.Empty(details.GetOutcomeDetails());
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

        [Fact]
        public async Task TheCheckUsesTheDialogAsItWasPressed_EvenWhenTheDialogIsEditedWhileTheCardAnswers()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var viewModel = CreateViewModel();
                var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var reader = new FakeFileSettingsProvider { FileSettings = Settings(), AuthenticationGate = release.Task };

                using (ReaderDeviceScope.Use(reader))
                {
                    var check = viewModel.CheckFileSettingsRelayCommand.ExecuteAsync(null);
                    Assert.Equal(1, reader.AuthCalls);

                    // Edited while the card is busy with the authentication: another size, application, file, key and key
                    // number. The card matches what the dialog said when the button was pressed.
                    viewModel.FileSizeCurrent = "32";
                    viewModel.AppNumberCurrent = "7";
                    viewModel.FileNumberCurrent = "9";
                    viewModel.DesfireAppKeyCurrent = "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF";
                    viewModel.SelectedDesfireAppKeyNumberCurrent = "5";

                    release.SetResult(true);
                    await check;
                }

                Assert.Equal(ERROR.NoError, viewModel.CurrentTaskErrorLevel);
                Assert.True(viewModel.LastFileSettingsComparison.Matches);
                Assert.Equal((1, 2), (reader.LastFileSettingsAppId, reader.LastFileSettingsFileNo));
                Assert.Equal(ZeroAesKey, reader.LastReadKey!.Replace(" ", string.Empty), ignoreCase: true);
                Assert.Equal(0, reader.LastReadKeyNumber);
            });
        }

        private static List<bool> Observe(MifareDesfireSetupViewModel viewModel)
        {
            var finished = new List<bool>();
            viewModel.OnUpdateStatus = isBusy => finished.Add(isBusy);

            return finished;
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
