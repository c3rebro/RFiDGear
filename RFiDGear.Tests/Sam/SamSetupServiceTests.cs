using RFiDGear.Contracts;
using RFiDGear.Services.Sam;
using RFiDGear.Tests.SecureKeys;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Threading.Tasks;

using Elatec.NET.Sam;

using Xunit;

namespace RFiDGear.Tests.Sam
{
    /// <summary>The steps that set a MIFARE SAM AV3 up: connect, authenticate, activate, write a key entry.</summary>
    public class SamSetupServiceTests
    {
        private const string ReaderName = "HID Global OMNIKEY 5422 Smartcard Reader 0";
        private const string TypedKeyHex = "00112233445566778899AABBCCDDEEFF";
        private const string StoredReference = "sam/master-secret-name";

        private static SecureString Secure(string text)
        {
            var secure = new SecureString();

            foreach (var c in text)
            {
                secure.AppendChar(c);
            }

            secure.MakeReadOnly();
            return secure;
        }

        private static SamApduResponse Version() => new SamApduResponse(SamTestData.VersionAnswer().SkipLast(2).ToArray(), 0x9000);

        /// <summary>A setup over a bench with the SAM in <see cref="ReaderName"/>, a scripted session and scripted answers.</summary>
        private sealed class Rig : IDisposable
        {
            internal Rig(ScriptedSecureKeyProvider? provider = null, bool connect = true)
            {
                Source = new FakeSamReaderSource { Answer = apdu => Convert.ToHexString(apdu) == "8060000000" ? SamTestData.VersionAnswer() : new byte[] { 0x90, 0x00 } }
                    .With(ReaderName, SamTestData.SamAtr);
                Provider = provider ?? ScriptedSecureKeyProvider.Returning(SecureKeyTestData.SixteenBytes());
                Service = new SamSetupService(new SamReaderFinder(Source), _ => Session, Dialogs, new SamKeyReader(SamTestKeys.RegistryOf(Provider)));

                if (connect)
                {
                    Assert.Equal(SamStepResult.Done, Service.ConnectAsync(ReaderName).GetAwaiter().GetResult().Result);
                }
            }

            internal FakeSamReaderSource Source { get; }

            internal FakeHostSession Session { get; } = new FakeHostSession();

            internal ScriptedSamDialogs Dialogs { get; } = new ScriptedSamDialogs();

            internal ScriptedSecureKeyProvider Provider { get; }

            internal SamSetupService Service { get; }

            internal FakeSamCardConnection Card => Source.Handed.Last().Connection;

            public void Dispose() => Service.Dispose();
        }

        #region connect

        [Fact]
        public async Task Connecting_reaches_the_sam_and_reports_what_it_says_about_its_version()
        {
            using var rig = new Rig(connect: false);

            var outcome = await rig.Service.ConnectAsync(ReaderName);

            Assert.Equal(SamStepResult.Done, outcome.Result);
            Assert.Equal(ReaderName, outcome.Reader!.Name);
            Assert.Equal("04010105002301", outcome.Version!.HardwareInfo);
            Assert.Equal("04010105011F01", outcome.Version.SoftwareInfo);
            Assert.True(rig.Service.IsConnected);
            Assert.False(rig.Service.IsAuthenticated);

            // The only thing sent is the version: no key is used and nothing is changed.
            Assert.Equal("8060000000", Convert.ToHexString(Assert.Single(Assert.Single(rig.Source.Handed).Transport.Sent)));
            Assert.Empty(rig.Session.Calls);
        }

        [Fact]
        public async Task Connecting_to_a_reader_without_a_sam_says_so_and_keeps_nothing_open()
        {
            using var rig = new Rig(connect: false);
            rig.Source.With("Empty reader", null);

            var outcome = await rig.Service.ConnectAsync("Empty reader");

            Assert.Equal(SamStepResult.NoSam, outcome.Result);
            Assert.False(rig.Service.IsConnected);
        }

        [Fact]
        public async Task A_reader_that_fails_while_connecting_is_a_reader_error_and_is_let_go_of()
        {
            using var rig = new Rig(connect: false);
            rig.Source.Answer = _ => throw new InvalidOperationException(SecureKeyTestData.Secret);

            var outcome = await rig.Service.ConnectAsync(ReaderName);

            Assert.Equal(SamStepResult.ReaderError, outcome.Result);
            Assert.False(rig.Service.IsConnected);
            Assert.True(rig.Card.Disposed);
        }

