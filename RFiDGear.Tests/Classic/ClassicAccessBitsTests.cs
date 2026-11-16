using System;
using System.Collections.Generic;
using System.Linq;
using RFiDGear.Infrastructure.Classic;
using Xunit;

namespace RFiDGear.Tests.Classic
{
    /// <summary>
    /// The access bits decide who may read and write what, and a trailer with wrong ones locks a sector for good. The rights tables are
    /// transcribed from the NXP data sheet (the rows are named by C1 C2 C3), the byte vectors are those of real chips, and the bits of
    /// every possible combination are round-tripped.
    /// </summary>
    public class ClassicAccessBitsTests
    {
        private static readonly byte[] Ff6 = Enumerable.Repeat((byte)0xFF, 6).ToArray();

        // "C1C2C3" as the data sheet writes it -> the condition number of the code.
        private static int Condition(string c1c2c3) => (c1c2c3[0] == '1' ? 1 : 0) + (c1c2c3[1] == '1' ? 2 : 0) + (c1c2c3[2] == '1' ? 4 : 0);

        private static ClassicKeyAccess Access(string text) => text switch
        {
            "-" => ClassicKeyAccess.None,
            "A" => ClassicKeyAccess.KeyA,
            "B" => ClassicKeyAccess.KeyB,
            "AB" => ClassicKeyAccess.KeyAOrB,
            _ => throw new ArgumentException(text)
        };

        private static ClassicAccessBits WithData(string c1c2c3) =>
            new ClassicAccessBits(Condition(c1c2c3), 0, 0, 4);

        private static ClassicAccessBits WithTrailer(string c1c2c3) =>
            new ClassicAccessBits(0, 0, 0, Condition(c1c2c3));

        public static IEnumerable<object[]> DataTable() => new[]
        {
            //            C1C2C3 read  write inc   dec
            new object[] { "000", "AB", "AB", "AB", "AB" },
            new object[] { "010", "AB", "-", "-", "-" },
            new object[] { "100", "AB", "B", "-", "-" },
            new object[] { "110", "AB", "B", "B", "AB" },
            new object[] { "001", "AB", "-", "-", "AB" },
            new object[] { "011", "B", "B", "-", "-" },
            new object[] { "101", "B", "-", "-", "-" },
            new object[] { "111", "-", "-", "-", "-" }
        };

        public static IEnumerable<object[]> TrailerTable() => new[]
        {
            //            C1C2C3 keyA-w acR  acW  keyB-r keyB-w
            new object[] { "000", "A", "A", "-", "A", "A" },
            new object[] { "010", "-", "A", "-", "A", "-" },
            new object[] { "100", "B", "AB", "-", "-", "B" },
            new object[] { "110", "-", "AB", "-", "-", "-" },
            new object[] { "001", "A", "A", "A", "A", "A" },
            new object[] { "011", "B", "AB", "B", "-", "B" },
            new object[] { "101", "-", "AB", "B", "-", "-" },
            new object[] { "111", "-", "AB", "-", "-", "-" }
        };

        [Theory]
        [MemberData(nameof(DataTable))]
        public void ADataCondition_GrantsWhatTheDataSheetSays(string c1c2c3, string read, string write, string increment, string decrement)
        {
            var bits = WithData(c1c2c3);

            // The same condition in each of the three groups.
            for (var group = 0; group < 3; group++)
            {
                var rights = new ClassicAccessBits(
                        group == 0 ? Condition(c1c2c3) : 0,
                        group == 1 ? Condition(c1c2c3) : 0,
                        group == 2 ? Condition(c1c2c3) : 0,
                        4)
                    .GetDataBlockRights(group);

                Assert.Equal(Access(read), rights.Read);
                Assert.Equal(Access(write), rights.Write);
                Assert.Equal(Access(increment), rights.Increment);
                Assert.Equal(Access(decrement), rights.DecrementTransferRestore);
            }

            Assert.Equal(Condition(c1c2c3), bits.GetCondition(0));
        }

        [Theory]
        [MemberData(nameof(TrailerTable))]
        public void ATrailerCondition_GrantsWhatTheDataSheetSays(string c1c2c3, string keyAWrite, string acRead, string acWrite, string keyBRead, string keyBWrite)
        {
            var rights = WithTrailer(c1c2c3).TrailerRights;

            Assert.Equal(Access(keyAWrite), rights.KeyAWrite);
            Assert.Equal(Access(acRead), rights.AccessBitsRead);
            Assert.Equal(Access(acWrite), rights.AccessBitsWrite);
            Assert.Equal(Access(keyBRead), rights.KeyBRead);
            Assert.Equal(Access(keyBWrite), rights.KeyBWrite);
        }

