namespace RFiDGear.Contracts
{
    /// <summary>
    /// Why continuous automatic mode last ended (see <see cref="IAutoModeHost.LastStopReason"/>). More reasons may be
    /// added in later releases; treat one you do not know like <see cref="RunAborted"/>: the mode stopped because the
    /// state of the card or of the reader was unclear.
    /// </summary>
    public enum AutoModeStopReason
    {
        /// <summary>
        /// The mode has not been stopped since the application started, or it is on again since its last stop. Nothing
        /// ended it that could be reported.
        /// </summary>
        None = 0,

        /// <summary>The user or the host asked for it: <see cref="IAutoModeHost.Stop"/> or the menu toggle.</summary>
        StopRequested = 1,

        /// <summary>
        /// A reader that had answered did not answer three looks in a row. The card on it, if any, is unknown. Nothing was
        /// processed because of the failed looks.
        /// </summary>
        ReaderLost = 2,

        /// <summary>
        /// The reader, its provider or its port was changed in the settings. The mode does not carry on with another
        /// reader unasked.
        /// </summary>
        ReaderChanged = 3,

        /// <summary>
        /// A run ended with a fault or a cancellation, or the automatic processing failed in a way that left the state
        /// of the card unknown. It does not matter who started the run: a run by hand, or one that an extension started
        /// with <c>ITaskExecHost.ExecuteAllAsync</c>, counts too while the mode is on. A run of the mode itself was
        /// reported through <see cref="IAutoModeHost.AutoModeRunCompleted"/> first (the other runs are not reported there,
        /// so do not wait for that event when the reason is <see cref="RunAborted"/>: an extension gets the result of its
        /// own run from <c>ExecuteAllAsync</c>). The card is not processed again unless it is taken away and put back.
        /// </summary>
        RunAborted = 4,

        /// <summary>
        /// A run timed out or the per-task watchdog gave it up (see <see cref="ExecutionFailureReason.TimedOut"/>): the
        /// state of the card is unknown. It does not matter who started the run: a run by hand, or one that an extension
        /// started with <c>ITaskExecHost.ExecuteAllAsync</c>, counts too while the mode is on. A run of the mode itself
        /// was reported through <see cref="IAutoModeHost.AutoModeRunCompleted"/> first; the other runs are not reported
        /// there, so do not wait for that event when the reason is <see cref="RunTimedOut"/> (an extension gets the result
        /// of its own run from <c>ExecuteAllAsync</c>). The card is not processed again unless it is taken away and put
        /// back.
        /// </summary>
        RunTimedOut = 5,
    }
}
