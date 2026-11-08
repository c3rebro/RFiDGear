using Elatec.NET;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.FileAccess;
using RFiDGear.Infrastructure.ReaderProviders;
using RFiDGear.Services;
using RFiDGear.UI.MVVMDialogs.ViewModels.Interfaces;
using RFiDGear.ViewModel;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Resources;
using System.Threading.Tasks;
using Xunit;

namespace RFiDGear.Tests
{
    /// <summary>
    /// A reader whose COM port cannot be opened (another program or another RFiDGear window holds it) must not leave the
    /// reader marked busy, must not end in an unhandled exception, and must be told apart from a reader that is not
    /// attached. None of this touches a real reader: the readers here are doubles.
    /// </summary>
    [Collection("SettingsFileAccess")]
    public class ReaderConnectErrorHandlingTests
    {
        [Fact]
        public async Task AReaderWhosePortIsRefused_IsATransportError_NotAnException()
        {
            var provider = new PortRefusingProvider();

            var result = await provider.ConnectAsync();

            Assert.Equal(ERROR.TransportError, result);
            // The real library turns the refused port into an error that names itself NotOpen.
            Assert.Contains("NotOpen", provider.LastConnectError);
            Assert.False(provider.IsConnected);
        }

        [Fact]
        public async Task NoReaderAttached_IsATransportError_WithoutAReason()
        {
            var provider = new NoReaderProvider();

            var result = await provider.ConnectAsync();

            Assert.Equal(ERROR.TransportError, result);
            Assert.Null(provider.LastConnectError);
        }

        [Fact]
        public async Task TheMonitorRetrying_LogsOneLinePerDistinctFailure()
        {
            var provider = new PortRefusingProvider();

            using (var logs = CapturedLogs.Install())
            {
                await provider.ConnectAsync();
                await provider.ConnectAsync();
                await provider.ConnectAsync();

                Assert.Single(logs.From(nameof(ElatecNetProvider)).Where(e => e.Level == LogEventLevel.Warning));
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ConnectInTheSettingsDialog_AlwaysReleasesTheReader_AndSaysWhenItCannotBeOpened(bool providerThrows)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var directory = Directory.CreateTempSubdirectory("ReaderConnect").FullName;
                try
                {
                    using var settings = new SettingsReaderWriter(directory, loadSettings: false);
                    settings.DefaultSpecification.DefaultReaderProvider = ReaderTypes.Elatec;
                    var provider = providerThrows
                        ? new ScriptedConnectProvider { Throw = new InvalidOperationException("The reader failed.") }
                        : new ScriptedConnectProvider { Result = ERROR.TransportError, Failure = "ReaderException: NotOpen" };
                    var viewModel = new SetupViewModel(provider, settings, new ObservableCollection<IDialogViewModel>());
                    var busy = new List<bool>();
                    viewModel.OnUpdateReaderStatus = isBusy => busy.Add(isBusy);

                    await viewModel.ConnectToReaderCommand.ExecuteAsync(null);

                    // Marked busy, then released again, whatever happened in between.
                    Assert.Equal(new[] { true, false }, busy);
                    Assert.Contains(ResourceLoader.GetResource("labelReaderSetupReaderNotAccessible"), viewModel.ReaderStatus);
                    if (!providerThrows)
                    {
                        Assert.Contains("ReaderException: NotOpen", viewModel.ReaderStatus);
                        Assert.Equal(0, provider.ReadChipCalls);
                    }
                }
                finally
                {
                    Directory.Delete(directory, recursive: true);
                }
            });
        }

