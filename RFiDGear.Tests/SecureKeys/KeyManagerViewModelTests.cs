#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Text;

using RFiDGear.Services.SecureKeys;

using Xunit;

namespace RFiDGear.Tests.SecureKeys
{
    /// <summary>
    /// What the key manager shows and does for the keys of a section of a task: one row for each slot, the switch between a
    /// typed key and a reference, the key store and the name, storing a key, and whether a program can supply keys. The task,
    /// the vault and the supplier are doubles, so none of it needs a window.
    /// </summary>
    public class KeyManagerViewModelTests
    {
        private const string Prefix = WindowsCredentialKeyProvider.TargetPrefix;

        private static readonly SecureKeyProviderInfo CredentialManager = new SecureKeyProviderInfo(WindowsCredentialKeyProvider.ProviderId, "Windows Credential Manager");
        private static readonly SecureKeyProviderInfo ExternalApplication = new SecureKeyProviderInfo(BridgeKeyProvider.ProviderId, "Connected external application");

        private sealed class Rig : IDisposable
        {
            internal Rig(string[] slots, Action<Action>? post = null, int hexLength = 32)
            {
                Host.HexLength = hexLength;
                ViewModel = KeyManagerViewModel.Create(
                    Host,
                    slots,
                    new[] { CredentialManager, ExternalApplication },
                    Dialogs,
                    new WindowsCredentialKeyProvider(Store),
                    Supplier,
                    TestText.English,
                    post);
            }

            internal FakeKeySlotHost Host { get; } = new FakeKeySlotHost();

            internal ScriptedKeyDialogs Dialogs { get; } = new ScriptedKeyDialogs();

            internal FakeCredentialStore Store { get; } = new FakeCredentialStore();

            internal FakeSupplierStatus Supplier { get; } = new FakeSupplierStatus();

            internal KeyManagerViewModel ViewModel { get; }

            internal KeyManagerSlotRow Row => ViewModel.Rows[0];

            public void Dispose() => ViewModel.Dispose();
        }

        // ── The rows ──

        [Fact]
        public void There_is_one_row_for_each_slot_in_the_order_given_with_the_name_of_the_slot()
        {
            using var rig = new Rig(new[] { SecureKeySlots.DesfireAppCurrent, SecureKeySlots.DesfireAppNew });

            Assert.Equal(new[] { SecureKeySlots.DesfireAppCurrent, SecureKeySlots.DesfireAppNew }, rig.ViewModel.Rows.Select(row => row.SlotId).ToArray());
            Assert.Equal(new[] { "Application key, current", "Application key, new" }, rig.ViewModel.Rows.Select(row => row.SlotName).ToArray());
            Assert.True(rig.ViewModel.HasRows);
        }

        [Fact]
        public void A_slot_that_is_given_twice_has_one_row_and_a_section_without_slots_has_none()
        {
            using var twice = new Rig(new[] { SecureKeySlots.DesfireRead, SecureKeySlots.DesfireRead });
            using var none = new Rig(Array.Empty<string>());

            Assert.Single(twice.ViewModel.Rows);
            Assert.Empty(none.ViewModel.Rows);
            Assert.False(none.ViewModel.HasRows);
        }

        [Fact]
        public void A_slot_this_version_does_not_know_is_named_by_its_id()
        {
            using var rig = new Rig(new[] { "future.slot" });

            Assert.Equal("future.slot", rig.Row.SlotName);
        }

        [Theory]
        [InlineData("en")]
        [InlineData("de")]
        public void Every_slot_has_a_name_in_both_languages(string culture)
        {
            var text = TestText.In(culture);

            foreach (var slot in SecureKeySlots.Desfire.Concat(SecureKeySlots.Classic))
            {
                var key = KeySlotNames.ResourceKeyOf(slot);

                Assert.NotNull(key);
                Assert.False(string.IsNullOrWhiteSpace(text(key!)), slot + " (" + culture + ")");
            }
        }

