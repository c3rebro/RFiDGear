using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using RFiDGear.Extensions.CardCheckAssistant.Localization;
using RFiDGear.Extensions.CardCheckAssistant.Mvvm;
using RFiDGear.Extensions.CardCheckAssistant.Services;

using Keys = RFiDGear.Extensions.CardCheckAssistant.Localization.ExtensionStrings.Keys;

namespace RFiDGear.Extensions.CardCheckAssistant.ViewModels
{
    /// <summary>Tells the window that holds the settings to close.</summary>
    internal sealed class CloseRequestedEventArgs : EventArgs
    {
        /// <param name="saved">Whether the settings were saved.</param>
        public CloseRequestedEventArgs(bool saved)
        {
            Saved = saved;
        }

        /// <summary>Whether the settings were saved, so the page should read them again.</summary>
        public bool Saved { get; }
    }

    /// <summary>Where the test of the order database stands.</summary>
    internal enum ConnectionTestState
    {
        /// <summary>Nothing was tested since the values were last changed.</summary>
        None,

        /// <summary>The test is running.</summary>
        Testing,

        /// <summary>The server answered and the table is as the assistant needs it.</summary>
        Succeeded,

        /// <summary>The test found a problem; the text says which.</summary>
        Failed
    }

    /// <summary>
    /// The state and behavior of the settings window: a draft of every value the operator can set, next to the values as
    /// they were loaded. OK checks the draft and saves what was changed; Cancel and a closed window throw the draft away
    /// (after asking, if something was changed). The window is only a shell around it: it binds, shows what the messages
    /// say, and closes when <see cref="CloseRequested"/> is raised.
    /// <para>
    /// Everything that reads or writes the disk or talks to the server runs off the UI thread. A structural problem (a
    /// relative folder, a text block without a name) stops the save; a folder or project that does not exist is only a
    /// warning, because it may be a network share that is not reachable just now.
    /// </para>
    /// </summary>
    internal sealed class SettingsViewModel : ObservableBase, IDisposable
    {
        private readonly IAssistantSettingsEditor _editor;
        private readonly IFileDialogService _dialogs;
        private readonly ISettingsPrompts _prompts;
        private readonly IFolderChecker _folderChecker;
        private readonly IPathChecker _pathChecker;

        private int _disposed;
        private int _loadStarted;
        private SettingsData _baseline = new SettingsData();
        private SettingsProblem _problem;
        private bool _loaded;
        private bool _isLoading;
        private bool _isSaving;
        private bool _oneFolderPerJob;
        private bool _removeTemporaryReports;
        private ReportLanguageChoice _reportLanguage = ReportLanguageChoice.German;
        private string _databaseServer = string.Empty;
        private string _databaseName = string.Empty;
        private string _databaseTable = string.Empty;
        private string? _saveErrorKey;
        private string? _databaseErrorKey;
        private ConnectionTestState _testState;
        private string? _testKey;
        private CancellationTokenSource? _testCancellation;
        private TextBlockViewModel? _selectedTextBlock;

        /// <param name="editor">Reads, saves and tests the settings.</param>
        /// <param name="dialogs">The pickers.</param>
        /// <param name="prompts">The question asked before changes are thrown away.</param>
        /// <param name="strings">The page's texts; the same instance as the page's, so a language change reaches this window.</param>
        /// <param name="folderChecker">The file-system check of a folder; the real one when omitted.</param>
        /// <param name="pathChecker">The file-system check of a project; the real one when omitted.</param>
        public SettingsViewModel(
            IAssistantSettingsEditor editor,
            IFileDialogService dialogs,
            ISettingsPrompts prompts,
            LocalizedStrings strings,
            IFolderChecker? folderChecker = null,
            IPathChecker? pathChecker = null)
        {
            _editor = editor ?? throw new ArgumentNullException(nameof(editor));
            _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
            _prompts = prompts ?? throw new ArgumentNullException(nameof(prompts));
            Strings = strings ?? throw new ArgumentNullException(nameof(strings));
            _folderChecker = folderChecker ?? new FileSystemFolderChecker();
            _pathChecker = pathChecker ?? new FileSystemPathChecker();

            OutputFolder = new PathFieldViewModel(strings, OnDraftChanged, BrowseFolder, () => InputsEnabled);
            DefaultProject = new PathFieldViewModel(strings, OnDraftChanged, BrowseProject, () => InputsEnabled);
            MemoryProject = new PathFieldViewModel(strings, OnDraftChanged, BrowseProject, () => InputsEnabled);

            OkCommand = new AsyncCommand(SaveAsync, () => CanSave, _ => SetSaveError(Keys.SaveFailed));
            CancelCommand = new DelegateCommand(Cancel, () => !_isSaving);
            TestConnectionCommand = new AsyncCommand(TestConnectionAsync, () => InputsEnabled, _ => SetTest(ConnectionTestState.Failed, Keys.TestFailed));
            AddTextBlockCommand = new DelegateCommand(AddTextBlock, () => InputsEnabled);
            RemoveTextBlockCommand = new DelegateCommand(RemoveTextBlock, () => InputsEnabled && _selectedTextBlock != null);

            Strings.PropertyChanged += OnStringsChanged;
        }

