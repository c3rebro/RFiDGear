using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

using RFiDGear.ExtensionLibraries.CardCheck;
using RFiDGear.Extensions.CardCheckAssistant.Localization;
using RFiDGear.Extensions.CardCheckAssistant.Services;
using RFiDGear.Extensions.CardCheckAssistant.ViewModels;
using RFiDGear.Tests.CardCheck;

using Xunit;

using static RFiDGear.Tests.CardCheckAssistant.ViewModelTestSupport;

namespace RFiDGear.Tests.CardCheckAssistant
{
    /// <summary>
    /// What the page remembers and offers: it starts from the remembered project without ever overwriting what the
    /// operator typed, opens the picker where the last project was, remembers where a project was loaded from, and makes
    /// the one-time offer to import the old assistant's settings. None of it may get in the way of using the page.
    /// </summary>
    public sealed class AssistantMemoryTests : IDisposable
    {
        private readonly TempFolder _folder = new TempFolder();
        private readonly RecordingCardCheckLog _log = new RecordingCardCheckLog();

        public void Dispose() => _folder.Dispose();

        private sealed class FakeSettings : IAssistantSettings
        {
            public AssistantDefaults Defaults { get; set; } = AssistantDefaults.None;

            public Exception? LoadFails { get; set; }

            public int Loads { get; private set; }

            public List<string> Remembered { get; } = new List<string>();

            public AssistantDefaults Load()
            {
                Loads++;

                return LoadFails != null ? throw LoadFails : Defaults;
            }

            public void RememberProjectFolder(string folder)
            {
                lock (Remembered)
                {
                    Remembered.Add(folder);
                }
            }
        }

        private sealed class FakeOffer : IImportOffer
        {
            public int Calls { get; private set; }

            // What the page handed over: its texts and the token that the window cancels when it closes.
            public LocalizedStrings? SeenStrings { get; private set; }

            public CancellationToken SeenToken { get; private set; }

            // What the offer does when the operator agrees: the settings change under the page.
            public Action? Imports { get; set; }

            public Task<bool> OfferAsync(LocalizedStrings strings, CancellationToken cancellationToken)
            {
                Calls++;
                SeenStrings = strings;
                SeenToken = cancellationToken;
                Imports?.Invoke();

                return Task.FromResult(Imports != null);
            }
        }

        private static CardCheckAssistantViewModel NewViewModel(FakeSettings settings, FakeOffer offer, IFileDialogService? dialogs = null) =>
            new CardCheckAssistantViewModel(new FakeExtensionHost(), Dispatcher.CurrentDispatcher, dialogs ?? new FakeFileDialogService(), null, settings, offer);

        // ── the page ────────────────────────────────────────────────────────

        [Fact]
        public async Task Open_PrefillsTheProject_ButNeverOverWhatWasTypedMeanwhile()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var remembered = new AssistantDefaults(Path.Combine("projects", "a.rfPrj"), "projects");
                using var prefilled = NewViewModel(new FakeSettings { Defaults = remembered }, new FakeOffer());
                using var typed = NewViewModel(new FakeSettings { Defaults = remembered }, new FakeOffer());

                await prefilled.Open();
                var opening = typed.Open();
                typed.ProjectPath = Path.Combine("mine", "b.rfPrj");   // typed while the settings are being read
                await opening;

