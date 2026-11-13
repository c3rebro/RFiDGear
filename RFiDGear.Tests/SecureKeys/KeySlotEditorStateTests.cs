using RFiDGear.Models;
using RFiDGear.Services.SecureKeys;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Security;
using System.Text;

using Xunit;

namespace RFiDGear.Tests.SecureKeys
{
    /// <summary>
    /// What the editor of a key slot decides: when the key of a task may be discarded in favour of a reference, what is
    /// bound, and how a key is stored. The task and the questions to the person are scripted, so none of it needs a window.
    /// </summary>
    public class KeySlotEditorStateTests
    {
        private const string Slot = SecureKeySlots.DesfireRead;
        private const string Prefix = WindowsCredentialKeyProvider.TargetPrefix;

        private static readonly SecureKeyProviderInfo CredentialManager =
            new SecureKeyProviderInfo(WindowsCredentialKeyProvider.ProviderId, WindowsCredentialKeyProvider.DisplayName);

        private static readonly SecureKeyProviderInfo OtherVault = new SecureKeyProviderInfo("other.vault", "Other vault");

        // ── Switching ──

        [Fact]
        public void A_task_that_holds_a_key_is_asked_about_before_the_key_is_discarded_and_a_refusal_changes_nothing()
        {
            var host = new FakeHost { EmbeddedKey = "A1B2" };
            var dialogs = new ScriptedDialogs { Confirm = false };
            var state = NewState(host, dialogs);

            state.ToggleMode();

            Assert.Equal(1, dialogs.Confirmations);
            Assert.Empty(host.Calls);
            Assert.False(state.IsReferenceMode);
            Assert.Equal("A1B2", host.EmbeddedKey);
        }

        [Fact]
        public void Agreeing_binds_the_slot_to_the_credential_manager_with_no_reference_yet_and_the_key_is_gone()
        {
            var host = new FakeHost { EmbeddedKey = "A1B2" };
            var state = NewState(host, new ScriptedDialogs { Confirm = true });

            state.ToggleMode();

            Assert.Equal($"bind {WindowsCredentialKeyProvider.ProviderId} ''", Assert.Single(host.Calls));
            Assert.True(state.IsReferenceMode);
            Assert.False(state.IsEmbeddedMode);
            Assert.Equal(string.Empty, host.EmbeddedKey);
        }

        [Fact]
        public void A_task_without_a_key_is_switched_without_a_question()
        {
            var host = new FakeHost();
            var dialogs = new ScriptedDialogs { Confirm = false };
            var state = NewState(host, dialogs);

            state.ToggleMode();

            Assert.Equal(0, dialogs.Confirmations);
            Assert.True(state.IsReferenceMode);
        }

        [Fact]
        public void Switching_back_removes_the_binding_and_asks_nothing()
        {
            var host = new FakeHost();
            var dialogs = new ScriptedDialogs { Confirm = true };
            var state = NewState(host, dialogs);
            state.ToggleMode();

            state.ToggleMode();

            Assert.False(state.IsReferenceMode);
            Assert.Equal("unbind", host.Calls.Last());
            Assert.Equal(0, dialogs.Confirmations);
        }

        [Fact]
        public void Without_a_provider_the_slot_cannot_be_switched()
        {
            var host = new FakeHost { EmbeddedKey = "A1B2" };
            var dialogs = new ScriptedDialogs { Confirm = true };
            var state = new KeySlotEditorState(host, Slot, Array.Empty<SecureKeyProviderInfo>(), dialogs, NewKeyStore(new FakeCredentialStore()));

            Assert.False(state.CanUseReference);
            Assert.False(state.ToggleModeCommand.CanExecute(null));

            state.ToggleMode();

            Assert.Empty(host.Calls);
            Assert.Equal(0, dialogs.Confirmations);
        }

        [Fact]
        public void The_credential_manager_is_the_default_provider_and_else_the_first_one()
        {
            var both = new FakeHost();
            NewState(both, new ScriptedDialogs(), providers: new[] { OtherVault, CredentialManager }).ToggleMode();

            var only = new FakeHost();
            NewState(only, new ScriptedDialogs(), providers: new[] { OtherVault }).ToggleMode();

            Assert.Equal($"bind {WindowsCredentialKeyProvider.ProviderId} ''", both.Calls.Single());
            Assert.Equal("bind other.vault ''", only.Calls.Single());
        }