        [Fact]
        public async Task A_second_connection_lets_go_of_the_first_one()
        {
            using var rig = new Rig();
            var first = rig.Card;
            var firstSession = rig.Session;

            await rig.Service.ConnectAsync(ReaderName);

            Assert.True(first.Disposed);
            Assert.True(firstSession.Disposed);
            Assert.NotSame(first, rig.Card);
        }

        #endregion

        #region authenticate

        [Fact]
        public async Task Authenticating_with_the_factory_key_uses_entry_0_version_0_and_all_zero_bytes()
        {
            using var rig = new Rig();

            var outcome = await rig.Service.AuthenticateAsync(SamKeySource.Factory());

            Assert.Equal(new SamStepOutcome(SamStepResult.Done), outcome);
            Assert.Equal(new[] { "authenticate 0 0" }, rig.Session.Calls);
            Assert.Equal(new byte[16], rig.Session.KeyCopies.Single());
            Assert.True(rig.Service.IsAuthenticated);
            Assert.Empty(rig.Provider.Requests);
        }

        [Fact]
        public async Task A_typed_key_is_passed_as_it_was_typed_and_erased_when_the_step_is_over()
        {
            using var rig = new Rig();
            using var typed = Secure(TypedKeyHex);

            var outcome = await rig.Service.AuthenticateAsync(SamKeySource.FromTyped(typed));

            Assert.Equal(SamStepResult.Done, outcome.Result);
            Assert.Equal(TypedKeyHex, Convert.ToHexString(rig.Session.KeyCopies.Single()));
            Assert.All(rig.Session.KeyArrays.Single(), b => Assert.Equal(0, b));
        }

        [Fact]
        public async Task A_stored_key_is_asked_for_once_with_what_it_is_for_and_is_erased_after_the_step()
        {
            var leases = new List<SecureKeyLease>();
            var provider = new ScriptedSecureKeyProvider((_, _) =>
            {
                var lease = new SecureKeyLease(SecureKeyTestData.SixteenBytes());
                leases.Add(lease);
                return new ValueTask<SecureKeyResolution>(SecureKeyResolution.Resolved(lease));
            });
            using var rig = new Rig(provider);

            var outcome = await rig.Service.AuthenticateAsync(SamKeySource.FromStored(StoredReference));

            Assert.Equal(SamStepResult.Done, outcome.Result);
            var request = Assert.Single(provider.Requests);
            Assert.Equal(SamSetupService.MasterSlot, request.SlotId);
            Assert.Equal(StoredReference, request.Reference);
            Assert.Equal(SecureKeyAlgorithm.Aes128, request.Algorithm);
            Assert.Equal(SecureKeyUsage.Authenticate, request.Usage);
            Assert.Equal(Convert.ToHexString(SecureKeyTestData.SixteenBytes()), Convert.ToHexString(rig.Session.KeyCopies.Single()));
            Assert.All(rig.Session.KeyArrays.Single(), b => Assert.Equal(0, b));
            Assert.Throws<ObjectDisposedException>(() => leases.Single().ToHexString());
        }

        [Fact]
        public async Task A_sam_that_was_never_activated_answers_6985_and_no_key_was_wrong()
        {
            using var rig = new Rig();
            rig.Session.AuthenticateFails = new SamException("part 1", 0x6985);

            var outcome = await rig.Service.AuthenticateAsync(SamKeySource.Factory());

            Assert.Equal(new SamStepOutcome(SamStepResult.NotActivated, 0x6985), outcome);
            Assert.True(rig.Service.IsConnected);
            Assert.False(rig.Service.IsAuthenticated);
        }

        [Fact]
        public async Task A_refused_authentication_is_tried_once_and_never_again()
        {
            using var rig = new Rig();
            rig.Session.AuthenticateFails = new SamException("part 2", 0x901E);

            var outcome = await rig.Service.AuthenticateAsync(SamKeySource.Factory());

            Assert.Equal(new SamStepOutcome(SamStepResult.AuthFailed, 0x901E), outcome);
            Assert.Single(rig.Session.Calls);
            Assert.True(rig.Service.IsConnected);
        }

        [Fact]
        public async Task A_proof_of_the_sam_that_does_not_check_out_is_a_failed_authentication_without_a_status_word()
        {
            using var rig = new Rig();
            rig.Session.AuthenticateFails = new SamException("The SAM failed to prove knowledge of the key (CMAC mismatch).", 0x90AF);

            var outcome = await rig.Service.AuthenticateAsync(SamKeySource.Factory());

            Assert.Equal(SamStepResult.AuthFailed, outcome.Result);
        }

