using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.Tasks;
using RFiDGear.ViewModel.TaskSetupViewModels;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Xunit;

namespace RFiDGear.Tests.KeySlots
{
    /// <summary>
    /// Pins which key slot of a DESFire task reaches which call of the reader, today. Every slot of a task gets its own
    /// recognizable key, the task runs against a reader that records the keys it receives, and the test compares the
    /// recorded calls with the slot names. The tests change how keys reach the reader (a key store instead of the
    /// property); this is the proof that a legacy project keeps authenticating with the same keys.
    /// </summary>
    public class DesfireKeySlotCharacterizationTests
    {
        private const string MasterCurrentKey = "10101010101010101010101010101010";
        private const string AppCurrentOldKey = "40404040404040404040404040404040";

        private static readonly Dictionary<string, string> SlotsByKey = new Dictionary<string, string>
        {
            [MasterCurrentKey] = "MasterCurrent",
            ["20202020202020202020202020202020"] = "MasterTarget",
            ["30303030303030303030303030303030"] = "AppCurrent",
            [AppCurrentOldKey] = "AppCurrentOld",
            ["50505050505050505050505050505050"] = "AppTarget",
            ["60606060606060606060606060606060"] = "Read",
            ["70707070707070707070707070707070"] = "Write",
        };

        // One entry per task type: the reader calls that carry keys, in order, with the slot of each key argument.
        internal static readonly Dictionary<TaskType_MifareDesfireTask, string> ExpectedCalls = new Dictionary<TaskType_MifareDesfireTask, string>
        {
            [TaskType_MifareDesfireTask.PICCMasterKeySettingsChangeover] = "AuthToMifareDesfireApplication[AppCurrent] | ChangeMifareDesfireApplicationKeySettings[AppCurrent]",
            [TaskType_MifareDesfireTask.ChangeFileSettings] = "ChangeMifareDesfireFileSettings[AppCurrent]",
            [TaskType_MifareDesfireTask.DeleteFile] = "AuthToMifareDesfireApplication[AppCurrent] | DeleteMifareDesfireFile[AppCurrent]",
            [TaskType_MifareDesfireTask.WriteData] = "WriteMiFareDESFireChipFile[Write]",
            [TaskType_MifareDesfireTask.ReadAppSettings] = "GetMifareDesfireAppSettings[AppCurrent]",
            [TaskType_MifareDesfireTask.ReadData] = "ReadMiFareDESFireChipFile[Read]",
            [TaskType_MifareDesfireTask.ApplicationKeyChangeover] = "AuthToMifareDesfireApplication[AppCurrent] | ChangeMifareDesfireKeyAsync[AppCurrent,AppTarget,AppCurrent] | VerifyMifareDesfireKeyChange[AppTarget]",
            [TaskType_MifareDesfireTask.FormatDesfireCard] = "AuthToMifareDesfireApplication[MasterCurrent] | GetMiFareDESFireChipAppIDs[MasterCurrent] | FormatDesfireCard[MasterCurrent]",
            [TaskType_MifareDesfireTask.AuthenticateApplication] = "AuthToMifareDesfireApplication[AppCurrent]",
            [TaskType_MifareDesfireTask.CheckAppKeyCount] = "GetMifareDesfireAppSettings[AppCurrent]",
            [TaskType_MifareDesfireTask.CreateFile] = "CreateMifareDesfireFile[AppCurrent]",
            [TaskType_MifareDesfireTask.PICCMasterKeyChangeover] = "AuthToMifareDesfireApplication[MasterCurrent] | ChangeMifareDesfireKeyAsync[MasterCurrent,MasterTarget,MasterCurrent] | VerifyMifareDesfireKeyChange[MasterTarget]",
            [TaskType_MifareDesfireTask.CheckFileSettings] = "AuthToMifareDesfireApplication[AppCurrent] | GetMifareDesfireFileSettings[AppCurrent]",
            [TaskType_MifareDesfireTask.DeleteApplication] = "DeleteMifareDesfireApplication[MasterCurrent]",
            [TaskType_MifareDesfireTask.ApplicationKeySettingsChangeover] = "AuthToMifareDesfireApplication[AppCurrent] | ChangeMifareDesfireApplicationKeySettings[AppCurrent]",
            [TaskType_MifareDesfireTask.AppExistCheck] = "GetMiFareDESFireChipAppIDs[AppCurrent]",
            [TaskType_MifareDesfireTask.CreateApplication] = "CreateMifareDesfireApplication[MasterCurrent]",
        };

