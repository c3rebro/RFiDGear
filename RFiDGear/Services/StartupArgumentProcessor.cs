using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

            var numbersInFileNames = Directory.GetFiles(directoryPath)
                .Select(file => ExtractNumber(path, file))
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

        // The number that a file name holds in the place of the question marks of the pattern, or -1 when the file is not one of the pattern.
        // What stands before the question marks and what stands after them (card-???-result.pdf) both have to match; ".pdf" at the end of
        // either is left out, as it always was.
        private static int ExtractNumber(string pattern, string fileName)
        {
            var patternName = WithoutPdf(Path.GetFileName(pattern));
            var name = WithoutPdf(Path.GetFileName(fileName));

            var start = patternName.IndexOf('?');
            if (start < 0)
            {
                return -1;
            }

            var end = start;
            while (end < patternName.Length && patternName[end] == '?')
            {
                end++;
            }

            var prefix = patternName.Substring(0, start);
            var suffix = patternName.Substring(end);

            if (name.Length <= prefix.Length + suffix.Length
                || !name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                || !name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return -1;
            }

            var digits = name.Substring(prefix.Length, name.Length - prefix.Length - suffix.Length);

            return digits.All(c => c is >= '0' and <= '9') && int.TryParse(digits, out var n) ? n : -1;
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
