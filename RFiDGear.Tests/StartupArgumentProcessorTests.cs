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
    }
}
