#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security;
using System.Security.Cryptography;
using System.Text;

namespace RFiDGear.Infrastructure.ExtensionPackages
{
    /// <summary>
    /// Reads an extension package (a zip archive with <c>extension.json</c> at the top) into memory and checks everything that
    /// can be checked without installing it: the archive's limits, the manifest, every name, that the archive holds exactly the
    /// files the manifest lists, and that each has the hash the manifest gives. Nothing is written to disk, and nothing in the
    /// archive is trusted before it was looked at: it never extracts by the names in the archive.
    /// <para>
    /// The hash protects against a damaged or a mixed-up package. It does not say who made it (the manifest is in the same
    /// archive): the person is asked before anything is installed.
    /// </para>
    /// </summary>
    internal static class ExtensionPackageReader
    {
        /// <summary>The most files a package may list. The archive may hold one more, the manifest.</summary>
        internal const int MaxFiles = 64;

        /// <summary>The most bytes a file of a package may have, as the same limit for a project's file (see <c>ProjectManager</c>).</summary>
        internal const long MaxFileBytes = 64L * 1024 * 1024;

        /// <summary>The most bytes the files of a package may have together.</summary>
        internal const long MaxTotalBytes = 256L * 1024 * 1024;

        private const long MaxManifestBytes = 64 * 1024;

        /// <summary>Reads a package from a file.</summary>
        /// <param name="path">The file.</param>
        /// <returns>The package, or why it cannot be used.</returns>
        internal static ExtensionPackageReadResult Read(string path)
        {
            try
            {
                // System.IO.FileAccess in full: inside RFiDGear.Infrastructure the name means the namespace of the host's file access.
                using var stream = new FileStream(path, FileMode.Open, System.IO.FileAccess.Read, FileShare.Read);
                return Read(stream);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or SecurityException)
            {
                return ExtensionPackageReadResult.Fail(ExtensionPackageProblem.NotAPackage);
            }
        }

        /// <summary>Reads a package from a stream.</summary>
        /// <param name="stream">The archive; it is left open.</param>
        /// <returns>The package, or why it cannot be used.</returns>
        internal static ExtensionPackageReadResult Read(Stream stream)
        {
            try
            {
                using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
                return Read(archive);
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or NotSupportedException or ArgumentException or InvalidOperationException)
            {
                // A zip that is cut off or damaged says so while it is read, not only when it is opened.
                return ExtensionPackageReadResult.Fail(ExtensionPackageProblem.NotAPackage);
            }
        }

        private static ExtensionPackageReadResult Read(ZipArchive archive)
        {
            var entries = archive.Entries;
            if (entries.Count > MaxFiles + 1)
            {
                return ExtensionPackageReadResult.Fail(ExtensionPackageProblem.TooLarge);
            }

            long total = 0;
            foreach (var entry in entries)
            {
                // A package is flat, like the folder it is installed into: a name with a folder in it is never looked at further.
                if (entry.FullName.Length == 0 || entry.FullName.IndexOfAny(new[] { '/', '\\' }) >= 0)
                {
                    return ExtensionPackageReadResult.Fail(ExtensionPackageProblem.BadFileName);
                }

                total += entry.Length;
                if (entry.Length > MaxFileBytes || total > MaxTotalBytes)
                {
                    return ExtensionPackageReadResult.Fail(ExtensionPackageProblem.TooLarge);
                }
            }

            var manifestEntry = entries.FirstOrDefault(entry => string.Equals(entry.FullName, ExtensionPackageLayout.ManifestFileName, StringComparison.OrdinalIgnoreCase));
            if (manifestEntry == null)
            {
                return ExtensionPackageReadResult.Fail(ExtensionPackageProblem.NoManifest);
            }

            var manifestBytes = ReadBounded(manifestEntry, MaxManifestBytes);
            if (manifestBytes == null)
            {
                return ExtensionPackageReadResult.Fail(ExtensionPackageProblem.BadManifest);
            }

            var (manifest, problem) = ExtensionPackageManifest.Parse(new UTF8Encoding(false, false).GetString(manifestBytes));
            if (manifest == null)
            {
                return ExtensionPackageReadResult.Fail(problem ?? ExtensionPackageProblem.BadManifest);
            }

            var files = entries.Where(entry => entry != manifestEntry).ToList();
            var archiveNames = files.Select(entry => entry.FullName).ToList();
            var listedNames = manifest.Files.Select(file => file.Name).ToList();
            if (archiveNames.Count != archiveNames.Distinct(StringComparer.OrdinalIgnoreCase).Count() ||
                !archiveNames.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(listedNames))
            {
                return ExtensionPackageReadResult.Fail(ExtensionPackageProblem.FileListMismatch);
            }

            var read = new List<ExtensionPackageFile>();
            foreach (var listed in manifest.Files)
            {
                var entry = files.First(candidate => string.Equals(candidate.FullName, listed.Name, StringComparison.OrdinalIgnoreCase));
                var content = ReadBounded(entry, MaxFileBytes);
                if (content == null)
                {
                    return ExtensionPackageReadResult.Fail(ExtensionPackageProblem.TooLarge);
                }

                if (!string.Equals(Convert.ToHexString(SHA256.HashData(content)), listed.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    return ExtensionPackageReadResult.Fail(ExtensionPackageProblem.HashMismatch);
                }

                read.Add(new ExtensionPackageFile(listed.Name, content));
            }

            return ExtensionPackageReadResult.Ok(new ExtensionPackage(manifest, read));
        }

        // The size an archive declares for a file is not believed: it is read only as far as the limit.
        private static byte[]? ReadBounded(ZipArchiveEntry entry, long limit)
        {
            using var source = entry.Open();
            using var target = new MemoryStream();
            var buffer = new byte[81920];
            long total = 0;
            int count;
            while ((count = source.Read(buffer, 0, buffer.Length)) > 0)
            {
                total += count;
                if (total > limit)
                {
                    return null;
                }

                target.Write(buffer, 0, count);
            }

            return target.ToArray();
        }
    }
}
