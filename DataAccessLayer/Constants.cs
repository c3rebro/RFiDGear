/*
 * Created by SharpDevelop.
 * Date: 12.10.2017
 * Time: 11:21
 * 
 */
using System;
using System.Collections.ObjectModel;

namespace RFiDGear.DataAccessLayer
{
	/// <summary>
	/// 
	/// </summary>
	public enum Task_Type
	{
		None,
		Authenticate,
		Add,
		Edit,
		ChangeDefault
	}
	
	/// <summary>
	/// 
	/// </summary>
	public enum Data_Block
	{
		Block0 = 0,
		Block1 = 1,
		Block2 = 2,
		BlockAll = 3
	}
	
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
	public enum Access_Condition
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
			cardType = _type;
			uid = _uid;
		}
		
		public string uid;
		public CARD_TYPE cardType;
	}
	
	/// <summary>
	/// Description of Constants.
	/// </summary>
	public enum CARD_TYPE
	{
		Mifare1K,
		Mifare2K,
		Mifare4K,
		DESFire,
		DESFireEV1,
		DESFireEV2,
		CT_PLUS_1K,
		CT_PLUS_2K,
		CT_PLUS_4K
	};
	
	/// <summary>
	/// 
	/// </summary>
	public enum ERROR
	{
		NoError,
		AuthenticationError
	}
	
	public enum KEY_ERROR
	{
		KEY_IS_EMPTY,
		KEY_HAS_WRONG_LENGTH,
		KEY_HAS_WRONG_FORMAT,
		NO_ERROR
	};
	
	public enum AUTH_ERROR
	{
		DESFIRE_WRONG_CARD_MASTER_KEY,
		DESFIRE_WRONG_APPLICATION_MASTER_KEY,
		DESFIRE_WRONG_READ_KEY,
		DESFIRE_WRONG_WRITE_KEY
	};
	
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
	
	public enum KeyType_EncryptionType
	{
		DES,
		TrippleDES,
		AES
	}
	
	public enum KeyType_MifareDesFireKeyType
	{
		DefaultDesfireCardCardMasterKey,
		DefaultDesfireCardApplicationMasterKey,
		DefaultDesfireCardReadKey,
		DefaultDesfireCardWriteKey
	}
	
	public struct MifareDesfireDefaultKeys
	{
		public MifareDesfireDefaultKeys(KeyType_MifareDesFireKeyType _keyType, KeyType_EncryptionType _encryptionType, string _key)
		{
			KeyType = _keyType;
			EncryptionType = _encryptionType;
			Key = _key;
		}
		
		public KeyType_MifareDesFireKeyType KeyType;
		public KeyType_EncryptionType EncryptionType;
		public string Key;
	}
	
	public struct MifareClassicDefaultKeys
	{
		public MifareClassicDefaultKeys(KeyType_MifareClassicKeyType _keyType, string _accessBits)
		{
			KeyType = _keyType;
			accessBits = _accessBits;
		}
		
		private string accessBits;
		
		public KeyType_MifareClassicKeyType KeyType;
		public string AccessBits { get { return accessBits; } set { accessBits = value; }}
	}
	
	public enum KeyType_MifareClassicKeyType
	{
		DefaultClassicCardAccessBits_Key00,
		DefaultClassicCardAccessBits_Key01,
		DefaultClassicCardAccessBits_Key02,
		DefaultClassicCardAccessBits_Key03,
		DefaultClassicCardAccessBits_Key04,
		DefaultClassicCardAccessBits_Key05,
		DefaultClassicCardAccessBits_Key06,
		DefaultClassicCardAccessBits_Key07,
		DefaultClassicCardAccessBits_Key08,
		DefaultClassicCardAccessBits_Key09,
		DefaultClassicCardAccessBits_Key10,
		DefaultClassicCardAccessBits_Key11,
		DefaultClassicCardAccessBits_Key12,
		DefaultClassicCardAccessBits_Key13,
		DefaultClassicCardAccessBits_Key14,
		DefaultClassicCardAccessBits_Key15
	}
	
}
