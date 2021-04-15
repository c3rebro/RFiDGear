using RFiDGear.Model;
using RFiDGear.ViewModel;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace RFiDGear.DataAccessLayer
{
    /// <summary>
    /// Serialize and Deserialize Chips and Tasks
    /// </summary>
    public class DatabaseReaderWriter
    {
        #region fields

        private readonly Version Version = Assembly.GetExecutingAssembly().GetName().Version;

        private const string chipDatabaseFileName = "chipdatabase.xml";
        private const string taskDatabaseFileName = "taskdatabase.xml";
        private string appDataPath;

        public ObservableCollection<RFiDChipParentLayerViewModel> treeViewModel;
        public ChipTaskHandlerModel setupModel;

        #endregion fields

        public DatabaseReaderWriter()
        {
            try
            {
                // Combine the base folder with the specific folder....
                appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RFiDGear");

                // Check if folder exists and if not, create it
                if (!Directory.Exists(appDataPath))
                    Directory.CreateDirectory(appDataPath);

                treeViewModel = new ObservableCollection<RFiDChipParentLayerViewModel>();
                setupModel = new ChipTaskHandlerModel();

                if (!File.Exists(Path.Combine(appDataPath, chipDatabaseFileName)))
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(ObservableCollection<RFiDChipParentLayerViewModel>));

                    TextWriter writer = new StreamWriter(Path.Combine(appDataPath, chipDatabaseFileName));

                    serializer.Serialize(writer, treeViewModel);

                    writer.Close();
                }

                if (!File.Exists(Path.Combine(appDataPath, taskDatabaseFileName)))
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(ChipTaskHandlerModel));

                    TextWriter writer = new StreamWriter(Path.Combine(appDataPath, taskDatabaseFileName));

                    serializer.Serialize(writer, setupModel);

                    writer.Close();
                }
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(string.Format("{0}; {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""));
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

            if (!string.IsNullOrWhiteSpace(_fileName) && !File.Exists(_fileName))
            {
                return false;
            }

            if (File.Exists(_fileName) || (string.IsNullOrWhiteSpace(_fileName) && File.Exists(Path.Combine(appDataPath, chipDatabaseFileName))))
            {
                //Path.Combine(appDataPath,databaseFileName)
                XmlDocument doc = new XmlDocument();

                try
                {
                    if (string.IsNullOrWhiteSpace(_fileName) && File.Exists(Path.Combine(appDataPath, chipDatabaseFileName)))
                    {
                        doc.Load(@Path.Combine(appDataPath, chipDatabaseFileName));

                        XmlNode node = doc.SelectSingleNode("//ManifestVersion");
                        verInfo = Convert.ToInt32(node.InnerText.Replace(".", string.Empty));

                        reader = new StreamReader(Path.Combine(appDataPath, chipDatabaseFileName));
                    }
                    else
                    {
                        doc.Load(_fileName);

                        XmlNode node = doc.SelectSingleNode("//ManifestVersion");
                        verInfo = Convert.ToInt32(node.InnerText.Replace(".", string.Empty));

                        reader = new StreamReader(_fileName);
                    }

                    if (verInfo > Convert.ToInt32(string.Format("{0}{1}{2}", Version.Major, Version.Minor, Version.Build)))
                    {
                        LogWriter.CreateLogEntry(string.Format("{0}; {1}", DateTime.Now, string.Format("database that was tried to open is newer ({0}) than this version of eventmessenger ({1})"
                                                                                                      , verInfo, Convert.ToInt32(string.Format("{0}{1}{2}", Version.Major, Version.Minor, Version.Build)))));
                        return true;
                    }

                    try
                    {
                        XmlSerializer serializer = new XmlSerializer(typeof(ChipTaskHandlerModel));
                        setupModel = (serializer.Deserialize(reader) as ChipTaskHandlerModel);
                    }
                    catch (Exception e)
                    {
                        try
                        {
                            XmlSerializer serializer = new XmlSerializer(typeof(ObservableCollection<RFiDChipParentLayerViewModel>));
                            treeViewModel = (serializer.Deserialize(reader) as ObservableCollection<RFiDChipParentLayerViewModel>);
                        }
                        catch (Exception innerE)
                        {
                            LogWriter.CreateLogEntry(string.Format("{0}; {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""));
                            LogWriter.CreateLogEntry(string.Format("{0}; {1}; {2}", DateTime.Now, innerE.Message, innerE.InnerException != null ? innerE.InnerException.Message : ""));
                            return true;
                        }
                    }
                }
                catch (Exception e)
                {
                    LogWriter.CreateLogEntry(string.Format("{0}; {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""));
                    return true;
                }

                return false;
            }

            return true;
        }

        public void WriteDatabase(ObservableCollection<RFiDChipParentLayerViewModel> objModel, string _path = "")
        {
            try
            {
                TextWriter writer;
                XmlSerializer serializer = new XmlSerializer(typeof(ObservableCollection<RFiDChipParentLayerViewModel>));

                if (!string.IsNullOrEmpty(_path))
                {
                    writer = new StreamWriter(@_path);
                }
                else
                    writer = new StreamWriter(@Path.Combine(appDataPath, chipDatabaseFileName), false, new UTF8Encoding(false));

                //writer.WriteStartDocument();
                //writer.WriteStartElement("Manifest");
                //writer.WriteAttributeString("version", string.Format("{0}.{1}.{2}",Version.Major,Version.Minor,Version.Build));
                //writer = new StreamWriter(@Path.Combine(appDataPath,databaseFileName));

                serializer.Serialize(writer, objModel);

                writer.Close();
            }
            catch (XmlException e)
            {
                LogWriter.CreateLogEntry(string.Format("{0}; {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""));
                Environment.Exit(0);
            }
        }

        public void WriteDatabase(ChipTaskHandlerModel objModel, string _path = "")
        {
            try
            {
                TextWriter writer;
                XmlSerializer serializer = new XmlSerializer(typeof(ChipTaskHandlerModel));

                if (!string.IsNullOrEmpty(_path))
                {
                    writer = new StreamWriter(@_path);
                }
                else
                    writer = new StreamWriter(@Path.Combine(appDataPath, taskDatabaseFileName), false, new UTF8Encoding(false));

                //writer.WriteStartDocument();
                //writer.WriteStartElement("Manifest");
                //writer.WriteAttributeString("version", string.Format("{0}.{1}.{2}",Version.Major,Version.Minor,Version.Build));
                //writer = new StreamWriter(@Path.Combine(appDataPath,databaseFileName));

                serializer.Serialize(writer, objModel);

                writer.Close();
            }
            catch (XmlException e)
            {
                LogWriter.CreateLogEntry(string.Format("{0}; {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""));
                Environment.Exit(0);
            }
        }

        public void DeleteDatabase()
        {
            File.Delete(Path.Combine(appDataPath, chipDatabaseFileName));
        }
    }
}