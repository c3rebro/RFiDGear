using RFiDGear.Infrastructure;
using Xunit;

namespace RFiDGear.Tests.Helpers
{
    public class UiDispatcherTests
    {
        [Fact]
        public void InvokeIfRequired_ExecutesAction_WhenDispatcherUnavailable()
        {
            var invoked = false;

            UiDispatcher.InvokeIfRequired(() => invoked = true);

            Assert.True(invoked);
        }
    }
}
