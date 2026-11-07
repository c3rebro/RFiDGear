using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.AccessControl;
using RFiDGear.Infrastructure.Tasks;
using RFiDGear.UI.MVVMDialogs.ViewModels.Interfaces;
using RFiDGear.ViewModel.TaskSetupViewModels;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Xunit;

namespace RFiDGear.Tests
{
    /// <summary>
    /// How Check: App Config compares the key-settings byte: the mode is saved in project files by name, a project saved
    /// with the earlier yes/no flag still loads, and each mode decides as it says.
    /// </summary>
    public class AppSettingsCompareModeTests
    {
        private const string ModeElement = "AppSettingsCompareMode";
        private const string LegacyElement = "IsAppSettingsExactMatch";

        [Fact]
        public void TheModeNamesAreSavedInProjectFiles_SoTheyAreFixed()
        {
            Assert.Equal(
                new[] { "AllSelected", "AnySelected", "Exact" },
                Enum.GetNames(typeof(DesfireAppSettingsCompareMode)));
        }

        [Theory]
        [InlineData(DesfireAppSettingsCompareMode.AllSelected)]
        [InlineData(DesfireAppSettingsCompareMode.AnySelected)]
        [InlineData(DesfireAppSettingsCompareMode.Exact)]
        public async Task TheModeSurvivesSavingAndLoading_AndTheLegacyFlagIsNotWritten(DesfireAppSettingsCompareMode mode)
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var xml = Serialize(new MifareDesfireSetupViewModel { AppSettingsCompareMode = mode });

