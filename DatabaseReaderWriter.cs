﻿using RFiDGear.Model;
using RFiDGear.ViewModel;

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
		
		public void WriteDatabase(object mifareClassicChip)
		{
			// TODO implement communication between database, viewmodel and mode
			try {

				XmlDocument doc = new XmlDocument();
				doc.Load(Path.Combine(appDataPath,databaseFileName));
				
				if (doc.SelectSingleNode("//UidNodesDatabase") != null) {
					XmlNode root = doc.SelectSingleNode("//UidNodesDatabase");
					
					if(doc.SelectSingleNode("//UidNode") == null)
					{
						XmlElement chipUidNode = doc.CreateElement("UidNode");
						XmlElement sectorTrailer = doc.CreateElement("SectorTrailerNode");
						
						if(mifareClassicChip is MifareClassicUidModel){
							chipUidNode.SetAttribute("ChipUid",(mifareClassicChip as MifareClassicUidModel).UidNumber);
							chipUidNode.SetAttribute("ChipType",converter._constCardType[(int)(mifareClassicChip as MifareClassicUidModel).CardType]);
						}
						
						
						for(int i = 0; i< 31; i++)
						{
							sectorTrailer.SetAttribute(String.Format("SectorTrailer{0:d2}",i),String.Format("{0:d2};000000000000,FF0780C3,FFFFFFFFFFFF",i));
						}

						root.AppendChild(chipUidNode);
						chipUidNode.AppendChild(sectorTrailer);
						
						doc.AppendChild(root);
						doc.Save(Path.Combine(appDataPath,databaseFileName));
					}
					
					foreach(XmlNode node in doc.SelectNodes("//SectorTrailerNode"))
					{
						//create List of gotten viewmodels with models as accessable properties
						List<MifareClassicSectorModel> sectorModels;
						if(mifareClassicChip is MifareClassicUidModel)
							sectorModels = new List<MifareClassicSectorModel>((mifareClassicChip as MifareClassicUidModel).SectorList);
						else
							sectorModels = new List<MifareClassicSectorModel>() {mifareClassicChip as MifareClassicSectorModel};
						
						// TODO dont need to iterate through all elem when saving single sectoraccessbits object. add if statement here
						foreach(XmlNode innerNode in node.Attributes)
						{
							string[] stCombined = innerNode.Value.Split(';');
							string[] stSeparated = stCombined[1].Split(',');
							
							if((mifareClassicChip is MifareClassicUidModel) &&
							   (Convert.ToInt32(stCombined[0]) > (mifareClassicChip as MifareClassicUidModel).SectorList.Count))
								return;
							
							foreach(MifareClassicSectorModel sectorModel in sectorModels)
							{
								if(Convert.ToInt32(stCombined[0]) == sectorModel.mifareClassicSectorNumber)
								{
									if(mifareClassicChip is MifareClassicUidModel){
										//add sector access bits from db string e.g. "FF0780C3" to the current selected model add decode to
										// match datagrid source for accessBit modify dialog as well as encoding for liblogicalaccess sectoraccessbits
										sectorModel.sectorAccessBits.sectorKeyAKey = stSeparated[0];
										sectorModel.sectorAccessBits.decodeSectorTrailer(stSeparated[1]);
										sectorModel.sectorAccessBits.sectorKeyBKey = stSeparated[2];
									}
									else{
										innerNode.Value = String.Format("{0}{1}{2}"
										                                ,sectorModel.sectorAccessBits.sectorKeyAKey
										                                ,sectorModel.sectorAccessBits.SectorTrailerAccessBits
										                                ,sectorModel.sectorAccessBits.sectorKeyBKey);
									}

								}
							}
						}
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
