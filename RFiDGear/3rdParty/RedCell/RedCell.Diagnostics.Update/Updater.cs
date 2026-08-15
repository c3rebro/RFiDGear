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
using Serilog;
using SerilogLog = Serilog.Log;

namespace RedCell.Diagnostics.Update
{
    public class Updater : IDisposable
    {
        #region Constants
        private static readonly ILogger Logger = SerilogLog.ForContext<Updater>();

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

                Logger.Information("Loaded updater");
                Logger.Information("Initializing using file '{ConfigFile}'", configFile);
                if (!configFile.Exists)
                {
                    Logger.Warning("Config file '{ConfigFile}' does not exist, stopping.", configFile);
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
                Logger.Error(e, "Failed to initialize updater");
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
                    Logger.Information("Starting monitoring every {CheckInterval}s.", _localConfig.CheckInterval);
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
                Logger.Information("Stopping monitoring.");
                if (_timer == null)
                {
                    Logger.Information("Monitoring was already stopped.");
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

                Logger.Information("Fetching '{RemoteConfigUri}'.", _localConfig.RemoteConfigUri);
                var http = new Fetch { Retries = 5, RetrySleep = 30000, Timeout = 30000 };
                try
                {
                    http.Load(remoteUri.AbsoluteUri);

                    if (!http.Success)
                    {

                        try
                        {
                            Logger.Error("Fetch error: {ReasonPhrase}", http.Response != null ? http.Response.ReasonPhrase : "");
                        }

                        catch
                        {
                            Logger.Information("Fetch error: Unknown http Err");
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
                    Logger.Information("Check ending.");
                    return;
                }
                Logger.Information("Check starting.");

                if (_updating)
                {
                    Logger.Warning("Updater is already updating.");
                    Logger.Information("Check ending.");
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

                Logger.Information("Remote config is valid.");
                var effectiveLocalVersion = GetEffectiveLocalVersion();
                Logger.Information("Local version is {LocalVersion}", effectiveLocalVersion);
                Logger.Information("Remote version is {RemoteVersion}", _remoteConfig.Version);

                var versionComparison = _remoteConfig.Version?.CompareTo(effectiveLocalVersion) ?? -1;

                if (versionComparison == 0)
                {
                    Logger.Information("Versions are the same. Check ending.");
                    UpdateAvailable = false;
                    return;
                }

                if (versionComparison < 0)
                {
                    Logger.Warning("Remote version is older. That's weird o_O. Check ending.");
                    UpdateAvailable = false;
                    return;
                }

                Logger.Information("Remote version is newer. Updating.");
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
                Logger.Error(e, "Error during update check");
            }
        }

        /// <summary>
        /// Resolves the version to compare for update checks by preferring the running assembly version
        /// when it is newer than the cached manifest.
        /// </summary>
        /// <param name="manifestVersion">The version read from the local manifest.</param>
        /// <param name="runningVersion">The current application version.</param>
        /// <returns>The version to use when evaluating update availability.</returns>
        internal static Version ResolveEffectiveLocalVersion(Version manifestVersion, Version runningVersion)
        {
            if (runningVersion == null && manifestVersion == null)
            {
                return new Version();
            }

            if (runningVersion == null)
            {
                return manifestVersion ?? new Version();
            }

            if (manifestVersion == null)
            {
                return runningVersion;
            }

            return runningVersion > manifestVersion ? runningVersion : manifestVersion;
        }

        /// <summary>
        /// Determines the most accurate local version for update comparisons.
        /// </summary>
        /// <returns>The version to compare against the remote manifest.</returns>
        private Version GetEffectiveLocalVersion()
        {
            var runningVersion = GetRunningVersion();
            var manifestVersion = _localConfig?.Version;
            var effectiveVersion = ResolveEffectiveLocalVersion(manifestVersion, runningVersion);

            if (manifestVersion != null && runningVersion != null && runningVersion > manifestVersion)
            {
                Logger.Information(
                    "Running version {RunningVersion} is newer than local manifest {LocalVersion}; using running version for comparisons.",
                    runningVersion,
                    manifestVersion);
                _localConfig.Version = effectiveVersion;
            }

            return effectiveVersion;
        }

        /// <summary>
        /// Gets the version of the currently running application.
        /// </summary>
        /// <returns>The running application version.</returns>
        private static Version GetRunningVersion()
        {
            return Assembly.GetExecutingAssembly().GetName().Version ?? new Version();
        }

        /// <summary>
        /// Downloads all update payloads and launches the installer.
        /// </summary>
        /// <param name="onProgress">
        /// Optional callback invoked after each chunk:
        /// <c>(fileIndex, fileCount, fileName, bytesReceived, totalBytes)</c>.
        /// <c>totalBytes</c> is -1 when the server does not send <c>Content-Length</c>.
        /// </param>
        /// <param name="cancellationToken">Token that aborts the download.</param>
        public async Task Update(
            Action<int, int, string, long, long> onProgress = null,
            CancellationToken cancellationToken = default)
        {
            UpdateAvailable = true;

            // Clean up any failed previous attempt.
            var workDir = Path.Combine(appDataPath, WorkPath);
            if (Directory.Exists(workDir))
            {
                try { Directory.Delete(workDir, true); }
                catch { return; }
            }

            try { Directory.CreateDirectory(workDir); }
            catch { return; }

            var payloads = _remoteConfig.Payloads;
            var fileCount = payloads.Length;

            // Download files in manifest.
            for (int fileIndex = 0; fileIndex < fileCount; fileIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var update = payloads[fileIndex];
                Logger.Information("Fetching '{UpdatePayload}'.", update);
                var url = _remoteConfig.BaseUri + update;

                IProgress<(long bytesReceived, long totalBytes)> chunkProgress = onProgress == null
                    ? null
                    : new Progress<(long bytesReceived, long totalBytes)>(t =>
                        onProgress(fileIndex, fileCount, update, t.bytesReceived, t.totalBytes));

                byte[] file;
                try
                {
                    file = await Fetch.GetWithProgressAsync(url, chunkProgress, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    Logger.Information("Update download cancelled.");
                    throw;
                }

                if (file == null)
                {
                    Logger.Error("Fetch failed for '{UpdatePayload}'.", update);
                    return;
                }

                var info = new FileInfo(Path.Combine(workDir, update));
                Directory.CreateDirectory(info.DirectoryName);
                File.WriteAllBytes(info.FullName, file);

                // Unzip
                if (Regex.IsMatch(update, @"\.zip"))
                {
                    try
                    {
                        ZipFile.ExtractToDirectory(info.FullName, workDir, overwriteFiles: true);
                        File.Delete(info.FullName);
                        AllowUpdate = true;
                    }
                    catch
                    {
                        return;
                    }
                }
            }

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
