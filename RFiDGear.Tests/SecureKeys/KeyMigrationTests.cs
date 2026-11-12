using RFiDGear.Infrastructure.FileAccess;
using RFiDGear.Infrastructure.Tasks;
using RFiDGear.Models;
using RFiDGear.Services.SecureKeys;
using RFiDGear.ViewModel.TaskSetupViewModels;

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Globalization;
using System.Linq;
using System.Resources;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Xunit;

namespace RFiDGear.Tests.SecureKeys
{
    /// <summary>
    /// The migration moves the typed keys of a project into the vault and writes a new project that names them. These tests run
    /// it on real project files in a folder of their own, with a vault that is a dictionary, and check what matters: the keys
    /// are in the vault and in no file, the new project names them and the old project is not touched, a failure at any step
    /// leaves nothing behind, and nothing that is reported holds a key.
    /// </summary>
    public sealed class KeyMigrationTests : IDisposable
    {
        private static readonly (string Slot, string Property)[] DesfireProperties =
        {
            (SecureKeySlots.DesfirePiccCurrent, "DesfireMasterKeyCurrent"),
            (SecureKeySlots.DesfirePiccNew, "DesfireMasterKeyTarget"),
            (SecureKeySlots.DesfireAppCurrent, "DesfireAppKeyCurrent"),
            (SecureKeySlots.DesfireAppOld, "DesfireAppKeyCurrentOld"),
            (SecureKeySlots.DesfireAppNew, "DesfireAppKeyTarget"),
            (SecureKeySlots.DesfireRead, "DesfireReadKeyCurrent"),
            (SecureKeySlots.DesfireWrite, "DesfireWriteKeyCurrent"),
        };

        private static readonly (string Slot, string Property)[] ClassicProperties =
        {
            (SecureKeySlots.ClassicACurrent, "ClassicKeyAKeyCurrent"),
            (SecureKeySlots.ClassicBCurrent, "ClassicKeyBKeyCurrent"),
            (SecureKeySlots.ClassicATarget, "ClassicKeyAKeyTarget"),
            (SecureKeySlots.ClassicBTarget, "ClassicKeyBKeyTarget"),
            (SecureKeySlots.ClassicMadACurrent, "ClassicMADKeyAKeyCurrent"),
            (SecureKeySlots.ClassicMadBCurrent, "ClassicMADKeyBKeyCurrent"),
            (SecureKeySlots.ClassicMadATarget, "ClassicMADKeyAKeyTarget"),
            (SecureKeySlots.ClassicMadBTarget, "ClassicMADKeyBKeyTarget"),
        };

        // One recognizable key per slot; no key is part of another and none is a number that a project file holds anyway.
        private static readonly Dictionary<string, string> KeyOf = DesfireProperties
            .Select((entry, index) => (entry.Slot, Key: string.Concat(Enumerable.Repeat($"{0xE1 + index:X2}", 8)) + string.Concat(Enumerable.Repeat($"{0xF1 + index:X2}", 8))))
            .Concat(ClassicProperties.Select((entry, index) => (entry.Slot, Key: string.Concat(Enumerable.Repeat($"{0xC1 + index:X2}5A", 3)))))
            .ToDictionary(entry => entry.Slot, entry => entry.Key);

        private readonly string _folder = Path.Combine(Path.GetTempPath(), "rfidgear-migration-" + Guid.NewGuid().ToString("N"));
        private readonly FakeCredentialStore _store = new FakeCredentialStore();
        private readonly DatabaseKeyMigrationProjects _projects = new DatabaseKeyMigrationProjects();

        public KeyMigrationTests()
        {
            Directory.CreateDirectory(_folder);
        }

        public void Dispose()
        {
            _projects.Dispose();

            if (Directory.Exists(_folder))
            {
                Directory.Delete(_folder, recursive: true);
            }
        }

        // ── Moving ──

        [Fact]
        public Task Every_typed_key_is_in_the_vault_and_in_no_file_and_the_new_project_names_it()
        {
            return Sta(async () =>
            {
                var source = WriteProject("source", NewDesfireTask(), NewClassicTask());
                var before = File.ReadAllBytes(source);
                using var logs = CapturedLogs.Install();

                var report = await NewMigration().MoveAsync(source, Target("migrated"));

                Assert.True(report.Succeeded, report.ToString());
                Assert.Equal(15, report.Moved.Count);
                Assert.Equal(15, report.DifferentKeys);
                Assert.Empty(report.NotMoved);
                Assert.Equal(Target("migrated"), report.TargetPath);
                Assert.Equal(before, File.ReadAllBytes(source));
                AssertHoldsNoKey(ProjectText(report.TargetPath));

                // Every key is in the vault under the reference the project names.
                Assert.Equal(15, _store.Blobs.Count);
                Assert.All(report.Moved, moved => Assert.Equal(KeyOf[moved.Slot], VaultKeyOf(moved.Reference), ignoreCase: true));

                // The new project is a project that opens: every moved slot is bound, none has a key typed into it.
                var reloaded = await _projects.ReadAsync(report.TargetPath, CancellationToken.None);
                Assert.NotNull(reloaded);
                Assert.All(report.Moved, moved =>
                {
                    var host = (IKeyMigrationHost)reloaded!.TaskCollection[moved.Task - 1];
                    Assert.Equal(moved.Reference, host.GetKeyBinding(moved.Slot)!.Reference);
                    Assert.Equal(WindowsCredentialKeyProvider.ProviderId, host.GetKeyBinding(moved.Slot)!.ProviderId);
                    Assert.False(host.HasEmbeddedKey(moved.Slot));
                });

                AssertHoldsNoKey(report.ToString());
                Assert.All(logs.All(), logEvent => AssertHoldsNoKey(CapturedLogs.EverythingWrittenFor(logEvent)));
            });
        }