        [Fact]
        public async Task A_reader_that_fails_during_authentication_ends_the_connection()
        {
            using var rig = new Rig();
            rig.Session.AuthenticateFails = new SamReaderException("reader");

            var outcome = await rig.Service.AuthenticateAsync(SamKeySource.Factory());

            Assert.Equal(new SamStepOutcome(SamStepResult.ReaderError), outcome);
            Assert.False(rig.Service.IsConnected);
            Assert.True(rig.Session.Disposed);
            Assert.True(rig.Card.Disposed);
        }

        [Fact]
        public async Task An_unexpected_exception_is_a_failure_and_not_an_exception_of_the_dialog()
        {
            using var rig = new Rig();
            rig.Session.AuthenticateFails = new InvalidOperationException(SecureKeyTestData.Secret);

            var outcome = await rig.Service.AuthenticateAsync(SamKeySource.Factory());

            Assert.Equal(SamStepResult.Failed, outcome.Result);
            Assert.False(rig.Service.IsConnected);
        }

        [Fact]
        public async Task Without_a_connection_nothing_can_be_authenticated()
        {
            using var rig = new Rig(connect: false);

            var outcome = await rig.Service.AuthenticateAsync(SamKeySource.Factory());

            Assert.Equal(SamStepResult.NoSam, outcome.Result);
            Assert.Empty(rig.Session.Calls);
        }

        #endregion

        #region keys that are not there

        [Fact]
        public async Task A_stored_key_that_is_not_there_ends_the_step_before_the_card_is_touched()
        {
            using var rig = new Rig(ScriptedSecureKeyProvider.Failing(SecureKeyFailureReason.NotFound));

            var outcome = await rig.Service.AuthenticateAsync(SamKeySource.FromStored(StoredReference));

            Assert.Equal(SamStepResult.KeyUnavailable, outcome.Result);
            Assert.Empty(rig.Session.Calls);
        }

        [Fact]
        public async Task A_provider_that_throws_is_an_unavailable_key_and_its_text_goes_nowhere()
        {
            using var logs = CapturedLogs.Install();
            var provider = new ScriptedSecureKeyProvider((_, _) => throw new InvalidOperationException(SecureKeyTestData.Secret));
            using var rig = new Rig(provider);

            var outcome = await rig.Service.AuthenticateAsync(SamKeySource.FromStored(StoredReference));

            Assert.Equal(SamStepResult.KeyUnavailable, outcome.Result);
            Assert.Empty(rig.Session.Calls);
            Assert.All(logs.All().Select(CapturedLogs.EverythingWrittenFor), text => Assert.DoesNotContain(SecureKeyTestData.Secret, text, StringComparison.Ordinal));
        }

        [Fact]
        public async Task A_stored_value_that_is_not_sixteen_bytes_is_not_a_key_of_the_sam()
        {
            using var rig = new Rig(ScriptedSecureKeyProvider.Returning(new byte[8]));

            var outcome = await rig.Service.AuthenticateAsync(SamKeySource.FromStored(StoredReference));

            Assert.Equal(SamStepResult.InvalidKey, outcome.Result);
            Assert.Empty(rig.Session.Calls);
        }

        [Fact]
        public async Task A_kind_of_key_the_host_does_not_know_is_not_a_key_and_is_disposed()
        {
            var unknown = new UnknownKindOfKey();
            var provider = new ScriptedSecureKeyProvider((_, _) => new ValueTask<SecureKeyResolution>(SecureKeyResolution.Resolved(unknown)));
            using var rig = new Rig(provider);

            var outcome = await rig.Service.AuthenticateAsync(SamKeySource.FromStored(StoredReference));

            Assert.Equal(SamStepResult.KeyUnavailable, outcome.Result);
            Assert.Equal(1, unknown.Disposals);
            Assert.Empty(rig.Session.Calls);
        }

        [Theory]
        [InlineData("")]
        [InlineData("00112233445566778899AABBCCDDEE")]
        [InlineData("00112233445566778899AABBCCDDEEFF00")]
        [InlineData("00112233445566778899AABBCCDDEEZZ")]
        public async Task A_typed_value_that_is_not_thirty_two_digits_is_not_a_key(string typedText)
        {
            using var rig = new Rig();
            using var typed = Secure(typedText);

            var outcome = await rig.Service.AuthenticateAsync(SamKeySource.FromTyped(typed));

            Assert.Equal(SamStepResult.InvalidKey, outcome.Result);
            Assert.Empty(rig.Session.Calls);
        }

