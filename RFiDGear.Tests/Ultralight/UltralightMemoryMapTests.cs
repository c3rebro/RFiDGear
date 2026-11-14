using System;
using System.Collections.Generic;
using System.Linq;
using RFiDGear.Infrastructure.Ultralight;
using Xunit;

namespace RFiDGear.Tests.Ultralight
{
    /// <summary>
    /// The layouts are what keeps a write away from the lock, configuration, password and key pages, so each row is pinned. The
    /// values are those of the NXP data sheets; the NTAG215 row was also read from a card.
    /// </summary>
    public class UltralightMemoryMapTests
    {
        public static IEnumerable<object[]> Layouts() => new[]
        {
            new object[] { UltralightVariant.Plain, 16, 15, "0-3" },
            new object[] { UltralightVariant.C, 48, 39, "0-3,40-47" },
            new object[] { UltralightVariant.Ev1Mf0ul11, 20, 15, "0-3,16-19" },
            new object[] { UltralightVariant.Ev1Mf0ul21, 41, 35, "0-3,36-40" },
            new object[] { UltralightVariant.Ntag213, 45, 39, "0-3,40-44" },
            new object[] { UltralightVariant.Ntag215, 135, 129, "0-3,130-134" },
            new object[] { UltralightVariant.Ntag216, 231, 225, "0-3,226-230" }
        };

        // 0,1,2,3,40,41 -> "0-3,40-41"
        private static string Describe(IEnumerable<int> pages)
        {
            var ranges = new List<string>();
            int? first = null;
            int? last = null;

            foreach (var page in pages.OrderBy(p => p))
            {
                if (last.HasValue && page == last.Value + 1)
                {
                    last = page;

                    continue;
                }

                if (first.HasValue)
                {
                    ranges.Add(first == last ? first.ToString() : first + "-" + last);
                }

                first = page;
                last = page;
            }

            if (first.HasValue)
            {
                ranges.Add(first == last ? first.ToString() : first + "-" + last);
            }

            return string.Join(",", ranges);
        }

        [Theory]
        [MemberData(nameof(Layouts))]
        public void EveryVariant_HasTheLayoutOfItsDataSheet(UltralightVariant variant, int pageCount, int lastUserPage, string protectedPages)
        {
            var map = UltralightMemoryMap.For(variant);

            Assert.Equal(variant, map.Variant);
            Assert.Equal(pageCount, map.PageCount);
            Assert.Equal(lastUserPage, map.LastUserPage);
            Assert.Equal(lastUserPage - 4 + 1, map.UserPageCount);

            // Looked at from outside the chip as well: nothing before page 0 or after the last page is anything.
            var around = Enumerable.Range(-3, pageCount + 6).ToList();
            Assert.Equal(protectedPages, Describe(around.Where(map.IsProtectedPage)));
            Assert.Equal(map.ProtectedPageCount, around.Count(map.IsProtectedPage));
            Assert.Equal("4-" + lastUserPage, Describe(around.Where(map.IsUserPage)));
            Assert.Equal("0-" + (pageCount - 1), Describe(around.Where(map.Contains)));
        }

        [Fact]
        public void EveryVariantOfTheEnum_IsInTheTable_AndNothingElseHasALayout()
        {
            var variants = Enum.GetValues(typeof(UltralightVariant)).Cast<UltralightVariant>().ToList();

            Assert.Equal(Layouts().Count(), variants.Count);
            Assert.All(variants, variant => Assert.Equal(variant, UltralightMemoryMap.For(variant).Variant));
            Assert.Throws<ArgumentOutOfRangeException>(() => UltralightMemoryMap.For((UltralightVariant)0));
            Assert.Throws<ArgumentOutOfRangeException>(() => UltralightMemoryMap.For((UltralightVariant)99));
        }

        [Fact]
        public void TheBiggestLayout_DefinesTheLimitNoChipHas()
        {
            Assert.Equal(
                UltralightMemoryMap.MaxPageCount,
                Enum.GetValues(typeof(UltralightVariant)).Cast<UltralightVariant>().Max(variant => UltralightMemoryMap.For(variant).PageCount));
        }