        [Fact]
        public Task Slots_with_the_same_key_share_one_credential()
        {
            return Sta(async () =>
            {
                var source = WriteProject("source", NewClassicTask(), NewClassicTask());

                var report = await NewMigration().MoveAsync(source, Target("migrated"));

                Assert.True(report.Succeeded, report.ToString());
                Assert.Equal(16, report.Moved.Count);
                Assert.Equal(8, report.DifferentKeys);
                Assert.Equal(8, _store.Blobs.Count);
                Assert.All(report.Moved.GroupBy(moved => moved.Slot), slot => Assert.Single(slot.Select(moved => moved.Reference).Distinct()));
            });
        }

        [Fact]
        public Task A_reference_that_is_taken_is_not_overwritten()
        {
            return Sta(async () =>
            {
                var taken = WindowsCredentialKeyProvider.TargetPrefix + "taken";
                _store.Blobs[taken] = new byte[] { 1, 2, 3 };
                var names = new Queue<string>(new[] { "taken" }.Concat(Enumerable.Range(1, 30).Select(i => "fresh" + i)));
                var source = WriteProject("source", NewClassicTask());

                var report = await NewMigration(references: () => names.Dequeue()).MoveAsync(source, Target("migrated"));

                Assert.True(report.Succeeded, report.ToString());
                Assert.Equal(new byte[] { 1, 2, 3 }, _store.Blobs[taken]);
                Assert.DoesNotContain(report.Moved, moved => moved.Reference == "taken");
            });
        }

        [Fact]
        public Task A_key_that_is_not_a_key_of_its_slot_stays_where_it_is_and_is_reported()
        {
            return Sta(async () =>
            {
                var task = NewDesfireTask();
                task.DesfireAppKeyTarget = "A5A5";
                Assert.Equal("A5A5", task.DesfireAppKeyTarget);
                var source = WriteProject("source", task);

                var report = await NewMigration().MoveAsync(source, Target("migrated"));

                Assert.True(report.Succeeded, report.ToString());
                Assert.Equal(6, report.Moved.Count);
                var unmoved = Assert.Single(report.NotMoved);
                Assert.Equal(SecureKeySlots.DesfireAppNew, unmoved.Slot);
                Assert.Equal(1, unmoved.Task);
                Assert.DoesNotContain(report.Moved, moved => moved.Slot == SecureKeySlots.DesfireAppNew);
            });
        }

        [Fact]
        public Task A_project_without_a_typed_key_is_not_written_again()
        {
            return Sta(async () =>
            {
                var task = NewDesfireTask();
                task.KeyBindings = DesfireProperties.Select(entry => new KeyBindingEntry { Slot = entry.Slot, ProviderId = "test.vault", Reference = "cards/" + entry.Slot }).ToArray();
                var source = WriteProject("source", task);

                var report = await NewMigration().MoveAsync(source, Target("migrated"));

                Assert.True(report.NothingToMove, report.ToString());
                Assert.False(report.Succeeded);
                Assert.False(File.Exists(Target("migrated")));
                Assert.Empty(_store.Blobs);
                Assert.Empty(_store.Writes);
            });
        }

        // ── Refusing ──

        [Fact]
        public Task The_project_is_never_written_over_itself_or_over_a_file_that_exists()
        {
            return Sta(async () =>
            {
                var source = WriteProject("source", NewClassicTask());
                var other = WriteProject("other", NewClassicTask());
                var before = File.ReadAllBytes(source);
                var otherBefore = File.ReadAllBytes(other);

                var sameFile = await NewMigration().MoveAsync(source, source);
                var existing = await NewMigration().MoveAsync(source, other);

                Assert.Equal(KeyMigrationFailure.SameFile, sameFile.Failure);
                Assert.Equal(KeyMigrationFailure.TargetExists, existing.Failure);
                Assert.Equal(before, File.ReadAllBytes(source));
                Assert.Equal(otherBefore, File.ReadAllBytes(other));
                Assert.Empty(_store.Writes);
            });
        }

        [Fact]
        public Task A_file_that_is_not_a_project_is_refused_and_nothing_is_stored()
        {
            return Sta(async () =>
            {
                var notAProject = Path.Combine(_folder, "garbage.rfPrj");
                File.WriteAllText(notAProject, "this is not a project");

                var garbage = await NewMigration().MoveAsync(notAProject, Target("migrated"));
                var missing = await NewMigration().MoveAsync(Path.Combine(_folder, "missing.rfPrj"), Target("migrated"));

                Assert.Equal(KeyMigrationFailure.SourceUnreadable, garbage.Failure);
                Assert.Equal(KeyMigrationFailure.SourceUnreadable, missing.Failure);
                Assert.False(File.Exists(Target("migrated")));
                Assert.Empty(_store.Writes);
            });
        }

