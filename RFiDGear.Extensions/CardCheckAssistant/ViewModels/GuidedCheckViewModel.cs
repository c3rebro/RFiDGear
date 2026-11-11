using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

using RFiDGear.Contracts;
using RFiDGear.ExtensionLibraries.CardCheck;
using RFiDGear.Extensions.CardCheckAssistant.Localization;
using RFiDGear.Extensions.CardCheckAssistant.Mvvm;
using RFiDGear.Extensions.CardCheckAssistant.Services;
using RFiDGear.Extensions.CardCheckAssistant.Workflow;

using Keys = RFiDGear.Extensions.CardCheckAssistant.Localization.ExtensionStrings.Keys;

namespace RFiDGear.Extensions.CardCheckAssistant.ViewModels
{
    /// <summary>
    /// The guided check as the window shows it: the list of orders, the chip, the check, the verdict and the end. It
    /// binds to an <see cref="IGuidedWorkflow"/>, shows each snapshot it raises and passes the operator's commands on.
    /// <para>
    /// Every change reaches the page through one funnel (<c>OnUiThread</c>): it is posted to the window's dispatcher,
    /// applied there in the order it arrived, and ignored if an equal or newer snapshot was shown already or the page
    /// is disposed. A snapshot is shown in full, never as the difference to the one before: each property is computed
    /// from the snapshot and the page's own few inputs, and every change announces them all.
    /// </para>
    /// <para>
    /// The list of orders is refreshed by a timer while nothing else is going on: the list is shown, no order is
    /// selected, the last read worked, and no window of the page is open. A refused start leaves a notice that stays
    /// until the operator acts.
    /// </para>
    /// </summary>
    internal sealed class GuidedCheckViewModel : ObservableBase, IDisposable
    {
        /// <summary>How often the list of orders is read again while the page waits for the operator to choose.</summary>
        internal static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(10);

        private readonly IRFiDGearExtensionHost _host;
        private readonly Dispatcher _dispatcher;
        private readonly IGuidedWorkflow _workflow;
        private readonly ISettingsDialog? _settingsDialog;
        private readonly IImportOffer _importOffer;
        private readonly IGuidedPageServices _services;
        private readonly Func<TimeSpan, Action, IDisposable> _startTimer;
        private readonly CancellationTokenSource _cancellation = new CancellationTokenSource();
        private readonly EventHandler<WorkflowSnapshot> _onWorkflowChanged;
        private readonly ObservableCollection<OrderRowViewModel> _orders = new ObservableCollection<OrderRowViewModel>();
        private readonly ObservableCollection<TaskOutcomeRowViewModel> _rows = new ObservableCollection<TaskOutcomeRowViewModel>();
        private readonly LanguageOption[] _languages;

        private WorkflowSnapshot _snapshot = new WorkflowSnapshot();
        private long _shownVersion = -1;
        private int _disposed;
        private int _opened;
        private Task _openTask = Task.CompletedTask;
        private IDisposable? _timer;
        private int _modal;
        private bool _asking;
        private bool _opening;
        private Failure? _openFailure;
        private bool _syncingRows;
        private bool _tickRead;
        private bool _sawLoading;
        private bool _readCancelled;
        private bool _listRead;
        private bool _languageTouched;
        private bool _showCloseHint;
        private Failure? _notice;
        private OrderRowViewModel? _selectedOrder;
        private LanguageOption _selectedLanguage;
        private ReaderSnapshot? _hostCard;
        private bool _hostRunning;
        private ExecResult? _shownResult;

        /// <summary>Creates the page and shows the workflow's current picture.</summary>
        /// <param name="host">The initialized host service.</param>
        /// <param name="dispatcher">The dispatcher of the window; every change the page shows is made on it.</param>
        /// <param name="workflow">The guided workflow this page is the window of; the page disposes it.</param>
        /// <param name="settingsDialog">Opens the settings window; <c>null</c> when the page has no settings button.</param>
        /// <param name="importOffer">The one-time offer to import the old assistant's settings.</param>
        /// <param name="pageServices">The questions the page asks the operator, and the viewer for a report.</param>
        /// <param name="startTimer">Starts the repeating timer of the list refresh; the action may be called on any thread.</param>
        /// <exception cref="ArgumentNullException">An argument other than <paramref name="settingsDialog"/> is null.</exception>
        public GuidedCheckViewModel(
            IRFiDGearExtensionHost host,
            Dispatcher dispatcher,
            IGuidedWorkflow workflow,
            ISettingsDialog? settingsDialog,
            IImportOffer importOffer,
            IGuidedPageServices pageServices,
            Func<TimeSpan, Action, IDisposable> startTimer)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _workflow = workflow ?? throw new ArgumentNullException(nameof(workflow));
            _settingsDialog = settingsDialog;
            _importOffer = importOffer ?? throw new ArgumentNullException(nameof(importOffer));
            _services = pageServices ?? throw new ArgumentNullException(nameof(pageServices));
            _startTimer = startTimer ?? throw new ArgumentNullException(nameof(startTimer));

