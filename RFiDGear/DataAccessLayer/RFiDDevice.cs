using LibLogicalAccess;
using RFiDGear.DataAccessLayer;
using RFiDGear.Model;
using System;
using System.Threading;

namespace RFiDGear
{
    /// <summary>
    /// Description of RFiDAccess.
    ///
    /// Initialize Reader
    /// </summary>
    ///
    public class RFiDDevice : IDisposable
    {
        // global (cross-class) Instances go here ->
        private IReaderProvider readerProvider;

        private IReaderUnit readerUnit;
        private chip card;

        private string chipType;
        private string chipUID;

        private byte[] cardDataBlock;
        private byte[][] cardDataSector;
        private bool[] blockAuthSuccessful;
        private bool[] blockReadSuccessful;
        private bool sectorIsKeyAAuthSuccessful;
        private bool sectorIsKeyBAuthSuccessful;
        private bool sectorCanRead;

        private byte[] desFireFileData;

        private bool _disposed = false;

        #region properties

        public MifareClassicSectorModel Sector { get; private set; }

        public ReaderTypes ReaderProvider { get; private set; }

        public string ReaderUnitName { get; private set; }

        public CARD_INFO CardInfo { get; private set; }

        public byte[][] currentSector { get; private set; }

        public byte[] currentDataBlock { get; private set; }

        public bool[] DataBlockSuccessfullyRead { get; private set; }

        public bool[] DataBlockSuccesfullyAuth { get; private set; }

        public bool SectorSuccessfullyRead { get; private set; }

        public bool SectorSuccesfullyAuth { get; private set; }

        public byte[] GetDESFireFileData { get; private set; }

        public uint[] AppIDList { get; private set; }

        public byte[] FileIDList { get; private set; }

        public byte MaxNumberOfAppKeys { get; private set; }

        public uint FreeMemory { get; private set; }

        public FileSetting DesfireFileSetting { get; private set; }

        public DESFireKeySettings DesfireAppKeySetting { get; private set; }

        #endregion properties

        #region contructor

