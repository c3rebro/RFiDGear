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
            // A UTF-16 document declares utf-16, as the serializer wrote it; the fixture helper rewrites that to utf-8.
            var utf16Xml = Project(2).Replace("encoding=\"utf-8\"", "encoding=\"utf-16\"", StringComparison.Ordinal);
            Assert.Contains("encoding=\"utf-16\"", utf16Xml, StringComparison.Ordinal);
            var bytes = encoding == "utf8-bom"
                ? System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(Project(2))).ToArray()
                : System.Text.Encoding.Unicode.GetPreamble().Concat(System.Text.Encoding.Unicode.GetBytes(utf16Xml)).ToArray();
            var (host, _) = CreateIsolatedHost();
            var file = Track(ProjectFixtures.CreateArchiveWithRawEntry("taskdatabase.xml", bytes));

            var result = await LoadAsync(host, file);

            Assert.True(result.Success, $"LoadAsync failed: {result.Error}");
            Assert.Equal(2, result.TaskCount);
        }

        [Theory]
        [InlineData("comment")]
        [InlineData("text")]
        public async Task LoadAsync_ProjectEntryWithAnInvalidUtf8Byte_IsRejectedNotRepaired(string where)
        {
            // The byte-based XML check rejects it. Decoding to text first would turn the byte into U+FFFD and
            // load a project whose data is not what the file says.
            var xml = Project(2);
            var afterDeclaration = xml.IndexOf("?>", StringComparison.Ordinal) + 2;
            var afterRootStartTag = afterDeclaration + xml[afterDeclaration..].IndexOf('>', StringComparison.Ordinal) + 1;
            // A comment right after the declaration, or character data right after the root element's start tag.
            var splitAt = where == "comment" ? afterDeclaration : afterRootStartTag;
            var bytes = new List<byte>(System.Text.Encoding.UTF8.GetBytes(xml[..splitAt]));
            bytes.AddRange(where == "comment"
                ? new byte[] { 0x3C, 0x21, 0x2D, 0x2D, 0xFF, 0x2D, 0x2D, 0x3E }   // <!--[0xFF]-->
                : new byte[] { 0xFF });
            bytes.AddRange(System.Text.Encoding.UTF8.GetBytes(xml[splitAt..]));

            var (host, _) = CreateIsolatedHost();
            var good = Track(ProjectFixtures.CreateProject(1));
            Assert.True((await LoadAsync(host, good)).Success);
            var file = Track(ProjectFixtures.CreateArchiveWithRawEntry("taskdatabase.xml", bytes.ToArray()));

            var result = await LoadAsync(host, file);

            Assert.False(result.Success);
            Assert.Equal(ProjectOpenError.UnsupportedFormat, result.Error);
            Assert.Equal(1, host.TaskCount);
        }

        [Fact]
        public void PrepareProject_ArchiveDeclaringTooManyEntries_IsRejectedBeforeTheDirectoryIsRead()
        {
            var manager = new ProjectManager(_root);
            var file = Track(ProjectFixtures.CreateProject(1));
            var bytes = File.ReadAllBytes(file);
            var record = LastIndexOfEndOfCentralDirectory(bytes);
            // Claim 5000 entries on this disk and in total; the directory itself still holds one.
            bytes[record + 8] = bytes[record + 10] = 0x88;
            bytes[record + 9] = bytes[record + 11] = 0x13;
            File.WriteAllBytes(file, bytes);

            var exception = Assert.Throws<InvalidDataException>(
                () => manager.PrepareProject(new FileInfo(file), new Version(2, 0, 10)));

            // Our own message: the library's complaint about a count that does not match comes later.
            Assert.Contains("more entries", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void PrepareProject_ArchiveUnderstatingItsEntryCount_IsNotAccepted()
        {
            // The end record says 2 entries, the directory holds 7: the declared count cannot be trusted alone.
            var manager = new ProjectManager(_root) { MaxArchiveEntries = 5 };
            var file = Track(ProjectFixtures.CreateArchiveWithEntries(EntriesWithProjectLast(count: 7)));
            var bytes = File.ReadAllBytes(file);
            var record = LastIndexOfEndOfCentralDirectory(bytes);
            bytes[record + 8] = bytes[record + 10] = 2;
            bytes[record + 9] = bytes[record + 11] = 0;
            File.WriteAllBytes(file, bytes);

            Assert.Throws<InvalidDataException>(() => manager.PrepareProject(new FileInfo(file), new Version(2, 0, 10)));
        }

        [Fact]
        public void ReadDeclaredEntryCount_ReadsTheCountFromTheEndOfTheFile()
        {
            var three = Track(ProjectFixtures.CreateArchiveWithEntries(("a.txt", "a"), ("b.txt", "b"), ("taskdatabase.xml", Project(1))));

            Assert.Equal(3, ProjectManager.ReadDeclaredEntryCount(three));
        }

        [Fact]
        public void ReadDeclaredEntryCount_FindsTheRecordBehindATrailingComment()
        {
            var file = Track(ProjectFixtures.CreateProject(1));
            using (var stream = new FileStream(file, FileMode.Append, System.IO.FileAccess.Write))
            {
                stream.Write(System.Text.Encoding.ASCII.GetBytes(new string('x', 300)));
            }

            Assert.Equal(1, ProjectManager.ReadDeclaredEntryCount(file));
        }

        [Fact]
        public void ReadDeclaredEntryCount_WhenThereIsNoRecord_ReturnsNull()
        {
            var notAnArchive = Track(ProjectFixtures.CreateNotAnArchive());
            var empty = Track(ProjectFixtures.CreateEmptyFile(".rfPrj"));

            Assert.Null(ProjectManager.ReadDeclaredEntryCount(notAnArchive));
            Assert.Null(ProjectManager.ReadDeclaredEntryCount(empty));
        }

        private static int LastIndexOfEndOfCentralDirectory(byte[] zip)
        {
            for (var i = zip.Length - 22; i >= 0; i--)
            {
                if (zip[i] == 0x50 && zip[i + 1] == 0x4B && zip[i + 2] == 0x05 && zip[i + 3] == 0x06)
                {
                    return i;
                }
            }

            throw new InvalidOperationException("The fixture has no end-of-central-directory record.");
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