            Strings = new LocalizedStrings(SafeRead(() => host.AppContext.UICultureName, string.Empty));
            _languages = new[]
            {
                new LanguageOption(ReportLanguage.German, Keys.LanguageGerman, Strings),
                new LanguageOption(ReportLanguage.English, Keys.LanguageEnglish, Strings)
            };
            _selectedLanguage = _languages[0];

            RefreshCommand = new DelegateCommand(Refresh, () => Offers(WorkflowControls.Refresh));
            StartOrderCommand = new DelegateCommand(StartOrder, () => Offers(WorkflowControls.Start) && _selectedOrder?.IsStartable == true);
            ConfirmReplaceCommand = new DelegateCommand(() => _workflow.ConfirmReplace(true), () => Offers(WorkflowControls.ConfirmReplace));
            KeepReportsCommand = new DelegateCommand(() => _workflow.ConfirmReplace(false), () => Offers(WorkflowControls.DeclineReplace));
            StartCheckCommand = new DelegateCommand(() => _workflow.StartCheck(), () => Offers(WorkflowControls.StartCheck));
            FinishCommand = new DelegateCommand(() => _workflow.Finish(), () => Offers(WorkflowControls.Finish));
            FinishWithoutReadOutCommand = new DelegateCommand(FinishWithoutReadOut, () => Offers(WorkflowControls.FinishWithoutReadOut));
            OpenReportCommand = new AsyncCommand(() => OpenInViewerAsync(ReadOutReport), () => CanOpen(ReadOutReport), OnOpenFailed);
            OpenFolderCommand = new AsyncCommand(() => OpenInViewerAsync(ReadOutFolder), () => CanOpen(ReadOutFolder), OnOpenFailed);
            RetryCommand = new DelegateCommand(() => _workflow.Retry(), () => Offers(WorkflowControls.Retry));
            CancelCommand = new DelegateCommand(Cancel, () => Offers(WorkflowControls.Cancel));
            BackCommand = new DelegateCommand(Back, () => Offers(WorkflowControls.Back));
            OpenSettingsCommand = new AsyncCommand(OpenSettingsAsync, () => _settingsDialog != null && _modal == 0 && Offers(WorkflowControls.Refresh));

            // Subscribe first, read the state second: a change in between is then seen by the handler instead of lost.
            _onWorkflowChanged = (_, snapshot) => OnUiThread(() => Apply(snapshot));
            _workflow.Changed += _onWorkflowChanged;
            host.Reader.ReaderStateChanged += OnReaderStateChanged;
            host.Execution.IsRunningChanged += OnIsRunningChanged;
            host.AppContext.CultureChanged += OnCultureChanged;

            _hostCard = SafeRead<ReaderSnapshot?>(() => host.Reader.CurrentSnapshot, null);
            _hostRunning = SafeRead(() => host.Execution.IsRunning, false);
            Apply(_workflow.Current);
        }

        /// <summary>Gets the page's texts, as a binding source: <c>{Binding Strings[Key]}</c>.</summary>
        public LocalizedStrings Strings { get; }

        /// <summary>Gets a value indicating whether the page has been disposed.</summary>
        public bool IsDisposed => Volatile.Read(ref _disposed) == 1;

        // ── commands ──

        /// <summary>Gets the command that reads the list of orders again.</summary>
        public DelegateCommand RefreshCommand { get; }

        /// <summary>Gets the command that starts the check of the selected order.</summary>
        public DelegateCommand StartOrderCommand { get; }

        /// <summary>Gets the command that replaces the reports of an earlier check.</summary>
        public DelegateCommand ConfirmReplaceCommand { get; }

        /// <summary>Gets the command that keeps the reports of an earlier check and goes back to the list.</summary>
        public DelegateCommand KeepReportsCommand { get; }

        /// <summary>Gets the command that runs the check on the chip that was seen.</summary>
        public DelegateCommand StartCheckCommand { get; }

        /// <summary>Gets the command that makes the finished report and stores it.</summary>
        public DelegateCommand FinishCommand { get; }

        /// <summary>Gets the command that finishes the order with the report made before the read-out, after the read-out failed.</summary>
        public DelegateCommand FinishWithoutReadOutCommand { get; }

        /// <summary>Gets the command that opens the report of the read-out in the viewer the operator has set up.</summary>
        public AsyncCommand OpenReportCommand { get; }

