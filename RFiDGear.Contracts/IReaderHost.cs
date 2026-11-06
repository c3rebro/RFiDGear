using System;

namespace RFiDGear.Contracts
{
    /// <summary>
    /// Read-only view of the active reader and card state.
    /// </summary>
    public interface IReaderHost
    {
        /// <summary>
        /// The reader's name as the main window shows it: a display string, not an identifier. Until a reader has
        /// connected it is the provider's or the selected PC/SC reader's name (<c>None</c> when no reader is selected,
        /// <c>N/A</c> when the periodic check finds none); once a reader has connected it is the reader's own name with
        /// its firmware version. It changes while the application runs, and <see cref="ReaderStateChanged"/> is raised
        /// when it does. It is not <c>null</c> by the time an extension can run.
        /// </summary>
        string? ReaderName { get; }

        /// <summary>UID of the card currently on the reader as a hex string, or <c>null</c> if absent.</summary>
        string? CurrentUID { get; }

        /// <summary>Card type name as reported by the reader SDK, or <c>null</c> if no card is present.</summary>
        string? ChipTypeName { get; }

        /// <summary><c>true</c> when a reader is connected and ready.</summary>
        bool IsConnected { get; }

        /// <summary>
        /// Raised when <see cref="ReaderName"/> changes: when the user selects a reader in the reader setup, and when
        /// the periodic reader check (every few seconds) connects or loses a reader. A card appearing or leaving is not
        /// announced yet in this release, so read <see cref="CurrentUID"/>, <see cref="ChipTypeName"/> and
        /// <see cref="IsConnected"/> when you need them; they describe the current reader when the event is raised.
        /// <para>
        /// Threading: raised synchronously on the thread that changes the name and never marshalled. That is the UI
        /// thread for the user's selection but a thread-pool thread for the periodic check, so a subscriber that
        /// touches UI must marshal to its own dispatcher.
        /// </para>
        /// <para>
        /// Failures: an exception thrown by an ordinary (non-async) subscriber is logged (type name, redacted message
        /// and stack trace) and does not reach the other subscribers or the application. The host cannot catch
        /// exceptions from an <c>async void</c> handler; see <see cref="ITaskExecHost.ExecutionCompleted"/> for what
        /// happens to those.
        /// </para>
        /// </summary>
        event EventHandler ReaderStateChanged;
    }
}
