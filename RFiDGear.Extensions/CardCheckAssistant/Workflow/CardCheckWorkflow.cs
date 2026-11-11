using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using RFiDGear.Contracts;
using RFiDGear.ExtensionLibraries.CardCheck;

namespace RFiDGear.Extensions.CardCheckAssistant.Workflow
{
    /// <summary>
    /// The guided check, without a window: from the list of orders to a finished, uploaded report, also for a chip that
    /// is programmed (waiting while it is programmed elsewhere, writing the second report, reading the memory out, the
    /// review; that leg is in <c>CardCheckWorkflow.Programming.cs</c>). The page binds to
    /// <see cref="Current"/> and <see cref="Changed"/> and calls the commands.
    /// <para>
    /// A command is a synchronous request: it returns <c>false</c> if the state does not offer it (see
    /// <see cref="WorkflowSnapshot.Controls"/>) and otherwise starts at most one operation. While an operation runs
    /// every command but <see cref="Cancel"/> is refused; a cancel is asked for, the operation ends in its own time
    /// (the host cancels a run only between two tasks), and what it brings back after the cancel is thrown away. The
    /// operation is awaitable through <see cref="CurrentOperation"/>, for a test or for closing the window.
    /// </para>
    /// <para>
    /// The workflow is for one context, the window's: call it from there and it continues there (it never uses
    /// <c>ConfigureAwait(false)</c>), and <see cref="Changed"/> is raised on the thread that made the change. Events are
    /// delivered one at a time in the order of the versions, also when a handler calls a command, and a handler that
    /// throws is logged and does not stop the others.
    /// </para>
    /// <para>
    /// The upload is not interrupted: a cancel is not offered while the report is being made and uploaded, and closing
    /// the window does not cancel it. Nothing that happens after <see cref="Dispose"/> raises an event.
    /// </para>
    /// </summary>
    [SuppressMessage("Minor Code Smell", "S2333:Partial is gratuitous in this context", Justification = "The class is split over two files to keep each of them short; the analysis reads one file at a time.")]
    internal sealed partial class CardCheckWorkflow : IGuidedWorkflow, IDisposable
    {
        /// <summary>How long the workflow waits for a card before it gives up, so that a window nobody uses does not look at the reader for ever.</summary>
        internal static readonly TimeSpan WaitCeiling = TimeSpan.FromMinutes(30);

        private readonly IWorkflowContextSource _context;
        private readonly IWorkflowSession _session;
        private readonly IReportFiles _files;
        private readonly IReportService _reports;
        private readonly ICheckRunner _runner;
        private readonly ICardWaiter _waiter;
        private readonly IOrderReservations _reservations;
        private readonly ICardCheckLog _log;
        private readonly CancellationTokenSource _disposeSource = new CancellationTokenSource();

        private readonly object _gate = new object();
        private readonly Queue<WorkflowSnapshot> _pending = new Queue<WorkflowSnapshot>();
        private bool _delivering;
        private WorkflowSnapshot _current;

        // What is going on. These are set before the event that tells it, and Cancel asks the token last.
        private bool _busy;
        private bool _cancelRequested;
        private bool _disposed;
        private long _operationId;
        private CancellationTokenSource? _operationSource;
        private Task _operation = Task.CompletedTask;
        private Job? _job;

        /// <summary>Creates the workflow.</summary>
        /// <param name="context">Where the order store and the settings come from.</param>
        /// <param name="session">The state of the check in this window; it must be this window's own.</param>
        /// <param name="files">The paths of the reports.</param>
        /// <param name="reports">Makes the finished report.</param>
        /// <param name="runner">Runs the check project.</param>
        /// <param name="waiter">Waits for a card.</param>
        /// <param name="reservations">Which orders the windows are checking; shared by all windows (<see cref="OrderReservations.Shared"/>).</param>
        /// <param name="log">Where an unexpected fault is noted (by type only).</param>
        /// <exception cref="ArgumentNullException">One of the arguments is null.</exception>
        public CardCheckWorkflow(
            IWorkflowContextSource context,
            IWorkflowSession session,
            IReportFiles files,
            IReportService reports,
            ICheckRunner runner,
            ICardWaiter waiter,
            IOrderReservations reservations,
            ICardCheckLog log)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _files = files ?? throw new ArgumentNullException(nameof(files));
            _reports = reports ?? throw new ArgumentNullException(nameof(reports));
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
            _waiter = waiter ?? throw new ArgumentNullException(nameof(waiter));
            _reservations = reservations ?? throw new ArgumentNullException(nameof(reservations));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _current = Compose(new WorkflowSnapshot());
        }