        [Fact]
        public Task A_name_that_is_no_path_is_refused_as_a_failure_and_nothing_is_stored()
        {
            return Sta(async () =>
            {
                var source = WriteProject("source", NewClassicTask());

                var badSource = await NewMigration().MoveAsync("source\0.rfPrj", Target("migrated"));
                var badTarget = await NewMigration().MoveAsync(source, "migrated\0.rfPrj");

                Assert.Equal(KeyMigrationFailure.SourceUnreadable, badSource.Failure);
                Assert.Equal(KeyMigrationFailure.TargetNotWritten, badTarget.Failure);
                Assert.Empty(_store.Writes);
            });
        }

        // ── Failing: whatever step fails, the vault and the folder are as they were ──

        [Fact]
        public Task A_vault_that_refuses_a_key_leaves_nothing_behind()
        {
            return Sta(async () =>
            {
                _store.WriteError = CredentialErrors.AccessDenied;
                var source = WriteProject("source", NewClassicTask());
                var before = File.ReadAllBytes(source);

                var report = await NewMigration().MoveAsync(source, Target("migrated"));

                Assert.Equal(KeyMigrationFailure.KeyStoreRefused, report.Failure);
                Assert.Equal(CredentialErrors.AccessDenied, report.FailureDetail);
                AssertNothingLeft(source, before);
            });
        }

        [Fact]
        public Task A_vault_that_fails_at_the_third_key_gives_back_the_two_before()
        {
            return Sta(async () =>
            {
                var flaky = new FlakyStore(_store) { FailWriteNumber = 3 };
                var source = WriteProject("source", NewClassicTask());
                var before = File.ReadAllBytes(source);

                var report = await NewMigration(store: flaky).MoveAsync(source, Target("migrated"));

                Assert.Equal(KeyMigrationFailure.KeyStoreRefused, report.Failure);
                Assert.Equal(2, _store.Writes.Count);
                Assert.Equal(2, _store.Deletes.Count);
                AssertNothingLeft(source, before);
            });
        }

        [Fact]
        public Task A_credential_that_cannot_be_taken_back_is_counted_in_the_log_without_naming_it()
        {
            return Sta(async () =>
            {
                using var logs = CapturedLogs.Install();
                var source = WriteProject("source", NewClassicTask());
                var flaky = new FlakyStore(_store) { FailWriteNumber = 2, DeleteError = CredentialErrors.AccessDenied };

                var report = await NewMigration(store: flaky).MoveAsync(source, Target("migrated"));

                Assert.Equal(KeyMigrationFailure.KeyStoreRefused, report.Failure);
                var warning = Assert.Single(logs.From(nameof(KeyMigration)), logEvent => logEvent.RenderMessage().Contains("could not be removed", StringComparison.Ordinal));
                Assert.Contains("1", warning.RenderMessage(), StringComparison.Ordinal);
                Assert.DoesNotContain(WindowsCredentialKeyProvider.TargetPrefix, warning.RenderMessage(), StringComparison.Ordinal);
            });
        }

        [Fact]
        public Task A_key_that_does_not_come_back_as_it_went_in_is_not_used()
        {
            return Sta(async () =>
            {
                var source = WriteProject("source", NewClassicTask());
                var before = File.ReadAllBytes(source);

                var report = await NewMigration(store: new FlakyStore(_store) { CorruptReads = true }).MoveAsync(source, Target("migrated"));

                Assert.Equal(KeyMigrationFailure.ReadBackDiffers, report.Failure);
                AssertNothingLeft(source, before);
            });
        }

        [Fact]
        public Task A_new_project_that_cannot_be_written_leaves_nothing_behind()
        {
            return Sta(async () =>
            {
                var source = WriteProject("source", NewClassicTask());
                var before = File.ReadAllBytes(source);
                var projects = new ProjectsDouble(_projects) { OnWrite = (_, _) => null };
                var throwing = new ProjectsDouble(_projects) { OnWrite = (_, _) => throw new IOException("disk full") };

                var refused = await NewMigration(projects).MoveAsync(source, Target("migrated"));
                var failed = await NewMigration(throwing).MoveAsync(source, Target("migrated"));

                Assert.Equal(KeyMigrationFailure.TargetNotWritten, refused.Failure);
                Assert.Equal(KeyMigrationFailure.TargetNotWritten, failed.Failure);
                AssertNothingLeft(source, before);
            });
        }

        [Fact]
        public Task A_new_project_that_still_holds_a_key_is_deleted_and_the_keys_are_taken_back()
        {
            return Sta(async () =>
            {
                var source = WriteProject("source", NewClassicTask());
                var before = File.ReadAllBytes(source);
                var projects = new ProjectsDouble(_projects) { OnText = (_, text) => text + KeyOf[SecureKeySlots.ClassicACurrent].ToLowerInvariant() };

                var report = await NewMigration(projects).MoveAsync(source, Target("migrated"));

                Assert.Equal(KeyMigrationFailure.VerificationFailed, report.Failure);
                AssertNothingLeft(source, before);
            });
        }

