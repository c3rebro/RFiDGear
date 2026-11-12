using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.FileAccess;
using RFiDGear.Infrastructure.Tasks;
using RFiDGear.Models;
using RFiDGear.Services.SecureKeys;
using RFiDGear.UI.MVVMDialogs.ViewModels.Interfaces;
using RFiDGear.ViewModel.TaskSetupViewModels;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Serialization;

using Xunit;

namespace RFiDGear.Tests.SecureKeys
{
    /// <summary>
    /// A task can say that a key slot gets its key from a provider. These tests check what that does to the project file: the
    /// reference is written, the key of that slot is not (not into the archive, and not into the staging copy either), a task
    /// without bindings is written exactly as before, and a reference survives whatever the loader does to the text.
    /// </summary>
    public class KeyBindingPersistenceTests
    {
        private const string Provider = "test.vault";
        private const string Reference = "cards/main";

        // One recognizable key per slot, in the form the view models keep.
        private static readonly Dictionary<string, (string Property, string Key)> DesfireSlots = new Dictionary<string, (string, string)>
        {
            [SecureKeySlots.DesfirePiccCurrent] = ("DesfireMasterKeyCurrent", "A1A1A1A1A1A1A1A1B1B1B1B1B1B1B1B1"),
            [SecureKeySlots.DesfirePiccNew] = ("DesfireMasterKeyTarget", "A2A2A2A2A2A2A2A2B2B2B2B2B2B2B2B2"),
            [SecureKeySlots.DesfireAppCurrent] = ("DesfireAppKeyCurrent", "A3A3A3A3A3A3A3A3B3B3B3B3B3B3B3B3"),
            [SecureKeySlots.DesfireAppOld] = ("DesfireAppKeyCurrentOld", "A4A4A4A4A4A4A4A4B4B4B4B4B4B4B4B4"),
            [SecureKeySlots.DesfireAppNew] = ("DesfireAppKeyTarget", "A5A5A5A5A5A5A5A5B5B5B5B5B5B5B5B5"),
            [SecureKeySlots.DesfireRead] = ("DesfireReadKeyCurrent", "A6A6A6A6A6A6A6A6B6B6B6B6B6B6B6B6"),
            [SecureKeySlots.DesfireWrite] = ("DesfireWriteKeyCurrent", "A7A7A7A7A7A7A7A7B7B7B7B7B7B7B7B7"),
        };

        private static readonly Dictionary<string, (string Property, string Key)> ClassicSlots = new Dictionary<string, (string, string)>
        {
            [SecureKeySlots.ClassicACurrent] = ("ClassicKeyAKeyCurrent", "A1A1A1A1A1A1"),
            [SecureKeySlots.ClassicBCurrent] = ("ClassicKeyBKeyCurrent", "B1B1B1B1B1B1"),
            [SecureKeySlots.ClassicATarget] = ("ClassicKeyAKeyTarget", "A2A2A2A2A2A2"),
            [SecureKeySlots.ClassicBTarget] = ("ClassicKeyBKeyTarget", "B2B2B2B2B2B2"),
            [SecureKeySlots.ClassicMadACurrent] = ("ClassicMADKeyAKeyCurrent", "C1C1C1C1C1C1"),
            [SecureKeySlots.ClassicMadBCurrent] = ("ClassicMADKeyBKeyCurrent", "D1D1D1D1D1D1"),
            [SecureKeySlots.ClassicMadATarget] = ("ClassicMADKeyAKeyTarget", "C2C2C2C2C2C2"),
            [SecureKeySlots.ClassicMadBTarget] = ("ClassicMADKeyBKeyTarget", "D2D2D2D2D2D2"),
        };

        public static IEnumerable<object[]> EveryDesfireSlot() => SecureKeySlots.Desfire.Select(slot => new object[] { slot });

        public static IEnumerable<object[]> EveryClassicSlot() => SecureKeySlots.Classic.Select(slot => new object[] { slot });

        // ── Writing ──

        [Fact]
        public async Task A_task_without_bindings_is_saved_as_before()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var xml = Serialize(NewDesfireTask());

