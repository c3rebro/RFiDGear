using Elatec.NET;
using Elatec.NET.Model;

using RFiDGear.DataAccessLayer;
using RFiDGear.Model;

using Log4CSharp;

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
        private readonly string FacilityName = "RFiDGear";
        private readonly TWN4ReaderDevice readerDevice;
        private ChipModel card;
        private bool _disposed = false;

        #region properties

        public MifareClassicSectorModel Sector { get; private set; }

        public MifareClassicDataBlockModel DataBlock { get; private set; }

        public GenericChipModel GenericChip { get; private set; }

        public ReaderTypes ReaderProvider { get; private set; }

        public string readerDeviceName { get; private set; }

        //public CARD_INFO CardInfo { get; private set; }

        public byte[] MifareClassicData { get; private set; }

        public bool DataBlockSuccessfullyRead { get; private set; }

        public bool DataBlockSuccesfullyAuth { get; private set; }

        public bool SectorSuccessfullyRead { get; private set; }

        public bool SectorSuccesfullyAuth { get; private set; }

        public byte[] MifareDESFireData { get; private set; }

        public uint[] AppIDList { get; private set; }

        public byte[] FileIDList { get; private set; }

        public byte[] MifareUltralightPageData { get; private set; }

        public byte MaxNumberOfAppKeys { get; private set; }

        public byte EncryptionType { get; private set; }

        public DESFireFileSettings DesfireFileSetting { get; private set; }

        public DESFireKeySettings DesfireAppKeySetting { get; private set; }

        #endregion properties

        #region contructor

        public static RFiDDevice Instance
        {
            get
            {
                lock (RFiDDevice.syncRoot)
                {
                    if (instance == null)
                    {
                        instance = new RFiDDevice();
                        return instance;
                        
                    }
                    else
                    {
                        return null;
                    }
                }
            }
        }

        private static readonly object syncRoot = new object();
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
                    if (defaultSettings.DefaultSpecification.DefaultReaderProvider == ReaderTypes.PCSC)
                    {
                        ReaderProvider = _readerType != ReaderTypes.None ? _readerType : defaultSettings.DefaultSpecification.DefaultReaderProvider;

                        GenericChip = new GenericChipModel("", CARD_TYPE.Unspecified);
                    }

                    else if (defaultSettings.DefaultSpecification.DefaultReaderProvider == ReaderTypes.Elatec)
                    {

                        ReaderProvider = _readerType != ReaderTypes.None ? _readerType : defaultSettings.DefaultSpecification.DefaultReaderProvider;

                        if (int.TryParse(defaultSettings.DefaultSpecification.LastUsedComPort, out int portNumber))
                        {
                            readerDevice = new TWN4ReaderDevice(portNumber);
                            readerDevice.Connect();
                        }

                        readerDevice.ReadChipPublic();
                    }

                    AppIDList = new uint[0];
                }
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""), FacilityName);
            }
        }

        #endregion contructor

        #region common

        public ERROR ReadChipPublic()
        {
            try
            {
                if (readerDevice.Connect())
                {
                    readerDevice.Beep();
                    //readerDeviceName = readerDevice.ConnectedName;

                    card = readerDevice.GetSingleChip();

                    if (!string.IsNullOrWhiteSpace(card.ChipIdentifier))
                    {
                        try
                        {
                            GenericChip = new GenericChipModel(card.ChipIdentifier, (CARD_TYPE)card.CardType);

                            if ((CARD_TYPE)card.CardType == CARD_TYPE.DESFire || (CARD_TYPE)card.CardType == CARD_TYPE.DESFireEV1)
                            {
                                int version = readerDevice.GetDesFireVersion();

                                if (version == 1)
                                {
                                    GenericChip.CardType = CARD_TYPE.DESFireEV1;
                                }
                                else if (version == 2)
                                {
                                    GenericChip.CardType = CARD_TYPE.DESFireEV2;
                                }
                                else if (version == 3)
                                {
                                    GenericChip.CardType = CARD_TYPE.DESFire;
                                }
                            }

                            return ERROR.NoError;
                        }
                        catch (Exception e)
                        {
                            LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""), FacilityName);
                            return ERROR.IOError;
                        }
                    }
                    else
                    {
                        return ERROR.DeviceNotReadyError;
                    }
                }
            }
            catch (Exception e)
            {
                if (readerDevice != null)
                {
                    readerDevice.Dispose();
                }

                LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""), FacilityName);

                readerDevice?.Disconnect();
                return ERROR.IOError;
            }
            readerDevice?.Disconnect();
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

                if (readerDevice != null)
                {
                    readerDevice.Disconnect();
                }

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

            try
            {
                /*
                //readerDeviceName = readerDevice.ConnectedName;

                card = readerDevice.GetSingleChip();

                if (!string.IsNullOrWhiteSpace(card.ChipIdentifier))
                {
                    try
                    {
                        //CardInfo = new CARD_INFO((CARD_TYPE)Enum.Parse(typeof(CARD_TYPE), card.Type), card.ChipIdentifier);
                        GenericChip = new GenericChipModel(card.ChipIdentifier, (CARD_TYPE)Enum.Parse(typeof(CARD_TYPE), card.Type));
                    }
                    catch (Exception e)
                    {
                        LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""), FacilityName);
                    }
                }

                var cmd = card.Commands as IMifareCommands;

                try
                { //try to Auth with Keytype A
                    for (int k = 0; k < (sectorNumber > 31 ? 16 : 4); k++) // if sector > 31 is 16 blocks each sector i.e. mifare 4k else its 1k or 2k with 4 blocks each sector
                    {
                        cmd.LoadKeyNo((byte)0, keyA, Elatec.NET.DataAccessLayer.MifareKeyType.KT_KEY_A);

                        DataBlock = new MifareClassicDataBlockModel(
                            (byte)CustomConverter.GetChipBasedDataBlockNumber(GenericChip.CardType, sectorNumber, k),
                            k);

                        try
                        {
                            cmd.AuthenticateKeyNo((byte)CustomConverter.GetChipBasedDataBlockNumber(GenericChip.CardType, sectorNumber, k),
                                                  (byte)0,
                                                  MifareKeyType.KT_KEY_A);

                            Sector.IsAuthenticated = true;

                            try
                            {
                                object data = cmd.ReadBinary(
                                    (byte)CustomConverter.GetChipBasedDataBlockNumber(GenericChip.CardType, sectorNumber, k),
                                    48);

                                DataBlock.Data = (byte[])data;

                                DataBlock.IsAuthenticated = true;

                                Sector.DataBlock.Add(DataBlock);
                            }
                            catch
                            {
                                DataBlock.IsAuthenticated = false;
                                Sector.DataBlock.Add(DataBlock);
                            }
                        }
                        catch
                        { // Try Auth with keytype b

                            try
                            {
                                cmd.LoadKeyNo((byte)1, keyB, MifareKeyType.KT_KEY_B);

                                cmd.AuthenticateKeyNo(
                                    (byte)CustomConverter.GetChipBasedDataBlockNumber(GenericChip.CardType, sectorNumber, k),
                                    (byte)1,
                                    MifareKeyType.KT_KEY_B);

                                Sector.IsAuthenticated = true;

                                try
                                {
                                    object data = cmd.ReadBinary(
                                        (byte)CustomConverter.GetChipBasedDataBlockNumber(GenericChip.CardType, sectorNumber, k),
                                        48);

                                    DataBlock.Data = (byte[])data;

                                    DataBlock.IsAuthenticated = true;

                                    Sector.DataBlock.Add(DataBlock);
                                }
                                catch
                                {

                                    DataBlock.IsAuthenticated = false;

                                    Sector.DataBlock.Add(DataBlock);

                                    return ERROR.AuthenticationError;
                                }
                            }
                            catch
                            {
                                Sector.IsAuthenticated = false;
                                DataBlock.IsAuthenticated = false;

                                Sector.DataBlock.Add(DataBlock);

                                return ERROR.AuthenticationError;
                            }
                        }
                    }
                }
                catch
                {
                    return ERROR.AuthenticationError;
                }
                */
                return ERROR.NoError;
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""), FacilityName);
                return ERROR.AuthenticationError;
            }
        }

        public ERROR WriteMiFareClassicSingleSector(int sectorNumber, string _aKey, string _bKey, byte[] buffer)
        {
            var keyA = new MifareKey() { Value = CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(_aKey) };
            var keyB = new MifareKey() { Value = CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(_bKey) };

            int blockCount = 0;

            try
            {
                /*
                if (readerDevice.ConnectToReader())
                {
                    if (readerDevice.WaitInsertion(Constants.MAX_WAIT_INSERTION))
                    {
                        if (readerDevice.Connect())
                        {
                            readerDeviceName = readerDevice.ConnectedName;
                            //readerSerialNumber = readerDevice.GetReaderSerialNumber();

                            card = readerDevice.GetSingleChip();

                            if (!string.IsNullOrWhiteSpace(card.ChipIdentifier))
                            {
                                try
                                {
                                    //CardInfo = new CARD_INFO((CARD_TYPE)Enum.Parse(typeof(CARD_TYPE), card.Type), card.ChipIdentifier);
                                    GenericChip = new GenericChipModel(card.ChipIdentifier, (CARD_TYPE)Enum.Parse(typeof(CARD_TYPE), card.Type));
                                }
                                catch (Exception e)
                                {
                                    LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""), FacilityName);
                                }
                            }

                            var cmd = card.Commands as IMifareCommands;

                            try
                            { //try to Auth with Keytype A
                                cmd.LoadKeyNo((byte)0, keyA, MifareKeyType.KT_KEY_A);
                                cmd.LoadKeyNo((byte)1, keyB, MifareKeyType.KT_KEY_B);

                                for (int k = 0; k < blockCount; k++)
                                {
                                    try
                                    {
                                        cmd.AuthenticateKeyNo(
                                            (byte)CustomConverter.GetChipBasedDataBlockNumber(GenericChip.CardType, sectorNumber, k),
                                            (byte)0,
                                            MifareKeyType.KT_KEY_A);

                                        try
                                        {
                                            cmd.WriteBinary(
                                                (byte)CustomConverter.GetChipBasedDataBlockNumber(GenericChip.CardType, sectorNumber, k),
                                                buffer);

                                            return ERROR.NoError;
                                        }
                                        catch
                                        {
                                            return ERROR.AuthenticationError;
                                        }
                                    }
                                    catch
                                    { // Try Auth with keytype b

                                        try
                                        {
                                            cmd.AuthenticateKeyNo((byte)CustomConverter.GetChipBasedDataBlockNumber(GenericChip.CardType, sectorNumber, k),
                                                                  (byte)1,
                                                                  MifareKeyType.KT_KEY_B);

                                            try
                                            {
                                                cmd.WriteBinary(
                                                    (byte)CustomConverter.GetChipBasedDataBlockNumber(GenericChip.CardType, sectorNumber, k),
                                                    buffer);

                                                return ERROR.NoError;

                                            }
                                            catch
                                            {
                                                return ERROR.AuthenticationError;
                                            }
                                        }
                                        catch
                                        {

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
                */
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""), FacilityName);

                return ERROR.IOError;
            }
                
            return ERROR.DeviceNotReadyError;
        }

        public ERROR WriteMiFareClassicSingleBlock(int _blockNumber, string _aKey, string _bKey, byte[] buffer)
        {
            try
            {
                /*
                var keyA = new MifareKey() { Value = CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(_aKey) };
                var keyB = new MifareKey() { Value = CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(_bKey) };

                if (readerDevice.ConnectToReader())
                {
                    if (readerDevice.WaitInsertion(Constants.MAX_WAIT_INSERTION))
                    {
                        if (readerDevice.Connect())
                        {
                            readerDeviceName = readerDevice.ConnectedName;
                            //readerSerialNumber = readerDevice.GetReaderSerialNumber();

                            card = readerDevice.GetSingleChip();

                            if (!string.IsNullOrWhiteSpace(card.ChipIdentifier))
                            {
                                try
                                {
                                    //CardInfo = new CARD_INFO((CARD_TYPE)Enum.Parse(typeof(CARD_TYPE), card.Type), card.ChipIdentifier);
                                    GenericChip = new GenericChipModel(card.ChipIdentifier, (CARD_TYPE)Enum.Parse(typeof(CARD_TYPE), card.Type));
                                }
                                catch (Exception e)
                                {
                                    LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""), FacilityName);
                                }
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
                */
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""), FacilityName);

                return ERROR.IOError;
            }
            return ERROR.IOError;
        }

        public ERROR ReadMiFareClassicSingleBlock(int _blockNumber, string _aKey, string _bKey)
        {
            try
            {
                var keyA = new MifareKey() { Value = CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(_aKey) };
                var keyB = new MifareKey() { Value = CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(_bKey) };

                /*
                if (readerDevice.ConnectToReader())
                {
                    if (readerDevice.WaitInsertion(Constants.MAX_WAIT_INSERTION))
                    {
                        if (readerDevice.Connect())
                        {
                            readerDeviceName = readerDevice.ConnectedName;
                            //readerSerialNumber = readerDevice.GetReaderSerialNumber();

                            card = readerDevice.GetSingleChip();

                            if (!string.IsNullOrWhiteSpace(card.ChipIdentifier))
                            {
                                try
                                {
                                    //CardInfo = new CARD_INFO((CARD_TYPE)Enum.Parse(typeof(CARD_TYPE), card.Type), card.ChipIdentifier);
                                    GenericChip = new GenericChipModel(card.ChipIdentifier, (CARD_TYPE)Enum.Parse(typeof(CARD_TYPE), card.Type));
                                }
                                catch (Exception e)
                                {
                                    LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""), FacilityName);
                                }
                            }

                            var cmd = card.Commands as IMifareCommands;

                            try
                            {
                                cmd.LoadKeyNo((byte)0, keyA, MifareKeyType.KT_KEY_A); // FIXME "sectorNumber" to 0

                                try
                                { //try to Auth with Keytype A
                                    cmd.AuthenticateKeyNo((byte)(_blockNumber), (byte)0, MifareKeyType.KT_KEY_A); // FIXME same as '303

                                    MifareClassicData = (byte[])cmd.ReadBinary((byte)(_blockNumber), 48);

                                    return ERROR.NoError;
                                }
                                catch
                                { // Try Auth with keytype b

                                    cmd.LoadKeyNo((byte)0, keyB, MifareKeyType.KT_KEY_B);

                                    try
                                    {
                                        cmd.AuthenticateKeyNo((byte)(_blockNumber), (byte)0, MifareKeyType.KT_KEY_B); // FIXME same as '303

                                        MifareClassicData = (byte[])cmd.ReadBinary((byte)(_blockNumber), 48);

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
                */
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""), FacilityName);

                return ERROR.IOError;
            }
            return ERROR.IOError;
        }

        public ERROR WriteMiFareClassicWithMAD(int _madApplicationID, int _madStartSector,
                                               string _aKeyToUse, string _bKeyToUse, string _aKeyToWrite, string _bKeyToWrite,
                                               string _madAKeyToUse, string _madBKeyToUse, string _madAKeyToWrite, string _madBKeyToWrite,
                                               byte[] buffer, byte _madGPB, SectorAccessBits _sab, bool _useMADToAuth = false, bool _keyToWriteUseMAD = false)
        {
            var settings = new SettingsReaderWriter();
            Sector = new MifareClassicSectorModel();

            settings.ReadSettings();

            var mAKeyToUse = new MifareKey() { Value = CustomConverter.KeyFormatQuickCheck(_aKeyToUse) ? _aKeyToUse : CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(_aKeyToUse) };
            var mBKeyToUse = new MifareKey() { Value = CustomConverter.KeyFormatQuickCheck(_bKeyToUse) ? _bKeyToUse : CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(_bKeyToUse) };

            var mAKeyToWrite = new MifareKey() { Value = CustomConverter.KeyFormatQuickCheck(_aKeyToWrite) ? _aKeyToWrite : CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(_aKeyToWrite) };
            var mBKeyToWrite = new MifareKey() { Value = CustomConverter.KeyFormatQuickCheck(_bKeyToWrite) ? _bKeyToWrite : CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(_bKeyToWrite) };

            var madAKeyToUse = new MifareKey() { Value = CustomConverter.KeyFormatQuickCheck(_madAKeyToUse) ? _madAKeyToUse : CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(_madAKeyToUse) };
            var madBKeyToUse = new MifareKey() { Value = CustomConverter.KeyFormatQuickCheck(_madBKeyToUse) ? _madBKeyToUse : CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(_madBKeyToUse) };

            var madAKeyToWrite = new MifareKey() { Value = CustomConverter.KeyFormatQuickCheck(_madAKeyToWrite) ? _madAKeyToWrite : CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(_madAKeyToWrite) };
            var madBKeyToWrite = new MifareKey() { Value = CustomConverter.KeyFormatQuickCheck(_madBKeyToWrite) ? _madBKeyToWrite : CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(_madBKeyToWrite) };

            try
            {
                /*
                if (readerDevice.ConnectToReader())
                {
                    if (readerDevice.WaitInsertion(Constants.MAX_WAIT_INSERTION))
                    {
                        if (readerDevice.Connect())
                        {
                            readerDeviceName = readerDevice.ConnectedName;

                            card = readerDevice.GetSingleChip();

                            if (!string.IsNullOrWhiteSpace(card.ChipIdentifier))
                            {
                                try
                                {
                                    GenericChip = new GenericChipModel(card.ChipIdentifier, (CARD_TYPE)Enum.Parse(typeof(CARD_TYPE), card.Type));
                                }
                                catch (Exception e)
                                {
                                    LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""), FacilityName);
                                }
                            }

                            MifareLocation mlocation = new MifareLocationClass
                            {
                                MADApplicationID = (ushort)_madApplicationID,
                                UseMAD = _keyToWriteUseMAD,
                                Sector = _madStartSector
                            };

                            MifareAccessInfo aiToWrite = new MifareAccessInfoClass
                            {
                                UseMAD = _keyToWriteUseMAD,
                                
                            };
                            aiToWrite.MADKeyA.Value = _aKeyToUse == _madAKeyToWrite ? madAKeyToUse.Value : madAKeyToWrite.Value;
                            aiToWrite.MADKeyB.Value = _bKeyToUse == _madBKeyToWrite ? madBKeyToUse.Value : madBKeyToWrite.Value;
                            aiToWrite.KeyA.Value = _aKeyToUse == _aKeyToWrite ? mAKeyToUse.Value : mAKeyToWrite.Value;
                            aiToWrite.KeyB.Value = _bKeyToUse == _bKeyToWrite ? mBKeyToUse.Value : mBKeyToWrite.Value;
                            aiToWrite.MADGPB = _madGPB;
                            aiToWrite.SAB = _sab;

                            var aiToUse = new MifareAccessInfoClass
                            {
                                UseMAD = _useMADToAuth,
                                KeyA = mAKeyToUse,
                                KeyB = mBKeyToUse
                            };

                            if (_useMADToAuth)
                            {
                                aiToUse.MADKeyA = madAKeyToUse;
                                aiToUse.MADKeyB = madBKeyToUse;
                                aiToUse.MADGPB = _madGPB;
                            }

                            var cmd = card.Commands as IMifareCommands;
                            var cardService = card.GetService(LibLogicalAccess.CardServiceType.CST_STORAGE) as IStorageCardService;

                            try
                            {
                                cardService.WriteData(mlocation, aiToUse, aiToWrite, buffer, buffer.Length, CardBehavior.CB_AUTOSWITCHAREA);
                            }
                            catch (Exception e)
                            {
                                LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""), FacilityName);
                                return ERROR.AuthenticationError;
                            }
                            return ERROR.NoError;
                        }
                    }
                }
                */
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""), FacilityName);
                return ERROR.AuthenticationError;
            }
            return ERROR.NoError;
        }

        public ERROR ReadMiFareClassicWithMAD(int madApplicationID, string _aKeyToUse, string _bKeyToUse, string _madAKeyToUse, string _madBKeyToUse, int _length, byte _madGPB, bool _useMADToAuth = true, bool _aiToUseIsMAD = false)
        {
            
            var settings = new SettingsReaderWriter();
            Sector = new MifareClassicSectorModel();

            settings.ReadSettings();

            var mAKeyToUse = new MifareKey() { Value = CustomConverter.KeyFormatQuickCheck(_aKeyToUse) ? _aKeyToUse : CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(_aKeyToUse) };
            var mBKeyToUse = new MifareKey() { Value = CustomConverter.KeyFormatQuickCheck(_bKeyToUse) ? _bKeyToUse : CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(_bKeyToUse) };

            var madAKeyToUse = new MifareKey() { Value = CustomConverter.KeyFormatQuickCheck(_madAKeyToUse) ? _madAKeyToUse : CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(_madAKeyToUse) };
            var madBKeyToUse = new MifareKey() { Value = CustomConverter.KeyFormatQuickCheck(_madBKeyToUse) ? _madBKeyToUse : CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(_madBKeyToUse) };

            try
            {
                /*
                if (readerDevice.ConnectToReader())
                {
                    if (readerDevice.WaitInsertion(Constants.MAX_WAIT_INSERTION))
                    {
                        if (readerDevice.Connect())
                        {
                            readerDeviceName = readerDevice.ConnectedName;

                            card = readerDevice.GetSingleChip();

                            if (!string.IsNullOrWhiteSpace(card.ChipIdentifier))
                            {
                                try
                                {
                                    GenericChip = new GenericChipModel(card.ChipIdentifier, (CARD_TYPE)Enum.Parse(typeof(CARD_TYPE), card.Type));
                                }
                                catch (Exception e)
                                {
                                    LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""), FacilityName);
                                }
                            }

                            MifareLocation mlocation = card.CreateLocation() as MifareLocation;
                            mlocation.MADApplicationID = (ushort)madApplicationID;
                            mlocation.UseMAD = _useMADToAuth;

                            var aiToUse = new MifareAccessInfoClass
                            {
                                UseMAD = _aiToUseIsMAD,
                                KeyA = mAKeyToUse,
                                KeyB = mBKeyToUse
                            };

                            if (_useMADToAuth)
                            {
                                aiToUse.MADKeyA = madAKeyToUse;
                                aiToUse.MADKeyB = madBKeyToUse;
                                aiToUse.MADGPB = _madGPB;
                            }

                            var cmd = card.Commands as IMifareCommands;
                            var cardService = card.GetService(CardServiceType.CST_STORAGE) as IStorageCardService;

                            try
                            {
                                MifareClassicData = (byte[])cardService.ReadData(mlocation, aiToUse, _length, CardBehavior.CB_AUTOSWITCHAREA);
                            }
                            catch (Exception e)
                            {
                                LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""), FacilityName);
                                return ERROR.AuthenticationError;
                            }
                            return ERROR.NoError;
                        }
                    }
                }
            */
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""), FacilityName);
                return ERROR.AuthenticationError;
            }
            return ERROR.NoError;
        }

        #endregion mifare classic

        #region mifare ultralight

        public ERROR ReadMifareUltralightSinglePage(int _pageNo)
        {
            try
            {
                /*
                if (readerDevice.ConnectToReader())
                {
                    if (readerDevice.WaitInsertion(Constants.MAX_WAIT_INSERTION))
                    {
                        if (readerDevice.Connect())
                        {
                            readerDeviceName = readerDevice.ConnectedName;
                            //readerSerialNumber = readerDevice.GetReaderSerialNumber();
                            RawFormatClass format = new RawFormatClass();

                            var chip = readerDevice.GetSingleChip() as IMifareUltralightChip;

                            var service = chip.GetService(CardServiceType.CST_STORAGE) as StorageCardService;

                            ILocation location = chip.CreateLocation() as ILocation;

                            if (chip.Type == "MifareUltralight")
                            {
                                var cmd = chip.Commands as MifareUltralightCommands;// IMifareUltralightCommands;
                                MifareUltralightPageData = cmd.ReadPages(_pageNo, _pageNo) as byte[];
                            }

                            return ERROR.NoError;
                        }
                    }
                }
                */
                return ERROR.NoError;
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""), FacilityName);
                return ERROR.IOError;
            }
        }

        #endregion mifare ultralight

        #region mifare desfire

        public ERROR GetMiFareDESFireChipAppIDs(string _appMasterKey = "00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00", DESFireKeyType _keyTypeAppMasterKey = DESFireKeyType.DF_KEY_DES)
        {
            try
            {
                /*
                // The excepted memory tree
                IDESFireLocation location = new DESFireLocation
                {
                    // File communication requires encryption
                    SecurityLevel = EncryptionMode.CM_ENCRYPT
                };

                // Keys to use for authentication
                IDESFireAccessInfo aiToUse = new DESFireAccessInfo();
                aiToUse.MasterCardKey.Value = _appMasterKey;
                aiToUse.MasterCardKey.KeyType = _keyTypeAppMasterKey;


                if (readerDevice.ConnectToReader())
                {
                    if (readerDevice.WaitInsertion(Constants.MAX_WAIT_INSERTION))
                    {
                        if (readerDevice.Connect())
                        {
                            readerDeviceName = readerDevice.ConnectedName;

                            card = readerDevice.GetSingleChip();

                            //Get AppIDs without Authentication (Free Directory Listing)
                            try
                            {

                                if (card.Type == "DESFire")
                                {
                                    var cmd = card.Commands as IDESFireCommands;

                                    try
                                    {
                                        cmd.SelectApplication((uint)0);

                                        object appIDsObject = cmd.GetApplicationIDs();
                                        AppIDList = (appIDsObject as UInt32[]);

                                        readerDevice.Disconnect();
                                        return ERROR.NoError;
                                    }
                                    catch
                                    {
                                        //Get AppIDs with Authentication (Directory Listing with PICC MK)
                                        try
                                        {
                                            cmd.SelectApplication((uint)0);
                                            cmd.Authenticate((byte)0, aiToUse.MasterCardKey);

                                            object appIDsObject = cmd.GetApplicationIDs();
                                            AppIDList = (appIDsObject as UInt32[]);

                                            readerDevice.Disconnect();
                                            return ERROR.NoError;
                                        }
                                        catch (Exception e)
                                        {
                                            if (e.Message != "" && e.Message.Contains("same number already exists"))
                                            {
                                                readerDevice.Disconnect();
                                                return ERROR.ItemAlreadyExistError;
                                            }
                                            else if (e.Message != "" && e.Message.Contains("status does not allow the requested command"))
                                            {
                                                readerDevice.Disconnect();
                                                return ERROR.AuthenticationError;
                                            }
                                            else
                                            {
                                                readerDevice.Disconnect();
                                                return ERROR.IOError;
                                            }

                                        }
                                    }
                                }

                                if (card.Type == "DESFireEV1" ||
                                    card.Type == "DESFireEV2")
                                {
                                    var cmd = card.Commands as IDESFireEV1Commands;

                                    try
                                    {
                                        GenericChip.FreeMemory = cmd.GetFreeMemory();
                                        object appIDsObject = cmd.GetApplicationIDs();
                                        AppIDList = (appIDsObject as UInt32[]);

                                        readerDevice.Disconnect();
                                        return ERROR.NoError;
                                    }
                                    catch
                                    {
                                        //Get AppIDs with Authentication (Directory Listing with PICC MK)
                                        try
                                        {
                                            cmd.SelectApplication((uint)0);
                                            cmd.Authenticate((byte)0, aiToUse.MasterCardKey);

                                            object appIDsObject = cmd.GetApplicationIDs();
                                            AppIDList = (appIDsObject as UInt32[]);
                                            GenericChip.FreeMemory = cmd.GetFreeMemory();

                                            readerDevice.Disconnect();
                                            return ERROR.NoError;
                                        }
                                        catch (Exception e)
                                        {
                                            if (e.Message != "" && e.Message.Contains("same number already exists"))
                                            {
                                                readerDevice.Disconnect();
                                                return ERROR.ItemAlreadyExistError;
                                            }
                                            else if (e.Message != "" && e.Message.Contains("status does not allow the requested command"))
                                            {
                                                readerDevice.Disconnect();
                                                return ERROR.AuthenticationError;
                                            }
                                            else
                                            {
                                                readerDevice.Disconnect();
                                                return ERROR.IOError;
                                            }

                                        }
                                    }
                                }
                                else
                                {
                                    readerDevice.Disconnect();
                                    return ERROR.DeviceNotReadyError;
                                }

                            }

                            catch
                            {
                                try
                                {
                                    if (card.Type == "DESFire")
                                    {
                                        var cmd = card.Commands as IDESFireCommands;

                                        cmd.SelectApplication((uint)0);
                                        cmd.Authenticate((byte)0, aiToUse.MasterCardKey);

                                        object appIDsObject = cmd.GetApplicationIDs();
                                        AppIDList = (appIDsObject as UInt32[]);

                                        readerDevice.Disconnect();
                                        return ERROR.NoError;
                                    }

                                    if (card.Type == "DESFireEV1" ||
                                        card.Type == "DESFireEV2")
                                    {
                                        var cmd = card.Commands as IDESFireEV1Commands;

                                    }
                                    else
                                    {
                                        readerDevice.Disconnect();
                                        return ERROR.DeviceNotReadyError;
                                    }

                                }

                                catch
                                {

                                }
                            }
                        }
                    }
                }
                */
                return ERROR.DeviceNotReadyError;
            }

            catch (Exception e)
            {
                if (e.Message != "" && e.Message.Contains("same number already exists"))
                {
                    readerDevice.Disconnect();
                    return ERROR.ItemAlreadyExistError;
                }
                else if (e.Message != "" && e.Message.Contains("status does not allow the requested command"))
                {
                    readerDevice.Disconnect();
                    return ERROR.AuthenticationError;
                }
                else
                {
                    readerDevice.Disconnect();
                    return ERROR.IOError;
                }

            }
        }

        public ERROR CreateMifareDesfireFile(string _appMasterKey, DESFireKeyType _keyTypeAppMasterKey, FileType_MifareDesfireFileType _fileType, DESFireAccessRights _accessRights, EncryptionMode _encMode,
                                             int _appID, int _fileNo, int _fileSize,
                                             int _minValue = 0, int _maxValue = 1000, int _initValue = 0, bool _isValueLimited = false,
                                             int _maxNbOfRecords = 100)
        {
            try
            {
                /*
                DESFireAccessRights accessRights = _accessRights;

                // Keys to use for authentication
                IDESFireAccessInfo aiToUse = new DESFireAccessInfo();
                CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_appMasterKey);
                aiToUse.MasterCardKey.Value = CustomConverter.DesfireKeyToCheck;
                aiToUse.MasterCardKey.KeyType = _keyTypeAppMasterKey;

                if (readerDevice.ConnectToReader())
                {
                    if (readerDevice.WaitInsertion(Constants.MAX_WAIT_INSERTION))
                    {
                        if (readerDevice.Connect())
                        {
                            readerDeviceName = readerDevice.ConnectedName;

                            card = readerDevice.GetSingleChip();

                            if (card.Type == "DESFireEV1" ||
                                card.Type == "DESFireEV2")
                            {
                                try
                                {
                                    var cmd = card.Commands as IDESFireEV1Commands;

                                    try
                                    {
                                        cmd.SelectApplication((uint)_appID);
                                        cmd.Authenticate((byte)0, aiToUse.MasterCardKey);
                                    }
                                    catch
                                    {
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
                                            default:
                                                throw new InvalidOperationException("Unexpected FileTypeSelection");
                                        }

                                        return ERROR.NoError;
                                    }

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
                                catch (Exception e)
                                {
                                    if (e.Message != "" && e.Message.Contains("same number already exists"))
                                    {
                                        return ERROR.ItemAlreadyExistError;
                                    }
                                    else if (e.Message != "" && e.Message.Contains("status does not allow the requested command"))
                                    {
                                        return ERROR.AuthenticationError;
                                    }
                                    else if (e.Message != "" && e.Message.Contains("Insufficient NV-Memory"))
                                    {
                                        return ERROR.OutOfMemory;
                                    }
                                    else
                                    {
                                        return ERROR.IOError;
                                    }
                                }
                            }
                            else if (card.Type == "DESFire")
                            {
                                try
                                {
                                    var cmd = card.Commands as IDESFireCommands;

                                    try
                                    {
                                        cmd.SelectApplication((uint)_appID);
                                        cmd.Authenticate((byte)0, aiToUse.MasterCardKey);
                                    }
                                    catch
                                    {
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
                                catch (Exception e)
                                {
                                    if (e.Message != "" && e.Message.Contains("same number already exists"))
                                    {
                                        return ERROR.ItemAlreadyExistError;
                                    }
                                    else if (e.Message != "" && e.Message.Contains("status does not allow the requested command"))
                                    {
                                        return ERROR.AuthenticationError;
                                    }
                                    else if (e.Message != "" && e.Message.Contains("Insufficient NV-Memory"))
                                    {
                                        return ERROR.OutOfMemory;
                                    }
                                    else
                                    {
                                        return ERROR.IOError;
                                    }
                                }
                            }
                            else
                            {
                                return ERROR.DeviceNotReadyError;
                            }
                        }
                    }
                }
                */
                return ERROR.DeviceNotReadyError;
            }
            catch
            {
                return ERROR.IOError;
            }
        }

        public ERROR ReadMiFareDESFireChipFile(string _appMasterKey, DESFireKeyType _keyTypeAppMasterKey,
                                               string _appReadKey, DESFireKeyType _keyTypeAppReadKey, int _readKeyNo,
                                               string _appWriteKey, DESFireKeyType _keyTypeAppWriteKey, int _writeKeyNo,
                                               EncryptionMode _encMode,
                                               int _fileNo, int _appID, int _fileSize)
        {
            try
            {
                /*
                // The excepted memory tree
                IDESFireLocation location = new DESFireLocation
                {
                    // The Application ID to use
                    aid = _appID,
                    // File 0 into this application
                    File = _fileNo,
                    // File communication requires encryption
                    SecurityLevel = _encMode
                };

                // Keys to use for authentication

                // Get the card storage service
                IStorageCardService storage = (IStorageCardService)card.GetService(CardServiceType.CST_STORAGE);

                // Change keys with the following ones
                IDESFireAccessInfo aiToWrite = new DESFireAccessInfo();
                CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_appMasterKey);
                aiToWrite.MasterApplicationKey.Value = CustomConverter.DesfireKeyToCheck;
                aiToWrite.MasterApplicationKey.KeyType = (LibLogicalAccess.DESFireKeyType)_keyTypeAppMasterKey;

                CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_appReadKey);
                aiToWrite.ReadKey.Value = CustomConverter.DesfireKeyToCheck;
                aiToWrite.ReadKey.KeyType = (LibLogicalAccess.DESFireKeyType)_keyTypeAppReadKey;
                aiToWrite.ReadKeyNo = (byte)_readKeyNo;

                CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_appWriteKey);
                aiToWrite.WriteKey.Value = CustomConverter.DesfireKeyToCheck;
                aiToWrite.WriteKey.KeyType = (LibLogicalAccess.DESFireKeyType)_keyTypeAppWriteKey;
                aiToWrite.WriteKeyNo = (byte)_writeKeyNo;

                if (readerDevice.ConnectToReader())
                {
                    if (readerDevice.WaitInsertion(Constants.MAX_WAIT_INSERTION))
                    {
                        if (readerDevice.Connect())
                        {
                            if (card.Type == "DESFire" ||
                                card.Type == "DESFireEV1" ||
                                card.Type == "DESFireEV2")
                            {
                                try
                                {
                                    var cmd = card.Commands as IDESFireCommands;

                                    try
                                    {

                                        cmd.SelectApplication((uint)_appID);

                                        cmd.Authenticate((byte)_readKeyNo, aiToWrite.ReadKey);

                                        DesfireFileSetting = cmd.GetFileSettings((byte)_fileNo);

                                        MifareDESFireData = (byte[])cmd.ReadData((byte)_fileNo, 0, DesfireFileSetting.dataFile.fileSize, EncryptionMode.CM_ENCRYPT);
                                    }
                                    catch
                                    {
                                        cmd.SelectApplication((uint)_appID);

                                        cmd.Authenticate((byte)_readKeyNo, aiToWrite.ReadKey);

                                        MifareDESFireData = (byte[])cmd.ReadData((byte)_fileNo, 0, (uint)_fileSize, EncryptionMode.CM_ENCRYPT);
                                    }

                                    return ERROR.NoError;
                                }

                                catch (Exception e)
                                {
                                    if (e.Message != "" && e.Message.Contains("same number already exists"))
                                    {
                                        return ERROR.ItemAlreadyExistError;
                                    }
                                    else if (e.Message != "" && e.Message.Contains("status does not allow the requested command"))
                                    {
                                        return ERROR.AuthenticationError;
                                    }
                                    else
                                    {
                                        return ERROR.IOError;
                                    }
                                }
                            }
                            else
                            {
                                return ERROR.DeviceNotReadyError;
                            }
                        }
                    }
                }
                */
                return ERROR.DeviceNotReadyError;
            }

            catch
            {
                return ERROR.IOError;
            }
        }

        public ERROR WriteMiFareDESFireChipFile(string _cardMasterKey, DESFireKeyType _keyTypeCardMasterKey,
                                                string _appMasterKey, DESFireKeyType _keyTypeAppMasterKey,
                                                string _appReadKey, DESFireKeyType _keyTypeAppReadKey, int _readKeyNo,
                                                string _appWriteKey, DESFireKeyType _keyTypeAppWriteKey, int _writeKeyNo,
                                                EncryptionMode _encMode,
                                                int _fileNo, int _appID, byte[] _data)
        {

            try
            {
                /*
                // The excepted memory tree
                IDESFireLocation location = new DESFireLocation
                {
                    // The Application ID to use
                    aid = _appID,
                    // File 0 into this application
                    File = _fileNo,
                    // File communication requires encryption
                    SecurityLevel = _encMode
                };

                // Keys to use for authentication

                // Get the card storage service
                IStorageCardService storage = (IStorageCardService)card.GetService(CardServiceType.CST_STORAGE);

                // Change keys with the following ones
                IDESFireAccessInfo aiToUse = new DESFireAccessInfo();
                CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_cardMasterKey);
                aiToUse.MasterCardKey.Value = CustomConverter.DesfireKeyToCheck;
                aiToUse.MasterCardKey.KeyType = _keyTypeAppMasterKey;

                IDESFireAccessInfo aiToWrite = new DESFireAccessInfo();
                aiToWrite.MasterCardKey.Value = CustomConverter.DesfireKeyToCheck;
                aiToWrite.MasterCardKey.KeyType = _keyTypeAppMasterKey;

                CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_appMasterKey);
                aiToWrite.MasterApplicationKey.Value = CustomConverter.DesfireKeyToCheck;
                aiToWrite.MasterApplicationKey.KeyType = _keyTypeAppMasterKey;

                CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_appReadKey);
                aiToWrite.ReadKey.Value = CustomConverter.DesfireKeyToCheck;
                aiToWrite.ReadKey.KeyType = _keyTypeAppReadKey;
                aiToWrite.ReadKeyNo = (byte)_readKeyNo;

                CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_appWriteKey);
                aiToWrite.WriteKey.Value = CustomConverter.DesfireKeyToCheck;
                aiToWrite.WriteKey.KeyType = _keyTypeAppWriteKey;
                aiToWrite.WriteKeyNo = (byte)_writeKeyNo;

                if (readerDevice.ConnectToReader())
                {
                    if (readerDevice.WaitInsertion(Constants.MAX_WAIT_INSERTION))
                    {
                        if (readerDevice.Connect())
                        {
                            if (card.Type == "DESFire" ||
                                card.Type == "DESFireEV1" ||
                                card.Type == "DESFireEV2")
                            {
                                try
                                {
                                    var cmd = card.Commands as IDESFireCommands;

                                    cmd.SelectApplication((uint)_appID);

                                    cmd.Authenticate((byte)_writeKeyNo, aiToWrite.WriteKey);

                                    cmd.WriteData((byte)_fileNo, 0, (uint)_data.Length, EncryptionMode.CM_ENCRYPT, _data);

                                    try
                                    {
                                        cmd.CommitTransaction();
                                    }
                                    catch { }

                                    return ERROR.NoError;
                                }

                                catch (Exception e)
                                {
                                    if (e.Message != "" && e.Message.Contains("same number already exists"))
                                    {
                                        return ERROR.ItemAlreadyExistError;
                                    }
                                    else if (e.Message != "" && e.Message.Contains("status does not allow the requested command"))
                                    {
                                        return ERROR.AuthenticationError;
                                    }
                                    else
                                    {
                                        return ERROR.IOError;
                                    }
                                }
                            }
                            else
                            {
                                return ERROR.DeviceNotReadyError;
                            }
                        }
                    }
                }
                */
                return ERROR.DeviceNotReadyError;
            }

            catch
            {
                return ERROR.IOError;
            }
        }

        public ERROR AuthToMifareDesfireApplication(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumber, int _appID = 0)
        {
            try
            {
                /*
                // The excepted memory tree
                IDESFireLocation location = new DESFireLocation
                {
                    // The Application ID to use
                    aid = _appID,
                    // File communication requires encryption
                    SecurityLevel = EncryptionMode.CM_ENCRYPT
                };

                // Keys to use for authentication
                IDESFireAccessInfo aiToUse = new DESFireAccessInfo();
                CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_applicationMasterKey);
                aiToUse.MasterCardKey.Value = CustomConverter.DesfireKeyToCheck;
                aiToUse.MasterCardKey.KeyType = (LibLogicalAccess.DESFireKeyType)_keyType;

                if (readerDevice.ConnectToReader())
                {
                    if (readerDevice.WaitInsertion(Constants.MAX_WAIT_INSERTION))
                    {
                        if (readerDevice.Connect())
                        {
                            readerDeviceName = readerDevice.ConnectedName;
                            //readerSerialNumber = readerDevice.GetReaderSerialNumber();

                            card = readerDevice.GetSingleChip();

                            if (card.Type == "DESFire")
                            {
                                var cmd = card.Commands as IDESFireCommands;
                                try
                                {
                                    cmd.SelectApplication((uint)_appID);
                                    if (_appID > 0)
                                    {
                                        cmd.Authenticate((byte)_keyNumber, aiToUse.MasterCardKey);
                                    }
                                    else
                                    {
                                        cmd.Authenticate((byte)0, aiToUse.MasterCardKey);
                                    }

                                    readerDevice.Disconnect();
                                    return ERROR.NoError;
                                }

                                catch (Exception e)
                                {
                                    if (e.Message != "" && e.Message.Contains("same number already exists"))
                                    {
                                        readerDevice.Disconnect();
                                        return ERROR.ItemAlreadyExistError;
                                    }
                                    else if (e.Message != "" && e.Message.Contains("status does not allow the requested command"))
                                    {
                                        readerDevice.Disconnect();
                                        return ERROR.AuthenticationError;
                                    }
                                    else
                                    {
                                        readerDevice.Disconnect();
                                        return ERROR.IOError;
                                    }

                                }
                            }

                            else if (card.Type == "DESFireEV1" ||
                                    card.Type == "DESFireEV2")
                            {
                                var cmd = card.Commands as IDESFireEV1Commands;
                                try
                                {
                                    cmd.SelectApplication((uint)_appID);
                                    if (_appID > 0)
                                    {
                                        cmd.Authenticate((byte)_keyNumber, aiToUse.MasterCardKey);
                                    }
                                    else
                                    {
                                        cmd.Authenticate((byte)0, aiToUse.MasterCardKey);
                                    }

                                    readerDevice.Disconnect();
                                    return ERROR.NoError;
                                }

                                catch (Exception e)
                                {
                                    if (e.Message != "" && e.Message.Contains("same number already exists"))
                                    {
                                        readerDevice.Disconnect();
                                        return ERROR.ItemAlreadyExistError;
                                    }
                                    else if (e.Message != "" && e.Message.Contains("status does not allow the requested command"))
                                    {
                                        readerDevice.Disconnect();
                                        return ERROR.AuthenticationError;
                                    }
                                    else
                                    {
                                        readerDevice.Disconnect();
                                        return ERROR.IOError;
                                    }

                                }
                            }
                            else
                            {
                                readerDevice.Disconnect();
                                return ERROR.DeviceNotReadyError;
                            }

                        }
                    }
                }
                */
                readerDevice.Disconnect();
                return ERROR.DeviceNotReadyError;
            }
            catch
            {
                readerDevice.Disconnect();
                return ERROR.IOError;
            }
        }

        public ERROR GetMifareDesfireAppSettings(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumberCurrent = 0, int _appID = 0)
        {
            //byte maxNbrOfKeys;
            //DESFireKeySettings keySettings;

            try
            {
                /*
                // Keys to use for authentication
                IDESFireAccessInfo aiToUse = new DESFireAccessInfo();
                CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_applicationMasterKey);
                aiToUse.MasterCardKey.Value = CustomConverter.DesfireKeyToCheck;
                aiToUse.MasterCardKey.KeyType = (LibLogicalAccess.DESFireKeyType)_keyType;

                if (readerDevice.ConnectToReader())
                {
                    if (readerDevice.WaitInsertion(Constants.MAX_WAIT_INSERTION))
                    {
                        if (readerDevice.Connect())
                        {
                            readerDeviceName = readerDevice.ConnectedName;
                            card = readerDevice.GetSingleChip();

                            if (card.Type == "DESFire")
                            {
                                var cmd = card.Commands as IDESFireCommands;
                                readerDeviceName = readerDevice.ConnectedName;

                                try
                                {
                                    cmd.SelectApplication((uint)_appID);
                                    GenericChip = new GenericChipModel(card.ChipIdentifier, (CARD_TYPE)Enum.Parse(typeof(CARD_TYPE), card.Type));

                                    try
                                    {
                                        cmd.Authenticate((byte)_keyNumberCurrent, aiToUse.MasterCardKey);
                                    }

                                    catch
                                    {
                                        try
                                        {
                                            cmd.GetKeySettings(out keySettings, out maxNbrOfKeys);
                                            MaxNumberOfAppKeys = (byte)(maxNbrOfKeys & 0x0F);
                                            EncryptionType = (byte)(maxNbrOfKeys & 0xF0);
                                            DesfireAppKeySetting = keySettings;

                                            return ERROR.NoError;
                                        }
                                        catch (Exception e)
                                        {
                                            if (e.Message != "" && e.Message.Contains("same number already exists"))
                                            {
                                                return ERROR.ItemAlreadyExistError;
                                            }
                                            else if (e.Message != "" && e.Message.Contains("status does not allow the requested command"))
                                            {
                                                return ERROR.AuthenticationError;
                                            }
                                            else
                                            {
                                                return ERROR.IOError;
                                            }
                                        }
                                    }
                                    cmd.GetKeySettings(out keySettings, out maxNbrOfKeys);
                                    MaxNumberOfAppKeys = (byte)(maxNbrOfKeys & 0x0F);
                                    EncryptionType = (byte)(maxNbrOfKeys & 0xF0);
                                    DesfireAppKeySetting = keySettings;

                                    return ERROR.NoError;
                                }
                                catch (Exception e)
                                {
                                    if (e.Message != "" && e.Message.Contains("same number already exists"))
                                    {
                                        return ERROR.ItemAlreadyExistError;
                                    }
                                    else if (e.Message != "" && e.Message.Contains("status does not allow the requested command"))
                                    {
                                        return ERROR.AuthenticationError;
                                    }
                                    else
                                    {
                                        return ERROR.IOError;
                                    }
                                }
                            }

                            else if (card.Type == "DESFireEV1" ||
                                    card.Type == "DESFireEV2" || card.Type == "GENERIC_T_CL_A")
                            {
                                var cmd = card.Commands as IDESFireEV1Commands;
                                readerDeviceName = readerDevice.ConnectedName;

                                try
                                {
                                    cmd.SelectApplication((uint)_appID);
                                    GenericChip = new GenericChipModel(card.ChipIdentifier, (CARD_TYPE)Enum.Parse(typeof(CARD_TYPE), card.Type));

                                    try
                                    {
                                        cmd.Authenticate((byte)_keyNumberCurrent, aiToUse.MasterCardKey);
                                    }

                                    catch
                                    {
                                        try
                                        {
                                            try
                                            {
                                                GenericChip.FreeMemory = cmd.GetFreeMemory();
                                            }

                                            catch { }

                                            cmd.GetKeySettings(out keySettings, out maxNbrOfKeys);
                                            MaxNumberOfAppKeys = (byte)(maxNbrOfKeys & 0x0F);
                                            EncryptionType = (byte)(maxNbrOfKeys & 0xF0);
                                            DesfireAppKeySetting = keySettings;

                                            return ERROR.NoError;
                                        }
                                        catch (Exception e)
                                        {
                                            if (e.Message != "" && e.Message.Contains("same number already exists"))
                                            {
                                                return ERROR.ItemAlreadyExistError;
                                            }
                                            else if (e.Message != "" && e.Message.Contains("status does not allow the requested command"))
                                            {
                                                return ERROR.NotAllowed;
                                            }
                                            else
                                            {
                                                return ERROR.IOError;
                                            }
                                        }
                                    }

                                    try
                                    {
                                        GenericChip.FreeMemory = cmd.GetFreeMemory();
                                    }

                                    catch { }

                                    cmd.GetKeySettings(out keySettings, out maxNbrOfKeys);
                                    MaxNumberOfAppKeys = (byte)(maxNbrOfKeys & 0x0F);
                                    EncryptionType = (byte)(maxNbrOfKeys & 0xF0);
                                    DesfireAppKeySetting = keySettings;

                                    return ERROR.NoError;
                                }
                                catch (Exception e)
                                {
                                    if (e.Message != "" && e.Message.Contains("same number already exists"))
                                    {
                                        return ERROR.ItemAlreadyExistError;
                                    }
                                    else if (e.Message != "" && e.Message.Contains("status does not allow the requested command"))
                                    {
                                        return ERROR.AuthenticationError;
                                    }
                                    else
                                    {
                                        return ERROR.IOError;
                                    }
                                }
                            }

                            else
                            {
                                return ERROR.DeviceNotReadyError;
                            }
                        }
                    }
                }
                */
                return ERROR.DeviceNotReadyError;
            }
            catch
            {
                return ERROR.IOError;
            }
        }

        public ERROR CreateMifareDesfireApplication(string _piccMasterKey, DESFireKeySettings _keySettingsTarget, DESFireKeyType _keyTypePiccMasterKey, DESFireKeyType _keyTypeTargetApplication, int _maxNbKeys, int _appID, bool authenticateToPICCFirst = true)
        {
            try
            {
                /*
                // The excepted memory tree
                IDESFireLocation location = new DESFireLocation
                {
                    // The Application ID to use
                    aid = _appID,
                    // File communication requires encryption
                    SecurityLevel = EncryptionMode.CM_ENCRYPT
                };

                // Keys to use for authentication
                IDESFireAccessInfo aiToUse = new DESFireAccessInfo();
                CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_piccMasterKey);
                aiToUse.MasterCardKey.Value = CustomConverter.DesfireKeyToCheck;
                aiToUse.MasterCardKey.KeyType = (LibLogicalAccess.DESFireKeyType)_keyTypePiccMasterKey;

                if (readerDevice.ConnectToReader())
                {
                    if (readerDevice.WaitInsertion(Constants.MAX_WAIT_INSERTION))
                    {
                        if (readerDevice.Connect())
                        {
                            readerDeviceName = readerDevice.ConnectedName;

                            card = readerDevice.GetSingleChip();

                            if (card.Type == "DESFire")
                            {
                                var cmd = card.Commands as IDESFireCommands;
                                try
                                {
                                    cmd.SelectApplication(0);

                                    if (authenticateToPICCFirst)
                                    {
                                        cmd.Authenticate(0, aiToUse.MasterCardKey);
                                    }

                                    cmd.CreateApplication((uint)_appID, _keySettingsTarget, (byte)_maxNbKeys); //_keySettingsTarget

                                    return ERROR.NoError;
                                }
                                catch (Exception e)
                                {
                                    if (e.Message != "" && e.Message.Contains("same number already exists"))
                                    {
                                        return ERROR.ItemAlreadyExistError;
                                    }
                                    else if (e.Message != "" && e.Message.Contains("status does not allow the requested command"))
                                    {
                                        return ERROR.AuthenticationError;
                                    }
                                    else
                                    {
                                        return ERROR.IOError;
                                    }
                                }
                            }

                            else if (card.Type == "DESFireEV1" ||
                                    card.Type == "DESFireEV2")
                            {
                                var cmd = card.Commands as IDESFireEV1Commands;
                                try
                                {
                                    cmd.SelectApplication(0);

                                    if (authenticateToPICCFirst)
                                    {
                                        cmd.Authenticate(0, aiToUse.MasterCardKey);
                                    }

                                    cmd.CreateApplicationEV1((uint)_appID, _keySettingsTarget, (byte)_maxNbKeys, false, _keyTypeTargetApplication, 0, 0);

                                    return ERROR.NoError;
                                }
                                catch (Exception e)
                                {
                                    if (e.Message != "" && e.Message.Contains("same number already exists"))
                                    {
                                        return ERROR.ItemAlreadyExistError;
                                    }
                                    else if (e.Message != "" && e.Message.Contains("status does not allow the requested command"))
                                    {
                                        return ERROR.AuthenticationError;
                                    }
                                    else if (e.Message != "" && e.Message.Contains("Insufficient NV-Memory"))
                                    {
                                        return ERROR.OutOfMemory;
                                    }
                                    else
                                    {
                                        return ERROR.IOError;
                                    }
                                }
                            }
                            else
                            {
                                return ERROR.DeviceNotReadyError;
                            }
                        }
                    }
                }
                */
                return ERROR.DeviceNotReadyError;
            }
            catch
            {
                return ERROR.IOError;
            }
        }

        public ERROR ChangeMifareDesfireApplicationKey(string _applicationMasterKeyCurrent, int _keyNumberCurrent, DESFireKeyType _keyTypeCurrent, string _applicationMasterKeyTarget, int _keyNumberTarget, DESFireKeyType _keyTypeTarget, int _appIDCurrent = 0, int _appIDTarget = 0, DESFireKeySettings keySettings = (DESFireKeySettings.KS_DEFAULT | DESFireKeySettings.KS_FREE_CREATE_DELETE_WITHOUT_MK))
        {
            try
            {
                /*
                DESFireKey masterApplicationKey = new DESFireKeyClass
                {
                    KeyType = (LibLogicalAccess.DESFireKeyType)_keyTypeCurrent
                };
                CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_applicationMasterKeyCurrent);
                masterApplicationKey.Value = CustomConverter.DesfireKeyToCheck;
                masterApplicationKey.KeyVersion = 0;

                DESFireKey applicationMasterKeyTarget = new DESFireKeyClass
                {
                    KeyType = (LibLogicalAccess.DESFireKeyType)_keyTypeCurrent
                };
                CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_applicationMasterKeyTarget);
                applicationMasterKeyTarget.Value = CustomConverter.DesfireKeyToCheck;
                applicationMasterKeyTarget.KeyVersion = 0;

                readerDevice.DisconnectFromReader();

                if (readerDevice.ConnectToReader())
                {
                    if (readerDevice.WaitInsertion(Constants.MAX_WAIT_INSERTION))
                    {
                        if (readerDevice.Connect())
                        {
                            readerDeviceName = readerDevice.ConnectedName;

                            card = readerDevice.GetSingleChip();

                            if (card.Type == "DESFire" ||
                                card.Type == "DESFireEV1" ||
                                card.Type == "DESFireEV2")
                            {
                                var cmd = card.Commands as DESFireCommands;
                                try
                                {
                                    if (_appIDCurrent == 0)
                                    {
                                        try
                                        {
                                            applicationMasterKeyTarget.KeyType = (DESFireKeyType)_keyTypeTarget;

                                            cmd.SelectApplication((uint)0);
                                            cmd.Authenticate((byte)0, masterApplicationKey);
                                            cmd.ChangeKeySettings(keySettings);
                                            cmd.Authenticate((byte)0, masterApplicationKey);
                                            cmd.ChangeKey((byte)0, applicationMasterKeyTarget);
                                            return ERROR.NoError;
                                        }

                                        catch
                                        {
                                            try
                                            {
                                                cmd.Authenticate((byte)0, masterApplicationKey);
                                                cmd.ChangeKey((byte)0, applicationMasterKeyTarget);
                                            }
                                            catch (Exception e)
                                            {
                                                if (e.Message != "" && e.Message.Contains("same number already exists"))
                                                {
                                                    return ERROR.ItemAlreadyExistError;
                                                }
                                                else if (e.Message != "" && e.Message.Contains("status does not allow the requested command"))
                                                {
                                                    return ERROR.AuthenticationError;
                                                }
                                                else
                                                {
                                                    return ERROR.IOError;
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        cmd.SelectApplication((uint)0);
                                        cmd.Authenticate((byte)0, masterApplicationKey);

                                        try
                                        {
                                            cmd.SelectApplication((uint)_appIDCurrent);

                                            cmd.Authenticate((byte)_keyNumberCurrent, masterApplicationKey);
                                            cmd.ChangeKey((byte)_keyNumberTarget, applicationMasterKeyTarget);
                                            cmd.Authenticate((byte)_keyNumberCurrent, applicationMasterKeyTarget);

                                            try
                                            {
                                                cmd.ChangeKeySettings(keySettings);
                                            }
                                            catch { }
                                        }

                                        catch (Exception ex)
                                        {
                                            try
                                            {
                                                cmd.Authenticate((byte)_keyNumberCurrent, masterApplicationKey);
                                                cmd.ChangeKeySettings(keySettings);
                                                cmd.Authenticate((byte)_keyNumberCurrent, masterApplicationKey);
                                                cmd.ChangeKey((byte)_keyNumberTarget, applicationMasterKeyTarget);
                                                return ERROR.NoError;
                                            }

                                            catch
                                            {
                                                try
                                                {
                                                    cmd.Authenticate((byte)_keyNumberCurrent, masterApplicationKey);
                                                    cmd.ChangeKey((byte)_keyNumberTarget, applicationMasterKeyTarget);
                                                }
                                                catch (Exception e)
                                                {
                                                    if (e.Message != "" && e.Message.Contains("same number already exists"))
                                                    {
                                                        return ERROR.ItemAlreadyExistError;
                                                    }
                                                    else if (e.Message != "" && e.Message.Contains("status does not allow the requested command"))
                                                    {
                                                        return ERROR.AuthenticationError;
                                                    }
                                                    else
                                                    {
                                                        return ERROR.IOError;
                                                    }
                                                }
                                            }
                                        }
                                    }

                                    return ERROR.NoError;
                                }
                                catch (Exception e)
                                {
                                    if (e.Message != "" && e.Message.Contains("same number already exists"))
                                    {
                                        return ERROR.ItemAlreadyExistError;
                                    }
                                    else if (e.Message != "" && e.Message.Contains("status does not allow the requested command"))
                                    {
                                        return ERROR.AuthenticationError;
                                    }
                                    else
                                    {
                                        return ERROR.IOError;
                                    }
                                }
                            }
                            else
                            {
                                return ERROR.DeviceNotReadyError;
                            }
                        }
                    }
                }
                */
                return ERROR.DeviceNotReadyError;
            }
            catch
            {
                return ERROR.IOError;
            }
        }

        public ERROR DeleteMifareDesfireApplication(string _applicationMasterKey, DESFireKeyType _keyType, int _appID = 0)
        {
            try
            {
                /*
                // The excepted memory tree
                IDESFireLocation location = new DESFireLocation
                {
                    // The Application ID to use
                    aid = _appID,
                    // File communication requires encryption
                    SecurityLevel = EncryptionMode.CM_ENCRYPT
                };

                // Keys to use for authentication
                IDESFireAccessInfo aiToUse = new DESFireAccessInfo();
                CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_applicationMasterKey);
                aiToUse.MasterCardKey.Value = CustomConverter.DesfireKeyToCheck;
                aiToUse.MasterCardKey.KeyType = _keyType;

                if (readerDevice.ConnectToReader())
                {
                    if (readerDevice.WaitInsertion(Constants.MAX_WAIT_INSERTION))
                    {
                        if (readerDevice.Connect())
                        {
                            readerDeviceName = readerDevice.ConnectedName;

                            card = readerDevice.GetSingleChip();

                            if (card.Type == "DESFire" ||
                                card.Type == "DESFireEV1" ||
                                card.Type == "DESFireEV2")
                            {
                                var cmd = card.Commands as IDESFireCommands;
                                try
                                {
                                    cmd.SelectApplication(0);
                                    cmd.Authenticate(0, aiToUse.MasterCardKey);

                                    cmd.DeleteApplication((uint)_appID);
                                    return ERROR.NoError;
                                }
                                catch
                                {
                                    try
                                    {
                                        cmd.SelectApplication((uint)_appID);
                                        cmd.Authenticate(0, aiToUse.MasterCardKey);
                                        cmd.DeleteApplication((uint)_appID);
                                        return ERROR.NoError;
                                    }

                                    catch (Exception e)
                                    {
                                        if (e.Message != "" && e.Message.Contains("same number already exists"))
                                        {
                                            return ERROR.ItemAlreadyExistError;
                                        }
                                        else if (e.Message != "" && e.Message.Contains("status does not allow the requested command"))
                                        {
                                            return ERROR.AuthenticationError;
                                        }
                                        else
                                        {
                                            return ERROR.IOError;
                                        }
                                    }
                                }
                            }
                            return ERROR.DeviceNotReadyError;
                        }
                    }
                }
                */
                return ERROR.DeviceNotReadyError;
            }
            catch
            {
                return ERROR.IOError;
            }
        }

        public ERROR DeleteMifareDesfireFile(string _applicationMasterKey, DESFireKeyType _keyType, int _appID = 0, int _fileID = 0)
        {
            try
            {
                /*
                // The excepted memory tree
                IDESFireLocation location = new DESFireLocation
                {
                    // The Application ID to use
                    aid = _appID,
                    // File communication requires encryption
                    SecurityLevel = EncryptionMode.CM_ENCRYPT
                };

                // Keys to use for authentication
                IDESFireAccessInfo aiToUse = new DESFireAccessInfo();
                CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_applicationMasterKey);
                aiToUse.MasterCardKey.Value = CustomConverter.DesfireKeyToCheck;
                aiToUse.MasterCardKey.KeyType = (LibLogicalAccess.DESFireKeyType)_keyType;

                if (readerDevice.ConnectToReader())
                {
                    if (readerDevice.WaitInsertion(Constants.MAX_WAIT_INSERTION))
                    {
                        if (readerDevice.Connect())
                        {
                            readerDeviceName = readerDevice.ConnectedName;

                            card = readerDevice.GetSingleChip();

                            if (card.Type == "DESFireEV1" ||
                                card.Type == "DESFireEV2" ||
                                card.Type == "DESFire")
                            {
                                try
                                {
                                    var cmd = card.Commands as IDESFireCommands;

                                    try
                                    {
                                        cmd.SelectApplication((uint)_appID);
                                        cmd.Authenticate(0, aiToUse.MasterCardKey);
                                    }

                                    catch
                                    {
                                        cmd.DeleteFile((byte)_fileID);
                                        return ERROR.NoError;
                                    }

                                    cmd.DeleteFile((byte)_fileID);
                                    return ERROR.NoError;
                                }
                                catch (Exception e)
                                {
                                    if (e.Message != "" && e.Message.Contains("same number already exists"))
                                    {
                                        return ERROR.ItemAlreadyExistError;
                                    }
                                    else if (e.Message != "" && e.Message.Contains("status does not allow the requested command"))
                                    {
                                        return ERROR.AuthenticationError;
                                    }
                                    else
                                    {
                                        return ERROR.IOError;
                                    }
                                }
                            }
                            else
                            {
                                return ERROR.DeviceNotReadyError;
                            }
                        }
                    }
                }
                */
                return ERROR.DeviceNotReadyError;
            }
            catch
            {
                return ERROR.IOError;
            }
        }

        public ERROR FormatDesfireCard(string _applicationMasterKey, DESFireKeyType _keyType, int _appID = 0)
        {
            try
            {
                /*
                // The excepted memory tree
                IDESFireLocation location = new DESFireLocation
                {
                    // The Application ID to use
                    aid = _appID,
                    // File communication requires encryption
                    SecurityLevel = EncryptionMode.CM_ENCRYPT
                };

                IDESFireEV1Commands cmd;
                // Keys to use for authentication
                IDESFireAccessInfo aiToUse = new DESFireAccessInfo();
                CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_applicationMasterKey);
                aiToUse.MasterCardKey.Value = CustomConverter.DesfireKeyToCheck;
                aiToUse.MasterCardKey.KeyType = (LibLogicalAccess.DESFireKeyType)_keyType;

                if (readerDevice.ConnectToReader())
                {
                    if (readerDevice.WaitInsertion(Constants.MAX_WAIT_INSERTION))
                    {
                        if (readerDevice.Connect())
                        {
                            readerDeviceName = readerDevice.ConnectedName;

                            card = readerDevice.GetSingleChip();

                            if (card.Type == "DESFire" ||
                               card.Type == "DESFireEV1" ||
                               card.Type == "DESFireEV2")
                            {
                                cmd = card.Commands as IDESFireEV1Commands;
                                try
                                {
                                    cmd.SelectApplication(0);
                                    cmd.Authenticate(0, aiToUse.MasterCardKey);

                                    cmd.Erase();

                                    return ERROR.NoError;
                                }
                                catch (Exception e)
                                {
                                    if (e.Message != "" && e.Message.Contains("same number already exists"))
                                    {
                                        return ERROR.ItemAlreadyExistError;
                                    }
                                    else if (e.Message != "" && e.Message.Contains("status does not allow the requested command"))
                                    {
                                        return ERROR.AuthenticationError;
                                    }
                                    else
                                    {
                                        return ERROR.IOError;
                                    }
                                }
                            }
                            else
                            {
                                return ERROR.DeviceNotReadyError;
                            }
                        }
                    }
                }
                */
                return ERROR.DeviceNotReadyError;
            }
            catch
            {
                return ERROR.IOError;
            }
        }

        public ERROR GetMifareDesfireFileList(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumberCurrent = 0, int _appID = 0)
        {
            try
            {
                /*
                // The excepted memory tree
                IDESFireLocation location = new DESFireLocation
                {
                    // The Application ID to use
                    aid = _appID,
                    // File communication requires encryption
                    SecurityLevel = EncryptionMode.CM_ENCRYPT
                };

                // Keys to use for authentication
                IDESFireAccessInfo aiToUse = new DESFireAccessInfo();
                CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_applicationMasterKey);
                aiToUse.MasterCardKey.Value = CustomConverter.DesfireKeyToCheck;
                aiToUse.MasterCardKey.KeyType = _keyType;

                object fileIDsObject;

                if (readerDevice.ConnectToReader())
                {
                    if (readerDevice.WaitInsertion(Constants.MAX_WAIT_INSERTION))
                    {
                        if (readerDevice.Connect())
                        {
                            readerDeviceName = readerDevice.ConnectedName;

                            card = readerDevice.GetSingleChip();

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
                                            fileIDsObject = cmd.GetFileIDs();
                                            FileIDList = (fileIDsObject as byte[]);
                                            return ERROR.NoError;
                                        }
                                        catch (Exception e)
                                        {
                                            if (e.Message != "" && e.Message.Contains("same number already exists"))
                                            {
                                                return ERROR.ItemAlreadyExistError;
                                            }
                                            else if (e.Message != "" && e.Message.Contains("status does not allow the requested command"))
                                            {
                                                return ERROR.AuthenticationError;
                                            }
                                            else
                                            {
                                                return ERROR.IOError;
                                            }
                                        }
                                    }

                                    fileIDsObject = cmd.GetFileIDs();
                                    FileIDList = (fileIDsObject as byte[]);

                                    return ERROR.NoError;
                                }
                                catch (Exception e)
                                {
                                    if (e.Message != "" && e.Message.Contains("same number already exists"))
                                    {
                                        return ERROR.ItemAlreadyExistError;
                                    }
                                    else if (e.Message != "" && e.Message.Contains("status does not allow the requested command"))
                                    {
                                        return ERROR.AuthenticationError;
                                    }
                                    else
                                    {
                                        return ERROR.IOError;
                                    }
                                }
                            }
                            else
                            {
                                return ERROR.DeviceNotReadyError;
                            }
                        }
                    }
                }
                */
                return ERROR.DeviceNotReadyError;
            }
            catch
            {
                return ERROR.IOError;
            }
        }

        public ERROR GetMifareDesfireFileSettings(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumberCurrent = 0, int _appID = 0, int _fileNo = 0)
        {
            try
            {
                /*
                // Keys to use for authentication
                IDESFireAccessInfo aiToUse = new DESFireAccessInfo();
                CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_applicationMasterKey);
                aiToUse.MasterCardKey.Value = CustomConverter.DesfireKeyToCheck;
                aiToUse.MasterCardKey.KeyType = (LibLogicalAccess.DESFireKeyType)_keyType;

                if (readerDevice.ConnectToReader())
                {
                    if (readerDevice.WaitInsertion(Constants.MAX_WAIT_INSERTION))
                    {
                        if (readerDevice.Connect())
                        {
                            readerDeviceName = readerDevice.ConnectedName;
                            card = readerDevice.GetSingleChip();

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
                                            DesfireFileSetting = cmd.GetFileSettings((byte)_fileNo);

                                            return ERROR.NoError;
                                        }
                                        catch (Exception e)
                                        {
                                            if (e.Message != "" && e.Message.Contains("same number already exists"))
                                            {
                                                return ERROR.ItemAlreadyExistError;
                                            }
                                            else if (e.Message != "" && e.Message.Contains("status does not allow the requested command"))
                                            {
                                                return ERROR.AuthenticationError;
                                            }
                                            else
                                            {
                                                return ERROR.IOError;
                                            }
                                        }
                                    }
                                    DesfireFileSetting = cmd.GetFileSettings((byte)_fileNo);

                                    return ERROR.NoError;
                                }
                                catch (Exception e)
                                {
                                    if (e.Message != "" && e.Message.Contains("same number already exists"))
                                    {
                                        return ERROR.ItemAlreadyExistError;
                                    }
                                    else if (e.Message != "" && e.Message.Contains("status does not allow the requested command"))
                                    {
                                        return ERROR.AuthenticationError;
                                    }
                                    else
                                    {
                                        return ERROR.IOError;
                                    }
                                }
                            }
                            else
                            {
                                return ERROR.DeviceNotReadyError;
                            }
                        }
                    }
                }
                */
                return ERROR.DeviceNotReadyError;
                 
            }
            catch
            {
                return ERROR.IOError;
            }

        }

        #endregion mifare desfire

        #region mifare plus

        public ERROR ReadMFPlusChip()
        {
            try
            {
                return ERROR.NoError;
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""), FacilityName);
                return ERROR.IOError;
            }
        }

        #endregion

        #region ISO15693

        public ERROR ReadISO15693Chip()
        {
            try
            {
                return ERROR.NoError;
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""), FacilityName);
                return ERROR.IOError;
            }
        }

        #endregion

        #region EM4135

        public ERROR ReadEM4135ChipPublic()
        {
            try
            {
               return ERROR.NoError;
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""), FacilityName);
                return ERROR.IOError;
            }
        }

        #endregion
    }
}