        [Fact]
        public void A_cancelled_switch_tells_the_window_to_look_at_the_state_again()
        {
            var host = new FakeHost { EmbeddedKey = "A1B2" };
            var state = NewState(host, new ScriptedDialogs { Confirm = false });
            var announced = new List<string>();
            state.PropertyChanged += (_, e) => announced.Add(e.PropertyName ?? "<all>");

            state.ToggleMode();

            // The button of a window flips when it is pressed; this brings it back to what the task says.
            Assert.Contains("<all>", announced.Select(name => name == string.Empty ? "<all>" : name));
        }

        // ── Reference and provider ──

        [Fact]
        public void Typing_a_reference_binds_it_and_the_same_reference_again_binds_nothing()
        {
            var host = new FakeHost();
            var state = NewState(host, new ScriptedDialogs());
            state.ToggleMode();
            host.Calls.Clear();

            state.Reference = "cards/main";
            state.Reference = "cards/main";

            Assert.Equal($"bind {WindowsCredentialKeyProvider.ProviderId} 'cards/main'", Assert.Single(host.Calls));
            Assert.Equal("cards/main", state.Reference);
        }

        [Fact]
        public void A_reference_is_not_bound_while_the_slot_holds_a_key()
        {
            var host = new FakeHost { EmbeddedKey = "A1B2" };
            var state = NewState(host, new ScriptedDialogs());

            state.Reference = "cards/main";

            Assert.Empty(host.Calls);
        }

        [Fact]
        public void Choosing_another_provider_keeps_the_reference()
        {
            var host = new FakeHost();
            var state = NewState(host, new ScriptedDialogs(), providers: new[] { CredentialManager, OtherVault });
            state.ToggleMode();
            state.Reference = "cards/main";

            state.ProviderId = "other.vault";

            Assert.Equal("bind other.vault 'cards/main'", host.Calls.Last());
            Assert.Equal("other.vault", state.ProviderId);
            Assert.Equal("cards/main", state.Reference);
        }

        [Theory]
        [InlineData("cards/main", true)]
        [InlineData("a.b_c-d:1/2", true)]
        [InlineData("", false)]
        [InlineData("a b", false)]
        [InlineData("keyä", false)]
        public void A_reference_is_valid_only_in_the_alphabet_of_references(string reference, bool valid)
        {
            var host = new FakeHost();
            var state = NewState(host, new ScriptedDialogs());
            state.ToggleMode();

            state.Reference = reference;

            Assert.Equal(valid, state.IsReferenceValid);
        }

        [Fact]
        public void The_provider_is_offered_for_choice_only_when_there_is_more_than_one()
        {
            Assert.False(NewState(new FakeHost(), new ScriptedDialogs()).ShowProviderChoice);
            Assert.True(NewState(new FakeHost(), new ScriptedDialogs(), providers: new[] { CredentialManager, OtherVault }).ShowProviderChoice);
        }

        // ── Storing a key ──

        [Theory]
        [InlineData(true, "cards/main", true)]
        [InlineData(true, "", false)]
        [InlineData(false, "cards/main", false)]
        public void A_key_can_be_stored_only_for_the_credential_manager_under_a_valid_reference(bool credentialManager, string reference, bool expected)
        {
            var host = new FakeHost();
            var providers = credentialManager ? new[] { CredentialManager } : new[] { OtherVault };
            var state = NewState(host, new ScriptedDialogs(), providers: providers);
            state.ToggleMode();
            state.Reference = reference;

            Assert.Equal(expected, state.CanStoreKey);
            Assert.Equal(expected, state.StoreKeyCommand.CanExecute(null));
            Assert.Equal(credentialManager, state.ProviderOffersStoreKey);
        }

        [Fact]
        public void A_key_is_asked_for_with_the_digits_its_type_has_and_stored_under_the_reference_without_being_shown()
        {
            var host = new FakeHost { HexLength = 32 };
            var store = new FakeCredentialStore();
            var dialogs = new ScriptedDialogs { Entry = "000102030405060708090A0B0C0D0E0F" };
            var state = NewState(host, dialogs, store: store);
            state.ToggleMode();
            state.Reference = "cards/main";

            state.StoreKey();

            Assert.Equal(32, dialogs.AskedHexLength);
            Assert.Equal(Encoding.Unicode.GetBytes("000102030405060708090A0B0C0D0E0F"), store.Blobs[Prefix + "cards/main"]);
            Assert.Equal((KeySlotMessage.KeyStored, 0), Assert.Single(dialogs.Shown));
        }

