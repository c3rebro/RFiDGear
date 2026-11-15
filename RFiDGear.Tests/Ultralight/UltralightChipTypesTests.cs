using System;
using System.Collections.Generic;
using System.Linq;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.Ultralight;
using Xunit;

namespace RFiDGear.Tests.Ultralight
{
    /// <summary>
    /// Which chips of the application's chip types are chips of the Ultralight family, and what layout each has. The chip tree shows
    /// the pages of a chip from this, and the reader layer reads them with it: a chip type that has no layout here has no pages in the
    /// tree (it is not guessed at), and the chips that are no Type 2 tags (an NTAG 424 DNA, a DESFire) are not of the family at all.
    /// </summary>
    public class UltralightChipTypesTests
    {
        public static IEnumerable<object[]> TheChipsWithALayout() => new[]
        {
            new object[] { CARD_TYPE.MifareUltralight, UltralightVariant.Plain, UltralightExpectation.Plain },
            new object[] { CARD_TYPE.MifareUltralightC, UltralightVariant.C, UltralightExpectation.C },
            new object[] { CARD_TYPE.MifareUltralight_EV1_MF0UL11, UltralightVariant.Ev1Mf0ul11, UltralightExpectation.Ev1Mf0ul11 },
            new object[] { CARD_TYPE.MifareUltralight_EV1_MF0UL21, UltralightVariant.Ev1Mf0ul21, UltralightExpectation.Ev1Mf0ul21 },
            new object[] { CARD_TYPE.NTAG_213, UltralightVariant.Ntag213, UltralightExpectation.Ntag213 },
            new object[] { CARD_TYPE.NTAG_215, UltralightVariant.Ntag215, UltralightExpectation.Ntag215 },
            new object[] { CARD_TYPE.NTAG_216, UltralightVariant.Ntag216, UltralightExpectation.Ntag216 },
        };

        // Of the family, and without a layout that is confirmed: an EV1 of another size, and the NTAGs whose layouts are not in the table.
        public static IEnumerable<object[]> TheChipsWithoutALayout() => new[]
        {
            new object[] { CARD_TYPE.MifareUltralight_EV1 },
            new object[] { CARD_TYPE.NTAG_210 },
            new object[] { CARD_TYPE.NTAG_211 },
            new object[] { CARD_TYPE.NTAG_212 },
            new object[] { CARD_TYPE.NTAG_214 },
        };

        public static IEnumerable<object[]> TheChipsOfOtherFamilies() => new[]
        {
            new object[] { CARD_TYPE.NTAG_424 },
            new object[] { CARD_TYPE.NTAG_426 },
            new object[] { CARD_TYPE.Mifare1K },
            new object[] { CARD_TYPE.Mifare4K },
            new object[] { CARD_TYPE.MifareClassic },
            new object[] { CARD_TYPE.DESFireEV1 },
            new object[] { CARD_TYPE.EM4102 },
            new object[] { CARD_TYPE.NOTAG },
            new object[] { CARD_TYPE.Unspecified },
        };

        [Theory]
        [MemberData(nameof(TheChipsWithALayout))]
        public void AChipWithALayout_IsOfTheFamily_AndHasTheLayoutOfItsVariant(CARD_TYPE type, UltralightVariant variant, UltralightExpectation expectation)
        {
            Assert.True(UltralightChipTypes.IsOfTheFamily(type));
            Assert.True(UltralightChipTypes.TryGetVariant(type, out var found));
            Assert.Equal(variant, found);
            Assert.Equal(expectation, UltralightChipTypes.ExpectationOf(type));
        }

        [Theory]
        [MemberData(nameof(TheChipsWithoutALayout))]
        public void AChipOfTheFamilyWithoutALayout_HasNoPages_AndAnyChipIsExpected(CARD_TYPE type)
        {
            Assert.True(UltralightChipTypes.IsOfTheFamily(type));
            Assert.False(UltralightChipTypes.TryGetVariant(type, out _));
            Assert.Equal(UltralightExpectation.Auto, UltralightChipTypes.ExpectationOf(type));
        }

        [Theory]
        [MemberData(nameof(TheChipsOfOtherFamilies))]
        public void AChipOfAnotherFamily_IsNotOfTheFamily(CARD_TYPE type)
        {
            Assert.False(UltralightChipTypes.IsOfTheFamily(type));
            Assert.False(UltralightChipTypes.TryGetVariant(type, out _));
            Assert.Equal(UltralightExpectation.Auto, UltralightChipTypes.ExpectationOf(type));
        }

        [Fact]
        public void EveryVariantOfTheTable_IsTheLayoutOfOneChipType()
        {
            // A variant that no chip type leads to would have a layout nobody can see.
            var reached = TheChipsWithALayout().Select(row => (UltralightVariant)row[1]).OrderBy(variant => (int)variant);

            Assert.Equal(Enum.GetValues(typeof(UltralightVariant)).Cast<UltralightVariant>().OrderBy(variant => (int)variant), reached);
        }

        [Fact]
        public void ANumberThatIsNoChipType_IsNotOfTheFamily()
        {
            Assert.False(UltralightChipTypes.IsOfTheFamily((CARD_TYPE)0x9F00));
            Assert.False(UltralightChipTypes.IsOfTheFamily((CARD_TYPE)0x9500));
            Assert.False(UltralightChipTypes.IsOfTheFamily((CARD_TYPE)0xA700));
            Assert.False(UltralightChipTypes.TryGetVariant((CARD_TYPE)0x9F00, out _));
        }
    }
}
