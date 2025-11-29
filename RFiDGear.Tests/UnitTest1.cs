using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RFiDGear.DataAccessLayer;
using RFiDGear.DataAccessLayer.Remote.FromIO;
using RFiDGear.Model;
using RFiDGear.Services.TaskExecution;
using RFiDGear.ViewModel;

namespace RFiDGear.Tests
{
    [TestClass]
    public class TaskExecutionServiceTests
    {
        [TestMethod]
        public async Task ExecuteOnceAsync_WhenNoDevice_Throws()
        {
            var service = CreateService(null);
            var request = CreateRequest();

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => service.ExecuteOnceAsync(request, CancellationToken.None));
        }

        [TestMethod]
        public async Task ExecuteOnceAsync_TaskLoopTimeout_ThrowsTimeout()
        {
            var device = new FakeReaderDevice(new GenericChipModel("ABC", CARD_TYPE.DESFireEV1));
            var service = CreateService(device);
            var descriptor = new TaskDescriptor(0, null, async token => await Task.Delay(TimeSpan.FromMilliseconds(100), token));

            var request = CreateRequest(new List<TaskDescriptor> { descriptor }, new TaskExecutionTimeouts
            {
                TaskLoopTimeout = TimeSpan.FromMilliseconds(25)
            });

            await Assert.ThrowsExceptionAsync<TimeoutException>(() => service.ExecuteOnceAsync(request, CancellationToken.None));
        }

        [TestMethod]
        public async Task ExecuteOnceAsync_RunSelectedOnly_ExecutesSingleDescriptor()
        {
            var device = new FakeReaderDevice(new GenericChipModel("ABC", CARD_TYPE.DESFireEV1));
            var service = CreateService(device);

            var executed = 0;
            var descriptors = new List<TaskDescriptor>
            {
                new TaskDescriptor(0, null, token =>
                {
                    executed++;
                    return Task.CompletedTask;
                }),
                new TaskDescriptor(1, null, token =>
                {
                    executed++;
                    return Task.CompletedTask;
                })
            };

            var request = CreateRequest(descriptors);
            request.RunSelectedOnly = true;

            var result = await service.ExecuteOnceAsync(request, CancellationToken.None);

            Assert.AreEqual(1, executed);
            Assert.IsTrue(result.RunSelectedOnly);
        }

        private static TaskExecutionService CreateService(ReaderDevice device)
        {
            var provider = new FakeReaderDeviceProvider(device);
            var triggerTimer = new FakeDispatcherTimerAdapter();
            var taskTimeout = new FakeDispatcherTimerAdapter();
            return new TaskExecutionService(provider, triggerTimer, taskTimeout, new TestLogger());
        }

        private static TaskExecutionRequest CreateRequest(IReadOnlyList<TaskDescriptor> descriptors = null, TaskExecutionTimeouts timeouts = null)
        {
            return new TaskExecutionRequest
            {
                TaskHandler = new ChipTaskHandlerModel(),
                TreeViewParentNodes = new ObservableCollection<RFiDChipParentLayerViewModel>(),
                Dialogs = new ObservableCollection<MVVMDialogs.ViewModels.Interfaces.IDialogViewModel>(),
                VariablesFromArgs = new Dictionary<string, string>(),
                Timeouts = timeouts ?? TaskExecutionTimeouts.Default,
                TaskDescriptors = descriptors,
                ReportOutputPath = string.Empty,
                ReportTemplateFile = string.Empty
            };
        }

        private class FakeReaderDeviceProvider : IReaderDeviceProvider
        {
            private readonly ReaderDevice device;

            public FakeReaderDeviceProvider(ReaderDevice device)
            {
                this.device = device;
            }

            public ReaderDevice GetInstance() => device;
        }

        private class FakeDispatcherTimerAdapter : IDispatcherTimerAdapter
        {
            public bool IsEnabled { get; set; }
            public object Tag { get; set; }
            public bool Disposed { get; private set; }

            public void Dispose()
            {
                Disposed = true;
                IsEnabled = false;
            }

            public void Start() => IsEnabled = true;

            public void Stop() => IsEnabled = false;
        }

        private class TestLogger : ITaskExecutionLogger
        {
            public readonly List<(string Stage, Exception Exception)> Errors = new List<(string, Exception)>();
            public readonly List<string> Messages = new List<string>();

            public void LogError(string stage, Exception exception, object details = null)
            {
                Errors.Add((stage, exception));
            }

            public void LogInformation(string stage, object details = null)
            {
                Messages.Add(stage);
            }
        }

        private class FakeReaderDevice : ReaderDevice
        {
            public FakeReaderDevice(GenericChipModel chip)
            {
                GenericChip = chip;
            }

            public override bool IsConnected => true;

            public override Task<ERROR> ConnectAsync() => Task.FromResult(ERROR.Empty);

            public override Task<ERROR> ReadChipPublic() => Task.FromResult(ERROR.Empty);

            public override Task<ERROR> ReadMifareClassicSingleSector(int sectorNumber, string aKey, string bKey) => Task.FromResult(ERROR.Empty);

            public override Task<ERROR> WriteMifareClassicSingleSector(int sectorNumber, string _aKey, string _bKey, byte[] buffer) => Task.FromResult(ERROR.Empty);

