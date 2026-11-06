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
        /// Executes all tasks in the current project once using the supplied runtime variables
        /// and returns a complete run summary including per-task outcomes.
        /// <para>
        /// Threading: in the main application, start the run from the UI thread, because it resets and updates
        /// UI-bound task state. Headless hosts accept any thread. The returned task can be awaited from any
        /// context.
        /// </para>
        /// <para>
        /// Busy behavior: it must not be called while <see cref="IsRunning"/> is <c>true</c>; doing so throws
        /// <see cref="InvalidOperationException"/> and does not interrupt the active run. A project load or a
        /// project edit (<c>New</c> / <c>ReplaceTaskCollection</c>) in progress is treated the same way.
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
        /// Cancellation: cancellation is honoured only at safe task boundaries.
        /// A task that has already started completes before the run stops; no card operation is
        /// interrupted and none is retried. The result has <see cref="ExecutionOutcome.Failure"/> and
        /// <see cref="ExecutionFailureReason.Cancelled"/> when cancelled mid-run.
        /// </para>
        /// <para>
        /// Event ordering: <see cref="ExecutionCompleted"/> is raised exactly once for every accepted run,
        /// after <see cref="IsRunning"/> has returned to <c>false</c> and before the returned task completes.
        /// </para>
        /// <para>
        /// Sensitive data: the host adds no key values, payload bytes, card data, or task descriptions to the
        /// returned <see cref="ExecResult"/>, its <see cref="TaskOutcome"/> entries, or the
        /// <see cref="ExecutionCompleted"/> payload. The one free-text field is
        /// <see cref="TaskOutcome.ProjectTaskIndex"/>, which is the task index exactly as stored in the project, so
        /// do not put secrets in task indexes. Failures are logged under the <see cref="ExecResult.RunId"/>.
        /// Exception messages and stack traces written to those logs pass through a best-effort filter: a value after
        /// <c>key</c>, <c>masterkey</c>, <c>picckey</c>, <c>appkey</c>, <c>readkey</c>, <c>writekey</c> or
        /// <c>payload</c> followed by <c>:</c> or <c>=</c> is replaced, as is any run of exactly 32 or 48 hexadecimal
        /// digits (16- and 24-byte keys), and message and stack trace are each cut to 256 characters. Other formats are
        /// not filtered, for example 6-byte MIFARE Classic keys or 8-byte DES keys (12 and 16 hex digits), separated
        /// hex, or other labels such as <c>KeyA</c> or <c>password</c>. Lines written by task code itself are outside
        /// this guarantee. Runtime variable values go to the execution service and may be substituted into task text
        /// and reports, so do not put secrets in them (see <see cref="ExecutionRequest"/>).
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
        /// <exception cref="InvalidOperationException">Thrown when a run, a project load, or a project edit is already in progress.</exception>
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
        /// only to clear results between runs, and only when <see cref="IsRunning"/> is <c>false</c>.
        /// <para>
        /// Busy behavior: it is not synchronized with a run. Calling it while <see cref="IsRunning"/> is <c>true</c>
        /// neither waits for the run nor is refused. It clears state the run is still reading and writing: execute
        /// conditions that depend on an earlier task then see a cleared (<c>Empty</c>) error level, so the dependent
        /// task is skipped, and the completion flags the run reads are cleared. For Mifare Classic, DESFire and
        /// common tasks (create report, check logic condition, run program), resetting the execution state after the
        /// task finished but before the loop moves on makes it run a second time, which breaks the one-attempt rule.
        /// </para>
        /// <para>
        /// Threading: in the main application it changes UI-bound task state, so call it on the UI thread.
        /// </para>
        /// </summary>
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
        /// exclusions apply: no key values, payload bytes, card data, or task descriptions added by the host.
        /// </para>
        /// </summary>
        event EventHandler<ExecResult> ExecutionCompleted;
    }
}