        [Fact]
        public Task A_new_project_whose_references_do_not_give_the_keys_is_deleted_and_the_keys_are_taken_back()
        {
            return Sta(async () =>
            {
                var source = WriteProject("source", NewClassicTask());
                var before = File.ReadAllBytes(source);

                // The text is looked at first when the new project is checked; the vault is empty by the time it is asked.
                var projects = new ProjectsDouble(_projects) { OnText = (_, text) => { _store.Blobs.Clear(); return text; } };

                var report = await NewMigration(projects).MoveAsync(source, Target("migrated"));

                Assert.Equal(KeyMigrationFailure.VerificationFailed, report.Failure);
                AssertNothingLeft(source, before);
            });
        }

        [Fact]
        public Task A_new_project_with_a_slot_that_is_not_bound_is_deleted_and_the_keys_are_taken_back()
        {
            return Sta(async () =>
            {
                var source = WriteProject("source", NewClassicTask());
                var before = File.ReadAllBytes(source);
                var projects = new ProjectsDouble(_projects)
                {
                    OnWrite = (model, path) =>
                    {
                        ((MifareClassicSetupViewModel)model.TaskCollection[0]).UnbindKeySlot(SecureKeySlots.ClassicBTarget);

                        return _projects.Write(model, path);
                    },
                };

                var report = await NewMigration(projects).MoveAsync(source, Target("migrated"));

                Assert.Equal(KeyMigrationFailure.VerificationFailed, report.Failure);
                AssertNothingLeft(source, before);
            });
        }

        [Fact]
        public Task Giving_up_after_the_first_key_was_stored_takes_it_back()
        {
            return Sta(async () =>
            {
                using var cancel = new CancellationTokenSource();
                var source = WriteProject("source", NewClassicTask());
                var before = File.ReadAllBytes(source);
                var flaky = new FlakyStore(_store) { AfterWrite = cancel.Cancel };

                var report = await NewMigration(store: flaky).MoveAsync(source, Target("migrated"), cancel.Token);

                Assert.Equal(KeyMigrationFailure.Cancelled, report.Failure);
                Assert.Single(_store.Writes);
                AssertNothingLeft(source, before);
            });
        }

        [Fact]
        public Task A_run_that_is_cancelled_before_it_starts_stores_nothing()
        {
            return Sta(async () =>
            {
                using var cancel = new CancellationTokenSource();
                cancel.Cancel();
                var source = WriteProject("source", NewClassicTask());
                var before = File.ReadAllBytes(source);

                var report = await NewMigration().MoveAsync(source, Target("migrated"), cancel.Token);

                Assert.Equal(KeyMigrationFailure.Cancelled, report.Failure);
                Assert.Empty(_store.Writes);
                AssertNothingLeft(source, before);
            });
        }

        // ── The old project ──

        [Fact]
        public Task The_old_project_is_deleted_only_after_the_new_one_has_been_checked_again()
        {
            return Sta(async () =>
            {
                var source = WriteProject("source", NewClassicTask());
                var migration = NewMigration();
                var report = await migration.MoveAsync(source, Target("migrated"));
                Assert.True(report.Succeeded, report.ToString());

                // The person emptied the vault in between: the new project would not work, so the old one must stay.
                var kept = _store.Blobs.ToDictionary(pair => pair.Key, pair => pair.Value);
                _store.Blobs.Clear();
                Assert.False(await migration.DeleteOriginalAsync(report));
                Assert.True(File.Exists(source));

                foreach (var pair in kept)
                {
                    _store.Blobs[pair.Key] = pair.Value;
                }

                Assert.True(await migration.DeleteOriginalAsync(report));
                Assert.False(File.Exists(source));
                Assert.True(File.Exists(report.TargetPath));
            });
        }

        [Fact]
        public Task Nothing_is_deleted_for_a_migration_that_did_not_succeed()
        {
            return Sta(async () =>
            {
                var source = WriteProject("source", NewClassicTask());
                var migration = NewMigration();
                var report = await migration.MoveAsync(source, Target("migrated"));
                Assert.True(report.Succeeded, report.ToString());

                // Everything but the failure is as it is after a success: the new project is there and gives its keys.
                var failed = report with { Failure = KeyMigrationFailure.VerificationFailed };
                // The same file on both sides: deleting "the original" would delete the new project.
                var oneFile = report with { SourcePath = report.TargetPath };

                Assert.False(await migration.DeleteOriginalAsync(failed));
                Assert.False(await migration.DeleteOriginalAsync(oneFile));
                Assert.True(File.Exists(source));
                Assert.True(File.Exists(report.TargetPath));
            });
        }

        [Fact]
        public Task A_check_that_throws_keeps_the_old_project()
        {
            return Sta(async () =>
            {
                var source = WriteProject("source", NewClassicTask());
                var report = await NewMigration().MoveAsync(source, Target("migrated"));
                Assert.True(report.Succeeded, report.ToString());
                var throwing = new ThrowingReads(_projects);

                var deleted = await NewMigration(throwing).DeleteOriginalAsync(report);

                Assert.False(deleted);
                Assert.True(File.Exists(source));
            });
        }

        // ── Review of #321: what can change between the check and the act ──

