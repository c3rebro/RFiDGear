using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml.Serialization;

using RFiDGear.Models;
using RFiDGear.ViewModel.TaskSetupViewModels;

namespace RFiDGear.Tests
{
    /// <summary>
    /// Builds throw-away project files for host-contract tests. Callers own the returned
    /// files and must delete them.
    /// </summary>
    internal static class ProjectFixtures
    {
        private const string XmlDeclaration = "<?xml version=\"1.0\" encoding=\"utf-8\"?>";
        private const string XsiNamespace = "http://www.w3.org/2001/XMLSchema-instance";

        /// <summary>Path of the checked-in, empty project fixture copied next to the test assembly.</summary>
        public static string EmptyProjectPath => Path.Combine(AppContext.BaseDirectory, "Fixtures", "minimal.rfPrj");

        /// <summary>
        /// Creates a project archive containing <paramref name="taskCount"/> <see cref="CommonTaskViewModel"/>
        /// tasks whose task indexes are "0", "1", ... in project order.
        /// </summary>
        /// <param name="taskCount">Number of tasks to serialize.</param>
        /// <param name="manifestVersion">Optional manifest version, for example a value newer than the app supports.</param>
        /// <returns>Full path of the generated file in the system temp directory.</returns>
        public static string CreateProject(int taskCount, string? manifestVersion = null) =>
            CreateArchive(".rfPrj", "taskdatabase.xml", SerializeProject(taskCount, manifestVersion));

        /// <summary>
        /// Creates a plain (not zipped) <c>.xml</c> project with <paramref name="taskCount"/> tasks, the
        /// format older versions wrote and the open dialog still offers.
        /// </summary>
        /// <param name="taskCount">Number of tasks to serialize.</param>
        /// <returns>Full path of the generated file in the system temp directory.</returns>
        public static string CreatePlainXmlProject(int taskCount) =>
            CreateTextFile(".xml", SerializeProject(taskCount, manifestVersion: null));

        /// <summary>
        /// Creates a ZIP archive whose file name ends in <c>.xml</c>, as versions between 2022 and 2025 wrote
        /// when the user typed that extension in "Save As".
        /// </summary>
        /// <param name="taskCount">Number of tasks to serialize.</param>
        /// <returns>Full path of the generated file in the system temp directory.</returns>
        public static string CreateZipNamedXml(int taskCount) =>
            CreateArchive(".xml", "taskdatabase.xml", SerializeProject(taskCount, manifestVersion: null));

        /// <summary>
        /// Creates a project archive whose only entry has the given name and content, for archives that
        /// lack the expected <c>taskdatabase.xml</c> entry.
        /// </summary>
        /// <param name="entryName">Name of the single archive entry.</param>
        /// <param name="xml">Content of the entry.</param>
        /// <returns>Full path of the generated file in the system temp directory.</returns>
        public static string CreateArchiveWithEntry(string entryName, string xml) =>
            CreateArchive(".rfPrj", entryName, xml);

        /// <summary>
        /// Creates a valid chip-tree XML document (what "Save Chip" writes), which is not a task project.
        /// </summary>
        /// <returns>Full path of the generated <c>.xml</c> file in the system temp directory.</returns>
        public static string CreateChipTreeXml() =>
            CreateTextFile(
                ".xml",
                XmlDeclaration + "<ArrayOfRFiDChipParentLayerViewModel xmlns:xsi=\"" + XsiNamespace + "\" />");

        /// <summary>
        /// Creates an XML file whose root element is explicitly nil, which deserializes to <c>null</c>.
        /// </summary>
        /// <returns>Full path of the generated <c>.xml</c> file in the system temp directory.</returns>
        public static string CreateNilRootXml() =>
            CreateTextFile(
                ".xml",
                XmlDeclaration + "<ChipTaskHandlerModel xmlns:xsi=\"" + XsiNamespace + "\" xsi:nil=\"true\" />");

        /// <summary>Creates an empty (0-byte) file with the given extension.</summary>
        /// <param name="extension">Extension including the dot.</param>
        /// <returns>Full path of the generated file in the system temp directory.</returns>
        public static string CreateEmptyFile(string extension)
        {
            var path = NewTempPath(extension);
            File.WriteAllBytes(path, Array.Empty<byte>());
            return path;
        }

        /// <summary>
        /// Creates a file with the <c>.rfPrj</c> extension whose content is not a zip archive.
        /// </summary>
        /// <returns>Full path of the generated file in the system temp directory.</returns>
        public static string CreateNotAnArchive() => CreateTextFile(".rfPrj", "this is not a zip archive");

        /// <summary>
        /// Serializes a project exactly as the app does, so fixtures stay in sync with the model. The
        /// XML declaration says UTF-8 (a <see cref="StringWriter"/> would claim UTF-16), so the text can be
        /// written to disk as UTF-8 and still be a valid, loadable document.
        /// </summary>
        public static string SerializeProject(int taskCount, string? manifestVersion)
        {
            var model = new ChipTaskHandlerModel();
            if (manifestVersion != null)
            {
                model.ManifestVersion = manifestVersion;
            }

            for (var i = 0; i < taskCount; i++)
            {
                model.TaskCollection.Add(new CommonTaskViewModel { CurrentTaskIndex = i.ToString() });
            }

            using var writer = new StringWriter();
            new XmlSerializer(typeof(ChipTaskHandlerModel)).Serialize(writer, model);
            return StripEncodingDeclaration(writer.ToString());
        }

        private static string CreateArchive(string extension, string entryName, string xml)
        {
            var path = NewTempPath(extension);
            using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
            var entry = archive.CreateEntry(entryName);
            using var entryStream = entry.Open();
            // Strip the writer's "utf-16" declaration: the entry bytes are UTF-8.
            using var writer = new StreamWriter(entryStream, new UTF8Encoding(false));
            writer.Write(StripEncodingDeclaration(xml));
            return path;
        }

        private static string CreateTextFile(string extension, string content)
        {
            var path = NewTempPath(extension);
            File.WriteAllText(path, StripEncodingDeclaration(content), new UTF8Encoding(false));
            return path;
        }

        private static string StripEncodingDeclaration(string xml) =>
            xml.Replace("encoding=\"utf-16\"", "encoding=\"utf-8\"", StringComparison.Ordinal);

        private static string NewTempPath(string extension) =>
            Path.Combine(Path.GetTempPath(), $"rfidgear-test-{Guid.NewGuid():N}{extension}");
    }
}
