using System;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;

using RFiDGear.ExtensionLibraries.CardCheck;
using RFiDGear.Extensions.CardCheckAssistant.Services;
using RFiDGear.Tests.CardCheck;

using Xunit;

namespace RFiDGear.Tests.CardCheckAssistant
{
    public sealed class WorkflowContextSourceTests : IDisposable
    {
        private readonly TempFolder _folder = new TempFolder();
        private readonly RecordingCardCheckLog _log = new RecordingCardCheckLog();
        private readonly FakeSettingsStore _settings = new FakeSettingsStore();
        private int _realStores;
        private int _stubStores;

        public void Dispose() => _folder.Dispose();

        private string Fallback => Path.Combine(_folder.Path, "fallback");

        private WorkflowContextSource Source() => new WorkflowContextSource(
            _settings,
            database =>
            {
                _realStores++;
                LastDatabase = database;

                return CardCheckServices.CreateStubOrderStore(_log);
            },
            () =>
            {
                _stubStores++;

                return CardCheckServices.CreateStubOrderStore(_log);
            },
            Fallback);

        private DatabaseSettings? LastDatabase { get; set; }

        private string Reports(string name = "reports")
        {
            var path = Path.Combine(_folder.Path, name);
            Directory.CreateDirectory(path);

            return path;
        }

        [Fact]
        public void InTestMode_TheMadeUpOrdersAreUsed_WithATestFolderBelowTheReportFolder_AndTheOtherSettingsPassedOn()
        {
            var reports = Reports();
            _settings.Set(new CardCheckSettings
            {
                UseStubJobs = true,
                DefaultProjectPath = "C:\\Projects\\check.rfPrj",
                Report = new ReportSettings { OutputPath = reports, CreateSubdirectory = true, RemoveTemporaryReports = true, DefaultLanguage = ReportLanguage.English },
            });

            var result = Source().Resolve();

            Assert.True(result.IsSuccess);
            var expected = Path.Combine(reports, "Test");
            Assert.Equal(new ReportLocation(expected, true), result.Value.Settings.Location);
            Assert.True(Directory.Exists(expected));
            Assert.True(result.Value.Settings.IsTestMode);
            Assert.True(result.Value.Settings.RemoveTemporaryReports);
            Assert.Equal(ReportLanguage.English, result.Value.Settings.DefaultLanguage);
            Assert.Equal("C:\\Projects\\check.rfPrj", result.Value.Settings.DefaultProjectPath);
            Assert.Equal(1, _stubStores);
            Assert.Equal(0, _realStores);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("relative\\folder")]
        [InlineData("<missing>")]
        public void InTestMode_WithoutAUsableReportFolder_TheTestFolderIsMadeInTheFallback(string? outputPath)
        {
            var path = outputPath == "<missing>" ? Path.Combine(_folder.Path, "nothere") : outputPath;
            _settings.Set(new CardCheckSettings { UseStubJobs = true, Report = new ReportSettings { OutputPath = path! } });

            var result = Source().Resolve();

            Assert.True(result.IsSuccess);
            Assert.Equal(Path.Combine(Fallback, "Test"), result.Value.Settings.Location.OutputRoot);
            Assert.True(Directory.Exists(Path.Combine(Fallback, "Test")));
        }

        [Fact]
        public void TheMadeUpOrders_AreOneSetForTheWindow_AlsoWhenTheModeIsSwitchedOffAndOn()
        {
            var reports = Reports();
            var source = Source();
            _settings.Set(new CardCheckSettings { UseStubJobs = true, Report = new ReportSettings { OutputPath = reports } });
            var first = source.Resolve().Value.Store;

            _settings.Set(new CardCheckSettings { UseStubJobs = false, Report = new ReportSettings { OutputPath = reports } });
            Assert.False(source.Resolve().Value.Settings.IsTestMode);
            _settings.Set(new CardCheckSettings { UseStubJobs = true, Report = new ReportSettings { OutputPath = reports } });
            var again = source.Resolve().Value.Store;

            Assert.Same(first, again);
            Assert.Equal(1, _stubStores);
        }

