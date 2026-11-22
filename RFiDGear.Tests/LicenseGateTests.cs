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
            var rig = new Rig(LicenseSnapshot.Expired);
            await rig.Gate.StartAsync();
            rig.Service.PurchaseFailWith = new InvalidOperationException("the person closed the Store");

            Assert.False(await rig.Gate.PurchaseAsync(IntPtr.Zero));
            Assert.True(rig.Host.Blocked);
            Assert.True(rig.Gate.IsExpired);
        }

        [Fact]
        public async Task AnAnswerOfUnknown_NeverReplacesALicenseThatIsKnown()
        {
            var rig = new Rig(LicenseSnapshot.Expired);
            await rig.Gate.StartAsync();
            Assert.True(rig.Host.Blocked);

            // Pushed by the Store ...
            rig.Service.Change(LicenseSnapshot.Unknown);
            Assert.True(rig.Host.Blocked);

            // ... and read when it is time: the service keeps saying that it cannot tell.
            rig.Now = rig.Now.Add(LicenseGate.RereadInterval);
            await rig.Gate.TickAsync();

            Assert.True(rig.Host.Blocked);
            Assert.True(rig.Gate.IsExpired);

            // Nothing was learned, so it asks again after the short interval and not after the long one.
            var reads = rig.Service.Reads;
            rig.Now = rig.Now.Add(LicenseGate.RetryInterval);
            await rig.Gate.TickAsync();

            Assert.Equal(reads + 1, rig.Service.Reads);
        }

        [Fact]
        public async Task AReadThatWasOvertakenByAPushedLicense_IsDropped()
        {
            var rig = new Rig(LicenseSnapshot.Unknown);
            rig.Service.HoldReads = true;

            var start = rig.Gate.StartAsync();
            rig.Service.Change(LicenseSnapshot.Full);
            Assert.False(rig.Host.Blocked);

            // The old read ends with an old answer, after the program was bought.
            rig.Service.CompleteHeldRead(LicenseSnapshot.Expired);
            await start;

            Assert.False(rig.Host.Blocked);
            Assert.Equal(0, rig.Host.EndShown);
            Assert.Equal(LicenseKind.Full, rig.Gate.Current.Kind);
        }

        [Fact]
        public async Task AStalledRead_DoesNotHoldBackTheEndOfATrialThatIsKnown()
        {
            var rig = new Rig(LicenseSnapshot.Unknown);
            rig.Service.Change(LicenseSnapshot.Trial(rig.Now.AddHours(2)));
            await rig.Gate.StartAsync();
            Assert.False(rig.Host.Blocked);

            // From now on the Store does not answer, and a read is due.
            rig.Service.HoldReads = true;
            rig.Now = rig.Now.Add(LicenseGate.RereadInterval);
            var tick = rig.Gate.TickAsync();

            Assert.True(rig.Host.Blocked);
            Assert.Equal(1, rig.Host.EndShown);
            Assert.False(tick.IsCompleted);
        }

        [Fact]
        public async Task ATickWhileAReadIsUnderWay_StartsNoSecondRead()
        {
            var rig = new Rig(LicenseSnapshot.Full);
            rig.Service.HoldReads = true;

            var start = rig.Gate.StartAsync();
            rig.Now = rig.Now.Add(LicenseGate.RereadInterval);

            // Without the guard the tick would start a second read that is held open as well; the limit turns that into a failure.
            await rig.Gate.TickAsync().WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(1, rig.Service.Reads);

            rig.Service.CompleteHeldRead(LicenseSnapshot.Full);
            await start;
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
    }
}
