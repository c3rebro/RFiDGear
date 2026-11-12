using System;

namespace RFiDGear.Contracts
{
    /// <summary>
    /// Controls the continuous automatic card-processing mode of the main application: the mode of the menu item
    /// "Write to all chips automatically" and of the <c>AUTOMODE=1</c> start-up argument. The host drives the one state
    /// machine of the application. A mode started through this interface is shown in the menu, can be stopped there,
    /// and finds and processes cards exactly as when the user started it; there is no second reader or task engine.
    /// <para>
    /// Explicit activation: nothing is processed automatically unless <see cref="Start"/> was called (or the user
    /// switched the mode on, or <c>AUTOMODE=1</c> was given). Loading a project, running tasks and connecting a reader
    /// never turn it on.
    /// </para>
    /// <para>
    /// What it does while it is on: about every 2.5 seconds the application looks at the reader. A card that is newly
    /// presented gets one run of all tasks of the project, the same run as "Run all tasks once". A card is processed
    /// <em>once per presence</em>. A card that stays on the reader is not processed again however often the reader is
    /// looked at. A presence ends when a look finds the reader without a card, so a card that is taken away and put
    /// back is processed again, and so is another card. A removal and a return within one look are not seen and count
    /// as the same presence. A card that is on the reader when the mode starts counts as newly presented, unless a run
    /// was made on it by hand, by <c>AUTORUN=1</c> or by an extension through
    /// <see cref="ITaskExecHost.ExecuteAllAsync(ExecutionRequest, System.Threading.CancellationToken)"/>, and it has not
    /// been taken away since: that run worked on the card, so the mode does not repeat it. The same one-attempt rule as
    /// for every run applies: nothing is retried, so a card whose run failed is processed again only after it was taken
    /// away and put back, which is for the operator to do.
    /// </para>
    /// <para>
    /// Stopping by itself: the mode stops, instead of guessing, when the state of the card or of the reader is unclear.
    /// That is the case when a run ends with a fault, a timeout or a cancellation, when the per-task watchdog gave a
    /// run up, when a reader that had answered stops answering (three looks in a row), and when the user changes the
    /// reader in the settings. It does not matter who started the run: a run by hand, or one that an extension started
    /// with <see cref="ITaskExecHost.ExecuteAllAsync(ExecutionRequest, System.Threading.CancellationToken)"/>, stops a
    /// mode that is on in the same way (for the extension's run, after <see cref="ITaskExecHost.IsRunningChanged"/> and
    /// <see cref="ITaskExecHost.ExecutionCompleted"/> have been raised for it). An ordinary failure of a task does not
    /// stop it: the run is reported as failed, the card is not retried while it stays, and the next card is processed.
    /// Whatever stops the mode, <see cref="IsAutoModeEnabled"/> is <c>false</c> afterwards and
    /// <see cref="AutoModeStateChanged"/> has been raised.
    /// </para>
    /// <para>
    /// Observing the cards: every run that the mode starts is reported with its result through
    /// <see cref="AutoModeRunCompleted"/>, under a <see cref="ExecResult.RunId"/> that also labels its lines in the
    /// application log. <see cref="ITaskExecHost.IsRunning"/> and <see cref="ITaskExecHost.IsRunningChanged"/> cover the
    /// run as well, and <see cref="IReaderHost.ReaderStateChanged"/> tells when a card comes or goes while the mode is
    /// on. Why the mode ended is in <see cref="LastStopReason"/>.
    /// </para>
    /// <para>
    /// Threading: <see cref="IsAutoModeEnabled"/>, <see cref="Start"/> and <see cref="Stop"/> may be used from any
    /// thread and never wait for the UI thread. <see cref="AutoModeStateChanged"/> is raised synchronously on the thread
    /// that made the change: the caller of <see cref="Start"/> or <see cref="Stop"/>, or the UI thread when the mode
    /// stops by itself or the user toggles it. <see cref="AutoModeRunCompleted"/> is raised on the UI thread. Neither is
    /// marshalled, so a subscriber that updates UI must marshal to its own dispatcher, and it should return quickly.
    /// Per card, in this order: the run starts (<see cref="ITaskExecHost.IsRunningChanged"/>, running), its tasks run, it
    /// ends (<see cref="ITaskExecHost.IsRunningChanged"/>, not running), <see cref="AutoModeRunCompleted"/> reports it,
    /// and only then, if that run made the mode stop, <see cref="AutoModeStateChanged"/>. A subscriber to
    /// <see cref="AutoModeStateChanged"/> therefore already has the result of the run that ended the mode.
    /// </para>
    /// <para>
    /// Headless hosts: a host without the main application's reader loop (the one the HIL tests create) cannot run the
    /// mode. There <see cref="IsAutoModeEnabled"/> is always <c>false</c>, <see cref="Stop"/> does nothing,
    /// <see cref="Start"/> throws <see cref="NotSupportedException"/>, and <see cref="AutoModeStateChanged"/> is never
    /// raised.
    /// </para>
    /// </summary>
    public interface IAutoModeHost
    {
        /// <summary>
        /// <c>true</c> while continuous automatic mode is on, whoever turned it on. It is the real state: it stays
        /// <c>true</c> while a run is in progress and while polling is held back for an operation of the main window,
        /// and becomes <c>false</c> the moment the mode is stopped, by <see cref="Stop"/>, by the user or by itself.
        /// <para>Threading: safe to read from any thread. The value can change right after it is read.</para>
        /// </summary>
        bool IsAutoModeEnabled { get; }

