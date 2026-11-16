#nullable enable
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.ReaderProviders;
using RFiDGear.Infrastructure.Ultralight;
using RFiDGear.Services.Sam;
using RFiDGear.Tests.Sam;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Xunit;

namespace RFiDGear.Tests.Reader
{
    /// <summary>
    /// The PC/SC reader as the relay between a MIFARE SAM AV3 and a DESFire card: the SAM is opened first and nothing is sent to the card until
    /// it is, one operation holds the session of the reader, the SAM and the card are let go of whatever happens, and the answers are those of
    /// the session. The SAM and the card are scripts that answer as they did on the bench; no native library is touched.
    /// </summary>
    public class LibLogicalAccessSamRelayTests
    {
        private const string SamReader = "HID Global OMNIKEY 5422 Smartcard Reader 0";
        private const string CardReader = "HID Global OMNIKEY 5422CL Smartcard Reader 0";

        private static readonly SamKeyReference Entry1 = Reference("1:0");
        private static readonly SamKeyReference Entry2 = Reference("2:0");

        private static SamKeyReference Reference(string text)
        {
            Assert.True(SamKeyReference.TryParse(text, out var reference));

            return reference;
        }

        private static DesfireSamAccess Access(DESFireKeyType type = DESFireKeyType.DF_KEY_AES, uint appId = 0, byte keyNo = 0) =>
            new DesfireSamAccess(appId, keyNo, type, Entry1);

        private static DesfireSamKeyChange Change() =>
            new DesfireSamKeyChange(0, 0, DESFireKeyType.DF_KEY_DES, Entry2, 0, DESFireKeyType.DF_KEY_AES, Entry2, Entry1);

        // ── what the reader says it can do ──

        [Fact]
        public void Only_the_pcsc_reader_can_work_with_keys_that_stay_in_a_sam()
        {
            using var elatec = new ElatecNetProvider();

            Assert.True(new RelayReader().SupportsDesfireSamKeys);
            Assert.False(elatec.SupportsDesfireSamKeys);
        }

        [Fact]
        public async Task A_reader_that_cannot_answers_protocol_constraint_and_touches_nothing()
        {
            using var reader = new ElatecNetProvider();

            var authenticated = await reader.AuthenticateMifareDesfireWithSamAsync(Access());
            var changed = await reader.ChangeMifareDesfireKeyWithSamAsync(Change());

            foreach (var result in new[] { authenticated, changed })
            {
                Assert.Equal(ERROR.ProtocolConstraint, result.Error);
                Assert.Equal(SamPiccFailure.NotSupported, result.Failure);
                Assert.False(result.KeyMayHaveChanged);
            }
        }

        [Fact]
        public async Task The_sam_is_opened_off_the_thread_of_the_caller_so_that_a_window_does_not_freeze()
        {
            var reader = new RelayReader();
            var context = new SynchronizationContext();
            var before = SynchronizationContext.Current;
            Task<SamPiccResult> call;

            // The gate is free, so the call runs on until its first real wait: whatever blocks before that blocks the caller (a window).
            SynchronizationContext.SetSynchronizationContext(context);

            try
            {
                call = reader.AuthenticateMifareDesfireWithSamAsync(Access());
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(before);
            }

            Assert.True((await call).Succeeded);
            Assert.True(reader.SamOpened);
            Assert.NotSame(context, reader.ContextWhenTheSamWasOpened);
        }

        // ── the order: the SAM first, the card second ──

        [Fact]
        public async Task The_sam_is_opened_before_the_card_is_touched_and_both_are_let_go_of_afterwards()
        {
            var reader = new RelayReader();

            var result = await reader.AuthenticateMifareDesfireWithSamAsync(Access());

            Assert.True(result.Succeeded);
            Assert.Equal(new[] { "sam open", "card open", "card release" }, reader.Events.Take(3));
            Assert.Equal(new[] { SamReader }, reader.Sam.Connected);
            Assert.True(reader.Sam.Handed.Single().Connection.Disposed);
            Assert.False(SamPortGate.IsHeld);
        }

