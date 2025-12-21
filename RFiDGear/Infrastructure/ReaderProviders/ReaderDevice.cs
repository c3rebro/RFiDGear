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
        /// <summary>
        /// Reads a single MIFARE Classic sector after authenticating with the supplied keys. It checks both A and B keys for authentication.
        /// </summary>
        /// <param name="sectorNumber">
        /// The sector number to read. Note: Every sector above sec32 (MIFARE 4K) is 4 times bigger than the lower sectors. They expect the
        /// sector number to be multiplied by 4. So sector 33 is (33 - 32) * 4 + 32 = 36 dec, sector 38 is (38 - 32) * 4 + 32 = 56 dec, and so on.
        /// </param>
        /// <param name="aKey">The primary key for authentication.</param>
        /// <param name="bKey">The fallback key for authentication.</param>
        /// <returns>The normalized error code that describes the outcome.</returns>
        public abstract Task<ERROR> ReadMifareClassicSingleSector(int sectorNumber, string aKey, string bKey);

        /// <summary>
        /// Writes a single MIFARE Classic sector after authenticating with the supplied keys. It checks both A and B keys for authentication.
        /// </summary>
        /// <param name="sectorNumber">
        /// The sector number to write. Note: Every sector above sec32 (MIFARE 4K) is 4 times bigger than the lower sectors. They expect the
        /// sector number to be multiplied by 4. So sector 33 is (33 - 32) * 4 + 32 = 36 dec, sector 38 is (38 - 32) * 4 + 32 = 56 dec, and so on.
        /// </param>
        /// <param name="aKey">The primary key for authentication.</param>
        /// <param name="bKey">The fallback key for authentication.</param>
        /// <param name="buffer">The payload to write.</param>
        /// <returns>The normalized error code that describes the outcome.</returns>
        public abstract Task<ERROR> WriteMifareClassicSingleSector(int sectorNumber, string aKey, string bKey, byte[] buffer);

        /// <summary>
        /// Writes a single MIFARE Classic block after authenticating with the supplied keys. It checks both A and B keys for authentication.
        /// </summary>
        /// <param name="_blockNumber">The chip-based block number to write.</param>
        /// <param name="aKey">The primary key for authentication.</param>
        /// <param name="bKey">The fallback key for authentication.</param>
        /// <param name="buffer">The 16-byte payload to write.</param>
        /// <returns>The normalized error code that describes the outcome.</returns>
        public abstract Task<ERROR> WriteMifareClassicSingleBlock(int _blockNumber, string aKey, string bKey, byte[] buffer);
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

        /// <summary>
        /// Try to read the AppIds on a MIFARE DESFire chip. Try with and without authentication. If authentication fails, an empty list is returned.
        /// </summary>
        /// <param name="_appMasterKey">The PICC master key to try when authentication is required.</param>
        /// <param name="_keyTypeAppMasterKey">The key type for the PICC master key.</param>
        /// <returns cref="ERROR">Result of the operation.</returns>
        public abstract Task<ERROR> GetMiFareDESFireChipAppIDs(string _appMasterKey = "00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00", DESFireKeyType _keyTypeAppMasterKey = DESFireKeyType.DF_KEY_DES);

        /// <summary>
        /// Creates a MIFARE DESFire file inside an application.
        /// </summary>
        /// <param name="_appMasterKey">The key 0 (app master key) of the application.</param>
        /// <param name="_keyTypeAppMasterKey">The app master key type (_appID != 0, keyNo = 0), used to authenticate: 3DES, 3K3DES, AES.</param>
        /// <param name="_fileType">The type of file to create.</param>
        /// <param name="_accessRights">Determines what key to use for read, write, and change operations.</param>
        /// <param name="_encMode">The communication mode (commSet) to use. File communication needs encryption: CM_PLAIN, CM_MAC, CM_ENCRYPT, CM_UNKNOWN.</param>
        /// <param name="_appID">The application that needs to be selected to create the file.</param>
        /// <param name="_fileNo">The file number to be created inside the application.</param>
        /// <param name="_fileSize">The size of the file to be created.</param>
        /// <param name="_minValue">Lower limit of a record file type.</param>
        /// <param name="_maxValue">Upper limit of a record file type.</param>
        /// <param name="_initValue">Initial value of a record file type.</param>
        /// <param name="_isValueLimited">Whether the value file enforces limits.</param>
        /// <param name="_maxNbOfRecords">Maximum number of records for record-based file types.</param>
        /// <returns cref="ERROR">Result of the operation.</returns>
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
        /// Creates a new application, authenticating to the PICC first and trying to create the application anyway if authentication fails.
        /// </summary>
        /// <param name="_piccMasterKey">The 16 byte PICC master key, used to authenticate.</param>
        /// <param name="_keySettingsTarget">
        /// Key settings byte: KS_CHANGE_KEY_WITH_MK = 0, KS_ALLOW_CHANGE_MK = 1, KS_FREE_LISTING_WITHOUT_MK = 2,
        /// KS_FREE_CREATE_DELETE_WITHOUT_MK = 4, KS_CONFIGURATION_CHANGEABLE = 8, KS_DEFAULT = 11,
        /// KS_CHANGE_KEY_WITH_TARGETED_KEYNO = 224, KS_CHANGE_KEY_FROZEN = 240.
        /// </param>
        /// <param name="_keyTypePiccMasterKey">The PICC master key type used to authenticate: 3DES, 3K3DES, AES.</param>
        /// <param name="_keyTypeTargetApplication">The targeted app key type: 0 = 3DES, 64 = 3K3DES, 128 = AES.</param>
        /// <param name="_maxNbKeys">Number of keys created in the application.</param>
        /// <param name="_appID">Application ID for the new app.</param>
        /// <param name="authenticateToPICCFirst">Should authentication be omitted or performed?</param>
        /// <returns>The operation result, including metadata about authentication and app creation.</returns>
        public abstract Task<OperationResult> CreateMifareDesfireApplication(string _piccMasterKey, AccessControl.DESFireKeySettings _keySettingsTarget, DESFireKeyType _keyTypePiccMasterKey, DESFireKeyType _keyTypeTargetApplication, int _maxNbKeys, int _appID, bool authenticateToPICCFirst = true);

        /// <summary>
        /// Changes a DESFire application key
        /// </summary>
        /// <param name="_applicationMasterKeyCurrent"></param>
        /// <param name="_keyNumberCurrent"></param>
        /// <param name="_keyTypeCurrent"></param>
        /// <param name="_oldKeyForChangeKey"></param>
        /// <param name="_oldKeyForTargetSlot"></param>
        /// <param name="_applicationMasterKeyTarget"></param>
        /// <param name="selectedDesfireAppKeyVersionTargetAsIntint"></param>
        /// <param name="_keyTypeTarget"></param>
        /// <param name="_appIDCurrent"></param>
        /// <param name="_appIDTarget"></param>
        /// <param name="keySettings"></param>
        /// <param name="keyVersion"></param>
        /// <param name="numberOfKeys"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Authenticates to and formats a Desfire Card
        /// </summary>
        /// <param name="_applicationMasterKey">The PICC MasterKey</param>
        /// <param name="_keyType">The PICC MasterKey Type (byte): 3DES, 3K3DES, AES</param>
        /// <returns></returns>
        public abstract Task<ERROR> FormatDesfireCard(string _applicationMasterKey, DESFireKeyType _keyType);
        public abstract Task<ERROR> GetMifareDesfireFileList(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumberCurrent = 0, int _appID = 0);
        public abstract Task<ERROR> GetMifareDesfireFileSettings(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumberCurrent = 0, int _appID = 0, int _fileNo = 0);

        /// <summary>
        /// Get the key version of a DESFire key.
        /// </summary>
        /// <param name="keyNo">Key number</param>
        /// <returns>Key version (byte)</returns>
        /// <exception cref="ReaderException"></exception>
        public abstract Task<byte> MifareDesfire_GetKeyVersionAsync(byte keyNo);

        #endregion

        protected abstract void Dispose(bool disposing);
        public abstract void Dispose();
    }
}
