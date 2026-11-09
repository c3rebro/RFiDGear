using System;
using System.Threading;
using System.Threading.Tasks;

namespace RFiDGear.Contracts
{
    /// <summary>
    /// The active reader and the card on it: a view of what the host last saw, and an on-demand scan of the reader.
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
        /// It does not read the reader: it is what the last scan (<see cref="ScanAsync"/>) found. Reads that RFiDGear
        /// makes itself (a task run, a quick check, a setup dialog, the Read button or the automatic mode of the main
        /// window) are not included. Until the first scan that reads the reader, and after the reader the user selected
        /// has changed, it is the host's guess from the state of the reader and can be out of date. Use
        /// <see cref="ScanAsync"/> when you need to know what is on the reader now. It is never <c>null</c>: with no
        /// reader its state is <see cref="ReaderScanState.NoReader"/>. Its state is always
        /// <see cref="ReaderScanState.Ready"/> or <see cref="ReaderScanState.NoReader"/>: the answer of a scan becomes the
        /// current snapshot only in those two states, never when the scan was busy or failed, and not when the reader
        /// changed while the scan was running (the scan still returns what it read).
        /// </para>
        /// <para>
        /// <see cref="CurrentUID"/> and <see cref="ChipTypeName"/> show the chip the application keeps in memory for its
        /// last read of any kind; a scan replaces it (it is empty while a scan reads), and <see cref="IsConnected"/> asks
        /// the reader. They are not part of this picture and can differ from it.
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
        /// While the host is running tasks (<see cref="ITaskExecHost.IsRunning"/>), loading a project, editing one or
        /// scanning already, the reader is not read and the answer has the state <see cref="ReaderScanState.Busy"/>: a
        /// read could end the card's session in the middle of an operation. Ask again later. While a scan runs, a load,
        /// a run or an edit started by someone else is refused as busy in turn. (A read that the user starts in RFiDGear's
        /// main window at the same moment is not detected yet.)
        /// </para>
        /// <para>
        /// A scan changes no data on the card, but it activates the card again, which ends a card session that was
        /// authenticated; that is why a scan is refused while tasks run. The list shows the cards RFiDGear can read: an
        /// empty list means no such card. A card of another kind (for example an HF card that is not a MIFARE card) is
        /// not listed. With a PC/SC reader a card that cannot be read is not told from no card, and neither is a reader
        /// that is unplugged: all of them answer with the state <see cref="ReaderScanState.Ready"/> and an empty list
        /// (<see cref="ReaderScanState.NoReader"/> is then only answered when RFiDGear has no reader at all).
        /// </para>
        /// <para>
        /// When the scan finds a different picture than the one before, <see cref="ReaderStateChanged"/> is raised after
        /// the scan has ended, so a handler may scan again. If the reader the user selected changes while a scan is
        /// running, the scan still returns what it read, but that answer is not stored and raises no event; read
        /// <see cref="CurrentSnapshot"/> or scan again.
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
        /// Raised when the reader changes or when the card on it does. Exactly these two cases:
        /// <list type="bullet">
        /// <item>
        /// <see cref="ReaderName"/> changes: the user selects a reader in the reader setup, or the periodic reader check
        /// (every few seconds) connects or loses a reader. What the old reader showed says nothing about the new one, so
        /// the picture of <see cref="CurrentSnapshot"/> starts again.
        /// </item>
        /// <item>
        /// A scan (<see cref="ScanAsync"/>) finds a different picture than the one before: a card arrived, was removed or
        /// was replaced, or a reader was lost or came back. The first scan that reads the reader (answers
        /// <see cref="ReaderScanState.Ready"/> or <see cref="ReaderScanState.NoReader"/>) after the host started, and the
        /// first after the reader changed, always raise it, whatever they find, because nothing was known before. A repeat
        /// of the same picture raises nothing, and neither does a scan that was busy, failed or running while the reader
        /// changed.
        /// </item>
        /// </list>
        /// A change of the card is only ever found by a scan: the host does not poll the reader on behalf of extensions.
        /// Read <see cref="CurrentSnapshot"/> when the event is raised. <see cref="CurrentUID"/>, <see cref="ChipTypeName"/> and
        /// <see cref="IsConnected"/> describe the current reader when the event is raised.
        /// <para>
        /// Threading: raised synchronously on the thread that made the change and never marshalled. That is the UI thread
        /// for the user's selection, a thread-pool thread for the periodic check, and for a scan the thread on which the
        /// scan finishes (a thread-pool thread); a subscriber that touches UI must marshal to its own dispatcher. The scan
        /// has ended when the event is raised, so a handler may call <see cref="ScanAsync"/> or start a run.
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
