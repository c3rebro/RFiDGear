using System.Globalization;
using RFiDGear.Extensions.VCNEditor.DataAccessLayer;
using Xunit;

namespace RFiDGear.Tests
{
    public class ResourceLoaderTests
    {
        [Fact]
        public void Convert_WhenValueIsNotEnum_ThrowsInvalidOperationException()
        {
            var loader = new ResourceLoader();

            var exception = Assert.Throws<InvalidOperationException>(() =>
                loader.Convert(123, typeof(string), null, CultureInfo.InvariantCulture));

            Assert.Contains("parameter:no param", exception.Message);
            Assert.Contains("value:123", exception.Message);
        }
    }
}
