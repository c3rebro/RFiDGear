using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using GemBox.Pdf;
using GemBox.Pdf.Forms;

using RFiDGear.ExtensionLibraries.CardCheck;

using Xunit;

namespace RFiDGear.Tests.CardCheck
{
    /// <summary>
    /// The PDF side of a check, on small reports the tests build themselves. Kept to the contract: what is read, what
    /// is written, nothing half written, nothing leaked.
    /// </summary>
    public sealed class ReportServiceTests : IDisposable
    {
        private readonly TempFolder _folder = new TempFolder();
        private readonly RecordingCardCheckLog _log = new RecordingCardCheckLog();

        public void Dispose() => _folder.Dispose();

        private IReportService Service() => CardCheckServices.CreateReportService(_log);

        private string PathOf(string name) => Path.Combine(_folder.Path, name);

        // Builds a one-page report with the given form fields: a check box for names starting with "CheckBox_", else
        // text. A leading "~" makes a text field of such a name, so a value a check box would normalise stays as typed.
        private string Report(string name, params (string Field, string Value)[] fields)
        {
            ComponentInfo.SetLicense("FREE-LIMITED-KEY");
            using var document = new PdfDocument();
            var page = document.Pages.Add();
            var top = 700.0;

            foreach (var (declared, value) in fields)
            {
                var asText = declared.StartsWith("~", StringComparison.Ordinal);
                var fieldName = asText ? declared.Substring(1) : declared;
                var field = !asText && fieldName.StartsWith("CheckBox_", StringComparison.Ordinal)
                    ? (PdfField)document.Form.Fields.AddCheckBox(page, 50, top, 14, 14)
                    : document.Form.Fields.AddText(page, 50, top, 200, 20);
                field.Name = fieldName;
                field.Value = value;
                top -= 30;
            }

            var path = PathOf(name);
            document.Save(path);

            return path;
        }

        private static (string, string)[] FirstReport(string supported = "Yes", string programmable = "Yes", string memory = "1760") => new[]
        {
            ("CheckBox_isChipSuppYes", supported),
            ("CheckBox_ChipCanUseYes", programmable),
            ("TextBox_Detail_FreeMem_1", memory),
            ("TextBox_ChipType", "MIFARE DESFire EV2"),
            ("TextBox_Notes_Site_2", "notes"),
        };

        private static Dictionary<string, string?> Values(string path)
        {
            ComponentInfo.SetLicense("FREE-LIMITED-KEY");
            using var document = PdfDocument.Load(path);

            return document.Form.Fields.ToDictionary(f => f.Name, f => f.Value as string);
        }

        private static bool AllReadOnly(string path, bool expected)
        {
            ComponentInfo.SetLicense("FREE-LIMITED-KEY");
            using var document = PdfDocument.Load(path);

            return document.Form.Fields.All(f => f.ReadOnly == expected);
        }

        private static bool IsReadOnly(string path, string field)
        {
            ComponentInfo.SetLicense("FREE-LIMITED-KEY");
            using var document = PdfDocument.Load(path);

            return document.Form.Fields[field]!.ReadOnly;
        }

        // ── reading the first report ────────────────────────────────────────

        [Fact]
        public void Analyze_ReadsTheFiveFieldsOfTheFirstReport()
        {
            var analysis = Service().Analyze(Report("step1.pdf", FirstReport())).Value;

            Assert.True(analysis.Supported);
            Assert.True(analysis.Programmable);
            Assert.Equal("1760", analysis.FreeMemoryText);
            Assert.Equal("MIFARE DESFire EV2", analysis.ChipType);
            Assert.Equal("notes", analysis.Notes);
        }

        [Fact]
        public void Analyze_ACheckBoxIsTickedOnlyWhenItsValueIsExactlyYes()
        {
            var off = Service().Analyze(Report("off.pdf", FirstReport(supported: "Off", programmable: "Off"))).Value;
            // As text fields, because a check box would normalise the spelling.
            var lower = Service().Analyze(Report("lower.pdf", ("~CheckBox_isChipSuppYes", "yes"), ("~CheckBox_ChipCanUseYes", "YES"))).Value;

            Assert.False(off.Supported);
            Assert.False(off.Programmable);
            Assert.False(lower.Supported);
            Assert.False(lower.Programmable);
        }

        [Fact]
        public void Analyze_AFieldTheReportDoesNotHave_ReadsAsEmpty()
        {
            var analysis = Service().Analyze(Report("partial.pdf", ("CheckBox_isChipSuppYes", "Yes"))).Value;

            Assert.True(analysis.Supported);
            Assert.False(analysis.Programmable);
            Assert.Equal(string.Empty, analysis.FreeMemoryText);
            Assert.Equal(string.Empty, analysis.ChipType);
        }

