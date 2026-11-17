#nullable enable
using RFiDGear.Infrastructure;
using RFiDGear.Services.Sam;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Xunit;

namespace RFiDGear.Tests.Sam
{
    /// <summary>
    /// The card side of a relay on a TWN4, and the whole relay through the TWN4 parts: the SAM is found in its socket first, the card is searched
    /// once and only afterwards, and the answers are those of the session that the PC/SC reader uses too. The reader is a script, the SAM and the
    /// card answer as they did on the bench.
    /// </summary>
    [Collection("SamPortGate")]
    public class Twn4RelayTests
    {
        private static readonly SamKeyReference Entry2 = new SamKeyReference(2, 0);

        // ── what the card is ──

        [Theory]
        [InlineData("0401013300 1A0591AF", true)]
        [InlineData("04010133001A059100", true)]
        [InlineData("04020133001A0591AF", false)]
        [InlineData("05010133001A0591AF", false)]
        [InlineData("04010133001A05", false)]
        [InlineData("0401", false)]
        [InlineData("040191AF", false)]
        [InlineData("040101330091AF", false)]
        [InlineData("04010133001A0691AF", true)]
        public void The_first_frame_of_get_version_tells_a_desfire_card(string hex, bool desfire)
        {
            Assert.Equal(desfire, Twn4CardSide.IsDesfireVersion(Convert.FromHexString(hex.Replace(" ", string.Empty))));
        }

        [Fact]
        public void No_answer_is_no_desfire_card()
        {
            Assert.False(Twn4CardSide.IsDesfireVersion(null));
        }

        [Theory]
        [InlineData(0x20, true)]
        [InlineData(0x28, true)]
        [InlineData(0x00, false)]
        [InlineData(0x08, false)]
        [InlineData(0x18, false)]
        public void The_sak_tells_a_card_that_talks_iso_14443_4(byte sak, bool talks)
        {
            Assert.Equal(talks, Twn4CardSide.TalksIso14443Part4(sak));
        }

        [Fact]
        public void No_sak_is_a_fault_of_the_reader_and_not_a_card_that_is_no_desfire_card()
        {
            // The reader answers nothing when the command fails: a card that was just found would be reported as an unsupported one.
            Assert.Throws<SamReaderException>(() => Twn4CardSide.TalksIso14443Part4(null));
        }

        [Fact]
        public void No_answer_to_get_version_is_a_fault_of_the_reader_and_not_a_card_that_is_no_desfire_card()
        {
            // A card that stops answering after the search would otherwise be reported as an unsupported card.
            Assert.Throws<SamReaderException>(() => Twn4CardSide.SightingOfVersion(null));
        }

        [Theory]
        [InlineData("04010133001A0591AF", "Desfire")]
        [InlineData("04020133001A0591AF", "NotDesfire")]
        [InlineData("6D00", "NotDesfire")]
        public void The_answer_to_get_version_says_what_the_card_is(string hex, string sighting)
        {
            Assert.Equal(Enum.Parse<Twn4CardSighting>(sighting), Twn4CardSide.SightingOfVersion(Convert.FromHexString(hex)));
        }

        [Fact]
        public async Task A_reader_that_fails_in_the_search_ends_the_relay_as_a_transport_error_and_powers_the_sam_down()
        {
            var rig = new PiccRig();
            var ports = Ports(rig);
            ports.SearchThrows = new SamReaderException("The reader gave no answer from the card.");

            var result = await SamRelay.RunAsync(
                Twn4SamRelay.Parts(ports, new SemaphoreSlim(1, 1)),
                "authenticate",
                session => session.AuthenticateAsync(0, 0, SamPiccKeyType.NativeTdea, Entry2));

            Assert.Equal(ERROR.TransportError, result.Error);
            Assert.Equal(SamPiccFailure.ReaderFailure, result.Failure);
            Assert.Empty(rig.Frames);
            Assert.Equal("power off Sam1", ports.Events.Last());
            Assert.False(SamPortGate.IsHeld);
        }

