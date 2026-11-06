using RFiDGear.Contracts;
using RFiDGear.Infrastructure.FileAccess;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Xunit;

namespace RFiDGear.Tests
{
    /// <summary>
    /// Issue 217: opening a <c>.rfPrj</c> reads the one project entry in memory. Nothing else in the archive is
    /// written anywhere, so a crafted archive cannot replace files the application trusts.
    /// </summary>
    [Collection("AppDataPath")]
    public sealed class ProjectArchiveReadTests : IsolatedProjectTestBase
    {
        private const string TrustedSettings = "<settings>trusted</settings>";

        /// <summary>Every file and folder below <paramref name="root"/>, relative and sorted, for before/after comparison.</summary>
        internal static string[] EntriesBelow(string root) =>
            Directory.GetFileSystemEntries(root, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(root, path))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();

        private static string Project(int taskCount) => ProjectFixtures.SerializeProject(taskCount, manifestVersion: null);

        [Fact]
        public async Task LoadAsync_ArchiveWithASettingsEntry_LeavesTheTrustedSettingsFileAlone()
        {
            var (host, manager) = CreateIsolatedHost();
            await File.WriteAllTextAsync(manager.SettingsPath, TrustedSettings);
            var file = Track(ProjectFixtures.CreateArchiveWithEntries(
                ("taskdatabase.xml", Project(2)),
                ("settings.xml", "<settings>replaced by the archive</settings>")));

            var result = await LoadAsync(host, file);

            Assert.True(result.Success, $"LoadAsync failed: {result.Error}");
            Assert.Equal(2, result.TaskCount);
            Assert.Equal(TrustedSettings, await File.ReadAllTextAsync(manager.SettingsPath));
        }

        [Fact]
        public async Task LoadAsync_ArchiveWithAReportTemplateEntry_WritesNoTemplateCopy()
        {
            var (host, manager) = CreateIsolatedHost();
            var file = Track(ProjectFixtures.CreateArchiveWithEntries(
                ("taskdatabase.xml", Project(1)),
                ("temptemplate.pdf", "not really a template")));

            Assert.True((await LoadAsync(host, file)).Success);

            Assert.False(File.Exists(manager.ReportTemplateTempPath));
        }

        [Theory]
        [InlineData("..\\escaped-217-backslash.txt")]
        [InlineData("../escaped-217-slash.txt")]
        [InlineData("sub/../../escaped-217-nested.txt")]
        [InlineData("nested/folder/payload-217.txt")]
        public async Task LoadAsync_ArchiveWithOtherEntries_WritesNothingToDisk(string hostileName)
        {
            var (host, _) = CreateIsolatedHost();
            var before = EntriesBelow(_root);
            // Where an extractor that honours the name would have put it, inside or outside the root.
            var wouldHaveBeenWritten = Path.GetFullPath(Path.Combine(_root, hostileName));
            Assert.False(File.Exists(wouldHaveBeenWritten));
            var file = Track(ProjectFixtures.CreateArchiveWithEntries(
                ("taskdatabase.xml", Project(1)),
                (hostileName, "should never reach the disk")));

            var result = await LoadAsync(host, file);

            Assert.True(result.Success, $"LoadAsync failed: {result.Error}");
            Assert.False(File.Exists(wouldHaveBeenWritten), $"{wouldHaveBeenWritten} was written");
            Assert.Equal(before, EntriesBelow(_root));
        }

        [Fact]
        public async Task LoadAsync_ArchiveWithAnAbsolutePathEntry_WritesNothingToThatPath()
        {
            var target = Path.Combine(NewScratchDirectory(), "absolute-217.txt");
            var (host, _) = CreateIsolatedHost();
            var file = Track(ProjectFixtures.CreateArchiveWithEntries(
                ("taskdatabase.xml", Project(1)),
                (target, "should never reach the disk")));

            var result = await LoadAsync(host, file);

            Assert.True(result.Success, $"LoadAsync failed: {result.Error}");
            Assert.False(File.Exists(target));
        }

        [Fact]
        public async Task LoadAsync_ProjectInASubfolderEntry_IsNotTheProject()
        {
            var (host, _) = CreateIsolatedHost();
            var file = Track(ProjectFixtures.CreateArchiveWithEntries(("backup/taskdatabase.xml", Project(3))));

            var result = await LoadAsync(host, file);

            Assert.False(result.Success);
            Assert.Equal(ProjectOpenError.UnsupportedFormat, result.Error);
        }

        [Fact]
        public async Task LoadAsync_OldestLayoutWithTheProjectNamedLikeTheArchive_StillLoads()
        {
            var (host, _) = CreateIsolatedHost();
            var file = Track(ProjectFixtures.CreateArchiveAt(
                NewScratchDirectory(), "legacy-217.rfPrj", entryName: "LEGACY-217.rfPrj", Project(4)));

            var result = await LoadAsync(host, file);

            Assert.True(result.Success, $"LoadAsync failed: {result.Error}");
            Assert.Equal(4, result.TaskCount);
        }

        [Theory]
        [InlineData("utf8-bom")]
        [InlineData("utf16-bom")]
        public async Task LoadAsync_ProjectEntryWithAByteOrderMark_StillLoads(string encoding)
        {
            // Older releases did not always write UTF-8 without a mark; the file-based reader coped, so this must too.
            var bytes = encoding == "utf8-bom"
                ? System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(Project(2))).ToArray()
                : System.Text.Encoding.Unicode.GetPreamble().Concat(System.Text.Encoding.Unicode.GetBytes(Project(2))).ToArray();
            var (host, _) = CreateIsolatedHost();
            var file = Track(ProjectFixtures.CreateArchiveWithRawEntry("taskdatabase.xml", bytes));

            var result = await LoadAsync(host, file);