        [Theory]
        [InlineData(0, 135, true)]
        [InlineData(134, 1, true)]
        [InlineData(4, 126, true)]
        [InlineData(0, 136, false)]
        [InlineData(134, 2, false)]
        [InlineData(135, 1, false)]
        [InlineData(0, 0, false)]
        [InlineData(5, -1, false)]
        [InlineData(-1, 2, false)]
        [InlineData(int.MaxValue, 2, false)]
        [InlineData(1, int.MaxValue, false)]
        public void ARange_IsInTheChipOnlyWhenEveryPageIs_AndNeverWrapsAround(int startPage, int pageCount, bool expected)
        {
            Assert.Equal(expected, UltralightMemoryMap.For(UltralightVariant.Ntag215).ContainsRange(startPage, pageCount));
        }

        public static IEnumerable<object[]> GetVersionAnswers() => new[]
        {
            // The NTAG215 that was on the bench: product type 0x04 (NTAG), storage size 0x11.
            new object[] { "0004040201001103", UltralightVariant.Ntag215 },
            new object[] { "00040402010 00F03".Replace(" ", string.Empty), UltralightVariant.Ntag213 },
            new object[] { "0004040201001303", UltralightVariant.Ntag216 },

            // Ultralight EV1 (product type 0x03), with the 17 pF and the 50 pF product subtype.
            new object[] { "0004030101000B03", UltralightVariant.Ev1Mf0ul11 },
            new object[] { "0004030201000B03", UltralightVariant.Ev1Mf0ul11 },
            new object[] { "0004030101000E03", UltralightVariant.Ev1Mf0ul21 },
            new object[] { "0004030201000E03", UltralightVariant.Ev1Mf0ul21 }
        };

        [Theory]
        [MemberData(nameof(GetVersionAnswers))]
        public void TheAnswerToGetVersion_NamesTheChip_ByProductTypeAndStorageSize(string answer, UltralightVariant expected)
        {
            Assert.True(UltralightMemoryMap.TryFromGetVersion(Convert.FromHexString(answer), out var map));
            Assert.Equal(expected, map.Variant);
        }

        [Theory]
        [InlineData("0004040101000B03")]   // NTAG210: same family, layout not confirmed
        [InlineData("0004040101000E03")]   // NTAG212: same family, layout not confirmed
        [InlineData("0004050201001103")]   // a product type that is neither Ultralight EV1 nor NTAG
        [InlineData("0004040201001203")]   // a storage size no known chip has
        [InlineData("000404FFFFFF1303")]   // NTAG216 size, but a subtype, major and minor version that no known chip has
        [InlineData("0004040101001103")]   // NTAG with the subtype of the 17 pF chips: no NTAG215 has it
        [InlineData("0004040301001103")]   // an unknown NTAG subtype
        [InlineData("0004040202001103")]   // a major version other than 1
        [InlineData("00040402FF001103")]
        [InlineData("0004040201011103")]   // a minor version other than 0
        [InlineData("000403FF01000B03")]   // an Ultralight EV1 with a subtype other than 17 pF and 50 pF
        [InlineData("0004030302000B03")]
        [InlineData("0004030102000B03")]   // an Ultralight EV1 with a major version other than 1
        [InlineData("0004030101010E03")]   // an Ultralight EV1 with a minor version other than 0
        [InlineData("0004040201001102")]   // a protocol type that is not ISO 14443-3
        [InlineData("0104040201001103")]   // a header byte that is not 0
        [InlineData("0005040201001103")]   // another vendor than NXP
        [InlineData("00040402010011")]     // 7 bytes
        [InlineData("0004040201001103FF")] // 9 bytes
        [InlineData("")]
        public void AnAnswerThatNamesNoKnownChip_IsRefused_NotGuessedAt(string answer)
        {
            Assert.False(UltralightMemoryMap.TryFromGetVersion(Convert.FromHexString(answer), out var map));
            Assert.Null(map);
        }

        [Fact]
        public void NoAnswer_IsRefused()
        {
            Assert.False(UltralightMemoryMap.TryFromGetVersion(null, out var map));
            Assert.Null(map);
        }
    }
}
