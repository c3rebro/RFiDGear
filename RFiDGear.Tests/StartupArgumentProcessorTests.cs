using System.IO;
using System.Threading.Tasks;
using RFiDGear.Infrastructure.FileAccess;
using RFiDGear.Models;
using RFiDGear.Services;
using RFiDGear.Services.Interfaces;
using Xunit;

namespace RFiDGear.Tests
{
    public class StartupArgumentProcessorTests
    {
        [Fact]
        public async Task CustomProjectFileArgument_TriggersOpenProjectEvenWhenAutoLoadDisabled()
        {
            var tempProjectFile = Path.GetTempFileName();
            try
            {
                var result = new StartupArgumentProcessor().Process(new[]
                {
                    "RFiDGear.exe",
                    $"CUSTOMPROJECTFILE={tempProjectFile}"
                });

                var projectManager = new ProjectManager();
                var settingsPath = projectManager.SettingsPath;

                using (var settingsWriter = new SettingsReaderWriter(projectManager.AppDataPath, loadSettings: false))
                {
                    var specification = new DefaultSpecification(true)
                    {
                        AutoLoadProjectOnStart = false
                    };

                    settingsWriter.SaveSettings(specification, settingsPath);
                }

                var openedPath = string.Empty;

                var request = new ProjectBootstrapRequest
                {
                    ProjectFilePath = result.ProjectFilePath,
                    OpenProjectAsync = path =>
                    {
                        openedPath = path ?? string.Empty;
                        return Task.CompletedTask;
                    }
                };

                await new ProjectBootstrapper().BootstrapAsync(request);

                Assert.Equal(new FileInfo(tempProjectFile).FullName, result.ProjectFilePath);
                Assert.Equal(result.ProjectFilePath, openedPath);
            }
            finally
            {
                File.Delete(tempProjectFile);
            }
        }

        [Fact]
        public void RawProjectFileArgument_LoadsProjectPath()
        {
            var tempProjectFile = Path.Combine(Path.GetTempPath(), $"{Path.GetRandomFileName()}.rfprj");

            try
            {
                File.WriteAllText(tempProjectFile, "test");

                var result = new StartupArgumentProcessor().Process(new[]
                {
                    "RFiDGear.exe",
                    tempProjectFile
                });

                Assert.Equal(new FileInfo(tempProjectFile).FullName, result.ProjectFilePath);
            }
            finally
            {
                File.Delete(tempProjectFile);
            }
        }

        [Fact]
        public void LegacyReportArguments_MapToReportFields()
        {
            var tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDirectory);

            var reportPattern = Path.Combine(tempDirectory, "Report-??.pdf");
            var existingReport = Path.Combine(tempDirectory, "Report-01.pdf");
            File.WriteAllText(existingReport, "report");

            var templateFile = Path.Combine(tempDirectory, "Template.pdf");
            File.WriteAllText(templateFile, "template");

            try
            {
                var result = new StartupArgumentProcessor().Process(new[]
                {
                    "RFiDGear.exe",
                    $"REPORTOUTPUTPATH={reportPattern}",
                    $"REPORTTEMPLATEPATH={templateFile}"
                });

                Assert.Equal(Path.Combine(tempDirectory, "Report-02.pdf"), result.ReportOutputPath);
                Assert.Equal(templateFile, result.ReportTemplateFile);
            }
            finally
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }
}
