#nullable enable
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;

using Serilog;

namespace RFiDGear.Infrastructure
{
    /// <summary>
    /// The compiled help: one CHM per language, next to <c>RFiDGear.exe</c>. This class says which file belongs to a language, opens it,
    /// and recognises its window, so that F1 does not open a second one.
    /// </summary>
    internal static class HelpFile
    {
        /// <summary>The name of the German help. It is built from <c>docs/chm_de</c> (<c>docs/Build-Help.ps1</c>) and copied by <c>RFiDGear.csproj</c>.</summary>
        internal const string GermanFileName = "RFiDGear_de.chm";

        /// <summary>The name of the English help. It is built from <c>docs/chm_en</c> (<c>docs/Build-Help.ps1</c>) and copied by <c>RFiDGear.csproj</c>.</summary>
        internal const string EnglishFileName = "RFiDGear_en.chm";

        /// <summary>The title of the window of the German help: the <c>Title=</c> of its <c>.hhp</c>.</summary>
        internal const string GermanWindowTitle = "RFiDGear Hilfe";

        /// <summary>The title of the window of the English help: the <c>Title=</c> of its <c>.hhp</c>.</summary>
        internal const string EnglishWindowTitle = "RFiDGear Help";

        /// <summary>Gets the name of the help file for a language of the settings.</summary>
        /// <param name="language">The value of <c>DefaultLanguage</c>: <c>german</c> or <c>english</c>, in any letter case. Anything else is English, as for the texts of the program.</param>
        /// <returns>The file name, without a folder.</returns>
        internal static string FileNameFor(string? language) =>
            string.Equals(language, "german", StringComparison.OrdinalIgnoreCase) ? GermanFileName : EnglishFileName;

        /// <summary>Gets the full path of the help file for a language.</summary>
        /// <param name="language">The value of <c>DefaultLanguage</c>.</param>
        /// <param name="baseDirectory">The folder of the program.</param>
        /// <returns>The path; the file may not exist.</returns>
        internal static string PathFor(string? language, string baseDirectory) => Path.Combine(baseDirectory, FileNameFor(language));

        /// <summary>
        /// Makes the start information for a help file. The shell has to start it (<c>hh.exe</c> is what <c>.chm</c> is associated with):
        /// a process started without the shell cannot be a document.
        /// </summary>
        /// <param name="path">The full path of the help file.</param>
        /// <returns>The start information.</returns>
        internal static ProcessStartInfo StartInfoFor(string path) => new ProcessStartInfo(path) { UseShellExecute = true };

        /// <summary>Tells whether the title of a window is the title of one of the help windows.</summary>
        /// <param name="title">The title of a window.</param>
        /// <returns><see langword="true"/> when it contains the title that the German or the English help sets.</returns>
        internal static bool IsHelpWindowTitle(string? title) =>
            !string.IsNullOrEmpty(title)
            && (title.Contains(GermanWindowTitle, StringComparison.Ordinal) || title.Contains(EnglishWindowTitle, StringComparison.Ordinal));

        /// <summary>Tells whether a help window is open already (a window of <c>hh.exe</c> with the title of the help).</summary>
        /// <returns><see langword="true"/> when one is.</returns>
        internal static bool IsOpen()
        {
            foreach (var process in Process.GetProcessesByName("hh"))
            {
                try
                {
                    if (IsHelpWindowTitle(process.MainWindowTitle))
                    {
                        return true;
                    }
                }
                catch (Exception failure) when (failure is InvalidOperationException || failure is Win32Exception)
                {
                    // The process ended while it was looked at: it is no help window then.
                }
                finally
                {
                    process.Dispose();
                }
            }

            return false;
        }

        /// <summary>Opens a help file.</summary>
        /// <param name="path">The full path of the help file.</param>
        /// <returns><see langword="true"/> when it was started; <see langword="false"/> when the file does not exist or could not be started (the reason is logged).</returns>
        internal static bool TryOpen(string path)
        {
            if (!File.Exists(path))
            {
                Log.Warning("The help file {Path} does not exist", path);

                return false;
            }

            try
            {
                using (Process.Start(StartInfoFor(path)))
                {
                    return true;
                }
            }
            catch (Exception failure) when (failure is Win32Exception || failure is InvalidOperationException || failure is FileNotFoundException)
            {
                Log.Warning(failure, "The help file {Path} could not be opened", path);

                return false;
            }
        }
    }
}
