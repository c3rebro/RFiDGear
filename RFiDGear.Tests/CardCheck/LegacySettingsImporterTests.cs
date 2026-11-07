using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using RFiDGear.ExtensionLibraries.CardCheck;

using Xunit;

namespace RFiDGear.Tests.CardCheck
{
    /// <summary>
    /// The import of the old assistant's settings. Kept to the properties that hold whatever the extension looks
    /// like: credentials are never read, the old file is never changed, an untrusted file cannot do harm, the import
    /// happens once and only with consent, and nothing entered in this version is overwritten.
    /// </summary>
    public sealed class LegacySettingsImporterTests : IDisposable
    {
        private const string SecretUser = "svc-cardcheck-SECRETUSER";
        private const string SecretPassword = "3A-85-DC-SECRETPWD";
        private const string SecretPort = "14331";

        private readonly TempFolder _appData = new TempFolder();
        private readonly TempFolder _store = new TempFolder();
        private readonly RecordingCardCheckLog _log = new RecordingCardCheckLog();

        public void Dispose()
        {
            _appData.Dispose();
            _store.Dispose();
        }

        private ISettingsStore Store() => CardCheckServices.CreateSettingsStore(Path.Combine(_store.Path, "new"), _log);

        private LegacySettingsImporter Importer(ISettingsStore? store = null) =>
            (LegacySettingsImporter)CardCheckServices.CreateLegacyImporter(store ?? Store(), _appData.Path, _log);

        private string UnpackagedFile => Path.Combine(_appData.Path, "CardCheckAssistant", "settings.xml");

        private string PackagedFile =>
            Path.Combine(_appData.Path, "Packages", "Vendor.CardCheckAssistant_8wekyb3d8bbwe", "LocalCache", "Local", "CardCheckAssistant", "settings.xml");

        private static string Write(string path, string content)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content, new UTF8Encoding(false));

