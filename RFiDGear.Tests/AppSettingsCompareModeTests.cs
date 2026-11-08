using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.AccessControl;
using RFiDGear.Infrastructure.Tasks;
using RFiDGear.UI.MVVMDialogs.ViewModels.Interfaces;
using RFiDGear.ViewModel.TaskSetupViewModels;
using System;
using System.Collections.ObjectModel;
using System.IO;
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
        public async Task AProjectSavedWithTheYesNoFlag_StillLoads(string? legacyValue, DesfireAppSettingsCompareMode expected)
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
                // Start from another mode, so that choosing the default one is a change too.
                var viewModel = new MifareDesfireSetupViewModel
                {
                    AppSettingsCompareMode = mode == DesfireAppSettingsCompareMode.Exact
                        ? DesfireAppSettingsCompareMode.AllSelected
                        : DesfireAppSettingsCompareMode.Exact
                };
                var property = typeof(MifareDesfireSetupViewModel).GetProperty(radio)!;

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

                // The text follows the language, so it is compared with the resource and not with a literal. It is
                // anchored at the arrow and the end of the line: in German "BESTANDEN" is part of "NICHT BESTANDEN".
                Assert.Contains(
                    "→ " + ResourceLoader.GetResource(expected == ERROR.NoError ? "textBoxStatusComparePass" : "textBoxStatusCompareFail") + "\n",
                    viewModel.StatusText);
            });
        }

        [Fact]
        public async Task TheCommand_PassesOnTheErrorOfAReadThatFailed()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var viewModel = CreateCheckAppConfigViewModel(DesfireAppSettingsCompareMode.AllSelected, 0x0B);
                var fakeProvider = new FakeAppSettingsProvider { KeySettings = 0x0F, Result = ERROR.TransportError };

                using (ReaderDeviceScope.Use(fakeProvider))
                {
                    await viewModel.ReadAppSettingsCommand();
                }

                // What the card said is not compared when the read failed: the error is the result.
                Assert.Equal(ERROR.TransportError, viewModel.CurrentTaskErrorLevel);
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

                var reason = ResourceLoader.GetResource("textBoxStatusNoKeySettingFlagSelected");
                Assert.False(string.IsNullOrWhiteSpace(reason));
                Assert.Equal(ERROR.ProtocolConstraint, viewModel.CurrentTaskErrorLevel);
                Assert.Equal(0, fakeProvider.AppSettingsCalls);
                Assert.Contains(reason, viewModel.StatusText);
                Assert.False(viewModel.IsTaskCompletedSuccessfully);
            });
        }

        // The upper nibble is the change-key policy: 0x00 master key, 0xE0 same key, 0xF0 frozen.
        [Theory]
        [InlineData(AccessCondition_MifareDesfireAppCreation.ChangeKeyUsingKeyNo, "1", 0x0B, 0xEB, ERROR.NoError)]
        [InlineData(AccessCondition_MifareDesfireAppCreation.ChangeKeyUsingKeyNo, "1", 0x0B, 0x0B, ERROR.IsNotTrue)]
        [InlineData(AccessCondition_MifareDesfireAppCreation.ChangeKeyFrozen, "1", 0x0B, 0xFB, ERROR.NoError)]
        [InlineData(AccessCondition_MifareDesfireAppCreation.ChangeKeyFrozen, "1", 0x0B, 0xEB, ERROR.IsNotTrue)]
        [InlineData(AccessCondition_MifareDesfireAppCreation.ChangeKeyUsingMK, "1", 0x0B, 0x0B, ERROR.NoError)]
        [InlineData(AccessCondition_MifareDesfireAppCreation.ChangeKeyUsingKeyNo, "0", 0x0B, 0x0B, ERROR.NoError)]
        public async Task TheExactComparison_IncludesTheChangeKeyPolicy(
            AccessCondition_MifareDesfireAppCreation policy,
            string appNumber,
            int tickedFlags,
            int actualKeySettings,
            ERROR expected)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                // The PICC (application 0) has no change-key policy, so what is chosen is not part of its comparison.
                var viewModel = CreateCheckAppConfigViewModel(DesfireAppSettingsCompareMode.Exact, tickedFlags, policy, appNumber);
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
        public async Task AllAndAny_LeaveThePolicyOut(DesfireAppSettingsCompareMode mode)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                // The policy that is chosen (frozen, 0xF0) is not the one the card has (same key, 0xE0), and the two
                // are not subsets of each other; neither mode looks at it.
                var viewModel = CreateCheckAppConfigViewModel(mode, 0x0B, AccessCondition_MifareDesfireAppCreation.ChangeKeyFrozen);
                var fakeProvider = new FakeAppSettingsProvider { KeySettings = 0xEB };

                using (ReaderDeviceScope.Use(fakeProvider))
                {
                    await viewModel.ReadAppSettingsCommand();
                }

                Assert.Equal(ERROR.NoError, viewModel.CurrentTaskErrorLevel);
            });
        }

        [Theory]
        [InlineData(DesfireAppSettingsCompareMode.AllSelected)]
        [InlineData(DesfireAppSettingsCompareMode.AnySelected)]
        public async Task AllAndAny_DoNotReportAPolicyThatIsNotShownAnyMore(DesfireAppSettingsCompareMode mode)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                // A policy was chosen for the exact comparison and the mode was changed back: the combo box is gone, so
                // the byte in the status line must not carry the policy the user cannot see.
                var viewModel = CreateCheckAppConfigViewModel(mode, 0x0B, AccessCondition_MifareDesfireAppCreation.ChangeKeyFrozen);
                var fakeProvider = new FakeAppSettingsProvider { KeySettings = 0x0B };

                using (ReaderDeviceScope.Use(fakeProvider))
                {
                    await viewModel.ReadAppSettingsCommand();
                }

                Assert.Contains("0x0B", viewModel.StatusText);
                Assert.DoesNotContain("0xFB", viewModel.StatusText);
            });
        }

        [Theory]
        [InlineData(TaskType_MifareDesfireTask.ReadAppSettings)]
        [InlineData(TaskType_MifareDesfireTask.CheckAppKeyCount)]
        public async Task AnApplicationIdMarkedInvalid_IsNotReadAsApplicationZero(TaskType_MifareDesfireTask taskType)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                // An id that cannot be parsed becomes 0, the PICC, which can be read and would match.
                var viewModel = CreateCheckAppConfigViewModel(DesfireAppSettingsCompareMode.AllSelected, 0x0B, appNumber: "abc");
                viewModel.SelectedTaskType = taskType;
                var fakeProvider = new FakeAppSettingsProvider { KeySettings = 0x0F, MaxNumberOfKeys = 1 };
                Assert.False(viewModel.IsValidAppNumberCurrent);

                using (ReaderDeviceScope.Use(fakeProvider))
                {
                    if (taskType == TaskType_MifareDesfireTask.ReadAppSettings)
                    {
                        await viewModel.ReadAppSettingsCommand();
                    }
                    else
                    {
                        await viewModel.CheckAppKeyCountCommand();
                    }
                }

                Assert.Equal(ERROR.ProtocolConstraint, viewModel.CurrentTaskErrorLevel);
                Assert.Equal(0, fakeProvider.AppSettingsCalls);
                Assert.False(viewModel.IsTaskCompletedSuccessfully);
            });
        }

        private static MifareDesfireSetupViewModel CreateCheckAppConfigViewModel(
            DesfireAppSettingsCompareMode mode,
            int tickedFlags,
            AccessCondition_MifareDesfireAppCreation policy = AccessCondition_MifareDesfireAppCreation.ChangeKeyUsingMK,
            string appNumber = "1")
        {
            return new MifareDesfireSetupViewModel
            {
                SelectedTaskType = TaskType_MifareDesfireTask.ReadAppSettings,
                AppSettingsCompareMode = mode,
                AppNumberCurrent = appNumber,
                DesfireAppKeyCurrent = "00000000000000000000000000000000",
                SelectedDesfireAppKeyEncryptionTypeCurrent = DESFireKeyType.DF_KEY_AES,
                SelectedDesfireAppKeyNumberCurrent = "0",
                SelectedDesfireAppKeySettingsCreateNewApp = policy,
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

            return (MifareDesfireSetupViewModel)new XmlSerializer(typeof(MifareDesfireSetupViewModel)).Deserialize(reader)!;
        }
    }
}