        /// <summary>Tells the window to close, with or without the settings having been saved.</summary>
        public event EventHandler<CloseRequestedEventArgs>? CloseRequested;

        /// <summary>The page's texts, as a binding source: <c>{Binding Strings[Key]}</c>.</summary>
        public LocalizedStrings Strings { get; }

        /// <summary>Checks the draft and saves what was changed; the window closes if that worked.</summary>
        public AsyncCommand OkCommand { get; }

        /// <summary>Asks the window to close without saving.</summary>
        public DelegateCommand CancelCommand { get; }

        /// <summary>Tries the order database as entered.</summary>
        public AsyncCommand TestConnectionCommand { get; }

        /// <summary>Adds a text block at the end of the list and selects it.</summary>
        public DelegateCommand AddTextBlockCommand { get; }

        /// <summary>Removes the selected text block.</summary>
        public DelegateCommand RemoveTextBlockCommand { get; }

        /// <summary>The folder reports are written to.</summary>
        public PathFieldViewModel OutputFolder { get; }

        /// <summary>The project a check runs first.</summary>
        public PathFieldViewModel DefaultProject { get; }

        /// <summary>The project that reads the memory of the card.</summary>
        public PathFieldViewModel MemoryProject { get; }

        /// <summary>The text blocks, in the order they are saved.</summary>
        public ObservableCollection<TextBlockViewModel> TextBlocks { get; } = new ObservableCollection<TextBlockViewModel>();

