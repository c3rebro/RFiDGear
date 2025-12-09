using RFiDGear;

using Microsoft.Extensions.Logging;

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using RedCell.Net;
using System.Windows.Threading;

namespace RedCell.Diagnostics.Update
{
    public class Updater : IDisposable
    {
        #region Constants
        private readonly EventLog eventLog = new EventLog("Application", ".", Assembly.GetEntryAssembly().GetName().Name);

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
        private DispatcherTimer _timer;
        private volatile bool _updating;
        private readonly Manifest _localConfig;
        private Manifest _remoteConfig;


        private readonly Process thisprocess = Process.GetCurrentProcess();
        private readonly string me;

        public bool AllowUpdate { get; set; }
        public bool UpdateAvailable { get; set; }
        public bool IsUserNotified { get; set; }
        public string UpdateInfoText { get; private set; }

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

                eventLog.WriteEntry(string.Format("Loaded"), EventLogEntryType.Information);
                eventLog.WriteEntry(string.Format("Initializing using file '{0}'.", configFile), EventLogEntryType.Information);
                if (!configFile.Exists)
                {
                    eventLog.WriteEntry(string.Format("Config file '{0}' does not exist, stopping.", configFile), EventLogEntryType.Warning);
                    return;
                }

                var data = File.ReadAllText(configFile.FullName, new UTF8Encoding(false));

                _localConfig = new Manifest(data);
#if DEBUG
                //_localConfig.Version = 0;
#endif
                if (_localConfig != null)
                {
                    Check(null, null);
                }
                var rootDirectory = new DirectoryInfo(Path.GetDirectoryName(me));
                var rootFiles = rootDirectory.GetFiles("*.*", SearchOption.TopDirectoryOnly);
            }

