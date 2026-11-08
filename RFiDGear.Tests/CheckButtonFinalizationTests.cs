using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.AccessControl;
using RFiDGear.Infrastructure.Tasks;
using RFiDGear.ViewModel.TaskSetupViewModels;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace RFiDGear.Tests
{
    /// <summary>
    /// A task is finished once per run: it records whether it succeeded and tells the dialog that the reader is free.
    /// The task loop prepares the task for that; a button of the task dialog has to do the same, or the second press
    /// leaves the result of the first.
    /// </summary>
    public class CheckButtonFinalizationTests
    {
        [Fact]
        public async Task CheckAppConfigButton_FinishesTheTaskOnEveryPress()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var viewModel = CreateCheckAppConfig();
                var statusUpdates = Observe(viewModel);
                var fakeProvider = new FakeAppSettingsProvider { KeySettings = 0x0F };

                using (ReaderDeviceScope.Use(fakeProvider))
                {
                    await viewModel.ReadAppSettingsRelayCommand.ExecuteAsync(null);
                    Assert.True(viewModel.IsTaskCompletedSuccessfully);
                    Assert.Equal(new[] { false }, statusUpdates);

                    // The card now differs; the second press must say so, once.
                    fakeProvider.KeySettings = 0x09;
                    await viewModel.ReadAppSettingsRelayCommand.ExecuteAsync(null);
                }

                Assert.False(viewModel.IsTaskCompletedSuccessfully);
                Assert.Equal(ERROR.IsNotTrue, viewModel.CurrentTaskErrorLevel);
                Assert.Equal(new[] { false, false }, statusUpdates);
            });
        }

        [Fact]
        public async Task CheckAppKeyCountButton_FinishesTheTaskOnEveryPress()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var viewModel = CreateCheckAppKeyCount();
                var statusUpdates = Observe(viewModel);
                var fakeProvider = new FakeAppSettingsProvider { MaxNumberOfKeys = 3 };

                using (ReaderDeviceScope.Use(fakeProvider))
                {
                    await viewModel.CheckAppKeyCountRelayCommand.ExecuteAsync(null);
                    Assert.True(viewModel.IsTaskCompletedSuccessfully);
                    Assert.Contains("→ " + ResourceLoader.GetResource("textBoxStatusComparePass") + "\n", viewModel.StatusText);

                    fakeProvider.MaxNumberOfKeys = 4;
                    await viewModel.CheckAppKeyCountRelayCommand.ExecuteAsync(null);
                }

                Assert.False(viewModel.IsTaskCompletedSuccessfully);
                Assert.Equal(new[] { false, false }, statusUpdates);
            });
        }

        [Theory]
        [InlineData(TaskType_MifareDesfireTask.ReadAppSettings)]
        [InlineData(TaskType_MifareDesfireTask.CheckAppKeyCount)]
        [InlineData(TaskType_MifareDesfireTask.CheckFileSettings)]
        public async Task AuthenticateButtonOfTheCheckTasks_UsesTheChosenKeyAndFinishesOnEveryPress(TaskType_MifareDesfireTask taskType)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                // The button used to pick its key from the change-key policy, which the panel has nothing to do with here,
                // so a chosen key 1 was tried as key 0.
                var viewModel = CreateConfigured(taskType);
                viewModel.SelectedDesfireAppKeyNumberCurrent = "1";
                var fakeProvider = new FakeAppSettingsProvider();

                using (ReaderDeviceScope.Use(fakeProvider))
                {
                    await viewModel.AuthenticateToCardApplicationCommand.ExecuteAsync(null);
                    Assert.Equal(1, fakeProvider.LastAuthKeyNumber);
                    Assert.True(viewModel.IsTaskCompletedSuccessfully);

                    fakeProvider.AuthenticationResult = ERROR.AuthFailure;
                    await viewModel.AuthenticateToCardApplicationCommand.ExecuteAsync(null);
                }

                Assert.Equal(2, fakeProvider.AuthCalls);
                Assert.False(viewModel.IsTaskCompletedSuccessfully);
            });
        }

        [Fact]
        public async Task AButtonThatThrows_StillFinishesTheTaskOnce()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var viewModel = CreateCheckAppConfig();
                var statusUpdates = Observe(viewModel);
                var fakeProvider = new FakeAppSettingsProvider { Throw = true };

                using (ReaderDeviceScope.Use(fakeProvider))
                {
                    await Assert.ThrowsAsync<InvalidOperationException>(() => viewModel.ReadAppSettingsRelayCommand.ExecuteAsync(null));
                }

                // The reader is released and the task does not count as done.
                Assert.Equal(new[] { false }, statusUpdates);
                Assert.False(viewModel.IsTaskCompletedSuccessfully);
            });
        }

        [Theory]
        [InlineData(DesfireAppSettingsCompareMode.AllSelected, DesfireAppSettingsCompareMode.Exact, 0x1B, ERROR.NoError)]
        [InlineData(DesfireAppSettingsCompareMode.Exact, DesfireAppSettingsCompareMode.AllSelected, 0x0B, ERROR.IsNotTrue)]
        public async Task ThePressIsDecidedByTheModeItWasMadeIn_EvenWhenTheModeChangesWhileTheCardAnswers(
            DesfireAppSettingsCompareMode modeOfThePress,
            DesfireAppSettingsCompareMode modeChangedTo,
            int cardKeySettings,
            ERROR expected)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                // The ticked flags are 0x0B and the policy that is chosen is frozen (0xF0): the card byte 0x1B satisfies
                // "all selected" but not "exact", and 0x0B the other way round, so the two modes cannot be mixed up.
                var viewModel = CreateCheckAppConfig();
                viewModel.SelectedDesfireAppKeySettingsCreateNewApp = AccessCondition_MifareDesfireAppCreation.ChangeKeyFrozen;
                viewModel.AppSettingsCompareMode = modeOfThePress;
                var fakeProvider = new FakeAppSettingsProvider
                {
                    KeySettings = (byte)cardKeySettings,
                    DuringSettingsRead = () => viewModel.AppSettingsCompareMode = modeChangedTo
                };

                using (ReaderDeviceScope.Use(fakeProvider))
                {
                    await viewModel.ReadAppSettingsRelayCommand.ExecuteAsync(null);
                }

                Assert.Equal(modeChangedTo, viewModel.AppSettingsCompareMode);
                Assert.Equal(expected, viewModel.CurrentTaskErrorLevel);
                Assert.Contains(ModeText(modeOfThePress), viewModel.StatusText);
                Assert.DoesNotContain(ModeText(modeChangedTo), viewModel.StatusText);
            });
        }

        [Fact]
        public async Task TheExpectedKeyCountOfThePress_IsTheOneItWasMadeWith()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var viewModel = CreateCheckAppKeyCount();
                var fakeProvider = new FakeAppSettingsProvider
                {
                    MaxNumberOfKeys = 3,
                    DuringSettingsRead = () => viewModel.SelectedDesfireAppMaxNumberOfKeys = "4"
                };

                using (ReaderDeviceScope.Use(fakeProvider))
                {
                    await viewModel.CheckAppKeyCountRelayCommand.ExecuteAsync(null);
                }

                Assert.Equal("4", viewModel.SelectedDesfireAppMaxNumberOfKeys);
                Assert.Equal(ERROR.NoError, viewModel.CurrentTaskErrorLevel);
                Assert.True(viewModel.IsTaskCompletedSuccessfully);
            });
        }

        [Fact]
        public async Task AnApplicationIdThatBecomesInvalidWhileTheCardAnswers_DoesNotTurnAMismatchIntoAPass()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                // The card differs from the ticked settings. The id was valid when the button was pressed.
                var viewModel = CreateCheckAppConfig();
                var fakeProvider = new FakeAppSettingsProvider
                {
                    KeySettings = 0x00,
                    DuringSettingsRead = () => viewModel.AppNumberCurrent = "abc"
                };

                using (ReaderDeviceScope.Use(fakeProvider))
                {
                    await viewModel.ReadAppSettingsRelayCommand.ExecuteAsync(null);
                }

                Assert.False(viewModel.IsValidAppNumberCurrent);
                Assert.Equal(ERROR.IsNotTrue, viewModel.CurrentTaskErrorLevel);
                Assert.False(viewModel.IsTaskCompletedSuccessfully);
            });
        }

        [Theory]
        [InlineData(TaskType_MifareDesfireTask.ReadAppSettings)]
        [InlineData(TaskType_MifareDesfireTask.CheckAppKeyCount)]
        [InlineData(TaskType_MifareDesfireTask.CheckFileSettings)]
        public async Task AuthenticateButtonOfTheCheckTasks_RefusesAnInvalidApplicationId_BeforeAskingTheReader(TaskType_MifareDesfireTask taskType)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                // An id that cannot be parsed becomes 0, the PICC, which the key authenticates to: not a pass for this task.
                var viewModel = CreateConfigured(taskType);
                viewModel.AppNumberCurrent = "abc";
                Assert.False(viewModel.IsValidAppNumberCurrent);
                var fakeProvider = new FakeAppSettingsProvider();

                using (ReaderDeviceScope.Use(fakeProvider))
                {
                    await viewModel.AuthenticateToCardApplicationCommand.ExecuteAsync(null);
                }

                Assert.Equal(0, fakeProvider.AuthCalls);
                Assert.Equal(ERROR.ProtocolConstraint, viewModel.CurrentTaskErrorLevel);
                Assert.False(viewModel.IsTaskCompletedSuccessfully);
            });
        }

        [Fact]
        public async Task AButtonPressedWhileAnotherOneIsWorking_IsIgnored_AndCannotFinishTheOthersTask()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var viewModel = CreateCheckAppConfig();
                var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var fakeProvider = new FakeAppSettingsProvider
                {
                    KeySettings = 0x0F,
                    AuthenticationResult = ERROR.AuthFailure,
                    AuthenticationGate = release.Task
                };

                using (ReaderDeviceScope.Use(fakeProvider))
                {
                    var authenticate = viewModel.AuthenticateToCardApplicationCommand.ExecuteAsync(null);
                    Assert.Equal(1, fakeProvider.AuthCalls);

                    // Pressed while the card is still busy with the authentication: the comparison would pass, and would
                    // have finished the task before the authentication had its say.
                    await viewModel.ReadAppSettingsRelayCommand.ExecuteAsync(null);
                    Assert.Equal(0, fakeProvider.AppSettingsCalls);

                    release.SetResult(true);
                    await authenticate;

                    Assert.Equal(ERROR.AuthFailure, viewModel.CurrentTaskErrorLevel);
                    Assert.False(viewModel.IsTaskCompletedSuccessfully);

                    // Nothing is held back afterwards.
                    fakeProvider.AuthenticationGate = null;
                    await viewModel.ReadAppSettingsRelayCommand.ExecuteAsync(null);
                }

                Assert.Equal(1, fakeProvider.AppSettingsCalls);
                Assert.True(viewModel.IsTaskCompletedSuccessfully);
            });
        }

        [Fact]
        public async Task AButtonThatThrows_DoesNotKeepTheOtherButtonsWaiting()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var viewModel = CreateCheckAppConfig();
                var fakeProvider = new FakeAppSettingsProvider { Throw = true, KeySettings = 0x0F };

                using (ReaderDeviceScope.Use(fakeProvider))
                {
                    await Assert.ThrowsAsync<InvalidOperationException>(() => viewModel.ReadAppSettingsRelayCommand.ExecuteAsync(null));

                    fakeProvider.Throw = false;
                    await viewModel.ReadAppSettingsRelayCommand.ExecuteAsync(null);
                }

                Assert.Equal(2, fakeProvider.AppSettingsCalls);
                Assert.True(viewModel.IsTaskCompletedSuccessfully);
            });
        }

        private static string ModeText(DesfireAppSettingsCompareMode mode)
        {
            return ResourceLoader.GetResource(mode switch
            {
                DesfireAppSettingsCompareMode.AnySelected => "textBoxStatusCompareModeAny",
                DesfireAppSettingsCompareMode.Exact => "textBoxStatusCompareModeExact",
                _ => "textBoxStatusCompareModeAll"
            });
        }

        private static List<bool> Observe(MifareDesfireSetupViewModel viewModel)
        {
            var statusUpdates = new List<bool>();
            viewModel.OnUpdateStatus = isBusy => statusUpdates.Add(isBusy);

            return statusUpdates;
        }

        private static MifareDesfireSetupViewModel CreateCheckAppConfig()
        {
            var viewModel = CreateConfigured(TaskType_MifareDesfireTask.ReadAppSettings);
            viewModel.AppSettingsCompareMode = DesfireAppSettingsCompareMode.AllSelected;
            viewModel.IsAllowChangeMKChecked = true;
            viewModel.IsAllowListingWithoutMKChecked = true;
            viewModel.IsAllowCreateDelWithoutMKChecked = false;
            viewModel.IsAllowConfigChangableChecked = true;

            return viewModel;
        }

        private static MifareDesfireSetupViewModel CreateCheckAppKeyCount()
        {
            var viewModel = CreateConfigured(TaskType_MifareDesfireTask.CheckAppKeyCount);
            viewModel.SelectedDesfireAppMaxNumberOfKeys = "3";

            return viewModel;
        }

        private static MifareDesfireSetupViewModel CreateConfigured(TaskType_MifareDesfireTask taskType)
        {
            return new MifareDesfireSetupViewModel
            {
                SelectedTaskType = taskType,
                AppNumberCurrent = "1",
                DesfireAppKeyCurrent = "00000000000000000000000000000000",
                SelectedDesfireAppKeyEncryptionTypeCurrent = DESFireKeyType.DF_KEY_AES,
                SelectedDesfireAppKeyNumberCurrent = "0"
            };
        }
    }
}
