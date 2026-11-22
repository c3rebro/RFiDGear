using System;
using System.Threading.Tasks;

using RFiDGear.Services.Licensing;

using Xunit;

namespace RFiDGear.Tests
{
    /// <summary>
    /// What the program does about the license of the Store edition: it fails open, it never cuts a run off, and a trial that has
    /// ended refuses new runs, stops automatic mode and asks the person once.
    /// </summary>
    public sealed class LicenseGateTests
    {
        private sealed class RecordingEnforcement : ILicenseEnforcement
        {
            public bool RunInProgress { get; set; }

            public bool? Blocked { get; private set; }

            public int AutoModeStops { get; private set; }

            public int EndShown { get; private set; }

            public bool IsRunInProgress => RunInProgress;

            public void SetNewRunsBlocked(bool blocked) => Blocked = blocked;

            public void StopAutomaticMode() => AutoModeStops++;

            public void ShowTrialEnded() => EndShown++;
        }

        private sealed class Rig
        {
            public Rig(LicenseSnapshot snapshot)
            {
                Service = new FakeLicenseService(snapshot);
                Gate = new LicenseGate(Service, Host, () => Now);
            }

            public DateTimeOffset Now { get; set; } = new DateTimeOffset(2026, 12, 1, 9, 0, 0, TimeSpan.Zero);

            public FakeLicenseService Service { get; }

            public RecordingEnforcement Host { get; } = new RecordingEnforcement();

            public LicenseGate Gate { get; }
        }

        [Fact]
        public async Task AFullLicense_BlocksNothingAndShowsNothing()
        {
            var rig = new Rig(LicenseSnapshot.Full);

            await rig.Gate.StartAsync();

            Assert.False(rig.Host.Blocked);
            Assert.False(rig.Gate.IsTrialRunning);
            Assert.False(rig.Gate.IsExpired);
            Assert.Equal(0, rig.Host.EndShown);
        }

        [Fact]
        public async Task ARunningTrial_CountsItsDays_AndBlocksNothing()
        {
            var rig = new Rig(LicenseSnapshot.Unknown);
            rig.Service.Change(LicenseSnapshot.Trial(rig.Now.AddDays(12)));

            await rig.Gate.StartAsync();

            Assert.True(rig.Gate.IsTrialRunning);
            Assert.Equal(12, rig.Gate.DaysLeft);
            Assert.False(rig.Host.Blocked);
            Assert.Equal(0, rig.Host.EndShown);
        }

        [Fact]
        public async Task ATrialThatHasEnded_RefusesNewRuns_StopsAutomaticMode_AndAsksOnce()
        {
            var rig = new Rig(LicenseSnapshot.Expired);

            await rig.Gate.StartAsync();
            await rig.Gate.TickAsync();
            await rig.Gate.TickAsync();

            Assert.True(rig.Host.Blocked);
            Assert.True(rig.Gate.IsExpired);
            Assert.True(rig.Host.AutoModeStops >= 1);
            Assert.Equal(1, rig.Host.EndShown);
        }

        [Fact]
        public async Task ATrialThatEndsDuringARun_IsNotAskedAboutUntilTheRunIsOver()
        {
            var rig = new Rig(LicenseSnapshot.Expired);
            rig.Host.RunInProgress = true;

            await rig.Gate.StartAsync();

            Assert.True(rig.Host.Blocked);
            Assert.Equal(0, rig.Host.EndShown);

            rig.Host.RunInProgress = false;
            await rig.Gate.TickAsync();
            await rig.Gate.TickAsync();

            Assert.Equal(1, rig.Host.EndShown);
        }

        [Fact]
        public async Task ATrialThatEndsByTheClock_IsNoticedAtTheNextTick()
        {
            var rig = new Rig(LicenseSnapshot.Unknown);
            rig.Service.Change(LicenseSnapshot.Trial(rig.Now.AddHours(2)));
            await rig.Gate.StartAsync();
            Assert.False(rig.Host.Blocked);

            rig.Now = rig.Now.AddHours(3);
            await rig.Gate.TickAsync();

            Assert.True(rig.Host.Blocked);
            Assert.Equal(1, rig.Host.EndShown);
        }

        [Fact]
        public async Task ALicenseThatCannotBeRead_CountsAsLicensed_AndIsReadAgainLater()
        {
            var rig = new Rig(LicenseSnapshot.Expired);
            rig.Service.FailWith = new InvalidOperationException("the Store is not reachable");

            await rig.Gate.StartAsync();

            Assert.False(rig.Host.Blocked);
            Assert.Equal(0, rig.Host.EndShown);
            Assert.Equal(1, rig.Service.Reads);

            // Not before the retry interval ...
            rig.Now = rig.Now.AddMinutes(5);
            await rig.Gate.TickAsync();
            Assert.Equal(1, rig.Service.Reads);

            // ... and then the answer comes.
            rig.Service.FailWith = null;
            rig.Now = rig.Now.Add(LicenseGate.RetryInterval);
            await rig.Gate.TickAsync();

            Assert.Equal(2, rig.Service.Reads);
            Assert.True(rig.Host.Blocked);
        }

