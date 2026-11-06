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
    /// Builds throw-away <c>.rfPrj</c> archives for host-contract tests. Callers own the returned
    /// files and must delete them.
    /// </summary>
    internal static class ProjectFixtures
    {
        /// <summary>Path of the checked-in, empty project fixture copied next to the test assembly.</summary>
        public static string EmptyProjectPath => Path.Combine(AppContext.BaseDirectory, "Fixtures", "minimal.rfPrj");

        /// <summary>
        /// Creates a project archive containing <paramref name="taskCount"/> <see cref="CommonTaskViewModel"/>
        /// tasks whose task indexes are "0", "1", ... in project order.
        /// </summary>
        /// <param name="taskCount">Number of tasks to serialize.</param>
        /// <param name="manifestVersion">Optional manifest version, for example a value newer than the app supports.</param>
        /// <returns>Full path of the generated file in the system temp directory.</returns>
        public static string CreateProject(int taskCount, string? manifestVersion = null)
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

            var path = NewTempProjectPath();
            var serializer = new XmlSerializer(typeof(ChipTaskHandlerModel));
            using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
            var entry = archive.CreateEntry("taskdatabase.xml");
            using var entryStream = entry.Open();
            // Serialize straight to a UTF-8 writer so the XML declaration matches the bytes.
            using var writer = new StreamWriter(entryStream, new UTF8Encoding(false));
            serializer.Serialize(writer, model);
            return path;
        }

        /// <summary>
        /// Creates a file with the <c>.rfPrj</c> extension whose content is not a zip archive.
        /// </summary>
        /// <returns>Full path of the generated file in the system temp directory.</returns>
        public static string CreateNotAnArchive()
        {
            var path = NewTempProjectPath();
            File.WriteAllText(path, "this is not a zip archive");
            return path;
        }

        private static string NewTempProjectPath() =>
            Path.Combine(Path.GetTempPath(), $"rfidgear-test-{Guid.NewGuid():N}.rfPrj");
    }
}