        [Fact]
        public void TheFactoryAndTheMadAccessBits_AreTheBytesOfRealChips()
        {
            Assert.Equal("FF0780", ClassicAccessBits.Transport.ToString());
            Assert.Equal("787788", ClassicAccessBits.MadStandard.ToString());

            // NFC Forum: data blocks 000, trailer 011.
            Assert.Equal("7F0788", new ClassicAccessBits(0, 0, 0, 6).ToString());

            // Everything 111.
            Assert.Equal("00F0FF", new ClassicAccessBits(7, 7, 7, 7).ToString());
        }

        [Theory]
        [InlineData("000000000000FF078069FFFFFFFFFFFF")] // sectors 0 to 38 of a factory 4K, read from one
        [InlineData("000000000000FF0780BCFFFFFFFFFFFF")] // sector 39 of the same chip
        public void ATrailerOfAFactoryChip_IsTheTransportCondition(string trailerHex)
        {
            Assert.True(ClassicAccessBits.TryFromTrailer(Convert.FromHexString(trailerHex), out var bits));
            Assert.Equal(ClassicAccessBits.Transport, bits);
        }

        [Fact]
        public void AMadTrailer_IsTheMadCondition()
        {
            Assert.True(ClassicAccessBits.TryFromTrailer(Convert.FromHexString("000000000000787788C1000000000000"), out var bits));
            Assert.Equal(ClassicAccessBits.MadStandard, bits);
        }

        [Fact]
        public void EveryCombination_SurvivesTheBytes()
        {
            for (var a = 0; a < 8; a++)
            {
                for (var b = 0; b < 8; b++)
                {
                    for (var c = 0; c < 8; c++)
                    {
                        for (var t = 0; t < 8; t++)
                        {
                            var bits = new ClassicAccessBits(a, b, c, t);

                            Assert.True(ClassicAccessBits.TryFromBytes(bits.ToBytes(), out var back));
                            Assert.Equal(bits, back);
                        }
                    }
                }
            }
        }

        [Fact]
        public void ABitThatIsNotTheInverseOfItsCopy_IsRefused()
        {
            foreach (var bits in new[] { ClassicAccessBits.Transport, ClassicAccessBits.MadStandard, new ClassicAccessBits(3, 5, 1, 2) })
            {
                for (var bit = 0; bit < 24; bit++)
                {
                    var bytes = bits.ToBytes();

                    bytes[bit / 8] ^= (byte)(1 << (bit % 8));

                    Assert.False(ClassicAccessBits.TryFromBytes(bytes, out var parsed));
                    Assert.Null(parsed);
                }
            }
        }

        [Fact]
        public void TheWrongNumberOfBytes_IsRefused()
        {
            Assert.False(ClassicAccessBits.TryFromBytes(new byte[] { 0xFF, 0x07 }, out _));
            Assert.False(ClassicAccessBits.TryFromBytes(new byte[] { 0xFF, 0x07, 0x80, 0x69 }, out _));
            Assert.False(ClassicAccessBits.TryFromTrailer(new byte[15], out _));
            Assert.False(ClassicAccessBits.TryFromTrailer(new byte[17], out _));
        }

