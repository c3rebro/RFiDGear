using System;
using System.Collections.Generic;
using System.Linq;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.Classic;
using Xunit;

namespace RFiDGear.Tests.Classic
{
    /// <summary>The block arithmetic is what every Classic operation stands on, so each size and each boundary is pinned.</summary>
    public class ClassicLayoutTests
    {
        public static IEnumerable<object[]> Sectors() => new[]
        {
            new object[] { 0, 0, 4, 3 },
            new object[] { 1, 4, 4, 7 },
            new object[] { 15, 60, 4, 63 },
            new object[] { 16, 64, 4, 67 },
            new object[] { 31, 124, 4, 127 },
            new object[] { 32, 128, 16, 143 },
            new object[] { 33, 144, 16, 159 },
            new object[] { 39, 240, 16, 255 }
        };

        [Theory]
        [MemberData(nameof(Sectors))]
        public void ASector_HasItsFirstBlockItsBlockCountAndItsTrailer(int sector, int first, int blocks, int trailer)
        {
            Assert.Equal(first, ClassicLayout.FirstBlock(sector));
            Assert.Equal(blocks, ClassicLayout.BlocksInSector(sector));
            Assert.Equal(blocks - 1, ClassicLayout.DataBlocksInSector(sector));
            Assert.Equal(trailer, ClassicLayout.TrailerBlock(sector));
        }

        [Fact]
        public void EveryBlock_BelongsToExactlyTheSectorThatContainsIt()
        {
            for (var block = 0; block <= ClassicLayout.MaxBlock; block++)
            {
                var sector = ClassicLayout.SectorOf(block);

                Assert.InRange(block, ClassicLayout.FirstBlock(sector), ClassicLayout.TrailerBlock(sector));
                Assert.Equal(block, ClassicLayout.FirstBlock(sector) + ClassicLayout.BlockInSector(block));
                Assert.Equal(block == ClassicLayout.TrailerBlock(sector), ClassicLayout.IsTrailer(block));
            }
        }

        [Fact]
        public void TheSectorsFollowOneAnother_AndAFourKHasFortyTrailers()
        {
            for (var sector = 1; sector <= ClassicLayout.MaxSector; sector++)
            {
                Assert.Equal(ClassicLayout.TrailerBlock(sector - 1) + 1, ClassicLayout.FirstBlock(sector));
            }

            Assert.Equal(40, Enumerable.Range(0, 256).Count(ClassicLayout.IsTrailer));
        }

        [Theory]
        [InlineData(ClassicCardSize.Mini, 5, 20)]
        [InlineData(ClassicCardSize.K1, 16, 64)]
        [InlineData(ClassicCardSize.K2, 32, 128)]
        [InlineData(ClassicCardSize.K4, 40, 256)]
        public void ASize_HasASectorCountAndABlockCountThatAgree(ClassicCardSize size, int sectors, int blocks)
        {
            Assert.Equal(sectors, ClassicLayout.SectorCount(size));
            Assert.Equal(blocks, ClassicLayout.BlockCount(size));
            Assert.Equal(blocks, Enumerable.Range(0, sectors).Sum(ClassicLayout.BlocksInSector));
        }

        [Fact]
        public void HasSectorAndHasBlock_StopAtTheEndOfTheChip()
        {
            Assert.True(ClassicLayout.HasSector(ClassicCardSize.K1, 15));
            Assert.False(ClassicLayout.HasSector(ClassicCardSize.K1, 16));
            Assert.False(ClassicLayout.HasSector(ClassicCardSize.K1, -1));
            Assert.True(ClassicLayout.HasSector(ClassicCardSize.K4, 39));
            Assert.False(ClassicLayout.HasSector(ClassicCardSize.K4, 40));

            Assert.True(ClassicLayout.HasBlock(ClassicCardSize.K1, 63));
            Assert.False(ClassicLayout.HasBlock(ClassicCardSize.K1, 64));
            Assert.True(ClassicLayout.HasBlock(ClassicCardSize.K4, 255));
            Assert.False(ClassicLayout.HasBlock(ClassicCardSize.K4, 256));
            Assert.False(ClassicLayout.HasBlock(ClassicCardSize.Mini, -1));
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(1, 1)]
        [InlineData(2, 2)]
        [InlineData(3, 3)]
        [InlineData(127, 3)]
        [InlineData(128, 0)]
        [InlineData(132, 0)]
        [InlineData(133, 1)]
        [InlineData(137, 1)]
        [InlineData(138, 2)]
        [InlineData(142, 2)]
        [InlineData(143, 3)]
        [InlineData(255, 3)]
        public void ABlock_FollowsTheAccessConditionOfItsGroup(int block, int group)
        {
            Assert.Equal(group, ClassicLayout.AccessGroup(block));
        }

        [Fact]
        public void OnlyBlockZeroIsTheManufacturerBlock()
        {
            Assert.True(ClassicLayout.IsManufacturerBlock(0));
            Assert.False(ClassicLayout.IsManufacturerBlock(1));
            Assert.False(ClassicLayout.IsManufacturerBlock(128));
            Assert.Throws<ArgumentOutOfRangeException>(() => ClassicLayout.IsManufacturerBlock(256));
        }

        [Fact]
        public void ANumberThatNoChipHas_IsRefusedInsteadOfCalculated()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ClassicLayout.FirstBlock(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => ClassicLayout.FirstBlock(40));
            Assert.Throws<ArgumentOutOfRangeException>(() => ClassicLayout.BlocksInSector(40));
            Assert.Throws<ArgumentOutOfRangeException>(() => ClassicLayout.TrailerBlock(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => ClassicLayout.SectorOf(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => ClassicLayout.SectorOf(256));
            Assert.Throws<ArgumentOutOfRangeException>(() => ClassicLayout.AccessGroup(256));
            Assert.Throws<ArgumentOutOfRangeException>(() => ClassicLayout.SectorCount((ClassicCardSize)0));
            Assert.Throws<ArgumentOutOfRangeException>(() => ClassicLayout.BlockCount((ClassicCardSize)99));
        }

        [Theory]
        [InlineData(CARD_TYPE.MifareMini, ClassicCardSize.Mini)]
        [InlineData(CARD_TYPE.Mifare1K, ClassicCardSize.K1)]
        [InlineData(CARD_TYPE.MifarePlus_SL1_1K, ClassicCardSize.K1)]
        [InlineData(CARD_TYPE.Mifare2K, ClassicCardSize.K2)]
        [InlineData(CARD_TYPE.MifarePlus_SL1_2K, ClassicCardSize.K2)]
        [InlineData(CARD_TYPE.Mifare4K, ClassicCardSize.K4)]
        [InlineData(CARD_TYPE.MifarePlus_SL1_4K, ClassicCardSize.K4)]
        public void AClassicChipType_MapsToItsSize(CARD_TYPE type, ClassicCardSize expected)
        {
            Assert.True(ClassicLayout.TryGetSize(type, out var size));
            Assert.Equal(expected, size);
        }

        [Theory]
        [InlineData(CARD_TYPE.DESFireEV1)]
        [InlineData(CARD_TYPE.Unspecified)]
        [InlineData(CARD_TYPE.NOTAG)]
        public void AChipThatIsNoClassicChip_HasNoSize(CARD_TYPE type)
        {
            Assert.False(ClassicLayout.TryGetSize(type, out _));
        }
    }
}