        [Fact]
        public async Task The_reader_that_is_configured_for_the_sam_is_the_one_that_is_opened_and_the_reader_of_the_card_is_not_it()
        {
            var reader = new RelayReader { ReaderUnitName = CardReader };
            reader.AssumeSamReader(SamReader);

            var result = await reader.AuthenticateMifareDesfireWithSamAsync(Access());

            Assert.True(result.Succeeded);
            Assert.Equal(new[] { SamReader }, reader.Sam.Connected);
        }

        [Fact]
        public async Task No_reader_for_the_sam_is_a_key_that_cannot_be_had_and_the_card_is_never_touched()
        {
            var reader = new RelayReader();
            reader.AssumeSamReader(string.Empty);

            var result = await reader.AuthenticateMifareDesfireWithSamAsync(Access());

            Assert.Equal(ERROR.KeyUnavailable, result.Error);
            Assert.Equal(SamPiccFailure.NotConfigured, result.Failure);
            Assert.Empty(reader.Events);
            Assert.Empty(reader.Sam.Connected);
        }

        [Fact]
        public async Task The_reader_of_the_card_is_never_the_reader_of_the_sam()
        {
            var reader = new RelayReader { ReaderUnitName = SamReader };

            var result = await reader.AuthenticateMifareDesfireWithSamAsync(Access());

            Assert.Equal(ERROR.KeyUnavailable, result.Error);
            Assert.Equal(SamPiccFailure.SamIsCardReader, result.Failure);
            Assert.Empty(reader.Events);
            Assert.Empty(reader.Sam.Connected);
        }

        [Fact]
        public async Task A_reader_whose_card_is_no_sam_is_not_used_and_the_card_is_never_touched()
        {
            var reader = new RelayReader(samAtr: SamTestData.DesfireAtr);

            var result = await reader.AuthenticateMifareDesfireWithSamAsync(Access());

            Assert.Equal(ERROR.KeyUnavailable, result.Error);
            Assert.Equal(SamPiccFailure.SamNotFound, result.Failure);
            Assert.DoesNotContain("card open", reader.Events);
            Assert.False(SamPortGate.IsHeld);
        }

        [Fact]
        public async Task A_sam_that_is_in_use_is_busy_and_stays_the_other_ones()
        {
            var reader = new RelayReader();
            Assert.True(SamPortGate.TryEnter());

            try
            {
                var result = await reader.AuthenticateMifareDesfireWithSamAsync(Access());

                Assert.Equal(ERROR.KeyUnavailable, result.Error);
                Assert.Equal(SamPiccFailure.SamBusy, result.Failure);
                Assert.Empty(reader.Events);
                Assert.True(SamPortGate.IsHeld);
            }
            finally
            {
                SamPortGate.Exit();
            }
        }

        [Fact]
        public async Task A_key_that_is_no_des_or_aes_key_is_refused_before_anything_is_opened()
        {
            var reader = new RelayReader();

            var authenticated = await reader.AuthenticateMifareDesfireWithSamAsync(Access(DESFireKeyType.DF_KEY_3K3DES));
            var changed = await reader.ChangeMifareDesfireKeyWithSamAsync(Change() with { NewKeyType = DESFireKeyType.DF_KEY_3K3DES });

            foreach (var result in new[] { authenticated, changed })
            {
                Assert.Equal(ERROR.ProtocolConstraint, result.Error);
                Assert.Equal(SamPiccFailure.InvalidRequest, result.Failure);
            }

            Assert.Empty(reader.Events);
            Assert.Empty(reader.Sam.Connected);
        }

        // ── a request that cannot be made is found before anything is opened ──

