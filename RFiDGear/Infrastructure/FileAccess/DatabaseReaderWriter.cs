using CommunityToolkit.Mvvm.Input;

using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using System.IO.Compression;
using System.Windows;

using RFiDGear.Models;
using RFiDGear.ViewModel;
using Serilog;

namespace RFiDGear.Infrastructure.FileAccess
{
    /// <summary>
    /// Serialize and Deserialize Chips and Tasks
    /// </summary>
    public class DatabaseReaderWriter
    {
        #region fields
        private readonly Version Version = Assembly.GetExecutingAssembly().GetName().Version;
        private readonly ILogger logger = Log.ForContext<DatabaseReaderWriter>();
        private readonly ProjectManager projectManager;

        public ObservableCollection<RFiDChipParentLayerViewModel> TreeViewModel;
        public ChipTaskHandlerModel SetupModel;
        public IAsyncRelayCommand AsyncRelayCommandLoadDB { get; }

        #endregion fields

        public DatabaseReaderWriter()
            : this(new ProjectManager())
        {
        }

        public DatabaseReaderWriter(ProjectManager projectManager)
        {
            try
            {
                this.projectManager = projectManager ?? throw new ArgumentNullException(nameof(projectManager));

                TreeViewModel = new ObservableCollection<RFiDChipParentLayerViewModel>();
                SetupModel = new ChipTaskHandlerModel();

                CreateDefaultChipDatabase();

                projectManager.ResetTemporaryArtifacts();
            }
            catch (Exception e)
            {
                logger.Error(e, "Failed to initialize database reader");
                return;
            }
        }

