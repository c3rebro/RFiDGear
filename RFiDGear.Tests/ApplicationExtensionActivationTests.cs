using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Hosting;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Resources;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

using RFiDGear.Contracts;
using RFiDGear.Infrastructure.ExtensionHost;
using RFiDGear.UI.MVVMDialogs.ViewModels;
using RFiDGear.UI.UIExtensions;
using RFiDGear.UI.UIExtensions.Interfaces;
using RFiDGear.ViewModel;

using Serilog.Events;
using Xunit;

namespace RFiDGear.Tests
{
    /// <summary>
    /// What happens when the user opens an application extension: the exported class is created on first use only, the
    /// initialized host service is handed to it, every opening gets fresh content, and the content is cleaned up when
    /// its window closes. Nothing here may happen while the menu is built or the application starts.
    /// </summary>
    public class ApplicationExtensionLauncherTests
    {
        private const string Secret = "00112233445566778899AABBCCDDEEFF";

        [Fact]
        public async Task Open_HandsTheHostToTheFactory_AndShowsWhatItBuilds()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                RecordingFactory.Recorder.Reset();
                using var parts = new PartSet(typeof(RecordingFactory));
                var (launcher, errors, _) = CreateLauncher();
                var host = new HeadlessExtensionHost();

                var window = launcher.Open(parts.Single, host);