        public static IEnumerable<object[]> RequestsThatCannotBeMade()
        {
            var good = Entry1;

            yield return new object[] { "an application of more than 24 bits", new DesfireSamAccess(0x1000000, 0, DESFireKeyType.DF_KEY_AES, good), null! };
            yield return new object[] { "a key number of the card that does not exist", new DesfireSamAccess(1, 14, DESFireKeyType.DF_KEY_AES, good), null! };
            yield return new object[] { "the master entry of the SAM", new DesfireSamAccess(0, 0, DESFireKeyType.DF_KEY_AES, default), null! };
            yield return new object[] { "a new key that is the master entry", null!, Change() with { NewKey = default } };
            yield return new object[] { "a key that authenticates that does not exist", null!, Change() with { AuthKeyNumber = 14 } };
            yield return new object[] { "a key to replace that does not exist", null!, Change() with { TargetKeyNumber = 200 } };
            yield return new object[] { "an application of more than 24 bits in a change", null!, Change() with { AppId = uint.MaxValue } };
            yield return new object[] { "an application key of another type than the key that authenticates", null!, Change() with { AppId = 1 } };
        }

        [Theory]
        [MemberData(nameof(RequestsThatCannotBeMade))]
        public async Task A_request_that_cannot_be_made_is_refused_before_the_sam_or_the_card_is_opened(string why, DesfireSamAccess? access, DesfireSamKeyChange? change)
        {
            Assert.NotEmpty(why);

            var reader = new RelayReader();

            var result = access.HasValue
                ? await reader.AuthenticateMifareDesfireWithSamAsync(access.Value)
                : await reader.ChangeMifareDesfireKeyWithSamAsync(change!.Value);

            Assert.Equal(ERROR.ProtocolConstraint, result.Error);
            Assert.Equal(SamPiccFailure.InvalidRequest, result.Failure);
            Assert.Empty(reader.Events);
            Assert.Empty(reader.Sam.Connected);
        }

        [Fact]
        public async Task The_card_master_key_may_change_its_type_and_an_application_key_keeps_the_type_of_the_application()
        {
            var master = new RelayReader();
            var sameType = new RelayReader();

            // The card master key: DES to AES is the change of the type of the card.
            var changed = await master.ChangeMifareDesfireKeyWithSamAsync(Change());
            // An application key of the same type as the key that authenticates.
            var kept = await sameType.ChangeMifareDesfireKeyWithSamAsync(Change() with { AppId = 1, AuthKeyType = DESFireKeyType.DF_KEY_AES, NewKeyType = DESFireKeyType.DF_KEY_AES });

            Assert.True(changed.Succeeded);
            Assert.True(kept.Succeeded);
        }

        // ── no card, or the wrong one ──

        [Theory]
        [InlineData(SamPiccFailure.NoCard, ERROR.TransportError)]
        [InlineData(SamPiccFailure.NotDesfire, ERROR.ProtocolConstraint)]
        [InlineData(SamPiccFailure.ReaderFailure, ERROR.TransportError)]
        public async Task A_card_that_cannot_be_used_ends_the_call_with_the_sam_opened_and_nothing_sent_to_it(SamPiccFailure why, ERROR expected)
        {
            var reader = new RelayReader { CardLinkFails = why };

            var result = await reader.AuthenticateMifareDesfireWithSamAsync(Access());

            Assert.Equal(expected, result.Error);
            Assert.Equal(why, result.Failure);

            // The SAM was opened first, and is let go of; not a single command was sent to it (the session never started). The card is let go
            // of too: it may be connected, and a DESFire card that cannot be used must not stay connected.
            Assert.Equal(new[] { "sam open", "card open", "card release" }, reader.Events);
            Assert.Empty(reader.Sam.Handed.Single().Transport.Sent);
            Assert.True(reader.Sam.Handed.Single().Connection.Disposed);
            Assert.False(SamPortGate.IsHeld);
        }