        [Theory]
        [InlineData("en", "saves it in the project", "The project saves only the name, never the key.")]
        [InlineData("de", "im Projekt gespeichert", "Das Projekt speichert nur den Namen, nie den Schlüssel.")]
        public void The_intro_says_that_a_typed_key_is_saved_in_the_project_and_only_a_reference_is_not(string culture, string typedKeyIsSaved, string claimOfAnyKey)
        {
            // A key that is typed into the task is serialized with it. Only a reference keeps the key out of the project, and the text
            // must not promise that for every key.
            var intro = TestText.In(culture)("labelKeyManagerIntro");

            Assert.Contains(typedKeyIsSaved, intro);
            Assert.DoesNotContain(claimOfAnyKey, intro);
        }

        [Fact]
        public void A_text_is_read_from_the_resources_once_however_often_the_window_asks_for_it()
        {
            // The resources read the settings from the disk on every lookup, and a keystroke in a name makes every row ask for all
            // its texts again: the manager keeps what it was given for as long as it lives.
            var asked = new Dictionary<string, int>();
            string Counting(string key)
            {
                asked[key] = asked.TryGetValue(key, out var count) ? count + 1 : 1;

                return TestText.English(key);
            }

            var host = new FakeKeySlotHost();
            using var viewModel = KeyManagerViewModel.Create(
                host,
                new[] { SecureKeySlots.DesfireRead, SecureKeySlots.DesfireWrite },
                new[] { CredentialManager, ExternalApplication },
                new ScriptedKeyDialogs(),
                new WindowsCredentialKeyProvider(new FakeCredentialStore()),
                new FakeSupplierStatus(),
                Counting);

            for (var round = 0; round < 5; round++)
            {
                foreach (var row in viewModel.Rows)
                {
                    row.ToggleModeCommand.Execute(null);
                    row.Reference = "cards/main" + round;
                    _ = (row.SlotName, row.ModeButtonText, row.TypedKeyText, row.KeyStoreLabel, row.KeyNameLabel, row.KeyPrompt,
                        row.ModeToolTip, row.ProviderToolTip, row.ReferenceToolTip, row.StoreButtonText, row.StoreNewToolTip);
                }

                viewModel.RefreshSupplierStatus();
                _ = (viewModel.Title, viewModel.IntroText, viewModel.NoSlotsText, viewModel.CloseText, viewModel.SupplierStatusText);
            }

            Assert.NotEmpty(asked);
            Assert.All(asked, pair => Assert.True(pair.Value == 1, pair.Key + " was read " + pair.Value + " times"));
        }

        [Fact]
        public void The_texts_come_in_the_language_of_the_application()
        {
            using var rig = new Rig(new[] { SecureKeySlots.DesfireRead });

            Assert.Equal("Key manager", rig.ViewModel.Title);
            Assert.False(string.IsNullOrWhiteSpace(rig.ViewModel.IntroText));
            Assert.False(string.IsNullOrWhiteSpace(rig.ViewModel.CloseText));
            Assert.Equal("Use a reference", rig.Row.ModeButtonText);
        }

        // ── Switching ──

        [Fact]
        public void Using_a_reference_binds_the_slot_to_the_credential_manager_with_no_name_yet_and_using_the_key_unbinds_it()
        {
            using var rig = new Rig(new[] { SecureKeySlots.DesfireRead });
            Assert.True(rig.Row.UsesTypedKey);

            rig.Row.ToggleModeCommand.Execute(null);

            Assert.Equal($"bind {SecureKeySlots.DesfireRead} {WindowsCredentialKeyProvider.ProviderId} ''", Assert.Single(rig.Host.Calls));
            Assert.True(rig.Row.IsReferenceMode);
            Assert.False(rig.Row.UsesTypedKey);
            Assert.Equal("Use the typed key", rig.Row.ModeButtonText);

            rig.Row.ToggleModeCommand.Execute(null);

            Assert.Equal($"unbind {SecureKeySlots.DesfireRead}", rig.Host.Calls.Last());
            Assert.True(rig.Row.UsesTypedKey);
        }

