#nullable enable
using RFiDGear.Services.Sam;
using RFiDGear.Services.SecureKeys;
using RFiDGear.Tests.SecureKeys;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Text;
using System.Threading.Tasks;

using Elatec.NET.Sam;

using Xunit;

namespace RFiDGear.Tests.Sam
{
    /// <summary>What the SAM setup of the menu shows and does, with a scripted SAM, a scripted session and a fake Credential Manager.</summary>
    public class SamSetupViewModelTests
    {
        private const string ReaderName = "HID Global OMNIKEY 5422 Smartcard Reader 0";
        private const string TypedKeyHex = "00112233445566778899AABBCCDDEEFF";
        private const string StoredKeyHex = "FFEEDDCCBBAA99887766554433221100";
        private const string StoredName = "sam/master";
        private const string Prefix = WindowsCredentialKeyProvider.TargetPrefix;

        private sealed class Rig : IDisposable
        {
            internal Rig(string culture = "en", bool withSam = true, params string[] storedKeys)
            {
                Source = new FakeSamReaderSource
                {
                    Answer = apdu => Convert.ToHexString(apdu) == "8060000000" ? SamTestData.VersionAnswer() : new byte[] { 0x90, 0x00 },
                };

                if (withSam)
                {
                    Source.With(ReaderName, SamTestData.SamAtr);
                }

                Source.With("HID Global OMNIKEY 5422CL Smartcard Reader 0", null);

                foreach (var name in storedKeys)
                {
                    Store.Blobs[Prefix + name] = Encoding.Unicode.GetBytes(StoredKeyHex);
                }

                KeyStore = new WindowsCredentialKeyProvider(Store);
                Service = new SamSetupService(new SamReaderFinder(Source), _ => Session, Dialogs, new SamKeyReader(SamTestKeys.RegistryOf(KeyStore)));
                ViewModel = SamSetupViewModel.Create(Service, KeyStore, TestText.In(culture));
            }

            internal FakeCredentialStore Store { get; } = new FakeCredentialStore();

            internal WindowsCredentialKeyProvider KeyStore { get; }

            internal FakeSamReaderSource Source { get; }

            internal FakeHostSession Session { get; } = new FakeHostSession();

            internal ScriptedSamDialogs Dialogs { get; } = new ScriptedSamDialogs();

            internal SamSetupService Service { get; }

            internal SamSetupViewModel ViewModel { get; }

            /// <summary>Searches, checks the SAM and authenticates: the way to a state from which the other steps start.</summary>
            internal async Task<SamSetupViewModel> ConnectedAsync()
            {
                await ViewModel.RefreshAsync();
                await ViewModel.CheckAsync();
                return ViewModel;
            }

            internal async Task<SamSetupViewModel> AuthenticatedAsync()
            {
                await ConnectedAsync();
                await ViewModel.AuthenticateAsync(null);
                return ViewModel;
            }

            public void Dispose() => ViewModel.Dispose();
        }

        #region search and check

        [Fact]
        public void Before_anything_is_found_no_step_can_be_started()
        {
            using var rig = new Rig();
            var viewModel = rig.ViewModel;

            Assert.Equal(SamSetupState.NoReader, viewModel.State);
            Assert.False(viewModel.CanCheck);
            Assert.False(viewModel.CanAuthenticate);
            Assert.False(viewModel.CanActivate);
            Assert.False(viewModel.CanWrite);
            Assert.True(viewModel.CanClose);
            Assert.False(viewModel.IsRunning);
            Assert.Empty(viewModel.ReaderNames);
            Assert.False(viewModel.HasResult);
            Assert.Equal("No MIFARE SAM AV3 found.", viewModel.StateText);

            // The only stored-key choice is to type the key.
            var choice = Assert.Single(viewModel.StoredKeys);
            Assert.Equal(string.Empty, choice.Reference);
            Assert.Equal("(enter the key below)", choice.DisplayName);
        }