        public static RFiDDevice Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new RFiDDevice();
                    return instance;
                }
                else
                    return null;
            }
        }

        private static RFiDDevice instance;

        public RFiDDevice() : this(ReaderTypes.None)
        {
        }

        public RFiDDevice(ReaderTypes _readerType = ReaderTypes.None)
        {
            try
            {
                using (SettingsReaderWriter defaultSettings = new SettingsReaderWriter())
                {
                    ReaderProvider = _readerType != ReaderTypes.None ? _readerType : defaultSettings.DefaultSpecification.DefaultReaderProvider;

                    readerProvider = new LibraryManagerClass().GetReaderProvider(Enum.GetName(typeof(ReaderTypes), ReaderProvider));
                    readerUnit = readerProvider.CreateReaderUnit();
                }
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""));
            }
        }

        #endregion contructor

        #region common

        public ERROR ChangeProvider(ReaderTypes _provider)
        {
            if (Enum.IsDefined(typeof(ReaderTypes), _provider))
            {
                if (readerProvider != null)
                {
                    try
                    {
                        readerUnit.DisconnectFromReader();
                        readerProvider.ReleaseInstance();
                    }
                    catch
                    {
                        return ERROR.IOError;
                    }
                }

                ReaderProvider = _provider;

                try
                {
                    readerProvider = new LibraryManagerClass().GetReaderProvider(Enum.GetName(typeof(ReaderTypes), ReaderProvider));
                    readerUnit = readerProvider.CreateReaderUnit();

                    return ERROR.NoError;
                }
                catch
                {
                    return ERROR.IOError;
                }
            }

            return ERROR.IOError;
        }

        public ERROR ReadChipPublic()
        {
            try
            {
                if (readerUnit.ConnectToReader())
                {
                    if (readerUnit.WaitInsertion(Constants.MAX_WAIT_INSERTION))
                    {
                        if (readerUnit.Connect())
                        {
                            ReaderUnitName = readerUnit.ConnectedName;
                            //string readerSerialNumber = readerUnit.GetReaderSerialNumber(); //-> ERROR with OmniKey (and some others?) Reader when card isnt removed before recalling!

                            card = readerUnit.GetSingleChip();

                            if (card.ChipIdentifier != chipUID && card.ChipIdentifier.Length != 0)
                            {
                                CARD_TYPE type;
                                Enum.TryParse(card.Type, out type);
                                CardInfo = new CARD_INFO(type, card.ChipIdentifier);

                                return ERROR.NoError;
                            }
                            else
                                return ERROR.DeviceNotReadyError;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                if (readerProvider != null)
                    readerProvider.ReleaseInstance();

                LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""));

                return ERROR.NoError;
            }

            return ERROR.IOError;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    instance = null;
                    // Dispose any managed objects
                    // ...
                }

                if (readerUnit != null)
                {
                    readerUnit.Disconnect();
                    readerUnit.DisconnectFromReader();
                }

                if (readerProvider != null)
                    readerProvider.ReleaseInstance();
                // Now disposed of any unmanaged objects
                // ...

                Thread.Sleep(200);
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion common

        #region mifare classic

        public ERROR ReadMiFareClassicSingleSector(int sectorNumber, string aKey, string bKey)
        {
            var settings = new SettingsReaderWriter();
            Sector = new MifareClassicSectorModel();

            settings.ReadSettings();

            var keyA = new MifareKey() { Value = CustomConverter.KeyFormatQuickCheck(aKey) ? aKey : CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(aKey) };
            var keyB = new MifareKey() { Value = CustomConverter.KeyFormatQuickCheck(bKey) ? bKey : CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(bKey) };

            int blockCount = 0;
            int dataBlockNumber = 0;
            sectorIsKeyAAuthSuccessful = true;
            sectorIsKeyBAuthSuccessful = false;
            sectorCanRead = true;

            try
            {
                if (readerUnit.ConnectToReader())
                {
                    if (readerUnit.WaitInsertion(Constants.MAX_WAIT_INSERTION))
                    {
                        if (readerUnit.Connect())
                        {
                            ReaderUnitName = readerUnit.ConnectedName;
                            //readerSerialNumber = readerUnit.GetReaderSerialNumber();

                            card = readerUnit.GetSingleChip();

                            if (card.ChipIdentifier != chipUID && card.ChipIdentifier.Length != 0)
                            {
                                chipUID = card.ChipIdentifier;
                                chipType = card.Type;
                            }

                            if (card.Type == "Mifare1K")
                            {
                                blockAuthSuccessful = new bool[64];
                                blockReadSuccessful = new bool[64];

                                blockCount = 4;

                                cardDataBlock = new byte[16];
                                cardDataSector = new byte[4][];
                            }
                            if (card.Type == "Mifare4K")
                            {
                                blockAuthSuccessful = new bool[256];
                                blockReadSuccessful = new bool[256];

                                blockCount = (sectorNumber <= 31 ? 4 : 16);

                                cardDataBlock = new byte[16];
                                cardDataSector = new byte[16][];
                            }

                            var cmd = card.Commands as IMifareCommands;

                            dataBlockNumber = sectorNumber <= 31
                                ? (((sectorNumber + 1) * blockCount) - (blockCount - dataBlockNumber))
                                : ((128 + (sectorNumber - 31) * blockCount) - (blockCount - dataBlockNumber));

                            try
                            { //try to Auth with Keytype A
                                for (int k = 0; k < blockCount; k++)
                                {
                                    cmd.LoadKeyNo((byte)0, keyA, MifareKeyType.KT_KEY_A); // FIXME "sectorNumber" to 0

                                    var dataBlock = new MifareClassicDataBlockModel();
                                    dataBlock.BlockNumber = dataBlockNumber + k;

                                    try
                                    {
                                        cmd.AuthenticateKeyNo((byte)(dataBlockNumber + k), (byte)0, MifareKeyType.KT_KEY_A); // FIXME same as '303
                                        blockAuthSuccessful[dataBlockNumber + k] = true;

                                        Sector.IsAuthenticated = true;

                                        try
                                        {
                                            object data = cmd.ReadBinary((byte)(dataBlockNumber + k), 48);

                                            cardDataBlock = (byte[])data;
                                            cardDataSector[k] = cardDataBlock;

                                            blockReadSuccessful[dataBlockNumber + k] = true;

                                            dataBlock.IsAuthenticated = true;
                                            dataBlock.Data = (byte[])data;

                                            Sector.DataBlock.Add(dataBlock);
                                        }
                                        catch
                                        {
                                            dataBlock.IsAuthenticated = false;
                                            Sector.DataBlock.Add(dataBlock);

                                            blockReadSuccessful[dataBlockNumber + k] = false;
                                            sectorCanRead = false;
                                        }
                                    }
                                    catch
                                    { // Try Auth with keytype b
                                        sectorIsKeyAAuthSuccessful = false;

                                        try
                                        {
                                            cmd.LoadKeyNo((byte)1, keyB, MifareKeyType.KT_KEY_B);

                                            cmd.AuthenticateKeyNo((byte)(dataBlockNumber + k), (byte)1, MifareKeyType.KT_KEY_B); // FIXME same as '303
                                            blockAuthSuccessful[dataBlockNumber + k] = true;
                                            sectorIsKeyBAuthSuccessful = true;

                                            Sector.IsAuthenticated = true;

                                            try
                                            {
                                                object data = cmd.ReadBinary((byte)(dataBlockNumber + k), 48);

                                                cardDataBlock = (byte[])data;
                                                cardDataSector[k] = cardDataBlock;

                                                blockReadSuccessful[dataBlockNumber + k] = true;
                                                dataBlock.IsAuthenticated = true;
                                                dataBlock.Data = (byte[])data;

                                                Sector.DataBlock.Add(dataBlock);
                                            }
                                            catch
                                            {
                                                dataBlock.IsAuthenticated = false;

                                                Sector.DataBlock.Add(dataBlock);

                                                blockReadSuccessful[dataBlockNumber + k] = false;
                                                sectorCanRead = false;

                                                return ERROR.AuthenticationError;
                                            }
                                        }
                                        catch
                                        {
                                            Sector.IsAuthenticated = false;
                                            dataBlock.IsAuthenticated = false;

                                            Sector.DataBlock.Add(dataBlock);

                                            blockAuthSuccessful[dataBlockNumber + k] = false;
                                            sectorIsKeyBAuthSuccessful = false;

                                            return ERROR.AuthenticationError;
                                        }
                                    }
                                }
                            }
                            catch
                            {
                                return ERROR.NoError;
                            }
                            return ERROR.NoError;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""));
                return ERROR.AuthenticationError;
            }
            return ERROR.NoError;
        }

        public ERROR WriteMiFareClassicSingleSector(int sectorNumber, string _aKey, string _bKey, byte[] buffer)
        {
            var keyA = new MifareKey() { Value = CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(_aKey) };
            var keyB = new MifareKey() { Value = CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(_bKey) };

            int blockCount = 0;
            int dataBlockNumber = 0;

            sectorIsKeyAAuthSuccessful = true;
            sectorIsKeyBAuthSuccessful = false;
            sectorCanRead = true;

            try
            {
                if (readerUnit.ConnectToReader())
                {
                    if (readerUnit.WaitInsertion(Constants.MAX_WAIT_INSERTION))
                    {
                        if (readerUnit.Connect())
                        {
                            ReaderUnitName = readerUnit.ConnectedName;
                            //readerSerialNumber = readerUnit.GetReaderSerialNumber();

                            card = readerUnit.GetSingleChip();

                            if (card.ChipIdentifier != chipUID && card.ChipIdentifier.Length != 0)
                            {
                                chipUID = card.ChipIdentifier;
                                chipType = card.Type;
                            }

                            if (card.Type == "Mifare1K")
                            {
                                blockAuthSuccessful = new bool[64];
                                blockReadSuccessful = new bool[64];

                                blockCount = 4;

                                cardDataBlock = new byte[16];
                                cardDataSector = new byte[4][];
                            }
                            if (card.Type == "Mifare4K")
                            {
                                blockAuthSuccessful = new bool[256];
                                blockReadSuccessful = new bool[256];

                                blockCount = (sectorNumber <= 31 ? 4 : 16);

                                cardDataBlock = new byte[16];
                                cardDataSector = new byte[16][];
                            }

                            var cmd = card.Commands as IMifareCommands;

                            dataBlockNumber = (sectorNumber <= 31
                                               ? (((sectorNumber + 1) * blockCount) - (blockCount - dataBlockNumber))
                                               : ((128 + (sectorNumber - 31) * blockCount) - (blockCount - dataBlockNumber)));

                            try
                            { //try to Auth with Keytype A
                                cmd.LoadKeyNo((byte)0, keyA, MifareKeyType.KT_KEY_A); // FIXME "sectorNumber" to 0
                                cmd.LoadKeyNo((byte)1, keyB, MifareKeyType.KT_KEY_B); // FIXME "sectorNumber" to 1

                                for (int k = 0; k < blockCount; k++)
                                {
                                    try
                                    {
                                        cmd.AuthenticateKeyNo((byte)(dataBlockNumber + k), (byte)0, MifareKeyType.KT_KEY_A); // FIXME same as '303
                                        blockAuthSuccessful[dataBlockNumber + k] = true;

                                        try
                                        {
                                            cmd.WriteBinary((byte)(dataBlockNumber + k), buffer);

                                            blockReadSuccessful[dataBlockNumber + k] = true;

                                            return ERROR.NoError;
                                        }
                                        catch
                                        {
                                            blockReadSuccessful[dataBlockNumber + k] = false;
                                            sectorCanRead = false;

                                            return ERROR.AuthenticationError;
                                        }
                                    }
                                    catch
                                    { // Try Auth with keytype b
                                        sectorIsKeyAAuthSuccessful = false;

                                        try
                                        {
                                            cmd.AuthenticateKeyNo((byte)(dataBlockNumber + k), (byte)1, MifareKeyType.KT_KEY_B); // FIXME same as '303
                                            blockAuthSuccessful[dataBlockNumber + k] = true;
                                            sectorIsKeyBAuthSuccessful = true;

                                            try
                                            {
                                                object data = cmd.ReadBinary((byte)(dataBlockNumber + k), 48);

                                                cardDataBlock = (byte[])data;
                                                cardDataSector[k] = cardDataBlock;

                                                blockReadSuccessful[dataBlockNumber + k] = true;

                                                return ERROR.NoError;
                                            }
                                            catch
                                            {
                                                blockReadSuccessful[dataBlockNumber + k] = false;
                                                sectorCanRead = false;

                                                return ERROR.AuthenticationError;
                                            }
                                        }
                                        catch
                                        {
                                            blockAuthSuccessful[dataBlockNumber + k] = false;
                                            sectorIsKeyBAuthSuccessful = false;

                                            //return ERROR.AuthenticationError;
                                        }
                                    }
                                }
                            }
                            catch
                            {
                                return ERROR.IOError;
                            }
                            return ERROR.DeviceNotReadyError;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""));

                return ERROR.IOError;
            }
            return ERROR.DeviceNotReadyError;
        }

        public ERROR WriteMiFareClassicSingleBlock(int _blockNumber, string _aKey, string _bKey, byte[] buffer)
        {
            try
            {
                var keyA = new MifareKey() { Value = CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(_aKey) };
                var keyB = new MifareKey() { Value = CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(_bKey) };

                if (readerUnit.ConnectToReader())
                {
                    if (readerUnit.WaitInsertion(Constants.MAX_WAIT_INSERTION))
                    {
                        if (readerUnit.Connect())
                        {
                            ReaderUnitName = readerUnit.ConnectedName;
                            //readerSerialNumber = readerUnit.GetReaderSerialNumber();

                            card = readerUnit.GetSingleChip();

                            if (card.ChipIdentifier != chipUID && card.ChipIdentifier.Length != 0)
                            {
                                chipUID = card.ChipIdentifier;
                                chipType = card.Type;
                            }

                            var cmd = card.Commands as IMifareCommands;

                            try
                            {
                                cmd.LoadKeyNo((byte)0, keyA, MifareKeyType.KT_KEY_A); // FIXME "sectorNumber" to 0

                                try
                                { //try to Auth with Keytype A
                                    cmd.AuthenticateKeyNo((byte)(_blockNumber), (byte)0, MifareKeyType.KT_KEY_A); // FIXME same as '303

                                    cmd.WriteBinary((byte)(_blockNumber), buffer);

                                    return ERROR.NoError;
                                }
                                catch
                                { // Try Auth with keytype b
                                    sectorIsKeyAAuthSuccessful = false;

                                    cmd.LoadKeyNo((byte)0, keyB, MifareKeyType.KT_KEY_B);

                                    try
                                    {
                                        cmd.AuthenticateKeyNo((byte)(_blockNumber), (byte)0, MifareKeyType.KT_KEY_B); // FIXME same as '303

                                        cmd.WriteBinary((byte)(_blockNumber), buffer);

                                        return ERROR.NoError;
                                    }
                                    catch
                                    {
                                        return ERROR.AuthenticationError;
                                    }
                                }
                            }
                            catch
                            {
                                return ERROR.AuthenticationError;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""));

                return ERROR.IOError;
            }
            return ERROR.IOError;
        }

        #endregion mifare classic

        #region mifare ultralight

        public ERROR ReadMifareUltralight()
        {
            try
            {
                if (readerUnit.ConnectToReader())
                {
                    if (readerUnit.WaitInsertion(Constants.MAX_WAIT_INSERTION))
                    {
                        if (readerUnit.Connect())
                        {
                            ReaderUnitName = readerUnit.ConnectedName;
                            //readerSerialNumber = readerUnit.GetReaderSerialNumber();

                            card = readerUnit.GetSingleChip();

                            if (card.Type == "MifareUltralight")
                            {
                                var cmd = card.Commands as MifareUltralightCCommands;// IMifareUltralightCommands;

                                object appIDsObject = cmd.ReadPages(0, 3);
                                //object res = cmd.ReadPage(4);

                                //appIDs = (appIDsObject as UInt32[]);
                            }

                            readerUnit.Disconnect();
                            readerUnit.DisconnectFromReader();
                            readerProvider.ReleaseInstance();
                            return ERROR.NoError;
                        }
                    }
                }
                return ERROR.NoError;
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""));
                return ERROR.IOError;
            }
        }

        #endregion mifare ultralight

        #region mifare desfire

        public ERROR GetMiFareDESFireChipAppIDs()
        {
            try
            {
                // The excepted memory tree
                IDESFireLocation location = new DESFireLocation();
                // File communication requires encryption
                location.SecurityLevel = EncryptionMode.CM_ENCRYPT;

                // Keys to use for authentication
                IDESFireAccessInfo aiToUse = new DESFireAccessInfo();
                aiToUse.MasterCardKey.Value = "00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00";

                if (readerUnit.ConnectToReader())
                {
                    if (readerUnit.WaitInsertion(Constants.MAX_WAIT_INSERTION))
                    {
                        if (readerUnit.Connect())
                        {
                            ReaderUnitName = readerUnit.ConnectedName;
                            //readerSerialNumber = readerUnit.GetReaderSerialNumber();

                            card = readerUnit.GetSingleChip();

                            if (card.Type == "DESFire")
                            {
                                var cmd = card.Commands as IDESFireCommands;

                                object appIDsObject = cmd.GetApplicationIDs();
                                AppIDList = (appIDsObject as UInt32[]);

                                return ERROR.NoError;
                            }
                            if (card.Type == "DESFireEV1" ||
                                card.Type == "DESFireEV2")
                            {
                                var cmd = card.Commands as IDESFireEV1Commands;

                                object appIDsObject = cmd.GetApplicationIDs();
                                AppIDList = (appIDsObject as UInt32[]);
                                FreeMemory = cmd.GetFreeMemory();

                                return ERROR.NoError;
                            }
                            else
                                return ERROR.Empty;
                        }
                    }
                }
                return ERROR.DeviceNotReadyError;
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""));
                return ERROR.IOError;
            }
        }

        public ERROR CreateMifareDesfireFile(string _appMasterKey, DESFireKeyType _keyTypeAppMasterKey, FileType_MifareDesfireFileType _fileType, DESFireAccessRights _accessRights, EncryptionMode _encMode,
                                             int _appID, int _fileNo, int _fileSize,
                                             int _minValue = 0, int _maxValue = 1000, int _initValue = 0, bool _isValueLimited = false,
                                             int _maxNbOfRecords = 100)
        {
            try
            {
                DESFireAccessRights accessRights = _accessRights;
                IDESFireCommands cmd;

                // Keys to use for authentication
                IDESFireAccessInfo aiToUse = new DESFireAccessInfo();
                CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_appMasterKey);
                aiToUse.MasterCardKey.Value = CustomConverter.desFireKeyToEdit;
                aiToUse.MasterCardKey.KeyType = _keyTypeAppMasterKey;

                if (readerUnit.ConnectToReader())
                {
                    if (readerUnit.WaitInsertion(Constants.MAX_WAIT_INSERTION))
                    {
                        if (readerUnit.Connect())
                        {
                            ReaderUnitName = readerUnit.ConnectedName;

                            card = readerUnit.GetSingleChip();

                            if (card.Type == "DESFireEV1")
                            {
                                cmd = card.Commands as IDESFireEV1Commands;
                                try
                                {
                                    //cmd.SelectApplication(0);
                                    //cmd.Authenticate((byte)0, aiToUse.MasterCardKey);

                                    cmd.SelectApplication((uint)_appID);
                                    cmd.Authenticate((byte)0, aiToUse.MasterCardKey);

                                    switch (_fileType)
                                    {
                                        case FileType_MifareDesfireFileType.StdDataFile:
                                            cmd.CreateStdDataFile((byte)_fileNo, _encMode, accessRights, (uint)_fileSize);
                                            break;

                                        case FileType_MifareDesfireFileType.BackupFile:
                                            cmd.CreateBackupFile((byte)_fileNo, _encMode, accessRights, (uint)_fileSize);
                                            break;

                                        case FileType_MifareDesfireFileType.ValueFile:
                                            cmd.CreateValueFile((byte)_fileNo, _encMode, accessRights, (uint)_minValue, (uint)_maxValue, (uint)_initValue, _isValueLimited);
                                            break;

                                        case FileType_MifareDesfireFileType.CyclicRecordFile:
                                            cmd.CreateCyclicRecordFile((byte)_fileNo, _encMode, accessRights, (uint)_fileSize, (uint)_maxNbOfRecords);
                                            break;

                                        case FileType_MifareDesfireFileType.LinearRecordFile:
                                            cmd.CreateLinearRecordFile((byte)_fileNo, _encMode, accessRights, (uint)_fileSize, (uint)_maxNbOfRecords);
                                            break;
                                    }

                                    return ERROR.NoError;
                                }
                                catch
                                {
                                    return ERROR.AuthenticationError;
                                }
                            }
                            if (card.Type == "DESFireEV2")
                            {
                            }
                            return ERROR.AuthenticationError;
                        }
                    }
                }
                return ERROR.IOError;
            }
            catch
            {
                return ERROR.AuthenticationError;
            }
        }

        public ERROR AuthToMifareDesfireApplication(string _applicationMasterKey, DESFireKeyType _keyType, MifareDesfireKeyNumber _keyNumber, int _appID = 0)
        {
            try
            {
                // The excepted memory tree
                IDESFireLocation location = new DESFireLocation();
                // The Application ID to use
                location.aid = _appID;
                // File communication requires encryption
                location.SecurityLevel = EncryptionMode.CM_ENCRYPT;

                IDESFireEV1Commands cmd;
                // Keys to use for authentication
                IDESFireAccessInfo aiToUse = new DESFireAccessInfo();
                CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_applicationMasterKey);
                aiToUse.MasterCardKey.Value = CustomConverter.desFireKeyToEdit;
                aiToUse.MasterCardKey.KeyType = _keyType;

                if (readerUnit.ConnectToReader())
                {
                    if (readerUnit.WaitInsertion(Constants.MAX_WAIT_INSERTION))
                    {
                        if (readerUnit.Connect())
                        {
                            ReaderUnitName = readerUnit.ConnectedName;
                            //readerSerialNumber = readerUnit.GetReaderSerialNumber();

                            card = readerUnit.GetSingleChip();

                            if (card.Type == "DESFireEV1")
                            {
                                cmd = card.Commands as IDESFireEV1Commands;
                                try
                                {
                                    cmd.SelectApplication((uint)_appID);
                                    if (_appID > 0)
                                        cmd.Authenticate((byte)_keyNumber, aiToUse.MasterCardKey);
                                    else
                                        cmd.Authenticate((byte)0, aiToUse.MasterCardKey);
                                    return ERROR.NoError;
                                }
                                catch
                                {
                                    return ERROR.AuthenticationError;
                                }
                            }
                            if (card.Type == "DESFireEV2")
                            {
                            }
                            return ERROR.AuthenticationError;
                        }
                    }
                }
                return ERROR.IOError;
            }
            catch
            {
                return ERROR.AuthenticationError;
            }
        }

        public ERROR GetMifareDesfireAppSettings(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumberCurrent = 0, int _appID = 0)
        {
            byte maxNbrOfKeys;
            DESFireKeySettings keySettings;

            try
            {
                // Keys to use for authentication
                IDESFireAccessInfo aiToUse = new DESFireAccessInfo();
                CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_applicationMasterKey);
                aiToUse.MasterCardKey.Value = CustomConverter.desFireKeyToEdit;
                aiToUse.MasterCardKey.KeyType = _keyType;

                if (readerUnit.ConnectToReader())
                {
                    if (readerUnit.WaitInsertion(Constants.MAX_WAIT_INSERTION))
                    {
                        if (readerUnit.Connect())
                        {
                            ReaderUnitName = readerUnit.ConnectedName;
                            card = readerUnit.GetSingleChip();

                            if (card.Type == "DESFire" ||
                                card.Type == "DESFireEV1" ||
                                card.Type == "DESFireEV2")
                            {
                                var cmd = card.Commands as IDESFireCommands;

                                try
                                {
                                    cmd.SelectApplication((uint)_appID);
                                    try
                                    {
                                        cmd.Authenticate((byte)_keyNumberCurrent, aiToUse.MasterCardKey);
                                    }
                                    catch
                                    {
                                        try
                                        {
                                            cmd.GetKeySettings(out keySettings, out maxNbrOfKeys);
                                            MaxNumberOfAppKeys = maxNbrOfKeys;
                                            DesfireAppKeySetting = keySettings;

                                            return ERROR.NoError;
                                        }
                                        catch
                                        {
                                            return ERROR.AuthenticationError;
                                        }
                                    }
                                    cmd.GetKeySettings(out keySettings, out maxNbrOfKeys);
                                    MaxNumberOfAppKeys = maxNbrOfKeys;
                                    DesfireAppKeySetting = keySettings;

                                    return ERROR.NoError;
                                }
                                catch
                                {
                                    return ERROR.AuthenticationError;
                                }
                            }
                            return ERROR.AuthenticationError;
                        }
                    }
                }
                return ERROR.DeviceNotReadyError;
            }
            catch
            {
                return ERROR.IOError;
            }
        }

        public ERROR CreateMifareDesfireApplication(string _piccMasterKey, DESFireKeySettings _keySettingsTarget, DESFireKeyType _keyTypePiccMasterKey, DESFireKeyType _keyTypeTargetApplication, int _maxNbKeys, int _appID)
        {
            try
            {
                // The excepted memory tree
                IDESFireLocation location = new DESFireLocation();
                // The Application ID to use
                location.aid = _appID;

                // File communication requires encryption
                location.SecurityLevel = EncryptionMode.CM_ENCRYPT;

                IDESFireEV1Commands cmd;
                // Keys to use for authentication
                IDESFireAccessInfo aiToUse = new DESFireAccessInfo();
                CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_piccMasterKey);
                aiToUse.MasterCardKey.Value = CustomConverter.desFireKeyToEdit;
                aiToUse.MasterCardKey.KeyType = _keyTypePiccMasterKey;

                if (readerUnit.ConnectToReader())
                {
                    if (readerUnit.WaitInsertion(Constants.MAX_WAIT_INSERTION))
                    {
                        if (readerUnit.Connect())
                        {
                            ReaderUnitName = readerUnit.ConnectedName;
                            //readerSerialNumber = readerUnit.GetReaderSerialNumber();

                            card = readerUnit.GetSingleChip();

                            if (card.Type == "DESFireEV1")
                            {
                                cmd = card.Commands as IDESFireEV1Commands;
                                try
                                {
                                    cmd.SelectApplication(0);
                                    cmd.Authenticate(0, aiToUse.MasterCardKey);
                                    cmd.CreateApplicationEV1((uint)_appID, _keySettingsTarget, (byte)_maxNbKeys, false, _keyTypeTargetApplication, 0, 0);
                                    //cmd.CreateApplication((uint)_appID,_keySettings,(byte)_maxNbKeys);
                                    //cmd.SelectApplication((uint)_appID);
                                    //cmd.AuthenticateKeyNo(0);
                                    //cmd.ChangeKey(1,aiToUse.MasterCardKey);

                                    return ERROR.NoError;
                                }
                                catch
                                {
                                    return ERROR.AuthenticationError;
                                }
                            }
                            if (card.Type == "DESFireEV2")
                            {
                            }
                            return ERROR.AuthenticationError;
                        }
                    }
                }
                return ERROR.IOError;
            }
            catch
            {
                return ERROR.AuthenticationError;
            }
        }

        public ERROR ChangeMifareDesfireApplicationKey(string _applicationMasterKeyCurrent, int _keyNumberCurrent, DESFireKeyType _keyTypeCurrent, string _applicationMasterKeyTarget, int _keyNumberTarget, DESFireKeyType _keyTypeTarget, int _appIDCurrent = 0, int _appIDTarget = 0)
        {
            try
            {
                // The excepted memory tree
                IDESFireLocation location = new DESFireLocation();
                // The Application ID to use
                location.aid = _appIDCurrent;
                // File communication requires encryption
                location.SecurityLevel = EncryptionMode.CM_ENCRYPT;

                IDESFireEV1Commands cmd;
                // Keys to use for authentication
                IDESFireAccessInfo aiToUse = new DESFireAccessInfo();
                if (_appIDCurrent > 0)
                {
                    CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_applicationMasterKeyCurrent);
                    aiToUse.MasterApplicationKey.Value = CustomConverter.desFireKeyToEdit;
                    aiToUse.MasterApplicationKey.KeyType = _keyTypeCurrent;
                }
                else
                {
                    CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_applicationMasterKeyCurrent);
                    aiToUse.MasterCardKey.Value = CustomConverter.desFireKeyToEdit;
                    aiToUse.MasterCardKey.KeyType = _keyTypeCurrent;
                }

                DESFireKey applicationMasterKeyTarget = new DESFireKeyClass();
                applicationMasterKeyTarget.KeyType = _keyTypeTarget;
                CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_applicationMasterKeyTarget);
                applicationMasterKeyTarget.Value = CustomConverter.desFireKeyToEdit;

                if (readerUnit.ConnectToReader())
                {
                    if (readerUnit.WaitInsertion(Constants.MAX_WAIT_INSERTION))
                    {
                        if (readerUnit.Connect())
                        {
                            ReaderUnitName = readerUnit.ConnectedName;
                            //readerSerialNumber = readerUnit.GetReaderSerialNumber();

                            card = readerUnit.GetSingleChip();

                            if (card.Type == "DESFireEV1")
                            {
                                cmd = card.Commands as IDESFireEV1Commands;
                                try
                                {
                                    cmd.SelectApplication((uint)_appIDCurrent);

                                    if (_appIDCurrent == 0 && _appIDTarget == 0)
                                    {
                                        cmd.Authenticate(0, aiToUse.MasterCardKey);
                                        cmd.ChangeKey((byte)0, applicationMasterKeyTarget);
                                    }
                                    else if (_appIDCurrent == 0 && _appIDTarget > 0)
                                    {
                                        cmd.Authenticate(0, aiToUse.MasterCardKey);
                                        cmd.SelectApplication((uint)_appIDTarget);
                                        cmd.Authenticate((byte)_keyNumberCurrent, aiToUse.MasterCardKey);
                                        cmd.ChangeKey((byte)_keyNumberTarget, applicationMasterKeyTarget);
                                    }
                                    else
                                    {
                                        cmd.Authenticate((byte)_keyNumberCurrent, aiToUse.MasterApplicationKey);
                                        cmd.ChangeKey((byte)_keyNumberTarget, applicationMasterKeyTarget);
                                    }

                                    return ERROR.NoError;
                                }
                                catch
                                {
                                    return ERROR.AuthenticationError;
                                }
                            }
                            if (card.Type == "DESFireEV2")
                            {
                            }
                            return ERROR.AuthenticationError;
                        }
                    }
                }
                return ERROR.IOError;
            }
            catch
            {
                return ERROR.AuthenticationError;
            }
        }

        public ERROR DeleteMifareDesfireApplication(string _applicationMasterKey, DESFireKeyType _keyType, int _appID = 0)
        {
            try
            {
                // The excepted memory tree
                IDESFireLocation location = new DESFireLocation();
                // The Application ID to use
                location.aid = _appID;
                // File communication requires encryption
                location.SecurityLevel = EncryptionMode.CM_ENCRYPT;

                IDESFireEV1Commands cmd;
                // Keys to use for authentication
                IDESFireAccessInfo aiToUse = new DESFireAccessInfo();
                CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_applicationMasterKey);
                aiToUse.MasterCardKey.Value = CustomConverter.desFireKeyToEdit;
                aiToUse.MasterCardKey.KeyType = _keyType;

                if (readerUnit.ConnectToReader())
                {
                    if (readerUnit.WaitInsertion(Constants.MAX_WAIT_INSERTION))
                    {
                        if (readerUnit.Connect())
                        {
                            ReaderUnitName = readerUnit.ConnectedName;
                            //readerSerialNumber = readerUnit.GetReaderSerialNumber();

                            card = readerUnit.GetSingleChip();

                            if (card.Type == "DESFireEV1")
                            {
                                cmd = card.Commands as IDESFireEV1Commands;
                                try
                                {
                                    cmd.SelectApplication(0);
                                    cmd.Authenticate(0, aiToUse.MasterCardKey);

                                    cmd.DeleteApplication((uint)_appID);
                                    return ERROR.NoError;
                                }
                                catch
                                {
                                    return ERROR.AuthenticationError;
                                }
                            }
                            if (card.Type == "DESFireEV2")
                            {
                            }
                            return ERROR.AuthenticationError;
                        }
                    }
                }
                return ERROR.IOError;
            }
            catch
            {
                return ERROR.AuthenticationError;
            }
        }

        public ERROR FormatDesfireCard(string _applicationMasterKey, DESFireKeyType _keyType, int _appID = 0)
        {
            try
            {
                // The excepted memory tree
                IDESFireLocation location = new DESFireLocation();
                // The Application ID to use
                location.aid = _appID;
                // File communication requires encryption
                location.SecurityLevel = EncryptionMode.CM_ENCRYPT;

                IDESFireEV1Commands cmd;
                // Keys to use for authentication
                IDESFireAccessInfo aiToUse = new DESFireAccessInfo();
                CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_applicationMasterKey);
                aiToUse.MasterCardKey.Value = CustomConverter.desFireKeyToEdit;
                aiToUse.MasterCardKey.KeyType = _keyType;

                if (readerUnit.ConnectToReader())
                {
                    if (readerUnit.WaitInsertion(Constants.MAX_WAIT_INSERTION))
                    {
                        if (readerUnit.Connect())
                        {
                            ReaderUnitName = readerUnit.ConnectedName;
                            //readerSerialNumber = readerUnit.GetReaderSerialNumber();

                            card = readerUnit.GetSingleChip();

                            if (card.Type == "DESFireEV1")
                            {
                                cmd = card.Commands as IDESFireEV1Commands;
                                try
                                {
                                    cmd.SelectApplication(0);
                                    cmd.Authenticate(0, aiToUse.MasterCardKey);

                                    cmd.Erase();

                                    return ERROR.NoError;
                                }
                                catch
                                {
                                    return ERROR.AuthenticationError;
                                }
                            }
                            if (card.Type == "DESFireEV2")
                            {
                            }
                            return ERROR.AuthenticationError;
                        }
                    }
                }
                return ERROR.IOError;
            }
            catch
            {
                return ERROR.AuthenticationError;
            }
        }

        public ERROR GetMifareDesfireFileList(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumberCurrent = 0, int _appID = 0)
        {
            try
            {
                // The excepted memory tree
                IDESFireLocation location = new DESFireLocation();
                // The Application ID to use
                location.aid = _appID;
                // File communication requires encryption
                location.SecurityLevel = EncryptionMode.CM_ENCRYPT;

                IDESFireEV1Commands cmd;
                // Keys to use for authentication
                IDESFireAccessInfo aiToUse = new DESFireAccessInfo();
                CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_applicationMasterKey);
                aiToUse.MasterCardKey.Value = CustomConverter.desFireKeyToEdit;
                aiToUse.MasterCardKey.KeyType = _keyType;

                object fileIDsObject;

                if (readerUnit.ConnectToReader())
                {
                    if (readerUnit.WaitInsertion(Constants.MAX_WAIT_INSERTION))
                    {
                        if (readerUnit.Connect())
                        {
                            ReaderUnitName = readerUnit.ConnectedName;
                            //readerSerialNumber = readerUnit.GetReaderSerialNumber();

                            card = readerUnit.GetSingleChip();

                            if (card.Type == "DESFireEV1")
                            {
                                cmd = card.Commands as IDESFireEV1Commands;
                                try
                                {
                                    cmd.SelectApplication((uint)_appID);
                                    try
                                    {
                                        cmd.Authenticate((byte)_keyNumberCurrent, aiToUse.MasterCardKey);
                                    }
                                    catch
                                    {
                                        try
                                        {
                                            fileIDsObject = cmd.GetFileIDs();
                                            FileIDList = (fileIDsObject as byte[]);
                                            return ERROR.NoError;
                                        }
                                        catch
                                        {
                                            return ERROR.AuthenticationError;
                                        }
                                    }

                                    fileIDsObject = cmd.GetFileIDs();
                                    FileIDList = (fileIDsObject as byte[]);

                                    return ERROR.NoError;
                                }
                                catch
                                {
                                    return ERROR.AuthenticationError;
                                }
                            }
                            if (card.Type == "DESFireEV2")
                            {
                            }
                            return ERROR.AuthenticationError;
                        }
                    }
                }
                return ERROR.IOError;
            }
            catch
            {
                return ERROR.AuthenticationError;
            }
        }

        public ERROR GetMifareDesfireFileSettings(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumberCurrent = 0, int _appID = 0, int _fileNo = 0)
        {
            try
            {
                IDESFireEV1Commands cmd;
                // Keys to use for authentication
                IDESFireAccessInfo aiToUse = new DESFireAccessInfo();
                CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_applicationMasterKey);
                aiToUse.MasterCardKey.Value = CustomConverter.desFireKeyToEdit;
                aiToUse.MasterCardKey.KeyType = _keyType;

                if (readerUnit.ConnectToReader())
                {
                    if (readerUnit.WaitInsertion(Constants.MAX_WAIT_INSERTION))
                    {
                        if (readerUnit.Connect())
                        {
                            ReaderUnitName = readerUnit.ConnectedName;
                            card = readerUnit.GetSingleChip();

                            if (card.Type == "DESFireEV1")
                            {
                                cmd = card.Commands as IDESFireEV1Commands;
                                try
                                {
                                    cmd.SelectApplication((uint)_appID);
                                    try
                                    {
                                        cmd.Authenticate((byte)_keyNumberCurrent, aiToUse.MasterCardKey);
                                    }
                                    catch
                                    {
                                        try
                                        {
                                            DesfireFileSetting = cmd.GetFileSettings((byte)_fileNo);

                                            return ERROR.NoError;
                                        }
                                        catch
                                        {
                                            return ERROR.AuthenticationError;
                                        }
                                    }
                                    DesfireFileSetting = cmd.GetFileSettings((byte)_fileNo);

                                    return ERROR.NoError;
                                }
                                catch
                                {
                                    return ERROR.AuthenticationError;
                                }
                            }
                            return ERROR.AuthenticationError;
                        }
                    }
                }
                return ERROR.DeviceNotReadyError;
            }
            catch
            {
                return ERROR.IOError;
            }
        }

        #endregion mifare desfire

        private bool ReadMiFareDESFireChipFile(int fileNo, int appid)
        {
            // The excepted memory tree
            IDESFireLocation location = new DESFireLocation();
            // The Application ID to use
            location.aid = appid;
            // File 0 into this application
            location.File = fileNo;
            // File communication requires encryption
            location.SecurityLevel = EncryptionMode.CM_ENCRYPT;

            IDESFireEV1Commands cmd;
            // Keys to use for authentication
            IDESFireAccessInfo aiToUse = new DESFireAccessInfo();
            aiToUse.MasterCardKey.Value = "11 22 33 44 55 66 77 88 99 00 11 22 33 44 55 66"; //"00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00";
            aiToUse.MasterCardKey.KeyType = DESFireKeyType.DF_KEY_AES;

            // Get the card storage service
            IStorageCardService storage = (IStorageCardService)card.GetService(CardServiceType.CST_STORAGE);

            // Change keys with the following ones
            IDESFireAccessInfo aiToWrite = new DESFireAccessInfo();

            aiToWrite.MasterCardKey.Value = "11 22 33 44 55 66 77 88 99 00 11 22 33 44 55 66";
            aiToWrite.MasterApplicationKey.Value = "c7 56 80 59 0f 31 2c 13 07 12 b6 df 8f a7 b1 dc";//"bd 9d 22 8c 06 72 14 a9 59 a3 28 91 fd bb 14 8c"; //"00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00";
            aiToWrite.MasterApplicationKey.KeyType = DESFireKeyType.DF_KEY_AES;

            aiToWrite.ReadKey.Value = "c7 56 80 59 0f 31 2c 13 07 12 b6 df 8f a7 b1 dc";//"bd 9d 22 8c 06 72 14 a9 59 a3 28 91 fd bb 14 8c";
            aiToWrite.ReadKey.KeyType = DESFireKeyType.DF_KEY_AES;
            aiToWrite.ReadKeyNo = 1;

            aiToWrite.WriteKey.Value = "c7 56 80 59 0f 31 2c 13 07 12 b6 df 8f a7 b1 dc";
            aiToWrite.WriteKey.KeyType = DESFireKeyType.DF_KEY_AES;
            aiToWrite.WriteKeyNo = 2;

            DESFireKeySettings desFireKeySet;
            byte nBNmbr;

            if (readerUnit.ConnectToReader())
            {
                if (readerUnit.WaitInsertion(100))
                {
                    if (readerUnit.Connect())
                    {
                        if (card.Type == "DESFireEV1")
                        {
                            cmd = card.Commands as IDESFireEV1Commands;

                            object appIDsObject = cmd.GetApplicationIDs();

                            cmd.GetKeySettings(out desFireKeySet, out nBNmbr);
                            cmd.SelectApplication(1);
                            cmd.Authenticate(1, aiToWrite.WriteKey);
                            FileSetting fSetting = cmd.GetFileSettings(0);

                            //cmd.CreateApplication(3, DESFireKeySettings.KS_DEFAULT, 3);
                            //cmd.DeleteApplication(4);
                            object fileIDs = cmd.GetFileIDs();
                            //appIDs = (appIDsObject as UInt32[]);

                            desFireFileData = (byte[])storage.ReadData(location, aiToWrite, 48, CardBehavior.CB_DEFAULT);
                        }
                        if (card.Type == "DESFireEV2")
                        {
                        }
                        return false;
                    }
                }
            }
            return true;
        }

        private bool WriteMiFareDESFireChipFile(int fileNo, int appid)
        {
            // The excepted memory tree
            IDESFireLocation location = new DESFireLocation();
            // The Application ID to use
            location.aid = appid;
            // File 0 into this application
            location.File = fileNo;
            // File communication requires encryption
            location.SecurityLevel = EncryptionMode.CM_ENCRYPT;

            IDESFireEV1Commands cmd;
            // Keys to use for authentication
            IDESFireAccessInfo aiToUse = new DESFireAccessInfo();
            aiToUse.MasterCardKey.Value = "00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00"; //"11 22 33 44 55 66 77 88 99 00 11 22 33 44 55 66"
            aiToUse.MasterCardKey.KeyType = DESFireKeyType.DF_KEY_AES;

            // Get the card storage service
            IStorageCardService storage = (IStorageCardService)card.GetService(CardServiceType.CST_STORAGE);

            // Change keys with the following ones
            IDESFireAccessInfo aiToWrite = new DESFireAccessInfo();

            aiToWrite.MasterCardKey.Value = "00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00";
            aiToWrite.MasterCardKey.KeyType = DESFireKeyType.DF_KEY_AES;

            aiToWrite.MasterApplicationKey.Value = "00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00";//"bd 9d 22 8c 06 72 14 a9 59 a3 28 91 fd bb 14 8c"; //"c7 56 80 59 0f 31 2c 13 07 12 b6 df 8f a7 b1 dc";
            aiToWrite.MasterApplicationKey.KeyType = DESFireKeyType.DF_KEY_AES;

            aiToWrite.ReadKey.Value = "00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00";//"bd 9d 22 8c 06 72 14 a9 59 a3 28 91 fd bb 14 8c";
            aiToWrite.ReadKey.KeyType = DESFireKeyType.DF_KEY_AES;
            aiToWrite.ReadKeyNo = 1;

            aiToWrite.WriteKey.Value = "00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00";
            aiToWrite.WriteKey.KeyType = DESFireKeyType.DF_KEY_AES;
            aiToWrite.WriteKeyNo = 2;

            DESFireKeySettings desFireKeySet;
            byte nBNmbr;

            if (readerUnit.ConnectToReader())
            {
                if (readerUnit.WaitInsertion(100))
                {
                    if (readerUnit.Connect())
                    {
                        if (card.Type == "DESFireEV1")
                        {
                            cmd = card.Commands as IDESFireEV1Commands;

                            object appIDsObject = cmd.GetApplicationIDs();

                            cmd.GetKeySettings(out desFireKeySet, out nBNmbr);
                            cmd.Authenticate(0, aiToUse.MasterCardKey);
                            cmd.DeleteApplication(1);

                            //cmd.SelectApplication(0);

                            //FileSetting fSetting = cmd.GetFileSettings(0);

                            //cmd.CreateApplication(3, DESFireKeySettings.KS_DEFAULT, 3);
                            //cmd.DeleteApplication(4);
                            //object fileIDs = cmd.GetFileIDs();
                            //appIDs = (appIDsObject as UInt32[]);
                            cmd.ChangeKey(0, aiToWrite.MasterCardKey);
                            //desFireFileData = (byte[])storage.ReadData(location, aiToWrite, 48, CardBehavior.CB_DEFAULT);
                        }
                        if (card.Type == "DESFireEV2")
                        {
                        }
                        return false;
                    }
                }
            }
            return true;
        }
    }
}