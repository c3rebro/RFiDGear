/*
 * Created by SharpDevelop.
 * User: rotts
 * Date: 26.07.2013
 * Time: 15:38
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
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
		const string settingsFileName = "settings.xml";
		const string _constClassicCardKeyAKey = "FFFFFFFFFFFF";
		const string _constClassicCardKeyBKey = "FFFFFFFFFFFF";
		const string _constDefaultReader = "";
		const string _constDefaultReaderProvider = "PCSC";
		const string _constDefaultLanguage = "german";
		const string _constDesfireCardCardMasterKey = "00000000000000000000000000000000";
		const string _constDesfireCardApplicationMasterKey = "00000000000000000000000000000000";
		const string _constDesfireCardReadKey = "00000000000000000000000000000000";
		const string _constDesfireCardWriteKey = "00000000000000000000000000000000";

		public string[] _defaultClassicCardKeysAKeys { get; set; }
		public string[] _defaultClassicCardKeysBKeys { get; set; }
		public string _defaultDesfireCardCardMasterKey { get; set; }
		public string _defaultDesfireCardCardMasterKeyType { get; set; }
		public string _defaultDesfireCardApplicationMasterKey { get; set; }
		public string _defaultDesfireCardApplicationMasterKeyType { get; set; }
		public string _defaultDesfireCardReadKey { get; set; }
		public string _defaultDesfireCardReadKeyType { get; set; }
		public string _defaultDesfireCardWriteKey { get; set; }
		public string _defaultDesfireCardWriteKeyType { get; set; }
		public string _defaultReader { get; set; }
		public string _defaultReaderProvider { get; set; }
		public string _defaultLanguage { get; set; }
		string _oldReader;
		string _oldReaderProvider;
		string _oldDesfireCardCardMasterKey;
		string _oldDesfireCardApplicationMasterKey;
		string _oldDesfireCardReadKey;
		string _oldDesfireCardWriteKey;
		string _oldKeyA;
		string _oldKeyB;
		string _oldLanguage;
		bool _loadKeysAuto = false;
		bool _oldLoadKeysAuto;

		#endregion
		
		
		helperClass converter;
		
		#region constructors
		public SettingsReaderWriter()
		{
			converter = new helperClass();
			this.readSettings();
		}
		#endregion
		
		#region methods
		public void saveSettings()
		{
			try {
				
				XmlDocument doc = new XmlDocument();
				doc.Load(settingsFileName);
				
				if (doc.SelectSingleNode("//defaultClassicCardKeys") == null) {
					XmlElement classicCardKey = doc.CreateElement("defaultClassicCardKeys");
					for (int i = 0; i <= 31; i++) {
						XmlAttribute classicCardKeyAKeyID = doc.CreateAttribute(string.Format("keyA{0:d2}", i));
						XmlAttribute classicCardKeyBKeyID = doc.CreateAttribute(string.Format("keyB{0:d2}", i));
						classicCardKeyAKeyID.Value = _constClassicCardKeyAKey;
						classicCardKeyBKeyID.Value = _constClassicCardKeyBKey;
						classicCardKey.Attributes.Append(classicCardKeyAKeyID);
						classicCardKey.Attributes.Append(classicCardKeyBKeyID);
					}
					doc.DocumentElement.AppendChild(classicCardKey);
					doc.Save(settingsFileName);
				}
				
				if (doc.SelectSingleNode("//defaultReader") == null) {
					XmlElement defaultReader = doc.CreateElement("defaultReader");
					XmlAttribute defaultReaderID = doc.CreateAttribute("readerName");
					XmlAttribute defaultReaderProvider = doc.CreateAttribute("readerProvider");
					defaultReaderID.Value = _constDefaultReader;
					defaultReader.Attributes.Append(defaultReaderID);
					defaultReader.Attributes.Append(defaultReaderProvider);
					doc.DocumentElement.AppendChild(defaultReader);
					doc.Save(settingsFileName);
				}
				
				if (doc.SelectSingleNode("//defaultLanguage") == null) {
					XmlElement defaultLanguage = doc.CreateElement("defaultLanguage");
					XmlAttribute defaultLanguageID = doc.CreateAttribute("lang");
					defaultLanguageID.Value = _constDefaultLanguage;
					defaultLanguage.Attributes.Append(defaultLanguageID);
					doc.DocumentElement.AppendChild(defaultLanguage);
					doc.Save(settingsFileName);
				}
				
				if (doc.SelectSingleNode("//autoLoadKeys") == null) {
					XmlElement autoLoadKeys = doc.CreateElement("autoLoadKeys");
					XmlAttribute autoLoadKeysID = doc.CreateAttribute("auto");
					autoLoadKeysID.Value = Convert.ToString(_loadKeysAuto);
					autoLoadKeys.Attributes.Append(autoLoadKeysID);
					doc.DocumentElement.AppendChild(autoLoadKeys);
					doc.Save(settingsFileName);
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
					
					desFireCardCardMasterKeyKeyID.Value = _constDesfireCardCardMasterKey;
					desFireCardCardMasterKeyTypeKeyID.Value = converter._constDesfireCardKeyType[Array.IndexOf(converter.libLogicalAccessKeyTypeEnumConverter,(int)DESFireKeyType.DF_KEY_AES)];
					
					desFireCardApplicationMasterKeyKeyID.Value = _constDesfireCardApplicationMasterKey;
					desFireCardApplicationMasterKeyTypeKeyID.Value = converter._constDesfireCardKeyType[Array.IndexOf(converter.libLogicalAccessKeyTypeEnumConverter,(int)DESFireKeyType.DF_KEY_AES)];;
					
					desFireCardReadKeyKeyID.Value = _constDesfireCardReadKey;
					desFireCardReadKeyTypeKeyID.Value = converter._constDesfireCardKeyType[Array.IndexOf(converter.libLogicalAccessKeyTypeEnumConverter,(int)DESFireKeyType.DF_KEY_AES)];;
					
					desFireCardWriteKeyKeyID.Value = _constDesfireCardWriteKey;
					desFireCardWriteKeyTypeKeyID.Value = converter._constDesfireCardKeyType[Array.IndexOf(converter.libLogicalAccessKeyTypeEnumConverter,(int)DESFireKeyType.DF_KEY_AES)];;
					
					defaultDesfireKeys.Attributes.Append(desFireCardCardMasterKeyKeyID);
					defaultDesfireKeys.Attributes.Append(desFireCardCardMasterKeyTypeKeyID);
					
					defaultDesfireKeys.Attributes.Append(desFireCardApplicationMasterKeyKeyID);
					defaultDesfireKeys.Attributes.Append(desFireCardApplicationMasterKeyTypeKeyID);
					
					defaultDesfireKeys.Attributes.Append(desFireCardReadKeyKeyID);
					defaultDesfireKeys.Attributes.Append(desFireCardReadKeyTypeKeyID);
					
					defaultDesfireKeys.Attributes.Append(desFireCardWriteKeyKeyID);
					defaultDesfireKeys.Attributes.Append(desFireCardWriteKeyTypeKeyID);
					
					doc.DocumentElement.AppendChild(defaultDesfireKeys);
					doc.Save(settingsFileName);
				}
				
				XmlNode node = doc.SelectSingleNode("//defaultClassicCardKeys");
				
				if ((_defaultClassicCardKeysAKeys != null) && (_defaultClassicCardKeysBKeys != null)) {
					
					for (int i = 0; i < _defaultClassicCardKeysAKeys.Length; i++) {
						_oldKeyA = node.Attributes[string.Format("keyA{0:d2}", i)].Value;
						_oldKeyB = node.Attributes[string.Format("keyB{0:d2}", i)].Value;
						
						if (_defaultClassicCardKeysAKeys != null) {
							if (_oldKeyA != _defaultClassicCardKeysAKeys[i]) {
								node.Attributes[string.Format("keyA{0:d2}", i)].Value = _defaultClassicCardKeysAKeys[i];

								doc.Save(settingsFileName);
							}
						}
						
						if (_defaultClassicCardKeysBKeys != null) {
							if (_oldKeyB != _defaultClassicCardKeysBKeys[i]) {
								node.Attributes[string.Format("keyB{0:d2}", i)].Value = _defaultClassicCardKeysBKeys[i];

								doc.Save(settingsFileName);
							}
						}
					}
				}

				node = doc.SelectSingleNode("//defaultReader");
				_oldReader = node.Attributes["readerName"].Value;
				_oldReaderProvider = node.Attributes["readerProvider"].Value;
				if (! (String.IsNullOrEmpty(_oldReader) || String.IsNullOrEmpty(_oldReaderProvider))) {
					
					node.Attributes["readerName"].Value = _oldReader;
					node.Attributes["readerProvider"].Value = _oldReaderProvider;

					doc.Save(settingsFileName);
				}
				
				node = doc.SelectSingleNode("//defaultLanguage");
				_oldLanguage = node.Attributes["lang"].Value;
				if ((_oldLanguage != "") & _oldLanguage != null) {
					
					node.Attributes["lang"].Value = _oldLanguage;

					doc.Save(settingsFileName);
				}
				
				node = doc.SelectSingleNode("//autoLoadKeys");
				_oldLoadKeysAuto = Convert.ToBoolean(node.Attributes["auto"].Value);
				if (_oldLoadKeysAuto != _loadKeysAuto) {
					
					node.Attributes["auto"].Value = Convert.ToString(_loadKeysAuto);

					doc.Save(settingsFileName);
				}
				
			} catch (XmlException e) {
				MessageBox.Show(string.Format("Fehler: Kann die {0}-Datei nicht lesen:\n\n{1}", settingsFileName, e), "Fehler",
				                MessageBoxButton.OK, MessageBoxImage.Error);
				Environment.Exit(0);
			}
		}
		
		public void saveSettings(string reader, string[] aKeys, string[] bKeys, string language)
		{
			
			try {
				
				XmlDocument doc = new XmlDocument();
				doc.Load(settingsFileName);
				
				if (doc.SelectSingleNode("//defaultClassicCardKeys") == null) {
					XmlElement defaultClassicCardKey = doc.CreateElement("defaultClassicCardKeys");
					for (int i = 0; i <= 31; i++) {
						XmlAttribute classicCardKeyAKeyID = doc.CreateAttribute(string.Format("keyA{0:d2}", i));
						XmlAttribute classicCardKeyBKeyID = doc.CreateAttribute(string.Format("keyB{0:d2}", i));
						classicCardKeyAKeyID.Value = _constClassicCardKeyAKey;
						classicCardKeyBKeyID.Value = _constClassicCardKeyBKey;
						defaultClassicCardKey.Attributes.Append(classicCardKeyAKeyID);
						defaultClassicCardKey.Attributes.Append(classicCardKeyBKeyID);
					}
					doc.DocumentElement.AppendChild(defaultClassicCardKey);
					doc.Save(settingsFileName);
				}
				
				if (doc.SelectSingleNode("//defaultReader") == null) {
					XmlElement defaultReader = doc.CreateElement("defaultReader");
					XmlAttribute defaultReaderID = doc.CreateAttribute("readerName");
					XmlAttribute defaultReaderProvider = doc.CreateAttribute("readerProvider");
					defaultReaderID.Value = _constDefaultReader;
					defaultReader.Attributes.Append(defaultReaderID);
					defaultReader.Attributes.Append(defaultReaderProvider);
					doc.DocumentElement.AppendChild(defaultReader);
					doc.Save(settingsFileName);
				}
				
				if (doc.SelectSingleNode("//defaultLanguage") == null) {
					XmlElement defaultLanguage = doc.CreateElement("defaultLanguage");
					XmlAttribute defaultLanguageID = doc.CreateAttribute("lang");
					defaultLanguageID.Value = _constDefaultLanguage;
					defaultLanguage.Attributes.Append(defaultLanguageID);
					doc.DocumentElement.AppendChild(defaultLanguage);
					doc.Save(settingsFileName);
				}
				
				if (doc.SelectSingleNode("//autoLoadKeys") == null) {
					XmlElement autoLoadKeys = doc.CreateElement("autoLoadKeys");
					XmlAttribute autoLoadKeysID = doc.CreateAttribute("auto");
					autoLoadKeysID.Value = Convert.ToString(_loadKeysAuto);
					autoLoadKeys.Attributes.Append(autoLoadKeysID);
					doc.DocumentElement.AppendChild(autoLoadKeys);
					doc.Save(settingsFileName);
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
					
					desFireCardCardMasterKeyKeyID.Value = _constDesfireCardCardMasterKey;
					desFireCardCardMasterKeyTypeKeyID.Value = converter._constDesfireCardKeyType[Array.IndexOf(converter.libLogicalAccessKeyTypeEnumConverter,(int)DESFireKeyType.DF_KEY_AES)];
					
					desFireCardApplicationMasterKeyKeyID.Value = _constDesfireCardApplicationMasterKey;
					desFireCardApplicationMasterKeyTypeKeyID.Value = converter._constDesfireCardKeyType[Array.IndexOf(converter.libLogicalAccessKeyTypeEnumConverter,(int)DESFireKeyType.DF_KEY_AES)];
					
					desFireCardReadKeyKeyID.Value = _constDesfireCardReadKey;
					desFireCardReadKeyTypeKeyID.Value = converter._constDesfireCardKeyType[Array.IndexOf(converter.libLogicalAccessKeyTypeEnumConverter,(int)DESFireKeyType.DF_KEY_AES)];
					
					desFireCardWriteKeyKeyID.Value = _constDesfireCardWriteKey;
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
					doc.Save(settingsFileName);
				}
				
				XmlNode node = doc.SelectSingleNode("//defaultClassicCardKeys");
				if ((aKeys != null) && (bKeys != null)) {
					
					for (int i = 0; i < aKeys.Length; i++) {
						_oldKeyA = node.Attributes[string.Format("keyA{0:d2}", i)].Value;
						_oldKeyB = node.Attributes[string.Format("keyB{0:d2}", i)].Value;
						
						if (aKeys != null) {
							if (_oldKeyA != aKeys[i]) {
								node.Attributes[string.Format("keyA{0:d2}", i)].Value = aKeys[i];

								doc.Save(settingsFileName);
							}
						}
						
						if (bKeys != null) {
							if (_oldKeyB != bKeys[i]) {
								node.Attributes[string.Format("keyB{0:d2}", i)].Value = bKeys[i];

								doc.Save(settingsFileName);
							}
						}
					}
				}

				node = doc.SelectSingleNode("//defaultReader");
				_oldReader = node.Attributes["readerName"].Value;
				_oldReaderProvider = node.Attributes["readerProvider"].Value;
				if (! (String.IsNullOrEmpty(_oldReader) || String.IsNullOrEmpty(_oldReaderProvider))) {
					
					node.Attributes["readerName"].Value = _oldReader;
					node.Attributes["readerProvider"].Value = _oldReaderProvider;

					doc.Save(settingsFileName);
				}
				
				node = doc.SelectSingleNode("//defaultLanguage");
				_oldLanguage = node.Attributes["lang"].Value;
				if ((_oldLanguage != language) & language != null) {
					
					node.Attributes["lang"].Value = language;

					doc.Save(settingsFileName);
				}
				
				node = doc.SelectSingleNode("//autoLoadKeys");
				_oldLoadKeysAuto = Convert.ToBoolean(node.Attributes["auto"].Value);
				if (_oldLoadKeysAuto != _loadKeysAuto) {
					
					node.Attributes["auto"].Value = Convert.ToString(_loadKeysAuto);

					doc.Save(settingsFileName);
				}
				
			} catch (XmlException e) {
				MessageBox.Show(string.Format("Fehler: Kann die {0}-Datei nicht lesen:\n\n{1}", settingsFileName, e), "Fehler",
				                MessageBoxButton.OK, MessageBoxImage.Error);
				Environment.Exit(0);
			}
		}
		
		public void saveSettings(string language)
		{
			
			try {
				
				XmlDocument doc = new XmlDocument();
				XmlNode node = doc.SelectSingleNode("//defaultClassicCardKeys");
				doc.Load(settingsFileName);
				
				if (doc.SelectSingleNode("//defaultClassicCardKeys") == null) {
					XmlElement classicCardKey = doc.CreateElement("defaultClassicCardKeys");
					for (int i = 0; i <= 31; i++) {
						XmlAttribute classicCardKeyAKeyID = doc.CreateAttribute(string.Format("keyA{0:d2}", i));
						XmlAttribute classicCardKeyBKeyID = doc.CreateAttribute(string.Format("keyB{0:d2}", i));
						classicCardKeyAKeyID.Value = _constClassicCardKeyAKey;
						classicCardKeyBKeyID.Value = _constClassicCardKeyBKey;
						classicCardKey.Attributes.Append(classicCardKeyAKeyID);
						classicCardKey.Attributes.Append(classicCardKeyBKeyID);
					}
					doc.DocumentElement.AppendChild(classicCardKey);
					doc.Save(settingsFileName);
				}
				
				if (doc.SelectSingleNode("//defaultLanguage") == null) {
					XmlElement defaultLanguage = doc.CreateElement("defaultLanguage");
					XmlAttribute defaultLanguageID = doc.CreateAttribute("lang");
					defaultLanguageID.Value = _constDefaultLanguage;
					defaultLanguage.Attributes.Append(defaultLanguageID);
					doc.DocumentElement.AppendChild(defaultLanguage);
					doc.Save(settingsFileName);
				}
				
				node = doc.SelectSingleNode("//defaultLanguage");
				_oldLanguage = node.Attributes["lang"].Value;
				if ((_oldLanguage != language) & language != null) {
					
					node.Attributes["lang"].Value = language;

					doc.Save(settingsFileName);
				}
				
			} catch (XmlException e) {
				MessageBox.Show(string.Format("Fehler: Kann die {0}-Datei nicht lesen:\n\n{1}", settingsFileName, e), "Fehler",
				                MessageBoxButton.OK, MessageBoxImage.Error);
				Environment.Exit(0);
			}
		}

		public void saveSettings(string readerName, string readerProviderByName)
		{
			
			try {
				
				XmlDocument doc = new XmlDocument();
				doc.Load(settingsFileName);
				
				if (doc.SelectSingleNode("//defaultReader") == null) {
					XmlElement defaultReader = doc.CreateElement("defaultReader");
					XmlAttribute defaultReaderID = doc.CreateAttribute("readerName");
					XmlAttribute defaultReaderProvider = doc.CreateAttribute("readerProvider");
					defaultReaderID.Value = _constDefaultReader;
					defaultReader.Attributes.Append(defaultReaderID);
					defaultReader.Attributes.Append(defaultReaderProvider);
					doc.DocumentElement.AppendChild(defaultReader);
					doc.Save(settingsFileName);
				}
				
				XmlNode node = doc.SelectSingleNode("//defaultReader");
				_oldReader = node.Attributes["readerName"].Value;
				_oldReaderProvider = node.Attributes["readerProvider"].Value;
				if (! (String.IsNullOrEmpty(readerName) || String.IsNullOrEmpty(readerProviderByName))) {
					
					node.Attributes["readerName"].Value = readerName;
					node.Attributes["readerProvider"].Value = readerProviderByName;

					doc.Save(settingsFileName);
				}
				
			} catch (XmlException e) {
				MessageBox.Show(string.Format("Fehler: Kann die {0}-Datei nicht lesen:\n\n{1}", settingsFileName, e), "Fehler",
				                MessageBoxButton.OK, MessageBoxImage.Error);
				Environment.Exit(0);
			}
		}
			
		public void readSettings()
		{
			if (!File.Exists(settingsFileName)) {
				XmlWriter writer = XmlWriter.Create(@settingsFileName);
				writer.WriteStartDocument();
				writer.WriteStartElement("RFIDGearSettings");
				
				writer.WriteEndElement();
				writer.Close();
				
				saveSettings();
				
			} else if (File.Exists(settingsFileName)) {
				
				XmlDocument doc = new XmlDocument();
				doc.Load(@settingsFileName);
				
				try {
					XmlNode node = doc.SelectSingleNode("//defaultClassicCardKeys");
					
					_defaultClassicCardKeysAKeys = new string[32];
					_defaultClassicCardKeysBKeys = new string[32];
					
					for (int i = 0; i <= 31; i++) {
						
						_defaultClassicCardKeysAKeys[i] = node.Attributes[string.Format("keyA{0:d2}", i)].Value;
						_defaultClassicCardKeysBKeys[i] = node.Attributes[string.Format("keyB{0:d2}", i)].Value;
					}

					
					node = doc.SelectSingleNode("//defaultReader");
					_defaultReader = node.Attributes["readerName"].Value;
					_defaultReaderProvider = node.Attributes["readerProvider"].Value;
					
					node = doc.SelectSingleNode("//defaultLanguage");
					_defaultLanguage = node.Attributes["lang"].Value;
					
					node = doc.SelectSingleNode("//autoLoadKeys");
					_loadKeysAuto = Convert.ToBoolean(node.Attributes["auto"].Value);
					
					node = doc.SelectSingleNode("//defaultDesfireKeys");
					_defaultDesfireCardCardMasterKey = node.Attributes["defaultDesfireCard_CardMasterKey"].Value;
					_defaultDesfireCardCardMasterKeyType = node.Attributes["defaultDesfireCard_CardMasterKeyType"].Value;
					
					_defaultDesfireCardApplicationMasterKey = node.Attributes["defaultDesfireCard_ApplicationMasterKey"].Value;
					_defaultDesfireCardApplicationMasterKeyType = node.Attributes["defaultDesfireCard_ApplicationMasterKeyType"].Value;
					
					_defaultDesfireCardReadKey = node.Attributes["defaultDesfireCard_ReadKey"].Value;
					_defaultDesfireCardReadKeyType = node.Attributes["defaultDesfireCard_ReadKeyType"].Value;
					
					_defaultDesfireCardWriteKey = node.Attributes["defaultDesfireCard_WriteKey"].Value;
					_defaultDesfireCardWriteKeyType = node.Attributes["defaultDesfireCard_WriteKeyType"].Value;
					
					
				} catch (XmlException e) {
					MessageBox.Show(string.Format("Fehler: Kann die {0}-Datei nicht lesen:\n\n{1}", settingsFileName, e), "Fehler",
					                MessageBoxButton.OK, MessageBoxImage.Error);
					Environment.Exit(0);
				}
				
			}
		}
		#endregion
		
		#region properties
		
		public string DefaultReader {
			get { return _defaultReader; }
			set { _defaultReader = value; }
		}
		public string DefaultReaderProvider {
			get { return _defaultReaderProvider; }
			set { _defaultReaderProvider = value; }
		}
		public string defaultLanguage {
			get { return _defaultLanguage; }
			set { _defaultLanguage = value; }
		}
		public bool AutoLoadKeys {
			get { return _loadKeysAuto; }
			set { _loadKeysAuto = value; }
		}
		#endregion
	}
	
	public class KeySettingsDialogContent{
		
	}
}
