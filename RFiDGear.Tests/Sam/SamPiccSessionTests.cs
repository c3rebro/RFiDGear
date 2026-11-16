#nullable enable
using RFiDGear.Infrastructure;
using RFiDGear.Services.Sam;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Elatec.NET.Sam;

using Xunit;

namespace RFiDGear.Tests.Sam
{
    /// <summary>
    /// A DESFire card authenticated and its keys changed through a SAM, with the PC as the relay. The frames are the ones of the bench
    /// (<c>SAM-PICC-FIRST-CONTACT.md</c>); the cryptograms are recognisable bytes, because the PC only passes them on.
    /// </summary>
    public class SamPiccSessionTests
    {
        private static readonly SamKeyReference Des = new SamKeyReference(2, 0);
        private static readonly SamKeyReference Aes = new SamKeyReference(1, 0);

        private static string Hex(params byte[] bytes) => Convert.ToHexString(bytes);

        private static string Repeat(byte value, int count) => Hex(Enumerable.Repeat(value, count).ToArray());

        private const string Kill = "SAM 80CA0100";

        private const string SelectCard = "CARD 905A00000300000000";

        // The frames of an authentication of key 0 of the card with a SAM key, for a native key (8 byte numbers) or AES (16).
        private static IEnumerable<string> AuthenticateFrames(SamPiccKeyType type, SamKeyReference key, string select, byte cardKeyNo)
        {
            var length = type == SamPiccKeyType.Aes128 ? 16 : 8;
            var ins = type == SamPiccKeyType.Aes128 ? "AA" : "0A";

            yield return Kill;
            yield return select;
            yield return "CARD 90" + ins + "0000" + "01" + cardKeyNo.ToString("X2") + "00";
            yield return "SAM 800A0000" + (2 + length).ToString("X2") + key.Entry.ToString("X2") + key.Version.ToString("X2") + Repeat(0xB1, length) + "00";
            yield return "CARD 90AF0000" + (2 * length).ToString("X2") + Repeat(0xC3, 2 * length) + "00";
            yield return "SAM 800A0000" + length.ToString("X2") + Repeat(0xA2, length);
        }

        [Fact]
        public async Task A_native_key_authenticates_the_card_with_the_frames_of_the_bench()
        {
            var rig = new PiccRig();

            var result = await rig.Session.AuthenticateAsync(0, 0, SamPiccKeyType.NativeTdea, Des);

            Assert.True(result.Succeeded);
            Assert.Equal(ERROR.NoError, result.Error);
            Assert.Equal(AuthenticateFrames(SamPiccKeyType.NativeTdea, Des, SelectCard, 0), rig.Frames);

            // What the bench showed, byte for byte: the SAM part 1 of the DES key of entry 2.
            Assert.Contains("SAM 800A00000A0200B1B1B1B1B1B1B1B100", rig.Frames);
            Assert.Contains("CARD 900A0000010000", rig.Frames);
        }

        [Fact]
        public async Task An_aes_key_authenticates_with_the_aes_command_and_numbers_of_16_bytes()
        {
            var rig = new PiccRig();

            var result = await rig.Session.AuthenticateAsync(0, 0, SamPiccKeyType.Aes128, Aes);

            Assert.True(result.Succeeded);
            Assert.Equal(AuthenticateFrames(SamPiccKeyType.Aes128, Aes, SelectCard, 0), rig.Frames);
            Assert.Contains("CARD 90AA0000010000", rig.Frames);
            Assert.Contains("SAM 800A00001201" + "00" + Repeat(0xB1, 16) + "00", rig.Frames);
        }

        [Fact]
        public async Task The_application_and_the_key_number_of_the_card_are_the_ones_that_were_asked_for()
        {
            var rig = new PiccRig();

            // The AID goes low byte first: 0x010203 is 03 02 01.
            var result = await rig.Session.AuthenticateAsync(0x010203, 2, SamPiccKeyType.Aes128, new SamKeyReference(9, 4));

            Assert.True(result.Succeeded);
            Assert.Contains("CARD 905A00000303020100", rig.Frames);
            Assert.Contains("CARD 90AA0000010200", rig.Frames);
            Assert.Contains("SAM 800A00001209" + "04" + Repeat(0xB1, 16) + "00", rig.Frames);
        }