        [Fact]
        public void TheStoreOfTheOrderSystem_IsMadeForTheDatabaseSettingsAsTheyAreNow_AndTheReportFolderIsUsedAsItIs()
        {
            var reports = Reports();
            _settings.Set(new CardCheckSettings
            {
                Database = new DatabaseSettings { Server = "SQL1\\INST", Database = "Orders", Table = "T_Checks" },
                Report = new ReportSettings { OutputPath = reports, CreateSubdirectory = false },
                DefaultProjectPath = "C:\\Projects\\check.rfPrj",
            });
            var source = Source();

            var first = source.Resolve();
            var second = source.Resolve();

            Assert.True(first.IsSuccess);
            Assert.Equal(new ReportLocation(reports, false), first.Value.Settings.Location);
            Assert.False(first.Value.Settings.IsTestMode);
            Assert.Equal(new DatabaseSettings { Server = "SQL1\\INST", Database = "Orders", Table = "T_Checks" }, LastDatabase);
            Assert.Equal(2, _realStores);   // a new store for each call, with the settings as they are
            Assert.NotSame(first.Value.Store, second.Value.Store);
            Assert.Equal(0, _stubStores);
            Assert.False(Directory.Exists(Path.Combine(reports, "Test")));
        }

        [Fact]
        public void WithoutAReportFolder_ThereIsNoContext_AndTheReasonIsNamed()
        {
            _settings.Set(new CardCheckSettings { Report = new ReportSettings { OutputPath = "  " } });

            var blank = Source().Resolve();

            Assert.False(blank.IsSuccess);
            Assert.Equal("report.root.notSet", blank.Failure!.Code);
            Assert.Equal(FailureKind.NotConfigured, blank.Failure.Kind);

            _settings.Set(new CardCheckSettings { Report = new ReportSettings { OutputPath = Path.Combine(_folder.Path, "nothere") } });
            var missing = Source().Resolve();

            Assert.Equal("report.root.missing", missing.Failure!.Code);
            Assert.Equal(FailureKind.NotFound, missing.Failure.Kind);
            Assert.Equal(0, _realStores);
        }

        [Theory]
        [InlineData(SettingsLoadStatus.Unreadable, "settings.unreadable", FailureKind.Io)]
        [InlineData(SettingsLoadStatus.NewerSchema, "settings.newerSchema", FailureKind.InvalidConfiguration)]
        public void SettingsThatCannotBeUsed_AreToldByTheirStatus_AndNothingIsGuessed(SettingsLoadStatus status, string code, FailureKind kind)
        {
            _settings.Set(new CardCheckSettings { UseStubJobs = true }, status);

            var result = Source().Resolve();

            Assert.False(result.IsSuccess);
            Assert.Equal(code, result.Failure!.Code);
            Assert.Equal(kind, result.Failure.Kind);
            Assert.Equal(0, _stubStores);
        }

        [Theory]
        [InlineData(SettingsLoadStatus.NotFound)]
        [InlineData(SettingsLoadStatus.Recovered)]
        public void SettingsThatAreNewOrWereRecovered_AreTheDefaults_AndAreUsed(SettingsLoadStatus status)
        {
            var reports = Reports();
            _settings.Set(new CardCheckSettings { Report = new ReportSettings { OutputPath = reports } }, status);

            var result = Source().Resolve();

            Assert.True(result.IsSuccess);
            Assert.False(result.Value.Settings.IsTestMode);
        }

        [Fact]
        public void ATestFolderThatCannotBeMade_IsAFailure_NotAnException()
        {
            var reports = Reports();
            File.WriteAllText(Path.Combine(reports, "Test"), "a file where the folder belongs");
            _settings.Set(new CardCheckSettings { UseStubJobs = true, Report = new ReportSettings { OutputPath = reports } });

            var result = Source().Resolve();

            Assert.False(result.IsSuccess);
            Assert.Equal("report.folder.io", result.Failure!.Code);
            Assert.Equal(FailureKind.Io, result.Failure.Kind);
        }

