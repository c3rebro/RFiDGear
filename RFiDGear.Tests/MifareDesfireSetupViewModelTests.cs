using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.AccessControl;
using RFiDGear.Infrastructure.Tasks;
using RFiDGear.Infrastructure.ReaderProviders;
using RFiDGear.Models;
using RFiDGear.ViewModel;
using RFiDGear.ViewModel.TaskSetupViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Xunit;

namespace RFiDGear.Tests
{
    public class MifareDesfireSetupViewModelTests
    {
        private static Task RunOnStaThreadAsync(Action action)
        {
            return StaTestRunner.RunOnStaThreadAsync(action);
        }

        private static Task RunOnStaThreadAsync(Func<Task> action)
        {
            return StaTestRunner.RunOnStaThreadAsync(action);
        }

        public static IEnumerable<object[]> DesfireKeyNormalizationCases()
        {
            yield return new object[]
            {
                new Action<MifareDesfireSetupViewModel, string>((vm, value) => vm.DesfireMasterKeyCurrent = value),
                new Func<MifareDesfireSetupViewModel, string>(vm => vm.DesfireMasterKeyCurrent),
                "00 01 02 03 04 05 06 07 08 09 0A 0B 0C 0D 0E 0F",
                "000102030405060708090A0B0C0D0E0F"
            };

            yield return new object[]
            {
                new Action<MifareDesfireSetupViewModel, string>((vm, value) => vm.DesfireAppKeyCurrent = value),
                new Func<MifareDesfireSetupViewModel, string>(vm => vm.DesfireAppKeyCurrent),
                "aa:bb:cc:dd:ee:ff:00:11:22:33:44:55:66:77:88:99:aa",
                "AABBCCDDEEFF00112233445566778899"
            };

            yield return new object[]
            {
                new Action<MifareDesfireSetupViewModel, string>((vm, value) => vm.DesfireReadKeyCurrent = value),
                new Func<MifareDesfireSetupViewModel, string>(vm => vm.DesfireReadKeyCurrent),
                "01.02.03.04.05.06.07.08.09.0A.0B.0C.0D.0E.0F.10",
                "0102030405060708090A0B0C0D0E0F10"
            };
        }

        [Fact]
        public async Task Constructor_DisablesTabsByDefault()
        {
            await RunOnStaThreadAsync(() =>
            {
                var viewModel = new MifareDesfireSetupViewModel();

                Assert.False(viewModel.IsDesfireFileAuthoringTabEnabled);
                Assert.False(viewModel.IsDataExplorerEditTabEnabled);
                Assert.False(viewModel.IsDesfirePICCAuthoringTabEnabled);
                Assert.False(viewModel.IsDesfireAuthenticationTabEnabled);
                Assert.False(viewModel.IsDesfireAppAuthenticationTabEnabled);
                Assert.False(viewModel.IsDesfireAppAuthoringTabEnabled);
                Assert.False(viewModel.IsDesfireAppCreationTabEnabled);
            });
        }

        [Fact]
        public async Task CommandDelegator_FinalizesTaskForNoOpSelection()
        {
            await RunOnStaThreadAsync(async () =>
            {
                var viewModel = new MifareDesfireSetupViewModel
                {
                    CurrentTaskErrorLevel = ERROR.NoError
                };

                await viewModel.CommandDelegator.ExecuteAsync(TaskType_MifareDesfireTask.None);

                Assert.True(viewModel.IsTaskCompletedSuccessfully);
            });
        }

