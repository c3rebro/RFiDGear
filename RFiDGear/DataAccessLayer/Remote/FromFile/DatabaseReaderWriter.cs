﻿using CommunityToolkit.Mvvm.Input;

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using Ionic.Zip;

using RFiDGear.Model;
using RFiDGear.ViewModel;

namespace RFiDGear.DataAccessLayer
{
    /// <summary>
    /// Serialize and Deserialize Chips and Tasks
    /// </summary>
    public class DatabaseReaderWriter
    {
        #region fields
        private readonly Version Version = Assembly.GetExecutingAssembly().GetName().Version;
        private readonly EventLog eventLog = new EventLog("Application", ".", "RFiDGear");

        private const string chipDatabaseFileName = "chipdatabase.xml";
        private const string taskDatabaseFileNameCompressed = "chipdatabase.rfPrj";
        private const string taskDatabaseFileName = "taskdatabase.xml";
        private readonly string appDataPath;

        public ObservableCollection<RFiDChipParentLayerViewModel> TreeViewModel;
        public ChipTaskHandlerModel SetupModel;
        public IAsyncRelayCommand AsyncRelayCommandLoadDB { get;  }

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

                TreeViewModel = new ObservableCollection<RFiDChipParentLayerViewModel>();
                SetupModel = new ChipTaskHandlerModel();

                if (!File.Exists(Path.Combine(appDataPath, chipDatabaseFileName)))
                {
                    var serializer = new XmlSerializer(typeof(ObservableCollection<RFiDChipParentLayerViewModel>));

                    TextWriter writer = new StreamWriter(Path.Combine(appDataPath, chipDatabaseFileName));

                    serializer.Serialize(writer, TreeViewModel);

                    writer.Close();
                }

                if (File.Exists(Path.Combine(appDataPath, taskDatabaseFileName)))
                {
                    File.Delete(Path.Combine(appDataPath, taskDatabaseFileName));
                }