        /// <summary>Gets the command that opens the folder of the report of the read-out.</summary>
        public AsyncCommand OpenFolderCommand { get; }

        /// <summary>Gets the command that repeats the step that failed.</summary>
        public DelegateCommand RetryCommand { get; }

        /// <summary>Gets the command that cancels what is going on.</summary>
        public DelegateCommand CancelCommand { get; }

        /// <summary>Gets the command that goes back to the list after the end of a check.</summary>
        public DelegateCommand BackCommand { get; }

        /// <summary>Gets the command that opens the settings window; only offered while the list is shown.</summary>
        public AsyncCommand OpenSettingsCommand { get; }

        /// <summary>Gets a value indicating whether the page has a settings window to open.</summary>
        public bool HasSettings => _settingsDialog != null;

        // ── where the window is ──

        /// <summary>Gets the step the window shows.</summary>
        public GuidedStep Step => GuidedSteps.Of(_snapshot.State);

        /// <summary>Gets a value indicating whether the list of orders is shown.</summary>
        public bool IsOrdersStep => Step == GuidedStep.Orders;

        /// <summary>Gets a value indicating whether the order is being prepared.</summary>
        public bool IsPrepareStep => Step == GuidedStep.Prepare;

        /// <summary>Gets a value indicating whether the question about earlier reports is shown.</summary>
        public bool IsReplaceStep => Step == GuidedStep.Replace;

        /// <summary>Gets a value indicating whether the chip is awaited or has been seen.</summary>
        public bool IsCardStep => Step == GuidedStep.Card;

        /// <summary>Gets a value indicating whether the check runs or has failed.</summary>
        public bool IsCheckStep => Step == GuidedStep.Check;

        /// <summary>Gets a value indicating whether the verdict is shown.</summary>
        public bool IsVerdictStep => Step == GuidedStep.Verdict;

        /// <summary>Gets a value indicating whether the report of the read-out is being checked.</summary>
        public bool IsReviewStep => Step == GuidedStep.Review;

        /// <summary>Gets a value indicating whether the finished report is made and stored, or that failed.</summary>
        public bool IsFinishStep => Step == GuidedStep.Finish;

        /// <summary>Gets a value indicating whether the check has ended.</summary>
        public bool IsEndStep => Step == GuidedStep.End;

        /// <summary>Gets a value indicating whether something is running.</summary>
        public bool IsBusy => _snapshot.IsBusy;

        /// <summary>Gets a value indicating whether a cancel is under way.</summary>
        public bool IsCancelling => _snapshot.IsCancelling;

        /// <summary>Gets the line that says the cancel is under way, or empty.</summary>
        public string CancellingText => _snapshot.IsCancelling ? Strings[Keys.GuidedCancelling] : string.Empty;

        /// <summary>Gets a value indicating whether the orders are the made-up ones of the test mode.</summary>
        public bool IsTestMode => _snapshot.IsTestMode;

        /// <summary>Gets the sentence that says what the test mode is.</summary>
        public string TestModeText => Strings[Keys.GuidedTestModeNotice];

        // ── the list of orders ──

        /// <summary>Gets the orders, in the order the order system gave them.</summary>
        public ObservableCollection<OrderRowViewModel> Orders => _orders;

        /// <summary>Gets or sets the order the operator picked. Setting it is the operator's act and clears an old notice.</summary>
        public OrderRowViewModel? SelectedOrder
        {
            get => _selectedOrder;
            set
            {
                // The list control writes null while the rows are replaced; that is not the operator letting go.
                if (_syncingRows || !SetProperty(ref _selectedOrder, value))
                {
                    return;
                }

                ClearNotice();
                OnPropertyChanged(nameof(SelectedOrderHint));
                RaiseCommandStates();
            }
        }

        /// <summary>Gets the two report languages to choose from.</summary>
        public IReadOnlyList<LanguageOption> Languages => _languages;

        /// <summary>Gets or sets the language of the report; follows the setting until the operator chooses one.</summary>
        public LanguageOption SelectedLanguage
        {
            get => _selectedLanguage;
            set
            {
                if (value != null && SetProperty(ref _selectedLanguage, value))
                {
                    _languageTouched = true;
                }
            }
        }

        /// <summary>Gets the hint under the list when the selected order cannot be checked, or empty.</summary>
        public string SelectedOrderHint =>
            _selectedOrder != null && !_selectedOrder.IsStartable ? Strings[Keys.GuidedOrderNotStartable] : string.Empty;

