using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

using RFiDGear.Contracts;
using RFiDGear.Extensions.CardCheckAssistant.Localization;
using RFiDGear.Extensions.CardCheckAssistant.Mvvm;
using RFiDGear.Extensions.CardCheckAssistant.Services;

using Keys = RFiDGear.Extensions.CardCheckAssistant.Localization.ExtensionStrings.Keys;

namespace RFiDGear.Extensions.CardCheckAssistant.ViewModels
{
    /// <summary>What the page is doing for the user right now.</summary>
    internal enum RunPhase
    {
        /// <summary>Nothing.</summary>
        Idle,

        /// <summary>Checking the entered paths against the file system, off the UI thread.</summary>
        Checking,

        /// <summary>Loading the chosen project into RFiDGear.</summary>
        Loading,

        /// <summary>Running its tasks.</summary>
        Running
    }

    /// <summary>
    /// The state and behavior of one opened Card Check Assistant window: choose a project, enter the values the tasks use,
    /// run the project once and show what happened.
    /// <para>
    /// It listens to four events of the host. Every handler may be called on any thread, so each one hands its work to the
    /// window's dispatcher and re-reads the state there instead of trusting what the event implied. <see cref="Dispose"/>
    /// removes all four, once, and drops what the page was showing.
    /// </para>
    /// </summary>
    internal sealed class CardCheckAssistantViewModel : ObservableBase, IDisposable
    {
        /// <summary>The runtime variable the project's tasks read the job number from.</summary>
        internal const string JobNumberVariable = "$JOBNUMBER";

        /// <summary>The runtime variable the project's tasks read the chip number from.</summary>
        internal const string ChipNumberVariable = "$CHIPNUMBER";

        /// <summary>The runtime variable that names the report file.</summary>
        internal const string ReportTargetVariable = "REPORTTARGETPATH";

        private readonly IRFiDGearExtensionHost _host;
        private readonly Dispatcher _dispatcher;
        private readonly IFileDialogService _dialogs;
        private readonly IPathChecker _pathChecker;
        private readonly IAssistantSettings _settings;
        private readonly IImportOffer _importOffer;
        private readonly CancellationTokenSource _cancellation = new CancellationTokenSource();
        private readonly ObservableCollection<TaskOutcomeRowViewModel> _rows = new ObservableCollection<TaskOutcomeRowViewModel>();

        private int _disposed;
        private int _cancellationDisposed;
        private int _opened;
        private Task _openTask = Task.CompletedTask;
        private Task _persistChain = Task.CompletedTask;
        private AssistantDefaults _defaults = AssistantDefaults.None;
        private string _projectPath = string.Empty;
        private string _jobNumber = string.Empty;
        private string _chipNumber = string.Empty;
        private string _reportTargetPath = string.Empty;
        private MessageRef? _projectError;
        private MessageRef? _reportTargetError;
        private MessageRef? _runError;
        private RunPhase _phase;
        private bool _isExecutionRunning;
        private bool _showCloseHint;
        private ExecResult? _result;

        /// <param name="host">The initialized host service.</param>
        /// <param name="dispatcher">The dispatcher of the window; every change the page shows is made on it.</param>
        /// <param name="dialogs">The file picker.</param>
        /// <param name="pathChecker">The file-system checks of a run; the real ones when omitted.</param>
        /// <param name="settings">What the page remembers between windows; nothing when omitted.</param>
        /// <param name="importOffer">The one-time offer to import the old assistant's settings; none when omitted.</param>
        public CardCheckAssistantViewModel(
            IRFiDGearExtensionHost host,
            Dispatcher dispatcher,
            IFileDialogService dialogs,
            IPathChecker? pathChecker = null,
            IAssistantSettings? settings = null,
            IImportOffer? importOffer = null)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
            _pathChecker = pathChecker ?? new FileSystemPathChecker();
            _settings = settings ?? AssistantServices.None.Settings;
            _importOffer = importOffer ?? AssistantServices.None.ImportOffer;

            Strings = new LocalizedStrings(SafeRead(() => host.AppContext.UICultureName, string.Empty));
            RunCommand = new AsyncCommand(RunAsync, () => CanRun, OnUnhandledRunFailure);
            BrowseCommand = new DelegateCommand(Browse, () => InputsEnabled);

