using System;

namespace RFiDGear.Contracts
{
    /// <summary>
    /// Controls the continuous automatic card-processing mode (see also issue #192).
    /// <para>
    /// <strong>Known limitation:</strong> in this release the host exposes a stub implementation, including in
    /// the main application. <see cref="IsAutoModeEnabled"/> is always <c>false</c>, <see cref="Stop"/> does nothing,
    /// and <see cref="Start"/> throws <see cref="NotSupportedException"/>. Full auto-mode wiring is tracked in
    /// issue #192 and is out of scope until then; extensions must not depend on it.
    /// </para>
    /// <para>
    /// Threading, busy behavior and events of the stub: every member is safe to call from any thread because
    /// none of them does any work. <see cref="Start"/> throws whatever the state of the host is (idle, running a
    /// task, or reader busy), <see cref="Stop"/> never throws and never waits, and
    /// <see cref="AutoModeStateChanged"/> is never raised, so no event ordering exists yet. The threading, busy
    /// behavior and event ordering of the real implementation will be documented when #192 lands; do not rely on
    /// any assumption about them.
    /// </para>
    /// </summary>
    public interface IAutoModeHost
    {
        /// <summary><c>true</c> when continuous automatic mode is active.</summary>
        bool IsAutoModeEnabled { get; }

        /// <summary>
        /// Activates continuous automatic mode (equivalent to the UI toggle).
        /// <para>In this release this is a stub: it always throws <see cref="NotSupportedException"/> (see the type remarks).</para>
        /// </summary>
        /// <exception cref="NotSupportedException">Always thrown by this release; automatic mode is tracked in issue #192.</exception>
        void Start();

        /// <summary>
        /// Deactivates continuous automatic mode cleanly without interrupting an in-progress task.
        /// <para>In this release this is a stub: it does nothing, never throws, and never waits (see the type remarks).</para>
        /// </summary>
        void Stop();

        /// <summary>
        /// Raised when <see cref="IsAutoModeEnabled"/> changes. In this release (stub) it is never raised.
        /// </summary>
        event EventHandler AutoModeStateChanged;
    }
}
