using System;
using System.IO;

namespace RFiDGear.ExtensionLibraries.CardCheck
{
    /// <summary>
    /// Chooses the project file for a check. A default project <c>name.rfPrj</c> may have language variants next
    /// to it, <c>name.deutsch.rfPrj</c> and <c>name.englisch.rfPrj</c>; the one for the chosen language is used if
    /// it exists.
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
            var variant = Path.Combine(directory, $"{name}.{VariantWord(language)}{extension}");

            return fileExists(variant) ? variant : defaultProjectFile;
        }
    }
}
