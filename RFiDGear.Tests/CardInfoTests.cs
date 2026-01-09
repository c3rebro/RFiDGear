using RFiDGear.DataAccessLayer;
using Xunit;

namespace RFiDGear.Tests
{
    public class CardInfoTests
    {
        [Fact]
        public void Equals_ReturnsTrueForMatchingValues()
        {
            var first = new CARD_INFO(CARD_TYPE.Mifare1K, "A1B2");
            var second = new CARD_INFO(CARD_TYPE.Mifare1K, "A1B2");

            Assert.Equal(first, second);
            Assert.True(first.Equals(second));
            Assert.Equal(first.GetHashCode(), second.GetHashCode());
        }

        [Fact]
        public void Equals_ReturnsFalseForDifferentValues()
        {
            var first = new CARD_INFO(CARD_TYPE.Mifare1K, "A1B2");
            var second = new CARD_INFO(CARD_TYPE.Mifare2K, "A1B2");
            var third = new CARD_INFO(CARD_TYPE.Mifare1K, "C3D4");

            Assert.NotEqual(first, second);
            Assert.NotEqual(first, third);
        }
    }
}