        [Theory]
        [InlineData("DESFire", true)]
        [InlineData("DESFireEV1", true)]
        [InlineData("DESFireEV2", true)]
        [InlineData("DESFireEV3", true)]
        [InlineData("MifareUltralight", false)]
        [InlineData("MifareClassic1K", false)]
        [InlineData("SAM_AV3", false)]
        [InlineData("desfire", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void Only_a_card_that_the_library_calls_desfire_gets_the_commands_of_a_desfire_card(string? cardType, bool desfire)
        {
            Assert.Equal(desfire, DesfireSamRequests.IsDesfireCardType(cardType));
        }

        [Fact]
        public async Task A_sam_whose_reader_fails_to_open_is_a_transport_error_and_the_card_is_never_touched()
        {
            using var logs = CapturedLogs.Install();
            var reader = new RelayReader();
            reader.Sam.ConnectFails[SamReader] = new InvalidOperationException("PRIVATE-READER-TEXT");

            var result = await reader.AuthenticateMifareDesfireWithSamAsync(Access());

            Assert.Equal(ERROR.TransportError, result.Error);
            Assert.Equal(SamPiccFailure.ReaderFailure, result.Failure);
            Assert.Empty(reader.Events);
            Assert.False(SamPortGate.IsHeld);
            Assert.All(logs.All().Select(CapturedLogs.EverythingWrittenFor), text => Assert.DoesNotContain("PRIVATE-READER-TEXT", text, StringComparison.Ordinal));
        }

        [Fact]
        public async Task A_reader_that_throws_ends_the_call_without_its_text_and_lets_everything_go()
        {
            using var logs = CapturedLogs.Install();
            var reader = new RelayReader { CardLinkThrows = new InvalidOperationException("PRIVATE-READER-TEXT") };

            var result = await reader.AuthenticateMifareDesfireWithSamAsync(Access());

            Assert.Equal(ERROR.TransportError, result.Error);
            Assert.Equal(SamPiccFailure.ReaderFailure, result.Failure);
            Assert.True(reader.Sam.Handed.Single().Connection.Disposed);
            Assert.Contains("card release", reader.Events);
            Assert.False(SamPortGate.IsHeld);
            Assert.All(logs.All().Select(CapturedLogs.EverythingWrittenFor), text => Assert.DoesNotContain("PRIVATE-READER-TEXT", text, StringComparison.Ordinal));

            // The gate of the session was given back too: the next call is not stuck.
            reader.CardLinkThrows = null;
            Assert.True((await reader.AuthenticateMifareDesfireWithSamAsync(Access())).Succeeded);
        }

        // ── what the session answers is what the task gets ──

        [Fact]
        public async Task The_answer_of_the_session_is_passed_on_and_the_card_is_let_go_of_after_a_refusal()
        {
            var reader = new RelayReader();
            reader.CardOverride = apdu => apdu[1] == 0xAF ? PiccRig.WithStatus(Array.Empty<byte>(), 0x91AE) : null;

            var result = await reader.AuthenticateMifareDesfireWithSamAsync(Access());

            Assert.Equal(ERROR.AuthFailure, result.Error);
            Assert.Equal(SamPiccFailure.CardRefused, result.Failure);
            Assert.Equal((ushort)0x91AE, result.CardStatus);
            Assert.Contains("card release", reader.Events);
            Assert.False(SamPortGate.IsHeld);
        }

        [Fact]
        public async Task A_key_change_is_checked_by_authenticating_with_the_new_key()
        {
            var reader = new RelayReader();

            var result = await reader.ChangeMifareDesfireKeyWithSamAsync(Change());

            Assert.True(result.Succeeded);

            // The change authenticates with the DES key (native 0A), changes the key, and authenticates with the new AES key (AA).
            var commands = reader.Card.Sent.Select(frame => frame[1]).ToArray();
            Assert.Equal(new byte[] { 0x5A, 0x0A, 0xAF, 0xC4, 0x5A, 0xAA, 0xAF }, commands);
        }

        [Fact]
        public async Task The_type_of_the_key_decides_the_authentication_command()
        {
            var aes = new RelayReader();
            var des = new RelayReader();

            await aes.AuthenticateMifareDesfireWithSamAsync(Access(DESFireKeyType.DF_KEY_AES, appId: 0x010203, keyNo: 3));
            await des.AuthenticateMifareDesfireWithSamAsync(Access(DESFireKeyType.DF_KEY_DES));

            Assert.Equal(0xAA, aes.Card.Sent[1][1]);
            Assert.Equal(0x0A, des.Card.Sent[1][1]);

            // The application of the select (three bytes, least significant first) and the number of the key.
            Assert.Equal("905A00000303020100", Convert.ToHexString(aes.Card.Sent[0]).Substring(0, 18));
            Assert.Equal(3, aes.Card.Sent[1][5]);
        }

        // ── one user of the reader session at a time ──

        [Fact]
        public async Task A_relay_holds_the_session_of_the_reader_and_a_page_operation_waits_for_it()
        {
            var reader = new RelayReader { HoldCardLink = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously) };

            var relay = reader.AuthenticateMifareDesfireWithSamAsync(Access());
            await WaitUntilAsync(() => reader.Events.Contains("card open"));

            var read = reader.ReadMifareUltralightPagesAsync(UltralightAccess.Open(), 4, 1);
            await Task.Delay(200);

            // The page operation has not selected a chip: it waits for the relay, as ReadChipPublic does.
            Assert.DoesNotContain("select", reader.Events);
            Assert.False(read.IsCompleted);

            reader.HoldCardLink.SetResult(true);
            var relayResult = await relay;
            var readResult = await read;

            Assert.True(relayResult.Succeeded);
            Assert.True(readResult.Succeeded);
            Assert.True(reader.Events.IndexOf("select") > reader.Events.IndexOf("card release"));
        }

        [Fact]
        public async Task Two_relays_are_never_in_the_reader_at_the_same_time()
        {
            var reader = new RelayReader { HoldCardLink = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously) };

            var first = reader.AuthenticateMifareDesfireWithSamAsync(Access());
            await WaitUntilAsync(() => reader.Events.Contains("card open"));

            var second = reader.AuthenticateMifareDesfireWithSamAsync(Access());
            await Task.Delay(200);

            Assert.Equal(1, reader.Events.Count(e => e == "sam open"));
            Assert.False(second.IsCompleted);

            reader.HoldCardLink.SetResult(true);
            var both = await Task.WhenAll(first, second);

            Assert.All(both, result => Assert.True(result.Succeeded));
            Assert.Equal(2, reader.Events.Count(e => e == "sam open"));
        }