            public override Task<ERROR> WriteMifareClassicSingleBlock(int _blockNumber, string _aKey, string _bKey, byte[] buffer) => Task.FromResult(ERROR.Empty);

            public override Task<ERROR> WriteMifareClassicWithMAD(int _madApplicationID, int _madStartSector, string _aKeyToUse, string _bKeyToUse, string _aKeyToWrite, string _bKeyToWrite, string _madAKeyToUse, string _madBKeyToUse, string _madAKeyToWrite, string _madBKeyToWrite, byte[] buffer, byte _madGPB, SectorAccessBits _sab, bool _useMADToAuth, bool _keyToWriteUseMAD) => Task.FromResult(ERROR.Empty);

            public override Task<ERROR> ReadMifareClassicWithMAD(int madApplicationID, string _aKeyToUse, string _bKeyToUse, string _madAKeyToUse, string _madBKeyToUse, int _length, byte _madGPB, bool _useMADToAuth, bool _aiToUseIsMAD) => Task.FromResult(ERROR.Empty);

            public override Task<ERROR> ReadMifareUltralightSinglePage(int _pageNo) => Task.FromResult(ERROR.Empty);

            public override Task<ERROR> GetMiFareDESFireChipAppIDs(string _appMasterKey = "00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00", DESFireKeyType _keyTypeAppMasterKey = DESFireKeyType.DF_KEY_DES) => Task.FromResult(ERROR.Empty);

            public override Task<ERROR> CreateMifareDesfireFile(string _appMasterKey, DESFireKeyType _keyTypeAppMasterKey, FileType_MifareDesfireFileType _fileType, DESFireAccessRights _accessRights, EncryptionMode _encMode, int _appID, int _fileNo, int _fileSize, int _minValue = 0, int _maxValue = 1000, int _initValue = 0, bool _isValueLimited = false, int _maxNbOfRecords = 100) => Task.FromResult(ERROR.Empty);

            public override Task<ERROR> ReadMiFareDESFireChipFile(string _appMasterKey, DESFireKeyType _keyTypeAppMasterKey, string _appReadKey, DESFireKeyType _keyTypeAppReadKey, int _readKeyNo, string _appWriteKey, DESFireKeyType _keyTypeAppWriteKey, int _writeKeyNo, EncryptionMode _encMode, int _fileNo, int _appID, int _fileSize) => Task.FromResult(ERROR.Empty);

            public override Task<ERROR> WriteMiFareDESFireChipFile(string _cardMasterKey, DESFireKeyType _keyTypeCardMasterKey, string _appMasterKey, DESFireKeyType _keyTypeAppMasterKey, string _appReadKey, DESFireKeyType _keyTypeAppReadKey, int _readKeyNo, string _appWriteKey, DESFireKeyType _keyTypeAppWriteKey, int _writeKeyNo, EncryptionMode _encMode, int _fileNo, int _appID, byte[] _data) => Task.FromResult(ERROR.Empty);

            public override Task<ERROR> AuthToMifareDesfireApplication(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumber, int _appID = 0) => Task.FromResult(ERROR.Empty);

            public override Task<ERROR> GetMifareDesfireAppSettings(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumberCurrent = 0, int _appID = 0) => Task.FromResult(ERROR.Empty);

            public override Task<ERROR> CreateMifareDesfireApplication(string _piccMasterKey, DESFireKeySettings _keySettingsTarget, DESFireKeyType _keyTypePiccMasterKey, DESFireKeyType _keyTypeTargetApplication, int _maxNbKeys, int _appID, bool authenticateToPICCFirst = true) => Task.FromResult(ERROR.Empty);

            public override Task<ERROR> ChangeMifareDesfireApplicationKey(string _applicationMasterKeyCurrent, int _keyNumberCurrent, DESFireKeyType _keyTypeCurrent, string _applicationMasterKeyTarget, int _keyNumberTarget, int selectedDesfireAppKeyVersionTargetAsIntint, DESFireKeyType _keyTypeTarget, int _appIDCurrent, int _appIDTarget, DESFireKeySettings keySettings, int keyVersion) => Task.FromResult(ERROR.Empty);

            public override Task<ERROR> DeleteMifareDesfireApplication(string _applicationMasterKey, DESFireKeyType _keyType, uint _appID = 0) => Task.FromResult(ERROR.Empty);

            public override Task<ERROR> DeleteMifareDesfireFile(string _applicationMasterKey, DESFireKeyType _keyType, int _appID = 0, int _fileID = 0) => Task.FromResult(ERROR.Empty);

            public override Task<ERROR> FormatDesfireCard(string _applicationMasterKey, DESFireKeyType _keyType) => Task.FromResult(ERROR.Empty);

            public override Task<ERROR> GetMifareDesfireFileList(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumberCurrent = 0, int _appID = 0) => Task.FromResult(ERROR.Empty);

            public override Task<ERROR> GetMifareDesfireFileSettings(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumberCurrent = 0, int _appID = 0, int _fileNo = 0) => Task.FromResult(ERROR.Empty);

            protected override void Dispose(bool disposing)
            {
            }

            public override void Dispose()
            {
            }
        }
    }
}
