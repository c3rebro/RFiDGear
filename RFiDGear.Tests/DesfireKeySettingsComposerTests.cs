using System.Threading.Tasks;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.AccessControl;
using Xunit;

namespace RFiDGear.Tests
{
    public class DesfireKeySettingsComposerTests
    {
        [Fact]
        public void BuildSettingsByte_PiccScopeZeroesUpperNibble()
        {
            var settings = DESFireKeySettings.ChangeKeyFrozen | DESFireKeySettings.AllowFreeCreateDeleteWithoutMasterKey;

            var settingsByte = DesfireKeySettingsComposer.BuildSettingsByte(settings, applyToPicc: true);

            Assert.Equal(0x04, settingsByte);
        }

        [Fact]
        public void BuildSettingsByte_AppScopeKeepsUiSelection()
        {
            var settings = DESFireKeySettings.ChangeKeyWithTargetedKeyNumber | DESFireKeySettings.AllowChangeMasterKey;

            var settingsByte = DesfireKeySettingsComposer.BuildSettingsByte(settings, applyToPicc: false);

            Assert.Equal((byte)settings, settingsByte);
        }
    }

    public class DesfireKeyChangeOrchestratorTests
    {
        [Fact]
        public async Task ChangeApplicationKeyAsync_PassesSettingsWithoutForcingHighNibble()
        {
            var provider = new FakeDesfireProvider();
            var orchestrator = new DesfireKeyChangeOrchestrator(provider);
            var settings = DESFireKeySettings.ChangeKeyWithTargetedKeyNumber | DESFireKeySettings.AllowChangeMasterKey;

            await orchestrator.ChangeApplicationKeyAsync(
                "11",
                currentKeyNumber: 2,
                DESFireKeyType.DF_KEY_DES,
                "33",
                "22",
                targetKeyVersion: 5,
                DESFireKeyType.DF_KEY_AES,
                appIdCurrent: 7,
                appIdTarget: 9,
                settings,
                keyVersion: 1);

            Assert.Equal(2, provider.LastAuthKeyNumber);
            Assert.Equal(2, provider.LastChangeKeyNumber);
            Assert.Equal((DESFireKeySettings)DesfireKeySettingsComposer.BuildSettingsByte(settings, applyToPicc: false), provider.LastSettings);
        }

        [Fact]
        public async Task ChangeApplicationKeySettingsAsync_MasksPiccSettingsAndAuthenticatesToPICC()
        {
            var provider = new FakeDesfireProvider();
            var orchestrator = new DesfireKeyChangeOrchestrator(provider);
            var settings = DESFireKeySettings.ChangeKeyFrozen | DESFireKeySettings.AllowChangeMasterKey;

            await orchestrator.ChangeApplicationKeySettingsAsync(
                "AA",
                currentKeyNumber: 0,
                DESFireKeyType.DF_KEY_DES,
                "BB",
                targetKeyVersion: 0,
                DESFireKeyType.DF_KEY_DES,
                appIdCurrent: 0,
                appIdTarget: 0,
                settings,
                keyVersion: 2);

            Assert.Equal(0, provider.LastAuthKeyNumber);
            Assert.Equal(0, provider.LastChangeKeyNumber);
            Assert.Equal((DESFireKeySettings)DesfireKeySettingsComposer.BuildSettingsByte(settings, applyToPicc: true), provider.LastSettings);
        }
    }

    internal class FakeDesfireProvider : IMifareDesfireProvider
    {
        public int LastAuthKeyNumber { get; private set; }

        public int LastChangeKeyNumber { get; private set; }

        public DESFireKeySettings LastSettings { get; private set; }

        public Task<ERROR> AuthenticateAsync(string applicationMasterKey, DESFireKeyType keyType, int keyNumber, int appId)
        {
            LastAuthKeyNumber = keyNumber;
            return Task.FromResult(ERROR.NoError);
        }

        public Task<ERROR> ChangeMifareDesfireApplicationKey(string applicationMasterKeyCurrent, int keyNumberCurrent, DESFireKeyType keyTypeCurrent, string oldKeyForChangeKey, string applicationMasterKeyTarget, int selectedDesfireAppKeyVersionTargetAsIntint, DESFireKeyType keyTypeTarget, int appIdCurrent, int appIdTarget, DESFireKeySettings keySettings, int keyVersion)
        {
            LastSettings = keySettings;
            LastChangeKeyNumber = keyNumberCurrent;
            return Task.FromResult(ERROR.NoError);
        }

        public Task<ERROR> ChangeMifareDesfireApplicationKeySettings(string applicationMasterKeyCurrent, int keyNumberCurrent, DESFireKeyType keyTypeCurrent, string applicationMasterKeyTarget, int selectedDesfireAppKeyVersionTargetAsIntint, DESFireKeyType keyTypeTarget, int appIdCurrent, int appIdTarget, DESFireKeySettings keySettings, int keyVersion)
        {
            LastSettings = keySettings;
            LastChangeKeyNumber = keyNumberCurrent;
            return Task.FromResult(ERROR.NoError);
        }
    }
}
