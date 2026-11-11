#nullable enable
using RFiDGear.Contracts;
using RFiDGear.Infrastructure.ExtensionHost;

using Serilog;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RFiDGear.Services.AutoMode
{
    /// <summary>Why automatic mode ended.</summary>
    internal enum AutoModeStopReason
    {
        /// <summary>Automatic mode has not been stopped since the application started.</summary>
        None = 0,

        /// <summary>The user or the host asked for it.</summary>
        StopRequested,

        /// <summary>The reader that had answered stopped answering.</summary>
        ReaderLost,

        /// <summary>The reader or its provider was changed in the settings.</summary>
        ReaderChanged,

        /// <summary>A run ended with a fault, a cancellation or an exception: the state of the card is unknown.</summary>
        RunAborted,

        /// <summary>A run was given up by the watchdog or ended with a timeout: the state of the card is unknown.</summary>
        RunTimedOut,
    }

    /// <summary>What <see cref="AutoModeController.TryStart"/> did.</summary>
    internal enum AutoModeStartResult
    {
        /// <summary>Automatic mode is on now.</summary>
        Started,

        /// <summary>Automatic mode was on already; nothing changed.</summary>
        AlreadyEnabled,

        /// <summary>A load, run, project edit or reader scan is in progress; nothing changed.</summary>
        Busy,

        /// <summary>No reader is selected (only when readiness was required); nothing changed.</summary>
        NoReader,

        /// <summary>The project has no task (only when readiness was required); nothing changed.</summary>
        NoTasks,
    }

    /// <summary>What one look at the reader showed.</summary>
    /// <param name="State">What the read meant; see <see cref="ReaderScanState"/>.</param>
    /// <param name="Uid">The UID of the primary card, or <c>null</c> when there is none.</param>
    internal readonly record struct AutoModePoll(ReaderScanState State, string? Uid);

    /// <summary>What came of the request to process a newly presented card.</summary>
    internal sealed class AutoModeRunReport
    {
        /// <summary>The request was not admitted (something else held the execution slot); nothing ran.</summary>
        internal static readonly AutoModeRunReport NotAdmitted = new AutoModeRunReport(false, null, false);

        /// <summary>Initializes a report.</summary>
        /// <param name="admitted">Whether a run was admitted.</param>
        /// <param name="result">The result of that run; required when it was admitted.</param>
        /// <param name="abandoned">Whether the watchdog gave the run up.</param>
        internal AutoModeRunReport(bool admitted, ExecResult? result, bool abandoned)
        {
            if (admitted && result == null)
            {
                throw new ArgumentNullException(nameof(result), "A run that was admitted has a result.");
            }

            Admitted = admitted;
            Result = result;
            Abandoned = abandoned;
        }

        /// <summary>Gets whether a run was admitted. Only then did anything happen to the card.</summary>
        internal bool Admitted { get; }

        /// <summary>Gets the result of the admitted run, or <c>null</c> when none was admitted.</summary>
        internal ExecResult? Result { get; }

        /// <summary>Gets whether the watchdog gave the run up.</summary>
        internal bool Abandoned { get; }
    }

    /// <summary>
    /// The one state machine of automatic mode: the menu toggle of the main window, the <c>AUTOMODE=1</c> start-up
    /// argument and the host API (<c>IAutoModeHost</c>) all drive this class, and nothing else polls the reader for new
    /// cards.
    /// <para>
    /// The timer is only a clock. What it is that automatic mode is on is <see cref="IsEnabled"/>, a flag of its own,
    /// because the timer cannot say: a run switches it off for its whole length and puts it back afterwards.
    /// </para>
    /// <para>
    /// A card is processed <em>once per presence</em>. A presence ends when a poll finds the reader without a card, so a
    /// card that stays is not processed again however often the clock ticks, a card that is taken away and put back is,
    /// and so is another card. A removal and a return within one poll interval are not seen and count as the same
    /// presence. The card of a presence is remembered when its run is <em>admitted</em>, before it starts, so a run that
    /// fails, times out or throws is never repeated by the machine: repeating is for the operator, who takes the card
    /// away and puts it back. A card whose run was refused because something else was busy has not been touched and
    /// is simply tried again at the next tick.
    /// </para>
    /// <para>
    /// Threading: <see cref="TryStart"/>, <see cref="Stop"/> and <see cref="IsEnabled"/> may be used from any thread and
    /// never wait. Everything else runs on the thread of the clock (the UI thread). <see cref="StateChanged"/> and
    /// <see cref="RunCompleted"/> are raised synchronously on the thread that made the change, one subscriber at a
    /// time; a subscriber that throws is logged and does not reach the machine.
    /// </para>
    /// </summary>
    internal sealed class AutoModeController
    {
        /// <summary>
        /// How many polls in a row must find a reader that had answered before not answering, until automatic mode
        /// stops because the reader is lost.
        /// </summary>
        internal const int ReaderLostAfterFailedPolls = 3;

        private static ILogger Logger => Log.ForContext<AutoModeController>();

        private readonly object _sync = new object();
        private readonly ExtensionOperationCoordinator _coordinator;
        private readonly Func<Task<AutoModePoll?>> _poll;
        private readonly Func<string, bool, Task<AutoModeRunReport>> _runCard;
        private readonly Action<bool> _setClock;
        private readonly Action<Action> _post;
        private readonly Func<bool> _isReaderSelected;
        private readonly Func<bool> _hasTasks;

        private bool _enabled;
        private AutoModeStopReason _stopReason;
        private bool _keepApplicationOpen;
        private int _ticking;
        private int _pauseCount;

        // Touched by the tick and by the calls that come from the UI thread only; the tick does not overlap itself.
        private string? _lastUid;
        private bool _readerSeen;
        private int _failedPolls;

        /// <summary>Initializes the machine; it is off until <see cref="TryStart"/>.</summary>
        /// <param name="coordinator">The admission gate shared with the runs of the main window and of extensions.</param>
        /// <param name="poll">
        /// Looks at the reader once, with the reader to itself; returns <c>null</c> when the reader is held by somebody
        /// else, in which case the tick does nothing.
        /// </param>
        /// <param name="runCard">
        /// Processes a card that was just presented (first argument: its UID; second: whether a program task <c>%EXIT</c>
        /// must leave the application open) and says what came of it. It reports a run that fails or throws as a result
        /// of the run; an exception that escapes it is taken as "the card state is unknown".
        /// </param>
        /// <param name="setClock">Switches the clock on or off. Called on the UI thread only (see <paramref name="post"/>).</param>
        /// <param name="post">Runs an action on the UI thread, at once when the caller is on it.</param>
        /// <param name="isReaderSelected">Whether a reader is selected, for a start that requires readiness.</param>
        /// <param name="hasTasks">Whether the project has a task, for a start that requires readiness.</param>
        internal AutoModeController(
            ExtensionOperationCoordinator coordinator,
            Func<Task<AutoModePoll?>> poll,
            Func<string, bool, Task<AutoModeRunReport>> runCard,
            Action<bool> setClock,
            Action<Action> post,
            Func<bool> isReaderSelected,
            Func<bool> hasTasks)
        {
            _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            _poll = poll ?? throw new ArgumentNullException(nameof(poll));
            _runCard = runCard ?? throw new ArgumentNullException(nameof(runCard));
            _setClock = setClock ?? throw new ArgumentNullException(nameof(setClock));
            _post = post ?? throw new ArgumentNullException(nameof(post));
            _isReaderSelected = isReaderSelected ?? throw new ArgumentNullException(nameof(isReaderSelected));
            _hasTasks = hasTasks ?? throw new ArgumentNullException(nameof(hasTasks));

            // A run puts the clock back to what it was when it began, which can undo a Stop that came in meanwhile. The
            // machine and the coordinator live for the whole session, so there is nothing to unsubscribe.
            _coordinator.IsExecutingChanged += (_, _) =>
            {
                if (!_coordinator.IsExecuting)
                {
                    SyncClock();
                }
            };
        }

        /// <summary>Gets whether automatic mode is on. Safe to read from any thread.</summary>
        internal bool IsEnabled
        {
            get
            {
                lock (_sync)
                {
                    return _enabled;
                }
            }
        }

        /// <summary>Gets why automatic mode last ended, or <see cref="AutoModeStopReason.None"/> if it never did.</summary>
        internal AutoModeStopReason LastStopReason
        {
            get
            {
                lock (_sync)
                {
                    return _stopReason;
                }
            }
        }

        /// <summary>
        /// Raised after <see cref="IsEnabled"/> changed, on the thread that changed it. Read <see cref="IsEnabled"/> and
        /// <see cref="LastStopReason"/> for the new state.
        /// </summary>
        internal event EventHandler? StateChanged;

        /// <summary>
        /// Raised for every run the machine started, after the run has ended and its slot is free, and before the
        /// machine stops because of that run (if it does).
        /// </summary>
        internal event EventHandler<ExecResult>? RunCompleted;

        /// <summary>
        /// Turns automatic mode on. It changes nothing when automatic mode is on already, or when something else is in
        /// progress. A card that is on the reader when it starts counts as newly presented, unless a run was made on
        /// it before (see <see cref="RememberCard"/>) and it has not been taken away since.
        /// </summary>
        /// <param name="requireReady">Also refuse when no reader is selected or the project has no task.</param>
        /// <param name="keepApplicationOpen">Whether a program task <c>%EXIT</c> ends the run of a card instead of closing the application.</param>
        /// <returns>What came of it; only <see cref="AutoModeStartResult.Started"/> changed anything.</returns>
        internal AutoModeStartResult TryStart(bool requireReady, bool keepApplicationOpen)
        {
            // The readiness checks look at the reader and the project, so they run outside the lock; the cheap
            // conditions are checked again inside it, where they decide.
            if (IsEnabled)
            {
                return AutoModeStartResult.AlreadyEnabled;
            }

            if (!_coordinator.IsIdle)
            {
                return AutoModeStartResult.Busy;
            }

            if (requireReady)
            {
                if (!_isReaderSelected())
                {
                    return AutoModeStartResult.NoReader;
                }

                if (!_hasTasks())
                {
                    return AutoModeStartResult.NoTasks;
                }
            }

            lock (_sync)
            {
                if (_enabled)
                {
                    return AutoModeStartResult.AlreadyEnabled;
                }

                if (!_coordinator.IsIdle)
                {
                    return AutoModeStartResult.Busy;
                }

                _enabled = true;
                _stopReason = AutoModeStopReason.None;
                _keepApplicationOpen = keepApplicationOpen;
                _readerSeen = false;
                _failedPolls = 0;
            }

            Logger.Information("Automatic mode started.");
            SyncClock();
            RaiseStateChanged();

            return AutoModeStartResult.Started;
        }

        /// <summary>
        /// Turns automatic mode off. It does nothing when it is off already, never throws and never waits. A run that is
        /// in progress is not interrupted: it ends, its result is reported, and nothing follows.
        /// </summary>
        /// <param name="reason">Why; the caller's own stop is <see cref="AutoModeStopReason.StopRequested"/>.</param>
        internal void Stop(AutoModeStopReason reason = AutoModeStopReason.StopRequested)
        {
            lock (_sync)
            {
                if (!_enabled)
                {
                    return;
                }

                _enabled = false;
                _stopReason = reason;
            }

            Logger.Information("Automatic mode stopped. Reason: {Reason}", reason);
            SyncClock();
            RaiseStateChanged();
        }

        /// <summary>
        /// The reader or its provider was changed: what the old reader showed says nothing about the new one, and a
        /// card that is on the new reader is not known. Automatic mode stops (it does not carry on with another reader
        /// unasked) and forgets the card.
        /// </summary>
        internal void OnReaderChanged()
        {
            Stop(AutoModeStopReason.ReaderChanged);
            ForgetCard();
        }

        /// <summary>
        /// Says that a run was made on the card that is on the reader, by a click or by <c>AUTORUN=1</c>, so that
        /// automatic mode, started now or later, does not run it a second time. The card counts as processed until a
        /// poll finds the reader without a card.
        /// </summary>
        /// <param name="uid">The UID of the primary card, as the polls report it.</param>
        internal void RememberCard(string uid)
        {
            if (!string.IsNullOrEmpty(uid))
            {
                _lastUid = uid;
            }
        }

        /// <summary>Forgets the card that was processed last, and whether the reader had answered.</summary>
        internal void ForgetCard()
        {
            _lastUid = null;
            _readerSeen = false;
            _failedPolls = 0;
        }

        /// <summary>
        /// Holds the clock's ticks back until the returned scope is disposed, without touching the on/off state: a
        /// <see cref="Stop"/> or <see cref="TryStart"/> that comes in meanwhile is kept. For an operation that reads the
        /// reader itself across an <c>await</c>.
        /// </summary>
        /// <returns>The scope; dispose it in a <c>finally</c> or a <c>using</c>.</returns>
        internal IDisposable PausePolling()
        {
            Interlocked.Increment(ref _pauseCount);

            return new PauseScope(this);
        }

        /// <summary>
        /// One tick of the clock: looks at the reader and, for a newly presented card, has it processed. It never throws
        /// and never overlaps itself; a tick that finds the machine off, paused or busy does nothing.
        /// </summary>
        /// <returns>A task that ends when the tick, and the run it started, has ended.</returns>
        internal async Task TickAsync()
        {
            if (!IsEnabled)
            {
                // The clock is on although automatic mode is not: a run put it back after a Stop. Put it right.
                SyncClock();

                return;
            }

            if (Volatile.Read(ref _pauseCount) > 0 || Interlocked.CompareExchange(ref _ticking, 1, 0) != 0)
            {
                return;
            }

            try
            {
                await TickCoreAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                // Unknown state: do not go on polling a card on the strength of it.
                var error = new SanitizedException(ex);
                Logger.Error(
                    "Automatic mode failed and stops. {ExceptionType}: {ExceptionMessage} {ExceptionStackTrace}",
                    error.TypeName, error.Message, error.StackTrace);
                Stop(AutoModeStopReason.RunAborted);
            }
            finally
            {
                Volatile.Write(ref _ticking, 0);
            }
        }

        private async Task TickCoreAsync()
        {
            // Something else has the card or the project: look again at the next tick. The presence does not change.
            if (!_coordinator.IsIdle)
            {
                return;
            }

            var poll = await _poll().ConfigureAwait(true);
            if (poll == null || !IsEnabled)
            {
                return;
            }

            if (poll.Value.State == ReaderScanState.Ready)
            {
                _readerSeen = true;
                _failedPolls = 0;

                if (string.IsNullOrEmpty(poll.Value.Uid))
                {
                    // A full poll without a card ends the presence.
                    _lastUid = null;
                }
                else if (!string.Equals(poll.Value.Uid, _lastUid, StringComparison.Ordinal))
                {
                    await ProcessCardAsync(poll.Value.Uid!).ConfigureAwait(true);
                }

                return;
            }

            // NoReader and ReadFailed say nothing about the card, and neither does a state that is not known (the
            // contract says to treat one like Busy). They never end a presence and never start a run. A reader that had
            // answered and now does not is lost, and then it is safer to stop than to guess; one that never answered
            // may simply not be plugged in yet.
            if (poll.Value.State != ReaderScanState.Busy && _readerSeen && ++_failedPolls >= ReaderLostAfterFailedPolls)
            {
                Stop(AutoModeStopReason.ReaderLost);
            }
        }

        private async Task ProcessCardAsync(string uid)
        {
            // Provisional: while the card is being processed it is the card of this presence. The run refines it with
            // RememberCard once it has read the card itself (the card may have been swapped since the poll), and it is
            // put back when the run was not admitted, because then nothing happened to the card. Left as it is when the
            // run throws: whether the card was touched is unknown, so nothing may repeat it.
            var previous = _lastUid;
            _lastUid = uid;

            AutoModeRunReport report;
            try
            {
                report = await _runCard(uid, _keepApplicationOpen).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                var error = new SanitizedException(ex);
                Logger.Error(
                    "Processing a card in automatic mode threw; automatic mode stops. {ExceptionType}: {ExceptionMessage} {ExceptionStackTrace}",
                    error.TypeName, error.Message, error.StackTrace);
                Stop(AutoModeStopReason.RunAborted);

                return;
            }

            if (!report.Admitted)
            {
                _lastUid = previous;

                return;
            }

            RaiseRunCompleted(report.Result!);

            var stopReason = StopReasonOf(report);
            if (stopReason != AutoModeStopReason.None)
            {
                Stop(stopReason);
            }

            // The run put the clock back to what it was; make it follow automatic mode again.
            SyncClock();
        }

        /// <summary>
        /// Says whether the end of a run makes it unsafe to go on. An ordinary failure of a task does not: the card is
        /// not retried while it stays, and the operator decides what to do with it. A fault, a timeout, a cancellation or
        /// a run the watchdog gave up leaves the card in a state nobody knows.
        /// </summary>
        private static AutoModeStopReason StopReasonOf(AutoModeRunReport report)
        {
            if (report.Abandoned)
            {
                return AutoModeStopReason.RunTimedOut;
            }

            return report.Result!.FailureReason switch
            {
                ExecutionFailureReason.None => AutoModeStopReason.None,
                ExecutionFailureReason.TimedOut => AutoModeStopReason.RunTimedOut,
                _ => AutoModeStopReason.RunAborted,
            };
        }

        /// <summary>
        /// Makes the clock follow <see cref="IsEnabled"/>, on the UI thread, with the value it has when the action runs,
        /// so that calls that race end in the state the last of them asked for. Never throws: at shutdown the UI thread
        /// may be gone.
        /// </summary>
        private void SyncClock()
        {
            try
            {
                _post(() => _setClock(IsEnabled));
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "The clock of automatic mode could not be set.");
            }
        }

        private void RaiseStateChanged()
        {
            var handlers = StateChanged;
            if (handlers == null)
            {
                return;
            }

            foreach (var handler in handlers.GetInvocationList().Cast<EventHandler>())
            {
                try
                {
                    handler(this, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    var error = new SanitizedException(ex);
                    Logger.Error(
                        "A subscriber of the state of automatic mode threw; the exception was not propagated. {ExceptionType}: {ExceptionMessage} {ExceptionStackTrace}",
                        error.TypeName, error.Message, error.StackTrace);
                }
            }
        }

        private void RaiseRunCompleted(ExecResult result)
        {
            var handlers = RunCompleted;
            if (handlers == null)
            {
                return;
            }

            foreach (var handler in handlers.GetInvocationList().Cast<EventHandler<ExecResult>>())
            {
                try
                {
                    handler(this, result);
                }
                catch (Exception ex)
                {
                    var error = new SanitizedException(ex);
                    Logger.Error(
                        "A subscriber of the end of a run of automatic mode threw for run {RunId}; the exception was not propagated. {ExceptionType}: {ExceptionMessage} {ExceptionStackTrace}",
                        result.RunId, error.TypeName, error.Message, error.StackTrace);
                }
            }
        }

        private sealed class PauseScope : IDisposable
        {
            private AutoModeController? _owner;

            internal PauseScope(AutoModeController owner) => _owner = owner;

            public void Dispose()
            {
                var owner = Interlocked.Exchange(ref _owner, null);
                if (owner != null)
                {
                    Interlocked.Decrement(ref owner._pauseCount);
                }
            }
        }
    }
}
