using LibLogicalAccess;
using LibLogicalAccess.Card;
using LibLogicalAccess.Reader;
using LibLogicalAccess.Crypto;

using ByteArrayHelper.Extensions;
using RFiDGear.Models;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Reflection;
using System.Linq;
using System.Globalization;
using RFiDGear.Infrastructure.Tasks;
using RFiDGear.Infrastructure.AccessControl;
using RFiDGear.Infrastructure.FileAccess;

namespace RFiDGear.Infrastructure.ReaderProviders
{
    /// <summary>
    /// Description of RFiDAccess.
    ///
    /// Initialize Reader
    /// </summary>
    ///
    public class LibLogicalAccessProvider : ReaderDevice, IDisposable
    {
        // global (cross-class) Instances go here ->
        private readonly EventLog eventLog = new EventLog("Application", ".", Assembly.GetEntryAssembly().GetName().Name);
        private readonly ProjectManager projectManager = new ProjectManager();
        private ReaderProvider readerProvider;
        private ReaderUnit readerUnit;
        private string selectedReaderName;
        private Chip card;
        public IReadOnlyCollection<string> AvailableReaders { get; private set; } = new List<string>();


        #region contructor
        public LibLogicalAccessProvider()
        {
        }

        public LibLogicalAccessProvider(ReaderTypes readerType, string readerName = null)
        {
            try
            {
                selectedReaderName = readerName;

                InitializeReaderProvider(readerType);

                GenericChip = new GenericChipModel("", CARD_TYPE.Unspecified);
            }
            catch (Exception e)
            {
                eventLog.WriteEntry(e.Message, EventLogEntryType.Error);
            }
        }

        #endregion contructor

        #region common

        /// <summary>
        /// 
        /// </summary>
        public override bool IsConnected => readerUnit?.isConnected() == true;

        private void InitializeReaderProvider(ReaderTypes readerType)
        {
            readerProvider = LibraryManager.getInstance().getReaderProvider(Enum.GetName(typeof(ReaderTypes), readerType));

            var readers = readerProvider.getReaderList();

            AvailableReaders = readers.Select(x => x.getName()).ToList();

            if (readerUnit == null)
            {
                readerUnit = readers.Where(r => r.getName() == selectedReaderName).FirstOrDefault() ?? readerProvider.createReaderUnit();
            }
            else
            {
                readerUnit = readers.Where(r => r.getName() == readerUnit.getName()).FirstOrDefault() ?? readerUnit;
            }

            ReaderUnitName = readerUnit?.getName();
        }

        public void UpdateSelectedReader(string readerName)
        {
            try
            {
                selectedReaderName = readerName;
                InitializeReaderProvider(ReaderTypes.PCSC);
            }
            catch (Exception e)
            {
                eventLog.WriteEntry(e.Message, EventLogEntryType.Error);
            }
        }

