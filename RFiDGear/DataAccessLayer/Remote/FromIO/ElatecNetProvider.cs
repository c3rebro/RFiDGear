﻿using Elatec.NET.Model;
using Elatec.NET;

using RFiDGear.Model;

using Log4CSharp;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RFiDGear.DataAccessLayer.Remote.FromIO
{
    class ElatecNetProvider : ReaderDevice
    {
        private string FacilityName = "RFiDGear";
        private TWN4ReaderDevice readerDevice;
        private ChipModel card;

        #region Common

        public ElatecNetProvider()
        {
            try
            {
                readerDevice = new TWN4ReaderDevice();
                //GenericChip = new GenericChipModel("", CARD_TYPE.Unspecified);
                AppIDList = new uint[0];
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(e, FacilityName);
            }
        }

        public ElatecNetProvider(ReaderTypes readerType)
        {
            try
            {
                readerDevice = new TWN4ReaderDevice();
                //GenericChip = new GenericChipModel("", CARD_TYPE.Unspecified);
                AppIDList = new uint[0];
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
                if (readerDevice.Connect())
                {
                    readerDevice.Beep();
                    readerDevice.GreenLED(true);

                    card = readerDevice.GetSingleChip();

                    if (card?.ChipIdentifier != null)
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
                            LogWriter.CreateLogEntry(e, FacilityName);
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

                LogWriter.CreateLogEntry(e, FacilityName);

                readerDevice?.Disconnect();
                return ERROR.IOError;
            }
            readerDevice?.Disconnect();
            return ERROR.IOError;
                
        }

        #endregion

        #region MifareClassic
        public override ERROR ReadMiFareClassicSingleSector(int sectorNumber, string aKey, string bKey)
        {
            throw new NotImplementedException();
        }
        public override ERROR WriteMiFareClassicSingleSector(int sectorNumber, string _aKey, string _bKey, byte[] buffer)
        {
            throw new NotImplementedException();
        }
        public override ERROR WriteMiFareClassicSingleBlock(int _blockNumber, string _aKey, string _bKey, byte[] buffer)
        {
            throw new NotImplementedException();
        }
        public override ERROR ReadMiFareClassicSingleBlock(int _blockNumber, string _aKey, string _bKey)
        {
            throw new NotImplementedException();
        }
        public override ERROR WriteMiFareClassicWithMAD(int _madApplicationID, int _madStartSector,
                                               string _aKeyToUse, string _bKeyToUse, string _aKeyToWrite, string _bKeyToWrite,
                                               string _madAKeyToUse, string _madBKeyToUse, string _madAKeyToWrite, string _madBKeyToWrite,
                                               byte[] buffer, byte _madGPB, SectorAccessBits _sab, bool _useMADToAuth = false, bool _keyToWriteUseMAD = false)
        {
            throw new NotImplementedException();
        }
        public override ERROR ReadMiFareClassicWithMAD(int madApplicationID, string _aKeyToUse, string _bKeyToUse, string _madAKeyToUse, string _madBKeyToUse, int _length, byte _madGPB, bool _useMADToAuth = true, bool _aiToUseIsMAD = false)
        {
            throw new NotImplementedException();
        }
        #endregion

        #region MifareUltralight
        public override ERROR ReadMifareUltralightSinglePage(int _pageNo)
        {
            throw new NotImplementedException();
        }
        #endregion

        #region MifareDesfire
        public override ERROR GetMiFareDESFireChipAppIDs(string _appMasterKey = "00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00", DESFireKeyType _keyTypeAppMasterKey = DESFireKeyType.DF_KEY_DES)
        {
            throw new NotImplementedException();
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
        public override ERROR AuthToMifareDesfireApplication(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumber, int _appID = 0)
        {
            throw new NotImplementedException();
        }
        public override ERROR GetMifareDesfireAppSettings(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumberCurrent = 0, int _appID = 0)
        {
            throw new NotImplementedException();
        }
        public override ERROR CreateMifareDesfireApplication(string _piccMasterKey, DESFireKeySettings _keySettingsTarget, 
                                        DESFireKeyType _keyTypePiccMasterKey, DESFireKeyType _keyTypeTargetApplication, 
                                        int _maxNbKeys, int _appID, bool authenticateToPICCFirst = true)
        {
            throw new NotImplementedException();
        }
        public override ERROR ChangeMifareDesfireApplicationKey(string _applicationMasterKeyCurrent, int _keyNumberCurrent, DESFireKeyType _keyTypeCurrent,
                                        string _applicationMasterKeyTarget, int _keyNumberTarget, int selectedDesfireAppKeyVersionTargetAsIntint,
                                        DESFireKeyType _keyTypeTarget, int _appIDCurrent, int _appIDTarget, DESFireKeySettings keySettings)
        {
            throw new NotImplementedException();
        }
        public override ERROR DeleteMifareDesfireApplication(string _applicationMasterKey, DESFireKeyType _keyType, int _appID = 0)
        {
            throw new NotImplementedException();
        }
        public override ERROR DeleteMifareDesfireFile(string _applicationMasterKey, DESFireKeyType _keyType, int _appID = 0, int _fileID = 0)
        {
            throw new NotImplementedException();
        }
        public override ERROR FormatDesfireCard(string _applicationMasterKey, DESFireKeyType _keyType, int _appID = 0)
        {
            throw new NotImplementedException();
        }
        public override ERROR GetMifareDesfireFileList(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumberCurrent = 0, int _appID = 0)
        {
            throw new NotImplementedException();
        }
        public override ERROR GetMifareDesfireFileSettings(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumberCurrent = 0, int _appID = 0, int _fileNo = 0)
        {
            throw new NotImplementedException();
        }
        #endregion
    }
}
