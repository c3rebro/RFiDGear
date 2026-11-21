#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace RFiDGear.Infrastructure.Tasks
{
    /// <summary>
    /// What the text of a program task may name in place of a value: the values of the card of the run, read when the program
    /// is started. A value that is not known (no card, no DESFire chip) is an empty text, never a failure.
    /// </summary>
    internal sealed class ProgramTaskValues
    {
        /// <summary>The UID of the card (<c>%UID</c>).</summary>
        public string Uid { get; init; } = string.Empty;

        /// <summary>The name of the chip type as the user sees it (<c>%CHIPTYPE</c>).</summary>
        public string ChipType { get; init; } = string.Empty;

        /// <summary>The time the program is started at (<c>%DATETIME</c> and <c>%DATE</c>).</summary>
        public DateTime Now { get; init; } = DateTime.Now;

        /// <summary>The free memory of a DESFire card (<c>%FREEMEM</c>).</summary>
        public string FreeMemory { get; init; } = string.Empty;

        /// <summary>The application ids of a DESFire card, already joined (<c>%LISTAPPS</c>).</summary>
        public string ApplicationIds { get; init; } = string.Empty;

        /// <summary>The number of applications of a DESFire card (<c>%COUNTAPPS</c>).</summary>
        public string ApplicationCount { get; init; } = string.Empty;

        /// <summary>
        /// The variables of the run (start-up arguments, <c>startRun</c>, an extension). Only a name with a dollar sign in it counts,
        /// anywhere in it (<c>$JOB</c>, <c>JOB$ID</c>), as the start-up arguments accept it. A name is found in the text without
        /// regard to case, as the variables of a run are matched everywhere else (<c>$job</c> fills <c>$JOB</c>).
        /// </summary>
        public IReadOnlyDictionary<string, string>? Variables { get; init; }
    }

    /// <summary>The program a program task starts, ready for <see cref="System.Diagnostics.ProcessStartInfo"/>.</summary>
    /// <param name="FileName">What to start, with every placeholder replaced.</param>
    /// <param name="Arguments">Its arguments, with every placeholder replaced.</param>
    /// <param name="UseShellExecute">Whether the shell starts it (a document) or the process is created directly.</param>
    internal sealed record ProgramStart(string FileName, string Arguments, bool UseShellExecute);

    /// <summary>
    /// Turns the text of a program task, <c>"C:\Tools\a.exe" -uid %UID -job $JOB</c>, into the program to start. The file name is
    /// the text between the first two quotation marks; everything after the second one is the arguments, as typed. Both are
    /// searched for placeholders: <c>%UID</c>, <c>%CHIPTYPE</c>, <c>%DATETIME</c>, <c>%DATE</c>, <c>%FREEMEM</c>,
    /// <c>%LISTAPPS</c>, <c>%COUNTAPPS</c> and the variables <c>$NAME</c> of the run.
    /// <para>
    /// The text of the task is never changed: the placeholders are replaced in copies, so the next run replaces them again, with
    /// the values of its own card.
    /// </para>
    /// </summary>
    internal static class ProgramCommandLine
    {
        /// <summary>Works out what to start.</summary>
        /// <param name="text">The text of the task.</param>
        /// <param name="values">The values the placeholders stand for.</param>
        /// <returns>The program to start, or <see langword="null"/> when the text has no file name in quotation marks.</returns>
        internal static ProgramStart? Build(string? text, ProgramTaskValues values)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var open = text.IndexOf('"');
            var close = open < 0 ? -1 : text.IndexOf('"', open + 1);

            if (close < 0)
            {
                return null;
            }

            var fileName = text.Substring(open + 1, close - open - 1);

            if (string.IsNullOrWhiteSpace(fileName))
            {
                return null;
            }

            fileName = Expand(fileName, values);
            var arguments = Expand(text.Substring(close + 1).Trim(), values);

            // A program is created directly; whatever else (a document, a script of another kind) is opened by the shell.
            var direct = fileName.Contains("bat") || fileName.Contains("exe") || fileName.Contains("msi");

            return new ProgramStart(fileName, arguments, !direct);
        }

        /// <summary>
        /// Replaces the placeholders of a text. The built-in ones come first, so what a variable holds is never replaced again;
        /// <c>%DATETIME</c> comes before <c>%DATE</c>, which it starts with, and the longer variable name before the shorter.
        /// </summary>
        /// <param name="text">The text to search.</param>
        /// <param name="values">The values the placeholders stand for.</param>
        /// <returns>The text with the placeholders replaced.</returns>
        internal static string Expand(string text, ProgramTaskValues values)
        {
            var result = text
                .Replace("%UID", values.Uid, StringComparison.Ordinal)
                .Replace("%CHIPTYPE", values.ChipType, StringComparison.Ordinal)
                .Replace("%DATETIME", values.Now.ToString(CultureInfo.CurrentCulture), StringComparison.Ordinal)
                .Replace("%DATE", values.Now.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture), StringComparison.Ordinal)
                .Replace("%FREEMEM", values.FreeMemory, StringComparison.Ordinal)
                .Replace("%LISTAPPS", values.ApplicationIds, StringComparison.Ordinal)
                .Replace("%COUNTAPPS", values.ApplicationCount, StringComparison.Ordinal);

            if (values.Variables == null)
            {
                return result;
            }

            // Walked once, longest name first, so that $JOB does not eat the start of $JOBNUMBER. A value is put in as it is:
            // text that happens to look like another name is left alone because every name is taken from the original text.
            var names = values.Variables.Keys
                .Where(name => name.Contains('$'))
                .OrderByDescending(name => name.Length)
                .ThenBy(name => name, StringComparer.Ordinal)
                .ToArray();

            return names.Length == 0 ? result : ReplaceNames(result, names, values.Variables);
        }

        private static string ReplaceNames(string text, string[] names, IReadOnlyDictionary<string, string> variables)
        {
            var builder = new StringBuilder(text.Length);
            var position = 0;

            while (position < text.Length)
            {
                string? hit = null;

                // A name may start anywhere (JOB$ID): every position is tried, the longest name first. The case does not matter,
                // as everywhere else a variable of a run is looked up; the value is that of the name as the dictionary holds it.
                // A dictionary that tells two spellings apart ($job and $JOB) keeps both values: the very spelling wins over another case.
                foreach (var name in names)
                {
                    if (hit != null && name.Length < hit.Length)
                    {
                        break;
                    }

                    if (string.Compare(text, position, name, 0, name.Length, StringComparison.OrdinalIgnoreCase) != 0)
                    {
                        continue;
                    }

                    hit ??= name;

                    if (string.CompareOrdinal(text, position, name, 0, name.Length) == 0)
                    {
                        hit = name;
                        break;
                    }
                }

                if (hit == null)
                {
                    builder.Append(text[position]);
                    position++;
                }
                else
                {
                    builder.Append(variables[hit]);
                    position += hit.Length;
                }
            }

            return builder.ToString();
        }
    }
}
