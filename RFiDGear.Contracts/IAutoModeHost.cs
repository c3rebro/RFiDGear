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
    /// </summary>
    public interface IAutoModeHost
    {
        /// <summary><c>true</c> when continuous automatic mode is active.</summary>
        bool IsAutoModeEnabled { get; }

        /// <summary>Activates continuous automatic mode (equivalent to the UI toggle).</summary>
        void Start();

        /// <summary>Deactivates continuous automatic mode cleanly without interrupting an in-progress task.</summary>
        void Stop();

        /// <summary>Raised when <see cref="IsAutoModeEnabled"/> changes.</summary>
        event EventHandler AutoModeStateChanged;
    }
}
