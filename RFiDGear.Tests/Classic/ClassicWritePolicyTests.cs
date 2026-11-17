using System;
using System.Linq;
using RFiDGear.Infrastructure.Classic;
using Xunit;

namespace RFiDGear.Tests.Classic
{
    /// <summary>What may be written at all is decided without a chip: the data blocks, and nothing else unless the options say so.</summary>
    public class ClassicWritePolicyTests
    {
        private static readonly byte[] Block = new byte[16];
        private static readonly byte[] Ff6 = Enumerable.Repeat((byte)0xFF, 6).ToArray();

        private static byte[] Trailer(ClassicAccessBits bits) => bits.BuildTrailer(Ff6, 0x69, Ff6);

        private static ClassicWriteViolation Check(int block, byte[]? data, ClassicWriteOptions? options = null, ClassicCardSize size = ClassicCardSize.K4) =>
            ClassicWritePolicy.CheckBlock(size, block, data, options ?? ClassicWriteOptions.DataOnly).Violation;

        [Fact]
        public void ADataBlock_OfSixteenBytes_MayBeWritten()
        {
            Assert.Equal(ClassicWriteViolation.None, Check(5, Block));
            Assert.Equal(ClassicWriteViolation.None, Check(255 - 1, Block));
            Assert.True(ClassicWritePolicy.CheckBlock(ClassicCardSize.K4, 5, Block, ClassicWriteOptions.DataOnly).IsAllowed);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(15)]
        [InlineData(17)]
        [InlineData(32)]
        public void AnythingButSixteenBytes_IsWrongLength(int length)
        {
            Assert.Equal(ClassicWriteViolation.WrongLength, Check(5, new byte[length]));
        }

        [Fact]
        public void NoData_IsWrongLength()
        {
            Assert.Equal(ClassicWriteViolation.WrongLength, Check(5, null));
        }

        [Theory]
        [InlineData(-1, ClassicCardSize.K4)]
        [InlineData(256, ClassicCardSize.K4)]
        [InlineData(64, ClassicCardSize.K1)]
        [InlineData(128, ClassicCardSize.K2)]
        [InlineData(20, ClassicCardSize.Mini)]
        public void ABlockThatTheChipDoesNotHave_IsOutsideTheChip(int block, ClassicCardSize size)
        {
            Assert.Equal(ClassicWriteViolation.OutsideTheChip, Check(block, Block, size: size));
        }

        [Fact]
        public void TheManufacturerBlock_NeedsItsOwnOption()
        {
            Assert.Equal(ClassicWriteViolation.ManufacturerBlock, Check(0, Block));
            Assert.Equal(ClassicWriteViolation.ManufacturerBlock, Check(0, Block, new ClassicWriteOptions { AllowTrailer = true, AllowFinalAccessBits = true }));
            Assert.Equal(ClassicWriteViolation.None, Check(0, Block, new ClassicWriteOptions { AllowManufacturerBlock = true }));
        }

        [Fact]
        public void ATrailer_NeedsItsOwnOption()
        {
            Assert.Equal(ClassicWriteViolation.TrailerNotAllowed, Check(3, Trailer(ClassicAccessBits.Transport)));
            Assert.Equal(ClassicWriteViolation.TrailerNotAllowed, Check(255, Trailer(ClassicAccessBits.Transport), new ClassicWriteOptions { AllowManufacturerBlock = true }));
            Assert.Equal(ClassicWriteViolation.None, Check(3, Trailer(ClassicAccessBits.Transport), new ClassicWriteOptions { AllowTrailer = true }));
            Assert.Equal(ClassicWriteViolation.None, Check(255, Trailer(ClassicAccessBits.MadStandard), new ClassicWriteOptions { AllowTrailer = true }));
        }

        [Fact]
        public void ATrailerWhoseBitsAreNotTheInverseOfTheirCopy_IsNeverWritten()
        {
            var broken = Trailer(ClassicAccessBits.Transport);

            broken[7] ^= 0x01;

            var everything = new ClassicWriteOptions { AllowManufacturerBlock = true, AllowTrailer = true, AllowFinalAccessBits = true };

            Assert.Equal(ClassicWriteViolation.AccessBitsNotConsistent, Check(3, broken, everything));
            Assert.Equal(ClassicWriteViolation.AccessBitsNotConsistent, Check(3, new byte[16], everything));
        }

        [Theory]
        [InlineData(0, true)]  // 000: the bits can never be written again
        [InlineData(1, true)]  // 100
        [InlineData(2, true)]  // 010
        [InlineData(3, true)]  // 110
        [InlineData(4, false)] // 001: the factory condition, key A writes the bits
        [InlineData(5, false)] // 101: key B writes the bits
        [InlineData(6, false)] // 011: the MAD condition, key B writes the bits
        [InlineData(7, true)]  // 111
        public void AccessBitsThatCanNeverBeChangedAgain_NeedASecondOption(int trailerCondition, bool final)
        {
            var data = Trailer(new ClassicAccessBits(0, 0, 0, trailerCondition));
            var trailerOnly = new ClassicWriteOptions { AllowTrailer = true };
            var forGood = new ClassicWriteOptions { AllowTrailer = true, AllowFinalAccessBits = true };

            Assert.Equal(final ? ClassicWriteViolation.AccessBitsAreFinal : ClassicWriteViolation.None, Check(7, data, trailerOnly));
            Assert.Equal(ClassicWriteViolation.None, Check(7, data, forGood));
        }

