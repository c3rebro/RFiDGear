using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using RFiDGear.Contracts;
using RFiDGear.Extensions.CardCheckAssistant.Services;
using RFiDGear.Infrastructure.ExtensionHost;
using RFiDGear.Infrastructure.FileAccess;

using Xunit;

namespace RFiDGear.Tests.CardCheckAssistant
{
    /// <summary>
    /// The page checks the report file before it asks the host to run, so the user sees what is wrong next to the field.
    /// The host stays the authority and refuses a bad target before any card operation. The page must never promise a
    /// run the host then refuses, so everything the host refuses the page refuses too. It must not block a run the host
    /// accepts either, except for the rows marked below: names Windows cannot create and read-only reports pass the
    /// host, whose report step then only logs the failure, and the page deliberately refuses them.
    /// </summary>
    [Collection("AppDataPath")]
    public class ReportTargetValidatorParityTests
    {
        [Fact]
        public async Task ThePageAndTheHost_AgreeOnWhichReportTargetsAreUsable()
        {
            using var area = new TempArea();
            Directory.CreateDirectory(Path.Combine(area.Directory, "sub"));
            var existingFile = Path.Combine(area.Directory, "existing.pdf");
            File.WriteAllText(existingFile, "already there");
            var separator = Path.DirectorySeparatorChar;

            var readOnlyReport = Path.Combine(area.Directory, "read-only.pdf");
            File.WriteAllText(readOnlyReport, "locked");
            File.SetAttributes(readOnlyReport, FileAttributes.ReadOnly);

            // PageMayBeStricter: the host accepts, the page refuses on purpose.
            var cases = new (string Description, string Target, bool PageMayBeStricter)[]
            {
                ("blank", string.Empty, false),
                ("only spaces", "   ", false),
                ("relative file", "report.pdf", false),
                ("relative with a folder", "sub" + separator + "report.pdf", false),
                ("drive-relative", "C:report.pdf", false),
                ("new file in an existing folder", Path.Combine(area.Directory, "new.pdf"), false),
                ("existing file", existingFile, false),
                ("file in a subfolder", Path.Combine(area.Directory, "sub", "report.pdf"), false),
                ("placeholder ???", Path.Combine(area.Directory, "Report_???.pdf"), false),
                ("placeholder ?", Path.Combine(area.Directory, "Report_?.pdf"), false),
                ("folder without separator", area.Directory, false),
                ("folder with separator", area.Directory + separator, false),
                ("subfolder as the target", Path.Combine(area.Directory, "sub"), false),
                ("folder that does not exist", Path.Combine(area.Directory, "missing", "report.pdf"), false),
                ("a file used as a folder", Path.Combine(existingFile, "report.pdf"), false),
                ("drive root", "C:" + separator, false),
                ("a forbidden character in the name", Path.Combine(area.Directory, "report*.pdf"), true),
                ("a device name", Path.Combine(area.Directory, "NUL.pdf"), true),
                ("a name ending in a dot", Path.Combine(area.Directory, "report."), true),
                ("an existing read-only report", readOnlyReport, true),
            };

            var disagreements = new List<string>();
            try
            {
                foreach (var (description, target, pageMayBeStricter) in cases)
                {
                    var sent = target.Trim();   // what the page sends
                    var pageRejects = ReportTargetValidator.Check(sent) is not (ReportTargetCheck.None or ReportTargetCheck.Valid);
                    var hostRefuses = await HostRefuses(sent);

                    if (hostRefuses && !pageRejects)
                    {
                        disagreements.Add($"{description} ({target}): the host refuses it but the page would let the run start");
                    }
                    else if (pageMayBeStricter && !pageRejects)
                    {
                        disagreements.Add($"{description} ({target}): the page is meant to refuse this");
                    }
                    else if (!pageMayBeStricter && pageRejects && !hostRefuses)
                    {
                        disagreements.Add($"{description} ({target}): the page blocks a run the host accepts");
                    }
                }
            }
            finally
            {
                File.SetAttributes(readOnlyReport, FileAttributes.Normal);
            }

            Assert.Empty(disagreements);
        }

        [Fact]
        public void TheTableCoversEveryVerdictOfTheValidator()
        {
            using var area = new TempArea();
            var verdicts = new HashSet<ReportTargetCheck>
            {
                ReportTargetValidator.Check(string.Empty),
                ReportTargetValidator.Check(Path.Combine(area.Directory, "new.pdf")),
                ReportTargetValidator.Check("report.pdf"),
                ReportTargetValidator.Check(Path.Combine(area.Directory, "missing", "report.pdf")),
                ReportTargetValidator.Check(area.Directory),
                ReportTargetValidator.Check(Path.Combine(area.Directory, "NUL.pdf")),
            };

            Assert.Equal(
                new[]
                {
                    ReportTargetCheck.None, ReportTargetCheck.Valid, ReportTargetCheck.NotFullyQualified,
                    ReportTargetCheck.FolderMissing, ReportTargetCheck.NotAFile, ReportTargetCheck.Unusable
                },
                verdicts);
        }

        private static async Task<bool> HostRefuses(string target)
        {
            using var host = new HeadlessExtensionHost(new DatabaseReaderWriter(), new CapturingExecService(), string.Empty);
            var request = new ExecutionRequest(new[] { new KeyValuePair<string, string>("REPORTTARGETPATH", target) });

            try
            {
                await host.Execution.ExecuteAllAsync(request);
                return false;
            }
            catch (ArgumentException)
            {
                return true;
            }
        }
    }
}
