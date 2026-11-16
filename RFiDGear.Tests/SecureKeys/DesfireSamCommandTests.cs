#nullable enable
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.ReaderProviders;
using RFiDGear.Infrastructure.Tasks;
using RFiDGear.Models;
using RFiDGear.Services.Sam;
using RFiDGear.Services.SecureKeys;
using RFiDGear.Tests.KeySlots;
using RFiDGear.ViewModel.TaskSetupViewModels;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Xunit;

namespace RFiDGear.Tests.SecureKeys
{
    /// <summary>
    /// The commands of a DESFire task that can use keys that stay in a MIFARE SAM AV3: the authentication of an application, the change of the
    /// PICC master key and of an application key, and the authentication of the PICC master key (the button of the dialog). They give the
    /// reader the references of the keys and never a key, they do nothing for a reader that cannot, and no other command ever reaches the reader
    /// with a key of the SAM.
    /// </summary>
    public class DesfireSamCommandTests : IDisposable
    {
        // What a project of before holds in the slots: recognisable, and never to reach the reader of a task whose slots name the SAM.
        private const string EmbeddedAes = "E0E0E0E0E0E0E0E0E0E0E0E0E0E0E0E0";

        private static readonly SamKeyReference Entry1 = Reference("1:0");
        private static readonly SamKeyReference Entry2 = Reference("2:0");
        private static readonly SamKeyReference Entry3 = Reference("3:1");
        private static readonly SamKeyReference Entry4 = Reference("4:2");

        public DesfireSamCommandTests()
        {
            KeyResolutionScope.SetDefault(null);
        }

        public void Dispose()
        {
            KeyResolutionScope.SetDefault(null);
        }

        private static SamKeyReference Reference(string text)
        {
            Assert.True(SamKeyReference.TryParse(text, out var reference));

            return reference;
        }

        private static KeyBindingEntry Sam(string slot, SamKeyReference reference) =>
            new KeyBindingEntry { Slot = slot, ProviderId = SamKeyProvider.ProviderId, Reference = reference.Format() };

        private static MifareDesfireSetupViewModel NewTask(TaskType_MifareDesfireTask type) => new MifareDesfireSetupViewModel
        {
            SelectedTaskType = type,
            CurrentTaskIndex = "0",
            AppNumberCurrent = "1",
            AppNumberNew = "2",
            FileNumberCurrent = "2",
            FileSizeCurrent = "16",
            DesfireMasterKeyCurrent = EmbeddedAes,
            DesfireMasterKeyTarget = EmbeddedAes,
            DesfireAppKeyCurrent = EmbeddedAes,
            DesfireAppKeyCurrentOld = EmbeddedAes,
            DesfireAppKeyTarget = EmbeddedAes,
            DesfireReadKeyCurrent = EmbeddedAes,
            DesfireWriteKeyCurrent = EmbeddedAes,
            SelectedDesfireMasterKeyEncryptionTypeCurrent = DESFireKeyType.DF_KEY_DES,
            SelectedDesfireMasterKeyEncryptionTypeTarget = DESFireKeyType.DF_KEY_AES,
            SelectedDesfireAppKeyEncryptionTypeCurrent = DESFireKeyType.DF_KEY_AES,
            SelectedDesfireAppKeyEncryptionTypeTarget = DESFireKeyType.DF_KEY_AES,
            SelectedDesfireReadKeyEncryptionType = DESFireKeyType.DF_KEY_AES,
            SelectedDesfireWriteKeyEncryptionType = DESFireKeyType.DF_KEY_AES,
            SelectedDesfireAppKeyNumberCurrent = "0",
        };

        private sealed record Run(ERROR Error, string Status);

        private static async Task<Run> RunAsync(MifareDesfireSetupViewModel task, KeyRecordingReader reader)
        {
            using (ReaderDeviceScope.Use(reader))
            {
                await task.CommandDelegator.ExecuteAsync(task.SelectedTaskType);
            }

            return new Run(task.CurrentTaskErrorLevel, task.StatusText ?? string.Empty);
        }

        private static async Task<Run> PressPiccAuthenticationAsync(MifareDesfireSetupViewModel task, KeyRecordingReader reader)
        {
            using (ReaderDeviceScope.Use(reader))
            {
                await task.AuthenticateToCardMasterApplicationCommand.ExecuteAsync(null);
            }

            return new Run(task.CurrentTaskErrorLevel, task.StatusText ?? string.Empty);
        }