        /// <summary>Raised after every change, with the new snapshot; see the class description.</summary>
        public event EventHandler<WorkflowSnapshot>? Changed;

        /// <summary>The current picture; never <c>null</c>.</summary>
        public WorkflowSnapshot Current
        {
            get
            {
                lock (_gate)
                {
                    return _current;
                }
            }
        }

        /// <summary>
        /// The operation that is running, or a task that is complete if none is. It is in place before the first event of
        /// the operation is raised, so a handler of that event that waits for it waits for this one. It does not fail:
        /// whatever goes wrong ends in the state <see cref="WorkflowState.Faulted"/>.
        /// </summary>
        public Task CurrentOperation => _operation;

        /// <summary>Whether an order can be started: only an order that is in progress has a check to make.</summary>
        /// <param name="order">The order.</param>
        /// <returns><c>true</c> if its status is <see cref="OrderStatus.InProgress"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="order"/> is null.</exception>
        public static bool IsStartable(OrderRecord order) =>
            order == null ? throw new ArgumentNullException(nameof(order)) : order.Status == OrderStatus.InProgress;

        /// <summary>Reads the list of orders again.</summary>
        /// <returns><c>true</c> if the state offers it and the read has started.</returns>
        public bool RefreshOrders()
        {
            if (!Offers(WorkflowControls.Refresh))
            {
                return false;
            }

            Run(WorkflowState.LoadingOrders, ReadOrdersAsync, null);

            return true;
        }

        /// <summary>
        /// Starts the check of an order: the list is read again (so a stale list cannot start a finished order), the
        /// settings and the report paths are checked, and the operator is asked about reports of an earlier attempt, or
        /// the wait for a card begins. A refusal leaves the workflow in <see cref="WorkflowState.Idle"/> with
        /// <see cref="WorkflowSnapshot.Failure"/>.
        /// </summary>
        /// <param name="orderId">The identifier of the order.</param>
        /// <param name="language">The language of the report.</param>
        /// <returns><c>true</c> if the state offers it and the start has begun.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="orderId"/> is null.</exception>
        public bool Start(string orderId, ReportLanguage language)
        {
            if (orderId == null)
            {
                throw new ArgumentNullException(nameof(orderId));
            }

            if (!Offers(WorkflowControls.Start))
            {
                return false;
            }

            Run(WorkflowState.Starting, ct => StartAsync(orderId, language, ct), null);

            return true;
        }

        /// <summary>Answers the question about the reports of an earlier attempt.</summary>
        /// <param name="replace"><c>true</c> to go on (the reports are replaced as the check goes), <c>false</c> to go back to the list.</param>
        /// <returns><c>true</c> if the question was being asked.</returns>
        public bool ConfirmReplace(bool replace)
        {
            if (!Offers(replace ? WorkflowControls.ConfirmReplace : WorkflowControls.DeclineReplace))
            {
                return false;
            }

            if (replace)
            {
                Run(WorkflowState.WaitingForCard, WatchAsync, s => s with { ExistingReports = Array.Empty<ReportFile>() });
            }
            else
            {
                GoBack();
            }

            return true;
        }

        /// <summary>
        /// Runs the check with the card that was seen. The reader is looked at once more first: a card that was taken
        /// off, or swapped for another, does not start a check.
        /// </summary>
        /// <returns><c>true</c> if a card has been seen and the check has started.</returns>
        public bool StartCheck()
        {
            if (!Offers(WorkflowControls.StartCheck))
            {
                return false;
            }

            Run(WorkflowState.Checking, CheckAsync, null);

            return true;
        }

        /// <summary>Makes the finished report from the first report and uploads it; the order is then finished.</summary>
        /// <returns><c>true</c> if the verdict leads to a finish and the upload has started.</returns>
        public bool Finish()
        {
            if (!Offers(WorkflowControls.Finish))
            {
                return false;
            }

            Run(WorkflowState.Completing, _ => CompleteAsync(), null);

            return true;
        }

