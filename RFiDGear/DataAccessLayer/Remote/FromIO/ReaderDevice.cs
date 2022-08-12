using RFiDGear.Model;
using RFiDGear.DataAccessLayer;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RFiDGear.DataAccessLayer.Remote.FromIO
{
    public abstract class ReaderDevice : IDisposable
    {
        public static ReaderDevice Instance
        {
            get
            {
                switch (ReaderType)
                {
                    case ReaderTypes.PCSC:
                        lock (LibLogicalAccessProvider.syncRoot)
                        {
                            if (instance == null)
                            {
                                instance = new LibLogicalAccessProvider(ReaderType);
                                return instance;
                            }
                            else
                                return instance;
                        }
                        break;
                    case ReaderTypes.Elatec:
                        lock (ElatecNetProvider.syncRoot)
                        {
                            if (instance == null)
                            {
                                instance = new ElatecNetProvider(PortNumber);
                                return instance;
                            }
                            else
                                return instance;
                        }
                        break;

                    case ReaderTypes.None:
                        return null;
                        break;

                    default:
                        return null;
                }

            }
        }

        private static object syncRoot = new object();
        private static ReaderDevice instance;

        public object chip;

        public static ReaderTypes ReaderType { get; set; }
        public static int PortNumber { get; set; }
        public MifareClassicSectorModel Sector { get; set; }
        public MifareClassicDataBlockModel DataBlock { get; set; }
        public GenericChipModel GenericChip { get; set; }
        public ReaderTypes ReaderProvider { get; set; }
        public string ReaderUnitName { get; set; }
        public byte[] MifareClassicData { get; set; }
        public bool DataBlockSuccessfullyRead { get; set; }
        public bool DataBlockSuccesfullyAuth { get; set; }
        public bool SectorSuccessfullyRead { get; set; }
        public bool SectorSuccesfullyAuth { get; set; }
        public byte[] MifareDESFireData { get; set; }
        public uint[] AppIDList { get; set; }
        public byte[] FileIDList { get; set; }
        public byte[] MifareUltralightPageData { get; set; }
        public byte MaxNumberOfAppKeys { get; set; }
        public byte EncryptionType { get; set; }
        public DESFireFileSettings DesfireFileSettings { get; set; }
        public DESFireKeySettings DesfireAppKeySetting { get; set; }




        #region Common

        public abstract ERROR ReadChipPublic();

        /*
		public ERROR ChangeProvider(ReaderTypes _provider)
        {

        }
		*/
        #endregion
        #region MifareClassic
        // Mifare Classic Method Definitions
        public abstract ERROR ReadMiFareClassicSingleSector(int sectorNumber, string aKey, string bKey);
        public abstract ERROR WriteMiFareClassicSingleSector(int sectorNumber, string _aKey, string _bKey, byte[] buffer);
        public abstract ERROR WriteMiFareClassicSingleBlock(int _blockNumber, string _aKey, string _bKey, byte[] buffer);
        public abstract ERROR ReadMiFareClassicSingleBlock(int _blockNumber, string _aKey, string _bKey);
        public abstract ERROR WriteMiFareClassicWithMAD(int _madApplicationID, int _madStartSector,
                                               string _aKeyToUse, string _bKeyToUse, string _aKeyToWrite, string _bKeyToWrite,
                                               string _madAKeyToUse, string _madBKeyToUse, string _madAKeyToWrite, string _madBKeyToWrite,
                                               byte[] buffer, byte _madGPB, SectorAccessBits _sab, bool _useMADToAuth = false, bool _keyToWriteUseMAD = false);
        public abstract ERROR ReadMiFareClassicWithMAD(int madApplicationID, string _aKeyToUse, string _bKeyToUse, string _madAKeyToUse, string _madBKeyToUse, int _length, byte _madGPB, bool _useMADToAuth = true, bool _aiToUseIsMAD = false);
        #endregion

        #region MifareUltralight
        // Mifare Ultralight Method Definitions
        public abstract ERROR ReadMifareUltralightSinglePage(int _pageNo);
        #endregion

        #region MifareDesfire
        public abstract ERROR GetMiFareDESFireChipAppIDs(string _appMasterKey = "00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00", DESFireKeyType _keyTypeAppMasterKey = DESFireKeyType.DF_KEY_DES);
        public abstract ERROR CreateMifareDesfireFile(string _appMasterKey, DESFireKeyType _keyTypeAppMasterKey, FileType_MifareDesfireFileType _fileType, DESFireAccessRights _accessRights, EncryptionMode _encMode,
                                        int _appID, int _fileNo, int _fileSize,
                                        int _minValue = 0, int _maxValue = 1000, int _initValue = 0, bool _isValueLimited = false,
                                        int _maxNbOfRecords = 100);
        public abstract ERROR ReadMiFareDESFireChipFile(string _appMasterKey, DESFireKeyType _keyTypeAppMasterKey,
                                        string _appReadKey, DESFireKeyType _keyTypeAppReadKey, int _readKeyNo,
                                        string _appWriteKey, DESFireKeyType _keyTypeAppWriteKey, int _writeKeyNo,
                                        EncryptionMode _encMode,
                                        int _fileNo, int _appID, int _fileSize);
        public abstract ERROR WriteMiFareDESFireChipFile(string _cardMasterKey, DESFireKeyType _keyTypeCardMasterKey,
                                        string _appMasterKey, DESFireKeyType _keyTypeAppMasterKey,
                                        string _appReadKey, DESFireKeyType _keyTypeAppReadKey, int _readKeyNo,
                                        string _appWriteKey, DESFireKeyType _keyTypeAppWriteKey, int _writeKeyNo,
                                        EncryptionMode _encMode,
                                        int _fileNo, int _appID, byte[] _data);
        public abstract ERROR AuthToMifareDesfireApplication(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumber, int _appID = 0);
        public abstract ERROR GetMifareDesfireAppSettings(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumberCurrent = 0, int _appID = 0);
        public abstract ERROR CreateMifareDesfireApplication(string _piccMasterKey, DESFireKeySettings _keySettingsTarget, DESFireKeyType _keyTypePiccMasterKey, DESFireKeyType _keyTypeTargetApplication, int _maxNbKeys, int _appID, bool authenticateToPICCFirst = true);
        public abstract ERROR ChangeMifareDesfireApplicationKey(string _applicationMasterKeyCurrent, int _keyNumberCurrent, DESFireKeyType _keyTypeCurrent,
                                        string _applicationMasterKeyTarget, int _keyNumberTarget, int selectedDesfireAppKeyVersionTargetAsIntint,
                                        DESFireKeyType _keyTypeTarget, int _appIDCurrent, int _appIDTarget,
                                        DESFireKeySettings keySettings);
        public abstract ERROR DeleteMifareDesfireApplication(string _applicationMasterKey, DESFireKeyType _keyType, int _appID = 0);
        public abstract ERROR DeleteMifareDesfireFile(string _applicationMasterKey, DESFireKeyType _keyType, int _appID = 0, int _fileID = 0);
        public abstract ERROR FormatDesfireCard(string _applicationMasterKey, DESFireKeyType _keyType, int _appID = 0);
        public abstract ERROR GetMifareDesfireFileList(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumberCurrent = 0, int _appID = 0);
        public abstract ERROR GetMifareDesfireFileSettings(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumberCurrent = 0, int _appID = 0, int _fileNo = 0);

        #endregion

        protected abstract void Dispose(bool disposing);
        public abstract void Dispose();
    }
}
