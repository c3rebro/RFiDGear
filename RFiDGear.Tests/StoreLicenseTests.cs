using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using RFiDGear.Services.Licensing;

using Xunit;

namespace RFiDGear.Store
{
    /// <summary>
    /// Stands in for the service of the Store assembly, which the test project cannot reference (it needs a Windows-versioned
    /// target framework): the loader looks for exactly this name.
    /// </summary>
    public sealed class StoreLicenseService : ILicenseService
    {
        private EventHandler<LicenseSnapshot> _changed;

        public event EventHandler<LicenseSnapshot> Changed
        {
            add { _changed += value; }
            remove { _changed -= value; }
        }

        public bool RequiresLicenseCheck => true;

        public Task<LicenseSnapshot> GetAsync(CancellationToken cancellationToken) => Task.FromResult(LicenseSnapshot.Full);

        public Task<bool> PurchaseAsync(IntPtr ownerWindow, CancellationToken cancellationToken) => Task.FromResult(true);
    }
}

namespace RFiDGear.Tests
{
    /// <summary>
    /// The two things of the Store edition's license that the program owns and can test without the Store: how the Store's answer
    /// becomes a license, and how the Store assembly is found.
    /// </summary>
    public sealed class StoreLicenseTests : IDisposable
    {
        private static readonly DateTimeOffset Ends = new DateTimeOffset(2026, 12, 20, 9, 0, 0, TimeSpan.Zero);

        private readonly string _folder = Directory.CreateTempSubdirectory("RFiDGearStoreAsm").FullName;

        public void Dispose() => Directory.Delete(_folder, recursive: true);

        [Fact]
        public void ACopyThatIsNotFromTheStore_IsNotGated_WhateverTheStoreWouldSay()
        {
            Assert.Equal(LicenseKind.Unknown, StoreLicenseMapper.Map(installedFromStore: false, isActive: false, isTrial: false, trialEnds: Ends).Kind);
            Assert.Equal(LicenseKind.Unknown, StoreLicenseMapper.Map(installedFromStore: false, isActive: true, isTrial: true, trialEnds: Ends).Kind);
        }

        [Fact]
        public void AnAppFromTheStoreWithoutALicenseInForce_HasAnEndedTrial()
        {
            Assert.Equal(LicenseKind.Expired, StoreLicenseMapper.Map(installedFromStore: true, isActive: false, isTrial: true, trialEnds: Ends).Kind);
        }

        [Fact]
        public void ATrialFromTheStore_KeepsItsEnd_AndABoughtAppIsFull()
        {
            var trial = StoreLicenseMapper.Map(installedFromStore: true, isActive: true, isTrial: true, trialEnds: Ends);

            Assert.Equal(LicenseKind.Trial, trial.Kind);
            Assert.Equal(Ends, trial.TrialEnds);

            Assert.Equal(LicenseKind.Full, StoreLicenseMapper.Map(installedFromStore: true, isActive: true, isTrial: false, trialEnds: Ends).Kind);
        }

        [Fact]
        public void TheLoader_MakesTheServiceOfAnAssemblyThatHasOne()
        {
            var service = StoreLicenseServiceLoader.CreateFrom(typeof(RFiDGear.Store.StoreLicenseService).Assembly);

            Assert.IsType<RFiDGear.Store.StoreLicenseService>(service);
            Assert.True(service.RequiresLicenseCheck);
        }

        [Fact]
        public void TheLoader_MakesNothingOfAnAssemblyWithoutTheService_OrWithoutAnAssembly()
        {
            Assert.Null(StoreLicenseServiceLoader.CreateFrom(typeof(object).Assembly));
            Assert.Null(StoreLicenseServiceLoader.CreateFrom(null));
        }

        [Fact]
        public void ThePackageWithoutTheStoreAssembly_GivesNoService()
        {
            Assert.Null(StoreLicenseServiceLoader.TryLoad(_folder));
            Assert.Null(StoreLicenseServiceLoader.TryLoad(string.Empty));
            Assert.Null(StoreLicenseServiceLoader.TryLoad(null));
        }

        [Fact]
        public void AStoreAssemblyThatCannotBeLoaded_GivesNoService_AndDoesNotThrow()
        {
            File.WriteAllText(Path.Combine(_folder, StoreLicenseServiceLoader.AssemblyFileName), "this is not an assembly");

            Assert.Null(StoreLicenseServiceLoader.TryLoad(_folder));
        }

        [Fact]
        public void OfSeveralReadsThatAreUnderWay_OnlyTheNewestIsTheLatest()
        {
            var reads = new ReadSequence();

            var first = reads.Begin();
            Assert.True(reads.IsLatest(first));

            // A second read starts while the first is still waiting for the Store: the first one is out of date, whenever it ends.
            var second = reads.Begin();

            Assert.False(reads.IsLatest(first));
            Assert.True(reads.IsLatest(second));
        }

        [Fact]
        public void ANoticeThatWaitsInTheQueue_IsNotPublishedWhenANewerReadStartedMeanwhile()
        {
            var reads = new ReadSequence();
            var published = 0;

            // The notice passed its check and was handed over to the thread of the window, where it waits ...
            var stale = reads.Guard(reads.Begin(), () => published++);
            var current = reads.Guard(reads.Begin(), () => published += 10);

            // ... a read started meanwhile (a purchase), and the queue runs the notices in the order in which they were posted.
            stale();
            current();

            Assert.Equal(10, published);
        }
    }
}