        [Fact]
        public void A_typed_key_is_asked_about_before_it_is_discarded_and_a_refusal_changes_nothing()
        {
            using var rig = new Rig(new[] { SecureKeySlots.DesfireRead });
            rig.Host.EmbeddedKeys[SecureKeySlots.DesfireRead] = "A1B2";
            rig.Dialogs.Confirm = false;

            rig.Row.ToggleModeCommand.Execute(null);

            Assert.Equal(1, rig.Dialogs.Confirmations);
            Assert.Empty(rig.Host.Calls);
            Assert.True(rig.Row.UsesTypedKey);
            Assert.Equal("A1B2", rig.Host.EmbeddedKeys[SecureKeySlots.DesfireRead]);
        }

        [Fact]
        public void The_key_store_and_the_name_reach_the_task_and_the_external_application_is_recognised()
        {
            using var rig = new Rig(new[] { SecureKeySlots.DesfireRead });
            rig.Row.ToggleModeCommand.Execute(null);

            rig.Row.ProviderId = BridgeKeyProvider.ProviderId;
            rig.Row.Reference = "orders/4711";

            Assert.Equal($"bind {SecureKeySlots.DesfireRead} {BridgeKeyProvider.ProviderId} 'orders/4711'", rig.Host.Calls.Last());
            Assert.Equal(BridgeKeyProvider.ProviderId, rig.Row.ProviderId);
            Assert.Equal("orders/4711", rig.Row.Reference);
            Assert.True(rig.Row.IsReferenceValid);
            Assert.True(rig.Row.UsesExternalApplication);
            Assert.False(rig.Row.ProviderOffersStoreKey);
        }

        [Fact]
        public void A_slot_with_a_typed_key_takes_no_key_store_and_no_name()
        {
            using var rig = new Rig(new[] { SecureKeySlots.DesfireRead });

            rig.Row.ProviderId = BridgeKeyProvider.ProviderId;
            rig.Row.Reference = "orders/4711";

            Assert.Empty(rig.Host.Calls);
            Assert.False(rig.Row.UsesExternalApplication);
        }

        [Fact]
        public void The_row_says_that_everything_changed_when_the_task_changes_its_bindings()
        {
            using var rig = new Rig(new[] { SecureKeySlots.DesfireRead });
            var raised = new List<string?>();
            rig.Row.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

            rig.Host.BindKeySlot(SecureKeySlots.DesfireRead, WindowsCredentialKeyProvider.ProviderId, "cards/main");

            Assert.Contains(string.Empty, raised);
            Assert.True(rig.Row.IsReferenceMode);
        }

        // ── Choosing a stored key ──

        [Fact]
        public void With_the_credential_manager_the_stored_keys_are_offered_by_name_in_order_and_no_key_is_read()
        {
            using var rig = new Rig(new[] { SecureKeySlots.DesfireRead });
            rig.Store.Blobs[Prefix + "cards/b"] = Encoding.Unicode.GetBytes("A1B2C3D4E5F6");
            rig.Store.Blobs[Prefix + "cards/a"] = Encoding.Unicode.GetBytes("A1B2C3D4E5F6");

            rig.Row.ToggleModeCommand.Execute(null);

            Assert.True(rig.Row.ShowStoredKeyChoice);
            Assert.False(rig.Row.UsesFreeTextReference);
            Assert.Equal(new[] { "cards/a", "cards/b" }, rig.Row.StoredKeys.Select(key => key.Reference));
            Assert.Equal(new[] { "cards/a", "cards/b" }, rig.Row.StoredKeys.Select(key => key.Text));
            Assert.False(rig.Row.HasNoStoredKeys);
            Assert.Empty(rig.Store.Reads);
        }

        [Fact]
        public void Choosing_a_stored_key_binds_the_slot_to_it()
        {
            using var rig = new Rig(new[] { SecureKeySlots.DesfireRead });
            rig.Store.Blobs[Prefix + "cards/main"] = Encoding.Unicode.GetBytes("A1B2C3D4E5F6");
            rig.Row.ToggleModeCommand.Execute(null);

            rig.Row.Reference = "cards/main";

            Assert.Equal($"bind {SecureKeySlots.DesfireRead} {WindowsCredentialKeyProvider.ProviderId} 'cards/main'", rig.Host.Calls.Last());
            Assert.Equal("cards/main", rig.Row.Reference);
        }