            return path;
        }

        private static string Xml(params string[] elements) =>
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<DefaultSettings xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">\n"
            + string.Concat(elements) + "\n</DefaultSettings>";

        private static string El(string name, string value) => $"<{name}>{SecurityElement.Escape(value)}</{name}>";

        private static string FullSample(string useMssql = "true") => Xml(
            El("ManifestVersion", "1.0.44"),
            El("CardCheckUseMSSQL", useMssql),
            El("SelectedDBServerName", "SRV01"),
            El("SelectedDBServerPort", SecretPort),
            El("SelectedDBTableName", "dbo.T_CardCheck_Prod"),
            El("SelectedDBName", "OrdersDb"),
            El("SelectedDBUsername", SecretUser),
            El("SelectedDBUserPwd", SecretPassword),
            El("DefaultTheme", "Light"),
            El("DefaultReportLanguage", "englisch"),
            El("AutoRunProjectOnStart", "false"),
            El("LastUsedProjectPath", Path.Combine("projects", "last.rfPrj")),
            El("RemoveTemporaryReportsIsEnabled", "true"),
            El("CreateSubdirectoryIsEnabled", "true"),
            El("LastUsedCustomProjectPath", Path.Combine("projects", "custom.rfPrj")),
            El("LastUsedDefaultProject", Path.Combine("projects", "default.rfPrj")),
            El("DefaultRFIDGearExePath", Path.Combine("tools", "RFiDGear.exe")),
            El("DefaultProjectOutputPath", "reports"),
            "<CardCheckTextTemplates>"
            + "<CardCheckTextTemplate><TemplateTextName>Greeting</TemplateTextName><TemplateTextContent>Hello &amp; welcome\nline 2 </TemplateTextContent></CardCheckTextTemplate>"
            + "<CardCheckTextTemplate><TemplateTextName>N/A</TemplateTextName><TemplateTextContent /></CardCheckTextTemplate>"
            + "</CardCheckTextTemplates>");

        private static Dictionary<string, LegacyElementDisposition> Dispositions(LegacyImportPreview preview) =>
            preview.Items.ToDictionary(i => i.Element, i => i.Disposition);

        // The old file must never change: same bytes, same time stamp.
        private sealed class Untouched
        {
            private readonly string _path;
            private readonly byte[] _bytes;
            private readonly DateTime _written;

            public Untouched(string path)
            {
                _path = path;
                _bytes = File.ReadAllBytes(path);
                _written = File.GetLastWriteTimeUtc(path);
            }

            public void AssertStill()
            {
                Assert.Equal(_bytes, File.ReadAllBytes(_path));
                Assert.Equal(_written, File.GetLastWriteTimeUtc(_path));
            }
        }

        // ── finding the old files ───────────────────────────────────────────

        [Fact]
        public void Discover_FindsTheUnpackagedAndThePackagedFile_NewestFirst_AndNothingWhenThereIsNone()
        {
            Assert.Empty(Importer().Discover());

            Write(UnpackagedFile, Xml());
            Write(PackagedFile, Xml());
            File.SetLastWriteTimeUtc(UnpackagedFile, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            File.SetLastWriteTimeUtc(PackagedFile, new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));

            Assert.Equal(new[] { PackagedFile, UnpackagedFile }, Importer().Discover().Select(c => c.Path));
        }

        // ── reading a file ──────────────────────────────────────────────────

        [Fact]
        public void Preview_ProposesEveryValueThatThisVersionUses_AndSaysWhatBecameOfEveryElement()
        {
            var preview = Importer().Preview(Write(UnpackagedFile, FullSample())).Value;
            var dispositions = Dispositions(preview);

            Assert.Equal("SRV01", preview.Proposed.Database.Server);
            Assert.Equal("OrdersDb", preview.Proposed.Database.Database);
            Assert.Equal("dbo.T_CardCheck_Prod", preview.Proposed.Database.Table);
            Assert.Equal(ReportLanguage.English, preview.Proposed.Report.DefaultLanguage);
            Assert.True(preview.Proposed.Report.RemoveTemporaryReports);
            Assert.True(preview.Proposed.Report.CreateSubdirectory);
            Assert.Equal("reports", preview.Proposed.Report.OutputPath);
            Assert.Equal(Path.Combine("projects", "default.rfPrj"), preview.Proposed.DefaultProjectPath);
            Assert.Equal(Path.Combine("projects", "custom.rfPrj"), preview.Proposed.CustomProjectPath);
            Assert.Equal("projects", preview.Proposed.LastProjectFolder);
            var template = Assert.Single(preview.Proposed.TextTemplates);
            Assert.Equal("Hello & welcome\nline 2 ", template.Content);
            Assert.Empty(preview.Notes);
            Assert.Equal(LegacyElementDisposition.Imported, dispositions["SelectedDBServerName"]);
            Assert.Equal(LegacyElementDisposition.NotUsed, dispositions["DefaultTheme"]);
            Assert.Equal(LegacyElementDisposition.NotUsed, dispositions["DefaultRFIDGearExePath"]);
        }

        [Fact]
        public void Credentials_AreNeverReadHeldProposedOrLogged()
        {
            var path = Write(UnpackagedFile, FullSample());
            var importer = Importer();

            var preview = importer.Preview(path).Value;
            importer.Apply(preview);

            var dispositions = Dispositions(preview);
            Assert.Equal(LegacyElementDisposition.NeverRead, dispositions["SelectedDBUsername"]);
            Assert.Equal(LegacyElementDisposition.NeverRead, dispositions["SelectedDBUserPwd"]);
            Assert.Equal(LegacyElementDisposition.NeverRead, dispositions["SelectedDBServerPort"]);
            var everything = JsonSerializer.Serialize(preview) + File.ReadAllText(Path.Combine(_store.Path, "new", "settings.json")) + _log.Everything;
            foreach (var secret in new[] { SecretUser, SecretPassword, SecretPort })
            {
                Assert.DoesNotContain(secret, everything, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void AnSqliteConfiguration_IsNotTakenOver_AndTheOperatorIsTold()
        {
            var preview = Importer().Preview(Write(UnpackagedFile, FullSample("false"))).Value;

            Assert.Equal(new DatabaseSettings(), preview.Proposed.Database);
            Assert.Equal(LegacyElementDisposition.Skipped, Dispositions(preview)["SelectedDBServerName"]);
            Assert.Equal("import.sqlite.unsupported", Assert.Single(preview.Notes).Code);
            Assert.True(preview.Proposed.Report.CreateSubdirectory);
        }

        [Fact]
        public void Preview_TextBlocks_KeepTheirContent_AndThePlaceholderIsLeftOut()
        {
            var preview = Importer().Preview(Write(UnpackagedFile, Xml(
                "<CardCheckTextTemplates>"
                + "<CardCheckTextTemplate><TemplateTextName>N/A</TemplateTextName><TemplateTextContent>placeholder</TemplateTextContent></CardCheckTextTemplate>"
                + "<CardCheckTextTemplate><TemplateTextName>One</TemplateTextName><TemplateTextContent>  keep   blanks </TemplateTextContent></CardCheckTextTemplate>"
                + "</CardCheckTextTemplates>"))).Value;

            var template = Assert.Single(preview.Proposed.TextTemplates);
            Assert.Equal("One", template.Name);
            Assert.Equal("  keep   blanks ", template.Content);
        }

        [Fact]
        public void Preview_UnknownEmptyAndUnusableElements_AreReportedByNameAndLeaveTheDefaults()
        {
            var preview = Importer().Preview(Write(UnpackagedFile, Xml(
                "<FutureSetting>SECRET-FUTURE-VALUE</FutureSetting>",
                "<SelectedDBName />",
                El("CreateSubdirectoryIsEnabled", "perhaps")))).Value;

            var dispositions = Dispositions(preview);
            Assert.Equal(LegacyElementDisposition.Unknown, dispositions["FutureSetting"]);
            Assert.Equal(LegacyElementDisposition.Empty, dispositions["SelectedDBName"]);
            Assert.Equal(LegacyElementDisposition.Invalid, dispositions["CreateSubdirectoryIsEnabled"]);
            Assert.False(preview.Proposed.Report.CreateSubdirectory);
            Assert.DoesNotContain("SECRET-FUTURE-VALUE", JsonSerializer.Serialize(preview), StringComparison.Ordinal);
        }

        [Fact]
        public void Preview_WritesNothing_NeitherToTheNewSettingsNorToTheOldFile()
        {
            var path = Write(UnpackagedFile, FullSample());
            var untouched = new Untouched(path);

            Importer().Preview(path);

            untouched.AssertStill();
            Assert.False(Directory.Exists(Path.Combine(_store.Path, "new")));
        }

        [Fact]
        public void Preview_TheOldAssistantStillHoldsTheFile_ItCanBeReadStill()
        {
            var path = Write(UnpackagedFile, FullSample());

            using (new FileStream(path, FileMode.Open, System.IO.FileAccess.ReadWrite, FileShare.ReadWrite))
            {
                Assert.True(Importer().Preview(path).IsSuccess);
            }
        }

        // ── files that must be refused ──────────────────────────────────────

        [Theory]
        [InlineData("this is not xml at all")]
        [InlineData("<DefaultSettings><SelectedDBServerName><nested>complex</nested></SelectedDBServerName></DefaultSettings>")]
        [InlineData("<DefaultSettings/>trailing garbage")]
        [InlineData("<DefaultSettings><SelectedDBName>x</SelectedDBName></DefaultSettings><DefaultSettings />")]
        public void Preview_ADamagedFile_IsRefusedAsCorrupt(string content)
        {
            var result = Importer().Preview(Write(UnpackagedFile, content));

            Assert.Equal("import.source.corrupt", result.Failure!.Code);
        }

        [Fact]
        public void Preview_ADocumentThatIsNotASettingsFile_IsRefused()
        {
            Assert.Equal("import.source.notSettings", Importer().Preview(Write(UnpackagedFile, "<Other><SelectedDBName>x</SelectedDBName></Other>")).Failure!.Code);
            Assert.Equal("import.source.notSettings", Importer().Preview(Write(UnpackagedFile, "<wrapper><DefaultSettings /></wrapper>")).Failure!.Code);
        }

        [Fact]
        public void Preview_ADocumentTypeDeclaration_IsRefused_AnEntityExpansionIncluded()
        {
            // Small on purpose: it is the declaration that is refused, not the size of what it would expand to.
            var xml = "<?xml version=\"1.0\"?><!DOCTYPE d [<!ENTITY a \"lol\"><!ENTITY b \"&a;&a;&a;&a;&a;\">]>"
                + "<DefaultSettings><SelectedDBServerName>&b;</SelectedDBServerName></DefaultSettings>";

            var result = Importer().Preview(Write(UnpackagedFile, xml));

            Assert.False(result.IsSuccess);
            Assert.Equal("import.source.corrupt", result.Failure!.Code);
        }

        [Fact]
        public void Preview_AnExternalEntity_IsNeverFollowed()
        {
            var secretFile = Write(Path.Combine(_appData.Path, "secret.txt"), "SECRET-LOCAL-FILE-CONTENT");
            var xml = $"<?xml version=\"1.0\"?><!DOCTYPE d [<!ENTITY x SYSTEM \"{new Uri(secretFile).AbsoluteUri}\">]><DefaultSettings><SelectedDBServerName>&x;</SelectedDBServerName></DefaultSettings>";

            var result = Importer().Preview(Write(UnpackagedFile, xml));

            Assert.False(result.IsSuccess);
            Assert.DoesNotContain("SECRET-LOCAL-FILE-CONTENT", result.Failure!.SafeMessage + _log.Everything, StringComparison.Ordinal);
        }

        [Fact]
        public void Preview_AFileOverTheSizeLimit_IsRefused_AndAMissingPathHasAReason()
        {
            var big = Write(UnpackagedFile, new string(' ', (int)LegacySettingsReader.MaxFileBytes + 1));

            Assert.Equal("import.source.tooLarge", Importer().Preview(big).Failure!.Code);
            Assert.Equal("import.source.missing", Importer().Preview(Path.Combine(_appData.Path, "nope.xml")).Failure!.Code);
        }

        // ── applying ────────────────────────────────────────────────────────

        [Fact]
        public void Apply_SavesTheMergedSettingsWithAMarker_AndLeavesTheOldFileAlone()
        {
            var path = Write(UnpackagedFile, FullSample());
            var untouched = new Untouched(path);
            var store = Store();
            var importer = Importer(store);

            var result = importer.Apply(importer.Preview(path).Value);

            Assert.True(result.IsSuccess);
            var saved = store.Load().Settings;
            Assert.Equal("SRV01", saved.Database.Server);
            Assert.Equal(ReportLanguage.English, saved.Report.DefaultLanguage);
            Assert.Equal(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant(), saved.LegacyImport!.SourceSha256);
            Assert.False(saved.LegacyImport.Declined);
            untouched.AssertStill();
        }

        [Fact]
        public void Apply_Twice_IsRefused_TheImportIsDoneOnce()
        {
            var importer = Importer();
            var preview = importer.Preview(Write(UnpackagedFile, FullSample())).Value;

            Assert.True(importer.Apply(preview).IsSuccess);

            Assert.Equal("import.alreadyDone", importer.Apply(preview).Failure!.Code);
        }

        [Fact]
        public void Apply_WhenTheFileChangedSincePreview_IsRefusedAndWritesNothing()
        {
            var path = Write(UnpackagedFile, FullSample());
            var importer = Importer();
            var preview = importer.Preview(path).Value;
            Write(path, Xml(El("SelectedDBServerName", "SOMETHING-ELSE")));

            var result = importer.Apply(preview);

            Assert.Equal("import.source.changed", result.Failure!.Code);
            Assert.False(Directory.Exists(Path.Combine(_store.Path, "new")));
        }

        [Fact]
        public void Apply_KeepsWhatWasAlreadyEnteredInThisVersion_AndFillsOnlyTheRest()
        {
            var store = Store();
            store.Save(new CardCheckSettings
            {
                Database = new DatabaseSettings { Server = "MY-OWN-SERVER", Table = "dbo.MyTable" },
                Report = new ReportSettings { OutputPath = "D:\\mine", DefaultLanguage = ReportLanguage.English },
                TextTemplates = new[] { new TextTemplate { Name = "greeting", Content = "mine" } },
            });
            var importer = Importer(store);

            var preview = importer.Preview(Write(UnpackagedFile, Xml(
                El("SelectedDBServerName", "OLD-SERVER"),
                El("SelectedDBName", "OldDb"),
                El("SelectedDBTableName", "dbo.OldTable"),
                El("DefaultProjectOutputPath", "D:\\old"),
                "<CardCheckTextTemplates><CardCheckTextTemplate><TemplateTextName>Greeting</TemplateTextName><TemplateTextContent>old</TemplateTextContent></CardCheckTextTemplate>"
                + "<CardCheckTextTemplate><TemplateTextName>New one</TemplateTextName><TemplateTextContent>n</TemplateTextContent></CardCheckTextTemplate></CardCheckTextTemplates>"))).Value;
            Assert.True(importer.Apply(preview).IsSuccess);
            var settings = store.Load().Settings;

            Assert.Equal("MY-OWN-SERVER", settings.Database.Server);
            Assert.Equal("OldDb", settings.Database.Database);
            Assert.Equal("dbo.MyTable", settings.Database.Table);
            Assert.Equal("D:\\mine", settings.Report.OutputPath);
            Assert.Equal(new[] { "greeting", "New one" }, settings.TextTemplates.Select(t => t.Name));
            Assert.Equal("mine", settings.TextTemplates[0].Content);
        }

        // ── the offer ───────────────────────────────────────────────────────

        private sealed class FakePrompt : IImportPrompt
        {
            public ImportDecision? Decision { get; set; }

            public bool Confirm { get; set; } = true;

            public Exception? Fault { get; set; }

            // Runs while the operator is "looking at the question", before the answer is given.
            public Action? WhileChoosing { get; set; }

            public int ChooseCalls { get; private set; }

            public int ConfirmCalls { get; private set; }

            public IReadOnlyList<LegacySettingsCandidate>? SeenCandidates { get; private set; }

            public LegacyImportPreview? SeenPreview { get; private set; }

            public CancellationToken SeenToken { get; private set; }

            public Task<ImportDecision> ChooseAsync(IReadOnlyList<LegacySettingsCandidate> candidates, CancellationToken cancellationToken)
            {
                ChooseCalls++;
                SeenCandidates = candidates;
                SeenToken = cancellationToken;
                WhileChoosing?.Invoke();

                return Fault != null ? Task.FromException<ImportDecision>(Fault) : Task.FromResult(Decision ?? new ImportDecision(ImportChoice.NotNow));
            }

            public Task<bool> ConfirmAsync(LegacyImportPreview preview, CancellationToken cancellationToken)
            {
                ConfirmCalls++;
                SeenPreview = preview;
                SeenToken = cancellationToken;

                return Task.FromResult(Confirm);
            }
        }

        private Task<ImportOffer> Offer(FakePrompt prompt, ISettingsStore? store = null) =>
            Importer(store).OfferAsync(prompt, CancellationToken.None);

        [Fact]
        public async Task Offer_WithNothingToImport_AsksNothing()
        {
            var prompt = new FakePrompt();

            var offer = await Offer(prompt);

            Assert.Equal(ImportOutcome.NothingToImport, offer.Outcome);
            Assert.Equal(0, prompt.ChooseCalls);
        }

        [Fact]
        public async Task Offer_Import_AsksForConfirmation_ThenImports_AndNeverAsksAgain()
        {
            var path = Write(UnpackagedFile, FullSample("false"));
            var untouched = new Untouched(path);
            var store = Store();
            var prompt = new FakePrompt { Decision = new ImportDecision(ImportChoice.Import, path) };

            var offer = await Offer(prompt, store);
            var again = await Offer(prompt, store);

            Assert.Equal(ImportOutcome.Imported, offer.Outcome);
            Assert.Contains(offer.Notes, n => n.Code == "import.sqlite.unsupported");
            Assert.Equal(1, prompt.ConfirmCalls);
            // The operator is shown the file that was found and what would become of it, before anything is written.
            Assert.Equal(path, Assert.Single(prompt.SeenCandidates!).Path);
            Assert.Equal(path, prompt.SeenPreview!.SourcePath);
            Assert.NotNull(store.Load().Settings.LegacyImport);
            Assert.Equal(ImportOutcome.AlreadyHandled, again.Outcome);
            Assert.Equal(1, prompt.ChooseCalls);
            untouched.AssertStill();
        }

        [Fact]
        public async Task Offer_NotConfirmed_WritesNothing()
        {
            var path = Write(UnpackagedFile, FullSample());
            var prompt = new FakePrompt { Decision = new ImportDecision(ImportChoice.Import, path), Confirm = false };

            var offer = await Offer(prompt);

            Assert.Equal(ImportOutcome.Cancelled, offer.Outcome);
            Assert.False(Directory.Exists(Path.Combine(_store.Path, "new")));
        }

        [Fact]
        public async Task Offer_NotNow_WritesNothing_AndAsksAgainNextTime()
        {
            Write(UnpackagedFile, FullSample());
            var prompt = new FakePrompt { Decision = new ImportDecision(ImportChoice.NotNow) };
            var store = Store();

            await Offer(prompt, store);
            await Offer(prompt, store);

            Assert.Equal(2, prompt.ChooseCalls);
            Assert.False(Directory.Exists(Path.Combine(_store.Path, "new")));
        }

        [Fact]
        public async Task Offer_Never_RemembersIt_ImportsNothing_AndNeverAsksAgain()
        {
            Write(UnpackagedFile, FullSample());
            var store = Store();

            // Something is entered while the question is open; "never" must be written onto that, not onto the
            // settings as they were when the question was asked.
            var prompt = new FakePrompt
            {
                Decision = new ImportDecision(ImportChoice.Never),
                WhileChoosing = () => store.Save(new CardCheckSettings { DefaultProjectPath = "entered-meanwhile" }),
            };

            var first = await Offer(prompt, store);
            var second = await Offer(prompt, store);

            Assert.Equal(ImportOutcome.Declined, first.Outcome);
            Assert.Equal(ImportOutcome.AlreadyHandled, second.Outcome);
            var settings = store.Load().Settings;
            Assert.True(settings.LegacyImport!.Declined);
            Assert.Equal(string.Empty, settings.Database.Server);
            Assert.Equal("entered-meanwhile", settings.DefaultProjectPath);
        }

        [Fact]
        public async Task Offer_APromptThatThrows_IsAFailure_LoggedByTypeOnly()
        {
            Write(UnpackagedFile, FullSample());
            var prompt = new FakePrompt { Fault = new InvalidOperationException("SECRET-DIALOG-FAULT") };

            var offer = await Offer(prompt);

            Assert.Equal(ImportOutcome.Failed, offer.Outcome);
            Assert.DoesNotContain("SECRET-DIALOG-FAULT", _log.Everything + offer.Failure!.SafeMessage, StringComparison.Ordinal);
            Assert.Contains("import.offer", _log.Everything, StringComparison.Ordinal);
            Assert.Contains(nameof(InvalidOperationException), _log.Everything, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Offer_NoCredentialOrPathEverReachesTheLog()
        {
            var path = Write(UnpackagedFile, FullSample());

            await Offer(new FakePrompt { Decision = new ImportDecision(ImportChoice.Import, path) });

            Assert.NotEmpty(_log.Entries);
            foreach (var secret in new[] { SecretUser, SecretPassword, SecretPort, "OrdersDb", "T_CardCheck_Prod" })
            {
                Assert.DoesNotContain(secret, _log.Everything, StringComparison.Ordinal);
            }

            Assert.DoesNotContain(_appData.Path, _log.Everything, StringComparison.OrdinalIgnoreCase);
        }
    }
}