        [Theory]
        [InlineData(TaskType_MifareDesfireTask.ChangeDefault, true, true, true, true, true, true, true)]
        [InlineData(TaskType_MifareDesfireTask.ReadAppSettings, false, false, true, false, false, false, false)]
        [InlineData(TaskType_MifareDesfireTask.ApplicationKeyChangeover, false, false, false, false, true, true, false)]
        [InlineData(TaskType_MifareDesfireTask.ApplicationKeySettingsChangeover, false, false, false, false, true, true, false)]
        [InlineData(TaskType_MifareDesfireTask.AuthenticateApplication, false, false, false, true, true, true, false)]
        public async Task SelectedTaskType_SetsExpectedTabAvailability(
            TaskType_MifareDesfireTask taskType,
            bool fileAuthoring,
            bool dataExplorerEdit,
            bool desfirePiccAuthoring,
            bool desfireAuthentication,
            bool desfireAppAuthentication,
            bool desfireAppAuthoring,
            bool desfireAppCreation)
        {
            await RunOnStaThreadAsync(() =>
            {
                var viewModel = new MifareDesfireSetupViewModel
                {
                    SelectedTaskType = taskType
                };

                Assert.Equal(fileAuthoring, viewModel.IsDesfireFileAuthoringTabEnabled);
                Assert.Equal(dataExplorerEdit, viewModel.IsDataExplorerEditTabEnabled);
                Assert.Equal(desfirePiccAuthoring, viewModel.IsDesfirePICCAuthoringTabEnabled);
                Assert.Equal(desfireAuthentication, viewModel.IsDesfireAuthenticationTabEnabled);
                Assert.Equal(desfireAppAuthentication, viewModel.IsDesfireAppAuthenticationTabEnabled);
                Assert.Equal(desfireAppAuthoring, viewModel.IsDesfireAppAuthoringTabEnabled);
                Assert.Equal(desfireAppCreation, viewModel.IsDesfireAppCreationTabEnabled);
            });
        }

        [Theory]
        [InlineData(TaskType_MifareDesfireTask.ApplicationKeyChangeover, true, false)]
        [InlineData(TaskType_MifareDesfireTask.ApplicationKeySettingsChangeover, false, true)]
        [InlineData(TaskType_MifareDesfireTask.ChangeDefault, false, false)]
        [InlineData(TaskType_MifareDesfireTask.ReadAppSettings, false, false)]
        [InlineData(TaskType_MifareDesfireTask.PICCMasterKeyChangeover, false, false)]
        [InlineData(TaskType_MifareDesfireTask.PICCMasterKeySettingsChangeover, false, false)]
        public async Task SelectedTaskType_TogglesKeyInputVisibility(
            TaskType_MifareDesfireTask taskType,
            bool showTarget,
            bool showSettings)
        {
            await RunOnStaThreadAsync(() =>
            {
                var viewModel = new MifareDesfireSetupViewModel
                {
                    SelectedTaskType = taskType
                };

                Assert.Equal(showTarget, viewModel.ShowAppKeyTargetInputs);
                Assert.Equal(showSettings, viewModel.ShowAppKeySettingsInputs);
            });
        }

        [Theory]
        [InlineData(TaskType_MifareDesfireTask.ReadData, false)]
        [InlineData(TaskType_MifareDesfireTask.WriteData, false)]
        [InlineData(TaskType_MifareDesfireTask.ChangeDefault, true)]
        [InlineData(TaskType_MifareDesfireTask.ApplicationKeyChangeover, true)]
        public async Task SelectedTaskType_TogglesCurrentAppKeyInputs(
            TaskType_MifareDesfireTask taskType,
            bool showCurrent)
        {
            await RunOnStaThreadAsync(() =>
            {
                var viewModel = new MifareDesfireSetupViewModel
                {
                    SelectedTaskType = taskType
                };

                Assert.Equal(showCurrent, viewModel.ShowAppKeyCurrentInputs);
            });
        }

        [Theory]
        [InlineData(TaskType_MifareDesfireTask.PICCMasterKeyChangeover, true, false)]
        [InlineData(TaskType_MifareDesfireTask.PICCMasterKeySettingsChangeover, false, true)]
        [InlineData(TaskType_MifareDesfireTask.ChangeDefault, false, false)]
        [InlineData(TaskType_MifareDesfireTask.ReadAppSettings, false, false)]
        [InlineData(TaskType_MifareDesfireTask.ApplicationKeyChangeover, false, false)]
        public async Task SelectedTaskType_TogglesPiccMasterKeyInputVisibility(
            TaskType_MifareDesfireTask taskType,
            bool showTarget,
            bool showSettings)
        {
            await RunOnStaThreadAsync(() =>
            {
                var viewModel = new MifareDesfireSetupViewModel
                {
                    SelectedTaskType = taskType
                };

                Assert.Equal(showTarget, viewModel.ShowPiccMasterKeyTargetInputs);
                Assert.Equal(showSettings, viewModel.ShowPiccMasterKeySettingsInputs);
            });
        }

