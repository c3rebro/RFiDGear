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
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// Executes all tasks in the current project once using the supplied runtime variables
        /// and returns a complete run summary including per-task outcomes.
        /// <para>
        /// Threading: this method is safe to call from any thread. It must not be called
        /// while <see cref="IsRunning"/> is <c>true</c>; doing so throws <see cref="InvalidOperationException"/>
        /// and does not interrupt the active run. A project load in progress is treated the same way.
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
        /// </summary>
        /// <param name="request">Runtime variables and execution parameters. Use <see cref="ExecutionRequest.Empty"/> for a plain run.</param>
        /// <param name="ct">Cancellation token. Cancellation is checked between tasks, never during one.</param>
        /// <returns>Complete run summary; <see cref="ExecResult.TaskOutcomes"/> is always populated.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown when a report variable cannot be honoured (see <see cref="ExecutionRequest"/>),
        /// before the run is accepted and before any card operation.
        /// </exception>
        /// <exception cref="InvalidOperationException">Thrown when a run or a project load is already in progress.</exception>
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
        /// only to clear results between runs. Do not call it while <see cref="IsRunning"/> is <c>true</c>.
        /// </summary>
        void ResetStatus();

        /// <summary>
        /// Raised once per accepted run when it ends, whether it succeeded, failed, or was cancelled.
        /// <para>
        /// Threading: raised on the thread that completes the run (typically a thread-pool thread, not
        /// the UI thread). Subscribers run synchronously, one after another, before the caller receives
        /// the result, so keep them short. An exception thrown by a subscriber is logged and is
        /// <em>not</em> propagated to the caller or to the other subscribers.
        /// </para>
        /// </summary>
        event EventHandler<ExecResult> ExecutionCompleted;
    }
}
