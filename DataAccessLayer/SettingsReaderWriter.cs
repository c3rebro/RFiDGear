using RFiDGear.DataAccessLayer;
using RFiDGear.Model;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Xml;
using System.Xml.Serialization;

using LibLogicalAccess;

namespace RFiDGear
{
	/// <summary>
	/// Description of Class1.
	/// </summary>
	public class SettingsReaderWriter
	{
		
		#region fields
		
		readonly string _settingsFileFileName = "settings.xml";
		readonly string _updateConfigFileFileName = "update.xml";
		readonly string _updateURL = @"http://rfidgear.hyperstack.de/update.xml";
		readonly int _updateInterval = 900;
		readonly string _securityToken = "D68EF3A7-E787-4CC4-B020-878BA649B4CD";
		readonly string _payload = "update.zip";
		readonly string _baseUri = @"http://rfidgear.hyperstack.de/download/";
		
		private readonly Version Version = Assembly.GetExecutingAssembly().GetName().Version;
		
		private string appDataPath;
		
		private XmlWriter xmlWriter;
		
		public DefaultSpecification DefaultSpecification { get; set; }
		
		#endregion
		
		public SettingsReaderWriter()
		{
			
			try{
				appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
				
				appDataPath = Path.Combine(appDataPath, "RFiDGear");
				
				if(!Directory.Exists(appDataPath))
					Directory.CreateDirectory(appDataPath);
				
				xmlWriter = XmlWriter.Create(Path.Combine(appDataPath, _updateConfigFileFileName));
				xmlWriter.WriteStartDocument();
				xmlWriter.WriteStartElement("Manifest");
				xmlWriter.WriteAttributeString("version", string.Format("{0}.{1}.{2}",Version.Major,Version.Minor,Version.Build));
				
				xmlWriter.WriteEndElement();
				xmlWriter.Close();
				
				XmlDocument doc = new XmlDocument();
				doc.Load(Path.Combine(appDataPath, _updateConfigFileFileName));
				
				if (doc.SelectSingleNode("//CheckInterval") == null) {
					
					XmlElement CheckIntervalElem = doc.CreateElement("CheckInterval");
					XmlElement RemoteConfigUriElem = doc.CreateElement("RemoteConfigUri");
					XmlElement SecurityTokenElem = doc.CreateElement("SecurityToken");
					XmlElement BaseUriElem = doc.CreateElement("BaseUri");
					XmlElement PayLoadElem = doc.CreateElement("Payload");
					
					doc.DocumentElement.AppendChild(CheckIntervalElem);
					doc.DocumentElement.AppendChild(RemoteConfigUriElem);
					doc.DocumentElement.AppendChild(SecurityTokenElem);
					doc.DocumentElement.AppendChild(BaseUriElem);
					doc.DocumentElement.AppendChild(PayLoadElem);
					
					CheckIntervalElem.InnerText = _updateInterval.ToString();
					RemoteConfigUriElem.InnerText = _updateURL;
					SecurityTokenElem.InnerText = _securityToken;
					BaseUriElem.InnerText = _baseUri;
					PayLoadElem.InnerText = _payload;
					
					doc.Save(Path.Combine(appDataPath, _updateConfigFileFileName));
				}
			}
			catch(Exception e)
			{
				LogWriter.CreateLogEntry(string.Format("{0}\n{1}",e.Message, e.InnerException != null ? e.InnerException.Message : ""));
			}
			
			if (!File.Exists(Path.Combine(appDataPath,_settingsFileFileName)))
			{
				try
				{
					DefaultSpecification = new DefaultSpecification(true);
					
					XmlSerializer serializer = new XmlSerializer(DefaultSpecification.GetType());
					
					TextWriter txtWriter = new StreamWriter(Path.Combine(appDataPath, _settingsFileFileName));
					
					serializer.Serialize(txtWriter, DefaultSpecification);
					
					txtWriter.Close();
					
				}
				catch(Exception e)
				{
					LogWriter.CreateLogEntry(string.Format("{0}; {1}; {2}",DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""));
				}
			}
			else
				ReadSettings();
		}
		
		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		public bool ReadSettings(string _fileName = "")
		{
			TextReader reader;
			int verInfo;
			
			if (!string.IsNullOrWhiteSpace(_fileName) && !File.Exists(_fileName)) {
				
				return false;
			}
			
			if (File.Exists(_fileName) || (string.IsNullOrWhiteSpace(_fileName) && File.Exists(Path.Combine(appDataPath,_settingsFileFileName)))) {
				
				//Path.Combine(appDataPath,databaseFileName)
				XmlDocument doc = new XmlDocument();

				
				
				try {
					XmlSerializer serializer = new XmlSerializer(typeof(DefaultSpecification));
					
					if(string.IsNullOrWhiteSpace(_fileName) && File.Exists(Path.Combine(appDataPath,_settingsFileFileName)))
					{
						doc.Load(@Path.Combine(appDataPath,_settingsFileFileName));
						
						XmlNode node = doc.SelectSingleNode("//ManifestVersion");
						verInfo = Convert.ToInt32(node.InnerText.Replace(".",string.Empty));
						
						reader = new StreamReader(Path.Combine(appDataPath,_settingsFileFileName));
					}

					else
					{
						doc.Load(_fileName);
						
						XmlNode node = doc.SelectSingleNode("//ManifestVersion");
						verInfo = Convert.ToInt32(node.InnerText.Replace(".",string.Empty));
						
						reader = new StreamReader(_fileName);
					}
					
					
					if(verInfo > Convert.ToInt32(string.Format("{0}{1}{2}",Version.Major,Version.Minor,Version.Build)))
					{
						throw new Exception(
							string.Format("database that was tried to open is newer ({0}) than this version of eventmessenger ({1})"
							              ,verInfo, Convert.ToInt32(string.Format("{0}{1}{2}",Version.Major,Version.Minor,Version.Build))
							             )
						);
					}
					

					//defaultSpecification = new DefaultSpecification();
					DefaultSpecification = (serializer.Deserialize(reader) as DefaultSpecification);
					
					
				} catch(Exception e) {
					LogWriter.CreateLogEntry(string.Format("{0}\n{1}",e.Message, e.InnerException != null ? e.InnerException.Message : ""));
					return true;
				}
				
				return false;
			}
			return true;
		}
		
		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		public bool SaveSettings(string _path = "")
		{
			try {
				TextWriter textWriter;
				XmlSerializer serializer = new XmlSerializer(typeof(DefaultSpecification));
				
				if(!string.IsNullOrEmpty(_path))
				{
					textWriter = new StreamWriter(@_path);
				}
				else
					textWriter = new StreamWriter(@Path.Combine(appDataPath,_settingsFileFileName),false);
				
				serializer.Serialize(textWriter, DefaultSpecification);

				textWriter.Close();
				
				return true;

			}
			catch (XmlException e) {
				LogWriter.CreateLogEntry(string.Format("{0}\n{1}",e.Message, e.InnerException != null ? e.InnerException.Message : ""));
				return false;
			}
		}
	}
}