        /// <summary>Gets the text that stands for an empty list: still loading, no orders, or none in progress; empty when there is a list.</summary>
        public string OrdersEmptyText
        {
            get
            {
                if (_orders.Count > 0)
                {
                    return _orders.Any(order => order.IsStartable) ? string.Empty : Strings[Keys.GuidedOrdersNoneInProgress];
                }

                // A read the timer started does not make a list that was read before unread again.
                return _listRead && (_snapshot.State == WorkflowState.Idle || _tickRead) ? Strings[Keys.GuidedOrdersNone] : Strings[Keys.GuidedOrdersLoading];
            }
        }

        /// <summary>Gets a value indicating whether <see cref="OrdersEmptyText"/> is shown.</summary>
        public bool HasOrdersEmptyText => IsOrdersStep && OrdersEmptyText.Length > 0 && _snapshot.OrdersFailure == null;

        /// <summary>Gets a value indicating whether the list is being read again by the timer (then only a small line says so).</summary>
        public bool IsUpdatingOrders => _tickRead && _snapshot.State == WorkflowState.LoadingOrders;

        /// <summary>Gets the line shown while the timer reads the list.</summary>
        public string UpdatingText => Strings[Keys.GuidedOrdersUpdating];

        /// <summary>Gets a value indicating whether the footer offers Cancel as a button: not for a read the timer started.</summary>
        public bool ShowCancel => _snapshot.Controls.HasFlag(WorkflowControls.Cancel) && !IsUpdatingOrders;

        // ── the order and its steps ──

        /// <summary>Gets the job number of the order being checked, or empty.</summary>
        public string OrderJob => _snapshot.Order?.JobNumber ?? string.Empty;

        /// <summary>Gets the chip number of the order being checked, or empty.</summary>
        public string OrderChip => _snapshot.Order?.ChipNumber ?? string.Empty;

        /// <summary>Gets the customer of the order being checked, or empty.</summary>
        public string OrderCustomer =>
            _snapshot.Order == null ? string.Empty : _snapshot.Orders.FirstOrDefault(order => order.Id == _snapshot.Order.Id)?.CustomerName ?? string.Empty;

        /// <summary>Gets a value indicating whether an order is being checked.</summary>
        public bool HasOrder => _snapshot.Order != null;

        /// <summary>Gets the line that says the order is being prepared.</summary>
        public string PrepareText => Strings[Keys.GuidedPreparing];

        /// <summary>Gets the sentence that names the reports of an earlier check.</summary>
        public string ReplaceText => GuidedTexts.Replace(_snapshot.ExistingReports, Strings);

        /// <summary>Gets the note that the finished report is only replaced at the end.</summary>
        public string ReplaceNote => Strings[Keys.GuidedReplaceNote];

        /// <summary>Gets the reader line: its name and whether it is connected.</summary>
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

        /// <summary>Gets what the card step asks of the operator.</summary>
        public string PromptText => GuidedTexts.Prompt(_snapshot, _hostCard, _hostRunning, Strings);

        /// <summary>Gets the line for the phase the check is in, or the hint after a failed check or read-out.</summary>
        public string CheckText => GuidedTexts.CheckText(_snapshot, Strings);

        /// <summary>Gets the label of the button that repeats a failed step.</summary>
        public string RetryText => GuidedTexts.RetryText(_snapshot, Strings);

        /// <summary>Gets the label of the button at the foot of the page.</summary>
        public string CancelText => GuidedTexts.CancelText(_snapshot, Strings);

        /// <summary>Gets a value indicating whether the button that finishes without the read-out is shown (only after the read-out failed).</summary>
        public bool ShowFinishWithoutReadOut => _snapshot.State == WorkflowState.ReadOutFailed;

        /// <summary>Gets the note to keep the chip on the reader.</summary>
        public string KeepChipText => Strings[Keys.GuidedKeepChip];

        /// <summary>Gets the task results of the check run, in the order the host delivered them.</summary>
        public ObservableCollection<TaskOutcomeRowViewModel> Rows => _rows;

        /// <summary>Gets a value indicating whether there are task results to show.</summary>
        public bool HasRows => _rows.Count > 0;

        /// <summary>Gets how many tasks failed, as a sentence, or empty.</summary>
        public string SummaryText => ShownExecution(_snapshot) is { } result
            ? Strings.Format(Keys.SummaryFormat, result.FailedTasks, result.TotalTasks)
            : string.Empty;

        // ── the verdict ──

        /// <summary>Gets what the verdict means for the order.</summary>
        public string VerdictText => GuidedTexts.Verdict(_snapshot.Selection, Strings);

        /// <summary>Gets a value indicating whether the verdict is the unclear one, which is shown as a warning.</summary>
        public bool IsIndeterminate => _snapshot.Selection?.Verdict == ChipVerdict.Indeterminate;

        /// <summary>Gets a value indicating whether the chip can be programmed, which this version does not do (only Cancel is offered).</summary>
        public bool IsProgrammable => _snapshot.Selection?.Verdict == ChipVerdict.Programmable;