                Assert.Equal(remembered.DefaultProjectPath, prefilled.ProjectPath);
                Assert.Equal(Path.Combine("mine", "b.rfPrj"), typed.ProjectPath);
            });
        }

        [Fact]
        public async Task Open_IsDoneOnce_AndAnImportMakesThePageReadItsDefaultsAgain()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var settings = new FakeSettings();
                var offer = new FakeOffer { Imports = () => settings.Defaults = new AssistantDefaults("imported.rfPrj", string.Empty) };
                using var viewModel = NewViewModel(settings, offer);

                await viewModel.Open();
                await viewModel.Open();

                Assert.Equal(1, offer.Calls);
                Assert.Same(viewModel.Strings, offer.SeenStrings);   // the questions are in the language the page shows
                Assert.True(offer.SeenToken.CanBeCanceled);          // the window can cancel the offer by closing
                Assert.Equal(2, settings.Loads);   // before the offer and again after the import, not once more for the second Open
                Assert.Equal("imported.rfPrj", viewModel.ProjectPath);
            });
        }

        [Fact]
        public async Task Open_WhenTheSettingsCannotBeRead_LeavesThePageWorking()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var viewModel = NewViewModel(new FakeSettings { LoadFails = new InvalidOperationException("SECRET") }, new FakeOffer());

                await viewModel.Open();   // does not throw

                Assert.Equal(string.Empty, viewModel.ProjectPath);
                viewModel.ProjectPath = "typed.rfPrj";
                Assert.Equal("typed.rfPrj", viewModel.ProjectPath);
            });
        }

        [Fact]
        public async Task Browse_OpensInTheFolderOfTheLastProject_UnlessAProjectIsEntered()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var dialogs = new FakeFileDialogService();
                var last = Path.Combine("projects", "last");
                using var viewModel = NewViewModel(new FakeSettings { Defaults = new AssistantDefaults(string.Empty, last) }, new FakeOffer(), dialogs);
                await viewModel.Open();

                viewModel.BrowseCommand.Execute(null);
                viewModel.ProjectPath = Path.Combine("entered", "p.rfPrj");
                viewModel.BrowseCommand.Execute(null);

                Assert.Equal(last + Path.DirectorySeparatorChar, dialogs.Requests[0].Current);
                Assert.Equal(Path.Combine("entered", "p.rfPrj"), dialogs.Requests[1].Current);
            });
        }

        [Fact]
        public async Task ALoadedProject_HasItsFolderRemembered_OnceWhileItStaysTheSame()
        {
            using var area = new TempArea();
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var settings = new FakeSettings();
                using var viewModel = NewViewModel(settings, new FakeOffer());
                await viewModel.Open();
                viewModel.ProjectPath = area.ProjectPath;

                await viewModel.RunCommand.ExecuteAsync();
                await WaitUntil(() => settings.Remembered.Count > 0, "the folder to be remembered");
                await viewModel.RunCommand.ExecuteAsync();
                await Flush();

                Assert.Equal(new[] { area.Directory }, settings.Remembered);
            });
        }

        // ── what is kept in the settings file ───────────────────────────────

        private ISettingsStore Store() => CardCheckServices.CreateSettingsStore(Path.Combine(_folder.Path, "settings"), _log);

        [Fact]
        public void SettingsMemory_RemembersTheFolder_AndChangesNothingElse()
        {
            var store = Store();
            store.Save(new CardCheckSettings { DefaultProjectPath = "default.rfPrj", Report = new ReportSettings { OutputPath = "reports" } });
            var memory = new SettingsMemory(store);

            memory.RememberProjectFolder("projects");

            var saved = store.Load().Settings;
            Assert.Equal("projects", saved.LastProjectFolder);
            Assert.Equal("default.rfPrj", saved.DefaultProjectPath);
            Assert.Equal("reports", saved.Report.OutputPath);
            Assert.Equal(new AssistantDefaults("default.rfPrj", "projects"), memory.Load());
        }

        [Fact]
        public void SettingsMemory_NeverWritesOverSettingsOfANewerVersion()
        {
            var folder = Path.Combine(_folder.Path, "settings");
            Directory.CreateDirectory(folder);
            const string content = "{\"schemaVersion\":2,\"settings\":{}}";
            File.WriteAllText(Path.Combine(folder, "settings.json"), content);

            new SettingsMemory(Store()).RememberProjectFolder("projects");

            Assert.Equal(content, File.ReadAllText(Path.Combine(folder, "settings.json")));
        }

        // ── the offer to import ─────────────────────────────────────────────

        private sealed class FakeDialogs : IImportDialogs
        {
            public ImportAnswer Answer { get; set; } = ImportAnswer.NotNow;

            public bool Agrees { get; set; } = true;

            public int Asked { get; private set; }

            public int Confirmed { get; private set; }

            public string AskedTitle { get; private set; } = string.Empty;

            public string AskedText { get; private set; } = string.Empty;

            public string ConfirmedTitle { get; private set; } = string.Empty;

            public string ConfirmedText { get; private set; } = string.Empty;

            // The three buttons of the question, in the order they are given: import, not now, never.
            public string[] Buttons { get; private set; } = Array.Empty<string>();

            public ImportAnswer Ask(string title, string text, string importButton, string notNowButton, string neverButton)
            {
                Asked++;
                AskedTitle = title;
                AskedText = text;
                Buttons = new[] { importButton, notNowButton, neverButton };

                return Answer;
            }

            public bool Confirm(string title, string text)
            {
                Confirmed++;
                ConfirmedTitle = title;
                ConfirmedText = text;

                return Agrees;
            }
        }

        private string AppData => Path.Combine(_folder.Path, "appdata");

        private void WriteOldSettings(bool useMssql = true)
        {
            Directory.CreateDirectory(Path.Combine(AppData, "CardCheckAssistant"));
            File.WriteAllText(
                Path.Combine(AppData, "CardCheckAssistant", "settings.xml"),
                "<DefaultSettings><CardCheckUseMSSQL>" + (useMssql ? "true" : "false") + "</CardCheckUseMSSQL><SelectedDBServerName>SRV</SelectedDBServerName><SelectedDBName>Db</SelectedDBName></DefaultSettings>");
        }

        private ImportOfferService Offer(ISettingsStore store, FakeDialogs dialogs) =>
            new ImportOfferService(CardCheckServices.CreateLegacyImporter(store, AppData, _log), dialogs, Dispatcher.CurrentDispatcher);

        [Fact]
        public async Task TheOffer_Import_AsksThenConfirms_AndTakesTheSettingsOver()
        {
            WriteOldSettings();
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var store = Store();
                var dialogs = new FakeDialogs { Answer = ImportAnswer.Import };

                var imported = await Offer(store, dialogs).OfferAsync(new LocalizedStrings("en-US"), CancellationToken.None);

                Assert.True(imported);
                Assert.Equal((1, 1), (dialogs.Asked, dialogs.Confirmed));
                Assert.Contains("settings.xml", dialogs.AskedText, StringComparison.Ordinal);
                Assert.Equal("SRV", store.Load().Settings.Database.Server);
            });
        }

        [Fact]
        public async Task TheOffer_NotNowImportsNothingAndAsksAgain_NeverIsRememberedAndAsksNoMore()
        {
            WriteOldSettings();
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var store = Store();
                var dialogs = new FakeDialogs();
                var offer = Offer(store, dialogs);

                Assert.False(await offer.OfferAsync(new LocalizedStrings("en-US"), CancellationToken.None));
                Assert.False(await offer.OfferAsync(new LocalizedStrings("en-US"), CancellationToken.None));
                Assert.Equal(2, dialogs.Asked);
                Assert.Equal(string.Empty, store.Load().Settings.Database.Server);

                dialogs.Answer = ImportAnswer.Never;
                Assert.False(await offer.OfferAsync(new LocalizedStrings("en-US"), CancellationToken.None));
                Assert.False(await offer.OfferAsync(new LocalizedStrings("en-US"), CancellationToken.None));
                Assert.Equal(3, dialogs.Asked);   // the fourth call asked nothing
                Assert.Equal(0, dialogs.Confirmed);
            });
        }

        [Fact]
        public async Task TheOffer_WhenTheOperatorDoesNotConfirm_WritesNothing()
        {
            WriteOldSettings();
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var store = Store();
                var dialogs = new FakeDialogs { Answer = ImportAnswer.Import, Agrees = false };

                var imported = await Offer(store, dialogs).OfferAsync(new LocalizedStrings("en-US"), CancellationToken.None);

                Assert.False(imported);
                Assert.False(File.Exists(Path.Combine(_folder.Path, "settings", "settings.json")));
            });
        }

        [Fact]
        public async Task TheQuestions_AreInThePagesLanguage_AndAnSqliteConfigurationIsSaidBeforeTheOperatorAgrees()
        {
            WriteOldSettings(useMssql: false);
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var german = new FakeDialogs { Answer = ImportAnswer.Import };
                var english = new FakeDialogs { Answer = ImportAnswer.Import };

                await Offer(Store(), german).OfferAsync(new LocalizedStrings("de-DE"), CancellationToken.None);
                await Offer(CardCheckServices.CreateSettingsStore(Path.Combine(_folder.Path, "other"), _log), english).OfferAsync(new LocalizedStrings("en-US"), CancellationToken.None);

                Assert.Contains("alten Card Check Assistant", german.AskedTitle, StringComparison.Ordinal);
                Assert.Equal(german.AskedTitle, german.ConfirmedTitle);
                Assert.Equal(new[] { "Übernehmen", "Jetzt nicht", "Nie wieder fragen" }, german.Buttons);
                Assert.Contains("SQLite", german.ConfirmedText, StringComparison.Ordinal);
                Assert.Contains("Settings of the old", english.AskedTitle, StringComparison.Ordinal);
                Assert.Equal(new[] { "Import", "Not now", "Never ask again" }, english.Buttons);
                Assert.Contains("SQLite", english.ConfirmedText, StringComparison.Ordinal);
            });
        }

        // ── the test run itself ─────────────────────────────────────────────

        [Fact]
        public async Task TheTestRun_IsSandboxed_TheRealFactoryNeverTouchesTheUsersApplicationData()
        {
            Assert.False(string.IsNullOrWhiteSpace(AppDataSandbox.Root));
            Assert.Equal(AppDataSandbox.Root, Environment.GetEnvironmentVariable(AssistantServicesFactory.AppDataOverrideVariable));

            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var services = AssistantServicesFactory.Create(Dispatcher.CurrentDispatcher);
                Assert.NotSame(AssistantServices.None, services);

                // Written through the real store. Other tests of the run may have remembered folders there as well, so
                // what is checked is where the file is, not what is in it.
                services.Settings.RememberProjectFolder(Path.Combine(AppDataSandbox.Root, "probe"));

                Assert.True(File.Exists(Path.Combine(AppDataSandbox.Root, "settings", "settings.json")));
            });
        }
    }
}
