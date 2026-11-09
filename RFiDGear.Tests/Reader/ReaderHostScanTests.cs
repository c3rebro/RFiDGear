using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using RFiDGear.Contracts;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.ExtensionHost;
using RFiDGear.Models;

using Xunit;

namespace RFiDGear.Tests.Reader
{
    /// <summary>
    /// The scan of the reader host, with a scripted reader and no hardware: what it reads and when it refuses, what it
    /// publishes, and when it raises the event. Reads are held with gates and waited for with tasks, never with sleeps.
    /// </summary>
    public class ReaderHostScanTests : IDisposable
    {
        private static readonly TimeSpan Patience = TimeSpan.FromSeconds(20);

        private readonly ScriptedReadProvider _provider = new ScriptedReadProvider();
        private readonly ExtensionOperationCoordinator _coordinator = new ExtensionOperationCoordinator();
        private readonly ReaderAccessGate _gate = new ReaderAccessGate();
        private readonly ReaderDeviceScope _scope;
        private readonly ReaderHostImpl _host;
        private readonly List<string> _log = new List<string>();
        private string _readerName = "Elatec";

        public ReaderHostScanTests()
        {
            _scope = ReaderDeviceScope.Use(_provider);
            _host = new ReaderHostImpl(() => _readerName, _coordinator, _gate);
            _host.ReaderStateChanged += (_, _) =>
            {
                lock (_log)
                {
                    _log.Add("event");
                }
            };
        }

        private int Events
        {
            get
            {
                lock (_log)
                {
                    return _log.Count(entry => entry == "event");
                }
            }
        }

        public void Dispose()
        {
            _scope.Dispose();
        }

        private static GenericChipModel Chip(string uid, CARD_TYPE type = CARD_TYPE.Mifare1K, params GenericChipModel[] children) =>
            new GenericChipModel(uid, type) { Childs = children.Length == 0 ? null! : children.ToList() };

        private static async Task Within(Task task)
        {
            var finished = await Task.WhenAny(task, Task.Delay(Patience));
            Assert.Same(task, finished);
            await task;
        }

        private static TaskCompletionSource<bool> NewGate() => new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        [Fact]
        public async Task AScan_ReadsTheCard_AndReturnsItInTheStateReady()
        {
            _provider.Script(ERROR.NoError, Chip("04aa", CARD_TYPE.Mifare1K, Chip("beef", CARD_TYPE.LEGIC)));

            var snapshot = await _host.ScanAsync();

            Assert.Equal(("Elatec", ReaderScanState.Ready), (snapshot.ReaderName, snapshot.State));
            Assert.Equal(new[] { "04AA", "BEEF" }, snapshot.Credentials.Select(c => c.Uid));
            Assert.Equal(1, _provider.ReadCount);
            Assert.Equal(snapshot, _host.CurrentSnapshot);
        }

        [Fact]
        public async Task AScan_ResetsTheChipFirst_SoAGoneCardAndItsChildrenAreNotSeenAgain()
        {
            _provider.GenericChip = Chip("04aa", CARD_TYPE.Mifare1K, Chip("beef", CARD_TYPE.LEGIC));
            _provider.Script(ERROR.NoError);

            var snapshot = await _host.ScanAsync();

            Assert.True(Assert.Single(_provider.ChipWasEmptyAtStart));
            Assert.Equal(ReaderScanState.Ready, snapshot.State);
            Assert.False(snapshot.HasCard);
        }

        [Fact]
        public async Task WithoutAReaderDevice_TheScanSaysNoReader_AndReadsNothing()
        {
            using var none = ReaderDeviceScope.Use(null);

            var snapshot = await _host.ScanAsync();

            Assert.Equal(("Elatec", ReaderScanState.NoReader), (snapshot.ReaderName, snapshot.State));
            Assert.Equal(0, _provider.ReadCount);
        }

        [Fact]
        public async Task AReaderThatIsNotConnected_IsNoReader_WhateverTheReadReturned()
        {
            _provider.Connected = false;
            _provider.Script(ERROR.NoError);

            var snapshot = await _host.ScanAsync();

            Assert.Equal(ReaderScanState.NoReader, snapshot.State);
            Assert.Equal(ReaderScanState.NoReader, _host.CurrentSnapshot.State);
        }