        [Theory]
        [InlineData(TaskType_MifareDesfireTask.CreateApplication, false, true, false)]
        [InlineData(TaskType_MifareDesfireTask.DeleteApplication, false, false, true)]
        [InlineData(TaskType_MifareDesfireTask.PICCMasterKeyChangeover, true, false, false)]
        public async Task SelectedTaskType_TogglesPiccAndAppCreationVisibility(
            TaskType_MifareDesfireTask taskType,
            bool showPiccSection,
            bool showCreateInputs,
            bool showDeleteInputs)
        {
            await RunOnStaThreadAsync(() =>
            {
                var viewModel = new MifareDesfireSetupViewModel
                {
                    SelectedTaskType = taskType
                };

                Assert.Equal(showPiccSection, viewModel.ShowPiccMasterKeyAuthoringSection);
                Assert.Equal(showCreateInputs, viewModel.ShowCreateApplicationInputs);
                Assert.Equal(showDeleteInputs, viewModel.ShowDeleteApplicationInputs);
            });
        }

        [Theory]
        [InlineData(TaskType_MifareDesfireTask.ReadData, false, false)]
        [InlineData(TaskType_MifareDesfireTask.WriteData, false, false)]
        [InlineData(TaskType_MifareDesfireTask.CreateFile, true, true)]
        [InlineData(TaskType_MifareDesfireTask.DeleteFile, true, true)]
        public async Task SelectedTaskType_TogglesFileMasteringVisibility(
            TaskType_MifareDesfireTask taskType,
            bool showAccessRights,
            bool showAuthoringCommands)
        {
            await RunOnStaThreadAsync(() =>
            {
                var viewModel = new MifareDesfireSetupViewModel
                {
                    SelectedTaskType = taskType
                };

                Assert.Equal(showAccessRights, viewModel.ShowFileAccessRights);
                Assert.Equal(showAuthoringCommands, viewModel.ShowFileAuthoringCommands);
            });
        }

        [Theory]
        [InlineData("0x4bc", 0x4BC)]
        [InlineData("0x4BC", 0x4BC)]
        [InlineData("1212", 1212)]
        public async Task AppNumberNew_SupportsHexOrDecimalInput(string value, int expected)
        {
            await RunOnStaThreadAsync(() =>
            {
                var viewModel = new MifareDesfireSetupViewModel
                {
                    AppNumberNew = value
                };

                Assert.True(viewModel.IsValidAppNumberNew);
                Assert.Equal(expected, viewModel.AppNumberNewAsInt);
            });
        }

        [Theory]
        [InlineData("0x4bc", 0x4BC)]
        [InlineData("1212", 1212)]
        public async Task AppNumberCurrent_SupportsHexPrefixOrDecimalInput(string value, int expected)
        {
            await RunOnStaThreadAsync(() =>
            {
                var viewModel = new MifareDesfireSetupViewModel
                {
                    AppNumberCurrent = value
                };

                Assert.True(viewModel.IsValidAppNumberCurrent);
                Assert.Equal(expected, viewModel.AppNumberCurrentAsInt);
            });
        }

        [Theory]
        [MemberData(nameof(DesfireKeyNormalizationCases))]
        public async Task DesfireKeyInputs_NormalizeToHex32(
            Action<MifareDesfireSetupViewModel, string> setter,
            Func<MifareDesfireSetupViewModel, string> getter,
            string input,
            string expected)
        {
            await RunOnStaThreadAsync(() =>
            {
                var viewModel = new MifareDesfireSetupViewModel();

                setter(viewModel, input);

                Assert.Equal(expected, getter(viewModel));
                Assert.Equal(32, getter(viewModel).Length);
            });
        }