        [Fact]
        public async Task A_stored_key_without_a_reference_is_not_asked_for()
        {
            using var rig = new Rig();

            var outcome = await rig.Service.AuthenticateAsync(SamKeySource.FromStored(string.Empty));

            Assert.Equal(SamStepResult.KeyUnavailable, outcome.Result);
            Assert.Empty(rig.Provider.Requests);
            Assert.Empty(rig.Session.Calls);
        }

        #endregion

        #region activate

        [Fact]
        public async Task Without_a_yes_nothing_is_sent_and_no_key_is_read()
        {
            using var rig = new Rig();
            rig.Dialogs.Agree = false;

            var outcome = await rig.Service.ActivateAsync(SamKeySource.FromStored(StoredReference));

            Assert.Equal(SamStepResult.Declined, outcome.Result);
            Assert.Equal(1, rig.Dialogs.ActivationAsked);
            Assert.Empty(rig.Session.Calls);
            Assert.Empty(rig.Provider.Requests);
        }

        [Fact]
        public async Task With_a_yes_the_sam_is_activated_once_with_entry_0_version_0_and_nothing_follows_it()
        {
            using var rig = new Rig();
            rig.Dialogs.Agree = true;

            var outcome = await rig.Service.ActivateAsync(SamKeySource.Factory());

            Assert.Equal(new SamStepOutcome(SamStepResult.Done), outcome);
            Assert.Equal(new[] { "activate 0 0" }, rig.Session.Calls);
            Assert.Equal(new byte[16], rig.Session.KeyCopies.Single());
            Assert.Equal(1, rig.Dialogs.ActivationAsked);
        }

        [Fact]
        public async Task A_key_that_is_not_there_costs_the_yes_but_not_the_sam()
        {
            using var rig = new Rig(ScriptedSecureKeyProvider.Failing(SecureKeyFailureReason.NotFound));
            rig.Dialogs.Agree = true;

            var outcome = await rig.Service.ActivateAsync(SamKeySource.FromStored(StoredReference));

            Assert.Equal(SamStepResult.KeyUnavailable, outcome.Result);
            Assert.Empty(rig.Session.Calls);
        }

        [Fact]
        public async Task A_refusal_of_the_sam_is_reported_with_its_status_word()
        {
            using var rig = new Rig();
            rig.Dialogs.Agree = true;
            rig.Session.ActivateFails = new SamException("part 1", 0x6985);

            var outcome = await rig.Service.ActivateAsync(SamKeySource.Factory());

            Assert.Equal(new SamStepOutcome(SamStepResult.Refused, 0x6985), outcome);
            Assert.Single(rig.Session.Calls);
        }

        [Fact]
        public async Task Success_of_the_last_command_with_a_proof_that_is_wrong_may_have_activated_the_sam()
        {
            using var rig = new Rig();
            rig.Dialogs.Agree = true;
            rig.Session.ActivateFails = new SamException("The SAM reported success, but the proof is invalid.", 0x9000);

            var outcome = await rig.Service.ActivateAsync(SamKeySource.Factory());

            Assert.Equal(SamStepResult.MayHaveTakenEffect, outcome.Result);

            // What is known of the SAM is not to be trusted any more: the next step starts from a fresh check, which is what the
            // window needs to stop offering the activation.
            Assert.False(rig.Service.IsConnected);
            Assert.True(rig.Session.Disposed);
            Assert.True(rig.Card.Disposed);
        }

        [Fact]
        public async Task A_reader_that_fails_during_the_activation_may_have_activated_the_sam_and_ends_the_connection()
        {
            using var rig = new Rig();
            rig.Dialogs.Agree = true;
            rig.Session.ActivateFails = new SamReaderException("reader");

            var outcome = await rig.Service.ActivateAsync(SamKeySource.Factory());

            Assert.Equal(SamStepResult.MayHaveTakenEffect, outcome.Result);
            Assert.False(rig.Service.IsConnected);
        }

        [Fact]
        public async Task Without_a_connection_nothing_is_asked_and_nothing_is_activated()
        {
            using var rig = new Rig(connect: false);
            rig.Dialogs.Agree = true;

            var outcome = await rig.Service.ActivateAsync(SamKeySource.Factory());

            Assert.Equal(SamStepResult.NoSam, outcome.Result);
            Assert.Equal(0, rig.Dialogs.ActivationAsked);
            Assert.Empty(rig.Session.Calls);
        }

