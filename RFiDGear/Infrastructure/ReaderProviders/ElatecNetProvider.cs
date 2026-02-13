//using Elatec.NET.Model;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using ByteArrayHelper.Extensions;
using Elatec.NET;
using RFiDGear.Models;
using RFiDGear.Infrastructure.Tasks;
using Elatec.NET.Cards.Mifare;
using Serilog;

namespace RFiDGear.Infrastructure.ReaderProviders
{
    public class ElatecNetProvider : ReaderDevice, IDisposable
    {
        private TWN4ReaderDevice readerDevice;
        private readonly byte DESFIRE_AUTHMODE_COMPATIBLE = 0;
        private readonly byte DESFIRE_AUTHMODE_EV1 = 1;

        private GenericChipModel hfTag;
        private GenericChipModel lfTag;
        private GenericChipModel legicTag;

        private bool _disposed;

        #region Constructor

        private async Task Initialize()
        {
            GenericChip ??= new GenericChipModel();

            try
            {
                var readerList = TWN4ReaderDevice.Instance;

                if (readerList != null && readerList.Count > 0)
                {
                    readerDevice = readerList.FirstOrDefault();

                    if (readerDevice.IsConnected)
                    {
                        ReaderUnitVersion = await readerDevice.GetVersionStringAsync();
                    }
                    else if (readerDevice != null && readerDevice.AvailableReadersCount >= 1 && !readerDevice.IsConnected)
                    {
                        if (await readerDevice.ConnectAsync())
                        {
                            ReaderUnitVersion = await readerDevice.GetVersionStringAsync();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.ForContext<ElatecNetProvider>().Error(e, "Elatec operation failed.");
            }
        }

        public ElatecNetProvider()
        {
            GenericChip ??= new GenericChipModel();
        }

        #endregion

        #region Common

        /// <summary>
        /// 
        /// </summary>
        public override bool IsConnected => readerDevice?.IsConnected == true;

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public async override Task<ERROR> ReadChipPublic()
        {
            try
            {
                if (readerDevice == null)
                {
                    await Initialize();
                }

                else if (readerDevice != null)
                {

                    if (!readerDevice.IsConnected)
                    {
                        await readerDevice.ConnectAsync();
                    }

                    await readerDevice.SetTagTypesAsync(LFTagTypes.NOTAG, HFTagTypes.AllHFTags & ~HFTagTypes.LEGIC);
                    var tmpTag = await readerDevice.GetSingleChipAsync();

                    if (tmpTag != null && tmpTag.ChipType == ChipType.MIFARE)
                    {
                        if ((MifareChipSubType)((byte)(tmpTag as MifareChip).SubType & 0xF0) == MifareChipSubType.MifareClassic)
                        {
                            hfTag = new MifareClassicChipModel(tmpTag.UIDHexString, (CARD_TYPE)((short)(tmpTag as MifareChip).SubType << 8));
                        }
                        else if ((MifareChipSubType)((byte)(tmpTag as MifareChip).SubType & 0x40) == MifareChipSubType.DESFire)
                        {
                            hfTag = new MifareDesfireChipModel(tmpTag.UIDHexString, (CARD_TYPE)((short)(tmpTag as MifareChip).SubType << 8),
                                    ByteArrayConverter.GetStringFrom((tmpTag as MifareChip).SAK),
                                    ByteArrayConverter.GetStringFrom((tmpTag as MifareChip).ATS),
                                    ByteArrayConverter.GetStringFrom((tmpTag as MifareChip).VersionL4));
                        }
                        else
                        {
                            hfTag = new GenericChipModel(tmpTag.UIDHexString, (CARD_TYPE)((short)(tmpTag as MifareChip).SubType << 8));
                        }
                    }
                    else if (tmpTag == null)
                    {
                        hfTag = null;
                    }

                    await readerDevice.SetTagTypesAsync(LFTagTypes.AllLFTags, HFTagTypes.NOTAG);
                    tmpTag = await readerDevice.GetSingleChipAsync();
                    lfTag = tmpTag != null ? new GenericChipModel(tmpTag.UIDHexString, (CARD_TYPE)tmpTag.ChipType) : null;

                    await readerDevice.SetTagTypesAsync(LFTagTypes.NOTAG, HFTagTypes.LEGIC);
                    tmpTag = await readerDevice.GetSingleChipAsync();
                    legicTag = tmpTag != null ? new GenericChipModel(tmpTag.UIDHexString, (CARD_TYPE)tmpTag.ChipType) : null;

                    await readerDevice.SetTagTypesAsync(LFTagTypes.NOTAG, HFTagTypes.AllHFTags);

                    if (!string.IsNullOrWhiteSpace(hfTag?.UID) && GenericChip.UID == hfTag.UID)
                    {
                        return ERROR.NoError;
                    }

                    if (!string.IsNullOrWhiteSpace(hfTag?.UID) && !(GenericChip.UID == hfTag.UID))
                    {
                        if (!string.IsNullOrWhiteSpace(hfTag?.UID))
                        {
                            GenericChip = hfTag;
                        }
                    }

                    if (hfTag != null && !string.IsNullOrEmpty(lfTag?.UID))
                    {
                        if (GenericChip.Childs == null)
                        {
                            GenericChip.Childs = new List<GenericChipModel>();
                        }

                        GenericChip.Childs.Add(lfTag);
                    }

                    else if (!string.IsNullOrEmpty(lfTag?.UID))
                    {
                        GenericChip = lfTag;
                    }

                    if (hfTag != null && !string.IsNullOrEmpty(legicTag?.UID))
                    {
                        if (GenericChip.Childs == null)
                        {
                            GenericChip.Childs = new List<GenericChipModel>();
                        }

                        GenericChip.Childs.Add(legicTag);
                    }

                    else if (!string.IsNullOrEmpty(legicTag?.UID))
                    {
                        GenericChip = legicTag;
                    }
                }

                if (hfTag == null && lfTag == null && legicTag == null)
                {
                    GenericChip = new GenericChipModel();
                }

                return ERROR.NoError;
            }
            catch (Exception e)
            {
                Log.ForContext<ElatecNetProvider>().Error(e, "Elatec operation failed.");

                return ERROR.TransportError;
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public async override Task<ERROR> ConnectAsync()
        {

            readerDevice = TWN4ReaderDevice.Instance.FirstOrDefault();

            if (readerDevice == null)
            {
                return ERROR.TransportError;
            }

            if (readerDevice != null && !readerDevice.IsConnected)
            {
                var result = false;

                if (!readerDevice.IsConnected)
                {
                    result = await readerDevice.ConnectAsync();
                }

                if (result)
                {
                    ReaderUnitVersion = await readerDevice.GetVersionStringAsync();
                    return ERROR.NoError;
                }
            }

            return ERROR.TransportError;
        }

        #endregion

        #region MifareClassic

        /// <inheritdoc />
        public async override Task<ERROR> WriteMifareClassicSingleBlock(int _blockNumber, string aKey, string bKey, byte[] buffer)
        {
            try
            {
                await readerDevice.MifareClassic_LoginAsync(aKey, 0, (byte)CustomConverter.GetSectorNumberFromChipBasedDataBlockNumber(_blockNumber));
            }
            catch
            {
                try
                {
                    await readerDevice.MifareClassic_LoginAsync(bKey, 1, (byte)CustomConverter.GetSectorNumberFromChipBasedDataBlockNumber(_blockNumber));
                }
                catch
                {
                    try
                    {
                        await readerDevice.MifareClassic_WriteBlockAsync(buffer, (byte)_blockNumber);
                    }
                    catch
                    {
                        return ERROR.AuthFailure;
                    }
                }
            } // Login  
            return ERROR.NoError;
        }

        /// <inheritdoc />
        public async override Task<ERROR> ReadMifareClassicSingleSector(int sectorNumber, string aKey, string bKey)
        {
            return await ReadWriteAccessOnClassicSector(sectorNumber, aKey, bKey, null);
        }

        /// <inheritdoc />
        public async override Task<ERROR> WriteMifareClassicSingleSector(int sectorNumber, string aKey, string bKey, byte[] buffer)
        {
            return await ReadWriteAccessOnClassicSector(sectorNumber, aKey, bKey, buffer);
        }


        public override Task<ERROR> WriteMifareClassicWithMAD(int _madApplicationID, int _madStartSector,
            string _aKeyToUse, string _bKeyToUse, string _aKeyToWrite, string _bKeyToWrite,
            string _madAKeyToUse, string _madBKeyToUse, string _madAKeyToWrite,
            string _madBKeyToWrite, byte[] buffer, byte _madGPB, AccessControl.SectorAccessBits _sab,
            bool _useMADToAuth, bool _keyToWriteUseMAD)
        {
            throw new NotImplementedException();
        }

        public override Task<ERROR> ReadMifareClassicWithMAD(int madApplicationID, string _aKeyToUse,
            string _bKeyToUse, string _madAKeyToUse, string _madBKeyToUse, int _length, byte _madGPB,
            bool _useMADToAuth, bool _aiToUseIsMAD)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sectorNumber"></param>
        /// <param name="aKey"></param>
        /// <param name="bKey"></param>
        /// <param name="buffer"></param>
        /// <returns></returns>
        private async Task<ERROR> ReadWriteAccessOnClassicSector(int sectorNumber, string aKey, string bKey, byte[] buffer)
        {
            if (!readerDevice.IsConnected)
            {
                return ERROR.TransportError;
            }

            if (readerDevice.IsTWN4LegicReader)
            {
                try
                {
                    await readerDevice.SearchTagAsync();
                }
                catch { }
            }

            Sector = new MifareClassicSectorModel();

            var elatecSpecificSectorNumber = sectorNumber > 31 ? (sectorNumber - 32) * 4 + 32 : sectorNumber; // elatec uses special sectornumbers

            for (byte k = 0; k < (sectorNumber > 31 ? 16 : 4); k++) // if sector > 31 is 16 blocks each sector i.e. mifare 4k else its 1k or 2k with 4 blocks each sector
            {
                DataBlock = new MifareClassicDataBlockModel(
                    (byte)CustomConverter.GetChipBasedDataBlockNumber(sectorNumber, k),
                    k);

                try
                {
                    var isAuth = false;

                    try
                    {
                        await readerDevice.MifareClassic_LoginAsync(aKey, 0, (byte)elatecSpecificSectorNumber);
                        isAuth = true;
                    }
                    catch
                    {
                        isAuth = false;
                    }

                    if (buffer == null || buffer.Length != 16 && isAuth) // Read Mode
                    {
                        try
                        {
                            var data = await readerDevice.MifareClassic_ReadBlockAsync((byte)CustomConverter.GetChipBasedDataBlockNumber(sectorNumber, k));

                            if (data.Length > 1)
                            {
                                DataBlock.Data = data;
                                DataBlock.IsAuthenticated = true;
                                Sector.IsAuthenticated = isAuth;
                                Sector.DataBlock.Add(DataBlock);
                            }
                        }
                        catch // No Read Access Allowed, try bKey
                        {
                            try
                            {
                                await readerDevice.MifareClassic_LoginAsync(bKey, 1, (byte)elatecSpecificSectorNumber);
                                isAuth = true;

                                var data = await readerDevice.MifareClassic_ReadBlockAsync((byte)CustomConverter.GetChipBasedDataBlockNumber(sectorNumber, k));

                                if (data.Length > 1)
                                {
                                    DataBlock.Data = data;
                                    DataBlock.IsAuthenticated = true;
                                    Sector.IsAuthenticated = isAuth;
                                    Sector.DataBlock.Add(DataBlock);
                                }
                            }
                            catch
                            {
                                isAuth = false;
                                Sector.IsAuthenticated = false;
                                DataBlock.IsAuthenticated = false; // finally failed to read data
                            }
                        }
                    } // read Data

                    else if (buffer != null && buffer.Length == 16)
                    {
                        try
                        {
                            await readerDevice.MifareClassic_WriteBlockAsync(buffer, k);
                            return ERROR.NoError;
                        }
                        catch
                        {
                            return ERROR.AuthFailure;
                        }
                    } // write Data
                }
                catch
                {
                    return ERROR.TransportError; // IO ElatecError
                }
            }

            if (Sector.IsAuthenticated)
            {
                return ERROR.NoError; //NO ElatecError
            }

            return ERROR.AuthFailure; // Auth ElatecError
        }

        #endregion

        #region MifareUltralight
        public override Task<ERROR> ReadMifareUltralightSinglePage(int _pageNo)
        {
            throw new NotImplementedException();
        }
        #endregion

        #region MifareDesfire

        /// <inheritdoc />
        public async override Task<byte> MifareDesfire_GetKeyVersionAsync(byte keyNo)
        {
            if (readerDevice == null || !readerDevice.IsConnected)
            {
                throw new ReaderException("Reader not connected");
            }

            try
            {
                var keyVersion = await readerDevice.MifareDesfire_GetKeyVersionAsync(keyNo);
                KeyVersion = keyVersion;
                return keyVersion;
            }
            catch (Exception e)
            {
                Log.ForContext<ElatecNetProvider>().Error(e, "Unable to read DESFire key version.");
                throw;
            }
        }

        /// <inheritdoc />
        public async override Task<ERROR> GetMiFareDESFireChipAppIDs(string _appMasterKey, DESFireKeyType _keyTypeAppMasterKey)
        {
            if (readerDevice.IsConnected)
            {
                uint[] appArr;

                DesfireChip ??= new MifareDesfireChipModel();

                DesfireChip.AppList = new List<MifareDesfireAppModel>();

                try
                {
                    await readerDevice.SearchTagAsync();
                    await readerDevice.MifareDesfire_SelectApplicationAsync(0);
                    DesfireChip.FreeMemory = await readerDevice.MifareDesfire_GetFreeMemoryAsync();
                }
                catch
                {
                    DesfireChip.FreeMemory = 0;
                }

                try
                {
                    appArr = await readerDevice.MifareDesfire_GetAppIDsAsync();

                    if (appArr != null)
                    {
                        foreach (var appid in appArr)
                        {
                            DesfireChip.AppList.Add(new MifareDesfireAppModel(appid));
                        }
                    }
                }
                catch
                {
                    return ERROR.AuthFailure;
                }

                return ERROR.NoError;
            }

            else
            {
                return ERROR.TransportError;
            }

        }

        /// <inheritdoc />
        public async override Task<ERROR> AuthToMifareDesfireApplication(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumber, int _appID)
        {
            if (readerDevice.IsConnected)
            {
                if (readerDevice.IsTWN4LegicReader)
                {
                    try
                    {
                        await readerDevice.SearchTagAsync();
                    }
                    catch { }
                }

                try
                {
                    await readerDevice.MifareDesfire_SelectApplicationAsync((uint)_appID);

                    await readerDevice.MifareDesfire_AuthenticateAsync(
                        _applicationMasterKey,
                        (byte)_keyNumber,
                        (byte)(int)Enum.Parse(typeof(Elatec.NET.Cards.Mifare.DESFireKeyType), Enum.GetName(typeof(DESFireKeyType), _keyType)),
                        DESFIRE_AUTHMODE_EV1);
                    return ERROR.NoError;
                }
                catch
                {
                    return ERROR.AuthFailure;
                }
            }

            else
            {
                return ERROR.TransportError;
            }

        }

        /// <inheritdoc />
        public async override Task<OperationResult> GetMifareDesfireAppSettings(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumberCurrent, int _appID, bool authenticateBeforeReading = true)
        {
            if (readerDevice.IsConnected)
            {
                if (readerDevice.IsTWN4LegicReader)
                {
                    try
                    {
                        await readerDevice.SearchTagAsync();
                    }
                    catch { }
                }

                try
                {
                    await readerDevice.MifareDesfire_SelectApplicationAsync((uint)_appID);

                    if (authenticateBeforeReading)
                    {
                        await readerDevice.MifareDesfire_AuthenticateAsync(_applicationMasterKey, (byte)_keyNumberCurrent, (byte)(int)Enum.Parse(typeof(Elatec.NET.Cards.Mifare.DESFireKeyType), Enum.GetName(typeof(DESFireKeyType), _keyType)), DESFIRE_AUTHMODE_EV1);
                    }

                    var ks = await readerDevice.MifareDesfire_GetKeySettingsAsync();

                    MaxNumberOfAppKeys = (byte)ks.NumberOfKeys;
                    EncryptionType = ResolveDesfireKeyType(ks.KeyType.ToString(), _keyType);
                    DesfireAppKeySetting = (AccessControl.DESFireKeySettings)ks.AccessRights;

                    var metadata = new Dictionary<string, string>
                    {
                        { "ApplicationId", _appID.ToString(CultureInfo.CurrentCulture) },
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
                        "Reading application settings timed out",
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
                        "Reader denied access while fetching application settings",
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
                    var code = ERROR.TransportError;
                    if (e.Message.Contains("AUTH") || e.Message.Contains("auth", StringComparison.InvariantCultureIgnoreCase))
                    {
                        code = ERROR.AuthFailure;
                    }

                    return OperationResult.Failure(
                        code,
                        "Failed to read application settings",
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

            else
            {
                return OperationResult.Failure(
                    ERROR.TransportError,
                    "Reader not connected",
                    operation: nameof(GetMifareDesfireAppSettings),
                    metadata: new Dictionary<string, string>
                    {
                        { "ApplicationId", _appID.ToString(CultureInfo.CurrentCulture) },
                        { "KeyNumber", _keyNumberCurrent.ToString(CultureInfo.CurrentCulture) }
                    });
            }

        }

        /// <inheritdoc />
        public async override Task<OperationResult> CreateMifareDesfireApplication(string _piccMasterKey, AccessControl.DESFireKeySettings _keySettingsTarget,
                                        DESFireKeyType _keyTypePiccMasterKey, DESFireKeyType _keyTypeTargetApplication,
                                        int _maxNbKeys, int _appID, bool authenticateToPICCFirst = true)
        {
            if (readerDevice.IsConnected)
            {
                if (readerDevice.IsTWN4LegicReader)
                {
                    try
                    {
                        await readerDevice.SearchTagAsync();
                    }
                    catch { }
                }

                try
                {
                    await readerDevice.MifareDesfire_SelectApplicationAsync(0);

                    if (authenticateToPICCFirst)
                    {
                        await readerDevice.MifareDesfire_AuthenticateAsync(_piccMasterKey, 0, (byte)(int)Enum.Parse(typeof(Elatec.NET.Cards.Mifare.DESFireKeyType), Enum.GetName(typeof(DESFireKeyType), _keyTypePiccMasterKey)), 1);
                    }

                    await readerDevice.MifareDesfire_CreateApplicationAsync(
                                                (DESFireAppAccessRights)_keySettingsTarget,
                                                (Elatec.NET.Cards.Mifare.DESFireKeyType)Enum.Parse(typeof(Elatec.NET.Cards.Mifare.DESFireKeyType), Enum.GetName(typeof(DESFireKeyType), _keyTypeTargetApplication)),
                                                _maxNbKeys,
                                                _appID);
                    var metadata = new Dictionary<string, string>
                    {
                        { "ApplicationId", _appID.ToString(CultureInfo.CurrentCulture) },
                        { "MaxKeys", _maxNbKeys.ToString(CultureInfo.CurrentCulture) },
                        { "AuthenticateToPICCFirst", authenticateToPICCFirst.ToString(CultureInfo.CurrentCulture) }
                    };

                    return OperationResult.Success(
                        operation: nameof(CreateMifareDesfireApplication),
                        wasAuthenticated: authenticateToPICCFirst,
                        metadata: metadata);
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
                        "Reader denied access while creating application",
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
                    var code = e.Message.Contains("AUTH", StringComparison.InvariantCultureIgnoreCase) ? ERROR.AuthFailure : ERROR.TransportError;
                    return OperationResult.Failure(
                        code,
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

            else
            {
                return OperationResult.Failure(
                    ERROR.TransportError,
                    "Reader not connected",
                    operation: nameof(CreateMifareDesfireApplication),
                    metadata: new Dictionary<string, string>
                    {
                        { "ApplicationId", _appID.ToString(CultureInfo.CurrentCulture) },
                        { "MaxKeys", _maxNbKeys.ToString(CultureInfo.CurrentCulture) }
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
            byte newTargetKeyVersion,
            string masterKeyHex,
            DESFireKeyType masterKeyType,
            AccessControl.DESFireKeySettings keySettings)
        {
            if (!readerDevice.IsConnected)
                return ERROR.TransportError;

            // Some TWN4 LEGIC-capable devices require a tag inventory kick before certain operations.
            if (readerDevice.IsTWN4LegicReader)
            {
                try { await readerDevice.SearchTagAsync(); }
                catch { /* ignore; best-effort workaround */ }
            }

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

                // Scope selection: appId==0 selects PICC; appId>0 selects that application.
                await readerDevice.MifareDesfire_SelectApplicationAsync(resolved.AppId);

                // Authenticate according to the change-key policy (key 0 or target key).
                await readerDevice.MifareDesfire_AuthenticateAsync(
                    resolved.AuthKeyHex,
                    resolved.AuthKeyNo,
                    (byte)ToElatecKeyType(resolved.AuthKeyType),
                    DESFIRE_AUTHMODE_EV1);

                // Some reader APIs require key count/key type context to issue ChangeKey.
                // Prefer reading live values; fall back to configured or conservative defaults.
                byte keyCount;
                DESFireKeyType keyTypeForContext = resolved.TargetKeyType;

                try
                {
                    var ks = await readerDevice.MifareDesfire_GetKeySettingsAsync();
                    keyCount = (byte)ks.NumberOfKeys;

                    // Optional: keep provider state synced if other operations depend on it.
                    MaxNumberOfAppKeys = keyCount;

                    // Try map the returned key type to your enum by name; if it fails, keep the requested targetKeyType.
                    if (Enum.TryParse<DESFireKeyType>(ks.KeyType.ToString(), out var parsed))
                        keyTypeForContext = ResolveKeyTypeForChange(resolved.AppId, resolved.TargetKeyType, parsed);
                }
                catch
                {
                    // PICC level is always 1 "master key slot" in your existing logic.
                    // For applications, prefer MaxNumberOfAppKeys if set, otherwise use 15 as a conservative default.
                    keyCount = resolved.AppId == 0
                        ? (byte)1
                        : (MaxNumberOfAppKeys > 0 ? MaxNumberOfAppKeys : (byte)15);
                }

                await readerDevice.MifareDesfire_ChangeKeyAsync(
                    resolved.OldTargetKeyHex,
                    resolved.NewTargetKeyHex,
                    resolved.NewTargetKeyVersion,
                    resolved.KeySettingsByteOnWire,
                    resolved.TargetKeyNo,
                    (uint)keyCount,
                    ToElatecKeyType(keyTypeForContext));

                return ERROR.NoError;
            }
            catch
            {
                // You may want a richer mapping (auth vs transport vs protocol) later.
                return ERROR.AuthFailure;
            }

            /// <summary>
            /// Converts your local DESFireKeyType enum to the Elatec wrapper enum.
            /// Uses name-based parsing to avoid brittle numeric casts when enum values differ.
            /// </summary>
            static Elatec.NET.Cards.Mifare.DESFireKeyType ToElatecKeyType(DESFireKeyType t)
            {
                if (Enum.TryParse<Elatec.NET.Cards.Mifare.DESFireKeyType>(t.ToString(), out var mapped))
                    return mapped;

                throw new ArgumentOutOfRangeException(nameof(t), $"No Elatec key type mapping for {t}.");
            }
        }

        /// <inheritdoc />
        public override async Task<ERROR> CommitTransactionAsync()
        {
            if (!readerDevice.IsConnected)
                return ERROR.TransportError;

            try
            {
                await readerDevice.MifareDesfire_CommitTransactionAsync();
                return ERROR.NoError;
            }
            catch
            {
                return ERROR.AuthFailure;
            }
        }

        /// <summary>
        /// Resolves the key type to use when issuing a DESFire ChangeKey command via the Elatec API.
        /// </summary>
        /// <param name="appId">Application identifier (0 = PICC, &gt;0 = application).</param>
        /// <param name="targetKeyType">Desired target key type from the UI.</param>
        /// <param name="detectedKeyType">Detected key type from the card, if available.</param>
        /// <returns>The key type to pass to the reader API for ChangeKey.</returns>
        internal static DESFireKeyType ResolveKeyTypeForChange(uint appId, DESFireKeyType targetKeyType, DESFireKeyType? detectedKeyType)
        {
            if (appId == 0)
            {
                return targetKeyType;
            }

            return detectedKeyType ?? targetKeyType;
        }

        /// <summary>
        /// Maps a provider-reported key type name to the local DESFire key type enum.
        /// </summary>
        /// <param name="providerKeyTypeName">Name reported by the provider for the key type.</param>
        /// <param name="fallback">Fallback when the name is not recognized.</param>
        /// <returns>The resolved DESFire key type.</returns>
        internal static DESFireKeyType ResolveDesfireKeyType(string providerKeyTypeName, DESFireKeyType fallback)
        {
            if (Enum.TryParse<DESFireKeyType>(providerKeyTypeName, out var parsed))
            {
                return parsed;
            }

            return fallback;
        }

        /// <inheritdoc />
        public async override Task<ERROR> ChangeMifareDesfireApplicationKeySettings(string _applicationMasterKeyCurrent, int _keyNumberCurrent, DESFireKeyType _keyTypeCurrent,
           int _appIDCurrent, AccessControl.DESFireKeySettings keySettings)
        {
            if (readerDevice.IsConnected)
            {
                if (readerDevice.IsTWN4LegicReader)
                {
                    try
                    {
                        await readerDevice.SearchTagAsync();
                    }
                    catch { }
                }

                try
                {
                    await readerDevice.MifareDesfire_SelectApplicationAsync((uint)_appIDCurrent);

                    await readerDevice.MifareDesfire_AuthenticateAsync(_applicationMasterKeyCurrent, 0, (byte)(int)Enum.Parse(typeof(Elatec.NET.Cards.Mifare.DESFireKeyType), Enum.GetName(typeof(DESFireKeyType), _keyTypeCurrent)), DESFIRE_AUTHMODE_EV1);

                    byte? cardKeyCount = null;
                    DESFireKeyType? cardKeyType = null;

                    try
                    {
                        var desfireSettings = await readerDevice.MifareDesfire_GetKeySettingsAsync();
                        cardKeyCount = (byte)desfireSettings.NumberOfKeys;
                        MaxNumberOfAppKeys = cardKeyCount.Value;

                        cardKeyType = ResolveDesfireKeyType(desfireSettings.KeyType.ToString(), _keyTypeCurrent);
                        EncryptionType = cardKeyType.Value;
                    }
                    catch (Exception e)
                    {
                        Log.ForContext<ElatecNetProvider>().Warning(e, "Unable to read DESFire key settings after authentication.");
                    }

                    var configuredKeyType = EncryptionType;
                    if (configuredKeyType == default && _keyTypeCurrent != default)
                    {
                        configuredKeyType = _keyTypeCurrent;
                    }

                    var resolvedSettings = DesfireKeySettingsResolver.Resolve(
                        keySettings,
                        cardKeyCount,
                        cardKeyType,
                        MaxNumberOfAppKeys,
                        configuredKeyType);

                    foreach (var warning in resolvedSettings.Warnings)
                    {
                        Log.ForContext<ElatecNetProvider>().Warning("{WarningMessage}", warning);
                    }

                    MaxNumberOfAppKeys = resolvedSettings.KeyCount;
                    EncryptionType = resolvedSettings.KeyType;

                    await readerDevice.MifareDesfire_ChangeKeySettingsAsync(
                        (DESFireAppAccessRights)resolvedSettings.Settings,
                        resolvedSettings.KeyCount,
                        (Elatec.NET.Cards.Mifare.DESFireKeyType)Enum.Parse(
                            typeof(Elatec.NET.Cards.Mifare.DESFireKeyType),
                            Enum.GetName(typeof(DESFireKeyType), resolvedSettings.KeyType)
                            )
                        );

                    return ERROR.NoError;
                }
                catch
                {
                    return ERROR.AuthFailure;
                }
            }

            else
            {
                return ERROR.TransportError;
            }
        }

        /// <inheritdoc />
        public async override Task<ERROR> DeleteMifareDesfireApplication(string _applicationMasterKey, DESFireKeyType _keyTypePiccMasterKey, uint _appID)
        {
            if (readerDevice.IsConnected)
            {
                if (readerDevice.IsTWN4LegicReader)
                {
                    try
                    {
                        await readerDevice.SearchTagAsync();
                    }
                    catch
                    {
                        return ERROR.TransportError;
                    }
                }

                try
                {
                    if (await AuthToMifareDesfireApplication(_applicationMasterKey, _keyTypePiccMasterKey, 0, 0) == ERROR.NoError)
                    {
                        await readerDevice.MifareDesfire_DeleteApplicationAsync(_appID);
                    }
                    else
                    {
                        await readerDevice.MifareDesfire_DeleteApplicationAsync(_appID);
                    }
                }
                catch
                {
                    return ERROR.AuthFailure;
                }
                return ERROR.NoError;
            }

            else
            {
                return ERROR.TransportError;
            }

        }

        /// <inheritdoc />
        public async override Task<ERROR> DeleteMifareDesfireFile(string _applicationMasterKey, DESFireKeyType _keyType, int _appID, int _fileID)
        {
            if (readerDevice.IsConnected)
            {
                if (readerDevice.IsTWN4LegicReader)
                {
                    try
                    {
                        await readerDevice.SearchTagAsync();
                    }
                    catch
                    {
                        return ERROR.TransportError;
                    }
                }

                try
                {
                    if (await AuthToMifareDesfireApplication(_applicationMasterKey, _keyType, 0, _appID) == ERROR.NoError)
                    {
                        await readerDevice.MifareDesfire_DeleteFileAsync((byte)_fileID);
                    }
                    else
                    {
                        return ERROR.AuthFailure;
                    }
                }
                catch
                {
                    return ERROR.AuthFailure;
                }
                return ERROR.NoError;
            }

            else
            {
                return ERROR.TransportError;
            }
        }

        /// <inheritdoc />
        public async override Task<ERROR> FormatDesfireCard(string _applicationMasterKey, DESFireKeyType _keyType)
        {
            if (readerDevice.IsConnected)
            {
                if (readerDevice.IsTWN4LegicReader)
                {
                    try
                    {
                        await readerDevice.SearchTagAsync();
                    }
                    catch
                    {
                        return ERROR.TransportError;
                    }
                }

                try
                {
                    if (await AuthToMifareDesfireApplication(_applicationMasterKey, _keyType, 0, 0) == ERROR.NoError)
                    {
                        await readerDevice.MifareDesfire_FormatTagAsync();
                    }
                    else
                    {
                        return ERROR.AuthFailure;
                    }
                }

                catch
                {
                    return ERROR.AuthFailure;
                }

                return ERROR.NoError;
            }

            else
            {
                return ERROR.TransportError;
            }


        }

        /// <inheritdoc />
        public async override Task<ERROR> GetMifareDesfireFileList(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumberCurrent, int _appID)
        {
            if (readerDevice.IsConnected)
            {
                if (readerDevice.IsTWN4LegicReader)
                {
                    try
                    {
                        await readerDevice.SearchTagAsync();
                    }
                    catch { }
                }

                try
                {
                    await readerDevice.MifareDesfire_SelectApplicationAsync((uint)_appID);

                    try
                    {
                        await readerDevice.MifareDesfire_AuthenticateAsync(_applicationMasterKey, (byte)_keyNumberCurrent, (byte)(int)Enum.Parse(typeof(Elatec.NET.Cards.Mifare.DESFireKeyType), Enum.GetName(typeof(DESFireKeyType), _keyType)), DESFIRE_AUTHMODE_EV1);
                    } // try to auth first.
                    catch
                    {

                    }

                    var fids = await readerDevice.MifareDesfire_GetFileIDsAsync();

                    if (fids != null)
                    {
                        FileIDList = fids;
                    }
                }
                catch
                {
                    return ERROR.TransportError;
                }

                return ERROR.NoError;
            }

            else
            {
                return ERROR.TransportError;
            }

        }

        /// <inheritdoc />
        public async override Task<ERROR> GetMifareDesfireFileSettings(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumberCurrent, int _appID, int _fileNo)
        {
            if (readerDevice.IsConnected)
            {
                if (readerDevice.IsTWN4LegicReader)
                {
                    try
                    {
                        await readerDevice.SearchTagAsync();
                    }
                    catch { }
                }

                try
                {
                    await readerDevice.MifareDesfire_SelectApplicationAsync((uint)_appID);

                    try
                    {
                        await readerDevice.MifareDesfire_AuthenticateAsync(
                            _applicationMasterKey,
                            (byte)_keyNumberCurrent,
                            (byte)(int)Enum.Parse(typeof(Elatec.NET.Cards.Mifare.DESFireKeyType),
                            Enum.GetName(typeof(DESFireKeyType), _keyType)),
                            1);
                    } // try to auth first.
                    catch
                    {

                    }

                    var fileSettings = await readerDevice.MifareDesfire_GetFileSettingsAsync((byte)_fileNo);

                    if (fileSettings != null)
                    {
                        DesfireFileSettings = new DESFireFileSettings
                        {
                            FileType = (byte)fileSettings.FileType,
                            comSett = fileSettings.ComSett
                        };
                        DesfireFileSettings.dataFile.fileSize = fileSettings.DataFileSetting != null ? fileSettings.DataFileSetting.FileSize : 0;
                        DesfireFileSettings.accessRights = new byte[2];
                        DesfireFileSettings.accessRights[0] |= fileSettings.accessRights.ReadKeyNo;
                        DesfireFileSettings.accessRights[0] |= (byte)(fileSettings.accessRights.WriteKeyNo << 4);
                        DesfireFileSettings.accessRights[1] |= fileSettings.accessRights.ReadWriteKeyNo;
                        DesfireFileSettings.accessRights[1] |= (byte)(fileSettings.accessRights.ChangeKeyNo << 4);

                        return ERROR.NoError;
                    }
                    else
                    {
                        return ERROR.AuthFailure;
                    }
                }
                catch
                {
                    return ERROR.TransportError;
                }
            }

            else
            {
                return ERROR.TransportError;
            }


        }

        /// <summary>
        /// Creates a DESFire file on the selected application.
        /// </summary>
        /// <remarks>
        /// Backup file creation prefers a dedicated Elatec API if available and falls back to the standard data file creation
        /// path with <see cref="DESFireFileType.BackupFile"/> when the API is unavailable.
        /// </remarks>
        public async override Task<ERROR> CreateMifareDesfireFile(string _appMasterKey, DESFireKeyType _keyTypeAppMasterKey, FileType_MifareDesfireFileType _fileType, DESFireAccessRights _accessRights, EncryptionMode _encMode,
                                     int _appID, int _fileNo, int _fileSize,
                                     int _minValue = 0, int _maxValue = 1000, int _initValue = 0, bool _isValueLimited = false,
                                     int _maxNbOfRecords = 100)
        {
            if (IsConnected)
            {
                try
                {
                    if (await AuthToMifareDesfireApplication(_appMasterKey, _keyTypeAppMasterKey, 0, _appID) == ERROR.NoError)
                    {
                        var ar = BuildDesfireAccessRights(_accessRights);

                        switch (_fileType)
                        {
                            case FileType_MifareDesfireFileType.StdDataFile:
                                try
                                {
                                    await CreateStdDataFileAsync((byte)_fileNo, _fileType, _encMode, ar, (uint)_fileSize);
                                }
                                catch
                                {
                                    return ERROR.AuthFailure;
                                }

                                break;

                            case FileType_MifareDesfireFileType.BackupFile:
                                try
                                {
                                    await CreateBackupFileAsync((byte)_fileNo, _encMode, ar, (uint)_fileSize);
                                }
                                catch
                                {
                                    return ERROR.AuthFailure;
                                }

                                break;
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.ForContext<ElatecNetProvider>().Error(e, "Elatec operation failed.");
                    return ERROR.TransportError;
                }

                return ERROR.NoError;
            }

            else
            {
                return ERROR.TransportError;
            }


        }

        private static DESFireFileAccessRights BuildDesfireAccessRights(DESFireAccessRights accessRights)
        {
            return new DESFireFileAccessRights
            {
                ReadKeyNo = (byte)accessRights.readAccess,
                WriteKeyNo = (byte)accessRights.writeAccess,
                ReadWriteKeyNo = (byte)accessRights.readAndWriteAccess,
                ChangeKeyNo = (byte)accessRights.changeAccess
            };
        }

        protected virtual Task CreateStdDataFileAsync(byte fileNo, FileType_MifareDesfireFileType fileType, EncryptionMode encMode, DESFireFileAccessRights accessRights, uint fileSize)
        {
            return readerDevice.MifareDesfire_CreateStdDataFileAsync(
                fileNo,
                (DESFireFileType)fileType,
                (Elatec.NET.Cards.Mifare.EncryptionMode)encMode,
                accessRights,
                fileSize);
        }

        protected virtual async Task CreateBackupFileAsync(byte fileNo, EncryptionMode encMode, DESFireFileAccessRights accessRights, uint fileSize)
        {
            var backupMethod = readerDevice?.GetType().GetMethod("MifareDesfire_CreateBackupFileAsync");
            if (backupMethod != null && TryBuildBackupMethodArguments(backupMethod, fileNo, encMode, accessRights, fileSize, out var arguments))
            {
                var result = backupMethod.Invoke(readerDevice, arguments);
                if (result is Task task)
                {
                    await task;
                    return;
                }
            }

            await CreateStdDataFileAsync(fileNo, FileType_MifareDesfireFileType.BackupFile, encMode, accessRights, fileSize);
        }

        private static bool TryBuildBackupMethodArguments(MethodInfo backupMethod, byte fileNo, EncryptionMode encMode, DESFireFileAccessRights accessRights, uint fileSize, out object[] arguments)
        {
            var parameters = backupMethod.GetParameters();

            if (parameters.Length == 4
                && parameters[0].ParameterType == typeof(byte)
                && parameters[1].ParameterType == typeof(Elatec.NET.Cards.Mifare.EncryptionMode)
                && parameters[2].ParameterType == typeof(DESFireFileAccessRights)
                && parameters[3].ParameterType == typeof(uint))
            {
                arguments = new object[]
                {
                    fileNo,
                    (Elatec.NET.Cards.Mifare.EncryptionMode)encMode,
                    accessRights,
                    fileSize
                };
                return true;
            }

            if (parameters.Length == 5
                && parameters[0].ParameterType == typeof(byte)
                && parameters[1].ParameterType == typeof(DESFireFileType)
                && parameters[2].ParameterType == typeof(Elatec.NET.Cards.Mifare.EncryptionMode)
                && parameters[3].ParameterType == typeof(DESFireFileAccessRights)
                && parameters[4].ParameterType == typeof(uint))
            {
                arguments = new object[]
                {
                    fileNo,
                    DESFireFileType.DF_FT_BACKUPDATAFILE,
                    (Elatec.NET.Cards.Mifare.EncryptionMode)encMode,
                    accessRights,
                    fileSize
                };
                return true;
            }

            arguments = Array.Empty<object>();
            return false;
        }

        /// <inheritdoc />
        public async override Task<ERROR> ReadMiFareDESFireChipFile(string _appReadKey, DESFireKeyType _keyTypeAppReadKey, int _readKeyNo,
                                       EncryptionMode _encMode,
                                       int _fileNo, int _appID, int _fileSize)
        {
            try
            {

                await readerDevice.MifareDesfire_SelectApplicationAsync((uint)_appID);

                if (await AuthToMifareDesfireApplication(_appReadKey, _keyTypeAppReadKey, _readKeyNo, _appID) == ERROR.NoError)
                {
                    MifareDESFireData = await readerDevice.MifareDesfire_ReadDataAsync((byte)_fileNo, _fileSize, (Elatec.NET.Cards.Mifare.EncryptionMode)_encMode);

                    if (MifareDESFireData != null)
                    {
                        return ERROR.NoError;
                    }
                    else
                    {
                        return ERROR.AuthFailure;
                    }
                }
                else
                {
                    return ERROR.AuthFailure;
                }

            }
            catch (Exception e)
            {
                Log.ForContext<ElatecNetProvider>().Error(e, "Elatec operation failed.");
                return ERROR.TransportError;
            }
        }

        /// <inheritdoc />
        public async override Task<ERROR> WriteMiFareDESFireChipFile(string _appWriteKey, DESFireKeyType _keyTypeAppWriteKey, int _writeKeyNo,
                                        EncryptionMode _encMode,
                                        int _fileNo, int _appID, byte[] _data)
        {
            try
            {

                await readerDevice.MifareDesfire_SelectApplicationAsync((uint)_appID);

                if (await AuthToMifareDesfireApplication(_appWriteKey, _keyTypeAppWriteKey, _writeKeyNo, _appID) == ERROR.NoError)
                {
                    await readerDevice.MifareDesfire_WriteDataAsync((byte)_fileNo, _data, (Elatec.NET.Cards.Mifare.EncryptionMode)_encMode);
                }
            }
            catch (Exception e)
            {
                Log.ForContext<ElatecNetProvider>().Error(e, "Elatec operation failed.");
                return ERROR.AuthFailure;
            }

            return ERROR.NoError;
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