        [Fact]
        public async Task TryGetDesfireWritePayload_ReturnsSelectedSlice()
        {
            await RunOnStaThreadAsync(() =>
            {
                var viewModel = new MifareDesfireSetupViewModel();
                var data = new byte[] { 1, 2, 3, 4, 5 };
                var desfireNode = new RFiDChipGrandChildLayerViewModel(new MifareDesfireFileModel(data, 0), null)
                {
                    SelectedDataIndexStartInBytes = 1,
                    SelectedDataLengthInBytes = 3
                };

                viewModel.ChildNodeViewModelTemp.Children.Add(desfireNode);

                var result = viewModel.TryGetDesfireWritePayload(out var payload, out var errorMessage);

                Assert.True(result);
                Assert.Null(errorMessage);
                Assert.Equal(new byte[] { 2, 3, 4 }, payload);
            });
        }

        [Fact]
        public async Task TryGetDesfireWritePayload_FailsWhenRangeExceedsData()
        {
            await RunOnStaThreadAsync(() =>
            {
                var viewModel = new MifareDesfireSetupViewModel();
                var data = new byte[] { 1, 2, 3 };
                var desfireNode = new RFiDChipGrandChildLayerViewModel(new MifareDesfireFileModel(data, 0), null)
                {
                    SelectedDataIndexStartInBytes = 2,
                    SelectedDataLengthInBytes = 4
                };

                viewModel.ChildNodeViewModelTemp.Children.Add(desfireNode);

                var result = viewModel.TryGetDesfireWritePayload(out var payload, out var errorMessage);

                Assert.False(result);
                Assert.NotNull(errorMessage);
                Assert.Null(payload);
            });
        }

        [Fact]
        public async Task RefreshDesfireDataFromFileBeforeWrite_RoundTripsThroughXmlSerialization()
        {
            await RunOnStaThreadAsync(() =>
            {
                var viewModel = new MifareDesfireSetupViewModel
                {
                    RefreshDesfireDataFromFileBeforeWrite = true,
                    DesfireDataFilePath = @"C:\data\sample.txt"
                };

                var serializer = new XmlSerializer(typeof(MifareDesfireSetupViewModel));
                string xml;

                using (var writer = new StringWriter())
                {
                    serializer.Serialize(writer, viewModel);
                    xml = writer.ToString();
                }

                MifareDesfireSetupViewModel roundTripped;
                using (var reader = new StringReader(xml))
                {
                    roundTripped = (MifareDesfireSetupViewModel)serializer.Deserialize(reader);
                }

                Assert.True(roundTripped.RefreshDesfireDataFromFileBeforeWrite);
                Assert.Equal(viewModel.DesfireDataFilePath, roundTripped.DesfireDataFilePath);
            });
        }

        [Fact]
        public async Task BuildReadDataOutputPath_OverwritesWhenEnabled()
        {
            await RunOnStaThreadAsync(() =>
            {
                var viewModel = new MifareDesfireSetupViewModel
                {
                    DesfireReadDataFilePath = @"C:\data\read.txt",
                    OverwriteReadDataFileOnRead = true
                };

                var result = viewModel.BuildReadDataOutputPath(new DateTime(2024, 1, 2, 3, 4, 5));

                Assert.Equal(@"C:\data\read.txt", result);
            });
        }

        [Fact]
        public async Task BuildReadDataOutputPath_AppendsTimestampWhenNotOverwriting()
        {
            await RunOnStaThreadAsync(() =>
            {
                var viewModel = new MifareDesfireSetupViewModel
                {
                    DesfireReadDataFilePath = @"C:\data\read.txt",
                    OverwriteReadDataFileOnRead = false
                };

                var result = viewModel.BuildReadDataOutputPath(new DateTime(2024, 1, 2, 3, 4, 5));

                Assert.Equal(@"C:\data\read_20240102_030405.txt", result);
            });
        }