        [Fact]
        public async Task What_the_sam_says_goes_to_the_card_and_what_the_card_says_goes_to_the_sam_unchanged()
        {
            var rig = new PiccRig();

            await rig.Session.AuthenticateAsync(0, 0, SamPiccKeyType.Aes128, Aes);

            Assert.Contains(rig.CardFrames, f => f.Contains(Hex(rig.SamPart1Answer(16)), StringComparison.Ordinal));
            Assert.Contains(rig.SamFrames, f => f.EndsWith(Hex(rig.CardResponse(16)), StringComparison.Ordinal));
        }

        [Fact]
        public async Task A_card_that_refuses_the_key_ends_with_an_authentication_failure_and_the_sam_is_killed_once_more()
        {
            var rig = new PiccRig();
            rig.CardOverride = apdu => apdu[1] == 0xAF ? PiccRig.WithStatus(Array.Empty<byte>(), 0x91AE) : null;

            var result = await rig.Session.AuthenticateAsync(0, 0, SamPiccKeyType.Aes128, Aes);

            Assert.Equal(ERROR.AuthFailure, result.Error);
            Assert.Equal(SamPiccFailure.CardRefused, result.Failure);
            Assert.Equal((ushort)0x91AE, result.CardStatus);

            // The SAM waits for its part 2: it is killed, and its part 2 is never sent.
            Assert.Equal(Kill, rig.Frames.Last());
            Assert.Equal(2, rig.SamFrames.Count(f => f == Kill));
            Assert.DoesNotContain(rig.SamFrames, f => f.StartsWith("SAM 800A000010", StringComparison.Ordinal));
        }

        [Fact]
        public async Task A_sam_that_refuses_part_1_is_a_key_that_is_unavailable_and_nothing_more_goes_to_the_card()
        {
            var rig = new PiccRig();
            rig.SamOverride = c => c.Ins == 0x0A && c.Le.HasValue ? new SamApduResponse(Array.Empty<byte>(), 0x6A82) : null;

            var result = await rig.Session.AuthenticateAsync(0, 0, SamPiccKeyType.Aes128, Aes);

            Assert.Equal(ERROR.KeyUnavailable, result.Error);
            Assert.Equal(SamPiccFailure.SamRefused, result.Failure);
            Assert.Equal((ushort)0x6A82, result.SamStatus);
            Assert.Single(rig.CardFrames.Where(f => f.StartsWith("CARD 90AF", StringComparison.Ordinal)).DefaultIfEmpty("none"), "none");
            Assert.Equal(Kill, rig.Frames.Last());
        }

        [Fact]
        public async Task A_sam_that_does_not_accept_the_answer_of_the_card_is_an_integrity_error_and_is_not_killed_again()
        {
            var rig = new PiccRig();
            rig.SamOverride = c => c.Ins == 0x0A && !c.Le.HasValue ? new SamApduResponse(Array.Empty<byte>(), 0x901E) : null;

            var result = await rig.Session.AuthenticateAsync(0, 0, SamPiccKeyType.Aes128, Aes);

            // 901E at part 2 is what a key entry without Keep IV gave on the bench.
            Assert.Equal(ERROR.Unknown, result.Error);
            Assert.Equal(SamPiccFailure.SamIntegrity, result.Failure);
            Assert.Equal((ushort)0x901E, result.SamStatus);
            Assert.Single(rig.SamFrames.Where(f => f == Kill));
        }

        [Fact]
        public async Task A_card_without_the_application_is_a_protocol_constraint_and_nothing_else_is_sent()
        {
            var rig = new PiccRig();
            rig.CardOverride = apdu => apdu[1] == 0x5A ? PiccRig.WithStatus(Array.Empty<byte>(), 0x91A0) : null;

            var result = await rig.Session.AuthenticateAsync(5, 0, SamPiccKeyType.Aes128, Aes);

            Assert.Equal(ERROR.ProtocolConstraint, result.Error);
            Assert.Equal(SamPiccFailure.CardRefusedBeforeSam, result.Failure);
            Assert.Equal(new[] { Kill }, rig.SamFrames);
            Assert.Single(rig.CardFrames);
        }

        [Fact]
        public async Task A_card_that_dropped_an_unfinished_authentication_answers_the_select_with_91CA_and_the_select_is_sent_once_more()
        {
            var rig = new PiccRig();
            rig.SelectAborts = 1;

            var result = await rig.Session.AuthenticateAsync(0, 0, SamPiccKeyType.Aes128, Aes);

            Assert.True(result.Succeeded);
            Assert.Equal(2, rig.CardFrames.Count(f => f == SelectCard));
        }