        [Theory]
        [InlineData("load")]
        [InlineData("run")]
        [InlineData("edit")]
        [InlineData("scan")]
        [InlineData("gate")]
        public async Task WhileTheHostIsBusy_TheScanSaysBusy_ReadsNothing_AndChangesNothing(string busyWith)
        {
            _provider.Script(ERROR.NoError, Chip("04aa"));
            await _host.ScanAsync();
            var before = _host.CurrentSnapshot;
            var events = Events;
            _provider.Script(ERROR.NoError, Chip("04bb"));
            var held = busyWith switch
            {
                "load" => _coordinator.TryBeginLoad(),
                "run" => _coordinator.TryBeginExecution(),
                "edit" => _coordinator.TryBeginProjectOperation(),
                "scan" => _coordinator.TryBeginScan(),
                _ => _gate.TryEnter(),
            };
            Assert.True(held);

            var snapshot = await _host.ScanAsync();

            Assert.Equal(("Elatec", ReaderScanState.Busy), (snapshot.ReaderName, snapshot.State));
            Assert.Empty(snapshot.Credentials);
            Assert.Equal(1, _provider.ReadCount);
            Assert.Equal(before, _host.CurrentSnapshot);
            Assert.Equal(events, Events);
        }

        [Fact]
        public async Task AScanThatIsCancelledBeforeItStarts_IsCancelled_ReadsNothing_AndHoldsNothing()
        {
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _host.ScanAsync(cancelled.Token));

            Assert.Equal(0, _provider.ReadCount);
            Assert.True(_coordinator.TryBeginLoad());
            _coordinator.EndLoad();
            Assert.True(_gate.TryEnter());
            _gate.Exit();
        }

        [Fact]
        public async Task WhileItReads_AScanHoldsTheSlotAndTheGate_AndNeitherAfterwards()
        {
            var hold = NewGate();
            _provider.Script(ERROR.NoError, Chip("04aa"), hold.Task);

            var scan = _host.ScanAsync();
            await Within(_provider.ReadStarted(1));

            Assert.True(_coordinator.IsScanning);
            Assert.True(_gate.IsHeld);
            Assert.False(_coordinator.TryBeginLoad());
            Assert.False(_coordinator.TryBeginExecution());

            hold.SetResult(true);
            await Within(scan);

            Assert.False(_coordinator.IsScanning);
            Assert.False(_gate.IsHeld);
            Assert.True(_coordinator.TryBeginLoad());
            _coordinator.EndLoad();
        }

        [Fact]
        public async Task AScanThatIsStartedWhileAnotherReads_SaysBusy_AndTheFirstIsNotDisturbed()
        {
            var hold = NewGate();
            _provider.Script(ERROR.NoError, Chip("04aa"), hold.Task);
            var first = _host.ScanAsync();
            await Within(_provider.ReadStarted(1));

            var second = await _host.ScanAsync();
            hold.SetResult(true);
            var firstSnapshot = await first;

            Assert.Equal(ReaderScanState.Busy, second.State);
            Assert.Equal(1, _provider.ReadCount);
            Assert.Equal("04AA", Assert.Single(firstSnapshot.Credentials).Uid);
        }

        [Theory]
        [InlineData("throws")]
        [InlineData("transport")]
        public async Task AReadThatFails_IsReadFailed_KeepsTheChipOfBefore_PublishesNothing_AndReleasesEverything(string how)
        {
            _provider.Script(ERROR.NoError, Chip("04aa"));
            await _host.ScanAsync();
            var chipBefore = _provider.GenericChip;
            var published = _host.CurrentSnapshot;
            var events = Events;
            if (how == "throws")
            {
                _provider.Script(ERROR.NoError, failure: new InvalidOperationException("The reader failed."));
            }
            else
            {
                _provider.Script(ERROR.TransportError);
            }

            var snapshot = await _host.ScanAsync();

            Assert.Equal(ReaderScanState.ReadFailed, snapshot.State);
            Assert.Empty(snapshot.Credentials);
            Assert.Same(chipBefore, _provider.GenericChip);
            Assert.Equal(published, _host.CurrentSnapshot);
            Assert.Equal(events, Events);
            Assert.False(_coordinator.IsScanning);
            Assert.False(_gate.IsHeld);
        }

        [Fact]
        public async Task TheEvent_IsRaisedForAnArrival_AReplacement_AndARemoval_ButNotForARepeat()
        {
            _provider.Script(ERROR.NoError, Chip("04aa"));
            _provider.Script(ERROR.NoError, Chip("04aa"));
            _provider.Script(ERROR.NoError, Chip("04aa", CARD_TYPE.Mifare1K, Chip("beef", CARD_TYPE.LEGIC)));
            _provider.Script(ERROR.NoError, Chip("04bb"));
            _provider.Script(ERROR.NoError, new GenericChipModel());
            _provider.Script(ERROR.NoError, new GenericChipModel());

            await _host.ScanAsync();
            Assert.Equal(1, Events);
            await _host.ScanAsync();
            Assert.Equal(1, Events);
            await _host.ScanAsync();
            Assert.Equal(2, Events);
            await _host.ScanAsync();
            Assert.Equal(3, Events);
            await _host.ScanAsync();
            Assert.Equal(4, Events);
            await _host.ScanAsync();
            Assert.Equal(4, Events);
        }