        [Fact]
        public Task A_project_of_a_newer_version_is_refused_like_the_application_refuses_it()
        {
            return Sta(async () =>
            {
                var source = WriteProjectOfVersion("source", "99.0.0", NewClassicTask());

                var report = await NewMigration().MoveAsync(source, Target("migrated"));

                Assert.Equal(KeyMigrationFailure.SourceUnreadable, report.Failure);
                Assert.False(File.Exists(Target("migrated")));
                Assert.Empty(_store.Writes);
            });
        }

        [Fact]
        public Task A_file_that_appears_while_the_new_project_is_written_is_kept()
        {
            return Sta(async () =>
            {
                var source = WriteProject("source", NewClassicTask());
                var projects = new ProjectsDouble(_projects)
                {
                    OnWrite = (model, path) =>
                    {
                        // Someone creates the file after the migration looked and before the project is put in place.
                        File.WriteAllText(path, "someone else's file");

                        return _projects.Write(model, path);
                    },
                };

                var report = await NewMigration(projects).MoveAsync(source, Target("migrated"));

                Assert.Equal(KeyMigrationFailure.TargetNotWritten, report.Failure);
                Assert.Equal("someone else's file", File.ReadAllText(Target("migrated")));
                Assert.False(report.LeftBehind);
                Assert.Empty(_store.Blobs);
            });
        }

        [Fact]
        public Task A_project_that_is_saved_while_it_is_read_is_not_migrated()
        {
            return Sta(async () =>
            {
                var source = WriteProject("source", NewClassicTask());
                var projects = new ProjectsDouble(_projects) { AfterRead = path => File.AppendAllText(path, "saved meanwhile") };

                var report = await NewMigration(projects).MoveAsync(source, Target("migrated"));

                Assert.Equal(KeyMigrationFailure.SourceUnreadable, report.Failure);
                Assert.Empty(_store.Writes);
                Assert.False(File.Exists(Target("migrated")));
            });
        }

        [Fact]
        public Task The_old_project_is_kept_when_it_was_saved_after_the_migration()
        {
            return Sta(async () =>
            {
                var source = WriteProject("source", NewClassicTask());
                var migration = NewMigration();
                var report = await migration.MoveAsync(source, Target("migrated"));
                Assert.True(report.Succeeded, report.ToString());

                // The project is saved again, with more in it: the new project holds the older state.
                WriteProject("source", NewClassicTask(), NewClassicTask());

                Assert.False(await migration.DeleteOriginalAsync(report));
                Assert.True(File.Exists(source));
            });
        }

        [Fact]
        public Task The_old_project_is_kept_when_it_is_saved_while_the_new_one_is_being_checked()
        {
            return Sta(async () =>
            {
                var source = WriteProject("source", NewClassicTask());
                var report = await NewMigration().MoveAsync(source, Target("migrated"));
                Assert.True(report.Succeeded, report.ToString());

                // The check reads the new project last; a save lands right then, after the first look at the old one.
                var projects = new ProjectsDouble(_projects)
                {
                    AfterRead = path =>
                    {
                        if (string.Equals(path, report.TargetPath, StringComparison.OrdinalIgnoreCase))
                        {
                            File.AppendAllText(source, "saved meanwhile");
                        }
                    },
                };

                Assert.False(await NewMigration(projects).DeleteOriginalAsync(report));
                Assert.True(File.Exists(source));
            });
        }

        [Fact]
        public Task The_old_project_is_kept_when_a_credential_now_holds_another_key_of_the_same_length()
        {
            return Sta(async () =>
            {
                var source = WriteProject("source", NewClassicTask());
                var migration = NewMigration();
                var report = await migration.MoveAsync(source, Target("migrated"));
                Assert.True(report.Succeeded, report.ToString());

                // Same length, valid digits, not the key: only a comparison of the key itself notices.
                var first = report.Moved[0];
                _store.Blobs[WindowsCredentialKeyProvider.TargetPrefix + first.Reference] = Encoding.Unicode.GetBytes("ABABABABABAB");

                Assert.False(await migration.DeleteOriginalAsync(report));
                Assert.True(File.Exists(source));
            });
        }

        [Fact]
        public Task The_old_project_is_kept_when_the_new_project_now_names_another_provider()
        {
            return Sta(async () =>
            {
                var source = WriteProject("source", NewClassicTask());
                var migration = NewMigration();
                var report = await migration.MoveAsync(source, Target("migrated"));
                Assert.True(report.Succeeded, report.ToString());

                // The reference stays and resolves in the Credential Manager, but a run would ask another provider for it.
                var migrated = await _projects.ReadAsync(report.TargetPath, CancellationToken.None);
                var first = report.Moved[0];
                ((IKeyMigrationHost)migrated!.TaskCollection[first.Task - 1]).BindKeySlot(first.Slot, "other.provider", first.Reference);
                new DatabaseReaderWriter(new ProjectManager(Path.Combine(_folder, "appdata"))).WriteDatabase(migrated, report.TargetPath);

                Assert.False(await migration.DeleteOriginalAsync(report));
                Assert.True(File.Exists(source));
            });
        }

