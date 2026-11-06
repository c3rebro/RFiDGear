using RFiDGear.Contracts;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.ExtensionHost;
using RFiDGear.Infrastructure.FileAccess;
using RFiDGear.Infrastructure.Tasks;
using RFiDGear.Infrastructure.Tasks.Interfaces;
using RFiDGear.Models;
using RFiDGear.Services.TaskExecution;
using RFiDGear.ViewModel.TaskSetupViewModels;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;

using Xunit;

namespace RFiDGear.Tests
{
    // ── AppContextImpl ────────────────────────────────────────────────────────

    public class AppContextImplTests
    {
        [Fact]
        public void DefaultCtor_UICultureName_ReturnsOsCulture()
        {
            var ctx = new AppContextImpl();
            Assert.Equal(System.Globalization.CultureInfo.CurrentUICulture.Name, ctx.UICultureName);
        }

        [Fact]
        public void WithGermanDelegate_UICultureName_ReturnsDeDe()
        {
            var ctx = new AppContextImpl(() => "german");
            Assert.Equal("de-DE", ctx.UICultureName);
        }

        [Fact]
        public void WithEnglishDelegate_UICultureName_ReturnsEnUs()
        {
            var ctx = new AppContextImpl(() => "english");
            Assert.Equal("en-US", ctx.UICultureName);
        }

        [Fact]
        public void WithNullReturnFromDelegate_UICultureName_FallsBackToOsCulture()
        {
            var ctx = new AppContextImpl(() => null);
            Assert.Equal(System.Globalization.CultureInfo.CurrentUICulture.Name, ctx.UICultureName);
        }

        [Fact]
        public void WithUnknownLanguage_UICultureName_ReturnsEnUs()
        {
            var ctx = new AppContextImpl(() => "french");
            Assert.Equal("en-US", ctx.UICultureName);
        }

        [Fact]
        public void RFiDGearVersion_IsNotEmpty()
        {
            var ctx = new AppContextImpl();
            Assert.False(string.IsNullOrWhiteSpace(ctx.RFiDGearVersion));
        }
    }

    // ── ExecutionRequest ─────────────────────────────────────────────────────

    public class ExecutionRequestTests
    {
        [Fact]
        public void DefaultCtor_Variables_IsReadOnly()
        {
            var req = ExecutionRequest.Empty;
            Assert.IsType<ReadOnlyDictionary<string, string>>(req.Variables);
        }

        [Fact]
        public void CollectionCtor_Variables_IsReadOnly()
        {
            var req = new ExecutionRequest(
                new[] { new KeyValuePair<string, string>("KEY", "val") });
            Assert.IsType<ReadOnlyDictionary<string, string>>(req.Variables);
        }

        [Fact]
        public void CollectionCtor_Variables_Mutation_Throws()
        {
            var req = new ExecutionRequest(
                new[] { new KeyValuePair<string, string>("KEY", "val") });
            var mutable = req.Variables as IDictionary<string, string>;
            Assert.NotNull(mutable);
            Assert.Throws<NotSupportedException>(() => mutable!["NEW"] = "x");
        }

        [Fact]
        public void CollectionCtor_LookupIsCaseInsensitive()
        {
            var req = new ExecutionRequest(
                new[] { new KeyValuePair<string, string>("ReportTargetPath", "C:\\out") });
            Assert.True(req.Variables.ContainsKey("reporttargetpath"));
            Assert.True(req.Variables.ContainsKey("REPORTTARGETPATH"));
        }

