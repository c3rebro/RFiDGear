#nullable enable
using RFiDGear.Services.Sam;

using System;

using Xunit;

namespace RFiDGear.Tests.Sam
{
    /// <summary>
    /// A connection to a SAM owns what it must let go of: for a PC/SC reader the card connection, for another reader (a TWN4) whatever that reader
    /// needs (powering the socket down). Letting go happens once, however often the connection is disposed.
    /// </summary>
    public class SamConnectionTests
    {
        private static readonly SamReaderInfo Reader = new SamReaderInfo("READER", SamTestData.SamAtr);

        [Fact]
        public void A_connection_over_a_channel_gives_that_channel_and_lets_go_once()
        {
            var channel = new ScriptedSamChannel(_ => throw new InvalidOperationException("not used"));
            var released = 0;
            var connection = new SamConnection(Reader, channel, () => released++);

            connection.Dispose();
            connection.Dispose();

            Assert.Same(channel, connection.Channel);
            Assert.Same(Reader, connection.Reader);
            Assert.Equal(1, released);
        }

        [Fact]
        public void A_connection_to_a_pcsc_card_carries_the_transport_of_the_card_and_lets_go_of_the_card()
        {
            var transport = ScriptedSamTransport.Always(0x90, 0x00);
            var card = new FakeSamCardConnection(SamTestData.SamAtr, transport);

            var connection = new SamConnection(Reader, card);

            Assert.IsType<LlaSamApduChannel>(connection.Channel);
            Assert.False(card.Disposed);

            connection.Dispose();

            Assert.True(card.Disposed);
        }

        [Fact]
        public void A_connection_without_a_channel_or_a_way_to_let_go_is_refused()
        {
            var channel = new ScriptedSamChannel(_ => throw new InvalidOperationException("not used"));

            Assert.Throws<ArgumentNullException>(() => new SamConnection(Reader, (Elatec.NET.Interfaces.ISamApduChannel)null!, () => { }));
            Assert.Throws<ArgumentNullException>(() => new SamConnection(Reader, channel, null!));
        }
    }
}