        [Fact]
        public async Task A_select_that_is_aborted_twice_is_no_reason_to_go_on()
        {
            var rig = new PiccRig();
            rig.SelectAborts = 2;

            var result = await rig.Session.AuthenticateAsync(0, 0, SamPiccKeyType.Aes128, Aes);

            Assert.False(result.Succeeded);
            Assert.Equal(SamPiccFailure.CardRefusedBeforeSam, result.Failure);
            Assert.Equal((ushort)0x91CA, result.CardStatus);
            Assert.Equal(2, rig.CardFrames.Count());
        }

        [Fact]
        public async Task A_card_that_answers_with_something_else_than_the_challenge_is_refused_before_the_sam_is_asked()
        {
            var rig = new PiccRig();
            rig.CardOverride = apdu => apdu[1] == 0xAA ? PiccRig.WithStatus(new byte[8], 0x91AF) : null;

            var result = await rig.Session.AuthenticateAsync(0, 0, SamPiccKeyType.Aes128, Aes);

            Assert.False(result.Succeeded);
            Assert.Equal(SamPiccFailure.CardRefusedBeforeSam, result.Failure);
            Assert.Equal(new[] { Kill }, rig.SamFrames);
        }

        [Fact]
        public async Task A_card_that_refuses_its_first_authentication_command_is_refused_before_the_sam_and_is_no_refusal_of_the_key()
        {
            // A card that holds no key of this kind at this key number (an AES authentication on a card with a DES key) answers 91AE at once.
            var rig = new PiccRig();
            rig.CardOverride = apdu => apdu[1] == 0xAA ? PiccRig.WithStatus(Array.Empty<byte>(), 0x91AE) : null;

            var result = await rig.Session.AuthenticateAsync(0, 0, SamPiccKeyType.Aes128, Aes);

            Assert.Equal(ERROR.AuthFailure, result.Error);
            Assert.Equal(SamPiccFailure.CardRefusedBeforeSam, result.Failure);
            Assert.Equal((ushort)0x91AE, result.CardStatus);
            Assert.Equal(new[] { Kill }, rig.SamFrames);
        }

        [Fact]
        public async Task A_failing_sam_channel_is_a_transport_error_and_not_an_exception()
        {
            var rig = new PiccRig();
            rig.SamOverride = c => c.Ins == 0x0A ? throw new SamReaderException("the reader failed") : (SamApduResponse?)null;

            var result = await rig.Session.AuthenticateAsync(0, 0, SamPiccKeyType.Aes128, Aes);

            Assert.Equal(ERROR.TransportError, result.Error);
            Assert.Equal(SamPiccFailure.ReaderFailure, result.Failure);
            Assert.False(result.KeyMayHaveChanged);
        }

        [Fact]
        public async Task A_failing_card_transport_is_a_transport_error_and_the_sam_is_not_asked_again()
        {
            var rig = new PiccRig();
            rig.CardOverride = apdu => apdu[1] == 0xAA ? throw new InvalidOperationException("card text that must not be logged") : (byte[]?)null;

            var result = await rig.Session.AuthenticateAsync(0, 0, SamPiccKeyType.Aes128, Aes);

            Assert.Equal(ERROR.TransportError, result.Error);
            Assert.Equal(new[] { Kill }, rig.SamFrames);
        }

        [Fact]
        public async Task A_kill_that_fails_does_not_stop_the_authentication()
        {
            var rig = new PiccRig();
            rig.SamOverride = c => c.Ins == 0xCA ? new SamApduResponse(Array.Empty<byte>(), 0x6700) : null;

            var result = await rig.Session.AuthenticateAsync(0, 0, SamPiccKeyType.Aes128, Aes);

            Assert.True(result.Succeeded);
        }

        [Theory]
        [InlineData(0x1000000u)]
        [InlineData(0x1000001u)]
        [InlineData(0xFFFFFFFFu)]
        public async Task An_application_number_of_more_than_24_bits_is_refused_before_anything_is_sent(uint appId)
        {
            // The select sends three bytes: 0x01000001 would select application 1, and 0x01000000 the card itself.
            var rig = new PiccRig();

            var authenticated = await rig.Session.AuthenticateAsync(appId, 0, SamPiccKeyType.Aes128, Aes);
            var changed = await rig.Session.ChangeKeyAsync(new SamPiccKeyChange(appId, 0, SamPiccKeyType.Aes128, Aes, 1, SamPiccKeyType.Aes128, Aes, Des, true));

            Assert.Equal(ERROR.ProtocolConstraint, authenticated.Error);
            Assert.Equal(SamPiccFailure.InvalidRequest, authenticated.Failure);
            Assert.Equal(SamPiccFailure.InvalidRequest, changed.Failure);
            Assert.Empty(rig.Frames);
        }