        public static IReadOnlyCollection<string> GetAvailableReaderNames(ReaderTypes readerType)
        {
            try
            {
                var provider = LibraryManager.getInstance().getReaderProvider(Enum.GetName(typeof(ReaderTypes), readerType));

                return provider.getReaderList().Select(r => r.getName()).ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        /// <summary>
        /// Connects to the configured reader unit when not already connected.
        /// </summary>
        /// <returns>The normalized error code representing the connection outcome.</returns>
        public override Task<ERROR> ConnectAsync()
        {
            if (readerUnit?.isConnected() == true)
            {
                return Task.FromResult(ERROR.NoError);
            }

            if (readerUnit?.connectToReader() == true)
            {
                return Task.FromResult(ERROR.NoError);
            }

            return Task.FromResult(ERROR.TransportError);
        }

        public override async Task<ERROR> ReadChipPublic()
        {
            try
            {
                if (await tryInitReader())
                {
                    card = readerUnit.getSingleChip();

                    if (!string.IsNullOrWhiteSpace(ByteArrayConverter.GetStringFrom(card.getChipIdentifier().ToArray())))
                    {
                        try
                        {
                            var uidAsString = ByteArrayConverter.GetStringFrom(card.getChipIdentifier().ToArray());
                            var t = card.getCardType();
                            var chipType = (CARD_TYPE)Enum.Parse(typeof(CARD_TYPE), card.getCardType());

                            if ((chipType & CARD_TYPE.DESFire) == CARD_TYPE.DESFire)
                            {
                                DESFireCommands cmd = card.getCommands() as DESFireCommands;

                                DESFireCommands.DESFireCardVersion version = cmd.getVersion();

                                GenericChip = new MifareDesfireChipModel(
                                    uidAsString,
                                    chipType,
                                    "unsupported",
                                    "unsupported",
                                    string.Format("{0}{1}{2}{3}{4}\n{5}\n{6}\n{7}",
                                    version.hardwareVendor.ToString("X2"),
                                    version.hardwareType.ToString("X2"),
                                    version.hardwareSubType.ToString("X2"),
                                    version.softwareMjVersion.ToString("X2"),
                                    version.softwareMnVersion.ToString("X2"),
                                    string.Format("Size: 0x{0}", version.hardwareStorageSize.ToString("X2")),
                                    string.Format("Prod Week: d:{0}", version.cwProd.ToString("X2")),
                                    string.Format("Prod Year: d:{0}", version.yearProd.ToString("D2"))
                                    ));
                            }

                            else if ((chipType & CARD_TYPE.MifareClassic) == CARD_TYPE.MifareClassic)
                            {
                                MifareCommands cmd = card.getCommands() as MifareCommands;

                                GenericChip = new MifareClassicChipModel(
                                    uidAsString,
                                    chipType
                                    );
                            }

                            return ERROR.NoError;
                        }
                        catch (Exception e)
                        {
                            eventLog.WriteEntry(e.Message, EventLogEntryType.Error);

                            return ERROR.TransportError;
                        }
                    }
                    else
                        return ERROR.TransportError;
                }
            }
            catch (Exception e)
            {
                if (readerProvider != null)
                    readerProvider.release();

                eventLog.WriteEntry(e.Message, EventLogEntryType.Error);

                return ERROR.TransportError;
            }

            return ERROR.TransportError;
        }

        #endregion common

        #region mifare classic

        /// <inheritdoc />
        public override async Task<ERROR> ReadMifareClassicSingleSector(int sectorNumber, string aKey, string bKey)
        {
            try
            {
                await projectManager.LoadSettingsAsync();
                Sector = new MifareClassicSectorModel();

                var keyA = new MifareKey(CustomConverter.KeyFormatQuickCheck(aKey) ? aKey : CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(aKey));
                var keyB = new MifareKey(CustomConverter.KeyFormatQuickCheck(bKey) ? bKey : CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(bKey));

                if (await tryInitReader())
                {

                    card = readerUnit.getSingleChip();

                    var cmd = card.getCommands() as MifareCommands;

                    try
                    { //try to Auth with Keytype A
                        for (int k = 0; k < (sectorNumber > 31 ? 16 : 4); k++) // if sector > 31 is 16 blocks each sector i.e. mifare 4k else its 1k or 2k with 4 blocks each sector
                        {
                            cmd.loadKey(0, MifareKeyType.KT_KEY_A, keyA);

                            DataBlock = new MifareClassicDataBlockModel(
                                (byte)CustomConverter.GetChipBasedDataBlockNumber(sectorNumber, k),
                                k);

                            try
                            {
                                cmd.authenticate((byte)CustomConverter.GetChipBasedDataBlockNumber(sectorNumber, k),
                                                      0,
                                                      MifareKeyType.KT_KEY_A);

                                Sector.IsAuthenticated = true;

                                try
                                {
                                    ByteVector data = cmd.readBinary(
                                        (byte)CustomConverter.GetChipBasedDataBlockNumber(sectorNumber, k),
                                        48);

                                    DataBlock.Data = data.ToArray();

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
                                    cmd.loadKey(1, MifareKeyType.KT_KEY_B, keyB);

                                    cmd.authenticate(
                                        (byte)CustomConverter.GetChipBasedDataBlockNumber(sectorNumber, k),
                                        1,
                                        MifareKeyType.KT_KEY_B);

                                    Sector.IsAuthenticated = true;

                                    try
                                    {
                                        object data = cmd.readBinary(
                                            (byte)CustomConverter.GetChipBasedDataBlockNumber(sectorNumber, k),
                                            48);

                                        DataBlock.Data = (byte[])data;

                                        DataBlock.IsAuthenticated = true;

                                        Sector.DataBlock.Add(DataBlock);
                                    }
                                    catch
                                    {

                                        DataBlock.IsAuthenticated = false;

                                        Sector.DataBlock.Add(DataBlock);

                                        return ERROR.AuthFailure;
                                    }
                                }
                                catch
                                {
                                    Sector.IsAuthenticated = false;
                                    DataBlock.IsAuthenticated = false;

                                    Sector.DataBlock.Add(DataBlock);

                                    return ERROR.AuthFailure;
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
                return ERROR.TransportError;
            }
            catch (Exception e)
            {
                eventLog.WriteEntry(e.Message, EventLogEntryType.Error);
                return ERROR.AuthFailure;
            }
        }

        /// <inheritdoc />
        public override async Task<ERROR> WriteMifareClassicSingleSector(int sectorNumber, string aKey, string bKey, byte[] buffer)
        {
            try
            {
                var keyA = new MifareKey(CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(aKey));
                var keyB = new MifareKey(CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(bKey));

                int blockCount = 0;

                if (await tryInitReader())
                {
                    card = readerUnit.getSingleChip();

                    var cmd = card.getCommands() as MifareCommands;

                    try
                    { //try to Auth with Keytype A
                        cmd.loadKey(0, MifareKeyType.KT_KEY_A, keyA);
                        cmd.loadKey(1, MifareKeyType.KT_KEY_B, keyB);

                        for (int k = 0; k < blockCount; k++)
                        {
                            try
                            {
                                cmd.authenticate(
                                    (byte)CustomConverter.GetChipBasedDataBlockNumber(sectorNumber, k),
                                    0,
                                    MifareKeyType.KT_KEY_A);

                                try
                                {
                                    cmd.updateBinary(
                                        (byte)CustomConverter.GetChipBasedDataBlockNumber(sectorNumber, k),
                                        new ByteVector(buffer));

                                    return ERROR.NoError;
                                }
                                catch
                                {
                                    return ERROR.AuthFailure;
                                }
                            }
                            catch
                            { // Try Auth with keytype b

                                try
                                {
                                    cmd.authenticate((byte)CustomConverter.GetChipBasedDataBlockNumber(sectorNumber, k),
                                                          1,
                                                          MifareKeyType.KT_KEY_B);

                                    try
                                    {
                                        cmd.updateBinary(
                                            (byte)CustomConverter.GetChipBasedDataBlockNumber(sectorNumber, k),
                                            new ByteVector(buffer));

                                        return ERROR.NoError;

                                    }
                                    catch
                                    {
                                        return ERROR.AuthFailure;
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
                        return ERROR.TransportError;
                    }
                    return ERROR.TransportError;
                }
            }
            catch (Exception e)
            {
                eventLog.WriteEntry(e.Message, EventLogEntryType.Error);

                return ERROR.TransportError;
            }
            return ERROR.TransportError;
        }

        /// <inheritdoc />
        public override async Task<ERROR> WriteMifareClassicSingleBlock(int _blockNumber, string aKey, string bKey, byte[] buffer)
        {
            try
            {
                var keyA = new MifareKey(CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(aKey));
                var keyB = new MifareKey(CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(bKey));

                if (await tryInitReader())
                {
                    card = readerUnit.getSingleChip();

                    var cmd = card.getCommands() as MifareCommands;

                    try
                    {
                        cmd.loadKey(0, MifareKeyType.KT_KEY_A, keyA); // FIXME "sectorNumber" to 0

                        try
                        { //try to Auth with Keytype A
                            cmd.authenticate((byte)_blockNumber, 0, MifareKeyType.KT_KEY_A); // FIXME same as '303

                            cmd.updateBinary((byte)_blockNumber, new ByteVector(buffer));

                            return ERROR.NoError;
                        }
                        catch
                        { // Try Auth with Keytype b

                            cmd.loadKey(0, MifareKeyType.KT_KEY_B, keyB);

                            try
                            {
                                cmd.authenticate((byte)_blockNumber, 0, MifareKeyType.KT_KEY_B); // FIXME same as '303

                                cmd.updateBinary((byte)_blockNumber, new ByteVector(buffer));

                                return ERROR.NoError;
                            }
                            catch
                            {
                                return ERROR.AuthFailure;
                            }
                        }
                    }
                    catch
                    {
                        return ERROR.AuthFailure;
                    }
                }
            }
            catch (Exception e)
            {
                eventLog.WriteEntry(e.Message, EventLogEntryType.Error);

                return ERROR.TransportError;
            }
            return ERROR.TransportError;
        }

        /// <inheritdoc />
        public async Task<ERROR> ReadMifareClassicSingleBlock(int _blockNumber, string _aKey, string _bKey)
        {
            try
            {
                var keyA = new MifareKey(CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(_aKey));
                var keyB = new MifareKey(CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(_bKey));

                if (await tryInitReader())
                {
                    card = readerUnit.getSingleChip();

                    if (!string.IsNullOrWhiteSpace(ByteArrayConverter.GetStringFrom(card.getChipIdentifier().ToArray())))
                    {
                        try
                        {
                            //CardInfo = new CARD_INFO((CARD_TYPE)Enum.Parse(typeof(CARD_TYPE), card.getCardType()), ByteArrayConverter.GetStringFrom(card.getChipIdentifier().ToArray()));
                            GenericChip = new GenericChipModel(ByteArrayConverter.GetStringFrom(card.getChipIdentifier().ToArray()), (CARD_TYPE)Enum.Parse(typeof(CARD_TYPE), card.getCardType()));
                        }
                        catch (Exception e)
                        {
                            eventLog.WriteEntry(e.Message, EventLogEntryType.Error);
                        }
                    }

                    var cmd = card.getCommands() as MifareCommands;

                    try
                    {
                        cmd.loadKey(0, MifareKeyType.KT_KEY_A, keyA); // FIXME "sectorNumber" to 0

                        try
                        { //try to Auth with Keytype A
                            cmd.authenticate((byte)_blockNumber, 0, MifareKeyType.KT_KEY_A); // FIXME same as '303

                            MifareClassicData = cmd.readBinary((byte)_blockNumber, 48).ToArray();

                            return ERROR.NoError;
                        }
                        catch
                        { // Try Auth with keytype b

                            cmd.loadKey(0, MifareKeyType.KT_KEY_B, keyB);

                            try
                            {
                                cmd.authenticate((byte)_blockNumber, 0, MifareKeyType.KT_KEY_B); // FIXME same as '303

                                MifareClassicData = cmd.readBinary((byte)_blockNumber, 48).ToArray();

                                return ERROR.NoError;
                            }
                            catch
                            {
                                return ERROR.AuthFailure;
                            }
                        }
                    }
                    catch
                    {
                        return ERROR.AuthFailure;
                    }
                }
            }
            catch (Exception e)
            {
                eventLog.WriteEntry(e.Message, EventLogEntryType.Error);

                return ERROR.TransportError;
            }
            return ERROR.TransportError;
        }

        /// <inheritdoc />
        public override async Task<ERROR> WriteMifareClassicWithMAD(int _madApplicationID, int _madStartSector,
            string _aKeyToUse, string _bKeyToUse, string _aKeyToWrite, string _bKeyToWrite,
            string _madAKeyToUse, string _madBKeyToUse, string _madAKeyToWrite, string _madBKeyToWrite,
            byte[] buffer, byte _madGPB, SectorAccessBits _sab, bool _useMADToAuth = false, bool _keyToWriteUseMAD = false)
        {
            try
            {
                Sector = new MifareClassicSectorModel();
                await projectManager.LoadSettingsAsync();

                var mAKeyToUse = new MifareKey(CustomConverter.KeyFormatQuickCheck(_aKeyToUse) ? _aKeyToUse : CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(_aKeyToUse));
                var mBKeyToUse = new MifareKey(CustomConverter.KeyFormatQuickCheck(_bKeyToUse) ? _bKeyToUse : CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(_bKeyToUse));

                var mAKeyToWrite = new MifareKey(CustomConverter.KeyFormatQuickCheck(_aKeyToWrite) ? _aKeyToWrite : CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(_aKeyToWrite));
                var mBKeyToWrite = new MifareKey(CustomConverter.KeyFormatQuickCheck(_bKeyToWrite) ? _bKeyToWrite : CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(_bKeyToWrite));

                var madAKeyToUse = new MifareKey(CustomConverter.KeyFormatQuickCheck(_madAKeyToUse) ? _madAKeyToUse : CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(_madAKeyToUse));
                var madBKeyToUse = new MifareKey(CustomConverter.KeyFormatQuickCheck(_madBKeyToUse) ? _madBKeyToUse : CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(_madBKeyToUse));

                var madAKeyToWrite = new MifareKey(CustomConverter.KeyFormatQuickCheck(_madAKeyToWrite) ? _madAKeyToWrite : CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(_madAKeyToWrite));
                var madBKeyToWrite = new MifareKey(CustomConverter.KeyFormatQuickCheck(_madBKeyToWrite) ? _madBKeyToWrite : CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(_madBKeyToWrite));

                if (await tryInitReader())
                {
                    card = readerUnit.getSingleChip();

                    MifareLocation mlocation = new MifareLocation
                    {
                        aid = (ushort)_madApplicationID,
                        useMAD = _keyToWriteUseMAD,
                        sector = _madStartSector
                    };

                    MifareAccessInfo aiToWrite = new MifareAccessInfo
                    {
                        useMAD = _keyToWriteUseMAD,

                    };

                    aiToWrite.madKeyA.fromString(_madAKeyToUse == _madAKeyToWrite ? madAKeyToUse.getString(true) : madAKeyToWrite.getString(true)); // only set new madkey if mad key has changed
                    aiToWrite.madKeyB.fromString(_madBKeyToUse == _madBKeyToWrite ? madBKeyToUse.getString(true) : madBKeyToWrite.getString(true)); // only set new madkey if mad key has changed
                    aiToWrite.keyA.fromString(_aKeyToUse == _aKeyToWrite ? mAKeyToUse.getString(true) : mAKeyToWrite.getString(true));
                    aiToWrite.keyB.fromString(_bKeyToUse == _bKeyToWrite ? mBKeyToUse.getString(true) : mBKeyToWrite.getString(true));
                    aiToWrite.madGPB = _madGPB;

                    var aiToUse = new MifareAccessInfo
                    {
                        useMAD = _useMADToAuth,
                        keyA = mAKeyToUse,
                        keyB = mBKeyToUse
                    };

                    if (_useMADToAuth)
                    {
                        aiToUse.madKeyA = madAKeyToUse;
                        aiToUse.madKeyB = madBKeyToUse;
                        aiToUse.madGPB = _madGPB;
                    }

                    //TODO: report BUG when using SL1
                    var cmd = card.getCommands() as MifareCommands;
                    var cardService = card.getService(CardServiceType.CST_STORAGE) as StorageCardService;

                    try
                    {
                        cardService.writeData(mlocation, aiToUse, aiToWrite, new ByteVector(buffer), CardBehavior.CB_AUTOSWITCHAREA);
                    }
                    catch (Exception e)
                    {
                        eventLog.WriteEntry(e.Message, EventLogEntryType.Error);
                        return ERROR.AuthFailure;
                    }
                    return ERROR.NoError;
                }
            }
            catch (Exception e)
            {
                eventLog.WriteEntry(e.Message, EventLogEntryType.Error);
                return ERROR.AuthFailure;
            }
            return ERROR.NoError;
        }

        /// <inheritdoc />
        public override async Task<ERROR> ReadMifareClassicWithMAD(int madApplicationID, string _aKeyToUse,
            string _bKeyToUse, string _madAKeyToUse, string _madBKeyToUse, int _length,
            byte _madGPB, bool _useMADToAuth = true, bool _aiToUseIsMAD = false)
        {
            try
            {
                Sector = new MifareClassicSectorModel();
                await projectManager.LoadSettingsAsync();

                var mAKeyToUse = new MifareKey(CustomConverter.KeyFormatQuickCheck(_aKeyToUse) ? _aKeyToUse : CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(_aKeyToUse));
                var mBKeyToUse = new MifareKey(CustomConverter.KeyFormatQuickCheck(_bKeyToUse) ? _bKeyToUse : CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(_bKeyToUse));

                var madAKeyToUse = new MifareKey(CustomConverter.KeyFormatQuickCheck(_madAKeyToUse) ? _madAKeyToUse : CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(_madAKeyToUse));
                var madBKeyToUse = new MifareKey(CustomConverter.KeyFormatQuickCheck(_madBKeyToUse) ? _madBKeyToUse : CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(_madBKeyToUse));

                if (await tryInitReader())
                {
                    card = readerUnit.getSingleChip();

                    MifareLocation mlocation = card.createLocation() as MifareLocation;
                    mlocation.aid = (ushort)madApplicationID;
                    mlocation.useMAD = _useMADToAuth;

                    var aiToUse = new MifareAccessInfo
                    {
                        useMAD = true,
                        keyA = mAKeyToUse,
                        keyB = mBKeyToUse,
                        madKeyA = madAKeyToUse,
                        madKeyB = madBKeyToUse,
                        madGPB = _madGPB
                    };

                    var cmd = card.getCommands() as MifareCommands;
                    var cardService = card.getService(CardServiceType.CST_STORAGE) as StorageCardService;

                    try
                    {
                        MifareClassicData = cardService.readData(mlocation, aiToUse, (uint)_length, CardBehavior.CB_AUTOSWITCHAREA).ToArray();
                    }
                    catch (Exception e)
                    {
                        eventLog.WriteEntry(e.Message, EventLogEntryType.Error);
                        return ERROR.AuthFailure;
                    }
                    return ERROR.NoError;
                }
            }
            catch (Exception e)
            {
                eventLog.WriteEntry(e.Message, EventLogEntryType.Error);
                return ERROR.AuthFailure;
            }
            return ERROR.NoError;
        }

        #endregion mifare classic

        #region mifare ultralight

        public override async Task<ERROR> ReadMifareUltralightSinglePage(int _pageNo)
        {
            try
            {
                if (await tryInitReader())
                {
                    ReaderUnitName = readerUnit.getConnectedName();
                    //readerSerialNumber = readerUnit.GetReaderSerialNumber();
                    RawFormat format = new RawFormat();

                    var chip = readerUnit.getSingleChip() as MifareUltralightChip;

                    var service = chip.getService(CardServiceType.CST_STORAGE) as StorageCardService;

                    Location location = chip.createLocation() as Location;

                    if (chip.getCardType() == "MifareUltralight")
                    {
                        var cmd = chip.getCommands() as MifareUltralightCommands;// IMifareUltralightCommands;
                        MifareUltralightPageData = cmd.readPages(_pageNo, _pageNo).ToArray();
                    }

                    return ERROR.NoError;
                }
                return ERROR.NoError;
            }
            catch (Exception e)
            {
                eventLog.WriteEntry(e.Message, EventLogEntryType.Error);
                return ERROR.TransportError;
            }
        }

        #endregion mifare ultralight

        #region mifare desfire


        /// <inheritdoc />
        public override async Task<ERROR> GetMiFareDESFireChipAppIDs(string _appMasterKey, DESFireKeyType _keyTypeAppMasterKey)
        {
            try
            {
                if (DesfireChip == null)
                {
                    DesfireChip = new MifareDesfireChipModel();
                }

                // The excepted memory tree
                DESFireLocation location = new DESFireLocation
                {
                    // File communication requires encryption
                    securityLevel = (LibLogicalAccess.Card.EncryptionMode)EncryptionMode.CM_ENCRYPT
                };

                // Keys to use for authentication
                DESFireAccessInfo aiToUse = new DESFireAccessInfo();
                aiToUse.masterCardKey.fromString(_appMasterKey);
                aiToUse.masterCardKey.setKeyType((LibLogicalAccess.Card.DESFireKeyType)_keyTypeAppMasterKey);


                if (await tryInitReader())
                {
                    card = readerUnit.getSingleChip();

                    //Get AppIDs without Authentication (Free Directory Listing)
                    try
                    {

                        if (card.getCardType() == "DESFire")
                        {
                            var cmd = card.getCommands() as DESFireCommands;

                            try
                            {
                                cmd.selectApplication(0);

                                UIntCollection appIDsObject = cmd.getApplicationIDs();
                                foreach (uint appID in appIDsObject.ToArray())
                                {
                                    DesfireChip.AppList.Add(new MifareDesfireAppModel(appID));
                                }

                                return ERROR.NoError;
                            }
                            catch
                            {
                                //Get AppIDs with Authentication (Directory Listing with PICC MK)
                                try
                                {
                                    cmd.selectApplication(0);
                                    cmd.authenticate(0, aiToUse.masterCardKey);

                                    UIntCollection appIDsObject = cmd.getApplicationIDs();
                                    foreach (uint appID in appIDsObject.ToArray())
                                    {
                                        DesfireChip.AppList.Add(new MifareDesfireAppModel(appID));
                                    }

                                    return ERROR.NoError;
                                }
                                catch (Exception e)
                                {
                                    if (e.Message != "" && e.Message.Contains("same number already exists"))
                                    {
                                        return ERROR.ProtocolConstraint;
                                    }
                                    else if (e.Message != "" && e.Message.Contains("status does not allow the requested command"))
                                    {
                                        return ERROR.AuthFailure;
                                    }
                                    else
                                        return ERROR.TransportError;
                                }
                            }
                        }

                        if (card.getCardType() == "DESFireEV1" ||
                            card.getCardType() == "DESFireEV2" ||
                            card.getCardType() == "DESFireEV3")
                        {

                            var cmd = (card as DESFireChip).getDESFireCommands();

                            try
                            {
                                UIntCollection appIDsObject = cmd.getApplicationIDs();
                                foreach (uint appID in appIDsObject.ToArray())
                                {
                                    DesfireChip.AppList.Add(new MifareDesfireAppModel(appID));
                                }

                                var ev1Cmd = (card as DESFireEV1Chip).getDESFireEV1Commands();
                                DesfireChip.FreeMemory = ev1Cmd.getFreeMem();

                                return ERROR.NoError;
                            }
                            catch
                            {
                                //Get AppIDs with Authentication (Directory Listing with PICC MK)
                                try
                                {
                                    cmd.selectApplication(0);
                                    cmd.authenticate(0, aiToUse.masterCardKey);

                                    UIntCollection appIDsObject = cmd.getApplicationIDs();
                                    foreach (uint appID in appIDsObject.ToArray())
                                    {
                                        DesfireChip.AppList.Add(new MifareDesfireAppModel(appID));
                                    }

                                    var ev1Cmd = (card as DESFireEV1Chip).getDESFireEV1Commands();
                                    DesfireChip.FreeMemory = ev1Cmd.getFreeMem();

                                    return ERROR.NoError;
                                }
                                catch (Exception e)
                                {
                                    if (e.Message != "" && e.Message.Contains("same number already exists"))
                                    {
                                        return ERROR.ProtocolConstraint;
                                    }
                                    else if (e.Message != "" && e.Message.Contains("status does not allow the requested command"))
                                    {
                                        return ERROR.AuthFailure;
                                    }
                                    else
                                        return ERROR.TransportError;
                                }
                            }
                        }
                        else
                            return ERROR.TransportError;
                    }

                    catch
                    {
                        try
                        {
                            if (card.getCardType() == "DESFire")
                            {
                                var cmd = card.getCommands() as DESFireCommands;

                                cmd.selectApplication(0);
                                cmd.authenticate(0, aiToUse.masterCardKey);

                                UIntCollection appIDsObject = cmd.getApplicationIDs();
                                foreach (uint appID in appIDsObject.ToArray())
                                {
                                    DesfireChip.AppList.Add(new MifareDesfireAppModel(appID));
                                }

                                return ERROR.NoError;
                            }

                            if (card.getCardType() == "DESFireEV1" ||
                                card.getCardType() == "DESFireEV2" ||
                                card.getCardType() == "DESFireEV3")
                            {
                                var cmd = (card as DESFireEV1Chip).getDESFireEV1Commands();

                            }
                            else
                                return ERROR.TransportError;
                        }

                        catch
                        {

                        }
                    }
                }
                return ERROR.TransportError;
            }

            catch (Exception e)
            {
                if (e.Message != "" && e.Message.Contains("same number already exists"))
                {
                    return ERROR.ProtocolConstraint;
                }
                else if (e.Message != "" && e.Message.Contains("status does not allow the requested command"))
                {
                    return ERROR.AuthFailure;
                }
                else
                    return ERROR.TransportError;
            }
        }

        /// <inheritdoc />
        public override async Task<ERROR> CreateMifareDesfireFile(string _appMasterKey, DESFireKeyType _keyTypeAppMasterKey, FileType_MifareDesfireFileType _fileType, DESFireAccessRights _accessRights, EncryptionMode _encMode,
                                             int _appID, int _fileNo, int _fileSize,
                                             int _minValue = 0, int _maxValue = 1000, int _initValue = 0, bool _isValueLimited = false,
                                             int _maxNbOfRecords = 100)
        {
            try
            {
                DESFireAccessRights accessRights = _accessRights;

                // Keys to use for authentication
                DESFireAccessInfo aiToUse = new DESFireAccessInfo();
                CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_appMasterKey);
                aiToUse.masterCardKey.fromString(CustomConverter.DesfireKeyToCheck);
                aiToUse.masterCardKey.setKeyType((LibLogicalAccess.Card.DESFireKeyType)_keyTypeAppMasterKey);

                var arToUse = new LibLogicalAccess.Card.DESFireAccessRights()
                {
                    changeAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.changeAccess,
                    readAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.readAccess,
                    writeAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.writeAccess,
                    readAndWriteAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.readAndWriteAccess
                };

                if (await tryInitReader())
                {
                    card = readerUnit.getSingleChip();

                    try
                    {
                        DESFireCommands cmd;

                        if (card.getCardType() == "DESFireEV1" ||
                            card.getCardType() == "DESFireEV2" ||
                            card.getCardType() == "DESFireEV3")
                        {
                            cmd = (card as DESFireChip).getDESFireCommands();
                        }
                        else if (card.getCardType() == "DESFire")
                        {
                            cmd = card.getCommands() as DESFireCommands;
                        }
                        else
                        {
                            cmd = (card as DESFireChip).getDESFireCommands();
                        }


                        cmd.selectApplication((uint)_appID);
                        cmd.authenticate(0, aiToUse.masterCardKey);

                        switch (_fileType)
                        {
                            case FileType_MifareDesfireFileType.StdDataFile:
                                cmd.createStdDataFile((byte)_fileNo, (LibLogicalAccess.Card.EncryptionMode)_encMode, arToUse, (uint)_fileSize);
                                break;

                            case FileType_MifareDesfireFileType.BackupFile:
                                cmd.createBackupFile((byte)_fileNo, (LibLogicalAccess.Card.EncryptionMode)_encMode, arToUse, (uint)_fileSize);
                                break;

                            case FileType_MifareDesfireFileType.ValueFile:
                                cmd.createValueFile((byte)_fileNo, (LibLogicalAccess.Card.EncryptionMode)_encMode, arToUse, _minValue, _maxValue, _initValue, _isValueLimited);
                                break;

                            case FileType_MifareDesfireFileType.CyclicRecordFile:
                                cmd.createCyclicRecordFile((byte)_fileNo, (LibLogicalAccess.Card.EncryptionMode)_encMode, arToUse, (uint)_fileSize, (uint)_maxNbOfRecords);
                                break;

                            case FileType_MifareDesfireFileType.LinearRecordFile:
                                cmd.createLinearRecordFile((byte)_fileNo, (LibLogicalAccess.Card.EncryptionMode)_encMode, arToUse, (uint)_fileSize, (uint)_maxNbOfRecords);
                                break;
                        }

                        return ERROR.NoError;
                    }
                    catch (Exception e)
                    {
                        if (e.Message != "" && e.Message.Contains("same number already exists"))
                        {
                            return ERROR.ProtocolConstraint;
                        }
                        else if (e.Message != "" && e.Message.Contains("status does not allow the requested command"))
                        {
                            return ERROR.AuthFailure;
                        }
                        else if (e.Message != "" && e.Message.Contains("Insufficient NV-Memory"))
                        {
                            return ERROR.ProtocolConstraint;
                        }
                        else
                            return ERROR.TransportError;
                    }
                }
                return ERROR.TransportError;
            }
            catch
            {
                return ERROR.TransportError;
            }
        }

        /// <inheritdoc />
        public override async Task<ERROR> ReadMiFareDESFireChipFile(string _appReadKey, DESFireKeyType _keyTypeAppReadKey, int _readKeyNo,
                                               string _appWriteKey, DESFireKeyType _keyTypeAppWriteKey, int _writeKeyNo,
                                               EncryptionMode _encMode,
                                               int _fileNo, int _appID, int _fileSize)
        {
            try
            {
                // The excepted memory tree
                DESFireLocation location = new DESFireLocation
                {
                    // The Application ID to use
                    aid = (uint)_appID,
                    // File 0 into this application
                    file = (byte)_fileNo,
                    // File communication requires encryption
                    securityLevel = (LibLogicalAccess.Card.EncryptionMode)_encMode
                };

                // Keys to use for authentication

                // Get the card storage service
                StorageCardService storage = (StorageCardService)card.getService(CardServiceType.CST_STORAGE);

                // Change keys with the following ones
                DESFireAccessInfo aiToRead = new DESFireAccessInfo();
                CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_appReadKey);
                aiToRead.readKey.fromString(CustomConverter.DesfireKeyToCheck);
                aiToRead.readKey.setKeyType((LibLogicalAccess.Card.DESFireKeyType)_keyTypeAppReadKey);
                aiToRead.readKeyno = (byte)_readKeyNo;

                CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_appWriteKey);
                aiToRead.writeKey.fromString(CustomConverter.DesfireKeyToCheck);
                aiToRead.writeKey.setKeyType((LibLogicalAccess.Card.DESFireKeyType)_keyTypeAppWriteKey);
                aiToRead.writeKeyno = (byte)_writeKeyNo;

                if (await tryInitReader())
                {
                    if (card.getCardType() == "DESFire" ||
                        card.getCardType() == "DESFireEV1" ||
                        card.getCardType() == "DESFireEV2" ||
                        card.getCardType() == "DESFireEV3")
                    {
                        try
                        {
                            var cmd = card.getCommands() as DESFireCommands;

                            try
                            {
                                cmd.selectApplication((uint)_appID);

                                cmd.authenticate((byte)_readKeyNo, aiToRead.readKey);

                                MifareDESFireData = cmd.readData((byte)_fileNo, 0, (uint)_fileSize, LibLogicalAccess.Card.EncryptionMode.CM_ENCRYPT).ToArray();
                            }
                            catch
                            {
                                cmd.selectApplication((uint)_appID);

                                MifareDESFireData = cmd.readData((byte)_fileNo, 0, (uint)_fileSize, LibLogicalAccess.Card.EncryptionMode.CM_ENCRYPT).ToArray();
                            }

                            return ERROR.NoError;
                        }

                        catch (Exception e)
                        {
                            if (e.Message != "" && e.Message.Contains("same number already exists"))
                            {
                                return ERROR.ProtocolConstraint;
                            }
                            else if (e.Message != "" && e.Message.Contains("status does not allow the requested command"))
                            {
                                return ERROR.AuthFailure;
                            }
                            else
                                return ERROR.TransportError;
                        }
                    }
                    else
                        return ERROR.TransportError;
                }
                return ERROR.TransportError;
            }

            catch
            {
                return ERROR.TransportError;
            }
        }

        /// <inheritdoc />
        public override async Task<ERROR> WriteMiFareDESFireChipFile(string _appReadKey, DESFireKeyType _keyTypeAppReadKey, int _readKeyNo,
                                                string _appWriteKey, DESFireKeyType _keyTypeAppWriteKey, int _writeKeyNo,
                                                EncryptionMode _encMode,
                                                int _fileNo, int _appID, byte[] _data)
        {
            try
            {
                // The excepted memory tree
                DESFireLocation location = new DESFireLocation
                {
                    // The Application ID to use
                    aid = (uint)_appID,
                    // File 0 into this application
                    file = (byte)_fileNo,
                    // File communication requires encryption
                    securityLevel = (LibLogicalAccess.Card.EncryptionMode)_encMode
                };

                // Keys to use for authentication

                // Get the card storage service
                StorageCardService storage = (StorageCardService)card.getService(CardServiceType.CST_STORAGE);

                // Change keys with the following ones
                DESFireAccessInfo aiToWrite = new DESFireAccessInfo();
                CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_appReadKey);
                aiToWrite.readKey.fromString(CustomConverter.DesfireKeyToCheck);
                aiToWrite.readKey.setKeyType((LibLogicalAccess.Card.DESFireKeyType)_keyTypeAppReadKey);
                aiToWrite.readKeyno = (byte)_readKeyNo;

                CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_appWriteKey);
                aiToWrite.writeKey.fromString(CustomConverter.DesfireKeyToCheck);
                aiToWrite.writeKey.setKeyType((LibLogicalAccess.Card.DESFireKeyType)_keyTypeAppWriteKey);
                aiToWrite.writeKeyno = (byte)_writeKeyNo;

                if (await tryInitReader())
                {
                    if (card.getCardType() == "DESFire" ||
                        card.getCardType() == "DESFireEV1" ||
                        card.getCardType() == "DESFireEV2" ||
                        card.getCardType() == "DESFireEV3")
                    {
                        try
                        {
                            var cmd = card.getCommands() as DESFireCommands;

                            cmd.selectApplication((uint)_appID);

                            cmd.authenticate((byte)_writeKeyNo, aiToWrite.writeKey);

                            //byte[] padded = new byte[161];
                            //Buffer.BlockCopy(_data, 0, padded, 0, _data.Length);
                            //_data = padded;

                            cmd.writeData((byte)_fileNo, 0, new ByteVector(_data), LibLogicalAccess.Card.EncryptionMode.CM_ENCRYPT);

                            return ERROR.NoError;
                        }

                        catch (Exception e)
                        {
                            if (e.Message != "" && e.Message.ToLower().Contains("same number already exists"))
                            {
                                return ERROR.ProtocolConstraint;
                            }
                            else if (e.Message != "" && e.Message.ToLower().Contains("status does not allow the requested command"))
                            {
                                return ERROR.AuthFailure;
                            }
                            else if (e.Message != "" && e.Message.ToLower().Contains("data requested but no more data"))
                            {
                                return ERROR.NoError;
                            }
                            else
                                return ERROR.TransportError;
                        }
                    }
                    else
                        return ERROR.TransportError;
                }
                return ERROR.TransportError;
            }

            catch
            {
                return ERROR.TransportError;
            }
        }

        /// <inheritdoc />
        public override async Task<ERROR> AuthToMifareDesfireApplication(string _applicationMasterKey,
                                                DESFireKeyType _keyType, int _keyNumber, int _appID = 0)
        {
            try
            {
                // The excepted memory tree
                DESFireLocation location = new DESFireLocation
                {
                    // The Application ID to use
                    aid = (uint)_appID
                };
                // File communication requires encryption

                // Keys to use for authentication
                DESFireAccessInfo aiToUse = new DESFireAccessInfo();
                CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_applicationMasterKey);
                aiToUse.masterCardKey.fromString(CustomConverter.DesfireKeyToCheck);
                aiToUse.masterCardKey.setKeyType((LibLogicalAccess.Card.DESFireKeyType)_keyType);

                if (await tryInitReader())
                {
                    card = readerUnit.getSingleChip();

                    if (card.getCardType() == "DESFire" ||
                        card.getCardType() == "DESFireEV1" ||
                        card.getCardType() == "DESFireEV2" ||
                        card.getCardType() == "DESFireEV3")
                    {
                        var cmd = card.getCommands() as DESFireCommands;
                        try
                        {
                            cmd.selectApplication((uint)_appID);
                            if (_appID > 0)
                                cmd.authenticate((byte)_keyNumber, aiToUse.masterCardKey);
                            else
                                cmd.authenticate(0, aiToUse.masterCardKey);
                            return ERROR.NoError;
                        }

                        catch (Exception e)
                        {
                            if (e.Message != "" && e.Message.Contains("same number already exists"))
                            {
                                return ERROR.ProtocolConstraint;
                            }
                            else if (e.Message != "" && e.Message.Contains("status does not allow the requested command"))
                            {
                                return ERROR.AuthFailure;
                            }
                            else
                                return ERROR.TransportError;
                        }
                    }

                    else
                        return ERROR.TransportError;
                }
                return ERROR.TransportError;
            }
            catch
            {
                return ERROR.TransportError;
            }
        }

        /// <inheritdoc />
        public override async Task<OperationResult> GetMifareDesfireAppSettings(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumberCurrent = 0, int _appID = 0, bool authenticateBeforeReading = true)
        {
            byte maxNbrOfKeys;
            LibLogicalAccess.Card.DESFireKeySettings keySettings;

            if (DesfireChip == null)
            {
                DesfireChip = new MifareDesfireChipModel();
            }

            try
            {
                DESFireAccessInfo aiToUse = new DESFireAccessInfo();
                CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_applicationMasterKey);
                aiToUse.masterCardKey.fromString(CustomConverter.DesfireKeyToCheck);
                aiToUse.masterCardKey.setKeyType((LibLogicalAccess.Card.DESFireKeyType)_keyType);

                if (!await tryInitReader())
                {
                    return OperationResult.Failure(
                        ERROR.TransportError,
                        "Reader not initialized",
                        operation: nameof(GetMifareDesfireAppSettings),
                        wasAuthenticated: false,
                        metadata: new Dictionary<string, string>
                        {
                            { "ApplicationId", _appID.ToString(CultureInfo.CurrentCulture) },
                            { "AuthenticateBeforeReading", authenticateBeforeReading.ToString(CultureInfo.CurrentCulture) },
                            { "KeyNumber", _keyNumberCurrent.ToString(CultureInfo.CurrentCulture) }
                        });
                }

                card = readerUnit.getSingleChip();
                DESFireCommands cmd;

                if (card.getCardType() == "DESFire")
                {
                    cmd = card.getCommands() as DESFireCommands;
                }

                else if (card.getCardType() == "DESFireEV1" ||
                        card.getCardType() == "DESFireEV2" ||
                        card.getCardType() == "DESFireEV3" ||
                        card.getCardType() == "GENERIC_T_CL_A")
                {
                    cmd = (card as DESFireChip).getDESFireCommands();
                }
                else
                {
                    return OperationResult.Failure(
                        ERROR.TransportError,
                        "Unsupported card type",
                        card.getCardType(),
                        nameof(GetMifareDesfireAppSettings),
                        authenticateBeforeReading,
                        new Dictionary<string, string>
                        {
                            { "ApplicationId", _appID.ToString(CultureInfo.CurrentCulture) },
                            { "CardType", card.getCardType() }
                        });
                }

                cmd.selectApplication((uint)_appID);
                DesfireChip.UID = ByteArrayConverter.GetStringFrom(card.getChipIdentifier().ToArray());

                if (authenticateBeforeReading)
                {
                    cmd.authenticate((byte)_keyNumberCurrent, aiToUse.masterCardKey);
                }

                cmd.getKeySettings(out keySettings, out maxNbrOfKeys);
                MaxNumberOfAppKeys = (byte)(maxNbrOfKeys & 0x0F);
                EncryptionType = (DESFireKeyType)(maxNbrOfKeys & 0xF0);
                DesfireAppKeySetting = (AccessControl.DESFireKeySettings)keySettings;

                var metadata = new Dictionary<string, string>
                {
                    { "ApplicationId", _appID.ToString(CultureInfo.CurrentCulture) },
                    { "AuthenticateBeforeReading", authenticateBeforeReading.ToString(CultureInfo.CurrentCulture) },
                    { "KeyNumber", _keyNumberCurrent.ToString(CultureInfo.CurrentCulture) }
                };

                return OperationResult.Success(
                    operation: nameof(GetMifareDesfireAppSettings),
                    wasAuthenticated: authenticateBeforeReading,
                    metadata: metadata);
            }
            catch (TimeoutException e)
            {
                return OperationResult.Failure(
                    ERROR.TransportError,
                    "Fetching application settings timed out",
                    e.Message,
                    nameof(GetMifareDesfireAppSettings),
                    authenticateBeforeReading,
                    new Dictionary<string, string>
                    {
                        { "ApplicationId", _appID.ToString(CultureInfo.CurrentCulture) },
                        { "KeyNumber", _keyNumberCurrent.ToString(CultureInfo.CurrentCulture) }
                    });
            }
            catch (InvalidOperationException e)
            {
                return OperationResult.Failure(
                    ERROR.ProtocolConstraint,
                    "Invalid operation while fetching application settings",
                    e.Message,
                    nameof(GetMifareDesfireAppSettings),
                    authenticateBeforeReading,
                    new Dictionary<string, string>
                    {
                        { "ApplicationId", _appID.ToString(CultureInfo.CurrentCulture) },
                        { "KeyNumber", _keyNumberCurrent.ToString(CultureInfo.CurrentCulture) }
                    });
            }
            catch (UnauthorizedAccessException e)
            {
                return OperationResult.Failure(
                    ERROR.PermissionDenied,
                    "Authentication required or denied",
                    e.Message,
                    nameof(GetMifareDesfireAppSettings),
                    authenticateBeforeReading,
                    new Dictionary<string, string>
                    {
                        { "ApplicationId", _appID.ToString(CultureInfo.CurrentCulture) },
                        { "KeyNumber", _keyNumberCurrent.ToString(CultureInfo.CurrentCulture) }
                    });
            }
            catch (Exception e)
            {
                if (e.Message != "" && e.Message.Contains("same number already exists"))
                {
                    return OperationResult.Failure(
                        ERROR.ProtocolConstraint,
                        "Application already exists",
                        e.Message,
                        nameof(GetMifareDesfireAppSettings),
                        authenticateBeforeReading,
                        new Dictionary<string, string>
                        {
                            { "ApplicationId", _appID.ToString(CultureInfo.CurrentCulture) }
                        });
                }
                else if (e.Message != "" && e.Message.Contains("status does not allow the requested command"))
                {
                    return OperationResult.Failure(
                        ERROR.AuthFailure,
                        "Authentication required",
                        e.Message,
                        nameof(GetMifareDesfireAppSettings),
                        authenticateBeforeReading,
                        new Dictionary<string, string>
                        {
                            { "ApplicationId", _appID.ToString(CultureInfo.CurrentCulture) }
                        });
                }

                return OperationResult.Failure(
                    ERROR.TransportError,
                    "Failed to fetch application settings",
                    e.Message,
                    nameof(GetMifareDesfireAppSettings),
                    authenticateBeforeReading,
                    new Dictionary<string, string>
                    {
                        { "ApplicationId", _appID.ToString(CultureInfo.CurrentCulture) },
                        { "KeyNumber", _keyNumberCurrent.ToString(CultureInfo.CurrentCulture) }
                    });
            }
        }

        /// <inheritdoc />
        public override async Task<OperationResult> CreateMifareDesfireApplication(
                string _piccMasterKey, AccessControl.DESFireKeySettings _keySettingsTarget, DESFireKeyType _keyTypePiccMasterKey,
                DESFireKeyType _keyTypeTargetApplication, int _maxNbKeys, int _appID, bool authenticateToPICCFirst = true)
        {
            try
            {
                // The excepted memory tree
                DESFireLocation location = new DESFireLocation
                {
                    // The Application ID to use
                    aid = (uint)_appID,

                    // File communication requires encryption
                    securityLevel = LibLogicalAccess.Card.EncryptionMode.CM_ENCRYPT
                };

                // IDESFireEV1Commands cmd;
                // Keys to use for authentication
                DESFireAccessInfo aiToUse = new DESFireAccessInfo();
                CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_piccMasterKey);
                aiToUse.masterCardKey.fromString(CustomConverter.DesfireKeyToCheck);
                aiToUse.masterCardKey.setKeyType((LibLogicalAccess.Card.DESFireKeyType)_keyTypePiccMasterKey);

                if (!await tryInitReader())
                {
                    return OperationResult.Failure(
                        ERROR.TransportError,
                        "Reader not initialized",
                        operation: nameof(CreateMifareDesfireApplication),
                        wasAuthenticated: authenticateToPICCFirst,
                        metadata: new Dictionary<string, string>
                        {
                            { "ApplicationId", _appID.ToString(CultureInfo.CurrentCulture) },
                            { "AuthenticateToPICCFirst", authenticateToPICCFirst.ToString(CultureInfo.CurrentCulture) }
                        });
                }

                card = readerUnit.getSingleChip();

                var cmd = card.getCommands() as DESFireCommands;
                var ev1Cmd = (card as DESFireEV1Chip).getDESFireEV1Commands();

                try
                {
                    cmd.selectApplication(0);

                    if (authenticateToPICCFirst)
                        cmd.authenticate(0, aiToUse.masterCardKey);

                    if (card.getCardType() == "DESFire")
                    {
                        cmd.createApplication((uint)_appID, (LibLogicalAccess.Card.DESFireKeySettings)_keySettingsTarget, (byte)_maxNbKeys);
                    }
                    else if (card.getCardType() == "DESFireEV1" ||
                            card.getCardType() == "DESFireEV2" ||
                            card.getCardType() == "DESFireEV3")
                    {
                        ev1Cmd.createApplication((uint)_appID, (LibLogicalAccess.Card.DESFireKeySettings)_keySettingsTarget, (byte)_maxNbKeys, (LibLogicalAccess.Card.DESFireKeyType)_keyTypeTargetApplication);
                    }
                    else
                    {
                        return OperationResult.Failure(
                            ERROR.PermissionDenied,
                            "Unsupported card type",
                            card.getCardType(),
                            nameof(CreateMifareDesfireApplication),
                            authenticateToPICCFirst,
                            new Dictionary<string, string>
                            {
                                { "ApplicationId", _appID.ToString(CultureInfo.CurrentCulture) },
                                { "CardType", card.getCardType() }
                            });
                    }
                    return OperationResult.Success(
                        operation: nameof(CreateMifareDesfireApplication),
                        wasAuthenticated: authenticateToPICCFirst,
                        metadata: new Dictionary<string, string>
                        {
                            { "ApplicationId", _appID.ToString(CultureInfo.CurrentCulture) },
                            { "MaxKeys", _maxNbKeys.ToString(CultureInfo.CurrentCulture) }
                        });
                }
                catch (TimeoutException e)
                {
                    return OperationResult.Failure(
                        ERROR.TransportError,
                        "Creating application timed out",
                        e.Message,
                        nameof(CreateMifareDesfireApplication),
                        authenticateToPICCFirst,
                        new Dictionary<string, string>
                        {
                            { "ApplicationId", _appID.ToString(CultureInfo.CurrentCulture) },
                            { "MaxKeys", _maxNbKeys.ToString(CultureInfo.CurrentCulture) }
                        });
                }
                catch (UnauthorizedAccessException e)
                {
                    return OperationResult.Failure(
                        ERROR.PermissionDenied,
                        "Authentication failed while creating application",
                        e.Message,
                        nameof(CreateMifareDesfireApplication),
                        authenticateToPICCFirst,
                        new Dictionary<string, string>
                        {
                            { "ApplicationId", _appID.ToString(CultureInfo.CurrentCulture) },
                            { "MaxKeys", _maxNbKeys.ToString(CultureInfo.CurrentCulture) }
                        });
                }
                catch (Exception e)
                {
                    if (e.Message != "" && e.Message.Contains("same number already exists"))
                    {
                        return OperationResult.Failure(
                            ERROR.ProtocolConstraint,
                            "Application already exists",
                            e.Message,
                            nameof(CreateMifareDesfireApplication),
                            authenticateToPICCFirst,
                            new Dictionary<string, string>
                            {
                                { "ApplicationId", _appID.ToString(CultureInfo.CurrentCulture) }
                            });
                    }
                    else if (e.Message != "" && e.Message.Contains("status does not allow the requested command"))
                    {
                        return OperationResult.Failure(
                            ERROR.AuthFailure,
                            "Authentication failed",
                            e.Message,
                            nameof(CreateMifareDesfireApplication),
                            authenticateToPICCFirst,
                            new Dictionary<string, string>
                            {
                                { "ApplicationId", _appID.ToString(CultureInfo.CurrentCulture) }
                            });
                    }
                    else if (e.Message != "" && e.Message.Contains("Insufficient NV-Memory"))
                    {
                        return OperationResult.Failure(
                            ERROR.ProtocolConstraint,
                            "Insufficient memory",
                            e.Message,
                            nameof(CreateMifareDesfireApplication),
                            authenticateToPICCFirst,
                            new Dictionary<string, string>
                            {
                                { "ApplicationId", _appID.ToString(CultureInfo.CurrentCulture) }
                            });
                    }
                    else
                        return OperationResult.Failure(
                            ERROR.TransportError,
                            "Failed to create application",
                            e.Message,
                            nameof(CreateMifareDesfireApplication),
                            authenticateToPICCFirst,
                            new Dictionary<string, string>
                            {
                                { "ApplicationId", _appID.ToString(CultureInfo.CurrentCulture) },
                                { "MaxKeys", _maxNbKeys.ToString(CultureInfo.CurrentCulture) }
                            });
                }
            }
            catch (Exception e)
            {
                return OperationResult.Failure(
                    ERROR.TransportError,
                    "Unexpected failure while creating application",
                    e.Message,
                    nameof(CreateMifareDesfireApplication),
                    authenticateToPICCFirst,
                    new Dictionary<string, string>
                    {
                        { "ApplicationId", _appID.ToString(CultureInfo.CurrentCulture) }
                    });
            }
        }

        /// <inheritdoc />
        public override async Task<ERROR> ChangeMifareDesfireKeyAsync(
            uint appId,
            byte targetKeyNo,
            DESFireKeyType targetKeyType,
            string currentTargetKeyHex,
            string newTargetKeyHex,
            byte newTargetKeyVersion,   // not used by LibLogicalAccess changeKey; kept for API symmetry
            string masterKeyHex,
            DESFireKeyType masterKeyType,
            AccessControl.DESFireKeySettings keySettings)
        {
            try
            {
                var resolved = DesfireKeyChangeInputs.Resolve(
                    appId,
                    targetKeyNo,
                    targetKeyType,
                    currentTargetKeyHex,
                    newTargetKeyHex,
                    newTargetKeyVersion,
                    masterKeyHex,
                    masterKeyType,
                    keySettings);

                // Build authentication key (either master key or target key depending on policy).
                var authKey = new DESFireKey();
                authKey.setKeyType((LibLogicalAccess.Card.DESFireKeyType)resolved.AuthKeyType);
                CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(resolved.AuthKeyHex);
                authKey.fromString(CustomConverter.DesfireKeyToCheck);

                // Build new key to be written.
                var newKey = new DESFireKey();
                newKey.setKeyType((LibLogicalAccess.Card.DESFireKeyType)resolved.TargetKeyType);
                CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(resolved.NewTargetKeyHex);
                newKey.fromString(CustomConverter.DesfireKeyToCheck);

                // Ensure a clean reader state before reconnecting.
                readerUnit.disconnectFromReader();

                if (!await tryInitReader())
                    return ERROR.TransportError;

                card = readerUnit.getSingleChip();
                if (card == null)
                    return ERROR.TransportError;

                var ct = card.getCardType();
                if (ct != "DESFire" && ct != "DESFireEV1" && ct != "DESFireEV2" && ct != "DESFireEV3")
                    return ERROR.TransportError;

                var cmd = card.getCommands() as DESFireCommands;
                if (cmd == null)
                    return ERROR.TransportError;

                try
                {
                    // Scope selection: appId==0 selects PICC; appId>0 selects that application.
                    cmd.selectApplication(resolved.AppId);

                    // Authenticate with required key number (master key or target key).
                    cmd.authenticate(resolved.AuthKeyNo, authKey);

                    // Change the requested key number to the new value.
                    cmd.changeKey(resolved.TargetKeyNo, newKey);

                    return ERROR.NoError;
                }
                catch (Exception e)
                {
                    // Keep your current mapping style but more focused.
                    if (!string.IsNullOrEmpty(e.Message) && e.Message.Contains("status does not allow the requested command"))
                        return ERROR.AuthFailure;

                    return ERROR.TransportError;
                }
            }
            catch
            {
                return ERROR.TransportError;
            }
        }

        /// <inheritdoc/>
        public override async Task<ERROR> ChangeMifareDesfireApplicationKeySettings(string _applicationMasterKeyCurrent, int _keyNumberCurrent, DESFireKeyType _keyTypeCurrent,
            int _appIDCurrent, AccessControl.DESFireKeySettings keySettings)
        {
            try
            {
                DESFireKey masterApplicationKey = new DESFireKey();
                masterApplicationKey.setKeyType((LibLogicalAccess.Card.DESFireKeyType)_keyTypeCurrent);
                CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_applicationMasterKeyCurrent);
                masterApplicationKey.fromString(CustomConverter.DesfireKeyToCheck);

                readerUnit.disconnectFromReader();

                if (await tryInitReader())
                {
                    card = readerUnit.getSingleChip();

                    if (card.getCardType() == "DESFire" ||
                        card.getCardType() == "DESFireEV1" ||
                        card.getCardType() == "DESFireEV2" ||
                        card.getCardType() == "DESFireEV3")
                    {
                        var cmd = card.getCommands() as DESFireCommands;
                        var ev1Cmd = (card as DESFireEV1Chip).getCommands() as DESFireEV1ISO7816Commands;
                        var ev2Cmd = (card as DESFireEV1Chip).getCommands() as DESFireEV2ISO7816Commands;
                        var ev3Cmd = (card as DESFireEV1Chip).getCommands() as DESFireEV3ISO7816Commands;

                        try
                        {
                            cmd.selectApplication((uint)_appIDCurrent);
                            cmd.authenticate(0, masterApplicationKey);
                            cmd.changeKeySettings((LibLogicalAccess.Card.DESFireKeySettings)keySettings);

                            return ERROR.NoError;
                        }

                        catch (Exception e)
                        {
                            if (e.Message != "" && e.Message.Contains("same number already exists"))
                            {
                                return ERROR.ProtocolConstraint;
                            }
                            else if (e.Message != "" && e.Message.Contains("status does not allow the requested command"))
                            {
                                return ERROR.AuthFailure;
                            }
                            else
                            {
                                return ERROR.TransportError;
                            }
                        }
                    }
                    else
                        return ERROR.TransportError;
                }
                return ERROR.TransportError;
            }
            catch
            {
                return ERROR.TransportError;
            }
        }

        /// <inheritdoc />
        public override async Task<ERROR> DeleteMifareDesfireApplication(string _applicationMasterKey, DESFireKeyType _keyType, uint _appID = 0)
        {
            try
            {
                // The excepted memory tree
                DESFireLocation location = new DESFireLocation
                {
                    // The Application ID to use
                    aid = _appID,
                    // File communication requires encryption
                    securityLevel = LibLogicalAccess.Card.EncryptionMode.CM_ENCRYPT
                };

                // IDESFireEV1Commands cmd;
                // Keys to use for authentication
                DESFireAccessInfo aiToUse = new DESFireAccessInfo();
                CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_applicationMasterKey);
                aiToUse.masterCardKey.fromString(CustomConverter.DesfireKeyToCheck);
                aiToUse.masterCardKey.setKeyType((LibLogicalAccess.Card.DESFireKeyType)_keyType);

                if (await tryInitReader())
                {
                    card = readerUnit.getSingleChip();

                    if (card.getCardType() == "DESFire" ||
                        card.getCardType() == "DESFireEV1" ||
                        card.getCardType() == "DESFireEV2" ||
                        card.getCardType() == "DESFireEV3")
                    {
                        var cmd = card.getCommands() as DESFireCommands;
                        try
                        {
                            cmd.selectApplication(0);
                            cmd.authenticate(0, aiToUse.masterCardKey);

                            cmd.deleteApplication(_appID);
                            return ERROR.NoError;
                        }
                        catch
                        {
                            try
                            {
                                cmd.selectApplication(_appID);
                                cmd.authenticate(0, aiToUse.masterCardKey);
                                cmd.deleteApplication(_appID);
                                return ERROR.NoError;
                            }

                            catch (Exception e)
                            {
                                if (e.Message != "" && e.Message.Contains("same number already exists"))
                                {
                                    return ERROR.ProtocolConstraint;
                                }
                                else if (e.Message != "" && e.Message.Contains("status does not allow the requested command"))
                                {
                                    return ERROR.AuthFailure;
                                }
                                else
                                    return ERROR.TransportError;
                            }
                        }
                    }
                    return ERROR.TransportError;
                }
                return ERROR.TransportError;
            }
            catch
            {
                return ERROR.TransportError;
            }
        }

        /// <inheritdoc />
        public override async Task<ERROR> DeleteMifareDesfireFile(string _applicationMasterKey, DESFireKeyType _keyType, int _appID = 0, int _fileID = 0)
        {
            try
            {
                // The excepted memory tree
                DESFireLocation location = new DESFireLocation
                {
                    // The Application ID to use
                    aid = (uint)_appID,
                    // File communication requires encryption
                    securityLevel = LibLogicalAccess.Card.EncryptionMode.CM_ENCRYPT
                };

                // Keys to use for authentication
                DESFireAccessInfo aiToUse = new DESFireAccessInfo();
                CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_applicationMasterKey);
                aiToUse.masterCardKey.fromString(CustomConverter.DesfireKeyToCheck);
                aiToUse.masterCardKey.setKeyType((LibLogicalAccess.Card.DESFireKeyType)_keyType);

                if (await tryInitReader())
                {
                    card = readerUnit.getSingleChip();

                    if (card.getCardType() == "DESFireEV1" ||
                        card.getCardType() == "DESFireEV2" ||
                        card.getCardType() == "DESFire" ||
                        card.getCardType() == "DESFireEV3")
                    {
                        try
                        {
                            var cmd = card.getCommands() as DESFireCommands;

                            try
                            {
                                cmd.selectApplication((uint)_appID);
                                cmd.authenticate(0, aiToUse.masterCardKey);
                            }

                            catch
                            {
                                cmd.deleteFile((byte)_fileID);
                                return ERROR.NoError;
                            }

                            cmd.deleteFile((byte)_fileID);
                            return ERROR.NoError;
                        }
                        catch (Exception e)
                        {
                            if (e.Message != "" && e.Message.Contains("same number already exists"))
                            {
                                return ERROR.ProtocolConstraint;
                            }
                            else if (e.Message != "" && e.Message.Contains("status does not allow the requested command"))
                            {
                                return ERROR.AuthFailure;
                            }
                            else
                                return ERROR.TransportError;
                        }
                    }
                    else
                        return ERROR.TransportError;
                }
                return ERROR.TransportError;
            }
            catch
            {
                return ERROR.TransportError;
            }
        }

