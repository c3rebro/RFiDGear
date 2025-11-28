using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using RFiDGear.DataAccessLayer;
using RFiDGear.Model;

namespace RFiDGear
{
    /// <summary>
    ///     Reads and writes the persisted application settings.
    /// </summary>
    public class SettingsReaderWriter : IDisposable
    {
        private readonly string appDataPath;
        private readonly EventLog eventLog = new EventLog("Application", ".", Assembly.GetEntryAssembly().GetName().Name);
        private readonly string updateConfigFileFileName = "update.xml";
        private readonly string updateURL = @"https://github.com/c3rebro/RFiDGear/releases/latest/download/update.xml";
        private readonly int updateInterval = 900;
        private readonly string securityToken = "D68EF3A7-E787-4CC4-B020-878BA649B4CD";
        private readonly string payload = "update.zip";
        private readonly string baseUri = @"https://github.com/c3rebro/RFiDGear/releases/latest/download/";
        private readonly string settingsFileFileName = "settings.xml";

        private readonly Version Version = Assembly.GetExecutingAssembly().GetName().Version;

        private bool disposed;
        private string infoText = "Version Info\n\ngoes here! \n==>";

        public SettingsReaderWriter()
            : this(new ProjectManager().AppDataPath)
        {
            new ProjectManager().EnsureSettingsFileExists();
        }

        public SettingsReaderWriter(string appDataPath)
        {
            this.appDataPath = appDataPath ?? throw new ArgumentNullException(nameof(appDataPath));
        }

        public DefaultSpecification DefaultSpecification { get; private set; } = new DefaultSpecification();

        public void InitUpdateFile()
        {
            XmlWriter xmlWriter;
            var xmlSettings = new XmlWriterSettings();
            xmlSettings.Encoding = new UTF8Encoding(false);

            xmlWriter = XmlWriter.Create(Path.Combine(appDataPath, updateConfigFileFileName), xmlSettings);
            xmlWriter.WriteStartDocument();
            xmlWriter.WriteStartElement("Manifest");
            xmlWriter.WriteAttributeString("version", string.Format("{0}.{1}.{2}", Version.Major, Version.Minor, Version.Build));

            xmlWriter.WriteEndElement();
            xmlWriter.Close();

            var doc = new XmlDocument();
            doc.Load(Path.Combine(appDataPath, updateConfigFileFileName));

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

                CheckIntervalElem.InnerText = updateInterval.ToString(CultureInfo.CurrentCulture);
                RemoteConfigUriElem.InnerText = updateURL;
                SecurityTokenElem.InnerText = securityToken;
                BaseUriElem.InnerText = baseUri;
                PayLoadElem.InnerText = payload;
                InfoTextElem.InnerText = infoText;

                doc.Save(Path.Combine(appDataPath, updateConfigFileFileName));
            }
        }

        public DefaultSpecification ReadSettings(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path cannot be null or whitespace.", nameof(filePath));
            }

            try
            {
                var serializer = new XmlSerializer(typeof(DefaultSpecification));

                using var reader = new StreamReader(filePath);

                DefaultSpecification = serializer.Deserialize(reader) as DefaultSpecification ?? new DefaultSpecification();
            }
            catch (Exception e) when (e is IOException || e is InvalidOperationException)
            {
                eventLog.WriteEntry(e.Message, EventLogEntryType.Error);
            }

            return DefaultSpecification;
        }

        public Task<DefaultSpecification> ReadSettingsAsync(string filePath)
        {
            return Task.Run(() => ReadSettings(filePath));
        }

        public async Task ReadSettings()
        {
            await ReadSettingsAsync().ConfigureAwait(false);
        }

        public DefaultSpecification ReadSettings()
        {
            return ReadSettings(Path.Combine(appDataPath, settingsFileFileName));
        }

        public Task<DefaultSpecification> ReadSettingsAsync()
        {
            return ReadSettingsAsync(Path.Combine(appDataPath, settingsFileFileName));
        }

        public void SaveSettings(DefaultSpecification specification, string path)
        {
            if (specification == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("File path cannot be null or whitespace.", nameof(path));
            }

            try
            {
                var serializer = new XmlSerializer(typeof(DefaultSpecification));

                using var textWriter = new StreamWriter(path, false);

                serializer.Serialize(textWriter, specification);

                DefaultSpecification = specification;
            }
            catch (XmlException e)
            {
                eventLog.WriteEntry(e.Message, EventLogEntryType.Error);
            }
        }

        public Task SaveSettingsAsync(DefaultSpecification specification, string path)
        {
            return Task.Run(() => SaveSettings(specification, path));
        }

        public void SaveSettings(DefaultSpecification specification)
        {
            SaveSettings(specification, Path.Combine(appDataPath, settingsFileFileName));
        }

        public Task SaveSettingsAsync(DefaultSpecification specification)
        {
            return SaveSettingsAsync(specification, Path.Combine(appDataPath, settingsFileFileName));
        }

        public async Task<bool> SaveSettings(string path = "")
        {
            try
            {
                await SaveSettingsAsync(DefaultSpecification, string.IsNullOrWhiteSpace(path) ? Path.Combine(appDataPath, settingsFileFileName) : path).ConfigureAwait(false);

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
            if (disposed)
            {
                return;
            }

            if (disposing)
            {
                try
                {
                    DefaultSpecification = null;
                }
                catch (Exception e)
                {
                    eventLog.WriteEntry(e.Message, EventLogEntryType.Error);
                }
            }

            disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
