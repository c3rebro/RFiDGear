using RFiDGear.Contracts;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.Tasks;
using RFiDGear.Models;
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
    /// The two setup view models as what the key slot editors edit: binding a slot clears its key, in that order, the
    /// digits a key has follow its type, and the dialog takes no input while the keys of a command are obtained.
    /// </summary>
    public class KeySlotHostTests : IDisposable
    {
        private const string Aes = "00112233445566778899AABBCCDDEEFF";
        private const string Classic = "A1B2C3D4E5F6";

        public KeySlotHostTests()
        {
            KeyResolutionScope.SetDefault(null);
        }

        public void Dispose()
        {
            KeyResolutionScope.SetDefault(null);
        }

        /// <summary>Gets every slot of a DESFire task.</summary>
        public static IEnumerable<object[]> DesfireSlots() => SecureKeySlots.Desfire.Select(slot => new object[] { slot });

        /// <summary>Gets every slot of a MIFARE Classic task.</summary>
        public static IEnumerable<object[]> ClassicSlots() => SecureKeySlots.Classic.Select(slot => new object[] { slot });

        // ── DESFire ──

        [Theory]
        [MemberData(nameof(DesfireSlots))]
        public async Task Binding_a_desfire_slot_stores_one_binding_for_it_and_clears_its_key(string slot)
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                    var task = new MifareDesfireSetupViewModel();
                    SetKey(task, slot, Aes);
                    Assert.True(task.HasEmbeddedKey(slot));

                    task.BindKeySlot(slot, "test.vault", "ref/one");
                    task.BindKeySlot(slot, "test.vault", "ref/two");

                    var binding = Assert.Single(task.KeyBindings, entry => entry.Slot == slot);
                    Assert.Equal("ref/two", binding.Reference);
                    Assert.True(task.IsKeyBound(slot));
                    Assert.False(task.HasEmbeddedKey(slot));
            });
        }

        [Theory]
        [MemberData(nameof(DesfireSlots))]
        public async Task The_binding_of_a_desfire_slot_is_announced_while_its_key_is_still_there(string slot)
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                    var task = new MifareDesfireSetupViewModel();
                    SetKey(task, slot, Aes);
                    bool? keyWhenAnnounced = null;
                    task.PropertyChanged += (_, e) =>
                    {
                        if (e.PropertyName == nameof(task.KeyBindings))
                        {
                            keyWhenAnnounced = task.HasEmbeddedKey(slot);
                        }
                    };

                    task.BindKeySlot(slot, "test.vault", "ref/one");

                    // Bound first, cleared second: a slot is never left with neither.
                    Assert.True(keyWhenAnnounced);
                    Assert.False(task.HasEmbeddedKey(slot));
            });
        }

        [Fact]
        public async Task Binding_the_current_application_key_also_clears_a_copy_of_it_in_the_old_key_but_keeps_an_old_key_that_differs()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                const string Different = "FFEEDDCCBBAA99887766554433221100";
                var copy = NewKeyChange(Aes, Aes);
                var differing = NewKeyChange(Aes, Different);
                Assert.True(copy.ShowAppKeyOldInputs);
                Assert.True(differing.ShowAppKeyOldInputs);

                copy.BindKeySlot(SecureKeySlots.DesfireAppCurrent, "test.vault", "ref/app");
                differing.BindKeySlot(SecureKeySlots.DesfireAppCurrent, "test.vault", "ref/app");

                // What was discarded is not saved through the old key; an old key of its own is.
                Assert.DoesNotContain(Aes, XmlOf(copy), StringComparison.OrdinalIgnoreCase);
                Assert.Contains(Different, XmlOf(differing), StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(Aes, XmlOf(differing), StringComparison.OrdinalIgnoreCase);
            });
        }

        [Theory]
        [MemberData(nameof(DesfireSlots))]
        public async Task Switching_a_desfire_slot_back_clears_a_key_that_a_loaded_task_kept_next_to_its_binding(string slot)
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var task = new MifareDesfireSetupViewModel();
                SetKey(task, slot, Aes);

                // As a file that was edited by hand can leave it: the binding and the key.
                task.KeyBindings = new[] { new KeyBindingEntry { Slot = slot, ProviderId = "test.vault", Reference = "ref/x" } };
                Assert.True(task.HasEmbeddedKey(slot));

                task.UnbindKeySlot(slot);

                Assert.False(task.IsKeyBound(slot));
                Assert.False(task.HasEmbeddedKey(slot));
            });
        }

        [Fact]
        public async Task Unbinding_a_slot_that_is_not_bound_leaves_the_key_that_is_typed()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var task = new MifareDesfireSetupViewModel();
                SetKey(task, SecureKeySlots.DesfireRead, Aes);

                task.UnbindKeySlot(SecureKeySlots.DesfireRead);

                Assert.True(task.HasEmbeddedKey(SecureKeySlots.DesfireRead));
            });
        }

        [Fact]
        public async Task Unbinding_a_desfire_slot_removes_only_its_binding()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                    var task = new MifareDesfireSetupViewModel();
                    task.BindKeySlot(SecureKeySlots.DesfireRead, "test.vault", "ref/read");
                    task.BindKeySlot(SecureKeySlots.DesfireWrite, "test.vault", "ref/write");

                    task.UnbindKeySlot(SecureKeySlots.DesfireRead);

                    Assert.False(task.IsKeyBound(SecureKeySlots.DesfireRead));
                    Assert.True(task.IsKeyBound(SecureKeySlots.DesfireWrite));
                    Assert.False(task.HasEmbeddedKey(SecureKeySlots.DesfireRead));
            });
        }

        [Fact]
        public async Task A_slot_of_a_classic_task_is_not_a_slot_of_a_desfire_task()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                    var task = new MifareDesfireSetupViewModel();

                    Assert.Throws<ArgumentOutOfRangeException>(() => task.BindKeySlot(SecureKeySlots.ClassicACurrent, "test.vault", "ref"));
                    Assert.Throws<ArgumentOutOfRangeException>(() => task.ExpectedKeyHexLength(SecureKeySlots.ClassicACurrent));
                    Assert.Throws<ArgumentOutOfRangeException>(() => task.HasEmbeddedKey(SecureKeySlots.ClassicACurrent));
                    Assert.Empty(task.KeyBindings);
            });
        }

        [Theory]
        [InlineData(SecureKeySlots.DesfirePiccCurrent)]
        [InlineData(SecureKeySlots.DesfirePiccNew)]
        [InlineData(SecureKeySlots.DesfireAppCurrent)]
        [InlineData(SecureKeySlots.DesfireAppOld)]
        [InlineData(SecureKeySlots.DesfireAppNew)]
        [InlineData(SecureKeySlots.DesfireRead)]
        [InlineData(SecureKeySlots.DesfireWrite)]
        public async Task The_digits_of_a_desfire_key_follow_the_type_that_was_chosen_for_it(string slot)
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                    var task = new MifareDesfireSetupViewModel();

                    SetKeyType(task, slot, DESFireKeyType.DF_KEY_3K3DES);
                    Assert.Equal(48, task.ExpectedKeyHexLength(slot));

                    SetKeyType(task, slot, DESFireKeyType.DF_KEY_AES);
                    Assert.Equal(32, task.ExpectedKeyHexLength(slot));
            });
        }

        // ── The new application key ──

        [Fact]
        public async Task The_new_application_key_is_cut_and_checked_by_the_target_type_and_not_by_the_current_one()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                    var task = new MifareDesfireSetupViewModel
                    {
                        SelectedDesfireAppKeyEncryptionTypeCurrent = DESFireKeyType.DF_KEY_AES,
                    };

                    // A task that was saved with a target type of its own.
                    task.SelectedDesfireAppKeyEncryptionTypeTarget = DESFireKeyType.DF_KEY_3K3DES;
                    task.DesfireAppKeyTarget = new string('A', 48);

                    Assert.Equal(48, task.DesfireAppKeyTarget.Length);
                    Assert.True(task.IsValidDesfireAppKeyTarget);

                    task.SelectedDesfireAppKeyEncryptionTypeTarget = DESFireKeyType.DF_KEY_AES;
                    Assert.False(task.IsValidDesfireAppKeyTarget);
            });
        }

        [Fact]
        public async Task The_target_type_of_the_application_key_follows_the_current_type()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                    var task = new MifareDesfireSetupViewModel();

                    task.SelectedDesfireAppKeyEncryptionTypeCurrent = DESFireKeyType.DF_KEY_3K3DES;
                    Assert.Equal(DESFireKeyType.DF_KEY_3K3DES, task.SelectedDesfireAppKeyEncryptionTypeTarget);

                    task.SelectedDesfireAppKeyEncryptionTypeCurrent = DESFireKeyType.DF_KEY_AES;
                    Assert.Equal(DESFireKeyType.DF_KEY_AES, task.SelectedDesfireAppKeyEncryptionTypeTarget);
            });
        }

        [Fact]
        public async Task A_target_type_that_was_saved_with_the_task_comes_back_with_it()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                    var task = new MifareDesfireSetupViewModel
                    {
                        SelectedDesfireAppKeyEncryptionTypeCurrent = DESFireKeyType.DF_KEY_AES,
                    };
                    task.SelectedDesfireAppKeyEncryptionTypeTarget = DESFireKeyType.DF_KEY_3K3DES;
                    task.DesfireAppKeyTarget = new string('B', 48);

                    var serializer = new XmlSerializer(typeof(MifareDesfireSetupViewModel));
                    using var stream = new MemoryStream();
                    serializer.Serialize(stream, task);
                    stream.Position = 0;
                    var restored = (MifareDesfireSetupViewModel)serializer.Deserialize(stream)!;

                    Assert.Equal(DESFireKeyType.DF_KEY_AES, restored.SelectedDesfireAppKeyEncryptionTypeCurrent);
                    Assert.Equal(DESFireKeyType.DF_KEY_3K3DES, restored.SelectedDesfireAppKeyEncryptionTypeTarget);
                    Assert.Equal(new string('B', 48), restored.DesfireAppKeyTarget);
            });
        }

        // ── MIFARE Classic ──

        [Theory]
        [MemberData(nameof(ClassicSlots))]
        public async Task Binding_a_classic_slot_stores_one_binding_for_it_and_clears_its_key(string slot)
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                    var task = new MifareClassicSetupViewModel();
                    SetKey(task, slot, Classic);
                    Assert.True(task.HasEmbeddedKey(slot));

                    task.BindKeySlot(slot, "test.vault", "ref/one");
                    task.BindKeySlot(slot, "test.vault", "ref/two");

                    var binding = Assert.Single(task.KeyBindings, entry => entry.Slot == slot);
                    Assert.Equal("ref/two", binding.Reference);
                    Assert.True(task.IsKeyBound(slot));
                    Assert.False(task.HasEmbeddedKey(slot));
                    Assert.Equal(12, task.ExpectedKeyHexLength(slot));
            });
        }

        [Theory]
        [MemberData(nameof(ClassicSlots))]
        public async Task The_binding_of_a_classic_slot_is_announced_while_its_key_is_still_there(string slot)
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                    var task = new MifareClassicSetupViewModel();
                    SetKey(task, slot, Classic);
                    bool? keyWhenAnnounced = null;
                    task.PropertyChanged += (_, e) =>
                    {
                        if (e.PropertyName == nameof(task.KeyBindings))
                        {
                            keyWhenAnnounced = task.HasEmbeddedKey(slot);
                        }
                    };

                    task.BindKeySlot(slot, "test.vault", "ref/one");

                    Assert.True(keyWhenAnnounced);
                    Assert.False(task.HasEmbeddedKey(slot));
            });
        }

        [Theory]
        [MemberData(nameof(ClassicSlots))]
        public async Task Switching_a_classic_slot_back_clears_a_key_that_a_loaded_task_kept_next_to_its_binding(string slot)
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var task = new MifareClassicSetupViewModel();
                SetKey(task, slot, Classic);
                task.KeyBindings = new[] { new KeyBindingEntry { Slot = slot, ProviderId = "test.vault", Reference = "ref/x" } };
                Assert.True(task.HasEmbeddedKey(slot));

                task.UnbindKeySlot(slot);

                Assert.False(task.IsKeyBound(slot));
                Assert.False(task.HasEmbeddedKey(slot));
            });
        }

        [Fact]
        public async Task Unbinding_a_classic_slot_that_is_not_bound_leaves_the_key_that_is_typed()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var task = new MifareClassicSetupViewModel();
                SetKey(task, SecureKeySlots.ClassicACurrent, Classic);

                task.UnbindKeySlot(SecureKeySlots.ClassicACurrent);

                Assert.True(task.HasEmbeddedKey(SecureKeySlots.ClassicACurrent));
            });
        }

        [Fact]
        public async Task Unbinding_a_classic_slot_removes_only_its_binding()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                    var task = new MifareClassicSetupViewModel();
                    task.BindKeySlot(SecureKeySlots.ClassicACurrent, "test.vault", "ref/a");
                    task.BindKeySlot(SecureKeySlots.ClassicBCurrent, "test.vault", "ref/b");

                    task.UnbindKeySlot(SecureKeySlots.ClassicACurrent);

                    Assert.False(task.IsKeyBound(SecureKeySlots.ClassicACurrent));
                    Assert.True(task.IsKeyBound(SecureKeySlots.ClassicBCurrent));
            });
        }

        [Fact]
        public async Task A_slot_of_a_desfire_task_is_not_a_slot_of_a_classic_task()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                    var task = new MifareClassicSetupViewModel();

                    Assert.Throws<ArgumentOutOfRangeException>(() => task.BindKeySlot(SecureKeySlots.DesfireRead, "test.vault", "ref"));
                    Assert.Throws<ArgumentOutOfRangeException>(() => task.ExpectedKeyHexLength(SecureKeySlots.DesfireRead));
                    Assert.Throws<ArgumentOutOfRangeException>(() => task.HasEmbeddedKey(SecureKeySlots.DesfireRead));
                    Assert.Empty(task.KeyBindings);
            });
        }

        // ── The dialog while the keys are obtained ──

        [Fact]
        public async Task A_desfire_dialog_takes_no_input_while_a_provider_is_asked_and_again_afterwards()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var provider = new BlockingKeyProvider(16);
                KeyResolutionScope.SetDefault(new KeyResolutionContext(provider.Registry(), "run-1", "0", null, interactive: false));
                var task = new MifareDesfireSetupViewModel
                {
                    SelectedTaskType = TaskType_MifareDesfireTask.ReadData,
                    CurrentTaskIndex = "0",
                    AppNumberCurrent = "1",
                    FileNumberCurrent = "2",
                    FileSizeCurrent = "16",
                    KeyBindings = new[] { new KeyBindingEntry { Slot = SecureKeySlots.DesfireRead, ProviderId = BlockingKeyProvider.Id, Reference = "ref/read" } },
                };
                var announced = new List<string>();
                task.PropertyChanged += (_, e) => announced.Add(e.PropertyName ?? string.Empty);

                using (ReaderDeviceScope.Use(new KeyRecordingReader()))
                {
                    var running = task.CommandDelegator.ExecuteAsync(TaskType_MifareDesfireTask.ReadData);
                    await provider.Asked;

                    Assert.True(task.IsFetchingKeys);
                    Assert.False(task.AreInputsEnabled);

                    provider.Release();
                    await running;
                }

                Assert.False(task.IsFetchingKeys);
                Assert.True(task.AreInputsEnabled);
                Assert.Contains(nameof(task.AreInputsEnabled), announced);
            });
        }

        [Fact]
        public async Task A_classic_dialog_takes_no_input_while_a_provider_is_asked_and_again_afterwards()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var provider = new BlockingKeyProvider(6);
                KeyResolutionScope.SetDefault(new KeyResolutionContext(provider.Registry(), "run-1", "0", null, interactive: false));
                var task = new MifareClassicSetupViewModel
                {
                    SelectedTaskType = TaskType_MifareClassicTask.ReadData,
                    CurrentTaskIndex = "0",
                    KeyBindings = new[]
                    {
                        new KeyBindingEntry { Slot = SecureKeySlots.ClassicACurrent, ProviderId = BlockingKeyProvider.Id, Reference = "ref/a" },
                        new KeyBindingEntry { Slot = SecureKeySlots.ClassicBCurrent, ProviderId = BlockingKeyProvider.Id, Reference = "ref/b" },
                    },
                };
                var announced = new List<string>();
                task.PropertyChanged += (_, e) => announced.Add(e.PropertyName ?? string.Empty);

                using (ReaderDeviceScope.Use(new KeyRecordingReader()))
                {
                    var running = task.CommandDelegator.ExecuteAsync(TaskType_MifareClassicTask.ReadData);
                    await provider.Asked;

                    Assert.True(task.IsFetchingKeys);
                    Assert.False(task.AreInputsEnabled);

                    provider.Release();
                    await running;
                }

                Assert.False(task.IsFetchingKeys);
                Assert.True(task.AreInputsEnabled);
                Assert.Contains(nameof(task.AreInputsEnabled), announced);
            });
        }

        [Fact]
        public async Task A_dialog_that_asks_no_provider_does_not_stay_locked()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var task = new MifareDesfireSetupViewModel
                {
                    SelectedTaskType = TaskType_MifareDesfireTask.ReadData,
                    CurrentTaskIndex = "0",
                    AppNumberCurrent = "1",
                    FileNumberCurrent = "2",
                    FileSizeCurrent = "16",
                };

                using (ReaderDeviceScope.Use(new KeyRecordingReader()))
                {
                    await task.CommandDelegator.ExecuteAsync(TaskType_MifareDesfireTask.ReadData);
                }

                Assert.True(task.AreInputsEnabled);
            });
        }

        // ── Helpers ──

        // A key change in an application other than the card, for a key other than 0: the old-key inputs show then.
        private static MifareDesfireSetupViewModel NewKeyChange(string current, string old)
        {
            var task = new MifareDesfireSetupViewModel
            {
                SelectedTaskType = TaskType_MifareDesfireTask.ApplicationKeyChangeover,
                AppNumberCurrent = "1",
                SelectedDesfireAppKeyNumberCurrent = "1",
            };
            task.DesfireAppKeyCurrent = current;
            task.DesfireAppKeyCurrentOld = old;

            return task;
        }

        private static string XmlOf(MifareDesfireSetupViewModel task)
        {
            using var writer = new StringWriter();
            new XmlSerializer(typeof(MifareDesfireSetupViewModel)).Serialize(writer, task);

            return writer.ToString();
        }

        private static void SetKey(MifareDesfireSetupViewModel task, string slot, string value)
        {
            switch (slot)
            {
                case SecureKeySlots.DesfirePiccCurrent: task.DesfireMasterKeyCurrent = value; break;
                case SecureKeySlots.DesfirePiccNew: task.DesfireMasterKeyTarget = value; break;
                case SecureKeySlots.DesfireAppCurrent: task.DesfireAppKeyCurrent = value; break;
                case SecureKeySlots.DesfireAppOld: task.DesfireAppKeyCurrentOld = value; break;
                case SecureKeySlots.DesfireAppNew: task.DesfireAppKeyTarget = value; break;
                case SecureKeySlots.DesfireRead: task.DesfireReadKeyCurrent = value; break;
                case SecureKeySlots.DesfireWrite: task.DesfireWriteKeyCurrent = value; break;
                default: throw new ArgumentOutOfRangeException(nameof(slot));
            }
        }

        private static void SetKeyType(MifareDesfireSetupViewModel task, string slot, DESFireKeyType type)
        {
            switch (slot)
            {
                case SecureKeySlots.DesfirePiccCurrent: task.SelectedDesfireMasterKeyEncryptionTypeCurrent = type; break;
                case SecureKeySlots.DesfirePiccNew: task.SelectedDesfireMasterKeyEncryptionTypeTarget = type; break;
                case SecureKeySlots.DesfireAppCurrent:
                case SecureKeySlots.DesfireAppOld: task.SelectedDesfireAppKeyEncryptionTypeCurrent = type; break;
                case SecureKeySlots.DesfireAppNew: task.SelectedDesfireAppKeyEncryptionTypeTarget = type; break;
                case SecureKeySlots.DesfireRead: task.SelectedDesfireReadKeyEncryptionType = type; break;
                case SecureKeySlots.DesfireWrite: task.SelectedDesfireWriteKeyEncryptionType = type; break;
                default: throw new ArgumentOutOfRangeException(nameof(slot));
            }
        }

        private static void SetKey(MifareClassicSetupViewModel task, string slot, string value)
        {
            switch (slot)
            {
                case SecureKeySlots.ClassicACurrent: task.ClassicKeyAKeyCurrent = value; break;
                case SecureKeySlots.ClassicBCurrent: task.ClassicKeyBKeyCurrent = value; break;
                case SecureKeySlots.ClassicATarget: task.ClassicKeyAKeyTarget = value; break;
                case SecureKeySlots.ClassicBTarget: task.ClassicKeyBKeyTarget = value; break;
                case SecureKeySlots.ClassicMadACurrent: task.ClassicMADKeyAKeyCurrent = value; break;
                case SecureKeySlots.ClassicMadBCurrent: task.ClassicMADKeyBKeyCurrent = value; break;
                case SecureKeySlots.ClassicMadATarget: task.ClassicMADKeyAKeyTarget = value; break;
                case SecureKeySlots.ClassicMadBTarget: task.ClassicMADKeyBKeyTarget = value; break;
                default: throw new ArgumentOutOfRangeException(nameof(slot));
            }
        }
    }
}
