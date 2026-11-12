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
using Xunit.Abstractions;

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
        [InlineData(TaskType_MifareDesfireTask.ReadAppSettings, false, false, true, true, false, false, false)]
        [InlineData(TaskType_MifareDesfireTask.CheckAppKeyCount, false, false, true, true, false, false, false)]
        [InlineData(TaskType_MifareDesfireTask.CheckFileSettings, true, false, false, false, true, true, false)]
        [InlineData(TaskType_MifareDesfireTask.ApplicationKeyChangeover, false, false, false, false, true, true, false)]
        [InlineData(TaskType_MifareDesfireTask.ApplicationKeySettingsChangeover, false, false, false, false, true, true, false)]
        [InlineData(TaskType_MifareDesfireTask.AuthenticateApplication, false, false, false, true, true, true, false)]
        [InlineData(TaskType_MifareDesfireTask.ReadData, true, true, false, false, true, true, false)]
        [InlineData(TaskType_MifareDesfireTask.CreateApplication, false, false, true, true, false, false, true)]
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
        [InlineData(TaskType_MifareDesfireTask.ApplicationKeyChangeover, true, true)]
        [InlineData(TaskType_MifareDesfireTask.ApplicationKeySettingsChangeover, false, true)]
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
        [InlineData(TaskType_MifareDesfireTask.PICCMasterKeyChangeover, true, true)]
        [InlineData(TaskType_MifareDesfireTask.PICCMasterKeySettingsChangeover, false, true)]
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
        [InlineData(TaskType_MifareDesfireTask.PICCMasterKeyChangeover, true)]
        [InlineData(TaskType_MifareDesfireTask.PICCMasterKeySettingsChangeover, true)]
        [InlineData(TaskType_MifareDesfireTask.ReadAppSettings, true)]
        [InlineData(TaskType_MifareDesfireTask.CheckAppKeyCount, false)]
        [InlineData(TaskType_MifareDesfireTask.FormatDesfireCard, false)]
        [InlineData(TaskType_MifareDesfireTask.ApplicationKeyChangeover, false)]
        public async Task SelectedTaskType_ShowsKeySettingsGroupForPiccTasksAndTheAppConfigCheck(
            TaskType_MifareDesfireTask taskType,
            bool showGroup)
        {
            await RunOnStaThreadAsync(() =>
            {
                var viewModel = new MifareDesfireSetupViewModel
                {
                    SelectedTaskType = taskType
                };

                Assert.Equal(showGroup, viewModel.ShowKeySettingsGroup);
            });
        }

        [Theory]
        [InlineData(TaskType_MifareDesfireTask.ApplicationKeyChangeover, "labelCurrentDesfireKeySettingsContext", "groupBoxheaderMifareDesfireCardMasterSettingTarget")]
        [InlineData(TaskType_MifareDesfireTask.PICCMasterKeySettingsChangeover, "labelTargetDesfireKeySettings", "groupBoxheaderMifareDesfireCardMasterSettingTarget")]
        [InlineData(TaskType_MifareDesfireTask.ReadAppSettings, "labelExpectedDesfireKeySettings", "groupBoxHeaderMifareDesfireExpectedAppSettings")]
        [InlineData(TaskType_MifareDesfireTask.CheckAppKeyCount, null, "groupBoxHeaderMifareDesfireExpectedAppSettings")]
        public async Task SelectedTaskType_NamesWhatTheShownSettingsAre(
            TaskType_MifareDesfireTask taskType,
            string? keySettingsKey,
            string targetGroupKey)
        {
            await RunOnStaThreadAsync(() =>
            {
                var viewModel = new MifareDesfireSetupViewModel
                {
                    SelectedTaskType = taskType
                };

                // The texts follow the language, so they are compared with the resource, which must exist. Where the
                // key-settings group is not shown at all there is no caption to pin.
                if (keySettingsKey != null)
                {
                    Assert.False(string.IsNullOrWhiteSpace(ResourceLoader.GetResource(keySettingsKey)), keySettingsKey);
                    Assert.Equal(ResourceLoader.GetResource(keySettingsKey), viewModel.KeySettingsPanelHeader);
                }

                Assert.False(string.IsNullOrWhiteSpace(ResourceLoader.GetResource(targetGroupKey)), targetGroupKey);
                Assert.Equal(ResourceLoader.GetResource(targetGroupKey), viewModel.TargetGroupHeader);
            });
        }

        [Theory]
        [InlineData(TaskType_MifareDesfireTask.ApplicationKeyChangeover, false)]
        [InlineData(TaskType_MifareDesfireTask.ApplicationKeySettingsChangeover, true)]
        [InlineData(TaskType_MifareDesfireTask.PICCMasterKeyChangeover, false)]
        public async Task SelectedTaskType_ShowsApplicationSettingsActionOnlyForSettingsChange(
            TaskType_MifareDesfireTask taskType,
            bool expected)
        {
            await RunOnStaThreadAsync(() =>
            {
                var viewModel = new MifareDesfireSetupViewModel { SelectedTaskType = taskType };
                Assert.Equal(expected, viewModel.ShowAppKeySettingsEditButton);
            });
        }

        [Theory]
        [InlineData(TaskType_MifareDesfireTask.PICCMasterKeyChangeover, false)]
        [InlineData(TaskType_MifareDesfireTask.PICCMasterKeySettingsChangeover, true)]
        [InlineData(TaskType_MifareDesfireTask.ApplicationKeyChangeover, false)]
        public async Task SelectedTaskType_ShowsPiccSettingsActionOnlyForSettingsChange(
            TaskType_MifareDesfireTask taskType,
            bool expected)
        {
            await RunOnStaThreadAsync(() =>
            {
                var viewModel = new MifareDesfireSetupViewModel { SelectedTaskType = taskType };
                Assert.Equal(expected, viewModel.ShowPiccMasterKeySettingsEditButton);
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
                    SelectedDesfireAppKeyVersionTarget = "01"
                };

                var payload = viewModel.BuildAppKeyChangePayload(
                    appId: 1,
                    keyNumberForChange: 0,
                    authKeyHex: "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF",
                    oldKeyForTargetSlot: "00000000000000000000000000000000",
                    newTargetKeyHex: "A1A1A1A1A1A1A1A1A1A1A1A1A1A1A1A1",
                    currentKeySettings: DESFireKeySettings.ChangeKeyWithTargetedKeyNumber |
                                        DESFireKeySettings.AllowChangeMasterKey |
                                        DESFireKeySettings.ConfigurationChangeable);

                Assert.Equal((uint)1, payload.AppId);
                Assert.Equal((byte)0, payload.TargetKeyNo);
                Assert.Equal(DESFireKeyType.DF_KEY_AES, payload.TargetKeyType);
                Assert.Equal("00000000000000000000000000000000", payload.CurrentTargetKeyHex);
                Assert.Equal("A1A1A1A1A1A1A1A1A1A1A1A1A1A1A1A1", payload.NewTargetKeyHex);
                Assert.Equal((byte)0x01, payload.NewTargetKeyVersion);
                Assert.Equal("FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF", payload.MasterKeyHex);
                Assert.Equal(DESFireKeyType.DF_KEY_DES, payload.MasterKeyType);
                Assert.Equal(
                    DESFireKeySettings.ChangeKeyWithTargetedKeyNumber |
                    DESFireKeySettings.AllowChangeMasterKey |
                    DESFireKeySettings.ConfigurationChangeable,
                    payload.CurrentKeySettings);
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

        [Theory]
        [InlineData("0xF482D0", 0xF482D0)]
        [InlineData("16024272", 0xF482D0)]
        public async Task AppNumberTarget_AcceptsHexAndDecimalApplicationIds(string value, int expected)
        {
            await RunOnStaThreadAsync(() =>
            {
                var viewModel = new MifareDesfireSetupViewModel { AppNumberTarget = value };

                Assert.True(viewModel.IsValidAppNumberTarget);
                Assert.Equal(expected, viewModel.AppNumberTargetAsInt);
            });
        }

        [Theory]
        [InlineData(ERROR.NoError, true)]
        [InlineData(ERROR.AuthFailure, false)]
        public async Task ChangeAppKeyCommand_RequiresAuthenticationWithNewKey(
            ERROR postChangeAuthenticationResult,
            bool expectedSuccess)
        {
            await RunOnStaThreadAsync(async () =>
            {
                var fakeProvider = new FakeElatecNetProvider
                {
                    PostChangeAuthenticationResult = postChangeAuthenticationResult
                };
                var originalInstance = GetReaderDeviceInstance();
                var originalReader = ReaderDevice.Reader;
                try
                {
                    ReaderDevice.Reader = ReaderTypes.Elatec;
                    SetReaderDeviceInstance(fakeProvider);
                    var viewModel = new MifareDesfireSetupViewModel
                    {
                        SelectedTaskType = TaskType_MifareDesfireTask.ApplicationKeyChangeover,
                        AppNumberCurrent = "1",
                        AppNumberTarget = "1",
                        SelectedDesfireAppKeySettingsCreateNewApp = AccessCondition_MifareDesfireAppCreation.ChangeKeyUsingMK,
                        SelectedDesfireAppKeyEncryptionTypeCurrent = DESFireKeyType.DF_KEY_AES,
                        SelectedDesfireAppKeyEncryptionTypeTarget = DESFireKeyType.DF_KEY_AES,
                        SelectedDesfireAppKeyNumberCurrent = "0",
                        DesfireAppKeyCurrent = "00000000000000000000000000000000",
                        DesfireAppKeyTarget = "11111111111111111111111111111111",
                        SelectedDesfireAppKeyVersionTarget = "01"
                    };

                    await viewModel.ChangeAppKeyCommand.ExecuteAsync(null);

                    Assert.Equal(1, fakeProvider.ChangeKeyCalls);
                    Assert.Equal(2, fakeProvider.AuthCalls);
                    Assert.Equal("11111111111111111111111111111111", fakeProvider.LastAuthKey);
                    Assert.Equal(DESFireKeyType.DF_KEY_AES, fakeProvider.LastAuthKeyType);
                    Assert.Equal(0, fakeProvider.LastAuthKeyNumber);
                    Assert.Equal(expectedSuccess ? ERROR.NoError : postChangeAuthenticationResult, viewModel.CurrentTaskErrorLevel);
                    Assert.Contains(expectedSuccess ? "Verified changed key" : "Unable to verify changed key", viewModel.StatusText);
                }
                finally
                {
                    SetReaderDeviceInstance(originalInstance);
                    ReaderDevice.Reader = originalReader;
                }
            });
        }

        [Fact]
        public async Task ChangeAppKeyCommand_WhenCommandReportsAuthFailureButNewKeyAuthenticates_Succeeds()
        {
            await RunOnStaThreadAsync(async () =>
            {
                var fakeProvider = new FakeElatecNetProvider
                {
                    ChangeKeyResult = ERROR.AuthFailure,
                    PostChangeAuthenticationResult = ERROR.NoError
                };
                var originalInstance = GetReaderDeviceInstance();
                var originalReader = ReaderDevice.Reader;
                try
                {
                    ReaderDevice.Reader = ReaderTypes.Elatec;
                    SetReaderDeviceInstance(fakeProvider);
                    var viewModel = new MifareDesfireSetupViewModel
                    {
                        SelectedTaskType = TaskType_MifareDesfireTask.ApplicationKeyChangeover,
                        AppNumberCurrent = "0xF482D0",
                        AppNumberTarget = "0xF482D0",
                        SelectedDesfireAppKeySettingsCreateNewApp = AccessCondition_MifareDesfireAppCreation.ChangeKeyUsingMK,
                        SelectedDesfireAppKeyEncryptionTypeCurrent = DESFireKeyType.DF_KEY_AES,
                        SelectedDesfireAppKeyEncryptionTypeTarget = DESFireKeyType.DF_KEY_AES,
                        SelectedDesfireAppKeyNumberCurrent = "1",
                        DesfireAppKeyCurrent = "00000000000000000000000000000000",
                        DesfireAppKeyCurrentOld = "00000000000000000000000000000000",
                        DesfireAppKeyTarget = "11111111111111111111111111111111",
                        SelectedDesfireAppKeyVersionTarget = "01"
                    };

                    await viewModel.ChangeAppKeyCommand.ExecuteAsync(null);

                    Assert.Equal(1, fakeProvider.ChangeKeyCalls);
                    Assert.Equal(2, fakeProvider.AuthCalls);
                    Assert.Equal(ERROR.NoError, viewModel.CurrentTaskErrorLevel);
                    Assert.Contains("Verified changed key", viewModel.StatusText);
                }
                finally
                {
                    SetReaderDeviceInstance(originalInstance);
                    ReaderDevice.Reader = originalReader;
                }
            });
        }

        [Theory]
        [InlineData(DESFireKeySettings.ChangeKeyWithMasterKey, false)]
        [InlineData(DESFireKeySettings.ChangeKeyWithTargetedKeyNumber, false)]
        [InlineData(DESFireKeySettings.ChangeKeyFrozen, true)]
        [InlineData(DESFireKeySettings.ChangeKeyFrozen | DESFireKeySettings.ConfigurationChangeable, true)]
        public async Task IsChangeKeyFrozen_RecognizesFrozenPolicy(
            DESFireKeySettings currentKeySettings,
            bool expected)
        {
            await RunOnStaThreadAsync(() =>
            {
                Assert.Equal(expected, MifareDesfireSetupViewModel.IsChangeKeyFrozen(currentKeySettings));
            });
        }
        private readonly ITestOutputHelper _output;
        public MifareDesfireSetupViewModelTests(ITestOutputHelper output) => _output = output;
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
                _output.WriteLine(line);
                Assert.Contains("AppID 1", line);
                Assert.Contains("KeyNo 2", line);
                Assert.Contains(nameof(DESFireKeySettings.ChangeKeyFrozen), line);
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
        public async Task ReadDataCommand_UsesProviderOwnedAuthenticationAndReadKey()
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

                    Assert.Null(fakeProvider.LastAuthKey);
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
        public async Task WriteDataCommand_UsesProviderOwnedAuthenticationAndWriteKey()
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

                    Assert.Null(fakeProvider.LastAuthKey);
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

        [Fact]
        public async Task CreateFileCommand_UsesProviderOwnedAuthentication()
        {
            await RunOnStaThreadAsync(async () =>
            {
                var fakeProvider = new FakeElatecNetProvider();
                var viewModel = new MifareDesfireSetupViewModel
                {
                    AppNumberCurrent = "1",
                    AppNumberNew = "1",
                    FileNumberCurrent = "0",
                    FileSizeCurrent = "16",
                    DesfireAppKeyCurrent = "00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00",
                    SelectedDesfireAppKeyEncryptionTypeCurrent = DESFireKeyType.DF_KEY_AES
                };

                var originalReader = ReaderDevice.Reader;
                var originalInstance = GetReaderDeviceInstance();
                try
                {
                    ReaderDevice.Reader = ReaderTypes.Elatec;
                    SetReaderDeviceInstance(fakeProvider);

                    await viewModel.CreateFileCommand.ExecuteAsync(null);

                    Assert.Null(fakeProvider.LastAuthKey);
                    Assert.Equal(viewModel.DesfireAppKeyCurrent, fakeProvider.LastCreateFileKey);
                    Assert.Equal(1, fakeProvider.CreateFileCalls);
                }
                finally
                {
                    ReaderDevice.Reader = originalReader;
                    SetReaderDeviceInstance(originalInstance);
                }
            });
        }

        [Fact]
        public async Task CreateApplicationCommand_UsesProviderOwnedAuthentication()
        {
            await RunOnStaThreadAsync(async () =>
            {
                var fakeProvider = new FakeElatecNetProvider();
                var viewModel = new MifareDesfireSetupViewModel
                {
                    AppNumberNew = "1",
                    SelectedDesfireMasterKeyEncryptionTypeCurrent = DESFireKeyType.DF_KEY_AES,
                    DesfireMasterKeyCurrent = "00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00"
                };

                var originalReader = ReaderDevice.Reader;
                var originalInstance = GetReaderDeviceInstance();
                try
                {
                    ReaderDevice.Reader = ReaderTypes.Elatec;
                    SetReaderDeviceInstance(fakeProvider);

                    await viewModel.CreateAppCommand.ExecuteAsync(null);

                    Assert.Null(fakeProvider.LastAuthKey);
                    Assert.Equal(viewModel.DesfireMasterKeyCurrent, fakeProvider.LastCreateApplicationKey);
                    Assert.Equal(1, fakeProvider.CreateApplicationCalls);
                }
                finally
                {
                    ReaderDevice.Reader = originalReader;
                    SetReaderDeviceInstance(originalInstance);
                }
            });
        }

        [Theory]
        [InlineData(0, 16, 3, 0x10, 0x32, true)]
        [InlineData(1, 16, 3, 0x10, 0x32, false)]
        [InlineData(0, 15, 3, 0x10, 0x32, false)]
        [InlineData(0, 16, 1, 0x10, 0x32, false)]
        [InlineData(0, 16, 3, 0x11, 0x32, false)]
        [InlineData(0, 16, 3, 0x10, 0x22, false)]
        public async Task CheckFileSettingsCommand_RequiresCompleteMatch(
            int actualFileType,
            int actualFileSize,
            int actualCommunicationMode,
            int actualAccessRights0,
            int actualAccessRights1,
            bool expectedSuccess)
        {
            await RunOnStaThreadAsync(async () =>
            {
                var fakeProvider = new FakeElatecNetProvider
                {
                    FileSettingsToReturn = new DESFireFileSettings
                    {
                        FileType = (byte)actualFileType,
                        comSett = (byte)actualCommunicationMode,
                        accessRights = new[] { (byte)actualAccessRights0, (byte)actualAccessRights1 },
                        dataFile = new DataFileSetting { fileSize = (uint)actualFileSize }
                    }
                };
                var viewModel = CreateFileSettingsCheckViewModel();
                var originalReader = ReaderDevice.Reader;
                var originalInstance = GetReaderDeviceInstance();
                try
                {
                    ReaderDevice.Reader = ReaderTypes.Elatec;
                    SetReaderDeviceInstance(fakeProvider);

                    await viewModel.CheckFileSettingsCommand();

                    Assert.Equal(1, fakeProvider.AuthCalls);
                    Assert.Equal(1, fakeProvider.GetFileSettingsCalls);
                    Assert.Equal(expectedSuccess ? ERROR.NoError : ERROR.IsNotTrue, viewModel.CurrentTaskErrorLevel);
                    Assert.Equal(expectedSuccess, viewModel.LastFileSettingsComparison.Matches);
                    Assert.Contains(
                        "→ " + ResourceLoader.GetResource(expectedSuccess ? "textBoxStatusComparePass" : "textBoxStatusCompareFail") + "\n",
                        viewModel.StatusText);
                }
                finally
                {
                    ReaderDevice.Reader = originalReader;
                    SetReaderDeviceInstance(originalInstance);
                }
            });
        }

        [Fact]
        public async Task CheckFileSettingsCommand_StopsWhenAuthenticationFails()
        {
            await RunOnStaThreadAsync(async () =>
            {
                var fakeProvider = new FakeElatecNetProvider { AuthenticationResult = ERROR.AuthFailure };
                var viewModel = CreateFileSettingsCheckViewModel();
                var originalReader = ReaderDevice.Reader;
                var originalInstance = GetReaderDeviceInstance();
                try
                {
                    ReaderDevice.Reader = ReaderTypes.Elatec;
                    SetReaderDeviceInstance(fakeProvider);

                    await viewModel.CheckFileSettingsCommand();

                    Assert.Equal(ERROR.AuthFailure, viewModel.CurrentTaskErrorLevel);
                    Assert.Equal(1, fakeProvider.AuthCalls);
                    Assert.Equal(0, fakeProvider.GetFileSettingsCalls);
                    Assert.Null(viewModel.LastFileSettingsComparison);
                }
                finally
                {
                    ReaderDevice.Reader = originalReader;
                    SetReaderDeviceInstance(originalInstance);
                }
            });
        }

        private static MifareDesfireSetupViewModel CreateFileSettingsCheckViewModel()
        {
            return new MifareDesfireSetupViewModel
            {
                SelectedTaskType = TaskType_MifareDesfireTask.CheckFileSettings,
                AppNumberCurrent = "1",
                FileNumberCurrent = "2",
                FileSizeCurrent = "16",
                DesfireAppKeyCurrent = "00000000000000000000000000000000",
                SelectedDesfireAppKeyEncryptionTypeCurrent = DESFireKeyType.DF_KEY_AES,
                SelectedDesfireAppKeyNumberCurrent = "0",
                SelectedDesfireFileType = FileType_MifareDesfireFileType.StdDataFile,
                SelectedDesfireFileCryptoMode = EncryptionMode.CM_ENCRYPT,
                SelectedDesfireFileAccessRightRead = TaskAccessRights.AR_KEY0,
                SelectedDesfireFileAccessRightWrite = TaskAccessRights.AR_KEY1,
                SelectedDesfireFileAccessRightReadWrite = TaskAccessRights.AR_KEY2,
                SelectedDesfireFileAccessRightChange = TaskAccessRights.AR_KEY3
            };
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

        [Fact]
        public async Task DeleteApplicationCommand_WhenAuthMethodIsApplicationKey_UsesAppMasterKey()
        {
            await RunOnStaThreadAsync(async () =>
            {
                var fakeProvider = new FakeElatecNetProvider();
                var viewModel = new MifareDesfireSetupViewModel
                {
                    AppNumberNew = "1",
                    DesfireMasterKeyCurrent = "11 11 11 11 11 11 11 11 11 11 11 11 11 11 11 11",
                    SelectedDesfireMasterKeyEncryptionTypeCurrent = DESFireKeyType.DF_KEY_3K3DES,
                    DesfireAppKeyCurrent = "22 22 22 22 22 22 22 22 22 22 22 22 22 22 22 22",
                    SelectedDesfireAppKeyEncryptionTypeCurrent = DESFireKeyType.DF_KEY_AES,
                    SelectedDesfireDeleteAuthMethod = DesfireDeleteAuthMethod.ApplicationMasterKey0
                };

                var originalReader = ReaderDevice.Reader;
                var originalInstance = GetReaderDeviceInstance();
                try
                {
                    ReaderDevice.Reader = ReaderTypes.Elatec;
                    SetReaderDeviceInstance(fakeProvider);

                    await viewModel.DeleteSignleCardApplicationCommand.ExecuteAsync(null);

                    Assert.Null(fakeProvider.LastAuthKey);
                    Assert.Equal(viewModel.DesfireAppKeyCurrent, fakeProvider.LastDeleteApplicationKey);
                    Assert.Equal(DESFireKeyType.DF_KEY_AES, fakeProvider.LastDeleteApplicationKeyType);
                    Assert.Equal((uint)1, fakeProvider.LastDeletedApplicationId);
                    Assert.Equal(1, fakeProvider.DeleteApplicationCalls);
                }
                finally
                {
                    ReaderDevice.Reader = originalReader;
                    SetReaderDeviceInstance(originalInstance);
                }
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
            public ERROR PostChangeAuthenticationResult { get; set; } = ERROR.NoError;
            public ERROR ChangeKeyResult { get; set; } = ERROR.NoError;
            public ERROR AuthenticationResult { get; set; } = ERROR.NoError;
            public int AuthCalls { get; private set; }
            public int ChangeKeyCalls { get; private set; }
            public int GetFileSettingsCalls { get; private set; }
            public DESFireFileSettings? FileSettingsToReturn { get; set; }
            public string LastAuthKey { get; private set; }
            public DESFireKeyType LastAuthKeyType { get; private set; }
            public int LastAuthKeyNumber { get; private set; }
            public string LastReadKey { get; private set; }
            public DESFireKeyType LastReadKeyType { get; private set; }
            public int LastReadKeyNumber { get; private set; }
            public string LastWriteKey { get; private set; }
            public DESFireKeyType LastWriteKeyType { get; private set; }
            public int LastWriteKeyNumber { get; private set; }
            public string LastCreateFileKey { get; private set; }
            public int CreateFileCalls { get; private set; }
            public string LastCreateApplicationKey { get; private set; }
            public int CreateApplicationCalls { get; private set; }
            public string LastDeleteApplicationKey { get; private set; }
            public DESFireKeyType LastDeleteApplicationKeyType { get; private set; }
            public uint LastDeletedApplicationId { get; private set; }
            public int DeleteApplicationCalls { get; private set; }

            public override Task<ERROR> AuthToMifareDesfireApplication(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumber, int _appID = 0)
            {
                AuthCalls++;
                LastAuthKey = _applicationMasterKey;
                LastAuthKeyType = _keyType;
                LastAuthKeyNumber = _keyNumber;
                return Task.FromResult(ChangeKeyCalls > 0 ? PostChangeAuthenticationResult : AuthenticationResult);
            }

            public override Task<ERROR> VerifyMifareDesfireKeyChange(
                string applicationKey,
                DESFireKeyType keyType,
                int keyNumber,
                int appId = 0) =>
                AuthToMifareDesfireApplication(applicationKey, keyType, keyNumber, appId);

            public override Task<ERROR> ChangeMifareDesfireKeyAsync(
                uint appId,
                byte targetKeyNo,
                DESFireKeyType targetKeyType,
                string currentTargetKeyHex,
                string newTargetKeyHex,
                byte newTargetKeyVersion,
                string masterKeyHex,
                DESFireKeyType masterKeyType,
                DESFireKeySettings currentKeySettings)
            {
                _ = appId;
                _ = targetKeyNo;
                _ = targetKeyType;
                _ = currentTargetKeyHex;
                _ = newTargetKeyHex;
                _ = newTargetKeyVersion;
                _ = masterKeyHex;
                _ = masterKeyType;
                _ = currentKeySettings;
                ChangeKeyCalls++;
                return Task.FromResult(ChangeKeyResult);
            }

            public override Task<ERROR> GetMifareDesfireFileSettings(
                string _applicationMasterKey,
                DESFireKeyType _keyType,
                int _keyNumberCurrent,
                int _appID,
                int _fileNo)
            {
                _ = _applicationMasterKey;
                _ = _keyType;
                _ = _keyNumberCurrent;
                _ = _appID;
                _ = _fileNo;
                GetFileSettingsCalls++;
                DesfireFileSettings = FileSettingsToReturn;
                return Task.FromResult(FileSettingsToReturn == null ? ERROR.TransportError : ERROR.NoError);
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

            public override Task<ERROR> CreateMifareDesfireFile(string _appMasterKey, DESFireKeyType _keyTypeAppMasterKey,
                FileType_MifareDesfireFileType _fileType, DESFireAccessRights _accessRights, EncryptionMode _encMode,
                int _appID, int _fileNo, int _fileSize, int _minValue = 0, int _maxValue = 1000,
                int _initValue = 0, bool _isValueLimited = false, int _maxNbOfRecords = 100)
            {
                _ = _keyTypeAppMasterKey;
                _ = _fileType;
                _ = _accessRights;
                _ = _encMode;
                _ = _appID;
                _ = _fileNo;
                _ = _fileSize;
                _ = _minValue;
                _ = _maxValue;
                _ = _initValue;
                _ = _isValueLimited;
                _ = _maxNbOfRecords;
                LastCreateFileKey = _appMasterKey;
                CreateFileCalls++;
                return Task.FromResult(ERROR.NoError);
            }

            public override Task<OperationResult> CreateMifareDesfireApplication(string _piccMasterKey,
                DESFireKeySettings _keySettingsTarget, DESFireKeyType _keyTypePiccMasterKey,
                DESFireKeyType _keyTypeTargetApplication, int _maxNbKeys, int _appID,
                bool authenticateToPICCFirst = true)
            {
                _ = _keySettingsTarget;
                _ = _keyTypePiccMasterKey;
                _ = _keyTypeTargetApplication;
                _ = _maxNbKeys;
                _ = _appID;
                LastCreateApplicationKey = _piccMasterKey;
                CreateApplicationCalls++;
                return Task.FromResult(OperationResult.Success(
                    operation: nameof(CreateMifareDesfireApplication),
                    wasAuthenticated: authenticateToPICCFirst));
            }

            public override Task<ERROR> DeleteMifareDesfireApplication(string applicationMasterKey,
                DESFireKeyType keyType, uint appID, DesfireDeleteAuthMethod authMethod = DesfireDeleteAuthMethod.PiccMasterKey)
            {
                LastDeleteApplicationKey = applicationMasterKey;
                LastDeleteApplicationKeyType = keyType;
                LastDeletedApplicationId = appID;
                DeleteApplicationCalls++;
                return Task.FromResult(ERROR.NoError);
            }
        }

        [Theory]
        [InlineData(0, DESFireKeySettings.ChangeKeyWithMasterKey, 5, 0)]
        [InlineData(1, DESFireKeySettings.ChangeKeyWithMasterKey, 5, 0)]
        [InlineData(1, DESFireKeySettings.ChangeKeyWithTargetedKeyNumber, 5, 5)]
        [InlineData(1, DESFireKeySettings.ChangeKeyWithTargetedKeyNumber | DESFireKeySettings.ConfigurationChangeable, 5, 5)]
        [InlineData(1, DESFireKeySettings.ChangeKeyFrozen, 5, 0)]
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