        [Fact]
        public void NullCollection_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                _ = new ExecutionRequest(null!));
        }
    }

    // ── TaskOutcome ───────────────────────────────────────────────────────────

    public class TaskOutcomeTests
    {
        [Fact]
        public void SkipReason_DefaultsToNull()
        {
            var outcome = new TaskOutcome(0, "0", "SomeTask", true, "NoError");
            Assert.Null(outcome.SkipReason);
        }

        [Fact]
        public void SkipReason_CanBeProvided()
        {
            var outcome = new TaskOutcome(1, "1", "SomeTask", null, "NoError", "NotStarted");
            Assert.Equal("NotStarted", outcome.SkipReason);
        }

        [Fact]
        public void IsSuccess_NullWithSkipReason_IndicatesNotExecuted()
        {
            var outcome = new TaskOutcome(0, "0", "SomeTask", null, "NoError", "NotStarted");
            Assert.Null(outcome.IsSuccess);
            Assert.Equal("NotStarted", outcome.SkipReason);
        }
    }

    // ── ExecResult ────────────────────────────────────────────────────────────

    public class ExecResultTests
    {
        [Fact]
        public void FailureReason_DefaultsToNone()
        {
            var now = DateTimeOffset.UtcNow;
            var result = new ExecResult("run", ExecutionOutcome.Success, 0, 0, now, now, Array.Empty<TaskOutcome>());

            Assert.Equal(ExecutionFailureReason.None, result.FailureReason);
        }
    }

    // ── ProjectHostImpl (headless) ────────────────────────────────────────────

    /// <summary>
    /// Serialises tests that call DatabaseReaderWriter (which extracts archives into the shared
    /// %LocalAppData%\RFiDGear directory) so that concurrent ResetTemporaryArtifacts() calls
    /// cannot delete files another test is in the middle of reading.
    /// </summary>
    [CollectionDefinition("AppDataPath", DisableParallelization = true)]
    public class AppDataPathCollection { }

    [Collection("AppDataPath")]
    public class ProjectHostImplTests
    {
        private static (ProjectHostImpl project, ExtensionOperationCoordinator coordinator) CreatePair()
        {
            var coordinator = new ExtensionOperationCoordinator();
            return (new ProjectHostImpl(new DatabaseReaderWriter(), coordinator), coordinator);
        }

        [Fact]
        public async Task LoadAsync_NullPath_ReturnsPathNotFound()
        {
            var (host, _) = CreatePair();
            var result = await host.LoadAsync(null!);
            Assert.False(result.Success);
            Assert.Equal(ProjectOpenError.PathNotFound, result.Error);
        }

        [Fact]
        public async Task LoadAsync_WhitespacePath_ReturnsPathNotFound()
        {
            var (host, _) = CreatePair();
            var result = await host.LoadAsync("   ");
            Assert.False(result.Success);
            Assert.Equal(ProjectOpenError.PathNotFound, result.Error);
        }

        [Fact]
        public async Task LoadAsync_NonExistentFile_ReturnsPathNotFound()
        {
            var (host, _) = CreatePair();
            var result = await host.LoadAsync(@"C:\this\path\does\not\exist.rfPrj");
            Assert.False(result.Success);
            Assert.Equal(ProjectOpenError.PathNotFound, result.Error);
        }

        [Fact]
        public async Task LoadAsync_WhileExecutionRunning_ReturnsBusy()
        {
            // File must exist: LoadAsync checks File.Exists before the coordinator gate.
            var tmp = Path.GetTempFileName();
            try
            {
                var coordinator = new ExtensionOperationCoordinator();
                Assert.True(coordinator.TryBeginExecution());
                var host = new ProjectHostImpl(new DatabaseReaderWriter(), coordinator);

                var result = await host.LoadAsync(tmp);
                Assert.False(result.Success);
                Assert.Equal(ProjectOpenError.Busy, result.Error);
            }
            finally
            {
                File.Delete(tmp);
            }
        }

        [Fact]
        public async Task LoadAsync_WhileLoadInProgress_ReturnsBusy()
        {
            var tmp = Path.GetTempFileName();
            try
            {
                var coordinator = new ExtensionOperationCoordinator();
                Assert.True(coordinator.TryBeginLoad());
                var host = new ProjectHostImpl(new DatabaseReaderWriter(), coordinator);

                var result = await host.LoadAsync(tmp);
                Assert.False(result.Success);
                Assert.Equal(ProjectOpenError.Busy, result.Error);
            }
            finally
            {
                File.Delete(tmp);
            }
        }

        [Fact]
        public async Task LoadAsync_UnknownExtension_ReturnsUnsupportedFormatWithoutSettingCurrentPath()
        {
            // GetTempFileName() yields a ".tmp" file, which is rejected on its extension alone.
            var tmp = Path.GetTempFileName();
            try
            {
                await File.WriteAllTextAsync(tmp, "this is not xml");
                var (host, _) = CreatePair();

                ProjectOpenResult result = null!;
                await StaTestRunner.RunOnStaThreadAsync(async () =>
                {
                    result = await host.LoadAsync(tmp);
                });

                Assert.False(result.Success);
                Assert.Equal(ProjectOpenError.UnsupportedFormat, result.Error);
                Assert.Null(host.CurrentPath);
            }
            finally
            {
                File.Delete(tmp);
            }
        }

        [Fact]
        public async Task LoadAsync_ValidFixture_ReturnsSuccessAndSetsCurrentPath()
        {
            var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "minimal.rfPrj");
            Assert.True(File.Exists(fixturePath), $"Test fixture not found: {fixturePath}");

            var (host, _) = CreatePair();

            ProjectOpenResult result = null!;
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                result = await host.LoadAsync(fixturePath);
            });

            Assert.True(result.Success, $"LoadAsync failed: {result.Error}");
            Assert.NotNull(host.CurrentPath);
            Assert.Equal(Path.GetFullPath(fixturePath), host.CurrentPath);
            Assert.Equal(0, result.TaskCount);
        }

        [Fact]
        public async Task LoadAsync_ValidFixture_FiresProjectChangedAndCommitsModel()
        {
            var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "minimal.rfPrj");
            Assert.True(File.Exists(fixturePath), $"Test fixture not found: {fixturePath}");

            var (host, _) = CreatePair();
            bool fired = false;
            host.ProjectChanged += (_, _) => fired = true;

            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                await host.LoadAsync(fixturePath);
            });

            Assert.True(fired, "ProjectChanged must fire after a successful load.");
            // SetupModel was committed — TaskCount reflects the (empty) fixture collection.
            Assert.Equal(0, host.TaskCount);
        }

        [Fact]
        public async Task LoadAsync_FailedLoad_PreservesExistingProject()
        {
            var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "minimal.rfPrj");
            Assert.True(File.Exists(fixturePath), $"Test fixture not found: {fixturePath}");

            var (host, _) = CreatePair();

            // First: load a valid project.
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var ok = await host.LoadAsync(fixturePath);
                Assert.True(ok.Success);
            });

            var pathAfterGoodLoad = host.CurrentPath;
            Assert.NotNull(pathAfterGoodLoad);

            // Second: attempt to load a corrupt file.
            var tmp = Path.GetTempFileName();
            try
            {
                await File.WriteAllTextAsync(tmp, "not valid xml or rfPrj");
                ProjectOpenResult bad = null!;
                await StaTestRunner.RunOnStaThreadAsync(async () =>
                {
                    bad = await host.LoadAsync(tmp);
                });

                Assert.False(bad.Success);
                // CurrentPath must still point to the previously loaded valid project.
                Assert.Equal(pathAfterGoodLoad, host.CurrentPath);
            }
            finally
            {
                File.Delete(tmp);
            }
        }

        [Fact]
        public async Task LoadAsync_WellFormedArchiveWithInvalidProjectXml_PreservesExistingProject()
        {
            var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "minimal.rfPrj");
            Assert.True(File.Exists(fixturePath), $"Test fixture not found: {fixturePath}");

            var (host, _) = CreatePair();
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var initial = await host.LoadAsync(fixturePath);
                Assert.True(initial.Success);
            });
            host.ReplaceTaskCollection(new object[] { new StubTask() });

            var originalPath = host.CurrentPath;
            var malformedPath = Path.Combine(Path.GetTempPath(), $"rfidgear-malformed-{Guid.NewGuid():N}.rfPrj");
            try
            {
                using (var archive = ZipFile.Open(malformedPath, ZipArchiveMode.Create))
                {
                    var entry = archive.CreateEntry("taskdatabase.xml");
                    using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
                    writer.Write("<?xml version=\"1.0\" encoding=\"utf-8\"?><InvalidProject><ManifestVersion>0</ManifestVersion></InvalidProject>");
                }

                ProjectOpenResult result = null!;
                await StaTestRunner.RunOnStaThreadAsync(async () =>
                {
                    result = await host.LoadAsync(malformedPath);
                });

                Assert.False(result.Success);
                Assert.Equal(ProjectOpenError.UnsupportedFormat, result.Error);
                Assert.Equal(originalPath, host.CurrentPath);
                Assert.Equal(1, host.TaskCount);
            }
            finally
            {
                File.Delete(malformedPath);
            }
        }

        [Fact]
        public async Task LoadAsync_CancellationBeforeRead_ThrowsOperationCanceled()
        {
            var tmp = Path.GetTempFileName();
            try
            {
                await File.WriteAllTextAsync(tmp, "data");
                var (host, _) = CreatePair();
                var cts = new CancellationTokenSource();
                cts.Cancel();
                await Assert.ThrowsAsync<OperationCanceledException>(
                    () => host.LoadAsync(tmp, cts.Token));
            }
            finally
            {
                File.Delete(tmp);
            }
        }

        [Fact]
        public async Task LoadAsync_Failure_DoesNotFireProjectChanged()
        {
            var (host, _) = CreatePair();
            bool fired = false;
            host.ProjectChanged += (_, _) => fired = true;

            await host.LoadAsync("no-such-file.rfPrj");

            Assert.False(fired);
        }

        /// <summary>Loads through an STA dispatcher thread, the way a WPF caller would.</summary>
        internal static async Task<ProjectOpenResult> LoadOnStaAsync(ProjectHostImpl host, string path)
        {
            ProjectOpenResult result = null!;
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                result = await host.LoadAsync(path);
            });
            return result;
        }

        [Fact]
        public async Task LoadAsync_NotAZipArchive_ReturnsUnsupportedFormatAndPreservesProject()
        {
            var good = ProjectFixtures.CreateProject(2);
            var bad = ProjectFixtures.CreateNotAnArchive();
            try
            {
                var (host, _) = CreatePair();
                Assert.True((await LoadOnStaAsync(host, good)).Success);
                var pathBefore = host.CurrentPath;

                var result = await LoadOnStaAsync(host, bad);

                Assert.False(result.Success);
                Assert.Equal(ProjectOpenError.UnsupportedFormat, result.Error);
                Assert.Equal(pathBefore, host.CurrentPath);
                Assert.Equal(2, host.TaskCount);
            }
            finally
            {
                File.Delete(good);
                File.Delete(bad);
            }
        }

        [Fact]
        public async Task LoadAsync_ManifestNewerThanApp_ReturnsUnsupportedFormatAndPreservesProject()
        {
            var good = ProjectFixtures.CreateProject(2);
            var tooNew = ProjectFixtures.CreateProject(1, manifestVersion: "99.0.0");
            try
            {
                var (host, _) = CreatePair();
                Assert.True((await LoadOnStaAsync(host, good)).Success);
                var pathBefore = host.CurrentPath;

                var result = await LoadOnStaAsync(host, tooNew);

                Assert.False(result.Success);
                Assert.Equal(ProjectOpenError.UnsupportedFormat, result.Error);
                Assert.Equal(pathBefore, host.CurrentPath);
                Assert.Equal(2, host.TaskCount);
            }
            finally
            {
                File.Delete(good);
                File.Delete(tooNew);
            }
        }

        [Fact]
        public async Task LoadAsync_BusyDuringExecution_PreservesExistingProject()
        {
            var first = ProjectFixtures.CreateProject(2);
            var second = ProjectFixtures.CreateProject(1);
            try
            {
                var (host, coordinator) = CreatePair();
                Assert.True((await LoadOnStaAsync(host, first)).Success);
                var pathBefore = host.CurrentPath;

                Assert.True(coordinator.TryBeginExecution());
                var result = await LoadOnStaAsync(host, second);
                coordinator.EndExecution();

                Assert.False(result.Success);
                Assert.Equal(ProjectOpenError.Busy, result.Error);
                Assert.Equal(pathBefore, host.CurrentPath);
                Assert.Equal(2, host.TaskCount);
            }
            finally
            {
                File.Delete(first);
                File.Delete(second);
            }
        }

        // ── main-app mode: commit into the live application model ──

        [Fact]
        public async Task LoadAsync_MainAppMode_CommitsToLiveModelBeforeRaisingProjectChanged()
        {
            var path = ProjectFixtures.CreateProject(2);
            try
            {
                var live = new ChipTaskHandlerModel();
                live.TaskCollection.Add(new StubTask());
                var events = new List<string>();
                var dispatchCount = 0;

                var host = new ProjectHostImpl(
                    new DatabaseReaderWriter(),
                    new ExtensionOperationCoordinator(),
                    () => live,
                    action =>
                    {
                        dispatchCount++;
                        action();
                        return Task.CompletedTask;
                    },
                    loaded =>
                    {
                        events.Add("apply");
                        live.TaskCollection.Clear();
                        foreach (var task in loaded.TaskCollection)
                            live.TaskCollection.Add(task);
                    });
                host.ProjectChanged += (_, _) => events.Add($"changed:{host.TaskCount}");

                var result = await LoadOnStaAsync(host, path);

                Assert.True(result.Success, $"LoadAsync failed: {result.Error}");
                Assert.Equal(2, result.TaskCount);
                Assert.Equal(1, dispatchCount);
                // The live model is updated first; subscribers then see the new project.
                Assert.Equal(new[] { "apply", "changed:2" }, events);
                Assert.Equal(2, live.TaskCollection.Count);
                Assert.All(live.TaskCollection, t => Assert.IsType<CommonTaskViewModel>(t));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public async Task LoadAsync_MainAppMode_FailedLoadNeverTouchesLiveModel()
        {
            var bad = ProjectFixtures.CreateNotAnArchive();
            try
            {
                var live = new ChipTaskHandlerModel();
                var original = new StubTask();
                live.TaskCollection.Add(original);
                var dispatchCount = 0;
                var applyCount = 0;

                var host = new ProjectHostImpl(
                    new DatabaseReaderWriter(),
                    new ExtensionOperationCoordinator(),
                    () => live,
                    action =>
                    {
                        dispatchCount++;
                        action();
                        return Task.CompletedTask;
                    },
                    _ => applyCount++);

                var result = await LoadOnStaAsync(host, bad);

                Assert.False(result.Success);
                Assert.Equal(0, dispatchCount);
                Assert.Equal(0, applyCount);
                Assert.Same(original, Assert.Single(live.TaskCollection));
                Assert.Null(host.CurrentPath);
            }
            finally
            {
                File.Delete(bad);
            }
        }

        // ── obsolete synchronous Load(): pinned behavior ──

#pragma warning disable CS0618 // exercising the obsolete compatibility wrapper on purpose
        [Fact]
        public void Load_SyncWrapper_ValidProject_LoadsAndSetsCanonicalPath()
        {
            var path = ProjectFixtures.CreateProject(1);
            try
            {
                var (host, _) = CreatePair();

                host.Load(path);

                Assert.Equal(Path.GetFullPath(path), host.CurrentPath);
                Assert.Equal(1, host.TaskCount);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void Load_SyncWrapper_FailedLoad_ThrowsAndPreservesProject()
        {
            var good = ProjectFixtures.CreateProject(2);
            var bad = ProjectFixtures.CreateNotAnArchive();
            try
            {
                var (host, _) = CreatePair();
                host.Load(good);
                var pathBefore = host.CurrentPath;

                Assert.Throws<InvalidOperationException>(() => host.Load(bad));

                Assert.Equal(pathBefore, host.CurrentPath);
                Assert.Equal(2, host.TaskCount);
            }
            finally
            {
                File.Delete(good);
                File.Delete(bad);
            }
        }

        [Fact]
        public void Load_SyncWrapper_WhileExecuting_ThrowsInvalidOperation()
        {
            var path = ProjectFixtures.CreateProject(1);
            try
            {
                var (host, coordinator) = CreatePair();
                Assert.True(coordinator.TryBeginExecution());

                Assert.Throws<InvalidOperationException>(() => host.Load(path));
                Assert.Null(host.CurrentPath);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void Load_SyncWrapper_EmptyPath_ThrowsArgumentException()
        {
            var (host, _) = CreatePair();

            Assert.Throws<ArgumentException>(() => host.Load("  "));
        }
#pragma warning restore CS0618
    }

    // ── ProjectManager: injectable root and file classification ───────────────

    public sealed class ProjectManagerTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "RFiDGear.Tests", Guid.NewGuid().ToString("N"));
        private readonly List<string> _files = new();

        public void Dispose()
        {
            foreach (var file in _files)
                File.Delete(file);
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }

        private string Track(string path)
        {
            _files.Add(path);
            return path;
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Ctor_BlankRoot_Throws(string? root)
        {
            Assert.Throws<ArgumentException>(() => new ProjectManager(root!));
        }

        [Fact]
        public void Ctor_CreatesInjectedRootAndExposesItAsAppDataPath()
        {
            var manager = new ProjectManager(_root);

            Assert.Equal(_root, manager.AppDataPath);
            Assert.True(Directory.Exists(_root));
            Assert.Equal(Path.Combine(_root, "taskdatabase.xml"), manager.TaskDatabasePath);
        }

        [Fact]
        public void GetProjectFileType_PlainXml_IsXml()
        {
            var file = Track(ProjectFixtures.CreatePlainXmlProject(1));

            Assert.Equal(ProjectFileType.Xml, new ProjectManager(_root).GetProjectFileType(new FileInfo(file)));
        }

        [Fact]
        public void GetProjectFileType_ZipNamedXml_IsArchive()
        {
            var file = Track(ProjectFixtures.CreateZipNamedXml(1));

            Assert.Equal(ProjectFileType.Archive, new ProjectManager(_root).GetProjectFileType(new FileInfo(file)));
        }

        [Fact]
        public void GetProjectFileType_RfPrjWithPlainXmlContent_StaysArchive()
        {
            // The extension decides for .rfPrj; bad content is reported later as UnsupportedFormat.
            var file = Track(ProjectFixtures.CreateNotAnArchive());

            Assert.Equal(ProjectFileType.Archive, new ProjectManager(_root).GetProjectFileType(new FileInfo(file)));
        }

        [Fact]
        public void GetProjectFileType_FileShorterThanZipHeader_IsXml()
        {
            var file = Track(ProjectFixtures.CreateEmptyFile(".xml"));

            Assert.Equal(ProjectFileType.Xml, new ProjectManager(_root).GetProjectFileType(new FileInfo(file)));
        }

        [Fact]
        public void GetProjectFileType_MissingXmlFile_FallsBackToXmlWithoutThrowing()
        {
            var missing = Path.Combine(_root, "does-not-exist.xml");

            Assert.Equal(ProjectFileType.Xml, new ProjectManager(_root).GetProjectFileType(new FileInfo(missing)));
        }

        [Fact]
        public void GetProjectFileType_OtherExtension_IsUnknown()
        {
            Assert.Equal(
                ProjectFileType.Unknown,
                new ProjectManager(_root).GetProjectFileType(new FileInfo(Path.Combine(_root, "project.txt"))));
        }
    }

    // ── Project load pipeline: formats, isolation, stale-file safety ──────────

    /// <summary>
    /// Runs the real load path against an isolated <see cref="ProjectManager"/> root so it neither
    /// depends on nor modifies the user's <c>%LocalAppData%\RFiDGear</c> folder.
    /// </summary>
    [Collection("AppDataPath")]
    public sealed class ProjectLoadPipelineTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "RFiDGear.Tests", Guid.NewGuid().ToString("N"));
        private readonly List<string> _files = new();

        public void Dispose()
        {
            foreach (var file in _files)
                File.Delete(file);
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }

        private string Track(string path)
        {
            _files.Add(path);
            return path;
        }

        private (ProjectHostImpl host, ProjectManager manager) CreateIsolatedHost()
        {
            var manager = new ProjectManager(_root);
            return (new ProjectHostImpl(new DatabaseReaderWriter(manager), new ExtensionOperationCoordinator()), manager);
        }

        private static Task<ProjectOpenResult> LoadAsync(ProjectHostImpl host, string path) =>
            ProjectHostImplTests.LoadOnStaAsync(host, path);

        [Fact]
        public async Task LoadAsync_PlainXmlProject_LoadsAllTasks()
        {
            var file = Track(ProjectFixtures.CreatePlainXmlProject(3));
            var (host, _) = CreateIsolatedHost();

            var result = await LoadAsync(host, file);

            Assert.True(result.Success, $"LoadAsync failed: {result.Error}");
            Assert.Equal(3, result.TaskCount);
            Assert.Equal(3, host.TaskCount);
            Assert.Equal(Path.GetFullPath(file), host.CurrentPath);
        }

        [Fact]
        public async Task LoadAsync_ZipSavedWithXmlExtension_LoadsAllTasks()
        {
            var file = Track(ProjectFixtures.CreateZipNamedXml(2));
            var (host, _) = CreateIsolatedHost();

            var result = await LoadAsync(host, file);

            Assert.True(result.Success, $"LoadAsync failed: {result.Error}");
            Assert.Equal(2, host.TaskCount);
        }

        [Fact]
        public async Task LoadAsync_ChipTreeXml_ReturnsUnsupportedFormatAndPreservesProject()
        {
            var good = Track(ProjectFixtures.CreateProject(2));
            var chipTree = Track(ProjectFixtures.CreateChipTreeXml());
            var (host, _) = CreateIsolatedHost();
            Assert.True((await LoadAsync(host, good)).Success);
            var pathBefore = host.CurrentPath;

            var result = await LoadAsync(host, chipTree);

            Assert.False(result.Success);
            Assert.Equal(ProjectOpenError.UnsupportedFormat, result.Error);
            Assert.Equal(pathBefore, host.CurrentPath);
            Assert.Equal(2, host.TaskCount);
        }

        [Fact]
        public async Task LoadAsync_NilRootXml_ReturnsUnsupportedFormat()
        {
            var file = Track(ProjectFixtures.CreateNilRootXml());
            var (host, _) = CreateIsolatedHost();

            var result = await LoadAsync(host, file);

            Assert.False(result.Success);
            Assert.Equal(ProjectOpenError.UnsupportedFormat, result.Error);
            Assert.Null(host.CurrentPath);
        }

        [Fact]
        public async Task LoadAsync_EmptyXmlFile_ReturnsUnsupportedFormat()
        {
            var file = Track(ProjectFixtures.CreateEmptyFile(".xml"));
            var (host, _) = CreateIsolatedHost();

            var result = await LoadAsync(host, file);

            Assert.False(result.Success);
            Assert.Equal(ProjectOpenError.UnsupportedFormat, result.Error);
            Assert.Null(host.CurrentPath);
        }

        [Fact]
        public async Task LoadAsync_ExtractsIntoTheInjectedRoot()
        {
            var file = Track(ProjectFixtures.CreateProject(1));
            var (host, manager) = CreateIsolatedHost();

            Assert.True((await LoadAsync(host, file)).Success);

            // The read used the injected reader's ProjectManager, not a fresh default one.
            Assert.True(File.Exists(manager.TaskDatabasePath));
        }

        [Fact]
        public async Task LoadAsync_DoesNotRunTheDestructiveResetOnEveryLoad()
        {
            var file = Track(ProjectFixtures.CreateProject(1));
            var (host, _) = CreateIsolatedHost();
            // Created after the host: the reader's constructor deletes *.rfprj in its root once.
            var sentinel = Path.Combine(_root, "sentinel.rfprj");
            await File.WriteAllTextAsync(sentinel, "keep me");

            Assert.True((await LoadAsync(host, file)).Success);
            Assert.True((await LoadAsync(host, file)).Success);

            Assert.True(File.Exists(sentinel), "A load must not delete unrelated project files.");
        }

        [Fact]
        public async Task LoadAsync_ArchiveWithoutTaskDatabaseEntry_IgnoresStaleStagingFile()
        {
            var (host, manager) = CreateIsolatedHost();
            // A previous save or load left a valid project behind in the staging slot.
            await File.WriteAllTextAsync(manager.TaskDatabasePath, ProjectFixtures.SerializeProject(5, manifestVersion: null));
            var file = Track(ProjectFixtures.CreateArchiveWithEntry("unrelated.xml", "<unrelated />"));

            var result = await LoadAsync(host, file);

            // Must not silently open the stale 5-task project.
            Assert.False(result.Success);
            Assert.Equal(ProjectOpenError.UnsupportedFormat, result.Error);
            Assert.Equal(0, host.TaskCount);
            Assert.Null(host.CurrentPath);
        }

        [Fact]
        public async Task ReadDatabase_Wrapper_AssignsSetupModelOnSuccessAndKeepsItOnFailure()
        {
            var good = Track(ProjectFixtures.CreateProject(2));
            var bad = Track(ProjectFixtures.CreateNotAnArchive());
            var reader = new DatabaseReaderWriter(new ProjectManager(_root));

            var ok = await reader.ReadDatabase(good);
            Assert.True(ok.Success);
            Assert.Equal(2, reader.SetupModel.TaskCollection.Count);

            var failed = await reader.ReadDatabase(bad);
            Assert.False(failed.Success);
            Assert.Equal(2, reader.SetupModel.TaskCollection.Count);
        }

        [Theory]
        [InlineData(".rfPrj")]
        [InlineData(".xml")]
        public async Task SaveAs_ThenLoadAsync_RoundTripsTheProject(string extension)
        {
            var output = Track(Path.Combine(Path.GetTempPath(), $"rfidgear-test-{Guid.NewGuid():N}{extension}"));
            var (writer, _) = CreateIsolatedHost();
            var (reader, _) = CreateIsolatedHost();
            writer.ReplaceTaskCollection(new object[]
            {
                new CommonTaskViewModel { CurrentTaskIndex = "0" },
                new CommonTaskViewModel { CurrentTaskIndex = "1" },
            });

            writer.SaveAs(output);
            var result = await LoadAsync(reader, output);

            Assert.True(result.Success, $"LoadAsync failed: {result.Error}");
            Assert.Equal(2, reader.TaskCount);
        }
    }

    // ── TaskExecHostImpl — lifecycle ──────────────────────────────────────────

    [Collection("AppDataPath")]
    public class TaskExecHostImplTests
    {
        private static (ProjectHostImpl project, TaskExecHostImpl exec, ExtensionOperationCoordinator coordinator) CreateTriple(
            ITaskExecutionService? service = null)
        {
            var coordinator = new ExtensionOperationCoordinator();
            var project = new ProjectHostImpl(new DatabaseReaderWriter(), coordinator);
            var exec = new TaskExecHostImpl(service ?? new CapturingExecService(), project, coordinator);
            return (project, exec, coordinator);
        }

        [Fact]
        public async Task ExecuteAllAsync_WhenCallerCancels_ReturnsFailureWithCancelledReason()
        {
            var execService = new CancellingExecService();
            var (_, host, _) = CreateTriple(execService);

            ExecResult? raisedResult = null;
            host.ExecutionCompleted += (_, r) => raisedResult = r;

            using var cts = new CancellationTokenSource();
            cts.Cancel();
            var result = await host.ExecuteAllAsync(ExecutionRequest.Empty, cts.Token);

            Assert.Equal(ExecutionOutcome.Failure, result.Outcome);
            Assert.Equal(ExecutionFailureReason.Cancelled, result.FailureReason);
            Assert.False(host.IsRunning, "Running flag must be released on cancellation.");
            Assert.NotNull(raisedResult);
            Assert.Equal(ExecutionOutcome.Failure, raisedResult!.Outcome);
            Assert.Equal(ExecutionFailureReason.Cancelled, raisedResult.FailureReason);
        }

        [Fact]
        public async Task ExecuteAllAsync_WhenServiceThrowsBareOperationCanceled_ReportsFaultedNotCancelled()
        {
            // The caller never cancelled, so this is not a caller cancellation.
            var (_, host, _) = CreateTriple(new CancellingExecService());

            var result = await host.ExecuteAllAsync(ExecutionRequest.Empty);

            Assert.Equal(ExecutionOutcome.Failure, result.Outcome);
            Assert.Equal(ExecutionFailureReason.Faulted, result.FailureReason);
        }

        [Fact]
        public async Task ExecuteAllAsync_WhenServiceThrowsGenericException_ReturnsFailureWithFaultedReason()
        {
            var execService = new FaultingExecService();
            var (_, host, _) = CreateTriple(execService);

            ExecResult? raisedResult = null;
            host.ExecutionCompleted += (_, r) => raisedResult = r;

            var result = await host.ExecuteAllAsync(ExecutionRequest.Empty);

            Assert.Equal(ExecutionOutcome.Failure, result.Outcome);
            Assert.Equal(ExecutionFailureReason.Faulted, result.FailureReason);
            Assert.False(host.IsRunning, "Running flag must be released on fault.");
            Assert.NotNull(raisedResult);
        }

        [Fact]
        public async Task ExecuteAllAsync_WhenServiceTimesOut_ReportsTimedOut()
        {
            var (_, host, _) = CreateTriple(new ThrowingExecService(() => new TimeoutException("stage timeout")));

            var result = await host.ExecuteAllAsync(ExecutionRequest.Empty);

            Assert.Equal(ExecutionOutcome.Failure, result.Outcome);
            Assert.Equal(ExecutionFailureReason.TimedOut, result.FailureReason);
        }

        [Fact]
        public async Task ExecuteAllAsync_WhenServiceReturnsNoTerminalStatus_ReportsFaulted()
        {
            var (_, host, _) = CreateTriple(new NullStatusExecService());

            var result = await host.ExecuteAllAsync(ExecutionRequest.Empty);

            Assert.Equal(ExecutionOutcome.Failure, result.Outcome);
            Assert.Equal(ExecutionFailureReason.Faulted, result.FailureReason);
        }

        [Fact]
        public async Task ExecuteAllAsync_OnSuccess_FailureReasonIsNone()
        {
            var (_, host, _) = CreateTriple(new CapturingExecService());

            var result = await host.ExecuteAllAsync(ExecutionRequest.Empty);

            Assert.Equal(ExecutionOutcome.Success, result.Outcome);
            Assert.Equal(ExecutionFailureReason.None, result.FailureReason);
        }

        [Fact]
        public async Task ExecuteAllAsync_Concurrent_SecondCallThrows()
        {
            var execService = new BlockingExecService();
            var coordinator = new ExtensionOperationCoordinator();
            var project = new ProjectHostImpl(new DatabaseReaderWriter(), coordinator);
            var host = new TaskExecHostImpl(execService, project, coordinator);

            var firstRunTask = host.ExecuteAllAsync(ExecutionRequest.Empty);
            await Task.Delay(20);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => host.ExecuteAllAsync(ExecutionRequest.Empty));

            execService.Unblock();
            var firstResult = await firstRunTask;

            // The rejected second call must not have disturbed the active run.
            Assert.Equal(ExecutionOutcome.Success, firstResult.Outcome);
            Assert.Equal(ExecutionFailureReason.None, firstResult.FailureReason);
            Assert.False(host.IsRunning);
        }

        [Fact]
        public async Task ExecuteAllAsync_WhileLoadInProgress_ThrowsWithLoadMessage()
        {
            var coordinator = new ExtensionOperationCoordinator();
            Assert.True(coordinator.TryBeginLoad());
            var project = new ProjectHostImpl(new DatabaseReaderWriter(), coordinator);
            var host = new TaskExecHostImpl(new CapturingExecService(), project, coordinator);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => host.ExecuteAllAsync(ExecutionRequest.Empty));
            Assert.Contains("load is in progress", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ExecuteAllAsync_WithVariables_PassesThroughToService()
        {
            var execService = new CapturingExecService();
            var (_, host, _) = CreateTriple(execService);

            var request = new ExecutionRequest(new[]
            {
                new KeyValuePair<string, string>("REPORTTARGETPATH", "C:\\out.pdf")
            });
            await host.ExecuteAllAsync(request);

            Assert.NotNull(execService.CapturedRequest);
            Assert.True(execService.CapturedRequest!.VariablesFromArgs.ContainsKey("REPORTTARGETPATH"));
        }

        [Fact]
        public async Task ExecuteAllAsync_OnFailure_ExecResultHasEmptyTaskOutcomes()
        {
            var execService = new CancellingExecService();
            var (_, host, _) = CreateTriple(execService);

            var result = await host.ExecuteAllAsync(ExecutionRequest.Empty);

            Assert.Empty(result.TaskOutcomes);
        }

        [Fact]
        public async Task ExecuteAllAsync_RunIdPreAllocatedAndSharedWithService()
        {
            var execService = new CapturingExecService();
            var (_, host, _) = CreateTriple(execService);

            var result = await host.ExecuteAllAsync(ExecutionRequest.Empty);

            Assert.NotEmpty(result.RunId);
            Assert.NotNull(execService.CapturedRequest);
            // The RunId pre-allocated by TaskExecHostImpl must reach the execution service.
            Assert.Equal(result.RunId, execService.CapturedRequest!.RunId);
        }

        [Fact]
        public async Task ExecuteAllAsync_ThrowingSubscriber_DoesNotLoseResultOrStarveOtherSubscribers()
        {
            var execService = new CapturingExecService();
            var (_, host, _) = CreateTriple(execService);

            int throwingCalls = 0;
            ExecResult? laterSubscriberResult = null;
            bool runningInsideHandler = true;

            host.ExecutionCompleted += (_, _) =>
            {
                Interlocked.Increment(ref throwingCalls);
                throw new InvalidOperationException("subscriber fault");
            };
            host.ExecutionCompleted += (_, r) =>
            {
                laterSubscriberResult = r;
                runningInsideHandler = host.IsRunning;
            };

            // The run already happened, so the caller must still receive its result.
            var result = await host.ExecuteAllAsync(ExecutionRequest.Empty);

            Assert.Equal(ExecutionOutcome.Success, result.Outcome);
            Assert.Equal(1, throwingCalls);
            Assert.Same(result, laterSubscriberResult);
            Assert.False(runningInsideHandler, "Subscribers must observe IsRunning == false.");
            Assert.False(host.IsRunning, "Coordinator must be released even when subscriber throws.");
        }

        [Fact]
        public async Task ExecuteAllAsync_WhenCancelledMidRun_PartialTaskOutcomesPreserved()
        {
            // The service completes the first task, then observes the cancellation.
            var execService = new ThrowingExecService(
                () => new OperationCanceledException(),
                beforeThrow: request =>
                {
                    var first = request.TaskHandler.TaskCollection.OfType<IGenericTask>().First();
                    first.ExecutionState = TaskExecutionState.Completed;
                    first.IsTaskCompletedSuccessfully = true;
                    first.CurrentTaskErrorLevel = ERROR.NoError;
                });
            var (project, host, _) = CreateTriple(execService);
            project.ReplaceTaskCollection(new object[]
            {
                new StubTask { CurrentTaskIndex = "0" },
                new StubTask { CurrentTaskIndex = "1" },
            });

            using var cts = new CancellationTokenSource();
            cts.Cancel();
            var result = await host.ExecuteAllAsync(ExecutionRequest.Empty, cts.Token);

            Assert.Equal(ExecutionOutcome.Failure, result.Outcome);
            Assert.Equal(ExecutionFailureReason.Cancelled, result.FailureReason);
            Assert.Equal(2, result.TaskOutcomes.Count);
            Assert.True(result.TaskOutcomes[0].IsSuccess);
            Assert.Null(result.TaskOutcomes[0].SkipReason);
            Assert.Null(result.TaskOutcomes[1].IsSuccess);
            Assert.Equal("NotStarted", result.TaskOutcomes[1].SkipReason);
        }

        // ── fresh state: results never leak from an earlier run ──

        [Fact]
        public async Task ExecuteAllAsync_SecondRun_DoesNotReportStateLeftByFirstRun()
        {
            var execService = new CapturingExecService();
            var (project, host, _) = CreateTriple(execService);
            project.ReplaceTaskCollection(new object[]
            {
                new StubTask { CurrentTaskIndex = "0" },
                new StubTask { CurrentTaskIndex = "1" },
            });

            var first = await host.ExecuteAllAsync(ExecutionRequest.Empty);
            Assert.All(first.TaskOutcomes, o => Assert.True(o.IsSuccess));

            // Second run: the service executes nothing, as if every task were skipped by its condition.
            execService.MarkTasksCompleted = false;
            var second = await host.ExecuteAllAsync(ExecutionRequest.Empty);

            Assert.Equal(2, second.TaskOutcomes.Count);
            Assert.All(second.TaskOutcomes, o =>
            {
                Assert.Null(o.IsSuccess);
                Assert.Equal("NotStarted", o.SkipReason);
                Assert.Equal(ERROR.Empty.ToString(), o.NormalizedErrorCode);
            });
            Assert.Equal(0, second.FailedTasks);
        }

        [Fact]
        public void ResetStatus_ClearsCompletionFlagErrorLevelAndExecutionState()
        {
            var (project, host, _) = CreateTriple();
            var task = new StubTask
            {
                IsTaskCompletedSuccessfully = false,
                CurrentTaskErrorLevel = ERROR.AuthFailure,
                ExecutionState = TaskExecutionState.Completed,
            };
            project.ReplaceTaskCollection(new object[] { task });

            host.ResetStatus();

            Assert.Null(task.IsTaskCompletedSuccessfully);
            Assert.Equal(ERROR.Empty, task.CurrentTaskErrorLevel);
            Assert.Equal(TaskExecutionState.NotStarted, task.ExecutionState);
        }

        // ── obsolete parameterless overload: pinned behavior ──

#pragma warning disable CS0618 // exercising the obsolete compatibility wrapper on purpose
        [Fact]
        public async Task ExecuteAllAsync_ParameterlessOverload_RunsWithEmptyVariablesAndReturnsResult()
        {
            var execService = new CapturingExecService();
            var (_, host, _) = CreateTriple(execService);
            ITaskExecHost api = host;

            var result = await api.ExecuteAllAsync();

            Assert.Equal(ExecutionOutcome.Success, result.Outcome);
            Assert.NotEmpty(result.RunId);
            Assert.NotNull(execService.CapturedRequest);
            Assert.Empty(execService.CapturedRequest!.VariablesFromArgs);
            Assert.False(api.IsRunning);
        }

        [Fact]
        public async Task ExecuteAllAsync_ParameterlessOverload_ReportsCancellationAsFailureResultNotException()
        {
            var (_, host, _) = CreateTriple(new CancellingExecService());
            ITaskExecHost api = host;
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var result = await api.ExecuteAllAsync(cts.Token);

            Assert.Equal(ExecutionOutcome.Failure, result.Outcome);
            Assert.Equal(ExecutionFailureReason.Cancelled, result.FailureReason);
        }
#pragma warning restore CS0618

        // ── report variables: same meaning as the equivalent startup arguments ──

        private static string CreateTempDirectory()
        {
            var dir = Path.Combine(Path.GetTempPath(), "RFiDGear.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static ExecutionRequest Vars(params (string Key, string Value)[] pairs) =>
            new ExecutionRequest(pairs.Select(p => new KeyValuePair<string, string>(p.Key, p.Value)));

        [Theory]
        [InlineData("REPORTTARGETPATH")]
        [InlineData("REPORTOUTPUTPATH")]
        [InlineData("reporttargetpath")]
        public async Task ExecuteAllAsync_ReportTarget_ReachesReportOutputPath(string key)
        {
            var dir = CreateTempDirectory();
            try
            {
                var execService = new CapturingExecService();
                var (_, host, _) = CreateTriple(execService);
                var target = Path.Combine(dir, "report.pdf");

                await host.ExecuteAllAsync(Vars((key, target)));

                Assert.Equal(target, execService.CapturedRequest!.ReportOutputPath);
                // The raw variable stays available to the task layer as well.
                Assert.Equal(target, execService.CapturedRequest.VariablesFromArgs[key]);
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }

        [Fact]
        public async Task ExecuteAllAsync_ReportTargetWithNumberPlaceholder_ResolvesNextFreeNumber()
        {
            var dir = CreateTempDirectory();
            try
            {
                var execService = new CapturingExecService();
                var (_, host, _) = CreateTriple(execService);

                await host.ExecuteAllAsync(Vars(("REPORTTARGETPATH", Path.Combine(dir, "report_???.pdf"))));

                Assert.Equal(Path.Combine(dir, "report_001.pdf"), execService.CapturedRequest!.ReportOutputPath);
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }

        [Theory]
        [InlineData("REPORTTEMPLATEFILE")]
        [InlineData("REPORTTEMPLATEPATH")]
        public async Task ExecuteAllAsync_ReportTemplate_ReachesReportTemplateFile(string key)
        {
            var dir = CreateTempDirectory();
            try
            {
                var template = Path.Combine(dir, "template.pdf");
                await File.WriteAllBytesAsync(template, new byte[] { 1 });
                var execService = new CapturingExecService();
                var (_, host, _) = CreateTriple(execService);

                await host.ExecuteAllAsync(Vars((key, template)));

                Assert.Equal(template, execService.CapturedRequest!.ReportTemplateFile);
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }

        [Fact]
        public async Task ExecuteAllAsync_WithoutReportVariables_LeavesReportPathsUnset()
        {
            var execService = new CapturingExecService();
            var (_, host, _) = CreateTriple(execService);

            await host.ExecuteAllAsync(Vars(("$JOBNUMBER", "J1")));

            Assert.Null(execService.CapturedRequest!.ReportOutputPath);
            Assert.Null(execService.CapturedRequest.ReportTemplateFile);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task ExecuteAllAsync_BlankReportVariables_AreTreatedAsNotSupplied(string blank)
        {
            var execService = new CapturingExecService();
            var (_, host, _) = CreateTriple(execService);

            await host.ExecuteAllAsync(Vars(("REPORTTARGETPATH", blank), ("REPORTTEMPLATEFILE", blank)));

            Assert.Null(execService.CapturedRequest!.ReportOutputPath);
            Assert.Null(execService.CapturedRequest.ReportTemplateFile);
        }

        [Fact]
        public async Task ExecuteAllAsync_ReportTargetInMissingDirectory_ThrowsBeforeAcceptingRun()
        {
            var execService = new CapturingExecService();
            var (_, host, _) = CreateTriple(execService);
            var eventCount = 0;
            host.ExecutionCompleted += (_, _) => eventCount++;
            var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "report.pdf");

            var ex = await Assert.ThrowsAsync<ArgumentException>(
                () => host.ExecuteAllAsync(Vars(("REPORTTARGETPATH", missing))));

            Assert.Contains("REPORTTARGETPATH", ex.Message);
            Assert.Null(execService.CapturedRequest);   // nothing was started
            Assert.Equal(0, eventCount);                 // no run was accepted, so no terminal result
            Assert.False(host.IsRunning);

            // The gate was never taken, so a valid request is still accepted afterwards.
            var result = await host.ExecuteAllAsync(ExecutionRequest.Empty);
            Assert.Equal(ExecutionOutcome.Success, result.Outcome);
        }

        [Fact]
        public async Task ExecuteAllAsync_MissingReportTemplate_ThrowsBeforeAcceptingRun()
        {
            var execService = new CapturingExecService();
            var (_, host, _) = CreateTriple(execService);
            var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "template.pdf");

            var ex = await Assert.ThrowsAsync<ArgumentException>(
                () => host.ExecuteAllAsync(Vars(("REPORTTEMPLATEFILE", missing))));

            Assert.Contains("REPORTTEMPLATEFILE", ex.Message);
            Assert.Null(execService.CapturedRequest);
            Assert.False(host.IsRunning);
        }
    }

    // ── TaskExecHostImpl driving the real TaskExecutionService ───────────────

    /// <summary>
    /// Runs the host against the real <see cref="TaskExecutionService"/> loop (task descriptors with
    /// executor lambdas, so no reader is needed) to cover behavior the fakes cannot: the cancellation
    /// check at task boundaries and ordered outcomes across several tasks.
    /// </summary>
    [Collection("AppDataPath")]
    public class TaskExecHostRealServiceTests
    {
        private static (ProjectHostImpl project, TaskExecHostImpl host) Create(ITaskExecutionService service)
        {
            var coordinator = new ExtensionOperationCoordinator();
            var project = new ProjectHostImpl(new DatabaseReaderWriter(), coordinator);
            return (project, new TaskExecHostImpl(service, project, coordinator));
        }

        private static IEnumerable<object> NewTasks(int count) =>
            Enumerable.Range(0, count).Select(i => (object)new StubTask { CurrentTaskIndex = i.ToString() });

        private static void MarkSuccess(IGenericTask task)
        {
            task.ExecutionState = TaskExecutionState.Completed;
            task.IsTaskCompletedSuccessfully = true;
            task.CurrentTaskErrorLevel = ERROR.NoError;
        }

        [Fact]
        public async Task ExecuteAllAsync_ThreeTasks_RunsInOrderAndReportsOrderedOutcomes()
        {
            var executed = new List<int>();
            var service = new DescriptorInjectingService((i, task) => ct =>
            {
                executed.Add(i);
                MarkSuccess(task);
                return Task.CompletedTask;
            });
            var (project, host) = Create(service);
            project.ReplaceTaskCollection(NewTasks(3));
            var completed = new List<ExecResult>();
            host.ExecutionCompleted += (_, r) => completed.Add(r);

            var result = await host.ExecuteAllAsync(ExecutionRequest.Empty);

            Assert.Equal(ExecutionOutcome.Success, result.Outcome);
            Assert.Equal(ExecutionFailureReason.None, result.FailureReason);
            Assert.Equal(new[] { 0, 1, 2 }, executed);
            Assert.Equal(new[] { 0, 1, 2 }, result.TaskOutcomes.Select(o => o.Position));
            Assert.Equal(new[] { "0", "1", "2" }, result.TaskOutcomes.Select(o => o.ProjectTaskIndex));
            Assert.All(result.TaskOutcomes, o =>
            {
                Assert.True(o.IsSuccess);
                Assert.Null(o.SkipReason);
            });
            Assert.Equal(3, result.TotalTasks);
            Assert.Equal(0, result.FailedTasks);

            // One terminal result per accepted run, correlated with the service's log identity.
            Assert.Equal(result.RunId, service.CapturedRunId);
            Assert.Same(result, Assert.Single(completed));
        }

        [Fact]
        public async Task ExecuteAllAsync_CancelledDuringFirstTask_StopsAtTaskBoundary()
        {
            using var cts = new CancellationTokenSource();
            var executed = new List<int>();
            var service = new DescriptorInjectingService((i, task) => ct =>
            {
                executed.Add(i);
                MarkSuccess(task);
                if (i == 0)
                    cts.Cancel();   // cancellation arrives while task 0 is running
                return Task.CompletedTask;
            });
            var (project, host) = Create(service);
            project.ReplaceTaskCollection(NewTasks(3));

            var result = await host.ExecuteAllAsync(ExecutionRequest.Empty, cts.Token);

            // Task 0 ran to completion; the loop stopped before task 1 started.
            Assert.Equal(new[] { 0 }, executed);
            Assert.Equal(ExecutionOutcome.Failure, result.Outcome);
            Assert.Equal(ExecutionFailureReason.Cancelled, result.FailureReason);
            Assert.Equal(3, result.TaskOutcomes.Count);
            Assert.True(result.TaskOutcomes[0].IsSuccess);
            Assert.All(result.TaskOutcomes.Skip(1), o =>
            {
                Assert.Null(o.IsSuccess);
                Assert.Equal("NotStarted", o.SkipReason);
            });
            Assert.False(host.IsRunning);
        }

        [Fact]
        public async Task ExecuteAllAsync_TaskThrows_ReportsFaultedWithPartialOutcomes()
        {
            var service = new DescriptorInjectingService((i, task) => ct =>
            {
                if (i == 1)
                    throw new InvalidOperationException("task blew up");
                MarkSuccess(task);
                return Task.CompletedTask;
            });
            var (project, host) = Create(service);
            project.ReplaceTaskCollection(NewTasks(3));

            var result = await host.ExecuteAllAsync(ExecutionRequest.Empty);

            Assert.Equal(ExecutionOutcome.Failure, result.Outcome);
            Assert.Equal(ExecutionFailureReason.Faulted, result.FailureReason);
            Assert.True(result.TaskOutcomes[0].IsSuccess);
            Assert.Null(result.TaskOutcomes[2].IsSuccess);
            Assert.False(host.IsRunning);
        }
    }

    // ── Integration: HeadlessExtensionHost end-to-end ────────────────────────

    [Collection("AppDataPath")]
    public class ExtensionHostIntegrationTests
    {
        [Fact(Timeout = 10_000)]
        public async Task LoadThenExecute_WithFixture_RunIdSetAndCoordinatorReleased()
        {
            var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "minimal.rfPrj");
            Assert.True(File.Exists(fixturePath), $"Test fixture not found: {fixturePath}");

            using var host = new HeadlessExtensionHost();

            ProjectOpenResult loadResult = null!;
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                loadResult = await host.Project.LoadAsync(fixturePath);
            });

            Assert.True(loadResult.Success, $"LoadAsync failed: {loadResult.Error}");
            Assert.Equal(0, loadResult.TaskCount);

            // Coordinator was released after load — execution is now allowed.
            var execResult = await host.Execution.ExecuteAllAsync(ExecutionRequest.Empty);

            Assert.False(host.Execution.IsRunning, "IsRunning must be false after execution completes.");
            Assert.NotEmpty(execResult.RunId);
            // 0 tasks from the empty fixture → TotalTasks == 0.
            Assert.Equal(0, execResult.TotalTasks);
        }

        [Fact(Timeout = 10_000)]
        public async Task LoadThenExecute_WithNonEmptyFixture_VariablesOutcomesAndRunIdCorrelated()
        {
            // Generate a fixture with one CommonTaskViewModel so we exercise a non-trivial load.
            var fixturePath = ProjectFixtures.CreateProject(1);
            // REPORTTARGETPATH must name a file in an existing directory.
            var reportDir = Path.Combine(Path.GetTempPath(), "RFiDGear.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(reportDir);
            var reportTarget = Path.Combine(reportDir, "out.pdf");
            try
            {
                var dbRW = new DatabaseReaderWriter();
                var execService = new CapturingExecService();
                using var host = new HeadlessExtensionHost(dbRW, execService, string.Empty);

                ProjectOpenResult loadResult = null!;
                await StaTestRunner.RunOnStaThreadAsync(async () =>
                {
                    loadResult = await host.Project.LoadAsync(fixturePath);
                });

                Assert.True(loadResult.Success, $"LoadAsync failed: {loadResult.Error}");
                Assert.Equal(1, loadResult.TaskCount);

                var variables = new[]
                {
                    new KeyValuePair<string, string>("REPORTTARGETPATH", reportTarget),
                    new KeyValuePair<string, string>("$JOBNUMBER", "JOB-001"),
                    new KeyValuePair<string, string>("$CHIPNUMBER", "CHIP-042"),
                };
                var execResult = await host.Execution.ExecuteAllAsync(new ExecutionRequest(variables));

                // The execution service received the full variables dictionary.
                Assert.NotNull(execService.CapturedRequest);
                Assert.Equal(reportTarget, execService.CapturedRequest!.VariablesFromArgs["REPORTTARGETPATH"]);
                Assert.Equal(reportTarget, execService.CapturedRequest.ReportOutputPath);
                Assert.Equal("JOB-001", execService.CapturedRequest!.VariablesFromArgs["$JOBNUMBER"]);
                Assert.Equal("CHIP-042", execService.CapturedRequest!.VariablesFromArgs["$CHIPNUMBER"]);

                // TotalTasks reflects the one task loaded from the fixture.
                Assert.Equal(1, execResult.TotalTasks);

                // Exactly one ordered TaskOutcome was built from the task collection.
                Assert.Single(execResult.TaskOutcomes);
                var taskOutcome = execResult.TaskOutcomes[0];
                Assert.Equal(0, taskOutcome.Position);
                Assert.Equal("0", taskOutcome.ProjectTaskIndex);
                Assert.Equal("CommonTaskViewModel", taskOutcome.TaskTypeName);
                Assert.True(taskOutcome.IsSuccess);
                Assert.Equal(ERROR.NoError.ToString(), taskOutcome.NormalizedErrorCode);
                Assert.Null(taskOutcome.SkipReason);

                // RunId was pre-allocated and shared with the execution service.
                Assert.NotEmpty(execResult.RunId);
                Assert.Equal(execResult.RunId, execService.CapturedRequest!.RunId);

                // Coordinator was released — no state leak.
                Assert.False(host.Execution.IsRunning);
            }
            finally
            {
                File.Delete(fixturePath);
                Directory.Delete(reportDir, recursive: true);
            }
        }

        [Fact(Timeout = 10_000)]
        public async Task LoadThenExecute_WithThreeTasks_OutcomesOrderedAndOneTerminalResult()
        {
            var fixturePath = ProjectFixtures.CreateProject(3);
            try
            {
                var execService = new CapturingExecService();
                using var host = new HeadlessExtensionHost(new DatabaseReaderWriter(), execService, string.Empty);
                var completed = new List<ExecResult>();
                host.Execution.ExecutionCompleted += (_, r) => completed.Add(r);

                ProjectOpenResult loadResult = null!;
                await StaTestRunner.RunOnStaThreadAsync(async () =>
                {
                    loadResult = await host.Project.LoadAsync(fixturePath);
                });
                Assert.True(loadResult.Success, $"LoadAsync failed: {loadResult.Error}");
                Assert.Equal(3, loadResult.TaskCount);

                var execResult = await host.Execution.ExecuteAllAsync(new ExecutionRequest());

                Assert.Equal(3, execResult.TotalTasks);
                Assert.Equal(new[] { 0, 1, 2 }, execResult.TaskOutcomes.Select(o => o.Position));
                Assert.Equal(new[] { "0", "1", "2" }, execResult.TaskOutcomes.Select(o => o.ProjectTaskIndex));
                Assert.All(execResult.TaskOutcomes, o => Assert.Equal("CommonTaskViewModel", o.TaskTypeName));

                // Exactly one terminal result, and its RunId is the one the service logged under.
                Assert.Same(execResult, Assert.Single(completed));
                Assert.Equal(execResult.RunId, execService.CapturedRequest!.RunId);
            }
            finally
            {
                File.Delete(fixturePath);
            }
        }
    }

    // ── Stub ITaskExecutionService implementations ────────────────────────────

    internal sealed class CancellingExecService : ITaskExecutionService
    {
        public event EventHandler<TaskExecutionCompletedEventArgs> ExecutionCompleted
        {
            add { }
            remove { }
        }

        public int CurrentTaskIndex => 0;

        public Task<TaskExecutionResult> ExecuteOnceAsync(
            TaskExecutionRequest request, CancellationToken ct = default)
            => throw new OperationCanceledException();

        public void HandleTaskTimeout() { }
    }

    internal sealed class FaultingExecService : ITaskExecutionService
    {
        public event EventHandler<TaskExecutionCompletedEventArgs> ExecutionCompleted
        {
            add { }
            remove { }
        }

        public int CurrentTaskIndex => 0;

        public Task<TaskExecutionResult> ExecuteOnceAsync(
            TaskExecutionRequest request, CancellationToken ct = default)
            => throw new InvalidOperationException("Simulated fault");

        public void HandleTaskTimeout() { }
    }

    /// <summary>Throws a caller-supplied exception, optionally after doing partial work on the request.</summary>
    internal sealed class ThrowingExecService : ITaskExecutionService
    {
        private readonly Func<Exception> _exception;
        private readonly Action<TaskExecutionRequest>? _beforeThrow;

        public ThrowingExecService(Func<Exception> exception, Action<TaskExecutionRequest>? beforeThrow = null)
        {
            _exception = exception;
            _beforeThrow = beforeThrow;
        }

        public event EventHandler<TaskExecutionCompletedEventArgs> ExecutionCompleted
        {
            add { }
            remove { }
        }

        public int CurrentTaskIndex => 0;

        public Task<TaskExecutionResult> ExecuteOnceAsync(
            TaskExecutionRequest request, CancellationToken ct = default)
        {
            _beforeThrow?.Invoke(request);
            throw _exception();
        }

        public void HandleTaskTimeout() { }
    }

    /// <summary>Returns a result that never reached the task loop's terminal evaluation.</summary>
    internal sealed class NullStatusExecService : ITaskExecutionService
    {
        public event EventHandler<TaskExecutionCompletedEventArgs> ExecutionCompleted
        {
            add { }
            remove { }
        }

        public int CurrentTaskIndex => 0;

        public Task<TaskExecutionResult> ExecuteOnceAsync(
            TaskExecutionRequest request, CancellationToken ct = default)
            => Task.FromResult(new TaskExecutionResult { RunId = request.RunId });

        public void HandleTaskTimeout() { }
    }

    /// <summary>
    /// Wraps the real <see cref="TaskExecutionService"/> and replaces the request's tasks with
    /// descriptors that run caller-supplied executors, so the real loop (boundary checks, ordering,
    /// terminal evaluation) runs without a reader.
    /// </summary>
    internal sealed class DescriptorInjectingService : ITaskExecutionService
    {
        private readonly TaskExecutionService _inner = new TaskExecutionService(
            new ReaderDeviceProvider(),
            new NullDispatcherTimerAdapter(),
            new NullDispatcherTimerAdapter());
        private readonly Func<int, IGenericTask, Func<CancellationToken, Task>> _executorFor;

        public DescriptorInjectingService(Func<int, IGenericTask, Func<CancellationToken, Task>> executorFor)
        {
            _executorFor = executorFor;
        }

        /// <summary>The RunId the host allocated for the most recent run.</summary>
        public string? CapturedRunId { get; private set; }

        public event EventHandler<TaskExecutionCompletedEventArgs> ExecutionCompleted
        {
            add { _inner.ExecutionCompleted += value; }
            remove { _inner.ExecutionCompleted -= value; }
        }

        public int CurrentTaskIndex => _inner.CurrentTaskIndex;

        public Task<TaskExecutionResult> ExecuteOnceAsync(
            TaskExecutionRequest request, CancellationToken ct = default)
        {
            CapturedRunId = request.RunId;
            request.TaskDescriptors = request.TaskHandler.TaskCollection
                .OfType<IGenericTask>()
                .Select((task, index) => new TaskDescriptor(index, task, _executorFor(index, task)))
                .ToList();
            return _inner.ExecuteOnceAsync(request, ct);
        }

        public void HandleTaskTimeout() => _inner.HandleTaskTimeout();
    }

    internal sealed class BlockingExecService : ITaskExecutionService
    {
        private readonly TaskCompletionSource<bool> _tcs = new();

        public event EventHandler<TaskExecutionCompletedEventArgs> ExecutionCompleted
        {
            add { }
            remove { }
        }

        public int CurrentTaskIndex => 0;

        public Task<TaskExecutionResult> ExecuteOnceAsync(
            TaskExecutionRequest request, CancellationToken ct = default)
        {
            return _tcs.Task.ContinueWith(
                _ => new TaskExecutionResult { TerminalStatus = TaskLoopTerminalStatus.Success },
                TaskScheduler.Default);
        }

        public void Unblock() => _tcs.TrySetResult(true);
        public void HandleTaskTimeout() { }
    }

    internal sealed class CapturingExecService : ITaskExecutionService
    {
        public TaskExecutionRequest? CapturedRequest { get; private set; }

        /// <summary>
        /// When <c>false</c> the service leaves task state untouched, as if every task had been skipped.
        /// </summary>
        public bool MarkTasksCompleted { get; set; } = true;

        public event EventHandler<TaskExecutionCompletedEventArgs> ExecutionCompleted
        {
            add { }
            remove { }
        }

        public int CurrentTaskIndex => 0;

        public Task<TaskExecutionResult> ExecuteOnceAsync(
            TaskExecutionRequest request, CancellationToken ct = default)
        {
            CapturedRequest = request;
            foreach (var task in MarkTasksCompleted
                ? request.TaskHandler?.TaskCollection?.OfType<IGenericTask>() ?? Enumerable.Empty<IGenericTask>()
                : Enumerable.Empty<IGenericTask>())
            {
                task.ExecutionState = TaskExecutionState.Completed;
                task.IsTaskCompletedSuccessfully = true;
                task.CurrentTaskErrorLevel = ERROR.NoError;
            }

            return Task.FromResult(new TaskExecutionResult
            {
                RunId = request.RunId,
                TerminalStatus = TaskLoopTerminalStatus.Success
            });
        }

        public void HandleTaskTimeout() { }
    }

    // ── Minimal IGenericTask stub for partial-outcome tests ───────────────────

    internal sealed class StubTask : IGenericTask
    {
        public bool? IsTaskCompletedSuccessfully { get; set; }
        public ERROR SelectedExecuteConditionErrorLevel { get; set; } = ERROR.Empty;
        public string SelectedExecuteConditionTaskIndex { get; set; } = "0";
        public ERROR CurrentTaskErrorLevel { get; set; } = ERROR.NoError;
        public string CurrentTaskIndex { get; set; } = "0";
        public int SelectedTaskIndexAsInt => 0;
        public ObservableCollection<TaskAttemptResult> AttemptResults { get; } = new();
        public TaskExecutionState ExecutionState { get; set; } = TaskExecutionState.NotStarted;
    }
}
