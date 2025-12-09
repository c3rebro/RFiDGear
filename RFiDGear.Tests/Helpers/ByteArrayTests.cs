using ByteArrayHelper;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RFiDGear.Tests.Helpers
{
    [TestClass]
    public class ByteArrayTests
    {
        [TestMethod]
        public void Constructor_DefensivelyCopiesInput()
        {
            byte[] source = { 0x01, 0x02 };

            var byteArray = new ByteArray(source);
            source[0] = 0xFF;

            Assert.AreNotSame(source, byteArray.Data);
            Assert.AreEqual(0x01, byteArray.Data[0]);
        }

        [TestMethod]
        public void Or_LittleEndianAppliesFromStart()
        {
            var target = new ByteArray(new byte[] { 0x00, 0x10, 0x20 });
            byte[] source = { 0x01, 0x02 };

            target.Or(source, isLittleEndian: true);

            CollectionAssert.AreEqual(new byte[] { 0x01, 0x12, 0x20 }, target.Data);
            CollectionAssert.AreEqual(new byte[] { 0x01, 0x02 }, source);
        }

        [TestMethod]
        public void Or_BigEndianAppliesFromEnd()
        {
            var target = new ByteArray(new byte[] { 0x00, 0x10, 0x20 });
            byte[] source = { 0x01, 0x02 };

            target.Or(source, isLittleEndian: false);

            CollectionAssert.AreEqual(new byte[] { 0x00, 0x11, 0x22 }, target.Data);
            CollectionAssert.AreEqual(new byte[] { 0x01, 0x02 }, source);
        }
    }
}
