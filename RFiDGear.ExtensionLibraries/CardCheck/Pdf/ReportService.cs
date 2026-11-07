using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GemBox.Pdf;
using GemBox.Pdf.Forms;

namespace RFiDGear.ExtensionLibraries.CardCheck
{
    /// <summary>The PDF implementation of <see cref="IReportService"/>.</summary>
    internal sealed class ReportService : IReportService
    {
        // The fields the check project fills in on the first run.
        internal const string SupportedField = "CheckBox_isChipSuppYes";
        internal const string ProgrammableField = "CheckBox_ChipCanUseYes";
        internal const string FreeMemoryField = "TextBox_Detail_FreeMem_1";
        internal const string ChipTypeField = "TextBox_ChipType";
        internal const string NotesField = "TextBox_Notes_Site_2";

        // The value of a ticked check box.
        internal const string Ticked = "Yes";

        private readonly ICardCheckLog _log;

        /// <summary>Creates the service.</summary>
        /// <param name="log">Where an unexpected fault is noted (by type only).</param>
        public ReportService(ICardCheckLog log)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        /// <inheritdoc />
        public Result<ReportAnalysis> Analyze(string reportPath)
        {
            try
            {
                var checkedPath = CheckExists(reportPath);

                if (checkedPath != null)
                {
                    return Result<ReportAnalysis>.Fail(checkedPath);
                }

                SetLicense();
                using var document = PdfDocument.Load(reportPath);
                var fields = document.Form.Fields;

                if (fields.Count == 0)
                {
                    return Result<ReportAnalysis>.Fail(new Failure(FailureKind.Corrupt, "report.noForm", "The file has no form fields, so it is not a report of the check."));
                }

                string Read(string name) => fields[name]?.Value as string ?? string.Empty;

                return Result<ReportAnalysis>.Ok(new ReportAnalysis
                {
                    Supported = Read(SupportedField) == Ticked,
                    Programmable = Read(ProgrammableField) == Ticked,
                    FreeMemoryText = Read(FreeMemoryField),
                    ChipType = Read(ChipTypeField),
                    Notes = Read(NotesField),
                });
            }
            catch (Exception ex)
            {
                return Result<ReportAnalysis>.Fail(Classify(ex, "report.analyze"));
            }
        }

        /// <inheritdoc />
        public Result WriteFields(string sourcePath, string targetPath, IReadOnlyDictionary<string, string> fields)
        {
            if (fields == null)
            {
                throw new ArgumentNullException(nameof(fields));
            }

            return Rewrite(sourcePath, targetPath, "report.write", document =>
            {
                var form = document.Form.Fields;
                var found = new List<(PdfField Field, string Value)>();
                var missing = new List<string>();

                foreach (var pair in fields)
                {
                    var field = form[pair.Key];

                    if (field == null)
                    {
                        missing.Add(pair.Key);
                    }
                    else
                    {
                        found.Add((field, pair.Value));
                    }
                }

                if (missing.Count > 0)
                {
                    // Field names are the library's own fixed words, never user data, so they may be named.
                    return new Failure(FailureKind.InvalidInput, "report.field.missing", "The report lacks the field(s): " + string.Join(", ", missing));
                }

                // Nothing is written before every field is known to exist.
                foreach (var (field, value) in found)
                {
                    field.Hidden = false;
                    field.ReadOnly = false;
                    field.Value = value;
                }

                return null;
            });
        }

        /// <inheritdoc />
        public Result SetFieldsReadOnly(string sourcePath, string targetPath, bool readOnly)
        {
            return Rewrite(sourcePath, targetPath, "report.lock", document =>
            {
                foreach (var field in document.Form.Fields)
                {
                    field.ReadOnly = readOnly;
                }

                return null;
            });
        }

        // Load once, let the change run, save once to a temporary name and swap it in.
        private Result Rewrite(string sourcePath, string targetPath, string operation, Func<PdfDocument, Failure?> change)
        {
            string? temp = null;

            try
            {
                var checkedPath = CheckExists(sourcePath);

                if (checkedPath != null)
                {
                    return Result.Fail(checkedPath);
                }

                if (string.IsNullOrWhiteSpace(targetPath))
                {
                    return Result.Fail(new Failure(FailureKind.InvalidInput, "report.target.missing", "No target file was given."));
                }

                SetLicense();

                using (var document = PdfDocument.Load(sourcePath))
                {
                    var failure = change(document);

                    if (failure != null)
                    {
                        return Result.Fail(failure);
                    }

                    temp = targetPath + ".tmp-" + Guid.NewGuid().ToString("N");
                    // The format is named explicitly: the component would otherwise pick it from the file extension.
                    document.Save(temp, new PdfSaveOptions());
                }

                File.Move(temp, targetPath, overwrite: true);
                temp = null;

                return Result.Ok();
            }
            catch (Exception ex)
            {
                return Result.Fail(Classify(ex, operation));
            }
            finally
            {
                DeleteQuietly(temp);
            }
        }

        // The component's free mode and the licence are process-wide; setting the free key again is harmless.
        private static void SetLicense() => ComponentInfo.SetLicense("FREE-LIMITED-KEY");

        private static Failure? CheckExists(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return new Failure(FailureKind.InvalidInput, "report.source.missing", "No report file was given.");
            }

            if (!File.Exists(path))
            {
                return new Failure(FailureKind.NotFound, "report.source.notFound", "The report file does not exist.");
            }

            // An empty file is a damaged report; the operating system would only report an invalid parameter.
            return new FileInfo(path).Length == 0 ? Unreadable() : null;
        }

        private static Failure Unreadable() => new Failure(FailureKind.Corrupt, "report.unreadable", "The file is not a readable PDF report.");

        // Turns whatever went wrong into a failure that says what kind of problem it is and nothing else.
        private Failure Classify(Exception ex, string operation)
        {
            switch (ex)
            {
                case UnauthorizedAccessException _:
                    return new Failure(FailureKind.AccessDenied, operation + ".denied", "The report file cannot be used: access denied.");
                case IOException _:
                    return new Failure(FailureKind.Io, operation + ".io", "The report file cannot be read or written; it may be open in another program.");
                case FreeLimitReachedException _:
                    return new Failure(FailureKind.InvalidConfiguration, "report.licenseLimit", "The PDF component's free limit was reached.");
                case InvalidDataException _:
                case NotSupportedException _:
                case ArgumentException _:
                    return Unreadable();
                default:
                    // The component's own exception types have no usable namespace, so they are recognised by assembly.
                    if (ex.GetType().Assembly == typeof(PdfDocument).Assembly)
                    {
                        return Unreadable();
                    }

                    _log.Unexpected(operation, UnexpectedFault.From(ex));

                    return new Failure(FailureKind.Unexpected, operation + ".unexpected", "The report could not be processed.");
            }
        }

        private static void DeleteQuietly(string? path)
        {
            if (path == null)
            {
                return;
            }

            try
            {
                File.Delete(path);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                // A leftover temporary file is harmless; the next write uses a new name.
            }
        }
    }
}
