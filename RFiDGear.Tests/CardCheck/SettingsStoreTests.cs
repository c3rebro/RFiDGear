using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using RFiDGear.ExtensionLibraries.CardCheck;

using Xunit;

namespace RFiDGear.Tests.CardCheck
{
    public sealed class SettingsStoreTests : IDisposable
    {
        private const string SecretServer = "SECRET-SQL-SERVER-01";
        private const string SecretDatabase = "SECRET-DATABASE";
        private const string SecretTable = "SECRET-TABLE";
        private const string SecretFolder = "SECRET-REPORT-FOLDER";

        private readonly TempFolder _folder = new TempFolder();
        private readonly RecordingCardCheckLog _log = new RecordingCardCheckLog();

        public void Dispose() => _folder.Dispose();

        private string SettingsFolder => Path.Combine(_folder.Path, "settings");

        private string FilePath => Path.Combine(SettingsFolder, "settings.json");

        private ISettingsStore Store() => CardCheckServices.CreateSettingsStore(SettingsFolder, _log);

        private void WriteFile(string content)
        {
            Directory.CreateDirectory(SettingsFolder);
            File.WriteAllText(FilePath, content, new UTF8Encoding(false));
        }

        private string[] LeftoverFiles() =>
            Directory.Exists(SettingsFolder)
                ? Directory.GetFiles(SettingsFolder).Select(file => Path.GetFileName(file)!).Where(name => name != "settings.json").ToArray()
                : Array.Empty<string>();

        private static CardCheckSettings Sample() => new CardCheckSettings
        {
            Database = new DatabaseSettings { Server = SecretServer, Database = SecretDatabase, Table = "dbo.T_CardCheck" },
            Report = new ReportSettings { OutputPath = "D:\\" + SecretFolder, CreateSubdirectory = true, RemoveTemporaryReports = true, DefaultLanguage = ReportLanguage.English },
            DefaultProjectPath = "D:\\projects\\check.rfPrj",
            CustomProjectPath = "D:\\projects\\custom \u00C4\u00D6\u00DC.rfPrj",
            LastProjectFolder = "D:\\projects",
            LastReportFolder = "D:\\reports",
            TextTemplates = new[]
            {
                new TextTemplate { Name = "Z last alphabetically", Content = "line 1\nline 2" },
                new TextTemplate { Name = "A first alphabetically", Content = "\u00DF \u20AC \"quoted\"" },
            },
            LegacyImport = new LegacyImportMarker { SourceSha256 = new string('a', 64), ImportedAtUtc = new DateTimeOffset(2026, 11, 7, 8, 30, 0, TimeSpan.Zero) },
        };

        private static void AssertSame(CardCheckSettings expected, CardCheckSettings actual)
        {
            Assert.Equal(expected.Database, actual.Database);
            Assert.Equal(expected.Report, actual.Report);
            Assert.Equal(expected.DefaultProjectPath, actual.DefaultProjectPath);
            Assert.Equal(expected.CustomProjectPath, actual.CustomProjectPath);
            Assert.Equal(expected.LastProjectFolder, actual.LastProjectFolder);
            Assert.Equal(expected.LastReportFolder, actual.LastReportFolder);
            Assert.Equal(expected.TextTemplates, actual.TextTemplates);
            Assert.Equal(expected.LegacyImport, actual.LegacyImport);
        }

        // ── defaults and round trip ─────────────────────────────────────────

        [Fact]
        public void Settings_ChangingTheListThatWasAssigned_ChangesNothing()
        {
            var assigned = new List<TextTemplate> { new TextTemplate { Name = "one", Content = "1" } };
            var settings = new CardCheckSettings { TextTemplates = assigned };

            assigned.Add(new TextTemplate { Name = "two", Content = "2" });
            assigned[0] = new TextTemplate { Name = "changed", Content = "x" };

            Assert.Equal(new[] { "one" }, settings.TextTemplates.Select(t => t.Name));
        }

