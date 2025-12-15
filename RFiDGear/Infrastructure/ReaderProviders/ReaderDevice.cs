using Elatec.NET.Cards.Mifare;
using LibLogicalAccess;
using RFiDGear.Infrastructure.AccessControl;
using RFiDGear.Infrastructure.Tasks;
using RFiDGear.Models;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RFiDGear.Infrastructure.ReaderProviders
{
    public abstract class ReaderDevice : IDisposable
    {
        public static ReaderDevice Instance
        {
            get
            {
                switch (Reader)
                {

                    case ReaderTypes.PCSC:
                        lock (syncRoot)
                        {
                            if (instance == null)
                            {
                                instance = new LibLogicalAccessProvider(Reader);
                                return instance;
                            }
                            else if (instance != null && !(instance is LibLogicalAccessProvider))
                            {
                                instance = new LibLogicalAccessProvider(Reader);
                                return instance;
                            }
                            else
                            {
                                return instance;
                            }
                        }

                    case ReaderTypes.Elatec:
                        lock (syncRoot)
                        {
                            if (instance == null)
                            {
                                instance = new ElatecNetProvider();
                                return instance;
                            }
                            else if (instance != null && !(instance is ElatecNetProvider))
                            {
                                instance = new ElatecNetProvider();
                                return instance;
                            }
                            else
                            {
                                return instance;
                            }

                        }

                    case ReaderTypes.None:
                        return null;


                    default:
                        return null;
                }

            }
        }

        private static object syncRoot = new object();
        private static ReaderDevice instance;

        public abstract bool IsConnected { get; }
        public static ReaderTypes Reader { get; set; }
        public static int PortNumber { get; set; }

        public MifareClassicSectorModel Sector { get; set; }
        public MifareClassicDataBlockModel DataBlock { get; set; }

        public GenericChipModel GenericChip { get; set; }
        public MifareDesfireChipModel DesfireChip { get; set; }
        public MifareClassicChipModel ClassicChip { get; set; }

        public ReaderTypes ReaderProvider { get; set; }
        public string ReaderUnitName { get; set; }
        public string ReaderUnitVersion { get; set; }
        public byte[] MifareClassicData { get; set; }
        public bool DataBlockSuccessfullyAuth { get; set; }
        public bool SectorSuccessfullyAuth { get; set; }
        public byte[] MifareDESFireData { get; set; }
        public byte[] FileIDList { get; set; }
        public byte[] MifareUltralightPageData { get; set; }
        public byte MaxNumberOfAppKeys { get; set; }
        public byte KeyVersion { get; set; }
        public DESFireKeyType EncryptionType { get; set; }
        public DESFireFileSettings DesfireFileSettings { get; set; }
        public AccessControl.DESFireKeySettings DesfireAppKeySetting { get; set; }

        #region Common
        public abstract Task<ERROR> ConnectAsync();
        public abstract Task<ERROR> ReadChipPublic();

        #endregion

        #region MifareClassic
        // Mifare Classic Method Definitions
        public abstract Task<ERROR> ReadMifareClassicSingleSector(int sectorNumber, string aKey, string bKey);
        public abstract Task<ERROR> WriteMifareClassicSingleSector(int sectorNumber, string _aKey, string _bKey, byte[] buffer);
        public abstract Task<ERROR> WriteMifareClassicSingleBlock(int _blockNumber, string _aKey, string _bKey, byte[] buffer);
        public abstract Task<ERROR> WriteMifareClassicWithMAD(int _madApplicationID, int _madStartSector,
                                               string _aKeyToUse, string _bKeyToUse, string _aKeyToWrite, string _bKeyToWrite,
                                               string _madAKeyToUse, string _madBKeyToUse, string _madAKeyToWrite, string _madBKeyToWrite,
                                               byte[] buffer, byte _madGPB, AccessControl.SectorAccessBits _sab, bool _useMADToAuth, bool _keyToWriteUseMAD);
        public abstract Task<ERROR> ReadMifareClassicWithMAD(int madApplicationID, string _aKeyToUse, string _bKeyToUse,
                                                string _madAKeyToUse, string _madBKeyToUse, int _length, byte _madGPB,
                                                bool _useMADToAuth, bool _aiToUseIsMAD);
        #endregion

        #region MifareUltralight
        // Mifare Ultralight Method Definitions
        public abstract Task<ERROR> ReadMifareUltralightSinglePage(int _pageNo);
        #endregion

        #region MifareDesfire
        public abstract Task<ERROR> GetMiFareDESFireChipAppIDs(string _appMasterKey = "00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00", DESFireKeyType _keyTypeAppMasterKey = DESFireKeyType.DF_KEY_DES);
        public abstract Task<ERROR> CreateMifareDesfireFile(string _appMasterKey, DESFireKeyType _keyTypeAppMasterKey, FileType_MifareDesfireFileType _fileType, DESFireAccessRights _accessRights, EncryptionMode _encMode,
                                        int _appID, int _fileNo, int _fileSize,
                                        int _minValue = 0, int _maxValue = 1000, int _initValue = 0, bool _isValueLimited = false,
                                        int _maxNbOfRecords = 100);
        public abstract Task<ERROR> ReadMiFareDESFireChipFile(string _appMasterKey, DESFireKeyType _keyTypeAppMasterKey,
                                        string _appReadKey, DESFireKeyType _keyTypeAppReadKey, int _readKeyNo,
                                        string _appWriteKey, DESFireKeyType _keyTypeAppWriteKey, int _writeKeyNo,
                                        EncryptionMode _encMode,
                                        int _fileNo, int _appID, int _fileSize);
        public abstract Task<ERROR> WriteMiFareDESFireChipFile(string _cardMasterKey, DESFireKeyType _keyTypeCardMasterKey,
                                        string _appMasterKey, DESFireKeyType _keyTypeAppMasterKey,
                                        string _appReadKey, DESFireKeyType _keyTypeAppReadKey, int _readKeyNo,
                                        string _appWriteKey, DESFireKeyType _keyTypeAppWriteKey, int _writeKeyNo,
                                        EncryptionMode _encMode,
                                        int _fileNo, int _appID, byte[] _data);
        public abstract Task<ERROR> AuthToMifareDesfireApplication(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumber, int _appID = 0);
        public abstract Task<OperationResult> GetMifareDesfireAppSettings(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumberCurrent = 0, int _appID = 0, bool authenticateBeforeReading = true);

        /// <summary>
        /// Creates a new Application
        /// </summary>
        /// <param name="_keySettingsTarget">byte: KS_CHANGE_KEY_WITH_MK = 0, KS_ALLOW_CHANGE_MK = 1, KS_FREE_LISTING_WITHOUT_MK = 2, KS_FREE_CREATE_DELETE_WITHOUT_MK = 4, KS_CONFIGURATION_CHANGEABLE = 8, KS_DEFAULT = 11, KS_CHANGE_KEY_WITH_TARGETED_KEYNO = 224, KS_CHANGE_KEY_FROZEN = 240</param>
        /// <param name="_keyTypeTargetApplication">byte: 0 = 3DES, 1 = 3K3DES, 2 = AES</param>
        /// <param name="_maxNbKeys">int max. number of keys</param>
        /// <param name="_appID">int application id</param>
        /// <returns>True if the Operation was successful, false otherwise</returns>
        public abstract Task<OperationResult> CreateMifareDesfireApplication(string _piccMasterKey, AccessControl.DESFireKeySettings _keySettingsTarget, DESFireKeyType _keyTypePiccMasterKey, DESFireKeyType _keyTypeTargetApplication, int _maxNbKeys, int _appID, bool authenticateToPICCFirst = true);

        public abstract Task<ERROR> ChangeMifareDesfireApplicationKey(string _applicationMasterKeyCurrent, int _keyNumberCurrent, DESFireKeyType _keyTypeCurrent,
                                        string _oldKeyForChangeKey, string _oldKeyForTargetSlot, string _applicationMasterKeyTarget, int selectedDesfireAppKeyVersionTargetAsIntint,
                                        DESFireKeyType _keyTypeTarget, int _appIDCurrent, int _appIDTarget,
                                        AccessControl.DESFireKeySettings keySettings, int keyVersion, int numberOfKeys = 0);

        /// <summary>
        /// Updates DESFire key settings without changing key material. Implementations must authenticate with key slot 0 of
        /// the selected PICC or application before applying <paramref name="keySettings"/>.
        /// </summary>
        /// <param name="_applicationMasterKeyCurrent">Master key used to authenticate against slot 0.</param>
        /// <param name="_keyNumberCurrent">Ignored for settings updates; slot 0 is always used.</param>
        /// <param name="_keyTypeCurrent">Cryptographic type of the current master key.</param>
        /// <param name="_applicationMasterKeyTarget">Not required because settings updates do not change keys.</param>
        /// <param name="_keyNumberTarget">Unused placeholder for compatibility.</param>
        /// <param name="selectedDesfireAppKeyVersionTargetAsIntint">Unused placeholder for compatibility.</param>
        /// <param name="_keyTypeTarget">Desired key type metadata to submit alongside the settings byte.</param>
        /// <param name="_appIDCurrent">Application identifier to select (0 targets the PICC level).</param>
        /// <param name="_appIDTarget">Unused placeholder for compatibility.</param>
        /// <param name="keySettings">Key settings byte to apply.</param>
        /// <param name="keyVersion">Unused placeholder for compatibility.</param>
        /// <returns>Status of the key settings update.</returns>
        public abstract Task<ERROR> ChangeMifareDesfireApplicationKeySettings(string _applicationMasterKeyCurrent, int _keyNumberCurrent, DESFireKeyType _keyTypeCurrent,
                                        string _applicationMasterKeyTarget, int selectedDesfireAppKeyVersionTargetAsIntint,
                                        DESFireKeyType _keyTypeTarget, int _appIDCurrent, int _appIDTarget, AccessControl.DESFireKeySettings keySettings, int keyVersion);

        public abstract Task<ERROR> DeleteMifareDesfireApplication(string _applicationMasterKey, DESFireKeyType _keyType, uint _appID = 0);
        public abstract Task<ERROR> DeleteMifareDesfireFile(string _applicationMasterKey, DESFireKeyType _keyType, int _appID = 0, int _fileID = 0);
        public abstract Task<ERROR> FormatDesfireCard(string _applicationMasterKey, DESFireKeyType _keyType);
        public abstract Task<ERROR> GetMifareDesfireFileList(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumberCurrent = 0, int _appID = 0);
        public abstract Task<ERROR> GetMifareDesfireFileSettings(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumberCurrent = 0, int _appID = 0, int _fileNo = 0);

        #endregion

        protected abstract void Dispose(bool disposing);
        public abstract void Dispose();
    }
}
