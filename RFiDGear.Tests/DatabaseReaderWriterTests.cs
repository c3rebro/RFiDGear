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
using System.Windows.Threading;
using RFiDGear.ViewModel;
using Xunit;

namespace RFiDGear.Tests
{
    public class DatabaseReaderWriterTests
    {
        [Fact]
        public async Task ExecuteOnStaThread_WhenApplicationIsNull_UsesStaThread()
        {
            // An isolated root: creating a DatabaseReaderWriter deletes the working files (taskdatabase.xml, *.rfPrj, *.tmp)
            // in the folder of its ProjectManager, and the default one is %LocalAppData%\RFiDGear of the person who runs this.
            using var folder = new TemporaryFolder();
            var readerWriter = new DatabaseReaderWriter(new ProjectManager(folder.Combine("appdata")));
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

        // Objects made on that thread can register with its dispatcher (the weak event table of WPF does). A thread that
        // ends with its dispatcher running leaves the registration to process exit, where WPF asks every such dispatcher to
        // run a clean-up and waits for the answer of a thread that is gone, once per thread: about half a second each in the
        // tests that read projects, and a test process that did not end within the ten seconds xunit.v3 allows.
        [Fact]
        public async Task ExecuteOnStaThread_WhenApplicationIsNull_ShutsDownTheDispatcherItsThreadGotUsed()
        {
            using var folder = new TemporaryFolder();
            var readerWriter = new DatabaseReaderWriter(new ProjectManager(folder.Combine("appdata")));
            var method = typeof(DatabaseReaderWriter).GetMethod(
                "ExecuteOnStaThread",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(method);

            Dispatcher dispatcherOfTheThread = null;

            await Task.Run(() =>
            {
                method.Invoke(readerWriter, new object[]
                {
                    (Action)(() => dispatcherOfTheThread = Dispatcher.CurrentDispatcher)
                });
            });

            Assert.NotNull(dispatcherOfTheThread);
            Assert.True(dispatcherOfTheThread.HasShutdownFinished);
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

        [Theory]
        [InlineData("project.rfPrj")]
        [InlineData("project.xml")]
        public void WriteProject_WithoutReplacing_KeepsAFileThatExistsAndLeavesNoStagingFiles(string name)
        {
            using (var folder = new TemporaryFolder())
            {
                var readerWriter = new DatabaseReaderWriter(new ProjectManager(folder.Combine("appdata")));
                var target = folder.Combine(name);
                File.WriteAllText(target, "old content");

                Assert.ThrowsAny<IOException>(() => readerWriter.WriteDatabase(new ChipTaskHandlerModel(), target, replaceExisting: false));

                Assert.Equal("old content", File.ReadAllText(target));
                Assert.Equal(new[] { target }, folder.Files());
            }
        }

        [Fact]
        public void WriteProject_WithoutReplacing_StillCreatesANewFile()
        {
            using (var folder = new TemporaryFolder())
            {
                var readerWriter = new DatabaseReaderWriter(new ProjectManager(folder.Combine("appdata")));
                var target = folder.Combine("project.rfPrj");

                var written = readerWriter.WriteDatabase(new ChipTaskHandlerModel(), target, replaceExisting: false);

                Assert.Equal(target, written);
                using (var archive = ZipFile.OpenRead(target))
                {
                    Assert.Contains(archive.Entries, entry => entry.Name == "taskdatabase.xml");
                }

                Assert.Equal(new[] { target }, folder.Files());
            }
        }

        [Fact]
        public void WriteProject_LeavesNoCopyOfTheProjectInTheApplicationDataFolder()
        {
            using (var folder = new TemporaryFolder())
            {
                var appData = folder.Combine("appdata");
                var projectManager = new ProjectManager(appData);
                var readerWriter = new DatabaseReaderWriter(projectManager);
                var before = Directory.GetFiles(appData).OrderBy(file => file, StringComparer.Ordinal).ToArray();

                readerWriter.WriteDatabase(new ChipTaskHandlerModel(), folder.Combine("project.rfPrj"));

                // The project used to be written there first and copied into the archive from there, which left the keys of
                // every task in plaintext in the profile until the next start.
                Assert.False(File.Exists(projectManager.TaskDatabasePath));
                Assert.Equal(before, Directory.GetFiles(appData).OrderBy(file => file, StringComparer.Ordinal).ToArray());
            }
        }

        [Fact]
        public void OpeningTheDatabase_RemovesTheCopyOfAProjectThatAnEarlierVersionLeftBehind()
        {
            using (var folder = new TemporaryFolder())
            {
                var appData = folder.Combine("appdata");
                Directory.CreateDirectory(appData);
                var projectManager = new ProjectManager(appData);
                File.WriteAllText(projectManager.TaskDatabasePath, "<ChipTaskHandlerModel>an earlier version wrote this</ChipTaskHandlerModel>");

                _ = new DatabaseReaderWriter(projectManager);

                Assert.False(File.Exists(projectManager.TaskDatabasePath));
            }
        }

        [Fact]
        public void WriteProject_StillPutsTheProjectIntoTheArchiveAsTheEntryThatIsReadBack()
        {
            using (var folder = new TemporaryFolder())
            {
                var readerWriter = new DatabaseReaderWriter(new ProjectManager(folder.Combine("appdata")));
                var target = folder.Combine("project.rfPrj");
                var model = new ChipTaskHandlerModel { ManifestVersion = "2.0.10" };

                readerWriter.WriteDatabase(model, target);

                using (var archive = ZipFile.OpenRead(target))
                {
                    var entry = Assert.Single(archive.Entries);
                    Assert.Equal("taskdatabase.xml", entry.Name);

                    using (var reader = new StreamReader(entry.Open()))
                    {
                        Assert.Contains("<ManifestVersion>2.0.10</ManifestVersion>", reader.ReadToEnd(), StringComparison.Ordinal);
                    }
                }
            }
        }

        [Fact]
        public void WriteProject_LeavesAFileNextToTheTargetAlone()
        {
            using (var folder = new TemporaryFolder())
            {
                var readerWriter = new DatabaseReaderWriter(new ProjectManager(folder.Combine("appdata")));
                var target = folder.Combine("project.rfPrj");
                var neighbour = folder.Combine("project.rfPrj.bak");
                File.WriteAllText(target, "old content");
                File.WriteAllText(neighbour, "somebody's backup");

                readerWriter.WriteDatabase(new ChipTaskHandlerModel(), target);

                Assert.Equal("somebody's backup", File.ReadAllText(neighbour));
                Assert.Equal(new[] { target, neighbour }, folder.Files());
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

        [Theory]
        [InlineData(@"C:\jobs\job.rfPrj", @"C:\jobs\job.rfPrj")]
        [InlineData(@"C:\jobs\job.RFPRJ", @"C:\jobs\job.RFPRJ")]
        [InlineData(@"C:\jobs\job.xml", @"C:\jobs\job.xml")]
        [InlineData(@"C:\jobs\job", @"C:\jobs\job.rfPrj")]
        [InlineData(@"C:\jobs\job.", @"C:\jobs\job.rfPrj")]
        [InlineData(@"C:\jobs\job.txt", @"C:\jobs\job.txt.rfPrj")]
        [InlineData(@"C:\jobs\job.rfprj.bak", @"C:\jobs\job.rfprj.bak.rfPrj")]
        public void ProjectSavePath_KeepsOnlyProjectExtensions(string chosen, string expected)
        {
            Assert.Equal(expected, DatabaseReaderWriter.ProjectSavePath(chosen));
        }

        [Fact]
        public void WriteProject_WithAnUnknownExtension_WritesAnArchiveThatTheOpenerRecognises()
        {
            using (var folder = new TemporaryFolder())
            {
                var projectManager = new ProjectManager(folder.Combine("appdata"));
                var readerWriter = new DatabaseReaderWriter(projectManager);

                var written = readerWriter.WriteDatabase(new ChipTaskHandlerModel(), folder.Combine("job.txt"));

                Assert.Equal(folder.Combine("job.txt.rfPrj"), written);
                Assert.Equal(ProjectFileType.Archive, projectManager.GetProjectFileType(new FileInfo(written)));
                Assert.False(File.Exists(folder.Combine("job.txt")));
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