        [Fact]
        public void A_reference_that_is_not_stored_stays_in_the_list_and_says_so_and_a_cleared_choice_changes_nothing()
        {
            using var rig = new Rig(new[] { SecureKeySlots.DesfireRead });
            rig.Host.BindKeySlot(SecureKeySlots.DesfireRead, WindowsCredentialKeyProvider.ProviderId, "gone/key");
            rig.Host.Calls.Clear();
            rig.Row.RefreshStoredKeys();

            var missing = Assert.Single(rig.Row.StoredKeys);
            Assert.Equal("gone/key", missing.Reference);
            Assert.Equal("gone/key (not stored)", missing.Text);

            // A list box that is emptied while its items are replaced reports no choice at all; the binding is not the person's choice.
            rig.Row.Reference = null!;

            Assert.Empty(rig.Host.Calls);
            Assert.Equal("gone/key", rig.Row.Reference);
        }

        [Fact]
        public void Without_a_stored_key_the_row_says_so_and_the_slot_has_no_choice_yet()
        {
            using var rig = new Rig(new[] { SecureKeySlots.DesfireRead });
            rig.Row.ToggleModeCommand.Execute(null);

            Assert.Empty(rig.Row.StoredKeys);
            Assert.True(rig.Row.HasNoStoredKeys);
            Assert.Equal("No key is stored yet. Create one below.", rig.Row.NoStoredKeysText);
            Assert.False(rig.Row.HasStoredKeysError);
        }

        [Fact]
        public void A_list_that_cannot_be_read_says_why_and_not_that_no_key_is_stored()
        {
            using var rig = new Rig(new[] { SecureKeySlots.DesfireRead });
            rig.Store.EnumerateError = CredentialErrors.AccessDenied;
            rig.Row.ToggleModeCommand.Execute(null);

            Assert.True(rig.Row.HasStoredKeysError);
            Assert.Equal("The stored keys could not be listed (error 5).", rig.Row.StoredKeysErrorText);
            Assert.False(rig.Row.HasNoStoredKeys);
        }

        [Fact]
        public void Another_key_store_takes_a_typed_name_and_has_no_list_and_no_place_to_create_a_key()
        {
            using var rig = new Rig(new[] { SecureKeySlots.DesfireRead });
            rig.Row.ToggleModeCommand.Execute(null);

            rig.Row.ProviderId = BridgeKeyProvider.ProviderId;

            Assert.True(rig.Row.UsesFreeTextReference);
            Assert.False(rig.Row.ShowStoredKeyChoice);
            Assert.False(rig.Row.CanStoreKey);
        }

        [Fact]
        public void Switching_to_the_credential_manager_reads_the_list_again()
        {
            using var rig = new Rig(new[] { SecureKeySlots.DesfireRead });
            rig.Row.ToggleModeCommand.Execute(null);
            rig.Row.ProviderId = BridgeKeyProvider.ProviderId;
            rig.Store.Blobs[Prefix + "cards/late"] = Encoding.Unicode.GetBytes("A1B2C3D4E5F6");

            rig.Row.ProviderId = WindowsCredentialKeyProvider.ProviderId;

            Assert.Equal(new[] { "cards/late" }, rig.Row.StoredKeys.Select(key => key.Reference));
        }

        [Fact]
        public void A_stored_key_that_differs_from_the_name_of_the_slot_only_in_case_is_the_key_of_the_slot()
        {
            using var rig = new Rig(new[] { SecureKeySlots.DesfireRead });
            rig.Store.Blobs[Prefix + "Cards/Main"] = Encoding.Unicode.GetBytes("A1B2C3D4E5F6");
            rig.Host.BindKeySlot(SecureKeySlots.DesfireRead, WindowsCredentialKeyProvider.ProviderId, "cards/main");
            rig.Row.RefreshStoredKeys();

            // The names in the Credential Manager do not depend on case, so the key exists. The choice has the spelling of the slot,
            // which is what the list selects by, and the text of the vault.
            var choice = Assert.Single(rig.Row.StoredKeys);
            Assert.Equal("cards/main", choice.Reference);
            Assert.Equal("Cards/Main", choice.Text);
        }

