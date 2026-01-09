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
    /// Reads and writes chip and task database XML files in the user profile.
    /// </summary>
    public class DatabaseReaderWriter
    {
        #region fields

        private readonly Version Version = Assembly.GetExecutingAssembly().GetName().Version;

        private const string chipDatabaseFileName = "chipdatabase.xml";
        private const string taskDatabaseFileName = "taskdatabase.xml";
        private string appDataPath;


        #endregion fields

        public DatabaseReaderWriter()
        {
            try
            {
                appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RFiDGear");

                if (!Directory.Exists(appDataPath))
                    Directory.CreateDirectory(appDataPath);


                if (!File.Exists(Path.Combine(appDataPath, chipDatabaseFileName)))
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(ObservableCollection<RFiDChipParentLayerViewModel>));

                    TextWriter writer = new StreamWriter(Path.Combine(appDataPath, chipDatabaseFileName));

                    writer.Close();
                }

                if (!File.Exists(Path.Combine(appDataPath, taskDatabaseFileName)))
                {
                    TextWriter writer = new StreamWriter(Path.Combine(appDataPath, taskDatabaseFileName));

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
        /// Attempts to load the chip or task database from disk.
        /// </summary>
        /// <param name="_fileName">Optional explicit file path to load.</param>
        /// <returns><c>true</c> when a blocking error occurs; otherwise <c>false</c>.</returns>
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

        /// <summary>
        /// Deletes the chip database file from the local application data folder.
        /// </summary>
        public void DeleteDatabase()
        {
            File.Delete(Path.Combine(appDataPath, chipDatabaseFileName));
        }
    }
}
