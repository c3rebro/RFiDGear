using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

using RedCell.Diagnostics.Update;

using RFiDGear.UI.MVVMDialogs.ViewModels;
using RFiDGear.ViewModel;

using Xunit;

namespace RFiDGear.Tests
{
    /// <summary>
    /// The update notifier has an updater of its own and the main window has another one, and both ask the server for the update
    /// manifest in the background (issue 387). What the window shows of its updater (the offer of an update, the notes, the
    /// result of "Check for updates") is only true once its first check is through, so each of these waits for
    /// <see cref="Updater.InitialCheck"/>, without blocking anyone. The dialogs are added where the test can see them: the thread
    /// of a window is replaced (<see cref="MainWindowViewModel.InvokeOnUiThread"/>), so nothing depends on an application.
    /// </summary>
    [Collection("SettingsFileAccess")]
    public sealed class MainWindowUpdateDialogTests : IDisposable
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        private static readonly TimeSpan HeldFor = TimeSpan.FromMilliseconds(500);

        private static readonly TimeSpan Limit = TimeSpan.FromSeconds(30);

        private readonly string _folder = Path.Combine(Path.GetTempPath(), "rfidgear-dialog-" + Guid.NewGuid().ToString("N"));

        public MainWindowUpdateDialogTests() => Directory.CreateDirectory(_folder);

        public void Dispose()
        {
            if (Directory.Exists(_folder))
            {
                Directory.Delete(_folder, recursive: true);
            }
        }

        // The main window, with an updater that asks the given server for the manifest (what the program does at its start).
        private async Task<MainWindowViewModel> WindowWithUpdaterAsync(ManifestServer server)
        {
            var viewModel = new MainWindowViewModel(
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
                new FakeProjectBootstrapper())
            {
                InvokeOnUiThread = action =>
                {
                    action();
                    return Task.CompletedTask;
                },
            };

            var updater = await Task.Run(() => new Updater(UpdateManifestTexts.WriteLocalManifest(_folder, server.Url)));
            await server.RequestReceived.WaitAsync(Limit);
            typeof(MainWindowViewModel).GetField("updater", Private)!.SetValue(viewModel, updater);

            return viewModel;
        }

        private static Task Call(MainWindowViewModel viewModel, string method, params object[] arguments)
        {
            var types = Array.ConvertAll(arguments, argument => argument.GetType());
            var target = typeof(MainWindowViewModel).GetMethod(method, Private, null, types, null)!;

            return (Task)target.Invoke(viewModel, arguments)!;
        }

        // Not through while the server holds the request of the window's updater.
        private static async Task AssertWaitsAsync(Task call) => Assert.NotSame(call, await Task.WhenAny(call, Task.Delay(HeldFor)));

        private static bool Notified(MainWindowViewModel viewModel) =>
            (bool)typeof(MainWindowViewModel).GetField("userIsNotifiedForAvailableUpdate", Private)!.GetValue(viewModel)!;

        private string NewerManifest(ManifestServer server) => UpdateManifestTexts.ManifestText(UpdateManifestTexts.NewerVersion, server.Url);

        private string OlderManifest(ManifestServer server) => UpdateManifestTexts.ManifestText(UpdateManifestTexts.OldVersion, server.Url);

        [Fact]
        public async Task TheOfferOfAnUpdate_WaitsForTheCheckOfTheWindow_ThenOffersNothingWhenThereIsNone()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var server = new ManifestServer();
                var viewModel = await WindowWithUpdaterAsync(server);

                var ask = Call(viewModel, "AskForUpdateNow", false);
                await AssertWaitsAsync(ask);

                server.Answer("503 Service Unavailable", string.Empty);   // no manifest: no update
                await ask.WaitAsync(Limit);

                Assert.Empty(viewModel.Dialogs);
                Assert.False(Notified(viewModel));   // it asks again at the next tick
            });
        }

        [Fact]
        public async Task TheOfferOfAnUpdate_IsMadeWhenTheWindowHasFoundOne()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var server = new ManifestServer();
                var viewModel = await WindowWithUpdaterAsync(server);

                var ask = Call(viewModel, "AskForUpdateNow", false);
                await AssertWaitsAsync(ask);

                server.Answer("200 OK", NewerManifest(server));
                await ask.WaitAsync(Limit);

                var dialog = Assert.IsType<UpdateNotifierViewModel>(Assert.Single(viewModel.Dialogs));
                Assert.Equal("Update Available", dialog.Caption);
                Assert.False(string.IsNullOrEmpty(dialog.UpdateHistoryText));   // the notes of the window's own manifest
                Assert.True(Notified(viewModel));
            });
        }

        [Fact]
        public async Task TheChangelog_WaitsForTheCheckOfTheWindow_AndIsShownWithItsNotesWhetherThereIsAnUpdateOrNot()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var server = new ManifestServer();
                var viewModel = await WindowWithUpdaterAsync(server);

                var show = Call(viewModel, "AskForUpdateNow", true);
                await AssertWaitsAsync(show);

                // A program that is up to date (the manifest is not newer than the program) must still be able to show its notes.
                server.Answer("200 OK", OlderManifest(server));
                await show.WaitAsync(Limit);

                var dialog = Assert.IsType<UpdateNotifierViewModel>(Assert.Single(viewModel.Dialogs));
                Assert.Equal("Changelog", dialog.Caption);
                Assert.False(string.IsNullOrEmpty(dialog.UpdateHistoryText));
            });
        }

        [Fact]
        public async Task CheckForUpdates_WaitsForTheCheckOfTheWindow_ThenOffersTheUpdateItFound()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var server = new ManifestServer();
                var viewModel = await WindowWithUpdaterAsync(server);

                var check = Call(viewModel, "OnNewCheckUpdateCommand");
                await AssertWaitsAsync(check);   // it did not report "no update" while the request was pending

                // The notifier of the test never offers anything (the one of the program has an updater of its own, which may
                // be through later than the window's): the command itself makes the offer.
                server.Answer("200 OK", NewerManifest(server));
                await check.WaitAsync(Limit);

                var dialog = Assert.IsType<UpdateNotifierViewModel>(Assert.Single(viewModel.Dialogs));
                Assert.Equal("Update Available", dialog.Caption);
            });
        }

        [Fact]
        public async Task CheckForUpdates_SaysThereIsNoUpdate_OnlyAfterTheCheckOfTheWindow()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var server = new ManifestServer();
                var viewModel = await WindowWithUpdaterAsync(server);

                var check = Call(viewModel, "OnNewCheckUpdateCommand");
                await AssertWaitsAsync(check);
                Assert.Empty(viewModel.Dialogs);

                server.Answer("200 OK", OlderManifest(server));
                await check.WaitAsync(Limit);

                Assert.IsType<MessageBoxViewModel>(Assert.Single(viewModel.Dialogs));
            });
        }
    }
}
