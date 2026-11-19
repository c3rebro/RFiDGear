#nullable enable

using System;
using System.Diagnostics;
using System.IO;

namespace RFiDGear.Infrastructure.ExtensionPackages
{
    /// <summary>
    /// Where an extension that was installed from a package lives, and how versions are written. The folder is the user's
    /// own, so installing needs no administrator: <c>%LocalAppData%\RFiDGear\InstalledExtensions\&lt;id&gt;\&lt;version&gt;\</c>, one
    /// folder for each version, with the extension and everything that belongs beside it (its support library and its
    /// payload) in it, flat, as in the machine-wide folder. A version folder is only ever made whole (by a rename of a
    /// staging folder) and never changed afterwards, so an extension that is loaded is never overwritten.
    /// <para>
    /// It is not the <c>Extensions</c> folder of the user's application data on purpose: that is where an extension keeps its
    /// own settings (the Card Check Assistant's are in <c>Extensions\CardCheckAssistant</c>), and code and data of an
    /// extension do not share a folder.
    /// </para>
    /// </summary>
    internal static class ExtensionPackageLayout
    {
        /// <summary>The folder inside the user's root where a package is unpacked before it is moved into place; a name that is no extension id.</summary>
        internal const string StagingFolderName = ".staging";

        /// <summary>The name of the file in a package that describes it.</summary>
        internal const string ManifestFileName = "extension.json";

        /// <summary>The file extension of a package.</summary>
        internal const string PackageExtension = ".rfext";

        /// <summary>The prefix of the name of an extension assembly; the host scans the machine-wide folder for it.</summary>
        internal const string ExtensionNamePrefix = "RFiDGear.Extensions";

        /// <summary>Gets the user's root for installed extensions: the user's application data, then the application's own folder.</summary>
        /// <param name="localAppData">The user's application data folder.</param>
        /// <param name="applicationName">The name of the application, which names the folder.</param>
        /// <returns>The full path; empty if either argument is blank (there is no place to install to).</returns>
        internal static string BuildUserRoot(string localAppData, string applicationName) =>
            string.IsNullOrWhiteSpace(localAppData) || string.IsNullOrWhiteSpace(applicationName)
                ? string.Empty
                : Path.Combine(localAppData, applicationName, "InstalledExtensions");

        /// <summary>Gets the user's root for installed extensions on this machine.</summary>
        /// <returns>The full path, or empty when the user has no application data folder.</returns>
        internal static string DefaultUserRoot() =>
            BuildUserRoot(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), typeof(ExtensionPackageLayout).Assembly.GetName().Name ?? string.Empty);

        /// <summary>Gets the folder of one version of an extension.</summary>
        /// <param name="userRoot">The user's root.</param>
        /// <param name="id">The id of the extension, which is the name of its assembly.</param>
        /// <param name="version">Its version.</param>
        /// <returns>The full path.</returns>
        internal static string VersionFolder(string userRoot, string id, Version version) =>
            Path.Combine(userRoot, id, Normalize(version).ToString());

        /// <summary>
        /// Gives a version four parts, so that <c>1.1.0</c> (what a person writes) and <c>1.1.0.0</c> (what a file says) are the
        /// same version and the same folder.
        /// </summary>
        /// <param name="version">The version.</param>
        /// <returns>The version with its build and revision filled in.</returns>
        internal static Version Normalize(Version version) =>
            new Version(version.Major, version.Minor, Math.Max(version.Build, 0), Math.Max(version.Revision, 0));

        /// <summary>Reads the version of an assembly file without loading it.</summary>
        /// <param name="path">The file.</param>
        /// <returns>Its file version; <c>null</c> when it has none or cannot be read.</returns>
        internal static Version? FileVersionOf(string path)
        {
            try
            {
                var info = FileVersionInfo.GetVersionInfo(path);
                var version = new Version(info.FileMajorPart, info.FileMinorPart, info.FileBuildPart, info.FilePrivatePart);
                return version == new Version(0, 0, 0, 0) ? null : version;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                return null;
            }
        }
    }
}
