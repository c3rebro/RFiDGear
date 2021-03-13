using RFiDGear;
using RFiDGear.DataAccessLayer;

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
    public class Updater
    {
        #region Constants

        private static readonly string appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RFiDGear");

        /// <summary>
        /// The default check interval
        /// </summary>
        public const int DefaultCheckInterval = 900; // 900s == 15 min
        private const int FirstCheckDelay = 15;

        /// <summary>
        /// The default configuration file
        /// </summary>
        public const string DefaultConfigFile = "update.xml";

        public const string WorkPath = "work";
        #endregion

        #region Fields
        //private readonly SettingsReaderWriter settings;
        private Timer _timer;
        private volatile bool _updating;
        private readonly Manifest _localConfig;
        private Manifest _remoteConfig;
        private readonly FileInfo _localConfigFile;


        private readonly Process thisprocess = Process.GetCurrentProcess();
        private readonly string me;

        public bool AllowUpdate { get; set; }
        public bool IsUserNotified { get; set; }

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

                //settings = new SettingsReaderWriter();

                Log.Debug = true;

                _localConfigFile = configFile;
                Log.Write("Loaded.");
                Log.Write("Initializing using file '{0}'.", configFile.FullName);
                if (!configFile.Exists)
                {
                    Log.Write("Config file '{0}' does not exist, stopping.", configFile.Name);
                    return;
                }

                string data = File.ReadAllText(configFile.FullName, new UTF8Encoding(false));
                this._localConfig = new Manifest(data);

                var rootDirectory = new DirectoryInfo(Path.GetDirectoryName(me));
                var rootFiles = rootDirectory.GetFiles("*.*", SearchOption.TopDirectoryOnly);
            }

            catch(Exception e)
            {
                LogWriter.CreateLogEntry(string.Format("{0}\n{1}", e.Message, e.InnerException != null ? e.InnerException.Message : ""));
            }
            //LogWriter.CreateLogEntry(string.Format("{0}\n{1}",e.Message, e.InnerException != null ? e.InnerException.Message : ""));
        }
        #endregion

        #region Methods
        /// <summary>
        /// Starts the monitoring.
        /// </summary>
        public void StartMonitoring()
        {
            if (this._localConfig != null)
            {
                Log.Write("Starting monitoring every {0}s.", this._localConfig.CheckInterval);
                _timer = new Timer(Check, null, 5000, this._localConfig.CheckInterval * 1000);
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
                if (IsUserNotified && !AllowUpdate)
                    return;

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
                        this._remoteConfig = null;
                        return;
                    }
                }

                catch (Exception e)
                {
                    LogWriter.CreateLogEntry(string.Format("{0}\n{1}", e.Message, e.InnerException != null ? e.InnerException.Message : ""));
                    this._remoteConfig = null;
                    return;
                }

                string data = Encoding.UTF8.GetString(http.ResponseData);
                this._remoteConfig = new Manifest(data);

                //string.Format("{0}{1}",this._localConfig.RemoteConfigUri,settings.DefaultLanguage == "german" ? "/de-de" : "/en-us")
                if (this._remoteConfig == null)
                    return;

                if (this._localConfig.SecurityToken != this._remoteConfig.SecurityToken)
                {
                    Log.Write("Security token mismatch.");
                    return;
                }
                Log.Write("Remote config is valid.");
                Log.Write("Local version is  {0}.", this._localConfig.Version);
                Log.Write("Remote version is {0}.", this._remoteConfig.Version);

                if (this._remoteConfig.Version == this._localConfig.Version)
                {
                    Log.Write("Versions are the same.");
                    Log.Write("Check ending.");
                    return;
                }
                if (this._remoteConfig.Version < this._localConfig.Version)
                {
                    Log.Write("Remote version is older. That's weird.");
                    Log.Write("Check ending.");
                    return;
                }

                Log.Write("Remote version is newer. Updating.");
                _timer.Change(0, 1000);

                if (!AllowUpdate && !IsUserNotified)
                {
                    IsUserNotified = true;
                    NewVersionAvailable(this, null);
                    return;
                }
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""));
            }
        }

        /// <summary>
        /// Updates this instance.
        /// </summary>
        private void Update()
        {

            Log.Write("Updating '{0}' files.", this._remoteConfig.Payloads.Length);

            // Clean up failed attempts.
            if (Directory.Exists(Path.Combine(appDataPath, WorkPath)))
            {
                Log.Write("WARNING: Work directory already exists.");
                try { Directory.Delete(Path.Combine(appDataPath, WorkPath), true); }
                catch (IOException e)
                {
                    LogWriter.CreateLogEntry(string.Format("{0}\n{1}", e.Message, e.InnerException != null ? e.InnerException.Message : ""));
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
                    LogWriter.CreateLogEntry(string.Format("{0}\n{1}", e.Message, e.InnerException != null ? e.InnerException.Message : ""));
                    return;
                }
            }


            // Download files in manifest.
            foreach (string update in this._remoteConfig.Payloads)
            {
                Log.Write("Fetching '{0}'.", update);
                var url = this._remoteConfig.BaseUri  + update; //TODO: make this localizable e.g. + (settings.DefaultSpecification.DefaultLanguage == "german" ? "de-de/" : "en-us/")
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
                            zip.ExtractAll(Path.Combine(appDataPath, WorkPath), ExtractExistingFileAction.Throw);
                        File.Delete(zipfile);
                    }
                    catch (Exception e)
                    {
                        LogWriter.CreateLogEntry(string.Format("{0}\n{1}", e.Message, e.InnerException != null ? e.InnerException.Message : ""));
                        return;
                    }
                }
            }

            if (IsUserNotified && AllowUpdate)
            {
                Process p = new Process();
                ProcessStartInfo info = new ProcessStartInfo(Path.Combine(appDataPath, WorkPath, "RFiDGearBundleSetup.exe"))
                {
                    //info.Arguments = string.Format("/i {0}", Path.Combine(appDataPath, WorkPath, "RFiDGearBundleSetup.exe"));
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
                    LogWriter.CreateLogEntry(string.Format("{0}\n{1}", e.Message, e.InnerException != null ? e.InnerException.Message : ""));
                    return;
                }
            }
        }
        #endregion
    }
}
