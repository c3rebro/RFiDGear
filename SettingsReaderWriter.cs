using System;
using System.Windows;
using System.ComponentModel;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using LibLogicalAccess;

namespace RFiDGear
{
	/// <summary>
	/// Description of BasicCardSettings.
	/// </summary>
	public class SettingsReaderWriter
	{
		
		#region fields
		readonly string _settingsFileFileName = "settings.xml";
		readonly string _classicCardDefaultSectorTrailer = "FFFFFFFFFFFF,FF0780C3,FFFFFFFFFFFF";
		readonly string _classicCardDefaultQuickCheckKeys = "FFFFFFFFFFFF,A1B2C3D4E5F6,1A2B3C4D5E6F," +
			"000000000000,A0B0C0D0E0F0,A1B1C1D1E1F1," +
			"A0A1A2A3A4A5,B0B1B2B3B4B5,4D3A99C351DD," +
			"1A982C7E459A,D3F7D3F7D3F7,AABBCCDDEEFF";
		readonly string _defaultReaderName = "";
		readonly string _defaultReaderProvider = "PCSC";
		readonly string _defaultLanguage = "english";
		readonly string _desfireCardCardMasterKey = "00000000000000000000000000000000";
		readonly string _desfireCardApplicationMasterKey = "00000000000000000000000000000000";
		readonly string _desfireCardReadKey = "00000000000000000000000000000000";
		readonly string _desfireCardWriteKey = "00000000000000000000000000000000";
		
		private string oldReaderName;
		private string oldReaderProvider;
		private string _oldDesfireCardCardMasterKey;
		private string _oldDesfireCardApplicationMasterKey;
		private string _oldDesfireCardReadKey;
		private string _oldDesfireCardWriteKey;
		private string oldKeyA;
		private string oldAccessBits;
		private string oldKeyB;
		private string oldLanguage;
		private string appDataPath;
		
		public List<string> DefaultClassicCardKeysAKeys { get; set; }
		public List<string> DefaultClassicCardAccessBits { get; set; }
		public List<string> DefaultClassicCardKeysBKeys { get; set; }
		public List<string> DefaultClassicCardQuickCheckKeys {get; set; }
		public string DefaultDesfireCardCardMasterKey { get; set; }
		public string DefaultDesfireCardCardMasterKeyType { get; set; }
		public string DefaultDesfireCardApplicationMasterKey { get; set; }
		public string DefaultDesfireCardApplicationMasterKeyType { get; set; }
		public string DefaultDesfireCardReadKey { get; set; }
		public string DefaultDesfireCardReadKeyType { get; set; }
		public string DefaultDesfireCardWriteKey { get; set; }
		public string DefaultDesfireCardWriteKeyType { get; set; }
		public string DefaultReaderName { get; set; }
		public string DefaultReaderProvider { get; set; }
		public string DefaultLanguage { get; set; }

		#endregion
		
		
		CustomConverter converter;
		
		#region constructors
		public SettingsReaderWriter()
		{
			converter = new CustomConverter();
			
			appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RFiDGear");

			// Check if folder exists and if not, create it
			if(!Directory.Exists(appDataPath))
				Directory.CreateDirectory(appDataPath);
			
			DefaultClassicCardQuickCheckKeys = new List<string>();
			DefaultClassicCardKeysAKeys = new List<string>();
			DefaultClassicCardKeysBKeys = new List<string>();
			DefaultClassicCardAccessBits = new List<string>();
			
			this.readSettings();
		}
		#endregion
		
