using System;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.AccessControl;
using RFiDGear.Infrastructure.ReaderProviders;
using Xunit;

namespace RFiDGear.Tests
{
    public class DesfireKeyChangeInputsTests
    {
        private const string MasterKeyHex = "00112233445566778899AABBCCDDEEFF";
        private const string NewKeyHex = "FFEEDDCCBBAA99887766554433221100";

        [Fact]
        public void Resolve_UsesMasterKeyAsCurrentTargetKeyForPiccMasterKeyChange()
        {
            var resolved = DesfireKeyChangeInputs.Resolve(
                appId: 0,
                targetKeyNo: 0,
                targetKeyType: DESFireKeyType.DF_KEY_AES,
                currentTargetKeyHex: null,
                newTargetKeyHex: NewKeyHex,
                newTargetKeyVersion: 0x01,
                masterKeyHex: MasterKeyHex,
                masterKeyType: DESFireKeyType.DF_KEY_AES,
                keySettings: DESFireKeySettings.ChangeKeyWithMasterKey);

            Assert.Equal("00 11 22 33 44 55 66 77 88 99 AA BB CC DD EE FF", resolved.OldTargetKeyHex);
            Assert.Equal(resolved.OldTargetKeyHex, resolved.AuthKeyHex);
            Assert.Equal((byte)0, resolved.AuthKeyNo);
        }

        [Fact]
        public void Resolve_ThrowsWhenCurrentTargetKeyMissingForApplicationKeyChange()
        {
            var exception = Assert.Throws<ArgumentException>(() => DesfireKeyChangeInputs.Resolve(
                appId: 1,
                targetKeyNo: 1,
                targetKeyType: DESFireKeyType.DF_KEY_AES,
                currentTargetKeyHex: null,
                newTargetKeyHex: NewKeyHex,
                newTargetKeyVersion: 0x01,
                masterKeyHex: MasterKeyHex,
                masterKeyType: DESFireKeyType.DF_KEY_AES,
                keySettings: DESFireKeySettings.ChangeKeyWithTargetedKeyNumber));

            Assert.Equal("currentTargetKeyHex", exception.ParamName);
        }
    }
}