        /// <inheritdoc />
        public override async Task<ERROR> FormatDesfireCard(string _applicationMasterKey, DESFireKeyType _keyType)
        {
            try
            {
                // The excepted memory tree
                DESFireLocation location = new DESFireLocation
                {
                    // The Application ID to use
                    aid = 0,
                    // File communication requires encryption
                    securityLevel = LibLogicalAccess.Card.EncryptionMode.CM_ENCRYPT
                };

                // Keys to use for authentication
                DESFireAccessInfo aiToUse = new DESFireAccessInfo();
                CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_applicationMasterKey);
                aiToUse.masterCardKey.fromString(CustomConverter.DesfireKeyToCheck);
                aiToUse.masterCardKey.setKeyType((LibLogicalAccess.Card.DESFireKeyType)_keyType);

                if (await tryInitReader())
                {
                    card = readerUnit.getSingleChip();

                    if (card.getCardType() == "DESFire" ||
                       card.getCardType() == "DESFireEV1" ||
                       card.getCardType() == "DESFireEV2" ||
                       card.getCardType() == "DESFireEV3")
                    {
                        var cmd = card.getCommands() as DESFireCommands;
                        try
                        {
                            cmd.selectApplication(0);
                            cmd.authenticate(0, aiToUse.masterCardKey);

                            cmd.erase();

                            return ERROR.NoError;
                        }
                        catch (Exception e)
                        {
                            if (e.Message != "" && e.Message.Contains("same number already exists"))
                            {
                                return ERROR.ProtocolConstraint;
                            }
                            else if (e.Message != "" && e.Message.Contains("status does not allow the requested command"))
                            {
                                return ERROR.AuthFailure;
                            }
                            else
                                return ERROR.TransportError;
                        }
                    }
                    else
                        return ERROR.TransportError;
                }
                return ERROR.TransportError;
            }
            catch
            {
                return ERROR.TransportError;
            }
        }