        #endregion

        #region write a key entry

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(128)]
        [InlineData(1000)]
        public async Task The_master_entry_and_numbers_outside_the_sam_are_never_written_and_cost_no_key(int entryNumber)
        {
            using var rig = new Rig();
            rig.Session.IsAuthenticated = true;

            var outcome = await rig.Service.WriteKeyEntryAsync(entryNumber, SamKeySource.FromStored(StoredReference));

            Assert.Equal(SamStepResult.InvalidEntry, outcome.Result);
            Assert.Empty(rig.Session.Calls);
            Assert.Empty(rig.Provider.Requests);
        }

        [Fact]
        public async Task Nothing_is_written_before_a_host_authentication()
        {
            using var rig = new Rig();

            var outcome = await rig.Service.WriteKeyEntryAsync(1, SamKeySource.FromStored(StoredReference));

            Assert.Equal(SamStepResult.NotAuthenticated, outcome.Result);
            Assert.Empty(rig.Session.Calls);
            Assert.Empty(rig.Provider.Requests);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(127)]
        public async Task An_aes_picc_key_is_written_with_version_0_and_the_master_key_as_the_key_that_changes_it(int entryNumber)
        {
            using var rig = new Rig();
            await rig.Service.AuthenticateAsync(SamKeySource.Factory());
            using var typed = Secure(TypedKeyHex);

            var outcome = await rig.Service.WriteKeyEntryAsync(entryNumber, SamKeySource.FromTyped(typed));

            // The entry is read back, and it is what was written: enabled (SET bit 9 clear), AES-128, PICC key class.
            Assert.Equal(new SamStepOutcome(SamStepResult.Done, 0, SamKeyEntryCheck.Matches), outcome);
            Assert.Equal("read " + entryNumber, rig.Session.Calls.Last());
            Assert.Equal((byte)entryNumber, rig.Session.LastEntryNumber);

            var entry = rig.Session.LastEntry!;
            Assert.Equal(TypedKeyHex, Convert.ToHexString(rig.Session.KeyCopies.Last()));
            Assert.Equal(0, entry.VersionA);
            Assert.Null(entry.KeyB);
            Assert.Null(entry.KeyC);
            Assert.Equal(SamAv3KeyEntry.SetKeyTypeAes128, entry.Set);
            Assert.Equal(SamAv3KeyEntry.ExtSetKeyClassPicc, entry.ExtSet);
            Assert.False(entry.IsHostKey);
            Assert.Equal(0, entry.KeyNoCek);
            Assert.Equal(0, entry.KeyVersionCek);
            Assert.Equal(SamAv3KeyEntry.NoKeyUsageCounter, entry.RefNoKuc);
            Assert.All(rig.Session.KeyArrays.Last(), b => Assert.Equal(0, b));
        }

        [Fact]
        public async Task The_key_to_write_is_asked_for_as_a_new_key_in_its_own_slot()
        {
            using var rig = new Rig();
            await rig.Service.AuthenticateAsync(SamKeySource.Factory());

            await rig.Service.WriteKeyEntryAsync(1, SamKeySource.FromStored(StoredReference));

            var request = Assert.Single(rig.Provider.Requests);
            Assert.Equal(SamSetupService.NewKeySlot, request.SlotId);
            Assert.Equal(SecureKeyUsage.ChangeKeyNew, request.Usage);
            Assert.Equal(SecureKeyAlgorithm.Aes128, request.Algorithm);
        }

        [Fact]
        public async Task A_refused_key_entry_is_reported_with_its_status_word_and_is_not_tried_again()
        {
            using var rig = new Rig();
            await rig.Service.AuthenticateAsync(SamKeySource.Factory());
            rig.Session.ChangeFails = new SamException("refused", 0x6A80);

            var outcome = await rig.Service.WriteKeyEntryAsync(1, SamKeySource.FromStored(StoredReference));

            Assert.Equal(new SamStepOutcome(SamStepResult.Refused, 0x6A80), outcome);
            Assert.Single(rig.Session.Calls.Where(c => c.StartsWith("change", StringComparison.Ordinal)));
            Assert.False(rig.Service.IsAuthenticated);
        }

