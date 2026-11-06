#nullable enable
using System.Threading;

namespace RFiDGear.Infrastructure.ExtensionHost
{
    /// <summary>
    /// Shared admission gate for <see cref="ProjectHostImpl"/> and <see cref="TaskExecHostImpl"/>.
    /// Guarantees that at most one load OR one execution is in progress at any time,
    /// eliminating the TOCTOU race between checking <c>IsRunning</c> and replacing the task collection.
    /// </summary>
    public sealed class ExtensionOperationCoordinator
    {
        // Possible states of the coordinator.
        private const int Idle = 0;
        private const int Loading = 1;
        private const int Executing = 2;

        private int _state;

        /// <summary>
        /// Attempts to begin a project load. Returns <c>true</c> if the load was admitted;
        /// returns <c>false</c> if another load or an execution is already in progress.
        /// Callers must pair each <c>true</c> return with a call to <see cref="EndLoad"/>.
        /// </summary>
        public bool TryBeginLoad() =>
            Interlocked.CompareExchange(ref _state, Loading, Idle) == Idle;

        /// <summary>
        /// Releases the load slot. Must be called in a <c>finally</c> block after <see cref="TryBeginLoad"/> returns <c>true</c>.
        /// </summary>
        public void EndLoad() =>
            Interlocked.Exchange(ref _state, Idle);

        /// <summary>
        /// Attempts to begin a task execution. Returns <c>true</c> if execution was admitted;
        /// returns <c>false</c> if a load or another execution is already in progress.
        /// Callers must pair each <c>true</c> return with a call to <see cref="EndExecution"/>.
        /// </summary>
        public bool TryBeginExecution() =>
            Interlocked.CompareExchange(ref _state, Executing, Idle) == Idle;

        /// <summary>
        /// Releases the execution slot. Must be called in a <c>finally</c> block after <see cref="TryBeginExecution"/> returns <c>true</c>.
        /// </summary>
        public void EndExecution() =>
            Interlocked.Exchange(ref _state, Idle);

        /// <summary><c>true</c> when a task execution is in progress.</summary>
        public bool IsExecuting => Volatile.Read(ref _state) == Executing;

        /// <summary><c>true</c> when a project load is in progress.</summary>
        public bool IsLoading => Volatile.Read(ref _state) == Loading;
    }
}
