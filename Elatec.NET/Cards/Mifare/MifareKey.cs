using System;

namespace Elatec.NET.Cards.Mifare
{
    #region Mifare Classic Key


    /// <summary>
    /// 
    /// </summary>
    public class MifareClassicKey
    {
        private int keyNumber;
        private string accessBits;
        private string key;

        public MifareClassicKey(int _keyNumber, string _accessBits)
        {
            KeyNumber = _keyNumber;
            accessBits = _accessBits;
        }

        public int KeyNumber
        {
            get => keyNumber; set => keyNumber = value;
        }
        public string AccessBits
        {
            get => accessBits; set => accessBits = value;
        }
        public string Key
        {
            get => key; set => key = value;
        }

    }

    /// <summary>
    /// 
    /// </summary>
    public enum MifareKeyType
    {
        KT_KEY_A,
        KT_KEY_B
    }

    /// <summary>
    /// 
    /// </summary>
    public class AccessBits
    {
        private ushort c1;
        private ushort c2;
        private ushort c3;

        public ushort C1 { get => c1; set => c1 = value; }
        public ushort C2 { get => c2; set => c2 = value; }
        public ushort C3 { get => c3; set => c3 = value; }
    }

    /// <summary>
    /// 
    /// </summary>
    public class SectorAccessBits
    {
        private int cx;

        private AccessBits d_data_block0_access_bits;
        private AccessBits d_data_block1_access_bits;
        private AccessBits d_data_block2_access_bits;
        private AccessBits d_sector_trailer_access_bits;

        public int Cx { get => cx; set => cx = value; }

        public AccessBits SAB_data_block0_access_bits { get => d_data_block0_access_bits; set => d_data_block0_access_bits = value; }
        public AccessBits SAB_data_block1_access_bits { get => d_data_block1_access_bits; set => d_data_block1_access_bits = value; }
        public AccessBits SAB_data_block2_access_bits { get => d_data_block2_access_bits; set => d_data_block2_access_bits = value; }
        public AccessBits SAB_sector_trailer_access_bits { get => d_sector_trailer_access_bits; set => d_sector_trailer_access_bits = value; }
    }

    #endregion

    #region Mifare Desfire Key

    /// <summary>
    /// 
    /// </summary>
    public class MifareDesfireKey
    {
        private DESFireKeyType keyType;
        private DESFireKeySettings keySettings;
        private string key;

        public MifareDesfireKey(DESFireKeyType _encryptionType, string _key)
        {
            KeyType = _encryptionType;
            Key = _key;
        }

        public MifareDesfireKey(DESFireKeyType _encryptionType, string _key, DESFireKeySettings _keySettings)
        {
            KeyType = _encryptionType;
            Key = _key;
            KeySettings = _keySettings;
        }

        public DESFireKeyType KeyType
        {
            get => keyType; set => keyType = value;
        }
        public string Key
        {
            get => key; set => key = value;
        }
        public DESFireKeySettings KeySettings
        {
            get => keySettings; set => keySettings = value;
        }
    }

    /// <summary>
    /// Access Rights <see cref="DESFireAppAccessRights"/>
    /// Number of Keys
    /// KeyType <see cref="DESFireKeyType"/>
    /// </summary>
    public class DESFireKeySettings
    {
        private DESFireAppAccessRights accessRights;
        private DESFireKeyType keyType;
        private UInt32 numberOfKeys;

        public DESFireAppAccessRights AccessRights
        {
            get => accessRights; set => accessRights = value;
        }
        public UInt32 NumberOfKeys
        {
            get => numberOfKeys; set => numberOfKeys = value;
        }
        public DESFireKeyType KeyType
        {
            get => keyType; set => keyType = value;
        }
    }

    /// <summary>
    /// CRYPTOMODE_AES128 = 2,
    /// CRYPTOMODE_3DES = 0
    /// CRYPTOMODE_3K3DES = 1
    /// </summary>    
    public enum DESFireKeyType
    {
        DF_KEY_DES = 0,
        DF_KEY_3K3DES = 1,
        DF_KEY_AES = 2
    }

    /// <summary>
    /// 
    /// </summary>
    public class DESFireFileSettings
    {
        private DESFireFileAccessRights fileAccessRights;
        private DESFireFileType fileType;
        private byte comSett;
        private DataFileSetting dataFileSetting;
        private RecordFileSetting recordFileSetting;
        private ValueFileSetting valueFileSetting;

        public DESFireFileSettings()
        {
            accessRights = new DESFireFileAccessRights();
        }

