/*
 * Created by SharpDevelop.
 * Date: 12.10.2017
 * Time: 15:26
 * 
 */
using RFiDGear.DataAccessLayer;
using RFiDGear.Model;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Xml;
using System.Xml.Serialization;

namespace RFiDGear.Model
{
	/// <summary>
	/// Description of MifareClassicDefaultSpecification.
	/// </summary>
	[XmlRootAttribute("DefaultSpecification", IsNullable = false)]
	public class DefaultSpecification : IDisposable
	{
		private Version Version = Assembly.GetExecutingAssembly().GetName().Version;
		
		public DefaultSpecification()
		{

		}
		
		public DefaultSpecification(bool init)
		{
			ManifestVersion = string.Format("{0}.{1}.{2}",Version.Major,Version.Minor,Version.Build);
			
			_defaultReaderName = "";
			_defaultReaderProvider = ReaderTypes.None;
			_defaultLanguage = "english";
			
			
			mifareClassicDefaultSecuritySettings = new List<MifareClassicDefaultKeys>
			{
				new MifareClassicDefaultKeys(KeyType_MifareClassicKeyType.DefaultClassicCardAccessBits_Key00, "FFFFFFFFFFFF,FF0780C3,FFFFFFFFFFFF"),
				new MifareClassicDefaultKeys(KeyType_MifareClassicKeyType.DefaultClassicCardAccessBits_Key01, "FFFFFFFFFFFF,FF0780C3,FFFFFFFFFFFF"),
				new MifareClassicDefaultKeys(KeyType_MifareClassicKeyType.DefaultClassicCardAccessBits_Key02, "FFFFFFFFFFFF,FF0780C3,FFFFFFFFFFFF"),
				new MifareClassicDefaultKeys(KeyType_MifareClassicKeyType.DefaultClassicCardAccessBits_Key03, "FFFFFFFFFFFF,FF0780C3,FFFFFFFFFFFF"),
				new MifareClassicDefaultKeys(KeyType_MifareClassicKeyType.DefaultClassicCardAccessBits_Key04, "FFFFFFFFFFFF,FF0780C3,FFFFFFFFFFFF"),
				new MifareClassicDefaultKeys(KeyType_MifareClassicKeyType.DefaultClassicCardAccessBits_Key05, "FFFFFFFFFFFF,FF0780C3,FFFFFFFFFFFF"),
				new MifareClassicDefaultKeys(KeyType_MifareClassicKeyType.DefaultClassicCardAccessBits_Key06, "FFFFFFFFFFFF,FF0780C3,FFFFFFFFFFFF"),
				new MifareClassicDefaultKeys(KeyType_MifareClassicKeyType.DefaultClassicCardAccessBits_Key07, "FFFFFFFFFFFF,FF0780C3,FFFFFFFFFFFF"),
				new MifareClassicDefaultKeys(KeyType_MifareClassicKeyType.DefaultClassicCardAccessBits_Key08, "FFFFFFFFFFFF,FF0780C3,FFFFFFFFFFFF"),
				new MifareClassicDefaultKeys(KeyType_MifareClassicKeyType.DefaultClassicCardAccessBits_Key09, "FFFFFFFFFFFF,FF0780C3,FFFFFFFFFFFF"),
				new MifareClassicDefaultKeys(KeyType_MifareClassicKeyType.DefaultClassicCardAccessBits_Key10, "FFFFFFFFFFFF,FF0780C3,FFFFFFFFFFFF"),
				new MifareClassicDefaultKeys(KeyType_MifareClassicKeyType.DefaultClassicCardAccessBits_Key11, "FFFFFFFFFFFF,FF0780C3,FFFFFFFFFFFF"),
				new MifareClassicDefaultKeys(KeyType_MifareClassicKeyType.DefaultClassicCardAccessBits_Key12, "FFFFFFFFFFFF,FF0780C3,FFFFFFFFFFFF"),
				new MifareClassicDefaultKeys(KeyType_MifareClassicKeyType.DefaultClassicCardAccessBits_Key13, "FFFFFFFFFFFF,FF0780C3,FFFFFFFFFFFF"),
				new MifareClassicDefaultKeys(KeyType_MifareClassicKeyType.DefaultClassicCardAccessBits_Key14, "FFFFFFFFFFFF,FF0780C3,FFFFFFFFFFFF"),
				new MifareClassicDefaultKeys(KeyType_MifareClassicKeyType.DefaultClassicCardAccessBits_Key15, "FFFFFFFFFFFF,FF0780C3,FFFFFFFFFFFF")
			};
			
			mifareDesfireDefaultSecuritySettings = new List<MifareDesfireDefaultKeys>
			{
				new MifareDesfireDefaultKeys(KeyType_MifareDesFireKeyType.DefaultDesfireCardApplicationMasterKey, KeyType_EncryptionType.AES, "00000000000000000000000000000000"),
				new MifareDesfireDefaultKeys(KeyType_MifareDesFireKeyType.DefaultDesfireCardCardMasterKey, KeyType_EncryptionType.AES, "00000000000000000000000000000000"),
				new MifareDesfireDefaultKeys(KeyType_MifareDesFireKeyType.DefaultDesfireCardReadKey, KeyType_EncryptionType.AES, "00000000000000000000000000000000"),
				new MifareDesfireDefaultKeys(KeyType_MifareDesFireKeyType.DefaultDesfireCardWriteKey, KeyType_EncryptionType.AES, "00000000000000000000000000000000")
			};

			_classicCardDefaultSectorTrailer = "FFFFFFFFFFFF,FF0780C3,FFFFFFFFFFFF";
			
			_classicCardDefaultQuickCheckKeys = new List<string>{
				"FFFFFFFFFFFF","A1B2C3D4E5F6","1A2B3C4D5E6F",
				"000000000000","A0B0C0D0E0F0","A1B1C1D1E1F1",
				"A0A1A2A3A4A5","B0B1B2B3B4B5","4D3A99C351DD",
				"1A982C7E459A","D3F7D3F7D3F7","AABBCCDDEEFF"};
		}
		#region properties

