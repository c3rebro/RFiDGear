using System;
using System.Globalization;
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

        /// <summary>
        /// Compares concatenated digits, which orders versions wrongly (<c>1.10.9054</c> becomes 1109054 and looks
        /// newer than <c>2.0.10</c>, which becomes 2010). Kept only for callers that still hold the number.
        /// </summary>
        /// <param name="manifestVersion">The manifest's digits run together, as in <see cref="ProjectLoadResult.ManifestVersion"/>.</param>
        /// <param name="appVersion">The running application's version.</param>
        /// <returns><c>true</c> when the run-together number is not larger than the application's.</returns>
        [Obsolete("Compares concatenated digits, so 1.10.9054 sorts after 2.0.10. Use ValidateManifestVersion(string, Version).")]
        public bool ValidateManifestVersion(int manifestVersion, Version appVersion)
        {
            return manifestVersion <= LegacyVersionNumber(appVersion);
        }

        /// <summary>
        /// Decides whether a file written with the given manifest version can be read by the running application,
        /// that is whether it was written by this release or an older one.
        /// <para>
        /// The manifest text is <c>major.minor.build</c>. The parts are compared as numbers one after the other, so
        /// <c>1.10.9054</c> is older than <c>2.0.10</c>, and <c>1.10.0</c> is newer than <c>1.9.9</c>. A missing
        /// manifest counts as supported. A bare number without dots comes from the oldest layout and keeps the
        /// original concatenated-digit meaning.
        /// </para>
        /// </summary>
        /// <param name="manifestVersion">The manifest text exactly as stored in the file; may be empty.</param>
        /// <param name="appVersion">The running application's version.</param>
        /// <returns><c>true</c> when the manifest is not newer than <paramref name="appVersion"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="appVersion"/> is <c>null</c>.</exception>
        /// <exception cref="FormatException">Thrown when the manifest text is not made of numbers separated by dots.</exception>
        public bool ValidateManifestVersion(string manifestVersion, Version appVersion)
        {
            if (appVersion == null)
            {
                throw new ArgumentNullException(nameof(appVersion));
            }

            if (string.IsNullOrWhiteSpace(manifestVersion))
            {
                return true;
            }

            var text = manifestVersion.Trim();

            if (text.IndexOf('.') < 0)
            {
                return long.Parse(text, NumberStyles.None, CultureInfo.InvariantCulture) <= LegacyVersionNumber(appVersion);
            }

            var parts = text.Split('.');

            if (parts.Length > 4)
            {
                throw new FormatException("A manifest version has at most four parts.");
            }

            var manifest = new int[parts.Length];

            for (var i = 0; i < parts.Length; i++)
            {
                manifest[i] = int.Parse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture);
            }

            var app = new[] { appVersion.Major, appVersion.Minor, Math.Max(appVersion.Build, 0) };

            for (var i = 0; i < app.Length; i++)
            {
                var part = i < manifest.Length ? manifest[i] : 0;

                if (part != app[i])
                {
                    return part < app[i];
                }
            }

            return true;
        }

        private static long LegacyVersionNumber(Version appVersion) =>
            Convert.ToInt64(string.Format(CultureInfo.InvariantCulture, "{0}{1}{2}", appVersion.Major, appVersion.Minor, appVersion.Build), CultureInfo.InvariantCulture);

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
            var manifestText = ReadManifestVersionText(resolvedPath);
            var versionToCompare = appVersion ?? Assembly.GetExecutingAssembly().GetName().Version;

            return new SettingsLoadResult(
                success: true,
                manifestVersion: ToDiagnosticNumber(manifestText),
                isSupportedVersion: ValidateManifestVersion(manifestText, versionToCompare),
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
            var manifestText = ReadManifestVersionText(path);
            // Validated before the reader is opened, so a malformed manifest cannot leak an open file.
            var isSupportedVersion = ValidateManifestVersion(manifestText, appVersion);

            return new ProjectLoadResult(
                success: true,
                manifestVersion: ToDiagnosticNumber(manifestText),
                isSupportedVersion: isSupportedVersion,
                reader: new StreamReader(path),
                fileType: projectFileType);
        }

        /// <summary>Reads the <c>ManifestVersion</c> element of a project or settings file, or <c>null</c> if it has none.</summary>
        private static string ReadManifestVersionText(string path)
        {
            var doc = new XmlDocument();
            doc.Load(path);

            return doc.SelectSingleNode("//ManifestVersion")?.InnerText.Trim();
        }

        /// <summary>
        /// The concatenated-digit number that results and logs have always carried (<c>1.10.9054</c> becomes
        /// 1109054). It is for display only; it is not used to decide whether a file is supported.
        /// </summary>
        private static int ToDiagnosticNumber(string manifestText) =>
            int.TryParse(manifestText?.Replace(".", string.Empty), NumberStyles.None, CultureInfo.InvariantCulture, out var number)
                ? number
                : 0;

        private string ExtractCompressedProject(string archivePath, string archiveFileName)
        {
            CleanTemporaryFiles();

            // The payload is chosen from the archive's own entries, never by looking for a file that happens
            // to be in the shared folder. Anything left there by an earlier load or save, including a
            // same-named entry from a different archive with the same file name, must not be mistaken for
            // this archive's project.
            var payloadEntry = FindProjectEntryName(archivePath, archiveFileName);

            if (payloadEntry == null)
            {
                return string.Empty;
            }

            // Extraction overwrites, so the payload on disk is exactly this archive's entry.
            ExtractArchive(archivePath);

            var payloadPath = Path.Combine(AppDataPath, payloadEntry);

            return File.Exists(payloadPath) ? payloadPath : string.Empty;
        }

        /// <summary>
        /// Finds the entry that holds the project: the entry named like the archive itself (the oldest layout)
        /// if there is one, otherwise <c>taskdatabase.xml</c>.
        /// </summary>
        /// <returns>The entry's name, or <c>null</c> when the archive contains no project entry.</returns>
        private static string FindProjectEntryName(string archivePath, string archiveFileName)
        {
            string taskDatabaseEntry = null;

            using (var archive = ZipFile.OpenRead(archivePath))
            {
                foreach (var entry in archive.Entries)
                {
                    if (entry.FullName.Equals(archiveFileName, StringComparison.OrdinalIgnoreCase))
                    {
                        return entry.FullName;
                    }

                    if (entry.FullName.Equals(TaskDatabaseFileName, StringComparison.OrdinalIgnoreCase))
                    {
                        taskDatabaseEntry = entry.FullName;
                    }
                }
            }

            return taskDatabaseEntry;
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
