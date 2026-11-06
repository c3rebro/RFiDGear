using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
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
        public async Task Open_WhenTheContentCannotBeShown_DisposesIt()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                ParentedContentFactory.Recorder.Reset();
                using var parts = new PartSet(typeof(ParentedContentFactory));
                var (launcher, errors, _) = CreateLauncher();

                var window = launcher.Open(parts.Single, new HeadlessExtensionHost());

                // The content already belongs to another element, so the window refuses it. A window that never
                // showed never raises Closed, so the launcher has to clean up itself.
                Assert.Null(window);
                Assert.Equal(new[] { "Parented content" }, errors);
                var created = Assert.Single(ParentedContentFactory.Recorder.Created);
                Assert.Equal(1, created.Content.DisposeCount);
                Assert.Equal(1, Assert.IsType<ProbeDataContext>(created.Content.DataContext).DisposeCount);
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

            var caption = Manifest.GetString("messageBoxExtensionOpenFailedCaption", culture);
            var message = Manifest.GetString("messageBoxExtensionOpenFailedMessage", culture);

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
                Manifest.GetString("messageBoxExtensionOpenFailedMessage", english),
                Manifest.GetString("messageBoxExtensionOpenFailedMessage", german));
            Assert.NotEqual(
                Manifest.GetString("messageBoxExtensionOpenFailedCaption", english),
                Manifest.GetString("messageBoxExtensionOpenFailedCaption", german));
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
        private int _disposeCount;

        public ProbeContent(bool throwOnDispose = false)
        {
            _throwOnDispose = throwOnDispose;
            DataContext = new ProbeDataContext();
        }

        public int DisposeCount => Volatile.Read(ref _disposeCount);

        public void Dispose()
        {
            Interlocked.Increment(ref _disposeCount);
            if (_throwOnDispose)
            {
                throw new InvalidOperationException("Key=00112233445566778899AABBCCDDEEFF");
            }
        }
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

    [UiExtension(Name = "Uri only", HostPlacement = HostPlacement.Application, Uri = UriOnlyExtension.PageUri)]
    public sealed class UriOnlyExtension : IUIExtension
    {
        public const string PageUri = "pack://application:,,,/RFiDGear.Tests;component/Page.xaml";
    }

    [UiExtension(Name = "Blank uri", HostPlacement = HostPlacement.Application, Uri = "")]
    public sealed class BlankUriExtension : IUIExtension
    {
    }
}
