using ByteArrayHelper;
using Xunit;

namespace RFiDGear.Tests.Helpers
{
    public class ByteArrayTests
    {
        [Fact]
        public void Constructor_DefensivelyCopiesInput()
        {
            byte[] source = { 0x01, 0x02 };

            var byteArray = new ByteArray(source);
            source[0] = 0xFF;

            Assert.NotSame(source, byteArray.Data);
            Assert.Equal(0x01, byteArray.Data[0]);
        }

        [Fact]
        public void Or_LittleEndianAppliesFromStart()
        {
            var target = new ByteArray(new byte[] { 0x00, 0x10, 0x20 });
            byte[] source = { 0x01, 0x02 };

            target.Or(source, isLittleEndian: true);

            Assert.Equal(new byte[] { 0x01, 0x12, 0x20 }, target.Data);
            Assert.Equal(new byte[] { 0x01, 0x02 }, source);
        }

        [Fact]
        public void Or_BigEndianAppliesFromEnd()
        {
            var target = new ByteArray(new byte[] { 0x00, 0x10, 0x20 });
            byte[] source = { 0x01, 0x02 };

            target.Or(source, isLittleEndian: false);

            Assert.Equal(new byte[] { 0x00, 0x11, 0x22 }, target.Data);
            Assert.Equal(new byte[] { 0x01, 0x02 }, source);
        }
    }
}
