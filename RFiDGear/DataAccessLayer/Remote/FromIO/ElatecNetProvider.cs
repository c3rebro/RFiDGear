using Elatec.NET.Model;
using Elatec.NET;

using RFiDGear.Model;

using Log4CSharp;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using ByteArrayHelper.Extensions;
using RFiDGear.ViewModel;
using System.Runtime.InteropServices.WindowsRuntime;

namespace RFiDGear.DataAccessLayer.Remote.FromIO
{
    public class ElatecNetProvider : ReaderDevice, IDisposable
    {
        private static readonly string FacilityName = "RFiDGear";

        private readonly TWN4ReaderDevice readerDevice;

        private GenericChipModel hfTag;
        private GenericChipModel lfTag;
        private GenericChipModel legicTag;

        private bool _disposed;

        #region Constructor

        public ElatecNetProvider()
        {
            try
            {
                readerDevice = new TWN4ReaderDevice(PortNumber);
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(e, FacilityName);
            }
        }

        public ElatecNetProvider(int _comPort)
        {
            try
            {
                readerDevice = new TWN4ReaderDevice(_comPort);
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(e, FacilityName);
            }

        }

        #endregion

        #region Common

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override ERROR ReadChipPublic()
        {
            try
            {
                if (readerDevice != null)
                {
                    if (!readerDevice.IsConnected)
                    {
                        Instance.Connect();
                    }

                    var tmpTag = readerDevice.GetSingleChip(true);
                    hfTag = new GenericChipModel(tmpTag.UID, (RFiDGear.DataAccessLayer.CARD_TYPE)tmpTag.CardType, tmpTag.SAK, tmpTag.RATS, tmpTag.VersionL4);
                    tmpTag = readerDevice.GetSingleChip(false);
                    lfTag = new GenericChipModel(tmpTag.UID, (RFiDGear.DataAccessLayer.CARD_TYPE)tmpTag.CardType);
                    tmpTag = readerDevice.GetSingleChip(true, true);
                    legicTag = new GenericChipModel(tmpTag.UID, (RFiDGear.DataAccessLayer.CARD_TYPE)tmpTag.CardType);
                    readerDevice.GetSingleChip(true);

                    if (
                            !(
                                string.IsNullOrWhiteSpace(hfTag?.UID) & 
                                string.IsNullOrWhiteSpace(lfTag?.UID) &
                                string.IsNullOrWhiteSpace(legicTag?.UID)
                            )
                        )
                    {
                        try
                        {
                            readerDevice.GreenLED(true);
                            readerDevice.RedLED(false);

                            GenericChip = new GenericChipModel(hfTag.UID, 
                                (CARD_TYPE)hfTag.CardType, 
                                hfTag.SAK, 
                                hfTag.RATS,
                                hfTag.VersionL4
                                );

                            if (lfTag != null && lfTag?.CardType != CARD_TYPE.NOTAG)
                            {
                                GenericChip.Child = new GenericChipModel(lfTag.UID, (RFiDGear.DataAccessLayer.CARD_TYPE)lfTag.CardType);
                            }
                            else if(legicTag != null && legicTag?.CardType != CARD_TYPE.NOTAG)
                            {
                                GenericChip.Child = new GenericChipModel(legicTag.UID, (RFiDGear.DataAccessLayer.CARD_TYPE)legicTag.CardType);
                            }
                            //readerDevice.GetSingleChip(true);

                            return ERROR.NoError;
                        }
                        catch (Exception e)
                        {
                            LogWriter.CreateLogEntry(e, FacilityName);
                            return ERROR.IOError;
                        }
                    }
                    else
                    {
                        readerDevice.Beep(3, 25, 600, 100);
                        readerDevice.RedLED(true);
                        GenericChip = null;

                        return ERROR.NotReadyError;
                    }
                }

                else
                {
                    return ERROR.IOError;
                }
            }
            catch (Exception e)
            {
                if (readerDevice != null)
                {
                    readerDevice.Dispose();
                }

                LogWriter.CreateLogEntry(e, FacilityName);

                return ERROR.IOError;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override ERROR Connect()
        {
            readerDevice.Beep(1, 50, 1000, 100);
            readerDevice.GreenLED(true);
            readerDevice.RedLED(false);

            return TWN4ReaderDevice.Instance.Connect() == true ? ERROR.NoError : ERROR.IOError;
        }

        #endregion

        #region MifareClassic
        public override ERROR WriteMifareClassicSingleBlock(int _blockNumber, string _aKey, string _bKey, byte[] buffer)
        {
            if (!readerDevice.MifareClassicLogin(_aKey, 0, (byte)CustomConverter.GetSectorNumberFromChipBasedDataBlockNumber(_blockNumber))) // No Access Allowed, try bKey
            {
                readerDevice.MifareClassicLogin(_bKey, 1, (byte)CustomConverter.GetSectorNumberFromChipBasedDataBlockNumber(_blockNumber));
                
            } // Login

            return readerDevice.MifareClassicWriteBlock(buffer, (byte)_blockNumber) == true ? ERROR.NoError : ERROR.AuthenticationError;
        }

        public override ERROR ReadMifareClassicSingleSector(int sectorNumber, string aKey, string bKey)
        {
            return readWriteAccessOnClassicSector(sectorNumber, aKey, bKey, null);
        }

        public override ERROR WriteMifareClassicSingleSector(int sectorNumber, string aKey, string bKey, byte[] buffer)
        {
            return readWriteAccessOnClassicSector(sectorNumber, aKey, bKey, buffer);
        }
 
        public override ERROR WriteMifareClassicWithMAD(int _madApplicationID, int _madStartSector,
            string _aKeyToUse, string _bKeyToUse, string _aKeyToWrite, string _bKeyToWrite,
            string _madAKeyToUse, string _madBKeyToUse, string _madAKeyToWrite, 
            string _madBKeyToWrite, byte[] buffer, byte _madGPB, SectorAccessBits _sab, 
            bool _useMADToAuth, bool _keyToWriteUseMAD)
        {
            throw new NotImplementedException();
        }

        public override ERROR ReadMifareClassicWithMAD(int madApplicationID, string _aKeyToUse, 
            string _bKeyToUse, string _madAKeyToUse, string _madBKeyToUse, int _length, byte _madGPB, 
            bool _useMADToAuth, bool _aiToUseIsMAD)
        {
            throw new NotImplementedException();
        }
       
        private ERROR readWriteAccessOnClassicSector(int sectorNumber, string aKey, string bKey, byte[] buffer)
        {
            Sector = new MifareClassicSectorModel();

            var elatecSpecificSectorNumber = sectorNumber > 31 ? (sectorNumber - 32) * 4 + 32 : sectorNumber; // elatec uses special sectornumbers

            for (byte k = 0; k < (sectorNumber > 31 ? 16 : 4); k++) // if sector > 31 is 16 blocks each sector i.e. mifare 4k else its 1k or 2k with 4 blocks each sector
            {
                DataBlock = new MifareClassicDataBlockModel(
                    (byte)CustomConverter.GetChipBasedDataBlockNumber(sectorNumber, k),
                    k);

                try
                {
                    var isAuth = readerDevice.MifareClassicLogin(aKey, 0, (byte)elatecSpecificSectorNumber);

                    if (buffer == null || buffer.Length != 16) // Read Mode
                    {
                        var data = readerDevice.MifareClassicReadBlock((byte)CustomConverter.GetChipBasedDataBlockNumber(sectorNumber, k));

                        if (data.Length > 1)
                        {
                            DataBlock.Data = data;
                            DataBlock.IsAuthenticated = true;
                            Sector.IsAuthenticated = isAuth;
                            Sector.DataBlock.Add(DataBlock);
                        }

                        else // No Read Access Allowed, try bKey
                        {
                            isAuth = readerDevice.MifareClassicLogin(bKey, 1, (byte)elatecSpecificSectorNumber);

                            data = readerDevice.MifareClassicReadBlock(k);

                            if (data.Length > 1)
                            {
                                DataBlock.Data = data;
                                DataBlock.IsAuthenticated = true;
                                Sector.IsAuthenticated = isAuth;
                                Sector.DataBlock.Add(DataBlock);
                            }
                            else
                            {
                                Sector.IsAuthenticated = false;
                                DataBlock.IsAuthenticated = false; // finally failed to read data
                            }
                        }
                    } // read Data

                    else if (buffer != null && buffer.Length == 16)
                    {
                        return readerDevice.MifareClassicWriteBlock(buffer, k) == true ? ERROR.NoError : ERROR.AuthenticationError;
                    } // write Data
                }
                catch
                {
                    return ERROR.IOError; // IO ElatecError
                }
            }

            if(Sector.IsAuthenticated)
            {
                return ERROR.NoError; //NO ElatecError
            }

            return ERROR.AuthenticationError; // Auth ElatecError
        }

        #endregion

        #region MifareUltralight
        public override ERROR ReadMifareUltralightSinglePage(int _pageNo)
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
        public override ERROR GetMiFareDESFireChipAppIDs(string _appMasterKey, DESFireKeyType _keyTypeAppMasterKey)
        {
            if (DesfireChip == null)
            {
                DesfireChip = new MifareDesfireChipModel();
            }

            DesfireChip.AppList = new System.Collections.Generic.List<MifareDesfireAppModel>();
            if (readerDevice.DesfireSelectApplication(0))
            {
                DesfireChip.FreeMemory = readerDevice.GetDesfireFreeMemory() ?? 0;

                var appArr = readerDevice.GetDesfireAppIDs();

                if (appArr != null)
                {
                    foreach (var appid in appArr)
                    {
                        DesfireChip.AppList.Add(new MifareDesfireAppModel(appid));
                    }
                }
            }

            return ERROR.NoError;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="_applicationMasterKey"></param>
        /// <param name="_keyType"></param>
        /// <param name="_keyNumber"></param>
        /// <param name="_appID"></param>
        /// <returns></returns>
        public override ERROR AuthToMifareDesfireApplication(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumber, int _appID)
        {

            if (readerDevice.DesfireSelectApplication((uint)_appID))
            {
                return readerDevice.DesfireAuthenticate(
                    _applicationMasterKey,
                    (byte)_keyNumber,
                    (byte)(int)Enum.Parse(typeof(Elatec.NET.DESFireKeyType), Enum.GetName(typeof(RFiDGear.DataAccessLayer.DESFireKeyType), _keyType)),
                    1) == true ? ERROR.NoError : ERROR.AuthenticationError;
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
        public override ERROR GetMifareDesfireAppSettings(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumberCurrent, int _appID)
        {
            if (readerDevice.DesfireSelectApplication((uint)_appID))
            {
                if (readerDevice.GetDesFireKeySettings())
                {
                    MaxNumberOfAppKeys = readerDevice.NumberOfKeys;
                    EncryptionType = (DESFireKeyType)Enum.Parse(typeof(DESFireKeyType), Enum.GetName(typeof(Elatec.NET.DESFireKeyType), readerDevice.KeyType));
                    DesfireAppKeySetting = (DESFireKeySettings)readerDevice.KeySettings;

                    return ERROR.NoError;
                } // Get Settings without authentication
                else
                {
                    if (readerDevice.DesfireAuthenticate(_applicationMasterKey, (byte)_keyNumberCurrent, (byte)(int)Enum.Parse(typeof(Elatec.NET.DESFireKeyType), Enum.GetName(typeof(RFiDGear.DataAccessLayer.DESFireKeyType), _keyType)), 1))
                    {
                        MaxNumberOfAppKeys = readerDevice.NumberOfKeys;
                        EncryptionType = (DESFireKeyType)readerDevice.KeyType;
                        DesfireAppKeySetting = (DESFireKeySettings)readerDevice.KeySettings;

                        return ERROR.NoError;
                    }
                    return ERROR.AuthenticationError;
                } // authenticate first

            }

            return ERROR.AuthenticationError;
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
        public override ERROR CreateMifareDesfireApplication(string _piccMasterKey, DESFireKeySettings _keySettingsTarget,
                                        DESFireKeyType _keyTypePiccMasterKey, DESFireKeyType _keyTypeTargetApplication,
                                        int _maxNbKeys, int _appID, bool authenticateToPICCFirst = true)
        {
            if (readerDevice.DesfireSelectApplication(0))
            {
                if (readerDevice.DesfireAuthenticate(_piccMasterKey, 0x00, (byte)(int)Enum.Parse(typeof(Elatec.NET.DESFireKeyType), Enum.GetName(typeof(RFiDGear.DataAccessLayer.DESFireKeyType), _keyTypePiccMasterKey)), 1))
                {
                    if (readerDevice.DesfireCreateApplication(
                        (Elatec.NET.DESFireKeySettings)_keySettingsTarget, 
                        (Elatec.NET.DESFireKeyType)Enum.Parse(typeof(Elatec.NET.DESFireKeyType), Enum.GetName(typeof(RFiDGear.DataAccessLayer.DESFireKeyType), _keyTypeTargetApplication)),
                        _maxNbKeys,
                        _appID))
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
                    if (readerDevice.DesfireCreateApplication((Elatec.NET.DESFireKeySettings)_keySettingsTarget, (Elatec.NET.DESFireKeyType)Enum.Parse(typeof(Elatec.NET.DESFireKeyType), Enum.GetName(typeof(RFiDGear.DataAccessLayer.DESFireKeyType), _keyTypeTargetApplication)), _maxNbKeys, _appID))
                    {
                        return ERROR.NoError;
                    }
                    else
                    {
                        return ERROR.AuthenticationError;
                    }
                }
            }
            return ERROR.IOError;
                    
        }

        public override ERROR ChangeMifareDesfireApplicationKey(string _applicationMasterKeyCurrent, int _keyNumberCurrent, DESFireKeyType _keyTypeCurrent,
                                        string _applicationMasterKeyTarget, int _keyNumberTarget, int selectedDesfireAppKeyVersionTargetAsIntint,
                                        DESFireKeyType _keyTypeTarget, int _appIDCurrent, int _appIDTarget, DESFireKeySettings keySettings, int keyVersion)
        {
            if (readerDevice.DesfireSelectApplication((uint)_appIDCurrent))
            {
                if (readerDevice.DesfireAuthenticate(_applicationMasterKeyCurrent, (byte)_keyNumberCurrent, (byte)(int)Enum.Parse(typeof(Elatec.NET.DESFireKeyType), Enum.GetName(typeof(RFiDGear.DataAccessLayer.DESFireKeyType), _keyTypeCurrent)), 1))
                {
                    if (readerDevice.DesfireChangeKey(
                        _applicationMasterKeyCurrent, 
                        _applicationMasterKeyTarget, 
                        (byte)keyVersion, 
                        _keyNumberCurrent == 0 ? (byte)keySettings : (byte)((byte)keySettings | 0xE0), 
                        (byte)_keyNumberTarget, 
                        1,
                        (byte)(int)Enum.Parse(typeof(Elatec.NET.DESFireKeyType), Enum.GetName(typeof(RFiDGear.DataAccessLayer.DESFireKeyType), _keyTypeTarget))))
                    {
                        if (readerDevice.DesfireAuthenticate(_applicationMasterKeyTarget, (byte)_keyNumberTarget, (byte)(int)Enum.Parse(typeof(Elatec.NET.DESFireKeyType), Enum.GetName(typeof(RFiDGear.DataAccessLayer.DESFireKeyType), _keyTypeTarget)), 1))
                        {
                            readerDevice.DesfireChangeKeySettings((byte)keySettings, 0, (byte)(int)Enum.Parse(typeof(Elatec.NET.DESFireKeyType), Enum.GetName(typeof(RFiDGear.DataAccessLayer.DESFireKeyType), _keyTypeTarget)));
                        }

                        return ERROR.NoError;
                    }
                    else
                    {
                        if (readerDevice.DesfireAuthenticate(_applicationMasterKeyTarget, (byte)_keyNumberTarget, (byte)(int)Enum.Parse(typeof(Elatec.NET.DESFireKeyType), Enum.GetName(typeof(RFiDGear.DataAccessLayer.DESFireKeyType), _keyTypeTarget)), 1))
                        {
                            readerDevice.DesfireChangeKeySettings((byte)keySettings, 0, (byte)(int)Enum.Parse(typeof(Elatec.NET.DESFireKeyType), Enum.GetName(typeof(RFiDGear.DataAccessLayer.DESFireKeyType), _keyTypeTarget)));
                        }

                        return ERROR.AuthenticationError;
                    }
                }
            }
            return ERROR.NotReadyError;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="_applicationMasterKey"></param>
        /// <param name="_keyTypePiccMasterKey"></param>
        /// <param name="_appID"></param>
        /// <returns></returns>
        public override ERROR DeleteMifareDesfireApplication(string _applicationMasterKey, DESFireKeyType _keyTypePiccMasterKey, int _appID)
        {
            if (readerDevice.DesfireSelectApplication((uint)_appID))
            {
                if (readerDevice.DesfireAuthenticate(_applicationMasterKey, 0x00, (byte)(int)Enum.Parse(typeof(Elatec.NET.DESFireKeyType), Enum.GetName(typeof(RFiDGear.DataAccessLayer.DESFireKeyType), _keyTypePiccMasterKey)), 1))
                {
                    if (readerDevice.DesfireDeleteApplication((uint)_appID))
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
                    if (readerDevice.DesfireDeleteApplication((uint)_appID))
                    {
                        return ERROR.NoError;
                    }
                    else
                    {
                        return ERROR.AuthenticationError;
                    }
                }
            }
            return ERROR.IOError;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="_applicationMasterKey"></param>
        /// <param name="_keyType"></param>
        /// <param name="_appID"></param>
        /// <param name="_fileID"></param>
        /// <returns></returns>
        public override ERROR DeleteMifareDesfireFile(string _applicationMasterKey, DESFireKeyType _keyType, int _appID, int _fileID)
        {
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
        public override ERROR FormatDesfireCard(string _applicationMasterKey, DESFireKeyType _keyType)
        {
            if (readerDevice.DesfireSelectApplication(0))
            {
                if (readerDevice.DesfireAuthenticate(_applicationMasterKey, 0x00, (byte)(int)Enum.Parse(typeof(Elatec.NET.DESFireKeyType), Enum.GetName(typeof(RFiDGear.DataAccessLayer.DESFireKeyType), _keyType)), 1))
                {
                    if (readerDevice.DesfireFormatTag())
                    {
                        return ERROR.NoError;
                    }
                    else
                    {
                        return ERROR.AuthenticationError;
                    }
                }
            }
            return ERROR.NotReadyError;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="_applicationMasterKey"></param>
        /// <param name="_keyType"></param>
        /// <param name="_keyNumberCurrent"></param>
        /// <param name="_appID"></param>
        /// <returns></returns>
        public override ERROR GetMifareDesfireFileList(string _applicationMasterKey, RFiDGear.DataAccessLayer.DESFireKeyType _keyType, int _keyNumberCurrent, int _appID)
        {
            if (readerDevice.DesfireSelectApplication((uint)_appID))
            {
                if (readerDevice.DesfireAuthenticate(_applicationMasterKey, (byte)_keyNumberCurrent, (byte)(int)Enum.Parse(typeof(Elatec.NET.DESFireKeyType), Enum.GetName(typeof(RFiDGear.DataAccessLayer.DESFireKeyType), _keyType)), 1))
                {
                    var appids = readerDevice.GetDesfireFileIDs();
                    if (appids != null)
                    {
                        FileIDList = appids;
                        return ERROR.NoError;
                    }
                    else
                    {
                        return ERROR.AuthenticationError;
                    }
                }
            }

            return ERROR.NotReadyError;
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
        public override ERROR GetMifareDesfireFileSettings(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumberCurrent, int _appID, int _fileNo)
        {
            if (readerDevice.DesfireSelectApplication((uint)_appID))
            {
                if (readerDevice.DesfireAuthenticate(_applicationMasterKey, (byte)_keyNumberCurrent, (byte)(int)Enum.Parse(typeof(Elatec.NET.DESFireKeyType), Enum.GetName(typeof(RFiDGear.DataAccessLayer.DESFireKeyType), _keyType)), 1))
                {
                    var fileSettings = readerDevice.GetDesFireFileSettings((byte)_fileNo);
                    uint fileSize = 0x00000000;

                    for (uint i = 9; i >= 6; i--)
                    {
                        fileSize = fileSize << 8;
                        fileSize |= (byte)(fileSettings[i]);
                    }
                    if (fileSettings != null)
                    {
                        DesfireFileSettings = new DESFireFileSettings();

                        DesfireFileSettings.FileType = fileSettings[2];
                        DesfireFileSettings.comSett = fileSettings[3];
                        DesfireFileSettings.dataFile.fileSize = fileSize;
                        DesfireFileSettings.accessRights = new byte[2];
                        DesfireFileSettings.accessRights[0] = fileSettings[4];
                        DesfireFileSettings.accessRights[1] = fileSettings[5];
                        return ERROR.NoError;
                    }
                    else
                    {
                        return ERROR.AuthenticationError;
                    }
                }
            }

            return ERROR.AuthenticationError;
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
        public override ERROR CreateMifareDesfireFile(string _appMasterKey, DESFireKeyType _keyTypeAppMasterKey, FileType_MifareDesfireFileType _fileType, DESFireAccessRights _accessRights, EncryptionMode _encMode,
                                     int _appID, int _fileNo, int _fileSize,
                                     int _minValue = 0, int _maxValue = 1000, int _initValue = 0, bool _isValueLimited = false,
                                     int _maxNbOfRecords = 100)
        {
            try
            {
                if (readerDevice.DesfireSelectApplication((uint)_appID))
                {
                    if (readerDevice.DesfireAuthenticate(_appMasterKey, (byte)0x00, (byte)(int)Enum.Parse(typeof(Elatec.NET.DESFireKeyType), Enum.GetName(typeof(RFiDGear.DataAccessLayer.DESFireKeyType), _keyTypeAppMasterKey)), 1))
                    {
                        UInt16 accessRights = 0x0000;
                        accessRights |= (byte)((((byte)_accessRights.readAccess) & 0xF0) >> 4);
                        accessRights |= (byte)((((byte)_accessRights.writeAccess) & 0x0F));
                        accessRights |= (byte)((((byte)_accessRights.readAndWriteAccess) & 0xF0) >> 4); //lsb, upper nibble
                        accessRights |= (byte)((((byte)_accessRights.changeAccess) & 0x0F)); //lsb , lower nibble

                        if (readerDevice.DesfireCreateFile((byte)_fileNo, (byte)_fileType, (byte)_encMode, accessRights, (UInt32)_fileSize))
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

                return ERROR.AuthenticationError;

            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(e, FacilityName);
                return ERROR.IOError;
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
        public override ERROR ReadMiFareDESFireChipFile(string _appMasterKey, DESFireKeyType _keyTypeAppMasterKey,
                                       string _appReadKey, DESFireKeyType _keyTypeAppReadKey, int _readKeyNo,
                                       string _appWriteKey, DESFireKeyType _keyTypeAppWriteKey, int _writeKeyNo,
                                       EncryptionMode _encMode,
                                       int _fileNo, int _appID, int _fileSize)
        {
            try
            {
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

                return ERROR.AuthenticationError;

            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(e, FacilityName);
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