        [Fact]
        public async Task A_sam_key_that_is_no_reference_is_refused_before_anything_is_sent_whichever_of_the_keys_it_is()
        {
            // default(SamKeyReference) is entry 0: the master entry of the SAM, which no key of a card is in.
            var rig = new PiccRig();
            var none = default(SamKeyReference);

            var authenticated = await rig.Session.AuthenticateAsync(0, 0, SamPiccKeyType.Aes128, none);
            var noAuthKey = await rig.Session.ChangeKeyAsync(new SamPiccKeyChange(0, 0, SamPiccKeyType.Aes128, none, 0, SamPiccKeyType.Aes128, Aes, Des, true));
            var noCurrentKey = await rig.Session.ChangeKeyAsync(new SamPiccKeyChange(0, 0, SamPiccKeyType.Aes128, Aes, 0, SamPiccKeyType.Aes128, none, Des, true));
            var noNewKey = await rig.Session.ChangeKeyAsync(new SamPiccKeyChange(0, 0, SamPiccKeyType.Aes128, Aes, 0, SamPiccKeyType.Aes128, Aes, none, true));

            Assert.All(new[] { authenticated, noAuthKey, noCurrentKey, noNewKey }, r =>
            {
                Assert.Equal(SamPiccFailure.InvalidRequest, r.Failure);
                Assert.Equal(ERROR.ProtocolConstraint, r.Error);
            });
            Assert.Empty(rig.Frames);
        }
        [Fact]
        public async Task The_largest_application_number_of_a_card_is_sent_as_it_is()
        {
            var rig = new PiccRig();

            await rig.Session.AuthenticateAsync(0xFFFFFF, 0, SamPiccKeyType.Aes128, Aes);

            Assert.Contains("CARD 905A000003FFFFFF00", rig.Frames);
        }

        [Theory]
        [InlineData(14)]
        [InlineData(255)]
        public async Task A_key_number_that_a_card_does_not_have_is_refused_before_anything_is_sent(int keyNo)
        {
            var rig = new PiccRig();

            var authenticated = await rig.Session.AuthenticateAsync(0, (byte)keyNo, SamPiccKeyType.Aes128, Aes);
            var authKey = await rig.Session.ChangeKeyAsync(new SamPiccKeyChange(1, (byte)keyNo, SamPiccKeyType.Aes128, Aes, 1, SamPiccKeyType.Aes128, Aes, Des, true));
            var target = await rig.Session.ChangeKeyAsync(new SamPiccKeyChange(1, 0, SamPiccKeyType.Aes128, Aes, (byte)keyNo, SamPiccKeyType.Aes128, Aes, Des, true));

            Assert.Equal(SamPiccFailure.InvalidRequest, authenticated.Failure);
            Assert.Equal(SamPiccFailure.InvalidRequest, authKey.Failure);
            Assert.Equal(SamPiccFailure.InvalidRequest, target.Failure);
            Assert.Empty(rig.Frames);
        }

        [Fact]
        public async Task An_answer_of_the_card_without_a_status_word_is_a_transport_failure_and_nothing_follows()
        {
            var rig = new PiccRig();
            rig.CardOverride = apdu => apdu[1] == 0x5A ? new byte[] { 0x91 } : null;

            var result = await rig.Session.AuthenticateAsync(0, 0, SamPiccKeyType.Aes128, Aes);

            Assert.Equal(ERROR.TransportError, result.Error);
            Assert.Equal(SamPiccFailure.ReaderFailure, result.Failure);
            Assert.False(result.KeyMayHaveChanged);
            Assert.Single(rig.CardFrames);
        }
        // ---- change key

        private static SamPiccKeyChange MasterChange(bool verify) =>
            new SamPiccKeyChange(0, 0, SamPiccKeyType.NativeTdea, Des, 0, SamPiccKeyType.Aes128, Des, Aes, verify);