        [Fact]
        public void Analyze_ThatCannotBeAReport_IsAFailureNotAnException()
        {
            File.WriteAllText(PathOf("garbage.pdf"), "this is not a pdf");
            File.WriteAllBytes(PathOf("empty.pdf"), Array.Empty<byte>());
            ComponentInfo.SetLicense("FREE-LIMITED-KEY");
            using (var blank = new PdfDocument())
            {
                blank.Pages.Add();
                blank.Save(PathOf("noform.pdf"));
            }

            Assert.Equal(FailureKind.NotFound, Service().Analyze(PathOf("missing.pdf")).Failure!.Kind);
            Assert.Equal(FailureKind.NotFound, Service().Analyze(_folder.Path).Failure!.Kind);
            Assert.Equal(FailureKind.InvalidInput, Service().Analyze(" ").Failure!.Kind);
            Assert.Equal(FailureKind.Corrupt, Service().Analyze(PathOf("garbage.pdf")).Failure!.Kind);
            Assert.Equal(FailureKind.Corrupt, Service().Analyze(PathOf("empty.pdf")).Failure!.Kind);
            Assert.Equal("report.noForm", Service().Analyze(PathOf("noform.pdf")).Failure!.Code);
        }

        // ── writing fields ──────────────────────────────────────────────────

        [Fact]
        public void WriteFields_SetsTheFieldsInACopy_AndLeavesTheSourceAlone()
        {
            var source = Report("step1.pdf", FirstReport());
            var before = File.ReadAllBytes(source);
            var target = PathOf("step2.pdf");

            var result = Service().WriteFields(source, target, new Dictionary<string, string>
            {
                ["TextBox_Notes_Site_2"] = "changed notes",
                ["CheckBox_ChipCanUseYes"] = "Off",
            });

            Assert.True(result.IsSuccess);
            Assert.Equal(before, File.ReadAllBytes(source));
            var written = Service().Analyze(target).Value;
            Assert.Equal("changed notes", written.Notes);
            Assert.False(written.Programmable);
            Assert.True(written.Supported);
            Assert.Equal("1760", written.FreeMemoryText);
        }

        [Fact]
        public void WriteFields_AFieldOfALockedReport_IsWrittenAndLeftEditable()
        {
            var locked = PathOf("locked.pdf");
            Service().SetFieldsReadOnly(Report("open.pdf", FirstReport()), locked, readOnly: true);
            Assert.True(IsReadOnly(locked, "TextBox_Notes_Site_2"));

            var result = Service().WriteFields(locked, PathOf("unlocked.pdf"), new Dictionary<string, string> { ["TextBox_Notes_Site_2"] = "new" });

            Assert.True(result.IsSuccess);
            Assert.Equal("new", Service().Analyze(PathOf("unlocked.pdf")).Value.Notes);
            Assert.False(IsReadOnly(PathOf("unlocked.pdf"), "TextBox_Notes_Site_2"));
            Assert.True(IsReadOnly(PathOf("unlocked.pdf"), "TextBox_ChipType"));
        }

        [Fact]
        public void WriteFields_WhenAFieldIsMissing_WritesNothingAtAll()
        {
            var source = Report("step1.pdf", FirstReport());
            var target = PathOf("step2.pdf");

            var result = Service().WriteFields(source, target, new Dictionary<string, string>
            {
                ["TextBox_Notes_Site_2"] = "would be written",
                ["Field_That_Is_Not_There"] = "x",
            });

            Assert.False(result.IsSuccess);
            Assert.Equal("report.field.missing", result.Failure!.Code);
            Assert.Contains("Field_That_Is_Not_There", result.Failure.SafeMessage, StringComparison.Ordinal);
            Assert.False(File.Exists(target));
        }

        [Fact]
        public void WriteFields_ReplacesAnExistingTarget_AndLeavesNoTemporaryFile()
        {
            var source = Report("step1.pdf", FirstReport());
            File.WriteAllText(PathOf("step2.pdf"), "an older file");

            Assert.True(Service().WriteFields(source, PathOf("step2.pdf"), new Dictionary<string, string> { ["TextBox_Notes_Site_2"] = "n" }).IsSuccess);

            Assert.Equal("n", Service().Analyze(PathOf("step2.pdf")).Value.Notes);
            Assert.Empty(Directory.GetFiles(_folder.Path, ReportService.TemporaryPrefix + "*"));
            Assert.Equal(2, Directory.GetFiles(_folder.Path).Length);
        }

        [Fact]
        public void WriteFields_ATargetNameNearTheFileSystemLimit_StillWorks()
        {
            // 230 characters is a valid file name; the temporary file must not be the target's name plus a suffix.
            var target = PathOf(new string('a', 226) + ".pdf");

            var result = Service().WriteFields(Report("step1.pdf", FirstReport()), target, new Dictionary<string, string> { ["TextBox_Notes_Site_2"] = "n" });

            Assert.True(result.IsSuccess);
            Assert.Equal("n", Service().Analyze(target).Value.Notes);
        }

        [Fact]
        public void WriteFields_ATargetThatCannotBeAFileName_IsRefusedWithoutAnException()
        {
            var source = Report("step1.pdf", FirstReport());

            var result = Service().WriteFields(source, PathOf("bad\0name.pdf"), new Dictionary<string, string>());

            Assert.Equal("report.target.invalid", result.Failure!.Code);
        }

