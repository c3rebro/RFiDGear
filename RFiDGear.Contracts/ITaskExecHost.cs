using System;
using System.Threading;
using System.Threading.Tasks;

namespace RFiDGear.Contracts
{
    /// <summary>
    /// Drives task execution from an extension or test harness.
    /// </summary>
    public interface ITaskExecHost
    {
        /// <summary><c>true</c> while a task run is in progress.</summary>
        bool IsRunning { get; }

        /// <summary>
        /// Executes all tasks in the current project once and returns a summary.
        /// Follows the same one-attempt rule as the normal UI execution path.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Run summary including outcome and task counts.</returns>
        Task<ExecResult> ExecuteAllAsync(CancellationToken ct = default);

        /// <summary>Resets all task completion states to their initial values.</summary>
        void ResetStatus();

        /// <summary>Raised when a task run completes (success or failure).</summary>
        event EventHandler<ExecResult> ExecutionCompleted;
    }
}
