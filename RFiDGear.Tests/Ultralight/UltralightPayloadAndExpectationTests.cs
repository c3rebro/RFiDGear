using System;
using System.Collections.Generic;
using System.Linq;
using RFiDGear.Infrastructure.Ultralight;
using Xunit;

namespace RFiDGear.Tests.Ultralight
{
    public class UltralightPayloadTests
    {
        [Theory]
        [InlineData("A1B2C3D4", "A1B2C3D4")]
        [InlineData("a1b2c3d4", "A1B2C3D4")]
        [InlineData("A1B2 C3D4", "A1B2C3D4")]
        [InlineData("  A1B2C3D4\r\n01020304\t", "A1B2C3D401020304")]
        [InlineData("00000000", "00000000")]
        public void ThePayload_IsReadWithoutRegardToCaseAndWhiteSpace(string text, string expectedBytes)
        {
            Assert.True(UltralightPayload.TryParse(text, out var data));
            Assert.Equal(expectedBytes, Convert.ToHexString(data));
            Assert.Equal(expectedBytes.Length / 8, UltralightPayload.PageCount(data));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("A1B2C3")]          // less than a page
        [InlineData("A1B2C3D")]         // an odd number of digits
        [InlineData("A1B2C3D4E5")]      // not a multiple of a page
        [InlineData("A1B2C3D4E5F6")]
        [InlineData("G1B2C3D4")]        // not a hexadecimal digit
        [InlineData("0xA1B2C3D4")]      // no prefix
        [InlineData("A1-B2-C3-D4")]     // no other separators than white space
        [InlineData("A1B2:C3D4")]
        public void APayloadThatIsNoWholeNumberOfPages_OrHasAnythingButDigits_IsRefused(string text)
        {
            Assert.False(UltralightPayload.TryParse(text, out var data));
            Assert.Null(data);
        }

        [Fact]
        public void TheMostPagesOfTheLargestChip_AreTheMostAWriteCanHold()
        {
            Assert.True(UltralightPayload.TryParse(new string('A', 8 * UltralightPayload.MaxPages), out var data));
            Assert.Equal(UltralightPayload.MaxPages, UltralightPayload.PageCount(data));

            Assert.False(UltralightPayload.TryParse(new string('A', 8 * (UltralightPayload.MaxPages + 1)), out _));
        }

        [Fact]
        public void FormatAndParse_AreEachOthersInverse_AndTheProjectFormHasNoSeparators()
        {
            var data = new byte[] { 0xA1, 0xB2, 0xC3, 0xD4, 0x00, 0x0F, 0xF0, 0xFF };

            var text = UltralightPayload.Format(data);

            Assert.Equal("A1B2C3D4000FF0FF", text);
            Assert.True(UltralightPayload.TryParse(text, out var back));
            Assert.Equal(data, back);
        }

        [Fact]
        public void BytesThatAreNoWholeNumberOfPages_AreNotFormattedOrCounted()
        {
            Assert.Throws<ArgumentException>(() => UltralightPayload.Format(new byte[3]));
            Assert.Throws<ArgumentException>(() => UltralightPayload.PageCount(new byte[5]));
            Assert.ThrowsAny<ArgumentException>(() => UltralightPayload.PageCount(null));
        }
    }

    public class UltralightExpectationTests
    {
        public static IEnumerable<object[]> Variants() =>
            Enum.GetValues(typeof(UltralightVariant)).Cast<UltralightVariant>().Select(variant => new object[] { variant });

        [Theory]
        [MemberData(nameof(Variants))]
        public void AnExpectation_NamesTheVariantWithTheSameName_AndTheSameNumber(UltralightVariant variant)
        {
            var expectation = (UltralightExpectation)Enum.Parse(typeof(UltralightExpectation), variant.ToString());

            // The expectation is stored in projects by name: a number that drifted from the variant would change what a task expects.
            Assert.Equal((int)variant, (int)expectation);
            Assert.True(expectation.TryGetVariant(out var named));
            Assert.Equal(variant, named);
        }

        [Fact]
        public void Auto_NamesNoVariant_AndNeitherDoesANumberThatIsNoExpectation()
        {
            Assert.False(UltralightExpectation.Auto.TryGetVariant(out _));
            Assert.False(((UltralightExpectation)99).TryGetVariant(out _));
        }

        [Fact]
        public void TheExpectedNamesAreInTheEnum_ForEveryVariantAndAuto()
        {
            Assert.Equal(
                Enum.GetValues(typeof(UltralightVariant)).Length + 1,
                Enum.GetValues(typeof(UltralightExpectation)).Length);
        }

        [Theory]
        [MemberData(nameof(Variants))]
        public void Auto_AcceptsEveryChip_AndANamedExpectationOnlyItsOwn(UltralightVariant found)
        {
            Assert.True(UltralightExpectation.Auto.Accepts(found));

            foreach (var expectation in Enum.GetValues(typeof(UltralightExpectation)).Cast<UltralightExpectation>().Where(e => e != UltralightExpectation.Auto))
            {
                Assert.True(expectation.TryGetVariant(out var named));
                Assert.Equal(named == found, expectation.Accepts(found));
            }
        }
    }
}
