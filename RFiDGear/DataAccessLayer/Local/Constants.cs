/*
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
		public const int MAX_WAIT_INSERTION = 200; //timeout for chip response in ms
		public const string TITLE_SUFFIX = ""; //turns out special app versions
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
	/// The available FileTypes for DesFire Chips 
	/// </summary>
	public enum FileType_MifareDesfireFileType
	{
		StdDataFile,
		BackupFile,
		ValueFile,
		CyclicRecordFile,
		LinearRecordFile
	}

    /// <summary>
    /// The availbale "Common Tasks"
    /// </summary>
    public enum TaskType_GenericChipTask
    {
        None,
        ChipIsOfType,
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
		ChangeDefault,
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
		FormatDesfireCard

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
		// Custom
		Unspecified = 0xE0,
		ISO15693 = 0xE1,
		MifareMini = 0xE2,
		Mifare1K = 0xE3,
		Mifare2K = 0xE4,
		Mifare4K = 0xE5,
		DESFire = 0xE6,
		DESFireEV1 = 0xE7,
		DESFireEV2 = 0xE8,
		MifarePlus_SL0_1K = 0xE9,
		MifarePlus_SL0_2K = 0xEA,
		MifarePlus_SL0_4K = 0xEB,
		MifarePlus_SL1_1K = 0xF5,
		MifarePlus_SL1_2K = 0xF6,
		MifarePlus_SL1_4K = 0xF7,
		MifarePlus_SL2_1K = 0xEC,
		MifarePlus_SL2_2K = 0xED,
		MifarePlus_SL2_4K = 0xEE,
		MifarePlus_SL3_1K = 0xEF,
		MifarePlus_SL3_2K = 0xF0,
		MifarePlus_SL3_4K = 0xF1,
		SmartMX_Mifare_2K = 0xF2,
		SmartMX_Mifare_4K = 0xF3,
		MifareUltralight = 0xF4,
		__UNSUPPORTED__DESFireEV2 = 0xFE,
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
		DeviceNotReadyError,
		IOError,
        ItemAlreadyExistError,
		IsNotTrue,
		IsNotFalse,
		OutOfMemory,
		NotAllowed
	}

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
		Admitto,
		AxessTMC13,
		Deister,
        Elatec,
        GigaTMS,
        Gunnebo,
		IdOnDemand,
		PCSC,
		Promag,
		RFIDeas,
		Rpleth,
		SCIEL,
		SmartID,
		STidPRG
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
		DF_KEY_3K3DES = 0,
		DF_KEY_AES = 64,
		DF_KEY_DES = 128
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

	public struct DESFireFileSettings
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

    #region LibLogicalAccess enums

    /*
    /// <summary>
    /// 
    /// </summary>
    [Flags]
    public enum DESFireKeyType
    {
        DF_KEY_3K3DES,
        DF_KEY_AES,
        DF_KEY_DES
    }

    */
    #endregion
}

