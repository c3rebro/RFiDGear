using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using RFiDGear.Extensions.CardCheckAssistant.Localization;
using RFiDGear.Extensions.CardCheckAssistant.Services;
using RFiDGear.Extensions.CardCheckAssistant.ViewModels;

using Xunit;

using static RFiDGear.Tests.CardCheckAssistant.ViewModelTestSupport;

using Keys = RFiDGear.Extensions.CardCheckAssistant.Localization.ExtensionStrings.Keys;

namespace RFiDGear.Tests.CardCheckAssistant
{
    /// <summary>
    /// The settings window's view model against fakes: what it shows after loading, what stops a save and what only warns,
    /// that it saves what was changed and nothing else, what it says when saving fails, when the window may close, and how
    /// it tests the order database.
    /// </summary>
    public class SettingsViewModelTests
    {
        internal sealed class FakeSettingsEditor : IAssistantSettingsEditor, IDisposable
        {
            private readonly List<(SettingsData Baseline, SettingsData Edited)> _saves = new List<(SettingsData, SettingsData)>();
            private readonly List<(DatabaseData Database, CancellationToken Token)> _tests = new List<(DatabaseData, CancellationToken)>();
            private int _saveStarted;
            private TaskCompletionSource<bool>? _testGate;
            private ManualResetEventSlim? _saveGate;

            public SettingsEditorLoad LoadResult { get; set; } = new SettingsEditorLoad(new SettingsData(), SettingsProblem.None);

            public SettingsSaveOutcome SaveResult { get; set; } = SettingsSaveOutcome.Saved;

            public ConnectionTestOutcome TestResult { get; set; } = ConnectionTestOutcome.Succeeded;

            public int LoadThreadId { get; private set; } = -1;

            public bool SaveStarted => Volatile.Read(ref _saveStarted) == 1;

            public IReadOnlyList<(SettingsData Baseline, SettingsData Edited)> Saves => _saves;

            public IReadOnlyList<(DatabaseData Database, CancellationToken Token)> Tests => _tests;

            /// <summary>Makes the next test of the connection wait until <see cref="ReleaseTest"/>.</summary>
            public void HoldTest() => _testGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            /// <summary>Lets the held test of the connection answer.</summary>
            public void ReleaseTest() => _testGate?.SetResult(true);

            /// <summary>Makes the next save wait until <see cref="ReleaseSave"/>.</summary>
            public void HoldSave() => _saveGate = new ManualResetEventSlim(false);

            /// <summary>Lets the held save finish.</summary>
            public void ReleaseSave() => _saveGate?.Set();

            public SettingsEditorLoad Load()
            {
                LoadThreadId = Environment.CurrentManagedThreadId;

                return LoadResult;
            }

            public SettingsSaveOutcome Save(SettingsData baseline, SettingsData edited)
            {
                _saves.Add((baseline, edited));
                Volatile.Write(ref _saveStarted, 1);
                _saveGate?.Wait(TimeSpan.FromSeconds(10));

                return SaveResult;
            }

            public async Task<ConnectionTestOutcome> TestConnectionAsync(DatabaseData database, CancellationToken cancellationToken)
            {
                _tests.Add((database, cancellationToken));
                if (_testGate != null)
                {
                    await _testGate.Task;
                }

                return TestResult;
            }

            public void Dispose() => _saveGate?.Dispose();
        }

        internal sealed class FakeSettingsPrompts : ISettingsPrompts
        {
            private readonly List<(string Title, string Message)> _asked = new List<(string, string)>();
            private bool _answer;

            public IReadOnlyList<(string Title, string Message)> Asked => _asked;

            /// <summary>Sets what the operator answers when asked whether changes may be discarded; no until set.</summary>
            public void WillAnswer(bool discard) => _answer = discard;

            public bool ConfirmDiscard(string title, string message)
            {
                _asked.Add((title, message));

                return _answer;
            }
        }

        internal sealed class FakeFolderChecker : IFolderChecker
        {
            private readonly List<string?> _requests = new List<string?>();

            public FolderCheck Answer { get; set; } = FolderCheck.Valid;

            public int ThreadId { get; private set; } = -1;

            public IReadOnlyList<string?> Requests => _requests;

            public FolderCheck CheckFolder(string? path)
            {
                ThreadId = Environment.CurrentManagedThreadId;
                _requests.Add(path);

                return Answer;
            }
        }

