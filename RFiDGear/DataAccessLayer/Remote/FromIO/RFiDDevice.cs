using LibLogicalAccess;
using LibLogicalAccess.Card;
using LibLogicalAccess.Reader;
using LibLogicalAccess.Crypto;

using ByteArrayHelper.Extensions;
using Elatec.NET;
using RFiDGear.DataAccessLayer;
using RFiDGear.Model;
using System;
using System.Threading;

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
		private ReaderProvider readerProvider;
		private ReaderUnit readerUnit;
		private TWN4ReaderDevice readerDevice;
		private Chip card;
		private bool _disposed = false;

		#region properties

		public MifareClassicSectorModel Sector { get; private set; }

		public MifareClassicDataBlockModel DataBlock { get; private set; }
		
        public GenericChipModel GenericChip { get; private set; }

		public ReaderTypes ReaderProvider { get; private set; }

		public string ReaderUnitName { get; private set; }

		//public CARD_INFO CardInfo { get; private set; }

		public byte[] MifareClassicData { get; private set; }

		public bool DataBlockSuccessfullyRead { get; private set; }

		public bool DataBlockSuccesfullyAuth { get; private set; }

		public bool SectorSuccessfullyRead { get; private set; }

		public bool SectorSuccesfullyAuth { get; private set; }

		public byte[] MifareDESFireData { get; private set; }

		public uint[] AppIDList { get; private set; }

		public byte[] FileIDList { get; private set; }
		
		public byte[] MifareUltralightPageData { get; private set; }

		public byte MaxNumberOfAppKeys { get; private set; }

		public byte EncryptionType { get; private set; }

		//public uint FreeMemory { get; private set; }

		// FIXME: FILESETTINGS
		public DESFireCommands.FileSetting DesfireFileSettings { get; private set; }

		public DESFireKeySettings DesfireAppKeySetting { get; private set; }

		#endregion properties

		#region contructor

		public static RFiDDevice Instance
		{
			get
			{
				lock (RFiDDevice.syncRoot)
				{
					if (instance == null)
					{
						instance = new RFiDDevice();
						return instance;
					}
					else
						return null;
				}
			}
		}
		
		private static object syncRoot = new object();
		private static RFiDDevice instance;

		public RFiDDevice() : this(ReaderTypes.None)
		{
		}

		public RFiDDevice(ReaderTypes _readerType = ReaderTypes.None)
		{
			try
			{
				using (SettingsReaderWriter defaultSettings = new SettingsReaderWriter())
				{
					if(defaultSettings.DefaultSpecification.DefaultReaderProvider == ReaderTypes.PCSC)
                    {
						ReaderProvider = _readerType != ReaderTypes.None ? _readerType : defaultSettings.DefaultSpecification.DefaultReaderProvider;

						LibraryManager lm = LibraryManager.getInstance();
						
						readerProvider = lm.getReaderProvider(Enum.GetName(typeof(ReaderTypes), ReaderProvider));

						readerUnit = readerProvider.createReaderUnit();

						GenericChip = new GenericChipModel("", CARD_TYPE.Unspecified);
					}

					else if(defaultSettings.DefaultSpecification.DefaultReaderProvider == ReaderTypes.Elatec)
                    {
						int portNumber;

						ReaderProvider = _readerType != ReaderTypes.None ? _readerType : defaultSettings.DefaultSpecification.DefaultReaderProvider;

						if(int.TryParse(defaultSettings.DefaultSpecification.LastUsedComPort,out portNumber))
							readerDevice = new TWN4ReaderDevice(portNumber);

						readerDevice.ReadChipPublic();
					}

					AppIDList = new uint[0];
				}
			} 
			catch (Exception e)
			{
				LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""));
			}
		}

		#endregion contructor

		#region common

		public ERROR ChangeProvider(ReaderTypes _provider)
		{
			if (Enum.IsDefined(typeof(ReaderTypes), _provider))
			{
				if (readerProvider != null)
				{
					try
					{
                        readerUnit.disconnectFromReader();
                        readerProvider.release();
					}
					catch
					{
						return ERROR.IOError;
					}
				}

				ReaderProvider = _provider;

				try
				{
					LibraryManager lm = LibraryManager.getInstance();

					readerProvider = lm.getReaderProvider(Enum.GetName(typeof(ReaderTypes), ReaderProvider));

					readerUnit = readerProvider.createReaderUnit();

					return ERROR.NoError;
				}
				catch
				{
					return ERROR.IOError;
				}
			}

			return ERROR.IOError;
		}

		public ERROR ReadChipPublic()
		{
			try
			{
				if (readerUnit.connectToReader())
				{
					if (readerUnit.waitInsertion(Constants.MAX_WAIT_INSERTION))
					{
						if (readerUnit.connect())
						{
							ReaderUnitName = readerUnit.getConnectedName();
							//string readerSerialNumber = readerUnit.GetReaderSerialNumber(); //-> ERROR with OmniKey (and some others?) Reader when card isnt removed before recalling!

							card = readerUnit.getSingleChip();

							if (!string.IsNullOrWhiteSpace(ByteConverter.HexToString(card.getChipIdentifier().ToArray())))
							{
								try {
									//CardInfo = new CARD_INFO((CARD_TYPE)Enum.Parse(typeof(CARD_TYPE), card.getCardType()), ByteConverter.HexToString(card.getChipIdentifier().ToArray()));
									//readerUnit.Disconnect();
                                    GenericChip = new GenericChipModel(ByteConverter.HexToString(card.getChipIdentifier().ToArray()), (CARD_TYPE)Enum.Parse(typeof(CARD_TYPE), card.getCardType()));
									//ISO15693Commands commands = card.Commands as ISO15693Commands;
									//SystemInformation si = commands.GetSystemInformation();
									//var block=commands.ReadBlock(21, 4);
                                    return ERROR.NoError;
								}
								catch (Exception e) {
									LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""));
									
									return ERROR.IOError;
								}
							}
							else
								return ERROR.DeviceNotReadyError;
						}
					}
				}
			}
			catch (Exception e)
			{
				if (readerProvider != null)
					readerProvider.release();

				LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""));

				return ERROR.IOError;
			}

			return ERROR.IOError;
		}

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

				if (readerUnit != null)
				{
					readerUnit.disconnect();
					readerUnit.disconnectFromReader();
				}

				if (readerProvider != null)
					readerProvider.release();
				// Now disposed of any unmanaged objects
				// ...

				Thread.Sleep(200);
				_disposed = true;
			}
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		#endregion common

		#region mifare classic

		public ERROR ReadMiFareClassicSingleSector(int sectorNumber, string aKey, string bKey)
		{
			var settings = new SettingsReaderWriter();
			Sector = new MifareClassicSectorModel();

			settings.ReadSettings();

			var keyA = new MifareKey(CustomConverter.KeyFormatQuickCheck(aKey) ? aKey : CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(aKey) );
			var keyB = new MifareKey(CustomConverter.KeyFormatQuickCheck(bKey) ? bKey : CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(bKey) );

			try
			{
				if (readerUnit.connectToReader()) {
					if (readerUnit.waitInsertion(Constants.MAX_WAIT_INSERTION))	{
						if (readerUnit.connect())
						{
							ReaderUnitName = readerUnit.getConnectedName();

							card = readerUnit.getSingleChip();

							if (!string.IsNullOrWhiteSpace(ByteConverter.HexToString(card.getChipIdentifier().ToArray())))
							{
								try {
                                    //CardInfo = new CARD_INFO((CARD_TYPE)Enum.Parse(typeof(CARD_TYPE), card.getCardType()), ByteConverter.HexToString(card.getChipIdentifier().ToArray()));
                                    GenericChip = new GenericChipModel(ByteConverter.HexToString(card.getChipIdentifier().ToArray()), (CARD_TYPE)Enum.Parse(typeof(CARD_TYPE), card.getCardType()));
                                }
								catch (Exception e) {
									LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""));
								}
							}

							var cmd = card.getCommands() as MifareCommands;

							try
							{ //try to Auth with Keytype A
								for (int k = 0; k < (sectorNumber > 31 ? 16 : 4); k++) // if sector > 31 is 16 blocks each sector i.e. mifare 4k else its 1k or 2k with 4 blocks each sector
								{
									cmd.loadKey((byte)0, MifareKeyType.KT_KEY_A, keyA);

									DataBlock = new MifareClassicDataBlockModel(
										(byte)CustomConverter.GetChipBasedDataBlockNumber(GenericChip.CardType, sectorNumber, k),
										k);

									try
									{
										cmd.authenticate((byte)CustomConverter.GetChipBasedDataBlockNumber(GenericChip.CardType, sectorNumber, k),
										                      (byte)0,
										                      MifareKeyType.KT_KEY_A);

										Sector.IsAuthenticated = true;

										try
										{
											object data = cmd.readBinary(
												(byte)CustomConverter.GetChipBasedDataBlockNumber(GenericChip.CardType, sectorNumber, k),
												48);

											DataBlock.Data = (byte[])data;
											
											DataBlock.IsAuthenticated = true;

											Sector.DataBlock.Add(DataBlock);
										}
										catch
										{
											DataBlock.IsAuthenticated = false;
											Sector.DataBlock.Add(DataBlock);
										}
									}
									catch
									{ // Try Auth with keytype b

										try
										{
											cmd.loadKey((byte)1, MifareKeyType.KT_KEY_B, keyB);

											cmd.authenticate(
												(byte)CustomConverter.GetChipBasedDataBlockNumber(GenericChip.CardType, sectorNumber, k),
												(byte)1,
												MifareKeyType.KT_KEY_B);

											Sector.IsAuthenticated = true;

											try
											{
												object data = cmd.readBinary(
													(byte)CustomConverter.GetChipBasedDataBlockNumber(GenericChip.CardType, sectorNumber, k),
													48);

												DataBlock.Data = (byte[])data;
												
												DataBlock.IsAuthenticated = true;

												Sector.DataBlock.Add(DataBlock);
											}
											catch
											{

												DataBlock.IsAuthenticated = false;

												Sector.DataBlock.Add(DataBlock);

												return ERROR.AuthenticationError;
											}
										}
										catch
										{
											Sector.IsAuthenticated = false;
											DataBlock.IsAuthenticated = false;

											Sector.DataBlock.Add(DataBlock);
											
											return ERROR.AuthenticationError;
										}
									}
								}
							}
							catch
							{
								return ERROR.NoError;
							}
							return ERROR.NoError;
						}
						return ERROR.DeviceNotReadyError;
					}
					return ERROR.DeviceNotReadyError;
				}
				return ERROR.DeviceNotReadyError;
			}
			catch (Exception e)
			{
				LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""));
				return ERROR.AuthenticationError;
			}
		}

		public ERROR WriteMiFareClassicSingleSector(int sectorNumber, string _aKey, string _bKey, byte[] buffer)
		{
			var keyA = new MifareKey(CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(_aKey));
			var keyB = new MifareKey(CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(_bKey));

			int blockCount = 0;

			try
			{
				if (readerUnit.connectToReader())
				{
					if (readerUnit.waitInsertion(Constants.MAX_WAIT_INSERTION))
					{
						if (readerUnit.connect())
						{
							ReaderUnitName = readerUnit.getConnectedName();
							//readerSerialNumber = readerUnit.GetReaderSerialNumber();

							card = readerUnit.getSingleChip();

							if (!string.IsNullOrWhiteSpace(ByteConverter.HexToString(card.getChipIdentifier().ToArray())))
							{
								try {
                                    //CardInfo = new CARD_INFO((CARD_TYPE)Enum.Parse(typeof(CARD_TYPE), card.getCardType()), ByteConverter.HexToString(card.getChipIdentifier().ToArray()));
                                    GenericChip = new GenericChipModel(ByteConverter.HexToString(card.getChipIdentifier().ToArray()), (CARD_TYPE)Enum.Parse(typeof(CARD_TYPE), card.getCardType()));
                                }
								catch (Exception e) {
									LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""));
								}
							}

							var cmd = card.getCommands() as MifareCommands;

							try
							{ //try to Auth with Keytype A
								cmd.loadKey((byte)0, MifareKeyType.KT_KEY_A, keyA);
								cmd.loadKey((byte)1, MifareKeyType.KT_KEY_B, keyB);

								for (int k = 0; k < blockCount; k++)
								{
									try
									{
										cmd.authenticate(
											(byte)CustomConverter.GetChipBasedDataBlockNumber(GenericChip.CardType, sectorNumber, k),
											(byte)0,
											MifareKeyType.KT_KEY_A);

										try
										{
											cmd.updateBinary(
												(byte)CustomConverter.GetChipBasedDataBlockNumber(GenericChip.CardType, sectorNumber, k),
												new ByteVector(buffer));

											return ERROR.NoError;
										}
										catch
										{
											return ERROR.AuthenticationError;
										}
									}
									catch
									{ // Try Auth with keytype b

										try
										{
											cmd.authenticate((byte)CustomConverter.GetChipBasedDataBlockNumber(GenericChip.CardType, sectorNumber, k),
											                      (byte)1,
											                      MifareKeyType.KT_KEY_B);

											try
											{
												cmd.updateBinary(
													(byte)CustomConverter.GetChipBasedDataBlockNumber(GenericChip.CardType, sectorNumber, k),
													new ByteVector(buffer));

												return ERROR.NoError;

											}
											catch
											{
												return ERROR.AuthenticationError;
											}
										}
										catch
										{
											
										}
									}
								}
							}
							catch
							{
								return ERROR.IOError;
							}
							return ERROR.DeviceNotReadyError;
						}
					}
				}
			}
			catch (Exception e)
			{
				LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""));

				return ERROR.IOError;
			}
			return ERROR.DeviceNotReadyError;
		}

		public ERROR WriteMiFareClassicSingleBlock(int _blockNumber, string _aKey, string _bKey, byte[] buffer)
		{
			try
			{
				var keyA = new MifareKey(CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(_aKey));
				var keyB = new MifareKey(CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(_bKey));

				if (readerUnit.connectToReader())
				{
					if (readerUnit.waitInsertion(Constants.MAX_WAIT_INSERTION))
					{
						if (readerUnit.connect())
						{
							ReaderUnitName = readerUnit.getConnectedName();
							//readerSerialNumber = readerUnit.GetReaderSerialNumber();

							card = readerUnit.getSingleChip();

							if (!string.IsNullOrWhiteSpace(ByteConverter.HexToString(card.getChipIdentifier().ToArray())))
							{
								try {
                                    //CardInfo = new CARD_INFO((CARD_TYPE)Enum.Parse(typeof(CARD_TYPE), card.getCardType()), ByteConverter.HexToString(card.getChipIdentifier().ToArray()));
                                    GenericChip = new GenericChipModel(ByteConverter.HexToString(card.getChipIdentifier().ToArray()), (CARD_TYPE)Enum.Parse(typeof(CARD_TYPE), card.getCardType()));
                                }
								catch (Exception e) {
									LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""));
								}
							}

							var cmd = card.getCommands() as MifareCommands;

							try
							{
								cmd.loadKey((byte)0, MifareKeyType.KT_KEY_A, keyA); // FIXME "sectorNumber" to 0

								try
								{ //try to Auth with Keytype A
									cmd.authenticate((byte)(_blockNumber), (byte)0, MifareKeyType.KT_KEY_A); // FIXME same as '303

									cmd.updateBinary((byte)(_blockNumber), new ByteVector(buffer));

									return ERROR.NoError;
								}
								catch
								{ // Try Auth with keytype b

									cmd.loadKey((byte)0, MifareKeyType.KT_KEY_B, keyB);

									try
									{
										cmd.authenticate((byte)(_blockNumber), (byte)0, MifareKeyType.KT_KEY_B); // FIXME same as '303

										cmd.updateBinary((byte)(_blockNumber), new ByteVector(buffer));

										return ERROR.NoError;
									}
									catch
									{
										return ERROR.AuthenticationError;
									}
								}
							}
							catch
							{
								return ERROR.AuthenticationError;
							}
						}
					}
				}
			}
			catch (Exception e)
			{
				LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""));

				return ERROR.IOError;
			}
			return ERROR.IOError;
		}

		public ERROR ReadMiFareClassicSingleBlock(int _blockNumber, string _aKey, string _bKey)
		{
			try
			{
				var keyA = new MifareKey(CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(_aKey));
				var keyB = new MifareKey(CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(_bKey));

				if (readerUnit.connectToReader())
				{
					if (readerUnit.waitInsertion(Constants.MAX_WAIT_INSERTION))
					{
						if (readerUnit.connect())
						{
							ReaderUnitName = readerUnit.getConnectedName();
							//readerSerialNumber = readerUnit.GetReaderSerialNumber();

							card = readerUnit.getSingleChip();

							if (!string.IsNullOrWhiteSpace(ByteConverter.HexToString(card.getChipIdentifier().ToArray())))
							{
								try {
                                    //CardInfo = new CARD_INFO((CARD_TYPE)Enum.Parse(typeof(CARD_TYPE), card.getCardType()), ByteConverter.HexToString(card.getChipIdentifier().ToArray()));
                                    GenericChip = new GenericChipModel(ByteConverter.HexToString(card.getChipIdentifier().ToArray()), (CARD_TYPE)Enum.Parse(typeof(CARD_TYPE), card.getCardType()));
                                }
								catch (Exception e) {
									LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""));
								}
							}

							var cmd = card.getCommands() as MifareCommands;

							try
							{
								cmd.loadKey((byte)0, MifareKeyType.KT_KEY_A, keyA); // FIXME "sectorNumber" to 0

								try
								{ //try to Auth with Keytype A
									cmd.authenticate((byte)(_blockNumber), (byte)0, MifareKeyType.KT_KEY_A); // FIXME same as '303

									MifareClassicData = cmd.readBinary((byte)(_blockNumber), 48).ToArray();

									return ERROR.NoError;
								}
								catch
								{ // Try Auth with keytype b

									cmd.loadKey((byte)0, MifareKeyType.KT_KEY_B, keyB);

									try
									{
										cmd.authenticate((byte)(_blockNumber), (byte)0, MifareKeyType.KT_KEY_B); // FIXME same as '303

										MifareClassicData = cmd.readBinary((byte)(_blockNumber), 48).ToArray();

										return ERROR.NoError;
									}
									catch
									{
										return ERROR.AuthenticationError;
									}
								}
							}
							catch
							{
								return ERROR.AuthenticationError;
							}
						}
					}
				}
			}
			catch (Exception e)
			{
				LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""));

				return ERROR.IOError;
			}
			return ERROR.IOError;
		}
		
		public ERROR WriteMiFareClassicWithMAD(int _madApplicationID, int _madStartSector,
		                                       string _madAKeyToUse, string _madBKeyToUse, string _madAKeyToWrite, string _madBKeyToWrite,
		                                       string _aKeyToUse, string _bKeyToUse, string _aKeyToWrite, string _bKeyToWrite,
		                                       byte[] buffer, byte _madGPB, bool _useMADToAuth = false)
		{
			var settings = new SettingsReaderWriter();
			Sector = new MifareClassicSectorModel();

			settings.ReadSettings();

			var mAKeyToUse = new MifareKey(CustomConverter.KeyFormatQuickCheck(_aKeyToUse) ? _aKeyToUse : CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(_aKeyToUse));
			var mBKeyToUse = new MifareKey(CustomConverter.KeyFormatQuickCheck(_bKeyToUse) ? _bKeyToUse : CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(_bKeyToUse));

			var mAKeyToWrite = new MifareKey(CustomConverter.KeyFormatQuickCheck(_aKeyToWrite) ? _aKeyToWrite : CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(_aKeyToWrite));
			var mBKeyToWrite = new MifareKey(CustomConverter.KeyFormatQuickCheck(_bKeyToWrite) ? _bKeyToWrite : CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(_bKeyToWrite));
			
			var madAKeyToUse = new MifareKey(CustomConverter.KeyFormatQuickCheck(_madAKeyToUse) ? _madAKeyToUse : CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(_madAKeyToUse));
			var madBKeyToUse = new MifareKey(CustomConverter.KeyFormatQuickCheck(_madBKeyToUse) ? _madBKeyToUse : CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(_madBKeyToUse));

			var madAKeyToWrite = new MifareKey(CustomConverter.KeyFormatQuickCheck(_madAKeyToWrite) ? _madAKeyToWrite : CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(_madAKeyToWrite));
			var madBKeyToWrite = new MifareKey(CustomConverter.KeyFormatQuickCheck(_madBKeyToWrite) ? _madBKeyToWrite : CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(_madBKeyToWrite));
			
			try
			{
				if (readerUnit.connectToReader()) {
					if (readerUnit.waitInsertion(Constants.MAX_WAIT_INSERTION))	{
						if (readerUnit.connect())
						{
							ReaderUnitName = readerUnit.getConnectedName();
							//readerSerialNumber = readerUnit.GetReaderSerialNumber();

							card = readerUnit.getSingleChip();

							if (!string.IsNullOrWhiteSpace(ByteConverter.HexToString(card.getChipIdentifier().ToArray())))
							{
								try {
                                    //CardInfo = new CARD_INFO((CARD_TYPE)Enum.Parse(typeof(CARD_TYPE), card.getCardType()), ByteConverter.HexToString(card.getChipIdentifier().ToArray()));
                                    GenericChip = new GenericChipModel(ByteConverter.HexToString(card.getChipIdentifier().ToArray()), (CARD_TYPE)Enum.Parse(typeof(CARD_TYPE), card.getCardType()));
                                }
								catch (Exception e) {
									LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""));
								}
							}

							MifareLocation mlocation = new MifareLocation(); //card.CreateLocation() as MifareLocation;
							mlocation.aid = (ushort)_madApplicationID;
							mlocation.useMAD = true;
							mlocation.sector = _madStartSector;
							
							MifareAccessInfo aiToWrite = new MifareAccessInfo();
							aiToWrite.useMAD = true;
							aiToWrite.madKeyA.setCipherKey(_madAKeyToUse == _madAKeyToWrite ? madAKeyToUse.getString(true) : madAKeyToWrite.getString(true)); // only set new madkey if mad key has changed
							aiToWrite.madKeyB.setCipherKey(_madBKeyToUse == _madBKeyToWrite ? madBKeyToUse.getString(true) : madBKeyToWrite.getString(true)); // only set new madkey if mad key has changed
							aiToWrite.keyA.setCipherKey(_aKeyToUse == _aKeyToWrite ? mAKeyToUse.getString(true) : mAKeyToWrite.getString(true));
							aiToWrite.keyB.setCipherKey(_bKeyToUse == _bKeyToWrite ? mBKeyToUse.getString(true) : mBKeyToWrite.getString(true));
							aiToWrite.madGPB = _madGPB;
							
							var aiToUse = new MifareAccessInfo();
							aiToUse.useMAD = _useMADToAuth;
							aiToUse.keyA = mAKeyToUse;
							aiToUse.keyB = mBKeyToUse;
							
							if(_useMADToAuth)
							{
								aiToUse.madKeyA = madAKeyToUse;
								aiToUse.madKeyB = madBKeyToUse;
								aiToUse.madGPB = _madGPB;
							}

							
							var cmd = card.getCommands() as MifareCommands;
							var cardService = card.getService(LibLogicalAccess.CardServiceType.CST_STORAGE) as StorageCardService;
							
							try
							{
								cardService.writeData(mlocation, aiToUse, aiToWrite, new ByteVector(buffer), CardBehavior.CB_AUTOSWITCHAREA);
								
							}
							catch (Exception e)
							{
                                LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""));
                                return ERROR.AuthenticationError;
							}
							return ERROR.NoError;
						}
					}
				}
			}
			catch (Exception e)
			{
				LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""));
				return ERROR.AuthenticationError;
			}
			return ERROR.NoError;
		}
		
		public ERROR ReadMiFareClassicWithMAD(int madApplicationID, string _aKeyToUse, string _bKeyToUse, string _madAKeyToUse, string _madBKeyToUse, int _length)
		{
			var settings = new SettingsReaderWriter();
			Sector = new MifareClassicSectorModel();

			settings.ReadSettings();

			var mAKeyToUse = new MifareKey(CustomConverter.KeyFormatQuickCheck(_aKeyToUse) ? _aKeyToUse : CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(_aKeyToUse));
			var mBKeyToUse = new MifareKey(CustomConverter.KeyFormatQuickCheck(_bKeyToUse) ? _bKeyToUse : CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(_bKeyToUse));

			var madAKeyToUse = new MifareKey(CustomConverter.KeyFormatQuickCheck(_madAKeyToUse) ? _madAKeyToUse : CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(_madAKeyToUse));
			var madBKeyToUse = new MifareKey(CustomConverter.KeyFormatQuickCheck(_madBKeyToUse) ? _madBKeyToUse : CustomConverter.FormatMifareClassicKeyWithSpacesEachByte(_madBKeyToUse));
			
			try
			{
				if (readerUnit.connectToReader()) {
					if (readerUnit.waitInsertion(Constants.MAX_WAIT_INSERTION))	{
						if (readerUnit.connect())
						{
							ReaderUnitName = readerUnit.getConnectedName();
							//readerSerialNumber = readerUnit.GetReaderSerialNumber();

							card = readerUnit.getSingleChip();

							if (!string.IsNullOrWhiteSpace(ByteConverter.HexToString(card.getChipIdentifier().ToArray())))
							{
								try {
                                    //CardInfo = new CARD_INFO((CARD_TYPE)Enum.Parse(typeof(CARD_TYPE), card.getCardType()), ByteConverter.HexToString(card.getChipIdentifier().ToArray()));
                                    GenericChip = new GenericChipModel(ByteConverter.HexToString(card.getChipIdentifier().ToArray()), (CARD_TYPE)Enum.Parse(typeof(CARD_TYPE), card.getCardType()));
                                }
								catch (Exception e) {
									LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""));
								}
							}

							MifareLocation mlocation = card.createLocation() as MifareLocation;
							mlocation.aid = (ushort)madApplicationID;
							mlocation.useMAD = true;
							
							var aiToUse = new MifareAccessInfo();
							aiToUse.useMAD = true;
							aiToUse.keyA = mAKeyToUse;
							aiToUse.keyB = mBKeyToUse;
							aiToUse.madKeyA = madAKeyToUse;
							aiToUse.madKeyB = madBKeyToUse;
							
							var cmd = card.getCommands() as MifareCommands;
							var cardService = card.getService(CardServiceType.CST_STORAGE) as StorageCardService;
							
							try
							{
								MifareClassicData = cardService.readData(mlocation, aiToUse, (uint)_length, CardBehavior.CB_AUTOSWITCHAREA).ToArray();
							}
							catch (Exception e)
							{
                                LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""));
                                return ERROR.AuthenticationError;
							}
							return ERROR.NoError;
						}
					}
				}
			}
			catch (Exception e)
			{
				LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""));
				return ERROR.AuthenticationError;
			}
			return ERROR.NoError;
		}
		
		#endregion mifare classic

		#region mifare ultralight

		public ERROR ReadMifareUltralightSinglePage(int _pageNo)
		{
			try
			{
				if (readerUnit.connectToReader())
				{
					if (readerUnit.waitInsertion(Constants.MAX_WAIT_INSERTION))
					{
						if (readerUnit.connect())
						{
							ReaderUnitName = readerUnit.getConnectedName();
							//readerSerialNumber = readerUnit.GetReaderSerialNumber();
							RawFormat format = new RawFormat();
							
							var chip = readerUnit.getSingleChip() as MifareUltralightChip;

							var service = chip.getService(CardServiceType.CST_STORAGE) as StorageCardService;
							
							Location location = chip.createLocation() as Location;
							
							if (chip.getCardType() == "MifareUltralight")
							{
								var cmd = chip.getCommands() as MifareUltralightCommands;// IMifareUltralightCommands;
								MifareUltralightPageData = cmd.readPages(_pageNo, _pageNo).ToArray();
							}

							return ERROR.NoError;
						}
					}
				}
				return ERROR.NoError;
			}
			catch (Exception e)
			{
				LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""));
				return ERROR.IOError;
			}
		}

		#endregion mifare ultralight

		#region mifare desfire

		public ERROR GetMiFareDESFireChipAppIDs(string _appMasterKey = "00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00", DESFireKeyType _keyTypeAppMasterKey = DESFireKeyType.DF_KEY_DES)
		{
			try
			{
				// The excepted memory tree
				DESFireLocation location = new DESFireLocation();
				// File communication requires encryption
				location.securityLevel = EncryptionMode.CM_ENCRYPT;

				// Keys to use for authentication
				DESFireAccessInfo aiToUse = new DESFireAccessInfo();
				aiToUse.masterCardKey.setCipherKey(_appMasterKey);
				aiToUse.masterCardKey.setKeyType(_keyTypeAppMasterKey);


				if (readerUnit.connectToReader())
				{
					if (readerUnit.waitInsertion(Constants.MAX_WAIT_INSERTION))
					{
						if (readerUnit.connect())
						{
							ReaderUnitName = readerUnit.getConnectedName();

							card = readerUnit.getSingleChip();

							//Get AppIDs without Authentication (Free Directory Listing)
							try
							{

								if (card.getCardType() == "DESFire")
								{
									var cmd = card.getCommands() as DESFireCommands;

									try
									{
										cmd.selectApplication((uint)0);

										object appIDsObject = cmd.getApplicationIDs();
										AppIDList = (appIDsObject as UInt32[]);

										return ERROR.NoError;
									}
									catch
									{
										//Get AppIDs with Authentication (Directory Listing with PICC MK)
										try
										{
											cmd.selectApplication((uint)0);
											cmd.authenticate((byte)0, aiToUse.masterCardKey);

											object appIDsObject = cmd.getApplicationIDs();
											AppIDList = (appIDsObject as UInt32[]);

											return ERROR.NoError;
										}
										catch (Exception e)
										{
											if (e.Message != "" && e.Message.Contains("same number already exists"))
											{
												return ERROR.ItemAlreadyExistError;
											}
											else if (e.Message != "" && e.Message.Contains("status does not allow the requested command"))
											{
												return ERROR.AuthenticationError;
											}
											else
												return ERROR.IOError;
										}
									}
								}

								if (card.getCardType() == "DESFireEV1" ||
									card.getCardType() == "DESFireEV2")
								{
									//var cmd = (card as DESFireEV1Chip).getDESFireEV1Commands();
									var cmd = (card as DESFireChip).getDESFireCommands();
									try
									{
										object appIDsObject = cmd.getApplicationIDs();
										AppIDList = (appIDsObject as UInt32[]);

										var ev1Cmd = (card as DESFireEV1Chip).getDESFireEV1Commands();
										GenericChip.FreeMemory = ev1Cmd.getFreeMem();
										
										return ERROR.NoError;
									}
									catch
									{
										//Get AppIDs with Authentication (Directory Listing with PICC MK)
										try
										{
											cmd.selectApplication((uint)0);
											cmd.authenticate((byte)0, aiToUse.masterCardKey);

											object appIDsObject = cmd.getApplicationIDs();
											AppIDList = (appIDsObject as UInt32[]);


											var ev1Cmd = (card as DESFireEV1Chip).getDESFireEV1Commands();
											GenericChip.FreeMemory = ev1Cmd.getFreeMem();

											return ERROR.NoError;
										}
										catch (Exception e)
										{
											if (e.Message != "" && e.Message.Contains("same number already exists"))
											{
												return ERROR.ItemAlreadyExistError;
											}
											else if (e.Message != "" && e.Message.Contains("status does not allow the requested command"))
											{
												return ERROR.AuthenticationError;
											}
											else
												return ERROR.IOError;
										}
									}
								}
								else
									return ERROR.DeviceNotReadyError;
							}

							catch
							{
								try
								{
									if (card.getCardType() == "DESFire")
									{
										var cmd = card.getCommands() as DESFireCommands;

										cmd.selectApplication((uint)0);
										cmd.authenticate((byte)0, aiToUse.masterCardKey);

										object appIDsObject = cmd.getApplicationIDs();
										AppIDList = (appIDsObject as UInt32[]);

										return ERROR.NoError;
									}

									if (card.getCardType() == "DESFireEV1" ||
										card.getCardType() == "DESFireEV2")
									{
										var cmd = (card as DESFireEV1Chip).getDESFireEV1Commands();

									}
									else
										return ERROR.DeviceNotReadyError;
								}

								catch
								{

								}
							}
						}
					}
				}
				return ERROR.DeviceNotReadyError;
			}

			catch (Exception e)
			{
				if (e.Message != "" && e.Message.Contains("same number already exists"))
				{
					return ERROR.ItemAlreadyExistError;
				}
				else if (e.Message != "" && e.Message.Contains("status does not allow the requested command"))
				{
					return ERROR.AuthenticationError;
				}
				else
					return ERROR.IOError;
			}
		}

		public ERROR CreateMifareDesfireFile(string _appMasterKey, DESFireKeyType _keyTypeAppMasterKey, FileType_MifareDesfireFileType _fileType, DESFireAccessRights _accessRights, EncryptionMode _encMode,
		                                     int _appID, int _fileNo, int _fileSize,
		                                     int _minValue = 0, int _maxValue = 1000, int _initValue = 0, bool _isValueLimited = false,
		                                     int _maxNbOfRecords = 100)
		{
			try
			{
				DESFireAccessRights accessRights = _accessRights;

				// Keys to use for authentication
				DESFireAccessInfo aiToUse = new DESFireAccessInfo();
				CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_appMasterKey);
				aiToUse.masterCardKey.setCipherKey(CustomConverter.DesfireKeyToCheck);
				aiToUse.masterCardKey.setKeyType(_keyTypeAppMasterKey);

				if (readerUnit.connectToReader())
				{
					if (readerUnit.waitInsertion(Constants.MAX_WAIT_INSERTION))
					{
						if (readerUnit.connect())
						{
							ReaderUnitName = readerUnit.getConnectedName();

							card = readerUnit.getSingleChip();

							if (card.getCardType() == "DESFireEV1" ||
							    card.getCardType() == "DESFireEV2")
							{
								try
								{
                                    var cmd = (card as DESFireChip).getDESFireCommands();

                                    try
                                    {
                                        cmd.selectApplication((uint)_appID);
                                        cmd.authenticate((byte)0, aiToUse.masterCardKey);
                                    }
                                    catch
                                    {
                                        switch (_fileType)
                                        {
                                            case FileType_MifareDesfireFileType.StdDataFile:
                                                cmd.createStdDataFile((byte)_fileNo, _encMode, accessRights, (uint)_fileSize);
                                                break;

                                            case FileType_MifareDesfireFileType.BackupFile:
                                                cmd.createBackupFile((byte)_fileNo, _encMode, accessRights, (uint)_fileSize);
                                                break;

                                            case FileType_MifareDesfireFileType.ValueFile:
                                                cmd.createValueFile((byte)_fileNo, _encMode, accessRights, _minValue, _maxValue, _initValue, _isValueLimited);
                                                break;

                                            case FileType_MifareDesfireFileType.CyclicRecordFile:
                                                cmd.createCyclicRecordFile((byte)_fileNo, _encMode, accessRights, (uint)_fileSize, (uint)_maxNbOfRecords);
                                                break;

                                            case FileType_MifareDesfireFileType.LinearRecordFile:
                                                cmd.createLinearRecordFile((byte)_fileNo, _encMode, accessRights, (uint)_fileSize, (uint)_maxNbOfRecords);
                                                break;
                                        }

										return ERROR.NoError;
                                    }

									switch (_fileType)
									{
										case FileType_MifareDesfireFileType.StdDataFile:
											cmd.createStdDataFile((byte)_fileNo, _encMode, accessRights, (uint)_fileSize);
											break;

										case FileType_MifareDesfireFileType.BackupFile:
											cmd.createBackupFile((byte)_fileNo, _encMode, accessRights, (uint)_fileSize);
											break;

										case FileType_MifareDesfireFileType.ValueFile:
											cmd.createValueFile((byte)_fileNo, _encMode, accessRights, _minValue, _maxValue, _initValue, _isValueLimited);
											break;

										case FileType_MifareDesfireFileType.CyclicRecordFile:
											cmd.createCyclicRecordFile((byte)_fileNo, _encMode, accessRights, (uint)_fileSize, (uint)_maxNbOfRecords);
											break;

										case FileType_MifareDesfireFileType.LinearRecordFile:
											cmd.createLinearRecordFile((byte)_fileNo, _encMode, accessRights, (uint)_fileSize, (uint)_maxNbOfRecords);
											break;
									}


									return ERROR.NoError;
								}
								catch (Exception e)
								{
									if (e.Message != "" && e.Message.Contains("same number already exists"))
									{
										return ERROR.ItemAlreadyExistError;
									}
									else if (e.Message != "" && e.Message.Contains("status does not allow the requested command"))
									{
										return ERROR.AuthenticationError;
									}
									else if (e.Message != "" && e.Message.Contains("Insufficient NV-Memory"))
									{
										return ERROR.OutOfMemory;
									}
									else
										return ERROR.IOError;
								}
							}
							else if(card.getCardType() == "DESFire")
							{
								try
								{
									var cmd = card.getCommands() as DESFireCommands;

									try
									{
										cmd.selectApplication((uint)_appID);
										cmd.authenticate((byte)0, aiToUse.masterCardKey);
									}
									catch
									{
										switch (_fileType)
										{
											case FileType_MifareDesfireFileType.StdDataFile:
												cmd.createStdDataFile((byte)_fileNo, _encMode, accessRights, (uint)_fileSize);
												break;

											case FileType_MifareDesfireFileType.BackupFile:
												cmd.createBackupFile((byte)_fileNo, _encMode, accessRights, (uint)_fileSize);
												break;

											case FileType_MifareDesfireFileType.ValueFile:
												cmd.createValueFile((byte)_fileNo, _encMode, accessRights, _minValue, _maxValue, _initValue, _isValueLimited);
												break;

											case FileType_MifareDesfireFileType.CyclicRecordFile:
												cmd.createCyclicRecordFile((byte)_fileNo, _encMode, accessRights, (uint)_fileSize, (uint)_maxNbOfRecords);
												break;

											case FileType_MifareDesfireFileType.LinearRecordFile:
												cmd.createLinearRecordFile((byte)_fileNo, _encMode, accessRights, (uint)_fileSize, (uint)_maxNbOfRecords);
												break;
										}

										return ERROR.NoError;
									}

									switch (_fileType)
									{
										case FileType_MifareDesfireFileType.StdDataFile:
											cmd.createStdDataFile((byte)_fileNo, _encMode, accessRights, (uint)_fileSize);
											break;

										case FileType_MifareDesfireFileType.BackupFile:
											cmd.createBackupFile((byte)_fileNo, _encMode, accessRights, (uint)_fileSize);
											break;

										case FileType_MifareDesfireFileType.ValueFile:
											cmd.createValueFile((byte)_fileNo, _encMode, accessRights, _minValue, _maxValue, _initValue, _isValueLimited);
											break;

										case FileType_MifareDesfireFileType.CyclicRecordFile:
											cmd.createCyclicRecordFile((byte)_fileNo, _encMode, accessRights, (uint)_fileSize, (uint)_maxNbOfRecords);
											break;

										case FileType_MifareDesfireFileType.LinearRecordFile:
											cmd.createLinearRecordFile((byte)_fileNo, _encMode, accessRights, (uint)_fileSize, (uint)_maxNbOfRecords);
											break;
									}


									return ERROR.NoError;
								}
								catch (Exception e)
								{
									if (e.Message != "" && e.Message.Contains("same number already exists"))
									{
										return ERROR.ItemAlreadyExistError;
									}
									else if (e.Message != "" && e.Message.Contains("status does not allow the requested command"))
									{
										return ERROR.AuthenticationError;
									}
									else if (e.Message != "" && e.Message.Contains("Insufficient NV-Memory"))
									{
										return ERROR.OutOfMemory;
									}
									else
										return ERROR.IOError;
								}
							}
							else
								return ERROR.DeviceNotReadyError;
						}
					}
				}
				return ERROR.DeviceNotReadyError;
			}
			catch
			{
				return ERROR.IOError;
			}
		}

		public ERROR ReadMiFareDESFireChipFile(string _appMasterKey, DESFireKeyType _keyTypeAppMasterKey,
		                                       string _appReadKey, DESFireKeyType _keyTypeAppReadKey, int _readKeyNo,
		                                       string _appWriteKey, DESFireKeyType _keyTypeAppWriteKey, int _writeKeyNo,
		                                       EncryptionMode _encMode,
		                                       int _fileNo, int _appID, int _fileSize)
		{
            try
            {
				// The excepted memory tree
				DESFireLocation location = new DESFireLocation();
				// The Application ID to use
				location.aid = (uint)_appID;
				// File 0 into this application
				location.file = (byte)_fileNo;
				// File communication requires encryption
				location.securityLevel = _encMode;

				// Keys to use for authentication

				// Get the card storage service
				StorageCardService storage = (StorageCardService)card.getService(CardServiceType.CST_STORAGE);

				// Change keys with the following ones
				DESFireAccessInfo aiToWrite = new DESFireAccessInfo();
				CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_appMasterKey);
				aiToWrite.masterApplicationKey.setCipherKey(CustomConverter.DesfireKeyToCheck);
				aiToWrite.masterApplicationKey.setKeyType((DESFireKeyType)_keyTypeAppMasterKey);

				CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_appReadKey);
				aiToWrite.readKey.setCipherKey(CustomConverter.DesfireKeyToCheck);
				aiToWrite.readKey.setKeyType((DESFireKeyType)_keyTypeAppReadKey);
				aiToWrite.readKeyno = (byte)_readKeyNo;

				CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_appWriteKey);
				aiToWrite.writeKey.setCipherKey(CustomConverter.DesfireKeyToCheck);
				aiToWrite.writeKey.setKeyType((DESFireKeyType)_keyTypeAppWriteKey);
				aiToWrite.writeKeyno = (byte)_writeKeyNo;

				if (readerUnit.connectToReader())
				{
					if (readerUnit.waitInsertion(Constants.MAX_WAIT_INSERTION))
					{
						if (readerUnit.connect())
						{
							if (card.getCardType() == "DESFire" ||
								card.getCardType() == "DESFireEV1" ||
								card.getCardType() == "DESFireEV2")
							{
								try
								{
									var cmd = card.getCommands() as DESFireCommands;

									try
									{
										cmd.selectApplication((uint)_appID);

										cmd.authenticate((byte)_readKeyNo, aiToWrite.readKey);

										MifareDESFireData = (byte[])cmd.readData((byte)_fileNo, 0, (uint)_fileSize, EncryptionMode.CM_ENCRYPT).ToArray();
									}
									catch
									{
										cmd.selectApplication((uint)_appID);

										MifareDESFireData = (byte[])cmd.readData((byte)_fileNo, 0, (uint)_fileSize, EncryptionMode.CM_ENCRYPT).ToArray();
									}

									return ERROR.NoError;
								}

								catch (Exception e)
								{
									if (e.Message != "" && e.Message.Contains("same number already exists"))
									{
										return ERROR.ItemAlreadyExistError;
									}
									else if (e.Message != "" && e.Message.Contains("status does not allow the requested command"))
									{
										return ERROR.AuthenticationError;
									}
									else
										return ERROR.IOError;
								}
							}
							else
								return ERROR.DeviceNotReadyError;
						}
					}
				}
				return ERROR.DeviceNotReadyError;
			}

			catch
            {
				return ERROR.IOError;
			}
		}
		
		public ERROR WriteMiFareDESFireChipFile(string _cardMasterKey, DESFireKeyType _keyTypeCardMasterKey,
		                                        string _appMasterKey, DESFireKeyType _keyTypeAppMasterKey,
		                                        string _appReadKey, DESFireKeyType _keyTypeAppReadKey, int _readKeyNo,
		                                        string _appWriteKey, DESFireKeyType _keyTypeAppWriteKey, int _writeKeyNo,
		                                        EncryptionMode _encMode,
		                                        int _fileNo, int _appID, byte[] _data)
		{

            try
            {
				// The excepted memory tree
				DESFireLocation location = new DESFireLocation();
				// The Application ID to use
				location.aid = (byte)_appID;
				// File 0 into this application
				location.file = (byte)_fileNo;
				// File communication requires encryption
				location.securityLevel = _encMode;

				// Keys to use for authentication

				// Get the card storage service
				StorageCardService storage = (StorageCardService)card.getService(CardServiceType.CST_STORAGE);

				// Change keys with the following ones
				DESFireAccessInfo aiToUse = new DESFireAccessInfo();
				CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_cardMasterKey);
				aiToUse.masterCardKey.setCipherKey(CustomConverter.DesfireKeyToCheck);
				aiToUse.masterCardKey.setKeyType(_keyTypeAppMasterKey);

				DESFireAccessInfo aiToWrite = new DESFireAccessInfo();
				aiToWrite.masterCardKey.setCipherKey(CustomConverter.DesfireKeyToCheck);
				aiToWrite.masterCardKey.setKeyType(_keyTypeAppMasterKey);

				CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_appMasterKey);
				aiToWrite.masterApplicationKey.setCipherKey(CustomConverter.DesfireKeyToCheck);
				aiToWrite.masterApplicationKey.setKeyType(_keyTypeAppMasterKey);

				CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_appReadKey);
				aiToWrite.readKey.setCipherKey(CustomConverter.DesfireKeyToCheck);
				aiToWrite.readKey.setKeyType(_keyTypeAppReadKey);
				aiToWrite.readKeyno = (byte)_readKeyNo;

				CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_appWriteKey);
				aiToWrite.writeKey.setCipherKey(CustomConverter.DesfireKeyToCheck);
				aiToWrite.writeKey.setKeyType(_keyTypeAppWriteKey);
				aiToWrite.writeKeyno = (byte)_writeKeyNo;

				if (readerUnit.connectToReader())
				{
					if (readerUnit.waitInsertion(Constants.MAX_WAIT_INSERTION))
					{
						if (readerUnit.connect())
						{
							if (card.getCardType() == "DESFire" ||
								card.getCardType() == "DESFireEV1" ||
								card.getCardType() == "DESFireEV2")
							{
								try
								{
									var cmd = card.getCommands() as DESFireCommands;

									cmd.selectApplication((uint)_appID);

									cmd.authenticate((byte)_writeKeyNo, aiToWrite.writeKey);

									cmd.writeData((byte)_fileNo, 0, new ByteVector(_data), EncryptionMode.CM_ENCRYPT);

									return ERROR.NoError;
								}

								catch (Exception e)
								{
									if (e.Message != "" && e.Message.Contains("same number already exists"))
									{
										return ERROR.ItemAlreadyExistError;
									}
									else if (e.Message != "" && e.Message.Contains("status does not allow the requested command"))
									{
										return ERROR.AuthenticationError;
									}
									else
										return ERROR.IOError;
								}
							}
							else
								return ERROR.DeviceNotReadyError;
						}
					}
				}
				return ERROR.DeviceNotReadyError;
			}

			catch
			{
				return ERROR.IOError;
			}
		}
		
		public ERROR AuthToMifareDesfireApplication(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumber, int _appID = 0)
		{
			try
			{
				// The excepted memory tree
				DESFireLocation location = new DESFireLocation();
				// The Application ID to use
				location.aid = (byte)_appID;
				// File communication requires encryption
				location.securityLevel = EncryptionMode.CM_ENCRYPT;

				// Keys to use for authentication
				DESFireAccessInfo aiToUse = new DESFireAccessInfo();
				CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_applicationMasterKey);
				aiToUse.masterCardKey.setCipherKey(CustomConverter.DesfireKeyToCheck);
				aiToUse.masterCardKey.setKeyType((DESFireKeyType)_keyType);

				if (readerUnit.connectToReader())
				{
					if (readerUnit.waitInsertion(Constants.MAX_WAIT_INSERTION))
					{
						if (readerUnit.connect())
						{
							ReaderUnitName = readerUnit.getConnectedName();

							card = readerUnit.getSingleChip();

							if (card.getCardType() == "DESFire" ||
								card.getCardType() == "DESFireEV1" ||
								card.getCardType() == "DESFireEV2")
							{
								var cmd = card.getCommands() as DESFireCommands;
								try
								{
									cmd.selectApplication((uint)_appID);
									if (_appID > 0)
										cmd.authenticate((byte)_keyNumber, aiToUse.masterCardKey);
									else
										cmd.authenticate((byte)0, aiToUse.masterCardKey);
									return ERROR.NoError;
								}

								catch (Exception e)
								{
									if (e.Message != "" && e.Message.Contains("same number already exists"))
									{
										return ERROR.ItemAlreadyExistError;
									}
									else if (e.Message != "" && e.Message.Contains("status does not allow the requested command"))
									{
										return ERROR.AuthenticationError;
									}
									else
										return ERROR.IOError;
								}
							}

							else
								return ERROR.DeviceNotReadyError;
						}
					}
				}
				return ERROR.DeviceNotReadyError;
			}
			catch
			{
				return ERROR.IOError;
			}
		}

		public ERROR GetMifareDesfireAppSettings(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumberCurrent = 0, int _appID = 0)
		{
			byte maxNbrOfKeys;
			DESFireKeySettings keySettings;

			try
			{
				// Keys to use for authentication
				DESFireAccessInfo aiToUse = new DESFireAccessInfo();
				CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_applicationMasterKey);
				aiToUse.masterCardKey.setCipherKey(CustomConverter.DesfireKeyToCheck);
				aiToUse.masterCardKey.setKeyType((DESFireKeyType)_keyType);

				if (readerUnit.connectToReader())
				{
					if (readerUnit.waitInsertion(Constants.MAX_WAIT_INSERTION))
					{
						if (readerUnit.connect())
						{
							ReaderUnitName = readerUnit.getConnectedName();
							card = readerUnit.getSingleChip();

							if (card.getCardType() == "DESFire")
							{
								var cmd = card.getCommands() as DESFireCommands;
                                ReaderUnitName = readerUnit.getConnectedName();
                                
                                try
								{
									cmd.selectApplication((uint)_appID);
                                    GenericChip = new GenericChipModel(ByteConverter.HexToString(card.getChipIdentifier().ToArray()), (CARD_TYPE)Enum.Parse(typeof(CARD_TYPE), card.getCardType()));

                                    try
									{
										cmd.authenticate((byte)_keyNumberCurrent, aiToUse.masterCardKey);
									}

									catch
									{
										try
										{
											cmd.getKeySettings(out keySettings, out maxNbrOfKeys);
											MaxNumberOfAppKeys = (byte)(maxNbrOfKeys & 0x0F);
											EncryptionType = (byte)(maxNbrOfKeys & 0xF0);
											DesfireAppKeySetting = keySettings;

											return ERROR.NoError;
										}
										catch (Exception e)
										{
											if (e.Message != "" && e.Message.Contains("same number already exists"))
											{
												return ERROR.ItemAlreadyExistError;
											}
											else if (e.Message != "" && e.Message.Contains("status does not allow the requested command"))
											{
												return ERROR.AuthenticationError;
											}
											else
												return ERROR.IOError;
										}
									}
									cmd.getKeySettings(out keySettings, out maxNbrOfKeys);
									MaxNumberOfAppKeys = (byte)(maxNbrOfKeys & 0x0F);
									EncryptionType = (byte)(maxNbrOfKeys & 0xF0);
									DesfireAppKeySetting = keySettings;

									return ERROR.NoError;
								}
								catch (Exception e)
								{
									if (e.Message != "" && e.Message.Contains("same number already exists"))
									{
										return ERROR.ItemAlreadyExistError;
									}
									else if (e.Message != "" && e.Message.Contains("status does not allow the requested command"))
									{
										return ERROR.AuthenticationError;
									}
									else
										return ERROR.IOError;
								}
							}

							else if (card.getCardType() == "DESFireEV1" ||
									card.getCardType() == "DESFireEV2" || card.getCardType() == "GENERIC_T_CL_A")
							{
								var cmd = (card as DESFireChip).getDESFireCommands();
								var ev1Cmd = (card as DESFireEV1Chip).getDESFireEV1Commands();

								ReaderUnitName = readerUnit.getConnectedName();
								
								try
								{
									cmd.selectApplication((uint)_appID);
									GenericChip = new GenericChipModel(ByteConverter.HexToString(card.getChipIdentifier().ToArray()), (CARD_TYPE)Enum.Parse(typeof(CARD_TYPE), card.getCardType()));

									try
									{
										cmd.authenticate((byte)_keyNumberCurrent, aiToUse.masterCardKey);
									}

									catch
									{
										try
										{
											try
											{
												GenericChip.FreeMemory = ev1Cmd.getFreeMem();
											}

											catch { }

											cmd.getKeySettings(out keySettings, out maxNbrOfKeys);
											MaxNumberOfAppKeys = (byte)(maxNbrOfKeys & 0x0F);
											EncryptionType = (byte)(maxNbrOfKeys & 0xF0);
											DesfireAppKeySetting = keySettings;

											return ERROR.NoError;
										}
										catch (Exception e)
										{
											if (e.Message != "" && e.Message.Contains("same number already exists"))
											{
												return ERROR.ItemAlreadyExistError;
											}
											else if (e.Message != "" && e.Message.Contains("status does not allow the requested command"))
											{
												return ERROR.NotAllowed;
											}
											else
												return ERROR.IOError;
										}
									}

									try
									{
										GenericChip.FreeMemory = ev1Cmd.getFreeMem();
									}

									catch { }

									cmd.getKeySettings(out keySettings, out maxNbrOfKeys);
									MaxNumberOfAppKeys = (byte)(maxNbrOfKeys & 0x0F);
									EncryptionType = (byte)(maxNbrOfKeys & 0xF0);
									DesfireAppKeySetting = keySettings;

									return ERROR.NoError;
								}
								catch (Exception e)
								{
									if (e.Message != "" && e.Message.Contains("same number already exists"))
									{
										return ERROR.ItemAlreadyExistError;
									}
									else if (e.Message != "" && e.Message.Contains("status does not allow the requested command"))
									{
										return ERROR.AuthenticationError;
									}
									else
										return ERROR.IOError;
								}
							}

							else
								return ERROR.DeviceNotReadyError;
						}
					}
				}
				return ERROR.DeviceNotReadyError;
			}
			catch
			{
				return ERROR.IOError;
			}
		}

		public ERROR CreateMifareDesfireApplication(string _piccMasterKey, DESFireKeySettings _keySettingsTarget, DESFireKeyType _keyTypePiccMasterKey, DESFireKeyType _keyTypeTargetApplication, int _maxNbKeys, int _appID, bool authenticateToPICCFirst = true)
		{
			try
			{
				// The excepted memory tree
				DESFireLocation location = new DESFireLocation();
				// The Application ID to use
				location.aid = (byte)_appID;

				// File communication requires encryption
				location.securityLevel = EncryptionMode.CM_ENCRYPT;

				// IDESFireEV1Commands cmd;
				// Keys to use for authentication
				DESFireAccessInfo aiToUse = new DESFireAccessInfo();
				CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_piccMasterKey);
				aiToUse.masterCardKey.setCipherKey(CustomConverter.DesfireKeyToCheck);
				aiToUse.masterCardKey.setKeyType((DESFireKeyType)_keyTypePiccMasterKey);

				if (readerUnit.connectToReader())
				{
					if (readerUnit.waitInsertion(Constants.MAX_WAIT_INSERTION))
					{
						if (readerUnit.connect())
						{
							ReaderUnitName = readerUnit.getConnectedName();

							card = readerUnit.getSingleChip();

							if (card.getCardType() == "DESFire")
							{
								var cmd = card.getCommands() as DESFireCommands;
								try
								{
									cmd.selectApplication(0);

									if(authenticateToPICCFirst)
										cmd.authenticate((byte)0, aiToUse.masterCardKey);
									
									cmd.createApplication((uint)_appID, _keySettingsTarget, (byte)_maxNbKeys);

									return ERROR.NoError;
								}
								catch (Exception e)
								{
									if (e.Message != "" && e.Message.Contains("same number already exists"))
									{
										return ERROR.ItemAlreadyExistError;
									}
									else if (e.Message != "" && e.Message.Contains("status does not allow the requested command"))
									{
										return ERROR.AuthenticationError;
									}
									else
										return ERROR.IOError;
								}
							}

							else if(card.getCardType() == "DESFireEV1" ||
									card.getCardType() == "DESFireEV2")
                            {
								var cmd = card.getCommands() as DESFireCommands;
								var ev1Cmd = (card as DESFireEV1Chip).getDESFireEV1Commands();
								try
								{
									cmd.selectApplication(0);

									if (authenticateToPICCFirst)
										cmd.authenticate((byte)0, aiToUse.masterCardKey);

									ev1Cmd.createApplication((uint)_appID, _keySettingsTarget, (byte)_maxNbKeys, (DESFireKeyType)_keyTypeTargetApplication);

									return ERROR.NoError;
								}
								catch (Exception e)
								{
									if (e.Message != "" && e.Message.Contains("same number already exists"))
									{
										return ERROR.ItemAlreadyExistError;
									}
									else if (e.Message != "" && e.Message.Contains("status does not allow the requested command"))
									{
										return ERROR.AuthenticationError;
									}
									else if (e.Message != "" && e.Message.Contains("Insufficient NV-Memory"))
									{
										return ERROR.OutOfMemory;
									}
									else
										return ERROR.IOError;
								}
							}
							else
								return ERROR.DeviceNotReadyError;
						}
					}
				}
				return ERROR.DeviceNotReadyError;
			}
			catch
			{
				return ERROR.IOError;
			}
		}

		public ERROR ChangeMifareDesfireApplicationKey(string _applicationMasterKeyCurrent, int _keyNumberCurrent, DESFireKeyType _keyTypeCurrent, string _applicationMasterKeyTarget, int _keyNumberTarget, DESFireKeyType _keyTypeTarget, int _appIDCurrent = 0, int _appIDTarget = 0, DESFireKeySettings keySettings = (DESFireKeySettings.KS_DEFAULT | DESFireKeySettings.KS_FREE_CREATE_DELETE_WITHOUT_MK))
		{
			try
			{
				// The excepted memory tree
				DESFireLocation location = new DESFireLocation();
				// The Application ID to use
				location.aid = (byte)_appIDCurrent;
				// File communication requires encryption
				location.securityLevel = EncryptionMode.CM_ENCRYPT;

				DESFireCommands cmd;

				// Keys to use for authentication
				DESFireAccessInfo aiToUse = new DESFireAccessInfo();
				if (_appIDCurrent > 0)
				{
					CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_applicationMasterKeyCurrent);
					aiToUse.masterApplicationKey.setCipherKey(CustomConverter.DesfireKeyToCheck);
					aiToUse.masterApplicationKey.setKeyType((DESFireKeyType)_keyTypeCurrent);
				}
				else
				{
					CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_applicationMasterKeyCurrent);
					aiToUse.masterCardKey.setCipherKey(CustomConverter.DesfireKeyToCheck);
					aiToUse.masterCardKey.setKeyType((DESFireKeyType)_keyTypeCurrent);
				}

				DESFireKey applicationMasterKeyTarget = new DESFireKey();
				applicationMasterKeyTarget.setKeyType((DESFireKeyType)_keyTypeTarget);
				
				CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_applicationMasterKeyTarget);
				applicationMasterKeyTarget.setCipherKey(CustomConverter.DesfireKeyToCheck);

				if (readerUnit.connectToReader())
				{
					if (readerUnit.waitInsertion(Constants.MAX_WAIT_INSERTION))
					{
						if (readerUnit.connect())
						{
							ReaderUnitName = readerUnit.getConnectedName();
							//readerSerialNumber = readerUnit.GetReaderSerialNumber();

							card = readerUnit.getSingleChip();

							if (card.getCardType() == "DESFire" ||
							    card.getCardType() == "DESFireEV1" ||
							    card.getCardType() == "DESFireEV2")
							{
								cmd = card.getCommands() as DESFireCommands;
								try
								{
									cmd.selectApplication((uint)_appIDCurrent);

									if (_appIDCurrent == 0 && _appIDTarget == 0)
									{
                                        try
                                        {
                                            cmd.authenticate((byte)0, aiToUse.masterCardKey);
                                            cmd.changeKey((byte)0, applicationMasterKeyTarget);
                                            cmd.authenticate((byte)0, applicationMasterKeyTarget);
                                            cmd.changeKeySettings(keySettings);
                                        }

                                        catch
                                        {
                                            try
                                            {
                                                cmd.authenticate((byte)0, aiToUse.masterCardKey);
                                                cmd.changeKeySettings(keySettings);
                                                cmd.authenticate((byte)0, aiToUse.masterCardKey);
                                                cmd.changeKey((byte)0, applicationMasterKeyTarget);
                                                return ERROR.NoError;
                                            }

                                            catch (Exception e)
                                            {
												if (e.Message != "" && e.Message.Contains("same number already exists"))
												{
													return ERROR.ItemAlreadyExistError;
												}
												else if (e.Message != "" && e.Message.Contains("status does not allow the requested command"))
                                                {
                                                    return ERROR.AuthenticationError;
                                                }
                                                else
                                                    return ERROR.IOError;
                                            }
                                        }
									}
									else if (_appIDCurrent == 0 && _appIDTarget > 0)
									{
										cmd.authenticate((byte)0, aiToUse.masterCardKey);
										cmd.selectApplication((uint)_appIDTarget);
										cmd.authenticate((byte)_keyNumberCurrent, aiToUse.masterCardKey);
										cmd.changeKey((byte)_keyNumberTarget, applicationMasterKeyTarget);
									}
									else
									{
                                        try
                                        {
                                            cmd.authenticate((byte)_keyNumberCurrent, aiToUse.masterApplicationKey);
                                            cmd.changeKey((byte)_keyNumberTarget, applicationMasterKeyTarget);
                                            cmd.authenticate((byte)_keyNumberCurrent, applicationMasterKeyTarget);

											try
                                            {
												cmd.changeKeySettings(keySettings);
											}
                                            catch { }
                                        }

                                        catch
                                        {
                                            try
                                            {
                                                cmd.authenticate((byte)_keyNumberCurrent, aiToUse.masterApplicationKey);
                                                cmd.changeKeySettings(keySettings);
                                                cmd.authenticate((byte)_keyNumberCurrent, aiToUse.masterApplicationKey);
                                                cmd.changeKey((byte)_keyNumberTarget, applicationMasterKeyTarget);
                                                return ERROR.NoError;
                                            }

											catch
                                            {
												try
												{
													cmd.authenticate((byte)_keyNumberCurrent, aiToUse.masterApplicationKey);
													cmd.changeKey((byte)_keyNumberTarget, applicationMasterKeyTarget);
												}
												catch (Exception e)
												{
													if (e.Message != "" && e.Message.Contains("same number already exists"))
													{
														return ERROR.ItemAlreadyExistError;
													}
													else if (e.Message != "" && e.Message.Contains("status does not allow the requested command"))
													{
														return ERROR.AuthenticationError;
													}
													else
														return ERROR.IOError;
												}
											}
                                        }
									}

									return ERROR.NoError;
								}
								catch (Exception e)
								{
									if (e.Message != "" && e.Message.Contains("same number already exists"))
									{
										return ERROR.ItemAlreadyExistError;
									}
									else if (e.Message != "" && e.Message.Contains("status does not allow the requested command"))
									{
										return ERROR.AuthenticationError;
									}
									else
										return ERROR.IOError;
								}
							}
							else
								return ERROR.DeviceNotReadyError;
						}
					}
				}
				return ERROR.DeviceNotReadyError;
			}
			catch
			{
				return ERROR.IOError;
			}
		}

		public ERROR DeleteMifareDesfireApplication(string _applicationMasterKey, DESFireKeyType _keyType, int _appID = 0)
		{
			try
			{
				// The excepted memory tree
				DESFireLocation location = new DESFireLocation();
				// The Application ID to use
				location.aid = (byte)_appID;
				// File communication requires encryption
				location.securityLevel = EncryptionMode.CM_ENCRYPT;

				// IDESFireEV1Commands cmd;
				// Keys to use for authentication
				DESFireAccessInfo aiToUse = new DESFireAccessInfo();
				CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_applicationMasterKey);
				aiToUse.masterCardKey.setCipherKey(CustomConverter.DesfireKeyToCheck);
				aiToUse.masterCardKey.setKeyType(_keyType);

				if (readerUnit.connectToReader())
				{
					if (readerUnit.waitInsertion(Constants.MAX_WAIT_INSERTION))
					{
						if (readerUnit.connect())
						{
							ReaderUnitName = readerUnit.getConnectedName();
							//readerSerialNumber = readerUnit.GetReaderSerialNumber();

							card = readerUnit.getSingleChip();

							if (card.getCardType() == "DESFire" ||
							    card.getCardType() == "DESFireEV1" ||
							    card.getCardType() == "DESFireEV2")
							{
								var cmd = card.getCommands() as DESFireCommands;
								try
								{
									cmd.selectApplication(0);
									cmd.authenticate((byte)0, aiToUse.masterCardKey);

									cmd.deleteApplication((uint)_appID);
									return ERROR.NoError;
								}
								catch
								{
									try
                                    {
										cmd.selectApplication((uint)_appID);
										cmd.authenticate((byte)0, aiToUse.masterCardKey);
										cmd.deleteApplication((uint)_appID);
										return ERROR.NoError;
									}

									catch (Exception e)
									{
										if (e.Message != "" && e.Message.Contains("same number already exists"))
										{
											return ERROR.ItemAlreadyExistError;
										}
										else if (e.Message != "" && e.Message.Contains("status does not allow the requested command"))
										{
											return ERROR.AuthenticationError;
										}
										else
											return ERROR.IOError;
									}		
								}
							}
							return ERROR.DeviceNotReadyError;
						}
					}
				}
				return ERROR.DeviceNotReadyError;
			}
			catch
			{
				return ERROR.IOError;
			}
		}

		public ERROR DeleteMifareDesfireFile(string _applicationMasterKey, DESFireKeyType _keyType, int _appID = 0, int _fileID = 0)
		{
			try
			{
				// The excepted memory tree
				DESFireLocation location = new DESFireLocation();
				// The Application ID to use
				location.aid = (byte)_appID;
				// File communication requires encryption
				location.securityLevel = EncryptionMode.CM_ENCRYPT;

				// Keys to use for authentication
				DESFireAccessInfo aiToUse = new DESFireAccessInfo();
				CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_applicationMasterKey);
				aiToUse.masterCardKey.setCipherKey(CustomConverter.DesfireKeyToCheck);
				aiToUse.masterCardKey.setKeyType((DESFireKeyType)_keyType);

				if (readerUnit.connectToReader())
				{
					if (readerUnit.waitInsertion(Constants.MAX_WAIT_INSERTION))
					{
						if (readerUnit.connect())
						{
							ReaderUnitName = readerUnit.getConnectedName();
							//readerSerialNumber = readerUnit.GetReaderSerialNumber();

							card = readerUnit.getSingleChip();

							if (card.getCardType() == "DESFireEV1" ||
							    card.getCardType() == "DESFireEV2" ||
							    card.getCardType() == "DESFire")
							{
								try
								{
                                    var cmd = card.getCommands() as DESFireCommands;

                                    try
                                    {
                                        cmd.selectApplication((uint)_appID);
                                        cmd.authenticate((byte)0, aiToUse.masterCardKey);
                                    }

                                    catch
                                    {
                                        cmd.deleteFile((byte)_fileID);
                                        return ERROR.NoError;
                                    }

                                    cmd.deleteFile((byte)_fileID);
                                    return ERROR.NoError;
								}
								catch (Exception e)
								{
									if (e.Message != "" && e.Message.Contains("same number already exists"))
									{
										return ERROR.ItemAlreadyExistError;
									}
									else if (e.Message != "" && e.Message.Contains("status does not allow the requested command"))
									{
										return ERROR.AuthenticationError;
									}
									else
										return ERROR.IOError;
								}
							}
							else
								return ERROR.DeviceNotReadyError;
						}
					}
				}
				return ERROR.DeviceNotReadyError;
			}
			catch
			{
				return ERROR.IOError;
			}
		}

		public ERROR FormatDesfireCard(string _applicationMasterKey, DESFireKeyType _keyType, int _appID = 0)
		{
			try
			{
				// The excepted memory tree
				DESFireLocation location = new DESFireLocation();
				// The Application ID to use
				location.aid = (byte)_appID;
				// File communication requires encryption
				location.securityLevel = EncryptionMode.CM_ENCRYPT;

				// Keys to use for authentication
				DESFireAccessInfo aiToUse = new DESFireAccessInfo();
				CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_applicationMasterKey);
				aiToUse.masterCardKey.setCipherKey(CustomConverter.DesfireKeyToCheck);
				aiToUse.masterCardKey.setKeyType((DESFireKeyType)_keyType);

				if (readerUnit.connectToReader())
				{
					if (readerUnit.waitInsertion(Constants.MAX_WAIT_INSERTION))
					{
						if (readerUnit.connect())
						{
							ReaderUnitName = readerUnit.getConnectedName();
							//readerSerialNumber = readerUnit.GetReaderSerialNumber();

							card = readerUnit.getSingleChip();

							if(card.getCardType() == "DESFire" ||
							   card.getCardType() == "DESFireEV1" ||
							   card.getCardType() == "DESFireEV2")
							{
								var cmd = card.getCommands() as DESFireCommands;
								try
								{
									cmd.selectApplication(0);
									cmd.authenticate((byte)0, aiToUse.masterCardKey);

									cmd.erase();

									return ERROR.NoError;
								}
								catch (Exception e)
								{
									if (e.Message != "" && e.Message.Contains("same number already exists"))
									{
										return ERROR.ItemAlreadyExistError;
									}
									else if (e.Message != "" && e.Message.Contains("status does not allow the requested command"))
									{
										return ERROR.AuthenticationError;
									}
									else
										return ERROR.IOError;
								}
							}
							else
								return ERROR.DeviceNotReadyError;
						}
					}
				}
				return ERROR.DeviceNotReadyError;
			}
			catch
			{
				return ERROR.IOError;
			}
		}

		public ERROR GetMifareDesfireFileList(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumberCurrent = 0, int _appID = 0)
		{
			try
			{
				// The excepted memory tree
				DESFireLocation location = new DESFireLocation();
				// The Application ID to use
				location.aid = (byte)_appID;
				// File communication requires encryption
				location.securityLevel = EncryptionMode.CM_ENCRYPT;

				//IDESFireEV1Commands cmd;
				// Keys to use for authentication
				DESFireAccessInfo aiToUse = new DESFireAccessInfo();
				CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_applicationMasterKey);
				aiToUse.masterCardKey.setCipherKey(CustomConverter.DesfireKeyToCheck);
				aiToUse.masterCardKey.setKeyType(_keyType);

				object fileIDsObject;

				if (readerUnit.connectToReader())
				{
					if (readerUnit.waitInsertion(Constants.MAX_WAIT_INSERTION))
					{
						if (readerUnit.connect())
						{
							ReaderUnitName = readerUnit.getConnectedName();
							//readerSerialNumber = readerUnit.GetReaderSerialNumber();

							card = readerUnit.getSingleChip();

							if(card.getCardType() == "DESFire" ||
							   card.getCardType() == "DESFireEV1" ||
							   card.getCardType() == "DESFireEV2")
							{
								var cmd = card.getCommands() as DESFireCommands;
								try
								{
									cmd.selectApplication((uint)_appID);
									try
									{
										cmd.authenticate((byte)_keyNumberCurrent, aiToUse.masterCardKey);
									}
									catch
									{
										try
										{
											fileIDsObject = cmd.getFileIDs();
											FileIDList = (fileIDsObject as byte[]);
											return ERROR.NoError;
										}
										catch (Exception e)
										{
											if (e.Message != "" && e.Message.Contains("same number already exists"))
											{
												return ERROR.ItemAlreadyExistError;
											}
											else if (e.Message != "" && e.Message.Contains("status does not allow the requested command"))
											{
												return ERROR.AuthenticationError;
											}
											else
												return ERROR.IOError;
										}
									}

									fileIDsObject = cmd.getFileIDs();
									FileIDList = (fileIDsObject as byte[]);

									return ERROR.NoError;
								}
								catch (Exception e)
								{
									if (e.Message != "" && e.Message.Contains("same number already exists"))
									{
										return ERROR.ItemAlreadyExistError;
									}
									else if (e.Message != "" && e.Message.Contains("status does not allow the requested command"))
									{
										return ERROR.AuthenticationError;
									}
									else
										return ERROR.IOError;
								}
							}
							else
								return ERROR.DeviceNotReadyError;
						}
					}
				}
				return ERROR.DeviceNotReadyError;
			}
			catch
			{
				return ERROR.IOError;
			}
		}

		public ERROR GetMifareDesfireFileSettings(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumberCurrent = 0, int _appID = 0, int _fileNo = 0)
		{
			try
			{
				// IDESFireEV1Commands cmd;
				// Keys to use for authentication
				DESFireAccessInfo aiToUse = new DESFireAccessInfo();
				CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_applicationMasterKey);
				aiToUse.masterCardKey.setCipherKey(CustomConverter.DesfireKeyToCheck);
				aiToUse.masterCardKey.setKeyType((DESFireKeyType)_keyType);

				if (readerUnit.connectToReader())
				{
					if (readerUnit.waitInsertion(Constants.MAX_WAIT_INSERTION))
					{
						if (readerUnit.connect())
						{
							ReaderUnitName = readerUnit.getConnectedName();
							card = readerUnit.getSingleChip();

							if (card.getCardType() == "DESFire" ||
							    card.getCardType() == "DESFireEV1" ||
							    card.getCardType() == "DESFireEV2")
							{
								var cmd = card.getCommands() as DESFireCommands;
								try
								{
									cmd.selectApplication((uint)_appID);
									try
									{
										cmd.authenticate((byte)_keyNumberCurrent, aiToUse.masterCardKey);
									}
									catch
									{
										try
										{
											DesfireFileSettings = cmd.getFileSettings((byte)_fileNo);

											return ERROR.NoError;
										}
										catch (Exception e)
										{
											if (e.Message != "" && e.Message.Contains("same number already exists"))
											{
												return ERROR.ItemAlreadyExistError;
											}
											else if (e.Message != "" && e.Message.Contains("status does not allow the requested command"))
											{
												return ERROR.AuthenticationError;
											}
											else
												return ERROR.IOError;
										}
									}
									DesfireFileSettings = cmd.getFileSettings((byte)_fileNo);
									
									return ERROR.NoError;
								}
								catch (Exception e)
								{
									if (e.Message != "" && e.Message.Contains("same number already exists"))
									{
										return ERROR.ItemAlreadyExistError;
									}
									else if (e.Message != "" && e.Message.Contains("status does not allow the requested command"))
									{
										return ERROR.AuthenticationError;
									}
									else
										return ERROR.IOError;
								}
							}
							else
								return ERROR.DeviceNotReadyError;
						}
					}
				}
				return ERROR.DeviceNotReadyError;
			}
			catch
			{
				return ERROR.IOError;
			}
		}

		#endregion mifare desfire
		
		#region mifare plus
		
		#endregion
		
		#region ISO15693
		
		public ERROR ReadISO15693Chip()
		{
			try
			{
				if (readerUnit.connectToReader())
				{
					if (readerUnit.waitInsertion(Constants.MAX_WAIT_INSERTION))
					{
						if (readerUnit.connect())
						{
							ReaderUnitName = readerUnit.getConnectedName();
							//readerSerialNumber = readerUnit.GetReaderSerialNumber();

							card = readerUnit.getSingleChip();

							
							if (card.getCardType() == "ISO15693")
							{
								var cmd = card.getCommands() as ISO15693Commands;// IMifareUltralightCommands;

								object t = cmd.getSystemInformation();
								//object res = cmd.ReadPage(4);

								//appIDs = (appIDsObject as UInt32[]);
							}

							return ERROR.NoError;
						}
					}
				}
				return ERROR.NoError;
			}
			catch (Exception e)
			{
				LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""));
				return ERROR.IOError;
			}
		}

        #endregion

        #region EM4135

        public ERROR ReadEM4135ChipPublic()
        {
            try
            {
                if (readerUnit.connectToReader())
                {
                    if (readerUnit.waitInsertion(Constants.MAX_WAIT_INSERTION))
                    {
                        if (readerUnit.connect())
                        {
                            ReaderUnitName = readerUnit.getConnectedName();
                            //readerSerialNumber = readerUnit.GetReaderSerialNumber();

                            card = readerUnit.getSingleChip();


                            if (true) //card.getCardType() == "ISO15693"
                            {
                                var cmd = (card as EM4135Chip).getChipIdentifier();// IMifareUltralightCommands;
                                //object res = cmd.ReadPage(4);

                                //appIDs = (appIDsObject as UInt32[]);
                            }

                            return ERROR.NoError;
                        }
                    }
                }
                return ERROR.NoError;
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""));
                return ERROR.IOError;
            }
        }

        #endregion
    }
}