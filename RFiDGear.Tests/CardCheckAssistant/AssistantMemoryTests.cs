using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

using RFiDGear.ExtensionLibraries.CardCheck;
using RFiDGear.Extensions.CardCheckAssistant.Localization;
using RFiDGear.Extensions.CardCheckAssistant.Services;
using RFiDGear.Extensions.CardCheckAssistant.Workflow;
using RFiDGear.Tests.CardCheck;

using Xunit;

namespace RFiDGear.Tests.CardCheckAssistant
{
    /// <summary>
    /// What the page offers: the one-time offer to import the old assistant's settings (which is off for now, and
    /// whose code is kept), and the proof that a test run never touches the settings of whoever runs it.
    /// </summary>
    public sealed class AssistantMemoryTests : IDisposable
    {
        private readonly TempFolder _folder = new TempFolder();
        private readonly RecordingCardCheckLog _log = new RecordingCardCheckLog();

        public void Dispose() => _folder.Dispose();

        // ── what is kept in the settings file ───────────────────────────────

        private ISettingsStore Store() => CardCheckServices.CreateSettingsStore(Path.Combine(_folder.Path, "settings"), _log);

        // A store that counts how it is written to.
        private sealed class SpyStore : ISettingsStore
        {
            private readonly ISettingsStore _inner;

            public SpyStore(ISettingsStore inner)
            {
                _inner = inner;
            }

            public int Updates { get; private set; }

            public int Saves { get; private set; }

            public SettingsLoad Load() => _inner.Load();

            public Result Save(CardCheckSettings settings)
            {
                Saves++;

                return _inner.Save(settings);
            }

            public Result Update(Func<CardCheckSettings, Result<CardCheckSettings>> change)
            {
                Updates++;

                return _inner.Update(change);
            }
        }

        [Fact]
        public async Task WhatDependsOnTheCurrentSettings_IsChangedInOneStep_NeverByReadingAndThenWriting()
        {
            WriteOldSettings();
            var importing = new SpyStore(CardCheckServices.CreateSettingsStore(Path.Combine(_folder.Path, "second"), _log));
            var declining = new SpyStore(CardCheckServices.CreateSettingsStore(Path.Combine(_folder.Path, "third"), _log));

            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                await Offer(importing, new FakeDialogs { Answer = ImportAnswer.Import }).OfferAsync(new LocalizedStrings("en-US"), CancellationToken.None);
                await Offer(declining, new FakeDialogs { Answer = ImportAnswer.Never }).OfferAsync(new LocalizedStrings("en-US"), CancellationToken.None);
            });

            // A write that follows a read of its own could write a copy that another window has made stale.
            Assert.All(new[] { importing, declining }, store => Assert.Equal((1, 0), (store.Updates, store.Saves)));
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

                // Written through the real store, by the settings window's own editor. Other tests of the run may have
                // written there as well, so what is checked is where the file is, not what is in it.
                var loaded = services.Editor.Load();
                Assert.Equal(SettingsSaveOutcome.Saved, services.Editor.Save(loaded.Data, loaded.Data with { MemoryProjectPath = "probe.rfPrj" }));

                Assert.True(File.Exists(Path.Combine(AppDataSandbox.Root, "settings", "settings.json")));
            });
        }

        [Fact]
        public async Task TheRealServices_ReadWhatTheSettingsWindowSaves_AndTheWorkflowListsTheMadeUpOrders()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var services = AssistantServicesFactory.Create(Dispatcher.CurrentDispatcher);
                var baseline = services.Editor.Load().Data;
                var reports = Path.Combine(_folder.Path, "reports");
                Directory.CreateDirectory(reports);

                try
                {
                    // Saved through the editor of the settings window, read by the workflow of the page: one store.
                    Assert.Equal(SettingsSaveOutcome.Saved, services.Editor.Save(baseline, baseline with { UseStubJobs = true, OutputFolder = reports }));

                    using var workflow = (CardCheckWorkflow)services.CreateWorkflow(new FakeExtensionHost());
                    Assert.True(workflow.RefreshOrders());
                    await workflow.CurrentOperation.WaitAsync(TimeSpan.FromSeconds(30));

                    Assert.Null(workflow.Current.OrdersFailure);
                    Assert.True(workflow.Current.IsTestMode);
                    Assert.Equal(5, workflow.Current.Orders.Count);
                }
                finally
                {
                    // The settings file of the sandbox is shared by the whole run: put back what was there.
                    services.Editor.Save(services.Editor.Load().Data, baseline);
                }
            });
        }

        // ── the offer to import the old app's settings is off for now ───────

        [Fact]
        public async Task TheOfferToImportTheOldAppsSettings_IsOff_EvenWhenTheOldFileIsThere_AndNothingIsReadOrMarked()
        {
            var oldFolder = Path.Combine(AppDataSandbox.Root, "CardCheckAssistant");
            Directory.CreateDirectory(oldFolder);
            var oldFile = Path.Combine(oldFolder, "settings.xml");
            File.WriteAllText(oldFile, "<DefaultSettings><SelectedDBServerName>OLD-SERVER</SelectedDBServerName></DefaultSettings>");

            try
            {
                await StaTestRunner.RunOnStaThreadAsync(async () =>
                {
                    var services = AssistantServicesFactory.Create(Dispatcher.CurrentDispatcher);

                    // Checked before the offer is used: were it the real one, it would ask a real question and wait for it.
                    Assert.Same(AssistantServices.None.ImportOffer, services.ImportOffer);

                    Assert.False(await services.ImportOffer.OfferAsync(new LocalizedStrings("en-US"), CancellationToken.None));
                });

                // Nothing of the old file reached the settings, and no marker says it was dealt with.
                var settingsFile = Path.Combine(AppDataSandbox.Root, "settings", "settings.json");
                var text = File.Exists(settingsFile) ? File.ReadAllText(settingsFile) : "{}";
                Assert.DoesNotContain("OLD-SERVER", text, StringComparison.Ordinal);

                using var document = JsonDocument.Parse(text);
                var holdsMarker = document.RootElement.TryGetProperty("settings", out var settings)
                    && settings.TryGetProperty("legacyImport", out var marker)
                    && marker.ValueKind != JsonValueKind.Null;
                Assert.False(holdsMarker, "a marker says the old settings were dealt with");
            }
            finally
            {
                File.Delete(oldFile);
            }
        }

        [Fact]
        public async Task TheImportCodeIsKept_SwitchedOnItIsTheRealOfferAgain()
        {
            // Not run: with the old file of the previous test possibly present, the real offer would ask a real question.
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var on = AssistantServicesFactory.Create(Dispatcher.CurrentDispatcher, offerLegacyImport: true);

                Assert.IsType<ImportOfferService>(on.ImportOffer);
            });
        }
    }
}
