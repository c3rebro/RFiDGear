#nullable enable
using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using RFiDGear.Infrastructure.FileAccess;
using RFiDGear.Models;

namespace RFiDGear.Services.SecureKeys
{
    /// <summary>
    /// The files of a migration: reading a project, writing the new one, and looking at the text of a project file. A seam,
    /// so that the migration is tested with real files and a vault that is a dictionary.
    /// </summary>
    internal interface IKeyMigrationProjects
    {
        /// <summary>Returns the file that a project written to <paramref name="path"/> ends up in; the writer adds an extension a project needs.</summary>
        /// <param name="path">The path the person chose.</param>
        /// <returns>The path of the file.</returns>
        string TargetPathOf(string path);

        /// <summary>Reads a project without changing anything that the application holds.</summary>
        /// <param name="path">The project file.</param>
        /// <param name="cancellationToken">Cancelled when the person gives up.</param>
        /// <returns>
        /// The project, or <c>null</c> when it cannot be read or when a newer version of the application made it: it would
        /// be written again without the parts this version does not know.
        /// </returns>
        Task<ChipTaskHandlerModel?> ReadAsync(string path, CancellationToken cancellationToken);

        /// <summary>Identifies the content of a file, so that a file which was replaced since can be told from the one that was read.</summary>
        /// <param name="path">The file.</param>
        /// <returns>The hash of the content as hexadecimal digits, or <c>null</c> when the file cannot be read.</returns>
        string? Fingerprint(string path);

        /// <summary>Writes a project to a new file. A file that exists, also one that appears while the project is written, is kept.</summary>
        /// <param name="model">The project.</param>
        /// <param name="path">The file.</param>
        /// <returns>The file that was written, or <c>null</c> when the project could not be written.</returns>
        string? Write(ChipTaskHandlerModel model, string path);

        /// <summary>Reads the text of the project in a file, the way it is stored: a plain project file, or the entry of an archive.</summary>
        /// <param name="path">The project file.</param>
        /// <returns>The text, or <c>null</c> when the file holds no project.</returns>
        string? ReadProjectText(string path);
    }

    /// <summary>
    /// The projects of the application. It has a folder of its own for the work files of the reader and the writer, so that
    /// opening it does not clear the files of the profile that the running application uses.
    /// </summary>
    internal sealed class DatabaseKeyMigrationProjects : IKeyMigrationProjects, IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "RFiDGearKeyMigration-" + Guid.NewGuid().ToString("N"));
        private readonly ProjectManager _projectManager;
        private readonly DatabaseReaderWriter _readerWriter;

        /// <summary>Creates the access to the projects.</summary>
        internal DatabaseKeyMigrationProjects()
        {
            _projectManager = new ProjectManager(_folder);
            _readerWriter = new DatabaseReaderWriter(_projectManager);
        }

        /// <inheritdoc />
        public string TargetPathOf(string path) => DatabaseReaderWriter.ProjectSavePath(path);

        /// <inheritdoc />
        public async Task<ChipTaskHandlerModel?> ReadAsync(string path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var (result, model) = await _readerWriter.ReadProjectAsync(path).ConfigureAwait(true);

            // The way the application opens a project: one from a newer version is refused, not read as far as it is understood.
            return result.Success && result.IsSupportedVersion ? model : null;
        }

        /// <inheritdoc />
        public string? Fingerprint(string path)
        {
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);

                return Convert.ToHexString(SHA256.HashData(stream));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                return null;
            }
        }

        /// <inheritdoc />
        public string? Write(ChipTaskHandlerModel model, string path) => _readerWriter.WriteDatabase(model, path, replaceExisting: false);

        /// <inheritdoc />
        public string? ReadProjectText(string path)
        {
            var loaded = _projectManager.PrepareProject(new FileInfo(path), Assembly.GetExecutingAssembly().GetName().Version);

            if (!loaded.Success || loaded.Reader == null)
            {
                return null;
            }

            using (loaded.Reader)
            {
                return loaded.Reader.ReadToEnd();
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_folder))
                {
                    Directory.Delete(_folder, recursive: true);
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                // A temporary folder that stays is cleaned by the system; it holds no project.
            }
        }
    }
}
