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
	public class DatabaseReaderWriter
	{
		
		#region fields
		private const string databaseFileName = "database.xml";
		private CustomConverter converter = new CustomConverter();
		private string appDataPath;
		
		public List<string> databaseUIDs { get; set; }
		public List<MifareClassicAccessBitsModel> databaseAccessBits { get; set; }
		#endregion
		
		public DatabaseReaderWriter()
		{
			// Combine the base folder with your specific folder....
			appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RFiDGear");

			// Check if folder exists and if not, create it
			if(!Directory.Exists(appDataPath))
				Directory.CreateDirectory(appDataPath);
			
			databaseUIDs = new List<string>();
			databaseAccessBits = new List<MifareClassicAccessBitsModel>();
			
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
				
				//WriteDatabase();
				
			} else if (File.Exists(Path.Combine(appDataPath,databaseFileName))) {
				
				XmlDocument doc = new XmlDocument();
				doc.Load(@Path.Combine(appDataPath,databaseFileName));
				
				try {
					/* TODO Read out Sector Trailer to add keys and access bits to "mifareClassicSectorModel". Access Bits
					 * will be modified by Dialog Window later */
					foreach(XmlNode node in doc.SelectNodes("//UidNode"))
					{
						databaseUIDs.Add(node.Attributes["ChipUid"].Value);
						databaseAccessBits.Add(new MifareClassicAccessBitsModel());
					}
					
					
				} catch (XmlException e) {
				}
				
			}
		}
		
		public void WriteDatabase(MifareClassicUidModel mifareClassicChip)
		{
			try {
				
				XmlDocument doc = new XmlDocument();
				doc.Load(Path.Combine(appDataPath,databaseFileName));
				
				if (doc.SelectSingleNode("//UidNodesDatabase") == null) {
					XmlElement root = doc.DocumentElement;
					XmlElement chipUidNode = doc.CreateElement("UidNode");
					XmlElement sectorTrailer = doc.CreateElement("SectorTrailerNode");
					
					chipUidNode.SetAttribute("ChipUid",mifareClassicChip.UidNumber);
					chipUidNode.SetAttribute("ChipType",converter._constCardType[(int)mifareClassicChip.CardType]);
					
					
					for(int i = 0; i< 31; i++)
					{
						sectorTrailer.SetAttribute(String.Format("SectorTrailer{0:d2}",i),String.Format("{0:d2};000000000000;FF0780C3;FFFFFFFFFFFF",i));
					}

					root.AppendChild(chipUidNode);
					chipUidNode.AppendChild(sectorTrailer);
					
					doc.AppendChild(root);
					doc.Save(Path.Combine(appDataPath,databaseFileName));
				}
				
				else
				{
					XmlNode root = doc.SelectSingleNode("//UidNodesDatabase");

					XmlNodeList chipUidNodesList = doc.SelectNodes("UidNode");
					XmlNodeList sectorTrailersList = doc.SelectNodes("SectorTrailerNode");
					
					if(doc.SelectSingleNode("//ChipUid") == null)
					{
						XmlElement chipUidNode = doc.CreateElement("UidNode");
						XmlElement sectorTrailer = doc.CreateElement("SectorTrailerNode");
						
						chipUidNode.SetAttribute("ChipUid",mifareClassicChip.UidNumber);
						chipUidNode.SetAttribute("ChipType",converter._constCardType[(int)mifareClassicChip.CardType]);
						
						root.AppendChild(chipUidNode);
						
						for(int i = 0; i< 31; i++)
						{
							sectorTrailer.SetAttribute(String.Format("SectorTrailer{0:d2}",i),String.Format("{0:d2}:000000000000;FF0780C3;FFFFFFFFFFFF",i));
							chipUidNode.AppendChild(sectorTrailer);
						}
						

						//chipUidNode.AppendChild(sectorTrailer);
						
						doc.AppendChild(root);
						doc.Save(Path.Combine(appDataPath,databaseFileName));
					}
					else if (databaseUIDs.Contains(mifareClassicChip.UidNumber))
					{
						foreach(XmlNode node in doc.SelectNodes("//UidNode"))
						{
							foreach(XmlNode innerNode in node.Attributes["SectorTrailer"])
							{
								string[] arr = innerNode.Value.Split(':');
							}
						}
					}
					
					
					foreach(MifareClassicSectorModel sector in mifareClassicChip.SectorList)
					{
						foreach(XmlElement elem in root.SelectNodes(""));
					}
				}
				
			} catch (XmlException e) {

				Environment.Exit(0);
			}
		}
		
		public void DeleteDatabase(){
			File.Delete(Path.Combine(appDataPath,databaseFileName));
		}
	}
}
