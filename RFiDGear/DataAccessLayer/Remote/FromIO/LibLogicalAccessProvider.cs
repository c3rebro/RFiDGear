using LibLogicalAccess;

using ByteArrayHelper.Extensions;
using RFiDGear.Model;

using Log4CSharp;

using System;
using System.Threading;

namespace RFiDGear.DataAccessLayer.Remote.FromIO
{
    /// <summary>
    /// Description of LibLogicalAccessProvider.
    ///
    /// Initialize Reader
    /// </summary>
    ///
    public class LibLogicalAccessProvider : ReaderDevice, IDisposable
    {
        // global (cross-class) Instances go here ->
        private static readonly string FacilityName = "RFiDGear";
        private IReaderProvider readerProvider;
        private IReaderUnit readerUnit;
        private chip card;
        private bool _disposed;

        private FileSetting desfireFileSetting { get; set; }

        #region contructor
        public LibLogicalAccessProvider()
        {
        }

        public LibLogicalAccessProvider(ReaderTypes readerType)
        {
            try
            {
                readerProvider = new LibraryManagerClass().GetReaderProvider(Enum.GetName(typeof(ReaderTypes), readerType));
                readerUnit = readerProvider.CreateReaderUnit();

                GenericChip = new GenericChipModel("", CARD_TYPE.Unspecified);
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(e, FacilityName);
            }
        }

        #endregion contructor

        #region common

        public override ERROR ReadChipPublic()
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

                            card = readerUnit.GetSingleChip();

                            if (!string.IsNullOrWhiteSpace(card.ChipIdentifier))
                            {
                                try
                                {
                                    GenericChip = new GenericChipModel(card.ChipIdentifier, (CARD_TYPE)Enum.Parse(typeof(CARD_TYPE), card.Type));

                                    if (Enum.GetName(typeof(CARD_TYPE), GenericChip.CardType).Contains("DESFire"))
                                    {
                                        var cmd = card.Commands as DESFireCommands;

                                        DESFireCardVersion version = cmd.GetVersion();

                                        if (version.softwareMjVersion == 1)
                                            GenericChip.CardType = CARD_TYPE.DESFireEV1;

                                        else if (version.softwareMjVersion == 2)
                                            GenericChip.CardType = CARD_TYPE.DESFireEV2;

                                        else if (version.softwareMjVersion == 3)
                                            GenericChip.CardType = CARD_TYPE.DESFireEV3;
                                    }

                                    DesfireChip = new MifareDesfireChipModel(GenericChip);

                                    return ERROR.NoError;
                                }
                                catch (Exception e)
                                {
                                    LogWriter.CreateLogEntry(e, FacilityName);

                                    return ERROR.IOError;
                                }
                            }
                            else
                                return ERROR.NotReadyError;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(e, FacilityName);

                return ERROR.IOError;
            }