        private void CreateDefaultChipDatabase()
        {
            if (File.Exists(projectManager.ChipDatabasePath))
            {
                return;
            }

            var serializer = new XmlSerializer(typeof(ObservableCollection<RFiDChipParentLayerViewModel>));

            using (TextWriter writer = new StreamWriter(projectManager.ChipDatabasePath))
            {
                serializer.Serialize(writer, TreeViewModel);
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        public async Task<DatabaseReadResult> ReadDatabase(string _fileName = "")
        {
            if (string.IsNullOrWhiteSpace(_fileName) || !File.Exists(_fileName))
            {
                return DatabaseReadResult.Failed();
            }

            var file = new FileInfo(_fileName);

            if (projectManager.GetProjectFileType(file) == ProjectFileType.Unknown)
            {
                return DatabaseReadResult.Failed(DatabaseReadFailureReason.UnsupportedFormat);
            }

            return await Task.Run(() =>
            {
                try
                {
                    var projectLoadResult = projectManager.PrepareProject(file, Version);

                    if (!projectLoadResult.Success)
                    {
                        return DatabaseReadResult.Failed(DatabaseReadFailureReason.UnsupportedFormat);
                    }

                    if (!projectLoadResult.IsSupportedVersion)
                    {
                        LogNewerManifest(projectLoadResult.ManifestVersion);
                    }

                    DeserializeProject(projectLoadResult);

                    var reason = projectLoadResult.IsSupportedVersion
                        ? DatabaseReadFailureReason.None
                        : DatabaseReadFailureReason.UnsupportedVersion;
                    return new DatabaseReadResult(true, projectLoadResult.ManifestVersion, projectLoadResult.IsSupportedVersion, reason);
                }
                catch (InvalidDataException e)
                {
                    logger.Error(e, "Project archive format is invalid {DatabaseFile}", _fileName);
                    return DatabaseReadResult.Failed(DatabaseReadFailureReason.UnsupportedFormat);
                }
                catch (IOException e)
                {
                    logger.Error(e, "Failed to access database file {DatabaseFile}", _fileName);
                    return DatabaseReadResult.Failed(DatabaseReadFailureReason.IoFailure);
                }
                catch (UnauthorizedAccessException e)
                {
                    logger.Error(e, "Access to database file was denied {DatabaseFile}", _fileName);
                    return DatabaseReadResult.Failed(DatabaseReadFailureReason.IoFailure);
                }
                catch (Exception e)
                {
                    logger.Error(e, "Failed to process database file {DatabaseFile}", _fileName);
                    return DatabaseReadResult.Failed(DatabaseReadFailureReason.DeserializationFailed);
                }
            }).ConfigureAwait(false);
        }

        private void DeserializeProject(ProjectLoadResult projectLoadResult)
        {
            using (projectLoadResult.Reader)
            {
                try
                {
                    if (projectLoadResult.FileType == ProjectFileType.Xml)
                    {
                        ExecuteOnStaThread(() => AsyncRelayCommandLoadDB?.Execute(projectLoadResult.Reader));
                        return;
                    }

                    ExecuteOnStaThread(() =>
                    {
                        var projectXml = ReadProjectXml(projectLoadResult.Reader);
                        var serializer = new XmlSerializer(typeof(ChipTaskHandlerModel));
                        using var stringReader = new StringReader(projectXml);
                        SetupModel = serializer.Deserialize(stringReader) as ChipTaskHandlerModel;
                    });
                }
                catch (Exception e)
                {
                    logger.Error(e, "Failed to deserialize project manifest from {ManifestFile}", projectLoadResult?.Reader?.ToString());
                    throw;
                }
            }
        }

        /// <summary>
        /// Reads the project XML content and normalizes legacy values before deserialization.
        /// </summary>
        /// <param name="reader">The source reader for the project XML.</param>
        /// <returns>The normalized XML string.</returns>
        private string ReadProjectXml(TextReader reader)
        {
            if (reader == null)
            {
                return string.Empty;
            }

            var xmlContent = reader.ReadToEnd();

            return projectManager.NormalizeLegacyProjectXml(xmlContent);
        }

        /// <summary>
        /// Executes the provided action on an STA thread to satisfy UI component requirements.
        /// </summary>
        /// <param name="action">The action to execute on an STA thread.</param>
        private void ExecuteOnStaThread(Action action)
        {
            if (action == null)
            {
                return;
            }

            var dispatcher = Application.Current?.Dispatcher;

            if (dispatcher != null && !dispatcher.HasShutdownStarted && !dispatcher.HasShutdownFinished)
            {
                if (dispatcher.CheckAccess())
                {
                    action();
                    return;
                }

                dispatcher.Invoke(action);
                return;
            }

            if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
            {
                action();
                return;
            }

            var tcs = new TaskCompletionSource<object>();
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                    tcs.SetResult(null);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            })
            {
                IsBackground = true
            };

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();

            tcs.Task.GetAwaiter().GetResult();
        }

        private void LogNewerManifest(int manifestVersion)
        {
            logger.Warning(
                "Database manifest version {ManifestVersion} is newer than supported version {SupportedVersion} at {Timestamp}",
                manifestVersion,
                Convert.ToInt32(string.Format("{0}{1}{2}", Version.Major, Version.Minor, Version.Build)),
                DateTime.Now);
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
                    writer = new StreamWriter(projectManager.ChipDatabasePath, false, new UTF8Encoding(false));
                }

                serializer.Serialize(writer, objModel);

