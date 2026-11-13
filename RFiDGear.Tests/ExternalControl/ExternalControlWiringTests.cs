#nullable enable
using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using RFiDGear.Bridge;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.FileAccess;
using RFiDGear.Models;
using RFiDGear.Services;
using RFiDGear.Services.ExternalControl;
using RFiDGear.Services.Interfaces;
using RFiDGear.ViewModel;

using Xunit;

namespace RFiDGear.Tests.ExternalControl
{
    /// <summary>
    /// How the main window switches the bridge on: the setting, the start-up argument (either one is enough), and what
    /// happens when the pipe cannot be opened. There is no main window in these tests, so the start-up of the window never
    /// runs: the bridge stays in its "not started yet" state, which is what is looked at.
    /// </summary>
    public class ExternalControlWiringTests
    {
        private sealed class ArgumentsStartupInitializer : IAppStartupInitializer
        {
            private readonly string[] _arguments;

            public ArgumentsStartupInitializer(string[] arguments) => _arguments = arguments;

            public AppStartupContext Initialize() => new AppStartupContext(new Mutex(), _arguments);
        }

        private static string NewPipeName() => "RFiDGear.Bridge.test-" + Guid.NewGuid().ToString("N");

        private static async Task<MainWindowViewModel> InitializeAsync(string pipeName, bool setting, params string[] arguments)
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "RFiDGear.Tests", Guid.NewGuid().ToString("N"));
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
                new ArgumentsStartupInitializer(arguments),
                new FakeTimerFactory(),
                new FakeTaskServiceInitializer(),
                new FakeMenuInitializer(),
                new FakeStartupArgumentProcessor(),
                new NoopUpdateScheduler(),
                new FakeReaderMonitor(),
                new FakeProjectBootstrapper(),
                () => new SettingsReaderWriter(settingsDirectory),
                () => new DatabaseReaderWriter(new ProjectManager(Path.Combine(tempRoot, "appdata"))),
                externalControlPipeName: pipeName);

            var initialization = viewModel.InitializeAsync();
            bootstrapper.Complete(new SettingsBootstrapResult
            {
                CurrentReaderName = "Reader",
                DefaultReaderProvider = ReaderTypes.None,
                Culture = CultureInfo.InvariantCulture,
                EnableExternalControl = setting,
            });
            await initialization;

            return viewModel;
        }

        private static async Task<bool> PipeExistsAsync(string pipeName)
        {
            try
            {
                using var client = await BridgeTestClient.ConnectAsync(pipeName, 400);

                return true;
            }
            catch (TimeoutException)
            {
                return false;
            }
        }

        [Fact]
        public async Task WithoutTheSettingAndTheArgument_NoPipeIsOpened()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var pipe = NewPipeName();

                var viewModel = await InitializeAsync(pipe, setting: false, "RFiDGear.exe", "AUTOMODE=1", "EXTERNALCONTROL=0");

                Assert.Null(viewModel.ExternalControl);
                Assert.False(await PipeExistsAsync(pipe));
                Assert.False(viewModel.IsExternalControlConnected);
            });
        }

        [Theory]
        [InlineData(true, new string[0])]
        [InlineData(false, new[] { "EXTERNALCONTROL=1" })]
        [InlineData(false, new[] { "externalcontrol=1" })]
        [InlineData(true, new[] { "EXTERNALCONTROL=0" })]
        [InlineData(true, new[] { "EXTERNALCONTROL=1" })]
        public async Task TheSettingOrTheArgument_IsEnough(bool setting, string[] arguments)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var pipe = NewPipeName();
                var viewModel = await InitializeAsync(pipe, setting, arguments);

                try
                {
                    Assert.NotNull(viewModel.ExternalControl);
                    Assert.Equal(BridgeServerState.Listening, viewModel.ExternalControl!.ServerState);
                    Assert.True(await PipeExistsAsync(pipe));
                }
                finally
                {
                    viewModel.ExternalControl?.Dispose();
                }
            });
        }

        [Fact]
        public async Task BeforeTheStartUpOfTheWindowHasRun_TheClientSeesIt_AndTheStatusBarFollowsTheConnection()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var pipe = NewPipeName();
                var viewModel = await InitializeAsync(pipe, setting: true);

                try
                {
                    using var client = await BridgeTestClient.ConnectAsync(pipe, 5000);
                    await client.HelloAsync();

                    var status = await client.Service.GetStatus(new GetStatusRequest());

                    Assert.False(status.StartupComplete);
                    Assert.True(status.UiResponsive);

                    // The window shows the item while a program is connected, and hides it when it has left.
                    await WaitUntilAsync(() => viewModel.IsExternalControlConnected);
                    client.Dispose();
                    await WaitUntilAsync(() => !viewModel.IsExternalControlConnected);
                }
                finally
                {
                    viewModel.ExternalControl?.Dispose();
                }
            });
        }

        [Fact]
        public async Task APipeThatCannotBeOpened_DoesNotKeepTheApplicationFromStarting()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var pipe = NewPipeName();
                using var other = new BridgeServer(pipe, new BridgeServiceOptions());
                Assert.True(other.Start());

                var viewModel = await InitializeAsync(pipe, setting: true);

                Assert.Null(viewModel.ExternalControl);
                Assert.Equal("Reader", viewModel.CurrentReader);
            });
        }

        [Fact]
        public async Task TheStartUp_RunsOnce_WhicheverTriggerComesFirst_AndThenTheClientSeesItIsComplete()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var pipe = NewPipeName();
                var viewModel = await InitializeAsync(pipe, setting: true);

                try
                {
                    using var client = await BridgeTestClient.ConnectAsync(pipe, 5000);
                    await client.HelloAsync();
                    var runs = 0;
                    Task Startup()
                    {
                        runs++;

                        return Task.CompletedTask;
                    }

                    Assert.False((await client.Service.GetStatus(new GetStatusRequest())).StartupComplete);

                    var first = await viewModel.RunStartupOnceAsync(Startup);
                    var second = await viewModel.RunStartupOnceAsync(Startup);

                    Assert.True(first);
                    Assert.False(second);
                    Assert.Equal(1, runs);
                    Assert.True((await client.Service.GetStatus(new GetStatusRequest())).StartupComplete);
                }
                finally
                {
                    viewModel.ExternalControl?.Dispose();
                }
            });
        }

        [Fact]
        public async Task AStartUpThatFails_StillEndsTheWaitOfTheClient()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var pipe = NewPipeName();
                var viewModel = await InitializeAsync(pipe, setting: true);

                try
                {
                    using var client = await BridgeTestClient.ConnectAsync(pipe, 5000);
                    await client.HelloAsync();

                    await Assert.ThrowsAsync<InvalidOperationException>(() => viewModel.RunStartupOnceAsync(() => throw new InvalidOperationException("The start-up failed.")));

                    Assert.True((await client.Service.GetStatus(new GetStatusRequest())).StartupComplete);
                }
                finally
                {
                    viewModel.ExternalControl?.Dispose();
                }
            });
        }

        [Fact]
        public async Task WithTheBridgeOff_TheStartUpStillRunsOnce()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var viewModel = await InitializeAsync(NewPipeName(), setting: false);
                var runs = 0;
                Task Startup()
                {
                    runs++;

                    return Task.CompletedTask;
                }

                Assert.True(await viewModel.RunStartupOnceAsync(Startup));
                Assert.False(await viewModel.RunStartupOnceAsync(Startup));
                Assert.Equal(1, runs);
            });
        }

        // ── The fallback for a window that is never activated ──

        private sealed class FakeWindow
        {
            public bool Drawn { get; set; }

            public int Subscriptions { get; private set; }

            public int Unsubscriptions { get; private set; }

            public EventHandler? Handler { get; private set; }

            public void Subscribe(EventHandler handler)
            {
                Subscriptions++;
                Handler = handler;
            }

            public void Unsubscribe(EventHandler handler)
            {
                Unsubscriptions++;

                if (ReferenceEquals(Handler, handler))
                {
                    Handler = null;
                }
            }

            public void RaiseDrawn() => Handler?.Invoke(this, EventArgs.Empty);
        }

        private static readonly TimeSpan ShortDelay = TimeSpan.FromMilliseconds(60);

        [Fact]
        public async Task WithTheBridgeOff_TheFallbackIsNotArmed()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var viewModel = await InitializeAsync(NewPipeName(), setting: false);
                var window = new FakeWindow { Drawn = true };
                var runs = 0;

                var armed = viewModel.ArmStartupFallback(() => window.Drawn, window.Subscribe, window.Unsubscribe, () => runs++, ShortDelay);
                await Task.Delay(300);

                Assert.False(armed);
                Assert.Equal(0, window.Subscriptions);
                Assert.Equal(0, runs);
            });
        }

        [Fact]
        public async Task AWindowThatIsDrawnButNeverActivated_IsStartedByTheFallback_Once_AndTheClientSeesIt()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var pipe = NewPipeName();
                var viewModel = await InitializeAsync(pipe, setting: true);

                try
                {
                    using var client = await BridgeTestClient.ConnectAsync(pipe, 5000);
                    await client.HelloAsync();
                    var window = new FakeWindow { Drawn = true };
                    var startups = 0;
                    Task Startup()
                    {
                        startups++;

                        return Task.CompletedTask;
                    }

                    var armed = viewModel.ArmStartupFallback(() => window.Drawn, window.Subscribe, window.Unsubscribe, () => _ = viewModel.RunStartupOnceAsync(Startup), ShortDelay);
                    await WaitUntilAsync(() => startups == 1);

                    // The activation that comes late finds the start-up done.
                    var late = await viewModel.RunStartupOnceAsync(Startup);

                    Assert.True(armed);
                    Assert.False(late);
                    Assert.Equal(1, startups);
                    Assert.True((await client.Service.GetStatus(new GetStatusRequest())).StartupComplete);
                }
                finally
                {
                    viewModel.ExternalControl?.Dispose();
                }
            });
        }

        [Fact]
        public async Task AWindowThatIsNotDrawnYet_WaitsForTheDrawing_ThenForTheDelay_AndLetsGoOfTheEvent()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var viewModel = await InitializeAsync(NewPipeName(), setting: true);

                try
                {
                    var window = new FakeWindow { Drawn = false };
                    var runs = 0;

                    viewModel.ArmStartupFallback(() => window.Drawn, window.Subscribe, window.Unsubscribe, () => runs++, ShortDelay);
                    await Task.Delay(300);

                    Assert.Equal(1, window.Subscriptions);
                    Assert.Equal(0, runs);

                    window.RaiseDrawn();
                    await WaitUntilAsync(() => runs == 1);

                    Assert.Equal(1, window.Unsubscriptions);
                }
                finally
                {
                    viewModel.ExternalControl?.Dispose();
                }
            });
        }

        [Fact]
        public async Task WhenTheActivationComesFirst_TheFallbackDoesNotStartAnything()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var viewModel = await InitializeAsync(NewPipeName(), setting: true);

                try
                {
                    var window = new FakeWindow { Drawn = true };
                    var fallbackRuns = 0;

                    viewModel.ArmStartupFallback(() => window.Drawn, window.Subscribe, window.Unsubscribe, () => fallbackRuns++, TimeSpan.FromMilliseconds(200));
                    var activation = await viewModel.RunStartupOnceAsync(() => Task.CompletedTask);
                    await Task.Delay(500);

                    Assert.True(activation);
                    Assert.Equal(0, fallbackRuns);
                }
                finally
                {
                    viewModel.ExternalControl?.Dispose();
                }
            });
        }

        private static async Task WaitUntilAsync(Func<bool> condition)
        {
            var until = DateTime.UtcNow.AddSeconds(5);

            while (!condition())
            {
                Assert.True(DateTime.UtcNow < until, "The condition did not become true in time.");
                await Task.Delay(10);
            }
        }
    }
}