        [Fact]
        public async Task Searching_finds_the_sam_selects_its_reader_and_lists_the_stored_keys_by_name()
        {
            using var rig = new Rig(storedKeys: new[] { "sam/master", "cards/a" });

            await rig.ViewModel.RefreshAsync();

            Assert.Equal(new[] { ReaderName }, rig.ViewModel.ReaderNames);
            Assert.Equal(ReaderName, rig.ViewModel.SelectedReader);
            Assert.Equal(SamSetupState.Found, rig.ViewModel.State);
            Assert.True(rig.ViewModel.CanCheck);
            Assert.False(rig.ViewModel.CanAuthenticate);
            Assert.Equal(new[] { string.Empty, "cards/a", "sam/master" }, rig.ViewModel.StoredKeys.Select(c => c.Reference));
            Assert.Equal("Found a MIFARE SAM AV3 in 1 reader(s).", rig.ViewModel.ResultText);
            Assert.False(rig.ViewModel.ResultIsError);

            // Only names were asked of the vault: nothing was read.
            Assert.Empty(rig.Store.Reads);
        }

        [Fact]
        public async Task A_vault_that_cannot_be_listed_keeps_the_choices_and_the_selections_and_says_why()
        {
            using var rig = new Rig(storedKeys: StoredName);
            await rig.ViewModel.RefreshAsync();
            rig.ViewModel.MasterUsesFactoryKey = false;
            rig.ViewModel.MasterReference = StoredName;
            rig.ViewModel.NewKeyReference = StoredName;
            rig.Store.EnumerateError = 5;

            await rig.ViewModel.RefreshAsync();

            // What the person chose is still chosen, and the list still has the key: a vault that does not answer now is not a vault
            // that has lost its keys.
            Assert.Equal(StoredName, rig.ViewModel.MasterReference);
            Assert.Equal(StoredName, rig.ViewModel.NewKeyReference);
            Assert.Equal(new[] { string.Empty, StoredName }, rig.ViewModel.StoredKeys.Select(c => c.Reference));

            // The search itself went well, and the vault is what the last line is about.
            Assert.Equal(SamSetupState.Found, rig.ViewModel.State);
            Assert.True(rig.ViewModel.ResultIsError);
            Assert.Equal("The stored keys could not be listed (error 5).", rig.ViewModel.ResultText);
            Assert.Contains("Found a MIFARE SAM AV3", rig.ViewModel.StatusText, StringComparison.Ordinal);

            // The vault answers again: the list is read again, and a key that is gone is gone.
            rig.Store.EnumerateError = 0;
            rig.Store.Blobs.Clear();

            await rig.ViewModel.RefreshAsync();

            Assert.Equal(string.Empty, rig.ViewModel.MasterReference);
            Assert.Equal(new[] { string.Empty }, rig.ViewModel.StoredKeys.Select(c => c.Reference));
        }
        [Fact]
        public async Task A_search_without_a_sam_says_so_and_is_no_error()
        {
            using var rig = new Rig(withSam: false);

            await rig.ViewModel.RefreshAsync();

            Assert.Equal(SamSetupState.NoReader, rig.ViewModel.State);
            Assert.False(rig.ViewModel.CanCheck);
            Assert.Contains("No MIFARE SAM AV3 was found", rig.ViewModel.ResultText, StringComparison.Ordinal);
            Assert.False(rig.ViewModel.ResultIsError);
        }