        [Fact]
        public void The_digits_may_be_typed_in_lower_case()
        {
            var store = new FakeCredentialStore();
            var state = NewState(new FakeHost { HexLength = 12 }, new ScriptedDialogs { Entry = "a1b2c3d4e5f6" }, store: store);
            state.ToggleMode();
            state.Reference = "cards/main";

            state.StoreKey();

            Assert.Equal(Encoding.Unicode.GetBytes("A1B2C3D4E5F6"), store.Blobs[Prefix + "cards/main"]);
        }

        [Fact]
        public void An_entry_that_is_handed_in_is_stored_without_asking_and_the_state_says_what_came_of_it()
        {
            // The key manager has a box for the key of its own: the state is given the entry and neither asks nor shows a message.
            var store = new FakeCredentialStore();
            var dialogs = new ScriptedDialogs();
            var state = NewState(new FakeHost { HexLength = 12 }, dialogs, store: store);
            state.ToggleMode();
            state.Reference = "cards/main";

            using var entered = TestText.Secure("a1b2c3d4e5f6");
            var result = state.StoreEntered(entered);

            Assert.Equal(new KeySlotStoreResult(KeySlotMessage.KeyStored, 0), result);
            Assert.Null(dialogs.AskedHexLength);
            Assert.Empty(dialogs.Shown);
            Assert.Equal(Encoding.Unicode.GetBytes("A1B2C3D4E5F6"), store.Blobs[Prefix + "cards/main"]);
        }

        [Fact]
        public void An_entry_of_the_wrong_length_or_a_refusing_vault_is_reported_and_an_impossible_store_is_not_tried()
        {
            var store = new FakeCredentialStore();
            var state = NewState(new FakeHost { HexLength = 12 }, new ScriptedDialogs(), store: store);

            using (var early = TestText.Secure("a1b2c3d4e5f6"))
            {
                // No reference yet: nothing can be stored, and nothing is tried.
                Assert.Null(state.StoreEntered(early));
            }

            state.ToggleMode();
            state.Reference = "cards/main";

            using (var shortKey = TestText.Secure("a1b2"))
            {
                Assert.Equal(new KeySlotStoreResult(KeySlotMessage.KeyInvalid, 12), state.StoreEntered(shortKey));
            }

            store.WriteError = 5;

            using (var refused = TestText.Secure("a1b2c3d4e5f6"))
            {
                Assert.Equal(new KeySlotStoreResult(KeySlotMessage.KeyNotStored, 5), state.StoreEntered(refused));
            }

            Assert.Empty(store.Blobs);
        }

        [Fact]
        public void The_state_knows_its_slot_and_how_many_digits_its_key_has()
        {
            var host = new FakeHost { HexLength = 48 };
            var state = NewState(host, new ScriptedDialogs());

            Assert.Equal(Slot, state.SlotId);
            Assert.Equal(48, state.ExpectedKeyHexLength);

            host.HexLength = 12;
            Assert.Equal(12, state.ExpectedKeyHexLength);
        }

        [Fact]
        public void A_cancelled_entry_stores_nothing_and_says_nothing()
        {
            var store = new FakeCredentialStore();
            var dialogs = new ScriptedDialogs { Entry = null };
            var state = NewState(new FakeHost(), dialogs, store: store);
            state.ToggleMode();
            state.Reference = "cards/main";

            state.StoreKey();

            Assert.Empty(store.Writes);
            Assert.Empty(dialogs.Shown);
        }

        [Theory]
        [InlineData("A1B2C3")]
        [InlineData("A1B2C3D4E5F6A1")]
        [InlineData("A1B2C3D4E5GG")]
        [InlineData("A1B2 C3D4E5F")]
        public void An_entry_that_is_not_a_key_of_the_right_length_stores_nothing_and_says_how_long_it_has_to_be(string entry)
        {
            var store = new FakeCredentialStore();
            var dialogs = new ScriptedDialogs { Entry = entry };
            var state = NewState(new FakeHost { HexLength = 12 }, dialogs, store: store);
            state.ToggleMode();
            state.Reference = "cards/main";

            state.StoreKey();

            Assert.Empty(store.Writes);
            Assert.Equal((KeySlotMessage.KeyInvalid, 12), Assert.Single(dialogs.Shown));
        }

        [Fact]
        public void A_refusal_of_the_key_store_is_reported_with_its_error_code()
        {
            var store = new FakeCredentialStore { WriteError = CredentialErrors.AccessDenied };
            var dialogs = new ScriptedDialogs { Entry = "A1B2C3D4E5F6" };
            var state = NewState(new FakeHost { HexLength = 12 }, dialogs, store: store);
            state.ToggleMode();
            state.Reference = "cards/main";

            state.StoreKey();

            Assert.Equal((KeySlotMessage.KeyNotStored, CredentialErrors.AccessDenied), Assert.Single(dialogs.Shown));
        }

