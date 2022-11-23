using Elatec.NET.Model;
using Elatec.NET;

using RFiDGear.Model;

using Log4CSharp;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace RFiDGear.DataAccessLayer.Remote.FromIO
{
    public class ElatecNetProvider : ReaderDevice, IDisposable
    {
        private static readonly string FacilityName = "RFiDGear";

        private readonly TWN4ReaderDevice readerDevice;

        private ChipModel card;
        private bool _disposed;

        #region Common

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

        public override ERROR ReadChipPublic()
        {
            try
            {
                if (readerDevice != null)
                {
                    if(!IsConnected)
                    {
                        Instance.Connect();
                    }                   

                    card = readerDevice.GetSingleChip();

                    if (!string.IsNullOrWhiteSpace(card?.ChipIdentifier))
                    {
                        try
                        {
                            readerDevice.Beep(1, 50, 1000, 100);

                            GenericChip = new GenericChipModel(card.ChipIdentifier, (CARD_TYPE)card.CardType);
                            readerDevice.GreenLED(true);
                            readerDevice.RedLED(false);
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
                        readerDevice.GreenLED(false);
                        readerDevice.RedLED(true);
                        GenericChip = null;

                        return ERROR.NotReadyError;
                    }
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
            return ERROR.IOError;

        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override ERROR Connect()
        {
            return ERROR.NoError;
        }

        #endregion

        #region MifareClassic
        public override ERROR WriteMifareClassicSingleBlock(int _blockNumber, string _aKey, string _bKey, byte[] buffer)
        {
            return WriteMifareClassicSingleSector(
                CustomConverter.GetSectorNumberFromChipBasedDataBlockNumber(_blockNumber), _aKey, _bKey, buffer);
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
                    return ERROR.IOError; // IO ERROR
                }
            }

            if(Sector.IsAuthenticated)
            {
                return ERROR.NoError; //NO ERROR
            }

            return ERROR.AuthenticationError; // Auth ERROR
        }

        #endregion

        #region MifareUltralight
        public override ERROR ReadMifareUltralightSinglePage(int _pageNo)
        {
            throw new NotImplementedException();
        }
        #endregion

        #region MifareDesfire
        public override ERROR GetMiFareDESFireChipAppIDs(string _appMasterKey, DESFireKeyType _keyTypeAppMasterKey)
        {
            if (DesfireChip == null)
            {
                DesfireChip = new MifareDesfireChipModel();
            }

            DesfireChip.AppList = new System.Collections.Generic.List<MifareDesfireAppModel>();

            foreach (var appid in readerDevice.GetDesfireAppIDs(_appMasterKey, (Elatec.NET.DESFireKeyType)_keyTypeAppMasterKey))
            {
                DesfireChip.AppList.Add(new MifareDesfireAppModel(appid));
            }
            
            
            return ERROR.NoError;
        }
 
        public override ERROR CreateMifareDesfireFile(string _appMasterKey, DESFireKeyType _keyTypeAppMasterKey, FileType_MifareDesfireFileType _fileType, DESFireAccessRights _accessRights, EncryptionMode _encMode,
                                     int _appID, int _fileNo, int _fileSize,
                                     int _minValue = 0, int _maxValue = 1000, int _initValue = 0, bool _isValueLimited = false,
                                     int _maxNbOfRecords = 100)
        {
            throw new NotImplementedException();
        }

        public override ERROR ReadMiFareDESFireChipFile(string _appMasterKey, DESFireKeyType _keyTypeAppMasterKey,
                                       string _appReadKey, DESFireKeyType _keyTypeAppReadKey, int _readKeyNo,
                                       string _appWriteKey, DESFireKeyType _keyTypeAppWriteKey, int _writeKeyNo,
                                       EncryptionMode _encMode,
                                       int _fileNo, int _appID, int _fileSize)
        {
            throw new NotImplementedException();
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

        public override ERROR AuthToMifareDesfireApplication(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumber, int _appID)
        {
            ReadChipPublic();

            return readerDevice.DesfireAuthenticate(
                _applicationMasterKey, 
                (byte)_keyNumber,
                (byte)(int)Enum.Parse(typeof(Elatec.NET.DESFireKeyType), Enum.GetName(typeof(RFiDGear.DataAccessLayer.DESFireKeyType), _keyType)),
                1) == true ? ERROR.NoError : ERROR.NotAllowed;

        }
        public override ERROR GetMifareDesfireAppSettings(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumberCurrent, int _appID)
        {
            AuthToMifareDesfireApplication(_applicationMasterKey, _keyType, _keyNumberCurrent, _appID);
            
            if(readerDevice.GetDesFireKeySettings((uint)_appID))
            {
                DesfireFileSettings.accessRights[0] = readerDevice.KeySettings;
                return ERROR.NoError;
            }
            else
            {
                return ERROR.AuthenticationError;
            }
        }
        public override ERROR CreateMifareDesfireApplication(string _piccMasterKey, DESFireKeySettings _keySettingsTarget,
                                        DESFireKeyType _keyTypePiccMasterKey, DESFireKeyType _keyTypeTargetApplication,
                                        int _maxNbKeys, int _appID, bool authenticateToPICCFirst = true)
        {
            var s = (int)Enum.Parse(typeof(Elatec.NET.DESFireKeyType), Enum.GetName(typeof(RFiDGear.DataAccessLayer.DESFireKeyType), _keyTypeTargetApplication));

            return readerDevice.DesfireCreateApplication((Elatec.NET.DESFireKeySettings)_keySettingsTarget, (Elatec.NET.DESFireKeyType)Enum.Parse(typeof(Elatec.NET.DESFireKeyType), Enum.GetName(typeof(RFiDGear.DataAccessLayer.DESFireKeyType), _keyTypeTargetApplication)), _maxNbKeys, _appID) == true ? ERROR.NoError : ERROR.NotAllowed;
        }
        public override ERROR ChangeMifareDesfireApplicationKey(string _applicationMasterKeyCurrent, int _keyNumberCurrent, DESFireKeyType _keyTypeCurrent,
                                        string _applicationMasterKeyTarget, int _keyNumberTarget, int selectedDesfireAppKeyVersionTargetAsIntint,
                                        DESFireKeyType _keyTypeTarget, int _appIDCurrent, int _appIDTarget, DESFireKeySettings keySettings, int keyVersion)
        {
            throw new NotImplementedException();
        }
        public override ERROR DeleteMifareDesfireApplication(string _applicationMasterKey, DESFireKeyType _keyType, int _appID)
        {
            throw new NotImplementedException();
        }
        public override ERROR DeleteMifareDesfireFile(string _applicationMasterKey, DESFireKeyType _keyType, int _appID, int _fileID)
        {
            throw new NotImplementedException();
        }
        public override ERROR FormatDesfireCard(string _applicationMasterKey, DESFireKeyType _keyType, int _appID = 0)
        {
            throw new NotImplementedException();
        }
        public override ERROR GetMifareDesfireFileList(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumberCurrent, int _appID)
        {
            throw new NotImplementedException();
        }
        public override ERROR GetMifareDesfireFileSettings(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumberCurrent, int _appID, int _fileNo)
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
            readerDevice.GreenLED(false);
            GC.SuppressFinalize(this);
        }
    }
}