        [Fact]
        public void WriteFields_ATargetTheFileSystemRefusesLate_IsAFailureAndNotAnException()
        {
            var source = Report("step1.pdf", FirstReport());

            // A colon inside a folder name passes the character check and fails only when the file is used.
            var result = Service().WriteFields(source, PathOf("a:b" + Path.DirectorySeparatorChar + "out.pdf"), new Dictionary<string, string>());

            Assert.False(result.IsSuccess);
        }

        [Fact]
        public void WriteFields_ToAPathInAFolderThatIsNotThere_FailsWithoutLeavingAnything()
        {
            var source = Report("step1.pdf", FirstReport());

            var result = Service().WriteFields(source, Path.Combine(_folder.Path, "no-such-folder", "out.pdf"), new Dictionary<string, string>());

            Assert.False(result.IsSuccess);
            Assert.Empty(Directory.GetFiles(_folder.Path, ReportService.TemporaryPrefix + "*"));
        }

        [Fact]
        public void WriteFields_WhenTheFinalSwapFails_TheTemporaryCopyOfTheReportIsRemoved()
        {
            var source = Report("step1.pdf", FirstReport());
            Directory.CreateDirectory(PathOf("taken.pdf"));   // a folder where the report should go, so the swap fails

            var result = Service().WriteFields(source, PathOf("taken.pdf"), new Dictionary<string, string> { ["TextBox_Notes_Site_2"] = "n" });

            Assert.False(result.IsSuccess);
            Assert.Empty(Directory.GetFiles(_folder.Path, ReportService.TemporaryPrefix + "*"));
        }

        // ── locking and unlocking ───────────────────────────────────────────

        [Fact]
        public void SetFieldsReadOnly_LocksEveryFieldInACopy_AndUnlocksThemAgain()
        {
            var source = Report("step1.pdf", FirstReport());
            var finalPath = PathOf("final.pdf");
            var editPath = PathOf("edit.pdf");

            Assert.True(Service().SetFieldsReadOnly(source, finalPath, readOnly: true).IsSuccess);
            Assert.True(Service().SetFieldsReadOnly(finalPath, editPath, readOnly: false).IsSuccess);

            Assert.True(AllReadOnly(finalPath, true));
            Assert.True(AllReadOnly(editPath, false));
            Assert.Equal(Values(source), Values(finalPath));
        }

        [Fact]
        public void SetFieldsReadOnly_OfAMissingOrDamagedFile_IsAFailure_AndWritesNothing()
        {
            File.WriteAllText(PathOf("garbage.pdf"), "not a pdf");

            Assert.Equal(FailureKind.NotFound, Service().SetFieldsReadOnly(PathOf("missing.pdf"), PathOf("out1.pdf"), true).Failure!.Kind);
            Assert.Equal(FailureKind.Corrupt, Service().SetFieldsReadOnly(PathOf("garbage.pdf"), PathOf("out2.pdf"), true).Failure!.Kind);
            Assert.False(File.Exists(PathOf("out1.pdf")));
            Assert.False(File.Exists(PathOf("out2.pdf")));
        }

        [Fact]
        public void AFileAnotherProgramHolds_IsAnIoFailure_NotAnException()
        {
            var source = Report("step1.pdf", FirstReport());

            using (new FileStream(source, FileMode.Open, System.IO.FileAccess.Read, FileShare.None))
            {
                Assert.Equal(FailureKind.Io, Service().Analyze(source).Failure!.Kind);
                Assert.Equal(FailureKind.Io, Service().SetFieldsReadOnly(source, PathOf("out.pdf"), true).Failure!.Kind);
            }
        }

        // ── what is kept out of failures and logs ───────────────────────────

        [Fact]
        public void NoFieldContentReachesAFailureOrTheLog()
        {
            const string secret = "SECRET-CUSTOMER-NOTE";
            var source = Report("step1.pdf", ("CheckBox_isChipSuppYes", "Yes"), ("TextBox_Notes_Site_2", secret));
            var messages = new List<string>
            {
                Service().WriteFields(source, PathOf("out.pdf"), new Dictionary<string, string> { ["Nope"] = secret }).Failure!.SafeMessage,
                Service().Analyze(PathOf("missing.pdf")).Failure!.SafeMessage,
            };

            Assert.All(messages, m => Assert.DoesNotContain(secret, m, StringComparison.Ordinal));
            Assert.DoesNotContain(secret, _log.Everything, StringComparison.Ordinal);
            Assert.DoesNotContain(_folder.Path, string.Join(" ", messages) + _log.Everything, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Arguments_AreChecked()
        {
            Assert.Throws<ArgumentNullException>(() => CardCheckServices.CreateReportService(null!));
            Assert.Throws<ArgumentNullException>(() => Service().WriteFields("a", "b", null!));
            Assert.Equal("report.target.missing", Service().SetFieldsReadOnly(Report("r.pdf", FirstReport()), " ", true).Failure!.Code);
        }
    }
}