		#region methods
		public void saveSettings()
		{
			try {
				
				XmlDocument doc = new XmlDocument();
				doc.Load(Path.Combine(appDataPath,_settingsFileFileName));
				
				if (doc.SelectSingleNode("//defaultClassicCardSectorTrailers") == null) {
					XmlElement classicCardKey = doc.CreateElement("defaultClassicCardSectorTrailers");
					for (int i = 0; i <= 31; i++) {
						XmlAttribute classicCardSectorTrailer = doc.CreateAttribute(string.Format("SectorTrailer{0:d2}", i));
						classicCardSectorTrailer.Value = _classicCardDefaultSectorTrailer;
						classicCardKey.Attributes.Append(classicCardSectorTrailer);
					}
					doc.DocumentElement.AppendChild(classicCardKey);
					doc.Save(Path.Combine(appDataPath,_settingsFileFileName));
				}
				
				if(doc.SelectSingleNode("//defaultQuickCheckKeys") == null) {
					string[] quickCheckKeysTemp = _classicCardDefaultQuickCheckKeys.Split(',');
					XmlElement classicCardQuickCheckKeysElem = doc.CreateElement("defaultQuickCheckKeys");
					for (int i = 0; i < quickCheckKeysTemp.Length; i++) {
						XmlAttribute classicCardQuickCheckKeyAttr = doc.CreateAttribute(string.Format("QuickCheckKey{0:d2}", i));
						classicCardQuickCheckKeyAttr.Value = quickCheckKeysTemp[i];
						classicCardQuickCheckKeysElem.Attributes.Append(classicCardQuickCheckKeyAttr);
					}
					doc.DocumentElement.AppendChild(classicCardQuickCheckKeysElem);
					doc.Save(Path.Combine(appDataPath,_settingsFileFileName));
				}
				
				if (doc.SelectSingleNode("//defaultReader") == null) {
					XmlElement defaultReaderElem = doc.CreateElement("defaultReader");
					XmlAttribute defaultReaderAttr = doc.CreateAttribute("readerName");
					XmlAttribute defaultReaderProviderAttr = doc.CreateAttribute("readerProvider");
					defaultReaderAttr.Value = _defaultReaderName;
					defaultReaderProviderAttr.Value = _defaultReaderProvider;
					defaultReaderElem.Attributes.Append(defaultReaderAttr);
					defaultReaderElem.Attributes.Append(defaultReaderProviderAttr);
					doc.DocumentElement.AppendChild(defaultReaderElem);
					doc.Save(Path.Combine(appDataPath,_settingsFileFileName));
				}
				
				if (doc.SelectSingleNode("//defaultLanguage") == null) {
					XmlElement defaultLanguageElem = doc.CreateElement("defaultLanguage");
					XmlAttribute defaultLanguageAttr = doc.CreateAttribute("lang");
					defaultLanguageAttr.Value = _defaultLanguage;
					defaultLanguageElem.Attributes.Append(defaultLanguageAttr);
					doc.DocumentElement.AppendChild(defaultLanguageElem);
					doc.Save(Path.Combine(appDataPath,_settingsFileFileName));
				}

				if (doc.SelectSingleNode("//defaultDesfireKeys") == null) {
					XmlElement defaultDesfireKeysElem = doc.CreateElement("defaultDesfireKeys");
					
					XmlAttribute desFireCardCardMasterKeyAttr = doc.CreateAttribute("defaultDesfireCard_CardMasterKey");
					XmlAttribute desFireCardCardMasterKeyTypeAttr = doc.CreateAttribute("defaultDesfireCard_CardMasterKeyType");
					
					XmlAttribute desFireCardApplicationMasterKeyAttr = doc.CreateAttribute("defaultDesfireCard_ApplicationMasterKey");
					XmlAttribute desFireCardApplicationMasterKeyTypeAttr = doc.CreateAttribute("defaultDesfireCard_ApplicationMasterKeyType");
					
					XmlAttribute desFireCardReadKeyAttr = doc.CreateAttribute("defaultDesfireCard_ReadKey");
					XmlAttribute desFireCardReadKeyTypeAttr = doc.CreateAttribute("defaultDesfireCard_ReadKeyType");
					
					XmlAttribute desFireCardWriteKeyAttr = doc.CreateAttribute("defaultDesfireCard_WriteKey");
					XmlAttribute desFireCardWriteKeyTypeAttr = doc.CreateAttribute("defaultDesfireCard_WriteKeyType");
					
					desFireCardCardMasterKeyAttr.Value = _desfireCardCardMasterKey;
					desFireCardCardMasterKeyTypeAttr.Value = converter._constDesfireCardKeyType[Array.IndexOf(converter.libLogicalAccessKeyTypeEnumConverter,(int)DESFireKeyType.DF_KEY_AES)];
					
					desFireCardApplicationMasterKeyAttr.Value = _desfireCardApplicationMasterKey;
					desFireCardApplicationMasterKeyTypeAttr.Value = converter._constDesfireCardKeyType[Array.IndexOf(converter.libLogicalAccessKeyTypeEnumConverter,(int)DESFireKeyType.DF_KEY_AES)];;
					
					desFireCardReadKeyAttr.Value = _desfireCardReadKey;
					desFireCardReadKeyTypeAttr.Value = converter._constDesfireCardKeyType[Array.IndexOf(converter.libLogicalAccessKeyTypeEnumConverter,(int)DESFireKeyType.DF_KEY_AES)];;
					
					desFireCardWriteKeyAttr.Value = _desfireCardWriteKey;
					desFireCardWriteKeyTypeAttr.Value = converter._constDesfireCardKeyType[Array.IndexOf(converter.libLogicalAccessKeyTypeEnumConverter,(int)DESFireKeyType.DF_KEY_AES)];;
					
					defaultDesfireKeysElem.Attributes.Append(desFireCardCardMasterKeyAttr);
					defaultDesfireKeysElem.Attributes.Append(desFireCardCardMasterKeyTypeAttr);
					
					defaultDesfireKeysElem.Attributes.Append(desFireCardApplicationMasterKeyAttr);
					defaultDesfireKeysElem.Attributes.Append(desFireCardApplicationMasterKeyTypeAttr);
					
					defaultDesfireKeysElem.Attributes.Append(desFireCardReadKeyAttr);
					defaultDesfireKeysElem.Attributes.Append(desFireCardReadKeyTypeAttr);
					
					defaultDesfireKeysElem.Attributes.Append(desFireCardWriteKeyAttr);
					defaultDesfireKeysElem.Attributes.Append(desFireCardWriteKeyTypeAttr);
					
					doc.DocumentElement.AppendChild(defaultDesfireKeysElem);
					doc.Save(Path.Combine(appDataPath,_settingsFileFileName));
				}

				if(doc.SelectSingleNode("//defaultReader") == null) {
					XmlElement defaultReaderElem = doc.CreateElement("defaultReader");
					XmlAttribute defaultReaderNameAttr = doc.CreateAttribute("readerName");
					XmlAttribute defaultReaderProviderAttr = doc.CreateAttribute("readerProvider");
					defaultReaderNameAttr.Value = _defaultReaderName;
					defaultReaderProviderAttr.Value = _defaultReaderProvider;
					defaultReaderElem.Attributes.Append(defaultReaderNameAttr);
					defaultReaderElem.Attributes.Append(defaultReaderProviderAttr);
					doc.DocumentElement.AppendChild(defaultReaderElem);
					doc.Save(Path.Combine(appDataPath,_settingsFileFileName));
				}
				
				if(doc.SelectSingleNode("//defaultLanguage") == null) {
					XmlElement defaultLanguageElem = doc.CreateElement("defaultLanguage");
					XmlAttribute defaultLanguageAttr = doc.CreateAttribute("lang");
					defaultLanguageAttr.Value = _defaultLanguage;
					defaultLanguageElem.Attributes.Append(defaultLanguageAttr);
					doc.DocumentElement.AppendChild(defaultLanguageElem);
					doc.Save(Path.Combine(appDataPath,_settingsFileFileName));
				}
				
				readSettings();
				
			} catch (XmlException e) {
				MessageBox.Show(string.Format("Fehler: Kann die Einstellungen-Datei ({0}) nicht lesen.\n\n{1}", Path.Combine(appDataPath,_settingsFileFileName), e), "Fehler",
				                MessageBoxButton.OK, MessageBoxImage.Error);
				Environment.Exit(0);
			}
		}
		