        /// <summary>
        /// Turns continuous automatic mode on (equivalent to the menu toggle). If it is on already, nothing happens and
        /// no event is raised.
        /// <para>
        /// A start insists on being ready, unlike the menu: a reader must be selected and the project must have at least
        /// one task. It also refuses to start while something else is in progress, because a card that was just run by
        /// that operation would otherwise be processed a second time. In all these cases it throws, changes nothing and
        /// raises no event. A program task <c>%EXIT</c> ends the run of the card as a normal finish; it does not close
        /// the application when the mode was started through this method.
        /// </para>
        /// <para>
        /// Threading: any thread. The state changes and <see cref="AutoModeStateChanged"/> is raised before this method
        /// returns; polling begins on the UI thread shortly afterwards, so a card that is on the reader is found at the
        /// next look, not necessarily at once.
        /// </para>
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// A project load, a task run, a project edit or a reader scan is in progress; no reader is selected; or the
        /// project has no task. Selecting a reader does not mean that one is plugged in: a reader that is missing is
        /// waited for, and only a reader that had answered and then stops answering stops the mode.
        /// </exception>
        /// <exception cref="NotSupportedException">The host has no reader loop (see the type remarks).</exception>
        void Start();

        /// <summary>
        /// Turns continuous automatic mode off. If it is off already, nothing happens and no event is raised.
        /// <para>
        /// It never throws and never waits. It does not interrupt a run that has started: a card operation is not
        /// stopped in the middle of a step. That run ends and is reported (its <see cref="AutoModeRunCompleted"/> follows
        /// the <see cref="AutoModeStateChanged"/> of this stop), and nothing is started after it. A card whose
        /// run has not started yet is not run either: the main application may make such a run wait (up to 20 seconds)
        /// for a reader scan of an extension to end, and a stop in that time withdraws it, so it does not start when the
        /// scan ends. The card that has been run is remembered, so starting the mode again does not process it a second
        /// time unless it was taken away and put back.
        /// </para>
        /// <para>Threading: any thread. <see cref="AutoModeStateChanged"/> is raised before this method returns.</para>
        /// </summary>
        void Stop();

        /// <summary>
        /// Raised each time <see cref="IsAutoModeEnabled"/> changes: once when the mode is turned on and once when it is
        /// turned off, by <see cref="Stop"/>, by the user or by itself. It is not raised for a call that changes nothing
        /// (<see cref="Start"/> on a mode that is on, <see cref="Stop"/> on one that is off) or is refused.
        /// <para>
        /// The state has changed when it is raised, so <see cref="IsAutoModeEnabled"/> reports the new value. The event
        /// carries no payload: subscribe first, then read <see cref="IsAutoModeEnabled"/> for the initial value, and read
        /// it again in the handler instead of assuming the direction of the change.
        /// </para>
        /// <para>
        /// Threading: raised synchronously on the thread that made the change, one subscriber after another, never
        /// marshalled. An exception from an ordinary (non-async) subscriber is logged (type name, redacted message and
        /// the start of the redacted stack trace) and does not reach the code that changed the state, nor the other
        /// subscribers. For an <c>async void</c> handler see <see cref="ITaskExecHost.ExecutionCompleted"/>.
        /// </para>
        /// </summary>
        event EventHandler AutoModeStateChanged;

