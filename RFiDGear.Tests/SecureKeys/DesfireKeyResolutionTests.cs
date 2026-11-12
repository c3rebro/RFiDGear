using RFiDGear.Contracts;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.AccessControl;
using RFiDGear.Infrastructure.ExtensionHost;
using RFiDGear.Infrastructure.FileAccess;
using RFiDGear.Infrastructure.Tasks;
using RFiDGear.Models;
using RFiDGear.Services.SecureKeys;
using RFiDGear.Services.TaskExecution;
using RFiDGear.Tests.KeySlots;
using RFiDGear.ViewModel.TaskSetupViewModels;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Serilog.Events;

using Xunit;

namespace RFiDGear.Tests.SecureKeys
{
    /// <summary>
    /// A DESFire task whose key slots are bound to a provider gets every key from the provider, before the card is touched,
    /// and never from the key it still holds in the project. The embedded keys and the keys of the vault are different
    /// values in every test, so a call to the reader says which of the two it carries. Every task type is run with all its
    /// slots bound.
    /// </summary>
    public class DesfireKeyResolutionTests : IDisposable
    {
        private const string Provider = "test.vault";

        // What a legacy project holds in each slot, and what the vault holds. Different, so a key on the card line shows
        // where it came from.
        private static readonly Dictionary<string, (string Short, string Embedded, string Vault)> Slots = new Dictionary<string, (string, string, string)>
        {
            [SecureKeySlots.DesfirePiccCurrent] = ("MasterCurrent", Repeat(0xE0), Repeat(0xB0)),
            [SecureKeySlots.DesfirePiccNew] = ("MasterTarget", Repeat(0xE1), Repeat(0xB1)),
            [SecureKeySlots.DesfireAppCurrent] = ("AppCurrent", Repeat(0xE2), Repeat(0xB2)),
            [SecureKeySlots.DesfireAppOld] = ("AppCurrentOld", Repeat(0xE3), Repeat(0xB3)),
            [SecureKeySlots.DesfireAppNew] = ("AppTarget", Repeat(0xE4), Repeat(0xB4)),
            [SecureKeySlots.DesfireRead] = ("Read", Repeat(0xE5), Repeat(0xB5)),
            [SecureKeySlots.DesfireWrite] = ("Write", Repeat(0xE6), Repeat(0xB6)),
        };

        public DesfireKeyResolutionTests()
        {
            KeyResolutionScope.SetDefault(null);
        }

        public void Dispose()
        {
            KeyResolutionScope.SetDefault(null);
        }

        private static string Repeat(byte value) => string.Concat(Enumerable.Repeat(value.ToString("X2"), 16));

        // ── Every task type ──

        [Theory]
        [MemberData(nameof(DesfireKeySlotCharacterizationTests.EveryTaskType), MemberType = typeof(DesfireKeySlotCharacterizationTests))]
        public async Task With_every_slot_bound_a_task_works_with_the_keys_of_the_vault_only(TaskType_MifareDesfireTask type, string expectedCalls)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var vault = new Vault();
                vault.Install();
                var task = NewTask(type);
                BindAll(task);

                var run = await RunAsync(task);

