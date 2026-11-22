namespace RFiDGear.Services.Licensing
{
    /// <summary>
    /// What the license gate does to the program, so that the gate itself needs no window: the main window gives it these
    /// four things.
    /// </summary>
    public interface ILicenseEnforcement
    {
        /// <summary>Gets a value indicating whether a run is in progress. The gate never interrupts one.</summary>
        bool IsRunInProgress { get; }

        /// <summary>
        /// Refuses (or admits again) new runs: those of the window, of automatic mode, of extensions and of the bridge all start
        /// through the same admission.
        /// </summary>
        /// <param name="blocked"><see langword="true"/> to refuse new runs.</param>
        void SetNewRunsBlocked(bool blocked);

        /// <summary>Stops automatic mode, which would otherwise be offered a card and refused again and again.</summary>
        void StopAutomaticMode();

        /// <summary>
        /// Tells the person that the trial has ended and lets them buy or close the program. It is called once per end of a
        /// trial, and only when no run is in progress.
        /// </summary>
        void ShowTrialEnded();
    }
}
