using System;
using System.IO;
using RFiDGear.Infrastructure;
using Xunit;

namespace RFiDGear.Tests
{
    public class MefHelperTests
    {
        [Fact]
        public void GetExtensionCatalogPaths_IncludesConfiguredExtensionsPath()
        {
            var tempRoot = Directory.CreateTempSubdirectory("RFiDGearExt").FullName;
            var extensionsPath = Path.Combine(tempRoot, "Extensions");
            var baseDirectory = Path.Combine(tempRoot, "a", "b", "c", "d", "e");

            try
            {
                Directory.CreateDirectory(extensionsPath);
                Directory.CreateDirectory(baseDirectory);

                var paths = MefHelper.GetExtensionCatalogPaths(baseDirectory, extensionsPath);

                Assert.Contains(extensionsPath, paths, StringComparer.OrdinalIgnoreCase);
            }
            finally
            {
                Directory.Delete(tempRoot, true);
            }
        }

        // The extension projects name their framework with and without the platform version, and that is the folder name.
        [Theory]
        [InlineData("net10.0-windows")]
        [InlineData("net10.0-windows7.0")]
        public void FindDevelopmentExtensionsPaths_ReturnsExtensionsOutputWhenPresent(string frameworkFolder)
        {
            var tempRoot = Directory.CreateTempSubdirectory("RFiDGearExtDev").FullName;
            var baseDirectory = Path.Combine(tempRoot, "a", "b", "c", "d", "e");
            var expectedPath = Path.Combine(
                tempRoot,
                "RFiDGear.Extensions",
                "DesfirePluginSample",
                "bin",
                "Debug",
                frameworkFolder);

            try
            {
                Directory.CreateDirectory(baseDirectory);
                Directory.CreateDirectory(expectedPath);

                var result = MefHelper.FindDevelopmentExtensionsPaths(baseDirectory);

                Assert.Contains(expectedPath, result, StringComparer.OrdinalIgnoreCase);
            }
            finally
            {
                Directory.Delete(tempRoot, true);
            }
        }

        [Fact]
        public void BuildExtensionsPath_UsesProgramDataRootAndExtensionsFolder()
        {
            var programDataRoot = Path.Combine(Path.GetTempPath(), "ProgramData");

            var result = MefHelper.BuildExtensionsPath(programDataRoot, "RFiDGear");

            Assert.Equal(Path.Combine(programDataRoot, "RFiDGear", "Extensions"), result);
        }

        [Fact]
        public void GetExtensionAssemblyPaths_ReturnsOnlyExtensionAssemblies()
        {
            var tempRoot = Directory.CreateTempSubdirectory("RFiDGearExtScan").FullName;

            try
            {
                var extensionPath = Path.Combine(tempRoot, "RFiDGear.Extensions.Sample.dll");
                var appPath = Path.Combine(tempRoot, "RFiDGear.dll");
                var otherPath = Path.Combine(tempRoot, "SomeOther.dll");

                File.WriteAllText(extensionPath, string.Empty);
                File.WriteAllText(appPath, string.Empty);
                File.WriteAllText(otherPath, string.Empty);

                var results = MefHelper.GetExtensionAssemblyPaths(tempRoot);

                Assert.Contains(extensionPath, results, StringComparer.OrdinalIgnoreCase);
                Assert.DoesNotContain(appPath, results, StringComparer.OrdinalIgnoreCase);
                Assert.DoesNotContain(otherPath, results, StringComparer.OrdinalIgnoreCase);
            }
            finally
            {
                Directory.Delete(tempRoot, true);
            }
        }
    }
}
