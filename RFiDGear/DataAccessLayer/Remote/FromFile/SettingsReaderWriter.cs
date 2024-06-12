using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using RFiDGear.Model;

namespace RFiDGear
{
    /// <summary>
    /// Description of Class1.
    /// </summary>
    public class SettingsReaderWriter : IDisposable
    {
        #region fields
        private readonly EventLog eventLog = new EventLog("Application", ".", Assembly.GetEntryAssembly().GetName().Name);
        private readonly string _settingsFileFileName = "settings.xml";
        private readonly string _updateConfigFileFileName = "update.xml";
        private readonly string _updateURL = @"https://github.com/c3rebro/RFiDGear/releases/latest/download/update.xml";
        private readonly int _updateInterval = 900;
        private readonly string _securityToken = "D68EF3A7-E787-4CC4-B020-878BA649B4CD";
        private readonly string _payload = "update.zip";
        private string _infoText = "Version Info\n\ngoes here! \n==>";
        private readonly string _baseUri = @"https://github.com/c3rebro/RFiDGear/releases/latest/download/";


        private readonly Version Version = Assembly.GetExecutingAssembly().GetName().Version;
        private readonly string appDataPath;

        private bool _disposed;

        public DefaultSpecification DefaultSpecification
        {
            get
            {
                ReadSettings().GetAwaiter().GetResult();
                return defaultSpecification ?? new DefaultSpecification();
            }

            set
            {
                defaultSpecification = value;
                if (defaultSpecification != null)
                {
                    SaveSettings().GetAwaiter().GetResult();
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
            }
            catch (Exception e)
            {
                eventLog.WriteEntry(e.Message, EventLogEntryType.Error);
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
                    eventLog.WriteEntry(e.Message, EventLogEntryType.Error);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public void InitUpdateFile()
        {
            XmlWriter xmlWriter;
            var xmlSettings = new XmlWriterSettings();
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
                var CheckIntervalElem = doc.CreateElement("CheckInterval");
                var RemoteConfigUriElem = doc.CreateElement("RemoteConfigUri");
                var SecurityTokenElem = doc.CreateElement("SecurityToken");
                var BaseUriElem = doc.CreateElement("BaseUri");
                var PayLoadElem = doc.CreateElement("Payload");
                var InfoTextElem = doc.CreateElement("VersionInfoText");

                doc.DocumentElement.AppendChild(CheckIntervalElem);
                doc.DocumentElement.AppendChild(RemoteConfigUriElem);
                doc.DocumentElement.AppendChild(SecurityTokenElem);
                doc.DocumentElement.AppendChild(BaseUriElem);
                doc.DocumentElement.AppendChild(PayLoadElem);
                doc.DocumentElement.AppendChild(InfoTextElem);

                CheckIntervalElem.InnerText = _updateInterval.ToString(CultureInfo.CurrentCulture);
                RemoteConfigUriElem.InnerText = _updateURL;
                SecurityTokenElem.InnerText = _securityToken;
                BaseUriElem.InnerText = _baseUri;
                PayLoadElem.InnerText = _payload;
                InfoTextElem.InnerText = _infoText;

                doc.Save(Path.Combine(appDataPath, _updateConfigFileFileName));
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public async Task ReadSettings()
        {
            await ReadSettings("").ConfigureAwait(false);
            return;
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        public async Task<bool> ReadSettings(string _fileName)
        {
            TextReader reader;
            int verInfo;

            if (!string.IsNullOrWhiteSpace(_fileName) && !File.Exists(_fileName))
            {
                return false;
            }

            if (File.Exists(_fileName) || (string.IsNullOrWhiteSpace(_fileName) && File.Exists(Path.Combine(appDataPath, _settingsFileFileName))))
            {
                var doc = new XmlDocument();

                try
                {
                    await Task.Run(() =>
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

                            var node = doc.SelectSingleNode("//ManifestVersion");
                            verInfo = Convert.ToInt32(node.InnerText.Replace(".", string.Empty));

                            reader = new StreamReader(_fileName);
                        }

                        defaultSpecification = (serializer.Deserialize(reader) as DefaultSpecification);

                        reader.Close();

                    }).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    eventLog.WriteEntry(e.Message, EventLogEntryType.Error);

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
        public async Task<bool> SaveSettings(string _path = "")
        {
            try
            {
                await Task.Run(() => {

                    if (defaultSpecification == null)
                    {
                        return;
                    }

                    TextWriter textWriter;
                    var serializer = new XmlSerializer(typeof(DefaultSpecification));

                    textWriter = new StreamWriter(!string.IsNullOrEmpty(_path) ? @_path : @Path.Combine(appDataPath, _settingsFileFileName), false);

                    serializer.Serialize(textWriter, defaultSpecification);

                    textWriter.Close();
                }).ConfigureAwait(false);

                return true;
            }
            catch (XmlException e)
            {
                eventLog.WriteEntry(e.Message, EventLogEntryType.Error);
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
                        defaultSpecification = null;
                    }

                    catch (Exception e)
                    {
                        eventLog.WriteEntry(e.Message, EventLogEntryType.Error);
                    }
                }

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