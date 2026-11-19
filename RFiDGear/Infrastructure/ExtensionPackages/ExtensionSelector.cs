#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RFiDGear.Infrastructure.ExtensionPackages
{
    /// <summary>One file that could be loaded as an extension.</summary>
    /// <param name="AssemblyPath">The full path of the extension's assembly.</param>
    /// <param name="IsUserInstalled">Whether it comes from the user's own folder (an installed package) and not from the machine-wide one.</param>
    /// <param name="Version">Its file version; <c>null</c> when it has none.</param>
    internal sealed record ExtensionCandidate(string AssemblyPath, bool IsUserInstalled, Version? Version);

    /// <summary>One extension and the files that could stand for it, the one that is to be used first.</summary>
    /// <param name="FileName">The name of the assembly file, which is what the extensions have in common.</param>
    /// <param name="Candidates">The files, the best first; the second, if there is one, is what is tried when the first cannot be loaded.</param>
    internal sealed record SelectedExtension(string FileName, IReadOnlyList<ExtensionCandidate> Candidates);

    /// <summary>
    /// Decides which file stands for an extension when it is installed twice: once for every user of the machine (the
    /// installer's folder) and once for this user (a package). The higher file version wins, and the machine-wide copy
    /// when they are equal. Never both: an extension that was loaded twice would export its parts twice, and the contract
    /// types of the host would be matched by two different copies.
    /// <para>
    /// It looks at names and versions only; it opens no file and loads nothing, so it can run before anything is composed.
    /// </para>
    /// </summary>
    internal static class ExtensionSelector
    {
        /// <summary>Chooses the files to load.</summary>
        /// <param name="machineAssemblyPaths">The extension assemblies in the machine-wide folder.</param>
        /// <param name="userRoot">The user's root for installed extensions; blank or missing means none are installed.</param>
        /// <param name="versionOf">Reads the file version of an assembly.</param>
        /// <returns>One entry for each extension, ordered by the name of the file.</returns>
        internal static IReadOnlyList<SelectedExtension> Select(IEnumerable<string> machineAssemblyPaths, string? userRoot, Func<string, Version?> versionOf)
        {
            var byName = new Dictionary<string, List<ExtensionCandidate>>(StringComparer.OrdinalIgnoreCase);

            foreach (var path in machineAssemblyPaths)
            {
                Add(byName, path, new ExtensionCandidate(path, false, versionOf(path)));
            }

            foreach (var installed in UserCandidates(userRoot, versionOf))
            {
                Add(byName, installed.AssemblyPath, installed);
            }

            return byName
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(pair => new SelectedExtension(pair.Key, pair.Value.OrderByDescending(Rank).ThenBy(candidate => candidate.IsUserInstalled).ToList()))
                .ToList();
        }

        /// <summary>Finds the extension that is to be used for an id and says which version it is.</summary>
        /// <param name="selection">What <see cref="Select"/> chose.</param>
        /// <param name="id">The id of the extension, the name of its assembly without the type.</param>
        /// <returns>The version; <c>null</c> when the extension is not installed, or its file has no version.</returns>
        internal static Version? VersionOfInstalled(IEnumerable<SelectedExtension> selection, string id) =>
            selection
                .FirstOrDefault(extension => string.Equals(Path.GetFileNameWithoutExtension(extension.FileName), id, StringComparison.OrdinalIgnoreCase))
                ?.Candidates[0].Version;

        // The version, with a file that has none last; between equal versions the machine-wide copy comes before the user's.
        private static Version Rank(ExtensionCandidate candidate) => candidate.Version ?? new Version(0, 0, 0, 0);

        private static void Add(Dictionary<string, List<ExtensionCandidate>> byName, string path, ExtensionCandidate candidate)
        {
            var name = Path.GetFileName(path);
            if (!byName.TryGetValue(name, out var list))
            {
                byName[name] = list = new List<ExtensionCandidate>();
            }

            list.Add(candidate);
        }

        // The newest version folder of each extension in the user's root that holds the extension's assembly. A folder with
        // another name (the staging folder, a version that is no version, a folder of something else) is not looked at.
        private static IEnumerable<ExtensionCandidate> UserCandidates(string? userRoot, Func<string, Version?> versionOf)
        {
            if (string.IsNullOrWhiteSpace(userRoot) || !Directory.Exists(userRoot))
            {
                yield break;
            }

            foreach (var extensionFolder in SafeFolders(userRoot!))
            {
                var id = Path.GetFileName(extensionFolder);
                if (!id.StartsWith(ExtensionPackageLayout.ExtensionNamePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string? best = null;
                Version? bestFolderVersion = null;
                foreach (var versionFolder in SafeFolders(extensionFolder))
                {
                    var main = Path.Combine(versionFolder, id + ".dll");
                    if (Version.TryParse(Path.GetFileName(versionFolder), out var folderVersion) &&
                        File.Exists(main) &&
                        !File.Exists(Path.Combine(versionFolder, ExtensionPackageLayout.FailedMarkerFileName)) &&
                        (bestFolderVersion == null || folderVersion > bestFolderVersion))
                    {
                        best = main;
                        bestFolderVersion = folderVersion;
                    }
                }

                if (best != null)
                {
                    yield return new ExtensionCandidate(best, true, versionOf(best) ?? bestFolderVersion);
                }
            }
        }

        private static IReadOnlyList<string> SafeFolders(string folder)
        {
            try
            {
                return Directory.EnumerateDirectories(folder).ToList();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return Array.Empty<string>();
            }
        }
    }
}