        [Fact]
        public void AConditionThatDoesNotExist_IsRefused()
        {
            Assert.Throws<ArgumentOutOfRangeException>("dataGroup0", () => new ClassicAccessBits(8, 0, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>("dataGroup1", () => new ClassicAccessBits(0, -1, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>("dataGroup2", () => new ClassicAccessBits(0, 0, 8, 0));
            Assert.Throws<ArgumentOutOfRangeException>("trailer", () => new ClassicAccessBits(0, 0, 0, 8));
            Assert.Throws<ArgumentOutOfRangeException>(() => ClassicAccessBits.Transport.GetCondition(4));
            Assert.Throws<ArgumentOutOfRangeException>(() => ClassicAccessBits.Transport.GetDataBlockRights(3));
            Assert.Throws<ArgumentOutOfRangeException>(() => ClassicAccessBits.Transport.GetDataBlockRights(-1));
        }

        [Theory]
        [InlineData("000", true)]
        [InlineData("010", true)]
        [InlineData("001", true)]
        [InlineData("100", false)]
        [InlineData("110", false)]
        [InlineData("011", false)]
        [InlineData("101", false)]
        [InlineData("111", false)]
        public void KeyBIsData_WhileAKeyMayReadIt(string trailer, bool keyBIsData)
        {
            var bits = WithTrailer(trailer);

            Assert.Equal(keyBIsData, bits.KeyBIsData);
            Assert.Equal(!keyBIsData, bits.AllowsLoginWith(ClassicKeyType.KeyB));
            Assert.True(bits.AllowsLoginWith(ClassicKeyType.KeyA));
        }

        [Theory]
        [InlineData("000", true, false)]
        [InlineData("010", true, true)]
        [InlineData("100", true, false)]
        [InlineData("110", true, true)]
        [InlineData("001", false, false)]
        [InlineData("011", false, false)]
        [InlineData("101", false, false)]
        [InlineData("111", true, true)]
        public void TheAccessBitsAreFinal_WhenNobodyMayWriteThem_AndTheTrailerIsFrozenWhenNobodyMayWriteAnything(string trailer, bool final, bool frozen)
        {
            var bits = WithTrailer(trailer);

            Assert.Equal(final, bits.AccessBitsAreFinal);
            Assert.Equal(frozen, bits.TrailerIsFrozen);
        }

        [Fact]
        public void TheKeyForAnOperation_IsKeyAFirst_AndKeyBOnlyWhenItCanLogIn()
        {
            var transport = ClassicAccessBits.Transport;
            var mad = ClassicAccessBits.MadStandard;

            // Factory chip: both keys may write, but key B is data, so only key A can.
            Assert.True(transport.TryChooseKey(transport.GetDataBlockRights(0).Write, true, true, out var key));
            Assert.Equal(ClassicKeyType.KeyA, key);
            Assert.False(transport.TryChooseKey(transport.GetDataBlockRights(0).Write, false, true, out _));

            // MAD sector: key A reads, only key B writes.
            Assert.True(mad.TryChooseKey(mad.GetDataBlockRights(0).Read, true, true, out key));
            Assert.Equal(ClassicKeyType.KeyA, key);
            Assert.False(mad.TryChooseKey(mad.GetDataBlockRights(0).Write, true, false, out _));
            Assert.True(mad.TryChooseKey(mad.GetDataBlockRights(0).Write, true, true, out key));
            Assert.Equal(ClassicKeyType.KeyB, key);
            Assert.True(mad.TryChooseKey(mad.GetDataBlockRights(0).Read, false, true, out key));
            Assert.Equal(ClassicKeyType.KeyB, key);

            // Nobody may do it.
            Assert.False(new ClassicAccessBits(7, 7, 7, 4).TryChooseKey(ClassicKeyAccess.None, true, true, out _));
        }

        [Fact]
        public void ATrailerIsBuiltFromTheKeysTheBitsAndTheGeneralPurposeByte()
        {
            var trailer = ClassicAccessBits.Transport.BuildTrailer(Ff6, 0x69, Ff6);

            Assert.Equal("FFFFFFFFFFFFFF078069FFFFFFFFFFFF", Convert.ToHexString(trailer));

            var madTrailer = ClassicAccessBits.MadStandard.BuildTrailer(
                Convert.FromHexString("A0A1A2A3A4A5"),
                0xC1,
                Convert.FromHexString("B0B1B2B3B4B5"));

            Assert.Equal("A0A1A2A3A4A5787788C1B0B1B2B3B4B5", Convert.ToHexString(madTrailer));
            Assert.True(ClassicAccessBits.TryFromTrailer(madTrailer, out var parsed));
            Assert.Equal(ClassicAccessBits.MadStandard, parsed);
        }

        [Fact]
        public void AKeyThatIsNotSixBytes_IsRefused()
        {
            Assert.Throws<ArgumentException>("keyA", () => ClassicAccessBits.Transport.BuildTrailer(new byte[5], 0, Ff6));
            Assert.Throws<ArgumentException>("keyB", () => ClassicAccessBits.Transport.BuildTrailer(Ff6, 0, new byte[7]));
        }

        [Fact]
        public void TwoAccessBitsAreEqual_WhenAllFourConditionsAre()
        {
            var a = new ClassicAccessBits(1, 2, 3, 4);
            var same = new ClassicAccessBits(1, 2, 3, 4);

            Assert.True(a == same);
            Assert.True(a.Equals((object)same));
            Assert.Equal(a.GetHashCode(), same.GetHashCode());

            foreach (var other in new[]
                     {
                         new ClassicAccessBits(0, 2, 3, 4),
                         new ClassicAccessBits(1, 0, 3, 4),
                         new ClassicAccessBits(1, 2, 0, 4),
                         new ClassicAccessBits(1, 2, 3, 0)
                     })
            {
                Assert.True(a != other);
                Assert.False(a.Equals(other));
            }

            Assert.False(a.Equals(null));
            Assert.False(a == null);
            Assert.True(a != null);
        }
    }
}
