using System;
using System.Threading;
using System.Threading.Tasks;

namespace RFiDGear.Contracts
{
    /// <summary>
    /// Drives task execution from an extension or test harness.
    /// All execution follows the same one-attempt rule as the normal UI "Run All Tasks Once" path.
    /// </summary>
    public interface ITaskExecHost
    {
        /// <summary>
        /// <c>true</c> while a task run is in progress. This also reflects runs started from the
        /// main window, because both share one admission gate.
        /// <para>
        /// Threading: safe to read from any thread. The value can change right after it is read, so use it to
        /// update a display, not to predict whether <c>ExecuteAllAsync</c> will be accepted: that call can
        /// still throw <see cref="InvalidOperationException"/>.
        /// </para>
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// Raised each time <see cref="IsRunning"/> changes: once, with <see cref="IsRunning"/> already <c>true</c>,
        /// when a run is accepted, and once, with it already <c>false</c>, when that run has ended. It covers runs
        /// started from the main window as well as runs started through
        /// <see cref="ExecuteAllAsync(ExecutionRequest, CancellationToken)"/>, because both share one admission gate.
        /// <para>
        /// Not raised: for a call that is refused (busy, or an argument error before admission), for a project load,
        /// for a project edit, or for a reader scan. Those do not change <see cref="IsRunning"/>.
        /// </para>
        /// <para>
        /// Ordering: for a run started through this interface, the change to <c>true</c> comes before any task work,
        /// and the change to <c>false</c> comes before <see cref="ExecutionCompleted"/>, which comes before the
        /// returned task completes.
        /// </para>
        /// <para>
        /// Threading: raised synchronously on the thread that makes the change, and never marshalled. A main-window
        /// run changes it on the UI thread both times. A run started through this interface changes it to
        /// <c>true</c> on the calling thread and to <c>false</c> on whichever thread finishes the run (typically a
        /// thread-pool thread). A subscriber that updates UI must marshal to its own dispatcher. Keep handlers short:
        /// a run does not start, and its result is not delivered, until they return. A handler that calls
        /// <c>ExecuteAllAsync</c> in response to the change to <c>true</c> is refused as busy.
        /// </para>
        /// <para>
        /// Reading the state: subscribe first, then read <see cref="IsRunning"/> for the initial value. Inside the
        /// handler, or inside a callback it queues, read <see cref="IsRunning"/> again instead of assuming the
        /// direction of the change: queued callbacks can run late or in a different order, but the property is
        /// always current. The event carries no payload for that reason.
        /// </para>
        /// <para>
        /// Failures: an exception thrown by an ordinary (non-async) subscriber is logged (type name, redacted
        /// message, and the start of the redacted stack trace) and is <em>not</em> propagated to the code that started
        /// or ended the run, nor to the other subscribers. The host cannot catch exceptions from an <c>async void</c>
        /// handler; see <see cref="ExecutionCompleted"/> for what happens to those.
        /// </para>
        /// </summary>
        event EventHandler IsRunningChanged;