		public void saveSettings(string reader, string[] aKeys, string[] bKeys, string language)
		{
			
			try {
				
				XmlDocument doc = new XmlDocument();
				doc.Load(Path.Combine(appDataPath,Path.Combine(appDataPath,_settingsFileFileName)));
				
				if (doc.SelectSingleNode("//defaultClassicCardSectorTrailers") == null) {
					XmlElement classicCardKey = doc.CreateElement("defaultClassicCardSectorTrailers");
					for (int i = 0; i <= 31; i++) {
						XmlAttribute classicCardSectorTrailer = doc.CreateAttribute(string.Format("SectorTrailer{0:d2}", i));
						classicCardSectorTrailer.Value = _classicCardDefaultSectorTrailer;
						classicCardKey.Attributes.Append(classicCardSectorTrailer);
					}
					doc.DocumentElement.AppendChild(classicCardKey);
					doc.Save(Path.Combine(appDataPath,_settingsFileFileName));
				}
				
				if (doc.SelectSingleNode("//defaultReader") == null) {
					XmlElement defaultReader = doc.CreateElement("defaultReader");
					XmlAttribute defaultReaderID = doc.CreateAttribute("readerName");
					XmlAttribute defaultReaderProvider = doc.CreateAttribute("readerProvider");
					defaultReaderID.Value = _defaultReaderName;
					defaultReader.Attributes.Append(defaultReaderID);
					defaultReader.Attributes.Append(defaultReaderProvider);
					doc.DocumentElement.AppendChild(defaultReader);
					doc.Save(Path.Combine(appDataPath,_settingsFileFileName));
				}
				
				if (doc.SelectSingleNode("//defaultLanguage") == null) {
					XmlElement defaultLanguage = doc.CreateElement("defaultLanguage");
					XmlAttribute defaultLanguageID = doc.CreateAttribute("lang");
					defaultLanguageID.Value = _defaultLanguage;
					defaultLanguage.Attributes.Append(defaultLanguageID);
					doc.DocumentElement.AppendChild(defaultLanguage);
					doc.Save(Path.Combine(appDataPath,_settingsFileFileName));
				}
				
				if (doc.SelectSingleNode("//defaultDesfireKeys") == null) {
					XmlElement defaultDesfireKeys = doc.CreateElement("defaultDesfireKeys");
					
					
					XmlAttribute desFireCardCardMasterKeyKeyID = doc.CreateAttribute("defaultDesfireCard_CardMasterKey");
					XmlAttribute desFireCardCardMasterKeyTypeKeyID = doc.CreateAttribute("defaultDesfireCard_CardMasterKeyType");
					
					XmlAttribute desFireCardApplicationMasterKeyKeyID = doc.CreateAttribute("defaultDesfireCard_ApplicationMasterKey");
					XmlAttribute desFireCardApplicationMasterKeyTypeKeyID = doc.CreateAttribute("defaultDesfireCard_ApplicationMasterKeyType");
					
					XmlAttribute desFireCardReadKeyKeyID = doc.CreateAttribute("defaultDesfireCard_ReadKey");
					XmlAttribute desFireCardReadKeyTypeKeyID = doc.CreateAttribute("defaultDesfireCard_ReadKeyType");
					
					XmlAttribute desFireCardWriteKeyKeyID = doc.CreateAttribute("defaultDesfireCard_WriteKey");
					XmlAttribute desFireCardWriteKeyTypeKeyID = doc.CreateAttribute("defaultDesfireCard_WriteKeyType");
					
					desFireCardCardMasterKeyKeyID.Value = _desfireCardCardMasterKey;
					desFireCardCardMasterKeyTypeKeyID.Value = converter._constDesfireCardKeyType[Array.IndexOf(converter.libLogicalAccessKeyTypeEnumConverter,(int)DESFireKeyType.DF_KEY_AES)];
					
					desFireCardApplicationMasterKeyKeyID.Value = _desfireCardApplicationMasterKey;
					desFireCardApplicationMasterKeyTypeKeyID.Value = converter._constDesfireCardKeyType[Array.IndexOf(converter.libLogicalAccessKeyTypeEnumConverter,(int)DESFireKeyType.DF_KEY_AES)];
					
					desFireCardReadKeyKeyID.Value = _desfireCardReadKey;
					desFireCardReadKeyTypeKeyID.Value = converter._constDesfireCardKeyType[Array.IndexOf(converter.libLogicalAccessKeyTypeEnumConverter,(int)DESFireKeyType.DF_KEY_AES)];
					
					desFireCardWriteKeyKeyID.Value = _desfireCardWriteKey;
					desFireCardWriteKeyTypeKeyID.Value = converter._constDesfireCardKeyType[Array.IndexOf(converter.libLogicalAccessKeyTypeEnumConverter,(int)DESFireKeyType.DF_KEY_AES)];
					
					defaultDesfireKeys.Attributes.Append(desFireCardCardMasterKeyKeyID);
					defaultDesfireKeys.Attributes.Append(desFireCardCardMasterKeyTypeKeyID);
					
					defaultDesfireKeys.Attributes.Append(desFireCardApplicationMasterKeyKeyID);
					defaultDesfireKeys.Attributes.Append(desFireCardApplicationMasterKeyTypeKeyID);
					
					defaultDesfireKeys.Attributes.Append(desFireCardReadKeyKeyID);
					defaultDesfireKeys.Attributes.Append(desFireCardReadKeyTypeKeyID);
					
					defaultDesfireKeys.Attributes.Append(desFireCardWriteKeyKeyID);
					defaultDesfireKeys.Attributes.Append(desFireCardWriteKeyTypeKeyID);
					
					doc.DocumentElement.AppendChild(defaultDesfireKeys);
					doc.Save(Path.Combine(appDataPath,_settingsFileFileName));
				}
				
				XmlNode node = doc.SelectSingleNode("//defaultClassicCardSectorTrailers");
				if ((aKeys != null) && (bKeys != null)) {
					
					for (int i = 0; i < aKeys.Length; i++) {
						oldKeyA = node.Attributes[string.Format("keyA{0:d2}", i)].Value;
						oldKeyB = node.Attributes[string.Format("keyB{0:d2}", i)].Value;
						
						if (aKeys != null) {
							if (oldKeyA != aKeys[i]) {
								node.Attributes[string.Format("keyA{0:d2}", i)].Value = aKeys[i];

								doc.Save(Path.Combine(appDataPath,_settingsFileFileName));
							}
						}
						
						if (bKeys != null) {
							if (oldKeyB != bKeys[i]) {
								node.Attributes[string.Format("keyB{0:d2}", i)].Value = bKeys[i];

								doc.Save(Path.Combine(appDataPath,_settingsFileFileName));
							}
						}
					}
				}

				node = doc.SelectSingleNode("//defaultReader");
				oldReaderName = node.Attributes["readerName"].Value;
				oldReaderProvider = node.Attributes["readerProvider"].Value;
				if (! (String.IsNullOrEmpty(oldReaderName) || String.IsNullOrEmpty(oldReaderProvider))) {
					
					node.Attributes["readerName"].Value = oldReaderName;
					node.Attributes["readerProvider"].Value = oldReaderProvider;

					doc.Save(Path.Combine(appDataPath,_settingsFileFileName));
				}
				
				node = doc.SelectSingleNode("//defaultLanguage");
				oldLanguage = node.Attributes["lang"].Value;
				if ((oldLanguage != language) & language != null) {
					
					node.Attributes["lang"].Value = language;

					doc.Save(Path.Combine(appDataPath,_settingsFileFileName));
				}
				
			} catch (XmlException e) {
				MessageBox.Show(string.Format("Fehler: Kann die {0}-Datei nicht lesen:\n\n{1}", Path.Combine(appDataPath,_settingsFileFileName), e), "Fehler",
				                MessageBoxButton.OK, MessageBoxImage.Error);
				Environment.Exit(0);
			}
		}
		
