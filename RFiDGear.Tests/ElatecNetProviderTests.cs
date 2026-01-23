using System.Threading.Tasks;
using Elatec.NET.Cards.Mifare;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.ReaderProviders;
using Xunit;
using RfidEncryptionMode = RFiDGear.Infrastructure.EncryptionMode;
using DESFireKeyType = RFiDGear.Infrastructure.DESFireKeyType;

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

        [Fact]
        public async Task CreateMifareDesfireFile_BackupFile_UsesBackupCreatePath()
        {
            var provider = new BackupFileTestProvider();
            var accessRights = new DESFireAccessRights
            {
                readAccess = TaskAccessRights.AR_KEY0,
                writeAccess = TaskAccessRights.AR_KEY1,
                readAndWriteAccess = TaskAccessRights.AR_KEY2,
                changeAccess = TaskAccessRights.AR_KEY3
            };

            var result = await provider.CreateMifareDesfireFile(
                _appMasterKey: "0000000000000000",
                _keyTypeAppMasterKey: DESFireKeyType.DF_KEY_AES,
                _fileType: Infrastructure.Tasks.FileType_MifareDesfireFileType.BackupFile,
                _accessRights: accessRights,
                _encMode: RfidEncryptionMode.CM_PLAIN,
                _appID: 1,
                _fileNo: 2,
                _fileSize: 16);

            Assert.Equal(ERROR.NoError, result);
            Assert.True(provider.BackupFileRequested);
            Assert.False(provider.StdDataFileRequested);
        }

        private sealed class BackupFileTestProvider : ElatecNetProvider
        {
            public bool BackupFileRequested { get; private set; }

            public bool StdDataFileRequested { get; private set; }

            public override bool IsConnected => true;

            public override Task<ERROR> AuthToMifareDesfireApplication(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumber, int _appID)
            {
                return Task.FromResult(ERROR.NoError);
            }

            protected override Task CreateStdDataFileAsync(byte fileNo, Infrastructure.Tasks.FileType_MifareDesfireFileType fileType, RfidEncryptionMode encMode, DESFireFileAccessRights accessRights, uint fileSize)
            {
                StdDataFileRequested = true;
                return Task.CompletedTask;
            }

            protected override Task CreateBackupFileAsync(byte fileNo, RfidEncryptionMode encMode, DESFireFileAccessRights accessRights, uint fileSize)
            {
                BackupFileRequested = true;
                return Task.CompletedTask;
            }
        }
    }
}
