#nullable enable
using RFiDGear.Services.Sam;

using System;

using Xunit;

namespace RFiDGear.Tests.Sam
{
    /// <summary>
    /// The SAM, opened for one operation: from the reader that is configured, never the reader of the card, never while somebody else has it,
    /// and only when the card in it says that it is a MIFARE SAM AV3. It is given back (and its state with it) when the operation is over.
    /// </summary>
    [Collection("SamPortGate")]
    public class SamPiccConnectorTests
    {
        private const string SamReader = "HID Global OMNIKEY 5422 Smartcard Reader 0";
        private const string CardReader = "HID Global OMNIKEY 5422CL Smartcard Reader 0";

        private static FakeSamReaderSource Source() => new FakeSamReaderSource().With(SamReader, SamTestData.SamAtr).With(CardReader, SamTestData.DesfireAtr);

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void No_sam_reader_in_the_settings_is_not_configured_and_nothing_is_touched(string? name)
        {
            var source = Source();

            var open = new SamPiccConnector(new SamReaderFinder(source)).Open(name, CardReader);

            Assert.Null(open.Connection);
            Assert.Equal(SamPiccFailure.NotConfigured, open.Failure);
            Assert.Empty(source.AtrRead);
            Assert.Empty(source.Connected);
            Assert.False(SamPortGate.IsHeld);
        }

        [Theory]
        [InlineData(CardReader)]
        [InlineData("hid global omnikey 5422cl smartcard reader 0")]
        [InlineData("  " + CardReader + " ")]
        public void The_reader_of_the_card_is_never_the_reader_of_the_sam(string samName)
        {
            var source = Source();

            var open = new SamPiccConnector(new SamReaderFinder(source)).Open(samName, CardReader);

            Assert.Null(open.Connection);
            Assert.Equal(SamPiccFailure.SamIsCardReader, open.Failure);
            Assert.Empty(source.Connected);
            Assert.False(SamPortGate.IsHeld);
        }

        [Fact]
        public void A_sam_that_somebody_else_holds_is_busy_and_stays_theirs()
        {
            var source = Source();
            Assert.True(SamPortGate.TryEnter());

            try
            {
                var open = new SamPiccConnector(new SamReaderFinder(source)).Open(SamReader, CardReader);

                Assert.Null(open.Connection);
                Assert.Equal(SamPiccFailure.SamBusy, open.Failure);
                Assert.Empty(source.Connected);
                Assert.True(SamPortGate.IsHeld);
            }
            finally
            {
                SamPortGate.Exit();
            }

            Assert.False(SamPortGate.IsHeld);
        }

        [Fact]
        public void A_reader_with_a_card_that_is_no_sam_is_not_opened_and_the_gate_is_given_back()
        {
            var source = new FakeSamReaderSource().With(SamReader, SamTestData.DesfireAtr);

            var open = new SamPiccConnector(new SamReaderFinder(source)).Open(SamReader, CardReader);

            Assert.Null(open.Connection);
            Assert.Equal(SamPiccFailure.SamNotFound, open.Failure);
            Assert.Empty(source.Connected);
            Assert.False(SamPortGate.IsHeld);
        }

        [Fact]
        public void A_reader_that_is_not_there_is_not_found_and_the_gate_is_given_back()
        {
            var source = new FakeSamReaderSource().With("another reader", SamTestData.SamAtr);

            var open = new SamPiccConnector(new SamReaderFinder(source)).Open(SamReader, CardReader);

            Assert.Equal(SamPiccFailure.SamNotFound, open.Failure);
            Assert.False(SamPortGate.IsHeld);
        }

        [Fact]
        public void A_reader_that_fails_is_a_reader_failure_and_the_gate_is_given_back()
        {
            var source = Source();
            source.ConnectFails[SamReader] = new SamReaderException("the reader failed");

            var open = new SamPiccConnector(new SamReaderFinder(source)).Open(SamReader, CardReader);

            Assert.Null(open.Connection);
            Assert.Equal(SamPiccFailure.ReaderFailure, open.Failure);
            Assert.False(SamPortGate.IsHeld);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Any_failure_of_the_reader_source_is_a_reader_failure_and_the_gate_is_given_back(bool inTheAtr)
        {
            // The library does not wrap every failure of a reader: an ordinary exception must not escape and must not keep the SAM held.
            var source = Source();
            (inTheAtr ? source.AtrFails : source.ConnectFails)[SamReader] = new InvalidOperationException("reader text that must not be logged");

            var open = new SamPiccConnector(new SamReaderFinder(source)).Open(SamReader, CardReader);

            Assert.Null(open.Connection);
            Assert.Equal(SamPiccFailure.ReaderFailure, open.Failure);
            Assert.False(SamPortGate.IsHeld);
        }
        [Fact]
        public void An_open_sam_holds_the_gate_until_it_is_disposed_and_then_lets_go_of_the_card_once()
        {
            var source = Source();

            var open = new SamPiccConnector(new SamReaderFinder(source)).Open(SamReader, CardReader);

            Assert.NotNull(open.Connection);
            Assert.Equal(SamPiccFailure.None, open.Failure);
            Assert.True(SamPortGate.IsHeld);
            Assert.NotNull(open.Connection!.Channel);
            Assert.Single(source.Handed);
            Assert.False(source.Handed[0].Connection.Disposed);

            open.Connection.Dispose();

            Assert.False(SamPortGate.IsHeld);
            Assert.True(source.Handed[0].Connection.Disposed);

            // A second dispose gives nothing back that is not held.
            open.Connection.Dispose();

            Assert.False(SamPortGate.IsHeld);
        }

        [Fact]
        public void Only_one_operation_can_hold_the_sam_at_a_time()
        {
            var source = Source();
            var connector = new SamPiccConnector(new SamReaderFinder(source));

            var first = connector.Open(SamReader, CardReader);
            var second = connector.Open(SamReader, CardReader);

            Assert.NotNull(first.Connection);
            Assert.Null(second.Connection);
            Assert.Equal(SamPiccFailure.SamBusy, second.Failure);

            first.Connection!.Dispose();

            var third = connector.Open(SamReader, CardReader);

            Assert.NotNull(third.Connection);
            third.Connection!.Dispose();
        }
    }
}