        /// <summary>Gets the chip type the report names, or empty.</summary>
        public string ChipTypeText => GuidedTexts.Line(Keys.GuidedChipTypeFormat, _snapshot.Findings?.ChipType, Strings);

        /// <summary>Gets the free memory the report names, or empty.</summary>
        public string FreeMemoryText => GuidedTexts.Line(Keys.GuidedFreeMemoryFormat, _snapshot.Findings?.FreeMemoryText, Strings);

        /// <summary>Gets the notes of the report, or empty.</summary>
        public string NotesText => GuidedTexts.Line(Keys.GuidedNotesFormat, _snapshot.Findings?.Notes, Strings);

        /// <summary>Gets the path of the first report, or empty.</summary>
        public string ReportPathText => GuidedTexts.Line(Keys.GuidedReportPathFormat, _snapshot.Check?.ReportPath, Strings);

        /// <summary>Gets the hint to open the first report to look at it.</summary>
        public string OpenReportHint => Strings[Keys.GuidedOpenReportHint];

        // ── the review ──

        /// <summary>Gets what the operator is asked to do at the review.</summary>
        public string ReviewPromptText => Strings[Keys.GuidedReviewPrompt];

        /// <summary>Gets the path of the report of the read-out, or empty.</summary>
        public string ReadOutPathText => GuidedTexts.Line(Keys.GuidedReadOutPathFormat, ReadOutReport, Strings);

        /// <summary>Gets what pressing the finish button commits to.</summary>
        public string ReviewCommitText => Strings[Keys.GuidedReviewCommit];

        /// <summary>Gets what is odd about the chip or its report; none if nothing is.</summary>
        public IReadOnlyList<string> ReviewWarnings => GuidedTexts.ReviewWarnings(_snapshot, Strings);

        /// <summary>Gets a value indicating whether there is a warning to show.</summary>
        public bool HasReviewWarnings => ReviewWarnings.Count > 0;

        // ── finishing and the end ──

        /// <summary>Gets the line while the finished report is made and stored, or the hint after that failed.</summary>
        public string FinishText => _snapshot.State == WorkflowState.CompletionFailed
            ? Strings[Keys.GuidedCompletionFailedHint]
            : Strings[Keys.GuidedCompleting];

        /// <summary>Gets what the end of the check came to.</summary>
        public string EndText => GuidedTexts.End(_snapshot, Strings);

        /// <summary>Gets a value indicating whether the order was finished.</summary>
        public bool IsCompleted => _snapshot.State == WorkflowState.Completed;

        // ── what went wrong ──

        /// <summary>
        /// Gets what the operator should read about a problem. In the list this is the notice a refused start left and,
        /// if it is another one, the reason the last read failed; in the other steps the failure of the step.
        /// </summary>
        public IReadOnlyList<FailureMessage> Messages
        {
            get
            {
                var messages = new List<FailureMessage>(2);
                if (Step == GuidedStep.Orders)
                {
                    AddMessage(messages, _notice);
                    AddMessage(messages, _snapshot.OrdersFailure);
                }
                else
                {
                    AddMessage(messages, _snapshot.Failure);
                    if (Step == GuidedStep.Review)
                    {
                        AddMessage(messages, _openFailure);
                    }
                }

                return messages;
            }
        }

        /// <summary>Gets a value indicating whether there is a message to show.</summary>
        public bool HasMessages => Messages.Count > 0;

        /// <summary>Gets a value indicating whether the hint that the window cannot be closed yet is shown.</summary>
        public bool ShowCloseHint
        {
            get => _showCloseHint;
            private set => SetProperty(ref _showCloseHint, value);
        }

        /// <summary>Gets the hint that the window cannot be closed yet.</summary>
        public string CloseHintText => Strings[Keys.GuidedCloseBlocked];

        // ── life of the page ──

        /// <summary>
        /// Starts what the page does once, when it is first shown: the one-time offer to import the old assistant's
        /// settings, then the first read of the list, then the timer that reads it again. Safe to call more than once,
        /// and it never throws.
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

        /// <summary>
        /// Asks whether the window may close. It may not while the workflow prepares, runs or stores something (the hint
        /// is shown then); while a check is in progress but nothing runs that cannot be left, the operator is asked first.
        /// </summary>
        /// <returns><c>true</c> if the window may close.</returns>
        public bool TryRequestClose()
        {
            if (IsDisposed)
            {
                return true;
            }

            var snapshot = _workflow.Current;
            switch (snapshot.Close)
            {
                case CloseAdvice.Allow:
                    return true;
                case CloseAdvice.Refuse:
                    ShowCloseHint = true;
                    return false;
                default:
                    return Confirm(snapshot);
            }
        }

