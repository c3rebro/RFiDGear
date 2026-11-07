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

                    fakeProvider.MaxNumberOfKeys = 4;
                    await viewModel.CheckAppKeyCountRelayCommand.ExecuteAsync(null);
                }

                Assert.False(viewModel.IsTaskCompletedSuccessfully);
                Assert.Equal(new[] { false, false }, statusUpdates);
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
