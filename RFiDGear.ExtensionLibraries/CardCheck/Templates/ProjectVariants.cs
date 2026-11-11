using System;
using System.Collections.Generic;
using System.IO;

namespace RFiDGear.ExtensionLibraries.CardCheck
{
    /// <summary>What the name of a default project says about language variants.</summary>
    public enum ProjectNameKind
    {
        /// <summary>One dot, before the extension: <c>name.rfPrj</c>. Variants may be next to it.</summary>
        Plain,

        /// <summary>More than one dot, such as <c>my.project.rfPrj</c>. The file is used as it is; no variant is looked for.</summary>
        MoreThanOneDot,

        /// <summary>The name ends in the word of a variant, such as <c>name.deutsch.rfPrj</c>: a reserved name. Used as it is.</summary>
        LooksLikeVariant
    }

    /// <summary>What a default project and the folder it is in say about language variants.</summary>
    /// <param name="Kind">What the name of the project says.</param>
    /// <param name="Variants">The words of the variants found next to a plain project, sorted; empty for any other kind.</param>
    /// <param name="ReservedWord">For <see cref="ProjectNameKind.LooksLikeVariant"/> the word the name ends in, otherwise <c>null</c>.</param>
    public sealed record ProjectLanguageInfo(ProjectNameKind Kind, System.Collections.Generic.IReadOnlyList<string> Variants, string? ReservedWord);

    /// <summary>
    /// Chooses the project file for a check. A default project <c>name.rfPrj</c> may have language variants next
    /// to it, <c>name.deutsch.rfPrj</c> and <c>name.englisch.rfPrj</c>; the one for the chosen language is used if
    /// it exists. A project whose name holds more than one dot is used as it is: no variant is looked for.
    /// </summary>
    public static class ProjectVariants
    {
        /// <summary>The word that marks the variant file of a language.</summary>
        /// <param name="language">The language of the check.</param>
        /// <returns><c>deutsch</c> or <c>englisch</c>.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="language"/> is not a defined language.</exception>
        public static string VariantWord(ReportLanguage language) => language switch
        {
            ReportLanguage.German => "deutsch",
            ReportLanguage.English => "englisch",
            _ => throw new ArgumentOutOfRangeException(nameof(language), language, "Unknown report language."),
        };

        /// <summary>
        /// The project file to run: the language variant next to the default project if that file exists, otherwise
        /// the default project itself. The default project is not checked; the host reports a missing project when
        /// it loads it.
        /// </summary>
        /// <param name="defaultProjectFile">Path of the default project, for example <c>D:\p\check.rfPrj</c>.</param>
        /// <param name="language">The language of the check.</param>
        /// <param name="fileExists">Tells whether a file exists; passed in so the choice can be tested.</param>
        /// <returns>The path of the variant, or <paramref name="defaultProjectFile"/>.</returns>
        /// <exception cref="ArgumentException"><paramref name="defaultProjectFile"/> is null, empty or whitespace.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="fileExists"/> is null.</exception>
        /// <remarks>A name with more than one dot (<c>my.project.rfPrj</c>) is returned as it is, without asking <paramref name="fileExists"/>.</remarks>
        public static string Resolve(string defaultProjectFile, ReportLanguage language, Func<string, bool> fileExists)
        {
            if (string.IsNullOrWhiteSpace(defaultProjectFile))
            {
                throw new ArgumentException("A project file is needed.", nameof(defaultProjectFile));
            }

            if (fileExists == null)
            {
                throw new ArgumentNullException(nameof(fileExists));
            }

            var directory = Path.GetDirectoryName(defaultProjectFile) ?? string.Empty;
            var name = Path.GetFileNameWithoutExtension(defaultProjectFile);
            var extension = Path.GetExtension(defaultProjectFile);
            if (name.Contains('.'))
            {
                return defaultProjectFile;
            }

            var variant = Path.Combine(directory, $"{name}.{VariantWord(language)}{extension}");

            return fileExists(variant) ? variant : defaultProjectFile;
        }

        /// <summary>
        /// Says what the name of a default project means for the language lookup, and which variants lie next to it.
        /// </summary>
        /// <param name="defaultProjectFile">Path of the default project.</param>
        /// <param name="filesInFolder">Lists the files of a folder (full paths); passed in so it can be tested and run off the UI thread.</param>
        /// <returns>The finding; a name that is not plain has no variants.</returns>
        /// <exception cref="ArgumentException"><paramref name="defaultProjectFile"/> is null, empty or whitespace.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="filesInFolder"/> is null.</exception>
        public static ProjectLanguageInfo Inspect(string defaultProjectFile, Func<string, IEnumerable<string>> filesInFolder)
        {
            if (string.IsNullOrWhiteSpace(defaultProjectFile))
            {
                throw new ArgumentException("A project file is needed.", nameof(defaultProjectFile));
            }

            if (filesInFolder == null)
            {
                throw new ArgumentNullException(nameof(filesInFolder));
            }

            var name = Path.GetFileNameWithoutExtension(defaultProjectFile);
            var extension = Path.GetExtension(defaultProjectFile);
            var none = Array.Empty<string>();

            var lastDot = name.LastIndexOf('.');
            if (lastDot >= 0)
            {
                var word = name.Substring(lastDot + 1);
                var reserved = Array.Find(KnownWords, known => string.Equals(known, word, StringComparison.OrdinalIgnoreCase));

                return new ProjectLanguageInfo(reserved == null ? ProjectNameKind.MoreThanOneDot : ProjectNameKind.LooksLikeVariant, none, reserved);
            }

            var directory = Path.GetDirectoryName(defaultProjectFile) ?? string.Empty;
            var prefix = name + ".";
            var words = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in filesInFolder(directory))
            {
                var fileName = Path.GetFileName(file);
                if (!string.Equals(Path.GetExtension(fileName), extension, StringComparison.OrdinalIgnoreCase)
                    || !fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var withoutExtension = Path.GetFileNameWithoutExtension(fileName);
                if (withoutExtension.Length <= prefix.Length)
                {
                    continue;
                }

                var middle = withoutExtension.Substring(prefix.Length);
                if (!middle.Contains('.'))
                {
                    words.Add(middle);
                }
            }

            return new ProjectLanguageInfo(ProjectNameKind.Plain, new List<string>(words), null);
        }

        private static readonly string[] KnownWords = { VariantWord(ReportLanguage.German), VariantWord(ReportLanguage.English) };
    }
}
