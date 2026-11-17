#nullable enable
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.AccessControl;
using RFiDGear.Infrastructure.ReaderProviders;
using RFiDGear.Infrastructure.Tasks;
using RFiDGear.Models;
using RFiDGear.Services.Sam;
using RFiDGear.Services.SecureKeys;
using RFiDGear.Tests.KeySlots;
using RFiDGear.ViewModel.TaskSetupViewModels;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Serialization;

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
        [InlineData("0", false)]
        [InlineData("3", false)]
        [InlineData("1", true)]
        [InlineData("3", true)]
        [InlineData("13", true)]
        public async Task The_authentication_of_an_application_uses_the_key_number_that_the_card_command_uses(string chosenKeyNumber, bool changeKeyWithTheTargetedKey)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                // The key number that authenticates is the chosen one only when the application says that a key is changed with itself: otherwise
                // it is key 0. Both cases are run, so that a key number that is always 0 is seen.
                var setting = changeKeyWithTheTargetedKey ? AccessCondition_MifareDesfireAppCreation.ChangeKeyUsingKeyNo : AccessCondition_MifareDesfireAppCreation.ChangeKeyUsingMK;

                var typed = NewTask(TaskType_MifareDesfireTask.AuthenticateApplication);
                typed.SelectedDesfireAppKeyNumberCurrent = chosenKeyNumber;
                typed.SelectedDesfireAppKeySettingsCreateNewApp = setting;
                var baseline = new KeyRecordingReader();
                await RunAsync(typed, baseline);

                var task = NewTask(TaskType_MifareDesfireTask.AuthenticateApplication);
                task.SelectedDesfireAppKeyNumberCurrent = chosenKeyNumber;
                task.SelectedDesfireAppKeySettingsCreateNewApp = setting;
                task.KeyBindings = new[] { Sam(SecureKeySlots.DesfireAppCurrent, Entry3) };
                var reader = new KeyRecordingReader { SupportsSam = true };

                await RunAsync(task, reader);

                var expected = (byte)(changeKeyWithTheTargetedKey ? int.Parse(chosenKeyNumber, System.Globalization.CultureInfo.InvariantCulture) : 0);
                Assert.Equal(expected, (byte)baseline.LastAuthKeyNumber);
                Assert.Equal(expected, Assert.Single(reader.SamAuthentications).KeyNumber);
            });
        }

        [Theory]
        [InlineData(TaskType_MifareDesfireTask.AuthenticateApplication, "14")]
        [InlineData(TaskType_MifareDesfireTask.AuthenticateApplication, "256")]
        [InlineData(TaskType_MifareDesfireTask.AuthenticateApplication, "257")]
        [InlineData(TaskType_MifareDesfireTask.ApplicationKeyChangeover, "14")]
        [InlineData(TaskType_MifareDesfireTask.ApplicationKeyChangeover, "256")]
        [InlineData(TaskType_MifareDesfireTask.ApplicationKeyChangeover, "257")]
        [InlineData(TaskType_MifareDesfireTask.AuthenticateApplication, "2147483648")]
        [InlineData(TaskType_MifareDesfireTask.AuthenticateApplication, "not a number")]
        [InlineData(TaskType_MifareDesfireTask.ApplicationKeyChangeover, "2147483648")]
        [InlineData(TaskType_MifareDesfireTask.ApplicationKeyChangeover, "not a number")]
        public async Task A_key_number_that_the_card_does_not_have_is_refused_before_the_reader_and_never_wraps_to_another_key(TaskType_MifareDesfireTask type, string chosenKeyNumber)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                // With the setting that changes a key with the targeted key number the chosen number is the key that authenticates, and for the
                // change it is the key that is replaced: a number of a project that was edited by hand (256 is key 0 as a byte) is no key.
                var task = NewTask(type);
                task.SelectedDesfireAppKeyNumberCurrent = chosenKeyNumber;
                task.SelectedDesfireAppKeySettingsCreateNewApp = AccessCondition_MifareDesfireAppCreation.ChangeKeyUsingKeyNo;
                task.KeyBindings = type == TaskType_MifareDesfireTask.AuthenticateApplication
                    ? new[] { Sam(SecureKeySlots.DesfireAppCurrent, Entry3) }
                    : new[] { Sam(SecureKeySlots.DesfireAppCurrent, Entry3), Sam(SecureKeySlots.DesfireAppNew, Entry4) };
                var reader = new KeyRecordingReader { SupportsSam = true };

                var run = await RunAsync(task, reader);

                Assert.Equal(ERROR.ProtocolConstraint, run.Error);
                Assert.Empty(reader.SamAuthentications);
                Assert.Empty(reader.SamChanges);
                Assert.Empty(reader.Calls);
            });
        }

        [Theory]
        [InlineData(TaskType_MifareDesfireTask.AuthenticateApplication)]
        [InlineData(TaskType_MifareDesfireTask.ApplicationKeyChangeover)]
        public async Task The_line_names_the_application_that_the_operation_was_made_for_whatever_the_dialog_says_meanwhile(TaskType_MifareDesfireTask type)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                // The dialog stays editable while the SAM and the card work: the application number that a person types in the meantime is
                // not the application that was authenticated or changed, and the line must not say so.
                var task = NewTask(type);
                task.KeyBindings = type == TaskType_MifareDesfireTask.AuthenticateApplication
                    ? new[] { Sam(SecureKeySlots.DesfireAppCurrent, Entry3) }
                    : new[] { Sam(SecureKeySlots.DesfireAppCurrent, Entry3), Sam(SecureKeySlots.DesfireAppNew, Entry4) };
                var reader = new KeyRecordingReader { SupportsSam = true, WhileASamOperationRuns = () => task.AppNumberCurrent = "7" };

                var run = await RunAsync(task, reader);
                var messages = StatusLines.WithoutTimestamps(run.Status);

                Assert.Equal(ERROR.NoError, run.Error);
                Assert.Equal(1u, reader.SamAuthentications.Select(access => access.AppId).Concat(reader.SamChanges.Select(change => change.AppId)).Single());
                Assert.Matches(@"AppID 1\b", messages);
                Assert.DoesNotContain("AppID 7", messages, StringComparison.Ordinal);
            });
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task A_list_that_is_filled_again_writes_nothing_and_changes_nothing_about_the_key_number(string? written)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                // A list writes null or empty while its items are replaced, and then the choice again; when it does not, the number that is
                // shown (and saved) is still the one the task was run with, and neither the number nor its being a number may change.
                var task = NewTask(TaskType_MifareDesfireTask.AuthenticateApplication);
                task.SelectedDesfireAppKeySettingsCreateNewApp = AccessCondition_MifareDesfireAppCreation.ChangeKeyUsingKeyNo;
                task.SelectedDesfireAppKeyNumberCurrent = "3";
                task.SelectedDesfireAppKeyNumberCurrent = written!;
                task.KeyBindings = new[] { Sam(SecureKeySlots.DesfireAppCurrent, Entry3) };
                var reader = new KeyRecordingReader { SupportsSam = true };

                var run = await RunAsync(task, reader);

                Assert.Equal("3", task.SelectedDesfireAppKeyNumberCurrent);
                Assert.Equal(ERROR.NoError, run.Error);
                Assert.Equal((byte)3, Assert.Single(reader.SamAuthentications).KeyNumber);
            });
        }

        [Theory]
        [InlineData("2147483648")]
        [InlineData("not a number")]
        public async Task A_key_number_that_was_not_read_is_still_not_read_after_the_task_was_saved_loaded_or_copied(string text)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                // The text that the person (or an edited file) gave is kept, so that whatever rebuilds the task from it (the project file, the
                // copy that the edit dialog makes from the public properties) rebuilds the refusal too and not a key 0 or the number before.
                var task = NewTask(TaskType_MifareDesfireTask.AuthenticateApplication);
                task.SelectedDesfireAppKeyNumberCurrent = "1";
                task.SelectedDesfireAppKeyNumberCurrent = text;
                task.SelectedDesfireAppKeySettingsCreateNewApp = AccessCondition_MifareDesfireAppCreation.ChangeKeyUsingKeyNo;
                task.KeyBindings = new[] { Sam(SecureKeySlots.DesfireAppCurrent, Entry3) };

                Assert.Equal(text, task.SelectedDesfireAppKeyNumberCurrent);

                var project = new ChipTaskHandlerModel();
                project.TaskCollection.Add(task);
                string xml;

                using (var writer = new StringWriter())
                {
                    new XmlSerializer(typeof(ChipTaskHandlerModel)).Serialize(writer, project);
                    xml = writer.ToString();
                }

                MifareDesfireSetupViewModel loaded;

                using (var reader = new StringReader(xml))
                {
                    var read = (ChipTaskHandlerModel)new XmlSerializer(typeof(ChipTaskHandlerModel)).Deserialize(reader)!;
                    loaded = Assert.IsType<MifareDesfireSetupViewModel>(Assert.Single(read.TaskCollection));
                }

                // What the edit dialog does with the public properties of a task.
                var copy = NewTask(TaskType_MifareDesfireTask.AuthenticateApplication);
                copy.SelectedDesfireAppKeySettingsCreateNewApp = AccessCondition_MifareDesfireAppCreation.ChangeKeyUsingKeyNo;
                copy.KeyBindings = new[] { Sam(SecureKeySlots.DesfireAppCurrent, Entry3) };
                copy.SelectedDesfireAppKeyNumberCurrent = task.SelectedDesfireAppKeyNumberCurrent;

                foreach (var candidate in new[] { loaded, copy })
                {
                    Assert.Equal(text, candidate.SelectedDesfireAppKeyNumberCurrent);

                    var reader = new KeyRecordingReader { SupportsSam = true };
                    var run = await RunAsync(candidate, reader);

                    Assert.Equal(ERROR.ProtocolConstraint, run.Error);
                    Assert.Empty(reader.SamAuthentications);
                }
            });
        }

        [Theory]
        [InlineData("14")]
        [InlineData("256")]
        [InlineData("257")]
        [InlineData("2147483648")]
        [InlineData("not a number")]
        public async Task The_key_that_is_replaced_is_checked_too_when_another_key_authenticates(string chosenKeyNumber)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                // With the master key policy key 0 authenticates (a number that exists) and the chosen key is the one that is replaced.
                var task = NewTask(TaskType_MifareDesfireTask.ApplicationKeyChangeover);
                task.SelectedDesfireAppKeyNumberCurrent = chosenKeyNumber;
                task.SelectedDesfireAppKeySettingsCreateNewApp = AccessCondition_MifareDesfireAppCreation.ChangeKeyUsingMK;
                task.DesfireAppKeyCurrentOld = "E1E1E1E1E1E1E1E1E1E1E1E1E1E1E1E1";
                task.KeyBindings = new[] { Sam(SecureKeySlots.DesfireAppCurrent, Entry3), Sam(SecureKeySlots.DesfireAppOld, Entry2), Sam(SecureKeySlots.DesfireAppNew, Entry4) };
                var reader = new KeyRecordingReader { SupportsSam = true };

                var run = await RunAsync(task, reader);

                Assert.Equal(ERROR.ProtocolConstraint, run.Error);
                Assert.Empty(reader.SamChanges);
                Assert.Empty(reader.Calls);
            });
        }

        [Theory]
        [InlineData(TaskType_MifareDesfireTask.ReadAppSettings)]
        [InlineData(TaskType_MifareDesfireTask.CheckAppKeyCount)]
        [InlineData(TaskType_MifareDesfireTask.CheckFileSettings)]
        public async Task The_check_tasks_that_share_the_authentication_refuse_a_key_of_the_sam_and_send_nothing(TaskType_MifareDesfireTask type)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                // They authenticate with the same method as the task that can use a key of the SAM, from the button of the dialog.
                var task = NewTask(type);
                task.KeyBindings = new[] { Sam(SecureKeySlots.DesfireAppCurrent, Entry3) };
                var reader = new KeyRecordingReader { SupportsSam = true };

                using (ReaderDeviceScope.Use(reader))
                {
                    await task.AuthenticateToCardApplicationCommand.ExecuteAsync(null);
                }

                Assert.Equal(ERROR.KeyUnavailable, task.CurrentTaskErrorLevel);
                Assert.Empty(reader.SamAuthentications);
                Assert.Empty(reader.Calls);
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

        [Fact]
        public void The_time_that_starts_a_line_of_the_status_is_not_part_of_what_is_looked_at()
        {
            // 11:05 holds the text of a reference, 1:0; the message does not.
            var status = "16.11.2026 11:05:09: The operation did not work.\n11/16/2026 1:05:09 PM: The card may hold the key.\n";

            Assert.Contains("1:0", status, StringComparison.Ordinal);
            Assert.DoesNotContain("1:0", StatusLines.WithoutTimestamps(status), StringComparison.Ordinal);
            Assert.Equal("The operation did not work.\nThe card may hold the key.\n", StatusLines.WithoutTimestamps(status));
            Assert.Equal("a line without a time: 1:0", StatusLines.WithoutTimestamps("a line without a time: 1:0"));
        }

        // ── what the reader answers is what the task ends with ──

        public static IEnumerable<object[]> Answers() => new[]
        {
            new object[] { ERROR.NoError, SamPiccFailure.None, false },
            new object[] { ERROR.AuthFailure, SamPiccFailure.CardRefused, false },
            new object[] { ERROR.PermissionDenied, SamPiccFailure.CardRefused, false },
            new object[] { ERROR.KeyUnavailable, SamPiccFailure.SamRefused, false },
            new object[] { ERROR.KeyUnavailable, SamPiccFailure.NotConfigured, false },
            new object[] { ERROR.KeyUnavailable, SamPiccFailure.SamNotFound, false },
            new object[] { ERROR.KeyUnavailable, SamPiccFailure.SamAmbiguous, false },
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
                else if (failure == SamPiccFailure.SamNotFound)
                {
                    Assert.Contains(ResourceLoader.GetResource("textBoxStatusSamNotFound").Substring(5), run.Status, StringComparison.Ordinal);
                }
                else if (failure == SamPiccFailure.SamAmbiguous)
                {
                    Assert.Contains(ResourceLoader.GetResource("textBoxStatusSamAmbiguous").Substring(5), run.Status, StringComparison.Ordinal);
                }
                else if (error != ERROR.NoError)
                {
                    // The reason and the status words, and nothing that names a key.
                    Assert.Contains(failure.ToString(), run.Status, StringComparison.Ordinal);
                    Assert.Contains("6A82", run.Status, StringComparison.Ordinal);
                    Assert.Contains("91AE", run.Status, StringComparison.Ordinal);
                }

                var messages = StatusLines.WithoutTimestamps(run.Status);
                Assert.DoesNotContain("2:0", messages, StringComparison.Ordinal);
                Assert.DoesNotContain("1:0", messages, StringComparison.Ordinal);
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
