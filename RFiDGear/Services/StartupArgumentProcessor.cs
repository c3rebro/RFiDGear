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

            if (args == null || args.Length <= 1)
            {
                return result;
            }

            foreach (var arg in args)
            {
                var splitArg = arg.Split('=');
                if (splitArg.Length < 2)
                {
                    continue;
                }

                var key = splitArg[0];
                var value = string.Join("=", splitArg.Skip(1));

                switch (key)
                {
                    case "REPORTTARGETPATH":
                        result.Variables[key] = value;
                        result.ReportOutputPath = NormalizeReportTargetPath(value);
                        break;

                    case "REPORTTEMPLATEFILE":
                        result.Variables[key] = value;
                        if (File.Exists(value))
                        {
                            result.ReportTemplateFile = value;
                        }
                        break;

                    case "AUTORUN":
                        result.Autorun = string.Equals(value, "1", StringComparison.Ordinal);
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

        private static string NormalizeReportTargetPath(string path)
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

        private static int ExtractNumber(string pattern, string fileName)
        {
            var sanitizedPattern = pattern.ToLowerInvariant()
                .Replace("?", string.Empty)
                .Replace(".pdf", string.Empty);

            var sanitizedFileName = fileName.ToLowerInvariant()
                .Replace(".pdf", string.Empty)
                .Replace(sanitizedPattern, string.Empty);

            return int.TryParse(sanitizedFileName, out var n) ? n : -1;
        }

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