		public void saveSettings(string language)
		{
			
			try {
				
				XmlDocument doc = new XmlDocument();
				XmlNode node = doc.SelectSingleNode("//defaultClassicCardSectorTrailers");
				doc.Load(Path.Combine(appDataPath,_settingsFileFileName));
				
				if (doc.SelectSingleNode("//defaultClassicCardSectorTrailers") == null) {
					XmlElement classicCardKey = doc.CreateElement("defaultClassicCardSectorTrailers");
					for (int i = 0; i <= 31; i++) {
						XmlAttribute classicCardKeyAKeyID = doc.CreateAttribute(string.Format("keyA{0:d2}", i));
						
						classicCardKeyAKeyID.Value = _classicCardDefaultSectorTrailer;

						classicCardKey.Attributes.Append(classicCardKeyAKeyID);

					}
					doc.DocumentElement.AppendChild(classicCardKey);
					doc.Save(Path.Combine(appDataPath,_settingsFileFileName));
				}
				
				if (doc.SelectSingleNode("//defaultLanguage") == null) {
					XmlElement defaultLanguage = doc.CreateElement("defaultLanguage");
					XmlAttribute defaultLanguageID = doc.CreateAttribute("lang");
					defaultLanguageID.Value = _defaultLanguage;
					defaultLanguage.Attributes.Append(defaultLanguageID);
					doc.DocumentElement.AppendChild(defaultLanguage);
					doc.Save(Path.Combine(appDataPath,_settingsFileFileName));
				}
				
				node = doc.SelectSingleNode("//defaultLanguage");
				oldLanguage = node.Attributes["lang"].Value;
				if ((oldLanguage != language) & language != null) {
					
					node.Attributes["lang"].Value = language;

					doc.Save(Path.Combine(appDataPath,_settingsFileFileName));
				}
				
			} catch (XmlException e) {
				MessageBox.Show(string.Format("Fehler: Kann die {0}-Datei nicht lesen:\n\n{1}", Path.Combine(appDataPath,_settingsFileFileName), e), "Fehler",
				                MessageBoxButton.OK, MessageBoxImage.Error);
				Environment.Exit(0);
			}
		}

