using System;
using LibLogicalAccess;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace RFiDGear
{
	/// <summary>
	/// Description of RFiDAccess.
	/// 
	/// Initialize Reader
	/// </summary>
	/// 
	
	public enum CARD_TYPE
	{
		CT_CLASSIC_1K,
		CT_CLASSIC_2K,
		CT_CLASSIC_4K,
		CT_DESFIRE_EV1,
		CT_DESFIRE_EV2}

	;
	
	public enum KEY_ERROR
	{
		KEY_IS_EMPTY,
		KEY_HAS_WRONG_LENGTH,
		KEY_HAS_WRONG_FORMAT,
		NO_ERROR}

	;
	
	public enum AUTH_ERROR
	{
		DESFIRE_WRONG_CARD_MASTER_KEY,
		DESFIRE_WRONG_APPLICATION_MASTER_KEY,
		DESFIRE_WRONG_READ_KEY,
		DESFIRE_WRONG_WRITE_KEY}

	;
	
	public class RFiDDevice
	{
		// global (cross-class) Instances go here ->
		private IReaderProvider readerProvider;
		private IReaderUnit readerUnit;
		private chip card;
		
		private string readerProviderName;
		private string readerUnitName;
		//private string readerSerialNumber;
		private string chipType;
		private string chipUID;
		
		private string classicCardKeyA;
		private string classicCardKeyB;
		
		private byte[] cardDataBlock;
		private byte[][] cardDataSector;
		private bool[] blockAuthSuccessful;
		private bool[] blockReadSuccessful;
		private bool sectorIsKeyAAuthSuccessful;
		private bool sectorIsKeyBAuthSuccessful;
		private bool sectorCanRead;
		
		private byte[] desFireFileData;

		private UInt32[] appIDs;
		
		public RFiDDevice(string _readerProviderName)
		{
			readerProviderName = _readerProviderName;
		}
		
		private void SetCurrentReaderProvider(IReaderProvider _readerProvider)
		{
			ReadChipPublic();
		}
		
		private bool ReadChipPublic()
		{
			try {
				readerProvider = new LibraryManagerClass().GetReaderProvider(readerProviderName);
				readerUnit = readerProvider.CreateReaderUnit();

				if (readerUnit.ConnectToReader()) {
					if (readerUnit.WaitInsertion(200)) {
						if (readerUnit.Connect()) {
							
							readerUnitName = readerUnit.ConnectedName;
							//readerSerialNumber = readerUnit.GetReaderSerialNumber(); -> ERROR with Reader!
							
							card = readerUnit.GetSingleChip();
							
							if (card.ChipIdentifier != chipUID && card.ChipIdentifier.Length != 0) {

								chipUID = card.ChipIdentifier;
								chipType = card.Type;
							}
							
							readerUnit.Disconnect();
							readerUnit.DisconnectFromReader();
							
							readerProvider.ReleaseInstance();
							
							return false;
						}

					} else {
						readerUnit.DisconnectFromReader();
					}
				}
				readerProvider.ReleaseInstance();
			} catch (Exception e) {
				throw new Exception("Uuups");
			}
			return false;
		}

		public bool ReadMiFareClassicSingleSector(int sectorNumber, int keyNumber)
		{
			SettingsReaderWriter settings = new SettingsReaderWriter();
			
			settings.readSettings();
			
			MifareKey keyA = new MifareKey() { Value = new CustomConverter().FormatMifareClassicKeyWithSpacesEachByte(settings.DefaultClassicCardKeysAKeys[keyNumber]) };
			MifareKey keyB = new MifareKey() { Value = new CustomConverter().FormatMifareClassicKeyWithSpacesEachByte(settings.DefaultClassicCardKeysBKeys[keyNumber]) };
			
			int blockCount = 0;
			int dataBlockNumber = 0;
			sectorIsKeyAAuthSuccessful = true;
			sectorCanRead = true;
			
			try {
				readerProvider = new LibraryManagerClass().GetReaderProvider(new SettingsReaderWriter().DefaultReaderProvider);
				readerUnit = readerProvider.CreateReaderUnit();
				
				if (readerUnit.ConnectToReader()) {
					if (readerUnit.WaitInsertion(200)) {
						if (readerUnit.Connect()) {
							
							readerUnitName = readerUnit.ConnectedName;
							//readerSerialNumber = readerUnit.GetReaderSerialNumber();
							
							card = readerUnit.GetSingleChip();
							
							if (card.ChipIdentifier != chipUID && card.ChipIdentifier.Length != 0) {

								chipUID = card.ChipIdentifier;
								chipType = card.Type;
							}
							
							
							if (card.Type == "Mifare1K") {
								
								blockAuthSuccessful = new bool[64];
								blockReadSuccessful = new bool[64];
								
								blockCount = 4;
								
								cardDataBlock = new byte[16];
								cardDataSector = new byte[4][];
								
							}
							if (card.Type == "Mifare4K") {
								
								blockAuthSuccessful = new bool[256];
								blockReadSuccessful = new bool[256];
								
								if(sectorNumber <= 31)
									blockCount = 4;
								else
									blockCount = 16;
								
								cardDataBlock = new byte[16];
								cardDataSector = new byte[16][];
								
							}
							

							IMifareCommands cmd = card.Commands as IMifareCommands;

							if(sectorNumber <= 31)
								dataBlockNumber = (((sectorNumber + 1) * blockCount) - (blockCount - dataBlockNumber));
							else
								dataBlockNumber = ((128 + (sectorNumber - 31) * blockCount) - (blockCount - dataBlockNumber));
							
							try {
								cmd.LoadKeyNo((byte)0, keyA, MifareKeyType.KT_KEY_A); // FIXME changed "keyNumber" to 0: for whatever reason some readers can contain more keys than others
								
								for (int k = 0; k < blockCount; k++) {

									try {
										
										cmd.AuthenticateKeyNo((byte)(dataBlockNumber + k), (byte)0, MifareKeyType.KT_KEY_A); // change "keyNumber" to 0 // FIXME same as '190
										blockAuthSuccessful[dataBlockNumber + k] = true;
										
										try {
											object data = cmd.ReadBinary((byte)(dataBlockNumber + k), 48);
											
											cardDataBlock = (byte[])data;
											cardDataSector[k] = cardDataBlock;
											
											blockReadSuccessful[dataBlockNumber + k] = true;
										} catch {
											blockReadSuccessful[dataBlockNumber + k] = false;
											sectorCanRead = false;
										}
									} catch {
										blockAuthSuccessful[dataBlockNumber + k] = false;
										sectorIsKeyAAuthSuccessful = false;
									}
								}
							} catch{
								return true;
								
							}
							return false;
						}

					} else {
						readerUnit.DisconnectFromReader();
					}
				}
				readerProvider.ReleaseInstance();
			} catch {
				throw new Exception("Uuups");
			}
			return true;
		}

		public bool ReadMiFareClassicSingleSector(int sectorNumber, string aKey, string bKey)
		{
			SettingsReaderWriter settings = new SettingsReaderWriter();
			
			settings.readSettings();
			
			MifareKey keyA = new MifareKey() { Value = new CustomConverter().KeyFormatQuickCheck(aKey) ? aKey : new CustomConverter().FormatMifareClassicKeyWithSpacesEachByte(aKey) };
			MifareKey keyB = new MifareKey() { Value = new CustomConverter().KeyFormatQuickCheck(bKey) ? bKey : new CustomConverter().FormatMifareClassicKeyWithSpacesEachByte(bKey) };
			
			int blockCount = 0;
			int dataBlockNumber = 0;
			sectorIsKeyAAuthSuccessful = true;
			sectorIsKeyBAuthSuccessful = false;
			sectorCanRead = true;
			
			try {
				readerProvider = new LibraryManagerClass().GetReaderProvider(new SettingsReaderWriter().DefaultReaderProvider);
				readerUnit = readerProvider.CreateReaderUnit();
				
				if (readerUnit.ConnectToReader()) {
					if (readerUnit.WaitInsertion(200)) {
						if (readerUnit.Connect()) {
							
							readerUnitName = readerUnit.ConnectedName;
							//readerSerialNumber = readerUnit.GetReaderSerialNumber();
							
							card = readerUnit.GetSingleChip();
							
							if (card.ChipIdentifier != chipUID && card.ChipIdentifier.Length != 0) {

								chipUID = card.ChipIdentifier;
								chipType = card.Type;
							}
							
							
							if (card.Type == "Mifare1K") {
								
								blockAuthSuccessful = new bool[64];
								blockReadSuccessful = new bool[64];
								
								blockCount = 4;
								
								cardDataBlock = new byte[16];
								cardDataSector = new byte[4][];
								
							}
							if (card.Type == "Mifare4K") {
								
								blockAuthSuccessful = new bool[256];
								blockReadSuccessful = new bool[256];
								
								if(sectorNumber <= 31)
									blockCount = 4;
								else
									blockCount = 16;
								
								cardDataBlock = new byte[16];
								cardDataSector = new byte[16][];
								
							}
							

							IMifareCommands cmd = card.Commands as IMifareCommands;

							if(sectorNumber <= 31)
								dataBlockNumber = (((sectorNumber + 1) * blockCount) - (blockCount - dataBlockNumber));
							else
								dataBlockNumber = ((128 + (sectorNumber - 31) * blockCount) - (blockCount - dataBlockNumber));
							
							try { //try to Auth with Keytype A
								
								cmd.LoadKeyNo((byte)0, keyA, MifareKeyType.KT_KEY_A); // FIXME "sectorNumber" to 0
								cmd.LoadKeyNo((byte)1, keyB, MifareKeyType.KT_KEY_B); // FIXME "sectorNumber" to 1
								
								for (int k = 0; k < blockCount; k++) {

									try {
										
										cmd.AuthenticateKeyNo((byte)(dataBlockNumber + k), (byte)0, MifareKeyType.KT_KEY_A); // FIXME same as '303
										blockAuthSuccessful[dataBlockNumber + k] = true;
										
										try {
											object data = cmd.ReadBinary((byte)(dataBlockNumber + k), 48);
											
											cardDataBlock = (byte[])data;
											cardDataSector[k] = cardDataBlock;
											
											blockReadSuccessful[dataBlockNumber + k] = true;
										} catch {
											
											blockReadSuccessful[dataBlockNumber + k] = false;
											sectorCanRead = false;
										}
										
									} catch { // Try Auth with keytype b
										
										sectorIsKeyAAuthSuccessful = false;
										
										try {

											cmd.AuthenticateKeyNo((byte)(dataBlockNumber + k), (byte)1, MifareKeyType.KT_KEY_B); // FIXME same as '303
											blockAuthSuccessful[dataBlockNumber + k] = true;
											sectorIsKeyBAuthSuccessful = true;
											
											try {
												object data = cmd.ReadBinary((byte)(dataBlockNumber + k), 48);
												
												cardDataBlock = (byte[])data;
												cardDataSector[k] = cardDataBlock;
												
												blockReadSuccessful[dataBlockNumber + k] = true;
											} catch {
												
												blockReadSuccessful[dataBlockNumber + k] = false;
												sectorCanRead = false;
											}
											
										} catch {
											
											blockAuthSuccessful[dataBlockNumber + k] = false;
											sectorIsKeyBAuthSuccessful = false;
										}
									}
								}
							} catch {
								return true;
								
							}
							return false;
						}

					} else {
						readerUnit.DisconnectFromReader();
					}
				}
				readerProvider.ReleaseInstance();
			} catch {
				throw new Exception("Uuups");
			}
			return true;
		}
		
		public bool WriteMiFareClassicSingleSector(int sectorNumber, string aKey, string bKey, byte[] buffer)
		{
			SettingsReaderWriter settings = new SettingsReaderWriter();
			
			settings.readSettings();
			
			MifareKey keyA = new MifareKey() { Value = new CustomConverter().KeyFormatQuickCheck(aKey) ? aKey : new CustomConverter().FormatMifareClassicKeyWithSpacesEachByte(aKey) };
			MifareKey keyB = new MifareKey() { Value = new CustomConverter().KeyFormatQuickCheck(bKey) ? bKey : new CustomConverter().FormatMifareClassicKeyWithSpacesEachByte(bKey) };
			
			int blockCount = 0;
			int dataBlockNumber = 0;
			sectorIsKeyAAuthSuccessful = true;
			sectorIsKeyBAuthSuccessful = false;
			sectorCanRead = true;
			
			try {
				readerProvider = new LibraryManagerClass().GetReaderProvider(new SettingsReaderWriter().DefaultReaderProvider);
				readerUnit = readerProvider.CreateReaderUnit();
				
				if (readerUnit.ConnectToReader()) {
					if (readerUnit.WaitInsertion(200)) {
						if (readerUnit.Connect()) {
							
							readerUnitName = readerUnit.ConnectedName;
							//readerSerialNumber = readerUnit.GetReaderSerialNumber();
							
							card = readerUnit.GetSingleChip();
							
							if (card.ChipIdentifier != chipUID && card.ChipIdentifier.Length != 0) {

								chipUID = card.ChipIdentifier;
								chipType = card.Type;
							}
							
							
							if (card.Type == "Mifare1K") {
								
								blockAuthSuccessful = new bool[64];
								blockReadSuccessful = new bool[64];
								
								blockCount = 3;
								
								cardDataBlock = new byte[16];
								cardDataSector = new byte[4][];
								
							}
							if (card.Type == "Mifare4K") {
								
								blockAuthSuccessful = new bool[256];
								blockReadSuccessful = new bool[256];
								
								if(sectorNumber <= 31)
									blockCount = 3;
								else
									blockCount = 16;
								
								cardDataBlock = new byte[16];
								cardDataSector = new byte[16][];
								
							}
							

							IMifareCommands cmd = card.Commands as IMifareCommands;

							if(sectorNumber <= 31)
								dataBlockNumber = (((sectorNumber + 1) * blockCount) - (blockCount - dataBlockNumber));
							else
								dataBlockNumber = ((128 + (sectorNumber - 31) * blockCount) - (blockCount - dataBlockNumber));
							
							try { //try to Auth with Keytype A
								
								cmd.LoadKeyNo((byte)0, keyA, MifareKeyType.KT_KEY_A); // FIXME "sectorNumber" to 0
								cmd.LoadKeyNo((byte)1, keyB, MifareKeyType.KT_KEY_B); // FIXME "sectorNumber" to 1
								
								for (int k = 0; k < blockCount; k++) {

									try {
										
										cmd.AuthenticateKeyNo((byte)(dataBlockNumber + k), (byte)0, MifareKeyType.KT_KEY_A); // FIXME same as '303
										blockAuthSuccessful[dataBlockNumber + k] = true;
										
										try {
											cmd.WriteBinary((byte)(dataBlockNumber + k), buffer);
											
											//cardDataBlock = (byte[])data;
											//cardDataSector[k] = cardDataBlock;
											
											blockReadSuccessful[dataBlockNumber + k] = true;
										} catch {
											
											blockReadSuccessful[dataBlockNumber + k] = false;
											sectorCanRead = false;
										}
										
									} catch { // Try Auth with keytype b
										
										sectorIsKeyAAuthSuccessful = false;
										
										try {

											cmd.AuthenticateKeyNo((byte)(dataBlockNumber + k), (byte)1, MifareKeyType.KT_KEY_B); // FIXME same as '303
											blockAuthSuccessful[dataBlockNumber + k] = true;
											sectorIsKeyBAuthSuccessful = true;
											
											try {
												object data = cmd.ReadBinary((byte)(dataBlockNumber + k), 48);
												
												cardDataBlock = (byte[])data;
												cardDataSector[k] = cardDataBlock;
												
												blockReadSuccessful[dataBlockNumber + k] = true;
											} catch {
												
												blockReadSuccessful[dataBlockNumber + k] = false;
												sectorCanRead = false;
											}
											
										} catch {
											
											blockAuthSuccessful[dataBlockNumber + k] = false;
											sectorIsKeyBAuthSuccessful = false;
										}
									}
								}
							} catch {
								return true;
								
							}
							return false;
						}

					} else {
						readerUnit.DisconnectFromReader();
					}
				}
				readerProvider.ReleaseInstance();
			} catch {
				throw new Exception("Uuups");
			}
			return true;
		}
		
		private bool GetMiFareDESFireChipAppIDs()
		{
			try {
				readerProvider = new LibraryManagerClass().GetReaderProvider(readerProviderName);
				readerUnit = readerProvider.CreateReaderUnit();
				
				// The excepted memory tree
				IDESFireLocation location = new DESFireLocation();
				// File communication requires encryption
				location.SecurityLevel = EncryptionMode.CM_ENCRYPT;
				
				
				IDESFireEV1Commands cmd;
				// Keys to use for authentication
				IDESFireAccessInfo aiToUse = new DESFireAccessInfo();
				aiToUse.MasterCardKey.Value = "00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00";

				CustomConverter converter = new CustomConverter();
				
				if (readerUnit.ConnectToReader()) {
					if (readerUnit.WaitInsertion(100)) {
						if (readerUnit.Connect()) {
							
							readerUnitName = readerUnit.ConnectedName;
							//readerSerialNumber = readerUnit.GetReaderSerialNumber();
							
							card = readerUnit.GetSingleChip();
							
							if (card.Type == "DESFireEV1") {
								cmd = card.Commands as IDESFireEV1Commands;
								
								object appIDsObject = cmd.GetApplicationIDs();
								appIDs = (appIDsObject as UInt32[]);
								
							}
							if (card.Type == "DESFireEV2") {
								
							}
							
							readerUnit.Disconnect();
							readerUnit.DisconnectFromReader();
							readerProvider.ReleaseInstance();
							return false;
						}
					}
					readerUnit.DisconnectFromReader();
				}
				readerProvider.ReleaseInstance();
				return true;
			} catch (Exception e) {
				throw new Exception(String.Format("Uuups: {0}", e));
				
			}
		}
		
		private bool ReadMiFareDESFireChipFile(int fileNo, int appid)
		{
			
			// The excepted memory tree
			IDESFireLocation location = new DESFireLocation();
			// The Application ID to use
			location.aid = appid;
			// File 0 into this application
			location.File = fileNo;
			// File communication requires encryption
			location.SecurityLevel = EncryptionMode.CM_ENCRYPT;
			
			IDESFireEV1Commands cmd;
			// Keys to use for authentication
			IDESFireAccessInfo aiToUse = new DESFireAccessInfo();
			aiToUse.MasterCardKey.Value = "11 22 33 44 55 66 77 88 99 00 11 22 33 44 55 66"; //"00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00";
			aiToUse.MasterCardKey.KeyType = DESFireKeyType.DF_KEY_AES;
			
			// Get the card storage service
			IStorageCardService storage = (IStorageCardService)card.GetService(CardServiceType.CST_STORAGE);
			
			// Change keys with the following ones
			IDESFireAccessInfo aiToWrite = new DESFireAccessInfo();

			
			aiToWrite.MasterCardKey.Value = "11 22 33 44 55 66 77 88 99 00 11 22 33 44 55 66";
			aiToWrite.MasterApplicationKey.Value = "c7 56 80 59 0f 31 2c 13 07 12 b6 df 8f a7 b1 dc";//"bd 9d 22 8c 06 72 14 a9 59 a3 28 91 fd bb 14 8c"; //"00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00";
			aiToWrite.MasterApplicationKey.KeyType = DESFireKeyType.DF_KEY_AES;

			aiToWrite.ReadKey.Value = "c7 56 80 59 0f 31 2c 13 07 12 b6 df 8f a7 b1 dc";//"bd 9d 22 8c 06 72 14 a9 59 a3 28 91 fd bb 14 8c";
			aiToWrite.ReadKey.KeyType = DESFireKeyType.DF_KEY_AES;
			aiToWrite.ReadKeyNo = 1;

			aiToWrite.WriteKey.Value = "c7 56 80 59 0f 31 2c 13 07 12 b6 df 8f a7 b1 dc";
			aiToWrite.WriteKey.KeyType = DESFireKeyType.DF_KEY_AES;
			aiToWrite.WriteKeyNo = 2;

			CustomConverter converter = new CustomConverter();
			
			DESFireKeySettings desFireKeySet;
			byte nBNmbr;
			
			if (readerUnit.ConnectToReader()) {
				if (readerUnit.WaitInsertion(100)) {
					if (readerUnit.Connect()) {
						
						if (card.Type == "DESFireEV1") {
							cmd = card.Commands as IDESFireEV1Commands;
							
							object appIDsObject = cmd.GetApplicationIDs();
							
							cmd.GetKeySettings(out desFireKeySet, out nBNmbr);
							cmd.SelectApplication(1);
							cmd.Authenticate(1, aiToWrite.WriteKey);
							FileSetting fSetting = cmd.GetFileSettings(0);

							//cmd.CreateApplication(3, DESFireKeySettings.KS_DEFAULT, 3);
							//cmd.DeleteApplication(4);
							object fileIDs = cmd.GetFileIDs();
							//appIDs = (appIDsObject as UInt32[]);
							
							desFireFileData = (byte[])storage.ReadData(location, aiToWrite, 48, CardBehavior.CB_DEFAULT);

							
						}
						if (card.Type == "DESFireEV2") {
							
						}
						return false;
					}
				}
			}
			return true;
		}
		
		private bool WriteMiFareDESFireChipFile(int fileNo, int appid)
		{
			
			// The excepted memory tree
			IDESFireLocation location = new DESFireLocation();
			// The Application ID to use
			location.aid = appid;
			// File 0 into this application
			location.File = fileNo;
			// File communication requires encryption
			location.SecurityLevel = EncryptionMode.CM_ENCRYPT;
			
			IDESFireEV1Commands cmd;
			// Keys to use for authentication
			IDESFireAccessInfo aiToUse = new DESFireAccessInfo();
			aiToUse.MasterCardKey.Value = "00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00"; //"11 22 33 44 55 66 77 88 99 00 11 22 33 44 55 66"
			aiToUse.MasterCardKey.KeyType = DESFireKeyType.DF_KEY_AES;
			
			// Get the card storage service
			IStorageCardService storage = (IStorageCardService)card.GetService(CardServiceType.CST_STORAGE);
			
			// Change keys with the following ones
			IDESFireAccessInfo aiToWrite = new DESFireAccessInfo();

			
			aiToWrite.MasterCardKey.Value = "00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00";
			aiToWrite.MasterCardKey.KeyType = DESFireKeyType.DF_KEY_AES;
			
			aiToWrite.MasterApplicationKey.Value = "c7 56 80 59 0f 31 2c 13 07 12 b6 df 8f a7 b1 dc";//"bd 9d 22 8c 06 72 14 a9 59 a3 28 91 fd bb 14 8c"; //"00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00";
			aiToWrite.MasterApplicationKey.KeyType = DESFireKeyType.DF_KEY_AES;

			aiToWrite.ReadKey.Value = "c7 56 80 59 0f 31 2c 13 07 12 b6 df 8f a7 b1 dc";//"bd 9d 22 8c 06 72 14 a9 59 a3 28 91 fd bb 14 8c";
			aiToWrite.ReadKey.KeyType = DESFireKeyType.DF_KEY_AES;
			aiToWrite.ReadKeyNo = 1;

			aiToWrite.WriteKey.Value = "c7 56 80 59 0f 31 2c 13 07 12 b6 df 8f a7 b1 dc";
			aiToWrite.WriteKey.KeyType = DESFireKeyType.DF_KEY_AES;
			aiToWrite.WriteKeyNo = 2;

			CustomConverter converter = new CustomConverter();
			
			DESFireKeySettings desFireKeySet;
			byte nBNmbr;
			
			if (readerUnit.ConnectToReader()) {
				if (readerUnit.WaitInsertion(100)) {
					if (readerUnit.Connect()) {
						
						if (card.Type == "DESFireEV1") {
							cmd = card.Commands as IDESFireEV1Commands;
							
							object appIDsObject = cmd.GetApplicationIDs();
							
							cmd.GetKeySettings(out desFireKeySet, out nBNmbr);
							cmd.Authenticate(0, aiToUse.MasterCardKey);
							cmd.DeleteApplication(1);
							
							//cmd.SelectApplication(0);
							
							FileSetting fSetting = cmd.GetFileSettings(0);

							//cmd.CreateApplication(3, DESFireKeySettings.KS_DEFAULT, 3);
							//cmd.DeleteApplication(4);
							//object fileIDs = cmd.GetFileIDs();
							//appIDs = (appIDsObject as UInt32[]);
							cmd.ChangeKey(0, aiToWrite.MasterCardKey);
							//desFireFileData = (byte[])storage.ReadData(location, aiToWrite, 48, CardBehavior.CB_DEFAULT);

							
						}
						if (card.Type == "DESFireEV2") {
							
						}
						return false;
					}
				}
			}
			return true;
		}
		
		private bool AuthToMifareDesfireMasterApplication(string cardMasterKey, DESFireKeyType keyType)
		{
			
			// The excepted memory tree
			IDESFireLocation location = new DESFireLocation();
			// The Application ID to use
			location.aid = 0;
			// File communication requires encryption
			location.SecurityLevel = EncryptionMode.CM_ENCRYPT;
			
			IDESFireEV1Commands cmd;
			// Keys to use for authentication
			IDESFireAccessInfo aiToUse = new DESFireAccessInfo();
			aiToUse.MasterCardKey.Value = cardMasterKey;
			aiToUse.MasterCardKey.KeyType = keyType;
			
			CustomConverter converter = new CustomConverter();
			
			
			if (readerUnit.ConnectToReader()) {
				if (readerUnit.WaitInsertion(100)) {
					if (readerUnit.Connect()) {
						
						if (card.Type == "DESFireEV1") {
							cmd = card.Commands as IDESFireEV1Commands;
							try {
								cmd.Authenticate(0, aiToUse.MasterCardKey);
								return false;
							} catch {
								return true;
							}

						}
						if (card.Type == "DESFireEV2") {
							
						}
						return false;
					}
				}
			}
			return true;
		}
		
		#region properties
		public bool IsChipPresent {
			get { return ReadChipPublic(); }
		}
		
		public string CurrentReaderUnitName {
			get { return readerUnitName; }
		}
		
		public string currentChipType {
			get { return chipType; }
		}
		
		public string currentChipUID {
			get { return chipUID; }
		}
		
		public byte[][] currentSector {
			get { return cardDataSector; }
		}
		
		public byte[] currentDataBlock {
			get { return cardDataBlock; }
		}
		
		public string usedClassicCardKeyA {
			get { return classicCardKeyA; }
			set { classicCardKeyA = value; }
		}
		
		public string usedClassicCardKeyB {
			get { return classicCardKeyB; }
			set { classicCardKeyB = value; }
		}
		
		public bool[] DataBlockSuccessfullyRead {
			get { return blockReadSuccessful; }
		}
		
		public bool[] DataBlockSuccesfullyAuth {
			get{ return blockAuthSuccessful; }
		}
		
		public bool SectorSuccessfullyRead {
			get { return sectorCanRead; }
		}
		
		public bool SectorSuccesfullyAuth {
			get{ return sectorIsKeyAAuthSuccessful; }
		}
		
		public UInt32[] GetAppIDList {
			get{ return !GetMiFareDESFireChipAppIDs() ? appIDs : null; }
		}
		
		public byte[] GetDESFireFileData {
			get { return desFireFileData; }
		}
		#endregion
	}
}


