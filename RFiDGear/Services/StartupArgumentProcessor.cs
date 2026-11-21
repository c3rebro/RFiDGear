using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using RFiDGear;
using RFiDGear.Infrastructure.FileAccess;
using RFiDGear.Services.Interfaces;

namespace RFiDGear.Services
{
    public class StartupArgumentProcessor : IStartupArgumentProcessor
    {
        public StartupArgumentResult Process(string[] args)
        {
            var result = new StartupArgumentResult();

            if (args == null || args.Length == 0)
            {
                return result;
            }

            foreach (var arg in args)
            {
                if (TryGetProjectFilePathFromArg(arg, out var projectFilePath))
                {
                    result.ProjectFilePath = projectFilePath;
                    continue;
                }

                var splitArg = arg.Split('=');
                if (splitArg.Length < 2)
                {
                    continue;
                }

                var key = splitArg[0];
                var value = string.Join("=", splitArg.Skip(1));
                var normalizedKey = key.ToUpperInvariant();

                switch (normalizedKey)
                {
                    case "REPORTTARGETPATH":
                    case "REPORTOUTPUTPATH":
                        result.Variables[key] = value;
                        result.ReportOutputPath = NormalizeReportTargetPath(value);
                        result.ReportOutputPattern = value;
                        break;

                    case "REPORTTEMPLATEFILE":
                    case "REPORTTEMPLATEPATH":
                        result.Variables[key] = value;
                        if (File.Exists(value))
                        {
                            result.ReportTemplateFile = value;
                        }
                        break;

                    case "AUTORUN":
                        result.Autorun = string.Equals(value, "1", StringComparison.Ordinal);
                        break;

                    case "AUTOMODE":
                        result.AutoMode = string.Equals(value, "1", StringComparison.Ordinal);
                        break;

                    case "EXTERNALCONTROL":
                        result.ExternalControl = string.Equals(value, "1", StringComparison.Ordinal);
                        break;

                    case "LASTUSEDPROJECTPATH":
                        PersistLastProjectPath(value);
                        break;

                    case "CUSTOMPROJECTFILE":
                        if (File.Exists(value))
                        {
                            result.ProjectFilePath = new DirectoryInfo(value).FullName;
                        }
                        break;

                    default:
                        if (key.Contains('$'))
                        {
                            result.Variables[key] = value;
                        }
                        break;
                }
            }

            return result;
        }

        /// <summary>
        /// Says whether the arguments switch the external control bridge on (<c>EXTERNALCONTROL=1</c>). The pipe has to open
        /// as soon as the host exists, before the arguments are processed; <see cref="Process"/> has effects (it takes the next
        /// free number of a report file, it writes a setting) that must happen once, so this reads that one argument and
        /// nothing else. It reads it the way <see cref="Process"/> does: the key without regard to case, the value exactly
        /// <c>1</c>, the last one wins.
        /// </summary>
        /// <param name="args">The command line arguments.</param>
        /// <returns><see langword="true"/> when the bridge is requested.</returns>
        public static bool IsExternalControlRequested(string[] args)
        {
            var requested = false;

            foreach (var arg in args ?? Array.Empty<string>())
            {
                var splitArg = (arg ?? string.Empty).Split('=');

                if (splitArg.Length >= 2 && string.Equals(splitArg[0], "EXTERNALCONTROL", StringComparison.OrdinalIgnoreCase))
                {
                    requested = string.Equals(string.Join("=", splitArg.Skip(1)), "1", StringComparison.Ordinal);
                }
            }

            return requested;
        }

        private static bool TryGetProjectFilePathFromArg(string arg, out string projectFilePath)
        {
            projectFilePath = null;

            if (string.IsNullOrWhiteSpace(arg))
            {
                return false;
            }

            if (!arg.EndsWith(".rfprj", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!File.Exists(arg))
            {
                return false;
            }

            projectFilePath = new FileInfo(arg).FullName;
            return true;
        }

        /// <summary>
        /// Resolves a <c>REPORTTARGETPATH</c> value: verifies that its directory exists and replaces a
        /// <c>?</c>, <c>??</c>, or <c>???</c> placeholder with the next free number in that directory.
        /// Shared with the extension host so both entry points interpret the variable identically.
        /// </summary>
        /// <param name="path">The report target path as supplied.</param>
        /// <returns>The resolved path, or <c>null</c> when the directory does not exist.</returns>
        internal static string NormalizeReportTargetPath(string path)
        {
            if (!FilePathHasExistingDirectory(path))
            {
                return null;
            }

            var directoryPath = Path.GetDirectoryName(path);
            if (directoryPath == null)
            {
                return null;
            }

            var matcher = NumberMatcherOf(path);
            var numbersInFileNames = Directory.GetFiles(directoryPath)
                .Select(file => ExtractNumber(matcher, file))
                .Where(n => n >= 0)
                .ToArray();

            if (!numbersInFileNames.Any())
            {
                numbersInFileNames = new[] { 0 };
            }

            var maxNumber = numbersInFileNames.Max();

            if (path.Contains("???"))
            {
                return path.Replace("???", string.Format("{0:D3}", maxNumber + 1));
            }

            if (path.Contains("??"))
            {
                return path.Replace("??", string.Format("{0:D2}", maxNumber + 1));
            }

            if (path.Contains('?'))
            {
                return path.Replace("?", string.Format("{0:D1}", maxNumber + 1));
            }

            return path;
        }

        private static bool FilePathHasExistingDirectory(string path)
        {
            var directory = Path.GetDirectoryName(path);
            return !string.IsNullOrEmpty(directory) && Directory.Exists(directory);
        }

        // What a file name of the pattern looks like. The placeholder that is replaced is the longest kind that the pattern holds
        // (??? before ??, ?), as NormalizeReportTargetPath replaces it, and every place of it gets the same number: card-???-result.pdf is
        // matched by card-007-result.pdf, ??-??.pdf by 07-07.pdf. The text around the places has to match too. ".pdf" at the end is left out,
        // as it always was. Null when the pattern holds no question mark.
        private static Regex NumberMatcherOf(string pattern)
        {
            var name = WithoutPdf(Path.GetFileName(pattern));
            var kind = name.Contains("???") ? "???" : name.Contains("??") ? "??" : "?";

            var texts = name.Split(kind);
            if (texts.Length < 2)
            {
                return null;
            }

            var expression = new StringBuilder("^").Append(Regex.Escape(texts[0])).Append("(?<n>[0-9]+)");
            for (var i = 1; i < texts.Length; i++)
            {
                expression.Append(Regex.Escape(texts[i]));
                if (i < texts.Length - 1)
                {
                    expression.Append("\\k<n>");
                }
            }

            return new Regex(
                expression.Append('$').ToString(),
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                TimeSpan.FromSeconds(1));
        }

        // The number that a file name holds in the place of the question marks, or -1 when the file is not one of the pattern.
        private static int ExtractNumber(Regex matcher, string fileName)
        {
            if (matcher == null)
            {
                return -1;
            }

            var match = matcher.Match(WithoutPdf(Path.GetFileName(fileName)));

            return match.Success && int.TryParse(match.Groups["n"].Value, out var n) ? n : -1;
        }

        private static string WithoutPdf(string name) =>
            name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? name.Substring(0, name.Length - ".pdf".Length) : name;

        private static void PersistLastProjectPath(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return;
            }

            using (var settings = new SettingsReaderWriter())
            {
                settings.DefaultSpecification.LastUsedProjectPath = new DirectoryInfo(filePath).FullName;
                settings.SaveSettings().GetAwaiter().GetResult();
            }
        }
    }
}