        [Theory]
        [InlineData("0x0A", "0A")]
        [InlineData("10", "0A")]
        public async Task KeyVersionCurrent_SupportsHexPrefixOrDecimalInput(string value, string expected)
        {
            await RunOnStaThreadAsync(() =>
            {
                var viewModel = new MifareDesfireSetupViewModel
                {
                    KeyVersionCurrent = value
                };

                Assert.True(viewModel.IsValidKeyVersionCurrent);
                Assert.Equal(expected, viewModel.KeyVersionCurrent, ignoreCase: true);
            });
        }

        [Theory]
        [InlineData("0x0B", "0B")]
        [InlineData("11", "0B")]
        public async Task SelectedDesfireAppKeyVersionTarget_SupportsHexPrefixOrDecimalInput(string value, string expected)
        {
            await RunOnStaThreadAsync(() =>
            {
                var viewModel = new MifareDesfireSetupViewModel
                {
                    SelectedDesfireAppKeyVersionTarget = value
                };

                Assert.True(viewModel.IsValidDesfireAppKeyVersionTarget);
                Assert.Equal(expected, viewModel.SelectedDesfireAppKeyVersionTarget, ignoreCase: true);
            });
        }

        [Theory]
        [InlineData("0x0C", 0x0C)]
        [InlineData("12", 12)]
        public async Task FileNumberCurrent_SupportsHexPrefixOrDecimalInput(string value, int expected)
        {
            await RunOnStaThreadAsync(() =>
            {
                var viewModel = new MifareDesfireSetupViewModel
                {
                    FileNumberCurrent = value
                };

                Assert.True(viewModel.IsValidFileNumberCurrent);
                Assert.Equal(expected, viewModel.FileNumberCurrentAsInt);
            });
        }

        [Theory]
        [InlineData("FF")]
        [InlineData("0x100")]
        public async Task FileNumberCurrent_InvalidValuesAreRejected(string value)
        {
            await RunOnStaThreadAsync(() =>
            {
                var viewModel = new MifareDesfireSetupViewModel
                {
                    FileNumberCurrent = value
                };

                Assert.False(viewModel.IsValidFileNumberCurrent);
            });
        }

        [Fact]
        public async Task BuildAppKeyChangePayload_UsesCurrentAuthKeyAsMasterKey()
        {
            await RunOnStaThreadAsync(() =>
            {
                var viewModel = new MifareDesfireSetupViewModel
                {
                    SelectedDesfireAppKeyEncryptionTypeCurrent = DESFireKeyType.DF_KEY_DES,
                    SelectedDesfireAppKeyEncryptionTypeTarget = DESFireKeyType.DF_KEY_AES,
                    SelectedDesfireAppKeyVersionTarget = "01",
                    DesfireAppKeyTarget = "A1A1A1A1A1A1A1A1A1A1A1A1A1A1A1A1"
                };

                var payload = viewModel.BuildAppKeyChangePayload(
                    appId: 1,
                    keyNumberForChange: 0,
                    authKeyHex: "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF",
                    oldKeyForTargetSlot: "00000000000000000000000000000000",
                    keySettings: DESFireKeySettings.ChangeKeyWithMasterKey);

                Assert.Equal((uint)1, payload.AppId);
                Assert.Equal((byte)0, payload.TargetKeyNo);
                Assert.Equal(DESFireKeyType.DF_KEY_AES, payload.TargetKeyType);
                Assert.Equal("00000000000000000000000000000000", payload.CurrentTargetKeyHex);
                Assert.Equal("A1A1A1A1A1A1A1A1A1A1A1A1A1A1A1A1", payload.NewTargetKeyHex);
                Assert.Equal((byte)0x01, payload.NewTargetKeyVersion);
                Assert.Equal("FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF", payload.MasterKeyHex);
                Assert.Equal(DESFireKeyType.DF_KEY_DES, payload.MasterKeyType);
                Assert.Equal(DESFireKeySettings.ChangeKeyWithMasterKey, payload.KeySettings);
            });
        }

