using System;

namespace RFiDGear.Contracts
{
    /// <summary>
    /// Read-only view of the active reader and card state.
    /// </summary>
    public interface IReaderHost
    {
        /// <summary>
        /// Display name of the reader the user has selected, or <c>null</c> if none. It follows the selection while
        /// the application runs; <see cref="ReaderStateChanged"/> is raised when it changes.
        /// </summary>
        string? ReaderName { get; }

        /// <summary>UID of the card currently on the reader as a hex string, or <c>null</c> if absent.</summary>
        string? CurrentUID { get; }

        /// <summary>Card type name as reported by the reader SDK, or <c>null</c> if no card is present.</summary>
        string? ChipTypeName { get; }

        /// <summary><c>true</c> when a reader is connected and ready.</summary>
        bool IsConnected { get; }

        /// <summary>
        /// Raised when the reader state changes. In this release the main application raises it when the user selects
        /// a different reader; a card appearing or leaving, or the connection changing, during a task run is not
        /// announced yet, so read <see cref="CurrentUID"/>, <see cref="ChipTypeName"/> and <see cref="IsConnected"/>
        /// when you need them. Raised synchronously on the thread that changes the selection (normally the UI
        /// thread). An exception from an ordinary subscriber is logged and does not reach the other subscribers or the
        /// application.
        /// </summary>
        event EventHandler ReaderStateChanged;
    }
}
