#nullable enable
using RFiDGear.Services.Sam;

using System;
using System.Linq;
using System.Threading.Tasks;

using Xunit;

namespace RFiDGear.Tests.Sam
{
    /// <summary>
    /// What a TWN4 does with the SAM: the T=1 settings come out of the ATR, and the SAM is found in whichever socket it is in, never guessed at
    /// and never used when a second one is there. The reader is a script.
    /// </summary>
    [Collection("SamPortGate")]
    public class Twn4SamTests
    {
        // ── the settings out of the ATR ──

        [Fact]
        public void The_atr_of_the_sam_av3_gives_the_values_that_were_used_on_the_bench()
        {
            var settings = Twn4AtrReader.SettingsOf(SamTestData.SamAtr);

            Assert.Equal(new Twn4SamSettings(GuardTime: 0xFF, WaitingIntegers: 0x43, Ifsc: 0xFE), settings);
        }

        [Fact]
        public void A_card_that_offers_t1_and_nothing_else_gets_the_defaults_of_the_standard()
        {
            // TS, T0 = TD1 only, TD1 = T=1, TCK.
            var settings = Twn4AtrReader.SettingsOf(new byte[] { 0x3B, 0x80, 0x01, 0x81 });

            Assert.Equal(new Twn4SamSettings(GuardTime: 0x00, WaitingIntegers: 0x4D, Ifsc: 0x20), settings);
        }

        [Theory]
        [InlineData("3B9F95801FC78031E073FE2113574A33052C323400BD")]
        [InlineData("3B6800004100000000000000")]
        [InlineData("")]
        [InlineData("3B")]
        [InlineData("0080018 1")]
        public void An_atr_that_does_not_offer_t1_gives_no_settings(string hex)
        {
            Assert.Null(Twn4AtrReader.SettingsOf(Convert.FromHexString(hex.Replace(" ", string.Empty))));
        }

        [Fact]
        public void No_atr_gives_no_settings()
        {
            Assert.Null(Twn4AtrReader.SettingsOf(null));
        }

        [Theory]
        [InlineData(8)]
        [InlineData(12)]
        [InlineData(20)]
        public void An_atr_that_is_cut_off_gives_no_settings(int length)
        {
            Assert.Null(Twn4AtrReader.SettingsOf(SamTestData.SamAtr.Take(length).ToArray()));
        }

        // ── finding the SAM in the sockets ──

        [Fact]
        public async Task The_sam_is_found_in_the_first_socket_and_set_up_for_t1_and_the_gate_is_held_until_it_is_let_go_of()
        {
            var ports = new FakeTwn4Ports { Channel = new ScriptedSamChannel(_ => throw new InvalidOperationException("not used")) };
            ports.Cards[Twn4SamSlot.Sam1] = SamTestData.SamAtr;

            var open = await new Twn4SamConnector(ports).OpenAsync();

            Assert.NotNull(open.Connection);
            Assert.Equal(SamPiccFailure.None, open.Failure);
            Assert.Same(ports.Channel, open.Connection!.Channel);
            Assert.Equal(new[] { "has Sam1", "power on Sam1", "has Sam2", "set t1 Sam1", "channel Sam1" }, ports.Events);
            Assert.Equal(new Twn4SamSettings(0xFF, 0x43, 0xFE), ports.LastSettings);
            Assert.True(SamPortGate.IsHeld);

            open.Connection.Dispose();

            Assert.Equal("power off Sam1", ports.Events.Last());
            Assert.False(SamPortGate.IsHeld);
        }

        [Fact]
        public async Task A_card_in_the_first_socket_that_is_no_sam_is_powered_down_again_and_the_sam_in_the_second_is_used()
        {
            var ports = new FakeTwn4Ports();
            ports.Cards[Twn4SamSlot.Sam1] = FakeTwn4Ports.GsmAtr;
            ports.Cards[Twn4SamSlot.Sam2] = SamTestData.SamAtr;

            var open = await new Twn4SamConnector(ports).OpenAsync();

            Assert.NotNull(open.Connection);
            Assert.Equal(
                new[] { "has Sam1", "power on Sam1", "power off Sam1", "has Sam2", "power on Sam2", "set t1 Sam2", "channel Sam2" },
                ports.Events);

            open.Connection!.Dispose();

            Assert.Equal("power off Sam2", ports.Events.Last());
            Assert.False(SamPortGate.IsHeld);
        }

