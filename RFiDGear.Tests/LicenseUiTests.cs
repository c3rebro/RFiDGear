using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.ExtensionHost;
using RFiDGear.Infrastructure.FileAccess;
using RFiDGear.Models;
using RFiDGear.Services;
using RFiDGear.Services.Interfaces;
using RFiDGear.Services.Licensing;
using RFiDGear.ViewModel;

using Xunit;

namespace RFiDGear.Tests
{
    /// <summary>
    /// The parts of the license that a person meets: the refusal of new runs, the dialog of the trial, and the status of the
    /// main window.
    /// </summary>
    [Collection("SettingsFileAccess")]
    public sealed class LicenseUiTests : IDisposable
    {
        private readonly List<string> _folders = new List<string>();

        public void Dispose()
        {
            foreach (var folder in _folders)
            {
                try
                {
                    if (Directory.Exists(folder))
                    {
                        Directory.Delete(folder, recursive: true);
                    }
                }
                catch (IOException)
                {
                    // A temporary folder that is still held is left for the system to clear; it must not fail a test.
                }
            }
        }

        [Fact]
        public void ABlockedCoordinator_RefusesNewExecutions_ButNeverARunThatIsInProgress()
        {
            var coordinator = new ExtensionOperationCoordinator();

            Assert.True(coordinator.TryBeginExecution());
            coordinator.ExecutionBlocked = true;
            Assert.True(coordinator.IsExecuting);

            coordinator.EndExecution();
            Assert.False(coordinator.TryBeginExecution());
            Assert.True(coordinator.IsIdle);

            coordinator.ExecutionBlocked = false;
            Assert.True(coordinator.TryBeginExecution());
            coordinator.EndExecution();
        }

        [Fact]
        public void TheDialogOfARunningTrial_ClosesWhenTheCloseButtonIsUsed()
        {
            var dialog = new LicenseDialogViewModel(trialHasEnded: false);
            var closed = 0;
            dialog.DialogClosing += (_, _) => closed++;

            dialog.CloseCommand.Execute(null);

            Assert.Equal(1, closed);
        }

        [Fact]
        public void TheDialogOfAnEndedTrial_IsLeftOnlyByBuyingOrByClosingTheProgram()
        {
            var exits = 0;
            var closed = 0;
            var dialog = new LicenseDialogViewModel(trialHasEnded: true) { OnExit = () => exits++ };
            dialog.DialogClosing += (_, _) => closed++;

            dialog.CloseCommand.Execute(null);
            dialog.RequestClose();

            Assert.Equal(2, exits);
            Assert.Equal(0, closed);
        }

        [Fact]
        public async Task ABuyThatGoesThrough_ClosesTheDialog()
        {
            var dialog = new LicenseDialogViewModel(trialHasEnded: true) { BuyAsync = () => Task.FromResult(true) };
            var closed = 0;
            dialog.DialogClosing += (_, _) => closed++;

            await dialog.BuyCommand.ExecuteAsync(null);

            Assert.Equal(1, closed);
            Assert.False(dialog.HasError);
            Assert.True(dialog.IsNotBusy);
        }

        [Fact]
        public async Task ABuyThatDoesNotGoThrough_KeepsTheDialogAndSaysSo()
        {
            var dialog = new LicenseDialogViewModel(trialHasEnded: true)
            {
                BuyAsync = () => Task.FromResult(false),
                PurchaseFailedText = "not completed",
            };
            var closed = 0;
            dialog.DialogClosing += (_, _) => closed++;

            await dialog.BuyCommand.ExecuteAsync(null);

            Assert.Equal(0, closed);
            Assert.True(dialog.HasError);
            Assert.Equal("not completed", dialog.ErrorText);
            Assert.True(dialog.IsNotBusy);
        }

        [Fact]
        public async Task TheWindow_WithATrial_ShowsTheDaysLeft_AndAnEditionWithoutALicense_ShowsNothing()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var trial = new FakeLicenseService(LicenseSnapshot.Trial(DateTimeOffset.UtcNow.AddDays(12).AddHours(1)));
                var trialWindow = await InitializedWindowAsync(trial);

                Assert.True(trialWindow.IsLicenseStatusVisible);
                Assert.Contains("13", trialWindow.LicenseStatusText);
                Assert.Empty(trialWindow.Dialogs);

                var plainWindow = await InitializedWindowAsync(new NullLicenseService());

                Assert.False(plainWindow.IsLicenseStatusVisible);
                Assert.Equal(string.Empty, plainWindow.LicenseStatusText);
            });
        }

        [Fact]
        public async Task TheWindow_WhenTheTrialHasEnded_RefusesRuns_AndShowsTheEndOfTheTrial()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var window = await InitializedWindowAsync(new FakeLicenseService(LicenseSnapshot.Expired));

                Assert.True(window.IsLicenseStatusVisible);

                var dialog = Assert.IsType<LicenseDialogViewModel>(Assert.Single(window.Dialogs));
                Assert.True(dialog.TrialHasEnded);

                Assert.False(await window.TryBeginExecutionAfterAScanAsync());
            });
        }

        // A main window that has started up with the given license service; its settings and project folders are the test's own.
        private async Task<MainWindowViewModel> InitializedWindowAsync(ILicenseService licenseService)
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "RFiDGear.Tests", Guid.NewGuid().ToString("N"));
            _folders.Add(tempRoot);
            var settingsDirectory = Path.Combine(tempRoot, "settings");
            Directory.CreateDirectory(settingsDirectory);

            using (var settings = new SettingsReaderWriter(settingsDirectory, loadSettings: false))
            {
                settings.DefaultSpecification = new DefaultSpecification(true);
                settings.SaveSettings(settings.DefaultSpecification, Path.Combine(settingsDirectory, "settings.xml"));
            }

            var bootstrapper = new DeferredSettingsBootstrapper();
            var viewModel = new MainWindowViewModel(
                bootstrapper,
                new NoopUpdateNotifier(),
                new FakeContextMenuBuilder(),
                new FakeAppStartupInitializer(),
                new FakeTimerFactory(),
                new FakeTaskServiceInitializer(),
                new FakeMenuInitializer(),
                new FakeStartupArgumentProcessor(),
                new NoopUpdateScheduler(),
                new FakeReaderMonitor(),
                new FakeProjectBootstrapper(),
                () => new SettingsReaderWriter(settingsDirectory),
                () => new DatabaseReaderWriter(new ProjectManager(Path.Combine(tempRoot, "appdata"))),
                licenseService: licenseService);

            var initializationTask = viewModel.InitializeAsync();
            bootstrapper.Complete(new SettingsBootstrapResult
            {
                CurrentReaderName = "Reader",
                DefaultReaderProvider = ReaderTypes.None,
                Culture = CultureInfo.InvariantCulture
            });
            await initializationTask;

            return viewModel;
        }
    }
}