        [Fact]
        public async Task A_success_whose_cmac_is_missing_or_wrong_may_have_written_the_key_and_is_no_refusal()
        {
            using var rig = new Rig();
            await rig.Service.AuthenticateAsync(SamKeySource.Factory());

            // What the library throws when the SAM said success and the CMAC of its answer does not check out.
            rig.Session.ChangeFails = new SamException("The CMAC of the SAM response is invalid.", 0x9000);

            var outcome = await rig.Service.WriteKeyEntryAsync(1, SamKeySource.FromStored(StoredReference));

            Assert.Equal(new SamStepOutcome(SamStepResult.MayHaveTakenEffect, 0x9000), outcome);
            Assert.Single(rig.Session.Calls.Where(c => c.StartsWith("change", StringComparison.Ordinal)));

            // The session is gone (the library discards it): a new authentication is needed. The connection is still there.
            Assert.False(rig.Service.IsAuthenticated);
            Assert.True(rig.Service.IsConnected);
        }

        [Fact]
        public async Task A_reader_that_fails_while_the_key_is_written_ends_the_connection()
        {
            using var rig = new Rig();
            await rig.Service.AuthenticateAsync(SamKeySource.Factory());
            rig.Session.ChangeFails = new SamReaderException("reader");

            var outcome = await rig.Service.WriteKeyEntryAsync(1, SamKeySource.FromStored(StoredReference));

            Assert.Equal(SamStepResult.ReaderError, outcome.Result);
            Assert.False(rig.Service.IsConnected);
        }

        #endregion

        #region the entry is read back

        // The 13 bytes that three entries of a real MIFARE SAM AV3 answered to SAM_GetKeyEntry (see SamKeyEntryInfoTests).
        private const string BlankEntryAnswer = "000000000000000000FF200200";
        private const string MasterEntryAnswer = "000000000000000000FF200000";

        [Fact]
        public async Task A_written_entry_that_is_still_disabled_is_said_to_be_so_and_the_write_is_still_done()
        {
            using var rig = new Rig();
            await rig.Service.AuthenticateAsync(SamKeySource.Factory());
            rig.Session.ReadAnswer = Convert.FromHexString(BlankEntryAnswer);

            var outcome = await rig.Service.WriteKeyEntryAsync(2, SamKeySource.FromStored(StoredReference));

            Assert.Equal(new SamStepOutcome(SamStepResult.Done, 0, SamKeyEntryCheck.Disabled), outcome);
        }

        [Fact]
        public async Task A_written_entry_that_is_not_of_the_picc_class_is_said_to_differ()
        {
            using var rig = new Rig();
            await rig.Service.AuthenticateAsync(SamKeySource.Factory());
            rig.Session.ReadAnswer = Convert.FromHexString(MasterEntryAnswer);

            var outcome = await rig.Service.WriteKeyEntryAsync(1, SamKeySource.FromStored(StoredReference));

            Assert.Equal(new SamStepOutcome(SamStepResult.Done, 0, SamKeyEntryCheck.WrongKind), outcome);
        }

        [Fact]
        public async Task An_answer_that_is_no_key_entry_is_unreadable_and_the_write_is_still_done()
        {
            using var rig = new Rig();
            await rig.Service.AuthenticateAsync(SamKeySource.Factory());
            rig.Session.ReadAnswer = new byte[5];

            var outcome = await rig.Service.WriteKeyEntryAsync(1, SamKeySource.FromStored(StoredReference));

            Assert.Equal(new SamStepOutcome(SamStepResult.Done, 0, SamKeyEntryCheck.Unreadable), outcome);
        }

        [Fact]
        public async Task A_sam_that_refuses_the_read_back_leaves_the_write_done_and_the_session_gone()
        {
            using var rig = new Rig();
            await rig.Service.AuthenticateAsync(SamKeySource.Factory());
            rig.Session.ReadFails = new SamException("refused", 0x6A86);

            var outcome = await rig.Service.WriteKeyEntryAsync(1, SamKeySource.FromStored(StoredReference));

            Assert.Equal(new SamStepOutcome(SamStepResult.Done, 0, SamKeyEntryCheck.Unreadable), outcome);
            Assert.False(rig.Service.IsAuthenticated);
            Assert.True(rig.Service.IsConnected);
        }

