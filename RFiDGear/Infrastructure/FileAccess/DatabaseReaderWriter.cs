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
        /// Reads a project file and stores the result in <see cref="SetupModel"/>.
        /// Prefer <see cref="ReadProjectAsync"/> when the current model must stay untouched on failure.
        /// </summary>
        /// <param name="_fileName">Path of the <c>.rfPrj</c> archive or <c>.xml</c> project to read.</param>
        /// <returns>The read result; <see cref="SetupModel"/> is replaced whenever <see cref="DatabaseReadResult.Success"/> is true.</returns>
        public async Task<DatabaseReadResult> ReadDatabase(string _fileName = "")
        {
            var (result, model) = await ReadProjectAsync(_fileName).ConfigureAwait(false);

            if (result.Success && model != null)
            {
                SetupModel = model;
            }

            return result;
        }

        /// <summary>
        /// Reads a project file and returns the deserialized model without changing any state of this
        /// instance, so a failed read can never damage the project that is currently loaded.
        /// Archives are extracted below this instance's <see cref="ProjectManager"/> root.
        /// </summary>
        /// <param name="fileName">Path of the <c>.rfPrj</c> archive or <c>.xml</c> project to read.</param>
        /// <returns>
        /// The read result and, when it succeeded, the deserialized model (otherwise <c>null</c>).
        /// A file that deserializes to nothing, such as one with a nil root, is reported as
        /// <see cref="DatabaseReadFailureReason.DeserializationFailed"/>.
        /// </returns>
        internal async Task<(DatabaseReadResult Result, ChipTaskHandlerModel Model)> ReadProjectAsync(string fileName = "")
        {
            if (string.IsNullOrWhiteSpace(fileName) || !File.Exists(fileName))
            {
                return (DatabaseReadResult.Failed(), null);
            }

            var file = new FileInfo(fileName);

            if (projectManager.GetProjectFileType(file) == ProjectFileType.Unknown)
            {
                return (DatabaseReadResult.Failed(DatabaseReadFailureReason.UnsupportedFormat), null);
            }

            return await Task.Run<(DatabaseReadResult Result, ChipTaskHandlerModel Model)>(() =>
            {
                try
                {
                    var projectLoadResult = projectManager.PrepareProject(file, Version);

                    if (!projectLoadResult.Success)
                    {
                        return (DatabaseReadResult.Failed(DatabaseReadFailureReason.UnsupportedFormat), null);
                    }

                    if (!projectLoadResult.IsSupportedVersion)
                    {
                        LogNewerManifest(projectLoadResult.ManifestVersion);
                    }

                    var model = DeserializeProject(projectLoadResult);

                    if (model == null)
                    {
                        logger.Error("Project file {DatabaseFile} does not contain a task project", fileName);
                        return (DatabaseReadResult.Failed(DatabaseReadFailureReason.DeserializationFailed), null);
                    }

                    var reason = projectLoadResult.IsSupportedVersion
                        ? DatabaseReadFailureReason.None
                        : DatabaseReadFailureReason.UnsupportedVersion;
                    return (new DatabaseReadResult(true, projectLoadResult.ManifestVersion, projectLoadResult.IsSupportedVersion, reason), model);
                }
                catch (InvalidDataException e)
                {
                    logger.Error(e, "Project archive format is invalid {DatabaseFile}", fileName);
                    return (DatabaseReadResult.Failed(DatabaseReadFailureReason.UnsupportedFormat), null);
                }
                catch (IOException e)
                {
                    logger.Error(e, "Failed to access database file {DatabaseFile}", fileName);
                    return (DatabaseReadResult.Failed(), null);
                }
                catch (UnauthorizedAccessException e)
                {
                    logger.Error(e, "Access to database file was denied {DatabaseFile}", fileName);
                    return (DatabaseReadResult.Failed(), null);
                }
                catch (Exception e)
                {
                    logger.Error(e, "Failed to process database file {DatabaseFile}", fileName);
                    return (DatabaseReadResult.Failed(DatabaseReadFailureReason.DeserializationFailed), null);
                }
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Deserializes the task project from the prepared reader. Plain XML projects and the
        /// <c>taskdatabase.xml</c> extracted from an archive share one format, so both use the same code.
        /// </summary>
        /// <returns>The model, or <c>null</c> when the document deserializes to nothing.</returns>
        private ChipTaskHandlerModel DeserializeProject(ProjectLoadResult projectLoadResult)
        {
            ChipTaskHandlerModel model = null;

            using (projectLoadResult.Reader)
            {
                // The model contains WPF view models, which must be created on an STA thread.
                ExecuteOnStaThread(() =>
                {
                    var projectXml = ReadProjectXml(projectLoadResult.Reader);
                    var serializer = new XmlSerializer(typeof(ChipTaskHandlerModel));
                    using var stringReader = new StringReader(projectXml);
                    model = serializer.Deserialize(stringReader) as ChipTaskHandlerModel;
                });
            }

            return model;
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

        /// <summary>
        /// Exports the chip tree as plain XML. The file is written next to the target and replaces it only when
        /// the write succeeded, so a failed export leaves an existing file as it was. A chip export always has
        /// the extension <c>.xml</c> (appended, or replacing <c>.rfPrj</c>) so it cannot be taken for a project;
        /// an empty path writes to the application data folder.
        /// </summary>
        /// <param name="objModel">The chip tree to write.</param>
        /// <param name="_path">Target path as chosen by the user, or empty for the application data folder.</param>
        /// <returns>The path that was actually written.</returns>
        /// <exception cref="InvalidOperationException">The chip tree could not be serialized.</exception>
        /// <exception cref="IOException">The file could not be written.</exception>
        /// <exception cref="UnauthorizedAccessException">The location is not writable.</exception>
        public string WriteDatabase(ObservableCollection<RFiDChipParentLayerViewModel> objModel, string _path)
        {
            var serializer = new XmlSerializer(typeof(ObservableCollection<RFiDChipParentLayerViewModel>));
            var target = string.IsNullOrEmpty(_path)
                ? projectManager.ChipDatabasePath
                : ChipExportPath(_path);

            WriteFileAtomically(target, temporaryPath => SerializeToFile(serializer, objModel, temporaryPath));

            return target;
        }

        /// <summary>
        /// The path a chip export is written to: <paramref name="path"/> with the extension <c>.xml</c>.
        /// </summary>
        /// <param name="path">Path chosen by the user.</param>
        /// <returns>
        /// <paramref name="path"/> if it already ends in <c>.xml</c>, with <c>.rfPrj</c> replaced by <c>.xml</c>,
        /// otherwise with <c>.xml</c> appended.
        /// </returns>
        internal static string ChipExportPath(string path)
        {
            var extension = Path.GetExtension(path);

            if (extension.Equals(".xml", StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }

            return extension.Equals(".rfPrj", StringComparison.OrdinalIgnoreCase)
                ? Path.ChangeExtension(path, ".xml")
                : string.Concat(path, ".xml");
        }

        private static void SerializeToFile(XmlSerializer serializer, object model, string path)
        {
            using (var writer = new StreamWriter(path, false, new UTF8Encoding(false)))
            {
                serializer.Serialize(writer, model);
            }
        }

        /// <summary>
        /// Produces <paramref name="targetPath"/> through a temporary file in the same folder: the file is
        /// written first and replaces the target only when that succeeded, so a failure at any point leaves an
        /// existing target as it was. The replaced file is kept as <c>.bak</c> while the replacement runs and
        /// removed afterwards.
        /// </summary>
        /// <param name="targetPath">The file to create or replace.</param>
        /// <param name="writeTo">Writes the complete new content to the path it is given.</param>
        private void WriteFileAtomically(string targetPath, Action<string> writeTo)
        {
            var fullTargetPath = Path.GetFullPath(targetPath);
            var temporaryPath = string.Concat(fullTargetPath, ".", Guid.NewGuid().ToString("N"), ".tmp");
            var backupPath = string.Concat(fullTargetPath, ".bak");

            try
            {
                writeTo(temporaryPath);

                if (File.Exists(fullTargetPath))
                {
                    File.Replace(temporaryPath, fullTargetPath, backupPath, true);
                    TryDeleteFile(backupPath);
                }
                else
                {
                    File.Move(temporaryPath, fullTargetPath);
                }
            }
            catch
            {
                TryDeleteFile(temporaryPath);
                throw;
            }
        }

        private void TryDeleteFile(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                logger.Warning(e, "Could not remove the leftover file {Path}", path);
            }
        }

        /// <summary>
        /// Writes a task project: plain XML for a <c>.xml</c> path, otherwise a ZIP archive containing
        /// <c>taskdatabase.xml</c>. A path without an extension gets <c>.rfPrj</c> appended; an empty path
        /// writes to the application data folder. The file is written next to the target and replaces it only
        /// when the write succeeded, so a failed save leaves an existing project as it was.
        /// </summary>
        /// <param name="objModel">The project to write.</param>
        /// <param name="_path">Target path as chosen by the user or caller.</param>
        /// <returns>
        /// The path that was actually written, which differs from <paramref name="_path"/> when an extension
        /// was appended, or <c>null</c> when the project could not be serialized (the failure is logged).
        /// </returns>
        /// <exception cref="IOException">The file could not be written.</exception>
        /// <exception cref="UnauthorizedAccessException">The location is not writable.</exception>
        public string WriteDatabase(ChipTaskHandlerModel objModel, string _path = "")
        {
            try
            {
                var serializer = new XmlSerializer(typeof(ChipTaskHandlerModel));

                var extension = string.IsNullOrWhiteSpace(_path)
                    ? string.Empty
                    : Path.GetExtension(_path);
                var saveAsXml = extension.Equals(".xml", StringComparison.OrdinalIgnoreCase);

                if (saveAsXml)
                {
                    WriteFileAtomically(_path, temporaryPath => SerializeToFile(serializer, objModel, temporaryPath));
                    return _path;
                }

                // The archive entry is staged in the application data folder; only the archive is the user's file.
                var xmlOutputPath = projectManager.TaskDatabasePath;
                SerializeToFile(serializer, objModel, xmlOutputPath);

                var archivePath = string.IsNullOrWhiteSpace(_path)
                    ? projectManager.CompressedTaskDatabasePath
                    : string.IsNullOrWhiteSpace(extension)
                        ? string.Concat(_path, ".rfPrj")
                        : _path;

                WriteFileAtomically(archivePath, temporaryPath =>
                {
                    using (var archive = ZipFile.Open(temporaryPath, ZipArchiveMode.Create))
                    {
                        archive.CreateEntryFromFile(
                            xmlOutputPath,
                            Path.GetFileName(projectManager.TaskDatabasePath),
                            CompressionLevel.Optimal);
                    }
                });

                return archivePath;
            }
            catch (XmlException e)
            {
                logger.Error(e, "Failed to write task database archive");
                return null;
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