        /// <summary>
        /// Stops the timer, stops listening, and disposes the workflow, which stops a wait and asks a running step to
        /// stop; an upload that has begun is not interrupted. Safe to call more than once.
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
            {
                return;
            }

            _timer?.Dispose();
            _timer = null;
            _workflow.Changed -= _onWorkflowChanged;
            _host.Reader.ReaderStateChanged -= OnReaderStateChanged;
            _host.Execution.IsRunningChanged -= OnIsRunningChanged;
            _host.AppContext.CultureChanged -= OnCultureChanged;

            _cancellation.Cancel();
            _cancellation.Dispose();
            _workflow.Dispose();
            _orders.Clear();
            _rows.Clear();
        }

        // ── the operator's commands ──

        private bool Offers(WorkflowControls control) => !IsDisposed && (_snapshot.Controls & control) != 0;

        private void Refresh()
        {
            ClearNotice();
            _tickRead = false;
            _workflow.RefreshOrders();
        }

        private void StartOrder()
        {
            var order = _selectedOrder;
            if (order == null)
            {
                return;
            }

            ClearNotice();
            _workflow.Start(order.Id, _selectedLanguage.Language);
        }

        private void Cancel()
        {
            // From the programming on, nothing resumes what is ended here: one click on a button that is always at the foot
            // of the page must not do it.
            if (GuidedTexts.CancelNeedsQuestion(_snapshot.State) && !Ask(Strings[Keys.GuidedCancelTitle], GuidedTexts.Close(_snapshot, Strings)))
            {
                return;
            }

            // A read that is cancelled ends in the list as it was, which looks like a read that found nothing.
            _readCancelled |= _snapshot.State == WorkflowState.LoadingOrders;
            _workflow.Cancel();
        }

        // The order is finished with the report made before the read-out, which holds no memory check: the operator is asked
        // first. Whatever was offered when the question was shown, the engine decides when it is answered.
        private void FinishWithoutReadOut()
        {
            if (Ask(Strings[Keys.GuidedAskFinishWithoutTitle], Strings[Keys.GuidedAskFinishWithout]))
            {
                _workflow.FinishWithoutReadOut();
            }
        }

        // ── the report of the read-out ──

        private string? ReadOutReport => _snapshot.ReadOut?.ReportPath;

        private string? ReadOutFolder => string.IsNullOrWhiteSpace(ReadOutReport) ? null : Path.GetDirectoryName(ReadOutReport);

        // Both buttons are one thing at a time: a second click, on either, waits for the viewer to have been asked.
        private bool CanOpen(string? path) =>
            !IsDisposed && !_opening && _snapshot.State == WorkflowState.Reviewing && !string.IsNullOrWhiteSpace(path);

        // The viewer is asked off this thread. What it answers counts only while the review is still shown.
        private async Task OpenInViewerAsync(string? path)
        {
            _opening = true;
            _openFailure = null;
            RaiseOpenerState();

            Failure? failure;
            try
            {
                failure = await _services.OpenReportAsync(path ?? string.Empty, _cancellation.Token);
            }
            finally
            {
                _opening = false;
            }

            if (IsDisposed)
            {
                return;
            }

            if (_snapshot.State == WorkflowState.Reviewing)
            {
                _openFailure = failure;
            }

            RaiseOpenerState();
        }

        // A viewer that failed in a way nobody expected: the page says so, by the code that has a text.
        private void OnOpenFailed(Exception exception)
        {
            if (IsDisposed || exception is OperationCanceledException)
            {
                return;
            }

            _openFailure = new Failure(FailureKind.Unexpected, OpenerFailureCodes.StartFailed, "The report could not be opened.");
            RaiseOpenerState();
        }

        private void RaiseOpenerState()
        {
            OnPropertyChanged(nameof(Messages));
            OnPropertyChanged(nameof(HasMessages));
            RaiseCommandStates();
        }

        private void Back()
        {
            // The list is read again at once: the order that was just finished must not look startable until the timer ticks.
            if (_workflow.Back())
            {
                SelectedOrder = null;
                _workflow.RefreshOrders();
            }
        }

        private Task OpenSettingsAsync()
        {
            if (_settingsDialog == null || IsDisposed)
            {
                return Task.CompletedTask;
            }

            ClearNotice();
            bool saved;
            _modal++;
            RaiseCommandStates();
            try
            {
                saved = _settingsDialog.Show(Strings);
            }
            finally
            {
                _modal--;
                RaiseCommandStates();
            }

            if (saved && !IsDisposed)
            {
                _workflow.RefreshOrders();
            }

            return Task.CompletedTask;
        }

        // The operator acted: what a refused start left behind has been read.
        private void ClearNotice()
        {
            if (_notice == null)
            {
                return;
            }

            _notice = null;
            OnPropertyChanged(nameof(Messages));
            OnPropertyChanged(nameof(HasMessages));
        }

        private bool Confirm(WorkflowSnapshot snapshot) => Ask(Strings[Keys.GuidedCloseTitle], GuidedTexts.Close(snapshot, Strings));

        // One question at a time: while one is open the page's own timer and refresh stand still (it counts as a modal
        // window), and a second question is answered "no" at once.
        private bool Ask(string title, string text)
        {
            if (_asking)
            {
                return false;
            }

            _asking = true;
            _modal++;
            try
            {
                return _services.Confirm(title, text);
            }
            finally
            {
                _modal--;
                _asking = false;
            }
        }

        // ── start and timer ──

        private async Task OpenAsync()
        {
            try
            {
                _modal++;
                try
                {
                    await _importOffer.OfferAsync(Strings, _cancellation.Token);
                }
                finally
                {
                    _modal--;
                }
            }
            catch (Exception)
            {
                // Importing is a convenience: a page whose offer failed still works.
            }

            if (IsDisposed)
            {
                return;
            }

            _workflow.RefreshOrders();
            _timer = _startTimer(RefreshInterval, OnTimerTick);
        }

        // Called on any thread: the check of the gate and the read both happen on the window's thread.
        private void OnTimerTick() => OnUiThread(() =>
        {
            var snapshot = _snapshot;
            if (_modal > 0 || snapshot.State != WorkflowState.Idle || snapshot.IsBusy || snapshot.OrdersFailure != null || _selectedOrder != null)
            {
                return;
            }

            _tickRead = true;
            if (!_workflow.RefreshOrders())
            {
                _tickRead = false;
            }
        });

        // ── what comes in ──

        private void OnReaderStateChanged(object? sender, EventArgs e) => OnUiThread(() =>
        {
            _hostCard = SafeRead<ReaderSnapshot?>(() => _host.Reader.CurrentSnapshot, null);
            RaiseAll();
        });

        private void OnIsRunningChanged(object? sender, EventArgs e) => OnUiThread(() =>
        {
            _hostRunning = SafeRead(() => _host.Execution.IsRunning, false);
            RaiseAll();
        });

        private void OnCultureChanged(object? sender, EventArgs e) => OnUiThread(() =>
        {
            Strings.Refresh(SafeRead(() => _host.AppContext.UICultureName, string.Empty));
            foreach (var language in _languages)
            {
                language.Refresh();
            }

            foreach (var row in _rows)
            {
                row.Refresh();
            }

            RaiseAll();
        });

        // The host and the workflow raise their events on whatever thread made the change, and the window may have
        // closed by the time the work runs, so the check is made again there. An exception inside the work stays with
        // the operation (it faults its task) and never reaches the application's dispatcher; the next change shows
        // everything in full.
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

        // Shows a snapshot in full. An equal or older one is ignored (the workflow delivers in order, but a posted one
        // can arrive after the constructor's own read).
        private void Apply(WorkflowSnapshot snapshot)
        {
            if (IsDisposed || snapshot.Version <= _shownVersion)
            {
                return;
            }

            _shownVersion = snapshot.Version;
            _snapshot = snapshot;
            TrackList(snapshot);
            TrackNotice(snapshot);
            SyncRows(snapshot.Orders);
            SyncTaskRows(ShownExecution(snapshot));
            if (snapshot.State != WorkflowState.Reviewing)
            {
                _openFailure = null;
            }

            if (!_languageTouched)
            {
                _selectedLanguage = _languages.First(option => option.Language == snapshot.DefaultLanguage);
            }

            ShowCloseHint = ShowCloseHint && snapshot.Close == CloseAdvice.Refuse;
            RaiseAll();
        }

        private void TrackList(WorkflowSnapshot snapshot)
        {
            if (snapshot.State != WorkflowState.LoadingOrders)
            {
                _tickRead = false;
            }

            if (snapshot.State == WorkflowState.LoadingOrders)
            {
                _sawLoading = true;
            }
            else if (_sawLoading && snapshot.State == WorkflowState.Idle && !snapshot.IsBusy)
            {
                _listRead |= !_readCancelled;
                _sawLoading = false;
                _readCancelled = false;
            }
        }

        // A refused start ends in the list with the reason in the failure; the next read would clear it, so the page
        // keeps it until the operator acts or the window leaves the list.
        private void TrackNotice(WorkflowSnapshot snapshot)
        {
            if (snapshot.State == WorkflowState.Idle && snapshot.Failure != null)
            {
                _notice = snapshot.Failure;
            }
            else if (GuidedSteps.Of(snapshot.State) != GuidedStep.Orders)
            {
                _notice = null;
            }
        }

        // The rows are replaced only when the list changed, and the selection is put back by id afterwards. The list
        // control writes null into the selection while the rows go, which the guard ignores.
        private void SyncRows(IReadOnlyList<OrderRecord> orders)
        {
            if (orders.SequenceEqual(_orders.Select(row => row.Order)))
            {
                return;
            }

            var selectedId = _selectedOrder?.Id;
            _syncingRows = true;
            try
            {
                _orders.Clear();
                foreach (var order in orders)
                {
                    _orders.Add(new OrderRowViewModel(order));
                }

                _selectedOrder = selectedId == null ? null : _orders.FirstOrDefault(row => row.Id == selectedId);
            }
            finally
            {
                _syncingRows = false;
            }

            OnPropertyChanged(nameof(SelectedOrder));
        }

        // The task results the page shows: those of the read-out from the read-out on (and none until it has run: the first
        // check's rows under a read-out would be a lie), those of the first check otherwise.
        private static ExecResult? ShownExecution(WorkflowSnapshot snapshot) =>
            snapshot.State is WorkflowState.ReadingOut or WorkflowState.ReadOutFailed or WorkflowState.Reviewing
                ? snapshot.ReadOut?.Execution
                : snapshot.Check?.Execution;

        // The rows follow the result the snapshot carries: a new result (another run) is a new set of rows, however many
        // tasks it has; the same result in a later snapshot leaves them alone.
        private void SyncTaskRows(ExecResult? result)
        {
            if (ReferenceEquals(result, _shownResult))
            {
                return;
            }

            _shownResult = result;
            _rows.Clear();
            if (result == null)
            {
                return;
            }

            foreach (var outcome in result.TaskOutcomes)
            {
                _rows.Add(new TaskOutcomeRowViewModel(outcome, Strings));
            }
        }

        private void RaiseAll()
        {
            // An empty name says that every property changed: each is computed from the snapshot and the page's inputs.
            OnPropertyChanged(string.Empty);
            RaiseCommandStates();
        }

        private void RaiseCommandStates()
        {
            RefreshCommand.RaiseCanExecuteChanged();
            StartOrderCommand.RaiseCanExecuteChanged();
            ConfirmReplaceCommand.RaiseCanExecuteChanged();
            KeepReportsCommand.RaiseCanExecuteChanged();
            StartCheckCommand.RaiseCanExecuteChanged();
            FinishCommand.RaiseCanExecuteChanged();
            FinishWithoutReadOutCommand.RaiseCanExecuteChanged();
            OpenReportCommand.RaiseCanExecuteChanged();
            OpenFolderCommand.RaiseCanExecuteChanged();
            RetryCommand.RaiseCanExecuteChanged();
            CancelCommand.RaiseCanExecuteChanged();
            BackCommand.RaiseCanExecuteChanged();
            OpenSettingsCommand.RaiseCanExecuteChanged();
        }

        private void AddMessage(ICollection<FailureMessage> messages, Failure? failure)
        {
            if (failure != null && messages.All(message => message.Code != failure.Code))
            {
                messages.Add(new FailureMessage(FailureTexts.Describe(failure, Strings), failure.Code));
            }
        }

        private static T SafeRead<T>(Func<T> read, T fallback)
        {
            try
            {
                return read();
            }
            catch (Exception)
            {
                // A host that cannot say is read as "unknown".
                return fallback;
            }
        }

        /// <summary>One of the two report languages the operator can choose between.</summary>
        internal sealed class LanguageOption : ObservableBase
        {
            private readonly string _key;
            private readonly LocalizedStrings _strings;

            /// <summary>Creates the option.</summary>
            /// <param name="language">The language of the report.</param>
            /// <param name="key">The text that names the language.</param>
            /// <param name="strings">The page's texts.</param>
            public LanguageOption(ReportLanguage language, string key, LocalizedStrings strings)
            {
                Language = language;
                _key = key;
                _strings = strings;
            }

            /// <summary>Gets the language of the report.</summary>
            public ReportLanguage Language { get; }

            /// <summary>Gets the name of the language, in the language of the page.</summary>
            public string Name => _strings[_key];

            /// <summary>Tells the page that the name changed because the language of the page did.</summary>
            public void Refresh() => OnPropertyChanged(nameof(Name));
        }

        /// <summary>A problem as the operator reads it: the text, and the code that support can ask for.</summary>
        /// <param name="Text">What went wrong and what to do about it.</param>
        /// <param name="Code">The failure's stable code.</param>
        internal sealed record FailureMessage(string Text, string Code);
    }
}