        [Fact]
        public async Task A_reader_that_fails_during_the_read_back_ends_the_connection_and_the_write_is_still_done()
        {
            using var rig = new Rig();
            await rig.Service.AuthenticateAsync(SamKeySource.Factory());
            rig.Session.ReadFails = new SamReaderException("reader");

            var outcome = await rig.Service.WriteKeyEntryAsync(1, SamKeySource.FromStored(StoredReference));

            Assert.Equal(new SamStepOutcome(SamStepResult.Done, 0, SamKeyEntryCheck.Unreadable), outcome);
            Assert.False(rig.Service.IsConnected);
        }

        [Fact]
        public async Task A_write_that_was_refused_or_cannot_be_proved_is_not_read_back()
        {
            using var refused = new Rig();
            await refused.Service.AuthenticateAsync(SamKeySource.Factory());
            refused.Session.ChangeFails = new SamException("refused", 0x6A80);

            var refusal = await refused.Service.WriteKeyEntryAsync(1, SamKeySource.FromStored(StoredReference));

            Assert.Equal(SamKeyEntryCheck.NotChecked, refusal.Check);
            Assert.DoesNotContain(refused.Session.Calls, call => call.StartsWith("read", StringComparison.Ordinal));

            using var unproved = new Rig();
            await unproved.Service.AuthenticateAsync(SamKeySource.Factory());
            unproved.Session.ChangeFails = new SamException("The CMAC of the SAM response is invalid.", 0x9000);

            var uncertain = await unproved.Service.WriteKeyEntryAsync(1, SamKeySource.FromStored(StoredReference));

            Assert.Equal(SamStepResult.MayHaveTakenEffect, uncertain.Result);
            Assert.Equal(SamKeyEntryCheck.NotChecked, uncertain.Check);
            Assert.DoesNotContain(unproved.Session.Calls, call => call.StartsWith("read", StringComparison.Ordinal));
        }

        #endregion

        #region logs and the end

