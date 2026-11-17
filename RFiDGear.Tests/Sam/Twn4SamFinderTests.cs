#nullable enable
using RFiDGear.Services.Sam;

using System;
using System.Linq;

using Xunit;

namespace RFiDGear.Tests.Sam
{
    /// <summary>
    /// The SAM sockets of a TWN4 in the list of the SAM setup: a SAM is listed by the ATR of the card in a socket and nothing else is sent to
    /// it, a card that is no SAM is let go of again, a SAM is opened for the whole window and powered down when the window lets go of it, and
    /// nothing here takes the gate of the SAM (the window holds it already). The reader is a script.
    /// </summary>
    [Collection("SamPortGate")]
    public class Twn4SamFinderTests
    {
        private static FakeTwn4Ports Ports(byte[]? sam1 = null, byte[]? sam2 = null)
        {
            var ports = new FakeTwn4Ports();

            if (sam1 != null)
            {
                ports.Cards[Twn4SamSlot.Sam1] = sam1;
            }

            if (sam2 != null)
            {
                ports.Cards[Twn4SamSlot.Sam2] = sam2;
            }

            return ports;
        }

        // ── looking for SAMs ──

        [Fact]
        public void A_sam_in_the_first_socket_is_listed_with_its_atr_and_the_socket_is_powered_down_again()
        {
            var ports = Ports(sam1: SamTestData.SamAtr);

            var result = new Twn4SamFinder(ports).Find();

            Assert.False(result.EnumerationFailed);
            var reader = Assert.Single(result.Readers);
            Assert.Equal("TWN4 SAM socket 1", reader.Name);
            Assert.Equal(SamTestData.SamAtr, reader.Atr);
            Assert.Equal(new[] { "has Sam1", "power on Sam1", "power off Sam1", "has Sam2" }, ports.Events);
        }

        [Fact]
        public void A_sam_in_each_socket_gives_two_entries()
        {
            var result = new Twn4SamFinder(Ports(SamTestData.SamAtr, SamTestData.SamAtr)).Find();

            Assert.Equal(new[] { "TWN4 SAM socket 1", "TWN4 SAM socket 2" }, result.Readers.Select(r => r.Name));
        }

        [Fact]
        public void A_card_that_is_no_sam_is_not_listed_and_is_powered_down_again_and_nothing_is_set_up()
        {
            var ports = Ports(sam1: FakeTwn4Ports.GsmAtr, sam2: SamTestData.SamAtr);

            var result = new Twn4SamFinder(ports).Find();

            Assert.Equal(new[] { "TWN4 SAM socket 2" }, result.Readers.Select(r => r.Name));
            Assert.Contains("power off Sam1", ports.Events);
            Assert.DoesNotContain(ports.Events, e => e.StartsWith("set t1", StringComparison.Ordinal));
        }

        [Fact]
        public void Empty_sockets_and_cards_that_do_not_answer_give_an_empty_list_that_is_no_failure()
        {
            var silent = new FakeTwn4Ports();
            silent.Cards[Twn4SamSlot.Sam1] = null;

            var empty = new Twn4SamFinder(new FakeTwn4Ports()).Find();
            var mute = new Twn4SamFinder(silent).Find();

            Assert.Empty(empty.Readers);
            Assert.False(empty.EnumerationFailed);
            Assert.Empty(mute.Readers);
            Assert.False(mute.EnumerationFailed);
        }

        [Fact]
        public void A_socket_that_cannot_be_looked_at_is_skipped_and_the_other_is_still_looked_at_without_its_text_in_the_log()
        {
            using var logs = CapturedLogs.Install();
            var ports = Ports(sam2: SamTestData.SamAtr);
            ports.HasCardThrows[Twn4SamSlot.Sam1] = new InvalidOperationException("PRIVATE-READER-TEXT");

            var result = new Twn4SamFinder(ports).Find();

            Assert.Equal(new[] { "TWN4 SAM socket 2" }, result.Readers.Select(r => r.Name));
            Assert.All(logs.All().Select(CapturedLogs.EverythingWrittenFor), text => Assert.DoesNotContain("PRIVATE-READER-TEXT", text, StringComparison.Ordinal));
        }

        // ── opening a SAM ──

        [Fact]
        public void Opening_a_listed_sam_sets_the_socket_up_for_t1_and_gives_the_channel_of_the_reader()
        {
            var ports = Ports(sam2: SamTestData.SamAtr);

            using var connection = new Twn4SamFinder(ports).Open("TWN4 SAM socket 2");

            Assert.NotNull(connection);
            Assert.Same(ports.Channel, connection!.Channel);
            Assert.Equal("TWN4 SAM socket 2", connection.Reader.Name);
            Assert.Equal(new Twn4SamSettings(0xFF, 0x43, 0xFE), ports.LastSettings);
            Assert.Equal(new[] { "has Sam2", "power on Sam2", "set t1 Sam2", "channel Sam2" }, ports.Events);
        }

        [Fact]
        public void Letting_go_of_the_connection_powers_the_socket_down_once_and_a_failure_of_that_is_swallowed()
        {
            var ports = Ports(sam1: SamTestData.SamAtr);
            var connection = new Twn4SamFinder(ports).Open("TWN4 SAM socket 1")!;
            ports.PowerOffThrows = new InvalidOperationException("PRIVATE-READER-TEXT");

            connection.Dispose();
            connection.Dispose();

            Assert.Equal(1, ports.Events.Count(e => e == "power off Sam1"));
        }

