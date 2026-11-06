using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml;
using RFiDGear.Models;

namespace RFiDGear.Infrastructure.FileAccess
{
    public class ProjectManager
    {
        private const string ChipDatabaseFileName = "chipdatabase.xml";
        private const string TaskDatabaseFileNameCompressed = "chipdatabase.rfPrj";
        private const string TaskDatabaseFileName = "taskdatabase.xml";
        private const string ReportTemplateTempFileName = "temptemplate.pdf";
        private const string SettingsFileName = "settings.xml";

        public string AppDataPath { get; }

        public string ChipDatabasePath => Path.Combine(AppDataPath, ChipDatabaseFileName);

        public string TaskDatabasePath => Path.Combine(AppDataPath, TaskDatabaseFileName);

        public string CompressedTaskDatabasePath => Path.Combine(AppDataPath, TaskDatabaseFileNameCompressed);

        public string ReportTemplateTempPath => Path.Combine(AppDataPath, ReportTemplateTempFileName);

        public string SettingsPath => Path.Combine(AppDataPath, SettingsFileName);

        /// <summary>
        /// Creates a manager rooted at the per-user application data folder (<c>%LocalAppData%\RFiDGear</c>).
        /// </summary>
        public ProjectManager()
            : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RFiDGear"))
        {
        }

        /// <summary>
        /// Creates a manager rooted at <paramref name="appDataPath"/>. Project archives are extracted and
        /// staging files are written below this folder, so tests can pass an isolated temporary root.
        /// </summary>
        /// <param name="appDataPath">Folder used for extraction and staging; created if missing.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="appDataPath"/> is null, empty, or whitespace.</exception>
        public ProjectManager(string appDataPath)
        {
            if (string.IsNullOrWhiteSpace(appDataPath))
            {
                throw new ArgumentException("The application data path must not be empty.", nameof(appDataPath));
            }

            AppDataPath = appDataPath;

            EnsureAppDataDirectory();
        }

        /// <summary>
        /// Classifies a project file. <c>.rfPrj</c> is always an archive. <c>.xml</c> is plain XML unless the
        /// file starts with a ZIP header: versions between 2022 and 2025 saved archives under whatever
        /// extension the user typed, so a ZIP named <c>.xml</c> must be opened as an archive.
        /// </summary>
        /// <param name="file">The file to classify.</param>
        /// <returns>The project file type, or <see cref="ProjectFileType.Unknown"/> for other extensions.</returns>
        public ProjectFileType GetProjectFileType(FileInfo file)
        {
            if (file.Extension.Equals(".rfprj", StringComparison.OrdinalIgnoreCase))
            {
                return ProjectFileType.Archive;
            }

            if (file.Extension.Equals(".xml", StringComparison.OrdinalIgnoreCase))
            {
                return StartsWithZipHeader(file) ? ProjectFileType.Archive : ProjectFileType.Xml;
            }

            return ProjectFileType.Unknown;
        }

