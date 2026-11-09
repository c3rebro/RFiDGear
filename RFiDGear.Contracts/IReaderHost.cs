using System;
using System.Threading;
using System.Threading.Tasks;

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
        /// The reader and the card on it as the host last saw them, as one consistent picture. Reading
        /// <see cref="CurrentUID"/>, <see cref="ChipTypeName"/> and <see cref="IsConnected"/> one after the other can mix
        /// two moments; this cannot, and it also lists a second credential (LF or LEGIC) read together with the card.
        /// <para>
        /// It does not read the reader: it is what the last read found. Use <see cref="ScanAsync"/> when you need to know
        /// what is on the reader now. It is never <c>null</c>: with no reader its state is
        /// <see cref="ReaderScanState.NoReader"/>. Its state is always <see cref="ReaderScanState.Ready"/> or
        /// <see cref="ReaderScanState.NoReader"/>: the answer of a scan becomes the current snapshot only in those two
        /// states, never when the scan was busy or failed.
        /// </para>
        /// </summary>
        ReaderSnapshot CurrentSnapshot { get; }

        /// <summary>
        /// Reads the reader once, now, and returns what it found. This is how an extension makes the host look at the
        /// reader; the host does not read it on its own for an extension. A read takes the reader's time, up to a few
        /// seconds, so await it and do not call it from a UI event handler that must return at once.
        /// <para>
        /// Failures are answers, not exceptions: a reader that cannot be read gives the state
        /// <see cref="ReaderScanState.ReadFailed"/>, and no reader gives <see cref="ReaderScanState.NoReader"/>.
        /// While the host is running tasks (<see cref="ITaskExecHost.IsRunning"/>), loading a project or already
        /// scanning, the reader is not read and the answer has the state <see cref="ReaderScanState.Busy"/>: a read could
        /// end the card's session in the middle of an operation. Ask again later.
        /// </para>
        /// <para>
        /// A scan changes nothing on the card. With a PC/SC reader a card that cannot be read is not told from no card:
        /// both answer with state <see cref="ReaderScanState.Ready"/> and an empty list.
        /// </para>
        /// <para>
        /// Cancellation: if <paramref name="ct"/> is cancelled before the read has started, the task is cancelled. A read
        /// that has started is not interrupted, because a half-finished exchange can leave the reader in an undefined
        /// state; its result is returned.
        /// </para>
        /// </summary>
        /// <param name="ct">Cancels the scan if it has not started reading yet.</param>
        /// <returns>What the reader found; see <see cref="ReaderSnapshot.State"/> for why a list may be empty.</returns>
        Task<ReaderSnapshot> ScanAsync(CancellationToken ct = default);

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
