using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.AccessControl;
using RFiDGear.Infrastructure.Tasks;
using RFiDGear.ViewModel.TaskSetupViewModels;

using System;
using System.Collections.Generic;
using System.IO;

namespace RFiDGear.HIL.Tests
{
    /// <summary>
    /// Builds pre-configured <see cref="MifareDesfireSetupViewModel"/> instances for HIL tests.
    /// All tasks target the reserved test AID <see cref="HilConstants.TestAppId"/>.
    /// </summary>
    internal static class HilTaskBuilder
    {
        // ── Application lifecycle ─────────────────────────────────────────────

        /// <summary>
        /// Creates a task collection that creates the test application on the card.
        /// PICC authentication always uses the factory-default DES master key.
        /// </summary>
        /// <param name="appId">The AID to create.</param>
        /// <param name="appKeyType">Key type for the new application.</param>
        public static IEnumerable<object> BuildCreateApplication(
            uint appId,
            DESFireKeyType appKeyType)
        {
            var task = new MifareDesfireSetupViewModel
            {
                SelectedTaskType = TaskType_MifareDesfireTask.CreateApplication,
                AppNumberNew = HexAid(appId),
                SelectedDesfireAppKeyEncryptionTypeCreateNewApp = appKeyType,
                SelectedDesfireAppKeySettingsCreateNewApp = AccessCondition_MifareDesfireAppCreation.ChangeKeyUsingMK,
                SelectedDesfireAppMaxNumberOfKeys = "2",
                // Factory default PICC master key is always DES (2K3DES, 16 bytes all-zero).
                DesfireMasterKeyCurrent = HilConstants.DefaultKeyDes,
                SelectedDesfireMasterKeyEncryptionTypeCurrent = DESFireKeyType.DF_KEY_DES,
                CurrentTaskIndex = "0",
                SelectedExecuteConditionErrorLevel = ERROR.Empty,
            };
            yield return task;
        }

        /// <summary>
        /// Creates a task collection that deletes the test application from the card.
        /// </summary>
        /// <param name="appId">The AID to delete.</param>
        /// <param name="appKeyType">Key type of the application master key used for auth.</param>
        /// <param name="appKey">Application master key (hex) or PICC master key when using <see cref="DesfireDeleteAuthMethod.PiccMasterKey"/>.</param>
        /// <param name="deleteAuthMethod">Which principal authenticates before the delete command.</param>
        public static IEnumerable<object> BuildDeleteApplication(
            uint appId,
            DESFireKeyType appKeyType,
            string appKey,
            DesfireDeleteAuthMethod deleteAuthMethod)
        {
            var task = new MifareDesfireSetupViewModel
            {
                SelectedTaskType = TaskType_MifareDesfireTask.DeleteApplication,
                AppNumberNew = HexAid(appId),
                SelectedDesfireDeleteAuthMethod = deleteAuthMethod,
                SelectedExecuteConditionErrorLevel = ERROR.Empty,
                CurrentTaskIndex = "0",
            };

            if (deleteAuthMethod == DesfireDeleteAuthMethod.ApplicationMasterKey0)
            {
                task.DesfireAppKeyCurrent = appKey;
                task.SelectedDesfireAppKeyEncryptionTypeCurrent = appKeyType;
            }
            else
            {
                task.DesfireMasterKeyCurrent = appKey;
                task.SelectedDesfireMasterKeyEncryptionTypeCurrent = appKeyType;
            }

            yield return task;
        }

        // ── Full workflow ─────────────────────────────────────────────────────

