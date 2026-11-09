#nullable enable
using System;
using System.Linq;
using System.Threading;

using Serilog;

namespace RFiDGear.Infrastructure.ExtensionHost
{
    /// <summary>
    /// Shared admission gate for <see cref="ProjectHostImpl"/> and <see cref="TaskExecHostImpl"/>.
    /// Guarantees that at most one load, one execution, one project edit (New, ReplaceTaskCollection, or a
    /// task-status reset) or one reader scan is in progress at any time, eliminating the TOCTOU race between checking a flag and replacing the task collection.
    /// </summary>
    public sealed class ExtensionOperationCoordinator
    {
        // Possible states of the coordinator.
        private const int Idle = 0;
        private const int Loading = 1;
        private const int Executing = 2;
        private const int Mutating = 3;
        private const int Scanning = 4;

        private int _state;

        /// <summary>
        /// Raised each time the coordinator enters or leaves the executing state: once after a successful
        /// <see cref="TryBeginExecution"/>, and once when a release finds the state <c>Executing</c>. It is never
        /// raised for a refused attempt, for loads, for project edits, or for reader scans. The state has already changed when it is
        /// raised, so <see cref="IsExecuting"/> reports the new value. Handlers run synchronously on the thread that
        /// made the transition; an exception from one is logged and does not reach the caller, because it would
        /// otherwise leave the gate held.
        /// </summary>
        internal event EventHandler? IsExecutingChanged;

        /// <summary>
        /// Attempts to begin a project load. Returns <c>true</c> if the load was admitted;
        /// returns <c>false</c> if another load, an execution, a project edit, or a reader scan is already in progress.
        /// Callers must pair each <c>true</c> return with a call to <see cref="EndLoad"/>.
        /// </summary>
        public bool TryBeginLoad() =>
            Interlocked.CompareExchange(ref _state, Loading, Idle) == Idle;

        /// <summary>
        /// Releases the load slot. Must be called in a <c>finally</c> block after <see cref="TryBeginLoad"/> returns <c>true</c>.
        /// </summary>
        public void EndLoad() =>
            ReleaseToIdle();

        /// <summary>
        /// Attempts to begin a task execution. Returns <c>true</c> if execution was admitted;
        /// returns <c>false</c> if a load, a project edit, a reader scan, or another execution is already in progress.
        /// Callers must pair each <c>true</c> return with a call to <see cref="EndExecution"/>.
        /// </summary>
        public bool TryBeginExecution()
        {
            if (Interlocked.CompareExchange(ref _state, Executing, Idle) != Idle)
            {
                return false;
            }

            RaiseIsExecutingChanged();
            return true;
        }

        /// <summary>
        /// Releases the execution slot. Must be called in a <c>finally</c> block after <see cref="TryBeginExecution"/> returns <c>true</c>.
        /// </summary>
        public void EndExecution() =>
            ReleaseToIdle();

        /// <summary>
        /// Attempts to begin a project edit: one that replaces the task collection (New, ReplaceTaskCollection) or one
        /// that clears the state of every task (a task-status reset).
        /// Returns <c>true</c> if the edit was admitted; returns <c>false</c> if a load, an execution, a reader scan, or
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
            ReleaseToIdle();

        /// <summary>
        /// Attempts to begin a reader scan (<c>IReaderHost.ScanAsync</c>). Returns <c>true</c> if the scan was admitted;
        /// returns <c>false</c> if a load, an execution, a project edit or another scan is in progress. While a scan is
        /// admitted, none of those can start: a scan re-activates the card, which would end a card session in the
        /// middle of an operation. Callers must pair each <c>true</c> return with a call to <see cref="EndScan"/>.
        /// </summary>
        public bool TryBeginScan() =>
            Interlocked.CompareExchange(ref _state, Scanning, Idle) == Idle;

        /// <summary>
        /// Releases the scan slot. Must be called in a <c>finally</c> block after <see cref="TryBeginScan"/> returns <c>true</c>.
        /// </summary>
        public void EndScan() =>
            ReleaseToIdle();

        /// <summary><c>true</c> when a task execution is in progress.</summary>
        public bool IsExecuting => Volatile.Read(ref _state) == Executing;

        /// <summary><c>true</c> when a project load is in progress.</summary>
        public bool IsLoading => Volatile.Read(ref _state) == Loading;

        /// <summary><c>true</c> when a project edit (New, ReplaceTaskCollection, or a task-status reset) is in progress.</summary>
        public bool IsMutating => Volatile.Read(ref _state) == Mutating;

        /// <summary><c>true</c> when a reader scan is in progress.</summary>
        public bool IsScanning => Volatile.Read(ref _state) == Scanning;

        /// <summary>
        /// Unconditionally returns to idle, as the <c>End*</c> methods always have, and announces the change
        /// only when the state it replaced was <c>Executing</c>. Releasing while idle, loading, mutating or scanning
        /// changes nothing a subscriber can observe.
        /// </summary>
        private void ReleaseToIdle()
        {
            var previous = Interlocked.Exchange(ref _state, Idle);
            if (previous == Executing)
            {
                RaiseIsExecutingChanged();
            }
        }

        /// <summary>
        /// Raises <see cref="IsExecutingChanged"/> to each handler in isolation. This runs inside admission and
        /// release, so an exception that escaped here would either leave the gate held or skip the release.
        /// </summary>
        private void RaiseIsExecutingChanged()
        {
            var handlers = IsExecutingChanged;
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
                    Log.ForContext<ExtensionOperationCoordinator>().Error(
                        "An IsExecutingChanged handler threw; the exception was not propagated. {ExceptionType}: {ExceptionMessage} {ExceptionStackTrace}",
                        error.TypeName, error.Message, error.StackTrace);
                }
            }
        }
    }
}