            catch (Exception e)
            {
                eventLog.WriteEntry(e.Message, EventLogEntryType.Error);
            }
        }
        #endregion

        #region Methods
        /// <summary>
        /// Starts the monitoring.
        /// </summary>
        public async Task StartMonitoring()
        {
            await Task.Run(() =>
            {
                if (_localConfig != null)
                {
                    eventLog.WriteEntry(string.Format("Starting monitoring every {0}s.", _localConfig.CheckInterval), EventLogEntryType.Information);
                    Check(null, null);

                    _timer = new DispatcherTimer
                    {
                        Interval = new TimeSpan(0, 0, 0, _localConfig.CheckInterval, 0)
                    };

                    _timer.Tick += Check;

                    _timer.Start();
                    _timer.IsEnabled = true;
                }
            }).ConfigureAwait(false);

            return;
        }

        /// <summary>
        /// Stops the monitoring.
        /// </summary>
        public async Task StopMonitoring()
        {
            await Task.Run(() =>
            {
                eventLog.WriteEntry(string.Format("Stopping monitoring."), EventLogEntryType.Information);
                if (_timer == null)
                {
                    eventLog.WriteEntry(string.Format("Monitoring was already stopped."), EventLogEntryType.Information);
                    return;
                }
                _timer.Stop();
            }).ConfigureAwait(false);

            return;
        }

        /// <summary>
        /// Checks the specified state.
        /// </summary>
        /// <param name="state"></param>
        /// <param name="args">true = download Changelog only</param>
        public async void Check(object state, EventArgs args)
        {
            try
            {
                var remoteUri = new Uri(_localConfig.RemoteConfigUri);

                eventLog.WriteEntry(string.Format("Fetching '{0}'.", _localConfig.RemoteConfigUri), EventLogEntryType.Information);
                var http = new Fetch { Retries = 5, RetrySleep = 30000, Timeout = 30000 };
                try
                {
                    http.Load(remoteUri.AbsoluteUri);

                    if (!http.Success)
                    {

                        try
                        {
                            eventLog.WriteEntry(string.Format("Fetch error: {0}", http.Response != null ? http.Response.StatusDescription : ""), EventLogEntryType.Error);
                        }

                        catch
                        {
                            eventLog.WriteEntry(string.Format("Fetch error: Unknown http Err"), EventLogEntryType.Information);
                        }

                        _remoteConfig = null;
                        return;
                    }
                }

                catch (Exception)
                {
                    _remoteConfig = null;
                    UpdateAvailable = false;
                    return;
                }

                var data = Encoding.UTF8.GetString(http.ResponseData);
                _remoteConfig = new Manifest(data);

                UpdateInfoText = _remoteConfig.VersionInfoText;

                if (AllowUpdate && !_updating)
                {
                    _timer.Interval = new TimeSpan(0, 0, 0, _localConfig.CheckInterval, 0);

                    _updating = true;
                    await Update();
                    _updating = false;
                    IsUserNotified = false;
                    eventLog.WriteEntry(string.Format("Check ending."), EventLogEntryType.Information);
                    return;
                }
                eventLog.WriteEntry(string.Format("Check starting."), EventLogEntryType.Information);

                if (_updating)
                {
                    eventLog.WriteEntry(string.Format("Updater is already updating."), EventLogEntryType.Warning);
                    eventLog.WriteEntry(string.Format("Check ending."), EventLogEntryType.Information); 
                    return;
                }

                if (_remoteConfig == null)
                {
                    UpdateAvailable = false;
                    return;
                }

                if (_localConfig.SecurityToken != _remoteConfig.SecurityToken)
                {
                    UpdateAvailable = false;
                    return;
                }

                eventLog.WriteEntry(string.Format("Remote config is valid."), EventLogEntryType.Information);
                eventLog.WriteEntry(string.Format("Local version is {0}", _localConfig.Version), EventLogEntryType.Information);
                eventLog.WriteEntry(string.Format("Remote version is {0}", _remoteConfig.Version), EventLogEntryType.Information);

                var versionComparison = _remoteConfig.Version?.CompareTo(_localConfig.Version ?? new Version()) ?? -1;

                if (versionComparison == 0)
                {
                    eventLog.WriteEntry(string.Format("Versions are the same. Check ending."), EventLogEntryType.Information);
                    UpdateAvailable = false;
                    return;
                }

                if (versionComparison < 0)
                {
                    eventLog.WriteEntry(string.Format("Remote version is older. That's weird o_O. Check ending."), EventLogEntryType.Warning);
                    UpdateAvailable = false;
                    return;
                }

                eventLog.WriteEntry(string.Format("Remote version is newer. Updating."), EventLogEntryType.Information);
                UpdateAvailable = true;
                /*
                if (_timer != null)
                {
                    _timer.Change(0, 1000);
                }
                */

                if (!AllowUpdate && !IsUserNotified)
                {
                    IsUserNotified = true;
                    UpdateInfoText = _remoteConfig.VersionInfoText;
                    return;
                }

                if (IsUserNotified && !AllowUpdate)
                {
                    await StopMonitoring();
                    return;
                }
            }
            catch (Exception e)
            {
                eventLog.WriteEntry(e.Message, EventLogEntryType.Error);
            }
        }

        /// <summary>
        /// Updates this instance.
        /// </summary>
        public async Task Update()
        {
            UpdateAvailable = true;

            await Task.Run(() =>
            {
                // Clean up failed attempts.
                if (Directory.Exists(Path.Combine(appDataPath, WorkPath)))
                {
                    //"WARNING: Work directory already exists."
                    try { Directory.Delete(Path.Combine(appDataPath, WorkPath), true); }
                    catch
                    {
                        return;
                    }
                }

                else
                {
                    try
                    {
                        Directory.CreateDirectory(Path.Combine(appDataPath, WorkPath));
                    }
                    catch
                    {
                        return;
                    }
                }


                // Download files in manifest.
                foreach (var update in _remoteConfig.Payloads)
                {
                    eventLog.WriteEntry(string.Format("Fetching '{0}'.", update), EventLogEntryType.Information); 
                    var url = _remoteConfig.BaseUri + update; //TODO: make this localizable ? e.g. + (settings.DefaultSpecification.DefaultLanguage == "german" ? "de-de/" : "en-us/")
                    var file = Fetch.Get(url);
                    if (file == null)
                    {
                        eventLog.WriteEntry(string.Format("Fetch failed."), EventLogEntryType.Error); 
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
                            ZipFile.ExtractToDirectory(zipfile, Path.Combine(appDataPath, WorkPath), overwriteFiles: true);

                            File.Delete(zipfile);

                            AllowUpdate = true;
                        }
                        catch
                        {
                            return;
                        }
                    }
                }
            }).ConfigureAwait(false);

            if (IsUserNotified && AllowUpdate)
            {
                var p = new Process();
                var info = new ProcessStartInfo()
                {
                    /*
                    FileName = "msiexec.exe",
                    Verb="runas",
                    Arguments = string.Format("/i \"{0}\" ", Path.Combine(appDataPath, WorkPath, "RFiDGearBundleSetup.exe")),
                    */
                    FileName = Path.Combine(appDataPath, WorkPath, "RFiDGearBundleSetup.exe"),
                    Verb = "runas",
                    UseShellExecute = false
                };

                try
                {
                    p.StartInfo = info;
                    p.Start();

                    thisprocess.Dispose();

                    Environment.Exit(0);
                }

                catch
                {
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
