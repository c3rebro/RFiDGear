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
    /// The host stays the authority and refuses a bad target before any card operation. If the two ever disagreed the
    /// page would either block a run the host accepts or promise a run the host then refuses, so they are compared here
    /// over a table of paths.
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

            var cases = new (string Description, string Target)[]
            {
                ("blank", string.Empty),
                ("only spaces", "   "),
                ("relative file", "report.pdf"),
                ("relative with a folder", "sub" + separator + "report.pdf"),
                ("drive-relative", "C:report.pdf"),
                ("new file in an existing folder", Path.Combine(area.Directory, "new.pdf")),
                ("existing file", existingFile),
                ("file in a subfolder", Path.Combine(area.Directory, "sub", "report.pdf")),
                ("placeholder ???", Path.Combine(area.Directory, "Report_???.pdf")),
                ("placeholder ?", Path.Combine(area.Directory, "Report_?.pdf")),
                ("folder without separator", area.Directory),
                ("folder with separator", area.Directory + separator),
                ("subfolder as the target", Path.Combine(area.Directory, "sub")),
                ("folder that does not exist", Path.Combine(area.Directory, "missing", "report.pdf")),
                ("a file used as a folder", Path.Combine(existingFile, "report.pdf")),
                ("drive root", "C:" + separator),
            };

            var disagreements = new List<string>();
            foreach (var (description, target) in cases)
            {
                var sent = target.Trim();   // what the page sends
                var pageRejects = ReportTargetValidator.Check(sent) is not (ReportTargetCheck.None or ReportTargetCheck.Valid);
                var hostRefuses = await HostRefuses(sent);

                if (pageRejects != hostRefuses)
                {
                    disagreements.Add($"{description} ({target}): page rejects={pageRejects}, host refuses={hostRefuses}");
                }
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
            };

            Assert.Equal(
                new[]
                {
                    ReportTargetCheck.None, ReportTargetCheck.Valid, ReportTargetCheck.NotFullyQualified,
                    ReportTargetCheck.FolderMissing, ReportTargetCheck.NotAFile
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
