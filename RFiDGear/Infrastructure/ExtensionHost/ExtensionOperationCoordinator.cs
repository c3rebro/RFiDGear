#nullable enable
using System.Threading;

namespace RFiDGear.Infrastructure.ExtensionHost
{
    /// <summary>
    /// Shared admission gate for <see cref="ProjectHostImpl"/> and <see cref="TaskExecHostImpl"/>.
    /// Guarantees that at most one load, one execution, or one project edit (New, ReplaceTaskCollection, or a
    /// task-status reset) is in progress at any time, eliminating the TOCTOU race between checking a flag and replacing the task collection.
    /// </summary>
    public sealed class ExtensionOperationCoordinator
    {
        // Possible states of the coordinator.
        private const int Idle = 0;
        private const int Loading = 1;
        private const int Executing = 2;
        private const int Mutating = 3;

        private int _state;

        /// <summary>
        /// Attempts to begin a project load. Returns <c>true</c> if the load was admitted;
        /// returns <c>false</c> if another load, an execution, or a project edit is already in progress.
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
        /// returns <c>false</c> if a load, a project edit, or another execution is already in progress.
        /// Callers must pair each <c>true</c> return with a call to <see cref="EndExecution"/>.
        /// </summary>
        public bool TryBeginExecution() =>
            Interlocked.CompareExchange(ref _state, Executing, Idle) == Idle;

        /// <summary>
        /// Releases the execution slot. Must be called in a <c>finally</c> block after <see cref="TryBeginExecution"/> returns <c>true</c>.
        /// </summary>
        public void EndExecution() =>
            Interlocked.Exchange(ref _state, Idle);

        /// <summary>
        /// Attempts to begin a project edit: one that replaces the task collection (New, ReplaceTaskCollection) or one
        /// that clears the state of every task (a task-status reset).
        /// Returns <c>true</c> if the edit was admitted; returns <c>false</c> if a load, an execution, or
        /// another edit is already in progress, so the collection can never change under a running task loop.
        /// Callers must pair each <c>true</c> return with a call to <see cref="EndProjectOperation"/>.
        /// </summary>
        public bool TryBeginProjectOperation() =>
            Interlocked.CompareExchange(ref _state, Mutating, Idle) == Idle;

        /// <summary>
        /// Releases the project-edit slot. Must be called in a <c>finally</c> block after
        /// <see cref="TryBeginProjectOperation"/> returns <c>true</c>.
        /// </summary>
        public void EndProjectOperation() =>
            Interlocked.Exchange(ref _state, Idle);

        /// <summary><c>true</c> when a task execution is in progress.</summary>
        public bool IsExecuting => Volatile.Read(ref _state) == Executing;

        /// <summary><c>true</c> when a project load is in progress.</summary>
        public bool IsLoading => Volatile.Read(ref _state) == Loading;

        /// <summary><c>true</c> when a project edit (New, ReplaceTaskCollection, or a task-status reset) is in progress.</summary>
        public bool IsMutating => Volatile.Read(ref _state) == Mutating;
    }
}