        /// <summary>
        /// Why continuous automatic mode last ended: <see cref="AutoModeStopReason.StopRequested"/> after
        /// <see cref="Stop"/> or the menu toggle, another reason when the mode stopped by itself. It is
        /// <see cref="AutoModeStopReason.None"/> until the mode has ended for the first time, and again from the moment it
        /// is started anew. Read it when <see cref="AutoModeStateChanged"/> reports that the mode is off.
        /// <para>Threading: safe to read from any thread. It is set before <see cref="AutoModeStateChanged"/> is raised.</para>
        /// </summary>
        AutoModeStopReason LastStopReason { get; }

        /// <summary>
        /// Raised once for every run that continuous automatic mode started for a card, whatever the run ended with:
        /// success, a task that failed, a fault, a timeout. The payload is the result that
        /// <see cref="ITaskExecHost.ExecuteAllAsync(ExecutionRequest, System.Threading.CancellationToken)"/> returns for
        /// its own runs, with the same rules: <see cref="ExecResult.RunId"/> labels the run in the application log, the
        /// ordered <see cref="ExecResult.TaskOutcomes"/> say which task ran, was skipped or was not reached, and
        /// <see cref="ExecResult.Outcome"/> and <see cref="ExecResult.FailureReason"/> give the end of the run. So a run of a
        /// card can be judged without reading a log file.
        /// <para>
        /// Not raised for a run that was not started by this mode: a run by hand, <c>AUTORUN=1</c>, and a run that an
        /// extension started with <see cref="ITaskExecHost.ExecuteAllAsync(ExecutionRequest, System.Threading.CancellationToken)"/>
        /// (that one is reported to its caller and through <see cref="ITaskExecHost.ExecutionCompleted"/>, which is
        /// <em>not</em> raised for runs of this mode). Exactly one event is raised per run, and none for a card whose run
        /// was refused because something else was busy, since nothing ran; that card is tried again at the next look.
        /// </para>
        /// <para>
        /// Ordering: after <see cref="ITaskExecHost.IsRunning"/> has returned to <c>false</c> (and
        /// <see cref="ITaskExecHost.IsRunningChanged"/> has been raised for it), before the next card is looked at, and
        /// before <see cref="AutoModeStateChanged"/> if this run makes the mode stop. When this run makes the mode stop,
        /// the mode has not been stopped yet when the handler runs, so <see cref="IsAutoModeEnabled"/> still reads
        /// <c>true</c>; the stop follows at once on the same thread, and <see cref="AutoModeStateChanged"/> and
        /// <see cref="LastStopReason"/> tell it then.
        /// </para>
        /// <para>
        /// A stop that came <em>while the run was in progress</em> (<see cref="Stop"/>, the menu toggle, a change of the
        /// reader) is different: a run that has started is never interrupted, so the mode is off already, and
        /// <see cref="AutoModeStateChanged"/> has already been raised, when the handler runs. The result of that run still
        /// follows when it ends. Do not stop collecting results when the mode turns off: a run may still be in progress
        /// (<see cref="ITaskExecHost.IsRunning"/>), and its result is raised on the UI thread shortly after
        /// <see cref="ITaskExecHost.IsRunningChanged"/> reports that it has ended.
        /// </para>
        /// <para>
        /// Threading: raised synchronously on the UI thread, never marshalled, one subscriber after another. The next
        /// card is not looked at until the subscribers have returned, so keep them short. An exception from an ordinary
        /// (non-async) subscriber is logged (type name, redacted message and the start of the redacted stack trace) and
        /// reaches neither the mode nor the other subscribers.
        /// </para>
        /// <para>
        /// Sensitive data: the payload is built like the result of <c>ExecuteAllAsync</c>, so the same exclusions apply:
        /// no key values, payload bytes, card data or task descriptions are added by the host (see
        /// <see cref="ITaskExecHost.ExecuteAllAsync(ExecutionRequest, System.Threading.CancellationToken)"/>). The UID of
        /// the card is not part of it; an extension that needs it reads it through <see cref="IReaderHost"/>.
        /// </para>
        /// </summary>
        event EventHandler<ExecResult> AutoModeRunCompleted;
    }
}
