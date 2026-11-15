using RFiDGear.Infrastructure.Ultralight;
using RFiDGear.Infrastructure.Tasks;
using RFiDGear.Models;
using RFiDGear.Services.SecureKeys;
using RFiDGear.UI.MVVMDialogs.ViewModels.Interfaces;
using RFiDGear.ViewModel.TaskSetupViewModels;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml.Serialization;

using Xunit;

namespace RFiDGear.Tests.SecureKeys
{
    /// <summary>
    /// What an Ultralight task keeps in a project and what its key slot editors ask of it: what the user chose (the pages, the
    /// chip, the authentication, the opt-in for protected pages, the data to write), the keys it holds itself, and the bindings
    /// that replace them. A key that is bound is not written next to its reference, as for a DESFire and a Classic task.
    /// </summary>
    public class UltralightTaskKeyTests
    {
        private const string Provider = "test.vault";

        // One recognizable key per slot, in the form the view model keeps: the key of a C is 16 bytes, the password 4, the PACK 2.
        private static readonly Dictionary<string, (string Property, string Key)> Slots = new Dictionary<string, (string, string)>
        {
            [SecureKeySlots.UltralightCKey] = ("UltralightCKey", "A1A1A1A1A1A1A1A1B1B1B1B1B1B1B1B1"),
            [SecureKeySlots.UltralightPassword] = ("UltralightPassword", "C1C2C3C4"),
            [SecureKeySlots.UltralightPack] = ("UltralightPack", "D1D2"),
        };

        public static IEnumerable<object[]> EverySlot() => SecureKeySlots.Ultralight.Select(slot => new object[] { slot });

        private static MifareUltralightSetupViewModel NewTask()
        {
            var task = new MifareUltralightSetupViewModel { CurrentTaskIndex = "0" };

            foreach (var (property, key) in Slots.Values)
            {
                typeof(MifareUltralightSetupViewModel).GetProperty(property)!.SetValue(task, key);
            }

            return task;
        }

        private static string Serialize(object task)
        {
            var project = new ChipTaskHandlerModel();
            project.TaskCollection.Add(task);

            using var writer = new StringWriter();
            new XmlSerializer(typeof(ChipTaskHandlerModel)).Serialize(writer, project);

            return writer.ToString();
        }

        private static ChipTaskHandlerModel Deserialize(string xml)
        {
            using var reader = new StringReader(xml);

            return (ChipTaskHandlerModel)new XmlSerializer(typeof(ChipTaskHandlerModel)).Deserialize(reader)!;
        }

        private static KeyBindingEntry Binding(string slot) => new KeyBindingEntry { Slot = slot, ProviderId = Provider, Reference = "cards/ultralight" };

        // ── What the project keeps ──

        [Fact]
        public async Task ANewTask_ReadsOneUserPage_OfAnyChip_WithoutAuthenticationAndWithoutTheOptIn()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var task = new MifareUltralightSetupViewModel();

