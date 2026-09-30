using System;

namespace RFiDGear.Contracts
{
    /// <summary>
    /// Read-only view of the active reader and card state.
    /// </summary>
    public interface IReaderHost
    {
        /// <summary>Display name of the configured reader, or <c>null</c> if not initialized.</summary>
        string? ReaderName { get; }

        /// <summary>UID of the card currently on the reader as a hex string, or <c>null</c> if absent.</summary>
        string? CurrentUID { get; }

        /// <summary>Card type name as reported by the reader SDK, or <c>null</c> if no card is present.</summary>
        string? ChipTypeName { get; }

        /// <summary><c>true</c> when a reader is connected and ready.</summary>
        bool IsConnected { get; }

        /// <summary>Raised when connection state, UID, or chip type changes.</summary>
        event EventHandler ReaderStateChanged;
    }
}