        /// <summary>Every task type with the calls it is expected to make.</summary>
        public static IEnumerable<object[]> EveryTaskType() =>
            ExpectedCalls.Select(entry => new object[] { entry.Key, entry.Value });

        [Fact]
        public void Every_task_type_has_an_expected_call_map()
        {
            var types = Enum.GetValues(typeof(TaskType_MifareDesfireTask)).Cast<TaskType_MifareDesfireTask>()
                .Where(type => type != TaskType_MifareDesfireTask.None);

            Assert.Equal(types.OrderBy(type => type), ExpectedCalls.Keys.OrderBy(type => type));
        }

        [Theory]
        [MemberData(nameof(EveryTaskType))]
        public async Task A_task_hands_each_slot_to_the_call_that_uses_it(TaskType_MifareDesfireTask type, string expected)
        {
            var run = await RunAsync(type);

            // The error level is not pinned: a check task ends in a verdict about the card's data, which the fake does not have.
            Assert.Equal(expected, run.Calls);
        }

        [Fact]
        public async Task A_key_settings_change_of_the_card_application_authenticates_with_the_master_key()
        {
            var run = await RunAsync(TaskType_MifareDesfireTask.PICCMasterKeySettingsChangeover, vm => vm.AppNumberCurrent = "0");

            Assert.Equal("AuthToMifareDesfireApplication[MasterCurrent] | ChangeMifareDesfireApplicationKeySettings[MasterCurrent]", run.Calls);
        }

        [Fact]
        public async Task Deleting_an_application_with_its_own_master_key_uses_the_application_key()
        {
            var run = await RunAsync(
                TaskType_MifareDesfireTask.DeleteApplication,
                vm => vm.SelectedDesfireDeleteAuthMethod = DesfireDeleteAuthMethod.ApplicationMasterKey0);

            Assert.Equal("DeleteMifareDesfireApplication[AppCurrent]", run.Calls);
        }

        [Fact]
        public async Task A_refused_application_creation_is_tried_once_more_without_authentication_with_the_same_slot()
        {
            var run = await RunAsync(
                TaskType_MifareDesfireTask.CreateApplication,
                reader: reader =>
                {
                    reader.FailMethod = "CreateMifareDesfireApplication";
                    reader.FailWith = ERROR.AuthFailure;
                });

            Assert.Equal("CreateMifareDesfireApplication[MasterCurrent] | CreateMifareDesfireApplicationWithoutAuth[MasterCurrent]", run.Calls);
        }

        [Fact]
        public async Task Changing_an_application_key_with_old_key_inputs_hands_the_old_slot_to_the_card_as_the_current_key()
        {
            // The old-key inputs show once a key other than 0 is addressed; they follow the current key until then.
            var run = await RunAsync(
                TaskType_MifareDesfireTask.ApplicationKeyChangeover,
                vm =>
                {
                    vm.SelectedDesfireAppKeyNumberCurrent = "1";
                    vm.DesfireAppKeyCurrentOld = AppCurrentOldKey;
                });

            Assert.Equal("AuthToMifareDesfireApplication[AppCurrent] | ChangeMifareDesfireKeyAsync[AppCurrentOld,AppTarget,AppCurrent] | VerifyMifareDesfireKeyChange[AppTarget]", run.Calls);
        }

        [Fact]
        public async Task Changing_an_application_key_with_an_empty_old_key_touches_nothing()
        {
            var run = await RunAsync(
                TaskType_MifareDesfireTask.ApplicationKeyChangeover,
                vm =>
                {
                    vm.SelectedDesfireAppKeyNumberCurrent = "1";
                    vm.DesfireAppKeyCurrentOld = string.Empty;
                });

            Assert.Equal(string.Empty, run.Calls);
            Assert.Equal(ERROR.Empty, run.Error);
        }

        [Fact]
        public async Task Changing_the_card_master_key_with_an_empty_master_key_touches_nothing()
        {
            var run = await RunAsync(TaskType_MifareDesfireTask.PICCMasterKeyChangeover, vm => vm.DesfireMasterKeyCurrent = string.Empty);

            Assert.Equal(string.Empty, run.Calls);
            Assert.Equal(ERROR.Empty, run.Error);
        }

