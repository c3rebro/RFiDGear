using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;

using RFiDGear.ExtensionLibraries.CardCheck;

namespace RFiDGear.Extensions.CardCheckAssistant.Services
{
    /// <summary>The stable codes of the failures of opening a report or a folder, for the page to turn into texts.</summary>
    internal static class OpenerFailureCodes
    {
        /// <summary>The path is empty, not a full path, or cannot be made into one.</summary>
        public const string PathInvalid = "opener.path.invalid";

        /// <summary>There is no file or folder at the path.</summary>
        public const string NotFound = "opener.notFound";

        /// <summary>The path names something that is neither a PDF file nor a folder, or a device.</summary>
        public const string NotAllowed = "opener.notAllowed";

        /// <summary>No program is set up to open PDF files.</summary>
        public const string NoViewer = "opener.noViewer";

        /// <summary>The program could not be started for another reason.</summary>
        public const string StartFailed = "opener.start.failed";
    }

    /// <summary>
    /// Opens a report, or the folder it is in, in the program the operator has set up for it, so that the report can be
    /// looked at (and corrected) before the order is finished. A seam: the page asks for a path, a test sees what was asked.
    /// </summary>
    internal interface IReportOpener
    {
        /// <summary>
        /// Opens a PDF file or a folder. It reads the disk (a path on a network share that does not answer can take long), so
        /// the page calls it off its UI thread.
        /// </summary>
        /// <param name="path">The full path.</param>
        /// <returns>Success, or why nothing was opened. Never throws.</returns>
        Result Open(string path);
    }

    /// <summary>
    /// The one place in the extension that starts a program, and only in the way the shell opens a document: the default
    /// program of an existing PDF file, or the file manager for an existing folder. It never starts a program by name, never
    /// passes arguments, and refuses everything else (other file types, relative paths, devices), so that a path from a
    /// report folder cannot become a command. The guard tests in <c>CardCheckAssistantExtensionTests</c> keep it that way: no
    /// other file of the extension may name <c>Process</c>, the assembly may use only the members of <c>Process</c> that
    /// are used here, and it has no P/Invoke at all.
    /// </summary>
    internal sealed class ShellOpener : IReportOpener
    {
        private readonly Func<ProcessStartInfo, Failure?> _start;

        /// <summary>Creates the opener that really starts the program.</summary>
        public ShellOpener()
            : this(StartWithTheShell)
        {
        }

        /// <summary>Creates an opener with another way to start the program; a test sees what would be started.</summary>
        /// <param name="start">Starts what the information describes; returns why it did not, or <c>null</c>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="start"/> is null.</exception>
        internal ShellOpener(Func<ProcessStartInfo, Failure?> start)
        {
            _start = start ?? throw new ArgumentNullException(nameof(start));
        }

        /// <inheritdoc/>
        public Result Open(string path)
        {
            var allowed = Check(path);
            if (!allowed.IsSuccess)
            {
                return Result.Fail(allowed.Failure!);
            }

            var failure = _start(Describe(allowed.Value));

            return failure == null ? Result.Ok() : Result.Fail(failure);
        }

        /// <summary>Checks that a path may be opened: a full path to an existing PDF file or an existing folder.</summary>
        /// <param name="path">The path.</param>
        /// <returns>The path as the system spells it, or why it may not be opened.</returns>
        internal static Result<string> Check(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
            {
                return Refuse(FailureKind.InvalidInput, OpenerFailureCodes.PathInvalid, "The path is not a full path.");
            }

            string full;
            try
            {
                full = Path.GetFullPath(path);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException)
            {
                return Refuse(FailureKind.InvalidInput, OpenerFailureCodes.PathInvalid, "The path cannot be used.");
            }

            // A device or an extended path opens a device or something the checks below cannot judge, and a colon after
            // the drive names a stream inside a file: neither is a document.
            if (full.StartsWith(@"\\.\", StringComparison.Ordinal) || full.StartsWith(@"\\?\", StringComparison.Ordinal)
                || full.StartsWith(@"\??\", StringComparison.Ordinal) || full.IndexOf(':', 2) >= 0)
            {
                return Refuse(FailureKind.InvalidInput, OpenerFailureCodes.NotAllowed, "This cannot be opened from here.");
            }

            if (Directory.Exists(full))
            {
                return Result<string>.Ok(full);
            }

            if (!File.Exists(full))
            {
                return Refuse(FailureKind.NotFound, OpenerFailureCodes.NotFound, "There is nothing at the path.");
            }

            return string.Equals(Path.GetExtension(full), ".pdf", StringComparison.OrdinalIgnoreCase)
                ? Result<string>.Ok(full)
                : Refuse(FailureKind.InvalidInput, OpenerFailureCodes.NotAllowed, "Only a PDF report or a folder is opened.");
        }

        /// <summary>What is asked of the shell for a path that passed <see cref="Check"/>: the document itself, no program, no arguments.</summary>
        /// <param name="canonicalPath">The path that <see cref="Check"/> returned.</param>
        /// <returns>The start information.</returns>
        internal static ProcessStartInfo Describe(string canonicalPath) =>
            new ProcessStartInfo { FileName = canonicalPath, UseShellExecute = true };

        /// <summary>What a refusal of the shell means, by the error it gives.</summary>
        /// <param name="nativeErrorCode">The Win32 error: 31 and 1155 (no program for the file type), 2 and 3 (not there any more).</param>
        /// <returns>The failure.</returns>
        internal static Failure FailureOf(int nativeErrorCode) => nativeErrorCode switch
        {
            31 or 1155 => new Failure(FailureKind.NotFound, OpenerFailureCodes.NoViewer, "No program is set up to open this."),
            2 or 3 => new Failure(FailureKind.NotFound, OpenerFailureCodes.NotFound, "There is nothing at the path any more."),
            _ => new Failure(FailureKind.Io, OpenerFailureCodes.StartFailed, "The program could not be started."),
        };

        private static Result<string> Refuse(FailureKind kind, string code, string message) =>
            Result<string>.Fail(new Failure(kind, code, message));

        private static Failure? StartWithTheShell(ProcessStartInfo info)
        {
            try
            {
                using var started = Process.Start(info);

                return null;
            }
            catch (Win32Exception ex)
            {
                return FailureOf(ex.NativeErrorCode);
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is IOException || ex is UnauthorizedAccessException)
            {
                return new Failure(FailureKind.Io, OpenerFailureCodes.StartFailed, "The program could not be started.");
            }
        }
    }
}
