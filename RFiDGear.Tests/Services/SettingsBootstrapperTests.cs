using Microsoft.VisualStudio.TestTools.UnitTesting;
using RFiDGear.Model;
using RFiDGear;
using RFiDGear.Services;
using System;
using System.IO;
using System.Threading.Tasks;

namespace RFiDGear.Tests.Services
{
    [TestClass]
    public class SettingsBootstrapperTests
    {
        [TestMethod]
        public async Task LoadAsync_UsesPersistedSpecification()
        {
            var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);

            var specification = new DefaultSpecification
            {
                DefaultReaderName = "TestReader",
                DefaultReaderProvider = ReaderTypes.None,
                DefaultLanguage = "german",
                AutoLoadProjectOnStart = true,
                LastUsedProjectPath = "tempProject.rfid",
                LastUsedComPort = "3"
            };

            using (var writer = new SettingsReaderWriter(tempDirectory, false))
            {
                writer.DefaultSpecification = specification;
                await writer.SaveSettings().ConfigureAwait(false);
            }

            var bootstrapper = new SettingsBootstrapper(() => new SettingsReaderWriter(tempDirectory));

            var result = await bootstrapper.LoadAsync().ConfigureAwait(false);

            Assert.AreEqual(specification.DefaultReaderName, result.CurrentReaderName);
            Assert.AreEqual(3, result.PortNumber);
            Assert.IsTrue(result.AutoLoadLastUsedProject);
            Assert.AreEqual(specification.LastUsedProjectPath, result.LastUsedProjectPath);
            Assert.AreEqual("de-DE", result.Culture.Name);
        }
    }
}
