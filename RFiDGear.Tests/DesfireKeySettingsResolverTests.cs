using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.AccessControl;
using RFiDGear.Infrastructure.ReaderProviders;
using Xunit;

namespace RFiDGear.Tests
{
    public class DesfireKeySettingsResolverTests
    {
        [Fact]
        public void Resolve_UsesCardValuesWhenAvailable()
        {
            var result = DesfireKeySettingsResolver.Resolve(
                DESFireKeySettings.AllowChangeMasterKey,
                cardKeyCount: 5,
                cardKeyType: DESFireKeyType.DF_KEY_AES,
                configuredKeyCount: 1,
                configuredKeyType: DESFireKeyType.DF_KEY_DES);

            Assert.Equal(DESFireKeySettings.AllowChangeMasterKey, result.Settings);
            Assert.Equal((byte)5, result.KeyCount);
            Assert.Equal(DESFireKeyType.DF_KEY_AES, result.KeyType);
            Assert.Empty(result.Warnings);
        }

        [Fact]
        public void Resolve_FallsBackToConfiguredValuesWhenCardMissing()
        {
            var result = DesfireKeySettingsResolver.Resolve(
                DESFireKeySettings.ConfigurationChangeable,
                cardKeyCount: null,
                cardKeyType: null,
                configuredKeyCount: 0,
                configuredKeyType: DESFireKeyType.DF_KEY_3K3DES);

            Assert.Equal(DESFireKeySettings.ConfigurationChangeable, result.Settings);
            Assert.Equal((byte)0, result.KeyCount);
            Assert.Equal(DESFireKeyType.DF_KEY_3K3DES, result.KeyType);
            Assert.Contains(result.Warnings, warning => warning.Contains("Key count unavailable"));
            Assert.Contains(result.Warnings, warning => warning.Contains("Key type unavailable"));
            Assert.Contains(result.Warnings, warning => warning.Contains("key count is zero"));
        }
    }
}
