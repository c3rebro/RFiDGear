﻿/*
 * Created by SharpDevelop.
 * Date: 12.10.2017
 * Time: 11:21
 *
 */

using System;

namespace RFiDGear.DataAccessLayer
{
    // change key using mk = 0 enum
    // allow change mk = 1 or-ing
    // listing without mk = 2 or-ing
    // create del without mk = 4 or-ing
    // config changeable = 8 or-ing
    // default setting = 11
    // change using keyno = 224 enum
    // change frozen = 240 enum

    public static class Constants
    {
        public static readonly int MAX_WAIT_INSERTION = 200; //timeout for chip response in ms
        public static readonly string TITLE_SUFFIX = ""; //turns out special app versions
                                                         //public const string TITLE_SUFFIX = "DEVELOPER PREVIEW"; //turns out special app versions
    }

    /// <summary>
    /// This enum is used to indicate what kind of checksum you will be calculating.
    /// </summary>
    public enum CRC8_POLY
    {
        CRC8 = 0xd5,
        CRC8_CCITT = 0x07,
        CRC8_DALLAS_MAXIM = 0x31,
        CRC8_SAE_J1850 = 0x1D,
        CRC_8_WCDMA = 0x9b,
    };

    /// <summary>
    /// UID and Type of Cardtechnology
    /// </summary>
    public struct CARD_INFO
    {
        public CARD_INFO(CARD_TYPE _type, string _uid)
        {
            CardType = _type;
            uid = _uid;
        }

        public string uid;
        public CARD_TYPE CardType;
    }

    /// <summary>
    /// The available FileTypes for DesFire Chips 
    /// </summary>
    [Flags]
    public enum FileType_MifareDesfireFileType
    {
        StdDataFile = 0,
        BackupFile = 1,
        ValueFile = 2,
        LinearRecordFile = 3,
        CyclicRecordFile = 4
    }

    /// <summary>
    /// The availbale "Common Tasks"
    /// </summary>
    public enum TaskType_GenericChipTask
    {
        None,
        ChipIsOfType,
        ChipIsMultiChip,
        CheckUID,
        ChangeDefault
    }

    /// <summary>
    /// The availbale "Report Tasks"
    /// </summary>
    public enum TaskType_CommonTask
    {
        None,
        CreateReport,
        CheckLogicCondition,
        ExecuteProgram,
        ChangeDefault
    }

    /// <summary>
    /// The availbale "Mifare Classic Tasks"
    /// </summary>
    public enum TaskType_MifareClassicTask
    {
        None,
        ReadData,
        WriteData,
        EmptyCheck,
        ChangeDefault
    }

    /// <summary>
    /// The availbale "Mifare Ultralight" Tasks
    /// </summary>
    public enum TaskType_MifareUltralightTask
    {
        None,
        ReadData,
        WriteData,
        ChangeDefault
    }

    /// <summary>
    /// The availbale "Mifare Desfire Tasks"
    /// </summary>
    public enum TaskType_MifareDesfireTask
    {
        None,
        AppExistCheck,
        ReadAppSettings,
        PICCMasterKeyChangeover,
        CreateApplication,
        AuthenticateApplication,
        ApplicationKeyChangeover,
        CreateFile,
        ReadData,
        WriteData,
        DeleteFile,
        DeleteApplication,
        FormatDesfireCard,
        ChangeDefault

    }

    /// <summary>
    /// Select DataBlock in Data Explorer
    /// </summary>
    [Flags]
    public enum DataExplorer_DataBlock
    {
        Block0 = 0,
        Block1 = 1,
        Block2 = 2,
        Block3 = 3
    }

    /// <summary>
    /// Build a SectorTrailer / Select DataBlock in Sector Trailer Access Bits
    /// </summary>
    public enum SectorTrailer_DataBlock
    {
        Block0 = 0,
        Block1 = 1,
        Block2 = 2,
        BlockAll = 3
    }

    /// <summary>
    /// Build a "SectorTrailer" / Determine Access To DataBlocks
    /// </summary>
    [Flags]
    public enum SectorTrailer_AccessType
    {
        WriteKeyB = 1,
        ReadKeyB = 2,
        WriteAccessBits = 4,
        ReadAccessBits = 8,
        WriteKeyA = 16,
        ReadKeyA = 32
    }

    /// <summary>
    ///
    /// </summary>
    [Flags]
    public enum AccessCondition_MifareDesfireAppCreation
    {
        ChangeKeyUsingMK = 0,
        ChangeKeyUsingKeyNo = 224,
        ChangeKeyFrozen = 240
    }

    /// <summary>
    ///
    /// </summary>
    public enum AccessCondition_MifareClassicSectorTrailer
    {
        NotApplicable,
        NotAllowed,
        Allowed_With_KeyA,
        Allowed_With_KeyB,
        Allowed_With_KeyA_Or_KeyB
    }