        internal sealed class FakePathChecker : IPathChecker
        {
            private readonly List<string?> _projects = new List<string?>();

            public Dictionary<string, ProjectPathCheck> Answers { get; } = new Dictionary<string, ProjectPathCheck>();

            public IReadOnlyList<string?> Projects => _projects;

            public ProjectPathCheck CheckProject(string? path)
            {
                _projects.Add(path);

                return path != null && Answers.TryGetValue(path, out var answer) ? answer : ProjectPathCheck.Valid;
            }
        }

        internal sealed class Rig : IDisposable
        {
            private readonly List<bool> _closed = new List<bool>();
            private readonly List<bool> _closePermitted = new List<bool>();

            public Rig(SettingsEditorLoad? load)
            {
                if (load != null)
                {
                    Editor.LoadResult = load;
                }

                Vm = new SettingsViewModel(Editor, Dialogs, Prompts, Strings, Folders, Paths);

                // The window answers the request by closing, which asks the view model again: it must be allowed by then.
                Vm.CloseRequested += (_, e) =>
                {
                    _closed.Add(e.Saved);
                    _closePermitted.Add(Vm.TryRequestClose());
                };
            }

            public FakeSettingsEditor Editor { get; } = new FakeSettingsEditor();

            public FakeFileDialogService Dialogs { get; } = new FakeFileDialogService();

            public FakeSettingsPrompts Prompts { get; } = new FakeSettingsPrompts();

            public FakeFolderChecker Folders { get; } = new FakeFolderChecker();

            public FakePathChecker Paths { get; } = new FakePathChecker();

            public LocalizedStrings Strings { get; } = new LocalizedStrings("en-US");

            public SettingsViewModel Vm { get; }

            public IReadOnlyList<bool> Closed => _closed;

            public IReadOnlyList<bool> ClosePermitted => _closePermitted;

            public static async Task<Rig> OpenAsync(SettingsEditorLoad? load = null)
            {
                var rig = new Rig(load);
                await rig.Vm.LoadAsync();

                return rig;
            }

            public void Dispose()
            {
                Vm.Dispose();
                Editor.Dispose();
            }
        }

        internal static SettingsEditorLoad Loaded(SettingsProblem problem = SettingsProblem.None) => new SettingsEditorLoad(
            new SettingsData
            {
                OutputFolder = " out ",
                OneFolderPerJob = true,
                RemoveTemporaryReports = false,
                UseStubJobs = true,
                ReportLanguage = ReportLanguageChoice.English,
                DefaultProjectPath = "check.rfPrj",
                MemoryProjectPath = string.Empty,
                Database = new DatabaseData("srv", "orders", "dbo.T_Orders"),
                TextBlocks = new[] { new TextBlockData("Greeting", "Hello"), new TextBlockData("Closing", "Bye") },
            },
            problem);

        // ── loading ─────────────────────────────────────────────────────────

        [Fact]
        public async Task Load_ReadsOffTheUiThread_AndShowsTheSavedValues_AsUnchanged()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var rig = await Rig.OpenAsync(Loaded());
                var vm = rig.Vm;

