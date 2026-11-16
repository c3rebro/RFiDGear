#nullable enable
using System;
using System.Globalization;
using System.Threading.Tasks;

using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.ReaderProviders;
using RFiDGear.Services.Sam;
using RFiDGear.Services.SecureKeys;

namespace RFiDGear.ViewModel.TaskSetupViewModels
{
    /// <summary>
    /// The commands of the task that can use keys that stay in a MIFARE SAM AV3: the authentication of a PICC key or an application key, and
    /// the change of the PICC master key or an application key. Every other command refuses a slot that names a key of the SAM before it touches
    /// the card (see <see cref="KeyAcquisition"/>). The keys never reach this class: it holds the references (<c>entry:version</c>), hands them to
    /// the reader, and the SAM and the card do the rest. What the typed version of a new key says is not used: the card gets the version of the
    /// key entry of the SAM.
    /// </summary>
    public partial class MifareDesfireSetupViewModel
    {
        // The SAM is allowed for a reader that can relay between the SAM and a card (the PC/SC reader); any other reader refuses it, and the
        // task ends with KeyUnavailable before anything is sent.
        private static SamKeyPolicy SamPolicyOf(ReaderDevice device) =>
            device.SupportsDesfireSamKeys ? SamKeyPolicy.AllOrNothing : SamKeyPolicy.Refuse;

        // What is wrong with a key of the SAM, in words; empty when the failure is none of that.
        private static string SamKeyProblemLine(KeyFailure? failure)
        {
            switch (failure?.Cause)
            {
                case KeyAcquisition.SamNotSupported:
                    return LocalizedStatusLine("textBoxStatusSamNotSupported", DateTime.Now);

                case KeyAcquisition.SamMixedKeys:
                    return LocalizedStatusLine("textBoxStatusSamMixedKeys", DateTime.Now);

                default:
                    return string.Empty;
            }
        }

        // Authenticates the PICC master key (application 0, key 0) with a key of the SAM.
        private async Task AuthenticatePiccWithSamAsync(ReaderDevice device, AcquiredKeys keys)
        {
            var result = await device.AuthenticateMifareDesfireWithSamAsync(
                new DesfireSamAccess(0, 0, SelectedDesfireMasterKeyEncryptionTypeCurrent, keys.SamKey(SecureKeySlots.DesfirePiccCurrent)));

            await ReportSamResultAsync(result, "textBoxStatusSamAuthenticated", 0, 0);
        }

        // Authenticates an application with a key of the SAM, the key number that the change-key policy of the application says.
        private async Task AuthenticateApplicationWithSamAsync(ReaderDevice device, AcquiredKeys keys)
        {
            if (IsValidAppNumberCurrent == false)
            {
                CurrentTaskErrorLevel = ERROR.ProtocolConstraint;

                return;
            }

            var authKeyNo = GetAuthKeyNumberForChangeAppKey(AppNumberCurrentAsInt, GetChangeKeyModeForApplication(AppNumberCurrentAsInt), selectedDesfireAppKeyNumberCurrentAsInt);
            var result = await device.AuthenticateMifareDesfireWithSamAsync(
                new DesfireSamAccess((uint)AppNumberCurrentAsInt, (byte)authKeyNo, SelectedDesfireAppKeyEncryptionTypeCurrent, keys.SamKey(SecureKeySlots.DesfireAppCurrent)));

            await ReportSamResultAsync(result, "textBoxStatusSamAuthenticated", AppNumberCurrentAsInt, authKeyNo);
        }

        // Changes the PICC master key: the key that authenticates is the key that is replaced, and the new key is the second key of the SAM.
        private async Task ChangePiccMasterKeyWithSamAsync(ReaderDevice device, AcquiredKeys keys)
        {
            var current = keys.SamKey(SecureKeySlots.DesfirePiccCurrent);
            var result = await device.ChangeMifareDesfireKeyWithSamAsync(
                new DesfireSamKeyChange(
                    0,
                    0,
                    SelectedDesfireMasterKeyEncryptionTypeCurrent,
                    current,
                    0,
                    SelectedDesfireMasterKeyEncryptionTypeTarget,
                    current,
                    keys.SamKey(SecureKeySlots.DesfirePiccNew)));

            await ReportSamResultAsync(result, "textBoxStatusSamKeyChanged", 0, 0);
        }

        // Changes an application key: the key that authenticates, the key that is replaced (the same unless the old-key inputs are shown) and the
        // new key are all keys of the SAM.
        private async Task ChangeApplicationKeyWithSamAsync(ReaderDevice device, AcquiredKeys keys, int authKeyNo, int keyNumberForChange, bool oldKeyIsOwnSlot)
        {
            var authKey = keys.SamKey(SecureKeySlots.DesfireAppCurrent);
            var result = await device.ChangeMifareDesfireKeyWithSamAsync(
                new DesfireSamKeyChange(
                    (uint)AppNumberCurrentAsInt,
                    (byte)authKeyNo,
                    SelectedDesfireAppKeyEncryptionTypeCurrent,
                    authKey,
                    (byte)keyNumberForChange,
                    SelectedDesfireAppKeyEncryptionTypeTarget,
                    oldKeyIsOwnSlot ? keys.SamKey(SecureKeySlots.DesfireAppOld) : authKey,
                    keys.SamKey(SecureKeySlots.DesfireAppNew)));

            await ReportSamResultAsync(result, "textBoxStatusSamKeyChanged", AppNumberCurrentAsInt, keyNumberForChange);
        }

        // The line for what the SAM and the card did, and the error level of the task. The lines hold the numbers of an application and a key, the
        // reason and the status words: never a key, a reference, a reader name, or the text of an exception.
        private async Task ReportSamResultAsync(SamPiccResult result, string successKey, int applicationId, int keyNumber)
        {
            CurrentTaskErrorLevel = result.Error;

            if (result.Succeeded)
            {
                StatusText += LocalizedStatusLine(successKey, DateTime.Now, applicationId, keyNumber);
            }
            else if (result.Failure == SamPiccFailure.NotConfigured)
            {
                StatusText += LocalizedStatusLine("textBoxStatusSamNotConfigured", DateTime.Now);
            }
            else
            {
                StatusText += LocalizedStatusLine(
                    "textBoxStatusSamFailed",
                    DateTime.Now,
                    result.Failure,
                    result.SamStatus.ToString("X4", CultureInfo.InvariantCulture),
                    result.CardStatus.ToString("X4", CultureInfo.InvariantCulture));
            }

            if (result.KeyMayHaveChanged)
            {
                StatusText += LocalizedStatusLine("textBoxStatusSamKeyMayHaveChanged", DateTime.Now);
            }

            await UpdateReaderStatusCommand.ExecuteAsync(false);
        }
    }
}
