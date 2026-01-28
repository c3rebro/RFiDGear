using Elatec.NET.Cards.Mifare;
using LibLogicalAccess;
using RFiDGear.Infrastructure.AccessControl;
using RFiDGear.Infrastructure.FileAccess;
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
                            using (SettingsReaderWriter srw = new SettingsReaderWriter())
                            {
                                var settings = srw.DefaultSpecification;

                                if (instance == null)
                                {      
                                    instance = new LibLogicalAccessProvider(Reader, settings.DefaultReaderName);
                                    return instance;
                                }
                                else if (instance != null && !(instance is LibLogicalAccessProvider))
                                {
                                    instance = new LibLogicalAccessProvider(Reader, settings.DefaultReaderName);
                                    return instance;
                                }
                                else
                                {
                                    return instance;
                                }
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

        // Common Method Definitions
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

        // Todo: add XML comments
        public abstract Task<ERROR> WriteMifareClassicWithMAD(int _madApplicationID, int _madStartSector,
                                               string _aKeyToUse, string _bKeyToUse, string _aKeyToWrite, string _bKeyToWrite,
                                               string _madAKeyToUse, string _madBKeyToUse, string _madAKeyToWrite, string _madBKeyToWrite,
                                               byte[] buffer, byte _madGPB, AccessControl.SectorAccessBits _sab, bool _useMADToAuth, bool _keyToWriteUseMAD);

        // Todo: add XML comments
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

        /// <summary>
        /// Reads a MIFARE DESFire file from the specified application using the provided application keys.
        /// </summary>
        /// <param name="_appReadKey">The application read key used to authenticate before reading.</param>
        /// <param name="_keyTypeAppReadKey">The key type for the application read key.</param>
        /// <param name="_readKeyNo">The key number for the application read key.</param>
        /// <param name="_appWriteKey">The application write key configured for the file.</param>
        /// <param name="_keyTypeAppWriteKey">The key type for the application write key.</param>
        /// <param name="_writeKeyNo">The key number for the application write key.</param>
        /// <param name="_encMode">The communication mode to use when reading the file.</param>
        /// <param name="_fileNo">The file number to read.</param>
        /// <param name="_appID">The application ID that owns the file.</param>
        /// <param name="_fileSize">The number of bytes to read from the file.</param>
        /// <returns cref="ERROR">Result of the operation.</returns>
        public abstract Task<ERROR> ReadMiFareDESFireChipFile(string _appReadKey, DESFireKeyType _keyTypeAppReadKey, int _readKeyNo,
                                        EncryptionMode _encMode,
                                        int _fileNo, int _appID, int _fileSize);

        /// <summary>
        /// Writes data to a MIFARE DESFire file in the specified application using the provided application keys.
        /// </summary>
        /// <param name="_appReadKey">The application read key configured for the file.</param>
        /// <param name="_keyTypeAppReadKey">The key type for the application read key.</param>
        /// <param name="_readKeyNo">The key number for the application read key.</param>
        /// <param name="_appWriteKey">The application write key used to authenticate before writing.</param>
        /// <param name="_keyTypeAppWriteKey">The key type for the application write key.</param>
        /// <param name="_writeKeyNo">The key number for the application write key.</param>
        /// <param name="_encMode">The communication mode to use when writing the file.</param>
        /// <param name="_fileNo">The file number to write.</param>
        /// <param name="_appID">The application ID that owns the file.</param>
        /// <param name="_data">The data to write to the file.</param>
        /// <returns cref="ERROR">Result of the operation.</returns>
        public abstract Task<ERROR> WriteMiFareDESFireChipFile(
                                        string _appWriteKey, DESFireKeyType _keyTypeAppWriteKey, int _writeKeyNo,
                                        EncryptionMode _encMode,
                                        int _fileNo, int _appID, byte[] _data);
        /// <summary>
        /// Try to authenticate to a MIFARE DESFire application. If _appID is omitted, it authenticates to _appId == 0 - the PICC level.
        /// </summary>
        /// <param name="_applicationMasterKey">The 16-bytes long key of the application.<</param>
        /// <param name="_keyType">The targeted app key type: 3DES, 3K3DES, AES.</param>
        /// <param name="_keyNumber">The Key Number used for Authentication. (KeyNo == 0 is the App's AppMasterKey / AMK)</param>
        /// <param name="_appID">The application ID for which authentication should be attempted.</param>
        /// <returns cref="ERROR">Result of the operation.</returns>
        public abstract Task<ERROR> AuthToMifareDesfireApplication(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumber, int _appID = 0);

        /// <summary>
        /// Tries to get the settings of a MIFARE DESFire application.
        /// </summary>
        /// <param name="_applicationMasterKey">The 16-bytes long key of the application.<</param>
        /// <param name="_keyType">The targeted app key type: 3DES, 3K3DES, AES.</param>
        /// <param name="_keyNumber">The Key Number used for Authentication. (KeyNo == 0 is the App's AppMasterKey / AMK)</param>
        /// <param name="_appID">The Application ID whose settings should be read</param>
        /// <param name="authenticateBeforeReading">Determines if authentication with keyNo = 0 should be performed before reading</param>
        /// <returns cref="ERROR">Result of the operation.</returns>
        public abstract Task<OperationResult> GetMifareDesfireAppSettings(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumber = 0, int _appID = 0, bool authenticateBeforeReading = true);

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
        /// <param name="_keyTypeTargetApplication">The targeted app key type: 3DES, 3K3DES, AES.</param>
        /// <param name="_maxNbKeys">Number of keys created in the application.</param>
        /// <param name="_appID">Application ID for the new app.</param>
        /// <param name="authenticateToPICCFirst">Should authentication be omitted or performed?</param>
        /// <returns>The operation result, including metadata about authentication and app creation.</returns>
        public abstract Task<OperationResult> CreateMifareDesfireApplication(string _piccMasterKey, AccessControl.DESFireKeySettings _keySettingsTarget, DESFireKeyType _keyTypePiccMasterKey, DESFireKeyType _keyTypeTargetApplication, int _maxNbKeys, int _appID, bool authenticateToPICCFirst = true);

        /// <summary>
        /// Changes a MIFARE DESFire key on the currently presented card.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method supports both PICC-level keys (<paramref name="appId"/> = 0) and application-level keys
        /// (<paramref name="appId"/> &gt; 0).
        /// </para>
        /// <para>
        /// The implementation will authenticate before changing the key. The authentication key number is chosen
        /// from <paramref name="keySettings"/> (your configured "change-key policy"):
        /// </para>
        /// <list type="bullet">
        ///   <item>
        ///     <description>
        ///     If <paramref name="keySettings"/> indicates "change key with targeted key number", the provider authenticates
        ///     with <paramref name="targetKeyNo"/> using <paramref name="currentTargetKeyHex"/>.
        ///     </description>
        ///   </item>
        ///   <item>
        ///     <description>
        ///     Otherwise the provider authenticates with key 0 (master key) using <paramref name="masterKeyHex"/>.
        ///     </description>
        ///   </item>
        /// </list>
        /// <para>
        /// This method performs <b>key changes only</b>. It does not alter key settings, key type, or number-of-keys.
        /// Some reader APIs (not the card itself) require additional context values (e.g., number of keys) when issuing
        /// a key change; providers should obtain those values from the card when possible and only fall back to configured values.
        /// </para>
        /// </remarks>
        /// <param name="appId">
        /// The DESFire application identifier to select before changing the key.
        /// Use <c>0</c> to target PICC-level keys; use a non-zero AID to target an application.
        /// </param>
        /// <param name="targetKeyNo">
        /// The key number to be changed within the selected scope (PICC or application).
        /// </param>
        /// <param name="targetKeyType">
        /// The cryptographic type of the <paramref name="targetKeyNo"/> slot (e.g., DES/3DES/AES as represented by your enum).
        /// Providers may use this type for validation, authentication (when policy requires self-key auth), and for passing
        /// type metadata to reader APIs.
        /// </param>
        /// <param name="currentTargetKeyHex">
        /// The current value (old value) of the target key slot, as a hex string.
        /// This value is required by some providers to perform the change-key operation and is also used for authentication
        /// when <paramref name="keySettings"/> requires authenticating with the targeted key number.
        /// </param>
        /// <param name="newTargetKeyHex">
        /// The desired new key value to write into the target key slot, as a hex string.
        /// </param>
        /// <param name="newTargetKeyVersion">
        /// Version byte to associate with the new key value.
        /// Some providers use this directly; others may ignore it if their API does not expose versioning for the change-key call.
        /// </param>
        /// <param name="masterKeyHex">
        /// The master key value (key 0) for the selected scope (PICC master key when <paramref name="appId"/> is 0,
        /// application master key when <paramref name="appId"/> &gt; 0).
        /// This is used when <paramref name="keySettings"/> requires authenticating with key 0 (master key).
        /// </param>
        /// <param name="masterKeyType">
        /// Cryptographic type of the master key value in <paramref name="masterKeyHex"/>.
        /// </param>
        /// <param name="keySettings">
        /// The (current) key settings for the selected scope, used to determine which key number must be authenticated
        /// before changing keys (e.g., master-key policy vs self-key policy).
        /// This value should reflect the card/app configuration, not an intended "new settings" value.
        /// </param>
        /// <returns>
        /// <see cref="ERROR.NoError"/> on success; otherwise an <see cref="ERROR"/> describing the failure.
        /// </returns>
        public abstract Task<ERROR> ChangeMifareDesfireKeyAsync(
            uint appId,
            byte targetKeyNo,
            DESFireKeyType targetKeyType,
            string currentTargetKeyHex,
            string newTargetKeyHex,
            byte newTargetKeyVersion,
            string masterKeyHex,
            DESFireKeyType masterKeyType,
            AccessControl.DESFireKeySettings keySettings);

        /// <summary>
        /// Updates DESFire key settings without changing key material. Implementations must authenticate with key 0 of
        /// the selected PICC or application before applying <paramref name="keySettings"/>.
        /// </summary>
        /// <param name="_applicationMasterKeyCurrent">Master key used to authenticate against the selected Apps, KeyNo 0</param>
        /// <param name="_keyNumberCurrent">Ignored for settings updates; key 0 is always used.</param>
        /// <param name="_keyTypeCurrent">Cryptographic type of the current master key.</param>
        /// <param name="selectedDesfireAppKeyVersionTargetAsIntint">Unused placeholder for compatibility.</param>
        /// <param name="_keyTypeTarget">Desired key type metadata to submit alongside the settings byte.</param>
        /// <param name="_appIDCurrent">Application identifier to select (0 targets the PICC level).</param>
        /// <param name="keySettings">Key settings byte to apply.</param>
        /// <returns>Status of the key settings update.</returns>
        public abstract Task<ERROR> ChangeMifareDesfireApplicationKeySettings(string _applicationMasterKeyCurrent, int _keyNumberCurrent, DESFireKeyType _keyTypeCurrent,
            int _appIDCurrent, AccessControl.DESFireKeySettings keySettings);

        /// <summary>
        /// Deletes a MIFARE DESFire application after authenticating to PICC Lvl (AppID = 0, KeyNo = 0).
        /// </summary>
        /// <param name="_applicationMasterKey">The 16 byte PICC master key, used to authenticate.</param>
        /// <param name="_keyType">The PICC MasterKey Type (byte): 3DES, 3K3DES, AES</param>
        /// <param name="_appID">The AppId to delete</param>
        /// <returns></returns>
        public abstract Task<ERROR> DeleteMifareDesfireApplication(string _applicationMasterKey, DESFireKeyType _keyType, uint _appID);

        /// <summary>
        /// Deletes a MIFARE DESFire file inside an application after authenticating to the application.
        /// </summary>
        /// <param name="_applicationMasterKey">Master key used to authenticate against the selected Apps, KeyNo 0</param>
        /// <param name="_keyType">Cryptographic type of the current master key. (byte): 3DES, 3K3DES, AES</param>
        /// <param name="_appID">The app ID that contains the file to be deleted.</param>
        /// <param name="_fileID">The ID of the File that should be deleted.</param>
        /// <returns></returns>
        public abstract Task<ERROR> DeleteMifareDesfireFile(string _applicationMasterKey, DESFireKeyType _keyType, int _appID = 0, int _fileID = 0);

        /// <summary>
        /// Authenticates to and formats a Desfire Card
        /// </summary>
        /// <param name="_applicationMasterKey">The PICC MasterKey</param>
        /// <param name="_keyType">The PICC MasterKey Type (byte): 3DES, 3K3DES, AES</param>
        /// <returns cref="ERROR">Result of the operation.</returns>
        public abstract Task<ERROR> FormatDesfireCard(string _applicationMasterKey, DESFireKeyType _keyType);

        /// <summary>
        /// Gets the file list of a MIFARE DESFire application.
        /// </summary>
        /// <param name="_applicationMasterKey"></param>
        /// <param name="_keyType"></param>
        /// <param name="_keyNumberCurrent"></param>
        /// <param name="_appID"></param>
        /// <returns></returns>
        public abstract Task<ERROR> GetMifareDesfireFileList(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumberCurrent = 0, int _appID = 0);

        /// <summary>
        /// Gets the file settings of a MIFARE DESFire file inside an application.
        /// </summary>
        /// <param name="_applicationMasterKey"></param>
        /// <param name="_keyType"></param>
        /// <param name="_keyNumberCurrent"></param>
        /// <param name="_appID"></param>
        /// <param name="_fileNo"></param>
        /// <returns></returns>
        public abstract Task<ERROR> GetMifareDesfireFileSettings(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumberCurrent = 0, int _appID = 0, int _fileNo = 0);

        /// <summary>
        /// Commits a DESFire transaction for FileType BACKUP, VALUE and RECORD.
        /// </summary>
        /// <returns></returns>
        public abstract Task<ERROR> CommitTransactionAsync();

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
