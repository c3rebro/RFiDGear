#nullable enable

using System;
using System.Collections.Generic;

namespace RFiDGear.Infrastructure.ExtensionPackages
{
    /// <summary>Why a package could not be read or installed. A code and nothing else: the text for the person is chosen by the caller, and no file name, path or exception text travels with it.</summary>
    internal enum ExtensionPackageProblem
    {
        /// <summary>The file cannot be read, or it is no zip archive.</summary>
        NotAPackage,

        /// <summary>The archive has more files than a package may have, or a file or the whole is bigger than a package may be.</summary>
        TooLarge,

        /// <summary>There is no <c>extension.json</c> at the top of the archive.</summary>
        NoManifest,

        /// <summary>The manifest is no valid JSON of the shape the package format asks for.</summary>
        BadManifest,

        /// <summary>The manifest says it is of a package format version this version of RFiDGear does not know.</summary>
        UnsupportedSchema,

        /// <summary>A file has a name that is not allowed: a folder, a path, a type other than dll and pdb.</summary>
        BadFileName,

        /// <summary>The package carries a file that belongs to RFiDGear and is shared by all extensions, or another extension.</summary>
        HostOwnedFile,

        /// <summary>The assembly of the extension (named like its id) is not among the files.</summary>
        MainAssemblyMissing,

        /// <summary>The archive holds a file that the manifest does not list, or the manifest lists one that is not in the archive.</summary>
        FileListMismatch,

        /// <summary>A file does not have the SHA-256 that the manifest gives.</summary>
        HashMismatch,

        /// <summary>The version of the assembly of the extension is not the version the manifest gives.</summary>
        VersionMismatch,

        /// <summary>The package needs a newer RFiDGear.</summary>
        HostTooOld,

        /// <summary>The same version of the extension is installed already.</summary>
        AlreadyInstalled,

        /// <summary>A newer version of the extension is installed already, and it is the one that would be used.</summary>
        NotNewer,

        /// <summary>The folder that the package is to be installed in cannot be written.</summary>
        CannotWrite,
    }

    /// <summary>One file of a package, as it was read and checked.</summary>
    /// <param name="Name">The name of the file, without a path.</param>
    /// <param name="Content">The content.</param>
    internal sealed record ExtensionPackageFile(string Name, byte[] Content);

    /// <summary>A package whose manifest was read and whose files all matched their hashes: nothing yet says that it may be installed.</summary>
    internal sealed class ExtensionPackage
    {
        /// <summary>Creates the package.</summary>
        /// <param name="manifest">What the package says about itself.</param>
        /// <param name="files">The files, as they were in the archive.</param>
        public ExtensionPackage(ExtensionPackageManifest manifest, IReadOnlyList<ExtensionPackageFile> files)
        {
            Manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
            Files = files ?? throw new ArgumentNullException(nameof(files));
        }

        /// <summary>Gets what the package says about itself.</summary>
        public ExtensionPackageManifest Manifest { get; }

        /// <summary>Gets the files.</summary>
        public IReadOnlyList<ExtensionPackageFile> Files { get; }
    }

    /// <summary>What reading a package came to: the package, or the one reason it cannot be used.</summary>
    /// <param name="Package">The package; <c>null</c> when there is a problem.</param>
    /// <param name="Problem">Why not; <c>null</c> when there is a package.</param>
    internal sealed record ExtensionPackageReadResult(ExtensionPackage? Package, ExtensionPackageProblem? Problem)
    {
        /// <summary>Gets a value indicating whether the package could be read.</summary>
        public bool Success => Package != null;

        /// <summary>Makes the result of a package that could be read.</summary>
        /// <param name="package">The package.</param>
        /// <returns>The result.</returns>
        internal static ExtensionPackageReadResult Ok(ExtensionPackage package) => new ExtensionPackageReadResult(package, null);

        /// <summary>Makes the result of a package that cannot be used.</summary>
        /// <param name="problem">Why not.</param>
        /// <returns>The result.</returns>
        internal static ExtensionPackageReadResult Fail(ExtensionPackageProblem problem) => new ExtensionPackageReadResult(null, problem);
    }
}
