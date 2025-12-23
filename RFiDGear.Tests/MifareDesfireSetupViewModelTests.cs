using RFiDGear.Infrastructure.AccessControl;
using RFiDGear.Infrastructure.Tasks;
using RFiDGear.ViewModel.TaskSetupViewModels;
using System;
using System.Threading.Tasks;
using Xunit;

namespace RFiDGear.Tests
{
    public class MifareDesfireSetupViewModelTests
    {
        [Fact]
        public void Constructor_DisablesTabsByDefault()
        {
            var viewModel = new MifareDesfireSetupViewModel();

            Assert.False(viewModel.IsDesfireFileAuthoringTabEnabled);
            Assert.False(viewModel.IsDataExplorerEditTabEnabled);
            Assert.False(viewModel.IsDesfirePICCAuthoringTabEnabled);
            Assert.False(viewModel.IsDesfireAuthenticationTabEnabled);
            Assert.False(viewModel.IsDesfireAppAuthenticationTabEnabled);
            Assert.False(viewModel.IsDesfireAppAuthoringTabEnabled);
            Assert.False(viewModel.IsDesfireAppCreationTabEnabled);
        }

        [Theory]
        [InlineData(TaskType_MifareDesfireTask.ChangeDefault, true, true, true, true, true, true, true)]
        [InlineData(TaskType_MifareDesfireTask.ReadAppSettings, false, false, true, false, false, false, false)]
        [InlineData(TaskType_MifareDesfireTask.ApplicationKeyChangeover, false, false, false, false, true, true, false)]
        [InlineData(TaskType_MifareDesfireTask.ApplicationKeySettingsChangeover, false, false, false, false, true, true, false)]
        [InlineData(TaskType_MifareDesfireTask.AuthenticateApplication, false, false, false, true, true, true, false)]
        public void SelectedTaskType_SetsExpectedTabAvailability(
            TaskType_MifareDesfireTask taskType,
            bool fileAuthoring,
            bool dataExplorerEdit,
            bool desfirePiccAuthoring,
            bool desfireAuthentication,
            bool desfireAppAuthentication,
            bool desfireAppAuthoring,
            bool desfireAppCreation)
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
        }

        [Theory]
        [InlineData(TaskType_MifareDesfireTask.ApplicationKeyChangeover, true, false)]
        [InlineData(TaskType_MifareDesfireTask.ApplicationKeySettingsChangeover, false, true)]
        [InlineData(TaskType_MifareDesfireTask.ChangeDefault, false, false)]
        [InlineData(TaskType_MifareDesfireTask.ReadAppSettings, false, false)]
        [InlineData(TaskType_MifareDesfireTask.PICCMasterKeyChangeover, false, false)]
        [InlineData(TaskType_MifareDesfireTask.PICCMasterKeySettingsChangeover, false, false)]
        public void SelectedTaskType_TogglesKeyInputVisibility(
            TaskType_MifareDesfireTask taskType,
            bool showTarget,
            bool showSettings)
        {
            var viewModel = new MifareDesfireSetupViewModel
            {
                SelectedTaskType = taskType
            };

            Assert.Equal(showTarget, viewModel.ShowAppKeyTargetInputs);
            Assert.Equal(showSettings, viewModel.ShowAppKeySettingsInputs);
        }

        [Theory]
        [InlineData(TaskType_MifareDesfireTask.PICCMasterKeyChangeover, true, false)]
        [InlineData(TaskType_MifareDesfireTask.PICCMasterKeySettingsChangeover, false, true)]
        [InlineData(TaskType_MifareDesfireTask.ChangeDefault, false, false)]
        [InlineData(TaskType_MifareDesfireTask.ReadAppSettings, false, false)]
        [InlineData(TaskType_MifareDesfireTask.ApplicationKeyChangeover, false, false)]
        public void SelectedTaskType_TogglesPiccMasterKeyInputVisibility(
            TaskType_MifareDesfireTask taskType,
            bool showTarget,
            bool showSettings)
        {
            var viewModel = new MifareDesfireSetupViewModel
            {
                SelectedTaskType = taskType
            };

            Assert.Equal(showTarget, viewModel.ShowPiccMasterKeyTargetInputs);
            Assert.Equal(showSettings, viewModel.ShowPiccMasterKeySettingsInputs);
        }

        [Theory]
        [InlineData(TaskType_MifareDesfireTask.CreateApplication, false, true, false)]
        [InlineData(TaskType_MifareDesfireTask.DeleteApplication, false, false, true)]
        [InlineData(TaskType_MifareDesfireTask.PICCMasterKeyChangeover, true, false, false)]
        public void SelectedTaskType_TogglesPiccAndAppCreationVisibility(
            TaskType_MifareDesfireTask taskType,
            bool showPiccSection,
            bool showCreateInputs,
            bool showDeleteInputs)
        {
            var viewModel = new MifareDesfireSetupViewModel
            {
                SelectedTaskType = taskType
            };

            Assert.Equal(showPiccSection, viewModel.ShowPiccMasterKeyAuthoringSection);
            Assert.Equal(showCreateInputs, viewModel.ShowCreateApplicationInputs);
            Assert.Equal(showDeleteInputs, viewModel.ShowDeleteApplicationInputs);
        }

        [Theory]
        [InlineData("0x4bc", 0x4BC)]
        [InlineData("0x4BC", 0x4BC)]
        [InlineData("1212", 1212)]
        public void AppNumberNew_SupportsHexOrDecimalInput(string value, int expected)
        {
            var viewModel = new MifareDesfireSetupViewModel
            {
                AppNumberNew = value
            };

            Assert.True(viewModel.IsValidAppNumberNew);
            Assert.Equal(expected, viewModel.AppNumberNewAsInt);
        }