        [Theory]
        [InlineData("Desfire", SamPiccFailure.None, true)]
        [InlineData("NotDesfire", SamPiccFailure.NotDesfire, false)]
        [InlineData("None", SamPiccFailure.NoCard, false)]
        public async Task The_card_link_is_a_transport_for_a_desfire_card_and_the_reason_for_anything_else(string sighting, SamPiccFailure failure, bool transport)
        {
            var ports = new FakeTwn4Ports { Sighting = Enum.Parse<Twn4CardSighting>(sighting) };

            var link = await Twn4CardSide.OpenAsync(ports);

            Assert.Equal(failure, link.Failure);
            Assert.Equal(transport, link.Transport != null);
            Assert.Equal(new[] { "search card" }, ports.Events);
        }

        // ── the way to the card ──

        [Fact]
        public async Task The_frames_go_to_the_card_as_they_are_and_the_answer_comes_back_with_its_status_word()
        {
            byte[]? sent = null;
            var ports = new FakeTwn4Ports
            {
                Card = apdu =>
                {
                    sent = apdu;

                    return new byte[] { 0xAA, 0xBB, 0x91, 0xAF };
                },
            };
            var link = await Twn4CardSide.OpenAsync(ports);

            var answer = link.Transport!.Exchange(new byte[] { 0x90, 0x5A, 0x00, 0x00, 0x03, 0x00, 0x00, 0x00, 0x00 });

            Assert.Equal(Convert.FromHexString("905A00000300000000"), sent);
            Assert.Equal(Convert.FromHexString("AABB91AF"), answer);
        }

