using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

using RFiDGear.ExtensionLibraries.CardCheck;
using RFiDGear.Extensions.CardCheckAssistant.Services;
using RFiDGear.Tests.CardCheck;

using Xunit;

namespace RFiDGear.Tests.CardCheckAssistant
{
    /// <summary>
    /// The real parts of the test mode: the made-up jobs of the support library as the page sees them, the Test folder the
    /// reports of a test run go to, and the wiring that keeps the order database out of it. They work on a store of their
    /// own in a temporary folder, never on the settings of the test run.
    /// </summary>
    public sealed class TestModeServicesTests : IDisposable
    {
        private readonly TempFolder _folder = new TempFolder();
        private readonly RecordingCardCheckLog _log = new RecordingCardCheckLog();

        public void Dispose() => _folder.Dispose();

        private string Reports => Path.Combine(_folder.Path, "reports");

        private string Fallback => Path.Combine(_folder.Path, "fallback");

        private ISettingsStore Store() => CardCheckServices.CreateSettingsStore(Path.Combine(_folder.Path, "settings"), _log);

        private ISettingsStore StoreWith(string outputPath, bool oneFolderPerJob)
        {
            var store = Store();
            var saved = store.Save(new CardCheckSettings { Report = new ReportSettings { OutputPath = outputPath, CreateSubdirectory = oneFolderPerJob } });
            Assert.True(saved.IsSuccess);

            return store;
        }

        private TestReportTarget Target(ISettingsStore store) =>
            new TestReportTarget(store, CardCheckServices.CreateReportFiles(_log), Fallback);

        private static JobEntry Job(string jobNumber = "STUB-1001", string chip = "1") => new JobEntry("STUB-1", jobNumber, chip, "Stub customer A", "Created");

        // ── the jobs ────────────────────────────────────────────────────────

        [Fact]
        public async Task TheStubJobSource_ListsTheMadeUpOrders_AsJobs()
        {
            var jobs = await new StubJobSource(_log).ListAsync(CancellationToken.None);

            Assert.Equal(new[] { "STUB-1", "STUB-2", "STUB-3", "STUB-4", "STUB-5" }, jobs.Select(job => job.Id));
            Assert.Equal(new[] { "STUB-1001", "STUB-1002", "STUB-1003", "STUB-1004", "STUB-1005" }, jobs.Select(job => job.JobNumber));
            Assert.Equal(new[] { "1", "2", "3", "4", "5" }, jobs.Select(job => job.ChipNumber));
            Assert.Equal("Stub customer A", jobs[0].Customer);
            Assert.Equal(new[] { "Created", "OrderConfirmed", "InProgress", "CheckFinished", "Closed" }, jobs.Select(job => job.Status));
        }

        [Fact]
        public async Task TheStubJobSource_MakesItsStoreWhenTheJobsAreFirstAskedFor_AndKeepsIt()
        {
            var source = new StubJobSource(_log);
            Assert.Empty(_log.Entries);

            await source.ListAsync(CancellationToken.None);
            var afterTheFirst = _log.Entries.Count;
            await source.ListAsync(CancellationToken.None);

            Assert.Equal(1, afterTheFirst);
            Assert.Equal(afterTheFirst, _log.Entries.Count);
        }

        [Fact]
        public async Task TheStubJobSource_GivesNoJobs_ForACancelledCall_AndNeedsALog()
        {
            using var source = new CancellationTokenSource();
            source.Cancel();

            var jobs = await new StubJobSource(_log).ListAsync(source.Token);

            Assert.Empty(jobs);
            Assert.Throws<ArgumentNullException>(() => new StubJobSource(null!));
        }

        [Fact]
        public async Task WithoutASource_ThePageGetsAnEmptyList_EvenForACancelledCall()
        {
            using var source = new CancellationTokenSource();
            source.Cancel();

            Assert.Empty(await AssistantServices.None.Jobs.ListAsync(CancellationToken.None));
            Assert.Empty(await AssistantServices.None.Jobs.ListAsync(source.Token));
        }

        [Fact]
        public void TheStubJobSource_IsGivenNothingThatCouldReachTheOrderDatabase()
        {
            // The jobs of the test mode are made without the database settings: all the source is given is the log, so
            // there is no server, database or table for it to connect to, whatever the settings say.
            var constructor = Assert.Single(typeof(StubJobSource).GetConstructors());

            Assert.Equal(new[] { typeof(ICardCheckLog) }, constructor.GetParameters().Select(parameter => parameter.ParameterType));
            Assert.Equal(
                new[] { typeof(Lazy<IOrderStore>) },
                typeof(StubJobSource).GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Select(field => field.FieldType));
        }

        [Fact]
        public void TheSettingsMemory_CarriesTheOptionToThePage()
        {
            var store = Store();
            var memory = new SettingsMemory(store);
            Assert.False(memory.Load().UseStubJobs);

            Assert.True(store.Save(new CardCheckSettings { UseStubJobs = true }).IsSuccess);
            Assert.True(memory.Load().UseStubJobs);

            Assert.True(store.Save(new CardCheckSettings { UseStubJobs = false }).IsSuccess);
            Assert.False(memory.Load().UseStubJobs);
        }

        [Fact]
        public async Task TheRealFactory_WiresTheStubJobsAndTheTestReports_AndTheDefaultIsOff()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var services = AssistantServicesFactory.Create(Dispatcher.CurrentDispatcher);