        /// <summary>
        /// Checks for the local-file-header signature <c>PK\x03\x04</c>. Any I/O problem falls back to
        /// "not a ZIP" so classification never throws; the real read then reports the failure.
        /// </summary>
        private static bool StartsWithZipHeader(FileInfo file)
        {
            try
            {
                using var stream = new FileStream(
                    file.FullName,
                    FileMode.Open,
                    System.IO.FileAccess.Read, // qualified: the RFiDGear.Infrastructure.FileAccess namespace shadows the enum
                    FileShare.ReadWrite | FileShare.Delete);

                Span<byte> header = stackalloc byte[4];
                return stream.Read(header) == header.Length
                    && header[0] == 0x50
                    && header[1] == 0x4B
                    && header[2] == 0x03
                    && header[3] == 0x04;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        public ProjectLoadResult PrepareProject(FileInfo file, Version appVersion)
        {
            EnsureAppDataDirectory();

            return GetProjectFileType(file) switch
            {
                ProjectFileType.Xml => CreateXmlLoadResult(file.FullName, appVersion, ProjectFileType.Xml),
                ProjectFileType.Archive => PrepareCompressedProject(file, appVersion),
                _ => ProjectLoadResult.Failed()
            };
        }

        public string GetReportTemplatePath(string reportTemplatePath)
        {
            EnsureAppDataDirectory();

            return reportTemplatePath;
        }

        public string GetTemporaryReportTemplatePath()
        {
            EnsureAppDataDirectory();

            return ReportTemplateTempPath;
        }

        public void CopyReportTemplateToTemp(string reportTemplatePath)
        {
            SafeCopy(reportTemplatePath, GetTemporaryReportTemplatePath());
        }

        public void SafeCopy(string sourcePath, string destinationPath, bool overwrite = true)
        {
            EnsureAppDataDirectory();

            if (string.IsNullOrWhiteSpace(sourcePath) || string.IsNullOrWhiteSpace(destinationPath))
            {
                return;
            }

            if (!File.Exists(sourcePath))
            {
                return;
            }

            EnsureDirectoryExists(Path.GetDirectoryName(destinationPath));

            File.Copy(sourcePath, destinationPath, overwrite);
        }

        public void ResetTemporaryArtifacts()
        {
            EnsureAppDataDirectory();

            CleanTemporaryFiles();
            RemoveTaskArtifacts();
        }

        public void EnsureSettingsFileExists(string settingsPath = null)
        {
            EnsureAppDataDirectory();

            var resolvedPath = ResolveSettingsPath(settingsPath);

            if (File.Exists(resolvedPath))
            {
                return;
            }

            EnsureDirectoryExists(Path.GetDirectoryName(resolvedPath));

            using var settingsReaderWriter = new SettingsReaderWriter(AppDataPath);
            settingsReaderWriter.SaveSettings(new DefaultSpecification(true), resolvedPath);
        }

        public Task EnsureSettingsFileExistsAsync(string settingsPath = null)
        {
            return Task.Run(() => EnsureSettingsFileExists(settingsPath));
        }

        public void InitializeUpdateConfiguration(string settingsPath = null)
        {
            EnsureSettingsFileExists(settingsPath);

            using var settingsReaderWriter = new SettingsReaderWriter(AppDataPath);
            settingsReaderWriter.InitUpdateFile();
        }

        public SettingsLoadResult LoadSettings(string settingsPath = null, Version appVersion = null)
        {
            EnsureSettingsFileExists(settingsPath);

            var resolvedPath = ResolveSettingsPath(settingsPath);
            var settingsReaderWriter = new SettingsReaderWriter(AppDataPath);
            var specification = settingsReaderWriter.ReadSettings(resolvedPath);

            return CreateSettingsLoadResult(appVersion, resolvedPath, specification);
        }

        public Task<SettingsLoadResult> LoadSettingsAsync(string settingsPath = null, Version appVersion = null)
        {
            return Task.Run(() => LoadSettings(settingsPath, appVersion));
        }

        public Task SaveSettingsAsync(DefaultSpecification defaultSpecification, string settingsPath = null)
        {
            EnsureSettingsFileExists(settingsPath);

            var resolvedPath = ResolveSettingsPath(settingsPath);
            var settingsReaderWriter = new SettingsReaderWriter(AppDataPath);

            return settingsReaderWriter.SaveSettingsAsync(defaultSpecification, resolvedPath);
        }

        public bool ValidateManifestVersion(int manifestVersion, Version appVersion)
        {
            var currentVersion = Convert.ToInt32(string.Format("{0}{1}{2}", appVersion.Major, appVersion.Minor, appVersion.Build));

            return manifestVersion <= currentVersion;
        }

        /// <summary>
        /// Normalizes legacy XML values in a project payload to the currently supported ones.
        /// </summary>
        /// <param name="projectXml">The raw project XML.</param>
        /// <returns>The normalized project XML.</returns>
        internal string NormalizeLegacyProjectXml(string projectXml)
        {
            if (string.IsNullOrWhiteSpace(projectXml))
            {
                return projectXml ?? string.Empty;
            }

            return projectXml.Replace("AuthenticationError", "AuthFailure", StringComparison.Ordinal);
        }

        private SettingsLoadResult CreateSettingsLoadResult(Version appVersion, string resolvedPath, DefaultSpecification specification)
        {
            var manifestVersion = GetManifestVersion(resolvedPath);
            var versionToCompare = appVersion ?? Assembly.GetExecutingAssembly().GetName().Version;

            return new SettingsLoadResult(
                success: true,
                manifestVersion: manifestVersion,
                isSupportedVersion: ValidateManifestVersion(manifestVersion, versionToCompare),
                defaultSpecification: specification,
                settingsPath: resolvedPath);
        }

        private ProjectLoadResult PrepareCompressedProject(FileInfo file, Version appVersion)
        {
            var xmlPath = ExtractCompressedProject(file.FullName, file.Name);

            if (string.IsNullOrEmpty(xmlPath) || !File.Exists(xmlPath))
            {
                return ProjectLoadResult.Failed();
            }

            return CreateXmlLoadResult(xmlPath, appVersion, ProjectFileType.Archive);
        }

        private ProjectLoadResult CreateXmlLoadResult(string path, Version appVersion, ProjectFileType projectFileType)
        {
            var manifestVersion = GetManifestVersion(path);

            return new ProjectLoadResult(
                success: true,
                manifestVersion: manifestVersion,
                isSupportedVersion: ValidateManifestVersion(manifestVersion, appVersion),
                reader: new StreamReader(path),
                fileType: projectFileType);
        }

        private int GetManifestVersion(string path)
        {
            var doc = new XmlDocument();
            doc.Load(path);

            var node = doc.SelectSingleNode("//ManifestVersion");

            return node == null ? 0 : Convert.ToInt32(node.InnerText.Replace(".", string.Empty));
        }

        private string ExtractCompressedProject(string archivePath, string archiveFileName)
        {
            CleanTemporaryFiles();

            // A staging file left by an earlier load or save must never be mistaken for this archive's
            // content: the fallback below would otherwise silently open the previous project when the
            // archive lacks the expected entry.
            if (File.Exists(TaskDatabasePath))
            {
                File.Delete(TaskDatabasePath);
            }

            ExtractArchive(archivePath);

            var archiveContentPath = Path.Combine(AppDataPath, archiveFileName);

            if (File.Exists(archiveContentPath))
            {
                return archiveContentPath;
            }

            return File.Exists(TaskDatabasePath) ? TaskDatabasePath : string.Empty;
        }

        private void ExtractArchive(string archivePath)
        {
            ZipFile.ExtractToDirectory(archivePath, AppDataPath, overwriteFiles: true);
        }

        private void CleanTemporaryFiles()
        {
            foreach (var tempFile in Directory.GetFiles(AppDataPath, "*.tmp"))
            {
                File.Delete(tempFile);
            }
        }

        private void RemoveTaskArtifacts()
        {
            if (File.Exists(TaskDatabasePath))
            {
                File.Delete(TaskDatabasePath);
            }

            foreach (var file in Directory.GetFiles(AppDataPath, "*.rfprj"))
            {
                File.Delete(file);
            }
        }

        private void EnsureAppDataDirectory()
        {
            if (!Directory.Exists(AppDataPath))
            {
                Directory.CreateDirectory(AppDataPath);
            }
        }

        private void EnsureDirectoryExists(string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                return;
            }

            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }
        }