    /// <summary>
    /// Currently Available Cardtechnologies
    /// </summary>
    public enum CARD_TYPE
    {
        NOTAG = 0,
        // LF Tags
        EM4102 = 0x40,    // "EM4x02/CASI-RUSCO" (aka IDRO_A)
        HITAG1S = 0x41,   // "HITAG 1/HITAG S"   (aka IDRW_B)
        HITAG2 = 0x42,    // "HITAG 2"           (aka IDRW_C)
        EM4150 = 0x43,    // "EM4x50"            (aka IDRW_D)
        AT5555 = 0x44,    // "T55x7"             (aka IDRW_E)
        ISOFDX = 0x45,    // "ISO FDX-B"         (aka IDRO_G)
        EM4026 = 0x46,    // N/A                 (aka IDRO_H)
        HITAGU = 0x47,    // N/A                 (aka IDRW_I)
        EM4305 = 0x48,    // "EM4305"            (aka IDRW_K)
        HIDPROX = 0x49,	// "HID Prox"
        TIRIS = 0x4A,	    // "ISO HDX/TIRIS"
        COTAG = 0x4B,	    // "Cotag"
        IOPROX = 0x4C,	// "ioProx"
        INDITAG = 0x4D,	// "Indala"
        HONEYTAG = 0x4E,	// "NexWatch"
        AWID = 0x4F,	    // "AWID"
        GPROX = 0x50,	    // "G-Prox"
        PYRAMID = 0x51,	// "Pyramid"
        KERI = 0x52,	    // "Keri"
        DEISTER = 0x53,	// "Deister"
        CARDAX = 0x54,	// "Cardax"
        NEDAP = 0x55,	    // "Nedap"
        PAC = 0x56,	    // "PAC"
        IDTECK = 0x57,	// "IDTECK"
        ULTRAPROX = 0x58,	// "UltraProx"
        ICT = 0x59,	    // "ICT"
        ISONAS = 0x5A,	// "Isonas"
        // HF Tags
        MIFARE = 0x80,	// "ISO14443A/MIFARE"
        ISO14443B = 0x81,	// "ISO14443B"
        ISO15693 = 0x82,	// "ISO15693"
        LEGIC = 0x83,	    // "LEGIC"
        HIDICLASS = 0x84,	// "HID iCLASS"
        FELICA = 0x85,	// "FeliCa"
        SRX = 0x86,	    // "SRX"
        NFCP2P = 0x87,	// "NFC Peer-to-Peer"
        BLE = 0x88,	    // "Bluetooth Low Energy"
        TOPAZ = 0x89,     // "Topaz"
        CTS = 0x8A,       // "CTS256 / CTS512"
        BLELC = 0x8B,     // "Bluetooth Low Energy LEGIC Connect"
        // Custom
        Unspecified = 0xB0,
        NTAG = 0xB1,
        MifareMini = 0xB2,
        Mifare1K = 0xB3,
        Mifare2K = 0xB4,
        Mifare4K = 0xB5,
        SAM_AV1 = 0xB6,
        SAM_AV2 = 0xB7,
        MifarePlus_SL0_1K = 0xB9,
        MifarePlus_SL0_2K = 0xBA,
        MifarePlus_SL0_4K = 0xBB,
        MifarePlus_SL1_1K = 0xBC,
        MifarePlus_SL1_2K = 0xBD,
        MifarePlus_SL1_4K = 0xBE,
        MifarePlus_SL2_1K = 0xBF,
        MifarePlus_SL2_2K = 0xC0,
        MifarePlus_SL2_4K = 0xC1,
        MifarePlus_SL3_1K = 0xC2,
        MifarePlus_SL3_2K = 0xC3,
        MifarePlus_SL3_4K = 0xC4,
        DESFire = 0xC5,
        DESFireEV1 = 0xC6,
        DESFireEV2 = 0xC7,
        DESFireEV3 = 0xC8,
        SmartMX_DESFire_Generic = 0xC9,
        SmartMX_DESFire_2K = 0xCA,
        SmartMX_DESFire_4K = 0xCB,
        SmartMX_DESFire_8K = 0xCC,
        SmartMX_DESFire_16K = 0xCD,
        SmartMX_DESFire_32K = 0xCE,
        DESFire_256 = 0xD0,
        DESFire_2K = 0xD1,
        DESFire_4K = 0xD2,
        DESFireEV1_256 = 0xD3,
        DESFireEV1_2K = 0xD4,
        DESFireEV1_4K = 0xD5,
        DESFireEV1_8K = 0xD6,
        DESFireEV2_2K = 0xD7,
        DESFireEV2_4K = 0xD8,
        DESFireEV2_8K = 0xD9,
        DESFireEV2_16K = 0xDA,
        DESFireEV2_32K = 0xDB,
        DESFireEV3_2K = 0xDC,
        DESFireEV3_4K = 0xDD,
        DESFireEV3_8K = 0xDE,
        DESFireEV3_16K = 0xDF,
        DESFireEV3_32K = 0xE0,
        DESFireLight = 0xE1,
        SmartMX_Mifare_1K = 0xF9,
        SmartMX_Mifare_4K = 0xFA,
        MifareUltralight = 0xFB,
        MifareUltralightC = 0xFC,
        GENERIC_T_CL_A = 0xFF
    };