                writer.Close();
            }
            catch (XmlException e)
            {
                logger.Error(e, "Failed to write chip database");
                Environment.Exit(0);
            }
        }

        public void WriteDatabase(ChipTaskHandlerModel objModel, string _path = "")
        {
            try
            {
                var serializer = new XmlSerializer(typeof(ChipTaskHandlerModel));

                var extension = string.IsNullOrWhiteSpace(_path)
                    ? string.Empty
                    : Path.GetExtension(_path);
                var saveAsXml = extension.Equals(".xml", StringComparison.OrdinalIgnoreCase);

                var xmlOutputPath = saveAsXml && !string.IsNullOrWhiteSpace(_path)
                    ? _path
                    : projectManager.TaskDatabasePath;

                using (var writer = new StreamWriter(xmlOutputPath, false, new UTF8Encoding(false)))
                {
                    serializer.Serialize(writer, objModel);
                }

                if (saveAsXml)
                {
                    return;
                }

                var archivePath = string.IsNullOrWhiteSpace(_path)
                    ? projectManager.CompressedTaskDatabasePath
                    : string.IsNullOrWhiteSpace(extension)
                        ? string.Concat(_path, ".rfPrj")
                        : _path;

                if (File.Exists(archivePath))
                {
                    File.Delete(archivePath);
                }

                using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
                {
                    archive.CreateEntryFromFile(
                        xmlOutputPath,
                        Path.GetFileName(projectManager.TaskDatabasePath),
                        CompressionLevel.Optimal);
                }

            }
            catch (XmlException e)
            {
                logger.Error(e, "Failed to write task database archive");
            }
        }

        public void DeleteDatabase()
        {
            File.Delete(projectManager.ChipDatabasePath);
        }

        /// <summary>
        /// Identifies the root cause of a failed <see cref="DatabaseReadResult"/>.
        /// Used by callers to map internal read failures to user-facing error codes without
        /// inferring cause from <see cref="DatabaseReadResult.IsSupportedVersion"/>.
        /// </summary>
        public enum DatabaseReadFailureReason
        {
            /// <summary>No failure; the read completed successfully.</summary>
            None,

            /// <summary>The project file's manifest version is newer than the application supports.</summary>
            UnsupportedVersion,

            /// <summary>The file extension or archive structure is not a supported project format.</summary>
            UnsupportedFormat,

            /// <summary>The project XML could not be deserialized (malformed, schema mismatch, etc.).</summary>
            DeserializationFailed,

            /// <summary>An I/O or preparation error prevented the file from being read.</summary>
            IoFailure,
        }

        public class DatabaseReadResult
        {
            /// <summary>
            /// Initializes a new result.
            /// </summary>
            /// <param name="success">Whether the read succeeded.</param>
            /// <param name="manifestVersion">The numeric manifest version extracted from the file.</param>
            /// <param name="isSupportedVersion">Whether the manifest version is within the supported range.</param>
            /// <param name="failureReason">Root-cause classification; <see cref="DatabaseReadFailureReason.None"/> on success.</param>
            public DatabaseReadResult(bool success, int manifestVersion, bool isSupportedVersion,
                DatabaseReadFailureReason failureReason = DatabaseReadFailureReason.None)
            {
                Success = success;
                ManifestVersion = manifestVersion;
                IsSupportedVersion = isSupportedVersion;
                FailureReason = failureReason;
            }

            /// <summary>Whether the read completed without error.</summary>
            public bool Success { get; }

            /// <summary>Numeric manifest version extracted from the file, or 0 on failure.</summary>
            public int ManifestVersion { get; }

            /// <summary>
            /// <c>true</c> when the file's manifest version is within the range this application supports.
            /// <c>false</c> on a version-mismatch (<see cref="DatabaseReadFailureReason.UnsupportedVersion"/>)
            /// or when <see cref="Success"/> is <c>false</c>.
            /// </summary>
            public bool IsSupportedVersion { get; }

            /// <summary>
            /// Root-cause classification when <see cref="Success"/> is <c>false</c>, or
            /// <see cref="DatabaseReadFailureReason.UnsupportedVersion"/> when the file loaded but
            /// its version is not supported. <see cref="DatabaseReadFailureReason.None"/> on success.
            /// </summary>
            public DatabaseReadFailureReason FailureReason { get; }

            /// <summary>Creates a failure result with an explicit cause.</summary>
            /// <param name="reason">Root-cause classification for the failure.</param>
            public static DatabaseReadResult Failed(DatabaseReadFailureReason reason = DatabaseReadFailureReason.IoFailure)
                => new DatabaseReadResult(false, 0, false, reason);
        }
    }
}
