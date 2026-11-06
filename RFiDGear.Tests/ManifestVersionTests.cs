using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using RFiDGear.Contracts;
using RFiDGear.Infrastructure.FileAccess;

using Xunit;

namespace RFiDGear.Tests
{
    /// <summary>
    /// The manifest version decides whether a project or settings file is too new to read. It used to compare
    /// the digits of <c>major.minor.build</c> run together, which rejected every project written by 1.10.x
    /// (1109054) as newer than 2.0.10 (2010) and accepted some genuinely newer files (2.1.0 became 210).
    /// </summary>
    [Collection("AppDataPath")]
    public sealed class ManifestVersionComparisonTests : IDisposable
    {
        private static readonly Version App = new(2, 0, 10, 0);

        // The manager creates its root folder on construction, so give it one this test can delete.
        private readonly string _root = Path.Combine(Path.GetTempPath(), "RFiDGear.Tests", Guid.NewGuid().ToString("N"));
        private readonly ProjectManager _manager;

        public ManifestVersionComparisonTests() => _manager = new ProjectManager(_root);

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }

        [Theory]
        [InlineData("1.10.9054", true)] // the real regression: older release, larger digit string
        [InlineData("1.9.9", true)]
        [InlineData("1.99.99999", true)]
        [InlineData("2.0.0", true)]
        [InlineData("2.0.9", true)]
        [InlineData("2.0.10", true)] // same release
        [InlineData("2.0.11", false)]
        [InlineData("2.1.0", false)] // old rule: 210 <= 2010 accepted a newer file
        [InlineData("2.10.0", false)]
        [InlineData("3.0.0", false)]
        [InlineData("99.0.0", false)]
        [InlineData("2.0", true)] // missing build counts as 0
        [InlineData("2.1", false)]
        [InlineData("2", true)] // a bare number keeps its legacy meaning (digits run together)
        [InlineData("2010", true)]
        [InlineData("2011", false)]
        [InlineData("1109054", false)]
        [InlineData("0", true)]
        public void ValidateManifestVersion_ComparesPartsInOrder(string manifest, bool expected)
        {
            Assert.Equal(expected, _manager.ValidateManifestVersion(manifest, App));
        }

        [Theory]
        [InlineData("1.10.0", "2.0.0.0", true)] // old rule: 1100 > 200
        [InlineData("1.10.0", "1.9.9.0", false)]
        [InlineData("1.9.9", "1.10.0.0", true)]
        [InlineData("2.0.1", "2.0", false)] // an app version without a build part counts as build 0
        [InlineData("2.0.0", "2.0", true)]
        public void ValidateManifestVersion_ComparesAgainstTheGivenAppVersion(string manifest, string app, bool expected)
        {
            Assert.Equal(expected, _manager.ValidateManifestVersion(manifest, Version.Parse(app)));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void ValidateManifestVersion_MissingManifest_IsSupported(string? manifest)
        {
            Assert.True(_manager.ValidateManifestVersion(manifest, App));
        }

        [Fact]
        public void ValidateManifestVersion_IgnoresSurroundingWhitespace()
        {
            Assert.True(_manager.ValidateManifestVersion("  1.10.9054\r\n", App));
            Assert.False(_manager.ValidateManifestVersion("\t2.1.0 ", App));
        }

        [Theory]
        [InlineData("abc")]
        [InlineData("1.x.3")]
        [InlineData("1..2")]
        [InlineData("1.2.")]
        [InlineData("-1.0.0")]
        [InlineData("1.2.3.4.5")]
        [InlineData("1,5.0")]
        public void ValidateManifestVersion_NotMadeOfNumbers_Throws(string manifest)
        {
            Assert.Throws<FormatException>(() => _manager.ValidateManifestVersion(manifest, App));
        }

        [Fact]
        public void ValidateManifestVersion_NumberTooLargeForAPart_Throws()
        {
            Assert.Throws<OverflowException>(() => _manager.ValidateManifestVersion("99999999999.0.0", App));
        }

        [Fact]
        public void ValidateManifestVersion_NullAppVersion_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => _manager.ValidateManifestVersion("1.0.0", null));
        }

#pragma warning disable CS0618 // the obsolete overload is pinned on purpose
        [Fact]
        public void ValidateManifestVersion_ObsoleteNumberOverload_StillComparesDigits()
        {
            Assert.True(_manager.ValidateManifestVersion(2010, App));
            Assert.False(_manager.ValidateManifestVersion(2011, App));
            Assert.False(_manager.ValidateManifestVersion(1109054, App));
        }
