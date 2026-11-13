namespace RFiDGear.Bridge
{
    /// <summary>
    /// The notifications the host sends. A client implements this on the object it registers as its local target. Only the
    /// events a client asked for in <see cref="HelloRequest.Events"/> are sent, in the order the host sent them
    /// (<see cref="BridgeEvent.Seq"/>). A handler must not wait for a call to the host: the host's own notifications are
    /// delivered one after the other.
    /// </summary>
    public interface IBridgeClientEvents
    {
        /// <summary>A run started over the bridge was admitted.</summary>
        /// <param name="e">The event.</param>
        [Capability(BridgeCapabilities.EventsExecution)]
        void RunStarted(RunStartedEvent e);

        /// <summary>A run started over the bridge ended. Once per run.</summary>
        /// <param name="e">The event with the result.</param>
        [Capability(BridgeCapabilities.EventsExecution)]
        void RunCompleted(RunCompletedEvent e);

        /// <summary>A task run, from anywhere, started or ended.</summary>
        /// <param name="e">The event.</param>
        [Capability(BridgeCapabilities.EventsRunning)]
        void RunningChanged(RunningChangedEvent e);

        /// <summary>The reader or the card on it changed.</summary>
        /// <param name="e">The event.</param>
        [Capability(BridgeCapabilities.EventsReader)]
        void ReaderChanged(ReaderChangedEvent e);

        /// <summary>The loaded project changed.</summary>
        /// <param name="e">The event.</param>
        [Capability(BridgeCapabilities.EventsProject)]
        void ProjectChanged(ProjectChangedEvent e);

        /// <summary>RFiDGear is closing. Best effort: the connection ending without it proves nothing.</summary>
        /// <param name="e">The event.</param>
        [Capability(BridgeCapabilities.EventsLifecycle)]
        void ShuttingDown(ShuttingDownEvent e);

        /// <summary>Automatic mode was switched on or off. Never merged, and in the order in which things happened.</summary>
        /// <param name="e">The event.</param>
        [Capability(BridgeCapabilities.EventsAutoMode)]
        void AutoModeChanged(AutoModeChangedEvent e);

        /// <summary>Automatic mode processed a card. Once per run of the mode.</summary>
        /// <param name="e">The event with the result.</param>
        [Capability(BridgeCapabilities.EventsAutoMode)]
        void AutoRunCompleted(AutoRunCompletedEvent e);
    }
}