        [Fact]
        public async Task TheFirstScan_AlwaysRaises_EvenWithoutACard_BecauseNothingWasKnownBefore()
        {
            _provider.Script(ERROR.NoError, new GenericChipModel());

            await _host.ScanAsync();

            Assert.Equal(1, Events);
        }

        [Fact]
        public async Task TheEvent_IsRaisedAfterTheSlotIsReleased_SoASubscriberCanStartAnOperation()
        {
            bool? admitted = null;
            _host.ReaderStateChanged += (_, _) =>
            {
                admitted = _coordinator.TryBeginLoad();
                if (admitted == true)
                {
                    _coordinator.EndLoad();
                }
            };
            _provider.Script(ERROR.NoError, Chip("04aa"));

            await _host.ScanAsync();

            Assert.True(admitted);
        }

        [Fact]
        public async Task ASubscriberThatThrows_DoesNotBreakTheOthers_NorTheScan()
        {
            _host.ReaderStateChanged += (_, _) => throw new InvalidOperationException("A subscriber failed.");
            var seen = 0;
            _host.ReaderStateChanged += (_, _) => seen++;
            _provider.Script(ERROR.NoError, Chip("04aa"));

            var snapshot = await _host.ScanAsync();

            Assert.Equal(ReaderScanState.Ready, snapshot.State);
            Assert.Equal(1, seen);
        }

        [Fact]
        public async Task AChangeOfTheReader_ForgetsThePublishedPicture_AndRaisesOnce()
        {
            _provider.Script(ERROR.NoError, Chip("04aa"));
            await _host.ScanAsync();
            Assert.True(_host.CurrentSnapshot.HasCard);
            var events = Events;

            // The new reader has nothing on it; the live view shows that, the old published picture would not.
            _provider.GenericChip = new GenericChipModel();
            _readerName = "Other reader";
            _host.NotifyReaderNameChanged();

            Assert.Equal(events + 1, Events);
            Assert.False(_host.CurrentSnapshot.HasCard);
            Assert.Equal("Other reader", _host.CurrentSnapshot.ReaderName);
        }

        [Fact]
        public async Task AScanThatWasOnItsWayWhenTheReaderChanged_IsAnsweredButNotPublishedNorAnnounced()
        {
            var hold = NewGate();
            _provider.Script(ERROR.NoError, Chip("04aa"), hold.Task);
            var scan = _host.ScanAsync();
            await Within(_provider.ReadStarted(1));

            _provider.GenericChip = new GenericChipModel();
            _host.NotifyReaderNameChanged();
            var eventsAfterTheChange = Events;
            hold.SetResult(true);
            var snapshot = await scan;

            Assert.Equal("04AA", Assert.Single(snapshot.Credentials).Uid);
            Assert.Equal(eventsAfterTheChange, Events);
        }

        [Fact]
        public async Task AChangeOfTheReader_BetweenTheStoreAndTheAnnouncement_LeavesTheEventToTheChangeOfTheReader()
        {
            _provider.Script(ERROR.NoError, Chip("04aa"));
            _host.BeforeAnnounce = () => _host.NotifyReaderNameChanged();

            var snapshot = await _host.ScanAsync();

            Assert.Equal("04AA", Assert.Single(snapshot.Credentials).Uid);
            Assert.Equal(1, Events);
        }

        [Fact]
        public async Task TheSnapshot_NeverPairsTheCardOfTheOldReaderWithTheNameOfTheNewOne()
        {
            ReaderHostImpl? host = null;
            var current = "Old reader";
            var armed = false;
            host = new ReaderHostImpl(
                () =>
                {
                    if (armed)
                    {
                        // The reader changes while the snapshot is being made.
                        armed = false;
                        current = "New reader";
                        host!.NotifyReaderNameChanged();
                    }

                    return current;
                },
                _coordinator,
                _gate);
            _provider.Script(ERROR.NoError, Chip("04aa"));
            await host.ScanAsync();
            _provider.GenericChip = new GenericChipModel();
            armed = true;

            var snapshot = host.CurrentSnapshot;

            Assert.False(snapshot.HasCard);
            Assert.Equal("New reader", snapshot.ReaderName);
        }

        [Fact]
        public async Task ThePublishedPicture_CarriesTheNameTheReaderHasNow()
        {
            _provider.Script(ERROR.NoError, Chip("04aa"));
            await _host.ScanAsync();

            _readerName = "Renamed";

            Assert.Equal("Renamed", _host.CurrentSnapshot.ReaderName);
            Assert.True(_host.CurrentSnapshot.HasCard);
        }