        [Fact]
        public async Task Formatting_does_not_need_the_app_key()
        {
            // The command authenticates and formats with the master key; the application key is not part of it.
            var run = await RunAsync(TaskType_MifareDesfireTask.FormatDesfireCard, vm => vm.DesfireAppKeyCurrent = string.Empty);

            Assert.Equal(ExpectedCalls[TaskType_MifareDesfireTask.FormatDesfireCard], run.Calls);
        }

        [Fact]
        public async Task Formatting_with_an_empty_master_key_touches_nothing()
        {
            var run = await RunAsync(TaskType_MifareDesfireTask.FormatDesfireCard, vm => vm.DesfireMasterKeyCurrent = string.Empty);

            Assert.Equal(string.Empty, run.Calls);
            Assert.Equal(ERROR.Empty, run.Error);
        }

        [Fact]
        public async Task Formatting_checks_the_master_key_against_its_own_key_type()
        {
            // A 3K3DES key has 48 hex digits; the 32 digits of the test key are too short for it, whatever the type of
            // the application key is.
            var run = await RunAsync(
                TaskType_MifareDesfireTask.FormatDesfireCard,
                vm => vm.SelectedDesfireMasterKeyEncryptionTypeCurrent = DESFireKeyType.DF_KEY_3K3DES);

            Assert.Equal(string.Empty, run.Calls);
            Assert.Equal(ERROR.Empty, run.Error);
        }

        private sealed record Run(string Calls, ERROR Error);

        private static async Task<Run> RunAsync(
            TaskType_MifareDesfireTask type,
            Action<MifareDesfireSetupViewModel>? viewModel = null,
            Action<KeyRecordingReader>? reader = null)
        {
            Run? run = null;

            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var vm = NewViewModel(type);
                viewModel?.Invoke(vm);

                var recording = new KeyRecordingReader();
                reader?.Invoke(recording);

                using (ReaderDeviceScope.Use(recording))
                {
                    await vm.CommandDelegator.ExecuteAsync(type);
                }

                run = new Run(Describe(recording), vm.CurrentTaskErrorLevel);
            });

            return run!;
        }

        private static string Describe(KeyRecordingReader reader) =>
            string.Join(" | ", reader.Calls.Select(call => $"{call.Method}[{string.Join(",", call.Keys.Select(NameOf))}]"));

        private static string NameOf(string key) =>
            key.Length == 0 ? "<empty>" : SlotsByKey.TryGetValue(key, out var slot) ? slot : $"<unknown {key}>";

        // The properties are set in this order on purpose: the old application key follows the current one until the
        // old-key inputs show, so it is set last.
        private static MifareDesfireSetupViewModel NewViewModel(TaskType_MifareDesfireTask type) => new MifareDesfireSetupViewModel
        {
            SelectedTaskType = type,
            CurrentTaskIndex = "0",
            AppNumberCurrent = "1",
            AppNumberNew = "2",
            FileNumberCurrent = "2",
            FileSizeCurrent = "16",
            DesfireMasterKeyCurrent = MasterCurrentKey,
            DesfireMasterKeyTarget = "20202020202020202020202020202020",
            DesfireAppKeyCurrent = "30303030303030303030303030303030",
            DesfireAppKeyCurrentOld = AppCurrentOldKey,
            DesfireAppKeyTarget = "50505050505050505050505050505050",
            DesfireReadKeyCurrent = "60606060606060606060606060606060",
            DesfireWriteKeyCurrent = "70707070707070707070707070707070",
            SelectedDesfireMasterKeyEncryptionTypeCurrent = DESFireKeyType.DF_KEY_AES,
            SelectedDesfireMasterKeyEncryptionTypeTarget = DESFireKeyType.DF_KEY_AES,
            SelectedDesfireAppKeyEncryptionTypeCurrent = DESFireKeyType.DF_KEY_AES,
            SelectedDesfireAppKeyEncryptionTypeTarget = DESFireKeyType.DF_KEY_AES,
            SelectedDesfireReadKeyEncryptionType = DESFireKeyType.DF_KEY_AES,
            SelectedDesfireWriteKeyEncryptionType = DESFireKeyType.DF_KEY_AES,
            SelectedDesfireAppKeyNumberCurrent = "0",
        };
    }
}
