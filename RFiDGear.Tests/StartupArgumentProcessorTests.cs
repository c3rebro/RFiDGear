using System.IO;
using System.Threading.Tasks;
using RFiDGear.Infrastructure.FileAccess;
using RFiDGear.Infrastructure;
using RFiDGear.Models;
using RFiDGear.Services;
using RFiDGear.Services.Interfaces;
using Xunit;

namespace RFiDGear.Tests
{
    [Collection("SettingsFileAccess")]
    public class StartupArgumentProcessorTests
    {
        [Fact]
        public async Task CustomProjectFileArgument_TriggersOpenProjectEvenWhenAutoLoadDisabled()
        {
            var tempProjectFile = Path.GetTempFileName();
            var tempSettingsDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempSettingsDirectory);
            try
            {
                var result = new StartupArgumentProcessor().Process(new[]
                {
                    "RFiDGear.exe",
                    $"CUSTOMPROJECTFILE={tempProjectFile}"
                });

                var settingsPath = Path.Combine(tempSettingsDirectory, "settings.xml");

                using (var settingsWriter = new SettingsReaderWriter(tempSettingsDirectory, loadSettings: false))
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

                await new ProjectBootstrapper(
                    () => new SettingsReaderWriter(tempSettingsDirectory)).BootstrapAsync(request);

                Assert.Equal(new FileInfo(tempProjectFile).FullName, result.ProjectFilePath);
                Assert.Equal(result.ProjectFilePath, openedPath);
            }
            finally
            {
                File.Delete(tempProjectFile);
                Directory.Delete(tempSettingsDirectory, recursive: true);
            }
        }

        [Fact]
        public async Task AutorunBootstrap_AppliesPersistedReaderProviderBeforeExecution()
        {
            var tempSettingsDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempSettingsDirectory);
            var settingsPath = Path.Combine(tempSettingsDirectory, "settings.xml");

            try
            {
                using (var writer = new SettingsReaderWriter(tempSettingsDirectory, loadSettings: false))
                {
                    var specification = new DefaultSpecification(true)
                    {
                        DefaultReaderProvider = ReaderTypes.Elatec,
                        AutoLoadProjectOnStart = false
                    };
                    writer.SaveSettings(specification, settingsPath);
                }

                var appliedProvider = ReaderTypes.None;
                var providerAtRead = ReaderTypes.None;
                var request = new ProjectBootstrapRequest
                {
                    Autorun = true,
                    SetReaderProvider = value => appliedProvider = value,
                    ResetTaskStatusAsync = () => Task.CompletedTask,
                    ReadChipAsync = () =>
                    {
                        providerAtRead = appliedProvider;
                        return Task.CompletedTask;
                    },
                    WriteOnceAsync = () => Task.CompletedTask
                };

                await new ProjectBootstrapper(() => new SettingsReaderWriter(tempSettingsDirectory)).BootstrapAsync(request);

                Assert.Equal(ReaderTypes.Elatec, appliedProvider);
                Assert.Equal(ReaderTypes.Elatec, providerAtRead);
            }
            finally
            {
                Directory.Delete(tempSettingsDirectory, true);
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
        public void AutoModeArgument_SetsAutoModeTrue()
        {
            var result = new StartupArgumentProcessor().Process(new[]
            {
                "RFiDGear.exe",
                "AUTOMODE=1"
            });

            Assert.True(result.AutoMode);
        }

        [Fact]
        public void AutoModeArgument_AbsentLeavesAutoModeFalse()
        {
            var result = new StartupArgumentProcessor().Process(new[]
            {
                "RFiDGear.exe"
            });

            Assert.False(result.AutoMode);
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
