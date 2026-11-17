using RFiDGear.Services.Sam;

using System;
using System.Linq;
using System.Threading.Tasks;

using Elatec.NET.Sam;

using Xunit;

namespace RFiDGear.Tests.Sam
{
    /// <summary>Recognising a MIFARE SAM AV3 and reading what SAM_GetVersion says about it, and no more.</summary>
    public class SamDetectionTests
    {
        [Fact]
        public void The_atr_of_the_sam_on_the_bench_is_a_sam_av3()
        {
            Assert.True(SamDetection.IsSamAv3(SamTestData.SamAtr));
        }

        [Fact]
        public void The_revision_character_after_the_marker_does_not_matter()
        {
            // "MIFARE SAM AV3" + another character than "0", and the check byte that goes with it.
            var atr = (byte[])SamTestData.SamAtr.Clone();
            atr[atr.Length - 2] = 0x31;

            Assert.True(SamDetection.IsSamAv3(atr));
        }

        [Theory]
        [InlineData("3B8D0180FBA000000397425446590401CF")] // the card Windows Hello makes up
        [InlineData("3B8180018080")] // a DESFire card
        [InlineData("3B8F8001804F0CA00000030603003D0000000056")] // an NTAG215
        // The marker with its last byte missing ("MIFARE SAM AV"): not a SAM AV3.
        [InlineData("3BDF18FF81F1FE43003F07834D49464152452053414D204156")]
        // A SAM AV2 says "MIFARE SAM AV2": a different SAM that this setup does not know.
        [InlineData("3BDF18FF81F1FE43003F07834D49464152452053414D2041563230")]
        [InlineData("")]
        public void Other_cards_are_not_a_sam_av3(string atrHex)
        {
            Assert.False(SamDetection.IsSamAv3(Convert.FromHexString(atrHex)));
        }

        [Fact]
        public void No_atr_is_not_a_sam_av3()
        {
            Assert.False(SamDetection.IsSamAv3(null));
        }

        [Fact]
        public void Only_the_hardware_and_the_software_information_are_taken_from_the_version_answer()
        {
            var answer = SamTestData.VersionAnswer();
            var response = new SamApduResponse(answer.Take(answer.Length - 2).ToArray(), 0x9000);

            var info = SamDetection.ParseVersion(response);

            Assert.NotNull(info);
            Assert.Equal("04010105002301", info!.HardwareInfo);
            Assert.Equal("04010105011F01", info.SoftwareInfo);

            // The bytes after them are the serial number: nothing of them is in the result, in any form.
            Assert.DoesNotContain("EE", info.ToString(), StringComparison.Ordinal);
            Assert.Equal(14, info.HardwareInfo.Length);
            Assert.Equal(14, info.SoftwareInfo.Length);
        }

        [Theory]
        [InlineData("0401010500230104010105011F01", 0x6985)] // enough bytes, but the SAM did not say success
        [InlineData("0401010500230104010105011F", 0x9000)] // one byte short
        [InlineData("", 0x9000)]
        [InlineData("", 0x6D00)]
        public void Anything_but_success_with_enough_bytes_gives_nothing(string dataHex, int statusWord)
        {
            var response = new SamApduResponse(Convert.FromHexString(dataHex), (ushort)statusWord);

            Assert.Null(SamDetection.ParseVersion(response));
        }

        [Fact]
        public async Task Reading_the_version_sends_sam_get_version_once_and_nothing_else()
        {
            var channel = new ScriptedSamChannel(_ =>
            {
                var answer = SamTestData.VersionAnswer();
                return new SamApduResponse(answer.Take(answer.Length - 2).ToArray(), 0x9000);
            });

            var info = await SamDetection.ReadVersionAsync(channel);

            Assert.NotNull(info);
            var sent = Assert.Single(channel.Sent);
            Assert.Equal("8060000000", Convert.ToHexString(LlaSamApduChannel.Encode(sent)));
        }

        [Fact]
        public async Task A_sam_that_refuses_the_version_gives_nothing()
        {
            var channel = new ScriptedSamChannel(_ => new SamApduResponse(Array.Empty<byte>(), 0x6985));

            Assert.Null(await SamDetection.ReadVersionAsync(channel));
        }
    }
}