        [Fact]
        public void A_list_that_cannot_be_read_does_not_say_that_the_key_of_the_slot_is_not_stored()
        {
            using var rig = new Rig(new[] { SecureKeySlots.DesfireRead });
            rig.Host.BindKeySlot(SecureKeySlots.DesfireRead, WindowsCredentialKeyProvider.ProviderId, "cards/main");
            rig.Store.EnumerateError = CredentialErrors.AccessDenied;

            rig.Row.RefreshStoredKeys();

            // Only a listing that worked can say that a key is missing; this one only says that it is not known, and the slot's own
            // name stays selectable.
            var choice = Assert.Single(rig.Row.StoredKeys);
            Assert.Equal("cards/main", choice.Reference);
            Assert.Equal("cards/main", choice.Text);
            Assert.True(rig.Row.HasStoredKeysError);
        }

        // ── Creating a new key ──

        [Fact]
        public void A_key_that_is_created_for_one_slot_is_offered_to_every_other_slot_that_uses_the_credential_manager()
        {
            using var rig = new Rig(new[] { SecureKeySlots.DesfireRead, SecureKeySlots.DesfireWrite });
            var first = rig.ViewModel.Rows[0];
            var second = rig.ViewModel.Rows[1];
            first.ToggleModeCommand.Execute(null);
            second.ToggleModeCommand.Execute(null);
            second.Reference = "cards/two";
            first.NewName = "cards/new";

            using (var entered = TestText.Secure("000102030405060708090A0B0C0D0E0F"))
            {
                Assert.True(first.StoreEntered(entered));
            }

            Assert.Contains("cards/new", second.StoredKeys.Select(key => key.Reference));

            // What the other slot names is not the choice of the person who made a key for the first.
            Assert.Equal("cards/two", second.Reference);
        }

        [Theory]
        [InlineData("cards/main", true)]
        [InlineData("a.b_c-d:1/2", true)]
        [InlineData("", false)]
        [InlineData("a b", false)]
        public void A_key_can_be_stored_only_under_a_valid_name(string name, bool expected)
        {
            using var rig = new Rig(new[] { SecureKeySlots.DesfireRead });
            rig.Row.ToggleModeCommand.Execute(null);

            rig.Row.NewName = name;

            Assert.Equal(expected, rig.Row.CanStoreKey);
        }

        [Fact]
        public void A_new_key_ends_in_the_vault_under_the_new_name_the_slot_uses_it_and_the_row_says_so()
        {
            using var rig = new Rig(new[] { SecureKeySlots.DesfireRead });
            rig.Row.ToggleModeCommand.Execute(null);
            rig.Row.NewName = "cards/new";
            var raised = new List<string?>();
            rig.Row.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

            using var entered = TestText.Secure("000102030405060708090a0b0c0d0e0f");
            var tried = rig.Row.StoreEntered(entered);

            Assert.True(tried);
            Assert.Equal(Encoding.Unicode.GetBytes("000102030405060708090A0B0C0D0E0F"), rig.Store.Blobs[Prefix + "cards/new"]);
            Assert.Equal("cards/new", rig.Row.Reference);
            Assert.Equal(new[] { "cards/new" }, rig.Row.StoredKeys.Select(key => key.Reference));
            Assert.Equal(string.Empty, rig.Row.NewName);
            Assert.True(rig.Row.HasResult);
            Assert.False(rig.Row.ResultIsError);
            Assert.Equal("The key was stored under the reference.", rig.Row.ResultText);
            Assert.Contains(nameof(KeyManagerSlotRow.ResultText), raised);
        }

