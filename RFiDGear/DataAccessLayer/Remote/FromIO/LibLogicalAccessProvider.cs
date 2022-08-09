using LibLogicalAccess;
using LibLogicalAccess.Card;
using LibLogicalAccess.Reader;
using LibLogicalAccess.Crypto;

using ByteArrayHelper.Extensions;

using RFiDGear.Model;

using Log4CSharp;

using System;
using System.Threading;

namespace RFiDGear.DataAccessLayer.Remote.FromIO
{
	/// <summary>
	/// Description of RFiDAccess.
	///
	/// Initialize Reader
	/// </summary>
	///
	public class LibLogicalAccessProvider : ReaderDevice
	{
		// global (cross-class) Instances go here ->
		private static readonly string FacilityName = "RFiDGear";
		private ReaderProvider readerProvider;
		private ReaderUnit readerUnit;
		private Chip card;
		#region properties

		#endregion properties

		#region contructor
		public LibLogicalAccessProvider()
		{
		}

		public LibLogicalAccessProvider(ReaderTypes readerType)
		{
			try
			{
				readerProvider = LibraryManager.getInstance().getReaderProvider(Enum.GetName(typeof(ReaderTypes), readerType));
				readerUnit = readerProvider.createReaderUnit();

				GenericChip = new GenericChipModel("", CARD_TYPE.Unspecified);
				AppIDList = new uint[0];
			}
			catch (Exception e)
			{
				LogWriter.CreateLogEntry(e, FacilityName);
			}
		}

		#endregion contructor

		#region common

		public override ERROR ReadChipPublic()
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

							card = readerUnit.getSingleChip();

							if (!string.IsNullOrWhiteSpace(ByteConverter.HexToString(card.getChipIdentifier().ToArray())))
							{
								try {
									//CardInfo = new CARD_INFO((CARD_TYPE)Enum.Parse(typeof(CARD_TYPE), card.getCardType()), ByteConverter.HexToString(card.getChipIdentifier().ToArray()));
									//readerUnit.Disconnect();

									GenericChip = new GenericChipModel(ByteConverter.HexToString(card.getChipIdentifier().ToArray()), (CARD_TYPE)Enum.Parse(typeof(CARD_TYPE), card.getCardType()));

									if (card.getCardType().Contains("DESFire"))
									{
										DESFireCommands cmd = card.getCommands() as DESFireCommands;

										DESFireCommands.DESFireCardVersion version = cmd.getVersion();

										if (version.hardwareMjVersion == 1)
											GenericChip.CardType = CARD_TYPE.DESFireEV1;

										else if (version.hardwareMjVersion == 2)
											GenericChip.CardType = CARD_TYPE.DESFireEV2;
									}

									//ISO15693Commands commands = card.Commands as ISO15693Commands;
									//SystemInformation si = commands.GetSystemInformation();
									//var block=commands.ReadBlock(21, 4);
									return ERROR.NoError;
								}
								catch (Exception e) {
									LogWriter.CreateLogEntry(e, FacilityName);
									
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

				LogWriter.CreateLogEntry(e, FacilityName);

				return ERROR.IOError;
			}

			return ERROR.IOError;
		}

		#endregion common

		#region mifare classic

		public override ERROR ReadMiFareClassicSingleSector(int sectorNumber, string aKey, string bKey)
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
									LogWriter.CreateLogEntry(e, FacilityName);
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
											ByteVector data = cmd.readBinary(
												(byte)CustomConverter.GetChipBasedDataBlockNumber(GenericChip.CardType, sectorNumber, k),
												48);

