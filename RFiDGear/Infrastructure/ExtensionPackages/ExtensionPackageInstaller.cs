#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace RFiDGear.Infrastructure.ExtensionPackages
{
    /// <summary>Who the Authenticode signature of a file names.</summary>
    /// <param name="Name">The name of the signer.</param>
    /// <param name="ChainTrusted">Whether the certificate of the signer chains to a root that this machine trusts. The signature itself (that it belongs to this file) is not checked.</param>
    internal sealed record SignerInfo(string Name, bool ChainTrusted);

    /// <summary>
    /// A package that is unpacked, checked and ready to be moved into place: what the person is asked about before it is.
    /// It owns a folder that is removed by <see cref="ExtensionPackageInstaller.Discard"/> or moved by
    /// <see cref="ExtensionPackageInstaller.Commit"/>.
    /// </summary>
    /// <param name="Manifest">What the package says about itself.</param>
    /// <param name="StagingFolder">The folder the files were written to.</param>
    /// <param name="InstalledVersion">The version of the extension that is used now (the machine-wide or the user's, the higher); <c>null</c> when it is not installed.</param>
    /// <param name="Signer">Who signed the extension's assembly; <c>null</c> when it is not signed.</param>
    internal sealed record StagedExtension(ExtensionPackageManifest Manifest, string StagingFolder, Version? InstalledVersion, SignerInfo? Signer);

    /// <summary>What preparing a package came to: the staged extension, or the one reason it cannot be installed.</summary>
    /// <param name="Staged">The staged extension; <c>null</c> when there is a problem.</param>
    /// <param name="Problem">Why not; <c>null</c> when there is a staged extension.</param>
    internal sealed record PrepareResult(StagedExtension? Staged, ExtensionPackageProblem? Problem);

    /// <summary>
    /// Installs an extension package for the user, in two steps so that the person can look before anything is in place:
    /// <see cref="Prepare(string)"/> reads the package, checks it against this RFiDGear and the extension that is installed,
    /// and unpacks it into a staging folder; <see cref="Commit"/> moves that folder to its version folder, in one rename.
    /// Nothing else is written, nothing is executed, and an existing version folder is never touched: the extension that RFiDGear
    /// has loaded is a file that is open, and a new version is used from the next start.
    /// </summary>
    internal sealed class ExtensionPackageInstaller
    {
        private readonly string _userRoot;
        private readonly string _machineFolder;
        private readonly Version _hostVersion;
        private readonly Func<string, Version?> _versionOf;
        private readonly Func<string, SignerInfo?> _signerOf;
        private readonly string _hostPlatform;

        /// <summary>Creates the installer.</summary>
        /// <param name="userRoot">The user's root for installed extensions.</param>
        /// <param name="machineFolder">The machine-wide extensions folder, to know which version is installed there.</param>
        /// <param name="hostVersion">The version of this RFiDGear.</param>
        /// <param name="versionOf">Reads the file version of an assembly; the file's own by default.</param>
        /// <param name="signerOf">Reads who signed a file; the Authenticode signature of the file by default.</param>
        /// <param name="hostPlatform">The platform of this RFiDGear (<c>x64</c>, <c>x86</c>); that of the running process by default.</param>
        /// <exception cref="ArgumentException"><paramref name="userRoot"/> is blank.</exception>
        public ExtensionPackageInstaller(
            string userRoot,
            string machineFolder,
            Version hostVersion,
            Func<string, Version?>? versionOf = null,
            Func<string, SignerInfo?>? signerOf = null,
            string? hostPlatform = null)
        {
            if (string.IsNullOrWhiteSpace(userRoot))
            {
                throw new ArgumentException("There is no folder to install to.", nameof(userRoot));
            }

            _userRoot = userRoot;
            _machineFolder = machineFolder ?? string.Empty;
            _hostVersion = hostVersion ?? throw new ArgumentNullException(nameof(hostVersion));
            _versionOf = versionOf ?? ExtensionPackageLayout.FileVersionOf;
            _signerOf = signerOf ?? ReadSigner;
            _hostPlatform = string.IsNullOrWhiteSpace(hostPlatform) ? RuntimeInformation.ProcessArchitecture.ToString() : hostPlatform!;
        }

        /// <summary>Reads a package from a file and prepares it.</summary>
        /// <param name="packagePath">The package.</param>
        /// <returns>The staged extension, or why it cannot be installed.</returns>
        public PrepareResult Prepare(string packagePath)
        {
            var read = ExtensionPackageReader.Read(packagePath);
            return read.Package == null ? new PrepareResult(null, read.Problem) : Prepare(read.Package);
        }

        /// <summary>Checks a package that was read against this RFiDGear and what is installed, and unpacks it.</summary>
        /// <param name="package">The package.</param>
        /// <returns>The staged extension, or why it cannot be installed.</returns>
        public PrepareResult Prepare(ExtensionPackage package)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            var manifest = package.Manifest;
            if (manifest.MinHostVersion != null && Normalized(manifest.MinHostVersion) > Normalized(_hostVersion))
            {
                return new PrepareResult(null, ExtensionPackageProblem.HostTooOld);
            }

            if (!FitsThisPlatform(manifest.Platform))
            {
                return new PrepareResult(null, ExtensionPackageProblem.WrongPlatform);
            }

            var installed = InstalledVersion(manifest.Id);
            if (installed != null && Normalized(manifest.Version) <= Normalized(installed))
            {
                return new PrepareResult(null, Normalized(manifest.Version) == Normalized(installed) ? ExtensionPackageProblem.AlreadyInstalled : ExtensionPackageProblem.NotNewer);
            }

            var staging = Path.Combine(_userRoot, ExtensionPackageLayout.StagingFolderName, Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(staging);
                foreach (var file in package.Files)
                {
                    // The names were checked as plain file names when the manifest was read; this is the second look.
                    if (!string.Equals(Path.GetFileName(file.Name), file.Name, StringComparison.Ordinal))
                    {
                        TryDeleteFolder(staging);
                        return new PrepareResult(null, ExtensionPackageProblem.BadFileName);
                    }

                    File.WriteAllBytes(Path.Combine(staging, file.Name), file.Content);
                }

                var main = Path.Combine(staging, manifest.MainAssemblyName);
                var fileVersion = _versionOf(main);
                if (fileVersion == null || Normalized(fileVersion) != Normalized(manifest.Version))
                {
                    TryDeleteFolder(staging);
                    return new PrepareResult(null, ExtensionPackageProblem.VersionMismatch);
                }

                return new PrepareResult(new StagedExtension(manifest, staging, installed, _signerOf(main)), null);
            }
            catch (Exception ex) when (IsFileProblem(ex))
            {
                TryDeleteFolder(staging);
                return new PrepareResult(null, ExtensionPackageProblem.CannotWrite);
            }
        }

        /// <summary>Moves a staged extension into its version folder. The extension is used from the next start of RFiDGear.</summary>
        /// <param name="staged">What <see cref="Prepare(string)"/> made.</param>
        /// <returns><c>null</c> when it is installed; else why not (the staging folder is removed either way).</returns>
        public ExtensionPackageProblem? Commit(StagedExtension staged)
        {
            if (staged == null)
            {
                throw new ArgumentNullException(nameof(staged));
            }

            var target = ExtensionPackageLayout.VersionFolder(_userRoot, staged.Manifest.Id, staged.Manifest.Version);
            try
            {
                if (Directory.Exists(target))
                {
                    Discard(staged);
                    return ExtensionPackageProblem.AlreadyInstalled;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                Directory.Move(staged.StagingFolder, target);
                return null;
            }
            catch (Exception ex) when (IsFileProblem(ex))
            {
                Discard(staged);
                return ExtensionPackageProblem.CannotWrite;
            }
        }

        /// <summary>Removes a staged extension that is not going to be installed.</summary>
        /// <param name="staged">What <see cref="Prepare(string)"/> made.</param>
        public void Discard(StagedExtension staged)
        {
            if (staged == null)
            {
                throw new ArgumentNullException(nameof(staged));
            }

            TryDeleteFolder(staged.StagingFolder);
        }

        /// <summary>Gets the version of an extension that is used now: the higher of the machine-wide and the user's copy.</summary>
        /// <param name="id">The id of the extension.</param>
        /// <returns>The version; <c>null</c> when it is not installed.</returns>
        public Version? InstalledVersion(string id)
        {
            var selection = ExtensionSelector.Select(MefHelper.GetExtensionAssemblyPaths(_machineFolder), _userRoot, _versionOf);
            return ExtensionSelector.VersionOfInstalled(selection, id);
        }

        /// <summary>
        /// Tidies the user's root before anything in it is loaded: what a package that was cut short left in the staging
        /// folder, and the old versions of each extension, of which the newest few are kept (the one that is used and the one
        /// before it, to go back to by hand). It never fails; what cannot be removed stays.
        /// </summary>
        /// <param name="userRoot">The user's root for installed extensions.</param>
        /// <param name="keep">How many versions of each extension stay.</param>
        internal static void Tidy(string? userRoot, int keep = 2)
        {
            if (string.IsNullOrWhiteSpace(userRoot) || !Directory.Exists(userRoot))
            {
                return;
            }

            try
            {
                var staging = Path.Combine(userRoot, ExtensionPackageLayout.StagingFolderName);
                if (Directory.Exists(staging))
                {
                    foreach (var leftover in Directory.EnumerateDirectories(staging).ToList())
                    {
                        TryDeleteFolder(leftover);
                    }
                }

                foreach (var extensionFolder in Directory.EnumerateDirectories(userRoot).ToList())
                {
                    if (!Path.GetFileName(extensionFolder).StartsWith(ExtensionPackageLayout.ExtensionNamePrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var versions = Directory.EnumerateDirectories(extensionFolder)
                        .Select(folder => (Folder: folder, Version: Version.TryParse(Path.GetFileName(folder), out var version) ? version : null))
                        .Where(entry => entry.Version != null)
                        .OrderByDescending(entry => entry.Version)
                        .ToList();

                    foreach (var old in versions.Skip(Math.Max(keep, 1)))
                    {
                        TryDeleteFolder(old.Folder);
                    }
                }
            }
            catch (Exception ex) when (IsFileProblem(ex))
            {
                // A folder that cannot be listed stays as it is.
            }
        }

        // A package made for no platform or for AnyCPU has nothing native in it and works in both; one made for x64 or x86 carries
        // native files (the SQL driver's sni.dll) that load only into a process of that platform. The version check would let the
        // wrong one in and then refuse the right one as "installed already".
        private bool FitsThisPlatform(string platform) =>
            string.IsNullOrWhiteSpace(platform)
            || string.Equals(platform, "AnyCPU", StringComparison.OrdinalIgnoreCase)
            || string.Equals(platform, _hostPlatform, StringComparison.OrdinalIgnoreCase);

        private static Version Normalized(Version version) => ExtensionPackageLayout.Normalize(version);

        private static bool IsFileProblem(Exception ex) =>
            ex is IOException or UnauthorizedAccessException or SecurityException or ArgumentException or NotSupportedException;

        private static void TryDeleteFolder(string folder)
        {
            try
            {
                if (Directory.Exists(folder))
                {
                    Directory.Delete(folder, recursive: true);
                }
            }
            catch (Exception ex) when (IsFileProblem(ex))
            {
                // A folder that cannot be removed (something holds a file in it) is tidied at the next start.
            }
        }

        // The signer is named for the person who is asked; the signature is not what the installation rests on.
        private static SignerInfo? ReadSigner(string path)
        {
            try
            {
                using var certificate = new X509Certificate2(X509Certificate.CreateFromSignedFile(path));
                using var chain = new X509Chain();
                var trusted = chain.Build(certificate);
                var name = certificate.GetNameInfo(X509NameType.SimpleName, false);
                return new SignerInfo(string.IsNullOrWhiteSpace(name) ? certificate.Subject : name, trusted);
            }
            catch (Exception ex) when (ex is CryptographicException or IOException or ArgumentException or NotSupportedException)
            {
                // No signature, or one that cannot be read, is "not signed".
                return null;
            }
        }
    }
}
