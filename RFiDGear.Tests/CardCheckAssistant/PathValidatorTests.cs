using System.IO;

using RFiDGear.Extensions.CardCheckAssistant.Services;

using Xunit;

namespace RFiDGear.Tests.CardCheckAssistant
{
    /// <summary>
    /// The file-system checks that run before the host is asked anything. The report check refuses what the host refuses
    /// (see the parity tests) and, besides, the names and targets that would make a run look successful while no report
    /// is written.
    /// </summary>
    public class PathValidatorTests
    {
        [Theory]
        [InlineData("report*.pdf")]
        [InlineData("report|1.pdf")]
        [InlineData("a<b.pdf")]
        [InlineData("a>b.pdf")]
        [InlineData("a\"b.pdf")]
        [InlineData("report.pdf:stream")]
        [InlineData("report.")]
        [InlineData("NUL")]
        [InlineData("nul.pdf")]
        [InlineData("CON.txt")]
        [InlineData("com1.pdf")]
        [InlineData("LPT9.pdf")]
        [InlineData("PRN")]
        [InlineData("aux.log")]
        public void AReportNameWindowsCannotCreate_IsUnusable(string fileName)
        {
            using var area = new TempArea();

            Assert.Equal(ReportTargetCheck.Unusable, ReportTargetValidator.Check(Path.Combine(area.Directory, fileName)));
        }

        [Theory]
        [InlineData("report.pdf")]
        [InlineData("Report_???.pdf")]
        [InlineData("Report_?.pdf")]
        [InlineData("console.pdf")]
        [InlineData("com10.pdf")]
        [InlineData("nulls.pdf")]
        [InlineData("aux1.pdf")]
        [InlineData("my report.pdf")]
        [InlineData("Bericht_Müller.pdf")]
        public void AnOrdinaryReportName_IsValid(string fileName)
        {
            using var area = new TempArea();

            Assert.Equal(ReportTargetCheck.Valid, ReportTargetValidator.Check(Path.Combine(area.Directory, fileName)));
        }

        [Fact]
        public void AnExistingReport_IsValidWhenItCanBeReplaced_AndUnusableWhenItIsReadOnly()
        {
            using var area = new TempArea();
            var report = Path.Combine(area.Directory, "job.pdf");
            File.WriteAllText(report, "the report of the previous chip");

            try
            {
                Assert.Equal(ReportTargetCheck.Valid, ReportTargetValidator.Check(report));

                File.SetAttributes(report, FileAttributes.ReadOnly);

                Assert.Equal(ReportTargetCheck.Unusable, ReportTargetValidator.Check(report));
            }
            finally
            {
                File.SetAttributes(report, FileAttributes.Normal);
            }
        }

        [Fact]
        public void TheReportTarget_IsCheckedWithoutItsSurroundingSpaces()
        {
            using var area = new TempArea();

            Assert.Equal(ReportTargetCheck.Valid, ReportTargetValidator.Check("  " + Path.Combine(area.Directory, "report.pdf") + "  "));
            Assert.Equal(ReportTargetCheck.None, ReportTargetValidator.Check("   "));
            Assert.Equal(ReportTargetCheck.None, ReportTargetValidator.Check(null));
        }

        [Fact]
        public void TheProjectPath_IsCheckedForPresenceFormAndExistence()
        {
            using var area = new TempArea();

            Assert.Equal(ProjectPathCheck.Required, ProjectPathValidator.Check(null));
            Assert.Equal(ProjectPathCheck.Required, ProjectPathValidator.Check("  "));
            Assert.Equal(ProjectPathCheck.NotFullyQualified, ProjectPathValidator.Check("check.rfPrj"));
            Assert.Equal(ProjectPathCheck.NotFullyQualified, ProjectPathValidator.Check("C:check.rfPrj"));
            Assert.Equal(ProjectPathCheck.NotFound, ProjectPathValidator.Check(area.ProjectPath + ".missing"));
            Assert.Equal(ProjectPathCheck.NotFound, ProjectPathValidator.Check(area.Directory));   // a folder is not a project file
            Assert.Equal(ProjectPathCheck.Valid, ProjectPathValidator.Check(area.ProjectPath));
            Assert.Equal(ProjectPathCheck.Valid, ProjectPathValidator.Check("  " + area.ProjectPath + "  "));
        }

        [Fact]
        public void TheRealChecker_UsesTheValidators()
        {
            using var area = new TempArea();
            IPathChecker checker = new FileSystemPathChecker();

            Assert.Equal(ProjectPathCheck.Valid, checker.CheckProject(area.ProjectPath));
            Assert.Equal(ProjectPathCheck.NotFound, checker.CheckProject(area.ProjectPath + ".missing"));
            Assert.Equal(ReportTargetCheck.Valid, checker.CheckReportTarget(area.ReportPath));
            Assert.Equal(ReportTargetCheck.NotFullyQualified, checker.CheckReportTarget("report.pdf"));
        }
    }
}