        [Fact]
        public async Task AScanThatIsCancelledWhileItReads_IsNotInterrupted_AndReturnsWhatItRead()
        {
            var hold = NewGate();
            _provider.Script(ERROR.NoError, Chip("04aa"), hold.Task);
            using var cancel = new CancellationTokenSource();
            var scan = _host.ScanAsync(cancel.Token);
            await Within(_provider.ReadStarted(1));

            cancel.Cancel();
            hold.SetResult(true);
            await Within(scan);

            Assert.Equal("04AA", Assert.Single((await scan).Credentials).Uid);
        }

        [Fact]
        public async Task TheRead_RunsOffTheThreadOfTheCaller()
        {
            _provider.Script(ERROR.NoError, Chip("04aa"));

            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var caller = Environment.CurrentManagedThreadId;

                await _host.ScanAsync();

                Assert.NotEqual(caller, _provider.LastReadThreadId);
            });
        }

        [Fact]
        public async Task APcscReader_CountsATransportErrorAsNoCard_WhateverItsConnectionAndOldChipSay()
        {
            _provider.Pcsc = true;
            _provider.Connected = false;
            _provider.GenericChip = Chip("04aa");
            _provider.Script(ERROR.TransportError);

            var snapshot = await _host.ScanAsync();

            Assert.Equal(ReaderScanState.Ready, snapshot.State);
            Assert.False(snapshot.HasCard);
            Assert.Equal(1, Events);
        }

        [Fact]
        public async Task TheSnapshot_IsWhatTheLastScanFound_NotWhatTheSharedChipSaysNow()
        {
            _provider.Script(ERROR.NoError, Chip("04aa"));
            await _host.ScanAsync();

            _provider.GenericChip = Chip("04bb");
            _provider.Connected = false;

            Assert.Equal(ReaderScanState.Ready, _host.CurrentSnapshot.State);
            Assert.Equal("04AA", Assert.Single(_host.CurrentSnapshot.Credentials).Uid);
        }

        [Fact]
        public async Task ANoReaderAnswer_IsPublishedToo_AndAnnouncedOnlyWhenItChangesThePicture()
        {
            _provider.Connected = false;
            _provider.Script(ERROR.NoError);
            _provider.Script(ERROR.NoError);
            _provider.Script(ERROR.NoError, new GenericChipModel());

            await _host.ScanAsync();
            Assert.Equal(1, Events);
            await _host.ScanAsync();
            Assert.Equal(1, Events);

            _provider.Connected = true;
            Assert.Equal(ReaderScanState.NoReader, _host.CurrentSnapshot.State);
            await _host.ScanAsync();
            Assert.Equal(2, Events);
            Assert.Equal(ReaderScanState.Ready, _host.CurrentSnapshot.State);
        }

        [Fact]
        public async Task AfterAChangeOfTheReader_TheFirstScanRaisesAgain_EvenForTheSameCard()
        {
            _provider.Script(ERROR.NoError, Chip("04aa"));
            _provider.Script(ERROR.NoError, Chip("04aa"));
            await _host.ScanAsync();
            _host.NotifyReaderNameChanged();
            var events = Events;

            await _host.ScanAsync();

            Assert.Equal(events + 1, Events);
        }

        [Fact]
        public async Task ANameThatCannotBeRead_IsNoObstacleToAScan_NorToTheSnapshot()
        {
            var host = new ReaderHostImpl(() => throw new InvalidOperationException("The name cannot be read."), _coordinator, _gate);
            _provider.Script(ERROR.NoError, Chip("04aa"));

            var snapshot = await host.ScanAsync();

            Assert.Equal(string.Empty, snapshot.ReaderName);
            Assert.Equal("04AA", Assert.Single(snapshot.Credentials).Uid);
            Assert.Equal(string.Empty, host.CurrentSnapshot.ReaderName);
        }

        [Fact]
        public void BeforeAnythingWasRead_TheSnapshotIsTheLiveView()
        {
            _provider.GenericChip = Chip("04aa");

            var snapshot = _host.CurrentSnapshot;

            Assert.Equal(ReaderScanState.Ready, snapshot.State);
            Assert.Equal("04AA", Assert.Single(snapshot.Credentials).Uid);
        }

        [Fact]
        public async Task WithoutACoordinatorAndAGate_AScanStillReads()
        {
            var bare = new ReaderHostImpl("Elatec");
            _provider.Script(ERROR.NoError, Chip("04aa"));

            var snapshot = await bare.ScanAsync();

            Assert.Equal("04AA", Assert.Single(snapshot.Credentials).Uid);
        }
    }
}