            Assert.True(result.Success, $"LoadAsync failed: {result.Error}");
            Assert.Equal(2, result.TaskCount);
        }

        [Fact]
        public async Task LoadAsync_ArchiveWithAsManyEntriesAsAllowed_Loads()
        {
            var (host, manager) = CreateIsolatedHost();
            manager.MaxArchiveEntries = 5;
            var file = Track(ProjectFixtures.CreateArchiveWithEntries(EntriesWithProjectLast(count: 5)));

            var result = await LoadAsync(host, file);

            Assert.True(result.Success, $"LoadAsync failed: {result.Error}");
        }

        [Fact]
        public async Task LoadAsync_ArchiveWithOneEntryTooMany_IsRejectedAndKeepsTheProject()
        {
            var (host, manager) = CreateIsolatedHost();
            var good = Track(ProjectFixtures.CreateProject(2));
            Assert.True((await LoadAsync(host, good)).Success);
            var pathBefore = host.CurrentPath;
            manager.MaxArchiveEntries = 5;
            var file = Track(ProjectFixtures.CreateArchiveWithEntries(EntriesWithProjectLast(count: 6)));

            var result = await LoadAsync(host, file);

            Assert.False(result.Success);
            Assert.Equal(ProjectOpenError.UnsupportedFormat, result.Error);
            Assert.Equal(pathBefore, host.CurrentPath);
            Assert.Equal(2, host.TaskCount);
        }

        [Fact]
        public async Task LoadAsync_ProjectEntryExactlyAtTheSizeLimit_Loads()
        {
            var (host, manager) = CreateIsolatedHost();
            var file = Track(ProjectFixtures.CreateProject(3));
            manager.MaxProjectEntryBytes = ProjectEntrySize(file);

            var result = await LoadAsync(host, file);

            Assert.True(result.Success, $"LoadAsync failed: {result.Error}");
        }

        [Fact]
        public async Task LoadAsync_ProjectEntryOneByteOverTheSizeLimit_IsRejectedAndKeepsTheProject()
        {
            var (host, manager) = CreateIsolatedHost();
            var good = Track(ProjectFixtures.CreateProject(2));
            Assert.True((await LoadAsync(host, good)).Success);
            var pathBefore = host.CurrentPath;
            var file = Track(ProjectFixtures.CreateProject(3));
            manager.MaxProjectEntryBytes = ProjectEntrySize(file) - 1;

            var result = await LoadAsync(host, file);

            Assert.False(result.Success);
            Assert.Equal(ProjectOpenError.UnsupportedFormat, result.Error);
            Assert.Equal(pathBefore, host.CurrentPath);
            Assert.Equal(2, host.TaskCount);
        }

        [Fact]
        public async Task LoadAsync_ProjectWithADocumentTypeDeclaration_IsRefused()
        {
            // A small entity expansion: harmless in size, but a document type declaration has no place in a project.
            var withDoctype = Project(1).Replace(
                "<ChipTaskHandlerModel",
                "<!DOCTYPE ChipTaskHandlerModel [<!ENTITY a \"aaaaaaaaaa\"><!ENTITY b \"&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;\">]><ChipTaskHandlerModel",
                StringComparison.Ordinal);
            Assert.Contains("<!DOCTYPE", withDoctype, StringComparison.Ordinal);
            var (host, _) = CreateIsolatedHost();
            var file = Track(ProjectFixtures.CreateArchiveWithEntries(("taskdatabase.xml", withDoctype)));

            var result = await LoadAsync(host, file);

            Assert.False(result.Success);
            Assert.Equal(ProjectOpenError.UnsupportedFormat, result.Error);
        }

        [Fact]
        public async Task ReadProjectAsync_ParallelLoadsOfDifferentArchives_EachGetsItsOwnProject()
        {
            // Loads used to share one staging file in the root; now they share nothing.
            var manager = new ProjectManager(_root);
            var counts = Enumerable.Range(1, 8).ToArray();
            var files = counts.Select(count => Track(ProjectFixtures.CreateProject(count))).ToArray();

            var results = await Task.WhenAll(files.Select(async file =>
            {
                var reader = new DatabaseReaderWriter(manager);
                var (read, model) = await reader.ReadProjectAsync(file);
                return (read.Success, Count: model?.TaskCollection.Count ?? -1);
            }));

            Assert.All(results, r => Assert.True(r.Success));
            Assert.Equal(counts, results.Select(r => r.Count).ToArray());
        }

        [Fact]
        public async Task ReadProjectAsync_AfterALoad_TheRootHoldsNoProjectPayload()
        {
            var manager = new ProjectManager(_root);
            var file = Track(ProjectFixtures.CreateProject(1));
            var before = EntriesBelow(_root);

            var (read, _) = await new DatabaseReaderWriter(manager).ReadProjectAsync(file);

            Assert.True(read.Success);
            var created = EntriesBelow(_root).Except(before).ToArray();
            Assert.DoesNotContain(created, name => name.EndsWith("taskdatabase.xml", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(created, name => name.EndsWith(".rfprj", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>The uncompressed size of the project entry the archive really holds.</summary>
        private static long ProjectEntrySize(string archivePath)
        {
            using var archive = System.IO.Compression.ZipFile.OpenRead(archivePath);

            return archive.GetEntry("taskdatabase.xml")!.Length;
        }

        /// <summary>A valid project as the last of <paramref name="count"/> entries, so the limit applies before it is reached.</summary>
        private static (string Name, string Content)[] EntriesWithProjectLast(int count)
        {
            var entries = new List<(string, string)>();

            for (var i = 0; i < count - 1; i++)
            {
                entries.Add(($"extra-{i}.txt", "filler"));
            }

            entries.Add(("taskdatabase.xml", Project(1)));

            return entries.ToArray();
        }
    }
}
