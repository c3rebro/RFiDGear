#nullable enable
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.Tasks;
using RFiDGear.Models;
using RFiDGear.Services.SecureKeys;
using RFiDGear.ViewModel.TaskSetupViewModels;

namespace RFiDGear.HIL.Tests
{
    /// <summary>
    /// Builds the DESFire tasks of the secure-key hardware cases for the reserved test application
    /// <see cref="HilConstants.TestAppId"/>. A slot is bound to the Windows Credential Manager by an entry in
    /// <c>KeyBindings</c>. The key that is typed into the task for a bound slot is a decoy on purpose: a case gives it a value
    /// that a card would accept or refuse the other way round from the vault key, so that a task which used the typed key
    /// instead of the bound one could not pass.
    /// </summary>
    internal static class HilSecureKeyTasks
    {
        /// <summary>The slot of the key that opens the test application.</summary>
        public const string AppCurrent = SecureKeySlots.DesfireAppCurrent;

        /// <summary>The slot of the new application key of a key change.</summary>
        public const string AppNew = SecureKeySlots.DesfireAppNew;

        /// <summary>Binds a slot to a credential of the Windows Credential Manager.</summary>
        /// <param name="slot">The slot id.</param>
        /// <param name="reference">The reference (see <see cref="HilVault"/>).</param>
        /// <returns>The entry.</returns>
        public static KeyBindingEntry Bind(string slot, string reference) =>
            new KeyBindingEntry { Slot = slot, ProviderId = WindowsCredentialKeyProvider.ProviderId, Reference = reference };

        /// <summary>
        /// Authenticates to the test application with application key 0. Nothing on the card changes.
        /// </summary>
        /// <param name="keyType">Key type of the application.</param>
        /// <param name="typedKey">The key typed into the task; it is not used for a slot that is bound.</param>
        /// <param name="index">The task index.</param>
        /// <param name="bindings">The slots that come from the Credential Manager.</param>
        /// <returns>The task.</returns>
        public static MifareDesfireSetupViewModel Authenticate(DESFireKeyType keyType, string typedKey, int index, params KeyBindingEntry[] bindings)
        {
            var task = new MifareDesfireSetupViewModel
            {
                SelectedTaskType = TaskType_MifareDesfireTask.AuthenticateApplication,
                AppNumberCurrent = HexAid(HilConstants.TestAppId),
                DesfireAppKeyCurrent = typedKey,
                SelectedDesfireAppKeyEncryptionTypeCurrent = keyType,
                SelectedDesfireAppKeyNumberCurrent = "0",
                CurrentTaskIndex = index.ToString(System.Globalization.CultureInfo.InvariantCulture),
                SelectedExecuteConditionErrorLevel = ERROR.Empty,
            };

            task.KeyBindings = bindings;

            return task;
        }

        /// <summary>
        /// Changes application key 0 of the test application. It authenticates with application key 0, as the full workflow
        /// of the other hardware cases does.
        /// </summary>
        /// <param name="keyType">Key type of the application.</param>
        /// <param name="typedCurrent">The key typed in for the current key; it is not used if the slot is bound.</param>
        /// <param name="typedNew">The key typed in for the new key; it is not used if the slot is bound.</param>
        /// <param name="index">The task index.</param>
        /// <param name="bindings">The slots that come from the Credential Manager.</param>
        /// <returns>The task.</returns>
        public static MifareDesfireSetupViewModel ChangeAppKey(DESFireKeyType keyType, string typedCurrent, string typedNew, int index, params KeyBindingEntry[] bindings)
        {
            var task = new MifareDesfireSetupViewModel
            {
                SelectedTaskType = TaskType_MifareDesfireTask.ApplicationKeyChangeover,
                AppNumberCurrent = HexAid(HilConstants.TestAppId),
                DesfireAppKeyCurrent = typedCurrent,
                SelectedDesfireAppKeyEncryptionTypeCurrent = keyType,
                SelectedDesfireAppKeyNumberCurrent = "0",
                DesfireAppKeyTarget = typedNew,
                SelectedDesfireAppKeyEncryptionTypeTarget = keyType,
                SelectedDesfireAppKeyVersionTarget = "01",
                CurrentTaskIndex = index.ToString(System.Globalization.CultureInfo.InvariantCulture),
                SelectedExecuteConditionErrorLevel = ERROR.Empty,
            };

            task.KeyBindings = bindings;

            return task;
        }

        private static string HexAid(uint appId) => $"0x{appId:X6}";
    }
}
