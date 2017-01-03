using RFiDGear.Model;

using System;
using System.IO;
using System.Xml;
using System.Collections.Generic;

namespace RFiDGear
{
	/// <summary>
	/// Description of Class1.
	/// </summary>
	public class ProjectFileReaderWriter
	{
		
		#region fields
		const string databaseFileName = "database.xml";
		
		public string[] databaseUids { get; set; }
		public string[] databaseChipTypes { get; set; }
		public string	databaseUid {get; set; }
		public string	databaseChipType { get; set; }

		CustomConverter converter = new CustomConverter();
		
		private string appDataPath;


		#endregion
		
		private List<string> MifareClassicAKeys;
		private List<string> MifareClassicBKeys;
		private List<string> SectorAccessBits;
		
		public ProjectFileReaderWriter()
		{
			// The folder for the roaming current user
			appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

			// Combine the base folder with your specific folder....
			appDataPath = Path.Combine(appDataPath, "RFiDGear");

			// Check if folder exists and if not, create it
			if(!Directory.Exists(appDataPath))
				Directory.CreateDirectory(appDataPath);
			
			ReadDatabase();
		}
		
		public void ReadDatabase()
		{
			if (!File.Exists(Path.Combine(appDataPath,databaseFileName))) {
				XmlWriter writer = XmlWriter.Create(@Path.Combine(appDataPath,databaseFileName));
				writer.WriteStartDocument();
				writer.WriteStartElement("UidNodesDatabase");
				
				writer.WriteEndElement();
				writer.Close();
				
				//saveSettings();
				
			} else if (File.Exists(Path.Combine(appDataPath,databaseFileName))) {
				
				XmlDocument doc = new XmlDocument();
				doc.Load(@Path.Combine(appDataPath,databaseFileName));
				
				try {
					XmlNode node = doc.SelectSingleNode("//defaultClassicCardKeys");
					
					
				} catch (XmlException e) {
				}
				
			}
		}
		
		public void WriteDatabase(MifareClassicUidModel mifareClassicChip)
		{
			try {
				
				XmlDocument doc = new XmlDocument();
				doc.Load(Path.Combine(appDataPath,databaseFileName));
				
				if (doc.SelectSingleNode("//UidNodes") == null) {
					XmlElement root = doc.DocumentElement;
					XmlElement chipUidNode = doc.CreateElement("UidNode");
					XmlElement sectorTrailer = doc.CreateElement("SectorTrailerNode");
					
					chipUidNode.SetAttribute("ChipUid",mifareClassicChip.UidNumber);
					chipUidNode.SetAttribute("ChipType",converter._constCardType[(int)mifareClassicChip.CardType]);
					
					
					for(int i = 0; i< 31; i++)
					{
						sectorTrailer.SetAttribute(String.Format("SectorTrailer{0}",i),"FFFFFFFF00112200FFFFFFFF");
					}

					root.AppendChild(chipUidNode);
					chipUidNode.AppendChild(sectorTrailer);
					
					doc.AppendChild(root);
					doc.Save(Path.Combine(appDataPath,databaseFileName));
				}
				
			} catch (XmlException e) {

				Environment.Exit(0);
			}
		}
	}
}