        [Theory]
        [InlineData("0")]
        [InlineData("-1")]
        public async Task ChangeAppKeyCommand_WhenAppIdNotPositive_SetsStatusAndStops(string appNumber)
        {
            await RunOnStaThreadAsync(async () =>
            {
                var viewModel = new MifareDesfireSetupViewModel
                {
                    AppNumberCurrent = appNumber,
                    IsDesfireAppCreationTabEnabled = true
                };

                await viewModel.ChangeAppKeyCommand.ExecuteAsync(null);

                Assert.True(viewModel.IsDesfireAppCreationTabEnabled);
                Assert.Contains("PICC master key tab", viewModel.StatusText);
            });
        }

        [Fact]
        public async Task GetPiccMasterKeyChangeSettings_UsesMinimalChangeKeyWithMasterKey()
        {
            await RunOnStaThreadAsync(() =>
            {
                var settings = MifareDesfireSetupViewModel.GetPiccMasterKeyChangeSettings();

                Assert.Equal(DESFireKeySettings.ChangeKeyWithMasterKey, settings);
            });
        }

        [Fact]
        public async Task BuildChangeAppKeyAuthStatusLine_IncludesSelectedValues()
        {
            await RunOnStaThreadAsync(() =>
            {
                var timestamp = new DateTime(2024, 2, 3, 4, 5, 6);

                var line = MifareDesfireSetupViewModel.BuildChangeAppKeyAuthStatusLine(
                    timestamp,
                    appId: 1,
                    appKeyNumber: 2,
                    selectedSettings: DESFireKeySettings.ChangeKeyFrozen,
                    authKeyNo: 3);

                Assert.Contains("AppID 1", line);
                Assert.Contains("KeyNo 2", line);
                Assert.Contains("Settings ChangeKeyFrozen", line);
                Assert.Contains("AuthKeyNo 3", line);
            });
        }

        [Fact]
        public async Task BuildChangeKeyFrozenWarningLine_IndicatesFrozenPolicy()
        {
            await RunOnStaThreadAsync(() =>
            {
                var timestamp = new DateTime(2024, 2, 3, 4, 5, 6);

                var line = MifareDesfireSetupViewModel.BuildChangeKeyFrozenWarningLine(timestamp);

                Assert.Contains("Warning", line);
                Assert.Contains("ChangeKeyFrozen", line);
            });
        }

        [Fact]
        public async Task ReadDataCommand_UsesReadKeyForAuthenticationAndRead()
        {
            await RunOnStaThreadAsync(async () =>
            {
                var fakeProvider = new FakeElatecNetProvider();
                var viewModel = new MifareDesfireSetupViewModel
                {
                    AppNumberCurrent = "1",
                    AppNumberNew = "1",
                    FileNumberCurrent = "1",
                    FileSizeCurrent = "4",
                    DesfireReadKeyCurrent = "00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00",
                    SelectedDesfireReadKeyEncryptionType = DESFireKeyType.DF_KEY_AES,
                    SelectedDesfireReadKeyNumber = "1",
                    DesfireWriteKeyCurrent = "11 11 11 11 11 11 11 11 11 11 11 11 11 11 11 11",
                    SelectedDesfireWriteKeyEncryptionType = DESFireKeyType.DF_KEY_3K3DES,
                    SelectedDesfireWriteKeyNumber = "2"
                };

                viewModel.ChildNodeViewModelTemp.Children.Add(new RFiDChipGrandChildLayerViewModel(
                    new MifareDesfireFileModel(new byte[4], 0),
                    viewModel.ChildNodeViewModelTemp));

                var originalReader = ReaderDevice.Reader;
                var originalInstance = GetReaderDeviceInstance();
                try
                {
                    ReaderDevice.Reader = ReaderTypes.Elatec;
                    SetReaderDeviceInstance(fakeProvider);

                    await viewModel.ReadDataCommand.ExecuteAsync(null);

                    Assert.Equal(viewModel.DesfireReadKeyCurrent, fakeProvider.LastAuthKey);
                    Assert.Equal(viewModel.SelectedDesfireReadKeyEncryptionType, fakeProvider.LastAuthKeyType);
                    Assert.Equal(1, fakeProvider.LastAuthKeyNumber);
                    Assert.Equal(viewModel.DesfireReadKeyCurrent, fakeProvider.LastReadKey);
                    Assert.Equal(viewModel.SelectedDesfireReadKeyEncryptionType, fakeProvider.LastReadKeyType);
                    Assert.Equal(1, fakeProvider.LastReadKeyNumber);
                    Assert.Null(fakeProvider.LastWriteKey);
                }
                finally
                {
                    ReaderDevice.Reader = originalReader;
                    SetReaderDeviceInstance(originalInstance);
                }
            });
        }