        [Fact]
        public void Settings_TheListHandedOutCannotBeChanged_NotInTheInstanceNorInACopy()
        {
            var settings = new CardCheckSettings { TextTemplates = new[] { new TextTemplate { Name = "one" } } };
            var copy = settings with { DefaultProjectPath = "other" };

            Assert.Throws<NotSupportedException>(() => ((IList<TextTemplate>)settings.TextTemplates).Add(new TextTemplate()));
            Assert.Throws<NotSupportedException>(() => ((IList<TextTemplate>)settings.TextTemplates).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<TextTemplate>)copy.TextTemplates)[0] = new TextTemplate());
            Assert.False(settings.TextTemplates is List<TextTemplate>);
            Assert.False(settings.TextTemplates is TextTemplate[]);
            Assert.Single(settings.TextTemplates);
        }

        [Fact]
        public void Settings_ANullListBecomesAnEmptyOne()
        {
            Assert.Empty(new CardCheckSettings { TextTemplates = null! }.TextTemplates);
        }

        [Fact]
        public void Load_TheTemplatesOfALoadedFile_CannotBeChangedEither()
        {
            Store().Save(Sample());

            var loaded = Store().Load().Settings;

            Assert.Throws<NotSupportedException>(() => ((IList<TextTemplate>)loaded.TextTemplates).Clear());
            Assert.Equal(2, loaded.TextTemplates.Count);
        }

        [Fact]
        public void Save_SettingsThatWouldMakeAFileLargerThanLoadAccepts_AreRefused_AndTheOldFileStays()
        {
            var store = Store();
            store.Save(Sample());
            var before = File.ReadAllBytes(FilePath);

            var result = store.Save(new CardCheckSettings { TextTemplates = new[] { new TextTemplate { Name = "huge", Content = new string('x', 1024 * 1024) } } });

            Assert.False(result.IsSuccess);
            Assert.Equal(FailureKind.InvalidInput, result.Failure!.Kind);
            Assert.Equal("settings.save.tooLarge", result.Failure.Code);
            Assert.Equal(before, File.ReadAllBytes(FilePath));
            Assert.Empty(LeftoverFiles());
        }

        [Fact]
        public void Save_TheSizeLimit_IsTheSameAsLoadsToTheByte_EverySuccessfulSaveCanBeLoaded()
        {
            var store = Store();
            Assert.True(store.Save(new CardCheckSettings { TextTemplates = new[] { new TextTemplate { Name = "pad", Content = string.Empty } } }).IsSuccess);
            var baseLength = new FileInfo(FilePath).Length;
            var padding = (int)(SettingsStore.MaxFileBytes - baseLength);

            var atTheLimit = store.Save(new CardCheckSettings { TextTemplates = new[] { new TextTemplate { Name = "pad", Content = new string('x', padding) } } });
            var lengthAtLimit = new FileInfo(FilePath).Length;
            var loaded = store.Load();
            var oneOver = store.Save(new CardCheckSettings { TextTemplates = new[] { new TextTemplate { Name = "pad", Content = new string('x', padding + 1) } } });

            Assert.True(atTheLimit.IsSuccess);
            Assert.Equal(SettingsStore.MaxFileBytes, lengthAtLimit);
            Assert.Equal(SettingsLoadStatus.Loaded, loaded.Status);
            Assert.False(oneOver.IsSuccess);
            Assert.Equal("settings.save.tooLarge", oneOver.Failure!.Code);
            Assert.Equal(SettingsLoadStatus.Loaded, store.Load().Status);
        }

        [Fact]
        public void Defaults_AreEmptyExceptTheTableAndTheLanguage()
        {
            var defaults = new CardCheckSettings();

            Assert.Equal(string.Empty, defaults.Database.Server);
            Assert.Equal(string.Empty, defaults.Database.Database);
            Assert.Equal("T_CardCheck", defaults.Database.Table);
            Assert.Equal(string.Empty, defaults.Report.OutputPath);
            Assert.False(defaults.Report.CreateSubdirectory);
            Assert.False(defaults.Report.RemoveTemporaryReports);
            Assert.Equal(ReportLanguage.German, defaults.Report.DefaultLanguage);
            Assert.Empty(defaults.TextTemplates);
            Assert.Null(defaults.LegacyImport);
        }

        [Fact]
        public void Load_WithoutAFile_GivesTheDefaultsAndSaysSo()
        {
            var load = Store().Load();

            Assert.Equal(SettingsLoadStatus.NotFound, load.Status);
            AssertSame(new CardCheckSettings(), load.Settings);
            Assert.False(Directory.Exists(SettingsFolder));
        }

        [Fact]
        public void SaveThenLoad_RoundTripsEveryField_AndKeepsTheOrderOfTheTemplates()
        {
            var store = Store();

            Assert.True(store.Save(Sample()).IsSuccess);
            var load = Store().Load();

            Assert.Equal(SettingsLoadStatus.Loaded, load.Status);
            AssertSame(Sample(), load.Settings);
            Assert.Equal(new[] { "Z last alphabetically", "A first alphabetically" }, load.Settings.TextTemplates.Select(t => t.Name));
        }

        [Fact]
        public void Save_CreatesTheFolder_AndLeavesNoTemporaryFileBehind()
        {
            Assert.False(Directory.Exists(SettingsFolder));

            Assert.True(Store().Save(Sample()).IsSuccess);

            Assert.True(File.Exists(FilePath));
            Assert.Empty(LeftoverFiles());
        }

        [Fact]
        public void Save_WritesTheSchemaVersion_AndNoCredentialAtAll()
        {
            Store().Save(Sample());
            var text = File.ReadAllText(FilePath);
            using var document = System.Text.Json.JsonDocument.Parse(text);

            Assert.Contains("\"schemaVersion\": 1", text, StringComparison.Ordinal);

            // Every name of every property in the file: none may be about a user, a password or a port.
            var names = PropertyNames(document.RootElement).ToList();
            Assert.Contains("server", names);
            foreach (var name in names)
            {
                foreach (var forbidden in new[] { "pass", "pwd", "user", "credential", "login", "account" })
                {
                    Assert.DoesNotContain(forbidden, name, StringComparison.OrdinalIgnoreCase);
                }

                // "port" alone or as the last word of a camel-case name (serverPort), but not inside "report".
                Assert.False(
                    name.Equals("port", StringComparison.OrdinalIgnoreCase) || name.EndsWith("Port", StringComparison.Ordinal),
                    $"'{name}' looks like a port setting");
            }
        }

        private static IEnumerable<string> PropertyNames(System.Text.Json.JsonElement element)
        {
            switch (element.ValueKind)
            {
                case System.Text.Json.JsonValueKind.Object:
                    foreach (var property in element.EnumerateObject())
                    {
                        yield return property.Name;

                        foreach (var inner in PropertyNames(property.Value))
                        {
                            yield return inner;
                        }
                    }

                    break;
                case System.Text.Json.JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray().SelectMany(PropertyNames))
                    {
                        yield return item;
                    }

                    break;
                default:
                    break;
            }
        }

        [Fact]
        public void Save_TidiesValues_BlanksAreRemovedAndMissingValuesBecomeEmpty()
        {
            var settings = new CardCheckSettings
            {
                Database = new DatabaseSettings { Server = "  SRV  ", Database = null!, Table = "\tT\t" },
                DefaultProjectPath = "  D:\\p.rfPrj ",
                TextTemplates = new[] { new TextTemplate { Name = null!, Content = " keep my blanks " } },
            };

            Assert.True(Store().Save(settings).IsSuccess);
            var loaded = Store().Load().Settings;

            Assert.Equal("SRV", loaded.Database.Server);
            Assert.Equal(string.Empty, loaded.Database.Database);
            Assert.Equal("T", loaded.Database.Table);
            Assert.Equal("D:\\p.rfPrj", loaded.DefaultProjectPath);
            Assert.Equal(" keep my blanks ", Assert.Single(loaded.TextTemplates).Content);
        }

        [Fact]
        public void Normalize_DoesNotChangeSettingsThatAreAlreadyTidy()
        {
            var once = SettingsStore.Normalize(Sample());
            var twice = SettingsStore.Normalize(once);

            AssertSame(once, twice);
        }

        [Fact]
        public void TwoStoresOnTheSameFolder_SeeEachOthersSaves()
        {
            var a = Store();
            var b = Store();

            a.Save(new CardCheckSettings { DefaultProjectPath = "from-a" });
            Assert.Equal("from-a", b.Load().Settings.DefaultProjectPath);

            b.Save(new CardCheckSettings { DefaultProjectPath = "from-b" });
            Assert.Equal("from-b", a.Load().Settings.DefaultProjectPath);
        }

        // ── what is tolerated in the file ───────────────────────────────────

        [Fact]
        public void Load_IgnoresUnknownPropertiesCommentsAndTrailingCommas()
        {
            WriteFile("// written by hand\n{ \"schemaVersion\": 1, \"another\": true, \"settings\": { \"database\": { \"server\": \"S\", \"extra\": 1, }, \"future\": { \"a\": [1,2] }, }, }");

            var load = Store().Load();

            Assert.Equal(SettingsLoadStatus.Loaded, load.Status);
            Assert.Equal("S", load.Settings.Database.Server);
        }

        [Fact]
        public void Load_MissingPropertiesGetTheirDefaults()
        {
            WriteFile("{\"schemaVersion\":1,\"settings\":{\"report\":{\"outputPath\":\"D:\\\\x\"}}}");

            var settings = Store().Load().Settings;

            Assert.Equal("D:\\x", settings.Report.OutputPath);
            Assert.Equal(ReportLanguage.German, settings.Report.DefaultLanguage);
            Assert.Equal("T_CardCheck", settings.Database.Table);
        }

        [Theory]
        [InlineData("{\"schemaVersion\":1}")]
        [InlineData("{\"schemaVersion\":1,\"settings\":null}")]
        [InlineData("{\"schemaVersion\":1,\"settings\":{}}")]
        public void Load_ASettingsFileWithoutSettings_IsLoadedAsTheDefaults(string content)
        {
            WriteFile(content);

            var load = Store().Load();

            Assert.Equal(SettingsLoadStatus.Loaded, load.Status);
            AssertSame(new CardCheckSettings(), load.Settings);
        }

        [Fact]
        public void Load_NullValuesInsideTheFile_BecomeDefaultsAndEmptyStrings()
        {
            WriteFile("{\"schemaVersion\":1,\"settings\":{\"database\":null,\"report\":null,\"defaultProjectPath\":null,\"textTemplates\":[null,{\"name\":null,\"content\":\"c\"}]}}");

            var load = Store().Load();

            Assert.Equal(SettingsLoadStatus.Loaded, load.Status);
            Assert.Equal(new DatabaseSettings(), load.Settings.Database);
            Assert.Equal(new ReportSettings(), load.Settings.Report);
            Assert.Equal(string.Empty, load.Settings.DefaultProjectPath);
            Assert.Equal(new TextTemplate { Name = string.Empty, Content = "c" }, Assert.Single(load.Settings.TextTemplates));
        }

        [Theory]
        [InlineData("\"english\"", ReportLanguage.English)]
        [InlineData("\"ENGLISH \"", ReportLanguage.English)]
        [InlineData("\"English\"", ReportLanguage.English)]
        [InlineData("\"German\"", ReportLanguage.German)]
        [InlineData("\"klingon\"", ReportLanguage.German)]
        [InlineData("1", ReportLanguage.German)]
        [InlineData("null", ReportLanguage.German)]
        [InlineData("{\"x\":1}", ReportLanguage.German)]
        [InlineData("[1,2]", ReportLanguage.German)]
        public void Load_TheLanguageIsReadLeniently_SoOneBadEntryDoesNotCostTheOthers(string json, ReportLanguage expected)
        {
            WriteFile("{\"schemaVersion\":1,\"settings\":{\"defaultProjectPath\":\"kept\",\"report\":{\"defaultLanguage\":" + json + "}}}");

            var load = Store().Load();

            Assert.Equal(SettingsLoadStatus.Loaded, load.Status);
            Assert.Equal(expected, load.Settings.Report.DefaultLanguage);
            Assert.Equal("kept", load.Settings.DefaultProjectPath);
        }

        [Fact]
        public void Save_WritesTheLanguageAsItsName()
        {
            Store().Save(new CardCheckSettings { Report = new ReportSettings { DefaultLanguage = ReportLanguage.English } });

            Assert.Contains("\"defaultLanguage\": \"English\"", File.ReadAllText(FilePath), StringComparison.Ordinal);
        }

        // ── a damaged file ──────────────────────────────────────────────────

        [Theory]
        [InlineData("this is not json")]
        [InlineData("")]
        [InlineData("[]")]
        [InlineData("null")]
        [InlineData("\"text\"")]
        [InlineData("5")]
        [InlineData("{}")]
        [InlineData("{\"schemaVersion\":0}")]
        [InlineData("{\"schemaVersion\":-1}")]
        [InlineData("{\"schemaVersion\":\"1\"}")]
        [InlineData("{\"schemaVersion\":1.5}")]
        [InlineData("{\"schemaVersion\":1,\"settings\":[1,2,3]}")]
        [InlineData("{\"schemaVersion\":1,\"settings\":{\"report\":{\"createSubdirectory\":\"yes please\"}}}")]
        [InlineData("{\"schemaVersion\":1,\"settings\":{\"database\":")]
        public void Load_ADamagedFile_IsSetAsideWithItsContentAndTheDefaultsAreUsed(string content)
        {
            WriteFile(content);

            var load = Store().Load();

            Assert.Equal(SettingsLoadStatus.Recovered, load.Status);
            AssertSame(new CardCheckSettings(), load.Settings);
            Assert.False(File.Exists(FilePath));
            var aside = Assert.Single(Directory.GetFiles(SettingsFolder, "settings.json.corrupt-*"));
            Assert.Equal(content, File.ReadAllText(aside));
        }

        [Fact]
        public void Load_AfterADamagedFileWasSetAside_FindsNothingAndASaveWorksAgain()
        {
            WriteFile("garbage");
            var store = Store();
            Assert.Equal(SettingsLoadStatus.Recovered, store.Load().Status);

            Assert.Equal(SettingsLoadStatus.NotFound, store.Load().Status);
            Assert.True(store.Save(Sample()).IsSuccess);
            Assert.Equal(SettingsLoadStatus.Loaded, store.Load().Status);
            Assert.Single(Directory.GetFiles(SettingsFolder, "settings.json.corrupt-*"));
        }

        [Fact]
        public void Load_TwoDamagedFilesInTheSameMoment_KeepBoth()
        {
            var store = (SettingsStore)Store();
            var moment = new DateTime(2026, 11, 7, 9, 0, 0, DateTimeKind.Utc);
            store.UtcNow = () => moment;   // the same timestamp for both, so the names would collide

            WriteFile("garbage one");
            store.Load();
            WriteFile("garbage two");
            store.Load();

            var asides = Directory.GetFiles(SettingsFolder, "settings.json.corrupt-*").Select(File.ReadAllText).OrderBy(x => x).ToArray();
            Assert.Equal(new[] { "garbage one", "garbage two" }, asides);
        }

        [Fact]
        public void Load_ADamagedFileIsSetAsideUnderTheNameOfTheMoment()
        {
            var store = (SettingsStore)Store();
            store.UtcNow = () => new DateTime(2026, 11, 7, 9, 5, 3, 42, DateTimeKind.Utc);

            WriteFile("garbage");
            store.Load();

            Assert.True(File.Exists(FilePath + ".corrupt-20261107090503042"));
        }

        [Fact]
        public void Load_AFileLargerThanAMegabyte_IsNotASettingsFile()
        {
            Directory.CreateDirectory(SettingsFolder);
            File.WriteAllText(FilePath, "{\"schemaVersion\":1,\"padding\":\"" + new string('x', 1024 * 1024 + 10) + "\"}");

            var load = Store().Load();

            Assert.Equal(SettingsLoadStatus.Recovered, load.Status);
            Assert.False(File.Exists(FilePath));
        }

        // ── changing the settings in one step ───────────────────────────────

        [Fact]
        public async Task Update_TwoStoresOfTheSameFile_LoseNoChange()
        {
            // Two windows of the extension each have a store of their own for the same file. Each adds 25 text blocks, one
            // update at a time; if reading and writing were not one step across the stores, one would write a copy that
            // does not have the other's blocks.
            var first = Store();
            var second = Store();

            async Task Add(ISettingsStore store, string prefix)
            {
                await Task.Run(() =>
                {
                    for (var i = 0; i < 25; i++)
                    {
                        var name = prefix + i;
                        var result = store.Update(settings => Result<CardCheckSettings>.Ok(
                            settings with { TextTemplates = settings.TextTemplates.Append(new TextTemplate { Name = name, Content = "x" }).ToList() }));
                        Assert.True(result.IsSuccess, result.Failure?.Code);
                    }
                });
            }

            await Task.WhenAll(Add(first, "a"), Add(second, "b"));

            Assert.Equal(50, Store().Load().Settings.TextTemplates.Count);
        }

        [Fact]
        public void Update_ChangesWhatIsThere_AndANoChangeOrAFailureWritesNothing()
        {
            var store = Store();
            store.Save(new CardCheckSettings { DefaultProjectPath = "kept.rfPrj" });
            var before = File.ReadAllBytes(FilePath);

            var same = store.Update(settings => Result<CardCheckSettings>.Ok(settings));
            var failed = store.Update(_ => Result<CardCheckSettings>.Fail(new Failure(FailureKind.InvalidInput, "test.stop", "stop")));

            Assert.True(same.IsSuccess);
            Assert.Equal("test.stop", failed.Failure!.Code);
            Assert.Equal(before, File.ReadAllBytes(FilePath));
            Assert.Empty(LeftoverFiles());

            Assert.True(store.Update(settings => Result<CardCheckSettings>.Ok(settings with { LastProjectFolder = "folder" })).IsSuccess);
            var after = store.Load().Settings;
            Assert.Equal(("kept.rfPrj", "folder"), (after.DefaultProjectPath, after.LastProjectFolder));
        }

        [Fact]
        public void Update_OverAFileOfANewerVersion_IsRefused_AndTheChangeIsNeverAsked()
        {
            const string content = "{\"schemaVersion\":2,\"settings\":{}}";
            WriteFile(content);
            var asked = false;

            var result = Store().Update(settings =>
            {
                asked = true;

                return Result<CardCheckSettings>.Ok(settings);
            });

            Assert.Equal("settings.newerSchema", result.Failure!.Code);
            Assert.False(asked);
            Assert.Equal(content, File.ReadAllText(FilePath));
        }

        // ── a file with a byte order mark ───────────────────────────────────

        private void WriteFileWithByteOrderMark(string content)
        {
            Directory.CreateDirectory(SettingsFolder);
            File.WriteAllBytes(FilePath, new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes(content)).ToArray());
        }

        [Fact]
        public void Load_AValidFileWithAByteOrderMark_IsRead_AndNotSetAside()
        {
            WriteFileWithByteOrderMark("{\"schemaVersion\":1,\"settings\":{\"database\":{\"server\":\"S\"}}}");

            var load = Store().Load();

            Assert.Equal(SettingsLoadStatus.Loaded, load.Status);
            Assert.Equal("S", load.Settings.Database.Server);
            Assert.Empty(Directory.GetFiles(SettingsFolder, "*corrupt*"));
        }

        [Fact]
        public void Save_OverANewerSchemaWithAByteOrderMark_IsRefused_ItIsNotTakenForADamagedFile()
        {
            WriteFileWithByteOrderMark("{\"schemaVersion\":2,\"settings\":{}}");

            var result = Store().Save(Sample());

            Assert.Equal("settings.newerSchema", result.Failure!.Code);
        }

        // ── a file from a newer version ─────────────────────────────────────

        [Fact]
        public void Load_ANewerSchema_UsesDefaultsAndLeavesTheFileAlone()
        {
            const string content = "{\"schemaVersion\":2,\"settings\":{\"somethingNew\":[1,2]}}";
            WriteFile(content);

            var load = Store().Load();

            Assert.Equal(SettingsLoadStatus.NewerSchema, load.Status);
            AssertSame(new CardCheckSettings(), load.Settings);
            Assert.Equal(content, File.ReadAllText(FilePath));
            Assert.Empty(Directory.GetFiles(SettingsFolder, "*corrupt*"));
        }

        [Fact]
        public void Save_OverANewerSchema_IsRefused_EvenWithoutALoadFirst()
        {
            const string content = "{\"schemaVersion\":2,\"settings\":{}}";
            WriteFile(content);

            var result = Store().Save(Sample());

            Assert.False(result.IsSuccess);
            Assert.Equal(FailureKind.InvalidConfiguration, result.Failure!.Kind);
            Assert.Equal("settings.newerSchema", result.Failure.Code);
            Assert.Equal(content, File.ReadAllText(FilePath));
            Assert.Empty(LeftoverFiles());
        }

        [Fact]
        public void Save_OverADamagedFile_Replaces_BecauseLoadWouldHaveSetItAside()
        {
            WriteFile("garbage");

            Assert.True(Store().Save(Sample()).IsSuccess);
            Assert.Equal(SettingsLoadStatus.Loaded, Store().Load().Status);
        }

        // ── a file that cannot be used right now ────────────────────────────

        [Fact]
        public void Load_AFileAnotherProgramHolds_IsUnreadableAndStaysWhereItIs()
        {
            Store().Save(Sample());

            using (new FileStream(FilePath, FileMode.Open, System.IO.FileAccess.Read, FileShare.None))
            {
                var load = Store().Load();

                Assert.Equal(SettingsLoadStatus.Unreadable, load.Status);
                AssertSame(new CardCheckSettings(), load.Settings);
            }

            AssertSame(Sample(), Store().Load().Settings);
        }

        [Fact]
        public void Save_OverAFileItCannotRead_IsRefusedInsteadOfOverwritingBlindly()
        {
            Store().Save(Sample());
            var before = File.ReadAllBytes(FilePath);

            using (new FileStream(FilePath, FileMode.Open, System.IO.FileAccess.Read, FileShare.None))
            {
                var result = Store().Save(new CardCheckSettings());

                Assert.False(result.IsSuccess);
                Assert.Equal("settings.save.unreadable", result.Failure!.Code);
            }

            Assert.Equal(before, File.ReadAllBytes(FilePath));
        }

        [Fact]
        public void Save_WhenTheFileCannotBeSwappedIn_FailsKeepsTheOldFileWholeAndRemovesTheTemporaryOne()
        {
            Store().Save(Sample());
            var before = File.ReadAllBytes(FilePath);

            // Readable by the store, but without permission to be replaced.
            using (new FileStream(FilePath, FileMode.Open, System.IO.FileAccess.Read, FileShare.Read))
            {
                var result = Store().Save(new CardCheckSettings { DefaultProjectPath = "new" });

                Assert.False(result.IsSuccess);
                Assert.True(result.Failure!.Code == "settings.save.io" || result.Failure.Code == "settings.save.denied", result.Failure.Code);

                // The message of the settings window sends the operator to the log, so the log has to say it.
                Assert.Contains("could not be saved", _log.Everything, StringComparison.Ordinal);
            }

            Assert.Equal(before, File.ReadAllBytes(FilePath));
            Assert.Empty(LeftoverFiles());
        }

        [Fact]
        public void Save_WhenAFileIsInTheWayOfTheFolder_FailsWithoutThrowing()
        {
            File.WriteAllText(SettingsFolder, "a file where the folder should be");

            var result = Store().Save(Sample());

            Assert.False(result.IsSuccess);
            Assert.Equal(FailureKind.Io, result.Failure!.Kind);
            Assert.Equal("settings.save.io", result.Failure.Code);
            Assert.Contains("could not be saved: an I/O error", _log.Everything, StringComparison.Ordinal);
            Assert.DoesNotContain(_folder.Path, _log.Everything, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Save_FromManyThreadsOnOneStore_NeverLeavesATornFile()
        {
            var store = Store();

            var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(thread => Task.Run(() =>
                Enumerable.Range(0, 25).Select(i => store.Save(new CardCheckSettings { DefaultProjectPath = $"thread-{thread}-{i}" })).ToArray())));

            Assert.All(results.SelectMany(r => r), r => Assert.True(r.IsSuccess));
            var final = Store().Load();
            Assert.Equal(SettingsLoadStatus.Loaded, final.Status);
            Assert.Matches("^thread-[0-7]-([0-9]|1[0-9]|2[0-4])$", final.Settings.DefaultProjectPath);
            Assert.Empty(LeftoverFiles());
        }

        // ── what is logged ──────────────────────────────────────────────────

        [Fact]
        public void TheLog_NeverHoldsAValueOfASetting()
        {
            var store = Store();
            store.Save(Sample());
            store.Load();
            WriteFile("{ garbage with " + SecretServer + " and " + SecretTable);
            store.Load();
            WriteFile("{\"schemaVersion\":9,\"settings\":{\"x\":\"" + SecretDatabase + "\"}}");
            store.Load();
            store.Save(Sample());

            Assert.NotEmpty(_log.Entries);
            foreach (var secret in new[] { SecretServer, SecretDatabase, SecretTable, SecretFolder })
            {
                Assert.DoesNotContain(secret, _log.Everything, StringComparison.Ordinal);
            }

            Assert.DoesNotContain(_folder.Path, _log.Everything, StringComparison.OrdinalIgnoreCase);
        }

        // ── arguments and the location ──────────────────────────────────────

        [Fact]
        public void Arguments_AreChecked()
        {
            Assert.Throws<ArgumentException>(() => CardCheckServices.CreateSettingsStore(" ", _log));
            Assert.Throws<ArgumentNullException>(() => CardCheckServices.CreateSettingsStore(SettingsFolder, null!));
            Assert.Throws<ArgumentNullException>(() => Store().Save(null!));
        }

        [Fact]
        public void TheDefaultFolder_IsTheAssistantsOwnFolderBelowTheUsersApplicationData_ApartFromTheHostsFiles()
        {
            var expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RFiDGear", "Extensions", "CardCheckAssistant");

            Assert.Equal(expected, CardCheckServices.DefaultSettingsFolder());
            Assert.NotEqual(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RFiDGear"), CardCheckServices.DefaultSettingsFolder());
        }
    }
}
