/*
 * Created by SharpDevelop.
 * Date: 12.10.2017
 * Time: 11:21
 *
 */

using LibLogicalAccess;

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
		public const string TITLE_SUFFIX = "DEVELOPER PREVIEW"; //turns out special app versions
	}
	
	///
	/// This enum is used to indicate what kind of checksum you will be calculating.
	/// 
	public enum CRC8_POLY
	{
		CRC8 = 0xd5,
		CRC8_CCITT = 0x07,
		CRC8_DALLAS_MAXIM = 0x31,
		CRC8_SAE_J1850 = 0x1D,
		CRC_8_WCDMA = 0x9b,
	};
	
	/// <summary>
	///
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
	///
	/// </summary>
	public enum TaskType_MifareClassicTask
	{
		None,
		ReadData,
		WriteData,
		ChangeDefault
	}

	public enum TaskType_MifareDesfireTask
	{
		None,
		FormatDesfireCard,
		PICCMasterKeyChangeover,
		ApplicationKeyChangeover,
		ReadData,
		WriteData,
		CreateApplication,
		CreateFile,
		DeleteApplication,
		DeleteFile,
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
	/// Select DataBlock in Sector Trailer Access Bits
	/// </summary>
	public enum SectorTrailer_DataBlock
	{
		Block0 = 0,
		Block1 = 1,
		Block2 = 2,
		BlockAll = 3
	}

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
	///
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
	/// Description of Constants.
	/// </summary>
	public enum CARD_TYPE
	{
		Unspecified,
		ISO15693,
		Mifare1K,
		Mifare2K,
		Mifare4K,
		DESFire,
		DESFireEV1,
		DESFireEV2,
		MifarePlus_SL3_1K,
		MifarePlus_SL3_2K,
		MifarePlus_SL3_4K,
		MifareUltralight
	};

	/// <summary>
	///
	/// </summary>
	public enum ERROR
	{
		Empty,
		NoError,
		AuthenticationError,
		DeviceNotReadyError,
		IOError
	}

	/// <summary>
	///
	/// </summary>
	public enum KEY_ERROR
	{
		KEY_IS_EMPTY,
		KEY_HAS_WRONG_LENGTH,
		KEY_HAS_WRONG_FORMAT,
		NO_ERROR
	};

	[Flags]
	public enum ReaderTypes
	{
		None = 0,
		Admitto = 1,
		AxessTMC13 = 2,
		Deister = 3,
		Gunnebo = 4,
		IdOnDemand = 5,
		PCSC = 6,
		Promag = 7,
		RFIDeas = 8,
		Rpleth = 9,
		SCIEL = 10,
		SmartID = 11,
		STidPRG = 12
	};

	public enum KeyType_MifareDesFireKeyType
	{
		DefaultDesfireCardCardMasterKey,
		DefaultDesfireCardApplicationMasterKey,
		DefaultDesfireCardReadKey,
		DefaultDesfireCardWriteKey
	}

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