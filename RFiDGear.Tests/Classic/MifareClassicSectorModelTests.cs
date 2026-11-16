using System;
using RFiDGear.Infrastructure.AccessControl;
using RFiDGear.Infrastructure.Classic;
using RFiDGear.Models;
using Xunit;

namespace RFiDGear.Tests.Classic
{
    /// <summary>
    /// The sector model of the dialogs: the text of the sector trailer is made from the conditions and read into them through the access bits
    /// of the Classic layer, so the three bytes of the chip, the text and the conditions of the groups always say the same.
    /// </summary>
    public class MifareClassicSectorModelTests
    {
        private const string Keys = "FFFFFFFFFFFF";

        private static string Text(ClassicAccessBits bits, byte gpb) => Keys + "," + Convert.ToHexString(bits.ToBytes()) + gpb.ToString("X2") + "," + Keys;

        [Fact]
        public void ASectorOfTheDefaultModel_HasTheTransportAccessBits()
        {
            var sector = new MifareClassicSectorModel(4);

            Assert.True(sector.IsValidSectorTrailer);
            Assert.Equal("FFFFFFFFFFFF,FF0780C3,FFFFFFFFFFFF", sector.AccessBitsAsString);
            Assert.Equal(0u, sector.DataBlock0.Cx);
            Assert.Equal(4, sector.Cx);
        }

        [Fact]
        public void TheAccessBitsOfARealChip_AreReadIntoTheConditions_AndWrittenBackAsTheyWere()
        {
            var sector = new MifareClassicSectorModel(4);

            sector.AccessBitsAsString = "FFFFFFFFFFFF,787788C1,FFFFFFFFFFFF";

            Assert.True(sector.IsValidSectorTrailer);
            Assert.Equal(1u, sector.DataBlock0.Cx);
            Assert.Equal(1u, sector.DataBlock1.Cx);
            Assert.Equal(1u, sector.DataBlock2.Cx);
            Assert.Equal(6, sector.Cx);
            Assert.Equal(0xC1, sector.GeneralPurposeByte);
            Assert.Equal("FFFFFFFFFFFF,787788C1,FFFFFFFFFFFF", sector.AccessBitsAsString);
        }

        [Theory]
        [InlineData(0, 1, 2, 3)]
        [InlineData(1, 2, 3, 4)]
        [InlineData(2, 3, 4, 5)]
        [InlineData(3, 4, 5, 6)]
        [InlineData(4, 5, 6, 7)]
        [InlineData(5, 6, 7, 0)]
        [InlineData(6, 7, 0, 1)]
        [InlineData(7, 0, 1, 2)]
        public void EveryCondition_OfEveryGroup_IsReadAndWrittenBack(int group0, int group1, int group2, int trailer)
        {
            // The block 0 is where the old code took the wrong bits for the second and the third condition bit.
            var bits = new ClassicAccessBits(group0, group1, group2, trailer);
            var text = Text(bits, 0x69);
            var sector = new MifareClassicSectorModel(4);

            sector.AccessBitsAsString = text;

            Assert.True(sector.IsValidSectorTrailer);
            Assert.Equal((uint)group0, sector.DataBlock0.Cx);
            Assert.Equal((uint)group1, sector.DataBlock1.Cx);
            Assert.Equal((uint)group2, sector.DataBlock2.Cx);
            Assert.Equal(trailer, sector.Cx);
            Assert.Equal(text, sector.AccessBitsAsString);
        }

        [Fact]
        public void TheEightDigitsAlone_AreTheAccessBitsToo()
        {
            var sector = new MifareClassicSectorModel(4);

            sector.AccessBitsAsString = "7F0788C3";

            Assert.True(sector.IsValidSectorTrailer);
            Assert.Equal(6, sector.Cx);
            Assert.Equal("FFFFFFFFFFFF,7F0788C3,FFFFFFFFFFFF", sector.AccessBitsAsString);
        }