        [Theory]
        [InlineData("0x4bc", 0x4BC)]
        [InlineData("1212", 1212)]
        public void AppNumberCurrent_SupportsHexPrefixOrDecimalInput(string value, int expected)
        {
            var viewModel = new MifareDesfireSetupViewModel
            {
                AppNumberCurrent = value
            };

            Assert.True(viewModel.IsValidAppNumberCurrent);
            Assert.Equal(expected, viewModel.AppNumberCurrentAsInt);
        }

        [Theory]
        [InlineData("0x0A", "0A")]
        [InlineData("10", "0A")]
        public void KeyVersionCurrent_SupportsHexPrefixOrDecimalInput(string value, string expected)
        {
            var viewModel = new MifareDesfireSetupViewModel
            {
                KeyVersionCurrent = value
            };

            Assert.True(viewModel.IsValidKeyVersionCurrent);
            Assert.Equal(expected, viewModel.KeyVersionCurrent, ignoreCase: true);
        }

        [Theory]
        [InlineData("0x0B", "0B")]
        [InlineData("11", "0B")]
        public void SelectedDesfireAppKeyVersionTarget_SupportsHexPrefixOrDecimalInput(string value, string expected)
        {
            var viewModel = new MifareDesfireSetupViewModel
            {
                SelectedDesfireAppKeyVersionTarget = value
            };

            Assert.True(viewModel.IsValidDesfireAppKeyVersionTarget);
            Assert.Equal(expected, viewModel.SelectedDesfireAppKeyVersionTarget, ignoreCase: true);
        }

        [Theory]
        [InlineData("0x0C", 0x0C)]
        [InlineData("12", 12)]
        public void FileNumberCurrent_SupportsHexPrefixOrDecimalInput(string value, int expected)
        {
            var viewModel = new MifareDesfireSetupViewModel
            {
                FileNumberCurrent = value
            };

            Assert.True(viewModel.IsValidFileNumberCurrent);
            Assert.Equal(expected, viewModel.FileNumberCurrentAsInt);
        }

        [Theory]
        [InlineData("FF")]
        [InlineData("0x100")]
        public void FileNumberCurrent_InvalidValuesAreRejected(string value)
        {
            var viewModel = new MifareDesfireSetupViewModel
            {
                FileNumberCurrent = value
            };

            Assert.False(viewModel.IsValidFileNumberCurrent);
        }

        [Fact]
        public void BuildAppKeyChangePayload_UsesCurrentAuthKeyAsMasterKey()
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
        }

        [Theory]
        [InlineData("0")]
        [InlineData("-1")]
        public async Task ChangeAppKeyCommand_WhenAppIdNotPositive_SetsStatusAndStops(string appNumber)
        {
            var viewModel = new MifareDesfireSetupViewModel
            {
                AppNumberCurrent = appNumber,
                IsDesfireAppCreationTabEnabled = true
            };

            await viewModel.ChangeAppKeyCommand.ExecuteAsync(null);

            Assert.True(viewModel.IsDesfireAppCreationTabEnabled);
            Assert.Contains("PICC master key tab", viewModel.StatusText);
        }

        [Fact]
        public void GetPiccMasterKeyChangeSettings_UsesMinimalChangeKeyWithMasterKey()
        {
            var settings = MifareDesfireSetupViewModel.GetPiccMasterKeyChangeSettings();

            Assert.Equal(DESFireKeySettings.ChangeKeyWithMasterKey, settings);
        }

        [Fact]
        public void BuildChangeAppKeyAuthStatusLine_IncludesSelectedValues()
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
        }

        [Fact]
        public void BuildChangeKeyFrozenWarningLine_IndicatesFrozenPolicy()
        {
            var timestamp = new DateTime(2024, 2, 3, 4, 5, 6);

            var line = MifareDesfireSetupViewModel.BuildChangeKeyFrozenWarningLine(timestamp);

            Assert.Contains("Warning", line);
            Assert.Contains("ChangeKeyFrozen", line);
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
        public void ShowAppKeyOldInputs_ReflectsAppIdKeyAndPolicy(
            string appId,
            string keyNumber,
            AccessCondition_MifareDesfireAppCreation changeKeyPolicy,
            bool expected)
        {
            var viewModel = new MifareDesfireSetupViewModel
            {
                SelectedTaskType = TaskType_MifareDesfireTask.ApplicationKeyChangeover,
                AppNumberCurrent = appId,
                SelectedDesfireAppKeyNumberCurrent = keyNumber,
                SelectedDesfireAppKeySettingsCreateNewApp = changeKeyPolicy
            };

            Assert.Equal(expected, viewModel.ShowAppKeyOldInputs);
        }

        [Theory]
        [InlineData(0, DESFireKeySettings.ChangeKeyWithMasterKey, 5, 0)]
        [InlineData(1, DESFireKeySettings.ChangeKeyWithMasterKey, 5, 0)]
        [InlineData(1, DESFireKeySettings.ChangeKeyWithTargetedKeyNumber, 5, 5)]
        public void GetAuthKeyNumberForChangeAppKey_MatchesPolicy(
            int appId,
            DESFireKeySettings changeKeyMode,
            int appKeyNumber,
            int expectedAuthKeyNumber)
        {
            var authKeyNumber = MifareDesfireSetupViewModel.GetAuthKeyNumberForChangeAppKey(
                appId,
                changeKeyMode,
                appKeyNumber);

            Assert.Equal(expectedAuthKeyNumber, authKeyNumber);
        }
    }
}