        [Fact]
        public async Task The_card_master_key_is_changed_from_a_native_key_to_an_aes_key_with_the_frames_of_the_bench()
        {
            var rig = new PiccRig();

            var result = await rig.Session.ChangeKeyAsync(MasterChange(true));

            Assert.True(result.Succeeded);

            var expected = AuthenticateFrames(SamPiccKeyType.NativeTdea, Des, SelectCard, 0)
                .Concat(new[]
                {
                    // P1 01: the key that is changed is the one that authenticated. P2 10: the card master key. Current entry 2, new entry 1.
                    "SAM 80C40110040200010000",

                    // Key number byte 80: key 0, and the new key is AES. Lc is 1 + 24.
                    "CARD 90C4000019" + "80" + Repeat(0xD4, 24) + "00",
                })
                .Concat(AuthenticateFrames(SamPiccKeyType.Aes128, Aes, SelectCard, 0));

            Assert.Equal(expected, rig.Frames);
        }

        [Fact]
        public async Task A_key_change_without_a_check_ends_with_the_answer_of_the_card()
        {
            var rig = new PiccRig();

            var result = await rig.Session.ChangeKeyAsync(MasterChange(false));

            Assert.True(result.Succeeded);
            Assert.StartsWith("CARD 90C4000019", rig.Frames.Last(), StringComparison.Ordinal);
        }

        [Fact]
        public async Task An_application_key_other_than_the_one_that_authenticated_is_changed_with_the_case_of_a_different_key()
        {
            var rig = new PiccRig();
            var change = new SamPiccKeyChange(1, 0, SamPiccKeyType.Aes128, Aes, 3, SamPiccKeyType.Aes128, new SamKeyReference(7, 1), new SamKeyReference(8, 2), true);

            var result = await rig.Session.ChangeKeyAsync(change);

            Assert.True(result.Succeeded);

            // P1 00: another key. P2 03: key number 3, not the card master key. Data: current 7:1, new 8:2.
            Assert.Contains("SAM 80C40003040701080200", rig.Frames);

            // The key number byte is the number itself: an application key keeps its type.
            Assert.Contains("CARD 90C4000019" + "03" + Repeat(0xD4, 24) + "00", rig.Frames);

            // The check authenticates key 3 with the new key, as an AES key (the type of the application).
            Assert.Contains("CARD 90AA0000010300", rig.Frames);
            Assert.Contains("SAM 800A0000120802" + Repeat(0xB1, 16) + "00", rig.Frames);
        }

        [Fact]
        public async Task The_master_key_of_an_application_is_no_card_master_key()
        {
            var rig = new PiccRig();
            var change = new SamPiccKeyChange(1, 0, SamPiccKeyType.Aes128, Aes, 0, SamPiccKeyType.Aes128, Aes, new SamKeyReference(8, 2), false);

            await rig.Session.ChangeKeyAsync(change);

            // P1 01: the key that authenticated. P2 00: key 0 of an application, and not the card master key (that would be 10).
            Assert.Contains("SAM 80C401000401000802" + "00", rig.Frames);

            // And the key number byte is the number: a type in its top bits is for the card master key only.
            Assert.Contains("CARD 90C4000019" + "00" + Repeat(0xD4, 24) + "00", rig.Frames);
        }
        [Fact]
        public async Task The_card_master_key_changes_to_a_native_key_with_the_key_number_byte_00()
        {
            var rig = new PiccRig();
            var change = new SamPiccKeyChange(0, 0, SamPiccKeyType.Aes128, Aes, 0, SamPiccKeyType.NativeTdea, Aes, Des, false);

            await rig.Session.ChangeKeyAsync(change);

            Assert.Contains("CARD 90C4000019" + "00" + Repeat(0xD4, 24) + "00", rig.Frames);
        }

        [Fact]
        public async Task A_sam_that_will_not_build_the_cryptogram_is_a_key_that_is_unavailable_and_the_card_is_not_asked_to_change_anything()
        {
            var rig = new PiccRig();
            rig.SamOverride = c => c.Ins == 0xC4 ? new SamApduResponse(Array.Empty<byte>(), 0x6985) : null;

            var result = await rig.Session.ChangeKeyAsync(MasterChange(true));

            Assert.Equal(ERROR.KeyUnavailable, result.Error);
            Assert.Equal(SamPiccFailure.SamRefused, result.Failure);
            Assert.Equal((ushort)0x6985, result.SamStatus);
            Assert.False(result.KeyMayHaveChanged);
            Assert.DoesNotContain(rig.CardFrames, f => f.StartsWith("CARD 90C4", StringComparison.Ordinal));
        }

