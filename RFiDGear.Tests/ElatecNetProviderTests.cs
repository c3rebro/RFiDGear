using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.ReaderProviders;
using Xunit;

namespace RFiDGear.Tests
{
    public class ElatecNetProviderTests
    {
        [Fact]
        public void ResolveKeyTypeForChange_PiccUsesTargetKeyType()
        {
            var result = ElatecNetProvider.ResolveKeyTypeForChange(
                appId: 0,
                targetKeyType: DESFireKeyType.DF_KEY_AES,
                detectedKeyType: DESFireKeyType.DF_KEY_DES);

            Assert.Equal(DESFireKeyType.DF_KEY_AES, result);
        }

        [Fact]
        public void ResolveKeyTypeForChange_AppUsesDetectedKeyTypeWhenAvailable()
        {
            var result = ElatecNetProvider.ResolveKeyTypeForChange(
                appId: 1,
                targetKeyType: DESFireKeyType.DF_KEY_AES,
                detectedKeyType: DESFireKeyType.DF_KEY_DES);

            Assert.Equal(DESFireKeyType.DF_KEY_DES, result);
        }

        [Fact]
        public void ResolveKeyTypeForChange_AppFallsBackToTargetWhenUnknown()
        {
            var result = ElatecNetProvider.ResolveKeyTypeForChange(
                appId: 1,
                targetKeyType: DESFireKeyType.DF_KEY_AES,
                detectedKeyType: null);

            Assert.Equal(DESFireKeyType.DF_KEY_AES, result);
        }

        [Fact]
        public void ResolveDesfireKeyType_UsesProviderNameWhenKnown()
        {
            var result = ElatecNetProvider.ResolveDesfireKeyType("DF_KEY_AES", DESFireKeyType.DF_KEY_DES);

            Assert.Equal(DESFireKeyType.DF_KEY_AES, result);
        }

        [Fact]
        public void ResolveDesfireKeyType_FallsBackWhenUnknown()
        {
            var result = ElatecNetProvider.ResolveDesfireKeyType("UnknownKeyType", DESFireKeyType.DF_KEY_3K3DES);

            Assert.Equal(DESFireKeyType.DF_KEY_3K3DES, result);
        }
    }
}