		public void saveSettings(string readerName, string readerProviderByName)
		{
			
			try {
				
				XmlDocument doc = new XmlDocument();
				doc.Load(Path.Combine(appDataPath,_settingsFileFileName));
				
				if (doc.SelectSingleNode("//defaultReader") == null) {
					XmlElement defaultReader = doc.CreateElement("defaultReader");
					XmlAttribute defaultReaderID = doc.CreateAttribute("readerName");
					XmlAttribute defaultReaderProvider = doc.CreateAttribute("readerProvider");
					defaultReaderID.Value = _defaultReaderName;
					defaultReader.Attributes.Append(defaultReaderID);
					defaultReader.Attributes.Append(defaultReaderProvider);
					doc.DocumentElement.AppendChild(defaultReader);
					doc.Save(Path.Combine(appDataPath,_settingsFileFileName));
				}
				
				XmlNode node = doc.SelectSingleNode("//defaultReader");
				oldReaderName = node.Attributes["readerName"].Value;
				oldReaderProvider = node.Attributes["readerProvider"].Value;
				if (! (String.IsNullOrEmpty(readerName) && String.IsNullOrEmpty(readerProviderByName))) {
					
					node.Attributes["readerName"].Value = readerName;
					node.Attributes["readerProvider"].Value = readerProviderByName;

					doc.Save(Path.Combine(appDataPath,_settingsFileFileName));
				}
				
			} catch (XmlException e) {
				MessageBox.Show(string.Format("Fehler: Kann die {0}-Datei nicht lesen:\n\n{1}", Path.Combine(appDataPath,_settingsFileFileName), e), "Fehler",
				                MessageBoxButton.OK, MessageBoxImage.Error);
				Environment.Exit(0);
			}
		}
		