        [Fact]
        public Task A_failed_run_that_cannot_undo_everything_names_what_is_left()
        {
            return Sta(async () =>
            {
                var source = WriteProject("source", NewClassicTask());
                var flaky = new FlakyStore(_store) { FailWriteNumber = 2, DeleteError = CredentialErrors.AccessDenied };
                var dialogs = new DialogsDouble { Source = source, Target = Target("migrated") };

                await NewFlow(dialogs, flaky).RunAsync();

                // The first key was stored and could not be removed again: it is in the vault, and the message names it.
                var stored = Assert.Single(_store.Blobs.Keys);
                var (text, isError) = Assert.Single(dialogs.Informed);
                Assert.True(isError);
                Assert.Contains(stored, text, StringComparison.Ordinal);
                Assert.DoesNotContain(Text("messageKeyMigrationUndone"), text, StringComparison.Ordinal);
            });
        }

        [Fact]
        public Task A_new_project_that_cannot_be_deleted_after_a_failure_is_named()
        {
            return Sta(async () =>
            {
                var source = WriteProject("source", NewClassicTask());
                FileStream? hold = null;
                var projects = new ProjectsDouble(_projects)
                {
                    // The check fails (a key in the text) while something has the new file open: it cannot be deleted.
                    OnText = (path, text) =>
                    {
                        hold = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);

                        return text + KeyOf[SecureKeySlots.ClassicACurrent];
                    },
                };

                try
                {
                    var report = await NewMigration(projects).MoveAsync(source, Target("migrated"));

                    Assert.Equal(KeyMigrationFailure.VerificationFailed, report.Failure);
                    Assert.Equal(Target("migrated"), report.UndeletedTarget);
                    Assert.True(report.LeftBehind);
                    Assert.Empty(_store.Blobs);
                }
                finally
                {
                    hold?.Dispose();
                }
            });
        }

        // ── The dialogs ──

        [Fact]
        public Task Nothing_happens_until_the_person_has_agreed_and_chosen_both_files()
        {
            return Sta(async () =>
            {
                var source = WriteProject("source", NewClassicTask());

                var declined = new DialogsDouble { Go = false, Source = source, Target = Target("migrated") };
                await NewFlow(declined).RunAsync();
                Assert.Equal(new[] { "confirm" }, declined.Calls);
                Assert.Equal(Text("messageKeyMigrationIntro"), declined.Introduction);

                var noSource = new DialogsDouble { Source = null, Target = Target("migrated") };
                await NewFlow(noSource).RunAsync();
                Assert.Equal(new[] { "confirm", "source" }, noSource.Calls);

                var noTarget = new DialogsDouble { Source = source, Target = null };
                await NewFlow(noTarget).RunAsync();
                Assert.Equal(new[] { "confirm", "source", "target" }, noTarget.Calls);

                Assert.Empty(_store.Writes);
                Assert.False(File.Exists(Target("migrated")));
            });
        }

        [Fact]
        public Task A_successful_migration_is_reported_without_a_key_and_the_old_project_stays_unless_the_person_says_so()
        {
            return Sta(async () =>
            {
                var source = WriteProject("source", NewClassicTask());
                var dialogs = new DialogsDouble { Source = source, Target = Target("migrated"), Delete = false };

                await NewFlow(dialogs).RunAsync();

                Assert.Equal(new[] { "confirm", "source", "target", "working", "inform", "askDelete" }, dialogs.Calls);
                Assert.Equal(Path.Combine(_folder, "source.secured.rfPrj"), dialogs.SuggestedTarget);
                var (done, isError) = Assert.Single(dialogs.Informed);
                Assert.False(isError);
                Assert.Contains(Target("migrated"), done, StringComparison.Ordinal);
                Assert.Contains("8 key slots", done, StringComparison.Ordinal);
                Assert.Contains("classic.a.current", done, StringComparison.Ordinal);
                Assert.Contains(source, dialogs.DeleteQuestion, StringComparison.Ordinal);
                AssertHoldsNoKey(done);
                Assert.True(File.Exists(source));
                Assert.Equal(0, dialogs.OpenWork);
            });
        }

        [Fact]
        public Task The_old_project_is_deleted_when_the_person_says_so()
        {
            return Sta(async () =>
            {
                var source = WriteProject("source", NewClassicTask());
                var dialogs = new DialogsDouble { Source = source, Target = Target("migrated"), Delete = true };

                await NewFlow(dialogs).RunAsync();

                Assert.False(File.Exists(source));
                Assert.True(File.Exists(Target("migrated")));
                var (last, isError) = dialogs.Informed.Last();
                Assert.False(isError);
                Assert.Equal(Text("messageKeyMigrationDeleted"), last);
                Assert.Equal(0, dialogs.OpenWork);
            });
        }

        [Fact]
        public Task A_long_report_lists_the_first_slots_only()
        {
            return Sta(async () =>
            {
                var source = WriteProject("source", NewClassicTask(), NewClassicTask());
                var dialogs = new DialogsDouble { Source = source, Target = Target("migrated") };

                await NewFlow(dialogs).RunAsync();

                var done = dialogs.Informed.First().Text;
                Assert.Contains("16 key slots", done, StringComparison.Ordinal);
                Assert.Equal(12, done.Split('\n').Count(line => line.Contains("classic.", StringComparison.Ordinal)));
                Assert.Contains("...", done, StringComparison.Ordinal);
            });
        }

