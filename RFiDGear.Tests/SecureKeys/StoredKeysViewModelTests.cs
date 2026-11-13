#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Text;

using RFiDGear.Services.SecureKeys;

using Xunit;

namespace RFiDGear.Tests.SecureKeys
{
    /// <summary>
    /// What the key manager of the menu decides: which keys are listed (by name only), what can be stored under which name, when it
    /// asks before it replaces or removes a key, and what it says about it. The vault, the questions to the person and the supplier of
    /// keys are scripted, so none of it needs a window or touches the Credential Manager of the person who runs the tests.
    /// </summary>
    public class StoredKeysViewModelTests
    {
        private const string Prefix = WindowsCredentialKeyProvider.TargetPrefix;

        private sealed class Rig : IDisposable
        {
            internal Rig(params string[] stored)
                : this("en", stored)
            {
            }

            private Rig(string culture, string[] stored)
            {
                foreach (var name in stored)
                {
                    Store.Blobs[Prefix + name] = Encoding.Unicode.GetBytes("A1B2C3D4E5F6");
                }

                ViewModel = StoredKeysViewModel.Create(new WindowsCredentialKeyProvider(Store), Supplier, Dialogs, TestText.In(culture));
            }

            internal static Rig German() => new Rig("de", Array.Empty<string>());

            internal FakeCredentialStore Store { get; } = new FakeCredentialStore();

            internal FakeSupplierStatus Supplier { get; } = new FakeSupplierStatus();

            internal ScriptedStoredKeysDialogs Dialogs { get; } = new ScriptedStoredKeysDialogs();

            internal StoredKeysViewModel ViewModel { get; }

            public void Dispose() => ViewModel.Dispose();

            internal bool Enter(string digits)
            {
                using var entered = TestText.Secure(digits);

                return ViewModel.StoreEntered(entered);
            }
        }

        // ── The list ──

        [Fact]
        public void The_stored_keys_are_listed_by_name_in_order_and_no_key_is_read()
        {
            using var rig = new Rig("cards/b", "Cards/a", "migrated/0123456789abcdef");

            Assert.Equal(new[] { "Cards/a", "cards/b", "migrated/0123456789abcdef" }, rig.ViewModel.Keys.Select(row => row.Reference));
            Assert.True(rig.ViewModel.HasKeys);
            Assert.False(rig.ViewModel.HasListError);
            Assert.Empty(rig.Store.Reads);
        }

        [Fact]
        public void Without_a_stored_key_the_list_is_empty_and_says_so()
        {
            using var rig = new Rig();

            Assert.Empty(rig.ViewModel.Keys);
            Assert.False(rig.ViewModel.HasKeys);
            Assert.Equal("No key is stored yet.", rig.ViewModel.NoKeysText);
            Assert.False(rig.ViewModel.HasListError);
        }

        [Fact]
        public void A_vault_that_cannot_be_listed_says_so_with_its_error_code_and_lists_nothing()
        {
            var store = new FakeCredentialStore { EnumerateError = CredentialErrors.AccessDenied };
            store.Blobs[Prefix + "cards/main"] = Encoding.Unicode.GetBytes("A1B2C3D4E5F6");

            using var viewModel = StoredKeysViewModel.Create(new WindowsCredentialKeyProvider(store), new FakeSupplierStatus(), new ScriptedStoredKeysDialogs(), TestText.English);

            Assert.Empty(viewModel.Keys);
            Assert.True(viewModel.HasListError);
            Assert.Equal("The stored keys could not be listed (error 5).", viewModel.ListErrorText);
        }

        // ── Storing a key ──

        [Theory]
        [InlineData("cards/main", true)]
        [InlineData("a.b_c-d:1/2", true)]
        [InlineData("", false)]
        [InlineData("a b", false)]
        [InlineData("keyä", false)]
        public void A_key_can_be_stored_only_under_a_name_in_the_alphabet_of_references(string name, bool valid)
        {
            using var rig = new Rig();

            rig.ViewModel.NewName = name;

            Assert.Equal(valid, rig.ViewModel.IsNameValid);
            Assert.Equal(valid, rig.ViewModel.CanStoreKey);
        }

        [Fact]
        public void A_key_that_is_entered_is_stored_under_the_name_and_the_list_shows_it_afterwards()
        {
            using var rig = new Rig("cards/other");
            rig.ViewModel.NewName = "cards/main";
            rig.ViewModel.SelectedHexLength = 32;

            var tried = rig.Enter("000102030405060708090a0b0c0d0e0f");

            Assert.True(tried);
            Assert.Equal(Encoding.Unicode.GetBytes("000102030405060708090A0B0C0D0E0F"), rig.Store.Blobs[Prefix + "cards/main"]);
            Assert.Equal(new[] { "cards/main", "cards/other" }, rig.ViewModel.Keys.Select(row => row.Reference));
            Assert.Equal("The key was stored under the reference.", rig.ViewModel.ResultText);
            Assert.True(rig.ViewModel.HasResult);
            Assert.False(rig.ViewModel.ResultIsError);

            // The name stays, so that the person sees what was stored; the digits are not here at all.
            Assert.Equal("cards/main", rig.ViewModel.NewName);
            Assert.Empty(rig.Store.Reads);
        }

