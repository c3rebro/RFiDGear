using System;
using System.IO;
using System.Linq;

namespace RFiDGear.Extensions.CardCheckAssistant.Services
{
    /// <summary>What <see cref="ReportTargetValidator"/> found out about a report target.</summary>
    internal enum ReportTargetCheck
    {
        /// <summary>Nothing was entered: no report is written.</summary>
        None,

        /// <summary>A full path to a file in an existing folder.</summary>
        Valid,

        /// <summary>A relative path, or a drive-relative one such as <c>C:out.pdf</c>.</summary>
        NotFullyQualified,

        /// <summary>The folder does not exist (or the path has no folder).</summary>
        FolderMissing,

        /// <summary>The path names a folder, or ends in a separator.</summary>
        NotAFile,

        /// <summary>The folder cannot be read, or the path is not valid.</summary>
        Unusable
    }

    /// <summary>
    /// Checks a report target the way the host does before it accepts a run (<c>REPORTTARGETPATH</c>), so the page can
    /// say what is wrong next to the field instead of after the host has refused the run. The host stays the authority:
    /// whatever it refuses, this refuses too. In one respect this is deliberately stricter: a name Windows cannot create
    /// (a forbidden character, a device name such as NUL) or an existing read-only file passes the host, whose report step
    /// then only logs the failure, so the run would look successful with no report written.
    /// <c>ReportTargetValidatorParityTests</c> compares the two over a table of paths and marks those rows.
    /// </summary>
    internal static class ReportTargetValidator
    {
        /// <summary>Checks one value.</summary>
        /// <param name="target">What the user typed; blank means no report.</param>
        /// <returns>The verdict.</returns>
        public static ReportTargetCheck Check(string? target)
        {
            if (string.IsNullOrWhiteSpace(target))
            {
                return ReportTargetCheck.None;
            }

            var path = target.Trim();

            try
            {
                if (!Path.IsPathFullyQualified(path))
                {
                    return ReportTargetCheck.NotFullyQualified;
                }

                var directory = Path.GetDirectoryName(path);
                if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                {
                    return ReportTargetCheck.FolderMissing;
                }

                // The host lists the folder to number "?" placeholders, so a folder it cannot read is unusable there too.
                _ = Directory.EnumerateFiles(directory).Any();

                // A "?" placeholder is replaced by a number, so only a path without one can already be a folder.
                var fileName = Path.GetFileName(path);
                if (string.IsNullOrEmpty(fileName) || (!path.Contains('?') && Directory.Exists(path)))
                {
                    return ReportTargetCheck.NotAFile;
                }

                // The host accepts any name and the report step later logs a failure and carries on, so a name Windows
                // cannot create would give a run that looks successful and a report that was never written.
                if (HasUnusableFileName(fileName))
                {
                    return ReportTargetCheck.Unusable;
                }

                // Same for an existing report that cannot be replaced.
                if (!path.Contains('?') && File.Exists(path) && File.GetAttributes(path).HasFlag(FileAttributes.ReadOnly))
                {
                    return ReportTargetCheck.Unusable;
                }

                return ReportTargetCheck.Valid;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is NotSupportedException || ex is ArgumentException)
            {
                return ReportTargetCheck.Unusable;
            }
        }

        private static readonly string[] ReservedDeviceNames =
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

        /// <summary>
        /// Whether Windows cannot create a file with this name: a character it forbids, a device name such as NUL (with
        /// or without an extension), or a trailing space or dot. A <c>?</c> is the numbering placeholder, not a character.
        /// </summary>
        private static bool HasUnusableFileName(string fileName)
        {
            var name = fileName.Replace("?", string.Empty);

            if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.EndsWith(' ') || name.EndsWith('.'))
            {
                return true;
            }

            var stem = Path.GetFileNameWithoutExtension(name);
            return Array.Exists(ReservedDeviceNames, reserved => string.Equals(reserved, stem, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>What <see cref="ProjectPathValidator"/> found out about the project path.</summary>
    internal enum ProjectPathCheck
    {
        /// <summary>A full path to a file that exists.</summary>
        Valid,

        /// <summary>Nothing was entered.</summary>
        Required,

        /// <summary>A relative path.</summary>
        NotFullyQualified,

        /// <summary>No such file.</summary>
        NotFound
    }

    /// <summary>
    /// The file-system checks of a run, as a seam. They touch the disk, and on a network path that can block for a long
    /// time, so the view model calls them on a worker thread and a test can see that it does.
    /// </summary>
    internal interface IPathChecker
    {
        /// <summary>Checks the project path.</summary>
        /// <param name="path">What the user typed or picked.</param>
        /// <returns>The verdict.</returns>
        ProjectPathCheck CheckProject(string? path);

        /// <summary>Checks the report target.</summary>
        /// <param name="target">What the user typed; blank means no report.</param>
        /// <returns>The verdict.</returns>
        ReportTargetCheck CheckReportTarget(string? target);
    }

    /// <summary>The real checks, against the file system.</summary>
    internal sealed class FileSystemPathChecker : IPathChecker
    {
        /// <inheritdoc/>
        public ProjectPathCheck CheckProject(string? path) => ProjectPathValidator.Check(path);

        /// <inheritdoc/>
        public ReportTargetCheck CheckReportTarget(string? target) => ReportTargetValidator.Check(target);
    }

    /// <summary>
    /// Checks the project path before the host is asked to load it. Only what is certain is rejected here; whether the
    /// file is a readable project is for the host to say.
    /// </summary>
    internal static class ProjectPathValidator
    {
        /// <summary>Checks one value.</summary>
        /// <param name="path">What the user typed or picked.</param>
        /// <returns>The verdict.</returns>
        public static ProjectPathCheck Check(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return ProjectPathCheck.Required;
            }

            var trimmed = path.Trim();

            if (!Path.IsPathFullyQualified(trimmed))
            {
                return ProjectPathCheck.NotFullyQualified;
            }

            return File.Exists(trimmed) ? ProjectPathCheck.Valid : ProjectPathCheck.NotFound;
        }
    }
}