        /// <summary>
        /// Tries the failed step again, or waits again. After a failed check the workflow waits for the card again (the one
        /// that was seen stays until the look before the check says otherwise); after a wait for the chip to be taken off
        /// and put back ran out, it waits again, from where it was; after the second report or the change date could not be
        /// written, or the read-out failed, that step is done again by the operator's request. Which state does what is
        /// written out here: a state that is given the retry control must be given its case too.
        /// </summary>
        /// <returns><c>true</c> if the state offers it.</returns>
        public bool Retry()
        {
            if (!Offers(WorkflowControls.Retry))
            {
                return false;
            }

            switch (Current.State)
            {
                case WorkflowState.CompletionFailed:
                    Run(WorkflowState.Completing, _ => CompleteAsync(), s => s with { Failure = null });
                    return true;
                case WorkflowState.RunFailed when Current.Card != null:
                    Move(WorkflowState.WaitingForCard, s => s with { Failure = null, Check = null, Phase = null });
                    return true;
                case WorkflowState.RunFailed:
                    Run(WorkflowState.WaitingForCard, WatchAsync, null);
                    return true;
                case WorkflowState.Programming:
                    WaitForMoveAgain();
                    return true;
                case WorkflowState.ProgrammingFailed:
                    Run(WorkflowState.WritingReport, ProgramAsync, null);
                    return true;
                case WorkflowState.ReadOutFailed:
                    Run(WorkflowState.ReadingOut, ReadOutAsync, s => s with { ReadOut = null });
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Cancels what is going on. A running step is asked to stop and ends in its own time; in a state where nothing
        /// runs the workflow is cancelled at once. A second cancel while one is under way is not accepted.
        /// </summary>
        /// <returns><c>true</c> if the cancel was accepted.</returns>
        public bool Cancel()
        {
            if (!Offers(WorkflowControls.Cancel))
            {
                return false;
            }

            if (!_busy)
            {
                // After a failed step the failure stays: it says whether the report was not made or the order system did not
                // answer, and the page needs that to tell the operator what to check.
                Move(WorkflowState.Cancelled, s => s with { CancelledAt = s.State, Failure = s.State is WorkflowState.CompletionFailed or WorkflowState.ProgrammingFailed or WorkflowState.ReadOutFailed ? s.Failure : null });

                return true;
            }

            // The flag first, so that whatever the step brings back is thrown away; the token last, because the step
            // may continue inside this call.
            _cancelRequested = true;
            Change(s => s);
            _operationSource?.Cancel();

            return true;
        }

        /// <summary>Goes back to the list of orders after the end of a check.</summary>
        /// <returns><c>true</c> if the check had ended.</returns>
        public bool Back()
        {
            if (!Offers(WorkflowControls.Back))
            {
                return false;
            }

            GoBack();

            return true;
        }

        /// <summary>Stops the running step, if any, and stops raising events. An upload that has begun is not interrupted.</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            lock (_gate)
            {
                _pending.Clear();
            }

            _disposeSource.Cancel();
            _disposeSource.Dispose();

            // A step that is still running (an upload) keeps the order until it has ended.
            if (!_busy)
            {
                ReleaseJob();
            }
        }

        // ── the operations ──────────────────────────────────────────────────

        // A read of the list never faults the workflow: what nobody expected (a source or a store that throws) is logged by
        // type and told as a failed read, with the list emptied since its origin is not known. The operator can try again.
        private async Task ReadOrdersAsync(CancellationToken ct)
        {
            try
            {
                await ReadOrdersCoreAsync(ct);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                _log.Unexpected("workflow.orders", UnexpectedFault.From(ex));
                if (StopIfCancelled())
                {
                    return;
                }

                var failure = new Failure(FailureKind.Unexpected, ContextFailureCodes.OrdersUnexpected, "The list of orders cannot be read just now (" + ex.GetType().Name + ").");
                Settle(WorkflowState.Idle, s => s with { Orders = Array.Empty<OrderRecord>(), OrdersFailure = failure });
            }
        }

        private async Task ReadOrdersCoreAsync(CancellationToken ct)
        {
            var resolved = await Task.Run(() => _context.Resolve(), ct);
            if (StopIfCancelled())
            {
                return;
            }

            if (!resolved.IsSuccess)
            {
                Settle(WorkflowState.Idle, s => s with { OrdersFailure = resolved.Failure });
                return;
            }

            var listed = await resolved.Value.Store.ListAsync(ct);
            if (StopIfCancelled())
            {
                return;
            }

            var settings = resolved.Value.Settings;
            Settle(
                WorkflowState.Idle,
                s => listed.IsSuccess
                    ? WithSettings(s, settings) with { Orders = listed.Value, OrdersFailure = null }
                    : WithListFailure(s, settings, listed.Failure!));
        }

        private async Task StartAsync(string orderId, ReportLanguage language, CancellationToken ct)
        {
            var resolved = await Task.Run(() => _context.Resolve(), ct);
            if (StopIfCancelled())
            {
                return;
            }

            if (!resolved.IsSuccess)
            {
                Refuse(resolved.Failure!);
                return;
            }

            var context = resolved.Value;
            var settings = context.Settings;
            var listed = await context.Store.ListAsync(ct);
            if (StopIfCancelled())
            {
                return;
            }

            if (!listed.IsSuccess)
            {
                Change(s => WithListFailure(s, settings, listed.Failure!));
                if (StopIfCancelled())
                {
                    return;
                }

                Refuse(listed.Failure!);
                return;
            }

            var orders = listed.Value;
            Change(s => WithSettings(s, settings) with { Orders = orders, OrdersFailure = null });

            // A handler of that event may have cancelled: that comes before any refusal.
            if (StopIfCancelled())
            {
                return;
            }

            var order = orders.FirstOrDefault(o => string.Equals(o.Id, orderId, StringComparison.Ordinal));
            var refusal = RefusalFor(order, settings);
            if (refusal != null)
            {
                Refuse(refusal);
                return;
            }

            await BeginOrderAsync(context, order!, language, ct);
        }

        // The last good list stays when a read fails, unless it came from the other mode: made-up orders must not look
        // like real ones.
        private static WorkflowSnapshot WithListFailure(WorkflowSnapshot s, WorkflowSettings settings, Failure failure) => WithSettings(s, settings) with
        {
            Orders = settings.IsTestMode == s.IsTestMode ? s.Orders : Array.Empty<OrderRecord>(),
            OrdersFailure = failure,
        };

        // What the page shows of the settings as they were when the list was read or an order started.
        private static WorkflowSnapshot WithSettings(WorkflowSnapshot s, WorkflowSettings settings) => s with
        {
            IsTestMode = settings.IsTestMode,
            DefaultLanguage = settings.DefaultLanguage,
            TextBlocks = settings.TextBlocks,
        };

        // Why an order cannot be started, or null when it can.
        private static Failure? RefusalFor(OrderRecord? order, WorkflowSettings settings)
        {
            if (order == null)
            {
                return new Failure(FailureKind.NotFound, WorkflowFailureCodes.OrderUnknown, "There is no such order.");
            }

            if (!IsStartable(order))
            {
                return new Failure(FailureKind.InvalidInput, WorkflowFailureCodes.OrderNotStartable, "The order is not in progress.");
            }

            return string.IsNullOrWhiteSpace(settings.DefaultProjectPath)
                ? new Failure(FailureKind.NotConfigured, WorkflowFailureCodes.ProjectNotConfigured, "No check project is set.")
                : null;
        }

        // The order is startable: its paths are worked out, the check begins in the session, and the operator is asked about
        // earlier reports, or the wait for a card starts.
        private async Task BeginOrderAsync(WorkflowContext context, OrderRecord order, ReportLanguage language, CancellationToken ct)
        {
            var settings = context.Settings;
            var paths = _files.Resolve(settings.Location, order.JobNumber, order.ChipNumber);
            if (!paths.IsSuccess)
            {
                Refuse(paths.Failure!);
                return;
            }

            // Two windows must not check the same order: they would overwrite each other's reports.
            var lease = _reservations.TryReserve(paths.Value.Final);
            if (lease == null)
            {
                Refuse(new Failure(FailureKind.InvalidInput, WorkflowFailureCodes.OrderInUse, "The order is being checked in another window."));
                return;
            }

            var workflowOrder = new WorkflowOrder(order.Id, order.JobNumber, order.ChipNumber);
            _job = new Job(context.Store, settings, workflowOrder, paths.Value, language, lease);
            var begun = _session.Begin(workflowOrder, language);
            if (!begun.IsSuccess)
            {
                Refuse(begun.Failure!);
                return;
            }

            var existing = await Task.Run(() => ExistingReportsOf(paths.Value), ct);
            if (StopIfCancelled())
            {
                return;
            }

            Change(s => s with { Order = workflowOrder, Language = language, ExistingReports = existing });

            // A handler of that event may have cancelled; the cancel must not be swallowed by the settle that follows.
            if (StopIfCancelled())
            {
                return;
            }

            if (existing.Count > 0)
            {
                Settle(WorkflowState.ConfirmingReplace);
                return;
            }

            Move(WorkflowState.WaitingForCard, null);
            await WatchAsync(ct);
        }

        // Waits for a card; the operation ends with the card seen (and nothing running, so that no scan is in flight when
        // the check starts), or with the end of the wait.
        private async Task WatchAsync(CancellationToken ct)
        {
            var waited = await _waiter.WaitForCardAsync(WaitCeiling, ct);
            if (StopIfCancelled())
            {
                return;
            }

            switch (waited.Outcome)
            {
                case CardWaitOutcome.Done:
                    Settle(WorkflowState.WaitingForCard, s => s with { Card = waited.Last, Failure = null });
                    break;
                case CardWaitOutcome.TimedOut:
                    Settle(
                        WorkflowState.Cancelled,
                        s => s with { CancelledAt = s.State, Failure = new Failure(FailureKind.Timeout, WorkflowFailureCodes.WaitTimedOut, "No card was put on the reader.") });
                    break;
                default:
                    EndCancelled();
                    break;
            }
        }

        private async Task CheckAsync(CancellationToken ct)
        {
            var job = _job!;
            var shown = Current.Card;

            // One more look: since the card was seen it may have been taken off or swapped, and the project would then
            // read nothing or the wrong card.
            var look = await _waiter.WaitForCardAsync(TimeSpan.Zero, ct);
            if (StopIfCancelled())
            {
                return;
            }

            var proceed = await DecideAfterLookAsync(look, shown, ct);
            if (!proceed)
            {
                return;
            }

            var request = new CheckRunRequest(
                job.Settings.Location,
                job.Order.JobNumber,
                job.Order.ChipNumber,
                job.Settings.DefaultProjectPath,
                job.Language,
                ReportFile.Step1,
                null);
            var result = await _runner.RunAsync(request, phase => Change(s => s with { Phase = phase }), ct);
            if (StopIfCancelled())
            {
                return;
            }

            if (result.Status == CheckRunStatus.Cancelled)
            {
                EndCancelled();
            }
            else if (result.Status == CheckRunStatus.Failed)
            {
                Settle(WorkflowState.RunFailed, s => s with { Check = result, Failure = result.Failure, Phase = null });
            }
            else
            {
                Assess(job, result);
            }
        }

        // Whether the check may start after the look; if not, the workflow has been moved on (or settled).
        private async Task<bool> DecideAfterLookAsync(CardWaitResult look, ReaderSnapshot? shown, CancellationToken ct)
        {
            if (look.Outcome == CardWaitOutcome.Cancelled)
            {
                EndCancelled();

                return false;
            }

            if (look.Outcome == CardWaitOutcome.Done)
            {
                if (shown != null && !string.Equals(CardWaiter.PrimaryUid(shown), CardWaiter.PrimaryUid(look.Last!), StringComparison.OrdinalIgnoreCase))
                {
                    Settle(
                        WorkflowState.WaitingForCard,
                        s => s with { Card = look.Last, Failure = new Failure(FailureKind.InvalidInput, WorkflowFailureCodes.CardChanged, "Another card is on the reader now.") });

                    return false;
                }

                return true;
            }

            if (ReaderIsEmpty(look))
            {
                // The card is gone: wait for one again.
                Move(WorkflowState.WaitingForCard, s => s with { Card = null });
                await WatchAsync(ct);

                return false;
            }

            Settle(WorkflowState.WaitingForCard, s => s with { Failure = CardUnclear() });

            return false;
        }

        // The reader answered, and there is no card on it (as against an answer that says nothing: busy, failed, not there).
        private static bool ReaderIsEmpty(CardWaitResult look) =>
            look.Last != null && look.Last.State == ReaderScanState.Ready && !look.Last.HasCard;

        private static Failure CardUnclear() => new Failure(FailureKind.Unreachable, WorkflowFailureCodes.CardUnclear, "The reader gave no clear answer.");

        private void Assess(Job job, CheckRunResult result)
        {
            var recorded = _session.RecordFindings(job.Order.Id, result.Findings!);
            if (!recorded.IsSuccess)
            {
                Settle(WorkflowState.Faulted, s => s with { Failure = recorded.Failure, Phase = null });

                return;
            }

            var selection = _session.Current.Selection!;
            job.Plan = FinishRules.AfterFirstRun(selection.Verdict);

            // A chip that is to be programmed is not touched without a read-out project (the read-out would fail after the
            // chip was changed): the page is told why programming is not offered.
            var problem = selection.Verdict == ChipVerdict.Programmable ? job.Settings.MemoryProjectProblem : null;
            Settle(WorkflowState.Assessing, s => s with { Check = result, Findings = result.Findings, Selection = selection, ProgrammingProblem = problem, Phase = null });
        }

        // The upload is not interrupted, so nothing here takes the token of the operation.
        private async Task CompleteAsync()
        {
            var job = _job!;
            var plan = job.Plan!;

            var made = await Task.Run(() => MakeFinalReport(plan, job.Paths));
            if (made.Failure != null)
            {
                Settle(WorkflowState.CompletionFailed, s => s with { Failure = made.Failure });
                return;
            }

            var uploaded = await job.Store.FinalizeAsync(job.Order.Id, made.Report!, CancellationToken.None);
            if (!uploaded.IsSuccess)
            {
                Settle(WorkflowState.CompletionFailed, s => s with { Failure = uploaded.Failure });
                return;
            }

            var cleaned = await Task.Run(() => CleanUp(job, plan));
            Settle(WorkflowState.Completed, s => s with { Cleanup = cleaned.Report, CleanupFailed = cleaned.Failed, Failure = null });
        }

        // ── helpers of the operations ───────────────────────────────────────

        private static IReadOnlyList<ReportFile> ExistingReportsOf(OrderReportPaths paths) =>
            new[] { ReportFile.Step1, ReportFile.Step2, ReportFile.Step3, ReportFile.Final }
                .Where(file => File.Exists(paths.PathOf(file)))
                .ToArray();

        // The finished report is the chosen report with every field locked; it is read back as the bytes that are uploaded.
        private (Failure? Failure, byte[]? Report) MakeFinalReport(FinishPlan plan, OrderReportPaths paths)
        {
            var locked = _reports.SetFieldsReadOnly(paths.PathOf(plan.Source), paths.Final, true);
            if (!locked.IsSuccess)
            {
                return (locked.Failure, null);
            }

            try
            {
                return (null, File.ReadAllBytes(paths.Final));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return (new Failure(FailureKind.Io, WorkflowFailureCodes.FinalUnreadable, "The finished report cannot be read."), null);
            }
        }

        // After the upload nothing may go wrong: the order is finished whatever the cleanup does.
        private (CleanupReport Report, bool Failed) CleanUp(Job job, FinishPlan plan)
        {
            try
            {
                var report = _files.Cleanup(job.Paths, plan.Cleanup, job.Settings.RemoveTemporaryReports);

                return (report, report.Failed.Count > 0);
            }
            catch (Exception ex)
            {
                _log.Unexpected("workflow.cleanup", UnexpectedFault.From(ex));

                return (CleanupReport.Nothing, true);
            }
        }

        // A cancel that was asked for, or a window that was closed, wins over whatever the step brought back.
        private bool StopIfCancelled()
        {
            if (!_cancelRequested && !_disposed)
            {
                return false;
            }

            EndCancelled();

            return true;
        }

        // The end of a step that was cancelled: the list and the preparation go back to the list, everything else is a
        // cancelled check.
        private void EndCancelled()
        {
            var from = Current.State;
            if (from == WorkflowState.LoadingOrders || from == WorkflowState.Starting)
            {
                _session.Reset();
                ReleaseJob();
                Settle(WorkflowState.Idle, ClearOrderParts);
            }
            else
            {
                Settle(WorkflowState.Cancelled, s => s with { CancelledAt = from, Failure = null, Phase = null, MoveStage = null });
            }
        }

        // A start that is refused: back to the list with the reason.
        private void Refuse(Failure failure)
        {
            _session.Reset();
            ReleaseJob();
            Settle(WorkflowState.Idle, s => ClearOrderParts(s) with { Failure = failure });
        }

        private void GoBack()
        {
            _session.Reset();
            ReleaseJob();
            Move(WorkflowState.Idle, ClearOrderParts);
        }

        // The window lets go of its order: the reports of an order are held by one window from the start of its check
        // until it goes back to the list, is cancelled before the check, or is closed.
        private void ReleaseJob()
        {
            var job = _job;
            _job = null;
            job?.Lease.Dispose();
        }

        private static WorkflowSnapshot ClearOrderParts(WorkflowSnapshot s) => s with
        {
            Order = null,
            ExistingReports = Array.Empty<ReportFile>(),
            Card = null,
            Phase = null,
            MoveStage = null,
            Move = null,
            ReadOut = null,
            Check = null,
            Findings = null,
            Selection = null,
            ProgrammingProblem = null,
            Failure = null,
            CancelledAt = null,
            Cleanup = null,
            CleanupFailed = false,
        };

        // ── the engine room: the one operation slot and the delivery of the snapshots ──

        private bool Offers(WorkflowControls control) => !_disposed && (Current.Controls & control) != 0;

        // Starts an operation: the busy flag and the new state come first, so the first event already says so.
        private void Run(WorkflowState to, Func<CancellationToken, Task> body, Func<WorkflowSnapshot, WorkflowSnapshot>? edit)
        {
            _cancelRequested = false;
            _operationSource = CancellationTokenSource.CreateLinkedTokenSource(_disposeSource.Token);
            var token = _operationSource.Token;
            var id = ++_operationId;
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            // The awaitable comes before the event: a handler of the first event that waits for the operation must wait
            // for this one. A handler that starts the next operation replaces it, and this one does not take it back.
            _operation = completion.Task;
            _busy = true;
            Publish(to, s => (edit == null ? s : edit(s)) with { Failure = null, Phase = null });
            _ = ExecuteAsync(body, token, id, completion);
        }

        // The step with this identity runs its body and, if the body ends without having settled, faults. A step whose
        // slot was taken over by a later one (a handler started it as soon as this one settled) leaves the slot alone.
        private async Task ExecuteAsync(Func<CancellationToken, Task> body, CancellationToken token, long id, TaskCompletionSource<bool> completion)
        {
            try
            {
                await body(token);
                if (OwnsSlot(id))
                {
                    throw new InvalidOperationException("A step ended without a result.");
                }
            }
            catch (OperationCanceledException) when (OwnsSlot(id) && (_cancelRequested || _disposed))
            {
                EndCancelled();
            }
            catch (Exception ex)
            {
                // Only the type goes to the log: the message may carry text nobody must see.
                _log.Unexpected("workflow", UnexpectedFault.From(ex));
                if (OwnsSlot(id))
                {
                    Settle(WorkflowState.Faulted, s => s with { Failure = new Failure(FailureKind.Unexpected, WorkflowFailureCodes.Faulted, "Something unexpected went wrong (" + ex.GetType().Name + ")."), Phase = null, MoveStage = null });
                }
            }
            finally
            {
                completion.TrySetResult(true);
            }
        }

        private bool OwnsSlot(long id) => _busy && _operationId == id;

        // The step is over: the slot is free and the new state is announced in one event.
        private void Settle(WorkflowState to, Func<WorkflowSnapshot, WorkflowSnapshot>? edit = null)
        {
            _busy = false;
            _cancelRequested = false;
            var source = _operationSource;
            _operationSource = null;
            Publish(to, edit);
            source?.Dispose();
            if (_disposed)
            {
                ReleaseJob();
            }
        }

        private void Move(WorkflowState to, Func<WorkflowSnapshot, WorkflowSnapshot>? edit) => Publish(to, edit);

        // For the test that shows what happens when the workflow asks for a state the table does not allow.
        internal void MoveForTest(WorkflowState to) => Move(to, null);

        private void Change(Func<WorkflowSnapshot, WorkflowSnapshot> edit) => Publish(null, edit);

        // Makes the next snapshot and queues it. A state that may not follow the current one is a defect of the
        // workflow: it is logged and the workflow is faulted instead.
        private void Publish(WorkflowState? to, Func<WorkflowSnapshot, WorkflowSnapshot>? edit)
        {
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                var from = _current.State;
                var edited = edit == null ? _current : edit(_current);
                if (to.HasValue && to.Value != from)
                {
                    if (WorkflowTables.IsAllowed(from, to.Value))
                    {
                        _log.Information("workflow {From} -> {To}", from, to.Value);
                        edited = edited with { State = to.Value };
                    }
                    else
                    {
                        _log.Error("workflow {From} -> {To} is not allowed", from, to.Value);
                        _busy = false;
                        _cancelRequested = false;
                        edited = edited with
                        {
                            State = WorkflowState.Faulted,
                            Failure = new Failure(FailureKind.Unexpected, WorkflowFailureCodes.Faulted, "Something unexpected went wrong."),
                        };
                    }
                }

                var next = Compose(edited with { Version = _current.Version + 1 });
                _current = next;
                _pending.Enqueue(next);
                if (_delivering)
                {
                    return;
                }

                _delivering = true;
            }

            Deliver();
        }

