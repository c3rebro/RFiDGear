using System;
using System.IO;
using System.Reflection;
using Xunit;

namespace RFiDGear.Tests
{
    public class AppAssemblyResolveTests
    {
        [Fact]
        public void GetExtensionAssemblyPath_ReturnsNullWhenAssemblyIsNotExtension()
        {
            var assemblyName = new AssemblyName("Some.Other.Library");

            var result = App.GetExtensionAssemblyPath("C:\\Extensions", assemblyName);

            Assert.Null(result);
        }

        [Fact]
        public void GetExtensionAssemblyPath_ReturnsPathWhenAssemblyExists()
        {
            var tempRoot = Directory.CreateTempSubdirectory("RFiDGearExtResolve").FullName;
            var extensionsPath = Path.Combine(tempRoot, "Extensions");
            var assemblyName = new AssemblyName("RFiDGear.Extensions.VCNEditor");
            var expectedPath = Path.Combine(extensionsPath, "RFiDGear.Extensions.VCNEditor.dll");

            try
            {
                Directory.CreateDirectory(extensionsPath);
                File.WriteAllText(expectedPath, string.Empty);

                var result = App.GetExtensionAssemblyPath(extensionsPath, assemblyName);

                Assert.Equal(expectedPath, result);
            }
            finally
            {
                Directory.Delete(tempRoot, true);
            }
        }
    }
}