        // ── the four commands, with a reader that can ──

        [Fact]
        public async Task The_authentication_of_an_application_gives_the_reader_the_reference_and_no_key()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var task = NewTask(TaskType_MifareDesfireTask.AuthenticateApplication);
                task.KeyBindings = new[] { Sam(SecureKeySlots.DesfireAppCurrent, Entry3) };
                var reader = new KeyRecordingReader { SupportsSam = true };

                var run = await RunAsync(task, reader);

                var access = Assert.Single(reader.SamAuthentications);
                Assert.Equal((1u, DESFireKeyType.DF_KEY_AES, Entry3), (access.AppId, access.KeyType, access.Key));
                Assert.Empty(reader.Calls);
                Assert.Empty(reader.SamChanges);
                Assert.Equal(ERROR.NoError, run.Error);
                Assert.True(task.IsTaskCompletedSuccessfully);
            });
        }

        [Theory]
        [InlineData("0")]
        [InlineData("1")]
        [InlineData("3")]
        [InlineData("13")]
        public async Task The_authentication_of_an_application_uses_the_key_number_that_the_card_command_uses(string chosenKeyNumber)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var typed = NewTask(TaskType_MifareDesfireTask.AuthenticateApplication);
                typed.SelectedDesfireAppKeyNumberCurrent = chosenKeyNumber;
                var baseline = new KeyRecordingReader();
                await RunAsync(typed, baseline);

                var task = NewTask(TaskType_MifareDesfireTask.AuthenticateApplication);
                task.SelectedDesfireAppKeyNumberCurrent = chosenKeyNumber;
                task.KeyBindings = new[] { Sam(SecureKeySlots.DesfireAppCurrent, Entry3) };
                var reader = new KeyRecordingReader { SupportsSam = true };

                await RunAsync(task, reader);

                Assert.Equal((byte)baseline.LastAuthKeyNumber, Assert.Single(reader.SamAuthentications).KeyNumber);
            });
        }

        [Fact]
        public async Task The_authentication_of_the_picc_master_key_gives_the_reader_the_reference_and_no_key()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var task = NewTask(TaskType_MifareDesfireTask.PICCMasterKeyChangeover);
                task.KeyBindings = new[] { Sam(SecureKeySlots.DesfirePiccCurrent, Entry2) };
                var reader = new KeyRecordingReader { SupportsSam = true };

                var run = await PressPiccAuthenticationAsync(task, reader);

                var access = Assert.Single(reader.SamAuthentications);
                Assert.Equal((0u, (byte)0, DESFireKeyType.DF_KEY_DES, Entry2), (access.AppId, access.KeyNumber, access.KeyType, access.Key));
                Assert.Empty(reader.Calls);
                Assert.Equal(ERROR.NoError, run.Error);
            });
        }

        [Fact]
        public async Task The_change_of_the_picc_master_key_has_the_sam_replace_the_key_with_the_key_that_authenticates()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var task = NewTask(TaskType_MifareDesfireTask.PICCMasterKeyChangeover);
                task.KeyBindings = new[] { Sam(SecureKeySlots.DesfirePiccCurrent, Entry2), Sam(SecureKeySlots.DesfirePiccNew, Entry1) };
                var reader = new KeyRecordingReader { SupportsSam = true };

                var run = await RunAsync(task, reader);

                var change = Assert.Single(reader.SamChanges);
                Assert.Equal(
                    new DesfireSamKeyChange(0, 0, DESFireKeyType.DF_KEY_DES, Entry2, 0, DESFireKeyType.DF_KEY_AES, Entry2, Entry1),
                    change);
                Assert.Empty(reader.Calls);
                Assert.Empty(reader.SamAuthentications);
                Assert.Equal(ERROR.NoError, run.Error);
            });
        }

        [Fact]
        public async Task The_change_of_an_application_key_names_the_three_keys_of_the_sam_and_the_key_numbers_of_the_policy()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                // The key number that authenticates is the one that the card command would use, with a key typed into the task or not.
                var baseline = new KeyRecordingReader();
                await RunAsync(NewTask(TaskType_MifareDesfireTask.ApplicationKeyChangeover), baseline);

                var task = NewTask(TaskType_MifareDesfireTask.ApplicationKeyChangeover);
                task.KeyBindings = new[] { Sam(SecureKeySlots.DesfireAppCurrent, Entry3), Sam(SecureKeySlots.DesfireAppNew, Entry4) };
                var reader = new KeyRecordingReader { SupportsSam = true };

                var run = await RunAsync(task, reader);

                var change = Assert.Single(reader.SamChanges);
                Assert.Equal(1u, change.AppId);
                Assert.Equal((byte)baseline.LastAuthKeyNumber, change.AuthKeyNumber);
                Assert.Equal((byte)0, change.TargetKeyNumber);
                Assert.Equal((Entry3, Entry3, Entry4), (change.AuthKey, change.CurrentKey, change.NewKey));
                Assert.Equal((DESFireKeyType.DF_KEY_AES, DESFireKeyType.DF_KEY_AES), (change.AuthKeyType, change.NewKeyType));
                Assert.Empty(reader.Calls);
                Assert.Equal(ERROR.NoError, run.Error);
            });
        }

        [Fact]
        public async Task The_replaced_key_of_an_application_key_change_is_its_own_slot_when_the_old_key_inputs_are_shown()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var task = NewTask(TaskType_MifareDesfireTask.ApplicationKeyChangeover);
                task.SelectedDesfireAppKeyNumberCurrent = "1";
                task.DesfireAppKeyCurrentOld = "E1E1E1E1E1E1E1E1E1E1E1E1E1E1E1E1";
                task.KeyBindings = new[]
                {
                    Sam(SecureKeySlots.DesfireAppCurrent, Entry3),
                    Sam(SecureKeySlots.DesfireAppOld, Entry2),
                    Sam(SecureKeySlots.DesfireAppNew, Entry4),
                };
                var reader = new KeyRecordingReader { SupportsSam = true };

                var run = await RunAsync(task, reader);

                var change = Assert.Single(reader.SamChanges);
                Assert.Equal((byte)1, change.TargetKeyNumber);
                Assert.Equal((Entry3, Entry2, Entry4), (change.AuthKey, change.CurrentKey, change.NewKey));
                Assert.Equal(ERROR.NoError, run.Error);
            });
        }

        // ── a reader that cannot, and keys that are mixed ──

        [Theory]
        [InlineData(TaskType_MifareDesfireTask.AuthenticateApplication)]
        [InlineData(TaskType_MifareDesfireTask.PICCMasterKeyChangeover)]
        [InlineData(TaskType_MifareDesfireTask.ApplicationKeyChangeover)]
        public async Task A_reader_that_cannot_use_a_sam_ends_the_task_before_anything_is_sent_and_says_why(TaskType_MifareDesfireTask type)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var task = NewTask(type);
                task.KeyBindings = SecureKeySlots.Desfire.Select(slot => Sam(slot, Entry1)).ToArray();
                var reader = new KeyRecordingReader { SupportsSam = false };

                var run = await RunAsync(task, reader);

                Assert.Equal(ERROR.KeyUnavailable, run.Error);
                Assert.Empty(reader.Calls);
                Assert.Empty(reader.SamAuthentications);
                Assert.Empty(reader.SamChanges);
                Assert.Contains(ResourceLoader.GetResource("textBoxStatusSamNotSupported").Substring(5), run.Status, StringComparison.Ordinal);
            });
        }

        [Fact]
        public async Task A_change_with_one_key_of_the_sam_and_one_typed_key_is_refused_before_anything_is_sent()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var task = NewTask(TaskType_MifareDesfireTask.PICCMasterKeyChangeover);
                task.KeyBindings = new[] { Sam(SecureKeySlots.DesfirePiccCurrent, Entry2) };
                var reader = new KeyRecordingReader { SupportsSam = true };

                var run = await RunAsync(task, reader);

                Assert.Equal(ERROR.KeyUnavailable, run.Error);
                Assert.Empty(reader.Calls);
                Assert.Empty(reader.SamChanges);
                Assert.Contains(ResourceLoader.GetResource("textBoxStatusSamMixedKeys").Substring(5), run.Status, StringComparison.Ordinal);
            });
        }

        // ── what the reader answers is what the task ends with ──

        public static IEnumerable<object[]> Answers() => new[]
        {
            new object[] { ERROR.NoError, SamPiccFailure.None, false },
            new object[] { ERROR.AuthFailure, SamPiccFailure.CardRefused, false },
            new object[] { ERROR.PermissionDenied, SamPiccFailure.CardRefused, false },
            new object[] { ERROR.KeyUnavailable, SamPiccFailure.SamRefused, false },
            new object[] { ERROR.KeyUnavailable, SamPiccFailure.NotConfigured, false },
            new object[] { ERROR.TransportError, SamPiccFailure.NoCard, false },
            new object[] { ERROR.Unknown, SamPiccFailure.ChangeUnverified, true },
        };

        [Theory]
        [MemberData(nameof(Answers))]
        public async Task The_answer_of_the_reader_is_the_error_of_the_task_and_a_key_that_may_have_changed_is_said(ERROR error, SamPiccFailure failure, bool mayHaveChanged)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var task = NewTask(TaskType_MifareDesfireTask.PICCMasterKeyChangeover);
                task.KeyBindings = new[] { Sam(SecureKeySlots.DesfirePiccCurrent, Entry2), Sam(SecureKeySlots.DesfirePiccNew, Entry1) };
                var reader = new KeyRecordingReader { SupportsSam = true, SamAnswer = new SamPiccResult(error, failure, 0x6A82, 0x91AE, mayHaveChanged) };

                var run = await RunAsync(task, reader);

                Assert.Equal(error, run.Error);
                Assert.Equal(error == ERROR.NoError, task.IsTaskCompletedSuccessfully);
                Assert.Equal(mayHaveChanged, run.Status.Contains(ResourceLoader.GetResource("textBoxStatusSamKeyMayHaveChanged").Substring(5), StringComparison.Ordinal));

                if (failure == SamPiccFailure.NotConfigured)
                {
                    Assert.Contains(ResourceLoader.GetResource("textBoxStatusSamNotConfigured").Substring(5), run.Status, StringComparison.Ordinal);
                }
                else if (error != ERROR.NoError)
                {
                    // The reason and the status words, and nothing that names a key.
                    Assert.Contains(failure.ToString(), run.Status, StringComparison.Ordinal);
                    Assert.Contains("6A82", run.Status, StringComparison.Ordinal);
                    Assert.Contains("91AE", run.Status, StringComparison.Ordinal);
                }

                Assert.DoesNotContain("2:0", run.Status, StringComparison.Ordinal);
                Assert.DoesNotContain("1:0", run.Status, StringComparison.Ordinal);
                Assert.DoesNotContain(EmbeddedAes, run.Status, StringComparison.OrdinalIgnoreCase);
            });
        }

        // ── the central safety test: only these commands reach the reader with a key of the SAM ──

        [Theory]
        [MemberData(nameof(DesfireKeySlotCharacterizationTests.EveryTaskType), MemberType = typeof(DesfireKeySlotCharacterizationTests))]
        public async Task With_every_slot_bound_to_the_sam_and_a_reader_that_can_only_the_three_commands_reach_the_reader_and_with_references(TaskType_MifareDesfireTask type, string expectedCalls)
        {
            Assert.NotEmpty(expectedCalls);

            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var task = NewTask(type);
                task.KeyBindings = SecureKeySlots.Desfire.Select(slot => Sam(slot, Entry1)).ToArray();
                var reader = new KeyRecordingReader { SupportsSam = true };

                var run = await RunAsync(task, reader);

                // No card command ever carries a typed key, whatever the task, and the typed keys are never read.
                Assert.Empty(reader.Calls);

                var uses = type == TaskType_MifareDesfireTask.AuthenticateApplication
                    || type == TaskType_MifareDesfireTask.PICCMasterKeyChangeover
                    || type == TaskType_MifareDesfireTask.ApplicationKeyChangeover;

                if (uses)
                {
                    Assert.Equal(1, reader.SamAuthentications.Count + reader.SamChanges.Count);
                    Assert.Equal(ERROR.NoError, run.Error);
                }
                else
                {
                    // Every other command stops before the card: the key of a SAM is no key for it.
                    Assert.Empty(reader.SamAuthentications);
                    Assert.Empty(reader.SamChanges);
                    Assert.Equal(ERROR.KeyUnavailable, run.Error);
                }
            });
        }
    }
}
