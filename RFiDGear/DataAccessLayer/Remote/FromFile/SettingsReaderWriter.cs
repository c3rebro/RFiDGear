using RFiDGear.DataAccessLayer;
using RFiDGear.Model;

using Log4CSharp;

using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace RFiDGear
{
    /// <summary>
    /// Description of Class1.
    /// </summary>
    public class SettingsReaderWriter : IDisposable
    {
        #region fields
        private static readonly string FacilityName = "RFiDGear";

        private readonly string FacilityName = "RFiDGear";
        private readonly string _settingsFileFileName = "settings.xml";
        private readonly string _updateConfigFileFileName = "update.xml";
        private readonly string _updateURL = @"https://github.com/c3rebro/RFiDGear/releases/latest/download/update.xml";
        private readonly int _updateInterval = 900;
        private readonly string _securityToken = "D68EF3A7-E787-4CC4-B020-878BA649B4CD";
        private readonly string _payload = "update.zip";
        private readonly string _infoText = "Version Info\n\ngoes here! \n==>";
        private readonly string _baseUri = @"https://github.com/c3rebro/RFiDGear/releases/latest/download/";

        private readonly Version Version = Assembly.GetExecutingAssembly().GetName().Version;

        private readonly string appDataPath;

        private bool _disposed = false;

        private XmlWriter xmlWriter;

        public DefaultSpecification DefaultSpecification
        {
            get
            {
                ReadSettings();
                return defaultSpecification ?? new DefaultSpecification();
            }

            set
            {
                defaultSpecification = value;
                if (defaultSpecification != null)
                {
                    SaveSettings();
                }
            }
        }

        private DefaultSpecification defaultSpecification;

        #endregion fields

        public SettingsReaderWriter()
        {
            try
            {
                appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

                appDataPath = Path.Combine(appDataPath, "RFiDGear");

                if (!Directory.Exists(appDataPath))
                {
                    Directory.CreateDirectory(appDataPath);
                }

                XmlWriterSettings xmlSettings = new XmlWriterSettings();
                xmlSettings.Encoding = new UTF8Encoding(false);

                xmlWriter = XmlWriter.Create(Path.Combine(appDataPath, _updateConfigFileFileName), xmlSettings);
                xmlWriter.WriteStartDocument();
                xmlWriter.WriteStartElement("Manifest");
                xmlWriter.WriteAttributeString("version", string.Format("{0}.{1}.{2}", Version.Major, Version.Minor, Version.Build));

                xmlWriter.WriteEndElement();
                xmlWriter.Close();

                var doc = new XmlDocument();
                doc.Load(Path.Combine(appDataPath, _updateConfigFileFileName));

                if (doc.SelectSingleNode("//CheckInterval") == null)
                {
                    XmlElement CheckIntervalElem = doc.CreateElement("CheckInterval");
                    XmlElement RemoteConfigUriElem = doc.CreateElement("RemoteConfigUri");
                    XmlElement SecurityTokenElem = doc.CreateElement("SecurityToken");
                    XmlElement BaseUriElem = doc.CreateElement("BaseUri");
                    XmlElement PayLoadElem = doc.CreateElement("Payload");
                    XmlElement InfoTextElem = doc.CreateElement("VersionInfoText");

                    doc.DocumentElement.AppendChild(CheckIntervalElem);
                    doc.DocumentElement.AppendChild(RemoteConfigUriElem);
                    doc.DocumentElement.AppendChild(SecurityTokenElem);
                    doc.DocumentElement.AppendChild(BaseUriElem);
                    doc.DocumentElement.AppendChild(PayLoadElem);
                    doc.DocumentElement.AppendChild(InfoTextElem);

                    CheckIntervalElem.InnerText = _updateInterval.ToString();
                    RemoteConfigUriElem.InnerText = _updateURL;
                    SecurityTokenElem.InnerText = _securityToken;
                    BaseUriElem.InnerText = _baseUri;
                    PayLoadElem.InnerText = _payload;
                    InfoTextElem.InnerText = _infoText;

                    doc.Save(Path.Combine(appDataPath, _updateConfigFileFileName));
                }
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""), FacilityName);
            }

            if (!File.Exists(Path.Combine(appDataPath, _settingsFileFileName)))
            {
                try
                {
                    defaultSpecification = new DefaultSpecification(true);

                    var serializer = new XmlSerializer(defaultSpecification.GetType());

                    var txtWriter = new StreamWriter(Path.Combine(appDataPath, _settingsFileFileName));

                    serializer.Serialize(txtWriter, defaultSpecification);

                    txtWriter.Close();
                }
                catch (Exception e)
                {
                    LogWriter.CreateLogEntry(string.Format("{0}; {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""), FacilityName);
                }
            }
            //else
            //ReadSettings();
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        public bool ReadSettings(string _fileName = "")
        {
            TextReader reader;
            int verInfo;

            if (!string.IsNullOrWhiteSpace(_fileName) && !File.Exists(_fileName))
            {
                return false;
            }

            if (File.Exists(_fileName) || (string.IsNullOrWhiteSpace(_fileName) && File.Exists(Path.Combine(appDataPath, _settingsFileFileName))))
            {
                //Path.Combine(appDataPath,databaseFileName)
                var doc = new XmlDocument();

                try
                {
                    var serializer = new XmlSerializer(typeof(DefaultSpecification));

                    if (string.IsNullOrWhiteSpace(_fileName) && File.Exists(Path.Combine(appDataPath, _settingsFileFileName)))
                    {
                        doc.Load(@Path.Combine(appDataPath, _settingsFileFileName));

                        var node = doc.SelectSingleNode("//ManifestVersion");
                        verInfo = Convert.ToInt32(node.InnerText.Replace(".", string.Empty));

                        reader = new StreamReader(Path.Combine(appDataPath, _settingsFileFileName));
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
                        throw new Exception(
                            string.Format("database that was tried to open is newer ({0}) than this version of rfidgear ({1})"
                                          , verInfo, Convert.ToInt32(string.Format("{0}{1}{2}", Version.Major, Version.Minor, Version.Build))
                                         )
                        );
                    }

                    //defaultSpecification = new DefaultSpecification();
                    defaultSpecification = (serializer.Deserialize(reader) as DefaultSpecification);

                    reader.Close();
                }
                catch (Exception e)
                {
                    LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""), FacilityName);

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
            try
            {
                TextWriter textWriter;
                var serializer = new XmlSerializer(typeof(DefaultSpecification));

                textWriter = new StreamWriter(!string.IsNullOrEmpty(_path) ? @_path : @Path.Combine(appDataPath, _settingsFileFileName), false);

                serializer.Serialize(textWriter, defaultSpecification);

                textWriter.Close();

                return true;
            }
            catch (XmlException e)
            {
                LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""), FacilityName);
                return false;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    try
                    {
                        if (xmlWriter != null)
                        {
                            xmlWriter.Close();
                        }

                        defaultSpecification = null;
                        // Dispose any managed objects
                        // ...
                    }

                    catch (Exception e)
                    {
                        LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""), FacilityName);
                    }
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
    }
}