        // What the operator may do follows from the state, whether something runs, and the details of the snapshot.
        private WorkflowSnapshot Compose(WorkflowSnapshot s)
        {
            var controls = WorkflowTables.ControlsOf(s.State);
            if (_busy)
            {
                controls &= WorkflowControls.Cancel;
            }

            if (_cancelRequested)
            {
                controls &= ~WorkflowControls.Cancel;
            }

            if (s.State == WorkflowState.WaitingForCard && s.Card == null)
            {
                controls &= ~WorkflowControls.StartCheck;
            }

            // The verdict decides how the check goes on: either there is a finish (the plan) or the chip is to be programmed.
            if (s.State == WorkflowState.Assessing)
            {
                var job = _job;
                if (job?.Plan == null)
                {
                    controls &= ~WorkflowControls.Finish;
                }

                if (job == null || job.Plan != null || job.Settings.MemoryProjectProblem != null)
                {
                    controls &= ~WorkflowControls.BeginProgramming;
                }
            }

            // Only a wait that ran out can be started again, and only a chip that was put back can go on.
            if (s.State == WorkflowState.Programming)
            {
                var outcome = s.Move?.Outcome;
                if (outcome != CardWaitOutcome.TimedOut)
                {
                    controls &= ~WorkflowControls.Retry;
                }

                if (outcome != CardWaitOutcome.Done)
                {
                    controls &= ~WorkflowControls.ContinueProgramming;
                }
            }

            return s with { IsBusy = _busy, IsCancelling = _cancelRequested, Controls = controls, Close = WorkflowTables.CloseAdviceOf(s.State) };
        }