                Assert.Equal(4, task.StartPage);
                Assert.Equal(1, task.PageCount);
                Assert.Equal(UltralightExpectation.Auto, task.ExpectedChip);
                Assert.Equal(UltralightAuth.None, task.Authentication);
                Assert.False(task.AllowProtectedPages);
                Assert.Equal(string.Empty, task.WriteDataHex);
                Assert.All(Slots.Values, slot => Assert.Equal(string.Empty, typeof(MifareUltralightSetupViewModel).GetProperty(slot.Property)!.GetValue(task)));
                Assert.Empty(task.KeyBindings);
            });
        }

        [Fact]
        public async Task WhatTheUserChose_IsInTheProject_AndComesBack()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var task = NewTask();
                task.SelectedTaskType = TaskType_MifareUltralightTask.WriteData;
                task.StartPage = 8;
                task.PageCount = 3;
                task.WriteDataHex = "A1B2C3D401020304";
                task.ExpectedChip = UltralightExpectation.Ntag215;
                task.Authentication = UltralightAuth.Password;
                task.AllowProtectedPages = true;

                var xml = Serialize(task);
                var reloaded = Assert.IsType<MifareUltralightSetupViewModel>(Assert.Single(Deserialize(xml).TaskCollection));

                Assert.Equal(TaskType_MifareUltralightTask.WriteData, reloaded.SelectedTaskType);
                Assert.Equal(8, reloaded.StartPage);
                Assert.Equal(3, reloaded.PageCount);
                Assert.Equal("A1B2C3D401020304", reloaded.WriteDataHex);
                Assert.Equal(UltralightExpectation.Ntag215, reloaded.ExpectedChip);
                Assert.Equal(UltralightAuth.Password, reloaded.Authentication);
                Assert.True(reloaded.AllowProtectedPages);
                Assert.All(Slots.Values, slot => Assert.Equal(slot.Key, typeof(MifareUltralightSetupViewModel).GetProperty(slot.Property)!.GetValue(reloaded)));
            });
        }

        [Fact]
        public async Task AProjectSavedBeforeTheseProperties_LoadsWithTheDefaults()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                const string Legacy =
                    "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                    "<ChipTaskHandlerModel xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\">" +
                    "<ManifestVersion>2.0.10</ManifestVersion><TaskCollection>" +
                    "<anyType xsi:type=\"MifareUltralightSetupViewModel\"><SelectedTaskType>ReadData</SelectedTaskType></anyType>" +
                    "</TaskCollection></ChipTaskHandlerModel>";

                var task = Assert.IsType<MifareUltralightSetupViewModel>(Assert.Single(Deserialize(Legacy).TaskCollection));

                Assert.Equal(4, task.StartPage);
                Assert.Equal(1, task.PageCount);
                Assert.Equal(UltralightExpectation.Auto, task.ExpectedChip);
                Assert.Equal(UltralightAuth.None, task.Authentication);
                Assert.False(task.AllowProtectedPages);
                Assert.Empty(task.KeyBindings);
            });
        }

        [Fact]
        public async Task ATaskWithoutBindings_IsSavedWithItsKeys_AndWithoutAKeyBindingsElement()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var xml = Serialize(NewTask());

                Assert.DoesNotContain("KeyBindings", xml, StringComparison.Ordinal);
                Assert.All(Slots.Values, slot => Assert.Contains(">" + slot.Key + "<", xml, StringComparison.Ordinal));
            });
        }

        [Theory]
        [MemberData(nameof(EverySlot))]
        public async Task ABoundSlot_IsSavedAsAReference_AndWithoutItsKey(string slot)
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var task = NewTask();
                task.KeyBindings = new[] { Binding(slot) };

                var xml = Serialize(task);

                Assert.Contains("<KeyBindings>", xml, StringComparison.Ordinal);
                Assert.Contains($"<Slot>{slot}</Slot>", xml, StringComparison.Ordinal);
                Assert.DoesNotContain(">" + Slots[slot].Key + "<", xml, StringComparison.Ordinal);
                Assert.All(Slots.Where(other => other.Key != slot), other => Assert.Contains(">" + other.Value.Key + "<", xml, StringComparison.Ordinal));
            });
        }

        [Fact]
        public async Task EveryKeyPropertyOfTheTask_IsASlot_AndIsKeptOutOfTheFileWhenItIsBound()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                // A key property added later without a slot would be written in the clear next to a reference. The keys of the task
                // are the strings that are named Ultralight...; every one of them has a slot and a rule for the serializer.
                var keyProperties = typeof(MifareUltralightSetupViewModel).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(property => property.PropertyType == typeof(string) && property.CanRead && property.CanWrite && property.Name.StartsWith("Ultralight", StringComparison.Ordinal))
                    .Select(property => property.Name)
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToList();

                Assert.Equal(Slots.Values.Select(slot => slot.Property).OrderBy(name => name, StringComparer.Ordinal), keyProperties);

                foreach (var (slot, (property, _)) in Slots)
                {
                    var task = NewTask();
                    var shouldSerialize = typeof(MifareUltralightSetupViewModel).GetMethod("ShouldSerialize" + property, BindingFlags.Public | BindingFlags.Instance);
                    Assert.NotNull(shouldSerialize);

                    Assert.True((bool)shouldSerialize!.Invoke(task, null)!, property + " is written while it is not bound");

                    task.KeyBindings = new[] { Binding(slot) };

                    Assert.False((bool)shouldSerialize.Invoke(task, null)!, property + " is written although " + slot + " is bound");
                }
            });
        }

        [Fact]
        public async Task TheKeyBindingsOfAnEditedTask_AreCopies_SoACancelledEditChangesNothing()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var original = NewTask();
                original.KeyBindings = new[] { Binding(SecureKeySlots.UltralightCKey) };

                var editor = new MifareUltralightSetupViewModel(original, new ObservableCollection<IDialogViewModel>());
                editor.BindKeySlot(SecureKeySlots.UltralightPassword, Provider, "ref/other");

                Assert.True(editor.IsKeyBound(SecureKeySlots.UltralightPassword));
                Assert.False(original.IsKeyBound(SecureKeySlots.UltralightPassword));
                Assert.Single(original.KeyBindings);
            });
        }

        // ── What the key slot editors ask of the task ──

        [Theory]
        [MemberData(nameof(EverySlot))]
        public async Task BindingASlot_StoresOneBindingForIt_AndClearsItsKey(string slot)
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var task = NewTask();
                Assert.True(task.HasEmbeddedKey(slot));

                task.BindKeySlot(slot, Provider, "ref/one");
                task.BindKeySlot(slot, Provider, "ref/two");

                var binding = Assert.Single(task.KeyBindings, entry => entry.Slot == slot);
                Assert.Equal("ref/two", binding.Reference);
                Assert.True(task.IsKeyBound(slot));
                Assert.False(task.HasEmbeddedKey(slot));
            });
        }

        [Theory]
        [InlineData(SecureKeySlots.UltralightCKey, 32)]
        [InlineData(SecureKeySlots.UltralightPassword, 8)]
        [InlineData(SecureKeySlots.UltralightPack, 4)]
        public async Task TheKeyOfASlot_HasTheLengthOfItsKind(string slot, int hexLength)
        {
            await StaTestRunner.RunOnStaThreadAsync(() => Assert.Equal(hexLength, new MifareUltralightSetupViewModel().ExpectedKeyHexLength(slot)));
        }

        [Theory]
        [MemberData(nameof(EverySlot))]
        public async Task TheBindingOfASlot_IsAnnouncedWhileItsKeyIsStillThere(string slot)
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var task = NewTask();
                bool? keyWhenAnnounced = null;
                task.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(task.KeyBindings))
                    {
                        keyWhenAnnounced = task.HasEmbeddedKey(slot);
                    }
                };

                task.BindKeySlot(slot, Provider, "ref/one");

                Assert.True(keyWhenAnnounced);
                Assert.False(task.HasEmbeddedKey(slot));
            });
        }

        [Theory]
        [MemberData(nameof(EverySlot))]
        public async Task SwitchingASlotBack_ClearsAKeyThatALoadedTaskKeptNextToItsBinding(string slot)
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var task = NewTask();
                task.KeyBindings = new[] { Binding(slot) };
                Assert.True(task.HasEmbeddedKey(slot));

                task.UnbindKeySlot(slot);

                Assert.False(task.IsKeyBound(slot));
                Assert.False(task.HasEmbeddedKey(slot));
            });
        }

        [Fact]
        public async Task UnbindingASlotThatIsNotBound_LeavesTheKeyThatIsTyped()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var task = NewTask();

                task.UnbindKeySlot(SecureKeySlots.UltralightCKey);

                Assert.True(task.HasEmbeddedKey(SecureKeySlots.UltralightCKey));
            });
        }

        [Fact]
        public async Task ASlotOfAnotherTask_IsNotASlotOfAnUltralightTask()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var task = new MifareUltralightSetupViewModel();

                Assert.Throws<ArgumentOutOfRangeException>(() => task.BindKeySlot(SecureKeySlots.DesfireRead, Provider, "ref"));
                Assert.Throws<ArgumentOutOfRangeException>(() => task.BindKeySlot(SecureKeySlots.ClassicACurrent, Provider, "ref"));
                Assert.Throws<ArgumentOutOfRangeException>(() => task.ExpectedKeyHexLength(SecureKeySlots.DesfireRead));
                Assert.Throws<ArgumentOutOfRangeException>(() => task.HasEmbeddedKey(SecureKeySlots.ClassicACurrent));
                Assert.Empty(task.KeyBindings);
            });
        }

        // ── What the migration asks of the task ──

        [Fact]
        public async Task TheMigrationSeesTheKeysThatTheProjectHoldsAsText_AndNotTheBoundOnes()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var task = NewTask();
                IKeyMigrationHost host = task;

                Assert.Equal(
                    SecureKeySlots.Ultralight.OrderBy(slot => slot, StringComparer.Ordinal),
                    host.EmbeddedKeySlots().OrderBy(slot => slot, StringComparer.Ordinal));
                Assert.All(Slots, slot => Assert.Equal(slot.Value.Key, host.GetEmbeddedKey(slot.Key)));

                task.KeyBindings = new[] { Binding(SecureKeySlots.UltralightPassword) };

                Assert.DoesNotContain(SecureKeySlots.UltralightPassword, host.EmbeddedKeySlots());
                Assert.Equal(2, host.EmbeddedKeySlots().Count);
            });
        }
    }
}