        private string ResolveSettingsPath(string settingsPath)
        {
            return string.IsNullOrWhiteSpace(settingsPath) ? SettingsPath : settingsPath;
        }
    }

    public enum ProjectFileType
    {
        Unknown,
        Xml,
        Archive
    }

    public class ProjectLoadResult
    {
        public ProjectLoadResult(bool success, int manifestVersion, bool isSupportedVersion, TextReader reader, ProjectFileType fileType)
        {
            Success = success;
            ManifestVersion = manifestVersion;
            IsSupportedVersion = isSupportedVersion;
            Reader = reader;
            FileType = fileType;
        }

        public bool Success { get; }

        public int ManifestVersion { get; }

        public bool IsSupportedVersion { get; }

        public TextReader Reader { get; }

        public ProjectFileType FileType { get; }

        public static ProjectLoadResult Failed()
        {
            return new ProjectLoadResult(false, 0, false, TextReader.Null, ProjectFileType.Unknown);
        }
    }

    public class SettingsLoadResult
    {
        public SettingsLoadResult(bool success, int manifestVersion, bool isSupportedVersion, DefaultSpecification defaultSpecification, string settingsPath)
        {
            Success = success;
            ManifestVersion = manifestVersion;
            IsSupportedVersion = isSupportedVersion;
            DefaultSpecification = defaultSpecification;
            SettingsPath = settingsPath;
        }

        public bool Success { get; }

        public int ManifestVersion { get; }

        public bool IsSupportedVersion { get; }

        public DefaultSpecification DefaultSpecification { get; }

        public string SettingsPath { get; }

        public static SettingsLoadResult Failed()
        {
            return new SettingsLoadResult(false, 0, false, new DefaultSpecification(), string.Empty);
        }
    }
}