        private static async Task WaitUntilAsync(Func<bool> condition)
        {
            var until = DateTime.UtcNow.AddSeconds(10);

            while (!condition() && DateTime.UtcNow < until)
            {
                await Task.Delay(10);
            }

            Assert.True(condition(), "The first call never reached the card.");
        }

        /// <summary>
        /// A PC/SC reader with a SAM in one slot and a DESFire card in the other, in memory: the SAM answers on the level of the bytes of the
        /// APDU as it did on the bench, the card is a scripted transport, and every step that touches either is recorded in order.
        /// </summary>
        private sealed class RelayReader : LibLogicalAccessProvider
        {
            private int _length = 16;

            internal RelayReader(byte[]? samAtr = null)
            {
                Sam = new FakeSamReaderSource().With(SamReader, samAtr ?? SamTestData.SamAtr).With(CardReader, SamTestData.DesfireAtr);
                Sam.Answer = OnSam;
                Card = new ScriptedSamTransport(OnCard);
                AssumeSamReader(SamReader);
            }

            internal FakeSamReaderSource Sam { get; }

            internal ScriptedSamTransport Card { get; }

            internal List<string> Events { get; } = new List<string>();

            internal Func<byte[], byte[]?>? CardOverride { get; set; }

            /// <summary>Gets the synchronization context of the thread that opened the SAM: the one of the caller, or none.</summary>
            internal SynchronizationContext? ContextWhenTheSamWasOpened { get; private set; }