        [Fact]
        public void A_name_that_is_stored_already_is_not_used_again_the_row_points_to_the_list_and_the_digits_are_kept()
        {
            using var rig = new Rig(new[] { SecureKeySlots.DesfireRead });
            rig.Store.Blobs[Prefix + "cards/main"] = Encoding.Unicode.GetBytes("A1B2C3D4E5F6");
            rig.Row.ToggleModeCommand.Execute(null);
            rig.Row.NewName = "Cards/Main";

            using var entered = TestText.Secure("000102030405060708090A0B0C0D0E0F");

            Assert.False(rig.Row.StoreEntered(entered));
            Assert.Empty(rig.Store.Writes);
            Assert.True(rig.Row.ResultIsError);
            Assert.Equal("A key named 'Cards/Main' is stored already. Choose it in the list above; to replace its key, use File > Key manager.", rig.Row.ResultText);
            Assert.Equal("Cards/Main", rig.Row.NewName);
        }

        [Fact]
        public void A_name_that_was_added_after_the_list_was_read_is_in_the_list_when_the_row_says_that_it_is_in_use()
        {
            using var rig = new Rig(new[] { SecureKeySlots.DesfireRead });
            rig.Row.ToggleModeCommand.Execute(null);
            Assert.Empty(rig.Row.StoredKeys);

            // Another program (cmdkey) stores a key after the row read the list; the row finds out when it is asked to store a new one.
            rig.Store.Blobs[Prefix + "cards/late"] = Encoding.Unicode.GetBytes("A1B2C3D4E5F6");
            rig.Row.NewName = "cards/late";

            using var entered = TestText.Secure("000102030405060708090A0B0C0D0E0F");

            Assert.False(rig.Row.StoreEntered(entered));
            Assert.Empty(rig.Store.Writes);
            Assert.Contains("stored already", rig.Row.ResultText);

            // The message sends the person to the list, so the list has the key.
            Assert.Equal(new[] { "cards/late" }, rig.Row.StoredKeys.Select(key => key.Reference));
        }

        [Fact]
        public void A_list_that_cannot_be_read_stores_nothing_and_says_so_and_the_digits_are_kept()
        {
            using var rig = new Rig(new[] { SecureKeySlots.DesfireRead });
            rig.Row.ToggleModeCommand.Execute(null);
            rig.Row.NewName = "cards/new";
            rig.Store.EnumerateError = CredentialErrors.AccessDenied;

            using var entered = TestText.Secure("000102030405060708090A0B0C0D0E0F");

            Assert.False(rig.Row.StoreEntered(entered));
            Assert.Empty(rig.Store.Writes);
            Assert.Equal("The stored keys could not be listed (error 5), so nothing was stored.", rig.Row.ResultText);
            Assert.True(rig.Row.ResultIsError);
        }

        [Fact]
        public void A_key_of_the_wrong_length_is_not_stored_the_row_says_how_many_digits_it_needs_and_the_digits_are_kept()
        {
            using var rig = new Rig(new[] { SecureKeySlots.ClassicACurrent }, hexLength: 12);
            rig.Row.ToggleModeCommand.Execute(null);
            rig.Row.NewName = "cards/new";

            using var entered = TestText.Secure("0102");

            Assert.False(rig.Row.StoreEntered(entered));
            Assert.Empty(rig.Store.Writes);
            Assert.True(rig.Row.ResultIsError);
            Assert.Contains("12", rig.Row.ResultText);
        }

        [Fact]
        public void A_vault_that_refuses_gives_an_error_the_row_shows_with_its_code_and_the_slot_is_not_changed()
        {
            using var rig = new Rig(new[] { SecureKeySlots.DesfireRead });
            rig.Store.WriteError = 5;
            rig.Row.ToggleModeCommand.Execute(null);
            rig.Row.NewName = "cards/new";

            using var entered = TestText.Secure("000102030405060708090A0B0C0D0E0F");

            Assert.True(rig.Row.StoreEntered(entered));
            Assert.True(rig.Row.ResultIsError);
            Assert.Contains("5", rig.Row.ResultText);
            Assert.Equal(string.Empty, rig.Row.Reference);
        }

