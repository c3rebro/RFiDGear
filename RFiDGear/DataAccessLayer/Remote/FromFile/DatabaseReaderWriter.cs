using RFiDGear.Model;
using RFiDGear.ViewModel;

using Log4CSharp;

using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Ionic.Zip;

namespace RFiDGear.DataAccessLayer
{
    /// <summary>
    /// Serialize and Deserialize Chips and Tasks
    /// </summary>
    public class DatabaseReaderWriter
    {
        #region fields
        private static readonly string FacilityName = "RFiDGear";

        private readonly Version Version = Assembly.GetExecutingAssembly().GetName().Version;

        private const string chipDatabaseFileName = "chipdatabase.xml";
        private const string chipDatabaseFileNameCompressed = "chipdatabase.rfPrj";
        private const string taskDatabaseFileName = "taskdatabase.xml";
        private readonly string appDataPath;

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
                {
                    Directory.CreateDirectory(appDataPath);
                }

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
                LogWriter.CreateLogEntry(string.Format("{0}; {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""), FacilityName);
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
            int verInfo = 0;
            FileInfo file;

            if (string.IsNullOrWhiteSpace(_fileName) || !File.Exists(_fileName))
            {
                return false;
            }
            else
            {
                file = new FileInfo(_fileName);
            }

            try
            {
                XmlDocument doc = new XmlDocument();

                if (file.Extension.ToLower() == ".xml")
                {
                    doc.Load(_fileName);

                    XmlNode node = doc.SelectSingleNode("//ManifestVersion");
                    verInfo = Convert.ToInt32(node.InnerText.Replace(".", string.Empty));

                    reader = new StreamReader(_fileName);

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
                            LogWriter.CreateLogEntry(string.Format("{0}; {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""), FacilityName);
                            LogWriter.CreateLogEntry(string.Format("{0}; {1}; {2}", DateTime.Now, innerE.Message, innerE.InnerException != null ? innerE.InnerException.Message : ""), FacilityName);
                            return true;
                        }
                    }

                }

                if (file.Extension.ToLower() == ".rfprj")
                {
                    using (ZipFile zip1 = ZipFile.Read(string.IsNullOrWhiteSpace(_fileName) ?
                    @Path.Combine(appDataPath, chipDatabaseFileNameCompressed) :
                    _fileName))
                    {
                        if(Directory.GetFiles(appDataPath, "*.tmp").Length > 0)
                        {
                            foreach(string tempFile in Directory.GetFiles(appDataPath, "*.tmp"))
                            {
                                File.Delete(tempFile);
                            } 
                        }
                        zip1.ExtractAll(appDataPath, ExtractExistingFileAction.OverwriteSilently);
                    }

                    doc.Load(@Path.Combine(appDataPath, file.Name));

                    XmlNode node = doc.SelectSingleNode("//ManifestVersion");
                    verInfo = Convert.ToInt32(node.InnerText.Replace(".", string.Empty));

                    reader = new StreamReader(@Path.Combine(appDataPath, file.Name));

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
                            LogWriter.CreateLogEntry(string.Format("{0}; {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""), FacilityName);
                            LogWriter.CreateLogEntry(string.Format("{0}; {1}; {2}", DateTime.Now, innerE.Message, innerE.InnerException != null ? innerE.InnerException.Message : ""), FacilityName);
                            return true;
                        }
                    }
                }

                if (verInfo > Convert.ToInt32(string.Format("{0}{1}{2}", Version.Major, Version.Minor, Version.Build)))
                {
                    LogWriter.CreateLogEntry(string.Format("{0}; {1}", DateTime.Now, string.Format("database that was tried to open is newer ({0}) than this version of rfidgear ({1})"
                                                                                                  , verInfo, Convert.ToInt32(string.Format("{0}{1}{2}", Version.Major, Version.Minor, Version.Build)))), FacilityName);
                    return true;
                }
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(string.Format("{0}; {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""), FacilityName);
                return true;
            }

            return false;
        }

        public void WriteDatabase(ObservableCollection<RFiDChipParentLayerViewModel> objModel, string _path = "")
        {
            ZipFile zip = new ZipFile();

            try
            {
                TextWriter writer;
                XmlSerializer serializer = new XmlSerializer(typeof(ObservableCollection<RFiDChipParentLayerViewModel>));

                if (!string.IsNullOrEmpty(_path))
                {
                    writer = new StreamWriter(@_path);
                }
                else
                {
                    writer = new StreamWriter(@Path.Combine(appDataPath, chipDatabaseFileName), false, new UTF8Encoding(false));
                }

                serializer.Serialize(writer, objModel);

                writer.Close();

                zip.AddFile(@Path.Combine(string.IsNullOrWhiteSpace(_path) ?
                    @Path.Combine(appDataPath, chipDatabaseFileName) :
                    @_path, chipDatabaseFileName));

                zip.Save(@Path.Combine(string.IsNullOrWhiteSpace(_path) ?
                    @Path.Combine(appDataPath, chipDatabaseFileNameCompressed) :
                    @_path, chipDatabaseFileNameCompressed));
            }
            catch (XmlException e)
            {
                LogWriter.CreateLogEntry(string.Format("{0}; {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""), FacilityName);
                Environment.Exit(0);
            }
        }

        public void WriteDatabase(ChipTaskHandlerModel objModel, string _path = "")
        {
            try
            {
                ZipFile zip = new ZipFile();
                FileInfo file;

                TextWriter writer;
                XmlSerializer serializer = new XmlSerializer(typeof(ChipTaskHandlerModel));

                if (!string.IsNullOrEmpty(_path))
                {
                    writer = new StreamWriter(@_path);
                    serializer.Serialize(writer, objModel);
                    writer.Close();
                    file = new FileInfo(@_path);
                }
                else
                {
                    writer = new StreamWriter(@Path.Combine(appDataPath, taskDatabaseFileName), false, new UTF8Encoding(false));
                    serializer.Serialize(writer, objModel);
                    writer.Close();
                    file = new FileInfo(@Path.Combine(appDataPath, taskDatabaseFileName));
                }

                zip.AddFile(string.IsNullOrWhiteSpace(_path) ?
                    @Path.Combine(appDataPath, chipDatabaseFileName) :
                    @_path, "");

                zip.Save(string.IsNullOrWhiteSpace(_path) ?
                    @Path.Combine(appDataPath, chipDatabaseFileNameCompressed) :
                    @_path);

            }
            catch (XmlException e)
            {
                LogWriter.CreateLogEntry(string.Format("{0}; {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""), FacilityName);
                Environment.Exit(0);
            }
        }

        public void DeleteDatabase()
        {
            File.Delete(Path.Combine(appDataPath, chipDatabaseFileName));
        }
    }
}