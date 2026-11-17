using System;
using System.Linq;
using RFiDGear.Infrastructure.Classic;
using Xunit;

namespace RFiDGear.Tests.Classic
{
    /// <summary>
    /// The MAD is a format that other readers have to understand, so the bytes are pinned: the CRC values are those that a real MIFARE
    /// Classic 4K carried (a blank MAD, and the MAD that LibLogicalAccess wrote for the AID 1234 in sector 3), and the order of the two bytes
    /// of an AID is the one of the specification.
    /// </summary>
    public class ClassicMadTests
    {
        private static byte[] Mad1WithAid1234InSector3()
        {
            var bytes = new byte[ClassicMad.Mad1Length];

            bytes[1] = 0x03;
            bytes[6] = 0x34;
            bytes[7] = 0x12;

            return bytes;
        }

        [Fact]
        public void TheCrcOfABlankMad_IsTheOneARealChipHad()
        {
            Assert.Equal(0xCE, ClassicMad.ComputeCrc(new byte[ClassicMad.Mad1Length]));
            Assert.Equal(0x16, ClassicMad.ComputeCrc(new byte[ClassicMad.Mad2Length]));
        }

        [Fact]
        public void TheCrcOfAMadWithOneAid_IsTheOneARealChipHad()
        {
            // Info byte 03, the AID 1234 stored as 34 12 in the entry of sector 3: the library wrote this and read it back as valid.
            Assert.Equal(0xD4, ClassicMad.ComputeCrc(Mad1WithAid1234InSector3()));
        }

        [Fact]
        public void TheMadOfANdefFormattedChip_HasTheCrcThatNdefToolsWrite()
        {
            // The MAD1 that NFC Forum NDEF formatting writes: info byte 01, every sector 1 to 15 owned by E103 (stored 03 E1).
            var mad = ClassicMad.Create(ClassicMadVersion.Mad1);

            mad.InfoByte1 = 0x01;

            for (var sector = 1; sector <= 15; sector++)
            {
                mad.SetAid(sector, ClassicMadAid.NdefApplication);
            }

            var bytes = mad.ToMad1Bytes();

            Assert.Equal("1401" + string.Concat(Enumerable.Repeat("03E1", 15)), Convert.ToHexString(bytes));
        }

        [Fact]
        public void TheCrcIgnoresItsOwnByte_AndSeesEveryOther()
        {
            var bytes = Mad1WithAid1234InSector3();
            var crc = ClassicMad.ComputeCrc(bytes);

            bytes[0] = 0xAA;
            Assert.Equal(crc, ClassicMad.ComputeCrc(bytes));

            for (var i = 1; i < bytes.Length; i++)
            {
                var changed = Mad1WithAid1234InSector3();

                changed[i] ^= 0x01;
                Assert.NotEqual(crc, ClassicMad.ComputeCrc(changed));
            }
        }

        [Fact]
        public void AnAid_IsStoredWithItsLowByteFirst_InMad1AndInMad2()
        {
            var mad = ClassicMad.Create(ClassicMadVersion.Mad2);

            mad.SetAid(3, 0x1234);
            mad.SetAid(20, ClassicMadAid.NdefApplication);

            var mad1 = mad.ToMad1Bytes();
            var mad2 = mad.ToMad2Bytes();

            // The same bytes that the real chip held for the AID 1234 in sector 3.
            Assert.Equal(0x34, mad1[6]);
            Assert.Equal(0x12, mad1[7]);

            // Sector 20 is the entry 4 of MAD2; E103 is stored as 03 E1, as an NDEF card does.
            Assert.Equal(0x03, mad2[8]);
            Assert.Equal(0xE1, mad2[9]);
        }

        [Fact]
        public void TheBytesOfAMad_CarryTheRightCrc_AndComeBackAsTheSameDirectory()
        {
            var mad = ClassicMad.Create(ClassicMadVersion.Mad2);

            mad.InfoByte1 = 0x03;
            mad.InfoByte2 = 0x11;
            mad.SetAid(1, 0xE103);
            mad.SetAid(15, 0x4A01);
            mad.SetAid(17, 0xE103);
            mad.SetAid(39, 0x0002);

            var mad1 = mad.ToMad1Bytes();
            var mad2 = mad.ToMad2Bytes();

            Assert.Equal(ClassicMad.ComputeCrc(mad1), mad1[0]);
            Assert.Equal(ClassicMad.ComputeCrc(mad2), mad2[0]);
            Assert.Equal(ClassicMad.Mad1Length, mad1.Length);
            Assert.Equal(ClassicMad.Mad2Length, mad2.Length);

            Assert.True(ClassicMad.TryParse(mad1, mad2, out var back));
            Assert.Equal(ClassicMadVersion.Mad2, back.Version);
            Assert.Equal(ClassicMadProblems.None, back.Problems);
            Assert.Equal(0x03, back.InfoByte1);
            Assert.Equal(0x11, back.InfoByte2);

            foreach (var sector in Enumerable.Range(1, 39).Where(back.Covers))
            {
                Assert.Equal(mad.GetAid(sector), back.GetAid(sector));
            }
        }

