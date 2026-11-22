using System;

namespace RFiDGear.Services.Licensing
{
    /// <summary>
    /// The enforcement of the main window, made of four delegates that the window gives it. It exists so that the gate can be
    /// built (and tested) without a window.
    /// </summary>
    public sealed class DelegateLicenseEnforcement : ILicenseEnforcement
    {
        private readonly Func<bool> _isRunInProgress;
        private readonly Action<bool> _setNewRunsBlocked;
        private readonly Action _stopAutomaticMode;
        private readonly Action _showTrialEnded;

        /// <summary>
        /// Initializes a new instance of the <see cref="DelegateLicenseEnforcement"/> class.
        /// </summary>
        /// <param name="isRunInProgress">Tells whether a run is in progress.</param>
        /// <param name="setNewRunsBlocked">Refuses or admits new runs.</param>
        /// <param name="stopAutomaticMode">Stops automatic mode.</param>
        /// <param name="showTrialEnded">Shows the end of the trial to the person.</param>
        public DelegateLicenseEnforcement(
            Func<bool> isRunInProgress,
            Action<bool> setNewRunsBlocked,
            Action stopAutomaticMode,
            Action showTrialEnded)
        {
            _isRunInProgress = isRunInProgress ?? throw new ArgumentNullException(nameof(isRunInProgress));
            _setNewRunsBlocked = setNewRunsBlocked ?? throw new ArgumentNullException(nameof(setNewRunsBlocked));
            _stopAutomaticMode = stopAutomaticMode ?? throw new ArgumentNullException(nameof(stopAutomaticMode));
            _showTrialEnded = showTrialEnded ?? throw new ArgumentNullException(nameof(showTrialEnded));
        }

        /// <inheritdoc />
        public bool IsRunInProgress => _isRunInProgress();

        /// <inheritdoc />
        public void SetNewRunsBlocked(bool blocked) => _setNewRunsBlocked(blocked);

        /// <inheritdoc />
        public void StopAutomaticMode() => _stopAutomaticMode();

        /// <inheritdoc />
        public void ShowTrialEnded() => _showTrialEnded();
    }
}