            // Subscribe first, read the state second: a change in between is then seen by the handler instead of lost.
            host.Execution.IsRunningChanged += OnIsRunningChanged;
            host.Project.ProjectChanged += OnProjectChanged;
            host.Reader.ReaderStateChanged += OnReaderStateChanged;
            host.AppContext.CultureChanged += OnCultureChanged;

            _isExecutionRunning = SafeRead(() => host.Execution.IsRunning, false);
        }

        /// <summary>The page's texts, as a binding source: <c>{Binding Strings[Key]}</c>.</summary>
        public LocalizedStrings Strings { get; }

        /// <summary>Runs the chosen project once.</summary>
        public AsyncCommand RunCommand { get; }

        /// <summary>Opens the file picker.</summary>
        public DelegateCommand BrowseCommand { get; }

        /// <summary>The result list, in the order the host delivered it.</summary>
        public ObservableCollection<TaskOutcomeRowViewModel> Rows => _rows;

        /// <summary>The full path of the project file.</summary>
        public string ProjectPath
        {
            get => _projectPath;
            set
            {
                if (SetProperty(ref _projectPath, value ?? string.Empty))
                {
                    ClearProjectError();
                }
            }
        }

        /// <summary>The value for <c>$JOBNUMBER</c>; blank leaves the variable out.</summary>
        public string JobNumber
        {
            get => _jobNumber;
            set => SetProperty(ref _jobNumber, value ?? string.Empty);
        }

        /// <summary>The value for <c>$CHIPNUMBER</c>; blank leaves the variable out.</summary>
        public string ChipNumber
        {
            get => _chipNumber;
            set => SetProperty(ref _chipNumber, value ?? string.Empty);
        }

        /// <summary>The report file, a full path; blank means no report.</summary>
        public string ReportTargetPath
        {
            get => _reportTargetPath;
            set
            {
                if (SetProperty(ref _reportTargetPath, value ?? string.Empty))
                {
                    ClearReportTargetError();
                }
            }
        }

        /// <summary>What is wrong with the project path, or <c>null</c>.</summary>
        public string? ProjectError => Describe(_projectError);

        /// <summary>Whether <see cref="ProjectError"/> has something to show.</summary>
        public bool HasProjectError => _projectError != null;

        /// <summary>What is wrong with the report file, or <c>null</c>.</summary>
        public string? ReportTargetError => Describe(_reportTargetError);

        /// <summary>Whether <see cref="ReportTargetError"/> has something to show.</summary>
        public bool HasReportTargetError => _reportTargetError != null;

        /// <summary>A failure of the whole run that belongs to no field, or <c>null</c>.</summary>
        public string? RunError => Describe(_runError);

        /// <summary>Whether <see cref="RunError"/> has something to show.</summary>
        public bool HasRunError => _runError != null;

        /// <summary>Whether this page is loading or running.</summary>
        public bool IsBusy => _phase != RunPhase.Idle;

        /// <summary>Whether the host is running tasks, whoever started them.</summary>
        public bool IsExecutionRunning => _isExecutionRunning;

        /// <summary>Whether the input controls can be used: not while this page or the host is busy.</summary>
        public bool InputsEnabled => !IsBusy && !_isExecutionRunning && !IsDisposed;

        /// <summary>Whether the run button can be used.</summary>
        public bool CanRun => InputsEnabled;

        /// <summary>The line that says what is going on, or empty.</summary>
        public string StatusText
        {
            get
            {
                if (_phase == RunPhase.Checking)
                {
                    return Strings[Keys.StatusChecking];
                }

                if (_phase == RunPhase.Loading)
                {
                    return Strings[Keys.StatusLoading];
                }

                if (_phase == RunPhase.Running)
                {
                    return Strings[Keys.StatusRunning];
                }

                return _isExecutionRunning ? Strings[Keys.StatusRunningElsewhere] : string.Empty;
            }
        }

        /// <summary>Whether the progress bar is shown.</summary>
        public bool ShowProgress => IsBusy || _isExecutionRunning;