        [Fact]
        public void AMadOfTheRealChip_IsReadAsItWasWritten()
        {
            var bytes = Mad1WithAid1234InSector3();

            bytes[0] = 0xD4;

            Assert.True(ClassicMad.TryParse(bytes, ReadOnlySpan<byte>.Empty, out var mad));
            Assert.Equal(ClassicMadVersion.Mad1, mad.Version);
            Assert.Equal(ClassicMadProblems.None, mad.Problems);
            Assert.Equal(0x1234, mad.GetAid(3));
            Assert.Equal(3, mad.FindFirstSector(0x1234));
        }

        [Fact]
        public void AWrongCrc_IsReportedPerPart_AndTheDirectoryIsStillRead()
        {
            var good = ClassicMad.Create(ClassicMadVersion.Mad2);

            good.SetAid(2, 0x0102);
            good.SetAid(18, 0x0304);

            var mad1 = good.ToMad1Bytes();
            var mad2 = good.ToMad2Bytes();

            var bad1 = (byte[])mad1.Clone();
            bad1[0] ^= 0xFF;

            Assert.True(ClassicMad.TryParse(bad1, mad2, out var first));
            Assert.Equal(ClassicMadProblems.Mad1CrcMismatch, first.Problems);
            Assert.Equal(0x0102, first.GetAid(2));

            var bad2 = (byte[])mad2.Clone();
            bad2[5] ^= 0x01;

            Assert.True(ClassicMad.TryParse(mad1, bad2, out var second));
            Assert.Equal(ClassicMadProblems.Mad2CrcMismatch, second.Problems);

            Assert.True(ClassicMad.TryParse(new byte[32], new byte[48], out var both));
            Assert.Equal(ClassicMadProblems.Mad1CrcMismatch | ClassicMadProblems.Mad2CrcMismatch, both.Problems);
        }

        [Theory]
        [InlineData(31, 0)]
        [InlineData(33, 0)]
        [InlineData(0, 0)]
        [InlineData(32, 47)]
        [InlineData(32, 49)]
        [InlineData(32, 32)]
        public void BytesThatAreNoMad_AreRefused(int mad1Length, int mad2Length)
        {
            Assert.False(ClassicMad.TryParse(new byte[mad1Length], new byte[mad2Length], out var mad));
            Assert.Null(mad);
        }

        [Fact]
        public void OnlyTheSectorsThatHaveAnEntry_CanBeAddressed()
        {
            var mad1 = ClassicMad.Create(ClassicMadVersion.Mad1);
            var mad2 = ClassicMad.Create(ClassicMadVersion.Mad2);

            foreach (var sector in new[] { 0, 16, 40, -1 })
            {
                Assert.False(mad2.Covers(sector));
                Assert.Throws<ArgumentOutOfRangeException>("sector", () => mad2.GetAid(sector));
                Assert.Throws<ArgumentOutOfRangeException>("sector", () => mad2.SetAid(sector, 1));
            }

            Assert.True(mad1.Covers(1));
            Assert.True(mad1.Covers(15));
            Assert.False(mad1.Covers(17));
            Assert.Throws<ArgumentOutOfRangeException>("sector", () => mad1.SetAid(17, 1));
            Assert.True(mad2.Covers(17));
            Assert.True(mad2.Covers(39));
            Assert.Equal(15, Enumerable.Range(0, 40).Count(mad1.Covers));
            Assert.Equal(38, Enumerable.Range(0, 40).Count(mad2.Covers));
        }

