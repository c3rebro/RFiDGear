namespace RFiDGear.Contracts
{
    /// <summary>
    /// What a look at the reader found: the card on it, no card, or why nothing could be said about the card. More states
    /// may be added in later releases; treat one you do not know like <see cref="Busy"/>: nothing can be said about the
    /// card.
    /// </summary>
    public enum ReaderScanState
    {
        /// <summary>
        /// No reader is connected, so there is nothing to read. <see cref="ReaderSnapshot.Credentials"/> is empty.
        /// </summary>
        NoReader = 0,

        /// <summary>
        /// The reader is connected and was read. <see cref="ReaderSnapshot.Credentials"/> lists what is on it; an empty
        /// list means there is no card on the reader. (A PC/SC reader cannot tell a card it cannot read from no card, so
        /// with one an empty list can also mean that; see <see cref="IReaderHost.ScanAsync"/>.)
        /// </summary>
        Ready = 1,

        /// <summary>
        /// The reader is connected but reading it failed, for example because it did not answer. Nothing can be said
        /// about the card: <see cref="ReaderSnapshot.Credentials"/> is empty, and that must not be taken as "no card".
        /// </summary>
        ReadFailed = 2,

        /// <summary>
        /// The reader was not read because the host is busy with a task, a project or another scan. Nothing can be said
        /// about the card: <see cref="ReaderSnapshot.Credentials"/> is empty, and that must not be taken as "no card".
        /// </summary>
        Busy = 3,
    }
}