											DataBlock.Data = data.ToArray();
											
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
				LogWriter.CreateLogEntry(e, FacilityName);
				return ERROR.AuthenticationError;
			}
		}

		public override ERROR WriteMiFareClassicSingleSector(int sectorNumber, string _aKey, string _bKey, byte[] buffer)
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
									LogWriter.CreateLogEntry(e, FacilityName);
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
				LogWriter.CreateLogEntry(e, FacilityName);

				return ERROR.IOError;
			}
			return ERROR.DeviceNotReadyError;
		}

		public override ERROR WriteMiFareClassicSingleBlock(int _blockNumber, string _aKey, string _bKey, byte[] buffer)
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
									LogWriter.CreateLogEntry(e, FacilityName);
								}
							}

							var cmd = card.getCommands() as MifareCommands;

							try
							{
								cmd.loadKey((byte)0, MifareKeyType.KT_KEY_A, keyA); // FIXME "sectorNumber" to 0

								try
								{ //try to Auth with Keytype A
									cmd.authenticate((byte)(_blockNumber), (byte)0, MifareKeyType.KT_KEY_A); // FIXME same as '303

									cmd.updateBinary((byte)_blockNumber, new ByteVector(buffer));

									return ERROR.NoError;
								}
								catch
								{ // Try Auth with Keytype b

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
				LogWriter.CreateLogEntry(e, FacilityName);

				return ERROR.IOError;
			}
			return ERROR.IOError;
		}

		public override ERROR ReadMiFareClassicSingleBlock(int _blockNumber, string _aKey, string _bKey)
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
									LogWriter.CreateLogEntry(e, FacilityName);
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
				LogWriter.CreateLogEntry(e, FacilityName);

				return ERROR.IOError;
			}
			return ERROR.IOError;
		}

		public override ERROR WriteMiFareClassicWithMAD(int _madApplicationID, int _madStartSector,
											   string _aKeyToUse, string _bKeyToUse, string _aKeyToWrite, string _bKeyToWrite,
											   string _madAKeyToUse, string _madBKeyToUse, string _madAKeyToWrite, string _madBKeyToWrite,
											   byte[] buffer, byte _madGPB, SectorAccessBits _sab, bool _useMADToAuth = false, bool _keyToWriteUseMAD = false)
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
				if (readerUnit.connectToReader())
				{
					if (readerUnit.waitInsertion(Constants.MAX_WAIT_INSERTION))
					{
						if (readerUnit.connect())
						{
							ReaderUnitName = readerUnit.getConnectedName();

							card = readerUnit.getSingleChip();

							if (!string.IsNullOrWhiteSpace(ByteConverter.HexToString(card.getChipIdentifier().ToArray())))
							{
								try
								{
									GenericChip = new GenericChipModel(ByteConverter.HexToString(card.getChipIdentifier().ToArray()), (CARD_TYPE)Enum.Parse(typeof(CARD_TYPE), card.getCardType()));
								}
								catch (Exception e)
								{
									LogWriter.CreateLogEntry(e, FacilityName);
								}
							}

							MifareLocation mlocation = new MifareLocation
							{
								aid = (ushort)_madApplicationID,
								useMAD = _keyToWriteUseMAD,
								sector = _madStartSector
							};

							MifareAccessInfo aiToWrite = new MifareAccessInfo
							{
								useMAD = _keyToWriteUseMAD,

							};

							aiToWrite.madKeyA.fromString(_madAKeyToUse == _madAKeyToWrite ? madAKeyToUse.getString(true) : madAKeyToWrite.getString(true)); // only set new madkey if mad key has changed
							aiToWrite.madKeyB.fromString(_madBKeyToUse == _madBKeyToWrite ? madBKeyToUse.getString(true) : madBKeyToWrite.getString(true)); // only set new madkey if mad key has changed
							aiToWrite.keyA.fromString(_aKeyToUse == _aKeyToWrite ? mAKeyToUse.getString(true) : mAKeyToWrite.getString(true));
							aiToWrite.keyB.fromString(_bKeyToUse == _bKeyToWrite ? mBKeyToUse.getString(true) : mBKeyToWrite.getString(true));
							aiToWrite.madGPB = _madGPB;

							var aiToUse = new MifareAccessInfo
							{
								useMAD = _useMADToAuth,
								keyA = mAKeyToUse,
								keyB = mBKeyToUse
							};

							if (_useMADToAuth)
							{
								aiToUse.madKeyA = madAKeyToUse;
								aiToUse.madKeyB = madBKeyToUse;
								aiToUse.madGPB = _madGPB;
							}

							//TODO: report BUG when using SL1
							var cmd = card.getCommands() as MifareCommands;
							var cardService = card.getService(LibLogicalAccess.CardServiceType.CST_STORAGE) as StorageCardService;

							try
							{
								cardService.writeData(mlocation, aiToUse, aiToWrite, new ByteVector(buffer), CardBehavior.CB_AUTOSWITCHAREA);
							}
							catch (Exception e)
							{
								LogWriter.CreateLogEntry(e, FacilityName);
								return ERROR.AuthenticationError;
							}
							return ERROR.NoError;
						}
					}
				}
			}
			catch (Exception e)
			{
				LogWriter.CreateLogEntry(e, FacilityName);
				return ERROR.AuthenticationError;
			}
			return ERROR.NoError;
		}

		public override ERROR ReadMiFareClassicWithMAD(int madApplicationID, string _aKeyToUse, string _bKeyToUse, string _madAKeyToUse, string _madBKeyToUse, int _length, byte _madGPB, bool _useMADToAuth = true, bool _aiToUseIsMAD = false)
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
									GenericChip = new GenericChipModel(ByteConverter.HexToString(card.getChipIdentifier().ToArray()), (CARD_TYPE)Enum.Parse(typeof(CARD_TYPE), card.getCardType()));
								}
								catch (Exception e) {
									LogWriter.CreateLogEntry(e, FacilityName);
								}
							}


							MifareLocation mlocation = card.createLocation() as MifareLocation;
							mlocation.aid = (ushort)madApplicationID;
							mlocation.useMAD = _useMADToAuth;

							var aiToUse = new MifareAccessInfo
							{
								useMAD = true,
								keyA = mAKeyToUse,
								keyB = mBKeyToUse,
								madKeyA = madAKeyToUse,
								madKeyB = madBKeyToUse,
								madGPB = _madGPB
							};

							var cmd = card.getCommands() as MifareCommands;
							var cardService = card.getService(CardServiceType.CST_STORAGE) as StorageCardService;
							
							try
							{
								MifareClassicData = cardService.readData(mlocation, aiToUse, (uint)_length, CardBehavior.CB_AUTOSWITCHAREA).ToArray();
							}
							catch (Exception e)
							{
                                LogWriter.CreateLogEntry(e, FacilityName);
                                return ERROR.AuthenticationError;
							}
							return ERROR.NoError;
						}
					}
				}
			}
			catch (Exception e)
			{
				LogWriter.CreateLogEntry(e, FacilityName);
				return ERROR.AuthenticationError;
			}
			return ERROR.NoError;
		}
		
		#endregion mifare classic

		#region mifare ultralight

		public override ERROR ReadMifareUltralightSinglePage(int _pageNo)
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
				LogWriter.CreateLogEntry(e, FacilityName);
				return ERROR.IOError;
			}
		}

		#endregion mifare ultralight

		#region mifare desfire

		public override ERROR GetMiFareDESFireChipAppIDs(string _appMasterKey, DESFireKeyType _keyTypeAppMasterKey)
		{
			try
			{
				// The excepted memory tree
				DESFireLocation location = new DESFireLocation();
				// File communication requires encryption
				location.securityLevel = (LibLogicalAccess.Card.EncryptionMode)EncryptionMode.CM_ENCRYPT;

				// Keys to use for authentication
				DESFireAccessInfo aiToUse = new DESFireAccessInfo();
				aiToUse.masterCardKey.fromString(_appMasterKey);
				aiToUse.masterCardKey.setKeyType((LibLogicalAccess.Card.DESFireKeyType)_keyTypeAppMasterKey);


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

										UIntCollection appIDsObject = cmd.getApplicationIDs();
										AppIDList = appIDsObject.ToArray();

										return ERROR.NoError;
									}
									catch
									{
										//Get AppIDs with Authentication (Directory Listing with PICC MK)
										try
										{
											cmd.selectApplication((uint)0);
											cmd.authenticate((byte)0, aiToUse.masterCardKey);

											UIntCollection appIDsObject = cmd.getApplicationIDs();
											AppIDList = appIDsObject.ToArray();

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

									var cmd = (card as DESFireChip).getDESFireCommands();
                  
									try
									{
										UIntCollection appIDsObject = cmd.getApplicationIDs();
										AppIDList = appIDsObject.ToArray();

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

											UIntCollection appIDsObject = cmd.getApplicationIDs();
											AppIDList = appIDsObject.ToArray();


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

										UIntCollection appIDsObject = cmd.getApplicationIDs();
										AppIDList = appIDsObject.ToArray();

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

		public override ERROR CreateMifareDesfireFile(string _appMasterKey, DESFireKeyType _keyTypeAppMasterKey, FileType_MifareDesfireFileType _fileType, DESFireAccessRights _accessRights, EncryptionMode _encMode,
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
				aiToUse.masterCardKey.fromString(CustomConverter.DesfireKeyToCheck);
				aiToUse.masterCardKey.setKeyType((LibLogicalAccess.Card.DESFireKeyType)_keyTypeAppMasterKey);
				
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
												cmd.createStdDataFile((byte)_fileNo, (LibLogicalAccess.Card.EncryptionMode)_encMode,
													new LibLogicalAccess.Card.DESFireAccessRights()
													{
														changeAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.changeAccess,
														readAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.readAccess,
														writeAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.writeAccess,
														readAndWriteAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.readAndWriteAccess
													}, (uint)_fileSize) ;
                                                break;

                                            case FileType_MifareDesfireFileType.BackupFile:
                                                cmd.createBackupFile((byte)_fileNo, (LibLogicalAccess.Card.EncryptionMode)_encMode,
													new LibLogicalAccess.Card.DESFireAccessRights()
													{
														changeAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.changeAccess,
														readAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.readAccess,
														writeAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.writeAccess,
														readAndWriteAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.readAndWriteAccess
													}, (uint)_fileSize);
												break;

                                            case FileType_MifareDesfireFileType.ValueFile:
                                                cmd.createValueFile((byte)_fileNo, (LibLogicalAccess.Card.EncryptionMode)_encMode,
													new LibLogicalAccess.Card.DESFireAccessRights()
													{
														changeAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.changeAccess,
														readAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.readAccess,
														writeAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.writeAccess,
														readAndWriteAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.readAndWriteAccess
													}, _minValue, _maxValue, _initValue, _isValueLimited);
                                                break;

                                            case FileType_MifareDesfireFileType.CyclicRecordFile:
                                                cmd.createCyclicRecordFile((byte)_fileNo, (LibLogicalAccess.Card.EncryptionMode)_encMode,
													new LibLogicalAccess.Card.DESFireAccessRights()
													{
														changeAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.changeAccess,
														readAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.readAccess,
														writeAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.writeAccess,
														readAndWriteAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.readAndWriteAccess
													}, (uint)_fileSize, (uint)_maxNbOfRecords);
                                                break;

                                            case FileType_MifareDesfireFileType.LinearRecordFile:
                                                cmd.createLinearRecordFile((byte)_fileNo, (LibLogicalAccess.Card.EncryptionMode)_encMode,
													new LibLogicalAccess.Card.DESFireAccessRights()
													{
														changeAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.changeAccess,
														readAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.readAccess,
														writeAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.writeAccess,
														readAndWriteAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.readAndWriteAccess
													}, (uint)_fileSize, (uint)_maxNbOfRecords);
                                                break;
                                        }

										return ERROR.NoError;
                                    }

									switch (_fileType)
									{
										case FileType_MifareDesfireFileType.StdDataFile:
											cmd.createStdDataFile((byte)_fileNo, (LibLogicalAccess.Card.EncryptionMode)_encMode,
													new LibLogicalAccess.Card.DESFireAccessRights()
													{
														changeAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.changeAccess,
														readAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.readAccess,
														writeAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.writeAccess,
														readAndWriteAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.readAndWriteAccess
													}, (uint)_fileSize);
											break;

										case FileType_MifareDesfireFileType.BackupFile:
											cmd.createBackupFile((byte)_fileNo, (LibLogicalAccess.Card.EncryptionMode)_encMode,
													new LibLogicalAccess.Card.DESFireAccessRights()
													{
														changeAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.changeAccess,
														readAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.readAccess,
														writeAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.writeAccess,
														readAndWriteAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.readAndWriteAccess
													}, (uint)_fileSize);
											break;

										case FileType_MifareDesfireFileType.ValueFile:
											cmd.createValueFile((byte)_fileNo, (LibLogicalAccess.Card.EncryptionMode)_encMode,
													new LibLogicalAccess.Card.DESFireAccessRights()
													{
														changeAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.changeAccess,
														readAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.readAccess,
														writeAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.writeAccess,
														readAndWriteAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.readAndWriteAccess
													}, _minValue, _maxValue, _initValue, _isValueLimited);
											break;

										case FileType_MifareDesfireFileType.CyclicRecordFile:
											cmd.createCyclicRecordFile((byte)_fileNo, (LibLogicalAccess.Card.EncryptionMode)_encMode,
													new LibLogicalAccess.Card.DESFireAccessRights()
													{
														changeAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.changeAccess,
														readAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.readAccess,
														writeAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.writeAccess,
														readAndWriteAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.readAndWriteAccess
													}, (uint)_fileSize, (uint)_maxNbOfRecords);
											break;

										case FileType_MifareDesfireFileType.LinearRecordFile:
											cmd.createLinearRecordFile((byte)_fileNo, (LibLogicalAccess.Card.EncryptionMode)_encMode,
													new LibLogicalAccess.Card.DESFireAccessRights()
													{
														changeAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.changeAccess,
														readAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.readAccess,
														writeAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.writeAccess,
														readAndWriteAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.readAndWriteAccess
													}, (uint)_fileSize, (uint)_maxNbOfRecords);
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
												cmd.createStdDataFile((byte)_fileNo, (LibLogicalAccess.Card.EncryptionMode)_encMode,
													new LibLogicalAccess.Card.DESFireAccessRights()
													{
														changeAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.changeAccess,
														readAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.readAccess,
														writeAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.writeAccess,
														readAndWriteAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.readAndWriteAccess
													}, (uint)_fileSize);
												break;

											case FileType_MifareDesfireFileType.BackupFile:
												cmd.createBackupFile((byte)_fileNo, (LibLogicalAccess.Card.EncryptionMode)_encMode,
													new LibLogicalAccess.Card.DESFireAccessRights()
													{
														changeAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.changeAccess,
														readAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.readAccess,
														writeAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.writeAccess,
														readAndWriteAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.readAndWriteAccess
													}, (uint)_fileSize);
												break;

											case FileType_MifareDesfireFileType.ValueFile:
												cmd.createValueFile((byte)_fileNo, (LibLogicalAccess.Card.EncryptionMode)_encMode,
													new LibLogicalAccess.Card.DESFireAccessRights()
													{
														changeAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.changeAccess,
														readAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.readAccess,
														writeAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.writeAccess,
														readAndWriteAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.readAndWriteAccess
													}, _minValue, _maxValue, _initValue, _isValueLimited);
												break;

											case FileType_MifareDesfireFileType.CyclicRecordFile:
												cmd.createCyclicRecordFile((byte)_fileNo, (LibLogicalAccess.Card.EncryptionMode)_encMode,
													new LibLogicalAccess.Card.DESFireAccessRights()
													{
														changeAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.changeAccess,
														readAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.readAccess,
														writeAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.writeAccess,
														readAndWriteAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.readAndWriteAccess
													},(uint)_fileSize, (uint)_maxNbOfRecords);
												break;

											case FileType_MifareDesfireFileType.LinearRecordFile:
												cmd.createLinearRecordFile((byte)_fileNo, (LibLogicalAccess.Card.EncryptionMode)_encMode,
													new LibLogicalAccess.Card.DESFireAccessRights()
													{
														changeAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.changeAccess,
														readAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.readAccess,
														writeAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.writeAccess,
														readAndWriteAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.readAndWriteAccess
													}, (uint)_fileSize, (uint)_maxNbOfRecords);
												break;
										}

										return ERROR.NoError;
									}

									switch (_fileType)
									{
										case FileType_MifareDesfireFileType.StdDataFile:
											cmd.createStdDataFile((byte)_fileNo, (LibLogicalAccess.Card.EncryptionMode)_encMode,
													new LibLogicalAccess.Card.DESFireAccessRights()
													{
														changeAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.changeAccess,
														readAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.readAccess,
														writeAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.writeAccess,
														readAndWriteAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.readAndWriteAccess
													}, (uint)_fileSize);
											break;

										case FileType_MifareDesfireFileType.BackupFile:
											cmd.createBackupFile((byte)_fileNo, (LibLogicalAccess.Card.EncryptionMode)_encMode,
													new LibLogicalAccess.Card.DESFireAccessRights()
													{
														changeAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.changeAccess,
														readAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.readAccess,
														writeAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.writeAccess,
														readAndWriteAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.readAndWriteAccess
													}, (uint)_fileSize);
											break;

										case FileType_MifareDesfireFileType.ValueFile:
											cmd.createValueFile((byte)_fileNo, (LibLogicalAccess.Card.EncryptionMode)_encMode,
													new LibLogicalAccess.Card.DESFireAccessRights()
													{
														changeAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.changeAccess,
														readAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.readAccess,
														writeAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.writeAccess,
														readAndWriteAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.readAndWriteAccess
													}, _minValue, _maxValue, _initValue, _isValueLimited);
											break;

										case FileType_MifareDesfireFileType.CyclicRecordFile:
											cmd.createCyclicRecordFile((byte)_fileNo, (LibLogicalAccess.Card.EncryptionMode)_encMode,
													new LibLogicalAccess.Card.DESFireAccessRights()
													{
														changeAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.changeAccess,
														readAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.readAccess,
														writeAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.writeAccess,
														readAndWriteAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.readAndWriteAccess
													}, (uint)_fileSize, (uint)_maxNbOfRecords);
											break;

										case FileType_MifareDesfireFileType.LinearRecordFile:
											cmd.createLinearRecordFile((byte)_fileNo, (LibLogicalAccess.Card.EncryptionMode)_encMode,
													new LibLogicalAccess.Card.DESFireAccessRights()
													{
														changeAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.changeAccess,
														readAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.readAccess,
														writeAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.writeAccess,
														readAndWriteAccess = (LibLogicalAccess.Card.TaskAccessRights)accessRights.readAndWriteAccess
													}, (uint)_fileSize, (uint)_maxNbOfRecords);
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

		public override ERROR ReadMiFareDESFireChipFile(string _appMasterKey, DESFireKeyType _keyTypeAppMasterKey,
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
				location.securityLevel = (LibLogicalAccess.Card.EncryptionMode)_encMode;

				// Keys to use for authentication

				// Get the card storage service
				StorageCardService storage = (StorageCardService)card.getService(CardServiceType.CST_STORAGE);

				// Change keys with the following ones
				DESFireAccessInfo aiToWrite = new DESFireAccessInfo();
				CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_appMasterKey);
				aiToWrite.masterApplicationKey.fromString(CustomConverter.DesfireKeyToCheck);
				aiToWrite.masterApplicationKey.setKeyType((LibLogicalAccess.Card.DESFireKeyType)_keyTypeAppMasterKey);

				CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_appReadKey);
				aiToWrite.readKey.fromString(CustomConverter.DesfireKeyToCheck);
				aiToWrite.readKey.setKeyType((LibLogicalAccess.Card.DESFireKeyType)_keyTypeAppReadKey);
				aiToWrite.readKeyno = (byte)_readKeyNo;

				CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_appWriteKey);
				aiToWrite.writeKey.fromString(CustomConverter.DesfireKeyToCheck);
				aiToWrite.writeKey.setKeyType((LibLogicalAccess.Card.DESFireKeyType)_keyTypeAppWriteKey);
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

										MifareDESFireData = cmd.readData((byte)_fileNo, 0, (uint)_fileSize, LibLogicalAccess.Card.EncryptionMode.CM_ENCRYPT).ToArray();
									}
									catch
									{
										cmd.selectApplication((uint)_appID);

										MifareDESFireData = cmd.readData((byte)_fileNo, 0, (uint)_fileSize, LibLogicalAccess.Card.EncryptionMode.CM_ENCRYPT).ToArray();
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
		
		public override ERROR WriteMiFareDESFireChipFile(string _cardMasterKey, DESFireKeyType _keyTypeCardMasterKey,
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
				location.aid = (uint)_appID;
				// File 0 into this application
				location.file = (byte)_fileNo;
				// File communication requires encryption
				location.securityLevel = (LibLogicalAccess.Card.EncryptionMode)_encMode;

				// Keys to use for authentication

				// Get the card storage service
				StorageCardService storage = (StorageCardService)card.getService(CardServiceType.CST_STORAGE);

				// Change keys with the following ones
				DESFireAccessInfo aiToUse = new DESFireAccessInfo();
				CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_cardMasterKey);
				aiToUse.masterCardKey.fromString(CustomConverter.DesfireKeyToCheck);
				aiToUse.masterCardKey.setKeyType((LibLogicalAccess.Card.DESFireKeyType)_keyTypeAppMasterKey);

				DESFireAccessInfo aiToWrite = new DESFireAccessInfo();
				aiToWrite.masterCardKey.fromString(CustomConverter.DesfireKeyToCheck);
				aiToWrite.masterCardKey.setKeyType((LibLogicalAccess.Card.DESFireKeyType)_keyTypeAppMasterKey);

				CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_appMasterKey);
				aiToWrite.masterApplicationKey.fromString(CustomConverter.DesfireKeyToCheck);
				aiToWrite.masterApplicationKey.setKeyType((LibLogicalAccess.Card.DESFireKeyType)_keyTypeAppMasterKey);

				CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_appReadKey);
				aiToWrite.readKey.fromString(CustomConverter.DesfireKeyToCheck);
				aiToWrite.readKey.setKeyType((LibLogicalAccess.Card.DESFireKeyType)_keyTypeAppReadKey);
				aiToWrite.readKeyno = (byte)_readKeyNo;

				CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_appWriteKey);
				aiToWrite.writeKey.fromString(CustomConverter.DesfireKeyToCheck);
				aiToWrite.writeKey.setKeyType((LibLogicalAccess.Card.DESFireKeyType)_keyTypeAppWriteKey);
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

									cmd.writeData((byte)_fileNo, 0, new ByteVector(_data), LibLogicalAccess.Card.EncryptionMode.CM_ENCRYPT);

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
		
		public override ERROR AuthToMifareDesfireApplication(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumber, int _appID = 0)
		{
			try
			{
				// The excepted memory tree
				DESFireLocation location = new DESFireLocation();
				// The Application ID to use
				location.aid = (uint)_appID;
				// File communication requires encryption
				//location.securityLevel = EncryptionMode.CM_ENCRYPT;

				// Keys to use for authentication
				DESFireAccessInfo aiToUse = new DESFireAccessInfo();
				CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_applicationMasterKey);
				aiToUse.masterCardKey.fromString(CustomConverter.DesfireKeyToCheck);
				aiToUse.masterCardKey.setKeyType((LibLogicalAccess.Card.DESFireKeyType)_keyType);

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

		public override ERROR GetMifareDesfireAppSettings(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumberCurrent = 0, int _appID = 0)
		{
			byte maxNbrOfKeys;
			LibLogicalAccess.Card.DESFireKeySettings keySettings;

			try
			{
				// Keys to use for authentication
				DESFireAccessInfo aiToUse = new DESFireAccessInfo();
				CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_applicationMasterKey);
				aiToUse.masterCardKey.fromString(CustomConverter.DesfireKeyToCheck);
				aiToUse.masterCardKey.setKeyType((LibLogicalAccess.Card.DESFireKeyType)_keyType);

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
											DesfireAppKeySetting = (DESFireKeySettings)keySettings;
											
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
									DesfireAppKeySetting = (DESFireKeySettings)keySettings;

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
											DesfireAppKeySetting = (DESFireKeySettings)keySettings;

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
									DesfireAppKeySetting = (DESFireKeySettings)keySettings;

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

		public override ERROR CreateMifareDesfireApplication(
			string _piccMasterKey, DESFireKeySettings _keySettingsTarget, DESFireKeyType _keyTypePiccMasterKey, 
			DESFireKeyType _keyTypeTargetApplication, int _maxNbKeys, int _appID, bool authenticateToPICCFirst = true)
		{
			try
			{
				// The excepted memory tree
				DESFireLocation location = new DESFireLocation();
				// The Application ID to use
				location.aid = (uint)_appID;

				// File communication requires encryption
				location.securityLevel = LibLogicalAccess.Card.EncryptionMode.CM_ENCRYPT;

				// IDESFireEV1Commands cmd;
				// Keys to use for authentication
				DESFireAccessInfo aiToUse = new DESFireAccessInfo();
				CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_piccMasterKey);
				aiToUse.masterCardKey.fromString(CustomConverter.DesfireKeyToCheck);
				aiToUse.masterCardKey.setKeyType((LibLogicalAccess.Card.DESFireKeyType)_keyTypePiccMasterKey);

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
									
									cmd.createApplication((uint)_appID, (LibLogicalAccess.Card.DESFireKeySettings)_keySettingsTarget, (byte)_maxNbKeys);

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

									ev1Cmd.createApplication((uint)_appID, (LibLogicalAccess.Card.DESFireKeySettings)_keySettingsTarget, (byte)_maxNbKeys, (LibLogicalAccess.Card.DESFireKeyType)_keyTypeTargetApplication);

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

		public override ERROR ChangeMifareDesfireApplicationKey(
			string _applicationMasterKeyCurrent, int _keyNumberCurrent, DESFireKeyType _keyTypeCurrent, 
			string _applicationMasterKeyTarget, int _keyNumberTarget, int selectedDesfireAppKeyVersionTargetAsIntint, 
			DESFireKeyType _keyTypeTarget, int _appIDCurrent, int _appIDTarget, DESFireKeySettings keySettings)
		{
			try
			{
				DESFireKey masterApplicationKey = new DESFireKey();
				masterApplicationKey.setKeyType((LibLogicalAccess.Card.DESFireKeyType)_keyTypeCurrent);
				CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_applicationMasterKeyCurrent);
				masterApplicationKey.fromString(CustomConverter.DesfireKeyToCheck);

				DESFireKey applicationMasterKeyTarget = new DESFireKey();
				applicationMasterKeyTarget.setKeyType((LibLogicalAccess.Card.DESFireKeyType)_keyTypeTarget);
				CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_applicationMasterKeyTarget);
				applicationMasterKeyTarget.fromString(CustomConverter.DesfireKeyToCheck);
				//applicationMasterKeyTarget.setKeyVersion((byte)selectedDesfireAppKeyVersionTargetAsIntint);

				readerUnit.disconnectFromReader();

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
								var ev1Cmd = (card as DESFireEV1Chip).getCommands() as DESFireEV1ISO7816Commands;

								//var kv = ev1Cmd?.getKeyVersion((byte)_keyNumberCurrent);

								//if (ev1Cmd != null)
                                //{
								//	masterApplicationKey.setKeyVersion((byte)kv);
								//}

								try
								{
									if (_appIDCurrent == 0)
									{
										try
										{
											cmd.selectApplication((uint)_appIDCurrent);
											cmd.authenticate((byte)_keyNumberCurrent, masterApplicationKey);
											cmd.changeKeySettings((LibLogicalAccess.Card.DESFireKeySettings)keySettings);
											cmd.authenticate((byte)_keyNumberCurrent, masterApplicationKey);
											cmd.changeKey((byte)_keyNumberCurrent, applicationMasterKeyTarget);
											return ERROR.NoError;
										}

										catch
										{
											try
											{
												cmd.authenticate((byte)_keyNumberCurrent, masterApplicationKey);
												cmd.changeKey((byte)_keyNumberCurrent, applicationMasterKeyTarget);
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
												{
													return ERROR.IOError;
												}
											}
										}
									}
									else
									{

										applicationMasterKeyTarget.setKeyType((LibLogicalAccess.Card.DESFireKeyType)_keyTypeCurrent);

										cmd.selectApplication((uint)_appIDCurrent);
										
										try
										{
											cmd.authenticate((byte)_keyNumberCurrent, masterApplicationKey);
											cmd.changeKey((byte)_keyNumberTarget, applicationMasterKeyTarget);
											cmd.authenticate((byte)_keyNumberCurrent, applicationMasterKeyTarget);

											try
											{
												cmd.changeKeySettings((LibLogicalAccess.Card.DESFireKeySettings)keySettings);
											}
											catch { }
										}

										catch (Exception ex)
										{
											try
											{
												cmd.authenticate((byte)_keyNumberCurrent, masterApplicationKey);
												cmd.changeKeySettings((LibLogicalAccess.Card.DESFireKeySettings)keySettings);
												cmd.authenticate((byte)_keyNumberCurrent, masterApplicationKey);
												cmd.changeKey((byte)_keyNumberTarget, applicationMasterKeyTarget);
												return ERROR.NoError;
											}

											catch
											{
												try
												{
													cmd.authenticate((byte)_keyNumberCurrent, masterApplicationKey);
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

		public override ERROR DeleteMifareDesfireApplication(string _applicationMasterKey, DESFireKeyType _keyType, int _appID = 0)
		{
			try
			{
				// The excepted memory tree
				DESFireLocation location = new DESFireLocation();
				// The Application ID to use
				location.aid = (uint)_appID;
				// File communication requires encryption
				location.securityLevel = LibLogicalAccess.Card.EncryptionMode.CM_ENCRYPT;

				// IDESFireEV1Commands cmd;
				// Keys to use for authentication
				DESFireAccessInfo aiToUse = new DESFireAccessInfo();
				CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_applicationMasterKey);
				aiToUse.masterCardKey.fromString(CustomConverter.DesfireKeyToCheck);
				aiToUse.masterCardKey.setKeyType((LibLogicalAccess.Card.DESFireKeyType)_keyType);

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

		public override ERROR DeleteMifareDesfireFile(string _applicationMasterKey, DESFireKeyType _keyType, int _appID = 0, int _fileID = 0)
		{
			try
			{
				// The excepted memory tree
				DESFireLocation location = new DESFireLocation();
				// The Application ID to use
				location.aid = (uint)_appID;
				// File communication requires encryption
				location.securityLevel = LibLogicalAccess.Card.EncryptionMode.CM_ENCRYPT;

				// Keys to use for authentication
				DESFireAccessInfo aiToUse = new DESFireAccessInfo();
				CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_applicationMasterKey);
				aiToUse.masterCardKey.fromString(CustomConverter.DesfireKeyToCheck);
				aiToUse.masterCardKey.setKeyType((LibLogicalAccess.Card.DESFireKeyType)_keyType);

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

		public override ERROR FormatDesfireCard(string _applicationMasterKey, DESFireKeyType _keyType, int _appID = 0)
		{
			try
			{
				// The excepted memory tree
				DESFireLocation location = new DESFireLocation();
				// The Application ID to use
				location.aid = (uint)_appID;
				// File communication requires encryption
				location.securityLevel = LibLogicalAccess.Card.EncryptionMode.CM_ENCRYPT;

				// Keys to use for authentication
				DESFireAccessInfo aiToUse = new DESFireAccessInfo();
				CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_applicationMasterKey);
				aiToUse.masterCardKey.fromString(CustomConverter.DesfireKeyToCheck);
				aiToUse.masterCardKey.setKeyType((LibLogicalAccess.Card.DESFireKeyType)_keyType);

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
									//cmd.commitTransaction();
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

		public override ERROR GetMifareDesfireFileList(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumberCurrent = 0, int _appID = 0)
		{
			try
			{
				// The excepted memory tree
				DESFireLocation location = new DESFireLocation();
				// The Application ID to use
				location.aid = (uint)_appID;
				// File communication requires encryption
				location.securityLevel = LibLogicalAccess.Card.EncryptionMode.CM_ENCRYPT;

				//IDESFireEV1Commands cmd;
				// Keys to use for authentication
				DESFireAccessInfo aiToUse = new DESFireAccessInfo();
				CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_applicationMasterKey);
				aiToUse.masterCardKey.fromString(CustomConverter.DesfireKeyToCheck);
				aiToUse.masterCardKey.setKeyType((LibLogicalAccess.Card.DESFireKeyType)_keyType);

				ByteVector fileIDsObject;

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
											FileIDList = fileIDsObject.ToArray();
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
									FileIDList = fileIDsObject.ToArray();

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

		public override ERROR GetMifareDesfireFileSettings(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumberCurrent = 0, int _appID = 0, int _fileNo = 0)
		{
			try
			{
				// IDESFireEV1Commands cmd;
				// Keys to use for authentication
				DESFireAccessInfo aiToUse = new DESFireAccessInfo();
				CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(_applicationMasterKey);
				aiToUse.masterCardKey.fromString(CustomConverter.DesfireKeyToCheck);
				aiToUse.masterCardKey.setKeyType((LibLogicalAccess.Card.DESFireKeyType)_keyType);

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
											/*
											var fs = cmd.getFileSettings((byte)_fileNo);
											DesfireFileSettings = new DESFireFileSettings({
												accessRights = fs.accessRights,
												comSett = fs.comSett,
												//dataFile = fs.getDataFile(),
												FileType = fs.fileType
											}); 
											*/
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
									/*
									var fs = cmd.getFileSettings((byte)_fileNo);
									DesfireFileSettings = new DESFireFileSettings({
										accessRights = fs.accessRights,
										comSett = fs.comSett,
										//dataFile = fs.getDataFile(),
										FileType = fs.fileType
									});
									*/
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
				LogWriter.CreateLogEntry(e, FacilityName);
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
                LogWriter.CreateLogEntry(e, FacilityName);
                return ERROR.IOError;
            }
        }

        #endregion
    }
}