        [Theory]
        [InlineData("TWN4 SAM socket 3")]
        [InlineData("OMNIKEY 5422 Smartcard Reader 0")]
        [InlineData("")]
        public void A_name_that_is_no_socket_of_the_twn4_gives_nothing_and_touches_nothing(string name)
        {
            var ports = Ports(sam1: SamTestData.SamAtr);

            Assert.Null(new Twn4SamFinder(ports).Open(name));
            Assert.Empty(ports.Events);
        }

        [Fact]
        public void An_empty_socket_gives_nothing()
        {
            var ports = Ports(sam2: SamTestData.SamAtr);

            Assert.Null(new Twn4SamFinder(ports).Open("TWN4 SAM socket 1"));
            Assert.Equal(new[] { "has Sam1" }, ports.Events);
        }

        [Fact]
        public void A_card_that_is_no_sam_is_powered_down_again_and_never_set_up()
        {
            var ports = Ports(sam1: FakeTwn4Ports.GsmAtr);

            Assert.Null(new Twn4SamFinder(ports).Open("TWN4 SAM socket 1"));
            Assert.Equal(new[] { "has Sam1", "power on Sam1", "power off Sam1" }, ports.Events);
        }

        [Fact]
        public void A_card_that_offers_t1_but_is_no_sam_never_gets_the_settings_of_t1()
        {
            // TS, T0 with TD1 only, TD1 = T=1, check byte: a valid ATR that does not say MIFARE SAM AV3.
            var ports = Ports(sam1: new byte[] { 0x3B, 0x80, 0x01, 0x81 });

            Assert.Null(new Twn4SamFinder(ports).Open("TWN4 SAM socket 1"));
            Assert.DoesNotContain(ports.Events, e => e.StartsWith("set t1", StringComparison.Ordinal));
            Assert.Equal("power off Sam1", ports.Events.Last());
        }

        [Fact]
        public void A_sam_whose_atr_offers_no_t1_is_powered_down_again_and_gives_nothing()
        {
            var ports = Ports(sam1: FakeTwn4Ports.SamAtrWithoutT1);

            Assert.Null(new Twn4SamFinder(ports).Open("TWN4 SAM socket 1"));
            Assert.DoesNotContain(ports.Events, e => e.StartsWith("set t1", StringComparison.Ordinal));
            Assert.Equal("power off Sam1", ports.Events.Last());
        }

        [Fact]
        public void A_fault_of_the_reader_while_opening_powers_the_socket_down_and_reaches_the_caller()
        {
            var ports = Ports(sam1: SamTestData.SamAtr);
            ports.SetT1Throws = new InvalidOperationException("PRIVATE-READER-TEXT");

            Assert.Throws<InvalidOperationException>(() => new Twn4SamFinder(ports).Open("TWN4 SAM socket 1"));
            Assert.Equal("power off Sam1", ports.Events.Last());
        }

        [Fact]
        public void A_cancellation_while_opening_powers_the_socket_down_and_is_passed_on()
        {
            var ports = Ports(sam1: SamTestData.SamAtr);
            ports.SetT1Throws = new OperationCanceledException();

            Assert.ThrowsAny<OperationCanceledException>(() => new Twn4SamFinder(ports).Open("TWN4 SAM socket 1"));
            Assert.Equal("power off Sam1", ports.Events.Last());
        }

        [Fact]
        public void A_cancellation_while_looking_powers_the_socket_down_and_is_passed_on()
        {
            var ports = Ports(sam1: SamTestData.SamAtr);
            ports.PowerOnThrows = new OperationCanceledException();

            Assert.ThrowsAny<OperationCanceledException>(() => new Twn4SamFinder(ports).Find());
            Assert.Equal("power off Sam1", ports.Events.Last());
        }

        [Fact]
        public void The_gate_of_the_sam_is_not_taken_because_the_window_holds_it_already()
        {
            var ports = Ports(sam1: SamTestData.SamAtr);
            Assert.True(SamPortGate.TryEnter());

            try
            {
                var found = new Twn4SamFinder(ports).Find();
                using var connection = new Twn4SamFinder(ports).Open("TWN4 SAM socket 1");

                Assert.Single(found.Readers);
                Assert.NotNull(connection);
                Assert.True(SamPortGate.IsHeld);
            }
            finally
            {
                SamPortGate.Exit();
            }

            Assert.False(SamPortGate.IsHeld);
        }

        // ── the factory of the window ──

        [Fact]
        public void Without_a_twn4_the_window_looks_at_the_pcsc_readers_only_and_with_one_it_lists_the_sockets_after_them()
        {
            var source = new FakeSamReaderSource().With("OMNIKEY 5422 Smartcard Reader 0", SamTestData.SamAtr);
            var ports = Ports(sam1: SamTestData.SamAtr);

            var without = SamFinders.Create(source, null).Find();
            var with = SamFinders.Create(source, ports).Find();

            Assert.Equal(new[] { "OMNIKEY 5422 Smartcard Reader 0" }, without.Readers.Select(r => r.Name));
            Assert.Equal(new[] { "OMNIKEY 5422 Smartcard Reader 0", "TWN4 SAM socket 1" }, with.Readers.Select(r => r.Name));
        }

    }
}