            return ERROR.IOError;
        }

        #endregion common

        #region mifare classic

        public override ERROR ReadMiFareClassicSingleSector(int sectorNumber, string aKey, string bKey)
        {
            var settings = new SettingsReaderWriter();
            Sector = new MifareClassicSectorModel();

            settings.ReadSettings();

            var keyA = new MifareKey() { Value = CustomConverter.KeyFormatQuickCheck(aKey) ? aKey : CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(aKey) };
            var keyB = new MifareKey() { Value = CustomConverter.KeyFormatQuickCheck(bKey) ? bKey : CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(bKey) };

            try
            {
                if (readerUnit.ConnectToReader())
                {
                    if (readerUnit.WaitInsertion(Constants.MAX_WAIT_INSERTION))
                    {
                        if (readerUnit.Connect())
                        {
                            ReaderUnitName = readerUnit.ConnectedName;

                            card = readerUnit.GetSingleChip();

                            if (!string.IsNullOrWhiteSpace(card.ChipIdentifier))
                            {
                                try
                                {
                                    GenericChip = new GenericChipModel(card.ChipIdentifier, (CARD_TYPE)Enum.Parse(typeof(CARD_TYPE), card.Type));
                                }
                                catch (Exception e)
                                {
                                    LogWriter.CreateLogEntry(e, FacilityName);
                                }
                            }

                            var cmd = card.Commands as IMifareCommands;

                            try
                            { //try to Auth with Keytype A
                                for (int k = 0; k < (sectorNumber > 31 ? 16 : 4); k++) // if sector > 31 is 16 blocks each sector i.e. mifare 4k else its 1k or 2k with 4 blocks each sector
                                {
                                    cmd.LoadKeyNo((byte)0, keyA, MifareKeyType.KT_KEY_A);

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
                                return ERROR.NoError;
                            }
                            return ERROR.NoError;
                        }
                        return ERROR.NotReadyError;
                    }
                    return ERROR.NotReadyError;
                }
                return ERROR.NotReadyError;
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(e, FacilityName);
                return ERROR.AuthenticationError;
            }
        }

        public override ERROR WriteMiFareClassicSingleSector(int sectorNumber, string _aKey, string _bKey, byte[] buffer)
        {
            var keyA = new MifareKey() { Value = CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(_aKey) };
            var keyB = new MifareKey() { Value = CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(_bKey) };

            int blockCount = 0;

            try
            {
                if (readerUnit.ConnectToReader())
                {
                    if (readerUnit.WaitInsertion(Constants.MAX_WAIT_INSERTION))
                    {
                        if (readerUnit.Connect())
                        {
                            ReaderUnitName = readerUnit.ConnectedName;

                            card = readerUnit.GetSingleChip();

                            if (!string.IsNullOrWhiteSpace(card.ChipIdentifier))
                            {
                                try
                                {
                                    GenericChip = new GenericChipModel(card.ChipIdentifier, (CARD_TYPE)Enum.Parse(typeof(CARD_TYPE), card.Type));
                                }
                                catch (Exception e)
                                {
                                    LogWriter.CreateLogEntry(e, FacilityName);
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
                            return ERROR.NotReadyError;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(e, FacilityName);

                return ERROR.IOError;
            }
            return ERROR.NotReadyError;
        }

        public override ERROR WriteMiFareClassicSingleBlock(int _blockNumber, string _aKey, string _bKey, byte[] buffer)
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

                            card = readerUnit.GetSingleChip();

                            if (!string.IsNullOrWhiteSpace(card.ChipIdentifier))
                            {
                                try
                                {
                                    GenericChip = new GenericChipModel(card.ChipIdentifier, (CARD_TYPE)Enum.Parse(typeof(CARD_TYPE), card.Type));
                                }
                                catch (Exception e)
                                {
                                    LogWriter.CreateLogEntry(e, FacilityName);
                                }
                            }

                            var cmd = card.Commands as IMifareCommands;

                            try
                            {
                                cmd.LoadKeyNo((byte)0, keyA, MifareKeyType.KT_KEY_A); // FIXME: "sectorNumber" to 0

                                try
                                { //try to Auth with Keytype A
                                    cmd.AuthenticateKeyNo((byte)(_blockNumber), (byte)0, MifareKeyType.KT_KEY_A); // FIXME: same as '393

                                    cmd.WriteBinary((byte)(_blockNumber), buffer);

                                    return ERROR.NoError;
                                }
                                catch
                                { // Try Auth with keytype b

                                    cmd.LoadKeyNo((byte)0, keyB, MifareKeyType.KT_KEY_B);

                                    try
                                    {
                                        cmd.AuthenticateKeyNo((byte)(_blockNumber), (byte)0, MifareKeyType.KT_KEY_B); // FIXME: same as '393

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
                LogWriter.CreateLogEntry(e, FacilityName);

                return ERROR.IOError;
            }
            return ERROR.IOError;
        }

        public override ERROR ReadMiFareClassicSingleBlock(int _blockNumber, string _aKey, string _bKey)
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

                            card = readerUnit.GetSingleChip();

                            if (!string.IsNullOrWhiteSpace(card.ChipIdentifier))
                            {
                                try
                                {
                                    GenericChip = new GenericChipModel(card.ChipIdentifier, (CARD_TYPE)Enum.Parse(typeof(CARD_TYPE), card.Type));
                                }
                                catch (Exception e)
                                {
                                    LogWriter.CreateLogEntry(e, FacilityName);
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
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(e, FacilityName);

                return ERROR.IOError;
            }
            return ERROR.IOError;
        }

        public override ERROR WriteMiFareClassicWithMAD(int _madApplicationID, int _madStartSector,
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
                if (readerUnit.ConnectToReader())
                {
                    if (readerUnit.WaitInsertion(Constants.MAX_WAIT_INSERTION))
                    {
                        if (readerUnit.Connect())
                        {
                            ReaderUnitName = readerUnit.ConnectedName;

                            card = readerUnit.GetSingleChip();

                            if (!string.IsNullOrWhiteSpace(card.ChipIdentifier))
                            {
                                try
                                {
                                    GenericChip = new GenericChipModel(card.ChipIdentifier, (CARD_TYPE)Enum.Parse(typeof(CARD_TYPE), card.Type));
                                }
                                catch (Exception e)
                                {
                                    LogWriter.CreateLogEntry(e, FacilityName);
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
                            var cardService = card.GetService(CardServiceType.CST_STORAGE) as StorageCardService;

                            try
                            {
                                cardService.WriteData(mlocation, aiToUse, aiToWrite, buffer, buffer.Length, CardBehavior.CB_AUTOSWITCHAREA);
                            }
                            catch (Exception e)
                            {
                                LogWriter.CreateLogEntry(e, FacilityName);
                                return ERROR.AuthenticationError;
                            }
                            return ERROR.NoError;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(e, FacilityName);
                return ERROR.AuthenticationError;
            }
            return ERROR.NoError;
        }

        public override ERROR ReadMiFareClassicWithMAD(int madApplicationID, string _aKeyToUse, string _bKeyToUse, string _madAKeyToUse, string _madBKeyToUse, int _length, byte _madGPB, bool _useMADToAuth = true, bool _aiToUseIsMAD = false)
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
                if (readerUnit.ConnectToReader())
                {
                    if (readerUnit.WaitInsertion(Constants.MAX_WAIT_INSERTION))
                    {
                        if (readerUnit.Connect())
                        {
                            ReaderUnitName = readerUnit.ConnectedName;

                            card = readerUnit.GetSingleChip();

                            if (!string.IsNullOrWhiteSpace(card.ChipIdentifier))
                            {
                                try
                                {
                                    GenericChip = new GenericChipModel(card.ChipIdentifier, (CARD_TYPE)Enum.Parse(typeof(CARD_TYPE), card.Type));
                                }
                                catch (Exception e)
                                {
                                    LogWriter.CreateLogEntry(e, FacilityName);
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
                            var cardService = card.GetService(CardServiceType.CST_STORAGE) as StorageCardService;

                            try
                            {
                                MifareClassicData = (byte[])cardService.ReadData(mlocation, aiToUse, _length, CardBehavior.CB_AUTOSWITCHAREA);
                            }
                            catch (Exception e)
                            {
                                LogWriter.CreateLogEntry(e, FacilityName);
                                return ERROR.AuthenticationError;
                            }
                            return ERROR.NoError;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(e, FacilityName);
                return ERROR.AuthenticationError;
            }
            return ERROR.NoError;
        }

        #endregion mifare classic

        #region mifare ultralight

        public override ERROR ReadMifareUltralightSinglePage(int _pageNo)
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
                            RawFormatClass format = new RawFormatClass();

                            var chip = readerUnit.GetSingleChip() as IMifareUltralightChip;

                            var service = chip.GetService(CardServiceType.CST_STORAGE) as StorageCardService;

                            ILocation location = chip.CreateLocation() as ILocation;

                            if (chip.Type == "MifareUltralight")
                            {
                                var cmd = chip.Commands as MifareUltralightCommands;
                                MifareUltralightPageData = cmd.ReadPages(_pageNo, _pageNo) as byte[];
                            }

                            return ERROR.NoError;
                        }
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

        #endregion mifare ultralight

        #region mifare desfire

        public override ERROR GetMiFareDESFireChipAppIDs(string _appMasterKey, DESFireKeyType _keyTypeAppMasterKey)
        {
            try
            {
                // The excepted memory tree
                IDESFireLocation location = new DESFireLocation
                {
                    // File communication requires encryption
                    SecurityLevel = (LibLogicalAccess.EncryptionMode)EncryptionMode.CM_ENCRYPT
                };

                // Keys to use for authentication
                IDESFireAccessInfo aiToUse = new DESFireAccessInfo();
                aiToUse.MasterCardKey.Value = _appMasterKey;
                aiToUse.MasterCardKey.KeyType = (LibLogicalAccess.DESFireKeyType)_keyTypeAppMasterKey;


                if (readerUnit.ConnectToReader())
                {
                    if (readerUnit.WaitInsertion(Constants.MAX_WAIT_INSERTION))
                    {
                        if (readerUnit.Connect())
                        {
                            ReaderUnitName = readerUnit.ConnectedName;

                            card = readerUnit.GetSingleChip();

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

                                        if(DesfireChip == null)
                                        {
                                            DesfireChip = new MifareDesfireChipModel();
                                        }
                                        else
                                        {
                                            DesfireChip.AppList = new System.Collections.Generic.List<MifareDesfireAppModel>();

                                            foreach (uint appid in appIDsObject as UInt32[])
                                            {
                                                DesfireChip.AppList.Add(new MifareDesfireAppModel(appid));
                                            }
                                        }
                                        

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

                                            if (DesfireChip == null)
                                            {
                                                DesfireChip = new MifareDesfireChipModel();
                                            }
                                            else
                                            {
                                                DesfireChip.AppList = new System.Collections.Generic.List<MifareDesfireAppModel>();

                                                foreach (uint appid in appIDsObject as UInt32[])
                                                {
                                                    DesfireChip.AppList.Add(new MifareDesfireAppModel(appid));
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
                                                return ERROR.IOError;
                                        }
                                    }
                                }

                                if (card.Type == "DESFireEV1" ||
                                    card.Type == "DESFireEV2")
                                {

                                    var cmd = card.Commands as IDESFireEV1Commands;

                                    try
                                    {
                                        DesfireChip.FreeMemory = cmd.GetFreeMemory();

                                        object appIDsObject = cmd.GetApplicationIDs();

                                        if (DesfireChip == null)
                                        {
                                            DesfireChip = new MifareDesfireChipModel();
                                        }
                                        else
                                        {
                                            DesfireChip.AppList = new System.Collections.Generic.List<MifareDesfireAppModel>();

                                            foreach (uint appid in appIDsObject as UInt32[])
                                            {
                                                DesfireChip.AppList.Add(new MifareDesfireAppModel(appid));
                                            }
                                        }

                                        return ERROR.NoError;
                                    }
                                    catch
                                    {
                                        //Get AppIDs with Authentication (Directory Listing with PICC MK)
                                        try
                                        {
                                            DesfireChip.FreeMemory = cmd.GetFreeMemory();

                                            cmd.SelectApplication((uint)0);
                                            cmd.Authenticate((byte)0, aiToUse.MasterCardKey);

                                            object appIDsObject = cmd.GetApplicationIDs();

                                            if (DesfireChip == null)
                                            {
                                                DesfireChip = new MifareDesfireChipModel();
                                            }
                                            else
                                            {
                                                DesfireChip.AppList = new System.Collections.Generic.List<MifareDesfireAppModel>();

                                                foreach (uint appid in appIDsObject as UInt32[])
                                                {
                                                    DesfireChip.AppList.Add(new MifareDesfireAppModel(appid));
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
                                                return ERROR.IOError;
                                        }
                                    }
                                }
                                else
                                    return ERROR.NotReadyError;
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

                                        if (DesfireChip == null)
                                        {
                                            DesfireChip = new MifareDesfireChipModel();
                                        }
                                        else
                                        {
                                            DesfireChip.AppList = new System.Collections.Generic.List<MifareDesfireAppModel>();

                                            foreach (uint appid in appIDsObject as UInt32[])
                                            {
                                                DesfireChip.AppList.Add(new MifareDesfireAppModel(appid));
                                            }
                                        }

                                        return ERROR.NoError;
                                    }

                                    if (card.Type == "DESFireEV1" ||
                                        card.Type == "DESFireEV2")
                                    {
                                        var cmd = card.Commands as IDESFireEV1Commands;

                                    }
                                    else
                                        return ERROR.NotReadyError;
                                }

                                catch
                                {

                                }
                            }
                        }
                    }
                }
                return ERROR.NotReadyError;
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
                    return ERROR.IOError;
            }
        }

        public override ERROR CreateMifareDesfireFile(string _appMasterKey, DESFireKeyType _keyTypeAppMasterKey, FileType_MifareDesfireFileType _fileType, DESFireAccessRights _accessRights, EncryptionMode _encMode,
                                             int _appID, int _fileNo, int _fileSize,
                                             int _minValue = 0, int _maxValue = 1000, int _initValue = 0, bool _isValueLimited = false,
                                             int _maxNbOfRecords = 100)
        {
            try
            {
                DESFireAccessRights accessRights = _accessRights;

                // Keys to use for authentication
                IDESFireAccessInfo aiToUse = new DESFireAccessInfo();
                CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_appMasterKey);
                aiToUse.MasterCardKey.Value = CustomConverter.DesfireKeyToCheck;
                aiToUse.MasterCardKey.KeyType = ((LibLogicalAccess.DESFireKeyType)_keyTypeAppMasterKey);

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
                                                cmd.CreateStdDataFile((byte)_fileNo, (LibLogicalAccess.EncryptionMode)_encMode,
                                                    new LibLogicalAccess.DESFireAccessRights()
                                                    {
                                                        changeAccess = (LibLogicalAccess.TaskAccessRights)accessRights.changeAccess,
                                                        readAccess = (LibLogicalAccess.TaskAccessRights)accessRights.readAccess,
                                                        writeAccess = (LibLogicalAccess.TaskAccessRights)accessRights.writeAccess,
                                                        readAndWriteAccess = (LibLogicalAccess.TaskAccessRights)accessRights.readAndWriteAccess
                                                    }, (uint)_fileSize);
                                                break;

                                            case FileType_MifareDesfireFileType.BackupFile:
                                                cmd.CreateBackupFile((byte)_fileNo, (LibLogicalAccess.EncryptionMode)_encMode,
                                                    new LibLogicalAccess.DESFireAccessRights()
                                                    {
                                                        changeAccess = (LibLogicalAccess.TaskAccessRights)accessRights.changeAccess,
                                                        readAccess = (LibLogicalAccess.TaskAccessRights)accessRights.readAccess,
                                                        writeAccess = (LibLogicalAccess.TaskAccessRights)accessRights.writeAccess,
                                                        readAndWriteAccess = (LibLogicalAccess.TaskAccessRights)accessRights.readAndWriteAccess
                                                    }, (uint)_fileSize);
                                                break;

                                            case FileType_MifareDesfireFileType.ValueFile:
                                                cmd.CreateValueFile((byte)_fileNo, (LibLogicalAccess.EncryptionMode)_encMode,
                                                    new LibLogicalAccess.DESFireAccessRights()
                                                    {
                                                        changeAccess = (LibLogicalAccess.TaskAccessRights)accessRights.changeAccess,
                                                        readAccess = (LibLogicalAccess.TaskAccessRights)accessRights.readAccess,
                                                        writeAccess = (LibLogicalAccess.TaskAccessRights)accessRights.writeAccess,
                                                        readAndWriteAccess = (LibLogicalAccess.TaskAccessRights)accessRights.readAndWriteAccess
                                                    }, (uint)_minValue, (uint)_maxValue, (uint)_initValue, _isValueLimited);
                                                break;

                                            case FileType_MifareDesfireFileType.CyclicRecordFile:
                                                cmd.CreateCyclicRecordFile((byte)_fileNo, (LibLogicalAccess.EncryptionMode)_encMode,
                                                    new LibLogicalAccess.DESFireAccessRights()
                                                    {
                                                        changeAccess = (LibLogicalAccess.TaskAccessRights)accessRights.changeAccess,
                                                        readAccess = (LibLogicalAccess.TaskAccessRights)accessRights.readAccess,
                                                        writeAccess = (LibLogicalAccess.TaskAccessRights)accessRights.writeAccess,
                                                        readAndWriteAccess = (LibLogicalAccess.TaskAccessRights)accessRights.readAndWriteAccess
                                                    }, (uint)_fileSize, (uint)_maxNbOfRecords);
                                                break;

                                            case FileType_MifareDesfireFileType.LinearRecordFile:
                                                cmd.CreateLinearRecordFile((byte)_fileNo, (LibLogicalAccess.EncryptionMode)_encMode,
                                                    new LibLogicalAccess.DESFireAccessRights()
                                                    {
                                                        changeAccess = (LibLogicalAccess.TaskAccessRights)accessRights.changeAccess,
                                                        readAccess = (LibLogicalAccess.TaskAccessRights)accessRights.readAccess,
                                                        writeAccess = (LibLogicalAccess.TaskAccessRights)accessRights.writeAccess,
                                                        readAndWriteAccess = (LibLogicalAccess.TaskAccessRights)accessRights.readAndWriteAccess
                                                    }, (uint)_fileSize, (uint)_maxNbOfRecords);
                                                break;

                                            default:
                                                break;
                                        }

                                        return ERROR.NoError;
                                    }

                                    switch (_fileType)
                                    {
                                        case FileType_MifareDesfireFileType.StdDataFile:
                                            cmd.CreateStdDataFile((byte)_fileNo, (LibLogicalAccess.EncryptionMode)_encMode,
                                                    new LibLogicalAccess.DESFireAccessRights()
                                                    {
                                                        changeAccess = (LibLogicalAccess.TaskAccessRights)accessRights.changeAccess,
                                                        readAccess = (LibLogicalAccess.TaskAccessRights)accessRights.readAccess,
                                                        writeAccess = (LibLogicalAccess.TaskAccessRights)accessRights.writeAccess,
                                                        readAndWriteAccess = (LibLogicalAccess.TaskAccessRights)accessRights.readAndWriteAccess
                                                    }, (uint)_fileSize);
                                            break;

                                        case FileType_MifareDesfireFileType.BackupFile:
                                            cmd.CreateBackupFile((byte)_fileNo, (LibLogicalAccess.EncryptionMode)_encMode,
                                                    new LibLogicalAccess.DESFireAccessRights()
                                                    {
                                                        changeAccess = (LibLogicalAccess.TaskAccessRights)accessRights.changeAccess,
                                                        readAccess = (LibLogicalAccess.TaskAccessRights)accessRights.readAccess,
                                                        writeAccess = (LibLogicalAccess.TaskAccessRights)accessRights.writeAccess,
                                                        readAndWriteAccess = (LibLogicalAccess.TaskAccessRights)accessRights.readAndWriteAccess
                                                    }, (uint)_fileSize);
                                            break;

                                        case FileType_MifareDesfireFileType.ValueFile:
                                            cmd.CreateValueFile((byte)_fileNo, (LibLogicalAccess.EncryptionMode)_encMode,
                                                    new LibLogicalAccess.DESFireAccessRights()
                                                    {
                                                        changeAccess = (LibLogicalAccess.TaskAccessRights)accessRights.changeAccess,
                                                        readAccess = (LibLogicalAccess.TaskAccessRights)accessRights.readAccess,
                                                        writeAccess = (LibLogicalAccess.TaskAccessRights)accessRights.writeAccess,
                                                        readAndWriteAccess = (LibLogicalAccess.TaskAccessRights)accessRights.readAndWriteAccess
                                                    }, (uint)_minValue, (uint)_maxValue, (uint)_initValue, _isValueLimited);
                                            break;

                                        case FileType_MifareDesfireFileType.CyclicRecordFile:
                                            cmd.CreateCyclicRecordFile((byte)_fileNo, (LibLogicalAccess.EncryptionMode)_encMode,
                                                    new LibLogicalAccess.DESFireAccessRights()
                                                    {
                                                        changeAccess = (LibLogicalAccess.TaskAccessRights)accessRights.changeAccess,
                                                        readAccess = (LibLogicalAccess.TaskAccessRights)accessRights.readAccess,
                                                        writeAccess = (LibLogicalAccess.TaskAccessRights)accessRights.writeAccess,
                                                        readAndWriteAccess = (LibLogicalAccess.TaskAccessRights)accessRights.readAndWriteAccess
                                                    }, (uint)_fileSize, (uint)_maxNbOfRecords);
                                            break;

                                        case FileType_MifareDesfireFileType.LinearRecordFile:
                                            cmd.CreateLinearRecordFile((byte)_fileNo, (LibLogicalAccess.EncryptionMode)_encMode,
                                                    new LibLogicalAccess.DESFireAccessRights()
                                                    {
                                                        changeAccess = (LibLogicalAccess.TaskAccessRights)accessRights.changeAccess,
                                                        readAccess = (LibLogicalAccess.TaskAccessRights)accessRights.readAccess,
                                                        writeAccess = (LibLogicalAccess.TaskAccessRights)accessRights.writeAccess,
                                                        readAndWriteAccess = (LibLogicalAccess.TaskAccessRights)accessRights.readAndWriteAccess
                                                    }, (uint)_fileSize, (uint)_maxNbOfRecords);
                                            break;

                                        default:
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
                                        return ERROR.IOError;
                                }
                            }
                            else
                                return ERROR.NotReadyError;
                        }
                    }
                }
                return ERROR.NotReadyError;
            }
            catch
            {
                return ERROR.IOError;
            }
        }

        public override ERROR ReadMiFareDESFireChipFile(string _appMasterKey, DESFireKeyType _keyTypeAppMasterKey,
                                               string _appReadKey, DESFireKeyType _keyTypeAppReadKey, int _readKeyNo,
                                               string _appWriteKey, DESFireKeyType _keyTypeAppWriteKey, int _writeKeyNo,
                                               EncryptionMode _encMode,
                                               int _fileNo, int _appID, int _fileSize)
        {
            try
            {
                // The excepted memory tree
                IDESFireLocation location = new DESFireLocation
                {
                    // The Application ID to use
                    aid = _appID,
                    // File 0 into this application
                    File = _fileNo,
                    // File communication requires encryption
                    SecurityLevel = (LibLogicalAccess.EncryptionMode)_encMode
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

                if (readerUnit.ConnectToReader())
                {
                    if (readerUnit.WaitInsertion(Constants.MAX_WAIT_INSERTION))
                    {
                        if (readerUnit.Connect())
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

                                        desfireFileSetting = cmd.GetFileSettings((byte)_fileNo);
                                        DesfireFileSettings = new DESFireFileSettings
                                        {
                                            accessRights = desfireFileSetting.accessRights,
                                            comSett = desfireFileSetting.comSett,
                                            FileType = desfireFileSetting.FileType,
                                            dataFile = new DataFileSetting { fileSize = desfireFileSetting.dataFile.fileSize }
                                        };

                                        MifareDESFireData = (byte[])cmd.ReadData((byte)_fileNo, 0, desfireFileSetting.dataFile.fileSize, (LibLogicalAccess.EncryptionMode)EncryptionMode.CM_ENCRYPT);
                                    }
                                    catch
                                    {
                                        cmd.SelectApplication((uint)_appID);

                                        cmd.Authenticate((byte)_readKeyNo, aiToWrite.ReadKey);

                                        MifareDESFireData = (byte[])cmd.ReadData((byte)_fileNo, 0, (uint)_fileSize, (LibLogicalAccess.EncryptionMode)EncryptionMode.CM_ENCRYPT);
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
                                        return ERROR.IOError;
                                }
                            }
                            else
                                return ERROR.NotReadyError;
                        }
                    }
                }
                return ERROR.NotReadyError;
            }

            catch
            {
                return ERROR.IOError;
            }
        }

        public override ERROR WriteMiFareDESFireChipFile(string _cardMasterKey, DESFireKeyType _keyTypeCardMasterKey,
                                                string _appMasterKey, DESFireKeyType _keyTypeAppMasterKey,
                                                string _appReadKey, DESFireKeyType _keyTypeAppReadKey, int _readKeyNo,
                                                string _appWriteKey, DESFireKeyType _keyTypeAppWriteKey, int _writeKeyNo,
                                                EncryptionMode _encMode,
                                                int _fileNo, int _appID, byte[] _data)
        {

            try
            {
                // The excepted memory tree
                IDESFireLocation location = new DESFireLocation
                {
                    // The Application ID to use
                    aid = _appID,
                    // File 0 into this application
                    File = _fileNo,
                    // File communication requires encryption
                    SecurityLevel = (LibLogicalAccess.EncryptionMode)_encMode
                };

                // Keys to use for authentication

                // Get the card storage service
                IStorageCardService storage = (IStorageCardService)card.GetService(CardServiceType.CST_STORAGE);

                // Change keys with the following ones
                IDESFireAccessInfo aiToUse = new DESFireAccessInfo();
                CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_cardMasterKey);
                aiToUse.MasterCardKey.Value = CustomConverter.DesfireKeyToCheck;
                aiToUse.MasterCardKey.KeyType = (LibLogicalAccess.DESFireKeyType)_keyTypeAppMasterKey;

                IDESFireAccessInfo aiToWrite = new DESFireAccessInfo();
                aiToWrite.MasterCardKey.Value = CustomConverter.DesfireKeyToCheck;
                aiToWrite.MasterCardKey.KeyType = (LibLogicalAccess.DESFireKeyType)_keyTypeAppMasterKey;

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


                if (readerUnit.ConnectToReader())
                {
                    if (readerUnit.WaitInsertion(Constants.MAX_WAIT_INSERTION))
                    {
                        if (readerUnit.Connect())
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

                                    cmd.WriteData((byte)_fileNo, 0, (uint)_data.Length, (LibLogicalAccess.EncryptionMode)EncryptionMode.CM_ENCRYPT, _data);

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
                                        return ERROR.IOError;
                                }
                            }
                            else
                                return ERROR.NotReadyError;
                        }
                    }
                }
                return ERROR.NotReadyError;
            }

            catch
            {
                return ERROR.IOError;
            }
        }

        public override ERROR AuthToMifareDesfireApplication(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumber, int _appID = 0)
        {
            try
            {
                // The excepted memory tree
                IDESFireLocation location = new DESFireLocation
                {
                    // The Application ID to use
                    aid = _appID,
                    // File communication requires encryption
                    SecurityLevel = (LibLogicalAccess.EncryptionMode)EncryptionMode.CM_ENCRYPT
                };

                // Keys to use for authentication
                IDESFireAccessInfo aiToUse = new DESFireAccessInfo();
                CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_applicationMasterKey);
                aiToUse.MasterCardKey.Value = CustomConverter.DesfireKeyToCheck;
                aiToUse.MasterCardKey.KeyType = ((LibLogicalAccess.DESFireKeyType)_keyType);

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
                                    if (_appID > 0)
                                    {
                                        cmd.Authenticate((byte)_keyNumber, aiToUse.MasterCardKey);
                                    }
                                    else
                                    {
                                        cmd.Authenticate((byte)0, aiToUse.MasterCardKey);
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
                                        return ERROR.IOError;
                                }
                            }

                            else
                                return ERROR.NotReadyError;
                        }
                    }
                }
                return ERROR.NotReadyError;
            }
            catch
            {
                return ERROR.IOError;
            }
        }

        public override ERROR GetMifareDesfireAppSettings(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumberCurrent = 0, int _appID = 0)
        {
            byte maxNbrOfKeys;
            LibLogicalAccess.DESFireKeySettings keySettings;
            LibLogicalAccess.DESFireKeyType keyType;

            try
            {
                // Keys to use for authentication
                IDESFireAccessInfo aiToUse = new DESFireAccessInfo();
                CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_applicationMasterKey);
                aiToUse.MasterCardKey.Value = CustomConverter.DesfireKeyToCheck;
                aiToUse.MasterCardKey.KeyType = ((LibLogicalAccess.DESFireKeyType)_keyType);

                if (readerUnit.ConnectToReader())
                {
                    if (readerUnit.WaitInsertion(Constants.MAX_WAIT_INSERTION))
                    {
                        if (readerUnit.Connect())
                        {
                            ReaderUnitName = readerUnit.ConnectedName;
                            card = readerUnit.GetSingleChip();

                            if (card.Type == "DESFire")
                            {
                                var cmd = card.Commands as IDESFireCommands;
                                ReaderUnitName = readerUnit.ConnectedName;

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
                                            EncryptionType = (DESFireKeyType)(maxNbrOfKeys & 0xF0);
                                            DesfireAppKeySetting = (DESFireKeySettings)keySettings;

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
                                                return ERROR.IOError;
                                        }
                                    }
                                    cmd.GetKeySettings(out keySettings, out maxNbrOfKeys);
                                    MaxNumberOfAppKeys = (byte)(maxNbrOfKeys & 0x0F);
                                    EncryptionType = (DESFireKeyType)(maxNbrOfKeys & 0xF0);
                                    DesfireAppKeySetting = (DESFireKeySettings)keySettings;

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
                                        return ERROR.IOError;
                                }
                            }

                            else if (card.Type == "DESFireEV1" ||
                                    card.Type == "DESFireEV2" || card.Type == "GENERIC_T_CL_A")
                            {
                                var cmd = card.Commands as IDESFireEV1Commands;

                                ReaderUnitName = readerUnit.ConnectedName;

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
                                                DesfireChip.FreeMemory = cmd.GetFreeMemory();
                                            }

                                            catch { }

                                            cmd.GetKeySettingsEV1(out keySettings, out maxNbrOfKeys, out keyType);
                                            MaxNumberOfAppKeys = (byte)(maxNbrOfKeys & 0x0F);
                                            EncryptionType = (DESFireKeyType)keyType;
                                            DesfireAppKeySetting = (DESFireKeySettings)keySettings;

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
                                                return ERROR.IOError;
                                        }
                                    }

                                    try
                                    {
                                        DesfireChip.FreeMemory = cmd.GetFreeMemory();
                                    }

                                    catch { }

                                    cmd.GetKeySettingsEV1(out keySettings, out maxNbrOfKeys, out keyType);
                                    MaxNumberOfAppKeys = (byte)(maxNbrOfKeys & 0x0F);
                                    EncryptionType = (DESFireKeyType)keyType;
                                    DesfireAppKeySetting = (DESFireKeySettings)keySettings;

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
                                        return ERROR.IOError;
                                }
                            }

                            else
                                return ERROR.NotReadyError;
                        }
                    }
                }
                return ERROR.NotReadyError;
            }
            catch
            {
                return ERROR.IOError;
            }
        }

        public override ERROR CreateMifareDesfireApplication(
            string _piccMasterKey, DESFireKeySettings _keySettingsTarget, DESFireKeyType _keyTypePiccMasterKey,
            DESFireKeyType _keyTypeTargetApplication, int _maxNbKeys, int _appID, bool authenticateToPICCFirst = true)
        {
            try
            {
                // The excepted memory tree
                IDESFireLocation location = new DESFireLocation
                {
                    // The Application ID to use
                    aid = _appID,
                    // File communication requires encryption
                    SecurityLevel = (LibLogicalAccess.EncryptionMode)EncryptionMode.CM_ENCRYPT
                };

                // Keys to use for authentication
                IDESFireAccessInfo aiToUse = new DESFireAccessInfo();
                CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_piccMasterKey);
                aiToUse.MasterCardKey.Value = CustomConverter.DesfireKeyToCheck;
                aiToUse.MasterCardKey.KeyType = (LibLogicalAccess.DESFireKeyType)_keyTypePiccMasterKey;

                if (readerUnit.ConnectToReader())
                {
                    if (readerUnit.WaitInsertion(Constants.MAX_WAIT_INSERTION))
                    {
                        if (readerUnit.Connect())
                        {
                            ReaderUnitName = readerUnit.ConnectedName;

                            card = readerUnit.GetSingleChip();

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

                                    cmd.CreateApplication((uint)_appID, (LibLogicalAccess.DESFireKeySettings)_keySettingsTarget, (byte)_maxNbKeys);

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
                                        return ERROR.IOError;
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
                                        cmd.Authenticate((byte)0, aiToUse.MasterCardKey);

                                    cmd.CreateApplicationEV1((uint)_appID, (LibLogicalAccess.DESFireKeySettings)_keySettingsTarget, (byte)_maxNbKeys, false, (LibLogicalAccess.DESFireKeyType)_keyTypeTargetApplication, 0, 0);

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
                                        return ERROR.IOError;
                                }
                            }
                            else
                                return ERROR.NotReadyError;
                        }
                    }
                }
                return ERROR.NotReadyError;
            }
            catch
            {
                return ERROR.IOError;
            }
        }

        public override ERROR ChangeMifareDesfireApplicationKey(
            string _applicationMasterKeyCurrent, int _keyNumberCurrent, DESFireKeyType _keyTypeCurrent,
            string _applicationMasterKeyTarget, int _keyNumberTarget, int selectedDesfireAppKeyVersionTargetAsIntint,
            DESFireKeyType _keyTypeTarget, int _appIDCurrent, int _appIDTarget, DESFireKeySettings keySettings)
        {
            try
            {
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
                applicationMasterKeyTarget.KeyVersion = applicationMasterKeyTarget.KeyVersion = +1;

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
                                    if (_appIDCurrent == 0)
                                    {
                                        try
                                        {
                                            applicationMasterKeyTarget.KeyType = (LibLogicalAccess.DESFireKeyType)_keyTypeTarget;

                                            cmd.SelectApplication((uint)0);
                                            cmd.Authenticate((byte)0, masterApplicationKey);
                                            cmd.ChangeKeySettings((LibLogicalAccess.DESFireKeySettings)keySettings);
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

                                        applicationMasterKeyTarget.KeyType = ((LibLogicalAccess.DESFireKeyType)_keyTypeCurrent);

                                        cmd.SelectApplication((uint)_appIDCurrent);

                                        try
                                        {
                                            cmd.Authenticate((byte)_keyNumberCurrent, masterApplicationKey);
                                            cmd.ChangeKey((byte)_keyNumberTarget, applicationMasterKeyTarget);
                                            cmd.Authenticate((byte)_keyNumberCurrent, applicationMasterKeyTarget);

                                            try
                                            {
                                                cmd.ChangeKeySettings((LibLogicalAccess.DESFireKeySettings)keySettings);
                                            }
                                            catch { }
                                        }

                                        catch (Exception ex)
                                        {
                                            try
                                            {
                                                cmd.Authenticate((byte)_keyNumberCurrent, masterApplicationKey);
                                                cmd.ChangeKeySettings((LibLogicalAccess.DESFireKeySettings)keySettings);
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
                                                        return ERROR.IOError;
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
                                        return ERROR.IOError;
                                }
                            }
                            else
                                return ERROR.NotReadyError;
                        }
                    }
                }
                return ERROR.NotReadyError;
            }
            catch
            {
                return ERROR.IOError;
            }
        }

        public override ERROR DeleteMifareDesfireApplication(string _applicationMasterKey, DESFireKeyType _keyType, int _appID = 0)
        {
            try
            {
                // The excepted memory tree
                IDESFireLocation location = new DESFireLocation
                {
                    // The Application ID to use
                    aid = _appID,
                    // File communication requires encryption
                    SecurityLevel = (LibLogicalAccess.EncryptionMode)EncryptionMode.CM_ENCRYPT
                };

                // Keys to use for authentication
                IDESFireAccessInfo aiToUse = new DESFireAccessInfo();
                CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_applicationMasterKey);
                aiToUse.MasterCardKey.Value = CustomConverter.DesfireKeyToCheck;
                aiToUse.MasterCardKey.KeyType = (LibLogicalAccess.DESFireKeyType)_keyType;

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
                                            return ERROR.IOError;
                                    }
                                }
                            }
                            return ERROR.NotReadyError;
                        }
                    }
                }
                return ERROR.NotReadyError;
            }
            catch
            {
                return ERROR.IOError;
            }
        }

        public override ERROR DeleteMifareDesfireFile(string _applicationMasterKey, DESFireKeyType _keyType, int _appID = 0, int _fileID = 0)
        {
            try
            {
                // The excepted memory tree
                IDESFireLocation location = new DESFireLocation
                {
                    // The Application ID to use
                    aid = _appID,
                    // File communication requires encryption
                    SecurityLevel = (LibLogicalAccess.EncryptionMode)EncryptionMode.CM_ENCRYPT
                };

                // Keys to use for authentication
                IDESFireAccessInfo aiToUse = new DESFireAccessInfo();
                CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_applicationMasterKey);
                aiToUse.MasterCardKey.Value = CustomConverter.DesfireKeyToCheck;
                aiToUse.MasterCardKey.KeyType = ((LibLogicalAccess.DESFireKeyType)_keyType);

                if (readerUnit.ConnectToReader())
                {
                    if (readerUnit.WaitInsertion(Constants.MAX_WAIT_INSERTION))
                    {
                        if (readerUnit.Connect())
                        {
                            ReaderUnitName = readerUnit.ConnectedName;

                            card = readerUnit.GetSingleChip();

                            if (card.Type == "DESFireEV1" ||
                                card.Type == "DESFireEV2" ||
                                card.Type == "DESFire")
                            {
                                try
                                {
                                    var cmd = card.Commands as IDESFireCommands;

                                    try
                                    {
                                        cmd.SelectApplication((uint)0);
                                        cmd.Authenticate((byte)0, aiToUse.MasterCardKey);
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
                                        return ERROR.IOError;
                                }
                            }
                            else
                                return ERROR.NotReadyError;
                        }
                    }
                }
                return ERROR.NotReadyError;
            }
            catch
            {
                return ERROR.IOError;
            }
        }

        public override ERROR FormatDesfireCard(string _applicationMasterKey, DESFireKeyType _keyType, int _appID = 0)
        {
            try
            {
                // The excepted memory tree
                IDESFireLocation location = new DESFireLocation
                {
                    // The Application ID to use
                    aid = _appID,
                    // File communication requires encryption
                    SecurityLevel = (LibLogicalAccess.EncryptionMode)EncryptionMode.CM_ENCRYPT
                };

                // Keys to use for authentication
                IDESFireAccessInfo aiToUse = new DESFireAccessInfo();
                CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_applicationMasterKey);
                aiToUse.MasterCardKey.Value = CustomConverter.DesfireKeyToCheck;
                aiToUse.MasterCardKey.KeyType = ((LibLogicalAccess.DESFireKeyType)_keyType);

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
                                        return ERROR.IOError;
                                }
                            }
                            else
                                return ERROR.NotReadyError;
                        }
                    }
                }
                return ERROR.NotReadyError;
            }
            catch
            {
                return ERROR.IOError;
            }
        }

        public override ERROR GetMifareDesfireFileList(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumberCurrent = 0, int _appID = 0)
        {
            try
            {
                IDESFireLocation location = new DESFireLocation
                {
                    // The Application ID to use
                    aid = _appID,
                    // File communication requires encryption
                    SecurityLevel = (LibLogicalAccess.EncryptionMode)EncryptionMode.CM_ENCRYPT
                };

                // Keys to use for authentication
                IDESFireAccessInfo aiToUse = new DESFireAccessInfo();
                CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_applicationMasterKey);
                aiToUse.MasterCardKey.Value = CustomConverter.DesfireKeyToCheck;
                aiToUse.MasterCardKey.KeyType = ((LibLogicalAccess.DESFireKeyType)_keyType);

                object fileIDsObject;

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
                                                return ERROR.IOError;
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
                                        return ERROR.IOError;
                                }
                            }
                            else
                                return ERROR.NotReadyError;
                        }
                    }
                }
                return ERROR.NotReadyError;
            }
            catch
            {
                return ERROR.IOError;
            }
        }

        public override ERROR GetMifareDesfireFileSettings(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumberCurrent = 0, int _appID = 0, int _fileNo = 0)
        {
            try
            {
                // Keys to use for authentication
                IDESFireAccessInfo aiToUse = new DESFireAccessInfo();
                CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_applicationMasterKey);
                aiToUse.MasterCardKey.Value = CustomConverter.DesfireKeyToCheck;
                aiToUse.MasterCardKey.KeyType = (LibLogicalAccess.DESFireKeyType)_keyType;

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
                                            desfireFileSetting = cmd.GetFileSettings((byte)_fileNo);
                                            DesfireFileSettings = new DESFireFileSettings 
                                            { 
                                                accessRights = desfireFileSetting.accessRights, 
                                                comSett = desfireFileSetting.comSett,
                                                FileType = desfireFileSetting.FileType,
                                                dataFile = new DataFileSetting { fileSize = desfireFileSetting.dataFile.fileSize }
                                            };

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
                                                return ERROR.IOError;
                                        }
                                    }

                                    desfireFileSetting = cmd.GetFileSettings((byte)_fileNo);
                                    DesfireFileSettings = new DESFireFileSettings
                                    {
                                        accessRights = desfireFileSetting.accessRights,
                                        comSett = desfireFileSetting.comSett,
                                        FileType = desfireFileSetting.FileType,
                                        dataFile = new DataFileSetting { fileSize = desfireFileSetting.dataFile.fileSize }
                                    };

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
                                        return ERROR.IOError;
                                }
                            }
                            else
                                return ERROR.NotReadyError;
                        }
                    }
                }
                return ERROR.NotReadyError;
            }
            catch
            {
                return ERROR.IOError;
            }
        }

        #endregion mifare desfire

        #region mifare plus

        #endregion

        #region ISO15693

        public ERROR ReadISO15693Chip()
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

                            card = readerUnit.GetSingleChip();


                            if (card.Type == "ISO15693")
                            {
                                var cmd = card.Commands as ISO15693Commands;

                                object t = cmd.GetSystemInformation();

                            }

                            return ERROR.NoError;
                        }
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

        #endregion

        #region EM4135

        public ERROR ReadEM4135ChipPublic()
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

                            card = readerUnit.GetSingleChip();


                            if (true)
                            {
                                var cmd = (card as EM4135Chip).ChipIdentifier;

                            }

                            return ERROR.NoError;
                        }
                    }
                }
                return ERROR.NoError;
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(e, FacilityName);
                return ERROR.IOError;
            }
        }

        #endregion

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                }

                _disposed = true;
            }
        }

        public override void Dispose()
        {
            _disposed = false;
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}