        [Theory]
        [InlineData("")]
        [InlineData("FF0780")]
        [InlineData("FFFFFFFFFFFF,FF0780C3")]
        [InlineData("FFFFFFFFFFFF,FF0780C3,FFFFFFFFFFF")]
        [InlineData("FFFFFFFFFFFF,FF0780C3,GGGGGGGGGGGG")]
        [InlineData("FFFFFFFFFFFF,FF0780,FFFFFFFFFFFF")]
        [InlineData("FFFFFFFFFFFF,FF0780C3FF,FFFFFFFFFFFF")]
        [InlineData("FFFFFFFFFFFF,FF0781C3,FFFFFFFFFFFF")] // the inverse of one bit is wrong
        [InlineData("FFFFFFFFFFFF,00000000,FFFFFFFFFFFF")]
        public void ATextThatIsNoSectorTrailer_IsNotTaken_AndChangesNothing(string text)
        {
            var sector = new MifareClassicSectorModel(4);

            sector.AccessBitsAsString = "FFFFFFFFFFFF,787788C1,FFFFFFFFFFFF";
            sector.AccessBitsAsString = text;

            Assert.False(sector.IsValidSectorTrailer);
            Assert.Equal("FFFFFFFFFFFF,787788C1,FFFFFFFFFFFF", sector.AccessBitsAsString);
            Assert.Equal(6, sector.Cx);
        }

        [Fact]
        public void ANullText_IsNotTaken()
        {
            var sector = new MifareClassicSectorModel(4);

            sector.AccessBitsAsString = null;

            Assert.False(sector.IsValidSectorTrailer);
            Assert.Equal(4, sector.Cx);
        }

        [Fact]
        public void TheAccessBitsOfTheReaders_AreMadeFromTheConditionsNow_AndNothingStaysFromBefore()
        {
            var sector = new MifareClassicSectorModel(4);

            sector.AccessBitsAsString = Text(new ClassicAccessBits(7, 7, 7, 7), 0xC3);

            var first = sector.SAB;

            sector.AccessBitsAsString = Text(new ClassicAccessBits(0, 1, 2, 4), 0xC3);

            var second = sector.SAB;

            Assert.Equal(1, first.d_sector_trailer_access_bits.c1);
            Assert.Equal(1, first.d_sector_trailer_access_bits.c2);
            Assert.Equal(1, first.d_sector_trailer_access_bits.c3);

            // 0 / 1 / 2 / 4 as C1 + 2 * C2 + 4 * C3: nothing of the 7 / 7 / 7 / 7 before it is left (the old state was ORed into and never cleared).
            Assert.Equal(new[] { 0, 0, 0 }, Bits(second.d_data_block0_access_bits));
            Assert.Equal(new[] { 1, 0, 0 }, Bits(second.d_data_block1_access_bits));
            Assert.Equal(new[] { 0, 1, 0 }, Bits(second.d_data_block2_access_bits));
            Assert.Equal(new[] { 0, 0, 1 }, Bits(second.d_sector_trailer_access_bits));
        }

        [Fact]
        public void TheAccessBitsOfTheReaders_ReadTheSameWhenAskedTwice()
        {
            var sector = new MifareClassicSectorModel(4);

            sector.AccessBitsAsString = Text(new ClassicAccessBits(5, 3, 6, 2), 0xC3);

            // Reading the text (the old getter changed the state) and the bits does not change what they say.
            var before = sector.SAB;
            _ = sector.AccessBitsAsString;
            _ = sector.AccessBitsAsString;

            Assert.Equal(before, sector.SAB);
            Assert.Equal(new[] { 1, 0, 1 }, Bits(sector.SAB.d_data_block0_access_bits));
        }

        [Fact]
        public void AModelWithoutConditions_GivesNoAccessBits()
        {
            // All zero is "no access bits given": the chip keeps its own.
            var sector = new MifareClassicSectorModel();

            Assert.Equal(default, sector.SAB);
        }

        [Fact]
        public void TheWriteRightOfKeyB_IsKept_AndIsNotTheReadRight()
        {
            var sector = new MifareClassicSectorModel();

            sector.Read_KeyB = AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA;
            sector.Write_KeyB = AccessCondition_MifareClassicSectorTrailer.NotAllowed;

            Assert.Equal(AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA, sector.Read_KeyB);
            Assert.Equal(AccessCondition_MifareClassicSectorTrailer.NotAllowed, sector.Write_KeyB);
            Assert.Equal(AccessCondition_MifareClassicSectorTrailer.NotAllowed, sector.SectorAccessCondition.Write_KeyB);
        }

        private static int[] Bits(AccessBits bits) => new[] { (int)bits.c1, bits.c2, bits.c3 };
    }
}
