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
        /// <summary><c>true</c> while a task run is in progress.</summary>
        bool IsRunning { get; }

        /// <summary>
        /// Executes all tasks in the current project once using the supplied runtime variables
        /// and returns a complete run summary including ordered per-task outcomes.
        /// <para>
        /// Threading: this method is safe to call from any thread. It must not be called
        /// while <see cref="IsRunning"/> is <c>true</c>; doing so throws <see cref="InvalidOperationException"/>.
        /// </para>
        /// <para>
        /// Cancellation: cancellation is honoured only at safe task boundaries.
        /// A non-idempotent task that has already started will complete before the run stops.
        /// The result will have <see cref="ExecutionOutcome.Failure"/> when cancelled mid-run.
        /// </para>
        /// </summary>
        /// <param name="request">Runtime variables and execution parameters. Use <see cref="ExecutionRequest.Empty"/> for a plain run.</param>
        /// <param name="ct">Cancellation token. Cancellation is fail-safe; it never leaves the card in an undefined state.</param>
        /// <returns>Complete run summary; <see cref="ExecResult.TaskOutcomes"/> is always populated.</returns>
        /// <exception cref="InvalidOperationException">Thrown when a run is already in progress.</exception>
        Task<ExecResult> ExecuteAllAsync(ExecutionRequest request, CancellationToken ct = default);

        /// <summary>
        /// Executes all tasks with no runtime variables. Equivalent to <c>ExecuteAllAsync(ExecutionRequest.Empty, ct)</c>.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        [Obsolete("Use ExecuteAllAsync(ExecutionRequest, CancellationToken) to supply runtime variables. This overload passes an empty variable set.")]
        Task<ExecResult> ExecuteAllAsync(CancellationToken ct = default);

        /// <summary>Resets all task completion states to their initial values.</summary>
        void ResetStatus();

        /// <summary>Raised on the thread-pool when a task run completes (success or failure).</summary>
        event EventHandler<ExecResult> ExecutionCompleted;
    }
}