        [Theory]
        [InlineData(12, "A1B2C3D4E5F6")]
        [InlineData(32, "000102030405060708090A0B0C0D0E0F")]
        [InlineData(48, "000102030405060708090A0B0C0D0E0F1011121314151617")]
        public void The_kind_of_key_decides_how_many_digits_are_needed(int hexLength, string digits)
        {
            using var rig = new Rig();
            rig.ViewModel.NewName = "cards/main";
            rig.ViewModel.SelectedHexLength = hexLength;

            Assert.Equal(hexLength.ToString(), rig.ViewModel.KeyPrompt.Split(' ')[2]);
            Assert.True(rig.Enter(digits));

            Assert.Equal(Encoding.Unicode.GetBytes(digits), rig.Store.Blobs[Prefix + "cards/main"]);
        }

        [Fact]
        public void The_three_kinds_of_key_are_offered_with_their_number_of_digits()
        {
            using var rig = new Rig();

            Assert.Equal(new[] { 12, 32, 48 }, rig.ViewModel.KeyKinds.Select(kind => kind.HexLength));
            Assert.All(rig.ViewModel.KeyKinds, kind => Assert.Contains(kind.HexLength.ToString(), kind.DisplayName));
            Assert.Contains(rig.ViewModel.SelectedHexLength, rig.ViewModel.KeyKinds.Select(kind => kind.HexLength));
        }

        [Theory]
        [InlineData("A1B2C3")]
        [InlineData("A1B2C3D4E5F6A1")]
        [InlineData("A1B2C3D4E5GG")]
        [InlineData("A1B2 C3D4E5F")]
        public void An_entry_that_is_not_a_key_of_the_right_length_stores_nothing_and_says_how_many_digits_are_needed(string entry)
        {
            using var rig = new Rig();
            rig.ViewModel.NewName = "cards/main";
            rig.ViewModel.SelectedHexLength = 12;

            // Not stored and not thrown away: the person can correct it, which is what the colour of the box is for.
            Assert.False(rig.Enter(entry));

            Assert.Empty(rig.Store.Writes);
            Assert.True(rig.ViewModel.ResultIsError);
            Assert.Equal("A key of 12 hexadecimal digits (0-9, A-F) without spaces is needed. Nothing was stored.", rig.ViewModel.ResultText);
        }

        [Fact]
        public void A_vault_that_refuses_the_key_says_so_with_its_error_code()
        {
            using var rig = new Rig();
            rig.ViewModel.NewName = "cards/main";
            rig.ViewModel.SelectedHexLength = 12;
            rig.Store.WriteError = CredentialErrors.AccessDenied;

            Assert.True(rig.Enter("A1B2C3D4E5F6"));

            Assert.True(rig.ViewModel.ResultIsError);
            Assert.Equal("The key could not be stored (error 5).", rig.ViewModel.ResultText);
            Assert.Empty(rig.Store.Blobs);
        }

        [Fact]
        public void Nothing_is_tried_while_the_name_is_not_valid_and_the_caller_is_told_to_keep_the_digits()
        {
            using var rig = new Rig();
            rig.ViewModel.NewName = "not valid";
            rig.ViewModel.SelectedHexLength = 12;

            Assert.False(rig.Enter("A1B2C3D4E5F6"));

            Assert.Empty(rig.Store.Writes);
            Assert.False(rig.ViewModel.HasResult);
        }

        // ── Replacing a key ──

        [Fact]
        public void A_name_that_is_in_use_asks_before_the_key_is_replaced_and_a_refusal_changes_nothing()
        {
            using var rig = new Rig("cards/main");
            var before = rig.Store.Blobs[Prefix + "cards/main"].ToArray();
            rig.Dialogs.AgreeToReplace = false;
            rig.ViewModel.NewName = "Cards/Main";
            rig.ViewModel.SelectedHexLength = 12;

            // Nothing was tried: the person is still at work on this entry, so its digits are kept.
            Assert.False(rig.Enter("000102030405"));

            Assert.Equal(new[] { "Cards/Main" }, rig.Dialogs.Replaced);
            Assert.Empty(rig.Store.Writes);
            Assert.Equal(before, rig.Store.Blobs[Prefix + "cards/main"]);
            Assert.False(rig.ViewModel.HasResult);
        }

