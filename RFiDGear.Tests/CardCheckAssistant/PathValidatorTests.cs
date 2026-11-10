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
        }
    }
}