        /// <summary>The reader line: its name and whether it is connected.</summary>
        public string ReaderText
        {
            get
            {
                var name = SafeRead(() => _host.Reader.ReaderName, null);
                var connected = SafeRead(() => (bool?)_host.Reader.IsConnected, null);

                var state = connected switch
                {
                    true => Strings[Keys.ReaderConnected],
                    false => Strings[Keys.ReaderNotConnected],
                    _ => Strings[Keys.ReaderUnknown]
                };

                return $"{Strings[Keys.ReaderLabel]} {(string.IsNullOrWhiteSpace(name) ? Strings[Keys.ReaderUnknown] : name)} ({state})";
            }
        }

        /// <summary>The line that says which project RFiDGear has open.</summary>
        public string OpenProjectText
        {
            get
            {
                var path = SafeRead(() => _host.Project.CurrentPath, null);
                var count = SafeRead(() => _host.Project.TaskCount, 0);

                var project = string.IsNullOrWhiteSpace(path)
                    ? Strings[Keys.NoOpenProject]
                    : Strings.Format(Keys.OpenProjectFormat, path!, count);

                return $"{Strings[Keys.OpenProjectLabel]} {project}";
            }
        }

        /// <summary>Whether a result is on show.</summary>
        public bool HasResult => _result != null;

        /// <summary>The terminal outcome of the last run, or empty.</summary>
        public string OutcomeText => _result == null
            ? string.Empty
            : _result.Outcome switch
            {
                ExecutionOutcome.Success => Strings[Keys.OutcomeSuccess],
                ExecutionOutcome.CompletedWithIntentionalFailures => Strings[Keys.OutcomeIntentionalFailures],
                _ => Strings[Keys.OutcomeFailure]
            };

        /// <summary>Why the last run failed, or <c>null</c> when the host gave no reason.</summary>
        public string? ReasonText
        {
            get
            {
                if (_result == null || _result.FailureReason == ExecutionFailureReason.None)
                {
                    return null;
                }

                var reason = _result.FailureReason switch
                {
                    ExecutionFailureReason.Cancelled => Strings[Keys.ReasonCancelled],
                    ExecutionFailureReason.TimedOut => Strings[Keys.ReasonTimedOut],
                    _ => Strings[Keys.ReasonFaulted]
                };

                return Strings.Format(Keys.ReasonFormat, reason);
            }
        }

        /// <summary>Whether <see cref="ReasonText"/> has something to show.</summary>
        public bool HasReason => ReasonText != null;

        /// <summary>How many tasks failed, as a sentence, or empty.</summary>
        public string SummaryText => _result == null
            ? string.Empty
            : Strings.Format(Keys.SummaryFormat, _result.FailedTasks, _result.TotalTasks);

        /// <summary>Whether the hint that the window cannot be closed yet is shown.</summary>
        public bool ShowCloseHint
        {
            get => _showCloseHint;
            private set => SetProperty(ref _showCloseHint, value);
        }

        /// <summary>The hint that the window cannot be closed while a run is going.</summary>
        public string CloseHintText => Strings[Keys.CloseBlocked];

        /// <summary>Whether <see cref="Dispose"/> has run.</summary>
        public bool IsDisposed => Volatile.Read(ref _disposed) == 1;

        /// <summary>
        /// Asks whether the window may close. It may not while this page is loading or running: closing would walk away
        /// from a card operation in the middle. The hint is then shown.
        /// </summary>
        /// <returns><c>true</c> if the window may close.</returns>
        public bool TryRequestClose()
        {
            if (!IsBusy)
            {
                return true;
            }

            ShowCloseHint = true;
            return false;
        }

        /// <summary>
        /// Stops listening to the host, asks a run that is still going to stop at its next safe point and drops what the
        /// page was showing. Safe to call more than once.
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
            {
                return;
            }

            _host.Execution.IsRunningChanged -= OnIsRunningChanged;
            _host.Project.ProjectChanged -= OnProjectChanged;
            _host.Reader.ReaderStateChanged -= OnReaderStateChanged;
            _host.AppContext.CultureChanged -= OnCultureChanged;

            try
            {
                _cancellation.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Already gone: nothing is waiting on it.
            }

            // A run that is still going owns the source until it ends; it disposes it then.
            if (!IsBusy)
            {
                DisposeCancellation();
            }