        [Fact]
        public async Task Readers_that_cannot_be_listed_are_said_to_be_an_error()
        {
            using var rig = new Rig();
            rig.Source.ListFails = new InvalidOperationException("PRIVATE");

            await rig.ViewModel.RefreshAsync();

            Assert.Equal(SamSetupState.NoReader, rig.ViewModel.State);
            Assert.True(rig.ViewModel.ResultIsError);
            Assert.Contains("Smart Card service", rig.ViewModel.ResultText, StringComparison.Ordinal);
            Assert.DoesNotContain("PRIVATE", rig.ViewModel.StatusText, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Checking_connects_and_shows_the_hardware_and_software_information_and_nothing_of_the_serial_number()
        {
            using var rig = new Rig();
            await rig.ViewModel.RefreshAsync();

            await rig.ViewModel.CheckAsync();

            Assert.Equal(SamSetupState.Connected, rig.ViewModel.State);
            Assert.Contains("04010105002301", rig.ViewModel.VersionText, StringComparison.Ordinal);
            Assert.Contains("04010105011F01", rig.ViewModel.VersionText, StringComparison.Ordinal);
            Assert.DoesNotContain("EEEE", rig.ViewModel.VersionText + rig.ViewModel.StatusText, StringComparison.OrdinalIgnoreCase);
            Assert.True(rig.ViewModel.CanAuthenticate);
            Assert.False(rig.ViewModel.CanActivate);
            Assert.False(rig.ViewModel.CanWrite);
            Assert.Empty(rig.Session.Calls);
        }

        [Fact]
        public async Task Checking_a_reader_whose_sam_was_taken_out_goes_back_to_found_with_an_error()
        {
            using var rig = new Rig();
            await rig.ViewModel.RefreshAsync();
            rig.Source.AtrFails.Clear();
            rig.Source.With("Other", null);
            rig.ViewModel.SelectedReader = "Other";

            await rig.ViewModel.CheckAsync();

            Assert.Equal(SamSetupState.Found, rig.ViewModel.State);
            Assert.True(rig.ViewModel.ResultIsError);
            Assert.Contains("'Other'", rig.ViewModel.ResultText, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Choosing_another_reader_after_connecting_lets_go_of_the_connection()
        {
            using var rig = new Rig();
            await rig.ConnectedAsync();
            var session = rig.Session;

            rig.ViewModel.SelectedReader = "Another";

            Assert.Equal(SamSetupState.Found, rig.ViewModel.State);
            Assert.True(session.Disposed);
            Assert.False(rig.Service.IsConnected);
            Assert.Equal(string.Empty, rig.ViewModel.VersionText);
        }

        #endregion

        #region authenticate and activate

        [Fact]
        public async Task The_factory_key_is_the_default_and_authenticates_with_zero_bytes()
        {
            using var rig = new Rig();
            await rig.ConnectedAsync();

            var tried = await rig.ViewModel.AuthenticateAsync(null);

            Assert.True(rig.ViewModel.MasterUsesFactoryKey);
            Assert.True(tried);
            Assert.Equal(SamSetupState.Authenticated, rig.ViewModel.State);
            Assert.Equal(new byte[16], rig.Session.KeyCopies.Single());
            Assert.Equal("Host authentication succeeded.", rig.ViewModel.ResultText);
            Assert.False(rig.ViewModel.ResultIsError);
            Assert.True(rig.ViewModel.CanWrite);
        }

        [Fact]
        public async Task A_sam_that_says_not_activated_offers_the_activation_and_nothing_else_changes()
        {
            using var rig = new Rig();
            await rig.ConnectedAsync();
            rig.Session.AuthenticateFails = new SamException("part 1", 0x6985);

            var tried = await rig.ViewModel.AuthenticateAsync(null);

            Assert.True(tried);
            Assert.Equal(SamSetupState.NotActivated, rig.ViewModel.State);
            Assert.True(rig.ViewModel.CanActivate);
            Assert.True(rig.ViewModel.CanAuthenticate);
            Assert.False(rig.ViewModel.CanWrite);
            Assert.True(rig.ViewModel.ResultIsError);
            Assert.Contains("6985", rig.ViewModel.ResultText, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Declining_the_activation_sends_nothing_and_leaves_the_state()
        {
            using var rig = new Rig();
            await rig.ConnectedAsync();
            rig.Session.AuthenticateFails = new SamException("part 1", 0x6985);
            await rig.ViewModel.AuthenticateAsync(null);
            rig.Dialogs.Agree = false;

            var tried = await rig.ViewModel.ActivateAsync(null);

            Assert.False(tried);
            Assert.Equal(SamSetupState.NotActivated, rig.ViewModel.State);
            Assert.Equal(1, rig.Dialogs.ActivationAsked);
            Assert.DoesNotContain(rig.Session.Calls, call => call.StartsWith("activate", StringComparison.Ordinal));
            Assert.Equal("Not activated.", rig.ViewModel.ResultText);
        }

        [Fact]
        public async Task A_confirmed_activation_activates_once_and_asks_for_the_authentication_next()
        {
            using var rig = new Rig();
            await rig.ConnectedAsync();
            rig.Session.AuthenticateFails = new SamException("part 1", 0x6985);
            await rig.ViewModel.AuthenticateAsync(null);
            rig.Dialogs.Agree = true;

            var tried = await rig.ViewModel.ActivateAsync(null);

            Assert.True(tried);
            Assert.Equal(1, rig.Session.Calls.Count(call => call.StartsWith("activate", StringComparison.Ordinal)));
            Assert.Equal(SamSetupState.Connected, rig.ViewModel.State);
            Assert.False(rig.ViewModel.CanActivate);
            Assert.Equal("The SAM was activated. Authenticate now.", rig.ViewModel.ResultText);

            // Nothing authenticated by itself after the activation.
            Assert.Equal(1, rig.Session.Calls.Count(call => call.StartsWith("authenticate", StringComparison.Ordinal)));
        }

        [Fact]
        public async Task An_activation_that_ended_with_an_error_says_that_it_may_have_taken_effect()
        {
            using var rig = new Rig();
            await rig.ConnectedAsync();
            rig.Session.AuthenticateFails = new SamException("part 1", 0x6985);
            await rig.ViewModel.AuthenticateAsync(null);
            rig.Dialogs.Agree = true;
            rig.Session.ActivateFails = new SamException("proof", 0x9000);

            await rig.ViewModel.ActivateAsync(null);

            Assert.True(rig.ViewModel.ResultIsError);
            Assert.Contains("may be activated all the same", rig.ViewModel.ResultText, StringComparison.Ordinal);

            // What the window knew is not to be trusted: it does not offer another activation, and asks for a fresh check first.
            Assert.Equal(SamSetupState.Found, rig.ViewModel.State);
            Assert.False(rig.ViewModel.CanActivate);
            Assert.False(rig.ViewModel.CanAuthenticate);
            Assert.True(rig.ViewModel.CanCheck);
        }

        [Fact]
        public async Task A_refused_authentication_names_the_status_word_and_keeps_the_connection()
        {
            using var rig = new Rig();
            await rig.ConnectedAsync();
            rig.Session.AuthenticateFails = new SamException("part 2", 0x901E);

            var tried = await rig.ViewModel.AuthenticateAsync(null);

            Assert.True(tried);
            Assert.Equal(SamSetupState.Connected, rig.ViewModel.State);
            Assert.True(rig.ViewModel.ResultIsError);
            Assert.Contains("901E", rig.ViewModel.ResultText, StringComparison.Ordinal);
            Assert.True(rig.Service.IsConnected);
        }

        [Fact]
        public async Task A_reader_that_fails_ends_the_connection_and_goes_back_to_found()
        {
            using var rig = new Rig();
            await rig.ConnectedAsync();
            rig.Session.AuthenticateFails = new SamReaderException("reader");

            await rig.ViewModel.AuthenticateAsync(null);

            Assert.Equal(SamSetupState.Found, rig.ViewModel.State);
            Assert.False(rig.Service.IsConnected);
            Assert.False(rig.ViewModel.CanAuthenticate);
            Assert.True(rig.ViewModel.CanCheck);
            Assert.True(rig.ViewModel.ResultIsError);
        }

        [Fact]
        public async Task A_typed_master_key_without_a_key_sends_nothing()
        {
            using var rig = new Rig();
            await rig.ConnectedAsync();
            rig.ViewModel.MasterUsesFactoryKey = false;

            var tried = await rig.ViewModel.AuthenticateAsync(null);

            Assert.False(tried);
            Assert.True(rig.ViewModel.MasterKeyIsTyped);
            Assert.Empty(rig.Session.Calls);
            Assert.True(rig.ViewModel.ResultIsError);
            Assert.Equal("Enter the key, or choose a stored one.", rig.ViewModel.ResultText);
        }

        [Fact]
        public async Task A_typed_master_key_reaches_the_sam_and_stays_in_no_property_of_the_view_model()
        {
            using var rig = new Rig();
            await rig.ConnectedAsync();
            rig.ViewModel.MasterUsesFactoryKey = false;
            using var typed = TestText.Secure(TypedKeyHex);

            var tried = await rig.ViewModel.AuthenticateAsync(typed);

            Assert.True(tried);
            Assert.Equal(TypedKeyHex, Convert.ToHexString(rig.Session.KeyCopies.Single()));
            Assert.All(StringValuesOf(rig.ViewModel), value => Assert.DoesNotContain(TypedKeyHex, value, StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task A_stored_master_key_is_taken_from_the_credential_manager_by_its_reference()
        {
            using var rig = new Rig(storedKeys: StoredName);
            await rig.ConnectedAsync();
            rig.ViewModel.MasterUsesFactoryKey = false;
            rig.ViewModel.MasterReference = StoredName;

            await rig.ViewModel.AuthenticateAsync(null);

            Assert.False(rig.ViewModel.MasterKeyIsTyped);
            Assert.Equal(StoredKeyHex, Convert.ToHexString(rig.Session.KeyCopies.Single()));
            Assert.All(StringValuesOf(rig.ViewModel), value => Assert.DoesNotContain(StoredKeyHex, value, StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task A_stored_key_that_has_gone_from_the_vault_is_no_longer_a_choice_after_the_next_search()
        {
            using var rig = new Rig(storedKeys: StoredName);
            await rig.ViewModel.RefreshAsync();
            rig.ViewModel.MasterReference = StoredName;
            rig.ViewModel.NewKeyReference = StoredName;
            rig.Store.Blobs.Clear();

            await rig.ViewModel.RefreshAsync();

            Assert.Equal(string.Empty, rig.ViewModel.MasterReference);
            Assert.Equal(string.Empty, rig.ViewModel.NewKeyReference);
        }

        #endregion

        #region write a key

        [Theory]
        [InlineData("0")]
        [InlineData("128")]
        [InlineData("1000")]
        [InlineData("-1")]
        [InlineData("abc")]
        [InlineData("")]
        [InlineData(" 5")]
        public async Task A_key_entry_outside_1_to_127_cannot_be_written_and_nothing_is_sent(string entryText)
        {
            using var rig = new Rig();
            await rig.AuthenticatedAsync();
            rig.ViewModel.EntryNumberText = entryText;
            using var typed = TestText.Secure(TypedKeyHex);

            Assert.False(rig.ViewModel.IsEntryNumberValid);
            Assert.False(rig.ViewModel.CanWrite);

            var tried = await rig.ViewModel.WriteKeyAsync(typed);

            Assert.False(tried);
            Assert.DoesNotContain(rig.Session.Calls, call => call.StartsWith("change", StringComparison.Ordinal));
            Assert.True(rig.ViewModel.ResultIsError);
        }

        [Theory]
        [InlineData("1")]
        [InlineData("5")]
        [InlineData("127")]
        public async Task A_typed_key_is_written_into_the_chosen_entry_and_stays_in_no_property_of_the_view_model(string entryText)
        {
            using var rig = new Rig();
            await rig.AuthenticatedAsync();
            rig.ViewModel.EntryNumberText = entryText;
            using var typed = TestText.Secure(TypedKeyHex);

            Assert.True(rig.ViewModel.CanWrite);

            var tried = await rig.ViewModel.WriteKeyAsync(typed);

            Assert.True(tried);
            Assert.Equal(byte.Parse(entryText), rig.Session.LastEntryNumber);
            Assert.Equal(TypedKeyHex, Convert.ToHexString(rig.Session.KeyCopies.Last()));
            Assert.Contains("key entry " + entryText, rig.ViewModel.ResultText, StringComparison.Ordinal);
            Assert.False(rig.ViewModel.ResultIsError);
            Assert.All(StringValuesOf(rig.ViewModel), value => Assert.DoesNotContain(TypedKeyHex, value, StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task A_key_that_was_written_is_read_back_and_the_last_line_says_that_the_entry_is_enabled_and_of_the_picc_class()
        {
            using var rig = new Rig();
            await rig.AuthenticatedAsync();
            rig.ViewModel.EntryNumberText = "5";
            using var typed = TestText.Secure(TypedKeyHex);

            await rig.ViewModel.WriteKeyAsync(typed);

            Assert.Equal("read 5", rig.Session.Calls.Last());
            Assert.Contains("The key was written to key entry 5.", rig.ViewModel.StatusText, StringComparison.Ordinal);
            Assert.Equal("Read back from the SAM: key entry 5 is enabled and holds an AES-128 key of the PICC key class, as written.", rig.ViewModel.ResultText);
            Assert.False(rig.ViewModel.ResultIsError);
        }

        [Fact]
        public async Task The_key_type_is_aes_until_the_person_chooses_another_and_both_kinds_are_offered()
        {
            using var rig = new Rig();

            Assert.Equal(SamEntryKind.Aes128, rig.ViewModel.SelectedEntryKind);
            Assert.Equal(new[] { SamEntryKind.Aes128, SamEntryKind.NativeTdea }, rig.ViewModel.EntryKinds.Select(k => k.Kind));
            Assert.Equal(new[] { "AES-128 (DESFire)", "DES / 2K3DES (DESFire native)" }, rig.ViewModel.EntryKinds.Select(k => k.DisplayName));
            Assert.False(string.IsNullOrWhiteSpace(rig.ViewModel.EntryKindLabel));
            Assert.False(string.IsNullOrWhiteSpace(rig.ViewModel.EntryKindToolTip));

            await Task.CompletedTask;
        }

        [Fact]
        public async Task A_native_key_is_written_through_the_native_path_and_the_last_line_says_that_it_is_a_native_picc_key()
        {
            using var rig = new Rig();
            await rig.AuthenticatedAsync();
            rig.ViewModel.SelectedEntryKind = SamEntryKind.NativeTdea;
            rig.ViewModel.EntryNumberText = "5";
            using var typed = TestText.Secure(TypedKeyHex);

            await rig.ViewModel.WriteKeyAsync(typed);

            Assert.Contains("writenative 5", rig.Session.Calls);
            Assert.Equal("read 5", rig.Session.Calls.Last());
            Assert.Equal("Read back from the SAM: key entry 5 is enabled and holds a DES / 2K3DES key of the PICC key class, as written.", rig.ViewModel.ResultText);
            Assert.False(rig.ViewModel.ResultIsError);
        }

        [Fact]
        public async Task A_native_key_that_reads_back_as_something_else_is_said_in_red_with_the_native_text()
        {
            using var rig = new Rig();
            await rig.AuthenticatedAsync();
            rig.ViewModel.SelectedEntryKind = SamEntryKind.NativeTdea;
            rig.Session.ReadAnswer = Convert.FromHexString("000000000000000000FF240001");
            rig.ViewModel.EntryNumberText = "5";
            using var typed = TestText.Secure(TypedKeyHex);

            await rig.ViewModel.WriteKeyAsync(typed);

            Assert.True(rig.ViewModel.ResultIsError);
            Assert.Contains("is not a DES / 2K3DES key of the PICC key class", rig.ViewModel.ResultText, StringComparison.Ordinal);
        }
        [Theory]
        [InlineData("000000000000000000FF200200", "is disabled (SET bit 9)")]
        [InlineData("000000000000000000FF200000", "is not an AES-128 key of the PICC key class")]
        [InlineData("000000000000000000FF200001", "Keep IV")]
        [InlineData("0000", "reading the key entry back failed")]
        public async Task A_read_back_that_is_not_what_was_written_is_said_in_red_after_the_line_that_the_key_was_written(string answerHex, string expected)
        {
            using var rig = new Rig();
            await rig.AuthenticatedAsync();
            rig.Session.ReadAnswer = Convert.FromHexString(answerHex);
            rig.ViewModel.EntryNumberText = "5";
            using var typed = TestText.Secure(TypedKeyHex);

            var tried = await rig.ViewModel.WriteKeyAsync(typed);

            Assert.True(tried);
            Assert.True(rig.ViewModel.ResultIsError);
            Assert.Contains(expected, rig.ViewModel.ResultText, StringComparison.Ordinal);
            Assert.Contains("The key was written to key entry 5.", rig.ViewModel.StatusText, StringComparison.Ordinal);
        }

        [Fact]
        public async Task A_key_that_was_not_written_is_not_read_back_and_nothing_is_said_about_the_entry()
        {
            using var rig = new Rig();
            await rig.AuthenticatedAsync();
            rig.Session.ChangeFails = new SamException("refused", 0x6A80);
            using var typed = TestText.Secure(TypedKeyHex);

            await rig.ViewModel.WriteKeyAsync(typed);

            Assert.DoesNotContain(rig.Session.Calls, call => call.StartsWith("read", StringComparison.Ordinal));
            Assert.DoesNotContain("Read back", rig.ViewModel.StatusText, StringComparison.Ordinal);
        }

        [Fact]
        public async Task A_stored_key_is_written_by_its_reference()
        {
            using var rig = new Rig(storedKeys: StoredName);
            await rig.AuthenticatedAsync();
            rig.ViewModel.NewKeyReference = StoredName;

            Assert.False(rig.ViewModel.NewKeyIsTyped);

            await rig.ViewModel.WriteKeyAsync(null);

            Assert.Equal(StoredKeyHex, Convert.ToHexString(rig.Session.KeyCopies.Last()));
        }

        [Fact]
        public async Task Without_a_key_nothing_is_written()
        {
            using var rig = new Rig();
            await rig.AuthenticatedAsync();

            var tried = await rig.ViewModel.WriteKeyAsync(null);

            Assert.False(tried);
            Assert.DoesNotContain(rig.Session.Calls, call => call.StartsWith("change", StringComparison.Ordinal));
            Assert.Equal("Enter the key, or choose a stored one.", rig.ViewModel.ResultText);
        }

        [Fact]
        public async Task A_key_entry_that_the_sam_refuses_ends_the_authentication_and_names_the_status_word()
        {
            using var rig = new Rig();
            await rig.AuthenticatedAsync();
            rig.Session.ChangeFails = new SamException("refused", 0x6A80);
            using var typed = TestText.Secure(TypedKeyHex);

            var tried = await rig.ViewModel.WriteKeyAsync(typed);

            Assert.True(tried);
            Assert.True(rig.ViewModel.ResultIsError);
            Assert.Contains("6A80", rig.ViewModel.ResultText, StringComparison.Ordinal);
            Assert.Equal(SamSetupState.Connected, rig.ViewModel.State);
            Assert.False(rig.ViewModel.CanWrite);
        }

        [Fact]
        public async Task A_write_whose_answer_could_not_be_checked_says_that_the_key_may_have_been_written_and_is_no_refusal()
        {
            using var rig = new Rig();
            await rig.AuthenticatedAsync();
            rig.Session.ChangeFails = new SamException("The CMAC of the SAM response is invalid.", 0x9000);
            rig.ViewModel.EntryNumberText = "4";
            using var typed = TestText.Secure(TypedKeyHex);

            var tried = await rig.ViewModel.WriteKeyAsync(typed);

            Assert.True(tried);
            Assert.True(rig.ViewModel.ResultIsError);
            Assert.Contains("may have been written to key entry 4", rig.ViewModel.ResultText, StringComparison.Ordinal);
            Assert.DoesNotContain("refused", rig.ViewModel.ResultText, StringComparison.OrdinalIgnoreCase);

            // The session is gone: another write needs another authentication.
            Assert.Equal(SamSetupState.Connected, rig.ViewModel.State);
            Assert.False(rig.ViewModel.CanWrite);
        }

        [Fact]
        public async Task A_key_that_is_not_32_digits_is_not_written_and_the_digits_stay_for_the_person_to_correct()
        {
            using var rig = new Rig();
            await rig.AuthenticatedAsync();
            using var typed = TestText.Secure("0011");

            var tried = await rig.ViewModel.WriteKeyAsync(typed);

            Assert.False(tried);
            Assert.DoesNotContain(rig.Session.Calls, call => call.StartsWith("change", StringComparison.Ordinal));
            Assert.Contains("not a key of 16 bytes", rig.ViewModel.ResultText, StringComparison.Ordinal);
        }

        #endregion

        #region running, closing, texts

        [Fact]
        public async Task While_a_step_runs_nothing_else_can_start_and_the_window_cannot_be_closed()
        {
            using var rig = new Rig();
            await rig.ConnectedAsync();
            var release = new TaskCompletionSource();
            rig.Session.AuthenticateBlocker = release.Task;
            var closeRequests = 0;
            rig.ViewModel.CloseRequested += (_, _) => closeRequests++;

            var first = rig.ViewModel.AuthenticateAsync(null);

            Assert.True(rig.ViewModel.IsRunning);
            Assert.False(rig.ViewModel.AreInputsEnabled);
            Assert.False(rig.ViewModel.CanClose);
            Assert.False(rig.ViewModel.CanCheck);
            Assert.False(rig.ViewModel.CanAuthenticate);
            Assert.False(rig.ViewModel.CloseCommand.CanExecute(null));
            Assert.False(await rig.ViewModel.AuthenticateAsync(null));
            Assert.Single(rig.Session.Calls);

            release.SetResult();
            Assert.True(await first);

            Assert.False(rig.ViewModel.IsRunning);
            Assert.True(rig.ViewModel.CanClose);
            Assert.True(rig.ViewModel.CloseCommand.CanExecute(null));

            rig.ViewModel.CloseCommand.Execute(null);

            Assert.Equal(1, closeRequests);
        }

        [Fact]
        public async Task A_step_that_throws_unexpectedly_ends_the_connection_and_gives_a_fixed_sentence()
        {
            using var rig = new Rig();
            await rig.ConnectedAsync();
            rig.Session.AuthenticateFails = new InvalidOperationException(SecureKeyTestData.Secret);

            await rig.ViewModel.AuthenticateAsync(null);

            Assert.Equal(SamSetupState.Found, rig.ViewModel.State);
            Assert.True(rig.ViewModel.ResultIsError);
            Assert.DoesNotContain(SecureKeyTestData.Secret, rig.ViewModel.StatusText, StringComparison.Ordinal);
        }

        [Fact]
        public async Task The_status_collects_what_the_steps_said_with_the_newest_last()
        {
            using var rig = new Rig();

            await rig.AuthenticatedAsync();

            var lines = rig.ViewModel.StatusText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(3, lines.Length);
            Assert.EndsWith("Found a MIFARE SAM AV3 in 1 reader(s).", lines[0], StringComparison.Ordinal);
            Assert.EndsWith("Host authentication succeeded.", lines[2], StringComparison.Ordinal);
            Assert.Equal("Host authentication succeeded.", rig.ViewModel.ResultText);
        }

        [Fact]
        public void The_texts_follow_the_language_and_none_is_empty()
        {
            using var english = new Rig();
            using var german = new Rig("de");

            foreach (var rig in new[] { english, german })
            {
                var viewModel = rig.ViewModel;

                Assert.All(
                    new[]
                    {
                        viewModel.Title, viewModel.IntroText, viewModel.ReaderLabel, viewModel.RefreshText, viewModel.CheckText, viewModel.CheckToolTip,
                        viewModel.MasterHeader, viewModel.FactoryKeyText, viewModel.FactoryKeyToolTip, viewModel.StoredKeyLabel, viewModel.StoredKeyToolTip,
                        viewModel.KeyDigitsLabel, viewModel.ShowKeyText, viewModel.ShowKeyToolTip, viewModel.AuthenticateText, viewModel.AuthenticateToolTip,
                        viewModel.ActivateText, viewModel.ActivateToolTip, viewModel.NewKeyHeader, viewModel.EntryLabel, viewModel.EntryToolTip,
                        viewModel.WriteText, viewModel.WriteToolTip, viewModel.StatusLabel, viewModel.CloseText, viewModel.StateText,
                    },
                    text => Assert.False(string.IsNullOrWhiteSpace(text)));
            }

            Assert.Equal("MIFARE SAM AV3 setup", english.ViewModel.Title);
            Assert.Equal("MIFARE-SAM-AV3-Einrichtung", german.ViewModel.Title);
            Assert.Equal("SAM aktivieren...", german.ViewModel.ActivateText);
        }

        [Fact]
        public void No_property_of_the_view_model_can_hold_a_key()
        {
            var allowed = new[] { typeof(string), typeof(bool), typeof(int), typeof(SamSetupState), typeof(System.Windows.Input.ICommand), typeof(IReadOnlyList<string>), typeof(IReadOnlyList<SamKeyChoice>), typeof(IReadOnlyList<SamEntryKindChoice>), typeof(SamEntryKind) };

            foreach (var property in typeof(SamSetupViewModel).GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                Assert.True(
                    allowed.Contains(property.PropertyType) || property.PropertyType == typeof(string),
                    property.Name + " has the type " + property.PropertyType.Name + ", which is not one that holds no key");
                Assert.NotEqual(typeof(SecureString), property.PropertyType);
                Assert.NotEqual(typeof(byte[]), property.PropertyType);
                Assert.NotEqual(typeof(char[]), property.PropertyType);
            }
        }

        [Fact]
        public async Task Disposing_lets_go_of_the_sam_and_the_reader()
        {
            var rig = new Rig();
            await rig.ConnectedAsync();
            var card = rig.Source.Handed.Last().Connection;

            rig.Dispose();

            Assert.True(card.Disposed);
            Assert.True(rig.Session.Disposed);
        }

        private static IEnumerable<string> StringValuesOf(SamSetupViewModel viewModel) =>
            typeof(SamSetupViewModel)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(property => property.PropertyType == typeof(string) && property.GetIndexParameters().Length == 0)
                .Select(property => (string?)property.GetValue(viewModel) ?? string.Empty);

        #endregion
    }
}
