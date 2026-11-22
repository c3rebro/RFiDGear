using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using RFiDGear.Infrastructure.Editions;
using RFiDGear.Infrastructure.FileAccess;
using RFiDGear.Services;
using RFiDGear.ViewModel;

using Xunit;

namespace RFiDGear.Tests
{
    /// <summary>
    /// The Store edition runs from an MSIX package and is updated by the Store: it must not reach out to GitHub, write the
    /// manifest of the self-updater or offer the menu entries for it. The MSI edition, and any run without a package, stays as it
    /// was.
    /// </summary>
    public sealed class EditionTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "rfidgear-edition-" + Guid.NewGuid().ToString("N"));

        public EditionTests() => Directory.CreateDirectory(_folder);

        public void Dispose()
        {
            if (Directory.Exists(_folder))
            {
                Directory.Delete(_folder, recursive: true);
            }
        }

        [Theory]
        [InlineData(0, true)]
        [InlineData(AppEdition.ErrorInsufficientBuffer, true)]
        [InlineData(AppEdition.AppModelErrorNoPackage, false)]
        [InlineData(5, false)]
        public void ThePackageAnswer_IsReadAsPackagedOnlyForAPackage(int result, bool expected)
        {
            Assert.Equal(expected, AppEdition.InterpretPackageResult(result));
        }

        [Fact]
        public void AProcessWithoutAPackage_IsTheStandardEdition()
        {
            Assert.False(AppEdition.IsPackaged);
            Assert.Same(EditionPolicy.Standard, EditionPolicy.Current);
        }

        [Fact]
        public void TheStandardEdition_UpdatesItself_AndTheStoreEditionDoesNot()
        {
            Assert.True(EditionPolicy.Standard.AllowSelfUpdate);
            Assert.Equal(string.Empty, EditionPolicy.Standard.TitleSuffix);

            Assert.False(EditionPolicy.Store.AllowSelfUpdate);
            Assert.Equal("(Store)", EditionPolicy.Store.TitleSuffix);
        }

        [Fact]
        public void TheLocalUpdateManifest_IsWrittenOnlyByAnEditionThatUpdatesItself()
        {
            var standardFolder = Directory.CreateDirectory(Path.Combine(_folder, "standard")).FullName;
            var storeFolder = Directory.CreateDirectory(Path.Combine(_folder, "store")).FullName;

            using (var standard = new SettingsReaderWriter(standardFolder, loadSettings: false) { Edition = EditionPolicy.Standard })
            using (var store = new SettingsReaderWriter(storeFolder, loadSettings: false) { Edition = EditionPolicy.Store })
            {
                standard.InitUpdateFile();
                store.InitUpdateFile();
            }

            Assert.True(File.Exists(Path.Combine(standardFolder, "update.xml")));
            Assert.False(File.Exists(Path.Combine(storeFolder, "update.xml")));
        }

        [Fact]
        public async Task TheNoUpdateServices_NeverCallBack()
        {
            var called = 0;

            Task OnUpdate()
            {
                Interlocked.Increment(ref called);
                return Task.CompletedTask;
            }

            using var notifier = new NoUpdateNotifier();
            using var scheduler = new NoUpdateScheduler();

            notifier.StartUpdateCheck(OnUpdate);
            await notifier.TriggerUpdateCheckAsync(OnUpdate);
            scheduler.Begin(OnUpdate);

            Assert.Equal(0, called);
        }

        [Fact]
        public void TheStandardEdition_LoadsExtensionsFromOutside_AndTheStoreEditionOnlyFromItsPackage()
        {
            Assert.True(EditionPolicy.Standard.AllowExternalExtensions);
            Assert.False(EditionPolicy.Store.AllowExternalExtensions);
        }

        // The main window as the Store edition builds it: the services are stand-ins, the edition is the Store policy.
        private static MainWindowViewModel NewStoreWindow() => new MainWindowViewModel(
            new DeferredSettingsBootstrapper(),
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
            editionPolicy: EditionPolicy.Store);

        [Fact]
        public async Task TheWindowOfTheStoreEdition_HasNoUpdater_AndCheckingForUpdatesDoesNothing()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var viewModel = NewStoreWindow();

                var updater = typeof(MainWindowViewModel).GetField("updater", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(viewModel);

                Assert.Null(updater);
                Assert.False(viewModel.IsSelfUpdateAllowed);

                await viewModel.CheckUpdateCommand.ExecuteAsync(null);

                Assert.Empty(viewModel.Dialogs);
            });
        }

        [Fact]
        public async Task TheWindowOfTheStoreEdition_OffersNoExtensionInstall_AndTheCommandDoesNothing()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var viewModel = NewStoreWindow();

                Assert.False(viewModel.IsExtensionInstallAllowed);

                // Without the guard the command opens a file dialog and waits for a person: the limit turns that into a failure.
                await viewModel.InstallExtensionCommand.ExecuteAsync(null).WaitAsync(TimeSpan.FromSeconds(10));

                Assert.Empty(viewModel.Dialogs);
            });
        }
    }
}