        [Theory]
        [InlineData(0, 32)]
        [InlineData(1, 48)]
        [InlineData(31, 48)]
        [InlineData(32, 240)]
        [InlineData(39, 240)]
        public void ASectorWrite_TakesTheDataBlocksOfTheSector(int sector, int bytes)
        {
            Assert.Equal(bytes, ClassicWritePolicy.SectorDataLength(sector, ClassicWriteOptions.DataOnly));
        }
        [Fact]
        public void TheManufacturerBlockOfSectorZero_IsPartOfTheSectorWrite_OnlyWhenItMayBeWritten()
        {
            Assert.Equal(32, ClassicWritePolicy.SectorDataLength(0, ClassicWriteOptions.DataOnly));
            Assert.Equal(48, ClassicWritePolicy.SectorDataLength(0, new ClassicWriteOptions { AllowManufacturerBlock = true }));
            Assert.Equal(48, ClassicWritePolicy.SectorDataLength(1, new ClassicWriteOptions { AllowManufacturerBlock = true }));
        }

        [Fact]
        public void ASectorWrite_NeedsExactlyTheBytesOfTheSector_AndASectorThatTheChipHas()
        {
            Assert.Equal(ClassicWriteViolation.None, ClassicWritePolicy.CheckSector(ClassicCardSize.K4, 2, new byte[48], ClassicWriteOptions.DataOnly).Violation);
            Assert.Equal(ClassicWriteViolation.WrongLength, ClassicWritePolicy.CheckSector(ClassicCardSize.K4, 2, new byte[47], ClassicWriteOptions.DataOnly).Violation);
            Assert.Equal(ClassicWriteViolation.WrongLength, ClassicWritePolicy.CheckSector(ClassicCardSize.K4, 2, new byte[64], ClassicWriteOptions.DataOnly).Violation);
            Assert.Equal(ClassicWriteViolation.WrongLength, ClassicWritePolicy.CheckSector(ClassicCardSize.K4, 2, null, ClassicWriteOptions.DataOnly).Violation);
            Assert.Equal(ClassicWriteViolation.WrongLength, ClassicWritePolicy.CheckSector(ClassicCardSize.K4, 0, new byte[48], ClassicWriteOptions.DataOnly).Violation);
            Assert.Equal(ClassicWriteViolation.OutsideTheChip, ClassicWritePolicy.CheckSector(ClassicCardSize.K1, 16, new byte[48], ClassicWriteOptions.DataOnly).Violation);
            Assert.Equal(ClassicWriteViolation.OutsideTheChip, ClassicWritePolicy.CheckSector(ClassicCardSize.K4, -1, new byte[48], ClassicWriteOptions.DataOnly).Violation);
        }

        [Theory]
        [InlineData(2, 16, ClassicWriteViolation.None)]
        [InlineData(2, 32, ClassicWriteViolation.None)]
        [InlineData(2, 48, ClassicWriteViolation.None)]
        [InlineData(2, 0, ClassicWriteViolation.WrongLength)]
        [InlineData(2, 15, ClassicWriteViolation.WrongLength)]
        [InlineData(2, 17, ClassicWriteViolation.WrongLength)]
        [InlineData(2, 64, ClassicWriteViolation.WrongLength)]
        [InlineData(0, 32, ClassicWriteViolation.None)]
        [InlineData(0, 48, ClassicWriteViolation.WrongLength)]
        [InlineData(33, 240, ClassicWriteViolation.None)]
        [InlineData(33, 256, ClassicWriteViolation.WrongLength)]
        public void TheFirstBlocksOfASector_AreWholeBlocks_AndNotMoreThanTheSectorHas(int sector, int length, ClassicWriteViolation expected)
        {
            Assert.Equal(expected, ClassicWritePolicy.CheckSectorPart(ClassicCardSize.K4, sector, new byte[length], ClassicWriteOptions.DataOnly).Violation);
        }

        [Fact]
        public void TheFirstBlocksOfASector_NeedDataAndASectorThatTheChipHas()
        {
            Assert.Equal(ClassicWriteViolation.WrongLength, ClassicWritePolicy.CheckSectorPart(ClassicCardSize.K4, 2, null, ClassicWriteOptions.DataOnly).Violation);
            Assert.Equal(ClassicWriteViolation.OutsideTheChip, ClassicWritePolicy.CheckSectorPart(ClassicCardSize.K1, 16, new byte[16], ClassicWriteOptions.DataOnly).Violation);
            Assert.Equal(ClassicWriteViolation.OutsideTheChip, ClassicWritePolicy.CheckSectorPart(ClassicCardSize.K4, -1, new byte[16], ClassicWriteOptions.DataOnly).Violation);
            Assert.Throws<ArgumentNullException>(() => ClassicWritePolicy.CheckSectorPart(ClassicCardSize.K4, 2, Block, null!));
        }

        [Fact]
        public void NoOptions_AreRefused()
        {
            Assert.Throws<ArgumentNullException>(() => ClassicWritePolicy.CheckBlock(ClassicCardSize.K4, 5, Block, null!));
            Assert.Throws<ArgumentNullException>(() => ClassicWritePolicy.CheckSector(ClassicCardSize.K4, 5, Block, null!));
            Assert.Throws<ArgumentNullException>(() => ClassicWritePolicy.SectorDataLength(5, null!));
        }
    }
}