        [Fact]
        public async Task A_reader_that_gives_no_answer_is_a_reader_fault_with_a_fixed_text()
        {
            var ports = new FakeTwn4Ports { Card = _ => null };
            var link = await Twn4CardSide.OpenAsync(ports);

            var fault = Assert.Throws<SamReaderException>(() => link.Transport!.Exchange(new byte[] { 0x90, 0x60, 0, 0, 0 }));

            Assert.DoesNotContain("PRIVATE", fault.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task A_reader_that_throws_is_a_reader_fault_without_its_text()
        {
            var ports = new FakeTwn4Ports { Card = _ => throw new InvalidOperationException("PRIVATE-READER-TEXT") };
            var link = await Twn4CardSide.OpenAsync(ports);

            var fault = Assert.Throws<SamReaderException>(() => link.Transport!.Exchange(new byte[] { 0x90, 0x60, 0, 0, 0 }));

            Assert.DoesNotContain("PRIVATE-READER-TEXT", fault.Message, StringComparison.Ordinal);
            Assert.Null(fault.InnerException);
        }

        // ── the whole relay through the parts of a TWN4 ──

        [Fact]
        public async Task An_authentication_finds_the_sam_first_searches_the_card_once_and_powers_the_sam_down_at_the_end()
        {
            var rig = new PiccRig();
            var ports = Ports(rig);
            var gate = new SemaphoreSlim(1, 1);

            var result = await SamRelay.RunAsync(
                Twn4SamRelay.Parts(ports, gate),
                "authenticate",
                session => session.AuthenticateAsync(0, 0, SamPiccKeyType.NativeTdea, Entry2));

            Assert.True(result.Succeeded);
            Assert.Equal(
                new[] { "has Sam1", "power on Sam1", "has Sam2", "set t1 Sam1", "channel Sam1", "search card" },
                ports.Events.Take(6));
            Assert.Equal("power off Sam1", ports.Events.Last());
            Assert.Equal(1, ports.Events.Count(e => e == "search card"));

            // The frames are the ones of the PC/SC relay: kill, select, card part 1, SAM part 1, card part 2, SAM part 2.
            Assert.Equal(6, rig.Frames.Count);
            Assert.StartsWith("SAM 80CA0100", rig.Frames[0], StringComparison.Ordinal);
            Assert.StartsWith("CARD 905A", rig.Frames[1], StringComparison.Ordinal);
            Assert.StartsWith("CARD 900A", rig.Frames[2], StringComparison.Ordinal);
            Assert.StartsWith("SAM 800A", rig.Frames[3], StringComparison.Ordinal);
            Assert.StartsWith("CARD 90AF", rig.Frames[4], StringComparison.Ordinal);
            Assert.StartsWith("SAM 800A", rig.Frames[5], StringComparison.Ordinal);
            Assert.Equal(1, gate.CurrentCount);
            Assert.False(SamPortGate.IsHeld);
        }

        [Fact]
        public async Task Without_a_sam_the_card_is_never_searched_and_the_task_is_told_the_key_is_unavailable()
        {
            var rig = new PiccRig();
            var ports = Ports(rig);
            ports.Cards.Clear();
            var gate = new SemaphoreSlim(1, 1);

            var result = await SamRelay.RunAsync(
                Twn4SamRelay.Parts(ports, gate),
                "authenticate",
                session => session.AuthenticateAsync(0, 0, SamPiccKeyType.NativeTdea, Entry2));

            Assert.Equal(ERROR.KeyUnavailable, result.Error);
            Assert.Equal(SamPiccFailure.SamNotFound, result.Failure);
            Assert.DoesNotContain("search card", ports.Events);
            Assert.Empty(rig.Frames);
            Assert.Equal(1, gate.CurrentCount);
        }

        [Fact]
        public async Task A_card_that_is_no_desfire_card_gets_nothing_and_the_sam_is_powered_down()
        {
            var rig = new PiccRig();
            var ports = Ports(rig);
            ports.Sighting = Twn4CardSighting.NotDesfire;

            var result = await SamRelay.RunAsync(
                Twn4SamRelay.Parts(ports, new SemaphoreSlim(1, 1)),
                "authenticate",
                session => session.AuthenticateAsync(0, 0, SamPiccKeyType.NativeTdea, Entry2));

            Assert.Equal(ERROR.ProtocolConstraint, result.Error);
            Assert.Equal(SamPiccFailure.NotDesfire, result.Failure);
            Assert.Empty(rig.Frames);
            Assert.DoesNotContain("card exchange", ports.Events);
            Assert.Equal("power off Sam1", ports.Events.Last());
            Assert.False(SamPortGate.IsHeld);
        }

        [Fact]
        public async Task A_reader_that_gives_no_answer_from_the_card_ends_the_relay_after_one_try_and_powers_the_sam_down()
        {
            var rig = new PiccRig();
            var ports = Ports(rig);
            ports.Card = _ => null;

            var result = await SamRelay.RunAsync(
                Twn4SamRelay.Parts(ports, new SemaphoreSlim(1, 1)),
                "authenticate",
                session => session.AuthenticateAsync(0, 0, SamPiccKeyType.NativeTdea, Entry2));

            Assert.Equal(ERROR.TransportError, result.Error);
            Assert.Equal(SamPiccFailure.ReaderFailure, result.Failure);

            // The kill that every attempt starts with, then the select that the reader did not answer: nothing is tried again.
            Assert.Equal(1, ports.Events.Count(e => e == "card exchange"));
            Assert.Equal("power off Sam1", ports.Events.Last());
            Assert.False(SamPortGate.IsHeld);
        }

        // A TWN4 with the SAM in the first socket whose SAM and card are the script of the bench.
        private static FakeTwn4Ports Ports(PiccRig rig)
        {
            var ports = new FakeTwn4Ports { Channel = rig.Sam };
            ports.Cards[Twn4SamSlot.Sam1] = SamTestData.SamAtr;
            ports.Card = apdu => rig.Card.Exchange(apdu);

            return ports;
        }
    }
}