        [Fact]
        public async Task ConnectInTheSettingsDialog_DoesNotBlameThePort_OfAReaderThatIsOpenAfterAll()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var directory = Directory.CreateTempSubdirectory("ReaderConnect").FullName;
                try
                {
                    using var settings = new SettingsReaderWriter(directory, loadSettings: false);
                    settings.DefaultSpecification.DefaultReaderProvider = ReaderTypes.Elatec;
                    var provider = new ScriptedConnectProvider { Result = ERROR.TransportError, Failure = "ReaderException: version timed out", StaysConnected = true };
                    var viewModel = new SetupViewModel(provider, settings, new ObservableCollection<IDialogViewModel>());

                    await viewModel.ConnectToReaderCommand.ExecuteAsync(null);

                    // The port is open, so the chip is asked for and the port is not blamed.
                    Assert.Equal(1, provider.ReadChipCalls);
                    Assert.DoesNotContain(ResourceLoader.GetResource("labelReaderSetupReaderNotAccessible"), viewModel.ReaderStatus);
                }
                finally
                {
                    Directory.Delete(directory, recursive: true);
                }
            });
        }

        [Fact]
        public async Task TheMonitor_ShowsAReaderWhosePortIsRefused_AsNotAccessible()
        {
            var provider = new ScriptedConnectProvider { Result = ERROR.TransportError, Failure = "ReaderException: NotOpen" };

            var result = await new ReaderConnectionProbe().ProbeAsync(provider, ReaderTypes.Elatec, isReaderBusy: false);

            Assert.NotNull(result);
            Assert.True(result.ChangesBusy);
            Assert.Null(result.Busy);
            Assert.Equal("Elatec " + ResourceLoader.GetResource("labelReaderPortNotAccessible"), result.Label);
        }

        [Fact]
        public async Task TheMonitor_DoesNotBlameThePort_OfAReaderThatIsOpenAfterAll()
        {
            var provider = new ScriptedConnectProvider { Result = ERROR.TransportError, Failure = "ReaderException: version timed out", StaysConnected = true };

            var result = await new ReaderConnectionProbe().ProbeAsync(provider, ReaderTypes.Elatec, isReaderBusy: null);

            Assert.NotNull(result);
            Assert.Equal("Elatec", result.Label);
        }

        [Fact]
        public async Task TheMonitor_DoesNotTalkOfAComPort_ForAPcscReader()
        {
            var provider = new ScriptedConnectProvider { Throw = new InvalidOperationException("SCardSvr was stopped.") };

            var result = await new ReaderConnectionProbe().ProbeAsync(provider, ReaderTypes.PCSC, isReaderBusy: null);

            Assert.NotNull(result);
            Assert.Equal("PCSC", result.Label);
        }

        [Fact]
        public async Task TheMonitor_ShowsAReaderThatIsNotAttached_AsBefore()
        {
            var provider = new ScriptedConnectProvider { Result = ERROR.TransportError };

            var result = await new ReaderConnectionProbe().ProbeAsync(provider, ReaderTypes.Elatec, isReaderBusy: null);

            Assert.NotNull(result);
            Assert.True(result.ChangesBusy);
            Assert.Null(result.Busy);
            Assert.Equal("Elatec", result.Label);
        }

        [Fact]
        public async Task TheMonitor_SurvivesAReaderThatThrows_AndLogsItOnce()
        {
            var provider = new ScriptedConnectProvider { Throw = new IOException("The reader was removed.") };
            var probe = new ReaderConnectionProbe();

            using (var logs = CapturedLogs.Install())
            {
                var first = await probe.ProbeAsync(provider, ReaderTypes.Elatec, isReaderBusy: false);
                await probe.ProbeAsync(provider, ReaderTypes.Elatec, isReaderBusy: null);

                Assert.NotNull(first);
                Assert.True(first.ChangesBusy);
                Assert.Null(first.Busy);
                Assert.Equal("Elatec " + ResourceLoader.GetResource("labelReaderPortNotAccessible"), first.Label);
                Assert.Single(logs.From(nameof(ReaderConnectionProbe)).Where(e => e.Level == LogEventLevel.Warning));
            }
        }

        [Fact]
        public async Task TheMonitor_ShowsAConnectedReaderWithItsVersion_AndLeavesAConnectedOneAlone()
        {
            var provider = new ScriptedConnectProvider { Version = "TWN4 v1" };
            var probe = new ReaderConnectionProbe();

            var connecting = await probe.ProbeAsync(provider, ReaderTypes.Elatec, isReaderBusy: null);
            var connected = await probe.ProbeAsync(provider, ReaderTypes.Elatec, isReaderBusy: false);

            Assert.NotNull(connecting);
            Assert.True(connecting.ChangesBusy);
            Assert.False(connecting.Busy);
            Assert.Equal("Elatec TWN4 v1", connecting.Label);

            // Already connected: the label is refreshed, the busy state is not touched.
            Assert.NotNull(connected);
            Assert.False(connected.ChangesBusy);
            Assert.Equal("Elatec TWN4 v1", connected.Label);
        }

        [Fact]
        public async Task TheMonitor_DoesNothingWhileTheReaderIsBusy_OrThereIsNoReader()
        {
            var provider = new ScriptedConnectProvider();
            var probe = new ReaderConnectionProbe();

            Assert.Null(await probe.ProbeAsync(provider, ReaderTypes.Elatec, isReaderBusy: true));
            Assert.Null(await probe.ProbeAsync(null, ReaderTypes.Elatec, isReaderBusy: null));
            Assert.False(provider.IsConnected);
        }

        [Theory]
        [InlineData("labelReaderSetupReaderNotAccessible")]
        [InlineData("labelReaderPortNotAccessible")]
        public void TheNewTexts_ExistInBothLanguages(string key)
        {
            var resources = new ResourceManager("RFiDGear.Resources.Manifest", typeof(SetupViewModel).Assembly);

            var english = resources.GetString(key, new CultureInfo("en"));
            var german = resources.GetString(key, new CultureInfo("de"));

            Assert.False(string.IsNullOrWhiteSpace(english));
            Assert.False(string.IsNullOrWhiteSpace(german));
            Assert.NotEqual(english, german);
        }

        /// <summary>Finds one reader whose COM port cannot be opened, as when another program holds it.</summary>
        private sealed class PortRefusingProvider : ElatecNetProvider
        {
            protected override IEnumerable<TWN4ReaderDevice> EnumerateReaders() =>
                new[] { new TWN4ReaderDevice("COM-TEST", port => throw new UnauthorizedAccessException("Access to the port is denied.")) };
        }

        /// <summary>Finds no reader at all.</summary>
        private sealed class NoReaderProvider : ElatecNetProvider
        {
            protected override IEnumerable<TWN4ReaderDevice> EnumerateReaders() => Array.Empty<TWN4ReaderDevice>();
        }

        /// <summary>A reader that does what it is told when asked to connect, and reads no chip.</summary>
        private sealed class ScriptedConnectProvider : ElatecNetProvider
        {
            private bool connected;

            /// <summary>Gets or sets what connecting throws; nothing is thrown when it is <see langword="null"/>.</summary>
            public Exception? Throw { get; set; }

            /// <summary>Gets or sets the result of connecting.</summary>
            public ERROR Result { get; set; } = ERROR.NoError;

            /// <summary>Gets or sets the reason a failed connect reports.</summary>
            public string? Failure { get; set; }

            /// <summary>Gets or sets the version a successful connect reports.</summary>
            public string Version { get; set; } = "TWN4";

            /// <summary>Gets or sets whether the reader is open although connecting reports an error.</summary>
            public bool StaysConnected { get; set; }

            /// <summary>Gets how often the chip was asked for.</summary>
            public int ReadChipCalls { get; private set; }

            /// <inheritdoc />
            public override bool IsConnected => connected;

            /// <inheritdoc />
            public override Task<ERROR> ConnectAsync()
            {
                if (Throw != null)
                {
                    throw Throw;
                }

                LastConnectError = Failure;
                connected = Result == ERROR.NoError || StaysConnected;
                if (connected)
                {
                    ReaderUnitVersion = Version;
                }

                return Task.FromResult(Result);
            }

            /// <inheritdoc />
            public override Task<ERROR> ReadChipPublic()
            {
                ReadChipCalls++;

                return Task.FromResult(ERROR.NoError);
            }
        }
    }
}
