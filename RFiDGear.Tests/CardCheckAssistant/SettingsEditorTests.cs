using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using RFiDGear.ExtensionLibraries.CardCheck;
using RFiDGear.Extensions.CardCheckAssistant.Services;
using RFiDGear.Tests.CardCheck;

using Xunit;

namespace RFiDGear.Tests.CardCheckAssistant
{
    /// <summary>
    /// What the settings UI reads and writes: every value it edits comes back as saved, what it does not edit is carried
    /// over (also what another window changed in between), a file it must not replace is not replaced, and what the test of
    /// the order database found is said in the extension's own words.
    /// </summary>
    public sealed class SettingsEditorTests : IDisposable
    {
        private readonly TempFolder _folder = new TempFolder();
        private readonly RecordingCardCheckLog _log = new RecordingCardCheckLog();

        public void Dispose() => _folder.Dispose();

        private string SettingsFolder => Path.Combine(_folder.Path, "settings");

        private ISettingsStore Store() => CardCheckServices.CreateSettingsStore(SettingsFolder, _log);

        // An order store whose connection test answers what the test says; nothing else of it is used.
        private sealed class ScriptedOrderStore : IOrderStore
        {
            private readonly Failure? _failure;

            public ScriptedOrderStore(Failure? failure)
            {
                _failure = failure;
            }

            public Task<Result> TestConnectionAsync(CancellationToken cancellationToken) =>
                Task.FromResult(_failure == null ? Result.Ok() : Result.Fail(_failure));

            public Task<Result<IReadOnlyList<OrderRecord>>> ListAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

            public Task<Result<byte[]>> DownloadReportAsync(string orderId, CancellationToken cancellationToken) => throw new NotSupportedException();

            public Task<Result> ReplaceReportAsync(string orderId, byte[] report, CancellationToken cancellationToken) => throw new NotSupportedException();

            public Task<Result> FinalizeAsync(string orderId, byte[] report, CancellationToken cancellationToken) => throw new NotSupportedException();

            public Task<Result> TouchChangedDateAsync(string orderId, CancellationToken cancellationToken) => throw new NotSupportedException();
        }

        private SettingsEditor Editor(ISettingsStore store, Func<DatabaseSettings, IOrderStore>? orders = null) =>
            new SettingsEditor(store, orders ?? (_ => new ScriptedOrderStore(null)));

        private static SettingsData Edited() => new SettingsData
        {
            OutputFolder = Path.Combine("reports", "out"),
            OneFolderPerJob = true,
            RemoveTemporaryReports = true,
            ReportLanguage = ReportLanguageChoice.English,
            DefaultProjectPath = Path.Combine("projects", "check.rfPrj"),
            MemoryProjectPath = Path.Combine("projects", "memory.rfPrj"),
            Database = new DatabaseData("SRV\\INST", "OrdersDb", "dbo.T_Orders"),
            TextBlocks = new[] { new TextBlockData("Greeting", "Hello\nline 2"), new TextBlockData("Closing", "Bye") },
        };

        // ── reading and saving ──────────────────────────────────────────────

        [Fact]
        public void Load_WithoutAFile_GivesTheDefaults_AndNoProblem()
        {
            var load = Editor(Store()).Load();

            Assert.Equal(SettingsProblem.None, load.Problem);
            Assert.Equal(string.Empty, load.Data.OutputFolder);
            Assert.Equal(ReportLanguageChoice.German, load.Data.ReportLanguage);
            Assert.Equal(new DatabaseData(string.Empty, string.Empty, "T_CardCheck"), load.Data.Database);
            Assert.Empty(load.Data.TextBlocks);
        }