        public DESFireFileAccessRights accessRights
        {
            get => fileAccessRights; set => fileAccessRights = value;
        }
        public DESFireFileType FileType
        {
            get => fileType; set => fileType = value;
        }
        public byte ComSett
        {
            get => comSett; set => comSett = value;
        }
        public DataFileSetting DataFileSetting
        {
            get => dataFileSetting; set => dataFileSetting = value;
        }
        public RecordFileSetting RecordFileSetting
        {
            get => recordFileSetting; set => recordFileSetting = value;
        }
        public ValueFileSetting ValueFileSetting
        {
            get => valueFileSetting; set => valueFileSetting = value;
        }
    }

    /// <summary>
    /// DESF_FILETYPE_STDDATAFILE           0
    /// DESF_FILETYPE_BACKUPDATAFILE        1
    /// DESF_FILETYPE_VALUEFILE             2
    /// DESF_FILETYPE_LINEARRECORDFILE      3
    /// DESF_FILETYPE_CYCLICRECORDFILE      4
    /// </summary>
    public enum DESFireFileType
    {
        DF_FT_STDDATAFILE = 0,
        DF_FT_BACKUPDATAFILE = 1,
        DF_FT_VALUEFILE = 2,
        DF_FT_LINEARRECORDFILE = 3,
        DF_FT_CYCLICRECORDFILE = 4
    }

    public class DataFileSetting
    {
        private UInt32 fileSize;
        public UInt32 FileSize
        {
            get => fileSize; set => fileSize = value;
        }
    }

    public class RecordFileSetting
    {
        private UInt32 recordSize;
        private UInt32 maxNumberOfRecords;
        private UInt32 currentNumberOfRecords;

        public UInt32 RecordSize
        {
            get => recordSize; set => recordSize = value;
        }
        public UInt32 MaxNumberOfRecords
        {
            get => maxNumberOfRecords; set => maxNumberOfRecords = value;
        }
        public UInt32 CurrentNumberOfRecords
        {
            get => currentNumberOfRecords; set => currentNumberOfRecords = value;
        }
    }

    public class ValueFileSetting
    {
        private UInt32 upperLimit;
        private UInt32 lowerLimit;
        private UInt32 limitedCreditValue;

        private byte limitedCreditEnabled;
        private byte freeGetValue;
        private byte rFU;

        public UInt32 UpperLimit
        {
            get => upperLimit; set => upperLimit = value;
        }
        public UInt32 LowerLimit
        {
            get => lowerLimit; set => lowerLimit = value;
        }
        public UInt32 LimitedCreditValue
        {
            get => limitedCreditValue; set => limitedCreditValue = value;
        }

        public byte LimitedCreditEnabled
        {
            get => limitedCreditEnabled; set => limitedCreditEnabled = value;
        }
        public byte FreeGetValue
        {
            get => freeGetValue; set => freeGetValue = value;
        }
        public byte RFU
        {
            get => rFU; set => rFU = value;
        }
    }

    /// <summary>
    /// KS_CHANGE_KEY_WITH_MK = 0,
    /// KS_ALLOW_CHANGE_MK = 1,
    /// KS_FREE_LISTING_WITHOUT_MK = 2,
    /// KS_FREE_CREATE_DELETE_WITHOUT_MK = 4,
    /// KS_CONFIGURATION_CHANGEABLE = 8,
    /// KS_DEFAULT = 11,
    /// KS_CHANGE_KEY_WITH_TARGETED_KEYNO = 224,
    /// KS_CHANGE_KEY_FROZEN = 240
    /// </summary>
    [Flags]
    public enum DESFireAppAccessRights
    {
        KS_CHANGE_KEY_WITH_MK = 0,
        KS_ALLOW_CHANGE_MK = 1,
        KS_FREE_LISTING_WITHOUT_MK = 2,
        KS_FREE_CREATE_DELETE_WITHOUT_MK = 4,
        KS_CONFIGURATION_CHANGEABLE = 8,
        KS_DEFAULT = 11,
        KS_CHANGE_KEY_WITH_TARGETED_KEYNO = 224,
        KS_CHANGE_KEY_FROZEN = 240
    }

    /// <summary>
    /// AccessRights to access a file. Stored in the app keysettings
    /// </summary>
    public class DESFireFileAccessRights
    {
        private byte readKeyNo;
        private byte writeKeyNo;
        private byte readWriteKeyNo;
        private byte changeKeyNo;

        public byte ReadKeyNo
        {
            get => readKeyNo; set => readKeyNo = value;
        }
        public byte WriteKeyNo
        {
            get => writeKeyNo; set => writeKeyNo = value;
        }
        public byte ReadWriteKeyNo
        {
            get => readWriteKeyNo; set => readWriteKeyNo = value;
        }
        public byte ChangeKeyNo
        {
            get => changeKeyNo; set => changeKeyNo = value;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public enum EncryptionMode
    {
        CM_PLAIN = 0,
        CM_MAC = 1,
        CM_ENCRYPT = 3
    }

    #endregion

}