        [Fact]
        public async Task No_key_no_reference_no_serial_and_no_exception_text_reaches_a_log()
        {
            using var logs = CapturedLogs.Install();
            using var rig = new Rig();
            rig.Dialogs.Agree = true;
            using var typed = Secure(TypedKeyHex);

            await rig.Service.AuthenticateAsync(SamKeySource.FromTyped(typed));
            await rig.Service.WriteKeyEntryAsync(5, SamKeySource.FromStored(StoredReference));
            rig.Session.ActivateFails = new SamException(SecureKeyTestData.Secret, 0x6A80);
            await rig.Service.ActivateAsync(SamKeySource.FromTyped(typed));
            rig.Session.AuthenticateFails = new InvalidOperationException(SecureKeyTestData.Secret);
            await rig.Service.AuthenticateAsync(SamKeySource.FromTyped(typed));

            var written = logs.All().Select(CapturedLogs.EverythingWrittenFor).ToList();
            Assert.NotEmpty(written);
            Assert.All(written, text =>
            {
                Assert.DoesNotContain(TypedKeyHex, text, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(StoredReference, text, StringComparison.Ordinal);
                Assert.DoesNotContain(SecureKeyTestData.Secret, text, StringComparison.Ordinal);
                Assert.DoesNotContain("EEEE", text, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(ReaderName, text, StringComparison.Ordinal);
            });
        }

        [Fact]
        public async Task Disposing_lets_go_of_the_session_and_the_card()
        {
            var rig = new Rig();
            var card = rig.Card;

            rig.Dispose();

            Assert.True(rig.Session.Disposed);
            Assert.True(card.Disposed);
            Assert.False(rig.Service.IsConnected);
            await Task.CompletedTask;
        }

        [Fact]
        public void A_missing_part_is_refused()
        {
            var source = new FakeSamReaderSource();
            var finder = new SamReaderFinder(source);
            var provider = ScriptedSecureKeyProvider.Returning(SecureKeyTestData.SixteenBytes());
            var dialogs = new ScriptedSamDialogs();
            var keys = new SamKeyReader(SamTestKeys.RegistryOf(provider));
            Func<Elatec.NET.Interfaces.ISamApduChannel, ISamHostSession> sessions = _ => new FakeHostSession();

            Assert.Throws<ArgumentNullException>(() => new SamSetupService(null!, sessions, dialogs, keys));
            Assert.Throws<ArgumentNullException>(() => new SamSetupService(finder, null!, dialogs, keys));
            Assert.Throws<ArgumentNullException>(() => new SamSetupService(finder, sessions, null!, keys));
            Assert.Throws<ArgumentNullException>(() => new SamSetupService(finder, sessions, dialogs, null!));
            Assert.Throws<ArgumentNullException>(() => new SamKeyReader(null!));
        }

        #endregion

        #region the real session, the real channel

        [Fact]
        public async Task A_sam_that_was_never_activated_is_told_apart_through_the_real_session_and_channel()
        {
            // No crypto is needed to get there: part 1 of SAM_AuthenticateHost already answers 6985.
            var source = new FakeSamReaderSource
            {
                Answer = apdu => apdu[1] == 0xA4 ? new byte[] { 0x69, 0x85 } : SamTestData.VersionAnswer(),
            }.With(ReaderName, SamTestData.SamAtr);
            using var service = new SamSetupService(
                new SamReaderFinder(source),
                channel => new Av3HostSession(channel),
                new ScriptedSamDialogs(),
                new SamKeyReader(SamTestKeys.RegistryOf(ScriptedSecureKeyProvider.Returning(SecureKeyTestData.SixteenBytes()))));
            await service.ConnectAsync(ReaderName);

            var outcome = await service.AuthenticateAsync(SamKeySource.Factory());

            Assert.Equal(new SamStepOutcome(SamStepResult.NotActivated, 0x6985), outcome);
            var sent = source.Handed.Single().Transport.Sent;
            Assert.Equal(2, sent.Count);
            Assert.Equal("80A400000300000200", Convert.ToHexString(sent[1]));
        }

        [Fact]
        public async Task A_reader_that_fails_is_told_apart_from_a_sam_that_says_no_through_the_real_session_and_channel()
        {
            var source = new FakeSamReaderSource
            {
                Answer = apdu => apdu[1] == 0xA4 ? throw new InvalidOperationException(SecureKeyTestData.Secret) : SamTestData.VersionAnswer(),
            }.With(ReaderName, SamTestData.SamAtr);
            using var service = new SamSetupService(
                new SamReaderFinder(source),
                channel => new Av3HostSession(channel),
                new ScriptedSamDialogs(),
                new SamKeyReader(SamTestKeys.RegistryOf(ScriptedSecureKeyProvider.Returning(SecureKeyTestData.SixteenBytes()))));
            await service.ConnectAsync(ReaderName);

            var outcome = await service.AuthenticateAsync(SamKeySource.Factory());

            Assert.Equal(SamStepResult.ReaderError, outcome.Result);
            Assert.False(service.IsConnected);
        }

        [Fact]
        public async Task The_real_session_wrapper_never_lifts_the_safeguards_of_the_library()
        {
            // The library checks both safeguards before it checks the authentication, so a session that was never
            // authenticated tells which one stopped the call: the safeguard, or the missing authentication.
            var channel = new ScriptedSamChannel(_ => new SamApduResponse(Array.Empty<byte>(), 0x9000));
            using var session = new Av3HostSession(channel);
            var key = new byte[16];

            var master = await Assert.ThrowsAsync<InvalidOperationException>(() => session.ChangeKeyEntryAsync(0, SamAv3KeyEntry.CreateAes128PiccKey(key, 0)));
            Assert.Contains("master key", master.Message, StringComparison.Ordinal);

            var locking = SamAv3KeyEntry.CreateAes128PiccKey(key, 0);
            locking.KeyNoCek = SamAv3KeyEntry.ChangeLocked;
            var lockout = await Assert.ThrowsAsync<InvalidOperationException>(() => session.ChangeKeyEntryAsync(1, locking));
            Assert.Contains("irreversibly", lockout.Message, StringComparison.Ordinal);

            Assert.Empty(channel.Sent);
        }

        [Fact]
        public async Task The_real_session_refuses_the_master_entry_before_the_card_is_asked()
        {
            var source = new FakeSamReaderSource().With(ReaderName, SamTestData.SamAtr);
            using var service = new SamSetupService(
                new SamReaderFinder(source),
                channel => new Av3HostSession(channel),
                new ScriptedSamDialogs(),
                new SamKeyReader(SamTestKeys.RegistryOf(ScriptedSecureKeyProvider.Returning(SecureKeyTestData.SixteenBytes()))));
            await service.ConnectAsync(ReaderName);
            var sentBefore = source.Handed.Single().Transport.Sent.Count;

            var outcome = await service.WriteKeyEntryAsync(0, SamKeySource.Factory());

            Assert.Equal(SamStepResult.InvalidEntry, outcome.Result);
            Assert.Equal(sentBefore, source.Handed.Single().Transport.Sent.Count);
        }

        #endregion
    }
}
