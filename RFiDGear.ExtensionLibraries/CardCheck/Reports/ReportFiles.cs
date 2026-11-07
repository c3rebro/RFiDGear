using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RFiDGear.ExtensionLibraries.CardCheck
{
    /// <summary>The file-system implementation of <see cref="IReportFiles"/>.</summary>
    internal sealed class ReportFiles : IReportFiles
    {
        /// <summary>The longest full path accepted; longer ones fail on a default Windows setup.</summary>
        internal const int MaxPathLength = 259;

        /// <summary>What joins the job number and the chip number in a file name.</summary>
        internal const string JobChipSeparator = "-";

        private readonly ICardCheckLog _log;

        /// <summary>Creates the service.</summary>
        /// <param name="log">Where a file that could not be removed is noted (by kind, never by path).</param>
        public ReportFiles(ICardCheckLog log)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        /// <inheritdoc />
        public Result<OrderReportPaths> Resolve(ReportLocation location, string jobNumber, string chipNumber)
        {
            if (location == null)
            {
                throw new ArgumentNullException(nameof(location));
            }

            var root = NormalizeRoot(location.OutputRoot);

            if (!root.IsSuccess)
            {
                return Result<OrderReportPaths>.Fail(root.Failure!);
            }

            var job = ReportNames.ToSegment(jobNumber, "job");

            if (!job.IsSuccess)
            {
                return Result<OrderReportPaths>.Fail(job.Failure!);
            }

            // The chip number may not contain the hyphen that joins it to the job number: with it, job "A-B" and chip "C"
            // would name the same files as job "A" and chip "B-C". Without it the stem splits at its last hyphen only one way.
            var chip = ReportNames.ToSegment(chipNumber, "chip", alsoNotAllowed: JobChipSeparator);

            if (!chip.IsSuccess)
            {
                return Result<OrderReportPaths>.Fail(chip.Failure!);
            }

            var folder = location.CreateSubdirectory ? Path.Combine(root.Value, job.Value) : root.Value;
            var stem = Path.Combine(folder, job.Value + JobChipSeparator + chip.Value);
            var paths = new OrderReportPaths(
                root.Value,
                folder,
                stem + ".step1.pdf",
                stem + ".step2.pdf",
                stem + ".step3.pdf",
                stem + "_final.pdf",
                stem + ".download.pdf",
                stem + ".edit.pdf");

            foreach (ReportFile file in Enum.GetValues(typeof(ReportFile)))
            {
                if (paths.PathOf(file).Length > MaxPathLength)
                {
                    return Result<OrderReportPaths>.Fail(new Failure(FailureKind.InvalidInput, "report.path.tooLong", "The report path is too long."));
                }
            }

            return Result<OrderReportPaths>.Ok(paths);
        }

        /// <inheritdoc />
        public Result EnsureOrderFolder(OrderReportPaths paths)
        {
            if (paths == null)
            {
                throw new ArgumentNullException(nameof(paths));
            }

            try
            {
                if (!Directory.Exists(paths.Root))
                {
                    return Result.Fail(new Failure(FailureKind.NotFound, "report.root.missing", "The report folder does not exist."));
                }

                if (!string.Equals(paths.Folder, paths.Root, StringComparison.Ordinal))
                {
                    Directory.CreateDirectory(paths.Folder);
                }

                return Result.Ok();
            }
            catch (UnauthorizedAccessException)
            {
                return Result.Fail(new Failure(FailureKind.AccessDenied, "report.folder.denied", "The order's report folder cannot be created: access denied."));
            }
            catch (Exception ex) when (ex is IOException || ex is NotSupportedException || ex is ArgumentException)
            {
                return Result.Fail(new Failure(FailureKind.Io, "report.folder.io", "The order's report folder cannot be created."));
            }
        }

        /// <inheritdoc />
        public Result PrepareStep(OrderReportPaths paths, ReportFile file)
        {
            if (paths == null)
            {
                throw new ArgumentNullException(nameof(paths));
            }

            if (file != ReportFile.Step1 && file != ReportFile.Step2 && file != ReportFile.Step3)
            {
                return Result.Fail(new Failure(FailureKind.InvalidInput, "report.file.unsupported", "Only step reports can be prepared."));
            }

            var path = paths.PathOf(file);

            if (!IsInOrderFolder(paths, path))
            {
                return Result.Fail(new Failure(FailureKind.InvalidInput, "report.path.outside", "A report path is not in the order's report folder."));
            }

            try
            {
                DeleteIfPresent(path);

                return Result.Ok();
            }
            catch (UnauthorizedAccessException)
            {
                return Result.Fail(new Failure(FailureKind.AccessDenied, "report.stale.denied", "An earlier report cannot be removed: access denied."));
            }
            catch (IOException)
            {
                return Result.Fail(new Failure(FailureKind.Io, "report.stale.io", "An earlier report cannot be removed; it may be open in another program."));
            }
        }

        /// <inheritdoc />
        public CleanupReport Cleanup(OrderReportPaths paths, CleanupScope scope, bool removeTemporaryReports)
        {
            if (paths == null)
            {
                throw new ArgumentNullException(nameof(paths));
            }

            var wanted = FilesToRemove(scope, removeTemporaryReports);

            if (wanted.Length == 0)
            {
                return CleanupReport.Nothing;
            }

            var removed = new List<ReportFile>();
            var failed = new List<ReportFile>();

            foreach (var file in wanted)
            {
                var path = paths.PathOf(file);

                if (!IsInOrderFolder(paths, path))
                {
                    failed.Add(file);
                    _log.Warning("A report path was not in the order's folder: {ReportFile}", file);

                    continue;
                }

                try
                {
                    if (DeleteIfPresent(path))
                    {
                        removed.Add(file);
                    }
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is NotSupportedException)
                {
                    failed.Add(file);
                    _log.Warning("A working report could not be removed: {ReportFile}", file);
                }
            }

            return new CleanupReport(removed, failed);
        }

        private static ReportFile[] FilesToRemove(CleanupScope scope, bool removeTemporaryReports)
        {
            switch (scope)
            {
                case CleanupScope.NotSupported:
                    return removeTemporaryReports ? new[] { ReportFile.Step1, ReportFile.Step2 } : Array.Empty<ReportFile>();
                case CleanupScope.SupportedOnly:
                    return Array.Empty<ReportFile>();
                case CleanupScope.Programmed:
                    return removeTemporaryReports ? new[] { ReportFile.Step1, ReportFile.Step2, ReportFile.Step3 } : Array.Empty<ReportFile>();
                case CleanupScope.ReEdit:
                    return new[] { ReportFile.Download, ReportFile.Edit };
                default:
                    throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unknown cleanup scope.");
            }
        }

        // The paths come from Resolve: files directly in the order's folder, which is the root or a folder directly in
        // it. The record can also be built or copied by hand, and PrepareStep and Cleanup delete what it names, so a path
        // is checked against the folder first. A path that cannot even be made absolute is outside.
        private static bool IsInOrderFolder(OrderReportPaths paths, string path)
        {
            try
            {
                var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(paths.Root));
                var folder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(paths.Folder));
                var parent = Path.GetDirectoryName(Path.GetFullPath(path));

                var folderIsInRoot = string.Equals(folder, root, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(Path.GetDirectoryName(folder), root, StringComparison.OrdinalIgnoreCase);

                return folderIsInRoot && string.Equals(parent, folder, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is IOException)
            {
                return false;
            }
        }

        // File.Exists would say "no" for a folder in the way and for a path it is not allowed to look at, and the caller
        // would take that for "nothing to delete". Only "not found" means absent; everything else reaches the caller.
        private static bool DeleteIfPresent(string path)
        {
            FileAttributes attributes;

            try
            {
                attributes = File.GetAttributes(path);
            }
            catch (Exception ex) when (ex is FileNotFoundException || ex is DirectoryNotFoundException)
            {
                return false;
            }

            if (attributes.HasFlag(FileAttributes.Directory))
            {
                throw new IOException("A folder is where the report should be.");
            }

            // A report someone marked read-only would otherwise refuse to be deleted.
            File.SetAttributes(path, FileAttributes.Normal);
            File.Delete(path);

            return true;
        }

        private static Result<string> NormalizeRoot(string? outputRoot)
        {
            var trimmed = outputRoot?.Trim();

            if (string.IsNullOrEmpty(trimmed))
            {
                return Result<string>.Fail(new Failure(FailureKind.NotConfigured, "report.root.notSet", "No report folder is set."));
            }

            if (trimmed.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            {
                return Result<string>.Fail(new Failure(FailureKind.InvalidConfiguration, "report.root.invalid", "The report folder is not a valid path."));
            }

            if (!Path.IsPathFullyQualified(trimmed))
            {
                return Result<string>.Fail(new Failure(FailureKind.InvalidConfiguration, "report.root.notAbsolute", "The report folder must be a full path."));
            }

            try
            {
                var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(trimmed));

                return HasUnusableFolderName(full)
                    ? Result<string>.Fail(new Failure(FailureKind.InvalidConfiguration, "report.root.invalid", "The report folder is not a valid path."))
                    : Result<string>.Ok(full);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException)
            {
                return Result<string>.Fail(new Failure(FailureKind.InvalidConfiguration, "report.root.invalid", "The report folder is not a valid path."));
            }
        }

        // .NET lets a path with a quote, a wildcard, an angle bracket or a bar through; Windows cannot create such a
        // folder, and a question mark is the host's numbering placeholder. None of them belongs anywhere in the path,
        // not even in the drive, the share or an extended-length prefix such as \\?\ in front. Inside the folder names
        // the colon and the control characters are refused too.
        private static bool HasUnusableFolderName(string fullPath)
        {
            if (fullPath.IndexOfAny(new[] { '?', '*', '<', '>', '"', '|' }) >= 0)
            {
                return true;
            }

            var folders = fullPath.Substring(Path.GetPathRoot(fullPath)?.Length ?? 0);
            var separators = new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };

            return folders
                .Split(separators)
                .Any(folder => folder.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0);
        }
    }
}
