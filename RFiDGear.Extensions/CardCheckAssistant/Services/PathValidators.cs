using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using RFiDGear.ExtensionLibraries.CardCheck;

namespace RFiDGear.Extensions.CardCheckAssistant.Services
{
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
    /// time, so the check runner calls them on a worker thread and a test can see that it does.
    /// </summary>
    internal interface IPathChecker
    {
        /// <summary>Checks the project path.</summary>
        /// <param name="path">What the user typed or picked.</param>
        /// <returns>The verdict.</returns>
        ProjectPathCheck CheckProject(string? path);

        /// <summary>Says what the name of the default project means for the language lookup, and which variants lie next to it.</summary>
        /// <param name="path">A full path to the default project.</param>
        /// <returns>The finding, or <c>null</c> when there is nothing to say: the file is not there, or the folder cannot be read.</returns>
        ProjectLanguageFinding? FindLanguageVariants(string path);
    }

    /// <summary>What the name of a default project says about language variants.</summary>
    internal enum LanguageNameKind
    {
        /// <summary><c>name.rfPrj</c>: variants may lie next to it.</summary>
        Plain,

        /// <summary>More than one dot: the file is used as it is.</summary>
        MoreThanOneDot,

        /// <summary>The name ends in the word of a variant: a reserved name, the file is used as it is.</summary>
        LooksLikeVariant
    }

    /// <summary>What <see cref="IPathChecker.FindLanguageVariants"/> found.</summary>
    /// <param name="Kind">What the name says.</param>
    /// <param name="Variants">The words of the variants next to a plain project, sorted.</param>
    /// <param name="ReservedWord">The variant word a reserved name ends in, otherwise <c>null</c>.</param>
    internal sealed record ProjectLanguageFinding(LanguageNameKind Kind, IReadOnlyList<string> Variants, string? ReservedWord);

    /// <summary>The real checks, against the file system.</summary>
    internal sealed class FileSystemPathChecker : IPathChecker
    {
        /// <inheritdoc/>
        public ProjectPathCheck CheckProject(string? path) => ProjectPathValidator.Check(path);

        /// <inheritdoc/>
        public ProjectLanguageFinding? FindLanguageVariants(string path)
        {
            try
            {
                var info = ProjectVariants.Inspect(path, folder => Directory.EnumerateFiles(folder));
                if (info.Kind == ProjectNameKind.Plain && !File.Exists(path))
                {
                    return null;
                }

                return new ProjectLanguageFinding(
                    info.Kind switch
                    {
                        ProjectNameKind.MoreThanOneDot => LanguageNameKind.MoreThanOneDot,
                        ProjectNameKind.LooksLikeVariant => LanguageNameKind.LooksLikeVariant,
                        _ => LanguageNameKind.Plain
                    },
                    info.Variants,
                    info.ReservedWord);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is NotSupportedException)
            {
                // A folder that cannot be read says nothing; the project check reports what is wrong with the path.
                return null;
            }
        }
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

    /// <summary>What <see cref="FolderPathValidator"/> found out about a folder path.</summary>
    internal enum FolderCheck
    {
        /// <summary>Nothing was entered: the setting is simply not set, which is allowed.</summary>
        None,

        /// <summary>A full path to a folder that exists.</summary>
        Valid,

        /// <summary>A relative path.</summary>
        NotFullyQualified,

        /// <summary>The text holds a character no path may hold.</summary>
        Unusable,

        /// <summary>The path names a file, not a folder.</summary>
        NotAFolder,

        /// <summary>No such folder.</summary>
        NotFound
    }

    /// <summary>
    /// The folder check of the settings, as a seam. It touches the disk, which on a network path can block for a long time,
    /// so the view model calls it on a worker thread.
    /// </summary>
    internal interface IFolderChecker
    {
        /// <summary>Checks a folder path.</summary>
        /// <param name="path">What the user typed or picked; blank means not set.</param>
        /// <returns>The verdict.</returns>
        FolderCheck CheckFolder(string? path);
    }

    /// <summary>The real check, against the file system.</summary>
    internal sealed class FileSystemFolderChecker : IFolderChecker
    {
        /// <inheritdoc/>
        public FolderCheck CheckFolder(string? path) => FolderPathValidator.Check(path);
    }

    /// <summary>
    /// Checks a folder that a setting names, such as the folder reports are written to. Only what is certain is rejected;
    /// whether the account may write there shows when a report is written.
    /// </summary>
    internal static class FolderPathValidator
    {
        // Not all of these are in Path.GetInvalidPathChars(), but none can be part of a folder path. The question mark is
        // refused everywhere, also in an extended-length prefix such as \\?\: the folder becomes the front of the report
        // path, and the host replaces every question mark of a report path with a number. The support library refuses
        // it the same way (ReportFiles), so a folder this check let through would be rejected when the first report is
        // prepared.
        private static readonly char[] NotInAPath = { '*', '?', '<', '>', '|', '"' };

        /// <summary>Checks one value.</summary>
        /// <param name="path">What the user typed or picked.</param>
        /// <returns>The verdict.</returns>
        public static FolderCheck Check(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return FolderCheck.None;
            }

            var trimmed = path.Trim();

            if (trimmed.IndexOfAny(Path.GetInvalidPathChars()) >= 0 || trimmed.IndexOfAny(NotInAPath) >= 0)
            {
                return FolderCheck.Unusable;
            }

            if (!Path.IsPathFullyQualified(trimmed))
            {
                return FolderCheck.NotFullyQualified;
            }

            if (File.Exists(trimmed))
            {
                return FolderCheck.NotAFolder;
            }

            return Directory.Exists(trimmed) ? FolderCheck.Valid : FolderCheck.NotFound;
        }
    }
}