        [Fact]
        public void Nothing_is_tried_and_nothing_is_said_when_no_key_can_be_stored_under_the_name()
        {
            using var rig = new Rig(new[] { SecureKeySlots.DesfireRead });
            rig.Row.ToggleModeCommand.Execute(null);

            using var entered = TestText.Secure("000102030405060708090A0B0C0D0E0F");

            Assert.False(rig.Row.StoreEntered(entered));
            Assert.Empty(rig.Store.Writes);
            Assert.False(rig.Row.HasResult);
        }

        [Fact]
        public void A_new_name_makes_the_old_result_disappear()
        {
            using var rig = new Rig(new[] { SecureKeySlots.DesfireRead });
            rig.Row.ToggleModeCommand.Execute(null);
            rig.Row.NewName = "cards/new";

            using (var entered = TestText.Secure("000102030405060708090A0B0C0D0E0F"))
            {
                rig.Row.StoreEntered(entered);
            }

            Assert.True(rig.Row.HasResult);

            rig.Row.NewName = "cards/other";
            Assert.False(rig.Row.HasResult);
        }

        [Fact]
        public void The_number_of_digits_of_the_key_follows_the_slot()
        {
            using var rig = new Rig(new[] { SecureKeySlots.DesfireRead }, hexLength: 48);

            Assert.Equal(48, rig.Row.KeyHexLength);

            rig.Host.HexLength = 12;
            rig.Host.BindKeySlot(SecureKeySlots.DesfireRead, WindowsCredentialKeyProvider.ProviderId, "x/1");

            Assert.Equal(12, rig.Row.KeyHexLength);
        }

        [Fact]
        public void The_label_of_the_key_box_says_how_many_digits_the_key_has_now()
        {
            using var rig = new Rig(new[] { SecureKeySlots.DesfireRead }, hexLength: 48);

            Assert.Contains("48", rig.Row.KeyPrompt);

            rig.Host.HexLength = 12;
            Assert.Contains("12", rig.Row.KeyPrompt);
        }

        // ── Whether a program can supply keys ──

        [Fact]
        public void The_status_text_follows_what_the_supplier_says_when_it_is_asked_again()
        {
            using var rig = new Rig(new[] { SecureKeySlots.DesfireRead });
            var raised = new List<string?>();
            rig.ViewModel.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

            Assert.Contains("not running", rig.ViewModel.SupplierStatusText);
            Assert.False(rig.ViewModel.SupplierIsConnected);

            rig.Supplier.Current = KeySupplierAvailability.Connected;
            rig.ViewModel.RefreshSupplierStatus();

            Assert.Equal("A connected program supplies keys.", rig.ViewModel.SupplierStatusText);
            Assert.True(rig.ViewModel.SupplierIsConnected);
            Assert.Contains(nameof(KeyManagerViewModel.SupplierStatusText), raised);
        }

        [Fact]
        public void A_change_the_supplier_announces_is_taken_over_on_the_thread_of_the_window()
        {
            var posted = new List<Action>();
            using var rig = new Rig(new[] { SecureKeySlots.DesfireRead }, post: posted.Add);
            rig.Supplier.Current = KeySupplierAvailability.NoSupplier;

            rig.Supplier.Raise();

            // Nothing is touched where the supplier raised it; the window's thread does it when it gets to it.
            Assert.Single(posted);

            var raised = new List<string?>();
            rig.ViewModel.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
            posted[0]();

            Assert.Contains(nameof(KeyManagerViewModel.SupplierStatusText), raised);
        }

        [Fact]
        public void The_status_is_shown_only_while_a_slot_is_named_in_the_key_store_of_the_external_application()
        {
            using var rig = new Rig(new[] { SecureKeySlots.DesfireRead });
            Assert.False(rig.ViewModel.ShowSupplierStatus);

            rig.Row.ToggleModeCommand.Execute(null);
            Assert.False(rig.ViewModel.ShowSupplierStatus);

            rig.Row.ProviderId = BridgeKeyProvider.ProviderId;
            Assert.True(rig.ViewModel.ShowSupplierStatus);

            rig.Row.ProviderId = WindowsCredentialKeyProvider.ProviderId;
            Assert.False(rig.ViewModel.ShowSupplierStatus);
        }

        // ── Closing ──