        [Fact]
        public void Nothing_is_asked_when_no_key_can_be_stored()
        {
            var dialogs = new ScriptedDialogs { Entry = "A1B2C3D4E5F6" };
            var state = NewState(new FakeHost { HexLength = 12 }, dialogs);

            // Still the key of the task: nothing to store a key under.
            state.StoreKey();

            Assert.Null(dialogs.AskedHexLength);
        }

        // ── Staying in step with the task ──

        [Fact]
        public void A_change_of_the_bindings_of_the_task_is_announced_and_a_state_that_is_disposed_listens_no_more()
        {
            var host = new FakeHost();
            var state = NewState(host, new ScriptedDialogs());
            var announced = 0;
            state.PropertyChanged += (_, _) => announced++;

            host.Bind(Slot, WindowsCredentialKeyProvider.ProviderId, "from/outside");
            var whileListening = announced;
            state.Dispose();
            host.Bind(Slot, WindowsCredentialKeyProvider.ProviderId, "again");

            Assert.True(whileListening > 0);
            Assert.Equal(whileListening, announced);
            Assert.True(state.IsReferenceMode);
        }

        // ── Helpers ──

        private static WindowsCredentialKeyProvider NewKeyStore(FakeCredentialStore store) => new WindowsCredentialKeyProvider(store);

        private static KeySlotEditorState NewState(
            FakeHost host,
            ScriptedDialogs dialogs,
            SecureKeyProviderInfo[]? providers = null,
            FakeCredentialStore? store = null) =>
            new KeySlotEditorState(host, Slot, providers ?? new[] { CredentialManager }, dialogs, NewKeyStore(store ?? new FakeCredentialStore()));

        /// <summary>A task whose slot has a key or a binding, and that notes what it was asked.</summary>
        private sealed class FakeHost : IKeySlotHost
        {
            private readonly List<KeyBindingEntry> _bindings = new List<KeyBindingEntry>();

            public event PropertyChangedEventHandler? PropertyChanged;

            public string EmbeddedKey { get; set; } = string.Empty;

            public int HexLength { get; set; } = 32;

            public List<string> Calls { get; } = new List<string>();

            public bool IsKeyBound(string slotId) => _bindings.Any(entry => entry.Slot == slotId);

            public KeyBindingEntry? GetKeyBinding(string slotId) => _bindings.FirstOrDefault(entry => entry.Slot == slotId)?.Clone();

            // The task has one slot; a question about another is a mistake of the editor.
            public bool HasEmbeddedKey(string slotId) => slotId == Slot && EmbeddedKey.Length > 0;

            public int ExpectedKeyHexLength(string slotId) => slotId == Slot ? HexLength : 0;

            public void BindKeySlot(string slotId, string providerId, string reference)
            {
                Calls.Add($"bind {providerId} '{reference}'");
                Bind(slotId, providerId, reference);
                EmbeddedKey = string.Empty;
            }

            public void UnbindKeySlot(string slotId)
            {
                Calls.Add("unbind");
                _bindings.RemoveAll(entry => entry.Slot == slotId);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("KeyBindings"));
            }

            /// <summary>Binds a slot the way a change from outside the editor would.</summary>
            public void Bind(string slotId, string providerId, string reference)
            {
                _bindings.RemoveAll(entry => entry.Slot == slotId);
                _bindings.Add(new KeyBindingEntry { Slot = slotId, ProviderId = providerId, Reference = reference });
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("KeyBindings"));
            }
        }

        /// <summary>The person, scripted: what they answer and what they were told.</summary>
        private sealed class ScriptedDialogs : IKeySlotEditorDialogs
        {
            public bool Confirm { get; set; }

            public int Confirmations { get; private set; }

            /// <summary>Gets or sets the digits that are entered; <c>null</c> is a cancelled entry.</summary>
            public string? Entry { get; set; }

            public int? AskedHexLength { get; private set; }

            public List<(KeySlotMessage Message, int Detail)> Shown { get; } = new List<(KeySlotMessage, int)>();

            public bool ConfirmReplaceKey()
            {
                Confirmations++;

                return Confirm;
            }

            public SecureString? AskForKey(int hexLength)
            {
                AskedHexLength = hexLength;

                if (Entry == null)
                {
                    return null;
                }

                var secure = new SecureString();

                foreach (var c in Entry)
                {
                    secure.AppendChar(c);
                }

                return secure;
            }

            public void Show(KeySlotMessage message, int detail) => Shown.Add((message, detail));
        }
    }
}