        [Fact]
        public Task A_failure_is_told_as_one_and_nothing_is_offered_to_delete()
        {
            return Sta(async () =>
            {
                _store.WriteError = CredentialErrors.AccessDenied;
                var source = WriteProject("source", NewClassicTask());
                var dialogs = new DialogsDouble { Source = source, Target = Target("migrated"), Delete = true };

                await NewFlow(dialogs).RunAsync();

                var (text, isError) = Assert.Single(dialogs.Informed);
                Assert.True(isError);
                Assert.Contains($"(error {CredentialErrors.AccessDenied})", text, StringComparison.Ordinal);
                Assert.DoesNotContain("askDelete", dialogs.Calls);
                Assert.True(File.Exists(source));
                Assert.Equal(0, dialogs.OpenWork);
            });
        }

        [Fact]
        public Task A_project_without_typed_keys_is_told_so_and_nothing_is_offered_to_delete()
        {
            return Sta(async () =>
            {
                var source = WriteProject("source");
                var dialogs = new DialogsDouble { Source = source, Target = Target("migrated"), Delete = true };

                await NewFlow(dialogs).RunAsync();

                var (text, isError) = Assert.Single(dialogs.Informed);
                Assert.False(isError);
                Assert.Equal(Text("messageKeyMigrationNothing"), text);
                Assert.DoesNotContain("askDelete", dialogs.Calls);
                Assert.False(File.Exists(Target("migrated")));
            });
        }

        // ── Helpers ──

        private static readonly ResourceManager Manifest = new ResourceManager("RFiDGear.Resources.Manifest", typeof(KeyMigration).Assembly);

        // The texts of the application, so that the placeholders of the messages are checked too.
        private static string Text(string key) => Manifest.GetString(key, CultureInfo.GetCultureInfo("en"))!;

        private KeyMigrationFlow NewFlow(IKeyMigrationDialogs dialogs, ICredentialStore? store = null) =>
            new KeyMigrationFlow(NewMigration(store: store), dialogs, Text);

        private static Task Sta(Func<Task> test) => StaTestRunner.RunOnStaThreadAsync(test);

        private string Target(string name) => Path.Combine(_folder, name + ".rfPrj");

        private KeyMigration NewMigration(IKeyMigrationProjects? projects = null, ICredentialStore? store = null, Func<string>? references = null) =>
            new KeyMigration(projects ?? _projects, new WindowsCredentialKeyProvider(store ?? _store), references);

        private string WriteProject(string name, params object[] tasks) => WriteProjectOfVersion(name, null, tasks);

        // A project as an application of another version wrote it: the manifest names that version.
        private string WriteProjectOfVersion(string name, string? manifestVersion, params object[] tasks)
        {
            var model = new ChipTaskHandlerModel();

            if (manifestVersion != null)
            {
                model.ManifestVersion = manifestVersion;
            }

            foreach (var task in tasks)
            {
                model.TaskCollection.Add(task);
            }

            var path = Path.Combine(_folder, name + ".rfPrj");
            new DatabaseReaderWriter(new ProjectManager(Path.Combine(_folder, "appdata"))).WriteDatabase(model, path);

            return path;
        }

        private string ProjectText(string path)
        {
            using var archive = ZipFile.OpenRead(path);
            using var reader = new StreamReader(archive.GetEntry("taskdatabase.xml")!.Open());

            return reader.ReadToEnd();
        }

        private string VaultKeyOf(string reference) => Encoding.Unicode.GetString(_store.Blobs[WindowsCredentialKeyProvider.TargetPrefix + reference]);

