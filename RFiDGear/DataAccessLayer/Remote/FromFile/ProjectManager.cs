using System;
using System.IO;
using System.Xml;
using Ionic.Zip;

namespace RFiDGear.DataAccessLayer
{
    public class ProjectManager
    {
        private const string ChipDatabaseFileName = "chipdatabase.xml";
        private const string TaskDatabaseFileNameCompressed = "chipdatabase.rfPrj";
        private const string TaskDatabaseFileName = "taskdatabase.xml";

        public string AppDataPath { get; }

        public string ChipDatabasePath => Path.Combine(AppDataPath, ChipDatabaseFileName);

        public string TaskDatabasePath => Path.Combine(AppDataPath, TaskDatabaseFileName);

        public string CompressedTaskDatabasePath => Path.Combine(AppDataPath, TaskDatabaseFileNameCompressed);

        public ProjectManager()
        {
            AppDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RFiDGear");

            EnsureAppDataDirectory();
        }

        public ProjectFileType GetProjectFileType(FileInfo file)
        {
            if (file.Extension.Equals(".xml", StringComparison.OrdinalIgnoreCase))
            {
                return ProjectFileType.Xml;
            }

            if (file.Extension.Equals(".rfprj", StringComparison.OrdinalIgnoreCase))
            {
                return ProjectFileType.Archive;
            }

            return ProjectFileType.Unknown;
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

        public void ResetTemporaryArtifacts()
        {
            EnsureAppDataDirectory();

            CleanTemporaryFiles();
            RemoveTaskArtifacts();
        }

        public bool ValidateManifestVersion(int manifestVersion, Version appVersion)
        {
            var currentVersion = Convert.ToInt32(string.Format("{0}{1}{2}", appVersion.Major, appVersion.Minor, appVersion.Build));

            return manifestVersion <= currentVersion;
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
            using (var zip = ZipFile.Read(archivePath))
            {
                zip.ExtractAll(AppDataPath, ExtractExistingFileAction.OverwriteSilently);
            }
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
}