        /// <summary>The block that is selected in the list, or <c>null</c>.</summary>
        public TextBlockViewModel? SelectedTextBlock
        {
            get => _selectedTextBlock;
            set
            {
                if (SetProperty(ref _selectedTextBlock, value))
                {
                    RemoveTextBlockCommand.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>Whether each order gets a folder of its own.</summary>
        public bool OneFolderPerJob
        {
            get => _oneFolderPerJob;
            set
            {
                if (SetProperty(ref _oneFolderPerJob, value))
                {
                    OnDraftChanged();
                }
            }
        }

        /// <summary>Whether working reports are removed when a check is finished.</summary>
        public bool RemoveTemporaryReports
        {
            get => _removeTemporaryReports;
            set
            {
                if (SetProperty(ref _removeTemporaryReports, value))
                {
                    OnDraftChanged();
                }
            }
        }

        /// <summary>The language of a report unless the operator picks another one.</summary>
        public ReportLanguageChoice ReportLanguage => _reportLanguage;

        /// <summary>Whether reports are made in German by default; the setter only takes <c>true</c> (a radio button).</summary>
        public bool IsGermanReport
        {
            get => _reportLanguage == ReportLanguageChoice.German;
            set => SetReportLanguage(value, ReportLanguageChoice.German);
        }

        /// <summary>Whether reports are made in English by default; the setter only takes <c>true</c> (a radio button).</summary>
        public bool IsEnglishReport
        {
            get => _reportLanguage == ReportLanguageChoice.English;
            set => SetReportLanguage(value, ReportLanguageChoice.English);
        }

        /// <summary>The database server, <c>host</c> or <c>host\instance</c>.</summary>
        public string DatabaseServer
        {
            get => _databaseServer;
            set
            {
                if (SetProperty(ref _databaseServer, value ?? string.Empty))
                {
                    OnDatabaseEdited();
                }
            }
        }

        /// <summary>The database on that server.</summary>
        public string DatabaseName
        {
            get => _databaseName;
            set
            {
                if (SetProperty(ref _databaseName, value ?? string.Empty))
                {
                    OnDatabaseEdited();
                }
            }
        }

        /// <summary>The order table, with or without schema.</summary>
        public string DatabaseTable
        {
            get => _databaseTable;
            set
            {
                if (SetProperty(ref _databaseTable, value ?? string.Empty))
                {
                    OnDatabaseEdited();
                }
            }
        }

        /// <summary>What is wrong with the database entries, or <c>null</c>.</summary>
        public string? DatabaseError => _databaseErrorKey == null ? null : Strings[_databaseErrorKey];

        /// <summary>Whether <see cref="DatabaseError"/> has something to show.</summary>
        public bool HasDatabaseError => _databaseErrorKey != null;

        /// <summary>What the test of the connection found, or <c>null</c> if nothing was tested.</summary>
        public string? TestResultText => _testKey == null ? null : Strings[_testKey];

        /// <summary>Whether <see cref="TestResultText"/> has something to show.</summary>
        public bool HasTestResult => _testKey != null;

        /// <summary>Whether the test is running.</summary>
        public bool IsTesting => _testState == ConnectionTestState.Testing;

        /// <summary>Whether the last test succeeded.</summary>
        public bool TestSucceeded => _testState == ConnectionTestState.Succeeded;

        /// <summary>What is wrong with the settings file, or <c>null</c>.</summary>
        public string? ProblemText => ProblemKey() is { } key ? Strings[key] : null;

        /// <summary>Whether <see cref="ProblemText"/> has something to show.</summary>
        public bool HasProblem => ProblemKey() != null;

        /// <summary>Why the last save did not happen, or <c>null</c>.</summary>
        public string? SaveErrorText => _saveErrorKey == null ? null : Strings[_saveErrorKey];

        /// <summary>Whether <see cref="SaveErrorText"/> has something to show.</summary>
        public bool HasSaveError => _saveErrorKey != null;

        /// <summary>Whether the settings are being read.</summary>
        public bool IsLoading => _isLoading;

        /// <summary>Whether the settings are being checked and saved.</summary>
        public bool IsSaving => _isSaving;

        /// <summary>Whether the progress bar is shown.</summary>
        public bool ShowProgress => _isLoading || _isSaving;

        /// <summary>Whether the controls can be used: not while loading or saving, and not if nothing can be saved.</summary>
        public bool InputsEnabled => _loaded && !_isLoading && !_isSaving && ProblemAllowsSave && !IsDisposed;

        /// <summary>Whether the OK button can be used.</summary>
        public bool CanSave => InputsEnabled;

        /// <summary>Whether the draft differs from the settings as they were loaded or last saved.</summary>
        public bool IsDirty => _loaded && !SameData(ToData(), _baseline);

        /// <summary>Whether <see cref="Dispose"/> has run.</summary>
        public bool IsDisposed => Volatile.Read(ref _disposed) == 1;

        private bool ProblemAllowsSave => _problem == SettingsProblem.None || _problem == SettingsProblem.DamagedFileSetAside;

        /// <summary>
        /// Reads the settings and shows them. Called once when the window is first shown; a second call does nothing. It
        /// never throws.
        /// </summary>
        /// <returns>The task of the load.</returns>
        internal async Task LoadAsync()
        {
            if (Interlocked.Exchange(ref _loadStarted, 1) == 1)
            {
                return;
            }

            _isLoading = true;
            RaiseStateProperties();

            SettingsEditorLoad load;
            try
            {
                // The editor reads the disk.
                load = await Task.Run(_editor.Load);
            }
            catch (Exception)
            {
                // The editor does not throw because of the file; if it does anyway, nothing is shown and nothing is saved.
                load = new SettingsEditorLoad(new SettingsData(), SettingsProblem.Unreadable);
            }

            if (IsDisposed)
            {
                return;
            }

            _problem = load.Problem;
            _baseline = Normalize(load.Data ?? new SettingsData());
            Show(_baseline);
            _loaded = true;
            _isLoading = false;
            RaiseStateProperties();
            OnPropertyChanged(nameof(ProblemText));
            OnPropertyChanged(nameof(HasProblem));
            OnPropertyChanged(nameof(IsDirty));
        }

        /// <summary>
        /// Asks whether the window may close. It may not while the settings are being saved. If the draft differs from the
        /// settings, the operator is asked whether the changes may be thrown away.
        /// </summary>
        /// <returns><c>true</c> if the window may close.</returns>
        public bool TryRequestClose()
        {
            if (IsDisposed)
            {
                return true;
            }

            if (_isSaving)
            {
                return false;
            }

            return !IsDirty || _prompts.ConfirmDiscard(Strings[Keys.DiscardTitle], Strings[Keys.DiscardMessage]);
        }

        /// <summary>Stops a running test, stops listening to the language, and drops what the window showed. Safe to call more than once.</summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
            {
                return;
            }

            Strings.PropertyChanged -= OnStringsChanged;
            CancelTest();
        }

        // ── showing and reading the draft ──

        private void Show(SettingsData data)
        {
            OutputFolder.Value = data.OutputFolder;
            OneFolderPerJob = data.OneFolderPerJob;
            RemoveTemporaryReports = data.RemoveTemporaryReports;
            SetReportLanguage(true, data.ReportLanguage);
            DefaultProject.Value = data.DefaultProjectPath;
            MemoryProject.Value = data.MemoryProjectPath;
            DatabaseServer = data.Database.Server;
            DatabaseName = data.Database.Database;
            DatabaseTable = data.Database.Table;

            TextBlocks.Clear();
            foreach (var block in data.TextBlocks)
            {
                TextBlocks.Add(new TextBlockViewModel(Strings, block.Name, block.Content, OnTextBlockEdited));
            }

            SelectedTextBlock = null;
        }

        // The draft as the settings keep it: no blanks around names and paths.
        private SettingsData ToData() => new SettingsData
        {
            OutputFolder = OutputFolder.Value.Trim(),
            OneFolderPerJob = _oneFolderPerJob,
            RemoveTemporaryReports = _removeTemporaryReports,
            ReportLanguage = _reportLanguage,
            DefaultProjectPath = DefaultProject.Value.Trim(),
            MemoryProjectPath = MemoryProject.Value.Trim(),
            Database = new DatabaseData(_databaseServer.Trim(), _databaseName.Trim(), _databaseTable.Trim()),
            TextBlocks = TextBlocks.Select(block => block.ToData()).ToList(),
        };

        private static SettingsData Normalize(SettingsData data) => data with
        {
            OutputFolder = (data.OutputFolder ?? string.Empty).Trim(),
            DefaultProjectPath = (data.DefaultProjectPath ?? string.Empty).Trim(),
            MemoryProjectPath = (data.MemoryProjectPath ?? string.Empty).Trim(),
            Database = new DatabaseData(
                (data.Database?.Server ?? string.Empty).Trim(),
                (data.Database?.Database ?? string.Empty).Trim(),
                (data.Database?.Table ?? string.Empty).Trim()),
            TextBlocks = (data.TextBlocks ?? new List<TextBlockData>())
                .Select(block => new TextBlockData((block.Name ?? string.Empty).Trim(), block.Content ?? string.Empty))
                .ToList(),
        };

        private static bool SameData(SettingsData a, SettingsData b) =>
            a.OutputFolder == b.OutputFolder
            && a.OneFolderPerJob == b.OneFolderPerJob
            && a.RemoveTemporaryReports == b.RemoveTemporaryReports
            && a.ReportLanguage == b.ReportLanguage
            && a.DefaultProjectPath == b.DefaultProjectPath
            && a.MemoryProjectPath == b.MemoryProjectPath
            && a.Database == b.Database
            && a.TextBlocks.SequenceEqual(b.TextBlocks);

        private void SetReportLanguage(bool selected, ReportLanguageChoice language)
        {
            // A radio button also reports "no longer selected" for the one that was left; only the new choice counts.
            if (!selected || _reportLanguage == language)
            {
                return;
            }

            _reportLanguage = language;
            OnPropertyChanged(nameof(ReportLanguage));
            OnPropertyChanged(nameof(IsGermanReport));
            OnPropertyChanged(nameof(IsEnglishReport));
            OnDraftChanged();
        }

        // ── changes of the draft ──

        private void OnDraftChanged()
        {
            if (IsDisposed)
            {
                return;
            }

            ClearSaveError();
            OnPropertyChanged(nameof(IsDirty));
        }

        private void OnDatabaseEdited()
        {
            // What was found about the old values says nothing about the new ones.
            ClearDatabaseError();
            ResetTest();
            OnDraftChanged();
        }

        private void OnTextBlockEdited()
        {
            RefreshTextBlockProblems();
            OnDraftChanged();
        }

        private void AddTextBlock()
        {
            var added = new TextBlockViewModel(Strings, UnusedTextBlockName(), string.Empty, OnTextBlockEdited);
            TextBlocks.Add(added);
            SelectedTextBlock = added;
            RefreshTextBlockProblems();
            OnDraftChanged();
        }

        // "New text block", then "New text block 2" and so on: the first name no other block has.
        private string UnusedTextBlockName()
        {
            var baseName = Strings[Keys.TextBlockNewName];
            var name = baseName;
            var number = 2;
            var used = new HashSet<string>(TextBlocks.Select(block => block.Name.Trim()), StringComparer.OrdinalIgnoreCase);
            while (used.Contains(name))
            {
                name = baseName + " " + number;
                number++;
            }

            return name;
        }

        private void RemoveTextBlock()
        {
            var selected = _selectedTextBlock;
            if (selected == null)
            {
                return;
            }

            var index = TextBlocks.IndexOf(selected);
            TextBlocks.Remove(selected);
            SelectedTextBlock = TextBlocks.Count == 0 ? null : TextBlocks[Math.Min(index, TextBlocks.Count - 1)];
            RefreshTextBlockProblems();
            OnDraftChanged();
        }

        // Runs on every edit of a name, so the names are counted once and each block looks its count up.
        private void RefreshTextBlockProblems()
        {
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var block in TextBlocks)
            {
                var name = block.Name.Trim();
                counts[name] = counts.TryGetValue(name, out var seen) ? seen + 1 : 1;
            }

            foreach (var block in TextBlocks)
            {
                var name = block.Name.Trim();
                block.NameProblem = name.Length == 0
                    ? TextBlockNameProblem.Blank
                    : counts[name] > 1
                        ? TextBlockNameProblem.Duplicate
                        : TextBlockNameProblem.None;
            }
        }

        // ── pickers ──

        private void BrowseFolder(PathFieldViewModel field)
        {
            var picked = _dialogs.PickFolder(field.Value, Strings[Keys.PickFolderTitle]);
            if (!string.IsNullOrWhiteSpace(picked))
            {
                field.Value = picked!;
            }
        }

        private void BrowseProject(PathFieldViewModel field)
        {
            var picked = _dialogs.PickProjectFile(field.Value, Strings[Keys.PickerTitle], Strings[Keys.PickerFilter]);
            if (!string.IsNullOrWhiteSpace(picked))
            {
                field.Value = picked!;
            }
        }

        // ── save ──

        private async Task SaveAsync()
        {
            if (!CanSave)
            {
                return;
            }

            ClearSaveError();
            _isSaving = true;
            RaiseStateProperties();

            string? errorKey = null;
            try
            {
                var data = ToData();
                if (!await ValidateAsync(data))
                {
                    errorKey = Keys.FixEntries;
                }
                else
                {
                    var baseline = _baseline;

                    // The editor reads and writes the disk.
                    var outcome = await Task.Run(() => _editor.Save(baseline, data));
                    if (outcome == SettingsSaveOutcome.Saved)
                    {
                        _baseline = data;
                    }
                    else
                    {
                        errorKey = SaveErrorKey(outcome);
                    }
                }
            }
            catch (Exception)
            {
                errorKey = Keys.SaveFailed;
            }
            finally
            {
                _isSaving = false;
            }

            if (IsDisposed)
            {
                return;
            }

            RaiseStateProperties();
            OnPropertyChanged(nameof(IsDirty));
            if (errorKey != null)
            {
                SetSaveError(errorKey);
            }
            else
            {
                CloseRequested?.Invoke(this, new CloseRequestedEventArgs(saved: true));
            }
        }

        // Shows what is wrong with the draft. The disk checks run on a worker thread: on a network path they can block.
        private async Task<bool> ValidateAsync(SettingsData data)
        {
            var (folder, defaultProject, memoryProject) = await Task.Run(() => (
                _folderChecker.CheckFolder(data.OutputFolder),
                CheckProject(data.DefaultProjectPath),
                CheckProject(data.MemoryProjectPath)));
            if (IsDisposed)
            {
                return false;
            }

            ShowFolderResult(folder);
            ShowProjectResult(DefaultProject, defaultProject);
            ShowProjectResult(MemoryProject, memoryProject);

            var database = data.Database;
            var someEntered = database.Server.Length > 0 || database.Database.Length > 0;
            var incomplete = someEntered && (database.Server.Length == 0 || database.Database.Length == 0 || database.Table.Length == 0);
            SetDatabaseError(incomplete ? Keys.DatabaseIncomplete : null);

            RefreshTextBlockProblems();

            return !OutputFolder.IsError
                && !DefaultProject.IsError
                && !MemoryProject.IsError
                && !incomplete
                && TextBlocks.All(block => block.NameProblem == TextBlockNameProblem.None);
        }

        // A project that is not entered is not set, which is allowed.
        private ProjectPathCheck CheckProject(string path) =>
            path.Length == 0 ? ProjectPathCheck.Valid : _pathChecker.CheckProject(path);

        private void ShowFolderResult(FolderCheck check)
        {
            switch (check)
            {
                case FolderCheck.NotFullyQualified:
                    OutputFolder.SetMessage(Keys.FolderNotAbsolute, isError: true);
                    break;
                case FolderCheck.Unusable:
                    OutputFolder.SetMessage(Keys.FolderUnusable, isError: true);
                    break;
                case FolderCheck.NotAFolder:
                    OutputFolder.SetMessage(Keys.FolderIsFile, isError: true);
                    break;
                case FolderCheck.NotFound:
                    OutputFolder.SetMessage(Keys.FolderNotFound, isError: false);
                    break;
                default:
                    OutputFolder.ClearMessage();
                    break;
            }
        }

        private static void ShowProjectResult(PathFieldViewModel field, ProjectPathCheck check)
        {
            switch (check)
            {
                case ProjectPathCheck.NotFullyQualified:
                    field.SetMessage(Keys.ProjectNotAbsolute, isError: true);
                    break;
                case ProjectPathCheck.NotFound:
                    field.SetMessage(Keys.ProjectNotFound, isError: false);
                    break;
                default:
                    field.ClearMessage();
                    break;
            }
        }

        private static string SaveErrorKey(SettingsSaveOutcome outcome) => outcome switch
        {
            SettingsSaveOutcome.NewerVersion => Keys.SaveNewerVersion,
            SettingsSaveOutcome.Unreadable => Keys.SaveUnreadable,
            SettingsSaveOutcome.Busy => Keys.SaveBusy,
            SettingsSaveOutcome.TooLarge => Keys.SaveTooLarge,
            SettingsSaveOutcome.Unavailable => Keys.SettingsUnavailable,
            _ => Keys.SaveFailed
        };

        private void Cancel() => CloseRequested?.Invoke(this, new CloseRequestedEventArgs(saved: false));

        // ── test of the connection ──

        private async Task TestConnectionAsync()
        {
            if (!InputsEnabled)
            {
                return;
            }

            CancelTest();
            var cancellation = new CancellationTokenSource();
            _testCancellation = cancellation;
            SetTest(ConnectionTestState.Testing, Keys.TestTesting);

            var database = new DatabaseData(_databaseServer.Trim(), _databaseName.Trim(), _databaseTable.Trim());
            ConnectionTestOutcome outcome;
            try
            {
                // The editor connects to the server; the call itself is also kept off the UI thread.
                outcome = await Task.Run(() => _editor.TestConnectionAsync(database, cancellation.Token));
            }
            catch (OperationCanceledException)
            {
                outcome = ConnectionTestOutcome.Cancelled;
            }
            catch (Exception)
            {
                outcome = ConnectionTestOutcome.Failed;
            }

            // A change of the values, or the closed window, replaced or cancelled this test: its answer is not wanted.
            var stillWanted = !IsDisposed && ReferenceEquals(_testCancellation, cancellation);
            cancellation.Dispose();
            if (!stillWanted)
            {
                return;
            }

            _testCancellation = null;
            ShowTestOutcome(outcome);
        }

        private void ShowTestOutcome(ConnectionTestOutcome outcome)
        {
            switch (outcome)
            {
                case ConnectionTestOutcome.Succeeded:
                    SetTest(ConnectionTestState.Succeeded, Keys.TestSucceeded);
                    break;
                case ConnectionTestOutcome.Cancelled:
                    SetTest(ConnectionTestState.None, null);
                    break;
                default:
                    SetTest(ConnectionTestState.Failed, TestFailureKey(outcome));
                    break;
            }
        }

        private static string TestFailureKey(ConnectionTestOutcome outcome) => outcome switch
        {
            ConnectionTestOutcome.NotConfigured => Keys.TestNotConfigured,
            ConnectionTestOutcome.TableInvalid => Keys.TestTableInvalid,
            ConnectionTestOutcome.ConnectionInvalid => Keys.TestConnectionInvalid,
            ConnectionTestOutcome.Unreachable => Keys.TestUnreachable,
            ConnectionTestOutcome.AccessDenied => Keys.TestAccessDenied,
            ConnectionTestOutcome.SchemaMismatch => Keys.TestSchemaMismatch,
            ConnectionTestOutcome.Timeout => Keys.TestTimeout,
            ConnectionTestOutcome.DriverMissing => Keys.TestDriverMissing,
            _ => Keys.TestFailed
        };

        // Stops a running test and forgets what it found.
        private void ResetTest()
        {
            CancelTest();
            if (_testState != ConnectionTestState.None)
            {
                SetTest(ConnectionTestState.None, null);
            }
        }

        private void CancelTest()
        {
            var running = _testCancellation;
            _testCancellation = null;
            if (running == null)
            {
                return;
            }

            try
            {
                running.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // The test has ended and disposed its source; there is nothing left to stop.
            }
        }

        private void SetTest(ConnectionTestState state, string? key)
        {
            _testState = state;
            _testKey = key;
            OnPropertyChanged(nameof(TestResultText));
            OnPropertyChanged(nameof(HasTestResult));
            OnPropertyChanged(nameof(IsTesting));
            OnPropertyChanged(nameof(TestSucceeded));
        }

        // ── messages and state ──

        private string? ProblemKey() => _problem switch
        {
            SettingsProblem.DamagedFileSetAside => Keys.SettingsDamaged,
            SettingsProblem.NewerVersion => Keys.SettingsNewerVersion,
            SettingsProblem.Unreadable => Keys.SettingsUnreadable,
            SettingsProblem.Unavailable => Keys.SettingsUnavailable,
            _ => null
        };

        private void SetSaveError(string key)
        {
            _saveErrorKey = key;
            OnPropertyChanged(nameof(SaveErrorText));
            OnPropertyChanged(nameof(HasSaveError));
        }

        private void ClearSaveError()
        {
            if (_saveErrorKey != null)
            {
                _saveErrorKey = null;
                OnPropertyChanged(nameof(SaveErrorText));
                OnPropertyChanged(nameof(HasSaveError));
            }
        }

        private void SetDatabaseError(string? key)
        {
            _databaseErrorKey = key;
            OnPropertyChanged(nameof(DatabaseError));
            OnPropertyChanged(nameof(HasDatabaseError));
        }

        private void ClearDatabaseError()
        {
            if (_databaseErrorKey != null)
            {
                SetDatabaseError(null);
            }
        }

        private void RaiseStateProperties()
        {
            OnPropertyChanged(nameof(IsLoading));
            OnPropertyChanged(nameof(IsSaving));
            OnPropertyChanged(nameof(ShowProgress));
            OnPropertyChanged(nameof(InputsEnabled));
            OnPropertyChanged(nameof(CanSave));
            OkCommand.RaiseCanExecuteChanged();
            CancelCommand.RaiseCanExecuteChanged();
            TestConnectionCommand.RaiseCanExecuteChanged();
            AddTextBlockCommand.RaiseCanExecuteChanged();
            RemoveTextBlockCommand.RaiseCanExecuteChanged();
            OutputFolder.BrowseCommand.RaiseCanExecuteChanged();
            DefaultProject.BrowseCommand.RaiseCanExecuteChanged();
            MemoryProject.BrowseCommand.RaiseCanExecuteChanged();
        }

        // The language changed: every text that was built from a key is written again.
        private void OnStringsChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (IsDisposed)
            {
                return;
            }

            OnPropertyChanged(nameof(ProblemText));
            OnPropertyChanged(nameof(SaveErrorText));
            OnPropertyChanged(nameof(DatabaseError));
            OnPropertyChanged(nameof(TestResultText));
            OutputFolder.RefreshText();
            DefaultProject.RefreshText();
            MemoryProject.RefreshText();
            foreach (var block in TextBlocks)
            {
                block.RefreshText();
            }
        }
    }
}