                // The same calls with the same slots as for embedded keys, but each key is the vault's: an embedded key
                // would show up as "embedded:..." in this line.
                Assert.Equal(expectedCalls, run.Calls);
                Assert.DoesNotContain("embedded:", run.Calls, StringComparison.Ordinal);
                Assert.DoesNotContain(vault.Requests, request => request.Interactive);
            });
        }

        [Theory]
        [MemberData(nameof(DesfireKeySlotCharacterizationTests.EveryTaskType), MemberType = typeof(DesfireKeySlotCharacterizationTests))]
        public async Task A_project_that_moved_to_the_provider_has_no_embedded_keys_left_and_still_works(TaskType_MifareDesfireTask type, string expectedCalls)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                new Vault().Install();
                var task = NewTask(type);
                BindAll(task);

                // As the project looks after the keys were moved: the reference is there, the hexadecimal text is not.
                task.DesfireMasterKeyCurrent = string.Empty;
                task.DesfireMasterKeyTarget = string.Empty;
                task.DesfireAppKeyCurrent = string.Empty;
                task.DesfireAppKeyCurrentOld = string.Empty;
                task.DesfireAppKeyTarget = string.Empty;
                task.DesfireReadKeyCurrent = string.Empty;
                task.DesfireWriteKeyCurrent = string.Empty;

                var run = await RunAsync(task);

                Assert.Equal(expectedCalls, run.Calls);
            });
        }

        [Theory]
        [MemberData(nameof(DesfireKeySlotCharacterizationTests.EveryTaskType), MemberType = typeof(DesfireKeySlotCharacterizationTests))]
        public async Task A_task_without_bindings_never_asks_a_provider_and_works_as_before(TaskType_MifareDesfireTask type, string expectedCalls)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var vault = new Vault();
                vault.Install();
                var task = NewTask(type);

                var run = await RunAsync(task);

                Assert.Equal(expectedCalls.Replace("[", "[embedded:").Replace(",", ",embedded:"), run.Calls);
                Assert.Empty(vault.Requests);
            });
        }

        // ── A key that cannot be obtained ──

        [Theory]
        [MemberData(nameof(DesfireKeySlotCharacterizationTests.EveryTaskType), MemberType = typeof(DesfireKeySlotCharacterizationTests))]
        public async Task A_key_that_cannot_be_obtained_stops_every_task_before_the_card_is_touched(TaskType_MifareDesfireTask type, string expectedCalls)
        {
            Assert.NotEmpty(expectedCalls);

            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var vault = new Vault { Failure = SecureKeyFailureReason.NotFound };
                vault.Install();
                var task = NewTask(type);
                BindAll(task);

                var run = await RunAsync(task);

                Assert.Equal(string.Empty, run.Calls);
                Assert.Equal(ERROR.KeyUnavailable, run.Error);
                Assert.Contains("desfire.", run.Status, StringComparison.Ordinal);
                AssertNothingSecretIn(run.Status);
            });
        }

        [Theory]
        [InlineData(SecureKeyFailureReason.NotFound)]
        [InlineData(SecureKeyFailureReason.NotAuthorized)]
        [InlineData(SecureKeyFailureReason.ProviderUnavailable)]
        [InlineData(SecureKeyFailureReason.Timeout)]
        [InlineData(SecureKeyFailureReason.Cancelled)]
        [InlineData(SecureKeyFailureReason.InvalidKey)]
        [InlineData(SecureKeyFailureReason.Unsupported)]
        public async Task Whatever_the_reason_the_task_ends_with_key_unavailable_and_no_card_call(SecureKeyFailureReason reason)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                new Vault { Failure = reason }.Install();
                var task = NewTask(TaskType_MifareDesfireTask.ReadData);
                task.KeyBindings = new[] { Bind(SecureKeySlots.DesfireRead) };

                var run = await RunAsync(task);

                Assert.Equal(string.Empty, run.Calls);
                Assert.Equal(ERROR.KeyUnavailable, run.Error);
            });
        }

        [Theory]
        [InlineData(TaskType_MifareDesfireTask.ApplicationKeyChangeover, "desfire.app.new")]
        [InlineData(TaskType_MifareDesfireTask.PICCMasterKeyChangeover, "desfire.picc.new")]
        public async Task A_new_key_that_cannot_be_obtained_costs_the_card_nothing_although_the_other_key_was(TaskType_MifareDesfireTask type, string failingSlot)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var vault = new Vault { FailSlots = { failingSlot } };
                vault.Install();
                var task = NewTask(type);
                BindAll(task);

                var run = await RunAsync(task);

                // The authenticating key was obtained, and not even that reached the card: no authentication, no key change.
                Assert.Contains(vault.Requests, request => request.SlotId != failingSlot);
                Assert.Equal(string.Empty, run.Calls);
                Assert.Equal(ERROR.KeyUnavailable, run.Error);
            });
        }

        [Fact]
        public async Task A_provider_nobody_installed_is_a_failure_and_never_the_embedded_key()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                new Vault().Install();
                var task = NewTask(TaskType_MifareDesfireTask.ReadData);
                task.KeyBindings = new[] { new KeyBindingEntry { Slot = SecureKeySlots.DesfireRead, ProviderId = "nobody.here", Reference = "ref/read" } };

                var run = await RunAsync(task);

                Assert.Equal(string.Empty, run.Calls);
                Assert.Equal(ERROR.KeyUnavailable, run.Error);
            });
        }

        [Fact]
        public async Task A_bound_slot_with_no_way_to_ask_is_a_failure_and_never_the_embedded_key()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                // No context at all: nothing set up a registry.
                var task = NewTask(TaskType_MifareDesfireTask.ReadData);
                task.KeyBindings = new[] { Bind(SecureKeySlots.DesfireRead) };

                var run = await RunAsync(task);

                Assert.Equal(string.Empty, run.Calls);
                Assert.Equal(ERROR.KeyUnavailable, run.Error);
            });
        }

        [Fact]
        public async Task A_key_of_the_wrong_length_for_its_type_is_refused()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var vault = new Vault { KeyLength = 15 };
                vault.Install();
                var task = NewTask(TaskType_MifareDesfireTask.ReadData);
                task.KeyBindings = new[] { Bind(SecureKeySlots.DesfireRead) };

                var run = await RunAsync(task);

                Assert.Equal(string.Empty, run.Calls);
                Assert.Equal(ERROR.KeyUnavailable, run.Error);
            });
        }

        [Fact]
        public async Task A_3K3DES_key_has_to_be_24_bytes()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var vault = new Vault { KeyLength = 24 };
                vault.Install();
                var task = NewTask(TaskType_MifareDesfireTask.ReadData);
                task.SelectedDesfireReadKeyEncryptionType = DESFireKeyType.DF_KEY_3K3DES;
                task.KeyBindings = new[] { Bind(SecureKeySlots.DesfireRead) };

                var run = await RunAsync(task);

                Assert.NotEqual(string.Empty, run.Calls);
                Assert.Equal(SecureKeyAlgorithm.TripleDes3K, Assert.Single(vault.Requests).Algorithm);
                Assert.Equal(48, Assert.Single(vault.ReturnedKeys).Length);
            });
        }

        [Fact]
        public async Task A_broken_binding_anywhere_in_the_task_stops_it_even_if_the_command_does_not_use_that_slot()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var vault = new Vault();
                vault.Install();
                var task = NewTask(TaskType_MifareDesfireTask.ReadData);
                task.KeyBindings = new[]
                {
                    Bind(SecureKeySlots.DesfireRead),
                    new KeyBindingEntry { Slot = "desfire.aap.current", ProviderId = Provider, Reference = "ref/typo" },
                };

                var run = await RunAsync(task);

                // A typo in a slot id would otherwise leave the embedded key in use that the person meant to replace.
                Assert.Equal(string.Empty, run.Calls);
                Assert.Equal(ERROR.KeyUnavailable, run.Error);
                Assert.Empty(vault.Requests);
            });
        }

        // ── Once per attempt, with the right circumstances ──

        [Theory]
        [InlineData(TaskType_MifareDesfireTask.FormatDesfireCard, 1)]
        [InlineData(TaskType_MifareDesfireTask.DeleteFile, 1)]
        [InlineData(TaskType_MifareDesfireTask.CheckFileSettings, 1)]
        [InlineData(TaskType_MifareDesfireTask.PICCMasterKeyChangeover, 2)]
        [InlineData(TaskType_MifareDesfireTask.ApplicationKeyChangeover, 2)]
        public async Task Each_slot_is_asked_for_once_however_often_the_command_uses_the_key(TaskType_MifareDesfireTask type, int expectedRequests)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var vault = new Vault();
                vault.Install();
                var task = NewTask(type);
                BindAll(task);

                await RunAsync(task);

                Assert.Equal(expectedRequests, vault.Requests.Count);
                Assert.Equal(vault.Requests.Count, vault.Requests.Select(request => request.SlotId).Distinct().Count());
            });
        }

        [Fact]
        public async Task The_retry_of_a_refused_application_creation_does_not_ask_again()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var vault = new Vault();
                vault.Install();
                var task = NewTask(TaskType_MifareDesfireTask.CreateApplication);
                BindAll(task);
                var reader = new KeyRecordingReader { FailMethod = "CreateMifareDesfireApplication", FailWith = ERROR.AuthFailure };

                var run = await RunAsync(task, reader);

                Assert.Equal(2, reader.CallsOf("CreateMifareDesfireApplication").Count + reader.CallsOf("CreateMifareDesfireApplicationWithoutAuth").Count);
                Assert.Single(vault.Requests);
                Assert.Equal("MasterCurrent", NamesIn(run.Calls).Distinct().Single());
            });
        }

        [Fact]
        public async Task A_format_that_goes_on_after_a_failed_listing_uses_the_key_of_the_vault_too()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                new Vault().Install();
                var format = NewTask(TaskType_MifareDesfireTask.FormatDesfireCard);
                BindAll(format);

                var formatted = await RunAsync(format, new KeyRecordingReader { FailMethod = "GetMiFareDESFireChipAppIDs", FailWith = ERROR.TransportError });

                Assert.Equal("AuthToMifareDesfireApplication[MasterCurrent] | GetMiFareDESFireChipAppIDs[MasterCurrent] | FormatDesfireCard[MasterCurrent]", formatted.Calls);
            });
        }

        [Fact]
        public async Task A_bound_key_that_does_not_authenticate_to_the_application_is_not_tried_on_another_one()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                new Vault().Install();
                var delete = NewTask(TaskType_MifareDesfireTask.DeleteFile);
                BindAll(delete);

                // The embedded key would go on and try to delete in the other application. The key of a provider was asked
                // for this application only.
                var deleted = await RunAsync(delete, new KeyRecordingReader { FailMethod = "AuthToMifareDesfireApplication", FailWith = ERROR.AuthFailure });

                Assert.Equal("AuthToMifareDesfireApplication[AppCurrent]", deleted.Calls);
                Assert.Equal(ERROR.AuthFailure, deleted.Error);
            });
        }

        [Fact]
        public async Task An_embedded_key_that_does_not_authenticate_still_goes_on_to_the_other_application_as_before()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var delete = NewTask(TaskType_MifareDesfireTask.DeleteFile);

                var deleted = await RunAsync(delete, new KeyRecordingReader { FailMethod = "AuthToMifareDesfireApplication", FailWith = ERROR.AuthFailure });

                Assert.Equal("AuthToMifareDesfireApplication[embedded:AppCurrent] | DeleteMifareDesfireFile[embedded:AppCurrent]", deleted.Calls);
            });
        }

        [Fact]
        public async Task The_scope_a_provider_is_told_is_where_the_card_operation_really_authenticates()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var vault = new Vault();
                vault.Install();

                // Key number 3 is chosen for the checks. Creating or deleting a file authenticates with key 0 of the
                // application in both readers, and the directory is read at card level.
                var create = NewTask(TaskType_MifareDesfireTask.CreateFile);
                create.SelectedDesfireAppKeyNumberCurrent = "3";
                create.KeyBindings = new[] { Bind(SecureKeySlots.DesfireAppCurrent) };
                var delete = NewTask(TaskType_MifareDesfireTask.DeleteFile);
                delete.SelectedDesfireAppKeyNumberCurrent = "3";
                delete.KeyBindings = new[] { Bind(SecureKeySlots.DesfireAppCurrent) };
                var exists = NewTask(TaskType_MifareDesfireTask.AppExistCheck);
                exists.SelectedDesfireAppKeyNumberCurrent = "3";
                exists.KeyBindings = new[] { Bind(SecureKeySlots.DesfireAppCurrent) };
                var deleteReader = new KeyRecordingReader();

                await RunAsync(create);
                await RunAsync(delete, deleteReader);
                await RunAsync(exists);

                Assert.Equal(
                    new (uint?, int?)[] { (1, 0), (1, 0), (0, 0) },
                    vault.Requests.Select(request => (request.Scope.ApplicationId, request.Scope.KeyNumber)));

                // And the preliminary authentication of the delete uses that key number too, for a bound key.
                Assert.Equal(0, deleteReader.LastAuthKeyNumber);
            });
        }

        [Fact]
        public async Task An_embedded_key_keeps_the_key_number_it_always_had_for_the_preliminary_authentication()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var delete = NewTask(TaskType_MifareDesfireTask.DeleteFile);
                delete.SelectedDesfireAppKeyNumberCurrent = "3";
                var reader = new KeyRecordingReader();

                await RunAsync(delete, reader);

                Assert.Equal(3, reader.LastAuthKeyNumber);
            });
        }

        [Fact]
        public async Task A_cancelled_run_asks_for_no_key_and_the_command_makes_no_card_call()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var cancel = new CancellationTokenSource();
                cancel.Cancel();
                var vault = new Vault();
                vault.Install(cancellationToken: cancel.Token);
                var task = NewTask(TaskType_MifareDesfireTask.ReadData);
                task.KeyBindings = new[] { Bind(SecureKeySlots.DesfireRead) };

                var run = await RunAsync(task);

                Assert.Equal(string.Empty, run.Calls);
                Assert.Equal(ERROR.KeyUnavailable, run.Error);
                Assert.Empty(vault.Requests);
            });
        }

        [Fact]
        public async Task A_run_cancelled_while_the_provider_answers_goes_on_to_no_card_call()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var cancel = new CancellationTokenSource();
                var vault = new Vault { OnRequest = _ => cancel.Cancel() };
                vault.Install(cancellationToken: cancel.Token);
                var task = NewTask(TaskType_MifareDesfireTask.ReadData);
                task.KeyBindings = new[] { Bind(SecureKeySlots.DesfireRead) };

                var run = await RunAsync(task);

                Assert.Single(vault.Requests);
                Assert.Equal(string.Empty, run.Calls);
                Assert.Equal(ERROR.KeyUnavailable, run.Error);
            });
        }

        [Fact]
        public async Task A_run_hands_its_cancellation_to_the_providers()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var cancel = new CancellationTokenSource();
                var seen = CancellationToken.None;
                var vault = new Vault { OnRequest = _ => seen = KeyResolutionScope.Current!.CancellationToken };
                var task = NewTask(TaskType_MifareDesfireTask.ReadData);
                task.KeyBindings = new[] { Bind(SecureKeySlots.DesfireRead) };

                using (ReaderDeviceScope.Use(new KeyRecordingReader()))
                {
                    await ExecuteAsync(task, new TaskExecutionService(new ReaderDeviceProvider(), new NullDispatcherTimerAdapter(), new NullDispatcherTimerAdapter(), null, vault.Registry()), cancel.Token);
                }

                Assert.Equal(cancel.Token, seen);
                Assert.True(seen.CanBeCanceled);
            });
        }

        [Fact]
        public async Task A_lease_the_provider_already_disposed_is_an_unusable_key_and_not_an_exception()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var vault = new Vault { ReturnDisposedLease = true };
                vault.Install();
                var task = NewTask(TaskType_MifareDesfireTask.ReadData);
                task.KeyBindings = new[] { Bind(SecureKeySlots.DesfireRead) };

                var run = await RunAsync(task);

                Assert.Equal(string.Empty, run.Calls);
                Assert.Equal(ERROR.KeyUnavailable, run.Error);
            });
        }

        [Fact]
        public async Task The_dialog_changing_while_a_provider_answers_cannot_change_which_keys_the_command_uses()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var task = NewTask(TaskType_MifareDesfireTask.ApplicationKeyChangeover);
                BindAll(task);

                // Key number 1 shows the old-key inputs. It is chosen after the command decided that it needs none, and
                // the command goes on with the keys it asked for.
                var vault = new Vault { OnRequest = _ => task.SelectedDesfireAppKeyNumberCurrent = "1" };
                vault.Install();

                var run = await RunAsync(task);

                Assert.Equal(
                    "AuthToMifareDesfireApplication[AppCurrent] | ChangeMifareDesfireKeyAsync[AppCurrent,AppTarget,AppCurrent] | VerifyMifareDesfireKeyChange[AppTarget]",
                    run.Calls);
            });
        }

        [Fact]
        public async Task The_delete_method_the_reader_is_told_is_the_one_the_key_was_asked_for()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var task = NewTask(TaskType_MifareDesfireTask.DeleteApplication);
                BindAll(task);
                var vault = new Vault { OnRequest = _ => task.SelectedDesfireDeleteAuthMethod = DesfireDeleteAuthMethod.ApplicationMasterKey0 };
                vault.Install();
                var reader = new KeyRecordingReader();

                await RunAsync(task, reader);

                // Asked for the card master key, and told the reader the same, although the dialog changed meanwhile.
                Assert.Equal(DesfireDeleteAuthMethod.PiccMasterKey, reader.LastDeleteAuthMethod);
                Assert.Equal(SecureKeySlots.DesfirePiccCurrent, Assert.Single(vault.Requests).SlotId);
            });
        }

        [Fact]
        public async Task A_key_change_asks_for_the_authenticating_key_the_replaced_key_and_the_new_key_with_their_roles()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var vault = new Vault();
                vault.Install();
                var task = NewTask(TaskType_MifareDesfireTask.ApplicationKeyChangeover);
                task.SelectedDesfireAppKeyNumberCurrent = "1";
                task.DesfireAppKeyCurrentOld = Slots[SecureKeySlots.DesfireAppOld].Embedded;
                BindAll(task);

                var run = await RunAsync(task);

                Assert.Equal(
                    new[]
                    {
                        (SecureKeySlots.DesfireAppCurrent, SecureKeyUsage.Authenticate),
                        (SecureKeySlots.DesfireAppOld, SecureKeyUsage.ChangeKeyCurrent),
                        (SecureKeySlots.DesfireAppNew, SecureKeyUsage.ChangeKeyNew),
                    },
                    vault.Requests.Select(request => (request.SlotId, request.Usage)));
                Assert.Equal(
                    "AuthToMifareDesfireApplication[AppCurrent] | ChangeMifareDesfireKeyAsync[AppCurrentOld,AppTarget,AppCurrent] | VerifyMifareDesfireKeyChange[AppTarget]",
                    run.Calls);
            });
        }

        [Fact]
        public async Task Without_old_key_inputs_the_replaced_key_is_the_authenticating_key_and_is_not_asked_for_twice()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var vault = new Vault();
                vault.Install();
                var task = NewTask(TaskType_MifareDesfireTask.ApplicationKeyChangeover);
                BindAll(task);

                await RunAsync(task);

                Assert.Equal(
                    new[] { SecureKeySlots.DesfireAppCurrent, SecureKeySlots.DesfireAppNew },
                    vault.Requests.Select(request => request.SlotId));
            });
        }

        [Fact]
        public async Task A_provider_is_told_where_the_key_is_used_and_by_what()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var vault = new Vault();
                vault.Install(runId: "run-9", taskIndex: "7", cardUid: "04AABBCCDD");
                var task = NewTask(TaskType_MifareDesfireTask.ReadData);
                task.SelectedDesfireReadKeyNumber = "2";
                task.KeyBindings = new[] { Bind(SecureKeySlots.DesfireRead) };

                await RunAsync(task);

                var request = Assert.Single(vault.Requests);
                Assert.Equal(SecureKeySlots.DesfireRead, request.SlotId);
                Assert.Equal("ref/" + SecureKeySlots.DesfireRead, request.Reference);
                Assert.Equal(SecureKeyAlgorithm.Aes128, request.Algorithm);
                Assert.Equal(SecureKeyUsage.Authenticate, request.Usage);
                Assert.Equal((uint)1, request.Scope.ApplicationId);
                Assert.Equal(2, request.Scope.KeyNumber);
                Assert.Equal("run-9", request.RunId);
                Assert.Equal("7", request.TaskIndex);
                Assert.Equal("04AABBCCDD", request.CardUid);
                Assert.False(request.Interactive);
            });
        }

        [Fact]
        public async Task A_mix_of_bound_and_embedded_slots_uses_each_where_it_belongs()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var vault = new Vault();
                vault.Install();
                var task = NewTask(TaskType_MifareDesfireTask.ApplicationKeyChangeover);
                task.KeyBindings = new[] { Bind(SecureKeySlots.DesfireAppCurrent) };

                var run = await RunAsync(task);

                Assert.Equal(
                    "AuthToMifareDesfireApplication[AppCurrent] | ChangeMifareDesfireKeyAsync[AppCurrent,embedded:AppTarget,AppCurrent] | VerifyMifareDesfireKeyChange[embedded:AppTarget]",
                    run.Calls);
                Assert.Equal(SecureKeySlots.DesfireAppCurrent, Assert.Single(vault.Requests).SlotId);
            });
        }

        [Fact]
        public async Task A_second_task_never_gets_the_key_of_the_first()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var vault = new Vault();
                vault.Install();
                var first = NewTask(TaskType_MifareDesfireTask.ReadData);
                first.KeyBindings = new[] { Bind(SecureKeySlots.DesfireRead) };
                var second = NewTask(TaskType_MifareDesfireTask.ReadData);
                second.KeyBindings = new[] { Bind(SecureKeySlots.DesfireRead, "ref/other") };
                var reader = new KeyRecordingReader();

                await RunAsync(first, reader);
                vault.FailReferences.Add("ref/other");
                var run = await RunAsync(second, reader);

                // The first task authenticated with the vault's key. The second one found nothing, and the reader heard
                // nothing: not the key of the first, not its own embedded key.
                Assert.Equal(ERROR.KeyUnavailable, run.Error);
                var calls = reader.CallsOf("ReadMiFareDESFireChipFile");
                Assert.Single(calls);
                Assert.Equal(Slots[SecureKeySlots.DesfireRead].Vault, calls[0].Keys[0]);
            });
        }

        [Fact]
        public async Task The_same_task_run_again_asks_again_and_keeps_nothing_from_before()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var vault = new Vault();
                vault.Install();
                var task = NewTask(TaskType_MifareDesfireTask.ReadData);
                task.KeyBindings = new[] { Bind(SecureKeySlots.DesfireRead) };
                var reader = new KeyRecordingReader();

                await RunAsync(task, reader);
                vault.Failure = SecureKeyFailureReason.ProviderUnavailable;
                var run = await RunAsync(task, reader);

                Assert.Equal(2, vault.Requests.Count);
                Assert.Equal(ERROR.KeyUnavailable, run.Error);
                Assert.Single(reader.CallsOf("ReadMiFareDESFireChipFile"));
            });
        }

        // ── Nothing secret is shown ──

        [Fact]
        public async Task Neither_the_log_nor_the_status_nor_the_task_ever_shows_a_key_or_a_reference()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var logs = CapturedLogs.Install();
                var vault = new Vault();
                vault.Install();
                var success = NewTask(TaskType_MifareDesfireTask.ApplicationKeyChangeover);
                BindAll(success);
                var failure = NewTask(TaskType_MifareDesfireTask.ReadData);
                failure.KeyBindings = new[] { Bind(SecureKeySlots.DesfireRead, "ref/missing") };
                vault.FailReferences.Add("ref/missing");

                var shown = new List<string>();
                shown.Add((await RunAsync(success)).Status);
                shown.Add((await RunAsync(failure)).Status);

                shown.AddRange(logs.All().Select(CapturedLogs.EverythingWrittenFor));
                foreach (var text in shown)
                {
                    AssertNothingSecretIn(text);
                    Assert.DoesNotContain("ref/", text, StringComparison.Ordinal);
                }
            });
        }

        [Fact]
        public async Task A_provider_that_throws_with_the_key_in_its_message_leaks_nothing()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var logs = CapturedLogs.Install();
                var vault = new Vault { ThrowText = "the vault failed for " + Slots[SecureKeySlots.DesfireRead].Vault };
                vault.Install();
                var task = NewTask(TaskType_MifareDesfireTask.ReadData);
                task.KeyBindings = new[] { Bind(SecureKeySlots.DesfireRead) };

                var run = await RunAsync(task);

                Assert.Equal(ERROR.KeyUnavailable, run.Error);
                AssertNothingSecretIn(run.Status);
                foreach (var logged in logs.All().Select(CapturedLogs.EverythingWrittenFor))
                {
                    AssertNothingSecretIn(logged);
                }
            });
        }

        [Fact]
        public async Task The_payload_of_a_key_change_prints_without_its_keys()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var task = NewTask(TaskType_MifareDesfireTask.ApplicationKeyChangeover);
                var payload = task.BuildAppKeyChangePayload(1, 0, Repeat(0x11), Repeat(0x22), Repeat(0x33), DESFireKeySettings.ConfigurationChangeable);

                var text = payload.ToString();

                Assert.DoesNotContain(Repeat(0x11), text, StringComparison.Ordinal);
                Assert.DoesNotContain(Repeat(0x22), text, StringComparison.Ordinal);
                Assert.DoesNotContain(Repeat(0x33), text, StringComparison.Ordinal);
                Assert.Contains("redacted", text, StringComparison.Ordinal);
            });
        }

        // ── The watchdog ──

        [Fact]
        public async Task The_watchdog_hooks_run_around_the_resolution_once_and_also_when_it_fails()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var started = 0;
                var finished = 0;
                var vault = new Vault();
                vault.Install(resolutionStarted: () => started++, resolutionFinished: () => finished++);
                var task = NewTask(TaskType_MifareDesfireTask.ApplicationKeyChangeover);
                BindAll(task);

                await RunAsync(task);
                Assert.Equal((1, 1), (started, finished));

                vault.FailSlots.Add(SecureKeySlots.DesfireAppNew);
                await RunAsync(task);
                Assert.Equal((2, 2), (started, finished));

                // Nothing bound, nothing asked, nothing paused.
                await RunAsync(NewTask(TaskType_MifareDesfireTask.ReadData));
                Assert.Equal((2, 2), (started, finished));
            });
        }

        [Fact]
        public async Task A_watchdog_hook_that_throws_does_not_stop_the_command()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var vault = new Vault();
                vault.Install(resolutionStarted: () => throw new InvalidOperationException("hook"), resolutionFinished: () => throw new InvalidOperationException("hook"));
                var task = NewTask(TaskType_MifareDesfireTask.ReadData);
                task.KeyBindings = new[] { Bind(SecureKeySlots.DesfireRead) };

                var run = await RunAsync(task);

                Assert.Equal("ReadMiFareDESFireChipFile[Read]", run.Calls);
            });
        }

        [Fact]
        public void A_vault_outage_is_not_an_error_the_fallback_branches_of_a_project_react_to()
        {
            var routed = (IReadOnlyCollection<ERROR>)typeof(TaskExecutionService)
                .GetField("RoutedErrorLevels", BindingFlags.NonPublic | BindingFlags.Static)!
                .GetValue(null)!;

            Assert.DoesNotContain(ERROR.KeyUnavailable, routed);
            Assert.Contains(ERROR.AuthFailure, routed);
        }

        // ── Through the execution service ──

        [Fact]
        public async Task A_run_asks_with_its_own_id_the_task_index_and_the_card_and_pauses_the_watchdog()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var timer = new RecordingTimer();
                var vault = new Vault { OnRequest = _ => timer.Asked.Add((timer.IsEnabled, timer.Events.Count, timer.Events.LastOrDefault())) };
                var registry = vault.Registry();
                var task = NewTask(TaskType_MifareDesfireTask.ReadData);
                task.CurrentTaskIndex = "4";
                task.KeyBindings = new[] { Bind(SecureKeySlots.DesfireRead) };
                var reader = new KeyRecordingReader { Card = new GenericChipModel("04AABBCCDD", CARD_TYPE.EM4102) };

                ExecResult result;
                using (ReaderDeviceScope.Use(reader))
                {
                    result = await ExecuteAsync(task, new TaskExecutionService(new ReaderDeviceProvider(), new NullDispatcherTimerAdapter(), timer, null, registry));
                }

                var request = Assert.Single(vault.Requests);
                Assert.Equal(result.RunId, request.RunId);
                Assert.Equal("4", request.TaskIndex);
                Assert.Equal("04AABBCCDD", request.CardUid);
                Assert.False(request.Interactive);

                // While the provider was asked the watchdog was stopped, and it was started again right after.
                var (enabledWhenAsked, eventsBefore, lastEventBefore) = Assert.Single(timer.Asked);
                Assert.False(enabledWhenAsked);
                Assert.Equal("Stop", lastEventBefore);
                Assert.Contains("Start", timer.Events.Skip(eventsBefore));
                Assert.Equal("ReadMiFareDESFireChipFile[Read]", Describe(reader));
                Assert.Null(KeyResolutionScope.Current);
            });
        }

        [Fact]
        public async Task A_run_whose_key_cannot_be_obtained_reports_key_unavailable_and_counts_as_failed()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var vault = new Vault { Failure = SecureKeyFailureReason.ProviderUnavailable };
                var task = NewTask(TaskType_MifareDesfireTask.ReadData);
                task.KeyBindings = new[] { Bind(SecureKeySlots.DesfireRead) };
                var reader = new KeyRecordingReader();

                ExecResult result;
                using (ReaderDeviceScope.Use(reader))
                {
                    result = await ExecuteAsync(task, new TaskExecutionService(new ReaderDeviceProvider(), new NullDispatcherTimerAdapter(), new RecordingTimer(), null, vault.Registry()));
                }

                var outcome = Assert.Single(result.TaskOutcomes);
                Assert.Equal("KeyUnavailable", outcome.NormalizedErrorCode);
                Assert.False(outcome.IsSuccess);
                Assert.Equal(ExecutionOutcome.Failure, result.Outcome);
                Assert.Empty(reader.Calls);
            });
        }

        [Fact]
        public async Task A_service_without_providers_fails_a_bound_task_and_does_not_fall_back()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var task = NewTask(TaskType_MifareDesfireTask.ReadData);
                task.KeyBindings = new[] { Bind(SecureKeySlots.DesfireRead) };
                var reader = new KeyRecordingReader();

                ExecResult result;
                using (ReaderDeviceScope.Use(reader))
                {
                    result = await ExecuteAsync(task, new TaskExecutionService(new ReaderDeviceProvider(), new NullDispatcherTimerAdapter(), new NullDispatcherTimerAdapter()));
                }

                Assert.Equal("KeyUnavailable", Assert.Single(result.TaskOutcomes).NormalizedErrorCode);
                Assert.Empty(reader.Calls);
            });
        }

        // ── Helpers ──

        private static MifareDesfireSetupViewModel NewTask(TaskType_MifareDesfireTask type) => new MifareDesfireSetupViewModel
        {
            SelectedTaskType = type,
            CurrentTaskIndex = "0",
            AppNumberCurrent = "1",
            AppNumberNew = "2",
            FileNumberCurrent = "2",
            FileSizeCurrent = "16",
            DesfireMasterKeyCurrent = Slots[SecureKeySlots.DesfirePiccCurrent].Embedded,
            DesfireMasterKeyTarget = Slots[SecureKeySlots.DesfirePiccNew].Embedded,
            DesfireAppKeyCurrent = Slots[SecureKeySlots.DesfireAppCurrent].Embedded,
            DesfireAppKeyCurrentOld = Slots[SecureKeySlots.DesfireAppOld].Embedded,
            DesfireAppKeyTarget = Slots[SecureKeySlots.DesfireAppNew].Embedded,
            DesfireReadKeyCurrent = Slots[SecureKeySlots.DesfireRead].Embedded,
            DesfireWriteKeyCurrent = Slots[SecureKeySlots.DesfireWrite].Embedded,
            SelectedDesfireMasterKeyEncryptionTypeCurrent = DESFireKeyType.DF_KEY_AES,
            SelectedDesfireMasterKeyEncryptionTypeTarget = DESFireKeyType.DF_KEY_AES,
            SelectedDesfireAppKeyEncryptionTypeCurrent = DESFireKeyType.DF_KEY_AES,
            SelectedDesfireAppKeyEncryptionTypeTarget = DESFireKeyType.DF_KEY_AES,
            SelectedDesfireReadKeyEncryptionType = DESFireKeyType.DF_KEY_AES,
            SelectedDesfireWriteKeyEncryptionType = DESFireKeyType.DF_KEY_AES,
            SelectedDesfireAppKeyNumberCurrent = "0",
        };

        private static KeyBindingEntry Bind(string slot, string? reference = null) =>
            new KeyBindingEntry { Slot = slot, ProviderId = Provider, Reference = reference ?? "ref/" + slot };

        private static void BindAll(MifareDesfireSetupViewModel task) =>
            task.KeyBindings = Slots.Keys.Select(slot => Bind(slot)).ToArray();

        private sealed record Run(string Calls, ERROR Error, string Status);

        private static async Task<Run> RunAsync(MifareDesfireSetupViewModel task, KeyRecordingReader? reader = null)
        {
            reader ??= new KeyRecordingReader();

            using (ReaderDeviceScope.Use(reader))
            {
                await task.CommandDelegator.ExecuteAsync(task.SelectedTaskType);
            }

            return new Run(Describe(reader), task.CurrentTaskErrorLevel, task.StatusText ?? string.Empty);
        }

        private static async Task<ExecResult> ExecuteAsync(MifareDesfireSetupViewModel task, TaskExecutionService service, CancellationToken cancellationToken = default)
        {
            // Its own folder: opening the project database resets the files of the application's profile.
            var root = Directory.CreateTempSubdirectory("KeyResolutionRun").FullName;

            try
            {
                var coordinator = new ExtensionOperationCoordinator();
                var project = new ProjectHostImpl(new DatabaseReaderWriter(new ProjectManager(root)), coordinator);
                var host = new TaskExecHostImpl(service, project, coordinator);
                project.ReplaceTaskCollection(new object[] { task });

                return await host.ExecuteAllAsync(ExecutionRequest.Empty, cancellationToken);
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        private static string Describe(KeyRecordingReader reader) =>
            string.Join(" | ", reader.Calls.Select(call => $"{call.Method}[{string.Join(",", call.Keys.Select(NameOf))}]"));

        private static IEnumerable<string> NamesIn(string calls) =>
            calls.Split(new[] { '[', ',', ']', '|' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Trim())
                .Where(part => Slots.Values.Any(slot => slot.Short == part));

        private static string NameOf(string key)
        {
            foreach (var slot in Slots.Values)
            {
                if (key == slot.Vault)
                {
                    return slot.Short;
                }

                if (key == slot.Embedded)
                {
                    return "embedded:" + slot.Short;
                }
            }

            return key.Length == 0 ? "<empty>" : "<unknown>";
        }

        private static void AssertNothingSecretIn(string text)
        {
            foreach (var slot in Slots.Values)
            {
                foreach (var key in new[] { slot.Embedded, slot.Vault })
                {
                    Assert.DoesNotContain(key, text, StringComparison.OrdinalIgnoreCase);
                    Assert.DoesNotContain(string.Join(" ", Enumerable.Range(0, key.Length / 2).Select(i => key.Substring(i * 2, 2))), text, StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        /// <summary>A vault: a provider that answers from the table above, and notes what it was asked.</summary>
        private sealed class Vault
        {
            private readonly List<SecureKeyRequest> _requests = new List<SecureKeyRequest>();
            private readonly List<string> _returnedKeys = new List<string>();

            /// <summary>Gets or sets a failure every request is answered with; <c>null</c> for none.</summary>
            public SecureKeyFailureReason? Failure { get; set; }

            /// <summary>Gets the slots that are answered with a failure.</summary>
            public HashSet<string> FailSlots { get; } = new HashSet<string>(StringComparer.Ordinal);

            /// <summary>Gets the references that are answered with a failure.</summary>
            public HashSet<string> FailReferences { get; } = new HashSet<string>(StringComparer.Ordinal);

            /// <summary>Gets or sets the length of the keys in bytes; 16 unless a test needs another.</summary>
            public int KeyLength { get; set; } = 16;

            /// <summary>Gets or sets a text that the vault throws instead of answering; <c>null</c> for none.</summary>
            public string? ThrowText { get; set; }

            /// <summary>Gets or sets whether the vault hands out a lease that is disposed already, as a provider with a defect might.</summary>
            public bool ReturnDisposedLease { get; set; }

            /// <summary>Gets or sets what happens when a request arrives.</summary>
            public Action<SecureKeyRequest>? OnRequest { get; set; }

            /// <summary>Gets the requests, in order.</summary>
            public IReadOnlyList<SecureKeyRequest> Requests
            {
                get
                {
                    lock (_requests)
                    {
                        return _requests.ToList();
                    }
                }
            }

            /// <summary>Gets the keys it handed out, as hexadecimal text.</summary>
            public IReadOnlyList<string> ReturnedKeys
            {
                get
                {
                    lock (_requests)
                    {
                        return _returnedKeys.ToList();
                    }
                }
            }

            public SecureKeyRegistry Registry() => SecureKeyRegistry.Create(
                new[] { new SecureKeyProviderRegistration(Provider, "Test vault", new ScriptedSecureKeyProvider((request, _) => new ValueTask<SecureKeyResolution>(Answer(request)))) },
                Array.Empty<Lazy<ISecureKeyProvider, RFiDGear.ExtensionSdk.ISecureKeyProviderMetadata>>());

            /// <summary>Makes this vault the one that commands without a run ask.</summary>
            public void Install(string? runId = "run-1", string taskIndex = "0", string? cardUid = null, Action? resolutionStarted = null, Action? resolutionFinished = null, CancellationToken cancellationToken = default) =>
                KeyResolutionScope.SetDefault(new KeyResolutionContext(Registry(), runId, taskIndex, cardUid, interactive: false, resolutionStarted, resolutionFinished, cancellationToken));

            private SecureKeyResolution Answer(SecureKeyRequest request)
            {
                lock (_requests)
                {
                    _requests.Add(request);
                }

                OnRequest?.Invoke(request);

                if (ThrowText != null)
                {
                    throw new InvalidOperationException(ThrowText);
                }

                if (Failure != null)
                {
                    return SecureKeyResolution.Failed(Failure.Value);
                }

                if (FailSlots.Contains(request.SlotId) || FailReferences.Contains(request.Reference) || !Slots.TryGetValue(request.SlotId, out var slot))
                {
                    return SecureKeyResolution.Failed(SecureKeyFailureReason.NotFound);
                }

                var bytes = Convert.FromHexString(slot.Vault);
                var key = KeyLength == bytes.Length ? bytes : Enumerable.Range(0, KeyLength).Select(i => bytes[i % bytes.Length]).ToArray();

                lock (_requests)
                {
                    _returnedKeys.Add(Convert.ToHexString(key));
                }

                var lease = new SecureKeyLease(key);

                if (ReturnDisposedLease)
                {
                    lease.Dispose();
                }

                return SecureKeyResolution.Resolved(lease);
            }
        }

        /// <summary>A watchdog timer that remembers whether it was running while a provider was asked.</summary>
        private sealed class RecordingTimer : IDispatcherTimerAdapter
        {
            public bool IsEnabled { get; set; }

            public object? Tag { get; set; }

            public List<string> Events { get; } = new List<string>();

            public List<(bool Enabled, int EventsSoFar, string? LastEvent)> Asked { get; } = new List<(bool, int, string?)>();

            public void Start()
            {
                Events.Add("Start");
                IsEnabled = true;
            }

            public void Stop()
            {
                Events.Add("Stop");
                IsEnabled = false;
            }

            public void Dispose()
            {
                // Nothing to release.
                IsEnabled = false;
            }
        }
    }
}