        [Fact]
        public void Agreeing_to_replace_stores_the_new_key()
        {
            using var rig = new Rig("cards/main");
            rig.Dialogs.AgreeToReplace = true;
            rig.ViewModel.NewName = "cards/main";
            rig.ViewModel.SelectedHexLength = 12;

            Assert.True(rig.Enter("000102030405"));

            Assert.Equal(Encoding.Unicode.GetBytes("000102030405"), rig.Store.Blobs[Prefix + "cards/main"]);
            Assert.False(rig.ViewModel.ResultIsError);
        }

        [Fact]
        public void A_new_name_is_stored_without_a_question()
        {
            using var rig = new Rig("cards/main");
            rig.Dialogs.AgreeToReplace = false;
            rig.ViewModel.NewName = "cards/second";
            rig.ViewModel.SelectedHexLength = 12;

            Assert.True(rig.Enter("000102030405"));

            Assert.Empty(rig.Dialogs.Replaced);
        }

        [Fact]
        public void While_the_list_cannot_be_read_nothing_is_stored_because_a_key_that_is_there_would_be_replaced_unasked()
        {
            using var rig = new Rig("cards/main");
            var before = rig.Store.Blobs[Prefix + "cards/main"].ToArray();
            rig.ViewModel.NewName = "cards/main";
            rig.ViewModel.SelectedHexLength = 12;

            // From now on the vault refuses to list; the key under the name is there all the same, and an empty list is no proof of
            // the opposite.
            rig.Store.EnumerateError = CredentialErrors.AccessDenied;

            // Nothing was tried: the digits stay where the person can still use them, and the error stays in view.
            Assert.False(rig.Enter("000102030405"));

            Assert.Empty(rig.Store.Writes);
            Assert.Empty(rig.Dialogs.Replaced);
            Assert.Equal(before, rig.Store.Blobs[Prefix + "cards/main"]);
            Assert.True(rig.ViewModel.HasListError);
            Assert.False(rig.ViewModel.HasResult);
        }

        // ── Removing a key ──

        [Fact]
        public void Removing_asks_first_and_a_refusal_removes_nothing()
        {
            using var rig = new Rig("cards/main");
            rig.Dialogs.AgreeToRemove = false;

            rig.ViewModel.Keys.Single().RemoveCommand.Execute(null);

            Assert.Equal(new[] { "cards/main" }, rig.Dialogs.Removed);
            Assert.Empty(rig.Store.Deletes);
            Assert.Single(rig.ViewModel.Keys);
            Assert.False(rig.ViewModel.HasResult);
        }

        [Fact]
        public void Removing_deletes_the_credential_the_list_follows_and_the_person_is_told()
        {
            using var rig = new Rig("cards/main", "cards/other");
            rig.Dialogs.AgreeToRemove = true;

            rig.ViewModel.Keys.Single(row => row.Reference == "cards/main").RemoveCommand.Execute(null);

            Assert.Equal(Prefix + "cards/main", Assert.Single(rig.Store.Deletes));
            Assert.Equal(new[] { "cards/other" }, rig.ViewModel.Keys.Select(row => row.Reference));
            Assert.Equal("The key 'cards/main' was removed.", rig.ViewModel.ResultText);
            Assert.False(rig.ViewModel.ResultIsError);
            Assert.Empty(rig.Store.Reads);
        }

        [Fact]
        public void A_key_that_is_gone_already_counts_as_removed_and_the_list_catches_up()
        {
            using var rig = new Rig("cards/main");
            rig.Dialogs.AgreeToRemove = true;
            var row = rig.ViewModel.Keys.Single();

            // Removed by another program (cmdkey, another RFiDGear) while the window was open.
            rig.Store.Blobs.Clear();
            row.RemoveCommand.Execute(null);

            Assert.False(rig.ViewModel.ResultIsError);
            Assert.Empty(rig.ViewModel.Keys);
        }

        [Fact]
        public void A_vault_that_refuses_to_remove_says_so_with_its_error_code_and_the_key_stays_in_the_list()
        {
            using var rig = new Rig("cards/main");
            rig.Dialogs.AgreeToRemove = true;
            rig.Store.DeleteError = CredentialErrors.AccessDenied;

            rig.ViewModel.Keys.Single().RemoveCommand.Execute(null);

            Assert.True(rig.ViewModel.ResultIsError);
            Assert.Equal("The key 'cards/main' could not be removed (error 5).", rig.ViewModel.ResultText);
            Assert.Single(rig.ViewModel.Keys);
        }

        // ── Language and status ──

