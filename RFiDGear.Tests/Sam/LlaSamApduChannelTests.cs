using RFiDGear.Services.Sam;

using System;
using System.Threading.Tasks;

using Elatec.NET.Sam;

using Xunit;

namespace RFiDGear.Tests.Sam
{
    /// <summary>The channel that carries the host protocol of a SAM over a PC/SC transport.</summary>
    public class LlaSamApduChannelTests
    {
        [Theory]
        // Case 1: header only.
        [InlineData(0x80, 0x10, 0x03, 0x00, "", -1, "80100300")]
        // Case 2: Le only, as SAM_GetVersion sends it.
        [InlineData(0x80, 0x60, 0x00, 0x00, "", 0x00, "8060000000")]
        // Case 3: data, no Le.
        [InlineData(0x80, 0xA4, 0x00, 0x00, "000002", -1, "80A4000003000002")]
        // Case 4: data and Le, as the first part of the host authentication sends it.
        [InlineData(0x80, 0xA4, 0x00, 0x00, "000002", 0x00, "80A400000300000200")]
        public void A_command_becomes_a_short_apdu(byte cla, byte ins, byte p1, byte p2, string dataHex, int le, string expectedHex)
        {
            var command = new SamApdu(cla, ins, p1, p2, Convert.FromHexString(dataHex), le < 0 ? (byte?)null : (byte)le);

            Assert.Equal(expectedHex, Convert.ToHexString(LlaSamApduChannel.Encode(command)));
        }

        [Fact]
        public void The_longest_data_field_gets_its_length_in_one_byte()
        {
            var data = new byte[255];
            Array.Fill(data, (byte)0x5A);

            var apdu = LlaSamApduChannel.Encode(new SamApdu(0x80, 0xC1, 0x00, 0x00, data, 0x00));

            Assert.Equal(4 + 1 + 255 + 1, apdu.Length);
            Assert.Equal(0xFF, apdu[4]);
            Assert.Equal(0x5A, apdu[5]);
            Assert.Equal(0x5A, apdu[259]);
            Assert.Equal(0x00, apdu[260]);
        }

        [Theory]
        [InlineData("9000", "", 0x9000)]
        [InlineData("0102039000", "010203", 0x9000)]
        // The answers the host protocol is made of, and the one of a SAM that was never activated: all are answers.
        [InlineData("AABB90AF", "AABB", 0x90AF)]
        [InlineData("6985", "", 0x6985)]
        [InlineData("6D00", "", 0x6D00)]
        public async Task The_status_word_is_split_off_and_an_error_status_word_is_no_exception(string answerHex, string dataHex, int statusWord)
        {
            var transport = ScriptedSamTransport.Always(Convert.FromHexString(answerHex));
            var channel = new LlaSamApduChannel(transport);

            var response = await channel.TransmitAsync(new SamApdu(0x80, 0xFF, 0x00, 0x00, null, 0x00));

            Assert.Equal((ushort)statusWord, response.StatusWord);
            Assert.Equal(dataHex, Convert.ToHexString(response.Data));
        }

        [Theory]
        [InlineData("")]
        [InlineData("90")]
        public async Task An_answer_shorter_than_a_status_word_is_an_error(string answerHex)
        {
            var channel = new LlaSamApduChannel(ScriptedSamTransport.Always(Convert.FromHexString(answerHex)));

            await Assert.ThrowsAsync<SamReaderException>(() => channel.TransmitAsync(new SamApdu(0x80, 0x60, 0x00, 0x00, null, 0x00)));
        }

        [Fact]
        public async Task The_command_goes_out_once_and_is_never_repeated_when_the_reader_fails()
        {
            var transport = new ScriptedSamTransport(_ => throw new InvalidOperationException("PRIVATE-READER-TEXT"));
            var channel = new LlaSamApduChannel(transport);

            var failure = await Assert.ThrowsAsync<SamReaderException>(() => channel.TransmitAsync(new SamApdu(0x80, 0xC1, 0x01, 0x00, new byte[] { 1 }, null)));

            Assert.Single(transport.Sent);

            // The text of the reader library does not travel on: the caller logs the type of this exception, and shows its text.
            Assert.DoesNotContain("PRIVATE-READER-TEXT", failure.Message, StringComparison.Ordinal);
            Assert.Null(failure.InnerException);
        }

        [Fact]
        public async Task An_exception_of_the_sam_layer_is_not_wrapped_again()
        {
            var original = new SamException("original", 0x6A80);
            var channel = new LlaSamApduChannel(new ScriptedSamTransport(_ => throw original));

            var thrown = await Assert.ThrowsAsync<SamException>(() => channel.TransmitAsync(new SamApdu(0x80, 0x60, 0x00, 0x00, null, 0x00)));

            Assert.Same(original, thrown);
        }

        [Fact]
        public async Task A_cancellation_is_not_turned_into_a_reader_error()
        {
            var channel = new LlaSamApduChannel(new ScriptedSamTransport(_ => throw new OperationCanceledException()));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => channel.TransmitAsync(new SamApdu(0x80, 0x60, 0x00, 0x00, null, 0x00)));
        }

        [Fact]
        public async Task The_blocking_call_to_the_reader_is_not_made_on_the_thread_of_the_caller()
        {
            var transport = ScriptedSamTransport.Always(0x90, 0x00);
            var channel = new LlaSamApduChannel(transport);
            var caller = Environment.CurrentManagedThreadId;

            await channel.TransmitAsync(new SamApdu(0x80, 0x60, 0x00, 0x00, null, 0x00));

            Assert.NotEqual(caller, transport.LastThread);
        }

        [Fact]
        public async Task A_missing_transport_or_command_is_refused()
        {
            Assert.Throws<ArgumentNullException>(() => new LlaSamApduChannel(null!));

            var channel = new LlaSamApduChannel(ScriptedSamTransport.Always(0x90, 0x00));

            await Assert.ThrowsAsync<ArgumentNullException>(() => channel.TransmitAsync(null!));
        }
    }
}
