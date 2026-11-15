using RFiDGear.Services.Sam;

using System;
using System.Linq;
using System.Threading.Tasks;

using Xunit;

namespace RFiDGear.Tests.Sam
{
    /// <summary>Finding the MIFARE SAM AV3 among the PC/SC readers without sending anything to a card.</summary>
    public class SamReaderFinderTests
    {
        private const string ContactReader = "HID Global OMNIKEY 5422 Smartcard Reader 0";
        private const string ContactlessReader = "HID Global OMNIKEY 5422CL Smartcard Reader 0";
        private const string HelloReader = "Windows Hello for Business 1";

        // The readers of the bench: the SAM in the contact slot, nothing in the contactless one, and the made-up one of Windows Hello.
        private static FakeSamReaderSource Bench() => new FakeSamReaderSource()
            .With(ContactReader, SamTestData.SamAtr)
            .With(ContactlessReader, null)
            .With(HelloReader, SamTestData.HelloAtr);

        [Fact]
        public void The_reader_with_the_sam_is_found_and_no_other()
        {
            var result = new SamReaderFinder(Bench()).Find();

            var found = Assert.Single(result.Readers);
            Assert.Equal(ContactReader, found.Name);
            Assert.Equal(SamTestData.SamAtr, found.Atr);
            Assert.False(result.EnumerationFailed);
        }

        [Fact]
        public void A_search_connects_to_no_card_and_so_sends_no_command_to_any()
        {
            var source = Bench().With("Another reader", SamTestData.DesfireAtr);

            new SamReaderFinder(source).Find();

            // The ATR of every card is read, and no card is opened for it: a connection is what lets the library talk to a card.
            Assert.Contains(ContactReader, source.AtrRead);
            Assert.Contains("Another reader", source.AtrRead);
            Assert.Empty(source.Connected);
            Assert.Empty(source.Handed);
        }

        [Fact]
        public void The_virtual_reader_of_windows_hello_is_not_even_looked_at()
        {
            var source = Bench();

            new SamReaderFinder(source).Find();

            Assert.DoesNotContain(HelloReader, source.AtrRead);
            Assert.Contains(ContactReader, source.AtrRead);
        }

        [Fact]
        public void Two_readers_with_a_sam_are_both_found()
        {
            var source = new FakeSamReaderSource()
                .With("First", SamTestData.SamAtr)
                .With("Second", SamTestData.SamAtr);

            var result = new SamReaderFinder(source).Find();

            Assert.Equal(new[] { "First", "Second" }, result.Readers.Select(r => r.Name));
        }

        [Fact]
        public void A_reader_that_cannot_be_looked_at_is_skipped_and_the_others_still_are()
        {
            var source = new FakeSamReaderSource()
                .With("Broken", SamTestData.SamAtr)
                .With("Good", SamTestData.SamAtr);
            source.AtrFails["Broken"] = new InvalidOperationException("PRIVATE-LIBRARY-TEXT");

            var result = new SamReaderFinder(source).Find();

            Assert.Equal("Good", Assert.Single(result.Readers).Name);
            Assert.False(result.EnumerationFailed);
        }

        [Fact]
        public void When_the_readers_cannot_be_listed_the_search_says_so_instead_of_finding_nothing()
        {
            var source = new FakeSamReaderSource { ListFails = new InvalidOperationException("PRIVATE-LIBRARY-TEXT") };

            var result = new SamReaderFinder(source).Find();

            Assert.True(result.EnumerationFailed);
            Assert.Empty(result.Readers);
        }

        [Fact]
        public void The_log_of_a_failed_search_names_the_type_of_the_exception_and_not_its_text()
        {
            using var logs = CapturedLogs.Install();
            var source = new FakeSamReaderSource { ListFails = new InvalidOperationException("PRIVATE-LIBRARY-TEXT") }
                .With("Broken", SamTestData.SamAtr);

            new SamReaderFinder(source).Find();

            var written = logs.From(nameof(SamReaderFinder)).Select(CapturedLogs.EverythingWrittenFor).ToList();
            Assert.NotEmpty(written);
            Assert.All(written, text => Assert.DoesNotContain("PRIVATE-LIBRARY-TEXT", text, StringComparison.Ordinal));
            Assert.Contains(written, text => text.Contains(nameof(InvalidOperationException), StringComparison.Ordinal));

            // A reader that fails is logged the same way, with no name of it.
            var failing = new FakeSamReaderSource().With("Broken-reader-name", SamTestData.SamAtr);
            failing.AtrFails["Broken-reader-name"] = new InvalidOperationException("PRIVATE-LIBRARY-TEXT");

            new SamReaderFinder(failing).Find();

            Assert.All(
                logs.From(nameof(SamReaderFinder)).Select(CapturedLogs.EverythingWrittenFor),
                text =>
                {
                    Assert.DoesNotContain("PRIVATE-LIBRARY-TEXT", text, StringComparison.Ordinal);
                    Assert.DoesNotContain("Broken-reader-name", text, StringComparison.Ordinal);
                });
        }

        [Fact]
        public async Task A_connection_to_the_sam_sends_commands_through_the_transport_and_lets_go_of_the_card_when_disposed()
        {
            var source = Bench();

            using (var connection = new SamReaderFinder(source).Open(ContactReader))
            {
                Assert.NotNull(connection);
                Assert.Equal(ContactReader, connection!.Reader.Name);

                await SamDetection.ReadVersionAsync(connection.Channel);

                var handed = Assert.Single(source.Handed);
                Assert.Equal("8060000000", Convert.ToHexString(Assert.Single(handed.Transport.Sent)));
                Assert.False(handed.Connection.Disposed);
            }

            Assert.True(Assert.Single(source.Handed).Connection.Disposed);
        }

        [Theory]
        [InlineData(ContactlessReader)] // no card
        [InlineData(HelloReader)] // a virtual reader
        [InlineData("No such reader")]
        [InlineData("")]
        public void There_is_no_connection_to_a_reader_without_a_sam(string readerName)
        {
            var source = Bench();

            Assert.Null(new SamReaderFinder(source).Open(readerName));
            Assert.Empty(source.Connected);
        }

        [Fact]
        public void A_card_that_is_no_sam_is_never_connected_to()
        {
            var source = new FakeSamReaderSource().With("Reader", SamTestData.DesfireAtr);

            var connection = new SamReaderFinder(source).Open("Reader");

            Assert.Null(connection);
            Assert.Contains("Reader", source.AtrRead);
            Assert.Empty(source.Connected);
            Assert.Empty(source.Handed);
        }

        [Fact]
        public void A_card_that_was_swapped_between_the_look_and_the_connection_gets_no_command_and_is_let_go_of()
        {
            var source = new FakeSamReaderSource().With("Reader", SamTestData.SamAtr);
            source.AtrOnConnect["Reader"] = SamTestData.DesfireAtr;

            var connection = new SamReaderFinder(source).Open("Reader");

            Assert.Null(connection);
            var handed = Assert.Single(source.Handed);
            Assert.True(handed.Connection.Disposed);
            Assert.Empty(handed.Transport.Sent);
        }

        [Fact]
        public void A_missing_source_is_refused()
        {
            Assert.Throws<ArgumentNullException>(() => new SamReaderFinder(null!));
        }
    }
}
