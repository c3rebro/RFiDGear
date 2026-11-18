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
    /// manifest in the background (issue 387). The dialog that offers the update shows the notes of the window's updater and the
    /// download uses its manifest, so the dialog must wait until that updater has one, whatever the notifier's has.
    /// </summary>
    [Collection("SettingsFileAccess")]
    public sealed class MainWindowUpdateDialogTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [Fact]
        public async Task TheUpdateDialog_IsNotShownBeforeTheUpdaterOfTheWindowKnowsTheManifest_AndTheNotificationIsKept()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
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

                // An updater that has no manifest and so knows of no update, whatever file this machine has.
                var withoutManifest = new Updater(new FileInfo(Path.Combine(Path.GetTempPath(), "RFiDGear.Tests", Guid.NewGuid().ToString("N"), "update.xml")));
                typeof(MainWindowViewModel).GetField("updater", Private)!.SetValue(viewModel, withoutManifest);

                var ask = typeof(MainWindowViewModel).GetMethod("AskForUpdateNow", Private, null, new[] { typeof(bool) }, null)!;
                await (Task)ask.Invoke(viewModel, new object[] { false })!;

                Assert.Empty(viewModel.Dialogs);
                Assert.False((bool)typeof(MainWindowViewModel).GetField("userIsNotifiedForAvailableUpdate", Private)!.GetValue(viewModel)!);   // it asks again at the next tick
            });
        }
    }
}