        [Fact]
        public async Task A_card_that_refuses_the_change_did_not_change_the_key_and_nothing_follows()
        {
            var rig = new PiccRig();
            rig.CardOverride = apdu => apdu[1] == 0xC4 ? PiccRig.WithStatus(Array.Empty<byte>(), 0x919E) : null;

            var result = await rig.Session.ChangeKeyAsync(MasterChange(true));

            Assert.Equal(ERROR.ProtocolConstraint, result.Error);
            Assert.Equal(SamPiccFailure.CardRefused, result.Failure);
            Assert.False(result.KeyMayHaveChanged);
            Assert.StartsWith("CARD 90C4", rig.Frames.Last(), StringComparison.Ordinal);
        }

        [Fact]
        public async Task A_card_that_does_not_answer_after_the_cryptogram_may_hold_the_new_key()
        {
            var rig = new PiccRig();
            rig.CardOverride = apdu => apdu[1] == 0xC4 ? throw new InvalidOperationException("card text that must not be logged") : (byte[]?)null;

            var result = await rig.Session.ChangeKeyAsync(MasterChange(true));

            Assert.Equal(ERROR.TransportError, result.Error);
            Assert.True(result.KeyMayHaveChanged);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task No_answer_or_one_byte_after_the_cryptogram_is_no_refusal_and_the_key_may_have_changed(int length)
        {
            var rig = new PiccRig();
            rig.CardOverride = apdu => apdu[1] == 0xC4 ? new byte[length] : null;

            var result = await rig.Session.ChangeKeyAsync(MasterChange(true));

            Assert.Equal(ERROR.TransportError, result.Error);
            Assert.Equal(SamPiccFailure.ReaderFailure, result.Failure);
            Assert.True(result.KeyMayHaveChanged);
        }
        [Fact]
        public async Task A_change_whose_check_fails_says_that_the_key_may_have_changed_and_could_not_be_verified()
        {
            var rig = new PiccRig();
            var changed = false;
            rig.CardOverride = apdu =>
            {
                if (apdu[1] == 0xC4)
                {
                    changed = true;
                }

                // After the change the card does not accept the SAM's answer.
                return changed && apdu[1] == 0xAF ? PiccRig.WithStatus(Array.Empty<byte>(), 0x91AE) : null;
            };

            var result = await rig.Session.ChangeKeyAsync(MasterChange(true));

            Assert.Equal(ERROR.Unknown, result.Error);
            Assert.Equal(SamPiccFailure.ChangeUnverified, result.Failure);
            Assert.True(result.KeyMayHaveChanged);
        }

        [Fact]
        public async Task A_failed_authentication_stops_the_change_before_the_sam_is_asked_for_a_cryptogram()
        {
            var rig = new PiccRig();
            rig.CardOverride = apdu => apdu[1] == 0xAF ? PiccRig.WithStatus(Array.Empty<byte>(), 0x91AE) : null;

            var result = await rig.Session.ChangeKeyAsync(MasterChange(true));

            Assert.Equal(ERROR.AuthFailure, result.Error);
            Assert.DoesNotContain(rig.SamFrames, f => f.StartsWith("SAM 80C4", StringComparison.Ordinal));
        }

        [Fact]
        public async Task No_reference_no_reader_text_and_no_exception_text_reaches_a_log()
        {
            using var logs = CapturedLogs.Install();
            var rig = new PiccRig();
            rig.CardOverride = apdu => apdu[1] == 0xC4 ? throw new InvalidOperationException("card text that must not be logged") : (byte[]?)null;

            await rig.Session.ChangeKeyAsync(new SamPiccKeyChange(0, 0, SamPiccKeyType.NativeTdea, new SamKeyReference(77, 66), 0, SamPiccKeyType.Aes128, new SamKeyReference(77, 66), new SamKeyReference(88, 99), true));

            var written = string.Join(" ", logs.All().Select(CapturedLogs.EverythingWrittenFor));

            Assert.NotEmpty(logs.All());
            Assert.DoesNotContain("card text that must not be logged", written, StringComparison.Ordinal);
            Assert.DoesNotContain("77:66", written, StringComparison.Ordinal);
            Assert.DoesNotContain("88:99", written, StringComparison.Ordinal);
            Assert.DoesNotContain("SamKeyReference", written, StringComparison.Ordinal);
        }
    }
}