        [Fact]
        public void AnAid_IsFoundInEverySectorThatCarriesIt()
        {
            var mad = ClassicMad.Create(ClassicMadVersion.Mad2);

            mad.SetAid(5, 0xE103);
            mad.SetAid(9, 0x0001);
            mad.SetAid(6, 0xE103);
            mad.SetAid(30, 0xE103);

            Assert.Equal(new[] { 5, 6, 30 }, mad.FindSectors(0xE103));
            Assert.Equal(5, mad.FindFirstSector(0xE103));
            Assert.Empty(mad.FindSectors(0x9999));
            Assert.Null(mad.FindFirstSector(0x9999));

            // Every entry that nobody set is free: 15 of MAD1 and 23 of MAD2, less the 4 that carry an AID.
            Assert.Equal(34, mad.FindSectors(ClassicMadAid.Free).Count);

            mad.SetAid(5, ClassicMadAid.Free);
            Assert.Equal(new[] { 6, 30 }, mad.FindSectors(0xE103));
        }

        [Fact]
        public void AMad1_HasNoSecondPart()
        {
            Assert.Throws<InvalidOperationException>(() => ClassicMad.Create(ClassicMadVersion.Mad1).ToMad2Bytes());
            Assert.Throws<ArgumentOutOfRangeException>("version", () => ClassicMad.Create((ClassicMadVersion)0));
        }

        [Fact]
        public void TheInfoByteOfMad2_IsOnlyWrittenWithMad2()
        {
            var mad = ClassicMad.Create(ClassicMadVersion.Mad2);

            mad.InfoByte1 = 0x05;
            mad.InfoByte2 = 0x12;

            Assert.Equal(0x05, mad.ToMad1Bytes()[1]);
            Assert.Equal(0x12, mad.ToMad2Bytes()[1]);
        }

        [Fact]
        public void AnAid_IsMadeOfItsFunctionClusterAndItsApplicationCode()
        {
            Assert.Equal(ClassicMadAid.NdefApplication, ClassicMadAid.Create(0xE1, 0x03));
            Assert.Equal(0xE1, ClassicMadAid.FunctionCluster(0xE103));
            Assert.Equal(0x03, ClassicMadAid.Application(0xE103));
            Assert.Equal(0x0000, ClassicMadAid.Free);
            Assert.Equal(0x0005, ClassicMadAid.NotApplicable);
        }

        [Theory]
        [InlineData(ClassicMadVersion.Mad1, true, 0xC1)]
        [InlineData(ClassicMadVersion.Mad2, true, 0xC2)]
        [InlineData(ClassicMadVersion.Mad1, false, 0x81)]
        [InlineData(ClassicMadVersion.Mad2, false, 0x82)]
        public void TheGeneralPurposeByteOfAMadSector_IsDaMaAndTheVersion(ClassicMadVersion version, bool multi, int expected)
        {
            var gpb = ClassicMadGpb.Create(version, multi);

            Assert.Equal(expected, gpb);
            Assert.True(ClassicMadGpb.TryParse(gpb, out var info));
            Assert.Equal(new ClassicMadGpbInfo(true, multi, version), info);
        }

        [Theory]
        [InlineData(0x69)] // a chip as it leaves the factory
        [InlineData(0xC3)] // the default of LibLogicalAccess: version bits 11
        [InlineData(0x00)]
        [InlineData(0xBC)]
        [InlineData(0xC5)] // a reserved bit
        public void AByteThatIsNoMadByte_IsRefused(int gpb)
        {
            Assert.False(ClassicMadGpb.TryParse((byte)gpb, out var info));
            Assert.Equal(default, info);
        }

        [Fact]
        public void ADirectoryWithoutTheAvailableBit_IsStillReadAsOne()
        {
            Assert.True(ClassicMadGpb.TryParse(0x41, out var info));
            Assert.False(info.MadAvailable);
            Assert.True(info.MultiApplication);
            Assert.Equal(ClassicMadVersion.Mad1, info.Version);
        }

        [Fact]
        public void TheDefaultsOfTheMadSectors_AreTheDocumentedBlocksAndKeys()
        {
            Assert.Equal(1, ClassicMad.Mad1FirstBlock);
            Assert.Equal(3, ClassicMad.Mad1TrailerBlock);
            Assert.Equal(ClassicMad.Mad2FirstBlock, ClassicLayout.FirstBlock(ClassicMad.Mad2Sector));
            Assert.Equal(ClassicMad.Mad2TrailerBlock, ClassicLayout.TrailerBlock(ClassicMad.Mad2Sector));
            Assert.Equal("A0A1A2A3A4A5", ClassicMad.DefaultKeyA);
            Assert.Equal("D3F7D3F7D3F7", ClassicMad.NfcForumKeyA);
        }
    }
}