        /// <summary>
        /// Executes all tasks in the current project once using the supplied runtime variables
        /// and returns a complete run summary including per-task outcomes.
        /// <para>
        /// Threading: in the main application, start the run from the UI thread, because it resets and updates
        /// UI-bound task state. Headless hosts accept any thread. The returned task can be awaited from any
        /// context.
        /// </para>
        /// <para>
        /// Busy behavior: it must not be called while <see cref="IsRunning"/> is <c>true</c>; doing so throws
        /// <see cref="InvalidOperationException"/> and does not interrupt the active run. A project load, a
        /// project edit (<c>New</c> / <c>ReplaceTaskCollection</c>) or a reader scan (<see cref="IReaderHost.ScanAsync"/>)
        /// in progress is treated the same way.
        /// </para>
        /// <para>
        /// Fresh state: task completion state is reset when the run is accepted, so the returned
        /// <see cref="ExecResult.TaskOutcomes"/> never contain results left over from an earlier run.
        /// </para>
        /// <para>
        /// Failures: once a run is accepted, this method does not throw for cancellation, timeouts,
        /// or execution faults. It returns an <see cref="ExecResult"/> with <see cref="ExecutionOutcome.Failure"/>
        /// and an <see cref="ExecResult.FailureReason"/>; details are written to the application log
        /// under <see cref="ExecResult.RunId"/>. Argument validation errors are thrown before the run is accepted.
        /// After a <see cref="ExecutionFailureReason.TimedOut"/> result, <see cref="IsRunning"/> being <c>false</c>
        /// does not always mean that nothing is still running; see <see cref="ExecutionFailureReason.TimedOut"/>
        /// (in this release the host configures no stage timeouts, so that case cannot currently happen).
        /// </para>
        /// <para>
        /// A project may end with a program task whose text is <c>%EXIT</c>, which closes RFiDGear when the main window
        /// runs the project. In a run started through this method it does not: it ends the run at that task as a normal
        /// finish. The task counts as successful, the tasks after it are not started and appear as not reached, and the
        /// outcome is judged on the tasks that ran. RFiDGear stays open and the result is returned as usual.
        /// </para>
        /// <para>
        /// Cancellation: cancellation is honoured only at safe task boundaries.
        /// A task that has already started completes before the run stops; no card operation is
        /// interrupted and none is retried. The result has <see cref="ExecutionOutcome.Failure"/> and
        /// <see cref="ExecutionFailureReason.Cancelled"/> when cancelled mid-run.
        /// </para>
        /// <para>
        /// Automatic mode: in the main application a run started through this method works on the card that is on the
        /// reader, as a run of <see cref="IAutoModeHost"/> does. The mode therefore counts the card as processed, and does
        /// not run it again until it has been taken away and put back, and a run that ends with a fault, a timeout, a
        /// cancellation or the watchdog's giving up stops a mode that is on. The mode's own events follow
        /// <see cref="ExecutionCompleted"/>. A run of the mode itself is not reported here; see
        /// <see cref="IAutoModeHost.AutoModeStateChanged"/>.
        /// </para>
        /// <para>
        /// Event ordering: <see cref="ExecutionCompleted"/> is raised exactly once for every accepted run,
        /// after <see cref="IsRunning"/> has returned to <c>false</c> (and <see cref="IsRunningChanged"/> has been
        /// raised for it) and before the returned task completes.
        /// </para>
        /// <para>
        /// Sensitive data: the host adds no key values, payload bytes, or task descriptions to the
        /// returned <see cref="ExecResult"/>, its <see cref="TaskOutcome"/> entries, or the
        /// <see cref="ExecutionCompleted"/> payload, and the only card data it adds is the UID of the card the run worked
        /// on (<see cref="ExecResult.CardUid"/>; it identifies a card, so keep it out of logs that are not meant to hold
        /// it). The one free-text field is
        /// <see cref="TaskOutcome.ProjectTaskIndex"/>, which is the task index exactly as stored in the project, so
        /// do not put secrets in task indexes. Failures are logged under the <see cref="ExecResult.RunId"/>.
        /// Exception messages and stack traces written to those logs pass through a best-effort filter. It replaces
        /// (1) the value after any word that contains <c>key</c>, <c>secret</c>, <c>password</c>, <c>passwd</c>,
        /// <c>pwd</c>, <c>passphrase</c>, <c>token</c>, <c>credential</c> or <c>payload</c> (for example <c>Key</c>,
        /// <c>KeyA</c>, <c>MasterKey</c>, <c>Password</c>) when it is directly followed by <c>:</c> or <c>=</c> (a
        /// closing quote may sit between, as in JSON), up to the next space, comma or semicolon, or the whole value if it
        /// is quoted; (2) any run of exactly 12, 16, 32 or 48 hexadecimal digits (6-byte MIFARE Classic keys, 8-byte DES
        /// keys, 16- and 24-byte keys); and (3) six or more hexadecimal bytes written as pairs with spaces, colons,
        /// dashes or commas between them, with or without <c>0x</c>. The hexadecimal rules run first, so a labelled key
        /// written as separate bytes is removed whole. Message and stack trace are each cut to 256 characters
        /// afterwards. The filter is deliberately greedy and also removes harmless text of the same shape, such as a
        /// MAC address, the last 12 digits of a GUID, or a variable called <c>keyboard</c>. It does not recognise a
        /// secret that has neither a label directly followed by <c>:</c> or <c>=</c> nor one of those shapes: a Base64
        /// key, bytes written in decimal, hexadecimal of another length or written in groups of more than one byte, a
        /// password in a sentence (<c>password is ...</c>) or in an XML element. Lines written by task code itself are
        /// outside this guarantee. Runtime variable values go to the execution service and may be substituted into
        /// task text and reports, so do not put secrets in them (see <see cref="ExecutionRequest"/>).
        /// </para>
        /// </summary>
        /// <param name="request">Runtime variables and execution parameters. Use <see cref="ExecutionRequest.Empty"/> for a plain run.</param>
        /// <param name="ct">Cancellation token. Cancellation is checked between tasks, never during one.</param>
        /// <returns>Complete run summary; <see cref="ExecResult.TaskOutcomes"/> is always populated.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown when a report variable cannot be honoured (see <see cref="ExecutionRequest"/>),
        /// before the run is accepted and before any card operation.
        /// </exception>
        /// <exception cref="InvalidOperationException">Thrown when a run, a project load, a project edit, or a reader scan is already in progress.</exception>
        Task<ExecResult> ExecuteAllAsync(ExecutionRequest request, CancellationToken ct = default);