                Assert.Contains("<" + ModeElement + ">" + mode + "</" + ModeElement + ">", xml);
                Assert.DoesNotContain(LegacyElement, xml);
                Assert.Equal(mode, Deserialize(xml).AppSettingsCompareMode);
            });
        }

        [Theory]
        [InlineData("true", DesfireAppSettingsCompareMode.Exact)]
        [InlineData("false", DesfireAppSettingsCompareMode.AllSelected)]
        [InlineData(null, DesfireAppSettingsCompareMode.AllSelected)]
        public async Task AProjectSavedWithTheYesNoFlag_StillLoads(string legacyValue, DesfireAppSettingsCompareMode expected)
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                // What an earlier version wrote: the flag, and no mode.
                var xml = Serialize(new MifareDesfireSetupViewModel());
                var modeElement = "<" + ModeElement + ">" + DesfireAppSettingsCompareMode.AllSelected + "</" + ModeElement + ">";
                Assert.Contains(modeElement, xml);
                var legacyXml = xml.Replace(
                    modeElement,
                    legacyValue == null ? string.Empty : "<" + LegacyElement + ">" + legacyValue + "</" + LegacyElement + ">");

                Assert.Equal(expected, Deserialize(legacyXml).AppSettingsCompareMode);
            });
        }

        [Theory]
        [InlineData(DesfireAppSettingsCompareMode.AllSelected)]
        [InlineData(DesfireAppSettingsCompareMode.AnySelected)]
        [InlineData(DesfireAppSettingsCompareMode.Exact)]
        public async Task EditingATask_KeepsItsMode(DesfireAppSettingsCompareMode mode)
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                // The constructor copies every public property of the task being edited, the legacy flag among them,
                // in an order that must not matter.
                var source = new MifareDesfireSetupViewModel
                {
                    SelectedTaskType = TaskType_MifareDesfireTask.ReadAppSettings,
                    AppSettingsCompareMode = mode
                };

                var editor = new MifareDesfireSetupViewModel(source, new ObservableCollection<IDialogViewModel>());

                Assert.Equal(mode, editor.AppSettingsCompareMode);
            });
        }

        [Fact]
        public async Task TheLegacyFlagSetToFalse_DoesNotUndoAMode()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                // "false" meant "all selected", the default. It is set, among all the other properties, when a task is
                // copied or an old file is read, and it must not replace a mode chosen with the new property.
                var viewModel = new MifareDesfireSetupViewModel { AppSettingsCompareMode = DesfireAppSettingsCompareMode.AnySelected };

                viewModel.IsAppSettingsExactMatch = false;

                Assert.Equal(DesfireAppSettingsCompareMode.AnySelected, viewModel.AppSettingsCompareMode);
                Assert.False(viewModel.IsAppSettingsExactMatch);

                viewModel.IsAppSettingsExactMatch = true;

                Assert.Equal(DesfireAppSettingsCompareMode.Exact, viewModel.AppSettingsCompareMode);
            });
        }

        [Theory]
        [InlineData(DesfireAppSettingsCompareMode.AnySelected, nameof(MifareDesfireSetupViewModel.IsCompareModeAny))]
        [InlineData(DesfireAppSettingsCompareMode.Exact, nameof(MifareDesfireSetupViewModel.IsCompareModeExact))]
        [InlineData(DesfireAppSettingsCompareMode.AllSelected, nameof(MifareDesfireSetupViewModel.IsCompareModeAll))]
        public async Task ChoosingARadioButton_SetsTheModeAndLeavingItDoesNot(DesfireAppSettingsCompareMode mode, string radio)
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var viewModel = new MifareDesfireSetupViewModel();
                var property = typeof(MifareDesfireSetupViewModel).GetProperty(radio);

                property.SetValue(viewModel, true);
                Assert.Equal(mode, viewModel.AppSettingsCompareMode);

                // A radio button that is unchecked reports false; that must not change the mode picked by the other one.
                property.SetValue(viewModel, false);
                Assert.Equal(mode, viewModel.AppSettingsCompareMode);
            });
        }

        // The ticked flags are a bit mask: 0x01 allow change MK, 0x02 free listing, 0x04 free create/delete, 0x08 config.
        [Theory]
        [InlineData(DesfireAppSettingsCompareMode.AllSelected, 0x0B, 0x0F, ERROR.NoError)]
        [InlineData(DesfireAppSettingsCompareMode.AllSelected, 0x0B, 0x09, ERROR.IsNotTrue)]
        [InlineData(DesfireAppSettingsCompareMode.AnySelected, 0x0B, 0x01, ERROR.NoError)]
        [InlineData(DesfireAppSettingsCompareMode.AnySelected, 0x0B, 0x04, ERROR.IsNotTrue)]
        [InlineData(DesfireAppSettingsCompareMode.Exact, 0x0B, 0x0B, ERROR.NoError)]
        [InlineData(DesfireAppSettingsCompareMode.Exact, 0x0B, 0x0F, ERROR.IsNotTrue)]
        [InlineData(DesfireAppSettingsCompareMode.Exact, 0x00, 0x00, ERROR.NoError)]
        [InlineData(DesfireAppSettingsCompareMode.Exact, 0x00, 0x01, ERROR.IsNotTrue)]
        public async Task TheCommand_ComparesInTheChosenMode(
            DesfireAppSettingsCompareMode mode,
            int tickedFlags,
            int actualKeySettings,
            ERROR expected)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var viewModel = CreateCheckAppConfigViewModel(mode, tickedFlags);
                var fakeProvider = new FakeAppSettingsProvider { KeySettings = (byte)actualKeySettings };

                using (ReaderDeviceScope.Use(fakeProvider))
                {
                    await viewModel.ReadAppSettingsCommand();
                }

                Assert.Equal(expected, viewModel.CurrentTaskErrorLevel);
                Assert.Equal(1, fakeProvider.AppSettingsCalls);
            });
        }

        [Theory]
        [InlineData(DesfireAppSettingsCompareMode.AllSelected)]
        [InlineData(DesfireAppSettingsCompareMode.AnySelected)]
        public async Task TheCommand_RefusesToCompareNothing_WithoutAskingTheCard(DesfireAppSettingsCompareMode mode)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                // "All" of nothing would match every card and "any" of nothing none; neither says what was meant.
                var viewModel = CreateCheckAppConfigViewModel(mode, tickedFlags: 0x00);
                var fakeProvider = new FakeAppSettingsProvider { KeySettings = 0x0F };

                using (ReaderDeviceScope.Use(fakeProvider))
                {
                    await viewModel.ReadAppSettingsCommand();
                }

                Assert.Equal(ERROR.ProtocolConstraint, viewModel.CurrentTaskErrorLevel);
                Assert.Equal(0, fakeProvider.AppSettingsCalls);
                Assert.False(string.IsNullOrWhiteSpace(viewModel.StatusText));
            });
        }

        private static MifareDesfireSetupViewModel CreateCheckAppConfigViewModel(DesfireAppSettingsCompareMode mode, int tickedFlags)
        {
            return new MifareDesfireSetupViewModel
            {
                SelectedTaskType = TaskType_MifareDesfireTask.ReadAppSettings,
                AppSettingsCompareMode = mode,
                AppNumberCurrent = "1",
                DesfireAppKeyCurrent = "00000000000000000000000000000000",
                SelectedDesfireAppKeyEncryptionTypeCurrent = DESFireKeyType.DF_KEY_AES,
                SelectedDesfireAppKeyNumberCurrent = "0",
                SelectedDesfireAppKeySettingsCreateNewApp = AccessCondition_MifareDesfireAppCreation.ChangeKeyUsingMK,
                IsAllowChangeMKChecked = (tickedFlags & 0x01) != 0,
                IsAllowListingWithoutMKChecked = (tickedFlags & 0x02) != 0,
                IsAllowCreateDelWithoutMKChecked = (tickedFlags & 0x04) != 0,
                IsAllowConfigChangableChecked = (tickedFlags & 0x08) != 0
            };
        }

        private static string Serialize(MifareDesfireSetupViewModel viewModel)
        {
            using var writer = new StringWriter();
            new XmlSerializer(typeof(MifareDesfireSetupViewModel)).Serialize(writer, viewModel);

            return writer.ToString();
        }

        private static MifareDesfireSetupViewModel Deserialize(string xml)
        {
            using var reader = new StringReader(xml);

            return (MifareDesfireSetupViewModel)new XmlSerializer(typeof(MifareDesfireSetupViewModel)).Deserialize(reader);
        }
    }
}