        // No key in any form a file or a message could show it: as it is, and in pairs separated by a space.
        private static void AssertHoldsNoKey(string text)
        {
            foreach (var key in KeyOf.Values)
            {
                Assert.DoesNotContain(key, text, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(string.Join(" ", Enumerable.Range(0, key.Length / 2).Select(i => key.Substring(i * 2, 2))), text, StringComparison.OrdinalIgnoreCase);
            }
        }

        // A failed migration: the old project is as it was, no new project, no credential.
        private void AssertNothingLeft(string source, byte[] sourceBefore)
        {
            Assert.Equal(sourceBefore, File.ReadAllBytes(source));
            Assert.False(File.Exists(Target("migrated")));
            Assert.Empty(_store.Blobs);
        }

        // The shape in which the old application key is a slot of its own: the key being replaced is not the one that
        // authenticates. The old key is set last, because setting the others would copy the application key over it.
        private static MifareDesfireSetupViewModel NewDesfireTask()
        {
            var task = new MifareDesfireSetupViewModel
            {
                SelectedTaskType = TaskType_MifareDesfireTask.ApplicationKeyChangeover,
                CurrentTaskIndex = "0",
                AppNumberCurrent = "1",
                SelectedDesfireAppKeyNumberCurrent = "1",
            };

            foreach (var (slot, property) in DesfireProperties.Where(entry => entry.Property != "DesfireAppKeyCurrentOld"))
            {
                typeof(MifareDesfireSetupViewModel).GetProperty(property)!.SetValue(task, KeyOf[slot]);
            }

            task.DesfireAppKeyCurrentOld = KeyOf[SecureKeySlots.DesfireAppOld];
            Assert.Equal(KeyOf[SecureKeySlots.DesfireAppOld], task.DesfireAppKeyCurrentOld);

            return task;
        }

        private static MifareClassicSetupViewModel NewClassicTask()
        {
            var task = new MifareClassicSetupViewModel { CurrentTaskIndex = "0" };

            foreach (var (slot, property) in ClassicProperties)
            {
                typeof(MifareClassicSetupViewModel).GetProperty(property)!.SetValue(task, KeyOf[slot]);
            }

            return task;
        }

        /// <summary>A person who answers as told, and notes what was asked.</summary>
        private sealed class DialogsDouble : IKeyMigrationDialogs
        {
            public bool Go { get; init; } = true;

            public string? Source { get; init; }

            public string? Target { get; init; }

            public bool Delete { get; init; }

            public List<string> Calls { get; } = new List<string>();

            public List<(string Text, bool IsError)> Informed { get; } = new List<(string, bool)>();

            public string? SuggestedTarget { get; private set; }

            public string? DeleteQuestion { get; private set; }

            public int OpenWork { get; private set; }

            public string? Introduction { get; private set; }

            public bool Confirm(string text)
            {
                Calls.Add("confirm");
                Introduction = text;

                return Go;
            }

            public string? ChooseSource()
            {
                Calls.Add("source");

                return Source;
            }

            public string? ChooseTarget(string suggestedPath)
            {
                Calls.Add("target");
                SuggestedTarget = suggestedPath;

                return Target;
            }

            public void Inform(string text, bool isError)
            {
                Calls.Add("inform");
                Informed.Add((text, isError));
            }

            public bool AskDeleteOriginal(string text)
            {
                Calls.Add("askDelete");
                DeleteQuestion = text;

                return Delete;
            }

            public IDisposable Working()
            {
                Calls.Add("working");
                OpenWork++;

                return new Scope(this);
            }

            private sealed class Scope : IDisposable
            {
                private DialogsDouble? _owner;

                public Scope(DialogsDouble owner)
                {
                    _owner = owner;
                }

                public void Dispose()
                {
                    if (_owner != null)
                    {
                        _owner.OpenWork--;
                        _owner = null;
                    }
                }
            }
        }

        /// <summary>The real projects, except for what a test changes.</summary>
        private sealed class ProjectsDouble : IKeyMigrationProjects
        {
            private readonly IKeyMigrationProjects _inner;

            public ProjectsDouble(IKeyMigrationProjects inner)
            {
                _inner = inner;
            }

            public Func<ChipTaskHandlerModel, string, string?>? OnWrite { get; init; }

            public Func<string, string, string>? OnText { get; init; }

            public Action<string>? AfterRead { get; init; }

            public string TargetPathOf(string path) => _inner.TargetPathOf(path);

            public async Task<ChipTaskHandlerModel?> ReadAsync(string path, CancellationToken cancellationToken)
            {
                var model = await _inner.ReadAsync(path, cancellationToken).ConfigureAwait(true);
                AfterRead?.Invoke(path);

                return model;
            }

            public string? Fingerprint(string path) => _inner.Fingerprint(path);

            public string? Write(ChipTaskHandlerModel model, string path) => OnWrite != null ? OnWrite(model, path) : _inner.Write(model, path);

            public string? ReadProjectText(string path)
            {
                var text = _inner.ReadProjectText(path);

                return text != null && OnText != null ? OnText(path, text) : text;
            }
        }

        /// <summary>Projects that cannot be read: the reader throws what it was not expected to.</summary>
        private sealed class ThrowingReads : IKeyMigrationProjects
        {
            private readonly IKeyMigrationProjects _inner;

            public ThrowingReads(IKeyMigrationProjects inner)
            {
                _inner = inner;
            }

            public string TargetPathOf(string path) => _inner.TargetPathOf(path);

            public Task<ChipTaskHandlerModel?> ReadAsync(string path, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();

                throw new InvalidDataException("not a project: " + path);
            }

            public string? Fingerprint(string path) => _inner.Fingerprint(path);

            public string? Write(ChipTaskHandlerModel model, string path) => _inner.Write(model, path);

            public string? ReadProjectText(string path) => _inner.ReadProjectText(path);
        }

        /// <summary>A vault that fails, or lies, or notes what happened, on request. Everything else is the fake.</summary>
        private sealed class FlakyStore : ICredentialStore
        {
            private readonly FakeCredentialStore _inner;
            private int _writes;

            public FlakyStore(FakeCredentialStore inner)
            {
                _inner = inner;
            }

            public int FailWriteNumber { get; init; }

            public int DeleteError { get; init; }

            public bool CorruptReads { get; init; }

            public Action? AfterWrite { get; init; }

            public CredentialRead Read(string targetName)
            {
                var read = _inner.Read(targetName);

                if (!CorruptReads || read.Blob == null)
                {
                    return read;
                }

                var wrong = (byte[])read.Blob.Clone();
                wrong[0] ^= 0x01;

                return new CredentialRead(0, wrong);
            }

            public int Write(string targetName, byte[] blob)
            {
                _writes++;

                if (_writes == FailWriteNumber)
                {
                    return CredentialErrors.AccessDenied;
                }

                var error = _inner.Write(targetName, blob);
                AfterWrite?.Invoke();

                return error;
            }

            public int Delete(string targetName) => DeleteError != 0 ? DeleteError : _inner.Delete(targetName);
        }
    }
}