                foreach(var file in Directory.GetFiles(appDataPath))
                {
                    var fi = new FileInfo(file);
                    if (fi.Extension.ToLower(CultureInfo.CurrentCulture).Contains("rfprj"))
                    {
                        fi.Delete();
                    }
                }
            }
            catch (Exception e)
            {
                eventLog.WriteEntry(e.Message, EventLogEntryType.Error);
                return;
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        public async Task<bool> ReadDatabase(string _fileName = "")
        {
            var verInfo = 0;
            FileInfo file;

            if (string.IsNullOrWhiteSpace(_fileName) || !File.Exists(_fileName))
            {
                return false;
            }
            else
            {
                file = new FileInfo(_fileName);
            }

            await Task.Run(() =>
            {
                try
                {
                    var doc = new XmlDocument();

                    if (file.Extension.ToLower(CultureInfo.CurrentCulture) == ".xml")
                    {
                        doc.Load(@_fileName);
                        TextReader reader = new StreamReader(_fileName);

                        var node = doc.SelectSingleNode("//ManifestVersion");
                        verInfo = Convert.ToInt32(node.InnerText.Replace(".", string.Empty));

                        try
                        {
                            AsyncRelayCommandLoadDB.Execute(reader);
                        }
                        catch (Exception e)
                        {
                            eventLog.WriteEntry(e.Message, EventLogEntryType.Error);
                        }

                    }

                    if (file.Extension.ToLower(CultureInfo.CurrentCulture) == ".rfprj")
                    {
                        using (var zip1 = ZipFile.Read(string.IsNullOrWhiteSpace(_fileName) ?
                        @Path.Combine(appDataPath, taskDatabaseFileNameCompressed) :
                        _fileName))
                        {
                            if (Directory.GetFiles(appDataPath, "*.tmp").Length > 0)
                            {
                                foreach (var tempFile in Directory.GetFiles(appDataPath, "*.tmp"))
                                {
                                    File.Delete(tempFile);
                                }
                            }
                            zip1.ExtractAll(appDataPath, ExtractExistingFileAction.OverwriteSilently);
                        }

                        TextReader reader = null;

                        if (File.Exists(@Path.Combine(appDataPath, file.Name)))
                        {
                            doc.Load(@Path.Combine(appDataPath, file.Name));
                            var node = doc.SelectSingleNode("//ManifestVersion");
                            verInfo = Convert.ToInt32(node.InnerText.Replace(".", string.Empty));
                            reader = new StreamReader(@Path.Combine(appDataPath, file.Name));
                        } // old Variant. Needed to open old databases
                        else if(File.Exists(@Path.Combine(appDataPath, taskDatabaseFileName)))
                        {
                            doc.Load(@Path.Combine(appDataPath, taskDatabaseFileName));
                            var node = doc.SelectSingleNode("//ManifestVersion");
                            verInfo = Convert.ToInt32(node.InnerText.Replace(".", string.Empty));
                            reader = new StreamReader(@Path.Combine(appDataPath, taskDatabaseFileName));
                        }
                    
                        try
                        {
                            XmlSerializer serializer = new XmlSerializer(typeof(ChipTaskHandlerModel));
                            SetupModel = (serializer.Deserialize(reader) as ChipTaskHandlerModel);
                            reader.Close();
                        }
                        catch (Exception e)
                        {
                            eventLog.WriteEntry(e.Message, EventLogEntryType.Error);
                        }
                    }

                    if (verInfo > Convert.ToInt32(string.Format("{0}{1}{2}", Version.Major, Version.Minor, Version.Build)))
                    {
                        eventLog.WriteEntry(string.Format("{0}; {1}", DateTime.Now, string.Format("database that was tried to open is newer ({0}) than this version of rfidgear ({1})"
                                                                                                      , verInfo, Convert.ToInt32(string.Format("{0}{1}{2}", Version.Major, Version.Minor, Version.Build)))), EventLogEntryType.Warning);
                        return ;
                    }
                }
                catch (Exception e)
                {
                    eventLog.WriteEntry(e.Message, EventLogEntryType.Error);
                    return ;
                }
            }).ConfigureAwait(false);

            return false;
        }

        public void WriteDatabase(ObservableCollection<RFiDChipParentLayerViewModel> objModel, string _path)
        {
            try
            {
                TextWriter writer;
                var serializer = new XmlSerializer(typeof(ObservableCollection<RFiDChipParentLayerViewModel>));

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
            }
            catch (XmlException e)
            {
                eventLog.WriteEntry(e.Message, EventLogEntryType.Error);
                Environment.Exit(0);
            }
        }

        public void WriteDatabase(ChipTaskHandlerModel objModel, string _path = "")
        {
            try
            {
                var zip = new ZipFile();
                FileInfo file;

                TextWriter writer;
                var serializer = new XmlSerializer(typeof(ChipTaskHandlerModel));

                if (!string.IsNullOrEmpty(_path))
                {
                    writer = new StreamWriter(Path.Combine(appDataPath, taskDatabaseFileName));
                    serializer.Serialize(writer, objModel);
                    writer.Close();
                }
                else
                {
                    writer = new StreamWriter(@Path.Combine(appDataPath, taskDatabaseFileName), false, new UTF8Encoding(false));
                    serializer.Serialize(writer, objModel);
                    writer.Close();
                    file = new FileInfo(@Path.Combine(appDataPath, taskDatabaseFileName));
                }

                zip.AddFile(@Path.Combine(appDataPath, taskDatabaseFileName), "");

                zip.Save(string.IsNullOrWhiteSpace(_path) ?
                    @Path.Combine(appDataPath, taskDatabaseFileNameCompressed) :
                    @_path);

            }
            catch (XmlException e)
            {
                eventLog.WriteEntry(e.Message, EventLogEntryType.Error);
            }
        }

        public void DeleteDatabase()
        {
            File.Delete(Path.Combine(appDataPath, chipDatabaseFileName));
        }
    }
}