		/// <summary>
		/// 
		/// </summary>
		public string ManifestVersion { get; set; }
		
		/// <summary>
		/// 
		/// </summary>
		public string DefaultReaderName
		{
			get { return _defaultReaderName; }
			set { _defaultReaderName = value; }
		}
		private string _defaultReaderName;
		
		/// <summary>
		/// 
		/// </summary>
		public ReaderTypes DefaultReaderProvider
		{
			get { return _defaultReaderProvider; }
			set { _defaultReaderProvider = value; }
		}
		private ReaderTypes _defaultReaderProvider;
		
		/// <summary>
		/// 
		/// </summary>
		public string DefaultLanguage
		{
			get { return _defaultLanguage; }
			set { _defaultLanguage = value; }
		}
		private string _defaultLanguage;
		
		public List<MifareDesfireDefaultKeys> MifareDesfireDefaultSecuritySettings
		{
			get { return mifareDesfireDefaultSecuritySettings; }
			set { mifareDesfireDefaultSecuritySettings = value; }
		} private List<MifareDesfireDefaultKeys> mifareDesfireDefaultSecuritySettings;
		
		public List<MifareClassicDefaultKeys> MifareClassicDefaultSecuritySettings
		{
			get { return mifareClassicDefaultSecuritySettings; }
			set { mifareClassicDefaultSecuritySettings = value; }
		} private List<MifareClassicDefaultKeys> mifareClassicDefaultSecuritySettings;
		
		/// <summary>
		/// 
		/// </summary>
		public string MifareClassicDefaultSectorTrailer
		{
			get { return _classicCardDefaultSectorTrailer; }
			set { _classicCardDefaultSectorTrailer = value; }
		}
		private string _classicCardDefaultSectorTrailer;
		
		/// <summary>
		/// 
		/// </summary>
		public List<string> MifareClassicDefaultQuickCheckKeys
		{
			get { return _classicCardDefaultQuickCheckKeys; }
			set { _classicCardDefaultQuickCheckKeys = value; }
		}
		private List<string> _classicCardDefaultQuickCheckKeys;
		
		#endregion
		
		#region Extensions
		
		private bool _disposed = false;

		void IDisposable.Dispose()
		{

		}
		
		protected virtual void Dispose(bool disposing)
		{
			if (!_disposed)
			{
				if (disposing)
				{
					// Dispose any managed objects
					// ...
				}

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

		// Destructor
		~DefaultSpecification()
		{
			Dispose(false);
		}
		#endregion
	}
}
