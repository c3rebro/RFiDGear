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

		
		

		#region properties
		public MifareClassicSectorModel Sector
		{
			get { return sectorModel; }
		} private MifareClassicSectorModel sectorModel;
		
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
		
		public uint[] GetAppIDList {
			get{ return !GetMiFareDESFireChipAppIDs() ? appIDs : null; }
		} private uint[] appIDs;
		
		public uint FreeMemory {
			get { return freeMemory; }
		} private uint freeMemory;
		#endregion
		
		public static RFiDDevice Instance
		{
			get {
				if(instance == null)
				{
					instance = new RFiDDevice();
					return instance;
				}
				else
					return null;
				
			}
		} private static RFiDDevice instance;
		
		public RFiDDevice() : this(ReaderTypes.None)
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
					if (readerUnit.WaitInsertion(100)) {
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

		public ERROR ReadMifareUltralight()
		{
			try {
				
				if (readerUnit.ConnectToReader()) {
					if (readerUnit.WaitInsertion(100)) {
						if (readerUnit.Connect()) {
							
							readerUnitName = readerUnit.ConnectedName;
							//readerSerialNumber = readerUnit.GetReaderSerialNumber();
							
							card = readerUnit.GetSingleChip();
							
							if (card.Type == "MifareUltralight") {
								var cmd = card.Commands as MifareUltralightCCommands;// IMifareUltralightCommands;
								
								object appIDsObject = cmd.ReadPages(0,3);
								//object res = cmd.ReadPage(4);
								
								//appIDs = (appIDsObject as UInt32[]);
								
							}
							
							readerUnit.Disconnect();
							readerUnit.DisconnectFromReader();
							readerProvider.ReleaseInstance();
							return ERROR.NoError;
						}
					}
				}
				return ERROR.NoError;
			} catch (Exception e) {
				LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}",DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""));
				return ERROR.IOError;
			}
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
			sectorModel = new MifareClassicSectorModel();
			
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
								
								//cmd.LoadKeyNo((byte)0, keyA, MifareKeyType.KT_KEY_A); // FIXME "sectorNumber" to 0
								//cmd.LoadKeyNo((byte)1, keyB, MifareKeyType.KT_KEY_B); // FIXME "sectorNumber" to 1
								
								for (int k = 0; k < blockCount; k++) {

									cmd.LoadKeyNo((byte)0, keyA, MifareKeyType.KT_KEY_A); // FIXME "sectorNumber" to 0
									
									MifareClassicDataBlockModel dataBlock = new MifareClassicDataBlockModel();
									dataBlock.BlockNumber = dataBlockNumber + k;
									
									try {

										cmd.AuthenticateKeyNo((byte)(dataBlockNumber + k), (byte)0, MifareKeyType.KT_KEY_A); // FIXME same as '303
										blockAuthSuccessful[dataBlockNumber + k] = true;
										
										sectorModel.IsAuthenticated = true;
										
										try {
											object data = cmd.ReadBinary((byte)(dataBlockNumber + k), 48);
											
											cardDataBlock = (byte[])data;
											cardDataSector[k] = cardDataBlock;
											
											blockReadSuccessful[dataBlockNumber + k] = true;
											
											dataBlock.IsAuthenticated = true;
											dataBlock.Data = (byte[])data;
											
											sectorModel.DataBlock.Add(dataBlock);
											
										} catch {
											
											dataBlock.IsAuthenticated = false;
											sectorModel.DataBlock.Add(dataBlock);
											
											blockReadSuccessful[dataBlockNumber + k] = false;
											sectorCanRead = false;
										}
										
									} catch { // Try Auth with keytype b
										
										sectorIsKeyAAuthSuccessful = false;
										
										try {

											cmd.LoadKeyNo((byte)0, keyB, MifareKeyType.KT_KEY_B);
											
											cmd.AuthenticateKeyNo((byte)(dataBlockNumber + k), (byte)0, MifareKeyType.KT_KEY_B); // FIXME same as '303
											blockAuthSuccessful[dataBlockNumber + k] = true;
											sectorIsKeyBAuthSuccessful = true;
											
											sectorModel.IsAuthenticated = true;
											
											
											
											
											try {
												object data = cmd.ReadBinary((byte)(dataBlockNumber + k), 48);
												
												cardDataBlock = (byte[])data;
												cardDataSector[k] = cardDataBlock;
												
												blockReadSuccessful[dataBlockNumber + k] = true;
												dataBlock.IsAuthenticated = true;
												dataBlock.Data = (byte[])data;
												
												sectorModel.DataBlock.Add(dataBlock);
												
											} catch {
												
												dataBlock.IsAuthenticated = false;
												
												sectorModel.DataBlock.Add(dataBlock);
												
												blockReadSuccessful[dataBlockNumber + k] = false;
												sectorCanRead = false;
											}
											
										} catch {
											
											sectorModel.IsAuthenticated = false;
											dataBlock.IsAuthenticated = false;
											
											sectorModel.DataBlock.Add(dataBlock);
											
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
								freeMemory = cmd.GetFreeMemory();
								DESFireKeySettings s;
								DESFireKeyType k;
								byte b;
								
								cmd.GetKeySettingsEV1(out s,out b,out k);
								
							}
							if (card.Type == "DESFireEV2") {
								
							}
							
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
			
			aiToWrite.MasterApplicationKey.Value = "00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00";//"bd 9d 22 8c 06 72 14 a9 59 a3 28 91 fd bb 14 8c"; //"c7 56 80 59 0f 31 2c 13 07 12 b6 df 8f a7 b1 dc";
			aiToWrite.MasterApplicationKey.KeyType = DESFireKeyType.DF_KEY_AES;

			aiToWrite.ReadKey.Value = "00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00";//"bd 9d 22 8c 06 72 14 a9 59 a3 28 91 fd bb 14 8c";
			aiToWrite.ReadKey.KeyType = DESFireKeyType.DF_KEY_AES;
			aiToWrite.ReadKeyNo = 1;

			aiToWrite.WriteKey.Value = "00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00";
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
							
							//FileSetting fSetting = cmd.GetFileSettings(0);

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
		
		public ERROR AuthToMifareDesfireApplication(string _applicationMasterKey, DESFireKeyType _keyType, MifareDesfireKeyNumber _keyNumber, int _appID = 0)
		{
			try
			{
				// The excepted memory tree
				IDESFireLocation location = new DESFireLocation();
				// The Application ID to use
				location.aid = _appID;
				// File communication requires encryption
				location.SecurityLevel = EncryptionMode.CM_ENCRYPT;
				
				IDESFireEV1Commands cmd;
				// Keys to use for authentication
				IDESFireAccessInfo aiToUse = new DESFireAccessInfo();
				CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_applicationMasterKey);
				aiToUse.MasterCardKey.Value = CustomConverter.desFireKeyToEdit;
				aiToUse.MasterCardKey.KeyType = _keyType;
				
				
				if (readerUnit.ConnectToReader()) {
					if (readerUnit.WaitInsertion(2000)) {
						if (readerUnit.Connect()) {
							
							readerUnitName = readerUnit.ConnectedName;
							//readerSerialNumber = readerUnit.GetReaderSerialNumber();
							
							card = readerUnit.GetSingleChip();
							
							if (card.Type == "DESFireEV1") {
								cmd = card.Commands as IDESFireEV1Commands;
								try {
									cmd.SelectApplication((uint)_appID);
									cmd.Authenticate((byte)_keyNumber, aiToUse.MasterCardKey);
									return ERROR.NoError;
								} catch {
									return ERROR.AuthenticationError;
								}

							}
							if (card.Type == "DESFireEV2") {
								
							}
							return ERROR.AuthenticationError;
						}
					}
				}
				return ERROR.IOError;
			}
			
			catch
			{
				return ERROR.AuthenticationError;
			}
			
		}
		
		public ERROR CreateMifareDesfireApplication(string _piccMasterKey, DESFireKeySettings _keySettingsTarget, DESFireKeyType _keyTypePiccMasterKey, DESFireKeyType _keyTypeTargetApplication, int _maxNbKeys, int _appID)
		{
			try
			{
				// The excepted memory tree
				IDESFireLocation location = new DESFireLocation();
				// The Application ID to use
				location.aid = _appID;
				
				// File communication requires encryption
				location.SecurityLevel = EncryptionMode.CM_ENCRYPT;
				
				IDESFireEV1Commands cmd;
				// Keys to use for authentication
				IDESFireAccessInfo aiToUse = new DESFireAccessInfo();
				CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_piccMasterKey);
				aiToUse.MasterCardKey.Value = CustomConverter.desFireKeyToEdit;
				aiToUse.MasterCardKey.KeyType = _keyTypePiccMasterKey;
				
				
				if (readerUnit.ConnectToReader()) {
					if (readerUnit.WaitInsertion(2000)) {
						if (readerUnit.Connect()) {
							
							readerUnitName = readerUnit.ConnectedName;
							//readerSerialNumber = readerUnit.GetReaderSerialNumber();
							
							card = readerUnit.GetSingleChip();
							
							if (card.Type == "DESFireEV1") {
								cmd = card.Commands as IDESFireEV1Commands;
								try {
									cmd.SelectApplication(0);
									cmd.Authenticate(0, aiToUse.MasterCardKey);
									cmd.CreateApplicationEV1((uint)_appID,_keySettingsTarget,(byte)_maxNbKeys,false,_keyTypeTargetApplication,0,0);
									//cmd.CreateApplication((uint)_appID,_keySettings,(byte)_maxNbKeys);
									//cmd.SelectApplication((uint)_appID);
									//cmd.AuthenticateKeyNo(0);
									//cmd.ChangeKey(1,aiToUse.MasterCardKey);
									
									return ERROR.NoError;
								} catch (Exception e) {
									return ERROR.AuthenticationError;
								}

							}
							if (card.Type == "DESFireEV2") {
								
							}
							return ERROR.AuthenticationError;
						}
					}
				}
				return ERROR.IOError;
			}
			
			catch
			{
				return ERROR.AuthenticationError;
			}
			
		}
		
		public ERROR ChangeMifareDesfireApplicationKey(string _applicationMasterKeyCurrent, int _keyNumberCurrent, DESFireKeyType _keyTypeCurrent, string _applicationMasterKeyTarget, DESFireKeyType _keyTypeTarget, int _appIDCurrent = 0, int _appIDTarget = 0)
		{
			try
			{
				// The excepted memory tree
				IDESFireLocation location = new DESFireLocation();
				// The Application ID to use
				location.aid = _appIDCurrent;
				// File communication requires encryption
				location.SecurityLevel = EncryptionMode.CM_ENCRYPT;
				
				IDESFireEV1Commands cmd;
				// Keys to use for authentication
				IDESFireAccessInfo aiToUse = new DESFireAccessInfo();
				if(_appIDCurrent > 0)
				{
					CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_applicationMasterKeyCurrent);
					aiToUse.MasterApplicationKey.Value = CustomConverter.desFireKeyToEdit;
					aiToUse.MasterApplicationKey.KeyType = _keyTypeCurrent;
				}
				else
				{
					CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_applicationMasterKeyCurrent);
					aiToUse.MasterCardKey.Value = CustomConverter.desFireKeyToEdit;
					aiToUse.MasterCardKey.KeyType = _keyTypeCurrent;
				}

				
				DESFireKey applicationMasterKeyTarget = new DESFireKeyClass();
				applicationMasterKeyTarget.KeyType = _keyTypeTarget;
				CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_applicationMasterKeyTarget);
				applicationMasterKeyTarget.Value = CustomConverter.desFireKeyToEdit;
				
				if (readerUnit.ConnectToReader()) {
					if (readerUnit.WaitInsertion(2000)) {
						if (readerUnit.Connect()) {
							
							readerUnitName = readerUnit.ConnectedName;
							//readerSerialNumber = readerUnit.GetReaderSerialNumber();
							
							card = readerUnit.GetSingleChip();
							
							if (card.Type == "DESFireEV1") {
								cmd = card.Commands as IDESFireEV1Commands;
								try {
									cmd.SelectApplication((uint)_appIDCurrent);
									
									if(_appIDCurrent == 0 && _appIDTarget == 0)
										cmd.Authenticate(0, aiToUse.MasterCardKey);
									else if(_appIDCurrent == 0 && _appIDTarget > 0)
									{
										cmd.Authenticate(0, aiToUse.MasterCardKey);
										cmd.SelectApplication((uint)_appIDTarget);
										cmd.Authenticate((byte)_keyNumberCurrent, aiToUse.MasterCardKey);
									}
									else
										cmd.Authenticate((byte)_keyNumberCurrent, aiToUse.MasterApplicationKey);
									
									cmd.ChangeKey((byte)1, applicationMasterKeyTarget);
									
									
									
									return ERROR.NoError;
								} catch (Exception e) {
									return ERROR.AuthenticationError;
								}

							}
							if (card.Type == "DESFireEV2") {
								
							}
							return ERROR.AuthenticationError;
						}
					}
				}
				return ERROR.IOError;
			}
			
			catch
			{
				return ERROR.AuthenticationError;
			}
		}

		public ERROR DeleteMifareDesfireApplication(string _applicationMasterKey, DESFireKeyType _keyType, int _appID = 0)
		{
			try
			{
				// The excepted memory tree
				IDESFireLocation location = new DESFireLocation();
				// The Application ID to use
				location.aid = _appID;
				// File communication requires encryption
				location.SecurityLevel = EncryptionMode.CM_ENCRYPT;
				
				IDESFireEV1Commands cmd;
				// Keys to use for authentication
				IDESFireAccessInfo aiToUse = new DESFireAccessInfo();
				CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_applicationMasterKey);
				aiToUse.MasterCardKey.Value = CustomConverter.desFireKeyToEdit;
				aiToUse.MasterCardKey.KeyType = _keyType;
				
				
				if (readerUnit.ConnectToReader()) {
					if (readerUnit.WaitInsertion(2000)) {
						if (readerUnit.Connect()) {
							
							readerUnitName = readerUnit.ConnectedName;
							//readerSerialNumber = readerUnit.GetReaderSerialNumber();
							
							card = readerUnit.GetSingleChip();
							
							if (card.Type == "DESFireEV1") {
								cmd = card.Commands as IDESFireEV1Commands;
								try {
									cmd.SelectApplication(0);
									cmd.Authenticate(0, aiToUse.MasterCardKey);
									
									cmd.DeleteApplication((uint)_appID);
									return ERROR.NoError;
								} catch {
									return ERROR.AuthenticationError;
								}

							}
							if (card.Type == "DESFireEV2") {
								
							}
							return ERROR.AuthenticationError;
						}
					}
				}
				return ERROR.IOError;
			}
			
			catch
			{
				return ERROR.AuthenticationError;
			}
			
		}
			
		public ERROR FormatDesfireCard(string _applicationMasterKey, DESFireKeyType _keyType, int _appID = 0)
		{
			try
			{
				// The excepted memory tree
				IDESFireLocation location = new DESFireLocation();
				// The Application ID to use
				location.aid = _appID;
				// File communication requires encryption
				location.SecurityLevel = EncryptionMode.CM_ENCRYPT;
				
				IDESFireEV1Commands cmd;
				// Keys to use for authentication
				IDESFireAccessInfo aiToUse = new DESFireAccessInfo();
				CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_applicationMasterKey);
				aiToUse.MasterCardKey.Value = CustomConverter.desFireKeyToEdit;
				aiToUse.MasterCardKey.KeyType = _keyType;
				
				
				if (readerUnit.ConnectToReader()) {
					if (readerUnit.WaitInsertion(2000)) {
						if (readerUnit.Connect()) {
							
							readerUnitName = readerUnit.ConnectedName;
							//readerSerialNumber = readerUnit.GetReaderSerialNumber();
							
							card = readerUnit.GetSingleChip();
							
							if (card.Type == "DESFireEV1") {
								cmd = card.Commands as IDESFireEV1Commands;
								try {
									cmd.SelectApplication(0);
									cmd.Authenticate(0, aiToUse.MasterCardKey);
									
									cmd.Erase();
									
									return ERROR.NoError;
								} catch {
									return ERROR.AuthenticationError;
								}

							}
							if (card.Type == "DESFireEV2") {
								
							}
							return ERROR.AuthenticationError;
						}
					}
				}
				return ERROR.IOError;
			}
			
			catch
			{
				return ERROR.AuthenticationError;
			}
			
		}
		
		
		private bool _disposed = false;
		
		protected virtual void Dispose(bool disposing)
		{
			if (!_disposed)
			{
				if (disposing)
				{
					instance = null;
					// Dispose any managed objects
					// ...
				}

				if(readerUnit != null)
				{
					readerUnit.Disconnect();
					readerUnit.DisconnectFromReader();
				}

				if(readerProvider != null)
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


