using System;
using System.Buffers.Binary;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
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
                manifestVersion: manifestText,
                isSupportedVersion: ValidateManifestVersion(manifestText, versionToCompare),
                defaultSpecification: specification,
                settingsPath: resolvedPath);
        }

        /// <summary>
        /// The most entries a project archive may hold. The application writes exactly one, so the limit only
        /// stops a crafted archive from making the loader walk a huge directory.
        /// </summary>
        internal int MaxArchiveEntries { get; set; } = 64;

        /// <summary>
        /// The most uncompressed bytes read from the project entry of an archive (64 MiB by default). It is
        /// enforced while reading, so a header that understates the size does not get around it.
        /// </summary>
        internal long MaxProjectEntryBytes { get; set; } = 64L * 1024 * 1024;

        private ProjectLoadResult PrepareCompressedProject(FileInfo file, Version appVersion)
        {
            CleanTemporaryFiles();

            // Only the project entry is read, in memory. Nothing from the archive is written to disk, so no
            // other entry (settings.xml, a report template, a path with ..) can reach the shared folder.
            var projectBytes = ReadProjectEntry(file.FullName, file.Name);

            if (projectBytes == null || projectBytes.Length == 0)
            {
                return ProjectLoadResult.Failed();
            }

            return CreateBufferLoadResult(projectBytes, appVersion, ProjectFileType.Archive);
        }

        private ProjectLoadResult CreateBufferLoadResult(byte[] projectBytes, Version appVersion, ProjectFileType projectFileType)
        {
            // Validated as bytes first (strict about the encoding, as the file-based path is), so a damaged byte
            // is rejected instead of being replaced by U+FFFD when the text is decoded below.
            var manifestText = ReadManifestVersionTextFromBytes(projectBytes);
            var isSupportedVersion = ValidateManifestVersion(manifestText, appVersion);

            return new ProjectLoadResult(
                success: true,
                manifestVersion: manifestText,
                isSupportedVersion: isSupportedVersion,
                reader: DecodeText(projectBytes),
                fileType: projectFileType);
        }

        /// <summary>
        /// Decodes project bytes the way the plain-file path does: UTF-8 unless a byte-order mark says otherwise.
        /// </summary>
        private static StringReader DecodeText(byte[] projectBytes)
        {
            using var buffer = new MemoryStream(projectBytes, writable: false);
            using var reader = new StreamReader(buffer, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

            return new StringReader(reader.ReadToEnd());
        }

        private ProjectLoadResult CreateXmlLoadResult(string path, Version appVersion, ProjectFileType projectFileType)
        {
            var manifestText = ReadManifestVersionText(path);
            // Validated before the reader is opened, so a malformed manifest cannot leak an open file.
            var isSupportedVersion = ValidateManifestVersion(manifestText, appVersion);

            return new ProjectLoadResult(
                success: true,
                manifestVersion: manifestText,
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
        /// Reads the <c>ManifestVersion</c> element of project bytes that came out of an archive, or <c>null</c> if
        /// it has none. The whole document is parsed from the bytes, so an encoding error anywhere is an
        /// <see cref="XmlException"/> (not a silent replacement character). The content is not trusted: a document
        /// type declaration (the way to write an entity expansion bomb) is refused and nothing external is resolved.
        /// </summary>
        private static string ReadManifestVersionTextFromBytes(byte[] projectBytes)
        {
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };

            using var bytes = new MemoryStream(projectBytes, writable: false);
            using var xmlReader = XmlReader.Create(bytes, settings);
            var doc = new XmlDocument { XmlResolver = null };
            doc.Load(xmlReader);

            return doc.SelectSingleNode("//ManifestVersion")?.InnerText.Trim();
        }

        /// <summary>
        /// A version as <c>major.minor.build</c>, the form a manifest is written in. An undefined build counts as 0.
        /// </summary>
        /// <param name="version">The version to describe.</param>
        /// <returns>For example <c>2.0.10</c>.</returns>
        internal static string FormatManifestVersion(Version version) =>
            string.Format(CultureInfo.InvariantCulture, "{0}.{1}.{2}", version.Major, version.Minor, Math.Max(version.Build, 0));

        /// <summary>
        /// Reads the project text out of an archive without extracting anything. The entry is chosen from the
        /// archive's own entries, never by looking for a file that happens to be in the shared folder: the
        /// entry named like the archive itself (the oldest layout) if there is one, otherwise
        /// <c>taskdatabase.xml</c>. Entries in subfolders never match.
        /// </summary>
        /// <param name="archivePath">Full path of the <c>.rfPrj</c> archive.</param>
        /// <param name="archiveFileName">The archive's file name, used to find the oldest layout's entry.</param>
        /// <returns>The project XML as bytes, or <c>null</c> when the archive contains no project entry.</returns>
        /// <exception cref="InvalidDataException">
        /// The file is not a ZIP archive, holds more than <see cref="MaxArchiveEntries"/> entries, or the project
        /// entry is larger than <see cref="MaxProjectEntryBytes"/> once uncompressed.
        /// </exception>
        private byte[] ReadProjectEntry(string archivePath, string archiveFileName)
        {
            // The entry count is declared in the record at the end of the file. Reading it first rejects a huge
            // directory before ZipArchive.Entries allocates an object for every entry in it. The declared count is
            // authoritative: ZipArchive itself rejects an archive whose directory holds a different number (pinned
            // by a test), so there is no second check on Entries.Count.
            if (ReadDeclaredEntryCount(archivePath) > MaxArchiveEntries)
            {
                throw new InvalidDataException("The archive holds more entries than a project archive does.");
            }

            using var archive = ZipFile.OpenRead(archivePath);

            ZipArchiveEntry projectEntry = null;

            foreach (var entry in archive.Entries)
            {
                if (entry.FullName.Equals(archiveFileName, StringComparison.OrdinalIgnoreCase))
                {
                    projectEntry = entry;
                    break;
                }

                if (entry.FullName.Equals(TaskDatabaseFileName, StringComparison.OrdinalIgnoreCase))
                {
                    projectEntry = entry;
                }
            }

            return projectEntry == null ? null : ReadEntryBytes(projectEntry);
        }

        /// <summary>
        /// Reads the number of entries a ZIP file declares in its end-of-central-directory record, without
        /// parsing the directory itself.
        /// </summary>
        /// <param name="archivePath">Full path of the file.</param>
        /// <returns>
        /// The declared count (65535 also stands for "ZIP64, more than that"), or <c>null</c> when the file ends
        /// with no such record, in which case opening it as an archive reports the problem.
        /// </returns>
        internal static int? ReadDeclaredEntryCount(string archivePath)
        {
            const int recordLength = 22;
            const int maxCommentLength = ushort.MaxValue;

            using var stream = new FileStream(
                archivePath,
                FileMode.Open,
                System.IO.FileAccess.Read, // qualified: the RFiDGear.Infrastructure.FileAccess namespace shadows the enum
                FileShare.ReadWrite | FileShare.Delete);

            var tailLength = (int)Math.Min(stream.Length, recordLength + maxCommentLength);

            if (tailLength < recordLength)
            {
                return null;
            }

            var tail = new byte[tailLength];
            stream.Seek(-tailLength, SeekOrigin.End);
            stream.ReadExactly(tail);

            // The record ends the file, followed only by its comment, so the last signature is the real one.
            for (var i = tailLength - recordLength; i >= 0; i--)
            {
                if (tail[i] == 0x50 && tail[i + 1] == 0x4B && tail[i + 2] == 0x05 && tail[i + 3] == 0x06)
                {
                    return BinaryPrimitives.ReadUInt16LittleEndian(tail.AsSpan(i + 10, 2));
                }
            }

            return null;
        }

        /// <summary>
        /// Reads one entry, giving up as soon as it grows past <see cref="MaxProjectEntryBytes"/>. The limit is
        /// checked on the bytes actually produced, not on the size the archive claims.
        /// </summary>
        private byte[] ReadEntryBytes(ZipArchiveEntry entry)
        {
            using var source = entry.Open();
            using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            long total = 0;
            int read;

            while ((read = source.Read(chunk, 0, chunk.Length)) > 0)
            {
                total += read;

                if (total > MaxProjectEntryBytes)
                {
                    throw new InvalidDataException("The project entry is larger than a project can be.");
                }

                buffer.Write(chunk, 0, read);
            }

            return buffer.ToArray();
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
        /// <summary>Creates the result of preparing a project file for reading.</summary>
        /// <param name="success">Whether the file could be prepared.</param>
        /// <param name="manifestVersion">The manifest text exactly as stored in the file, or <c>null</c> if it has none.</param>
        /// <param name="isSupportedVersion">Whether the running application can read a file of that manifest version.</param>
        /// <param name="reader">The project text.</param>
        /// <param name="fileType">How the file is stored.</param>
        public ProjectLoadResult(bool success, string manifestVersion, bool isSupportedVersion, TextReader reader, ProjectFileType fileType)
        {
            Success = success;
            ManifestVersion = manifestVersion;
            IsSupportedVersion = isSupportedVersion;
            Reader = reader;
            FileType = fileType;
        }

        public bool Success { get; }

        /// <summary>The manifest text as stored in the file (for example <c>2.0.10</c>), or <c>null</c> if it has none.</summary>
        public string ManifestVersion { get; }

        public bool IsSupportedVersion { get; }

        public TextReader Reader { get; }

        public ProjectFileType FileType { get; }

        public static ProjectLoadResult Failed()
        {
            return new ProjectLoadResult(false, null, false, TextReader.Null, ProjectFileType.Unknown);
        }
    }

    public class SettingsLoadResult
    {
        /// <summary>Creates the result of loading the settings file.</summary>
        /// <param name="success">Whether the settings could be loaded.</param>
        /// <param name="manifestVersion">The manifest text exactly as stored in the file, or <c>null</c> if it has none.</param>
        /// <param name="isSupportedVersion">Whether the running application can read a file of that manifest version.</param>
        /// <param name="defaultSpecification">The settings.</param>
        /// <param name="settingsPath">The file the settings came from.</param>
        public SettingsLoadResult(bool success, string manifestVersion, bool isSupportedVersion, DefaultSpecification defaultSpecification, string settingsPath)
        {
            Success = success;
            ManifestVersion = manifestVersion;
            IsSupportedVersion = isSupportedVersion;
            DefaultSpecification = defaultSpecification;
            SettingsPath = settingsPath;
        }

        public bool Success { get; }

        /// <summary>The manifest text as stored in the file (for example <c>2.0.10</c>), or <c>null</c> if it has none.</summary>
        public string ManifestVersion { get; }

        public bool IsSupportedVersion { get; }

        public DefaultSpecification DefaultSpecification { get; }

        public string SettingsPath { get; }

        public static SettingsLoadResult Failed()
        {
            return new SettingsLoadResult(false, null, false, new DefaultSpecification(), string.Empty);
        }
    }
}