		public void readSettings()
		{
			if (!File.Exists(Path.Combine(appDataPath,_settingsFileFileName))) {
				XmlWriter writer = XmlWriter.Create(Path.Combine(appDataPath,_settingsFileFileName));
				writer.WriteStartDocument();
				writer.WriteStartElement("RFIDGearSettings");
				
				writer.WriteEndElement();
				writer.Close();
				
				saveSettings();
				
			} else if (File.Exists(Path.Combine(appDataPath,_settingsFileFileName))) {
				
				XmlDocument doc = new XmlDocument();
				doc.Load(Path.Combine(appDataPath,_settingsFileFileName));
				
				try {
					XmlNode node = doc.SelectSingleNode("//defaultClassicCardSectorTrailers");
					
					foreach(XmlAttribute atr in node.Attributes) {
						string[] sectorTrailerTemp = atr.Value.Split(',');
						DefaultClassicCardKeysAKeys.Add(sectorTrailerTemp[0]);
						DefaultClassicCardAccessBits.Add(sectorTrailerTemp[1]);
						DefaultClassicCardKeysBKeys.Add(sectorTrailerTemp[2]);
					}
					
					node = doc.SelectSingleNode("//defaultQuickCheckKeys");
					
					foreach(XmlAttribute atr in node.Attributes) {
						DefaultClassicCardQuickCheckKeys.Add(atr.Value);
					}
					
					node = doc.SelectSingleNode("//defaultReader");
					DefaultReaderName = node.Attributes["readerName"].Value;
					DefaultReaderProvider = node.Attributes["readerProvider"].Value;
					
					node = doc.SelectSingleNode("//defaultLanguage");
					DefaultLanguage = node.Attributes["lang"].Value;
					
					node = doc.SelectSingleNode("//defaultDesfireKeys");
					DefaultDesfireCardCardMasterKey = node.Attributes["defaultDesfireCard_CardMasterKey"].Value;
					DefaultDesfireCardCardMasterKeyType = node.Attributes["defaultDesfireCard_CardMasterKeyType"].Value;
					
					DefaultDesfireCardApplicationMasterKey = node.Attributes["defaultDesfireCard_ApplicationMasterKey"].Value;
					DefaultDesfireCardApplicationMasterKeyType = node.Attributes["defaultDesfireCard_ApplicationMasterKeyType"].Value;
					
					DefaultDesfireCardReadKey = node.Attributes["defaultDesfireCard_ReadKey"].Value;
					DefaultDesfireCardReadKeyType = node.Attributes["defaultDesfireCard_ReadKeyType"].Value;
					
					DefaultDesfireCardWriteKey = node.Attributes["defaultDesfireCard_WriteKey"].Value;
					DefaultDesfireCardWriteKeyType = node.Attributes["defaultDesfireCard_WriteKeyType"].Value;
					
					
				} catch (XmlException e) {
					MessageBox.Show(string.Format("Fehler: Kann die {0}-Datei nicht lesen:\n\n{1}", Path.Combine(appDataPath,_settingsFileFileName), e), "Fehler",
					                MessageBoxButton.OK, MessageBoxImage.Error);
					Environment.Exit(0);
				}
				
			}
		}
		#endregion
	}
}
