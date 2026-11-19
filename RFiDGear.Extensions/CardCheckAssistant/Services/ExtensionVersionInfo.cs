using System;
using System.Reflection;

namespace RFiDGear.Extensions.CardCheckAssistant.Services
{
    /// <summary>
    /// Which version of the Card Check Assistant is running, for the page to show. The extension is released on its own,
    /// not with RFiDGear, so the number of the application says nothing about it: this is the number of the extension
    /// file itself, the number of its support library (which has to be the same, the two files are one product) and
    /// where the file was loaded from.
    /// </summary>
    /// <param name="Version">The version of the extension, for example <c>1.1.0</c>; empty if it cannot be read.</param>
    /// <param name="Build">The short commit the extension was built from, or empty.</param>
    /// <param name="LibraryVersion">The version of the support library; empty if it is not known.</param>
    /// <param name="HostVersion">The version of RFiDGear that loaded the extension; empty if the host does not say.</param>
    /// <param name="Location">The full path of the extension file; empty if the assembly has none.</param>
    internal sealed record ExtensionVersionInfo(string Version, string Build, string LibraryVersion, string HostVersion, string Location)
    {
        /// <summary>The length of the commit that is shown; a build writes the whole hash.</summary>
        private const int BuildLength = 7;

        /// <summary>
        /// Gets a value indicating whether the support library is of another version than the extension: a half-finished
        /// update, where one of the two files was replaced and the other was not.
        /// </summary>
        public bool LibraryDiffers =>
            LibraryVersion.Length > 0 && Version.Length > 0 && !string.Equals(Version, LibraryVersion, StringComparison.Ordinal);

        /// <summary>Reads what the two assemblies say about themselves.</summary>
        /// <param name="extension">The extension's assembly.</param>
        /// <param name="library">The support library's assembly, or <c>null</c> if it is not known.</param>
        /// <param name="hostVersion">The version RFiDGear reports for itself, or <c>null</c>.</param>
        /// <returns>The information; never throws, and a value that cannot be read is empty.</returns>
        internal static ExtensionVersionInfo Read(Assembly extension, Assembly? library, string? hostVersion)
        {
            if (extension == null)
            {
                throw new ArgumentNullException(nameof(extension));
            }

            return new ExtensionVersionInfo(
                VersionOf(extension),
                BuildOf(extension),
                library == null ? string.Empty : VersionOf(library),
                hostVersion?.Trim() ?? string.Empty,
                LocationOf(extension));
        }

        /// <summary>
        /// The version a person is told for an assembly: its informational version without the commit that the build
        /// appends (<c>1.1.0+6ee08e0...</c> is 1.1.0), else its file version without a trailing <c>.0</c>.
        /// </summary>
        /// <param name="assembly">The assembly.</param>
        /// <returns>The version, or empty.</returns>
        internal static string VersionOf(Assembly assembly)
        {
            var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(informational))
            {
                var plus = informational!.IndexOf('+');
                var version = (plus < 0 ? informational : informational.Substring(0, plus)).Trim();
                if (version.Length > 0)
                {
                    return version;
                }
            }

            var file = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
            return string.IsNullOrWhiteSpace(file) ? string.Empty : TrimTrailingZeroPart(file!.Trim());
        }

        /// <summary>The commit the assembly was built from, shortened, or empty.</summary>
        /// <param name="assembly">The assembly.</param>
        /// <returns>The commit, or empty.</returns>
        internal static string BuildOf(Assembly assembly)
        {
            var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            var plus = informational?.IndexOf('+') ?? -1;
            if (informational == null || plus < 0)
            {
                return string.Empty;
            }

            var build = informational.Substring(plus + 1).Trim();
            return build.Length > BuildLength ? build.Substring(0, BuildLength) : build;
        }

        // The file version has four parts and the number a person knows has three: 1.1.0.0 is 1.1.0.
        private static string TrimTrailingZeroPart(string fileVersion) =>
            fileVersion.EndsWith(".0", StringComparison.Ordinal) && fileVersion.Split('.').Length == 4
                ? fileVersion.Substring(0, fileVersion.Length - 2)
                : fileVersion;

        private static string LocationOf(Assembly assembly)
        {
            try
            {
                return assembly.Location ?? string.Empty;
            }
            catch (NotSupportedException)
            {
                // A dynamic assembly has no file.
                return string.Empty;
            }
        }
    }
}