        [Fact]
        public async Task WriteDataCommand_UsesWriteKeyForAuthenticationAndWrite()
        {
            await RunOnStaThreadAsync(async () =>
            {
                var fakeProvider = new FakeElatecNetProvider();
                var viewModel = new MifareDesfireSetupViewModel
                {
                    AppNumberCurrent = "1",
                    AppNumberNew = "1",
                    FileNumberCurrent = "1",
                    FileSizeCurrent = "4",
                    DesfireReadKeyCurrent = "00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00",
                    SelectedDesfireReadKeyEncryptionType = DESFireKeyType.DF_KEY_AES,
                    SelectedDesfireReadKeyNumber = "1",
                    DesfireWriteKeyCurrent = "22 22 22 22 22 22 22 22 22 22 22 22 22 22 22 22",
                    SelectedDesfireWriteKeyEncryptionType = DESFireKeyType.DF_KEY_DES,
                    SelectedDesfireWriteKeyNumber = "3"
                };

                viewModel.ChildNodeViewModelTemp.Children.Add(new RFiDChipGrandChildLayerViewModel(
                    new MifareDesfireFileModel(new byte[] { 0x01, 0x02, 0x03, 0x04 }, 0),
                    viewModel.ChildNodeViewModelTemp));

                var originalReader = ReaderDevice.Reader;
                var originalInstance = GetReaderDeviceInstance();
                try
                {
                    ReaderDevice.Reader = ReaderTypes.Elatec;
                    SetReaderDeviceInstance(fakeProvider);

                    await viewModel.WriteDataCommand.ExecuteAsync(null);

                    Assert.Equal(viewModel.DesfireWriteKeyCurrent, fakeProvider.LastAuthKey);
                    Assert.Equal(viewModel.SelectedDesfireWriteKeyEncryptionType, fakeProvider.LastAuthKeyType);
                    Assert.Equal(3, fakeProvider.LastAuthKeyNumber);
                    Assert.Null(fakeProvider.LastReadKey);
                    Assert.Equal(viewModel.DesfireWriteKeyCurrent, fakeProvider.LastWriteKey);
                    Assert.Equal(viewModel.SelectedDesfireWriteKeyEncryptionType, fakeProvider.LastWriteKeyType);
                    Assert.Equal(3, fakeProvider.LastWriteKeyNumber);
                }
                finally
                {
                    ReaderDevice.Reader = originalReader;
                    SetReaderDeviceInstance(originalInstance);
                }
            });
        }

        [Theory]
        [InlineData("0", "0", AccessCondition_MifareDesfireAppCreation.ChangeKeyUsingMK, false)]
        [InlineData("0", "1", AccessCondition_MifareDesfireAppCreation.ChangeKeyUsingMK, false)]
        [InlineData("1", "0", AccessCondition_MifareDesfireAppCreation.ChangeKeyUsingMK, false)]
        [InlineData("1", "1", AccessCondition_MifareDesfireAppCreation.ChangeKeyUsingMK, true)]
        [InlineData("0", "0", AccessCondition_MifareDesfireAppCreation.ChangeKeyUsingKeyNo, false)]
        [InlineData("0", "1", AccessCondition_MifareDesfireAppCreation.ChangeKeyUsingKeyNo, false)]
        [InlineData("1", "0", AccessCondition_MifareDesfireAppCreation.ChangeKeyUsingKeyNo, false)]
        [InlineData("1", "1", AccessCondition_MifareDesfireAppCreation.ChangeKeyUsingKeyNo, false)]
        public async Task ShowAppKeyOldInputs_ReflectsAppIdKeyAndPolicy(
            string appId,
            string keyNumber,
            AccessCondition_MifareDesfireAppCreation changeKeyPolicy,
            bool expected)
        {
            await RunOnStaThreadAsync(() =>
            {
                var viewModel = new MifareDesfireSetupViewModel
                {
                    SelectedTaskType = TaskType_MifareDesfireTask.ApplicationKeyChangeover,
                    AppNumberCurrent = appId,
                    SelectedDesfireAppKeyNumberCurrent = keyNumber,
                    SelectedDesfireAppKeySettingsCreateNewApp = changeKeyPolicy
                };

                Assert.Equal(expected, viewModel.ShowAppKeyOldInputs);
            });
        }

