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

            return await Task.Run(() =>
            {
                try
                {
                    var projectLoadResult = projectManager.PrepareProject(file, Version);

                    if (!projectLoadResult.Success)
                    {
                        return DatabaseReadResult.Failed();
                    }

                    if (!projectLoadResult.IsSupportedVersion)
                    {
                        LogNewerManifest(projectLoadResult.ManifestVersion);
                        // codex TODO: instead of returning we need to prompt and ask the user to update an older database, return new DatabaseReadResult(false, projectLoadResult.ManifestVersion, false);

                    }

                    DeserializeProject(projectLoadResult);

                    return new DatabaseReadResult(true, projectLoadResult.ManifestVersion, projectLoadResult.IsSupportedVersion);
                }
                catch (Exception e)
                {
                    logger.Error(e, "Failed to process database file {DatabaseFile}", _fileName);
                    return DatabaseReadResult.Failed();
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
                        var serializer = new XmlSerializer(typeof(ChipTaskHandlerModel));
                        SetupModel = serializer.Deserialize(projectLoadResult.Reader) as ChipTaskHandlerModel;
                    });
                }
                catch (Exception e)
                {
                    logger.Error(e, "Failed to deserialize project manifest from {ManifestFile}", projectLoadResult?.Reader?.ToString());
                }
            }
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

            if (dispatcher != null)
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

        public class DatabaseReadResult
        {
            public DatabaseReadResult(bool success, int manifestVersion, bool isSupportedVersion)
            {
                Success = success;
                ManifestVersion = manifestVersion;
                IsSupportedVersion = isSupportedVersion;
            }

            public bool Success { get; }

            public int ManifestVersion { get; }

            public bool IsSupportedVersion { get; }

            public static DatabaseReadResult Failed()
            {
                return new DatabaseReadResult(false, 0, false);
            }
        }
    }
}