        [Fact]
        public async Task Empty_sockets_and_cards_that_do_not_answer_are_no_sam()
        {
            var empty = new FakeTwn4Ports();
            var silent = new FakeTwn4Ports();
            silent.Cards[Twn4SamSlot.Sam1] = null;

            var none = await new Twn4SamConnector(empty).OpenAsync();
            var mute = await new Twn4SamConnector(silent).OpenAsync();

            Assert.Null(none.Connection);
            Assert.Equal(SamPiccFailure.SamNotFound, none.Failure);
            Assert.Equal(new[] { "has Sam1", "has Sam2" }, empty.Events);
            Assert.Null(mute.Connection);
            Assert.Equal(SamPiccFailure.SamNotFound, mute.Failure);
            Assert.Contains("power off Sam1", silent.Events);
            Assert.False(SamPortGate.IsHeld);
        }

        [Fact]
        public async Task A_sam_in_each_socket_is_no_choice_that_is_made_and_both_are_powered_down()
        {
            var ports = new FakeTwn4Ports();
            ports.Cards[Twn4SamSlot.Sam1] = SamTestData.SamAtr;
            ports.Cards[Twn4SamSlot.Sam2] = SamTestData.SamAtr;

            var open = await new Twn4SamConnector(ports).OpenAsync();

            Assert.Null(open.Connection);
            Assert.Equal(SamPiccFailure.SamAmbiguous, open.Failure);
            Assert.DoesNotContain("set t1 Sam1", ports.Events);
            Assert.DoesNotContain("channel Sam1", ports.Events);
            Assert.Contains("power off Sam1", ports.Events);
            Assert.Contains("power off Sam2", ports.Events);
            Assert.False(SamPortGate.IsHeld);
        }

        [Fact]
        public async Task A_sam_whose_atr_offers_no_t1_is_refused_and_powered_down()
        {
            var ports = new FakeTwn4Ports();
            ports.Cards[Twn4SamSlot.Sam1] = FakeTwn4Ports.SamAtrWithoutT1;

            var open = await new Twn4SamConnector(ports).OpenAsync();

            Assert.Null(open.Connection);
            Assert.Equal(SamPiccFailure.SamRefused, open.Failure);
            Assert.DoesNotContain("set t1 Sam1", ports.Events);
            Assert.Equal("power off Sam1", ports.Events.Last());
            Assert.False(SamPortGate.IsHeld);
        }

        [Fact]
        public async Task A_reader_that_fails_is_a_reader_failure_without_its_text_and_lets_go_of_everything()
        {
            using var logs = CapturedLogs.Install();
            var ports = new FakeTwn4Ports { SetT1Throws = new InvalidOperationException("PRIVATE-READER-TEXT") };
            ports.Cards[Twn4SamSlot.Sam1] = SamTestData.SamAtr;

            var open = await new Twn4SamConnector(ports).OpenAsync();

            Assert.Null(open.Connection);
            Assert.Equal(SamPiccFailure.ReaderFailure, open.Failure);
            Assert.Equal("power off Sam1", ports.Events.Last());
            Assert.False(SamPortGate.IsHeld);
            Assert.All(logs.All().Select(CapturedLogs.EverythingWrittenFor), text => Assert.DoesNotContain("PRIVATE-READER-TEXT", text, StringComparison.Ordinal));
        }

        [Fact]
        public async Task A_sam_that_is_in_use_is_busy_and_nothing_is_sent_to_the_reader()
        {
            var ports = new FakeTwn4Ports();
            ports.Cards[Twn4SamSlot.Sam1] = SamTestData.SamAtr;
            Assert.True(SamPortGate.TryEnter());

            try
            {
                var open = await new Twn4SamConnector(ports).OpenAsync();

                Assert.Null(open.Connection);
                Assert.Equal(SamPiccFailure.SamBusy, open.Failure);
                Assert.Empty(ports.Events);
            }
            finally
            {
                SamPortGate.Exit();
            }
        }

        [Fact]
        public async Task Letting_go_twice_or_with_a_socket_that_will_not_power_down_still_gives_the_gate_back()
        {
            var ports = new FakeTwn4Ports();
            ports.Cards[Twn4SamSlot.Sam1] = SamTestData.SamAtr;
            var open = await new Twn4SamConnector(ports).OpenAsync();
            ports.PowerOffThrows = new InvalidOperationException("PRIVATE-READER-TEXT");

            open.Connection!.Dispose();
            open.Connection.Dispose();

            Assert.Equal(1, ports.Events.Count(e => e == "power off Sam1"));
            Assert.False(SamPortGate.IsHeld);
        }
    }
}
