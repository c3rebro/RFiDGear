using System;
using System.IO;
using RFiDGear.Infrastructure.FileAccess;
using Xunit;

namespace RFiDGear.Tests
{
    public class SettingsReaderWriterTests
    {
        [Fact]
        public void ReadSettings_CreatesSettingsFileWhenMissing()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "RFiDGear.Tests", Guid.NewGuid().ToString("N"));
            var settingsPath = Path.Combine(tempRoot, "settings.xml");

            Directory.CreateDirectory(tempRoot);

            using var readerWriter = new SettingsReaderWriter(tempRoot, loadSettings: false);

            var specification = readerWriter.ReadSettings(settingsPath);

            Assert.NotNull(specification);
            Assert.True(File.Exists(settingsPath));
        }
    }
}