                Assert.NotEqual(Environment.CurrentManagedThreadId, rig.Editor.LoadThreadId);
                Assert.Equal(("out", true, false, true, false), (vm.OutputFolder.Value, vm.OneFolderPerJob, vm.RemoveTemporaryReports, vm.IsEnglishReport, vm.IsGermanReport));
                Assert.True(vm.UseStubJobs);
                Assert.Equal(("check.rfPrj", string.Empty), (vm.DefaultProject.Value, vm.MemoryProject.Value));
                Assert.Equal(("srv", "orders", "dbo.T_Orders"), (vm.DatabaseServer, vm.DatabaseName, vm.DatabaseTable));
                Assert.Equal(new[] { "Greeting", "Closing" }, vm.TextBlocks.Select(block => block.Name));
                Assert.Equal(new[] { "Hello", "Bye" }, vm.TextBlocks.Select(block => block.Content));
                Assert.False(vm.IsDirty);
                Assert.True(vm.InputsEnabled);
                Assert.False(vm.HasProblem);
            });
        }

        [Theory]
        [InlineData(nameof(SettingsProblem.DamagedFileSetAside), true)]
        [InlineData(nameof(SettingsProblem.NewerVersion), false)]
        [InlineData(nameof(SettingsProblem.Unreadable), false)]
        [InlineData(nameof(SettingsProblem.Unavailable), false)]
        public async Task Load_WithAProblem_SaysSo_AndOnlyADamagedFileCanBeSavedOver(string problemName, bool canSave)
        {
            // The problem is passed by name: the type is internal to the extension and a public test method cannot take it.
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var rig = await Rig.OpenAsync(Loaded(Enum.Parse<SettingsProblem>(problemName)));

                Assert.True(rig.Vm.HasProblem);
                Assert.False(string.IsNullOrWhiteSpace(rig.Vm.ProblemText));
                Assert.Equal(canSave, rig.Vm.InputsEnabled);
                Assert.Equal(canSave, rig.Vm.OkCommand.CanExecute(null));
            });
        }

        // ── saving ──────────────────────────────────────────────────────────

        [Fact]
        public async Task Ok_ChecksOffTheUiThread_ThenSavesWhatWasChanged_Trimmed_AndClosesAsSaved()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var rig = await Rig.OpenAsync(Loaded());
                var vm = rig.Vm;
                vm.OutputFolder.Value = "  new-out  ";
                vm.DatabaseServer = " other-srv ";
                vm.IsGermanReport = true;
                Assert.True(vm.IsDirty);

                await vm.OkCommand.ExecuteAsync();

                var (baseline, edited) = Assert.Single(rig.Editor.Saves);
                Assert.Equal(("out", "srv", ReportLanguageChoice.English), (baseline.OutputFolder, baseline.Database.Server, baseline.ReportLanguage));
                Assert.Equal(("new-out", "other-srv", ReportLanguageChoice.German), (edited.OutputFolder, edited.Database.Server, edited.ReportLanguage));
                Assert.Equal(new[] { new TextBlockData("Greeting", "Hello"), new TextBlockData("Closing", "Bye") }, edited.TextBlocks);
                Assert.Equal(new[] { "new-out" }, rig.Folders.Requests);
                Assert.NotEqual(Environment.CurrentManagedThreadId, rig.Folders.ThreadId);
                Assert.Equal(new[] { "check.rfPrj" }, rig.Paths.Projects);
                Assert.Equal(new[] { true }, rig.Closed);
                Assert.Equal(new[] { true }, rig.ClosePermitted);
                Assert.Empty(rig.Prompts.Asked);
                Assert.False(vm.IsDirty);
                Assert.False(vm.HasSaveError);
            });
        }

        [Fact]
        public async Task EveryValueTheOperatorCanEdit_CountsAsAChange()
        {
            // Losing a change silently when the window closes is the worst this view model can do, so each value is tried.
            var changes = new Action<SettingsViewModel>[]
            {
                vm => vm.OutputFolder.Value = "other",
                vm => vm.OneFolderPerJob = false,
                vm => vm.RemoveTemporaryReports = true,
                vm => vm.UseStubJobs = false,
                vm => vm.IsGermanReport = true,
                vm => vm.DefaultProject.Value = "other.rfPrj",
                vm => vm.MemoryProject.Value = "memory.rfPrj",
                vm => vm.DatabaseServer = "other",
                vm => vm.DatabaseName = "other",
                vm => vm.DatabaseTable = "other",
                vm => vm.TextBlocks[0].Name = "Other",
                vm => vm.TextBlocks[0].Content = "other",
                vm =>
                {
                    vm.SelectedTextBlock = vm.TextBlocks[1];
                    vm.RemoveTextBlockCommand.Execute(null);
                },
                vm => vm.AddTextBlockCommand.Execute(null),
            };

            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                foreach (var change in changes)
                {
                    using var rig = await Rig.OpenAsync(Loaded());

                    var raised = new List<string?>();
                    rig.Vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

                    change(rig.Vm);

                    Assert.True(rig.Vm.IsDirty, "a change of " + change.Method.Name + " was not noticed");

                    // And the window is told, so that what depends on it (a message about an earlier save) follows.
                    Assert.True(raised.Contains(nameof(SettingsViewModel.IsDirty)), "a change of " + change.Method.Name + " was not announced");
                }
            });
        }

        [Theory]
        [InlineData(nameof(FolderCheck.NotFullyQualified), Keys.FolderNotAbsolute, true)]
        [InlineData(nameof(FolderCheck.Unusable), Keys.FolderUnusable, true)]
        [InlineData(nameof(FolderCheck.NotAFolder), Keys.FolderIsFile, true)]
        [InlineData(nameof(FolderCheck.NotFound), Keys.FolderNotFound, false)]
        public async Task Ok_WithAFolderProblem_StopsOnAnError_ButOnlyWarnsAboutAMissingFolder(string verdictName, string messageKey, bool stopsTheSave)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var rig = await Rig.OpenAsync(Loaded());
                rig.Folders.Answer = Enum.Parse<FolderCheck>(verdictName);

                await rig.Vm.OkCommand.ExecuteAsync();

                Assert.Equal(rig.Strings[messageKey], rig.Vm.OutputFolder.Message);
                Assert.Equal(stopsTheSave, rig.Vm.OutputFolder.IsError);
                Assert.Equal(!stopsTheSave, rig.Vm.OutputFolder.IsWarning);
                Assert.Equal(stopsTheSave ? 0 : 1, rig.Editor.Saves.Count);
                Assert.Equal(stopsTheSave ? Array.Empty<bool>() : new[] { true }, rig.Closed);
                Assert.Equal(stopsTheSave ? rig.Strings[Keys.FixEntries] : null, rig.Vm.SaveErrorText);

                // What was said about a value is gone as soon as the value is changed.
                rig.Vm.OutputFolder.Value = "another";

                Assert.False(rig.Vm.OutputFolder.HasMessage);
            });
        }

        [Fact]
        public async Task Ok_WithAProjectThatIsNotAFullPath_Stops_ButAMissingProjectOnlyWarns()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var rig = await Rig.OpenAsync(Loaded());
                rig.Vm.MemoryProject.Value = "memory.rfPrj";
                rig.Paths.Answers["check.rfPrj"] = ProjectPathCheck.NotFound;
                rig.Paths.Answers["memory.rfPrj"] = ProjectPathCheck.NotFullyQualified;

                await rig.Vm.OkCommand.ExecuteAsync();

                Assert.Equal((rig.Strings[Keys.ProjectNotFound], true), (rig.Vm.DefaultProject.Message, rig.Vm.DefaultProject.IsWarning));
                Assert.Equal((rig.Strings[Keys.ProjectNotAbsolute], true), (rig.Vm.MemoryProject.Message, rig.Vm.MemoryProject.IsError));
                Assert.Empty(rig.Editor.Saves);

                rig.Vm.MemoryProject.Value = string.Empty;
                Assert.False(rig.Vm.MemoryProject.HasMessage);
                await rig.Vm.OkCommand.ExecuteAsync();

                Assert.Single(rig.Editor.Saves);
                Assert.Equal(new[] { true }, rig.Closed);
            });
        }

        [Fact]
        public async Task Ok_WithATextBlockWithoutAName_ADuplicateName_OrHalfADatabase_SavesNothing_UntilItIsCorrected()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var rig = await Rig.OpenAsync(Loaded());
                var vm = rig.Vm;
                vm.AddTextBlockCommand.Execute(null);
                vm.AddTextBlockCommand.Execute(null);
                vm.TextBlocks[2].Name = "greeting";
                vm.TextBlocks[3].Name = "  ";
                vm.DatabaseName = string.Empty;

                await vm.OkCommand.ExecuteAsync();

                Assert.Empty(rig.Editor.Saves);
                Assert.Equal(rig.Strings[Keys.TextBlockNameDuplicate], vm.TextBlocks[0].NameError);
                Assert.Equal(rig.Strings[Keys.TextBlockNameDuplicate], vm.TextBlocks[2].NameError);
                Assert.Equal(rig.Strings[Keys.TextBlockNameBlank], vm.TextBlocks[3].NameError);
                Assert.Null(vm.TextBlocks[1].NameError);
                Assert.Equal(rig.Strings[Keys.DatabaseIncomplete], vm.DatabaseError);
                Assert.Equal(rig.Strings[Keys.FixEntries], vm.SaveErrorText);

                vm.TextBlocks[2].Name = " Third ";
                Assert.Null(vm.TextBlocks[0].NameError);
                Assert.Null(vm.TextBlocks[2].NameError);
                vm.SelectedTextBlock = vm.TextBlocks[3];
                vm.RemoveTextBlockCommand.Execute(null);
                vm.DatabaseName = "orders";
                await vm.OkCommand.ExecuteAsync();

                var (_, edited) = Assert.Single(rig.Editor.Saves);
                Assert.Equal(new[] { "Greeting", "Closing", "Third" }, edited.TextBlocks.Select(block => block.Name));
                Assert.Equal(new[] { true }, rig.Closed);
            });
        }

        [Fact]
        public async Task Ok_AlsoChecksTextBlocksThatCameFromTheFile_ThatNobodyHasEdited()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var data = new SettingsData { TextBlocks = new[] { new TextBlockData("Same", "one"), new TextBlockData("same", "two") } };
                using var rig = await Rig.OpenAsync(new SettingsEditorLoad(data, SettingsProblem.None));

                await rig.Vm.OkCommand.ExecuteAsync();

                Assert.Empty(rig.Editor.Saves);
                Assert.All(rig.Vm.TextBlocks, block => Assert.Equal(rig.Strings[Keys.TextBlockNameDuplicate], block.NameError));
            });
        }

        [Theory]
        [InlineData(nameof(SettingsSaveOutcome.NewerVersion), Keys.SaveNewerVersion)]
        [InlineData(nameof(SettingsSaveOutcome.Unreadable), Keys.SaveUnreadable)]
        [InlineData(nameof(SettingsSaveOutcome.Busy), Keys.SaveBusy)]
        [InlineData(nameof(SettingsSaveOutcome.TooLarge), Keys.SaveTooLarge)]
        [InlineData(nameof(SettingsSaveOutcome.Unavailable), Keys.SettingsUnavailable)]
        [InlineData(nameof(SettingsSaveOutcome.Failed), Keys.SaveFailed)]
        public async Task Ok_WhenTheSaveFails_StaysOpenWithTheReason_UntilSomethingIsEdited(string outcomeName, string messageKey)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var rig = await Rig.OpenAsync(Loaded());
                rig.Editor.SaveResult = Enum.Parse<SettingsSaveOutcome>(outcomeName);
                rig.Vm.OneFolderPerJob = false;

                await rig.Vm.OkCommand.ExecuteAsync();

                Assert.Equal(rig.Strings[messageKey], rig.Vm.SaveErrorText);
                Assert.Empty(rig.Closed);
                Assert.True(rig.Vm.InputsEnabled);
                Assert.True(rig.Vm.IsDirty);

                rig.Vm.OneFolderPerJob = true;

                Assert.False(rig.Vm.HasSaveError);
            });
        }

        // ── closing ─────────────────────────────────────────────────────────

        [Fact]
        public async Task Cancel_AsksTheWindowToCloseWithoutSaving()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var rig = await Rig.OpenAsync(Loaded());

                rig.Vm.CancelCommand.Execute(null);

                Assert.Equal(new[] { false }, rig.Closed);
                Assert.Empty(rig.Editor.Saves);
            });
        }

        [Fact]
        public async Task TheWindowMayClose_WithoutAsking_IfNothingChanged_AndAfterAskingIfSomethingDid()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var rig = await Rig.OpenAsync(Loaded());
                var vm = rig.Vm;

                Assert.True(vm.TryRequestClose());
                Assert.Empty(rig.Prompts.Asked);

                vm.DefaultProject.Value = "other.rfPrj";
                rig.Prompts.WillAnswer(false);
                Assert.False(vm.TryRequestClose());
                Assert.Equal((rig.Strings[Keys.DiscardTitle], rig.Strings[Keys.DiscardMessage]), Assert.Single(rig.Prompts.Asked));

                rig.Prompts.WillAnswer(true);
                Assert.True(vm.TryRequestClose());

                // Putting everything back the way it was is no change.
                vm.DefaultProject.Value = " check.rfPrj ";
                Assert.False(vm.IsDirty);
                Assert.True(vm.TryRequestClose());
                Assert.Equal(2, rig.Prompts.Asked.Count);
            });
        }

        [Fact]
        public async Task TheWindowMayNotClose_WhileTheSettingsAreBeingSaved()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var rig = await Rig.OpenAsync(Loaded());
                rig.Editor.HoldSave();
                rig.Vm.OneFolderPerJob = false;

                // Were the operator asked, the answer would be "discard": only the save in progress can say no.
                rig.Prompts.WillAnswer(true);
                var saving = rig.Vm.OkCommand.ExecuteAsync();
                await WaitUntil(() => rig.Editor.SaveStarted, "the editor to start saving");

                Assert.True(rig.Vm.IsSaving);
                Assert.False(rig.Vm.InputsEnabled);
                Assert.False(rig.Vm.TryRequestClose());
                Assert.Empty(rig.Prompts.Asked);
                Assert.False(rig.Vm.CancelCommand.CanExecute(null));

                rig.Editor.ReleaseSave();
                await saving;

                Assert.False(rig.Vm.IsSaving);
                Assert.Equal(new[] { true }, rig.Closed);
            });
        }

        // ── the order database ──────────────────────────────────────────────

        [Theory]
        [InlineData(nameof(ConnectionTestOutcome.Succeeded), Keys.TestSucceeded)]
        [InlineData(nameof(ConnectionTestOutcome.NotConfigured), Keys.TestNotConfigured)]
        [InlineData(nameof(ConnectionTestOutcome.TableInvalid), Keys.TestTableInvalid)]
        [InlineData(nameof(ConnectionTestOutcome.ConnectionInvalid), Keys.TestConnectionInvalid)]
        [InlineData(nameof(ConnectionTestOutcome.Unreachable), Keys.TestUnreachable)]
        [InlineData(nameof(ConnectionTestOutcome.AccessDenied), Keys.TestAccessDenied)]
        [InlineData(nameof(ConnectionTestOutcome.SchemaMismatch), Keys.TestSchemaMismatch)]
        [InlineData(nameof(ConnectionTestOutcome.Timeout), Keys.TestTimeout)]
        [InlineData(nameof(ConnectionTestOutcome.DriverMissing), Keys.TestDriverMissing)]
        [InlineData(nameof(ConnectionTestOutcome.Failed), Keys.TestFailed)]
        [InlineData(nameof(ConnectionTestOutcome.Cancelled), null)]
        public async Task TestConnection_TriesTheValuesAsEntered_AndSaysWhatItFound(string outcomeName, string? messageKey)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var rig = await Rig.OpenAsync(Loaded());
                rig.Editor.TestResult = Enum.Parse<ConnectionTestOutcome>(outcomeName);
                rig.Vm.DatabaseServer = "  host\\instance ";

                await rig.Vm.TestConnectionCommand.ExecuteAsync();

                Assert.Equal(new DatabaseData("host\\instance", "orders", "dbo.T_Orders"), Assert.Single(rig.Editor.Tests).Database);
                Assert.Equal(messageKey == null ? null : rig.Strings[messageKey], rig.Vm.TestResultText);
                Assert.Equal(messageKey != null, rig.Vm.HasTestResult);
                Assert.Equal(messageKey == Keys.TestSucceeded, rig.Vm.TestSucceeded);
                Assert.False(rig.Vm.IsTesting);
                Assert.Empty(rig.Editor.Saves);
            });
        }

        [Fact]
        public async Task ChangingTheDatabaseWhileATestRuns_CancelsIt_AndForgetsItsAnswer()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var rig = await Rig.OpenAsync(Loaded());
                rig.Editor.HoldTest();

                var testing = rig.Vm.TestConnectionCommand.ExecuteAsync();
                await WaitUntil(() => rig.Editor.Tests.Count == 1, "the editor to start the test");
                Assert.True(rig.Vm.IsTesting);
                Assert.Equal(rig.Strings[Keys.TestTesting], rig.Vm.TestResultText);

                rig.Vm.DatabaseTable = "dbo.Other";

                Assert.True(rig.Editor.Tests[0].Token.IsCancellationRequested);
                Assert.False(rig.Vm.IsTesting);
                Assert.False(rig.Vm.HasTestResult);

                rig.Editor.ReleaseTest();
                await testing;

                // The answer to the old values is not shown for the new ones.
                Assert.False(rig.Vm.HasTestResult);
                Assert.False(rig.Vm.TestSucceeded);
            });
        }

        [Fact]
        public async Task ClosingTheWindow_CancelsARunningTest()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var rig = await Rig.OpenAsync(Loaded());
                rig.Editor.HoldTest();
                var testing = rig.Vm.TestConnectionCommand.ExecuteAsync();
                await WaitUntil(() => rig.Editor.Tests.Count == 1, "the editor to start the test");

                rig.Vm.Dispose();
                rig.Editor.ReleaseTest();
                await testing;

                Assert.True(rig.Editor.Tests[0].Token.IsCancellationRequested);
                Assert.False(rig.Vm.TestSucceeded);
            });
        }

        // ── pickers, text blocks, language ──────────────────────────────────

        [Fact]
        public async Task Pickers_PutThePickedPathIntoTheirField_AndACancelledPickerChangesNothing()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var rig = await Rig.OpenAsync(Loaded());
                var vm = rig.Vm;
                rig.Dialogs.FolderAnswer = "picked-folder";
                rig.Dialogs.Answer = "picked.rfPrj";

                vm.OutputFolder.BrowseCommand.Execute(null);
                vm.MemoryProject.BrowseCommand.Execute(null);

                Assert.Equal(("picked-folder", "picked.rfPrj"), (vm.OutputFolder.Value, vm.MemoryProject.Value));
                Assert.Equal(("out", rig.Strings[Keys.PickFolderTitle]), Assert.Single(rig.Dialogs.FolderRequests));
                Assert.Equal(("", rig.Strings[Keys.PickerTitle], rig.Strings[Keys.PickerFilter]), (rig.Dialogs.Requests[0].Current ?? string.Empty, rig.Dialogs.Requests[0].Title, rig.Dialogs.Requests[0].Filter));

                rig.Dialogs.FolderAnswer = null;
                vm.OutputFolder.BrowseCommand.Execute(null);

                Assert.Equal("picked-folder", vm.OutputFolder.Value);
            });
        }

        [Fact]
        public async Task TextBlocks_GetAUniqueNameWhenAdded_AndTheNeighbourIsSelectedWhenOneIsRemoved()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var rig = await Rig.OpenAsync(new SettingsEditorLoad(new SettingsData(), SettingsProblem.None));
                var vm = rig.Vm;

                vm.AddTextBlockCommand.Execute(null);
                vm.AddTextBlockCommand.Execute(null);
                vm.AddTextBlockCommand.Execute(null);

                Assert.Equal(new[] { "New text block", "New text block 2", "New text block 3" }, vm.TextBlocks.Select(block => block.Name));
                Assert.Same(vm.TextBlocks[2], vm.SelectedTextBlock);
                Assert.True(vm.IsDirty);

                vm.SelectedTextBlock = vm.TextBlocks[1];
                vm.RemoveTextBlockCommand.Execute(null);

                Assert.Equal(new[] { "New text block", "New text block 3" }, vm.TextBlocks.Select(block => block.Name));
                Assert.Same(vm.TextBlocks[1], vm.SelectedTextBlock);

                vm.RemoveTextBlockCommand.Execute(null);
                vm.RemoveTextBlockCommand.Execute(null);

                Assert.Empty(vm.TextBlocks);
                Assert.Null(vm.SelectedTextBlock);
                Assert.False(vm.RemoveTextBlockCommand.CanExecute(null));
                Assert.False(vm.IsDirty);
            });
        }

        [Fact]
        public async Task ANewTextBlockIsNotNamedLikeAnExistingOne_WhateverTheCase()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var data = new SettingsData { TextBlocks = new[] { new TextBlockData("NEW TEXT BLOCK", "x") } };
                using var rig = await Rig.OpenAsync(new SettingsEditorLoad(data, SettingsProblem.None));
                rig.Vm.TextBlocks[0].Name = " NEW TEXT BLOCK ";

                rig.Vm.AddTextBlockCommand.Execute(null);

                Assert.Equal("New text block 2", rig.Vm.TextBlocks[1].Name);
                Assert.All(rig.Vm.TextBlocks, block => Assert.Null(block.NameError));
            });
        }

        [Fact]
        public async Task ALanguageChange_WritesTheMessagesAgain()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var rig = await Rig.OpenAsync(Loaded());
                rig.Folders.Answer = FolderCheck.NotFullyQualified;
                await rig.Vm.OkCommand.ExecuteAsync();
                var changed = new List<string?>();
                rig.Vm.OutputFolder.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

                rig.Strings.Refresh("de-DE");

                Assert.Equal("Geben Sie den vollständigen Pfad des Ordners ein.", rig.Vm.OutputFolder.Message);
                Assert.Equal("Korrigieren Sie zuerst die markierten Eingaben.", rig.Vm.SaveErrorText);
                Assert.Contains(nameof(PathFieldViewModel.Message), changed);
            });
        }
    }
}
