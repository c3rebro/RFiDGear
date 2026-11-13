#nullable enable
using System.Threading;
using System.Threading.Tasks;

using RFiDGear.Contracts;

namespace RFiDGear.Infrastructure.ExtensionHost
{
    /// <summary>
    /// An execution host that can start a run under an id the caller chose. The external control bridge needs the id before
    /// the run starts, so that it can tell the client at once which run it is and recognise the run again after a
    /// reconnection; the public <see cref="ITaskExecHost"/> mints its own and stays as it is (its snapshot is frozen).
    /// </summary>
    internal interface IPreassignedRunExecution
    {
        /// <summary>
        /// Starts a run under <paramref name="runId"/>. Admission works as for <see cref="ITaskExecHost.ExecuteAllAsync(ExecutionRequest, CancellationToken)"/>:
        /// it happens in the synchronous part of the call, so a refusal is a task that has already faulted with
        /// <see cref="System.InvalidOperationException"/> (busy) or <see cref="System.ArgumentException"/> (a report variable
        /// that cannot produce its report), and a task that is still pending was admitted.
        /// </summary>
        /// <param name="request">The variables of the run.</param>
        /// <param name="runId">The id the run has in its result and in the log.</param>
        /// <param name="ct">Cancels the run at a task boundary.</param>
        /// <returns>The result of the run.</returns>
        Task<ExecResult> ExecuteAllAsync(ExecutionRequest request, string runId, CancellationToken ct);
    }
}