                Assert.IsType<StubJobSource>(services.Jobs);
                Assert.IsType<TestReportTarget>(services.TestReports);
                Assert.False(services.Settings.Load().UseStubJobs);
            });
        }

        // ── the Test folder ─────────────────────────────────────────────────

        [Fact]
        public void TheReport_GoesToTheTestFolderBelowTheReportFolder_InAFolderOfItsOwnForTheJob()
        {
            Directory.CreateDirectory(Reports);
            var target = Target(StoreWith(Reports, oneFolderPerJob: true));

            var path = target.Prepare(Job());

            Assert.False(string.IsNullOrEmpty(path));
            var jobFolder = Path.Combine(Reports, TestReportTarget.FolderName, "STUB-1001");
            Assert.Equal(jobFolder, Path.GetDirectoryName(path));
            Assert.True(Directory.Exists(jobFolder));
            Assert.StartsWith("STUB-1001", Path.GetFileName(path), StringComparison.Ordinal);
            Assert.False(File.Exists(path));
        }

        [Fact]
        public void WithoutAFolderForEachJob_TheReportLiesInTheTestFolderItself()
        {
            Directory.CreateDirectory(Reports);
            var target = Target(StoreWith(Reports, oneFolderPerJob: false));

            var path = target.Prepare(Job("STUB-1002", "2"));

            Assert.Equal(Path.Combine(Reports, TestReportTarget.FolderName), Path.GetDirectoryName(path));
        }

        [Fact]
        public void TheReportFolderItself_IsLeftAsItWas_OnlyTheTestFolderIsMade()
        {
            Directory.CreateDirectory(Reports);
            File.WriteAllText(Path.Combine(Reports, "real-report.pdf"), "a real report");
            var target = Target(StoreWith(Reports, oneFolderPerJob: true));

            target.Prepare(Job());
            target.Prepare(Job("STUB-1002", "2"));

            Assert.Equal(
                new[] { "Test", "real-report.pdf" }.OrderBy(name => name),
                Directory.GetFileSystemEntries(Reports).Select(entry => Path.GetFileName(entry)!).OrderBy(name => name));
            Assert.Equal("a real report", File.ReadAllText(Path.Combine(Reports, "real-report.pdf")));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("relative\\folder")]
        [InlineData("no-such-folder-for-a-test")]
        public void WithoutAUsableReportFolder_TheTestFolderIsMadeInTheFallback(string outputPath)
        {
            var target = Target(StoreWith(outputPath.Replace('\\', Path.DirectorySeparatorChar), oneFolderPerJob: true));

            var path = target.Prepare(Job());

            var expected = Path.Combine(Fallback, TestReportTarget.FolderName, "STUB-1001");
            Assert.Equal(expected, Path.GetDirectoryName(path));
            Assert.True(Directory.Exists(expected));
        }

        [Fact]
        public void AReportFolderThatIsNotThere_IsNotUsed_AndNotMade()
        {
            // A full path, so only its absence tells it from a usable one: a share that is not reachable just now, say.
            var missing = Path.Combine(_folder.Path, "not-there");
            var target = Target(StoreWith(missing, oneFolderPerJob: true));

            var path = target.Prepare(Job());

            Assert.Equal(Path.Combine(Fallback, TestReportTarget.FolderName, "STUB-1001"), Path.GetDirectoryName(path));
            Assert.False(Directory.Exists(missing));
        }

        [Fact]
        public void ANumberThatCannotBeAFileName_GivesNoPath_AndMakesNoFolderForIt()
        {
            Directory.CreateDirectory(Reports);
            var target = Target(StoreWith(Reports, oneFolderPerJob: true));

            Assert.Equal(string.Empty, target.Prepare(Job("CON")));
            Assert.Equal(string.Empty, target.Prepare(Job(chip: string.Empty)));
            Assert.False(Directory.Exists(Path.Combine(Reports, TestReportTarget.FolderName, "CON")));

            // Nothing was made for them at all, not even the Test folder.
            Assert.False(Directory.Exists(Path.Combine(Reports, TestReportTarget.FolderName)));
        }

        [Fact]
        public void AFallbackThatIsNoFullPath_GivesNoPath_AndMakesNoFolderBesideTheProgram()
        {
            var relative = "relative-fallback-" + Guid.NewGuid().ToString("N");
            var target = new TestReportTarget(StoreWith(string.Empty, true), CardCheckServices.CreateReportFiles(_log), relative);

            var path = target.Prepare(Job());

            Assert.Equal(string.Empty, path);
            Assert.False(Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), relative)));
        }

        [Fact]
        public void WhenTheFolderCannotBeMade_NoPathIsGiven_AndNothingIsThrown()
        {
            // The place for the Test folder is a file, so a folder cannot be made in it.
            var blocker = Path.Combine(_folder.Path, "blocker");
            File.WriteAllText(blocker, "not a folder");
            var target = new TestReportTarget(StoreWith(string.Empty, true), CardCheckServices.CreateReportFiles(_log), blocker);

            Assert.Equal(string.Empty, target.Prepare(Job()));
        }

        [Fact]
        public void TheTarget_NeedsItsPartsAndAJob()
        {
            var files = CardCheckServices.CreateReportFiles(_log);

            Assert.Throws<ArgumentNullException>(() => new TestReportTarget(null!, files, Fallback));
            Assert.Throws<ArgumentNullException>(() => new TestReportTarget(Store(), null!, Fallback));
            Assert.Throws<ArgumentNullException>(() => new TestReportTarget(Store(), files, null!));
            Assert.Throws<ArgumentNullException>(() => Target(Store()).Prepare(null!));
        }
    }
}