                try
                {
                    Assert.NotNull(window);
                    Assert.Empty(errors);
                    var created = Assert.Single(RecordingFactory.Recorder.Created);
                    Assert.Same(host, created.Host);
                    Assert.Same(created.Content, window!.Content);
                    Assert.Equal("Recording app", window.Title);
                    Assert.True(window.IsVisible);
                }
                finally
                {
                    window?.Close();
                }
            });
        }

        [Fact]
        public async Task Open_ShowsTheWindowOnTheMonitorOfTheMainWindow_AndLetsGoOfTheOwner()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                RecordingFactory.Recorder.Reset();
                using var parts = new PartSet(typeof(RecordingFactory));
                var main = ShownMainWindow();
                var launcher = new ApplicationExtensionLauncher(_ => { }, null, () => main);

                var window = launcher.Open(parts.Single, new HeadlessExtensionHost());

                try
                {
                    Assert.NotNull(window);

                    // Placed relative to the main window, which is what keeps it on the monitor the main window is on.
                    Assert.Same(main, Assert.Single(RecordingFactory.Recorder.Created).Content.OwnerAtFirstLayout);
                    AssertPlacedOnTheMainWindow(main, window!);

                    // The owner only placed it: it is no dialog of the main window.
                    Assert.Null(window.Owner);
                    Assert.True(window.IsVisible);
                }
                finally
                {
                    window?.Close();
                    main.Close();
                }
            });
        }

        [Fact]
        public async Task Open_ShowsTheWindowOfAnExtensionThatNamesAPage_OnTheMonitorOfTheMainWindowToo()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                using var parts = new PartSet(typeof(FramePageExtension));
                var main = ShownMainWindow();
                var errors = new List<string>();
                var launcher = new ApplicationExtensionLauncher(errors.Add, null, () => main);   // the original frame-based opener

                var window = launcher.Open(parts.Single, new HeadlessExtensionHost());

                try
                {
                    Assert.Empty(errors);
                    Assert.NotNull(window);

                    // The page does not exist; the frame says so when the dispatcher runs, which must not end the test run.
                    ((Frame)window!.Content).NavigationFailed += (_, e) => e.Handled = true;

                    AssertPlacedOnTheMainWindow(main, window);
                    Assert.Null(window.Owner);
                    Assert.True(window.IsVisible);
                }
                finally
                {
                    window?.Close();
                    main.Close();
                }
            });
        }

        [Fact]
        public async Task Open_WithTheMainWindowOfAnotherThread_OpensTheWindowAsBefore()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                // WPF lets only the thread of a window touch it, so a main window of another thread cannot own this one.
                RecordingFactory.Recorder.Reset();
                using var parts = new PartSet(typeof(RecordingFactory));
                var (foreign, shutDown) = ShownMainWindowOnAnotherThread();
                var errors = new List<string>();
                var launcher = new ApplicationExtensionLauncher(errors.Add, null, () => foreign);

                var window = launcher.Open(parts.Single, new HeadlessExtensionHost());

                try
                {
                    Assert.Empty(errors);
                    Assert.NotNull(window);
                    Assert.Null(Assert.Single(RecordingFactory.Recorder.Created).Content.OwnerAtFirstLayout);
                    Assert.True(window!.IsVisible);
                }
                finally
                {
                    window?.Close();
                    shutDown();
                }
            });
        }

        [Fact]
        public async Task Open_WithAnApplicationOfAnotherThread_OpensTheWindowAsBefore()
        {
            // Other tests leave an Application behind whose dispatcher belongs to a thread that has ended. Its main
            // window cannot be touched from here, and asking for it throws.
            await StaTestRunner.RunOnStaThreadAsync(() => { _ = Application.Current ?? new Application(); });

            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                RecordingFactory.Recorder.Reset();
                using var parts = new PartSet(typeof(RecordingFactory));
                var errors = new List<string>();
                var launcher = new ApplicationExtensionLauncher(errors.Add);   // looks for the main window of the application itself

                var window = launcher.Open(parts.Single, new HeadlessExtensionHost());

                try
                {
                    Assert.Empty(errors);
                    Assert.NotNull(window);
                    Assert.Null(window!.Owner);
                    Assert.True(window.IsVisible);
                }
                finally
                {
                    window?.Close();
                }
            });
        }

        [Fact]
        public async Task Open_WithAMinimizedMainWindow_NeverMakesItTheOwner()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                // Windows hides the windows a minimized window owns, so the new window would not be seen.
                RecordingFactory.Recorder.Reset();
                using var parts = new PartSet(typeof(RecordingFactory));
                var main = ShownMainWindow();
                main.WindowState = WindowState.Minimized;
                var launcher = new ApplicationExtensionLauncher(_ => { }, null, () => main);

                var window = launcher.Open(parts.Single, new HeadlessExtensionHost());

                try
                {
                    Assert.NotNull(window);
                    Assert.Null(Assert.Single(RecordingFactory.Recorder.Created).Content.OwnerAtFirstLayout);
                    Assert.Null(window!.Owner);
                    Assert.True(window.IsVisible);
                }
                finally
                {
                    window?.Close();
                    main.Close();
                }
            });
        }

        [Fact]
        public async Task Open_WithoutAMainWindowThatIsShown_OpensTheWindowAsBefore()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                using var parts = new PartSet(typeof(RecordingFactory));
                var hidden = ShownMainWindow();
                hidden.Hide();
                var neverShown = new Window();

                // No main window, one that was never shown (a window that has not been shown cannot own another) and one
                // that is hidden.
                foreach (var main in new Window?[] { null, neverShown, hidden })
                {
                    RecordingFactory.Recorder.Reset();
                    var errors = new List<string>();
                    var launcher = new ApplicationExtensionLauncher(errors.Add, null, () => main);

                    var window = launcher.Open(parts.Single, new HeadlessExtensionHost());

                    try
                    {
                        Assert.Empty(errors);
                        Assert.NotNull(window);
                        Assert.Null(Assert.Single(RecordingFactory.Recorder.Created).Content.OwnerAtFirstLayout);
                        Assert.Null(window!.Owner);
                        Assert.True(window.IsVisible);
                    }
                    finally
                    {
                        window?.Close();
                    }
                }

                hidden.Close();
            });
        }

        [Fact]
        public async Task Open_CreatesTheExportedClassOnce_AndFreshContentEveryTime()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                RecordingFactory.Recorder.Reset();
                using var parts = new PartSet(typeof(RecordingFactory));
                var (launcher, _, _) = CreateLauncher();
                var host = new HeadlessExtensionHost();

                Assert.Equal(0, RecordingFactory.Recorder.Constructed);   // composing and reading metadata created nothing

                var first = launcher.Open(parts.Single, host);
                var second = launcher.Open(parts.Single, host);

                try
                {
                    Assert.Equal(1, RecordingFactory.Recorder.Constructed);
                    Assert.Equal(2, RecordingFactory.Recorder.Created.Count);
                    Assert.NotSame(first!.Content, second!.Content);
                    Assert.NotSame(first, second);
                }
                finally
                {
                    first?.Close();
                    second?.Close();
                }
            });
        }

        [Fact]
        public async Task ClosingTheWindow_DisposesTheContentAndItsDataContext_ExactlyOnce()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                RecordingFactory.Recorder.Reset();
                using var parts = new PartSet(typeof(RecordingFactory));
                var (launcher, _, _) = CreateLauncher();
                var window = launcher.Open(parts.Single, new HeadlessExtensionHost())!;
                var created = Assert.Single(RecordingFactory.Recorder.Created);
                var dataContext = Assert.IsType<ProbeDataContext>(created.Content.DataContext);
                Assert.Equal(0, created.Content.DisposeCount);

                window.Close();

                Assert.Equal(1, created.Content.DisposeCount);
                Assert.Equal(1, dataContext.DisposeCount);

                window.Close();   // closing a closed window is a no-op; nothing is disposed twice
                Assert.Equal(1, created.Content.DisposeCount);
                Assert.Equal(1, dataContext.DisposeCount);
            });
        }

        [Fact]
        public async Task ClosingTheWindow_WhenDisposeThrows_StillDisposesTheDataContext_AndLogsWithoutTheSecret()
        {
            using var logs = CapturedLogs.Install();

            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                ThrowingDisposeFactory.Recorder.Reset();
                using var parts = new PartSet(typeof(ThrowingDisposeFactory));
                var (launcher, errors, _) = CreateLauncher();
                var window = launcher.Open(parts.Single, new HeadlessExtensionHost())!;
                var created = Assert.Single(ThrowingDisposeFactory.Recorder.Created);

                // The window raises Closed from the message pump, so an exception that escaped the handler would not
                // come back through Close(): it would reach the dispatcher and end the process.
                Exception? escaped = null;
                Dispatcher.CurrentDispatcher.UnhandledException += (_, e) =>
                {
                    escaped = e.Exception;
                    e.Handled = true;
                };

                window.Close();

                Assert.Null(escaped);
                Assert.Empty(errors);
                Assert.Equal(1, created.Content.DisposeCount);
                Assert.Equal(1, Assert.IsType<ProbeDataContext>(created.Content.DataContext).DisposeCount);
            });

            var entry = Assert.Single(logs.From(nameof(ApplicationExtensionLauncher)), e => e.Level == LogEventLevel.Error);
            Assert.Null(entry.Exception);
            var written = CapturedLogs.EverythingWrittenFor(entry);
            Assert.DoesNotContain(Secret, written, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("[REDACTED", written);
        }

        [Fact]
        public async Task Open_WhenCreateContentThrows_ReportsTheNameAndShowsNothing()
        {
            using var logs = CapturedLogs.Install();

            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                using var parts = new PartSet(typeof(ThrowingCreateFactory));
                var (launcher, errors, _) = CreateLauncher();

                var window = launcher.Open(parts.Single, new HeadlessExtensionHost());

                Assert.Null(window);
                Assert.Equal(new[] { "Throwing create" }, errors);
            });

            var entry = Assert.Single(logs.From(nameof(ApplicationExtensionLauncher)), e => e.Level == LogEventLevel.Error);
            Assert.DoesNotContain(Secret, CapturedLogs.EverythingWrittenFor(entry), StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Open_WhenCreateContentReturnsNull_ReportsAndShowsNothing()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                using var parts = new PartSet(typeof(NullContentFactory));
                var (launcher, errors, _) = CreateLauncher();

                var window = launcher.Open(parts.Single, new HeadlessExtensionHost());

                Assert.Null(window);
                Assert.Equal(new[] { "Null content" }, errors);
            });
        }

        [Fact]
        public async Task Open_WhenTheClassCannotBeCreated_ReportsEachTime()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                ThrowingConstructorFactory.Recorder.Reset();
                using var parts = new PartSet(typeof(ThrowingConstructorFactory));
                var (launcher, errors, _) = CreateLauncher();
                var host = new HeadlessExtensionHost();

                Assert.Null(launcher.Open(parts.Single, host));
                Assert.Null(launcher.Open(parts.Single, host));

                // MEF does not remember a part that failed to construct, so every opening tries again and the user
                // gets the error again. A constructor that failed once may succeed later (a file that was missing).
                Assert.Equal(new[] { "Throwing constructor", "Throwing constructor" }, errors);
                Assert.Equal(2, ThrowingConstructorFactory.Recorder.Constructed);
            });
        }

        [Fact]
        public async Task Open_RejectsContentThatAlreadyBelongsToAnotherElement_AndLeavesItAlone()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                ParentedContentFactory.Recorder.Reset();
                using var parts = new PartSet(typeof(ParentedContentFactory));
                var (launcher, errors, _) = CreateLauncher();

                var window = launcher.Open(parts.Single, new HeadlessExtensionHost());

                // Another element, possibly a window that is still on screen, is showing this content. It is not the
                // launcher's to show or to dispose, so it is reported and left exactly as it was.
                Assert.Null(window);
                Assert.Equal(new[] { "Parented content" }, errors);
                var created = Assert.Single(ParentedContentFactory.Recorder.Created);
                Assert.Equal(0, created.Content.DisposeCount);
                Assert.Equal(0, Assert.IsType<ProbeDataContext>(created.Content.DataContext).DisposeCount);
            });
        }

        [Fact]
        public async Task Open_WhenShowFails_ClosesTheWindow_AndDisposesTheContentExactlyOnce()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                ExplodingLayoutFactory.Recorder.Reset();
                using var parts = new PartSet(typeof(ExplodingLayoutFactory));
                var (launcher, errors, _) = CreateLauncher();

                // The content throws during the first layout, which runs inside Show() after the window already
                // exists and is visible.
                var window = launcher.Open(parts.Single, new HeadlessExtensionHost());

                Assert.Null(window);
                Assert.Equal(new[] { "Exploding layout" }, errors);
                var created = Assert.Single(ExplodingLayoutFactory.Recorder.Created);
                var dataContext = Assert.IsType<ProbeDataContext>(created.Content.DataContext);
                Assert.Equal(1, created.Content.DisposeCount);
                Assert.Equal(1, dataContext.DisposeCount);
                Assert.True(
                    Window.GetWindow(created.Content)?.IsVisible != true,
                    "no empty window may stay open after the failure");

                // Nothing is left that could dispose the content a second time.
                Window.GetWindow(created.Content)?.Close();
                Assert.Equal(1, created.Content.DisposeCount);
                Assert.Equal(1, dataContext.DisposeCount);
            });
        }

        [Fact]
        public async Task Open_ForAnExtensionThatNamesAPage_NeverCreatesItsClass()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                PageNamingExtension.Recorder.Reset();
                using var parts = new PartSet(typeof(PageNamingExtension));
                var (launcher, errors, legacy) = CreateLauncher();

                launcher.Open(parts.Single, new HeadlessExtensionHost());

                // The class would fail to construct, and also implements the factory interface; the page wins and
                // an extension that worked as a bare marker before keeps working.
                Assert.Empty(errors);
                var opened = Assert.Single(legacy);
                Assert.Equal(PageNamingExtension.PageUri, opened.Uri);
                Assert.Equal(0, PageNamingExtension.Recorder.Constructed);
            });
        }

        [Fact]
        public async Task Open_ForAnExtensionWithoutAFactory_NavigatesToItsUri()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                using var parts = new PartSet(typeof(UriOnlyExtension));
                var (launcher, errors, legacy) = CreateLauncher();

                launcher.Open(parts.Single, new HeadlessExtensionHost());

                Assert.Empty(errors);
                var opened = Assert.Single(legacy);
                Assert.Equal(UriOnlyExtension.PageUri, opened.Uri);
                Assert.Equal("Uri only", opened.Title);
            });
        }

        [Fact]
        public async Task Open_ForAnExtensionWithABlankUri_ReportsInsteadOfThrowing()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                using var parts = new PartSet(typeof(BlankUriExtension));
                var errors = new List<string>();
                var launcher = new ApplicationExtensionLauncher(errors.Add);   // the original frame-based opener

                var window = launcher.Open(parts.Single, new HeadlessExtensionHost());

                Assert.Null(window);
                Assert.Equal(new[] { "Blank uri" }, errors);
            });
        }

        [Fact]
        public async Task Open_WhenReportingTheFailureThrows_DoesNotPropagate()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                using var parts = new PartSet(typeof(NullContentFactory));
                var launcher = new ApplicationExtensionLauncher(_ => throw new InvalidOperationException("no dialog available"));

                Assert.Null(launcher.Open(parts.Single, new HeadlessExtensionHost()));
            });
        }

        [Fact]
        public async Task Open_RejectsMissingArguments()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                using var parts = new PartSet(typeof(RecordingFactory));
                var (launcher, _, _) = CreateLauncher();

                Assert.Throws<ArgumentNullException>(() => launcher.Open(null!, new HeadlessExtensionHost()));
                Assert.Throws<ArgumentNullException>(() => launcher.Open(parts.Single, null!));
                Assert.Throws<ArgumentNullException>(() => new ApplicationExtensionLauncher(null!));
            });
        }

        // A main window that has been shown, in the middle of the work area of the primary monitor. A window of 900 by 600
        // centered on it is then as close to the middle of the work area as the display allows, whatever its size and
        // scaling, and can be told from one that Windows placed.
        private static Window ShownMainWindow()
        {
            const double width = 400;
            const double height = 300;
            var workArea = SystemParameters.WorkArea;
            var main = new Window
            {
                Left = workArea.Left + ((workArea.Width - width) / 2),
                Top = workArea.Top + ((workArea.Height - height) / 2),
                Width = width,
                Height = height,
                ShowActivated = false,
                ShowInTaskbar = false
            };
            main.Show();

            return main;
        }

        // The window is centered on the main window, or, when the display is too small for that, kept inside the work area
        // of the monitor, which is what WPF does for a window that would not fit.
        private static void AssertPlacedOnTheMainWindow(Window main, Window window)
        {
            var workArea = SystemParameters.WorkArea;
            var centeredLeft = main.Left + ((main.Width - window.Width) / 2);
            var centeredTop = main.Top + ((main.Height - window.Height) / 2);
            var keptInLeft = Math.Max(workArea.Left, Math.Min(centeredLeft, workArea.Right - window.Width));
            var keptInTop = Math.Max(workArea.Top, Math.Min(centeredTop, workArea.Bottom - window.Height));

            Assert.True(
                Math.Abs(window.Left - centeredLeft) <= 2 || Math.Abs(window.Left - keptInLeft) <= 2,
                $"Left {window.Left} is neither centered ({centeredLeft}) nor kept in the work area ({keptInLeft}).");
            Assert.True(
                Math.Abs(window.Top - centeredTop) <= 2 || Math.Abs(window.Top - keptInTop) <= 2,
                $"Top {window.Top} is neither centered ({centeredTop}) nor kept in the work area ({keptInTop}).");
        }

        // A shown window that belongs to a thread of its own, which this thread must not touch.
        private static (Window Window, Action ShutDown) ShownMainWindowOnAnotherThread()
        {
            Window? window = null;
            Dispatcher? dispatcher = null;
            using var ready = new ManualResetEventSlim();
            var thread = new Thread(() =>
            {
                window = new Window { Left = 40, Top = 40, Width = 1000, Height = 650, ShowActivated = false, ShowInTaskbar = false };
                window.Show();
                dispatcher = Dispatcher.CurrentDispatcher;
                ready.Set();
                Dispatcher.Run();
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            ready.Wait();

            return (window!, () =>
            {
                dispatcher!.InvokeShutdown();
                thread.Join();
            });
        }

        private static (ApplicationExtensionLauncher Launcher, List<string> Errors, List<(string Uri, string Title)> Legacy) CreateLauncher()
        {
            var errors = new List<string>();
            var legacy = new List<(string Uri, string Title)>();
            var launcher = new ApplicationExtensionLauncher(
                errors.Add,
                (uri, title) =>
                {
                    legacy.Add((uri, title));
                    return null;
                });
            return (launcher, errors, legacy);
        }
    }

    /// <summary>
    /// The Extensions menu of the main window: entries are built from metadata alone, are unusable until the host
    /// service exists, and hand the launcher the lazy extension together with that service.
    /// </summary>
    [Collection("SettingsFileAccess")]
    public class ApplicationExtensionMenuTests
    {
        [Fact]
        public async Task Menu_IsBuiltFromMetadata_WithoutCreatingAnyExtension()
        {
            await RunWithViewModel(async (tempRoot) =>
            {
                RecordingFactory.Recorder.Reset();
                using var parts = new PartSet(typeof(RecordingFactory));
                var (viewModel, initialize) = MainWindowViewModelTests.CreateUninitializedViewModel(tempRoot);

                viewModel.ApplicationExtensionItems = parts.Exports;
                var item = Assert.Single(viewModel.ApplicationExtensionMenuItems);
                Assert.Equal("Recording app", item.Header);
                Assert.Equal(0, RecordingFactory.Recorder.Constructed);

                await initialize(null);
                _ = item.Command.CanExecute(null);

                Assert.Equal(0, RecordingFactory.Recorder.Constructed);
            });
        }

        [Fact]
        public async Task MenuEntries_AreDisabledUntilTheHostExists_ThenEnabled()
        {
            await RunWithViewModel(async (tempRoot) =>
            {
                using var parts = new PartSet(typeof(RecordingFactory));
                var (viewModel, initialize) = MainWindowViewModelTests.CreateUninitializedViewModel(tempRoot);
                viewModel.ApplicationExtensionItems = parts.Exports;
                var command = Assert.Single(viewModel.ApplicationExtensionMenuItems).Command;
                var changed = 0;
                command.CanExecuteChanged += (_, _) => changed++;

                Assert.False(command.CanExecute(null), "the host service does not exist yet");

                await initialize(null);

                Assert.True(changed >= 1, "the menu must be told the entry became usable");
                Assert.True(command.CanExecute(null));
            });
        }

        [Fact]
        public async Task ExecutingBeforeStartUpFinished_ShowsAnError_AndNeverCallsTheLauncher()
        {
            await RunWithViewModel(async (tempRoot) =>
            {
                using var parts = new PartSet(typeof(RecordingFactory));
                var launcher = new RecordingLauncher();
                var (viewModel, initialize) = MainWindowViewModelTests.CreateUninitializedViewModel(tempRoot, launcher);
                viewModel.ApplicationExtensionItems = parts.Exports;
                var command = Assert.Single(viewModel.ApplicationExtensionMenuItems).Command;

                command.Execute(null);   // a command can be executed without asking CanExecute first

                Assert.Empty(launcher.Calls);
                var dialog = Assert.IsType<CustomDialogViewModel>(Assert.Single(viewModel.Dialogs));
                Assert.Contains("Recording app", dialog.Message);
                Assert.DoesNotContain("{0}", dialog.Message);
                Assert.False(string.IsNullOrWhiteSpace(dialog.Caption), "a wrong caption key would leave this empty");
                await initialize(null);
            });
        }

        [Fact]
        public async Task OpeningAnEntry_HandsTheLauncherTheLazyExtension_AndTheViewModelsHostService()
        {
            await RunWithViewModel(async (tempRoot) =>
            {
                using var parts = new PartSet(typeof(RecordingFactory));
                var launcher = new RecordingLauncher();
                var viewModel = await MainWindowViewModelTests.CreateInitializedViewModelAsync(tempRoot, launcher: launcher);
                viewModel.ApplicationExtensionItems = parts.Exports;

                Assert.Single(viewModel.ApplicationExtensionMenuItems).Command.Execute(null);

                var call = Assert.Single(launcher.Calls);
                Assert.Same(parts.Single, call.Extension);
                Assert.Same(viewModel.ExtensionHost, call.Host);
                Assert.Empty(viewModel.Dialogs);
            });
        }

        [Fact]
        public async Task OpeningAnEntry_EndToEnd_GivesTheFactoryTheSameHostServiceTheViewModelExposes()
        {
            await RunWithViewModel(async (tempRoot) =>
            {
                RecordingFactory.Recorder.Reset();
                using var parts = new PartSet(typeof(RecordingFactory));
                var viewModel = await MainWindowViewModelTests.CreateInitializedViewModelAsync(tempRoot);
                viewModel.ApplicationExtensionItems = parts.Exports;

                Assert.Single(viewModel.ApplicationExtensionMenuItems).Command.Execute(null);

                var created = Assert.Single(RecordingFactory.Recorder.Created);
                try
                {
                    Assert.Same(viewModel.ExtensionHost, created.Host);
                    Assert.NotNull(Window.GetWindow(created.Content));
                }
                finally
                {
                    Window.GetWindow(created.Content)?.Close();
                }

                Assert.Equal(1, created.Content.DisposeCount);
            });
        }

        [Fact]
        public async Task AnExtensionThatFails_LeavesTheOtherEntriesAndStartUpIntact()
        {
            await RunWithViewModel(async (tempRoot) =>
            {
                RecordingFactory.Recorder.Reset();
                using var parts = new PartSet(typeof(ThrowingCreateFactory), typeof(RecordingFactory));
                var viewModel = await MainWindowViewModelTests.CreateInitializedViewModelAsync(tempRoot);
                viewModel.ApplicationExtensionItems = parts.Exports;
                var byHeader = viewModel.ApplicationExtensionMenuItems.ToDictionary(item => item.Header);

                byHeader["Throwing create"].Command.Execute(null);
                var dialog = Assert.IsType<CustomDialogViewModel>(Assert.Single(viewModel.Dialogs));
                Assert.Contains("Throwing create", dialog.Message);

                byHeader["Recording app"].Command.Execute(null);

                var created = Assert.Single(RecordingFactory.Recorder.Created);
                Window.GetWindow(created.Content)?.Close();
                Assert.Same(viewModel.ExtensionHost, created.Host);
            });
        }

        [Fact]
        public async Task ReaderName_FollowsTheConfiguredReader_AndSubscribersAreToldOnlyOfRealChanges()
        {
            await RunWithViewModel(async (tempRoot) =>
            {
                var viewModel = await MainWindowViewModelTests.CreateInitializedViewModelAsync(tempRoot);
                var reader = viewModel.ExtensionHost.Reader;
                var seen = new List<string?>();
                reader.ReaderStateChanged += (_, _) => seen.Add(reader.ReaderName);

                Assert.Equal("Reader", reader.ReaderName);   // the name the settings gave at start-up

                viewModel.CurrentReader = "Other reader";
                viewModel.CurrentReader = "Other reader";   // not a change

                Assert.Equal("Other reader", reader.ReaderName);
                Assert.Equal(new[] { "Other reader" }, seen);
            });
        }

        [Fact]
        public async Task ReaderStateChanged_WhenASubscriberThrows_DoesNotBreakTheSetterOrTheNextSubscriber()
        {
            using var logs = CapturedLogs.Install();

            await RunWithViewModel(async (tempRoot) =>
            {
                var viewModel = await MainWindowViewModelTests.CreateInitializedViewModelAsync(tempRoot);
                var later = 0;
                viewModel.ExtensionHost.Reader.ReaderStateChanged += (_, _) => throw new InvalidOperationException("Key=" + "00112233445566778899AABBCCDDEEFF");
                viewModel.ExtensionHost.Reader.ReaderStateChanged += (_, _) => later++;

                viewModel.CurrentReader = "Another reader";   // must not throw

                Assert.Equal("Another reader", viewModel.CurrentReader);
                Assert.Equal(1, later);
            });

            var entry = Assert.Single(logs.From(nameof(ReaderHostImpl)), e => e.Level == LogEventLevel.Error);
            Assert.DoesNotContain("00112233445566778899AABBCCDDEEFF", CapturedLogs.EverythingWrittenFor(entry), StringComparison.OrdinalIgnoreCase);
        }

        private static async Task RunWithViewModel(Func<string, Task> body)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var tempRoot = Path.Combine(Path.GetTempPath(), "RFiDGear.Tests", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempRoot);
                try
                {
                    await body(tempRoot);
                }
                finally
                {
                    Directory.Delete(tempRoot, recursive: true);
                }
            });
        }
    }

    public class ReaderHostImplTests
    {
        [Fact]
        public void ReaderName_IsReadFromTheProviderEachTime()
        {
            var name = "first";
            var reader = new ReaderHostImpl(() => name);

            Assert.Equal("first", reader.ReaderName);
            name = "second";
            Assert.Equal("second", reader.ReaderName);
        }

        [Fact]
        public void ReaderName_WithAFixedName_StillWorks()
        {
            Assert.Equal("Elatec", new ReaderHostImpl("Elatec").ReaderName);
        }

        [Fact]
        public void Constructor_RejectsAMissingProvider()
        {
            Assert.Throws<ArgumentNullException>(() => new ReaderHostImpl((Func<string?>)null!));
        }

        [Fact]
        public void RaiseStateChanged_WithNoSubscribers_DoesNothing()
        {
            var reader = new ReaderHostImpl("reader");

            Assert.Null(Record.Exception(reader.RaiseStateChanged));
        }

        [Fact]
        public void RaiseStateChanged_IsolatesEachSubscriber_AndSendsTheHostAsSender()
        {
            var reader = new ReaderHostImpl("reader");
            var senders = new List<object?>();
            reader.ReaderStateChanged += (_, _) => throw new InvalidOperationException("failure");
            reader.ReaderStateChanged += (sender, _) => senders.Add(sender);

            reader.RaiseStateChanged();

            Assert.Same(reader, Assert.Single(senders));
        }
    }

    /// <summary>The strings of the dialog shown when an extension cannot be opened.</summary>
    public class ExtensionOpenFailedStringsTests
    {
        [Theory]
        [InlineData("en")]
        [InlineData("de")]
        public void BothLanguages_HaveACaptionAndAMessageWithAPlaceholderForTheName(string language)
        {
            var culture = new CultureInfo(language);

            var caption = Manifest.GetString(MainWindowViewModel.ExtensionOpenFailedCaptionKey, culture);
            var message = Manifest.GetString(MainWindowViewModel.ExtensionOpenFailedMessageKey, culture);

            Assert.False(string.IsNullOrWhiteSpace(caption));
            Assert.Contains("{0}", message);
            Assert.Contains("Probe", string.Format(culture, message!, "Probe"), StringComparison.Ordinal);
        }

        [Fact]
        public void TheGermanStrings_AreTranslated()
        {
            var german = new CultureInfo("de");
            var english = new CultureInfo("en");

            Assert.NotEqual(
                Manifest.GetString(MainWindowViewModel.ExtensionOpenFailedMessageKey, english),
                Manifest.GetString(MainWindowViewModel.ExtensionOpenFailedMessageKey, german));
            Assert.NotEqual(
                Manifest.GetString(MainWindowViewModel.ExtensionOpenFailedCaptionKey, english),
                Manifest.GetString(MainWindowViewModel.ExtensionOpenFailedCaptionKey, german));
        }

        private static ResourceManager Manifest { get; } =
            new ResourceManager("RFiDGear.Resources.Manifest", typeof(MainWindowViewModel).Assembly);
    }

    // ── fixtures ──────────────────────────────────────────────────────────────

    /// <summary>The exports of a few part types, as the main window receives them from MEF.</summary>
    internal sealed class PartSet : IDisposable
    {
        private readonly CompositionContainer _container;

        public PartSet(params Type[] partTypes)
        {
            _container = new CompositionContainer(new TypeCatalog(partTypes));
            Exports = _container.GetExports<IUIExtension, IUIExtensionDetails>().ToArray();
        }

        public Lazy<IUIExtension, IUIExtensionDetails>[] Exports { get; }

        public Lazy<IUIExtension, IUIExtensionDetails> Single => Exports.Single();

        public void Dispose() => _container.Dispose();
    }

    internal sealed class RecordingLauncher : IApplicationExtensionLauncher
    {
        public List<(Lazy<IUIExtension, IUIExtensionDetails> Extension, IRFiDGearExtensionHost Host)> Calls { get; } = new();

        public Window? Open(Lazy<IUIExtension, IUIExtensionDetails> extension, IRFiDGearExtensionHost host)
        {
            Calls.Add((extension, host));
            return null;
        }
    }

    /// <summary>What a factory part saw: how often it was constructed and what it built for which host.</summary>
    public sealed class ActivationRecorder
    {
        private readonly object _gate = new();
        private readonly List<(IRFiDGearExtensionHost Host, ProbeContent Content)> _created = new();
        private int _constructed;

        public int Constructed => Volatile.Read(ref _constructed);

        public IReadOnlyList<(IRFiDGearExtensionHost Host, ProbeContent Content)> Created
        {
            get
            {
                lock (_gate)
                {
                    return _created.ToList();
                }
            }
        }

        public void RecordConstruction() => Interlocked.Increment(ref _constructed);

        public void RecordContent(IRFiDGearExtensionHost host, ProbeContent content)
        {
            lock (_gate)
            {
                _created.Add((host, content));
            }
        }

        public void Reset()
        {
            Volatile.Write(ref _constructed, 0);
            lock (_gate)
            {
                _created.Clear();
            }
        }
    }

    /// <summary>Content that counts how often it is disposed, optionally failing with a secret in the message.</summary>
    public sealed class ProbeContent : UserControl, IDisposable
    {
        private readonly bool _throwOnDispose;
        private readonly bool _throwOnLayout;
        private int _disposeCount;
        private bool _measured;

        public ProbeContent(bool throwOnDispose = false, bool throwOnLayout = false)
        {
            _throwOnDispose = throwOnDispose;
            _throwOnLayout = throwOnLayout;
            DataContext = new ProbeDataContext();
        }

        public int DisposeCount => Volatile.Read(ref _disposeCount);

        /// <summary>The owner of the window when this content was first laid out, which happens inside <c>Window.Show()</c>.</summary>
        public Window? OwnerAtFirstLayout { get; private set; }

        protected override Size MeasureOverride(Size constraint)
        {
            if (!_measured)
            {
                _measured = true;
                OwnerAtFirstLayout = Window.GetWindow(this)?.Owner;
            }

            return _throwOnLayout
                ? throw new InvalidOperationException("Layout failed.")
                : base.MeasureOverride(constraint);
        }

        public void Dispose()
        {
            Interlocked.Increment(ref _disposeCount);
            if (_throwOnDispose)
            {
                FailLikeAnExtension();
            }
        }

        /// <summary>The failure a faulty extension's Dispose produces, with key-like text in its message.</summary>
        private static void FailLikeAnExtension() =>
            throw new InvalidOperationException("Key=00112233445566778899AABBCCDDEEFF");
    }

    public sealed class ProbeDataContext : IDisposable
    {
        private int _disposeCount;

        public int DisposeCount => Volatile.Read(ref _disposeCount);

        public void Dispose() => Interlocked.Increment(ref _disposeCount);
    }

    [UiExtension(Name = "Recording app", HostPlacement = HostPlacement.Application)]
    public sealed class RecordingFactory : IApplicationUIExtension
    {
        public RecordingFactory() => Recorder.RecordConstruction();

        public static ActivationRecorder Recorder { get; } = new ActivationRecorder();

        public FrameworkElement CreateContent(IRFiDGearExtensionHost host)
        {
            var content = new ProbeContent();
            Recorder.RecordContent(host, content);
            return content;
        }
    }

    [UiExtension(Name = "Throwing dispose", HostPlacement = HostPlacement.Application)]
    public sealed class ThrowingDisposeFactory : IApplicationUIExtension
    {
        public static ActivationRecorder Recorder { get; } = new ActivationRecorder();

        public FrameworkElement CreateContent(IRFiDGearExtensionHost host)
        {
            var content = new ProbeContent(throwOnDispose: true);
            Recorder.RecordContent(host, content);
            return content;
        }
    }

    [UiExtension(Name = "Throwing create", HostPlacement = HostPlacement.Application)]
    public sealed class ThrowingCreateFactory : IApplicationUIExtension
    {
        public FrameworkElement CreateContent(IRFiDGearExtensionHost host) =>
            throw new InvalidOperationException("Key=00112233445566778899AABBCCDDEEFF");
    }

    [UiExtension(Name = "Null content", HostPlacement = HostPlacement.Application)]
    public sealed class NullContentFactory : IApplicationUIExtension
    {
        public FrameworkElement CreateContent(IRFiDGearExtensionHost host) => null!;
    }

    [UiExtension(Name = "Throwing constructor", HostPlacement = HostPlacement.Application)]
    public sealed class ThrowingConstructorFactory : IApplicationUIExtension
    {
        public ThrowingConstructorFactory()
        {
            Recorder.RecordConstruction();
            throw new InvalidOperationException("The constructor always fails.");
        }

        public static ActivationRecorder Recorder { get; } = new ActivationRecorder();

        public FrameworkElement CreateContent(IRFiDGearExtensionHost host) => new ProbeContent();
    }

    /// <summary>Builds content that already belongs to another element, which no window accepts.</summary>
    [UiExtension(Name = "Parented content", HostPlacement = HostPlacement.Application)]
    public sealed class ParentedContentFactory : IApplicationUIExtension
    {
        public static ActivationRecorder Recorder { get; } = new ActivationRecorder();

        public FrameworkElement CreateContent(IRFiDGearExtensionHost host)
        {
            var content = new ProbeContent();
            Recorder.RecordContent(host, content);
            _ = new StackPanel { Children = { content } };
            return content;
        }
    }

    /// <summary>Content whose first layout throws, which happens inside <c>Window.Show()</c>.</summary>
    [UiExtension(Name = "Exploding layout", HostPlacement = HostPlacement.Application)]
    public sealed class ExplodingLayoutFactory : IApplicationUIExtension
    {
        public static ActivationRecorder Recorder { get; } = new ActivationRecorder();

        public FrameworkElement CreateContent(IRFiDGearExtensionHost host)
        {
            var content = new ProbeContent(throwOnLayout: true);
            Recorder.RecordContent(host, content);
            return content;
        }
    }

    /// <summary>Names a page and also looks like a factory whose class cannot be constructed.</summary>
    [UiExtension(Name = "Page naming", HostPlacement = HostPlacement.Application, Uri = PageNamingExtension.PageUri)]
    public sealed class PageNamingExtension : IApplicationUIExtension
    {
        internal const string PageUri = "pack://application:,,,/RFiDGear.Tests;component/Named.xaml";

        public PageNamingExtension()
        {
            Recorder.RecordConstruction();
            throw new InvalidOperationException("The constructor always fails.");
        }

        public static ActivationRecorder Recorder { get; } = new ActivationRecorder();

        public FrameworkElement CreateContent(IRFiDGearExtensionHost host) => new ProbeContent();
    }

    [UiExtension(Name = "Uri only", HostPlacement = HostPlacement.Application, Uri = UriOnlyExtension.PageUri)]
    public sealed class UriOnlyExtension : IUIExtension
    {
        internal const string PageUri = "pack://application:,,,/RFiDGear.Tests;component/Page.xaml";
    }

    [UiExtension(Name = "Blank uri", HostPlacement = HostPlacement.Application, Uri = "")]
    public sealed class BlankUriExtension : IUIExtension
    {
    }

    /// <summary>Names a page that does not exist; the window of the original opener is placed before it navigates.</summary>
    [UiExtension(Name = "Frame page", HostPlacement = HostPlacement.Application, Uri = FramePageExtension.PageUri)]
    public sealed class FramePageExtension : IUIExtension
    {
        internal const string PageUri = "file:///C:/RFiDGear-no-such-page.xaml";
    }
}
