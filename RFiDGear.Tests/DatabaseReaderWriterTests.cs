using System;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using RFiDGear.Infrastructure.FileAccess;
using RFiDGear.Models;
using RFiDGear.ViewModel;
using Xunit;

namespace RFiDGear.Tests
{
    public class DatabaseReaderWriterTests
    {
        [Fact]
        public async Task ExecuteOnStaThread_WhenApplicationIsNull_UsesStaThread()
        {
            var readerWriter = new DatabaseReaderWriter(new ProjectManager());
            var method = typeof(DatabaseReaderWriter).GetMethod(
                "ExecuteOnStaThread",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(method);

            ApartmentState? observedState = null;

            await Task.Run(() =>
            {
                method.Invoke(readerWriter, new object[]
                {
                    (Action)(() => observedState = Thread.CurrentThread.GetApartmentState())
                });
            });

            Assert.Equal(ApartmentState.STA, observedState);
        }

        [Fact]
        public void WriteProject_OverAnExistingArchive_ReplacesItAndLeavesNoStagingFiles()
        {
            using (var folder = new TemporaryFolder())
            {
                var readerWriter = new DatabaseReaderWriter(new ProjectManager(folder.Combine("appdata")));
                var target = folder.Combine("project.rfPrj");
                File.WriteAllText(target, "old content");

                var written = readerWriter.WriteDatabase(new ChipTaskHandlerModel(), target);

                Assert.Equal(target, written);
                using (var archive = ZipFile.OpenRead(target))
                {
                    Assert.Contains(archive.Entries, entry => entry.Name == "taskdatabase.xml");
                }

                Assert.Equal(new[] { target }, folder.Files());
            }
        }

        [Fact]
        public void WriteProject_WhenTheTargetCannotBeReplaced_KeepsTheOldFileAndCleansUp()
        {
            using (var folder = new TemporaryFolder())
            {
                var readerWriter = new DatabaseReaderWriter(new ProjectManager(folder.Combine("appdata")));
                var target = folder.Combine("project.rfPrj");
                File.WriteAllText(target, "old content");

                // A file that another process holds open cannot be replaced.
                using (new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    Assert.ThrowsAny<IOException>(() => readerWriter.WriteDatabase(new ChipTaskHandlerModel(), target));
                }

                Assert.Equal("old content", File.ReadAllText(target));
                Assert.Equal(new[] { target }, folder.Files());
            }
        }

        [Fact]
        public void WriteProject_AsXml_ReplacesAnExistingFileThroughATemporaryFile()
        {
            using (var folder = new TemporaryFolder())
            {
                var readerWriter = new DatabaseReaderWriter(new ProjectManager(folder.Combine("appdata")));
                var target = folder.Combine("project.xml");
                File.WriteAllText(target, "old content");

                var written = readerWriter.WriteDatabase(new ChipTaskHandlerModel(), target);

                Assert.Equal(target, written);
                Assert.StartsWith("<?xml", File.ReadAllText(target));
                Assert.Equal(new[] { target }, folder.Files());
            }
        }

        [Theory]
        [InlineData(@"C:\chips\tree.xml", @"C:\chips\tree.xml")]
        [InlineData(@"C:\chips\tree.XML", @"C:\chips\tree.XML")]
        [InlineData(@"C:\chips\tree.rfPrj", @"C:\chips\tree.xml")]
        [InlineData(@"C:\chips\tree", @"C:\chips\tree.xml")]
        [InlineData(@"C:\chips\tree.v2", @"C:\chips\tree.v2.xml")]
        public void ChipExportPath_AlwaysEndsInXml(string chosen, string expected)
        {
            Assert.Equal(expected, DatabaseReaderWriter.ChipExportPath(chosen));
        }

        [Fact]
        public void WriteChipTree_WithAProjectExtension_WritesPlainXmlUnderAnXmlName()
        {
            using (var folder = new TemporaryFolder())
            {
                var readerWriter = new DatabaseReaderWriter(new ProjectManager(folder.Combine("appdata")));
                var chosen = folder.Combine("tree.rfPrj");

                var written = readerWriter.WriteDatabase(new ObservableCollection<RFiDChipParentLayerViewModel>(), chosen);

                Assert.Equal(folder.Combine("tree.xml"), written);
                Assert.False(File.Exists(chosen));
                Assert.StartsWith("<?xml", File.ReadAllText(written));
            }
        }

        [Fact]
        public void WriteChipTree_WhenTheTargetCannotBeReplaced_ThrowsInsteadOfEndingTheProcess()
        {
            using (var folder = new TemporaryFolder())
            {
                var readerWriter = new DatabaseReaderWriter(new ProjectManager(folder.Combine("appdata")));
                var target = folder.Combine("tree.xml");
                File.WriteAllText(target, "old content");

                using (new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    Assert.ThrowsAny<IOException>(
                        () => readerWriter.WriteDatabase(new ObservableCollection<RFiDChipParentLayerViewModel>(), target));
                }

                Assert.Equal("old content", File.ReadAllText(target));
                Assert.Equal(new[] { target }, folder.Files());
            }
        }

        private sealed class TemporaryFolder : IDisposable
        {
            private readonly string root = Path.Combine(Path.GetTempPath(), "rfidgear-save-" + Guid.NewGuid().ToString("N"));

            public TemporaryFolder()
            {
                Directory.CreateDirectory(root);
            }

            public string Combine(string name) => Path.Combine(root, name);

            /// <summary>The files directly in the folder (not in the application data subfolder), sorted.</summary>
            public string[] Files() =>
                Directory.GetFiles(root).OrderBy(file => file, StringComparer.Ordinal).ToArray();

            public void Dispose()
            {
                Directory.Delete(root, true);
            }
        }
    }
}
