#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RFiDGear.Infrastructure.ExtensionPackages
{
    /// <summary>One file that a package lists: its name and the SHA-256 of its content.</summary>
    /// <param name="Name">The name, without a path.</param>
    /// <param name="Sha256">The hash, as 64 hexadecimal digits in lower case.</param>
    internal sealed record ExtensionPackageManifestFile(string Name, string Sha256);

    /// <summary>
    /// What a package says about itself (<c>extension.json</c> at the top of the archive). The format is deliberately small:
    /// what the extension is, which version, which files make it (the extension, its support libraries, its payload), and the
    /// oldest RFiDGear it works with. It has no part that runs: an extension provider's own deployment is the choice of the
    /// files, nothing is executed when a package is installed.
    /// <code>
    /// { "schema": 1, "id": "RFiDGear.Extensions.CardCheckAssistant", "displayName": "Card Check Assistant", "version": "1.1.0",
    ///   "minHostVersion": "2.1.0", "notes": "...", "files": [ { "name": "RFiDGear.Extensions.CardCheckAssistant.dll", "sha256": "..." } ] }
    /// </code>
    /// </summary>
    internal sealed class ExtensionPackageManifest
    {
        /// <summary>The package format this version of RFiDGear reads.</summary>
        internal const int SupportedSchema = 1;

        private static readonly Regex IdPattern = new Regex(@"^RFiDGear\.Extensions\.[A-Za-z0-9_]+(\.[A-Za-z0-9_]+)*$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
        private static readonly Regex FileNamePattern = new Regex(@"^[A-Za-z0-9][A-Za-z0-9_.\-]{0,127}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
        private static readonly Regex HashPattern = new Regex("^[0-9a-fA-F]{64}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
        private static readonly string[] AllowedFileTypes = { ".dll", ".pdb" };

        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        private ExtensionPackageManifest(string id, string displayName, Version version, Version? minHostVersion, string notes, IReadOnlyList<ExtensionPackageManifestFile> files)
        {
            Id = id;
            DisplayName = displayName;
            Version = version;
            MinHostVersion = minHostVersion;
            Notes = notes;
            Files = files;
        }

        /// <summary>Gets the id of the extension, which is the name of its assembly (<c>RFiDGear.Extensions.CardCheckAssistant</c>).</summary>
        public string Id { get; }

        /// <summary>Gets the name for the person: the one that the confirmation shows.</summary>
        public string DisplayName { get; }

        /// <summary>Gets the version of the extension.</summary>
        public Version Version { get; }

        /// <summary>Gets the oldest RFiDGear the package works with; <c>null</c> if it does not say.</summary>
        public Version? MinHostVersion { get; }

        /// <summary>Gets a short text for the person from the provider; empty if there is none.</summary>
        public string Notes { get; }

        /// <summary>Gets the files that make the extension, each with the hash it has to have.</summary>
        public IReadOnlyList<ExtensionPackageManifestFile> Files { get; }

        /// <summary>Gets the name of the assembly of the extension itself: the one file that carries its id.</summary>
        public string MainAssemblyName => Id + ".dll";

        /// <summary>Reads and checks a manifest. Nothing in it is trusted: every name is looked at before it is used for anything.</summary>
        /// <param name="json">The content of <c>extension.json</c>.</param>
        /// <returns>The manifest, or the problem.</returns>
        internal static (ExtensionPackageManifest? Manifest, ExtensionPackageProblem? Problem) Parse(string json)
        {
            Document? document;
            try
            {
                document = JsonSerializer.Deserialize<Document>(json, Options);
            }
            catch (JsonException)
            {
                return (null, ExtensionPackageProblem.BadManifest);
            }

            if (document == null)
            {
                return (null, ExtensionPackageProblem.BadManifest);
            }

            if (document.Schema != SupportedSchema)
            {
                return (null, document.Schema > SupportedSchema ? ExtensionPackageProblem.UnsupportedSchema : ExtensionPackageProblem.BadManifest);
            }

            if (document.Id == null || !IdPattern.IsMatch(document.Id) || !TryVersion(document.Version, out var version) ||
                string.IsNullOrWhiteSpace(document.DisplayName) || document.DisplayName.Length > 100 ||
                document.Files == null || document.Files.Count == 0 || document.Files.Count > ExtensionPackageReader.MaxFiles ||
                (document.Notes?.Length ?? 0) > 1000)
            {
                return (null, ExtensionPackageProblem.BadManifest);
            }

            Version? minHost = null;
            if (!string.IsNullOrWhiteSpace(document.MinHostVersion) && !TryVersion(document.MinHostVersion, out minHost))
            {
                return (null, ExtensionPackageProblem.BadManifest);
            }

            var files = new List<ExtensionPackageManifestFile>();
            foreach (var file in document.Files)
            {
                if (file?.Name == null || file.Sha256 == null || !HashPattern.IsMatch(file.Sha256))
                {
                    return (null, ExtensionPackageProblem.BadManifest);
                }

                var problem = CheckFileName(file.Name, document.Id);
                if (problem != null)
                {
                    return (null, problem);
                }

                files.Add(new ExtensionPackageManifestFile(file.Name, file.Sha256.ToLowerInvariant()));
            }

            if (files.Select(file => file.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != files.Count)
            {
                return (null, ExtensionPackageProblem.BadFileName);
            }

            if (!files.Any(file => string.Equals(file.Name, document.Id + ".dll", StringComparison.OrdinalIgnoreCase)))
            {
                return (null, ExtensionPackageProblem.MainAssemblyMissing);
            }

            return (new ExtensionPackageManifest(document.Id, document.DisplayName.Trim(), version!, minHost, document.Notes?.Trim() ?? string.Empty, files), null);
        }

        /// <summary>
        /// Looks at the name of a file before it is used as one: only a plain name of a dll or a pdb, none of the files that
        /// RFiDGear owns and that every extension shares (a second copy of them would load their types twice), and no
        /// assembly named like an extension other than the one of the package (the host would load it as one).
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="extensionId">The id of the extension the file is part of.</param>
        /// <returns>The problem, or <c>null</c> when the name is fine.</returns>
        internal static ExtensionPackageProblem? CheckFileName(string name, string extensionId)
        {
            if (!FileNamePattern.IsMatch(name) || name.Contains("..", StringComparison.Ordinal) ||
                !AllowedFileTypes.Any(type => name.EndsWith(type, StringComparison.OrdinalIgnoreCase)) ||
                string.Equals(name, ExtensionPackageLayout.ManifestFileName, StringComparison.OrdinalIgnoreCase))
            {
                return ExtensionPackageProblem.BadFileName;
            }

            var assembly = name.Substring(0, name.LastIndexOf('.'));
            if (HostOwnedAssemblies.Contains(assembly))
            {
                return ExtensionPackageProblem.HostOwnedFile;
            }

            var isOtherExtension = assembly.StartsWith(ExtensionPackageLayout.ExtensionNamePrefix, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(assembly, extensionId, StringComparison.OrdinalIgnoreCase);
            return isOtherExtension ? ExtensionPackageProblem.HostOwnedFile : null;
        }

        /// <summary>
        /// The assemblies that the host owns and that every extension uses from it. They are the same ones that the build of an
        /// extension refuses to copy next to it (<c>RejectPrivateCopiesOfHostAssemblies</c>): two copies of the contract types
        /// are two different sets of types.
        /// </summary>
        internal static readonly ISet<string> HostOwnedAssemblies = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "RFiDGear",
            "RFiDGear.Contracts",
            "RFiDGear.ExtensionSdk",
            "RFiDGear.Bridge.Contracts",
            "System.ComponentModel.Composition",
            "GemBox.Pdf",
            "Serilog",
            "SkiaSharp",
            "HarfBuzzSharp",
            "Newtonsoft.Json",
        };

        private static bool TryVersion(string? text, out Version? version)
        {
            version = null;
            if (string.IsNullOrWhiteSpace(text) || !Version.TryParse(text.Trim(), out var parsed) || parsed.Major < 0 || parsed.Minor < 0)
            {
                return false;
            }

            version = ExtensionPackageLayout.Normalize(parsed);
            return true;
        }

        private sealed class Document
        {
            public int Schema { get; set; }

            public string? Id { get; set; }

            public string? DisplayName { get; set; }

            public string? Version { get; set; }

            public string? MinHostVersion { get; set; }

            public string? Notes { get; set; }

            public List<FileEntry?>? Files { get; set; }
        }

        private sealed class FileEntry
        {
            public string? Name { get; set; }

            public string? Sha256 { get; set; }
        }
    }
}
