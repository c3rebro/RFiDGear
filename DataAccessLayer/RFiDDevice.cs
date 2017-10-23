using RFiDGear.DataAccessLayer;
using RFiDGear.Model;

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
	

	
	public class RFiDDevice : IDisposable
	{
		// global (cross-class) Instances go here ->
		private IReaderProvider readerProvider;
		private IReaderUnit readerUnit;
		private chip card;
		private SettingsReaderWriter defaultSettings;
		
		private string readerProviderName;

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

		#region properties
		public bool IsChipPresent {
			get { return ReadChipPublic(); }
		}
		
		public ReaderTypes ReaderProvider { get; set; }
		
		public string CurrentReaderUnitName {
			get { return readerUnitName; }
		} private string readerUnitName;
		
		public CARD_INFO CardInfo {
			get; private set;
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
		
		public byte[] GetDESFireFileData {
			get { return desFireFileData; }
		}
		
		public bool EraseDesfireCard {
			get { return FormatDesFireCard(null, DESFireKeyType.DF_KEY_AES); }
		}
		
		public UInt32[] GetAppIDList {
			get{ return !GetMiFareDESFireChipAppIDs() ? appIDs : null; }
		}
		#endregion
		
		public RFiDDevice()
		{
		}
		
		public RFiDDevice(ReaderTypes _readerType = ReaderTypes.None)
		{
			try{
				defaultSettings = new SettingsReaderWriter();
				
				if(_readerType != ReaderTypes.None)
					ReaderProvider = _readerType;
				else
					ReaderProvider = defaultSettings.DefaultSpecification.DefaultReaderProvider;
				
				
				readerProvider = new LibraryManagerClass().GetReaderProvider(Enum.GetName(typeof(ReaderTypes), ReaderProvider));
				readerUnit = readerProvider.CreateReaderUnit();
			}

			catch(Exception e)
			{
				LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}",DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""));
			}

		}
		
		/// <summary>
		/// Test reader connection. A chip with matching technology  (e.g. mifare card - omnikey reader) must be present on the reader
		/// </summary>
		/// <returns></returns>
		public bool ReadChipPublic()
		{
			try {

				if (readerUnit.ConnectToReader()) {
					if (readerUnit.WaitInsertion(2000)) {
						if (readerUnit.Connect()) {
							
							readerUnitName = readerUnit.ConnectedName;
							//string readerSerialNumber = readerUnit.GetReaderSerialNumber(); //-> ERROR with OmniKey (and some others?) Reader when card isnt removed before recalling!
							
							card = readerUnit.GetSingleChip();
							
							if (card.ChipIdentifier != chipUID && card.ChipIdentifier.Length != 0) {
								
								CARD_TYPE type;
								
								Enum.TryParse(card.Type, out type);
								
								CardInfo = new CARD_INFO(type, card.ChipIdentifier);
							}
							return false;
						}
					}
				}
			} catch (Exception e) {
				if(readerProvider != null)
					readerProvider.ReleaseInstance();
				
				LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}",DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""));
			}
			return false;
		}

		protected bool ReadMiFareClassicSingleSector(int sectorNumber, int keyNumber)
		{
			SettingsReaderWriter settings = new SettingsReaderWriter();
			
			settings.ReadSettings();
			
			//MifareKey keyA = new MifareKey() { Value = CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(settings.DefaultSpecification.SecurityInfo.Where(x => x.Key == DefaultClassicCardKeysAKeys[keyNumber]) };
			//MifareKey keyB = new MifareKey() { Value = CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(settings.DefaultClassicCardKeysBKeys[keyNumber]) };
			
			int blockCount = 0;
			int dataBlockNumber = 0;
			sectorIsKeyAAuthSuccessful = true;
			sectorCanRead = true;
			
			try {

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
								//cmd.LoadKeyNo((byte)0, keyA, MifareKeyType.KT_KEY_A); // FIXME changed "keyNumber" to 0: for whatever reason some readers can contain more keys than others
								
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
					}
				}
			} catch(Exception e) {
				LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}",DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""));
			}
			return true;
		}

		public ERROR ReadMiFareClassicSingleSector(int sectorNumber, string aKey, string bKey)
		{
			SettingsReaderWriter settings = new SettingsReaderWriter();
			
			settings.ReadSettings();
			
			MifareKey keyA = new MifareKey() { Value = CustomConverter.KeyFormatQuickCheck(aKey) ? aKey : CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(aKey) };
			MifareKey keyB = new MifareKey() { Value = CustomConverter.KeyFormatQuickCheck(bKey) ? bKey : CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(bKey) };
			
			int blockCount = 0;
			int dataBlockNumber = 0;
			sectorIsKeyAAuthSuccessful = true;
			sectorIsKeyBAuthSuccessful = false;
			sectorCanRead = true;
			
			try {
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
								return ERROR.NoError;
								
							}
							return ERROR.NoError;
						}
					}
				}
			} catch(Exception e) {
				LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}",DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""));
				return ERROR.AuthenticationError;
			}
			return ERROR.NoError;
		}
		
		public bool WriteMiFareClassicSingleSector(int sectorNumber, string sectorTrailer, byte[] buffer)
		{
			SettingsReaderWriter settings = new SettingsReaderWriter();
			
			settings.ReadSettings();
			
			string[] keys = sectorTrailer.Split(',');
			
			string accessBits = keys[1];
			
			MifareKey keyA = new MifareKey() { Value = CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(keys[0]) };
			MifareKey keyB = new MifareKey() { Value = CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(keys[2]) };
			
			int blockCount = 0;
			int dataBlockNumber = 0;
			sectorIsKeyAAuthSuccessful = true;
			sectorIsKeyBAuthSuccessful = false;
			sectorCanRead = true;
			
			
			byte[] test;
			
			try {
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
											if(k <= 3)
												cmd.WriteBinary((byte)(dataBlockNumber + k), buffer);
											else
											{
												test = new ByteConverter().ConvertFrom(keys) as byte[];
												cmd.WriteBinary((byte)(dataBlockNumber + k), new ByteConverter().ConvertFrom(keys) as byte[]);
											}

											
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
					}
				}
			} catch(Exception e) {
				LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}",DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""));
			}
			return true;
		}
		
		private bool GetMiFareDESFireChipAppIDs()
		{
			try {
				// The excepted memory tree
				IDESFireLocation location = new DESFireLocation();
				// File communication requires encryption
				location.SecurityLevel = EncryptionMode.CM_ENCRYPT;
				
				IDESFireEV1Commands cmd;
				// Keys to use for authentication
				IDESFireAccessInfo aiToUse = new DESFireAccessInfo();
				aiToUse.MasterCardKey.Value = "00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00";
				
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
				}
				return true;
			} catch (Exception e) {
				LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}",DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""));
				return true;
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
		
		public bool AuthToMifareDesfireMasterApplication(string cardMasterKey, DESFireKeyType keyType)
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
		
		private bool FormatDesFireCard(string cardMasterKey, DESFireKeyType keyType)
		{
			try {
				// The excepted memory tree
				IDESFireLocation location = new DESFireLocation();
				// File communication requires encryption
				location.SecurityLevel = EncryptionMode.CM_ENCRYPT;
				
				
				IDESFireEV1Commands cmd;
				// Keys to use for authentication
				IDESFireAccessInfo aiToUse = new DESFireAccessInfo();
				aiToUse.MasterCardKey.Value = "00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00";
				//aiToUse.MasterCardKey.Value = "ff ff ff ff ff ff ff ff ff ff ff ff ff ff ff ff";
				aiToUse.MasterCardKey.KeyType = keyType;
				
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
								cmd.SelectApplication(0);

								cmd.Authenticate(0, aiToUse.MasterCardKey);
								cmd.DeleteApplication(1);
								cmd.Erase();
								
							}
							if (card.Type == "DESFireEV2") {
								
							}
							
							readerUnit.Disconnect();
							readerUnit.DisconnectFromReader();
							readerProvider.ReleaseInstance();
							return false;
						}
					}
				}
				return true;
			} catch (Exception e) {
				LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}",DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""));
			}
			return true;
		}
		
		
		private bool _disposed = false;
		
		protected virtual void Dispose(bool disposing)
		{
			if (!_disposed)
			{
				if (disposing)
				{
					// Dispose any managed objects
					// ...
				}

				readerUnit.Disconnect();
				readerUnit.DisconnectFromReader();
				readerProvider.ReleaseInstance();
				// Now disposed of any unmanaged objects
				// ...

				_disposed = true;
			}
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}
	}
}