        [Fact]
        public void Save_ThenLoad_GivesBackEveryEditedValue()
        {
            var editor = Editor(Store());
            var data = Edited();

            Assert.Equal(SettingsSaveOutcome.Saved, editor.Save(data));
            var loaded = editor.Load().Data;

            Assert.Equal(
                (data.OutputFolder, data.OneFolderPerJob, data.RemoveTemporaryReports, data.ReportLanguage, data.DefaultProjectPath, data.MemoryProjectPath, data.Database),
                (loaded.OutputFolder, loaded.OneFolderPerJob, loaded.RemoveTemporaryReports, loaded.ReportLanguage, loaded.DefaultProjectPath, loaded.MemoryProjectPath, loaded.Database));
            Assert.Equal(data.TextBlocks, loaded.TextBlocks);
        }

        [Fact]
        public void Save_CarriesOverWhatTheEditorDoesNotShow_AndWhatAnotherWindowChangedMeanwhile()
        {
            var store = Store();
            var marker = new LegacyImportMarker { Declined = true, ImportedAtUtc = new DateTimeOffset(2026, 11, 7, 0, 0, 0, TimeSpan.Zero) };
            store.Save(new CardCheckSettings { LastProjectFolder = "first", LastReportFolder = "reports-folder", LegacyImport = marker });
            var editor = Editor(store);
            var shown = editor.Load();

            // Another window remembers a folder after this one has read the settings and before it saves.
            store.Update(settings => Result<CardCheckSettings>.Ok(settings with { LastProjectFolder = "changed meanwhile" }));
            Assert.Equal(SettingsSaveOutcome.Saved, editor.Save(shown.Data with { OutputFolder = "reports" }));

            var saved = store.Load().Settings;
            Assert.Equal(("changed meanwhile", "reports-folder", true, "reports"), (saved.LastProjectFolder, saved.LastReportFolder, saved.LegacyImport!.Declined, saved.Report.OutputPath));
        }

        [Fact]
        public void ASettingsFileOfANewerVersion_IsShownAsDefaults_WithAProblem_AndIsNeverSavedOver()
        {
            Directory.CreateDirectory(SettingsFolder);
            const string content = "{\"schemaVersion\":2,\"settings\":{\"defaultProjectPath\":\"kept\"}}";
            var file = Path.Combine(SettingsFolder, "settings.json");
            File.WriteAllText(file, content);
            var editor = Editor(Store());

            var load = editor.Load();
            var outcome = editor.Save(Edited());

            Assert.Equal(SettingsProblem.NewerVersion, load.Problem);
            Assert.Equal(string.Empty, load.Data.DefaultProjectPath);
            Assert.Equal(SettingsSaveOutcome.NewerVersion, outcome);
            Assert.Equal(content, File.ReadAllText(file));
        }

        [Fact]
        public void ADamagedSettingsFile_IsSaidSo_AndTheNextSaveWorks()
        {
            Directory.CreateDirectory(SettingsFolder);
            File.WriteAllText(Path.Combine(SettingsFolder, "settings.json"), "this is not json");
            var editor = Editor(Store());

            Assert.Equal(SettingsProblem.DamagedFileSetAside, editor.Load().Problem);
            Assert.Equal(SettingsSaveOutcome.Saved, editor.Save(Edited()));
        }

        [Fact]
        public void WithoutAPlaceToKeepSettings_TheEditorSaysSo()
        {
            var none = AssistantServices.None.Editor;

            Assert.Equal(SettingsProblem.Unavailable, none.Load().Problem);
            Assert.Equal(SettingsSaveOutcome.Unavailable, none.Save(Edited()));
        }

        // ── testing the order database ──────────────────────────────────────

