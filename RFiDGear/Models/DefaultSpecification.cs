/*
 * Created by SharpDevelop.
 * Date: 12.10.2017
 * Time: 15:26
 *
 */

using RFiDGear.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml.Serialization;

namespace RFiDGear.Models
{
    /// <summary>
    /// Description of MifareClassicDefaultSpecification.
    /// </summary>
    [XmlRootAttribute("DefaultSpecification", IsNullable = false)]
    public class DefaultSpecification : IDisposable
    {
        private Version Version = Assembly.GetExecutingAssembly().GetName().Version;

        public DefaultSpecification()
        {
        }

        public DefaultSpecification(bool init)
        {
            ManifestVersion = string.Format("{0}.{1}.{2}", Version.Major, Version.Minor, Version.Build);

            var runtimeDefaults = DefaultSpecificationRuntimeDefaults.Current;

            _defaultReaderName = runtimeDefaults.DefaultReaderName;
            _defaultReaderProvider = runtimeDefaults.DefaultReaderProvider;
            _defaultLanguage = runtimeDefaults.DefaultLanguage;
            defaultAutoPerformTasksEnabled = runtimeDefaults.DefaultAutoPerformTasksEnabled;
            autoCheckForUpdates = runtimeDefaults.AutoCheckForUpdates;
            _autoLoadProjectOnStart = runtimeDefaults.AutoLoadProjectOnStart;
            _lastUsedProjectPath = runtimeDefaults.LastUsedProjectPath;
            LastUsedComPort = runtimeDefaults.LastUsedComPort;
            LastUsedBaudRate = runtimeDefaults.LastUsedBaudRate;

            mifareClassicDefaultSecuritySettings = runtimeDefaults.MifareClassicDefaultSecuritySettings
                .Select(key => new MifareClassicDefaultKeys(key.KeyNumber, key.AccessBits))
                .ToList();

            mifareDesfireDefaultSecuritySettings = runtimeDefaults.MifareDesfireDefaultSecuritySettings
                .Select(key => new MifareDesfireDefaultKeys(key.KeyType, key.EncryptionType, key.Key))
                .ToList();

            _classicCardDefaultSectorTrailer = runtimeDefaults.ClassicCardDefaultSectorTrailer;

            _classicCardDefaultQuickCheckKeys = runtimeDefaults.ClassicCardDefaultQuickCheckKeys.ToList();
        }

        #region properties

        /// <summary>
        ///
        /// </summary>
        public string ManifestVersion { get; set; }

        /// <summary>
        ///
        /// </summary>
        public string DefaultReaderName
        {
            get => _defaultReaderName;
            set => _defaultReaderName = value;
        }

        private string _defaultReaderName;

        /// <summary>
        ///
        /// </summary>
        public ReaderTypes DefaultReaderProvider
        {
            get => _defaultReaderProvider;
            set => _defaultReaderProvider = value;
        }

        private ReaderTypes _defaultReaderProvider;



        /// <summary>
        ///
        /// </summary>
        public bool AutoCheckForUpdates
        {
            get => autoCheckForUpdates;
            set => autoCheckForUpdates = value;
        }
        private bool autoCheckForUpdates;

        /// <summary>
        ///
        /// </summary>
        public string DefaultLanguage
        {
            get => _defaultLanguage;
            set => _defaultLanguage = value;
        }
        private string _defaultLanguage;

        /// <summary>
        ///
        /// </summary>
        public bool DefaultAutoPerformTasksEnabled
        {
            get => defaultAutoPerformTasksEnabled;
            set => defaultAutoPerformTasksEnabled = value;
        }

        private bool defaultAutoPerformTasksEnabled;

        public List<MifareDesfireDefaultKeys> MifareDesfireDefaultSecuritySettings
        {
            get => mifareDesfireDefaultSecuritySettings;
            set => mifareDesfireDefaultSecuritySettings = value;
        }

        private List<MifareDesfireDefaultKeys> mifareDesfireDefaultSecuritySettings;

        public List<MifareClassicDefaultKeys> MifareClassicDefaultSecuritySettings
        {
            get => mifareClassicDefaultSecuritySettings;
            set => mifareClassicDefaultSecuritySettings = value;
        }

        private List<MifareClassicDefaultKeys> mifareClassicDefaultSecuritySettings;

        /// <summary>
        ///
        /// </summary>
        public string MifareClassicDefaultSectorTrailer
        {
            get => _classicCardDefaultSectorTrailer;
            set => _classicCardDefaultSectorTrailer = value;
        }
        private string _classicCardDefaultSectorTrailer;

        /// <summary>
        ///
        /// </summary>
        public List<string> MifareClassicDefaultQuickCheckKeys
        {
            get => _classicCardDefaultQuickCheckKeys;
            set => _classicCardDefaultQuickCheckKeys = value;
        }

        private List<string> _classicCardDefaultQuickCheckKeys;

        /// <summary>
        ///
        /// </summary>
        public bool AutoLoadProjectOnStart
        {
            get => _autoLoadProjectOnStart;
            set => _autoLoadProjectOnStart = value;
        }
        private bool _autoLoadProjectOnStart;

        /// <summary>
        ///
        /// </summary>
        public string LastUsedProjectPath
        {
            get => _lastUsedProjectPath;
            set => _lastUsedProjectPath = value;
        }
        private string _lastUsedProjectPath;

        /// <summary>
        ///
        /// </summary>
        public string LastUsedBaudRate
        {
            get => _lastUsedBaudRate;
            set => _lastUsedBaudRate = value;
        }
        private string _lastUsedBaudRate;

        /// <summary>
        ///
        /// </summary>
        public string LastUsedComPort
        {
            get => _lastUsedComPort;
            set => _lastUsedComPort = value;
        }
        private string _lastUsedComPort;

        #endregion properties

        #region Extensions

        private bool _disposed;

        void IDisposable.Dispose()
        {
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Dispose any managed objects
                    // ...
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

        // Destructor
        ~DefaultSpecification()
        {
            Dispose(false);
        }

        #endregion Extensions
    }
}