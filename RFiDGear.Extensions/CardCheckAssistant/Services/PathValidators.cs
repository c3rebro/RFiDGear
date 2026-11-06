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
    /// say what is wrong next to the field instead of after the host has refused the run. The host stays the authority;
    /// <c>ReportTargetValidatorParityTests</c> compares the two over a table of paths.
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
                if (string.IsNullOrEmpty(Path.GetFileName(path)) || (!path.Contains('?') && Directory.Exists(path)))
                {
                    return ReportTargetCheck.NotAFile;
                }

                return ReportTargetCheck.Valid;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is NotSupportedException || ex is ArgumentException)
            {
                return ReportTargetCheck.Unusable;
            }
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
