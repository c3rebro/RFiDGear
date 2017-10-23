using RFiDGear.ViewModel;
using RFiDGear.DataAccessLayer;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Xml;
using System.Xml.Serialization;


namespace RFiDGear.DataAccessLayer
{
	/// <summary>
	/// Description of Class1.
	/// </summary>
	public class DatabaseReaderWriter
	{
		
		#region fields
		private readonly Version Version = Assembly.GetExecutingAssembly().GetName().Version;
		
		private const string databaseFileName = "database.xml";
		private string appDataPath;
		
		public ObservableCollection<TreeViewParentNodeViewModel> treeViewModel;
		
		#endregion
		
		public DatabaseReaderWriter()
		{
			try{
				
				
				// Combine the base folder with the specific folder....
				appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RFiDGear");

				// Check if folder exists and if not, create it
				if(!Directory.Exists(appDataPath))
					Directory.CreateDirectory(appDataPath);
				
				treeViewModel = new ObservableCollection<TreeViewParentNodeViewModel>();
				
				if (!File.Exists(Path.Combine(appDataPath,databaseFileName))) {
					
					XmlSerializer serializer = new XmlSerializer(typeof(ObservableCollection<TreeViewParentNodeViewModel>));
					
					TextWriter writer = new StreamWriter(Path.Combine(appDataPath, databaseFileName));
					
					serializer.Serialize(writer, treeViewModel);

					writer.Close();
				}
			}
			catch(Exception e)
			{
				LogWriter.CreateLogEntry(string.Format("{0}\n{1}",e.Message, e.InnerException != null ? e.InnerException.Message : ""));
				return;
			}
		}
		
		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		public bool ReadDatabase(string _fileName = "")
		{
			TextReader reader;
			int verInfo;
			
			if (!string.IsNullOrWhiteSpace(_fileName) && !File.Exists(_fileName)) {
				
				return false;
			}
			
			if (File.Exists(_fileName) || (string.IsNullOrWhiteSpace(_fileName) && File.Exists(Path.Combine(appDataPath,databaseFileName)))) {
				
				//Path.Combine(appDataPath,databaseFileName)
				XmlDocument doc = new XmlDocument();

				
				
				try {
					XmlSerializer serializer = new XmlSerializer(typeof(ObservableCollection<TreeViewParentNodeViewModel>));
					
					if(string.IsNullOrWhiteSpace(_fileName) && File.Exists(Path.Combine(appDataPath,databaseFileName)))
					{
						doc.Load(@Path.Combine(appDataPath,databaseFileName));
						
						XmlNode node = doc.SelectSingleNode("//ManifestVersion");
						verInfo = Convert.ToInt32(node.InnerText.Replace(".",string.Empty));
						
						reader = new StreamReader(Path.Combine(appDataPath,databaseFileName));
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
					
					treeViewModel = (serializer.Deserialize(reader) as ObservableCollection<TreeViewParentNodeViewModel>);
					
				} catch(Exception e) {
					LogWriter.CreateLogEntry(string.Format("{0}\n{1}",e.Message, e.InnerException != null ? e.InnerException.Message : ""));
					return true;
				}
				
				return false;
			}
			
			return true;
		}
		
		public void WriteDatabase(ObservableCollection<TreeViewParentNodeViewModel> objModel, string _path = "")
		{
			try {
				TextWriter writer;
				XmlSerializer serializer = new XmlSerializer(typeof(ObservableCollection<TreeViewParentNodeViewModel>));
				
				if(!string.IsNullOrEmpty(_path))
				{
					writer = new StreamWriter(@_path);
				}
				else
					writer = new StreamWriter(@Path.Combine(appDataPath, databaseFileName));
				
				//writer.WriteStartDocument();
				//writer.WriteStartElement("Manifest");
				//writer.WriteAttributeString("version", string.Format("{0}.{1}.{2}",Version.Major,Version.Minor,Version.Build));
				//writer = new StreamWriter(@Path.Combine(appDataPath,databaseFileName));
				
				serializer.Serialize(writer, objModel);

				writer.Close();

			}
			catch (XmlException e) {
				LogWriter.CreateLogEntry(string.Format("{0}\n{1}",e.Message, e.InnerException != null ? e.InnerException.Message : ""));
				Environment.Exit(0);
			}
		}
		
		public void DeleteDatabase(){
			File.Delete(Path.Combine(appDataPath,databaseFileName));
		}
	}
}