    /// <summary>
    /// Currently Available Error Conditions
    /// </summary>
    public enum ERROR
    {
        Empty,
        NoError,
        AuthenticationError,
        NotReadyError,
        IOError,
        ItemAlreadyExistError,
        IsNotTrue,
        IsNotFalse,
        OutOfMemory,
        NotAllowed
    }

    /// <summary>
    /// The Possible Logical States For Checkpoint Counter
    /// </summary>
    public enum EQUALITY_OPERATOR
    {
        EQUAL,
        LESS_THAN,
        MORE_THAN,
        LESS_OR_EQUAL,
        MORE_OR_EQUAL
    };


    /// <summary>
    /// The Possible Logical States
    /// </summary>
    public enum LOGIC_STATE
    {
        AND,
        OR,
        NAND,
        NOR,
        NOT,
        COUNT,
        COMPARE
    };

    /// <summary>
    /// Key Formatting Errors
    /// </summary>
    public enum KEY_ERROR
    {
        KEY_IS_EMPTY,
        KEY_HAS_WRONG_LENGTH,
        KEY_HAS_WRONG_FORMAT,
        NO_ERROR
    };

    /// <summary>
    /// Available Readers
    /// </summary>
    [Flags]
    public enum ReaderTypes
    {
        None,
        Elatec,
        PCSC
    };

    public enum KeyType_MifareDesFireKeyType
    {
        DefaultDesfireCardCardMasterKey,
        DefaultDesfireCardApplicationMasterKey,
        DefaultDesfireCardReadKey,
        DefaultDesfireCardWriteKey
    }

    [Flags]
    public enum DESFireKeyType
    {
        DF_KEY_DES = 0,
        DF_KEY_3K3DES = 64,
        DF_KEY_AES = 128
    }

    /// <summary>
    /// 
    /// </summary>
    [Flags]
    public enum DESFireKeySettings
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

    public class DESFireFileSettings
    {
        public byte[] accessRights;
        public byte FileType;
        public byte comSett;
        public DataFileSetting dataFile;
        public RecordFileSetting recordFile;
        public ValueFileSetting valueFile;
    }

    public struct DataFileSetting
    {
        public uint fileSize;
    }

    public struct RecordFileSetting
    {

    }

    public struct ValueFileSetting
    {

    }

    public struct AccessBits
    {
        public short c1;
        public short c2;
        public short c3;
    }

    public struct SectorAccessBits
    {
        public int Cx;

        public AccessBits d_data_block0_access_bits;
        public AccessBits d_data_block1_access_bits;
        public AccessBits d_data_block2_access_bits;
        public AccessBits d_sector_trailer_access_bits;
    }

    /// <summary>
    /// 
    /// </summary>
    [Flags]
    public enum TaskAccessRights
    {
        AR_KEY0 = 0,
        AR_KEY1 = 1,
        AR_KEY2 = 2,
        AR_KEY3 = 3,
        AR_KEY4 = 4,
        AR_KEY5 = 5,
        AR_KEY6 = 6,
        AR_KEY7 = 7,
        AR_KEY8 = 8,
        AR_KEY9 = 9,
        AR_KEY10 = 10,
        AR_KEY11 = 11,
        AR_KEY12 = 12,
        AR_KEY13 = 13,
        AR_FREE = 14,
        AR_NEVER = 15
    }

    /// <summary>
    /// 
    /// </summary>
    public struct DESFireAccessRights
    {
        public TaskAccessRights readAccess;
        public TaskAccessRights writeAccess;
        public TaskAccessRights changeAccess;
        public TaskAccessRights readAndWriteAccess;
    }

    /// <summary>
    /// 
    /// </summary>
    [Flags]
    public enum EncryptionMode
    {
        CM_PLAIN = 0,
        CM_MAC = 1,
        CM_ENCRYPT = 3,
        CM_UNKNOWN = 255
    }

    /// <summary>
    /// 
    /// </summary>
    public struct MifareDesfireDefaultKeys
    {
        public MifareDesfireDefaultKeys(KeyType_MifareDesFireKeyType _keyType, DESFireKeyType _encryptionType, string _key)
        {
            KeyType = _keyType;
            EncryptionType = _encryptionType;
            Key = _key;
        }

        public KeyType_MifareDesFireKeyType KeyType;
        public DESFireKeyType EncryptionType;

        public string Key;
    }

    /// <summary>
    /// 
    /// </summary>
    public struct MifareClassicDefaultKeys
    {
        public MifareClassicDefaultKeys(int _keyNumber, string _accessBits)
        {
            KeyNumber = _keyNumber;
            accessBits = _accessBits;
        }

        private string accessBits;

        public int KeyNumber;
        public string AccessBits { get { return accessBits; } set { accessBits = value; } }
    }
}

