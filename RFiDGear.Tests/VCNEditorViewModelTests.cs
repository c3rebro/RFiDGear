using System.Reflection;

using RFiDGear.Extensions.VCNEditor.ViewModel;

using Xunit;

namespace RFiDGear.Tests
{
    public class VCNEditorViewModelTests
    {
        [Fact]
        public void SetFlag_SetsAndClearsMask()
        {
            MethodInfo? setFlagMethod = typeof(VCNEditorViewModel).GetMethod(
                "SetFlag",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.NotNull(setFlagMethod);

            byte value = 0x00;
            object[] setArgs = { value, (byte)0x10, true };
            setFlagMethod!.Invoke(null, setArgs);

            byte setResult = (byte)setArgs[0];
            Assert.Equal((byte)0x10, setResult);

            object[] clearArgs = { setResult, (byte)0x10, false };
            setFlagMethod.Invoke(null, clearArgs);

            byte clearResult = (byte)clearArgs[0];
            Assert.Equal((byte)0x00, clearResult);
        }
    }
}