            _result = null;
            _rows.Clear();
        }

        /// <summary>
        /// Starts what the page does once, when it is first shown: prefill the project from what was remembered, then make
        /// the one-time offer to import the old assistant's settings, and prefill again if something was imported. What
        /// the operator has typed in the meantime is never overwritten. Safe to call more than once, and it never throws.
        /// </summary>
        /// <returns>The task of the start, which ends when the offer is answered or the window is closed.</returns>
        internal Task Open()
        {
            if (Interlocked.Exchange(ref _opened, 1) == 0)
            {
                _openTask = OpenAsync();
            }

            return _openTask;
        }

        private async Task OpenAsync()
        {
            try
            {
                await ApplyDefaultsAsync();
                if (IsDisposed)
                {
                    return;
                }

                if (await _importOffer.OfferAsync(Strings, _cancellation.Token) && !IsDisposed)
                {
                    await ApplyDefaultsAsync();
                }
            }
            catch (Exception)
            {
                // Remembering is a convenience: a page that cannot read its settings, or whose offer failed, still works.
            }
        }

        // The settings are read on a worker thread; the project is prefilled on the window's thread, and only while the
        // operator has not entered one.
        private async Task ApplyDefaultsAsync()
        {
            var defaults = await Task.Run(_settings.Load);
            if (IsDisposed)
            {
                return;
            }

            _defaults = defaults;
            if (string.IsNullOrWhiteSpace(ProjectPath) && !string.IsNullOrWhiteSpace(defaults.DefaultProjectPath))
            {
                ProjectPath = defaults.DefaultProjectPath;
            }
        }

        // Remembers where the project is, off the UI thread, so the picker opens there the next time.
        private void RememberProjectFolder(string projectPath)
        {
            string? folder;
            try
            {
                folder = Path.GetDirectoryName(projectPath.Trim());
            }
            catch (ArgumentException)
            {
                return;
            }

            if (string.IsNullOrEmpty(folder) || string.Equals(folder, _defaults.LastProjectFolder, StringComparison.Ordinal))
            {
                return;
            }

            _defaults = _defaults with { LastProjectFolder = folder };

            // One write after the other, in the order the projects were loaded: independent workers could finish out of
            // order and leave the older folder as the last one written. Only the window's thread gets here, so the chain
            // needs no lock of its own.
            _persistChain = _persistChain.ContinueWith(
                _ =>
                {
                    try
                    {
                        _settings.RememberProjectFolder(folder);
                    }
                    catch (Exception)
                    {
                        // Not remembering one folder is no reason to bother the operator.
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);
        }

        private void Browse()
        {
            // With no project entered the picker opens in the folder of the last one (a trailing separator means "this
            // folder, no file name").
            var start = string.IsNullOrWhiteSpace(ProjectPath) && !string.IsNullOrWhiteSpace(_defaults.LastProjectFolder)
                ? Path.TrimEndingDirectorySeparator(_defaults.LastProjectFolder) + Path.DirectorySeparatorChar
                : ProjectPath;

            var picked = _dialogs.PickProjectFile(start, Strings[Keys.PickerTitle], Strings[Keys.PickerFilter]);
            if (!string.IsNullOrWhiteSpace(picked))
            {
                ProjectPath = picked!;
            }
        }

        private async Task RunAsync()
        {
            if (!CanRun)
            {
                return;
            }

            ClearResultAndErrors();
            SetPhase(RunPhase.Checking);

            try
            {
                // The checks touch the disk, and on an unreachable network path that blocks for a long time, so they
                // run on a worker thread while the controls stay disabled and the window stays responsive.
                var projectText = ProjectPath;
                var reportText = ReportTargetPath;
                var (projectCheck, reportCheck) = await Task.Run(
                    () => (_pathChecker.CheckProject(projectText), _pathChecker.CheckReportTarget(reportText)));
                if (IsDisposed)
                {
                    return;
                }

                _projectError = ProjectMessage(projectCheck);
                _reportTargetError = ReportMessage(reportCheck);
                if (_projectError != null || _reportTargetError != null)
                {
                    RaiseErrorProperties();
                    return;
                }

                SetPhase(RunPhase.Loading);
                var token = _cancellation.Token;
                var load = await _host.Project.LoadAsync(ProjectPath.Trim(), token);
                if (IsDisposed)
                {
                    return;
                }

                if (!load.Success)
                {
                    _projectError = LoadMessage(load.Error);
                    RaiseErrorProperties();
                    return;
                }

                RememberProjectFolder(ProjectPath);
                SetPhase(RunPhase.Running);
                var result = await _host.Execution.ExecuteAllAsync(BuildRequest(), token);
                if (IsDisposed)
                {
                    return;
                }

                ShowResult(result);
            }
            catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
            {
                // The window was closed in the middle of the run; there is nobody left to tell.
            }
            catch (ArgumentException)
            {
                // The only text the page passes that the host can refuse is the report target.
                _reportTargetError = new MessageRef(Keys.ReportUnusable);
                RaiseErrorProperties();
            }
            catch (InvalidOperationException)
            {
                _runError = new MessageRef(Keys.RunBusy);
                RaiseErrorProperties();
            }
            catch (Exception ex)
            {
                // Only the type name goes on screen: the message may carry text the user must not be shown.
                _runError = new MessageRef(Keys.RunUnexpectedFormat, ex.GetType().Name);
                RaiseErrorProperties();
            }
            finally
            {
                if (!IsDisposed)
                {
                    SetPhase(RunPhase.Idle);
                }
                else
                {
                    // The window closed while this was running, so Dispose left the cancellation source to this method.
                    SetPhaseQuietly(RunPhase.Idle);
                    DisposeCancellation();
                }
            }
        }

        private ExecutionRequest BuildRequest()
        {
            var variables = new List<KeyValuePair<string, string>>();
            AddIfNotBlank(variables, JobNumberVariable, JobNumber);
            AddIfNotBlank(variables, ChipNumberVariable, ChipNumber);
            AddIfNotBlank(variables, ReportTargetVariable, ReportTargetPath);
            return new ExecutionRequest(variables);
        }

        private static void AddIfNotBlank(ICollection<KeyValuePair<string, string>> variables, string name, string value)
        {
            // A blank value means "not supplied". Sending it would be read as a value that is empty on purpose.
            if (!string.IsNullOrWhiteSpace(value))
            {
                variables.Add(new KeyValuePair<string, string>(name, value.Trim()));
            }
        }

        private void ShowResult(ExecResult result)
        {
            _result = result;
            _rows.Clear();
            foreach (var outcome in result.TaskOutcomes)
            {
                _rows.Add(new TaskOutcomeRowViewModel(outcome, Strings));
            }

            RaiseResultProperties();
        }

        private void ClearResultAndErrors()
        {
            _result = null;
            _rows.Clear();
            _projectError = null;
            _reportTargetError = null;
            _runError = null;
            ShowCloseHint = false;
            RaiseResultProperties();
            RaiseErrorProperties();
        }

        private void OnUnhandledRunFailure(Exception ex)
        {
            _runError = new MessageRef(Keys.RunUnexpectedFormat, ex.GetType().Name);
            RaiseErrorProperties();
        }

        private void SetPhase(RunPhase phase)
        {
            SetPhaseQuietly(phase);
            RaiseBusyProperties();
        }

        private void SetPhaseQuietly(RunPhase phase)
        {
            _phase = phase;
            if (phase == RunPhase.Idle)
            {
                ShowCloseHint = false;
            }
        }

        private void DisposeCancellation()
        {
            if (Interlocked.Exchange(ref _cancellationDisposed, 1) == 0)
            {
                _cancellation.Dispose();
            }
        }

        // ── host events ──

        private void OnIsRunningChanged(object? sender, EventArgs e) => OnUiThread(() =>
        {
            _isExecutionRunning = SafeRead(() => _host.Execution.IsRunning, false);
            RaiseBusyProperties();
        });

        private void OnProjectChanged(object? sender, EventArgs e) =>
            OnUiThread(() => OnPropertyChanged(nameof(OpenProjectText)));

        private void OnReaderStateChanged(object? sender, EventArgs e) =>
            OnUiThread(() => OnPropertyChanged(nameof(ReaderText)));

        private void OnCultureChanged(object? sender, EventArgs e) => OnUiThread(() =>
        {
            Strings.Refresh(SafeRead(() => _host.AppContext.UICultureName, string.Empty));
            foreach (var row in _rows)
            {
                row.Refresh();
            }

            RaiseAllTextProperties();
        });

        /// <summary>
        /// Runs a change on the window's dispatcher. The host raises its events on whatever thread made the change, and the
        /// window may have closed by the time the work runs, so the check is made again there.
        /// </summary>
        private void OnUiThread(Action work)
        {
            if (IsDisposed)
            {
                return;
            }

            _ = _dispatcher.InvokeAsync(() =>
            {
                if (!IsDisposed)
                {
                    work();
                }
            });
        }

        // ── notification ──

        private void RaiseBusyProperties()
        {
            OnPropertyChanged(nameof(IsBusy));
            OnPropertyChanged(nameof(IsExecutionRunning));
            OnPropertyChanged(nameof(InputsEnabled));
            OnPropertyChanged(nameof(CanRun));
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(ShowProgress));
            RunCommand.RaiseCanExecuteChanged();
            BrowseCommand.RaiseCanExecuteChanged();
        }

        private void RaiseErrorProperties()
        {
            OnPropertyChanged(nameof(ProjectError));
            OnPropertyChanged(nameof(HasProjectError));
            OnPropertyChanged(nameof(ReportTargetError));
            OnPropertyChanged(nameof(HasReportTargetError));
            OnPropertyChanged(nameof(RunError));
            OnPropertyChanged(nameof(HasRunError));
        }

        private void RaiseResultProperties()
        {
            OnPropertyChanged(nameof(HasResult));
            OnPropertyChanged(nameof(OutcomeText));
            OnPropertyChanged(nameof(ReasonText));
            OnPropertyChanged(nameof(HasReason));
            OnPropertyChanged(nameof(SummaryText));
        }

        private void RaiseAllTextProperties()
        {
            RaiseErrorProperties();
            RaiseResultProperties();
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(ReaderText));
            OnPropertyChanged(nameof(OpenProjectText));
            OnPropertyChanged(nameof(CloseHintText));
        }

        private void ClearProjectError()
        {
            if (_projectError != null)
            {
                _projectError = null;
                OnPropertyChanged(nameof(ProjectError));
                OnPropertyChanged(nameof(HasProjectError));
            }
        }

        private void ClearReportTargetError()
        {
            if (_reportTargetError != null)
            {
                _reportTargetError = null;
                OnPropertyChanged(nameof(ReportTargetError));
                OnPropertyChanged(nameof(HasReportTargetError));
            }
        }

        // ── messages ──

        private string? Describe(MessageRef? message) =>
            message == null ? null : Strings.Format(message.Key, message.Arguments);

        private static MessageRef? ProjectMessage(ProjectPathCheck check) => check switch
        {
            ProjectPathCheck.Required => new MessageRef(Keys.ProjectRequired),
            ProjectPathCheck.NotFullyQualified => new MessageRef(Keys.ProjectNotAbsolute),
            ProjectPathCheck.NotFound => new MessageRef(Keys.ProjectNotFound),
            _ => null
        };

        private static MessageRef? ReportMessage(ReportTargetCheck check) => check switch
        {
            ReportTargetCheck.NotFullyQualified => new MessageRef(Keys.ReportNotAbsolute),
            ReportTargetCheck.FolderMissing => new MessageRef(Keys.ReportFolderMissing),
            ReportTargetCheck.NotAFile => new MessageRef(Keys.ReportIsFolder),
            ReportTargetCheck.Unusable => new MessageRef(Keys.ReportUnusable),
            _ => null
        };

        private static MessageRef LoadMessage(ProjectOpenError error) => error switch
        {
            ProjectOpenError.PathNotFound => new MessageRef(Keys.LoadPathNotFound),
            ProjectOpenError.UnsupportedFormat => new MessageRef(Keys.LoadUnsupportedFormat),
            ProjectOpenError.Busy => new MessageRef(Keys.LoadBusy),
            _ => new MessageRef(Keys.LoadUnknown)
        };

        /// <summary>
        /// Reads something from the host without letting a fault in the host's own code break the page: what cannot be
        /// read is shown as unknown.
        /// </summary>
        private static T SafeRead<T>(Func<T> read, T fallback)
        {
            try
            {
                return read();
            }
            catch (Exception)
            {
                return fallback;
            }
        }

        /// <summary>A message kept as its key and arguments, so it can be written again in another language.</summary>
        private sealed class MessageRef
        {
            public MessageRef(string key, params object[] arguments)
            {
                Key = key;
                Arguments = arguments;
            }

            public string Key { get; }

            public object[] Arguments { get; }
        }
    }
}
