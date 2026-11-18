using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

using RedCell.Diagnostics.Update;

using RFiDGear.ViewModel;

using Xunit;

namespace RFiDGear.Tests
{
    /// <summary>
    /// The update notifier has an updater of its own and the main window has another one, and both ask the server for the update
    /// manifest in the background (issue 387). What the window shows of its updater (the offer of an update, the notes, the
    /// result of "Check for updates") is only true once its first check is through, so each of these waits for
    /// <see cref="Updater.InitialCheck"/>, without blocking anyone.
    /// </summary>
    [Collection("SettingsFileAccess")]
    public sealed class MainWindowUpdateDialogTests : IDisposable
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        private static readonly TimeSpan HeldFor = TimeSpan.FromMilliseconds(500);

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
        private async Task<(MainWindowViewModel ViewModel, Updater Updater)> WindowWithUpdaterAsync(ManifestServer server)
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
                new FakeProjectBootstrapper());

            var updater = await Task.Run(() => new Updater(UpdateManifestTexts.WriteLocalManifest(_folder, server.Url)));
            await server.RequestReceived.WaitAsync(TimeSpan.FromSeconds(30));
            typeof(MainWindowViewModel).GetField("updater", Private)!.SetValue(viewModel, updater);

            return (viewModel, updater);
        }

        private static Task Call(MainWindowViewModel viewModel, string method, params object[] arguments)
        {
            var types = Array.ConvertAll(arguments, argument => argument.GetType());
            var target = typeof(MainWindowViewModel).GetMethod(method, Private, null, types, null)!;

            return (Task)target.Invoke(viewModel, arguments)!;
        }

        private static async Task AssertWaitsAsync(Task call)
        {
            // Not through while the server holds the request of the window's updater.
            Assert.NotSame(call, await Task.WhenAny(call, Task.Delay(HeldFor)));
        }

        [Fact]
        public async Task TheOfferOfAnUpdate_WaitsForTheCheckOfTheWindow_ThenOffersNothingWhenThereIsNone()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var server = new ManifestServer();
                var (viewModel, _) = await WindowWithUpdaterAsync(server);

                var ask = Call(viewModel, "AskForUpdateNow", false);
                await AssertWaitsAsync(ask);

                server.Answer("503 Service Unavailable", string.Empty);   // no manifest: no update
                await ask.WaitAsync(TimeSpan.FromSeconds(30));

                Assert.Empty(viewModel.Dialogs);
                Assert.False((bool)typeof(MainWindowViewModel).GetField("userIsNotifiedForAvailableUpdate", Private)!.GetValue(viewModel)!);   // it asks again at the next tick
            });
        }

        [Fact]
        public async Task TheChangelog_WaitsForTheCheckOfTheWindow_AndIsShownWhetherThereIsAnUpdateOrNot()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var server = new ManifestServer();
                var (viewModel, updater) = await WindowWithUpdaterAsync(server);

                var show = Call(viewModel, "AskForUpdateNow", true);
                await AssertWaitsAsync(show);

                // A program that is up to date (the manifest is not newer than the program) must still be able to show its notes.
                server.Answer("200 OK", UpdateManifestTexts.ManifestText(UpdateManifestTexts.OldVersion, server.Url));
                await updater.InitialCheck.WaitAsync(TimeSpan.FromSeconds(30));

                Assert.False(updater.UpdateAvailable);
                Assert.False(string.IsNullOrEmpty(updater.UpdateInfoText));   // what the dialog is given, once it is let through

                // The dialog itself needs the application of the program; the test host has none.
                await Assert.ThrowsAsync<NullReferenceException>(() => show.WaitAsync(TimeSpan.FromSeconds(30)));
            });
        }

        [Fact]
        public async Task CheckForUpdates_WaitsForTheCheckOfTheWindow_BeforeItSaysThereIsNoUpdate()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var server = new ManifestServer();
                var (viewModel, updater) = await WindowWithUpdaterAsync(server);

                var check = Call(viewModel, "OnNewCheckUpdateCommand");
                await AssertWaitsAsync(check);   // it did not report "no update" while the request was pending

                server.Answer("200 OK", UpdateManifestTexts.ManifestText(UpdateManifestTexts.NewerVersion, server.Url));
                await check.WaitAsync(TimeSpan.FromSeconds(30));   // there is an update: no message of "none" is made (that needs the application of the program)

                Assert.True(updater.UpdateAvailable);
            });
        }
    }
}