        /// <summary>
        /// Executes all tasks with no runtime variables. Equivalent to <c>ExecuteAllAsync(ExecutionRequest.Empty, ct)</c>,
        /// including its failure reporting: cancellation and faults are returned as a
        /// <see cref="ExecutionOutcome.Failure"/> result rather than thrown.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        [Obsolete("Use ExecuteAllAsync(ExecutionRequest, CancellationToken) to supply runtime variables. This overload passes an empty variable set.")]
        Task<ExecResult> ExecuteAllAsync(CancellationToken ct = default);

        /// <summary>
        /// Resets the completion flag, error level, and execution state of every task to their initial values.
        /// <see cref="ExecuteAllAsync(ExecutionRequest, CancellationToken)"/> does this automatically; call it
        /// only to clear results between runs.
        /// <para>
        /// Busy behavior: it is refused while a task run (<see cref="IsRunning"/>), a project load, a project edit
        /// (<c>New</c> / <c>ReplaceTaskCollection</c>) or a reader scan is in progress. The call then throws
        /// <see cref="InvalidOperationException"/> and changes nothing; the active operation is not interrupted.
        /// Clearing the state under a running task loop would make execute conditions that depend on an earlier task
        /// see a cleared (<c>Empty</c>) error level, and for Mifare Classic, DESFire and common tasks (create report,
        /// check logic condition, run program) it can make a task that already finished run a second time, which
        /// breaks the one-attempt rule. While the reset itself runs it counts as a project edit: a run, load, save or
        /// edit started at the same moment is rejected as busy.
        /// </para>
        /// <para>
        /// This check covers calls through this interface. The reset commands in the main window's own menus are
        /// separate and are not subject to it.
        /// </para>
        /// <para>
        /// Threading: in the main application it changes UI-bound task state, so call it on the UI thread.
        /// </para>
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when a run, a project load, a project edit, or a reader scan is in progress.</exception>
        void ResetStatus();

        /// <summary>
        /// Raised once per accepted run when it ends, whether it succeeded, failed, or was cancelled.
        /// <para>
        /// Threading: raised on the thread that completes the run (typically a thread-pool thread, not
        /// the UI thread). Subscribers run synchronously, one after another, before the caller receives
        /// the result, so keep them short. An exception thrown by an ordinary (non-async) subscriber is logged (type
        /// name, redacted message, and the start of the redacted stack trace; the exception object and inner
        /// exceptions are not logged) and is <em>not</em> propagated to the caller or to the other subscribers. The
        /// host cannot catch exceptions from an <c>async void</c> handler at all: they are rethrown on the
        /// synchronization context that was current when the handler started (or on a thread-pool thread),
        /// bypassing this event dispatch. They then reach the process-level unhandled-exception handling: in the
        /// main application the exception is logged as fatal (the raw exception, not filtered as described above), a
        /// dispatcher exception is marked handled, and an unhandled thread-pool exception ends the process. Handle
        /// exceptions inside such a handler.
        /// </para>
        /// <para>
        /// Sensitive data: the payload is the same <see cref="ExecResult"/> the caller receives, so the same
        /// exclusions apply: no key values, payload bytes, or task descriptions added by the host, and no card data other
        /// than the UID in <see cref="ExecResult.CardUid"/>.
        /// </para>
        /// </summary>
        event EventHandler<ExecResult> ExecutionCompleted;
    }
}