        [Theory]
        [InlineData("orders.notConfigured", nameof(ConnectionTestOutcome.NotConfigured))]
        [InlineData("orders.table.invalid", nameof(ConnectionTestOutcome.TableInvalid))]
        [InlineData("orders.connection.invalid", nameof(ConnectionTestOutcome.ConnectionInvalid))]
        [InlineData("orders.unreachable", nameof(ConnectionTestOutcome.Unreachable))]
        [InlineData("orders.denied", nameof(ConnectionTestOutcome.AccessDenied))]
        [InlineData("orders.schema", nameof(ConnectionTestOutcome.SchemaMismatch))]
        [InlineData("orders.timeout", nameof(ConnectionTestOutcome.Timeout))]
        [InlineData("orders.driver.missing", nameof(ConnectionTestOutcome.DriverMissing))]
        [InlineData("orders.cancelled", nameof(ConnectionTestOutcome.Cancelled))]
        [InlineData("orders.failed", nameof(ConnectionTestOutcome.Failed))]
        [InlineData("something.new", nameof(ConnectionTestOutcome.Failed))]
        public async Task TestConnection_SaysWhatWasFound_InTheExtensionsOwnWords(string code, string expectedName)
        {
            // The outcome is passed by name: the type is internal to the extension and a public test method cannot take it.
            var editor = Editor(Store(), _ => new ScriptedOrderStore(new Failure(FailureKind.Unexpected, code, "text")));

            Assert.Equal(Enum.Parse<ConnectionTestOutcome>(expectedName), await editor.TestConnectionAsync(new DatabaseData("s", "d", "t"), CancellationToken.None));
        }

        [Fact]
        public async Task TestConnection_TriesTheValuesAsEntered_AndSavesNothing()
        {
            var tried = new List<DatabaseSettings>();
            var editor = Editor(Store(), settings =>
            {
                tried.Add(settings);

                return new ScriptedOrderStore(null);
            });

            var outcome = await editor.TestConnectionAsync(new DatabaseData("SRV\\INST", "OrdersDb", "dbo.T_Orders"), CancellationToken.None);

            Assert.Equal(ConnectionTestOutcome.Succeeded, outcome);
            Assert.Equal(new DatabaseSettings { Server = "SRV\\INST", Database = "OrdersDb", Table = "dbo.T_Orders" }, Assert.Single(tried));
            Assert.False(File.Exists(Path.Combine(SettingsFolder, "settings.json")));
        }

        [Fact]
        public async Task TestConnection_WithNothingEntered_IsNotConfigured_ThroughTheRealStore()
        {
            var editor = new SettingsEditor(Store(), settings => CardCheckServices.CreateOrderStore(settings, _log));

            Assert.Equal(ConnectionTestOutcome.NotConfigured, await editor.TestConnectionAsync(new DatabaseData(string.Empty, string.Empty, "T"), CancellationToken.None));
            Assert.Equal(ConnectionTestOutcome.TableInvalid, await editor.TestConnectionAsync(new DatabaseData("s", "d", "T; DROP TABLE T"), CancellationToken.None));
        }

        // ── the folder check ────────────────────────────────────────────────

        [Fact]
        public void FolderPathValidator_SaysWhatIsWrongWithAFolder()
        {
            var file = Path.Combine(_folder.Path, "a-file.txt");
            File.WriteAllText(file, "x");

            Assert.Equal(FolderCheck.None, FolderPathValidator.Check(null));
            Assert.Equal(FolderCheck.None, FolderPathValidator.Check("   "));
            Assert.Equal(FolderCheck.NotFullyQualified, FolderPathValidator.Check(Path.Combine("relative", "folder")));
            Assert.Equal(FolderCheck.Unusable, FolderPathValidator.Check(Path.Combine(_folder.Path, "a|b")));
            Assert.Equal(FolderCheck.Unusable, FolderPathValidator.Check(Path.Combine(_folder.Path, "a*b")));
            Assert.Equal(FolderCheck.NotAFolder, FolderPathValidator.Check(file));
            Assert.Equal(FolderCheck.NotFound, FolderPathValidator.Check(Path.Combine(_folder.Path, "missing")));
            Assert.Equal(FolderCheck.Valid, FolderPathValidator.Check(_folder.Path));
            Assert.Equal(FolderCheck.Valid, FolderPathValidator.Check("  " + _folder.Path + "  "));
        }
    }
}