                Assert.DoesNotContain("KeyBindings", xml, StringComparison.Ordinal);
                Assert.All(DesfireSlots.Values, slot => AssertHasKey(xml, slot.Key));
            });
        }

        [Fact]
        public async Task A_classic_task_without_bindings_is_saved_as_before()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var xml = Serialize(NewClassicTask());

                Assert.DoesNotContain("KeyBindings", xml, StringComparison.Ordinal);
                Assert.All(ClassicSlots.Values, slot => AssertHasKey(xml, slot.Key));
            });
        }

        [Theory]
        [MemberData(nameof(EveryDesfireSlot))]
        public async Task A_bound_desfire_slot_is_saved_as_a_reference_and_without_its_key(string slot)
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var task = NewDesfireTask();
                task.KeyBindings = new[] { Binding(slot) };

                var xml = Serialize(task);

                AssertWritesBinding(xml, slot);
                AssertNoKey(xml, DesfireSlots[slot].Key);
                Assert.All(DesfireSlots.Where(other => other.Key != slot), other => AssertHasKey(xml, other.Value.Key));
            });
        }

        [Theory]
        [MemberData(nameof(EveryClassicSlot))]
        public async Task A_bound_classic_slot_is_saved_as_a_reference_and_without_its_key(string slot)
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var task = NewClassicTask();
                task.KeyBindings = new[] { Binding(slot) };

                var xml = Serialize(task);

                AssertWritesBinding(xml, slot);
                AssertNoKey(xml, ClassicSlots[slot].Key);
                Assert.All(ClassicSlots.Where(other => other.Key != slot), other => AssertHasKey(xml, other.Value.Key));
            });
        }

        [Fact]
        public async Task A_bound_slots_key_reaches_neither_the_archive_nor_the_staging_copy()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var folder = new TempFolder();
                var projectManager = new ProjectManager(folder.Combine("appdata"));
                var readerWriter = new DatabaseReaderWriter(projectManager);
                var task = NewDesfireTask();
                var bound = new[] { SecureKeySlots.DesfirePiccCurrent, SecureKeySlots.DesfireAppNew, SecureKeySlots.DesfireWrite };
                task.KeyBindings = bound.Select(Binding).ToArray();
                var project = new ChipTaskHandlerModel();
                project.TaskCollection.Add(task);
                var target = folder.Combine("project.rfPrj");

                readerWriter.WriteDatabase(project, target);

                var inTheArchive = ReadArchiveXml(target);
                var inTheStagingCopy = File.ReadAllText(projectManager.TaskDatabasePath);
                foreach (var text in new[] { inTheArchive, inTheStagingCopy })
                {
                    Assert.All(DesfireSlots.Where(slot => bound.Contains(slot.Key)), slot => AssertNoKey(text, slot.Value.Key));
                    Assert.All(DesfireSlots.Where(slot => !bound.Contains(slot.Key)), slot => AssertHasKey(text, slot.Value.Key));
                }

                var (result, loaded) = await readerWriter.ReadProjectAsync(target);
                Assert.True(result.Success);
                var reloaded = Assert.IsType<MifareDesfireSetupViewModel>(Assert.Single(loaded.TaskCollection));
                Assert.Equal(bound.OrderBy(slot => slot), reloaded.KeyBindings.Select(entry => entry.Slot).OrderBy(slot => slot));
                Assert.All(reloaded.KeyBindings, entry => Assert.Equal((Provider, Reference), (entry.ProviderId, entry.Reference)));
                Assert.All(bound, slot => Assert.True(reloaded.IsKeyBound(slot)));
            });
        }

        // ── Reading ──

        [Fact]
        public async Task A_file_with_both_a_key_and_a_binding_for_a_slot_keeps_the_binding_and_loses_the_key_on_saving()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                // Hand-written, as a tool or an earlier edit might leave it: the embedded key is still there.
                var xml = ProjectOf(
                    "<DesfireAppKeyCurrent>" + DesfireSlots[SecureKeySlots.DesfireAppCurrent].Key + "</DesfireAppKeyCurrent>" +
                    "<KeyBindings><KeyBindingEntry><Slot>desfire.app.current</Slot><ProviderId>" + Provider + "</ProviderId><Reference>" + Reference + "</Reference></KeyBindingEntry></KeyBindings>");

                var task = Assert.IsType<MifareDesfireSetupViewModel>(Assert.Single(Deserialize(xml).TaskCollection));

                Assert.True(task.IsKeyBound(SecureKeySlots.DesfireAppCurrent));
                Assert.Equal(DesfireSlots[SecureKeySlots.DesfireAppCurrent].Key, task.DesfireAppKeyCurrent);
                AssertNoKey(Serialize(task), DesfireSlots[SecureKeySlots.DesfireAppCurrent].Key);
            });
        }

        [Fact]
        public async Task The_old_key_that_only_mirrors_a_bound_application_key_is_not_written_either()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                // A read task shows no old-key inputs, so the old key is a copy of the application key.
                var task = new MifareDesfireSetupViewModel { SelectedTaskType = TaskType_MifareDesfireTask.ReadData };
                task.DesfireAppKeyCurrent = DesfireSlots[SecureKeySlots.DesfireAppCurrent].Key;
                Assert.Equal(task.DesfireAppKeyCurrent, task.DesfireAppKeyCurrentOld);
                task.KeyBindings = new[] { Binding(SecureKeySlots.DesfireAppCurrent) };

                var xml = Serialize(task);

                AssertNoKey(xml, DesfireSlots[SecureKeySlots.DesfireAppCurrent].Key);
            });
        }

        [Fact]
        public async Task An_entry_for_a_slot_nobody_knows_survives_a_round_trip()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var task = NewDesfireTask();
                task.KeyBindings = new[] { Binding("desfire.from.the.future"), Binding(SecureKeySlots.DesfireRead) };

                var reloaded = Assert.IsType<MifareDesfireSetupViewModel>(Assert.Single(Deserialize(Serialize(task)).TaskCollection));

                Assert.Equal(new[] { "desfire.from.the.future", SecureKeySlots.DesfireRead }, reloaded.KeyBindings.Select(entry => entry.Slot));
            });
        }

        [Fact]
        public async Task A_reference_that_contains_the_old_error_name_comes_back_unchanged()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var folder = new TempFolder();
                var readerWriter = new DatabaseReaderWriter(new ProjectManager(folder.Combine("appdata")));
                var task = NewDesfireTask();
                task.KeyBindings = new[]
                {
                    new KeyBindingEntry { Slot = SecureKeySlots.DesfireAppCurrent, ProviderId = "authenticationerror.vault", Reference = "AuthenticationError" },
                    new KeyBindingEntry { Slot = SecureKeySlots.DesfireRead, ProviderId = Provider, Reference = "keys/AuthenticationError/read" },
                };
                var project = new ChipTaskHandlerModel();
                project.TaskCollection.Add(task);
                var target = folder.Combine("project.rfPrj");

                readerWriter.WriteDatabase(project, target);
                var (result, loaded) = await readerWriter.ReadProjectAsync(target);

                Assert.True(result.Success);
                var reloaded = Assert.IsType<MifareDesfireSetupViewModel>(Assert.Single(loaded.TaskCollection));
                Assert.Equal(
                    new[] { "AuthenticationError", "keys/AuthenticationError/read" },
                    reloaded.KeyBindings.Select(entry => entry.Reference));
            });
        }

        [Fact]
        public void The_old_error_name_is_still_normalized_where_an_error_code_is_stored()
        {
            var manager = new ProjectManager(Path.Combine(Path.GetTempPath(), "rfidgear-norm-" + Guid.NewGuid().ToString("N")));
            const string Legacy =
                "<SelectedExecuteConditionErrorLevel>AuthenticationError</SelectedExecuteConditionErrorLevel>" +
                "<Checkpoint><ErrorLevel>AuthenticationError</ErrorLevel></Checkpoint>";

            var normalized = manager.NormalizeLegacyProjectXml(Legacy);

            Assert.Equal(
                "<SelectedExecuteConditionErrorLevel>AuthFailure</SelectedExecuteConditionErrorLevel>" +
                "<Checkpoint><ErrorLevel>AuthFailure</ErrorLevel></Checkpoint>",
                normalized);
        }

        [Fact]
        public void The_old_error_name_is_left_alone_anywhere_else()
        {
            var manager = new ProjectManager(Path.Combine(Path.GetTempPath(), "rfidgear-norm-" + Guid.NewGuid().ToString("N")));
            const string Xml =
                "<Reference>AuthenticationError</Reference><ProviderId>x.AuthenticationError</ProviderId>" +
                "<TaskDescription>AuthenticationError handling</TaskDescription>";

            Assert.Equal(Xml, manager.NormalizeLegacyProjectXml(Xml));
        }

        // ── The list ──

        [Fact]
        public async Task The_list_is_copied_going_in_and_coming_out()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var task = new MifareDesfireSetupViewModel();
                var given = new[] { Binding(SecureKeySlots.DesfireRead) };

                task.KeyBindings = given;
                given[0].Reference = "changed.after.setting";
                var firstRead = task.KeyBindings;
                firstRead[0].Reference = "changed.after.reading";
                firstRead[0] = Binding(SecureKeySlots.DesfireWrite);

                var binding = Assert.Single(task.KeyBindings);
                Assert.Equal(SecureKeyTestReference, binding.Reference);
                Assert.Equal(SecureKeySlots.DesfireRead, binding.Slot);
                Assert.Same(task.KeyBindings.GetType(), typeof(KeyBindingEntry[]));
            });
        }

        [Fact]
        public async Task The_classic_list_is_copied_going_in_and_coming_out_and_cleaned_of_gaps()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var task = new MifareClassicSetupViewModel();
                var given = new[] { Binding(SecureKeySlots.ClassicACurrent), null! };

                task.KeyBindings = given;
                given[0].Reference = "changed.after.setting";
                var firstRead = task.KeyBindings;
                firstRead[0].Reference = "changed.after.reading";
                firstRead[0] = Binding(SecureKeySlots.ClassicBCurrent);

                var binding = Assert.Single(task.KeyBindings);
                Assert.Equal(Reference, binding.Reference);
                Assert.Equal(SecureKeySlots.ClassicACurrent, binding.Slot);

                task.KeyBindings = null!;
                Assert.Empty(task.KeyBindings);
                Assert.False(task.ShouldSerializeKeyBindings());
            });
        }

        [Fact]
        public async Task Setting_nothing_or_a_list_with_gaps_gives_an_empty_or_clean_list()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var task = new MifareDesfireSetupViewModel { KeyBindings = null! };
                Assert.Empty(task.KeyBindings);
                Assert.False(task.ShouldSerializeKeyBindings());

                task.KeyBindings = new[] { null!, Binding(SecureKeySlots.DesfireRead) };

                Assert.Equal(new[] { SecureKeySlots.DesfireRead }, task.KeyBindings.Select(entry => entry.Slot));
            });
        }

        [Fact]
        public async Task Editing_a_task_in_the_dialog_cannot_change_the_task_through_its_bindings()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var original = NewDesfireTask();
                original.KeyBindings = new[] { Binding(SecureKeySlots.DesfireAppCurrent) };

                var editor = new MifareDesfireSetupViewModel(original, new ObservableCollection<IDialogViewModel>());
                Assert.Equal(SecureKeySlots.DesfireAppCurrent, Assert.Single(editor.KeyBindings).Slot);

                editor.KeyBindings[0].Reference = "edited.in.place";
                editor.KeyBindings = new[] { Binding(SecureKeySlots.DesfireRead) };

                Assert.Equal(SecureKeySlots.DesfireAppCurrent, Assert.Single(original.KeyBindings).Slot);
                Assert.Equal(SecureKeyTestReference, Assert.Single(original.KeyBindings).Reference);
            });
        }

        [Fact]
        public async Task Editing_a_classic_task_in_the_dialog_cannot_change_the_task_through_its_bindings()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var original = NewClassicTask();
                original.KeyBindings = new[] { Binding(SecureKeySlots.ClassicACurrent) };

                var editor = new MifareClassicSetupViewModel(original, new ObservableCollection<IDialogViewModel>());
                Assert.Equal(SecureKeySlots.ClassicACurrent, Assert.Single(editor.KeyBindings).Slot);

                editor.KeyBindings = new[] { Binding(SecureKeySlots.ClassicBCurrent) };

                Assert.Equal(SecureKeySlots.ClassicACurrent, Assert.Single(original.KeyBindings).Slot);
            });
        }

        // ── Nothing forgotten ──

        [Fact]
        public async Task Every_embedded_key_property_of_the_desfire_task_has_a_slot_that_keeps_it_out_of_the_file()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
                AssertEveryKeyPropertyHasASlot<MifareDesfireSetupViewModel>(DesfireSlots, SecureKeySlots.Desfire, NewDesfireTask));
        }

        [Fact]
        public async Task Every_embedded_key_property_of_the_classic_task_has_a_slot_that_keeps_it_out_of_the_file()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
                AssertEveryKeyPropertyHasASlot<MifareClassicSetupViewModel>(ClassicSlots, SecureKeySlots.Classic, NewClassicTask));
        }

        private static void AssertEveryKeyPropertyHasASlot<T>(
            IReadOnlyDictionary<string, (string Property, string Key)> slots,
            IReadOnlyList<string> allSlots,
            Func<T> create)
            where T : class
        {
            // A key property added later without a slot would be written in the clear next to a reference. Names end the
            // same way for every key of both tasks.
            var keyProperties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.PropertyType == typeof(string) && property.CanRead && property.CanWrite)
                .Where(property => Regex.IsMatch(property.Name, "Key(Current|Target|CurrentOld)$"))
                .Select(property => property.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList();

            Assert.Equal(slots.Values.Select(slot => slot.Property).OrderBy(name => name, StringComparer.Ordinal), keyProperties);
            Assert.Equal(allSlots.OrderBy(slot => slot, StringComparer.Ordinal), slots.Keys.OrderBy(slot => slot, StringComparer.Ordinal));

            foreach (var (slot, (property, _)) in slots)
            {
                var task = create();
                var shouldSerialize = typeof(T).GetMethod("ShouldSerialize" + property, BindingFlags.Public | BindingFlags.Instance);
                Assert.NotNull(shouldSerialize);
                Assert.Equal(typeof(bool), shouldSerialize!.ReturnType);

                Assert.True((bool)shouldSerialize.Invoke(task, null)!, property + " is written while it is not bound");

                typeof(T).GetProperty("KeyBindings")!.SetValue(task, new[] { Binding(slot) });

                Assert.False((bool)shouldSerialize.Invoke(task, null)!, property + " is written although " + slot + " is bound");
            }
        }

        // ── Helpers ──

        private const string SecureKeyTestReference = Reference;

        private static KeyBindingEntry Binding(string slot) => new KeyBindingEntry { Slot = slot, ProviderId = Provider, Reference = Reference };

        private static MifareDesfireSetupViewModel NewDesfireTask()
        {
            // The shape in which the old application key is a slot of its own: the key being replaced is not the one that
            // authenticates. The old key is set last, because setting the others would copy the application key over it.
            var task = new MifareDesfireSetupViewModel
            {
                SelectedTaskType = TaskType_MifareDesfireTask.ApplicationKeyChangeover,
                CurrentTaskIndex = "0",
                AppNumberCurrent = "1",
                SelectedDesfireAppKeyNumberCurrent = "1",
            };

            foreach (var (property, key) in DesfireSlots.Values.Where(slot => slot.Property != "DesfireAppKeyCurrentOld"))
            {
                typeof(MifareDesfireSetupViewModel).GetProperty(property)!.SetValue(task, key);
            }

            task.DesfireAppKeyCurrentOld = DesfireSlots[SecureKeySlots.DesfireAppOld].Key;
            Assert.Equal(DesfireSlots[SecureKeySlots.DesfireAppOld].Key, task.DesfireAppKeyCurrentOld);

            return task;
        }

        private static MifareClassicSetupViewModel NewClassicTask()
        {
            var task = new MifareClassicSetupViewModel { CurrentTaskIndex = "0" };

            foreach (var (property, key) in ClassicSlots.Values)
            {
                typeof(MifareClassicSetupViewModel).GetProperty(property)!.SetValue(task, key);
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

        private static string ProjectOf(string taskElements) =>
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
            "<ChipTaskHandlerModel xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\">" +
            "<ManifestVersion>2.0.10</ManifestVersion><TaskCollection>" +
            "<anyType xsi:type=\"MifareDesfireSetupViewModel\"><SelectedTaskType>ReadData</SelectedTaskType>" + taskElements + "</anyType>" +
            "</TaskCollection></ChipTaskHandlerModel>";

        private static string ReadArchiveXml(string archivePath)
        {
            using var archive = ZipFile.OpenRead(archivePath);
            using var reader = new StreamReader(archive.GetEntry("taskdatabase.xml")!.Open());

            return reader.ReadToEnd();
        }

        private static void AssertWritesBinding(string xml, string slot)
        {
            Assert.Contains("<KeyBindings>", xml, StringComparison.Ordinal);
            Assert.Contains($"<Slot>{slot}</Slot>", xml, StringComparison.Ordinal);
            Assert.Contains($"<ProviderId>{Provider}</ProviderId>", xml, StringComparison.Ordinal);
            Assert.Contains($"<Reference>{Reference}</Reference>", xml, StringComparison.Ordinal);
        }

        // The key in the forms a file could show it: as it is kept, in lower case, and in pairs separated by a space.
        private static IEnumerable<string> FormsOf(string key)
        {
            yield return key;
            yield return string.Join(" ", Enumerable.Range(0, key.Length / 2).Select(i => key.Substring(i * 2, 2)));
        }

        private static void AssertNoKey(string text, string key) =>
            Assert.All(FormsOf(key), form => Assert.DoesNotContain(form, text, StringComparison.OrdinalIgnoreCase));

        private static void AssertHasKey(string text, string key) =>
            Assert.Contains(key, text, StringComparison.OrdinalIgnoreCase);

        private sealed class TempFolder : IDisposable
        {
            private readonly string _root = Path.Combine(Path.GetTempPath(), "rfidgear-keys-" + Guid.NewGuid().ToString("N"));

            public TempFolder()
            {
                Directory.CreateDirectory(_root);
            }

            public string Combine(string name) => Path.Combine(_root, name);

            public void Dispose()
            {
                Directory.Delete(_root, true);
            }
        }
    }
}
