using RFiDGear;
using RFiDGear.DataAccessLayer;

using Log4CSharp;

using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Ionic.Zip;
using RedCell.Net;

namespace RedCell.Diagnostics.Update
{
    public class Updater : IDisposable
    {
        #region Constants
        private static readonly string FacilityName = "RFiDGear";

        private static readonly string appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RFiDGear");

        private bool _disposed;
        /// <summary>
        /// The default check interval
        /// </summary>
        public static readonly int DefaultCheckInterval = 900; // 900s == 15 min

        /// <summary>
        /// The default configuration file
        /// </summary>
        public static readonly string DefaultConfigFile = "update.xml";

        public static readonly string WorkPath = "work";
        #endregion

        #region Fields
        //private readonly SettingsReaderWriter settings;
        private Timer _timer;
        private volatile bool _updating;
        private readonly Manifest _localConfig;
        private Manifest _remoteConfig;


        private readonly Process thisprocess = Process.GetCurrentProcess();
        private readonly string me;

        public bool AllowUpdate { get; set; }
        public bool IsUserNotified { get; set; }
        public string UpdateInfoText { get; private set; }

        #endregion

        #region events

        public event EventHandler NewVersionAvailable;

        #endregion

        #region Initialization
        /// <summary>
        /// Initializes a new instance of the <see cref="Updater"/> class.
        /// </summary>
        public Updater()
            : this(new FileInfo(Path.Combine(appDataPath, DefaultConfigFile)))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Updater"/> class.
        /// </summary>
        /// <param name="configFile">The configuration file.</param>
        public Updater(FileInfo configFile)
        {
            try
            {
                me = thisprocess.MainModule.FileName;

                Log.Write("Loaded.");
                Log.Write("Initializing using file '{0}'.", configFile.FullName);
                if (!configFile.Exists)
                {
                    Log.Write("Config file '{0}' does not exist, stopping.", configFile.Name);
                    return;
                }

                string data = File.ReadAllText(configFile.FullName, new UTF8Encoding(false));

                _localConfig = new Manifest(data);
#if DEBUG
                _localConfig.Version = 0;
#endif
                var rootDirectory = new DirectoryInfo(Path.GetDirectoryName(me));
                var rootFiles = rootDirectory.GetFiles("*.*", SearchOption.TopDirectoryOnly);
            }

            catch (Exception e)
            {
                LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""), FacilityName);

            }
        }
        #endregion

        #region Methods
        /// <summary>
        /// Starts the monitoring.
        /// </summary>
        public void StartMonitoring()
        {
            if (_localConfig != null)
            {
                Log.Write("Starting monitoring every {0}s.", _localConfig.CheckInterval);
                Check(null);

                _timer = new Timer(Check, null, 5000, _localConfig.CheckInterval * 1000);
            }

        }

        /// <summary>
        /// Stops the monitoring.
        /// </summary>
        public void StopMonitoring()
        {
            Log.Write("Stopping monitoring.");
            if (_timer == null)
            {
                Log.Write("Monitoring was already stopped.");
                return;
            }
            _timer.Dispose();
        }

        /// <summary>
        /// Checks the specified state.
        /// </summary>
        /// <param name="state">The state.</param>
        private void Check(object state)
        {
            try
            {
                if (AllowUpdate && !_updating)
                {
                    _timer.Change(5000, DefaultCheckInterval * 1000);

                    _updating = true;
                    Update();
                    _updating = false;
                    IsUserNotified = false;
                    Log.Write("Check ending.");
                    return;
                }
                Log.Write("Check starting.");

                if (_updating)
                {
                    Log.Write("Updater is already updating.");
                    Log.Write("Check ending.");
                    return;
                }

                var remoteUri = new Uri(_localConfig.RemoteConfigUri);

                Log.Write("Fetching '{0}'.", remoteUri.AbsoluteUri);
                var http = new Fetch { Retries = 5, RetrySleep = 30000, Timeout = 30000 };
                try
                {
                    http.Load(remoteUri.AbsoluteUri);

                    if (!http.Success)
                    {
                        try
                        {
                            Log.Write("Fetch error: {0}", http.Response != null ? http.Response.StatusDescription : "");
                        }

                        catch
                        {
                            Log.Write("Fetch error: Unknown http Err");
                        }
                        _remoteConfig = null;
                        return;
                    }
                }

                catch (Exception e)
                {
                    LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""), FacilityName);
                    _remoteConfig = null;

                    return;
                }

                string data = Encoding.UTF8.GetString(http.ResponseData);
                _remoteConfig = new Manifest(data);

                if (_remoteConfig == null)
                {
                    return;
                }

                if (_localConfig.SecurityToken != _remoteConfig.SecurityToken)
                {
                    LogWriter.CreateLogEntry(string.Format("{0}: {1}", DateTime.Now, "Security token mismatch."), FacilityName);
                    return;
                }
                LogWriter.CreateLogEntry(string.Format("{0}: {1}", DateTime.Now, "Remote config is valid."), FacilityName);
                LogWriter.CreateLogEntry(string.Format("{0}: {1}, {2}", DateTime.Now, "Local version is ", _localConfig.Version), FacilityName);
                LogWriter.CreateLogEntry(string.Format("{0}: {1}, {2}", DateTime.Now, "Remote version is ", _remoteConfig.Version), FacilityName);

                if (_remoteConfig.Version == _localConfig.Version)
                {
                    LogWriter.CreateLogEntry(string.Format("{0}: {1}", DateTime.Now, "Versions are the same. Check ending."), FacilityName);
                    return;
                }
                if (_remoteConfig.Version < _localConfig.Version)
                {
                    LogWriter.CreateLogEntry(string.Format("{0}: {1}", DateTime.Now, "Remote version is older. That's weird. Check ending."), FacilityName);
                    return;
                }

                LogWriter.CreateLogEntry(string.Format("{0}: {1}", DateTime.Now, "Remote version is newer. Updating."), FacilityName);
                _timer.Change(0, 1000);

                if (!AllowUpdate && !IsUserNotified)
                {
                    IsUserNotified = true;
                    UpdateInfoText = _remoteConfig.VersionInfoText;
                    NewVersionAvailable(this, null);
                    return;
                }

                if (IsUserNotified && !AllowUpdate)
                {
                    StopMonitoring();
                    return;
                }
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(e, FacilityName);
            }
        }

        /// <summary>
        /// Updates this instance.
        /// </summary>
        public void Update()
        {

            Log.Write("Updating '{0}' files.", _remoteConfig.Payloads.Length);

            // Clean up failed attempts.
            if (Directory.Exists(Path.Combine(appDataPath, WorkPath)))
            {
                Log.Write("WARNING: Work directory already exists.");
                try { Directory.Delete(Path.Combine(appDataPath, WorkPath), true); }
                catch (IOException e)
                {
                    LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""), FacilityName);

                    return;
                }
            }

            else
            {
                try
                {
                    Directory.CreateDirectory(Path.Combine(appDataPath, WorkPath));
                }
                catch (Exception e)
                {
                    LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""), FacilityName);

                    return;
                }
            }


            // Download files in manifest.
            foreach (string update in _remoteConfig.Payloads)
            {
                Log.Write("Fetching '{0}'.", update);
                var url = _remoteConfig.BaseUri + update; //TODO: make this localizable ? e.g. + (settings.DefaultSpecification.DefaultLanguage == "german" ? "de-de/" : "en-us/")
                var file = Fetch.Get(url);
                if (file == null)
                {
                    Log.Write("Fetch failed.");
                    return;
                }
                var info = new FileInfo(Path.Combine(Path.Combine(appDataPath, WorkPath), update));
                Directory.CreateDirectory(info.DirectoryName);
                File.WriteAllBytes(Path.Combine(Path.Combine(appDataPath, WorkPath), update), file);

                // Unzip
                if (Regex.IsMatch(update, @"\.zip"))
                {
                    try
                    {
                        var zipfile = Path.Combine(Path.Combine(appDataPath, WorkPath), update);
                        using (var zip = ZipFile.Read(zipfile))
                        {
                            zip.ExtractAll(Path.Combine(appDataPath, WorkPath), ExtractExistingFileAction.Throw);
                        }

                        File.Delete(zipfile);

                        AllowUpdate = true;
                    }
                    catch (Exception e)
                    {
                        LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""), FacilityName);

                        return;
                    }
                }
            }

            if (IsUserNotified && AllowUpdate)
            {
                Process p = new Process();
                ProcessStartInfo info = new ProcessStartInfo()
                {
                    FileName = "msiexec.exe",
                    Arguments = string.Format("/i \"{0}\" /lv \"c:\\temp\\rfidgeardeploy.log\"", Path.Combine(appDataPath, WorkPath, "Setup.msi")),
                    UseShellExecute = false
                };

                try
                {
                    p.StartInfo = info;
                    p.Start();

                    thisprocess.Dispose();

                    Environment.Exit(0);
                }

                catch (Exception e)
                {
                    LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""), FacilityName);

                    return;
                }
            }
        }
        #endregion

        protected void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {

                }

                _disposed = true;
            }
        }

        public void Dispose()
        {
            _disposed = false;
            Dispose(true);
        }

    }
}
