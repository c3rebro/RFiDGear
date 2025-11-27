//using Elatec.NET.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using ByteArrayHelper.Extensions;
using Elatec.NET;
using Elatec.NET.Cards.Mifare;
using RFiDGear.Model;

namespace RFiDGear.DataAccessLayer.Remote.FromIO
{
    public class ElatecNetProvider : ReaderDevice, IDisposable
    {
        private TWN4ReaderDevice readerDevice;
        private readonly EventLog eventLog = new EventLog("Application", ".", Assembly.GetEntryAssembly().GetName().Name);

        private GenericChipModel hfTag;
        private GenericChipModel lfTag;
        private GenericChipModel legicTag;

        private bool _disposed;

        #region Constructor

        private async Task Initialize()
        {
            if (GenericChip == null)
            {
                GenericChip = new GenericChipModel();
            }

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
                eventLog.WriteEntry(e.Message, EventLogEntryType.Error);
            }
        }

        public ElatecNetProvider()
        {
            if (GenericChip == null)
            {
                GenericChip = new GenericChipModel();
            }
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
                eventLog.WriteEntry(e.Message, EventLogEntryType.Error);

                return ERROR.IOError;
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
                return ERROR.NotReadyError;
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

            return ERROR.NotReadyError;
        }

        #endregion

        #region MifareClassic

        public async override Task<ERROR> WriteMifareClassicSingleBlock(int _blockNumber, string _aKey, string _bKey, byte[] buffer)
        {
            return await Task.Run(async () =>
            {
                try
                {
                    await readerDevice.MifareClassic_LoginAsync(_aKey, 0, (byte)CustomConverter.GetSectorNumberFromChipBasedDataBlockNumber(_blockNumber));
                }
                catch
                {
                    try
                    {
                        await readerDevice.MifareClassic_LoginAsync(_bKey, 1, (byte)CustomConverter.GetSectorNumberFromChipBasedDataBlockNumber(_blockNumber));
                    }
                    catch
                    {
                        try
                        {
                            await readerDevice.MifareClassic_WriteBlockAsync(buffer, (byte)_blockNumber);
                        }
                        catch
                        {
                            return ERROR.AuthenticationError;
                        }
                    }
                } // Login  
                return ERROR.NoError;
            });

        }

        public async override Task<ERROR> ReadMifareClassicSingleSector(int sectorNumber, string aKey, string bKey)
        {
            return await ReadWriteAccessOnClassicSector(sectorNumber, aKey, bKey, null);
        }

        public async override Task<ERROR> WriteMifareClassicSingleSector(int sectorNumber, string aKey, string bKey, byte[] buffer)
        {
            return await ReadWriteAccessOnClassicSector(sectorNumber, aKey, bKey, buffer);
        }

        public async override Task<ERROR> WriteMifareClassicWithMAD(int _madApplicationID, int _madStartSector,
            string _aKeyToUse, string _bKeyToUse, string _aKeyToWrite, string _bKeyToWrite,
            string _madAKeyToUse, string _madBKeyToUse, string _madAKeyToWrite,
            string _madBKeyToWrite, byte[] buffer, byte _madGPB, SectorAccessBits _sab,
            bool _useMADToAuth, bool _keyToWriteUseMAD)
        {
            throw new NotImplementedException();
        }

        public async override Task<ERROR> ReadMifareClassicWithMAD(int madApplicationID, string _aKeyToUse,
            string _bKeyToUse, string _madAKeyToUse, string _madBKeyToUse, int _length, byte _madGPB,
            bool _useMADToAuth, bool _aiToUseIsMAD)
        {
            throw new NotImplementedException();
        }

        private async Task<ERROR> ReadWriteAccessOnClassicSector(int sectorNumber, string aKey, string bKey, byte[] buffer)
        {
            if (readerDevice.IsConnected)
            {
                return await Task.Run(async () =>
                {
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
                                    return ERROR.AuthenticationError;
                                }
                            } // write Data
                        }
                        catch
                        {
                            return ERROR.IOError; // IO ElatecError
                        }
                    }