        private static ReaderDevice GetReaderDeviceInstance()
        {
            return typeof(ReaderDevice).GetField("instance", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null) as ReaderDevice;
        }

        private static void SetReaderDeviceInstance(ReaderDevice instance)
        {
            typeof(ReaderDevice).GetField("instance", BindingFlags.NonPublic | BindingFlags.Static)?.SetValue(null, instance);
        }

        private sealed class FakeElatecNetProvider : ElatecNetProvider
        {
            public string LastAuthKey { get; private set; }
            public DESFireKeyType LastAuthKeyType { get; private set; }
            public int LastAuthKeyNumber { get; private set; }
            public string LastReadKey { get; private set; }
            public DESFireKeyType LastReadKeyType { get; private set; }
            public int LastReadKeyNumber { get; private set; }
            public string LastWriteKey { get; private set; }
            public DESFireKeyType LastWriteKeyType { get; private set; }
            public int LastWriteKeyNumber { get; private set; }

            public override Task<ERROR> AuthToMifareDesfireApplication(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumber, int _appID = 0)
            {
                LastAuthKey = _applicationMasterKey;
                LastAuthKeyType = _keyType;
                LastAuthKeyNumber = _keyNumber;
                return Task.FromResult(ERROR.NoError);
            }

            public override Task<ERROR> ReadMiFareDESFireChipFile(string _appReadKey, DESFireKeyType _keyTypeAppReadKey, int _readKeyNo,
                EncryptionMode _encMode,
                int _fileNo, int _appID, int _fileSize)
            {
                LastReadKey = _appReadKey;
                LastReadKeyType = _keyTypeAppReadKey;
                LastReadKeyNumber = _readKeyNo;
                MifareDESFireData = new byte[_fileSize];
                return Task.FromResult(ERROR.NoError);
            }

            public override Task<ERROR> WriteMiFareDESFireChipFile(string _appWriteKey, DESFireKeyType _keyTypeAppWriteKey, int _writeKeyNo,
                EncryptionMode _encMode,
                int _fileNo, int _appID, byte[] _data)
            {
                LastWriteKey = _appWriteKey;
                LastWriteKeyType = _keyTypeAppWriteKey;
                LastWriteKeyNumber = _writeKeyNo;
                return Task.FromResult(ERROR.NoError);
            }
        }

        [Theory]
        [InlineData(0, DESFireKeySettings.ChangeKeyWithMasterKey, 5, 0)]
        [InlineData(1, DESFireKeySettings.ChangeKeyWithMasterKey, 5, 0)]
        [InlineData(1, DESFireKeySettings.ChangeKeyWithTargetedKeyNumber, 5, 5)]
        public async Task GetAuthKeyNumberForChangeAppKey_MatchesPolicy(
            int appId,
            DESFireKeySettings changeKeyMode,
            int appKeyNumber,
            int expectedAuthKeyNumber)
        {
            await RunOnStaThreadAsync(() =>
            {
                var authKeyNumber = MifareDesfireSetupViewModel.GetAuthKeyNumberForChangeAppKey(
                    appId,
                    changeKeyMode,
                    appKeyNumber);

                Assert.Equal(expectedAuthKeyNumber, authKeyNumber);
            });
        }
    }
}