            internal bool SamOpened { get; private set; }

            internal SamPiccFailure? CardLinkFails { get; set; }

            internal Exception? CardLinkThrows { get; set; }

            internal TaskCompletionSource<bool>? HoldCardLink { get; set; }

            internal override ISamPiccConnector CreateSamPiccConnector() => new RecordingConnector(this);

            internal override async Task<SamCardLink> OpenCardLinkForSamAsync()
            {
                Events.Add("card open");

                if (CardLinkThrows != null)
                {
                    throw CardLinkThrows;
                }

                if (HoldCardLink != null)
                {
                    await HoldCardLink.Task;
                }

                return CardLinkFails.HasValue ? new SamCardLink(CardLinkFails.Value, null) : new SamCardLink(SamPiccFailure.None, Card);
            }

            internal override Task ReleaseCardLinkForSamAsync()
            {
                Events.Add("card release");

                return Task.CompletedTask;
            }

            protected override Task<PcscUltralightSighting> SelectPcscUltralightChipAsync()
            {
                Events.Add("select");

                return Task.FromResult(new PcscUltralightSighting(true, "MifareUltralight"));
            }

            protected override Task<byte[]> ReadPcscUltralightPageAsync(int page) => Task.FromResult(new byte[] { (byte)page, 2, 3, 4 });

            // The SAM, by the bytes of the APDU: a kill, part 1 (it has an Le), part 2 (it has none), the cryptogram of a key change.
            private byte[] OnSam(byte[] apdu)
            {
                switch (apdu[1])
                {
                    case 0xCA:
                        return new byte[] { 0x90, 0x00 };

                    case 0x0A:
                        var lc = apdu[4];

                        return apdu.Length == 5 + lc + 1
                            ? PiccRig.WithStatus(Enumerable.Repeat((byte)0xC3, 2 * (lc - 2)).ToArray(), 0x90AF)
                            : new byte[] { 0x90, 0x00 };

                    case 0xC4:
                        return PiccRig.WithStatus(Enumerable.Repeat((byte)0xD4, 24).ToArray(), 0x9000);

                    default:
                        return new byte[] { 0x6D, 0x00 };
                }
            }

            // The card, by its command byte: select, the first part of an authentication (0A native, AA AES), the second part, the change.
            private byte[] OnCard(byte[] apdu)
            {
                var overridden = CardOverride?.Invoke(apdu);

                if (overridden != null)
                {
                    return overridden;
                }

                switch (apdu[1])
                {
                    case 0x0A:
                    case 0xAA:
                        _length = apdu[1] == 0xAA ? 16 : 8;

                        return PiccRig.WithStatus(Enumerable.Repeat((byte)0xB1, _length).ToArray(), 0x91AF);

                    case 0xAF:
                        return PiccRig.WithStatus(Enumerable.Repeat((byte)0xA2, _length).ToArray(), 0x9100);

                    default:
                        return PiccRig.WithStatus(Array.Empty<byte>(), 0x9100);
                }
            }

            // The real connector over the scripted readers, with the opening of the SAM recorded.
            private sealed class RecordingConnector : ISamPiccConnector
            {
                private readonly RelayReader _reader;

                internal RecordingConnector(RelayReader reader)
                {
                    _reader = reader;
                }

                public SamPiccOpen Open(string? samReaderName, string? cardReaderName)
                {
                    _reader.ContextWhenTheSamWasOpened = SynchronizationContext.Current;
                    _reader.SamOpened = true;

                    var open = new SamPiccConnector(new SamReaderFinder(_reader.Sam)).Open(samReaderName, cardReaderName);

                    if (open.Connection != null)
                    {
                        _reader.Events.Add("sam open");
                    }

                    return open;
                }
            }
        }
    }
}