                    if (Sector.IsAuthenticated)
                    {
                        return ERROR.NoError; //NO ElatecError
                    }

                    return ERROR.AuthenticationError; // Auth ElatecError
                });
            }

            else
            {
                return ERROR.NotReadyError;
            }
        }

        #endregion

        #region MifareUltralight
        public override Task<ERROR> ReadMifareUltralightSinglePage(int _pageNo)
        {
            throw new NotImplementedException();
        }
        #endregion

        #region MifareDesfire

        /// <summary>
        /// 
        /// </summary>
        /// <param name="_appMasterKey"></param>
        /// <param name="_keyTypeAppMasterKey"></param>
        /// <returns>ElatecError Level</returns>
        public async override Task<ERROR> GetMiFareDESFireChipAppIDs(string _appMasterKey, DESFireKeyType _keyTypeAppMasterKey)
        {
            if (readerDevice.IsConnected)
            {
                return await Task.Run(async () =>
                {
                    uint[] appArr;

                    if (DesfireChip == null)
                    {
                        DesfireChip = new MifareDesfireChipModel();
                    }

                    DesfireChip.AppList = new System.Collections.Generic.List<MifareDesfireAppModel>();

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
                        return ERROR.AuthenticationError;
                    }

                    return ERROR.NoError;
                });
            }

            else
            {
                return ERROR.NotReadyError;
            }

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="_applicationMasterKey"></param>
        /// <param name="_keyType"></param>
        /// <param name="_keyNumber"></param>
        /// <param name="_appID"></param>
        /// <returns></returns>
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
                        (byte)(int)Enum.Parse(typeof(Elatec.NET.Cards.Mifare.DESFireKeyType), Enum.GetName(typeof(RFiDGear.DataAccessLayer.DESFireKeyType), _keyType)),
                        1);
                    return ERROR.NoError;
                }
                catch
                {
                    return ERROR.AuthenticationError;
                }
            }

            else
            {
                return ERROR.NotReadyError;
            }

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="_applicationMasterKey"></param>
        /// <param name="_keyType"></param>
        /// <param name="_keyNumberCurrent"></param>
        /// <param name="_appID"></param>
        /// <returns></returns>
        public async override Task<ERROR> GetMifareDesfireAppSettings(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumberCurrent, int _appID)
        {
            if (readerDevice.IsConnected)
            {
                return await Task.Run(async () =>
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
                            var ks = await readerDevice.MifareDesfire_GetKeySettingsAsync();

                            MaxNumberOfAppKeys = (byte)ks.NumberOfKeys;
                            EncryptionType = (DESFireKeyType)Enum.Parse(typeof(Elatec.NET.Cards.Mifare.DESFireKeyType), Enum.GetName(typeof(Elatec.NET.Cards.Mifare.DESFireKeyType), ks.KeyType));
                            DesfireAppKeySetting = (DESFireKeySettings)ks.AccessRights;

                            return ERROR.NoError;
                        } // Get Settings without authentication
                        catch
                        {
                            try
                            {
                                await readerDevice.MifareDesfire_AuthenticateAsync(_applicationMasterKey, (byte)_keyNumberCurrent, (byte)(int)Enum.Parse(typeof(Elatec.NET.Cards.Mifare.DESFireKeyType), Enum.GetName(typeof(RFiDGear.DataAccessLayer.DESFireKeyType), _keyType)), 1);
                                var ks = await readerDevice.MifareDesfire_GetKeySettingsAsync();

                                MaxNumberOfAppKeys = (byte)ks.NumberOfKeys;
                                EncryptionType = (DESFireKeyType)Enum.Parse(typeof(DESFireKeyType), Enum.GetName(typeof(Elatec.NET.Cards.Mifare.DESFireKeyType), ks.KeyType));
                                DesfireAppKeySetting = (DESFireKeySettings)ks.AccessRights;

                                return ERROR.NoError;
                            }
                            catch
                            {
                                return ERROR.AuthenticationError;
                            }
                        } // needs Auth
                    }
                    catch
                    {
                        return ERROR.AuthenticationError;
                    }
                });
            }

            else
            {
                return ERROR.NotReadyError;
            }

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="_piccMasterKey"></param>
        /// <param name="_keySettingsTarget"></param>
        /// <param name="_keyTypePiccMasterKey"></param>
        /// <param name="_keyTypeTargetApplication"></param>
        /// <param name="_maxNbKeys"></param>
        /// <param name="_appID"></param>
        /// <param name="authenticateToPICCFirst"></param>
        /// <returns></returns>
        public async override Task<ERROR> CreateMifareDesfireApplication(string _piccMasterKey, DESFireKeySettings _keySettingsTarget,
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

                    await readerDevice.MifareDesfire_CreateApplicationAsync(
                                                (Elatec.NET.Cards.Mifare.DESFireAppAccessRights)_keySettingsTarget,
                                                (Elatec.NET.Cards.Mifare.DESFireKeyType)Enum.Parse(typeof(Elatec.NET.Cards.Mifare.DESFireKeyType), Enum.GetName(typeof(RFiDGear.DataAccessLayer.DESFireKeyType), _keyTypeTargetApplication)),
                                                _maxNbKeys,
                                                _appID);
                    return ERROR.NoError;
                } // free create ?
                catch
                {
                    try
                    {
                        await readerDevice.MifareDesfire_AuthenticateAsync(_piccMasterKey, 0, (byte)(int)Enum.Parse(typeof(Elatec.NET.Cards.Mifare.DESFireKeyType), Enum.GetName(typeof(RFiDGear.DataAccessLayer.DESFireKeyType), _keyTypePiccMasterKey)), 1);

                        await readerDevice.MifareDesfire_CreateApplicationAsync(
                                                (Elatec.NET.Cards.Mifare.DESFireAppAccessRights)_keySettingsTarget,
                                                (Elatec.NET.Cards.Mifare.DESFireKeyType)Enum.Parse(typeof(Elatec.NET.Cards.Mifare.DESFireKeyType), Enum.GetName(typeof(RFiDGear.DataAccessLayer.DESFireKeyType), _keyTypeTargetApplication)),
                                                _maxNbKeys,
                                                _appID);

                        return ERROR.NoError;
                    } // auth first ?
                    catch
                    {
                        return ERROR.AuthenticationError;
                    }
                }
            }

            else
            {
                return ERROR.NotReadyError;
            }


        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="_applicationMasterKeyCurrent"></param>
        /// <param name="_keyNumberCurrent"></param>
        /// <param name="_keyTypeCurrent"></param>
        /// <param name="_applicationMasterKeyTarget"></param>
        /// <param name="_keyNumberTarget"></param>
        /// <param name="selectedDesfireAppKeyVersionTargetAsIntint"></param>
        /// <param name="_keyTypeTarget"></param>
        /// <param name="_appIDCurrent"></param>
        /// <param name="_appIDTarget"></param>
        /// <param name="keySettings"></param>
        /// <param name="keyVersion"></param>
        /// <returns></returns>
        public async override Task<ERROR> ChangeMifareDesfireApplicationKey(string _applicationMasterKeyCurrent, int _keyNumberCurrent, DESFireKeyType _keyTypeCurrent,
                                        string _applicationMasterKeyTarget, int _keyNumberTarget, int selectedDesfireAppKeyVersionTargetAsIntint,
                                        DESFireKeyType _keyTypeTarget, int _appIDCurrent, int _appIDTarget, DESFireKeySettings keySettings, int keyVersion)
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

                    await readerDevice.MifareDesfire_AuthenticateAsync(_applicationMasterKeyCurrent, (byte)_keyNumberCurrent, (byte)(int)Enum.Parse(typeof(Elatec.NET.Cards.Mifare.DESFireKeyType), Enum.GetName(typeof(RFiDGear.DataAccessLayer.DESFireKeyType), _keyTypeCurrent)), 1);

                    if (_applicationMasterKeyCurrent == _applicationMasterKeyTarget)
                    {
                        await readerDevice.MifareDesfire_ChangeKeySettingsAsync(
                            (DESFireAppAccessRights)keySettings,
                            0,
                            (Elatec.NET.Cards.Mifare.DESFireKeyType)Enum.Parse(
                                typeof(Elatec.NET.Cards.Mifare.DESFireKeyType),
                                Enum.GetName(typeof(DESFireKeyType), _keyTypeTarget)
                                )
                            );
                    }

                    else
                    {
                        await readerDevice.MifareDesfire_ChangeKeyAsync(
                            _applicationMasterKeyCurrent,
                            _applicationMasterKeyTarget,
                            (byte)keyVersion,
                            _keyNumberCurrent == 0 ? (byte)keySettings : (byte)((byte)keySettings | 0xE0),
                            (byte)_keyNumberTarget,
                            1,
                            (Elatec.NET.Cards.Mifare.DESFireKeyType)Enum.Parse(typeof(Elatec.NET.Cards.Mifare.DESFireKeyType), Enum.GetName(typeof(RFiDGear.DataAccessLayer.DESFireKeyType), _keyTypeTarget)));
                    }

                    return ERROR.NoError;
                }
                catch
                {
                    return ERROR.AuthenticationError;
                }
            }

            else
            {
                return ERROR.NotReadyError;
            }


        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="_applicationMasterKey"></param>
        /// <param name="_keyTypePiccMasterKey"></param>
        /// <param name="_appID"></param>
        /// <returns></returns>
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
                        return ERROR.NotReadyError;
                    }
                }

                try
                {
                    if (await AuthToMifareDesfireApplication(_applicationMasterKey, _keyTypePiccMasterKey, 0, 0) == ERROR.NoError)
                    {
                        await readerDevice.MifareDesfire_DeleteApplicationAsync(_appID);
                    }
                    else {
                        await readerDevice.MifareDesfire_DeleteApplicationAsync(_appID);
                    }
                }
                catch
                {
                    return ERROR.AuthenticationError;
                }
                return ERROR.NoError;
            }

            else
            {
                return ERROR.NotReadyError;
            }

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="_applicationMasterKey"></param>
        /// <param name="_keyType"></param>
        /// <param name="_appID"></param>
        /// <param name="_fileID"></param>
        /// <returns></returns>
        public async override Task<ERROR> DeleteMifareDesfireFile(string _applicationMasterKey, DESFireKeyType _keyType, int _appID, int _fileID)
        {
            /*
            if (readerDevice.DesfireSelectApplication((uint)_appID))
            {
                if (readerDevice.DesfireAuthenticate(_applicationMasterKey, 0x00, (byte)(int)Enum.Parse(typeof(Elatec.NET.DESFireKeyType), Enum.GetName(typeof(RFiDGear.DataAccessLayer.DESFireKeyType), _keyType)), 1))
                {
                    if (readerDevice.DesfireDeleteFile((byte)_fileID))
                    {
                        return ERROR.NoError;
                    }
                    else
                    {
                        return ERROR.AuthenticationError;
                    }
                }
            }
            */
            return ERROR.NotReadyError;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="_applicationMasterKey"></param>
        /// <param name="_keyType"></param>
        /// <param name="_appID"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
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
                        return ERROR.NotReadyError;
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
                        return ERROR.AuthenticationError;
                    }
                }

                catch
                {
                    return ERROR.AuthenticationError;
                }

                return ERROR.NoError;
            }

            else
            {
                return ERROR.NotReadyError;
            }


        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="_applicationMasterKey"></param>
        /// <param name="_keyType"></param>
        /// <param name="_keyNumberCurrent"></param>
        /// <param name="_appID"></param>
        /// <returns></returns>
        public async override Task<ERROR> GetMifareDesfireFileList(string _applicationMasterKey, RFiDGear.DataAccessLayer.DESFireKeyType _keyType, int _keyNumberCurrent, int _appID)
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
                        await readerDevice.MifareDesfire_AuthenticateAsync(_applicationMasterKey, (byte)_keyNumberCurrent, (byte)(int)Enum.Parse(typeof(Elatec.NET.Cards.Mifare.DESFireKeyType), Enum.GetName(typeof(RFiDGear.DataAccessLayer.DESFireKeyType), _keyType)), 1);
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
                    return ERROR.NotReadyError;
                }

                return ERROR.NoError;
            }

            else
            {
                return ERROR.NotReadyError;
            }

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="_applicationMasterKey"></param>
        /// <param name="_keyType"></param>
        /// <param name="_keyNumberCurrent"></param>
        /// <param name="_appID"></param>
        /// <param name="_fileNo"></param>
        /// <returns></returns>
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
                            Enum.GetName(typeof(RFiDGear.DataAccessLayer.DESFireKeyType), _keyType)),
                            1);
                    } // try to auth first.
                    catch
                    {

                    }

                    var fileSettings = await readerDevice.MifareDesfire_GetFileSettingsAsync((byte)_fileNo);

                    if (fileSettings != null)
                    {
                        DesfireFileSettings = new DESFireFileSettings();

                        DesfireFileSettings.FileType = (byte)fileSettings.FileType;
                        DesfireFileSettings.comSett = (byte)fileSettings.ComSett;
                        DesfireFileSettings.dataFile.fileSize = fileSettings.DataFileSetting != null ? fileSettings.DataFileSetting.FileSize : 0;
                        DesfireFileSettings.accessRights = new byte[2];
                        DesfireFileSettings.accessRights[0] |= (byte)fileSettings.accessRights.ReadKeyNo;
                        DesfireFileSettings.accessRights[0] |= (byte)(fileSettings.accessRights.WriteKeyNo << 4);
                        DesfireFileSettings.accessRights[1] |= (byte)(fileSettings.accessRights.ReadWriteKeyNo);
                        DesfireFileSettings.accessRights[1] |= (byte)(fileSettings.accessRights.ChangeKeyNo << 4);

                        return ERROR.NoError;
                    }
                    else
                    {
                        return ERROR.AuthenticationError;
                    }
                }
                catch
                {
                    return ERROR.NotReadyError;
                }
            }

            else
            {
                return ERROR.NotReadyError;
            }


        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="_appMasterKey"></param>
        /// <param name="_keyTypeAppMasterKey"></param>
        /// <param name="_fileType"></param>
        /// <param name="_accessRights"></param>
        /// <param name="_encMode"></param>
        /// <param name="_appID"></param>
        /// <param name="_fileNo"></param>
        /// <param name="_fileSize"></param>
        /// <param name="_minValue"></param>
        /// <param name="_maxValue"></param>
        /// <param name="_initValue"></param>
        /// <param name="_isValueLimited"></param>
        /// <param name="_maxNbOfRecords"></param>
        /// <returns></returns>
        public async override Task<ERROR> CreateMifareDesfireFile(string _appMasterKey, DESFireKeyType _keyTypeAppMasterKey, FileType_MifareDesfireFileType _fileType, DESFireAccessRights _accessRights, EncryptionMode _encMode,
                                     int _appID, int _fileNo, int _fileSize,
                                     int _minValue = 0, int _maxValue = 1000, int _initValue = 0, bool _isValueLimited = false,
                                     int _maxNbOfRecords = 100)
        {
            if (readerDevice.IsConnected)
            {
                try
                {
                    if (await AuthToMifareDesfireApplication(_appMasterKey, _keyTypeAppMasterKey, 0, _appID) == ERROR.NoError)
                    {
                        switch (_fileType)
                        {
                            case FileType_MifareDesfireFileType.StdDataFile:
                                var ar = new DESFireFileAccessRights
                                {
                                    ReadKeyNo = (byte)_accessRights.readAccess,
                                    WriteKeyNo = (byte)_accessRights.writeAccess,
                                    ReadWriteKeyNo = (byte)_accessRights.readAndWriteAccess,
                                    ChangeKeyNo = (byte)_accessRights.changeAccess
                                };

                                try
                                {
                                    await readerDevice.MifareDesfire_CreateStdDataFileAsync(
                                        (byte)_fileNo,
                                        (DESFireFileType)_fileType,
                                        (Elatec.NET.Cards.Mifare.EncryptionMode)_encMode,
                                        ar,
                                        (UInt32)_fileSize);
                                }

                                catch
                                {
                                    return ERROR.AuthenticationError;
                                }

                                break;
                        }
                    }
                }
                catch (Exception e)
                {
                    eventLog.WriteEntry(e.Message, EventLogEntryType.Error);
                    return ERROR.IOError;
                }

                return ERROR.NoError;
            }

            else
            {
                return ERROR.NotReadyError;
            }


        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="_appMasterKey"></param>
        /// <param name="_keyTypeAppMasterKey"></param>
        /// <param name="_appReadKey"></param>
        /// <param name="_keyTypeAppReadKey"></param>
        /// <param name="_readKeyNo"></param>
        /// <param name="_appWriteKey"></param>
        /// <param name="_keyTypeAppWriteKey"></param>
        /// <param name="_writeKeyNo"></param>
        /// <param name="_encMode"></param>
        /// <param name="_fileNo"></param>
        /// <param name="_appID"></param>
        /// <param name="_fileSize"></param>
        /// <returns></returns>
        public async override Task<ERROR> ReadMiFareDESFireChipFile(string _appMasterKey, DESFireKeyType _keyTypeAppMasterKey,
                                       string _appReadKey, DESFireKeyType _keyTypeAppReadKey, int _readKeyNo,
                                       string _appWriteKey, DESFireKeyType _keyTypeAppWriteKey, int _writeKeyNo,
                                       EncryptionMode _encMode,
                                       int _fileNo, int _appID, int _fileSize)
        {
            try
            {
                /*
                if (readerDevice.DesfireSelectApplication((uint)_appID))
                {
                    if (readerDevice.DesfireAuthenticate(_appReadKey, (byte)_readKeyNo, (byte)(int)Enum.Parse(typeof(Elatec.NET.DESFireKeyType), Enum.GetName(typeof(RFiDGear.DataAccessLayer.DESFireKeyType), _keyTypeAppReadKey)), 1))
                    {
                        MifareDESFireData = readerDevice.DesfireReadData((byte)_fileNo, _fileSize, (byte)_encMode);

                        if (MifareDESFireData != null)
                        {
                            return ERROR.NoError;
                        }
                        else
                        {
                            return ERROR.AuthenticationError;
                        }
                    }
                    else
                    {
                        return ERROR.AuthenticationError;
                    }
                }
                */
                return ERROR.AuthenticationError;

            }
            catch (Exception e)
            {
                eventLog.WriteEntry(e.Message, EventLogEntryType.Error);
                return ERROR.IOError;
            }
        }

        public async override Task<ERROR> WriteMiFareDESFireChipFile(string _cardMasterKey, DESFireKeyType _keyTypeCardMasterKey,
                                        string _appMasterKey, DESFireKeyType _keyTypeAppMasterKey,
                                        string _appReadKey, DESFireKeyType _keyTypeAppReadKey, int _readKeyNo,
                                        string _appWriteKey, DESFireKeyType _keyTypeAppWriteKey, int _writeKeyNo,
                                        EncryptionMode _encMode,
                                        int _fileNo, int _appID, byte[] _data)
        {
            throw new NotImplementedException();
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
