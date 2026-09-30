using System;

namespace RFiDGear.Contracts
{
    /// <summary>
    /// Controls the continuous automatic card-processing mode (see also issue #192).
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
