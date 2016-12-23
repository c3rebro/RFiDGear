/*
 * Created by SharpDevelop.
 * User: rotts
 * Date: 29.11.2016
 * Time: 15:31
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using LibLogicalAccess;
using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Runtime.InteropServices;
using System.Globalization;
using System.Linq;
using System.IO;
using System.Resources;
using System.Timers;
using System.Windows.Interop;

namespace RFiDGear
{
	/// <summary>
	/// Description of RFiDAccess.
	/// 
	/// Initialize Reader
	/// The folllowing Readers are supportet by the Lib
	/// 
	/// 	new PCSCReaderProvider(),
	///		new A3MLGM5600ReaderProviderClass(),
	///		new AdmittoReaderProviderClass(),
	///		new AxessTMC13ReaderProviderClass(),
	///		new DeisterReaderProviderClass(),
	///		new ElatecReaderProviderClass(),
	///		new GunneboReaderProviderClass(),
	///		new IdOnDemandReaderProviderClass(),
	///		new KeyboardReaderProviderClass(),
	///		new OK5553ReaderProviderClass(),
	///		new PCSCReaderProviderClass(),
	///		new PromagReaderProviderClass(),
	///		new RFIDeasReaderProviderClass(),
	///		new RplethReaderProviderClass(),
	///		new SCIELReaderProviderClass(),
	///		new SmartIDReaderProviderClass(),
	///		new STidPRGReaderProviderClass()
	/// 
	/// </summary>
	/// 
	
	public enum CARD_TYPE
	{
		CT_CLASSIC_1K,
		CT_CLASSIC_2K,
		CT_CLASSIC_4K,
		CT_DESFIRE_EV1,
		CT_DESFIRE_EV2
	};
	
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
	
	public class RFiDAccess
	{
		// global (cross-class) Instances go here ->
		IReaderProvider readerProvider;
		IReaderUnit readerUnit;
		chip card;
		
		string readerProviderName;
		string readerUnitName;
		string readerSerialNumber;
		string chipType;
		string chipUID;
		
		string classicCardKeyA;
		string classicCardKeyB;
		
		byte[] cardDataBlock;
		byte[][] cardDataSector;
		bool[] blockAuthSuccessful;
		bool[] blockReadSuccessful;
		bool sectorIsAuth;
		bool sectorCanRead;
		
		int blockCounter = 0;
		
		byte[] desFireFileData;

		UInt32[] appIDs;
		
		public RFiDAccess(string _readerProviderName)
		{
			readerProviderName = _readerProviderName;
		}
		
		//********************************************************
		//Function Name:
		//Input(Parameter) :-------
		//OutPutParameter:-------
		//Description:
		//********************************************************
		public void authToMifareDesfireCard()
		{
			helperClass converter = new helperClass();
			
		}
		
		public void setCurrentReaderProvider(IReaderProvider readerProvider)
		{
			readChipPublic();
		}
		
		
		//********************************************************
		//Function Name: readChip
		//Input Parameter:-------
		//OutPutParameter:-------
		//Description:Perform action after time interval passed
		//********************************************************
		public bool readChipPublic()
		{
			try {
				readerProvider = new LibraryManagerClass().GetReaderProvider(readerProviderName);
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
							
							readerUnit.Disconnect();
							readerUnit.DisconnectFromReader();
							
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
			return false;
		}

		//********************************************************
		//Function Name: readChip
		//Input Parameter:-------
		//OutPutParameter:-------
		//Description:Perform action after time interval passed
		//********************************************************
		public bool readMiFareClassicSingleSector(int sectorNumber,int keyNumber, MifareClassicAccessBits sab)
		{
			SettingsReaderWriter settings = new SettingsReaderWriter();
			
			settings.readSettings();
			
			MifareKey keyA = new MifareKey() { Value = new helperClass().FormatSectorStringWithSpacesEachByte(new SettingsReaderWriter()._defaultClassicCardKeysAKeys[keyNumber])};
			MifareKey keyB = new MifareKey() { Value = new helperClass().FormatSectorStringWithSpacesEachByte(new SettingsReaderWriter()._defaultClassicCardKeysBKeys[keyNumber])};
			
			int blockCount = 0, sectorCount = 0; sectorIsAuth = true; sectorCanRead = true;
			
			try {
				readerProvider = new LibraryManagerClass().GetReaderProvider(new SettingsReaderWriter()._defaultReaderProvider);
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
								sectorCount = 16;
								
								cardDataBlock = new byte[16];
								cardDataSector = new byte[4][];
								
							}
							if (card.Type == "Mifare4K") {
								
								blockAuthSuccessful = new bool[256];
								blockReadSuccessful = new bool[256];
								
								blockCount = 16;
								sectorCount = 40;
								
								cardDataBlock = new byte[16];
								cardDataSector = new byte[16][];
								
							}
							

							IMifareCommands cmd = card.Commands as IMifareCommands;

							try {
								cmd.LoadKeyNo((byte)keyNumber, keyA, MifareKeyType.KT_KEY_A);
								
								for (int k = blockCount; k > 0; k--) {

									try {
										cmd.AuthenticateKeyNo((byte)(((sectorNumber+1)*blockCount)-k), (byte)keyNumber, MifareKeyType.KT_KEY_A);
										blockAuthSuccessful[(((sectorNumber+1)*blockCount)-k)] = true;
										
										try {
											object data = cmd.ReadBinary((byte)(((sectorNumber+1)*blockCount)-k), 48);
											
											cardDataBlock = (byte[])data;
											cardDataSector[blockCount-k] = cardDataBlock;
											
											blockReadSuccessful[(((sectorNumber+1)*blockCount)-k)] = true;
										} catch (Exception a) {
											blockReadSuccessful[(((sectorNumber+1)*blockCount)-k)] = false;
											sectorCanRead = false;
										}
									} catch (Exception b) {
										blockAuthSuccessful[(((sectorNumber+1)*blockCount)-k)] = false;
										sectorIsAuth = false;
									}
								}
							} catch (Exception c) {
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
		
		
		public bool getMiFareDESFireChipAppIDs()
		{
			
			// The excepted memory tree
			IDESFireLocation location = new DESFireLocation();
			// File communication requires encryption
			location.SecurityLevel = EncryptionMode.CM_ENCRYPT;
			
			
			IDESFireEV1Commands cmd;
			// Keys to use for authentication
			IDESFireAccessInfo aiToUse = new DESFireAccessInfo();
			aiToUse.MasterCardKey.Value = "00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00";

			helperClass converter = new helperClass();
			
			if (readerUnit.ConnectToReader()) {
				if (readerUnit.WaitInsertion(100)) {
					if (readerUnit.Connect()) {
						
						if (card.Type == "DESFireEV1") {
							cmd = card.Commands as IDESFireEV1Commands;
							
							object appIDsObject = cmd.GetApplicationIDs();
							appIDs = (appIDsObject as UInt32[]);
							
						}
						if (card.Type == "DESFireEV2") {
							
						}
						
						readerUnit.Disconnect();
						readerUnit.DisconnectFromReader();
						return false;
					}
				}
				readerUnit.DisconnectFromReader();
			}
			return true;
		}
		
		public bool readMiFareDESFireChipFile(int fileNo, int appid)
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

			helperClass converter = new helperClass();
			
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
		
		public bool writeMiFareDESFireChipFile(int fileNo, int appid)
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

			helperClass converter = new helperClass();
			
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
		
		public bool authToMifareDesfireMasterApplication(string cardMasterKey, DESFireKeyType keyType)
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
			
			helperClass converter = new helperClass();
			
			
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
		
		//********************************************************
		//Function Name: writeChip
		//Input Parameter:-------
		//OutPutParameter:-------
		//Description:Perform action after time interval passed
		//********************************************************
		/*
 		public bool writeChipData()
		{
				
			string workSectorTrailer = "000000";

			int i = 0;
			int blockCounter = 0;
			
			bool authSuccess = false;
			bool readSuccess = false;
			bool authKeyA = true;
						
			timer1.Stop();
			if (classicCardKeyA.Length != 17 || classicCardKeyB.Length != 17)
				return true;
			
			if (true) {
				
				helperClass converter = new helperClass();
				settings.CryptoKeys[0] = classicCardKeyA;
				settings.saveSettings(null, settings.CryptoKeys, null);
	
				if (readerUnit.ConnectToReader()) {
					if (readerUnit.WaitInsertion(100)) {
						if (readerUnit.Connect()) {
							//helperClass.createLogEntry(richTextBoxLog,res_man.GetString("richTextBoxLogCardConnectionSuccessful",cul) + "\n", System.Drawing.Color.Black);
							
							IMifareCommands cmd = card.Commands as IMifareCommands;
							
							MifareKey key = new MifareKey();
							key.Value = "ff ff ff ff ff ff"; //textBoxClassicCardKey.Text;
							cmd.LoadKeyNo(1, key, MifareKeyType.KT_KEY_A);
							cmd.AuthenticateKeyNo(0, 1, MifareKeyType.KT_KEY_A);
							cmd.wReadBinary(0, 16);

							SectorAccessBits sab = new SectorAccessBits();

							for (i = 0; i < 15; i++) {
								cmd.LoadKeyNo((byte)i, key, MifareKeyType.KT_KEY_A);
							}

							cardData = new byte[16][];

							for (i = 1; i < 15; i++) {
								try {
									
									object data = cmd.ReadSector(i, key, key, sab, true);

									cardData[i] = (byte[])data;

								} catch {
									throw;
								}
		
								readSuccess = false;
								authKeyA = true;
								authSuccess = false;
								readSuccess = false;
									
							}

						} else
							textBoxCardStatus.Text = "Verbindungsfehler";
						return true;
					}
					return true;
				}
				return true;
			}
		}

		 */
		
		#region properties
		public string currentReaderUnitName {
			get { return readerUnitName; }
		}
		
		public string ReaderSerialNumber {
			get { return readerSerialNumber; }
		}
		
		public string currentChipType {
			get { return chipType; }
		}
		
		public string currentChipUID {
			get {return chipUID; }
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
		
		public bool[] dataBlockSuccessfullyRead {
			get { return blockReadSuccessful; }
		}
		
		public bool[] dataBlockSuccesfullyAuth {
			get{ return blockAuthSuccessful; }
		}
		
		public bool? sectorSuccessfullyRead {
			get { return sectorCanRead; }
		}
		
		public bool? sectorSuccesfullyAuth {
			get{ return sectorIsAuth; }
		}
		
		public UInt32[] getAppIDs {
			get{ return appIDs; }
		}
		
		public byte[] getDESFireFileData {
			get { return desFireFileData; }
		}
		#endregion
	}

	

	



	public class dataSourceClassMifareBlocks
	{

		byte[] currentMifareClassicSector;
		
		helperClass converter = new helperClass();
		
		byte blocknSectorData;
		int discarded;
		
		public dataSourceClassMifareBlocks(byte[] dataBlock, int indexByte)
		{
			currentMifareClassicSector = dataBlock;
			blocknSectorData = currentMifareClassicSector[indexByte];

		}
		
		[DisplayName("Nr")]
		public string RowNumber{
			get { return "0";}
		}
		
		[DisplayName("Int")]
		public byte singleByteBlock0AsByte {
			get { return blocknSectorData; }
			set { blocknSectorData = value; }
		}
		
		[DisplayName("Hex")]
		public string singleByteBlock0AsString {
			get { return blocknSectorData.ToString("X2"); }
			set { blocknSectorData = converter.GetBytes(value, out discarded)[0]; }
		}

		[DisplayName("ASCII")]
		public char singleByteBlock0AsChar {
			get {
				if (blocknSectorData < 32 | blocknSectorData > 126)
					return '.';
				else
					return (char)blocknSectorData;
			}
			
			set {
				if ((byte)value < 32 | (byte)value > 126)
					blocknSectorData |= 0;
				else
					blocknSectorData = (byte)value;
			}
			
		}
	}

	public class dataSourceClassDESFireFileData
	{
		
		helperClass converter = new helperClass();
		
		byte data;
		int discarded;
		
		public dataSourceClassDESFireFileData(byte[] readerClass, int index)
		{
			data = readerClass[index];
		}
		
		[DisplayName("Data as Int")]
		public byte singleByteAsByte {
			get { return data; }
			set { data = value; }
		}
		
		[DisplayName("Data as Hex")]
		public string singleByteAsString {
			get { return data.ToString("X2"); }
			set { data = converter.GetBytes(value, out discarded)[0]; }
		}

		[DisplayName("Data as ASCII")]
		public char singleByteAsChar {
			get {
				if (data < 32 | data > 126)
					return '.';
				else
					return (char)data;
			}
			
			set {
				if ((byte)value < 32 | (byte)value > 126)
					data |= 0;
				else
					data = (byte)value;
			}
		}
	}
}


