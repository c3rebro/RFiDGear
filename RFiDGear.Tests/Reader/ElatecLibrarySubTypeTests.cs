using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Elatec.NET;
using Elatec.NET.Cards.Mifare;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.Ultralight;
using RFiDGear.Tests.SecureKeys;
using Xunit;

namespace RFiDGear.Tests.Reader
{
    /// <summary>
    /// What Elatec.NET 0.7.0-beta.2 reports for the chips of the Ultralight family, and what the application does with it. Since that
    /// version the library asks an Ultralight-class chip for GET_VERSION itself and reports an NTAG or an Ultralight EV1 as such
    /// (the values 0x92 to 0x94 and 0xA0 to 0xA6) instead of as MifareUltralight; an Ultralight C is still reported as
    /// MifareUltralight.
    /// </summary>
    public class ElatecLibrarySubTypeTests
    {
        // The sub types of the family: 0x90-0x9F (Ultralight) and 0xA0-0xAF (NTAG).
        private static bool IsInTheFamilyRanges(int value) => (value >= 0x90 && value <= 0x9F) || (value >= 0xA0 && value <= 0xAF);

        public static IEnumerable<object[]> TheSubTypesOfTheFamily() =>
            Enum.GetValues(typeof(MifareChipSubType)).Cast<MifareChipSubType>()
                .Where(subType => IsInTheFamilyRanges((int)subType))
                .Distinct()
                .Select(subType => new object[] { subType });

        [Theory]
        [MemberData(nameof(TheSubTypesOfTheFamily))]
        public void EverySubTypeOfTheFamily_ThatTheLibraryHas_IsAChipTypeOfTheApplication_WithTheSameName(MifareChipSubType subType)
        {
            // The Elatec provider makes the chip type of the application from the sub type: (CARD_TYPE)(subType << 8). A value that
            // the application has no name for is shown without one, and the tree of the chip fails on it. The names are the same, so
            // the guard says which value of the library is missing or called differently.
            var cardType = (CARD_TYPE)((short)subType << 8);

            Assert.True(Enum.IsDefined(typeof(CARD_TYPE), cardType), subType + " has no CARD_TYPE");
            Assert.Equal(subType.ToString(), Enum.GetName(typeof(CARD_TYPE), cardType));
        }

        [Theory]
        [MemberData(nameof(TheSubTypesOfTheFamily))]
        public void EverySubTypeOfTheFamily_HasANameInBothLanguages(MifareChipSubType subType)
        {
            // The header of the tree reads the name from the resources: "ENUM.CARD_TYPE.<name>".
            foreach (var culture in new[] { "en", "de" })
            {
                Assert.False(string.IsNullOrWhiteSpace(TestText.In(culture)("ENUM.CARD_TYPE." + subType)), subType + " (" + culture + ")");
            }
        }

        [Theory]
        [InlineData(MifareChipSubType.MifareUltralight, UltralightVariant.Plain)]
        [InlineData(MifareChipSubType.MifareUltralight_EV1_MF0UL11, UltralightVariant.Ev1Mf0ul11)]
        [InlineData(MifareChipSubType.MifareUltralight_EV1_MF0UL21, UltralightVariant.Ev1Mf0ul21)]
        [InlineData(MifareChipSubType.NTAG_213, UltralightVariant.Ntag213)]
        [InlineData(MifareChipSubType.NTAG_215, UltralightVariant.Ntag215)]
        [InlineData(MifareChipSubType.NTAG_216, UltralightVariant.Ntag216)]
        public async Task AChipThatTheLibraryNamesByItsSubType_IsReadWithItsLayout(MifareChipSubType subType, UltralightVariant variant)
        {
            // GET_VERSION is asked again by the reader layer and decides: the sub type of the library only has to be one of the family.
            var reader = new UltralightTestReader(variant) { SubType = subType };

            var result = await reader.ReadMifareUltralightPagesAsync(UltralightAccess.Open(), 4, 1);

            Assert.True(result.Succeeded, subType + " -> " + result.Error);
            Assert.Equal(variant, result.Variant);
            Assert.Equal(reader.PagesOf(4, 1), result.Data);
        }

        [Fact]
        public async Task AnEv1OfAnotherSize_IsReadWithTheLayoutItsGetVersionAnswerNames()
        {
            // 0x92: the library reports an EV1 of a size it has no value for; the answer of GET_VERSION still names the chip.
            var reader = new UltralightTestReader(UltralightVariant.Ev1Mf0ul21) { SubType = MifareChipSubType.MifareUltralight_EV1 };

            var result = await reader.ReadMifareUltralightPagesAsync(UltralightAccess.Open(), 4, 1);

            Assert.True(result.Succeeded);
            Assert.Equal(UltralightVariant.Ev1Mf0ul21, result.Variant);
        }

        [Theory]
        [InlineData(0x8F)]
        [InlineData(0x95)]
        [InlineData(0x9F)]
        [InlineData(0xA7)]
        [InlineData(0xAA)]
        [InlineData(0xB0)]
        public async Task AChipOutsideTheFamily_IsRefused_AndNothingIsAskedOfIt(int subType)
        {
            var reader = new UltralightTestReader(UltralightVariant.Ntag215) { SubType = (MifareChipSubType)subType };

            var result = await reader.ReadMifareUltralightPagesAsync(UltralightAccess.Open(), 4, 1);

            Assert.Equal(ERROR.ProtocolConstraint, result.Error);
            Assert.Equal(new[] { "select" }, reader.Calls);
        }
    }
}