        private void Deliver()
        {
            var drained = false;

            try
            {
                while (true)
                {
                    WorkflowSnapshot next;

                    lock (_gate)
                    {
                        if (_pending.Count == 0)
                        {
                            _delivering = false;
                            drained = true;

                            return;
                        }

                        next = _pending.Dequeue();
                    }

                    Raise(next);
                }
            }
            finally
            {
                if (!drained)
                {
                    lock (_gate)
                    {
                        _delivering = false;
                    }
                }
            }
        }

        private void Raise(WorkflowSnapshot snapshot)
        {
            var handlers = Changed;
            if (handlers == null)
            {
                return;
            }

            foreach (var handler in handlers.GetInvocationList().Cast<EventHandler<WorkflowSnapshot>>())
            {
                if (_disposed)
                {
                    return;
                }

                try
                {
                    handler(this, snapshot);
                }
                catch (Exception ex)
                {
                    _log.Unexpected("workflow.changed", UnexpectedFault.From(ex));
                }
            }
        }

        // What the workflow knows about the order being checked, from the start to the end.
        private sealed class Job
        {
            public Job(IOrderStore store, WorkflowSettings settings, WorkflowOrder order, OrderReportPaths paths, ReportLanguage language, IDisposable lease)
            {
                Lease = lease;
                Store = store;
                Settings = settings;
                Order = order;
                Paths = paths;
                Language = language;
            }

            // The reservation of the order's reports.
            public IDisposable Lease { get; }

            public IOrderStore Store { get; }

            public WorkflowSettings Settings { get; }

            public WorkflowOrder Order { get; }

            public OrderReportPaths Paths { get; }

            public ReportLanguage Language { get; }

            // How the check ends; there from the verdict on, and null for a chip that is to be programmed.
            public FinishPlan? Plan { get; set; }

            // The report fields the operator's answers make; there from the moment the answers were accepted, and kept for a
            // repeat of the write.
            public IReadOnlyDictionary<string, string>? Fields { get; set; }
        }
    }
}