        [Fact]
        public void Closing_asks_the_window_to_close()
        {
            using var rig = new Rig(new[] { SecureKeySlots.DesfireRead });
            var closed = 0;
            rig.ViewModel.CloseRequested += (_, _) => closed++;

            rig.ViewModel.CloseCommand.Execute(null);

            Assert.Equal(1, closed);
        }

        [Fact]
        public void Disposing_lets_go_of_the_supplier_and_the_task_and_does_so_once()
        {
            var rig = new Rig(new[] { SecureKeySlots.DesfireRead, SecureKeySlots.DesfireWrite });
            Assert.Equal(1, rig.Supplier.Subscribers);
            Assert.Equal(2, rig.Host.Listeners);

            rig.Dispose();
            rig.Dispose();

            // The supplier lives as long as the application and the task as long as its dialog: neither may keep a closed window.
            Assert.Equal(0, rig.Supplier.Subscribers);
            Assert.Equal(0, rig.Host.Listeners);

            rig.Supplier.Raise();
            rig.ViewModel.RefreshSupplierStatus();
        }

        // ── No key leaves the entry ──

        [Fact]
        public void No_view_model_of_the_manager_exposes_a_key_as_text_or_a_secret_or_a_buffer()
        {
            var allowedTexts = new[]
            {
                // The row: names, labels and tooltips, and what the person chose.
                nameof(KeyManagerSlotRow.SlotId), nameof(KeyManagerSlotRow.SlotName), nameof(KeyManagerSlotRow.ProviderId),
                nameof(KeyManagerSlotRow.Reference), nameof(KeyManagerSlotRow.ModeButtonText), nameof(KeyManagerSlotRow.TypedKeyText),
                nameof(KeyManagerSlotRow.KeyStoreLabel), nameof(KeyManagerSlotRow.KeyNameLabel), nameof(KeyManagerSlotRow.KeyPrompt),
                nameof(KeyManagerSlotRow.ModeToolTip), nameof(KeyManagerSlotRow.ProviderToolTip), nameof(KeyManagerSlotRow.ReferenceToolTip),
                nameof(KeyManagerSlotRow.StoreButtonText), nameof(KeyManagerSlotRow.ResultText),
                nameof(KeyManagerSlotRow.StoredKeyLabel), nameof(KeyManagerSlotRow.StoredKeyToolTip), nameof(KeyManagerSlotRow.NoStoredKeysText),
                nameof(KeyManagerSlotRow.StoredKeysErrorText), nameof(KeyManagerSlotRow.NewKeyHeader), nameof(KeyManagerSlotRow.NewName),
                nameof(KeyManagerSlotRow.ShowKeyText), nameof(KeyManagerSlotRow.ShowKeyToolTip), nameof(KeyManagerSlotRow.StoreNewToolTip),

                // A choice in the list of stored keys: the name of the key, and what the person reads for it.
                nameof(StoredKeyChoice.Reference), nameof(StoredKeyChoice.Text),

                // The window.
                nameof(KeyManagerViewModel.Title), nameof(KeyManagerViewModel.IntroText), nameof(KeyManagerViewModel.NoSlotsText),
                nameof(KeyManagerViewModel.CloseText), nameof(KeyManagerViewModel.SupplierStatusText),
            };

            var found = new List<string>();

            foreach (var type in new[] { typeof(KeyManagerSlotRow), typeof(KeyManagerViewModel), typeof(StoredKeyChoice) })
            {
                foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    Assert.False(
                        property.PropertyType == typeof(SecureString) || property.PropertyType == typeof(char[]) || property.PropertyType == typeof(byte[])
                        || property.PropertyType == typeof(Span<byte>) || property.PropertyType == typeof(ReadOnlySpan<byte>),
                        type.Name + "." + property.Name + " holds a key");

                    if (property.PropertyType == typeof(string))
                    {
                        found.Add(property.Name);
                    }
                }
            }

            // A text property that is added later must be looked at: is it a key? Then add it here knowingly, or do not add it.
            Assert.Equal(allowedTexts.OrderBy(name => name), found.OrderBy(name => name));
        }
    }
}