        [Fact]
        public async Task AFailedReadAfterTheTrialEnded_DoesNotLiftTheEnd()
        {
            var rig = new Rig(LicenseSnapshot.Expired);
            await rig.Gate.StartAsync();
            Assert.True(rig.Host.Blocked);

            rig.Service.FailWith = new InvalidOperationException("the Store is not reachable");
            rig.Now = rig.Now.Add(LicenseGate.RereadInterval);
            await rig.Gate.TickAsync();

            Assert.True(rig.Host.Blocked);
            Assert.True(rig.Gate.IsExpired);
        }

        [Fact]
        public async Task TheStoreTellingThatTheTrialEnded_IsAppliedAtOnce()
        {
            var rig = new Rig(LicenseSnapshot.Unknown);
            rig.Service.Change(LicenseSnapshot.Trial(rig.Now.AddDays(1)));
            await rig.Gate.StartAsync();
            Assert.False(rig.Host.Blocked);

            rig.Service.Change(LicenseSnapshot.Expired);

            Assert.True(rig.Host.Blocked);
            Assert.Equal(1, rig.Host.EndShown);
        }

        [Fact]
        public async Task APurchase_LiftsTheEnd_AndTheNextEndIsToldAgain()
        {
            var rig = new Rig(LicenseSnapshot.Expired);
            await rig.Gate.StartAsync();

            var bought = await rig.Gate.PurchaseAsync(IntPtr.Zero);

            Assert.True(bought);
            Assert.Equal(1, rig.Service.Purchases);
            Assert.False(rig.Host.Blocked);
            Assert.False(rig.Gate.IsExpired);

            rig.Service.Change(LicenseSnapshot.Expired);
            Assert.Equal(2, rig.Host.EndShown);
        }

        [Fact]
        public async Task AFailedPurchase_ChangesNothing()
        {
            // The purchase of the fake always works, so this one uses a service whose purchase throws (the person closed the Store).
            var host = new RecordingEnforcement();
            var gate = new LicenseGate(new RefusingService(), host, () => new DateTimeOffset(2026, 12, 1, 9, 0, 0, TimeSpan.Zero));
            await gate.StartAsync();

            Assert.False(await gate.PurchaseAsync(IntPtr.Zero));
            Assert.True(host.Blocked);
            Assert.True(gate.IsExpired);
        }

        [Theory]
        [InlineData(12, 0, 12)]
        [InlineData(11, 1, 12)]
        [InlineData(0, 1, 1)]
        [InlineData(0, 0, 0)]
        public void TheDaysOfATrial_AreCountedUp_ADayThatHasStartedIsADay(int days, int hours, int expected)
        {
            var now = new DateTimeOffset(2026, 12, 1, 9, 0, 0, TimeSpan.Zero);
            var snapshot = LicenseSnapshot.Trial(now.AddDays(days).AddHours(hours));

            Assert.Equal(expected, snapshot.DaysLeftAt(now));
            Assert.Equal(expected == 0, snapshot.IsExpiredAt(now));
        }

        [Theory]
        [InlineData("full", LicenseKind.Full)]
        [InlineData("FULL", LicenseKind.Full)]
        [InlineData("expired", LicenseKind.Expired)]
        [InlineData("trial:12", LicenseKind.Trial)]
        [InlineData(" Trial:3 ", LicenseKind.Trial)]
        public void TheDebugSwitch_NamesALicense(string text, LicenseKind expected)
        {
            var snapshot = FakeLicenseSetting.Parse(text, DateTimeOffset.UtcNow);

            Assert.NotNull(snapshot);
            Assert.Equal(expected, snapshot.Kind);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("bogus")]
        [InlineData("trial:")]
        [InlineData("trial:x")]
        [InlineData("trial:-1")]
        [InlineData("trial:99999")]
        public void TheDebugSwitch_IsOffForAnythingElse(string text)
        {
            Assert.Null(FakeLicenseSetting.Parse(text, DateTimeOffset.UtcNow));
        }

        [Fact]
        public async Task TheLicenseOfAnEditionWithoutOne_IsFullAndSellsNothing()
        {
            var service = new NullLicenseService();

            Assert.False(service.RequiresLicenseCheck);
            Assert.Equal(LicenseKind.Full, (await service.GetAsync(default)).Kind);
            Assert.False(await service.PurchaseAsync(IntPtr.Zero, default));
        }

        private sealed class RefusingService : ILicenseService
        {
            private EventHandler<LicenseSnapshot> _changed;

            public event EventHandler<LicenseSnapshot> Changed
            {
                add { _changed += value; }
                remove { _changed -= value; }
            }

            public bool RequiresLicenseCheck => true;

            public Task<LicenseSnapshot> GetAsync(System.Threading.CancellationToken cancellationToken) =>
                Task.FromResult(LicenseSnapshot.Expired);

            public Task<bool> PurchaseAsync(IntPtr ownerWindow, System.Threading.CancellationToken cancellationToken) =>
                Task.FromException<bool>(new InvalidOperationException("the person closed the Store"));
        }
    }
}