#pragma warning restore CS0618
    }

    /// <summary>
    /// The same decision, made on real files through the load pipeline and the settings loader.
    /// </summary>
    [Collection("AppDataPath")]
    public sealed class ManifestVersionLoadTests : IsolatedProjectTestBase
    {
        private static readonly Version AppVersion = typeof(DatabaseReaderWriter).Assembly.GetName().Version!;

        private static string OlderThanApp => $"{AppVersion.Major - 1}.10.9054";

        private static string NewerMinorThanApp => $"{AppVersion.Major}.{AppVersion.Minor + 1}.0";

        private static string SameAsApp => $"{AppVersion.Major}.{AppVersion.Minor}.{AppVersion.Build}";

        [Fact]
        public async Task LoadAsync_ProjectWrittenByAnOlderReleaseWithALongBuildNumber_Loads()
        {
            // 1.10.9054 is what the user's real projects carry; its digits run together exceed 2.0.10's.
            var file = Track(ProjectFixtures.CreateProject(3, manifestVersion: "1.10.9054"));
            var (host, _) = CreateIsolatedHost();

            var result = await LoadAsync(host, file);

            Assert.True(result.Success, $"LoadAsync failed: {result.Error}");
            Assert.Equal(3, host.TaskCount);
            Assert.Equal(Path.GetFullPath(file), host.CurrentPath);
        }

        [Fact]
        public async Task LoadAsync_PlainXmlProjectFromAnOlderRelease_Loads()
        {
            var file = Track(ProjectFixtures.CreatePlainXmlProject(2, manifestVersion: "1.10.9054"));
            var (host, _) = CreateIsolatedHost();

            var result = await LoadAsync(host, file);

            Assert.True(result.Success, $"LoadAsync failed: {result.Error}");
            Assert.Equal(2, host.TaskCount);
        }

        [Theory]
        [InlineData("older")]
        [InlineData("same")]
        public async Task LoadAsync_ManifestNotNewerThanTheRunningApp_Loads(string which)
        {
            var manifest = which == "older" ? OlderThanApp : SameAsApp;
            var file = Track(ProjectFixtures.CreateProject(1, manifestVersion: manifest));
            var (host, _) = CreateIsolatedHost();

            var result = await LoadAsync(host, file);

            Assert.True(result.Success, $"manifest {manifest} should load, got {result.Error}");
        }

        [Fact]
        public async Task LoadAsync_ManifestNewerInTheMinorPart_IsRejectedAndKeepsTheProject()
        {
            // Digits run together, 2.1.0 is 210 and was accepted by an app at 2.0.10 (2010).
            var good = Track(ProjectFixtures.CreateProject(2));
            var newer = Track(ProjectFixtures.CreateProject(1, manifestVersion: NewerMinorThanApp));
            var (host, _) = CreateIsolatedHost();
            Assert.True((await LoadAsync(host, good)).Success);
            var pathBefore = host.CurrentPath;

            var result = await LoadAsync(host, newer);

            Assert.False(result.Success);
            Assert.Equal(ProjectOpenError.UnsupportedFormat, result.Error);
            Assert.Equal(pathBefore, host.CurrentPath);
            Assert.Equal(2, host.TaskCount);
        }

        [Fact]
        public async Task ReadProjectAsync_ReportsTheSupportedVerdictAndTheDigitsForDiagnostics()
        {
            var file = Track(ProjectFixtures.CreateProject(1, manifestVersion: "1.10.9054"));
            var reader = new DatabaseReaderWriter(new ProjectManager(_root));

            var (read, model) = await reader.ReadProjectAsync(file);

            Assert.True(read.Success);
            Assert.True(read.IsSupportedVersion);
            Assert.Equal(DatabaseReaderWriter.DatabaseReadFailureReason.None, read.FailureReason);
            Assert.Equal(1109054, read.ManifestVersion);
            Assert.NotNull(model);
        }

        [Fact]
        public async Task LoadAsync_ManifestThatIsNotAVersion_FailsCleanlyWithoutLockingTheStagingFile()
        {
            var good = Track(ProjectFixtures.CreateProject(2));
            var garbled = Track(ProjectFixtures.CreateProject(1, manifestVersion: "not-a-version"));
            var (host, _) = CreateIsolatedHost();
            Assert.True((await LoadAsync(host, good)).Success);

            var result = await LoadAsync(host, garbled);

            Assert.False(result.Success);
            Assert.Equal(2, host.TaskCount);

            // A reader left open on the extracted file would make this delete (and the test's cleanup) fail.
            File.Delete(Path.Combine(_root, "taskdatabase.xml"));
        }

        [Theory]
        [InlineData("1.10.9054", true)]
        [InlineData("2.0.10", true)]
        [InlineData("2.1.0", false)]
        [InlineData("3.0.0", false)]
        public void LoadSettings_DecidesSupportFromTheManifestParts(string manifest, bool expectedSupported)
        {
            var manager = new ProjectManager(_root);
            var settingsPath = Path.Combine(_root, "settings-under-test.xml");
            manager.EnsureSettingsFileExists(settingsPath);
            var xml = File.ReadAllText(settingsPath);
            const string manifestElement = "<ManifestVersion>[^<]*</ManifestVersion>";
            // The default settings must carry a manifest, or rewriting it proves nothing. (Comparing the text
            // before and after would not do: the default is the running version, so one case rewrites it to itself.)
            Assert.Matches(manifestElement, xml);
            File.WriteAllText(settingsPath, Regex.Replace(xml, manifestElement, $"<ManifestVersion>{manifest}</ManifestVersion>"));

            var result = manager.LoadSettings(settingsPath, new Version(2, 0, 10, 0));

            Assert.True(result.Success);
            Assert.Equal(expectedSupported, result.IsSupportedVersion);
        }
    }
}