        [Fact]
        public void The_texts_follow_the_language()
        {
            using var english = new Rig();
            using var german = Rig.German();

            Assert.NotEqual(english.ViewModel.Title, string.Empty);
            Assert.NotEqual(english.ViewModel.IntroText, german.ViewModel.IntroText);
            Assert.NotEqual(english.ViewModel.NoKeysText, german.ViewModel.NoKeysText);
            Assert.NotEqual(english.ViewModel.ListHeader, german.ViewModel.ListHeader);
            Assert.NotEqual(english.ViewModel.AddHeader, german.ViewModel.AddHeader);
            Assert.NotEqual(english.ViewModel.KeyKinds[0].DisplayName, german.ViewModel.KeyKinds[0].DisplayName);
        }

        [Fact]
        public void The_intro_says_that_a_project_holds_the_name_only_once_and_does_not_repeat_it_as_the_list_showing_names_only()
        {
            using var english = new Rig();
            using var german = Rig.German();

            // The key that is entered can be shown, so "a key is never shown" is not even true, and the sentence before it said it.
            Assert.DoesNotContain("never shown", english.ViewModel.IntroText);
            Assert.DoesNotContain("nie angezeigt", german.ViewModel.IntroText);
            Assert.Equal(2, english.ViewModel.IntroText.Split(". ").Length);
            Assert.Equal(2, german.ViewModel.IntroText.Split(". ").Length);
            Assert.Contains("nie selbst", german.ViewModel.IntroText);
        }

        [Fact]
        public void The_status_of_the_supplier_follows_it_and_a_disposed_window_listens_no_more()
        {
            using var rig = new Rig();

            Assert.Contains("not running", rig.ViewModel.SupplierStatusText);
            Assert.False(rig.ViewModel.SupplierIsConnected);

            rig.Supplier.Current = KeySupplierAvailability.Connected;
            rig.ViewModel.RefreshSupplierStatus();

            Assert.Equal("A connected program supplies keys.", rig.ViewModel.SupplierStatusText);
            Assert.True(rig.ViewModel.SupplierIsConnected);
            Assert.Equal(1, rig.Supplier.Subscribers);

            rig.Dispose();

            Assert.Equal(0, rig.Supplier.Subscribers);
        }

        [Fact]
        public void The_close_command_asks_the_window_to_close()
        {
            using var rig = new Rig();
            var closed = 0;
            rig.ViewModel.CloseRequested += (_, _) => closed++;

            rig.ViewModel.CloseCommand.Execute(null);

            Assert.Equal(1, closed);
        }

        // ── No key leaves the entry ──

        [Fact]
        public void No_view_model_of_the_key_manager_exposes_a_key_as_text_or_a_secret_or_a_buffer()
        {
            var allowedTexts = new[]
            {
                // The list: a name, and what the button of the row says.
                nameof(StoredKeyRow.Reference), nameof(StoredKeyRow.RemoveText), nameof(StoredKeyRow.RemoveToolTip),

                // The kinds of key: a label.
                nameof(KeyKindItem.DisplayName),

                // The window: labels, tooltips, what the person typed as a name, and what came of it.
                nameof(StoredKeysViewModel.Title), nameof(StoredKeysViewModel.IntroText), nameof(StoredKeysViewModel.ListHeader),
                nameof(StoredKeysViewModel.NoKeysText), nameof(StoredKeysViewModel.ListErrorText), nameof(StoredKeysViewModel.AddHeader),
                nameof(StoredKeysViewModel.NameLabel), nameof(StoredKeysViewModel.NameToolTip), nameof(StoredKeysViewModel.NewName),
                nameof(StoredKeysViewModel.KindLabel), nameof(StoredKeysViewModel.KindToolTip), nameof(StoredKeysViewModel.KeyPrompt),
                nameof(StoredKeysViewModel.StoreButtonText), nameof(StoredKeysViewModel.StoreToolTip), nameof(StoredKeysViewModel.ResultText),
                nameof(StoredKeysViewModel.ShowKeyText), nameof(StoredKeysViewModel.ShowKeyToolTip),
                nameof(StoredKeysViewModel.CloseText), nameof(StoredKeysViewModel.SupplierStatusText),
            };

            var found = new List<string>();

            foreach (var type in new[] { typeof(StoredKeyRow), typeof(KeyKindItem), typeof(StoredKeysViewModel) })
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

    /// <summary>The person, scripted: whether they agree to remove or to replace a key, and what they were asked about.</summary>
    internal sealed class ScriptedStoredKeysDialogs : IStoredKeysDialogs
    {
        public bool AgreeToRemove { get; set; } = true;

        public bool AgreeToReplace { get; set; } = true;

        public List<string> Removed { get; } = new List<string>();

        public List<string> Replaced { get; } = new List<string>();

        bool IStoredKeysDialogs.ConfirmRemove(string reference)
        {
            Removed.Add(reference);

            return AgreeToRemove;
        }

        bool IStoredKeysDialogs.ConfirmReplace(string reference)
        {
            Replaced.Add(reference);

            return AgreeToReplace;
        }
    }
}