        [Fact]
        public void ATestFolderThatMayNotBeMade_IsAFailure_OfTheKindAccessDenied()
        {
            var reports = Reports();
            var folder = new DirectoryInfo(reports);
            var deny = new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!, FileSystemRights.CreateDirectories, AccessControlType.Deny);
            var security = folder.GetAccessControl();
            security.AddAccessRule(deny);
            folder.SetAccessControl(security);

            try
            {
                _settings.Set(new CardCheckSettings { UseStubJobs = true, Report = new ReportSettings { OutputPath = reports } });

                var result = Source().Resolve();

                Assert.False(result.IsSuccess);
                Assert.Equal("report.folder.denied", result.Failure!.Code);
                Assert.Equal(FailureKind.AccessDenied, result.Failure.Kind);
            }
            finally
            {
                security.RemoveAccessRule(deny);
                folder.SetAccessControl(security);
            }
        }

        [Fact]
        public void AMadeUpStoreThatCouldNotBeMade_IsNotRemembered_AndTheExceptionIsLeftForTheWorkflow()
        {
            _settings.Set(new CardCheckSettings { UseStubJobs = true, Report = new ReportSettings { OutputPath = Reports() } });
            var calls = 0;
            var source = new WorkflowContextSource(
                _settings,
                _ => throw new NotSupportedException(),
                () => calls++ == 0 ? throw new InvalidOperationException("the first try fails") : CardCheckServices.CreateStubOrderStore(_log),
                Fallback);

            Assert.Throws<InvalidOperationException>(() => source.Resolve());
            Assert.True(source.Resolve().IsSuccess);
            Assert.Equal(2, calls);
        }

        [Fact]
        public void ASettingsStoreThatThrows_IsAFailure_NotAnException()
        {
            _settings.Throw = new IOException("secret path of the share");

            var result = Source().Resolve();

            Assert.False(result.IsSuccess);
            Assert.Equal("settings.unreadable", result.Failure!.Code);
            Assert.Equal(FailureKind.Io, result.Failure.Kind);
            Assert.DoesNotContain("secret", result.Failure.SafeMessage, StringComparison.Ordinal);
        }

        [Fact]
        public void Arguments_AreChecked()
        {
            Assert.Throws<ArgumentNullException>(() => new WorkflowContextSource(null!, _ => CardCheckServices.CreateStubOrderStore(_log), () => CardCheckServices.CreateStubOrderStore(_log), Fallback));
            Assert.Throws<ArgumentNullException>(() => new WorkflowContextSource(_settings, null!, () => CardCheckServices.CreateStubOrderStore(_log), Fallback));
            Assert.Throws<ArgumentNullException>(() => new WorkflowContextSource(_settings, _ => CardCheckServices.CreateStubOrderStore(_log), null!, Fallback));
            Assert.Throws<ArgumentNullException>(() => new WorkflowContextSource(_settings, _ => CardCheckServices.CreateStubOrderStore(_log), () => CardCheckServices.CreateStubOrderStore(_log), null!));
        }

        private sealed class FakeSettingsStore : ISettingsStore
        {
            private SettingsLoad _load = new SettingsLoad(new CardCheckSettings(), SettingsLoadStatus.NotFound);

            public Exception? Throw { get; set; }

            public void Set(CardCheckSettings settings, SettingsLoadStatus status = SettingsLoadStatus.Loaded) =>
                _load = new SettingsLoad(settings, status);

            public SettingsLoad Load() => Throw != null ? throw Throw : _load;

            public Result Save(CardCheckSettings settings) => throw new NotSupportedException();

            public Result Update(Func<CardCheckSettings, Result<CardCheckSettings>> change) => throw new NotSupportedException();
        }
    }
}