        /// <summary>
        /// Builds the full HIL workflow task sequence:
        /// CreateApp → CreateFile → Write → Read → ChangeKey → Read with new key → DeleteFile → DeleteApp.
        /// PICC authentication always uses the factory-default DES master key.
        /// DeleteApplication always authenticates as PICC master key (DESFire EV1+ protocol requirement).
        /// </summary>
        /// <param name="appId">AID to create and delete.</param>
        /// <param name="keyType">Key type for application keys.</param>
        /// <param name="defaultKey">Zero-value key in the correct format for <paramref name="keyType"/>.</param>
        /// <param name="changedKey">New key value used after key-change step.</param>
        /// <param name="writePayloadPath">Path to a temp file containing the hex write payload.</param>
        public static IEnumerable<object> BuildFullWorkflow(
            uint appId,
            DESFireKeyType keyType,
            string defaultKey,
            string changedKey,
            string writePayloadPath)
        {
            var aidStr = HexAid(appId);

            // 0 — CreateApplication (factory PICC master key is always DES)
            yield return new MifareDesfireSetupViewModel
            {
                SelectedTaskType = TaskType_MifareDesfireTask.CreateApplication,
                AppNumberNew = aidStr,
                SelectedDesfireAppKeyEncryptionTypeCreateNewApp = keyType,
                SelectedDesfireAppKeySettingsCreateNewApp = AccessCondition_MifareDesfireAppCreation.ChangeKeyUsingMK,
                SelectedDesfireAppMaxNumberOfKeys = "2",
                DesfireMasterKeyCurrent = HilConstants.DefaultKeyDes,
                SelectedDesfireMasterKeyEncryptionTypeCurrent = DESFireKeyType.DF_KEY_DES,
                CurrentTaskIndex = "0",
                SelectedExecuteConditionErrorLevel = ERROR.Empty,
            };

            // 1 — CreateFile (standard data file, free read/write via key 0)
            yield return new MifareDesfireSetupViewModel
            {
                SelectedTaskType = TaskType_MifareDesfireTask.CreateFile,
                AppNumberCurrent = aidStr,
                DesfireAppKeyCurrent = defaultKey,
                SelectedDesfireAppKeyEncryptionTypeCurrent = keyType,
                SelectedDesfireAppKeyNumberCurrent = "0",
                FileNumberCurrent = HilConstants.TestFileNo.ToString(),
                FileSizeCurrent = HilConstants.TestFileSize.ToString(),
                CurrentTaskIndex = "1",
                SelectedExecuteConditionErrorLevel = ERROR.Empty,
            };

            // 2 — WriteData
            yield return new MifareDesfireSetupViewModel
            {
                SelectedTaskType = TaskType_MifareDesfireTask.WriteData,
                AppNumberCurrent = aidStr,
                DesfireWriteKeyCurrent = defaultKey,
                SelectedDesfireWriteKeyEncryptionType = keyType,
                SelectedDesfireWriteKeyNumber = "0",
                FileNumberCurrent = HilConstants.TestFileNo.ToString(),
                FileSizeCurrent = HilConstants.TestFileSize.ToString(),
                DesfireDataFilePath = writePayloadPath,
                RefreshDesfireDataFromFileBeforeWrite = true,
                CurrentTaskIndex = "2",
                SelectedExecuteConditionErrorLevel = ERROR.Empty,
            };

            // 3 — ReadData (verify write)
            yield return new MifareDesfireSetupViewModel
            {
                SelectedTaskType = TaskType_MifareDesfireTask.ReadData,
                AppNumberCurrent = aidStr,
                DesfireReadKeyCurrent = defaultKey,
                SelectedDesfireReadKeyEncryptionType = keyType,
                SelectedDesfireReadKeyNumber = "0",
                FileNumberCurrent = HilConstants.TestFileNo.ToString(),
                FileSizeCurrent = HilConstants.TestFileSize.ToString(),
                CurrentTaskIndex = "3",
                SelectedExecuteConditionErrorLevel = ERROR.Empty,
            };

            // 4 — ApplicationKeyChangeover (change app key 0 from defaultKey to changedKey)
            yield return new MifareDesfireSetupViewModel
            {
                SelectedTaskType = TaskType_MifareDesfireTask.ApplicationKeyChangeover,
                AppNumberCurrent = aidStr,
                DesfireAppKeyCurrent = defaultKey,
                SelectedDesfireAppKeyEncryptionTypeCurrent = keyType,
                SelectedDesfireAppKeyNumberCurrent = "0",
                DesfireAppKeyTarget = changedKey,
                SelectedDesfireAppKeyEncryptionTypeTarget = keyType,
                SelectedDesfireAppKeyVersionTarget = "01",
                CurrentTaskIndex = "4",
                SelectedExecuteConditionErrorLevel = ERROR.Empty,
            };

            // 5 — ReadData again (with the new key — verifies auth with changed key)
            yield return new MifareDesfireSetupViewModel
            {
                SelectedTaskType = TaskType_MifareDesfireTask.ReadData,
                AppNumberCurrent = aidStr,
                DesfireReadKeyCurrent = changedKey,
                SelectedDesfireReadKeyEncryptionType = keyType,
                SelectedDesfireReadKeyNumber = "0",
                FileNumberCurrent = HilConstants.TestFileNo.ToString(),
                FileSizeCurrent = HilConstants.TestFileSize.ToString(),
                CurrentTaskIndex = "5",
                SelectedExecuteConditionErrorLevel = ERROR.Empty,
            };

            // 6 — DeleteFile
            yield return new MifareDesfireSetupViewModel
            {
                SelectedTaskType = TaskType_MifareDesfireTask.DeleteFile,
                AppNumberCurrent = aidStr,
                DesfireAppKeyCurrent = changedKey,
                SelectedDesfireAppKeyEncryptionTypeCurrent = keyType,
                SelectedDesfireAppKeyNumberCurrent = "0",
                FileNumberCurrent = HilConstants.TestFileNo.ToString(),
                CurrentTaskIndex = "6",
                SelectedExecuteConditionErrorLevel = ERROR.Empty,
            };

            // 7 — DeleteApplication via PICC master key.
            // DESFire EV1+ requires PICC-level auth for DeleteApplication; authenticating at app
            // level and issuing the command from there is rejected by the card.
            yield return new MifareDesfireSetupViewModel
            {
                SelectedTaskType = TaskType_MifareDesfireTask.DeleteApplication,
                AppNumberNew = aidStr,
                SelectedDesfireDeleteAuthMethod = DesfireDeleteAuthMethod.PiccMasterKey,
                DesfireMasterKeyCurrent = HilConstants.DefaultKeyDes,
                SelectedDesfireMasterKeyEncryptionTypeCurrent = DESFireKeyType.DF_KEY_DES,
                CurrentTaskIndex = "7",
                SelectedExecuteConditionErrorLevel = ERROR.Empty,
            };
        }

        // ── Utilities ─────────────────────────────────────────────────────────

        /// <summary>
        /// Writes the test payload to a temporary file as a hex string and returns the path.
        /// The caller is responsible for deleting the file after the test.
        /// </summary>
        public static string WriteTestPayloadTempFile()
        {
            var path = Path.Combine(Path.GetTempPath(), $"hil_payload_{Guid.NewGuid():N}.hex");
            // Format each byte as two-digit uppercase hex, space-separated.
            File.WriteAllText(path, BitConverter.ToString(HilConstants.TestPayload).Replace("-", " "));
            return path;
        }

        private static string HexAid(uint appId) => $"0x{appId:X6}";
    }
}
