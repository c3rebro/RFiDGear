using System;
using System.Threading.Tasks;

namespace RFiDGear.Infrastructure.AccessControl
{
    /// <summary>
    /// Builds DESFire key-setting bytes while respecting card scope (PICC vs. application).
    /// The upper nibble is cleared for PICC operations because the change-key mode is
    /// ignored for the master application and must be zeroed according to NXP guidance.
    /// </summary>
    public static class DesfireKeySettingsComposer
    {
        /// <summary>
        /// Produces the settings byte for a DESFire operation.
        /// </summary>
        /// <param name="settings">The flag combination selected in the UI.</param>
        /// <param name="applyToPicc">Whether the settings target the PICC (application id 0).</param>
        /// <returns>A single byte with the correct nibble semantics for the target scope.</returns>
        public static byte BuildSettingsByte(DESFireKeySettings settings, bool applyToPicc)
        {
            AccessConditionValidation.EnsureValid(settings);

            var settingsByte = (byte)settings;
            return applyToPicc ? (byte)(settingsByte & 0x0F) : settingsByte;
        }
    }

    /// <summary>
    /// Minimal contract used by the orchestrator to issue DESFire application key commands.
    /// This wrapper allows test doubles to intercept parameters without requiring hardware.
    /// </summary>
    public interface IMifareDesfireProvider
    {
        /// <summary>
        /// Authenticates to the target application using the supplied key slot.
        /// </summary>
        Task<ERROR> AuthenticateAsync(string applicationMasterKey, DESFireKeyType keyType, int keyNumber, int appId);

        /// <summary>
        /// Changes a DESFire application key.
        /// </summary>
        Task<ERROR> ChangeMifareDesfireApplicationKey(string applicationMasterKeyCurrent, int keyNumberCurrent, DESFireKeyType keyTypeCurrent,
            string applicationMasterKeyTarget, int selectedDesfireAppKeyVersionTargetAsIntint,
            DESFireKeyType keyTypeTarget, int appIdCurrent, int appIdTarget, DESFireKeySettings keySettings, int keyVersion);

        /// <summary>
        /// Updates the key settings associated with a DESFire application.
        /// </summary>
        Task<ERROR> ChangeMifareDesfireApplicationKeySettings(string applicationMasterKeyCurrent, int keyNumberCurrent, DESFireKeyType keyTypeCurrent,
            string applicationMasterKeyTarget, int selectedDesfireAppKeyVersionTargetAsIntint,
            DESFireKeyType keyTypeTarget, int appIdCurrent, int appIdTarget, DESFireKeySettings keySettings, int keyVersion);
    }

    /// <summary>
    /// Encapsulates the sequencing for DESFire application key updates to keep invocation
    /// details out of the UI layer and enable parameter validation in isolation.
    /// </summary>
    public class DesfireKeyChangeOrchestrator
    {
        private readonly IMifareDesfireProvider provider;

        /// <summary>
        /// Initializes a new instance of the orchestrator.
        /// </summary>
        /// <param name="provider">Provider abstraction used to issue DESFire commands.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="provider"/> is <c>null</c>.</exception>
        public DesfireKeyChangeOrchestrator(IMifareDesfireProvider provider)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        /// <summary>
        /// Authenticates to the specified application and updates a key value.
        /// </summary>
        public async Task<ERROR> ChangeApplicationKeyAsync(
            string currentKey,
            int currentKeyNumber,
            DESFireKeyType currentKeyType,
            string targetKey,
            int targetKeyVersion,
            DESFireKeyType targetKeyType,
            int appIdCurrent,
            int appIdTarget,
            DESFireKeySettings selectedSettings,
            int keyVersion)
        {
            var settingsByte = DesfireKeySettingsComposer.BuildSettingsByte(selectedSettings, appIdCurrent == 0);
            var normalizedSettings = (DESFireKeySettings)settingsByte;

            var authResult = await provider.AuthenticateAsync(currentKey, currentKeyType, currentKeyNumber, appIdCurrent).ConfigureAwait(false);
            if (authResult != ERROR.NoError)
            {
                return authResult;
            }

            return await provider.ChangeMifareDesfireApplicationKey(
                currentKey,
                currentKeyNumber,
                currentKeyType,
                targetKey,
                targetKeyVersion,
                targetKeyType,
                appIdCurrent,
                appIdTarget,
                normalizedSettings,
                keyVersion).ConfigureAwait(false);
        }

        /// <summary>
        /// Authenticates to the specified application and updates its key settings.
        /// </summary>
        public async Task<ERROR> ChangeApplicationKeySettingsAsync(
            string currentKey,
            int currentKeyNumber,
            DESFireKeyType currentKeyType,
            string targetKey,
            int targetKeyVersion,
            DESFireKeyType targetKeyType,
            int appIdCurrent,
            int appIdTarget,
            DESFireKeySettings selectedSettings,
            int keyVersion)
        {
            var settingsByte = DesfireKeySettingsComposer.BuildSettingsByte(selectedSettings, appIdCurrent == 0);
            var normalizedSettings = (DESFireKeySettings)settingsByte;

            var authResult = await provider.AuthenticateAsync(currentKey, currentKeyType, currentKeyNumber, appIdCurrent).ConfigureAwait(false);
            if (authResult != ERROR.NoError)
            {
                return authResult;
            }

            return await provider.ChangeMifareDesfireApplicationKeySettings(
                currentKey,
                currentKeyNumber,
                currentKeyType,
                targetKey,
                targetKeyVersion,
                targetKeyType,
                appIdCurrent,
                appIdTarget,
                normalizedSettings,
                keyVersion).ConfigureAwait(false);
        }
    }
}