        /// <inheritdoc />
        public override async Task<ERROR> GetMifareDesfireFileList(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumberCurrent = 0, int _appID = 0)
        {
            try
            {
                // The excepted memory tree
                DESFireLocation location = new DESFireLocation
                {
                    // The Application ID to use
                    aid = (uint)_appID,
                    // File communication requires encryption
                    securityLevel = LibLogicalAccess.Card.EncryptionMode.CM_ENCRYPT
                };

                //IDESFireEV1Commands cmd;
                // Keys to use for authentication
                DESFireAccessInfo aiToUse = new DESFireAccessInfo();
                CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_applicationMasterKey);
                aiToUse.masterCardKey.fromString(CustomConverter.DesfireKeyToCheck);
                aiToUse.masterCardKey.setKeyType((LibLogicalAccess.Card.DESFireKeyType)_keyType);

                ByteVector fileIDsObject;

                if (await tryInitReader())
                {
                    card = readerUnit.getSingleChip();

                    if (card.getCardType() == "DESFire" ||
                       card.getCardType() == "DESFireEV1" ||
                       card.getCardType() == "DESFireEV2" ||
                       card.getCardType() == "DESFireEV3")
                    {
                        var cmd = card.getCommands() as DESFireCommands;
                        try
                        {
                            cmd.selectApplication((uint)_appID);
                            try
                            {
                                cmd.authenticate((byte)_keyNumberCurrent, aiToUse.masterCardKey);
                            }
                            catch
                            {
                                try
                                {
                                    fileIDsObject = cmd.getFileIDs();
                                    FileIDList = fileIDsObject.ToArray();
                                    return ERROR.NoError;
                                }
                                catch (Exception e)
                                {
                                    if (e.Message != "" && e.Message.Contains("same number already exists"))
                                    {
                                        return ERROR.ProtocolConstraint;
                                    }
                                    else if (e.Message != "" && e.Message.Contains("status does not allow the requested command"))
                                    {
                                        return ERROR.AuthFailure;
                                    }
                                    else
                                        return ERROR.TransportError;
                                }
                            }

                            fileIDsObject = cmd.getFileIDs();
                            FileIDList = fileIDsObject.ToArray();

                            return ERROR.NoError;
                        }
                        catch (Exception e)
                        {
                            if (e.Message != "" && e.Message.Contains("same number already exists"))
                            {
                                return ERROR.ProtocolConstraint;
                            }
                            else if (e.Message != "" && e.Message.Contains("status does not allow the requested command"))
                            {
                                return ERROR.AuthFailure;
                            }
                            else
                                return ERROR.TransportError;
                        }
                    }
                    else
                        return ERROR.TransportError;
                }
                return ERROR.TransportError;
            }
            catch
            {
                return ERROR.TransportError;
            }
        }

        /// <inheritdoc />
        public override async Task<ERROR> GetMifareDesfireFileSettings(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumberCurrent = 0, int _appID = 0, int _fileNo = 0)
        {
            try
            {
                // Keys to use for authentication
                DESFireAccessInfo aiToUse = new DESFireAccessInfo();
                CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_applicationMasterKey);
                aiToUse.masterCardKey.fromString(CustomConverter.DesfireKeyToCheck);
                aiToUse.masterCardKey.setKeyType((LibLogicalAccess.Card.DESFireKeyType)_keyType);

                DESFireCommands.FileSetting fsFromChip;

                if (await tryInitReader())
                {
                    card = readerUnit.getSingleChip();

                    if (card.getCardType() == "DESFire" ||
                        card.getCardType() == "DESFireEV1" ||
                        card.getCardType() == "DESFireEV2" ||
                        card.getCardType() == "DESFireEV3")
                    {
                        var cmd = card.getCommands() as DESFireCommands;
                        try
                        {
                            cmd.selectApplication((uint)_appID);
                            try
                            {
                                cmd.authenticate((byte)_keyNumberCurrent, aiToUse.masterCardKey);
                            }
                            catch
                            {
                                try
                                {
                                    fsFromChip = cmd.getFileSettings((byte)_fileNo);
                                    DesfireFileSettings = new DESFireFileSettings
                                    {
                                        accessRights = fsFromChip.accessRights,
                                        comSett = fsFromChip.comSett,
                                        //dataFile = fs.getDataFile(),
                                        FileType = fsFromChip.fileType
                                    };

                                    return ERROR.NoError;
                                }
                                catch (Exception e)
                                {
                                    if (e.Message != "" && e.Message.Contains("same number already exists"))
                                    {
                                        return ERROR.ProtocolConstraint;
                                    }
                                    else if (e.Message != "" && e.Message.Contains("status does not allow the requested command"))
                                    {
                                        return ERROR.AuthFailure;
                                    }
                                    else
                                        return ERROR.TransportError;
                                }
                            }

                            fsFromChip = cmd.getFileSettings((byte)_fileNo);
                            DesfireFileSettings = new DESFireFileSettings
                            {
                                accessRights = fsFromChip.accessRights,
                                comSett = fsFromChip.comSett,
                                //dataFile = fs.getDataFile(),
                                FileType = fsFromChip.fileType
                            };

                            return ERROR.NoError;
                        }
                        catch (Exception e)
                        {
                            if (e.Message != "" && e.Message.Contains("same number already exists"))
                            {
                                return ERROR.ProtocolConstraint;
                            }
                            else if (e.Message != "" && e.Message.Contains("status does not allow the requested command"))
                            {
                                return ERROR.AuthFailure;
                            }
                            else
                                return ERROR.TransportError;
                        }
                    }
                    else
                        return ERROR.TransportError;
                }
                return ERROR.TransportError;
            }
            catch
            {
                return ERROR.TransportError;
            }
        }

        /// <inheritdoc />
        public override Task<byte> MifareDesfire_GetKeyVersionAsync(byte keyNo)
        {
            throw new NotSupportedException("Retrieving key versions is not supported for LibLogicalAccessProvider.");
        }

        #endregion mifare desfire

        #region mifare plus

        #endregion

        private async Task<bool> tryInitReader()
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (readerUnit?.isConnected() == true)
                    {
                        readerUnit.connect();
                        return true;
                    }
                    else if (readerUnit?.connectToReader() == true)
                    {
                        if (readerUnit.waitInsertion(Constants.MAX_WAIT_INSERTION))
                        {
                            if (readerUnit?.connect() == true)
                            {
                                ReaderUnitName = ReaderUnitName ?? readerUnit.getConnectedName();

                                return true;
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    return false;
                }

                return false;
            });
        }

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
        private bool _disposed;
    }
}
