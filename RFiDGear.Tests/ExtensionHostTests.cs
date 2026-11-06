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

        // ── CultureChanged ──

        [Fact]
        public void NotifyCultureMayHaveChanged_WhenTheLanguageChanged_RaisesOnceAndSubscribersSeeTheNewLanguage()
        {
            var language = "english";
            var ctx = new AppContextImpl(() => language);
            var seen = new List<string>();
            object? sender = null;
            ctx.CultureChanged += (s, _) =>
            {
                sender = s;
                seen.Add(ctx.UICultureName);
            };

            language = "german";
            ctx.NotifyCultureMayHaveChanged();

            Assert.Equal(new[] { "de-DE" }, seen);
            Assert.Same(ctx, sender);
        }

        [Fact]
        public void NotifyCultureMayHaveChanged_WhenTheLanguageIsTheSame_RaisesNothing()
        {
            var language = "german";
            var ctx = new AppContextImpl(() => language);
            var raised = 0;
            ctx.CultureChanged += (_, _) => raised++;

            ctx.NotifyCultureMayHaveChanged();
            ctx.NotifyCultureMayHaveChanged();

            Assert.Equal(0, raised);
        }

        [Fact]
        public void NotifyCultureMayHaveChanged_RaisesOncePerChange_NotOncePerNotification()
        {
            var language = "english";
            var ctx = new AppContextImpl(() => language);
            var seen = new List<string>();
            ctx.CultureChanged += (_, _) => seen.Add(ctx.UICultureName);

            language = "german";
            ctx.NotifyCultureMayHaveChanged();
            ctx.NotifyCultureMayHaveChanged();   // nothing new since the last notification
            language = "english";
            ctx.NotifyCultureMayHaveChanged();

            Assert.Equal(new[] { "de-DE", "en-US" }, seen);
        }

        [Fact]
        public void NotifyCultureMayHaveChanged_WithoutAConfiguredLanguageSource_NeverRaises()
        {
            var ctx = new AppContextImpl();
            var raised = 0;
            ctx.CultureChanged += (_, _) => raised++;

            ctx.NotifyCultureMayHaveChanged();

            Assert.Equal(0, raised);
        }

        [Fact]
        public void NotifyCultureMayHaveChanged_AfterUnsubscribing_DoesNotCallTheHandler()
        {
            var language = "english";
            var ctx = new AppContextImpl(() => language);
            var raised = 0;
            EventHandler handler = (_, _) => raised++;
            ctx.CultureChanged += handler;
            ctx.CultureChanged -= handler;

            language = "german";
            ctx.NotifyCultureMayHaveChanged();

            Assert.Equal(0, raised);
        }

        [Fact]
        public void NotifyCultureMayHaveChanged_WhenASubscriberThrows_LogsItWithoutTheSecretAndStillCallsTheRest()
        {
            const string secret = "00112233445566778899AABBCCDDEEFF";
            using var logs = CapturedLogs.Install();
            var language = "english";
            var ctx = new AppContextImpl(() => language);
            var later = 0;
            ctx.CultureChanged += (_, _) => throw new InvalidOperationException($"Key={secret}");
            ctx.CultureChanged += (_, _) => later++;

            language = "german";
            ctx.NotifyCultureMayHaveChanged();   // must not throw

            Assert.Equal(1, later);
            var entry = Assert.Single(logs.From(nameof(AppContextImpl)));
            var written = CapturedLogs.EverythingWrittenFor(entry);
            Assert.Contains(nameof(InvalidOperationException), written);
            Assert.Contains("[REDACTED", written);
            Assert.DoesNotContain(secret, written, StringComparison.OrdinalIgnoreCase);
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
            var outcome = new TaskOutcome(1, "1", "SomeTask", null, "NoError", TaskSkipReasons.NotReached);
            Assert.Equal(TaskSkipReasons.NotReached, outcome.SkipReason);
        }

        [Fact]
        public void IsSuccess_NullWithSkipReason_IndicatesNotExecuted()
        {
            var outcome = new TaskOutcome(0, "0", "SomeTask", null, "NoError", TaskSkipReasons.NotReached);
            Assert.Null(outcome.IsSuccess);
            Assert.Equal(TaskSkipReasons.NotReached, outcome.SkipReason);
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
                        {
                            live.TaskCollection.Add(task);
                        }
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

        // ── Load() on the main application's UI thread: refused instead of deadlocking ──

        private static (ProjectHostImpl host, ExtensionOperationCoordinator coordinator, int[] touched) CreateMainAppHost(Func<bool>? isOnUiThread)
        {
            var live = new ChipTaskHandlerModel();
            var coordinator = new ExtensionOperationCoordinator();
            var touched = new int[2]; // [0] = dispatches, [1] = applies
            var host = new ProjectHostImpl(
                new DatabaseReaderWriter(),
                coordinator,
                () => live,
                action =>
                {
                    touched[0]++;
                    action();
                    return Task.CompletedTask;
                },
                loaded =>
                {
                    touched[1]++;
                    live.TaskCollection.Clear();
                    foreach (var task in loaded.TaskCollection)
                    {
                        live.TaskCollection.Add(task);
                    }
                },
                isOnUiThread);
            return (host, coordinator, touched);
        }

        [Theory]
        [InlineData("valid-project", false)]
        [InlineData("missing-file", false)]
        [InlineData("valid-project", true)] // busy as well: the thread check comes first
        public void Load_SyncWrapper_OnTheUiThread_ThrowsAtOnceAndTouchesNothing(string file, bool hostIsBusy)
        {
            var path = file == "valid-project"
                ? ProjectFixtures.CreateProject(1)
                : Path.Combine(Path.GetTempPath(), $"rfidgear-test-missing-{Guid.NewGuid():N}.rfPrj");
            try
            {
                var (host, coordinator, touched) = CreateMainAppHost(isOnUiThread: () => true);
                if (hostIsBusy)
                {
                    Assert.True(coordinator.TryBeginExecution());
                }

                var refused = Assert.Throws<InvalidOperationException>(() => host.Load(path));

                Assert.Contains("UI thread", refused.Message, StringComparison.Ordinal);
                Assert.Contains("LoadAsync", refused.Message, StringComparison.Ordinal);
                Assert.Equal(new[] { 0, 0 }, touched);
                Assert.Null(host.CurrentPath);
                Assert.Equal(0, host.TaskCount);
                // Nothing was reserved: the coordinator is exactly as the test left it.
                Assert.Equal(hostIsBusy, coordinator.IsExecuting);
                Assert.False(coordinator.IsLoading);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void Load_SyncWrapper_OnTheUiThread_EmptyPathStillThrowsArgumentException()
        {
            var (host, _, _) = CreateMainAppHost(isOnUiThread: () => true);

            Assert.Throws<ArgumentException>(() => host.Load("  "));
        }

        [Fact]
        public void Load_SyncWrapper_OffTheUiThread_LoadsIntoTheLiveModel()
        {
            var path = ProjectFixtures.CreateProject(2);
            try
            {
                var (host, _, touched) = CreateMainAppHost(isOnUiThread: () => false);

                host.Load(path);

                Assert.Equal(Path.GetFullPath(path), host.CurrentPath);
                Assert.Equal(2, host.TaskCount);
                Assert.Equal(new[] { 1, 1 }, touched);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void Load_SyncWrapper_AsksWhetherItIsOnTheUiThreadFromTheCallingThread()
        {
            // The probe must run on the caller's thread: asked from inside the worker the load uses, it would
            // always answer "not the UI thread" and the guard would never fire.
            var path = ProjectFixtures.CreateProject(1);
            try
            {
                var askedOnThread = -1;
                var (host, _, _) = CreateMainAppHost(isOnUiThread: () =>
                {
                    askedOnThread = Environment.CurrentManagedThreadId;
                    return false;
                });

                host.Load(path);

                Assert.Equal(Environment.CurrentManagedThreadId, askedOnThread);
            }
            finally
            {
                File.Delete(path);
            }
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
            {
                File.Delete(file);
            }
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
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
    /// Shared plumbing for tests that run the real project host against an isolated
    /// <see cref="ProjectManager"/> root, so they neither depend on nor modify the user's
    /// <c>%LocalAppData%\RFiDGear</c> folder. Deletes the root and every tracked file afterwards.
    /// </summary>
    public abstract class IsolatedProjectTestBase : IDisposable
    {
        protected readonly string _root = Path.Combine(Path.GetTempPath(), "RFiDGear.Tests", Guid.NewGuid().ToString("N"));
        private readonly List<string> _files = new();
        private readonly List<string> _scratchDirectories = new();

        public void Dispose()
        {
            foreach (var file in _files)
            {
                File.Delete(file);
            }
            foreach (var directory in _scratchDirectories.Append(_root))
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
        }

        /// <summary>Creates an empty folder outside the isolated root that is deleted when the test ends.</summary>
        protected string NewScratchDirectory()
        {
            var directory = Path.Combine(Path.GetTempPath(), "RFiDGear.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            _scratchDirectories.Add(directory);
            return directory;
        }

        /// <summary>Registers a file for deletion when the test ends and returns its path.</summary>
        protected string Track(string path)
        {
            _files.Add(path);
            return path;
        }

        internal (ProjectHostImpl host, ProjectManager manager) CreateIsolatedHost()
        {
            var (host, manager, _) = CreateIsolatedHostWithCoordinator();
            return (host, manager);
        }

        internal (ProjectHostImpl host, ProjectManager manager, ExtensionOperationCoordinator coordinator) CreateIsolatedHostWithCoordinator()
        {
            var manager = new ProjectManager(_root);
            var coordinator = new ExtensionOperationCoordinator();
            return (new ProjectHostImpl(new DatabaseReaderWriter(manager), coordinator), manager, coordinator);
        }

        internal static Task<ProjectOpenResult> LoadAsync(ProjectHostImpl host, string path) =>
            ProjectHostImplTests.LoadOnStaAsync(host, path);
    }

    /// <summary>
    /// Runs the real load path against an isolated <see cref="ProjectManager"/> root.
    /// </summary>
    [Collection("AppDataPath")]
    public sealed class ProjectLoadPipelineTests : IsolatedProjectTestBase
    {

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
        public async Task LoadAsync_SecondArchiveWithTheSameFileName_LoadsItsOwnProjectNotTheFirstOnesPayload()
        {
            // Both archives are called job.rfPrj. The first stores its project in an entry named like the
            // archive (the oldest layout), which extraction leaves in the shared folder as job.rfPrj.
            var first = Track(ProjectFixtures.CreateArchiveAt(
                NewScratchDirectory(), "job.rfPrj", entryName: "job.rfPrj",
                ProjectFixtures.SerializeProject(2, manifestVersion: null)));
            var second = Track(ProjectFixtures.CreateArchiveAt(
                NewScratchDirectory(), "job.rfPrj", entryName: "taskdatabase.xml",
                ProjectFixtures.SerializeProject(1, manifestVersion: null)));
            var (host, _) = CreateIsolatedHost();
            var firstResult = await LoadAsync(host, first);
            Assert.True(firstResult.Success, $"LoadAsync failed: {firstResult.Error}");
            Assert.Equal(2, firstResult.TaskCount);

            var result = await LoadAsync(host, second);

            Assert.True(result.Success, $"LoadAsync failed: {result.Error}");
            Assert.Equal(1, result.TaskCount);   // not the 2 tasks left behind by the first archive
            Assert.Equal(1, host.TaskCount);
            Assert.Equal(Path.GetFullPath(second), host.CurrentPath);
        }

        [Fact]
        public async Task LoadAsync_SecondArchiveWithTheSameFileNameButNoProjectEntry_IsRejectedAndKeepsTheProject()
        {
            var first = Track(ProjectFixtures.CreateArchiveAt(
                NewScratchDirectory(), "job.rfPrj", entryName: "job.rfPrj",
                ProjectFixtures.SerializeProject(2, manifestVersion: null)));
            var second = Track(ProjectFixtures.CreateArchiveAt(
                NewScratchDirectory(), "job.rfPrj", entryName: "unrelated.xml", "<unrelated />"));
            var (host, _) = CreateIsolatedHost();
            Assert.True((await LoadAsync(host, first)).Success);
            var pathBefore = host.CurrentPath;

            var result = await LoadAsync(host, second);

            // It has no project of its own, so it must not succeed on the first archive's leftovers.
            Assert.False(result.Success);
            Assert.Equal(ProjectOpenError.UnsupportedFormat, result.Error);
            Assert.Equal(pathBefore, host.CurrentPath);
            Assert.Equal(2, host.TaskCount);
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

    // ── ExtensionOperationCoordinator ─────────────────────────────────────────

    public class ExtensionOperationCoordinatorTests
    {
        [Fact]
        public void ProjectOperation_WhenIdle_IsAdmittedAndReleased()
        {
            var coordinator = new ExtensionOperationCoordinator();

            Assert.True(coordinator.TryBeginProjectOperation());
            Assert.True(coordinator.IsMutating);

            coordinator.EndProjectOperation();
            Assert.False(coordinator.IsMutating);
            Assert.True(coordinator.TryBeginProjectOperation());
        }

        [Fact]
        public void ProjectOperation_IsRejectedWhileLoadingExecutingOrMutating()
        {
            var loading = new ExtensionOperationCoordinator();
            Assert.True(loading.TryBeginLoad());
            Assert.False(loading.TryBeginProjectOperation());

            var executing = new ExtensionOperationCoordinator();
            Assert.True(executing.TryBeginExecution());
            Assert.False(executing.TryBeginProjectOperation());

            var mutating = new ExtensionOperationCoordinator();
            Assert.True(mutating.TryBeginProjectOperation());
            Assert.False(mutating.TryBeginProjectOperation());
        }

        [Fact]
        public void LoadAndExecution_AreRejectedWhileAProjectOperationIsInProgress()
        {
            var coordinator = new ExtensionOperationCoordinator();
            Assert.True(coordinator.TryBeginProjectOperation());

            Assert.False(coordinator.TryBeginLoad());
            Assert.False(coordinator.TryBeginExecution());
            Assert.False(coordinator.IsLoading);
            Assert.False(coordinator.IsExecuting);
        }
    }

    // ── Project operations: edits, saves, current path ────────────────────────

    [Collection("AppDataPath")]
    public sealed class ProjectOperationTests : IsolatedProjectTestBase
    {
        private ProjectHostImpl CreateLiveHost(
            ChipTaskHandlerModel live,
            ExtensionOperationCoordinator coordinator,
            Func<Action, Task>? dispatch = null,
            Action<ChipTaskHandlerModel>? apply = null) =>
            new ProjectHostImpl(
                new DatabaseReaderWriter(new ProjectManager(_root)),
                coordinator,
                () => live,
                dispatch ?? (action => { action(); return Task.CompletedTask; }),
                apply ?? (loaded =>
                {
                    live.TaskCollection.Clear();
                    foreach (var task in loaded.TaskCollection)
                    {
                        live.TaskCollection.Add(task);
                    }
                }));

        private string NewOutputPath(string extension) =>
            Track(Path.Combine(Path.GetTempPath(), $"rfidgear-test-{Guid.NewGuid():N}{extension}"));

        // ── New / ReplaceTaskCollection ──

        [Fact]
        public async Task New_ClearsProjectAndPath_AndRaisesProjectChangedOnce()
        {
            var project = Track(ProjectFixtures.CreateProject(2));
            var (host, _) = CreateIsolatedHost();
            Assert.True((await LoadAsync(host, project)).Success);
            var raised = 0;
            host.ProjectChanged += (_, _) => raised++;

            host.New();

            Assert.Equal(0, host.TaskCount);
            Assert.Null(host.CurrentPath);
            Assert.Equal(1, raised);
        }

        [Fact]
        public void New_AndReplaceTaskCollection_AreRejectedDuringARunAndLeaveTheProjectUnchanged()
        {
            var (host, _, coordinator) = CreateIsolatedHostWithCoordinator();
            host.ReplaceTaskCollection(new object[] { new StubTask() });
            var raised = 0;
            host.ProjectChanged += (_, _) => raised++;

            Assert.True(coordinator.TryBeginExecution());
            Assert.Throws<InvalidOperationException>(() => host.New());
            Assert.Throws<InvalidOperationException>(() => host.ReplaceTaskCollection(new object[] { new StubTask(), new StubTask() }));
            coordinator.EndExecution();

            Assert.Equal(1, host.TaskCount);
            Assert.Equal(0, raised);

            // Rejection must not leak the slot.
            host.New();
            Assert.Equal(0, host.TaskCount);
        }

        [Fact]
        public void New_AndReplaceTaskCollection_AreRejectedDuringALoad()
        {
            var (host, _, coordinator) = CreateIsolatedHostWithCoordinator();
            host.ReplaceTaskCollection(new object[] { new StubTask() });

            Assert.True(coordinator.TryBeginLoad());
            Assert.Throws<InvalidOperationException>(() => host.New());
            Assert.Throws<InvalidOperationException>(() => host.ReplaceTaskCollection(new object[0]));

            Assert.Equal(1, host.TaskCount);
        }

        [Fact]
        public async Task ExecuteAllAsync_IsRejectedWhileAProjectEditIsInProgress()
        {
            var coordinator = new ExtensionOperationCoordinator();
            var project = new ProjectHostImpl(new DatabaseReaderWriter(new ProjectManager(_root)), coordinator);
            var exec = new TaskExecHostImpl(new CapturingExecService(), project, coordinator);
            Assert.True(coordinator.TryBeginProjectOperation());

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => exec.ExecuteAllAsync(ExecutionRequest.Empty));

            Assert.Contains("project edit", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task New_InMainAppMode_AppliesAnEmptyModelInlineAndClearsThePath()
        {
            var project = Track(ProjectFixtures.CreateProject(2));
            var live = new ChipTaskHandlerModel();
            var applied = new List<ChipTaskHandlerModel>();
            var host = CreateLiveHost(
                live,
                new ExtensionOperationCoordinator(),
                apply: model =>
                {
                    applied.Add(model);
                    live.TaskCollection.Clear();
                    foreach (var task in model.TaskCollection)
                    {
                        live.TaskCollection.Add(task);
                    }
                });
            Assert.True((await LoadAsync(host, project)).Success);
            Assert.Equal(2, live.TaskCollection.Count);
            applied.Clear();

            host.New();

            Assert.Empty(Assert.Single(applied).TaskCollection);
            Assert.Empty(live.TaskCollection);
            Assert.Equal(0, host.TaskCount);
            Assert.Null(host.CurrentPath);
        }

        [Fact]
        public void ReplaceTaskCollection_InMainAppMode_AppliesThroughTheSameCallbackAsALoad()
        {
            var live = new ChipTaskHandlerModel();
            live.TaskCollection.Add(new StubTask());
            var originalCollection = live.TaskCollection;
            var host = CreateLiveHost(live, new ExtensionOperationCoordinator());
            var replacements = new object[] { new StubTask { CurrentTaskIndex = "7" }, new StubTask { CurrentTaskIndex = "8" } };

            host.ReplaceTaskCollection(replacements);

            Assert.Same(originalCollection, live.TaskCollection);   // bound collection instance is kept
            Assert.Equal(replacements, live.TaskCollection);
            Assert.Equal(2, host.TaskCount);
        }

        // ── file gate: a load and a save never overlap ──

        [Fact]
        public async Task WhileALoadIsCommitting_SaveNewAndLoadAreRejected_ThenEverythingWorksAgain()
        {
            var first = Track(ProjectFixtures.CreateProject(1));
            var second = Track(ProjectFixtures.CreateProject(2));
            var output = NewOutputPath(".rfPrj");
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var host = CreateLiveHost(
                new ChipTaskHandlerModel(),
                new ExtensionOperationCoordinator(),
                dispatch: async action =>
                {
                    entered.SetResult();
                    await release.Task;
                    action();
                });

            var loading = LoadAsync(host, first);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));

            // The load owns the staging files and the project: nothing else may touch either.
            Assert.Throws<InvalidOperationException>(() => host.SaveAs(output));
            Assert.Throws<InvalidOperationException>(() => host.New());
            Assert.Equal(ProjectOpenError.Busy, (await host.LoadAsync(second)).Error);

            release.SetResult();
            Assert.True((await loading).Success);

            host.SaveAs(output);
            Assert.True(File.Exists(output));
        }

        [Fact]
        public void Save_DuringATaskRun_IsAllowed()
        {
            var (host, _, coordinator) = CreateIsolatedHostWithCoordinator();
            host.ReplaceTaskCollection(new object[] { new CommonTaskViewModel { CurrentTaskIndex = "0" } });
            var output = NewOutputPath(".rfPrj");
            Assert.True(coordinator.TryBeginExecution());

            host.SaveAs(output);

            Assert.True(File.Exists(output));
            Assert.Equal(Path.GetFullPath(output), host.CurrentPath);
        }

        [Fact]
        public void Save_DuringAProjectEdit_IsRejected()
        {
            var (host, _, coordinator) = CreateIsolatedHostWithCoordinator();
            var output = NewOutputPath(".rfPrj");
            Assert.True(coordinator.TryBeginProjectOperation());

            Assert.Throws<InvalidOperationException>(() => host.SaveAs(output));

            Assert.False(File.Exists(output));
        }

        // ── events ──

        [Fact]
        public async Task ProjectChanged_ThrowingSubscriber_DoesNotFailLoadOrEditAndLaterSubscribersRun()
        {
            var project = Track(ProjectFixtures.CreateProject(1));
            var (host, _) = CreateIsolatedHost();
            var calls = 0;
            host.ProjectChanged += (_, _) => throw new InvalidOperationException("subscriber fault");
            host.ProjectChanged += (_, _) => calls++;

            var result = await LoadAsync(host, project);

            Assert.True(result.Success, $"LoadAsync failed: {result.Error}");
            Assert.Equal(1, host.TaskCount);
            Assert.Equal(1, calls);

            host.New();   // must not throw either

            Assert.Equal(0, host.TaskCount);
            Assert.Equal(2, calls);
        }

        [Fact]
        public async Task WhileASaveIsSerializing_NewAndReplaceAreRejected_SoTheSaveKeepsItsPathAndData()
        {
            var output = NewOutputPath(".rfPrj");
            var live = new ChipTaskHandlerModel();
            live.TaskCollection.Add(new CommonTaskViewModel { CurrentTaskIndex = "0" });
            using var entered = new ManualResetEventSlim(false);
            using var release = new ManualResetEventSlim(false);
            var parkNextRead = 0;

            // The save reads the live model through this accessor while it owns the gate, so parking the
            // accessor parks the save mid-write, deterministically.
            var host = new ProjectHostImpl(
                new DatabaseReaderWriter(new ProjectManager(_root)),
                new ExtensionOperationCoordinator(),
                () =>
                {
                    if (Interlocked.Exchange(ref parkNextRead, 0) == 1)
                    {
                        entered.Set();
                        release.Wait(TimeSpan.FromSeconds(10));
                    }

                    return live;
                },
                action => { action(); return Task.CompletedTask; },
                loaded =>
                {
                    live.TaskCollection.Clear();
                    foreach (var task in loaded.TaskCollection)
                    {
                        live.TaskCollection.Add(task);
                    }
                });

            Volatile.Write(ref parkNextRead, 1);
            var saving = Task.Run(() => host.SaveAs(output));
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10)), "The save never reached the live model.");

            // An edit that slipped in here would clear the path and swap the collection mid-serialization,
            // and the finishing save would then write the old project's path back.
            Assert.Throws<InvalidOperationException>(() => host.New());
            Assert.Throws<InvalidOperationException>(() => host.ReplaceTaskCollection(new object[] { new StubTask() }));

            release.Set();
            await saving;

            Assert.Equal(Path.GetFullPath(output), host.CurrentPath);
            Assert.Equal(1, host.TaskCount);

            // Once the save is done, edits work again and the rejected ones left nothing behind.
            host.New();
            Assert.Null(host.CurrentPath);
            Assert.Equal(0, host.TaskCount);
        }

        [Fact]
        public async Task ProjectChanged_SubscriberFault_IsLoggedWithoutLeakingSecretsFromItsMessage()
        {
            const string secret = "00112233445566778899AABBCCDDEEFF";   // key-sized hex
            var project = Track(ProjectFixtures.CreateProject(1));
            var (host, _) = CreateIsolatedHost();
            host.ProjectChanged += (_, _) => throw new InvalidOperationException("Key=" + secret + " also " + secret);
            using var logs = CapturedLogs.Install();

            Assert.True((await LoadAsync(host, project)).Success);

            var entry = Assert.Single(logs.From(nameof(ProjectHostImpl)), e => e.Level == Serilog.Events.LogEventLevel.Error);
            Assert.Null(entry.Exception);   // the raw exception is not attached
            var written = CapturedLogs.EverythingWrittenFor(entry);
            Assert.DoesNotContain(secret, written, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("[REDACTED", written);
            Assert.Contains(nameof(InvalidOperationException), written);
        }

        [Fact]
        public async Task LoadAsync_WhileARealRunIsInFlight_IsRejectedAsBusyAndTheRunIsUndisturbed()
        {
            var project = Track(ProjectFixtures.CreateProject(1));
            var service = new BlockingExecService();
            var coordinator = new ExtensionOperationCoordinator();
            var host = new ProjectHostImpl(new DatabaseReaderWriter(new ProjectManager(_root)), coordinator);
            var exec = new TaskExecHostImpl(service, host, coordinator);

            var run = exec.ExecuteAllAsync(ExecutionRequest.Empty);   // parks inside the execution service
            Assert.True(exec.IsRunning);

            var rejected = await host.LoadAsync(project);

            Assert.False(rejected.Success);
            Assert.Equal(ProjectOpenError.Busy, rejected.Error);
            Assert.Null(host.CurrentPath);

            service.Unblock();
            var result = await run;
            Assert.Equal(ExecutionOutcome.Success, result.Outcome);
            Assert.Equal(ExecutionFailureReason.None, result.FailureReason);

            // With the run over, the same load is accepted.
            Assert.True((await LoadAsync(host, project)).Success);
        }

        [Theory]
        [InlineData("null")]
        [InlineData("whitespace")]
        [InlineData("missing-file")]
        public async Task LoadAsync_PathNotFound_LeavesThePriorProjectIntact(string kind)
        {
            var project = Track(ProjectFixtures.CreateProject(2));
            var (host, _) = CreateIsolatedHost();
            Assert.True((await LoadAsync(host, project)).Success);
            var pathBefore = host.CurrentPath;
            var raised = 0;
            host.ProjectChanged += (_, _) => raised++;
            var path = kind switch
            {
                "null" => null!,
                "whitespace" => "   ",
                _ => Path.Combine(_root, "does-not-exist.rfPrj"),
            };

            var result = await host.LoadAsync(path);

            Assert.False(result.Success);
            Assert.Equal(ProjectOpenError.PathNotFound, result.Error);
            Assert.Equal(pathBefore, host.CurrentPath);
            Assert.Equal(2, host.TaskCount);
            Assert.Equal(0, raised);
        }

        // ── save: the path that was actually written ──

        [Fact]
        public void SaveAs_WithoutExtension_AppendsRfPrjAndRecordsTheCanonicalWrittenPath()
        {
            var (host, _) = CreateIsolatedHost();
            host.ReplaceTaskCollection(new object[] { new CommonTaskViewModel { CurrentTaskIndex = "0" } });
            var typed = Path.Combine(Path.GetTempPath(), $"rfidgear-test-{Guid.NewGuid():N}");
            var written = Track(typed + ".rfPrj");

            host.SaveAs(typed);

            Assert.True(File.Exists(written));
            Assert.Equal(Path.GetFullPath(written), host.CurrentPath);
        }

        [Fact]
        public void SaveAs_WithXmlExtension_WritesPlainXmlAndRecordsThatPath()
        {
            var (host, _) = CreateIsolatedHost();
            host.ReplaceTaskCollection(new object[] { new CommonTaskViewModel { CurrentTaskIndex = "0" } });
            var output = NewOutputPath(".xml");

            host.SaveAs(output);

            Assert.StartsWith("<?xml", File.ReadAllText(output));
            Assert.Equal(Path.GetFullPath(output), host.CurrentPath);
        }

        [Fact]
        public async Task SaveAs_WhenTheWriteFails_ThrowsKeepsCurrentPathAndReleasesTheGate()
        {
            var project = Track(ProjectFixtures.CreateProject(1));
            var (host, _) = CreateIsolatedHost();
            Assert.True((await LoadAsync(host, project)).Success);
            var pathBefore = host.CurrentPath;
            var unwritable = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing-directory", "p.rfPrj");

            Assert.ThrowsAny<IOException>(() => host.SaveAs(unwritable));

            Assert.Equal(pathBefore, host.CurrentPath);
            host.SaveAs(NewOutputPath(".rfPrj"));   // the gate was released, so a later save works
        }

        [Fact]
        public async Task Save_WritesToTheCurrentPath_AfterSaveAs()
        {
            var (writer, _) = CreateIsolatedHost();
            var (reader, _) = CreateIsolatedHost();
            var output = NewOutputPath(".rfPrj");
            writer.ReplaceTaskCollection(new object[] { new CommonTaskViewModel { CurrentTaskIndex = "0" } });
            writer.SaveAs(output);

            writer.ReplaceTaskCollection(new object[]
            {
                new CommonTaskViewModel { CurrentTaskIndex = "0" },
                new CommonTaskViewModel { CurrentTaskIndex = "1" },
                new CommonTaskViewModel { CurrentTaskIndex = "2" },
            });
            writer.Save();

            Assert.True((await LoadAsync(reader, output)).Success);
            Assert.Equal(3, reader.TaskCount);
        }

        [Fact]
        public void Save_WithoutAPath_Throws()
        {
            var (host, _) = CreateIsolatedHost();

            Assert.Throws<InvalidOperationException>(() => host.Save());
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
        public async Task ExecuteAllAsync_WhenTheLoopStoppedBeforeItsLastTask_ReportsFailureWithTheUnreachedTasksListed()
        {
            // A run the loop left unfinished (for example the Release watchdog stopped it) is a failure even though
            // no task that ran failed: Outcome is the field a caller trusts, and it must not say Success here.
            var (project, host, _) = CreateTriple(new EarlyEndExecService(reached: 1));
            project.ReplaceTaskCollection(new object[]
            {
                new StubTask { CurrentTaskIndex = "10" },
                new StubTask { CurrentTaskIndex = "11" },
                new StubTask { CurrentTaskIndex = "12" },
            });

            var result = await host.ExecuteAllAsync(ExecutionRequest.Empty);

            Assert.Equal(ExecutionOutcome.Failure, result.Outcome);
            Assert.Equal(ExecutionFailureReason.None, result.FailureReason);
            Assert.Equal(3, result.TotalTasks);
            Assert.Equal(0, result.FailedTasks);
            Assert.Equal(new bool?[] { true, null, null }, result.TaskOutcomes.Select(o => o.IsSuccess));
            Assert.Equal(
                new string?[] { null, TaskSkipReasons.NotReached, TaskSkipReasons.NotReached },
                result.TaskOutcomes.Select(o => o.SkipReason));
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
                    request.Observations!.Add(TestObservations.Executed(0, first, success: true));
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
            Assert.Equal(TaskSkipReasons.NotReached, result.TaskOutcomes[1].SkipReason);
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
                Assert.Equal(TaskSkipReasons.NotReached, o.SkipReason);
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

        [Fact]
        public async Task ResetStatus_WhileARealRunIsInFlight_IsRefusedAndTheRunIsUndisturbed()
        {
            var service = new BlockingExecService();
            var (project, host, coordinator) = CreateTriple(service);
            var task = new StubTask();
            project.ReplaceTaskCollection(new object[] { task });

            var run = host.ExecuteAllAsync(ExecutionRequest.Empty);   // parks inside the execution service
            Assert.True(host.IsRunning);

            // The run has meanwhile finished the task. A reset now would clear what the loop still reads.
            task.IsTaskCompletedSuccessfully = true;
            task.CurrentTaskErrorLevel = ERROR.AuthFailure;
            task.ExecutionState = TaskExecutionState.Completed;

            var refused = Assert.Throws<InvalidOperationException>(() => host.ResetStatus());

            Assert.Contains("task run is in progress", refused.Message, StringComparison.OrdinalIgnoreCase);
            Assert.True(task.IsTaskCompletedSuccessfully);
            Assert.Equal(ERROR.AuthFailure, task.CurrentTaskErrorLevel);
            Assert.Equal(TaskExecutionState.Completed, task.ExecutionState);
            Assert.True(host.IsRunning, "A refused reset must not release the run's slot.");

            service.Unblock();
            var result = await run;
            Assert.Equal(ExecutionOutcome.Success, result.Outcome);
            Assert.False(host.IsRunning);

            // With the run over, the same call is accepted.
            host.ResetStatus();
            Assert.Null(task.IsTaskCompletedSuccessfully);
            Assert.True(coordinator.TryBeginExecution(), "The reset must leave the coordinator idle.");
            coordinator.EndExecution();
        }

        [Theory]
        [InlineData("load", "project load")]
        [InlineData("edit", "project edit")]
        [InlineData("run", "task run")]
        public void ResetStatus_WhileAnotherOperationOwnsTheCoordinator_IsRefusedAndKeepsThatOperationsSlot(string operation, string expectedInMessage)
        {
            var (project, host, coordinator) = CreateTriple();
            var task = new StubTask
            {
                IsTaskCompletedSuccessfully = true,
                CurrentTaskErrorLevel = ERROR.AuthFailure,
                ExecutionState = TaskExecutionState.Completed,
            };
            project.ReplaceTaskCollection(new object[] { task });

            var admitted = operation switch
            {
                "load" => coordinator.TryBeginLoad(),
                "edit" => coordinator.TryBeginProjectOperation(),
                _ => coordinator.TryBeginExecution(),
            };
            Assert.True(admitted);

            var refused = Assert.Throws<InvalidOperationException>(() => host.ResetStatus());

            Assert.Contains(expectedInMessage, refused.Message, StringComparison.OrdinalIgnoreCase);
            Assert.True(task.IsTaskCompletedSuccessfully);
            Assert.Equal(ERROR.AuthFailure, task.CurrentTaskErrorLevel);
            Assert.Equal(TaskExecutionState.Completed, task.ExecutionState);

            // The refused call must not have released the slot it never owned.
            Assert.Equal(operation == "load", coordinator.IsLoading);
            Assert.Equal(operation == "edit", coordinator.IsMutating);
            Assert.Equal(operation == "run", coordinator.IsExecuting);
        }

        [Fact]
        public void ResetStatus_WhenClearingFailsPartWay_StillReleasesTheCoordinator()
        {
            var (project, host, coordinator) = CreateTriple();
            var task = new StubTask { OnExecutionStateAssigned = () => throw new InvalidOperationException("boom") };
            project.ReplaceTaskCollection(new object[] { task });

            Assert.Throws<InvalidOperationException>(() => host.ResetStatus());

            Assert.True(coordinator.TryBeginExecution(), "A failed reset must not leave the host busy forever.");
            coordinator.EndExecution();
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

        [Theory]
        [InlineData("existing-folder")]
        [InlineData("folder-with-trailing-separator")]
        [InlineData("relative-path-with-folder")]
        [InlineData("bare-file-name")]
        public async Task ExecuteAllAsync_ReportTargetThatCannotBeAFile_ThrowsBeforeAcceptingRun(string kind)
        {
            var dir = CreateTempDirectory();
            try
            {
                var target = kind switch
                {
                    // The folder exists, so a folder-only check lets all of these through.
                    "existing-folder" => dir,
                    "folder-with-trailing-separator" => dir + Path.DirectorySeparatorChar,
                    "relative-path-with-folder" => Path.Combine("reports", "out.pdf"),
                    "bare-file-name" => "out.pdf",
                    _ => throw new ArgumentOutOfRangeException(nameof(kind)),
                };
                var execService = new CapturingExecService();
                var (_, host, _) = CreateTriple(execService);
                var eventCount = 0;
                host.ExecutionCompleted += (_, _) => eventCount++;

                var ex = await Assert.ThrowsAsync<ArgumentException>(
                    () => host.ExecuteAllAsync(Vars(("REPORTTARGETPATH", target))));

                Assert.Contains("REPORTTARGETPATH", ex.Message);
                Assert.Null(execService.CapturedRequest);   // no card operation was started
                Assert.Equal(0, eventCount);
                Assert.False(host.IsRunning);
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }

        [Fact]
        public async Task ExecuteAllAsync_Timestamps_AreUtcAndBracketTheRun()
        {
            var (_, host, _) = CreateTriple(new CapturingExecService());
            var before = DateTimeOffset.UtcNow;

            var result = await host.ExecuteAllAsync(ExecutionRequest.Empty);

            var after = DateTimeOffset.UtcNow;
            Assert.InRange(result.StartedAt, before, after);
            Assert.InRange(result.CompletedAt, result.StartedAt, after);
            Assert.Equal(TimeSpan.Zero, result.StartedAt.Offset);
            Assert.Equal(TimeSpan.Zero, result.CompletedAt.Offset);
        }

        [Fact]
        public async Task ExecuteAllAsync_FailedRun_StillCarriesUtcTimestamps()
        {
            var (_, host, _) = CreateTriple(new FaultingExecService());
            var before = DateTimeOffset.UtcNow;

            var result = await host.ExecuteAllAsync(ExecutionRequest.Empty);

            var after = DateTimeOffset.UtcNow;
            Assert.InRange(result.StartedAt, before, after);
            Assert.InRange(result.CompletedAt, result.StartedAt, after);
            Assert.Equal(TimeSpan.Zero, result.CompletedAt.Offset);
        }

        [Fact]
        public async Task ExecuteAllAsync_CancelledRun_RaisesExactlyOneTerminalResult()
        {
            var (_, host, _) = CreateTriple(new CancellingExecService());
            var raised = new List<ExecResult>();
            host.ExecutionCompleted += (_, r) => raised.Add(r);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var result = await host.ExecuteAllAsync(ExecutionRequest.Empty, cts.Token);

            Assert.Same(result, Assert.Single(raised));
        }

        [Fact]
        public async Task ExecuteAllAsync_FaultedRun_RaisesExactlyOneTerminalResult()
        {
            var (_, host, _) = CreateTriple(new FaultingExecService());
            var raised = new List<ExecResult>();
            host.ExecutionCompleted += (_, r) => raised.Add(r);

            var result = await host.ExecuteAllAsync(ExecutionRequest.Empty);

            Assert.Same(result, Assert.Single(raised));
        }

        [Fact]
        public async Task ExecuteAllAsync_FaultLog_DoesNotRepeatSecretsFromTheExceptionMessage()
        {
            const string secret = "00112233445566778899AABBCCDDEEFF";   // key-sized hex
            var (_, host, _) = CreateTriple(new ThrowingExecService(
                () => new InvalidOperationException("Key=" + secret + " and again " + secret)));
            using var logs = CapturedLogs.Install();

            var result = await host.ExecuteAllAsync(ExecutionRequest.Empty);

            Assert.Equal(ExecutionFailureReason.Faulted, result.FailureReason);
            var entry = Assert.Single(logs.From(nameof(TaskExecHostImpl)), e => e.Level == Serilog.Events.LogEventLevel.Error);
            // The execution service redacts these messages; attaching the raw exception here would write
            // the original text a second time.
            Assert.Null(entry.Exception);
            var written = CapturedLogs.EverythingWrittenFor(entry);
            Assert.DoesNotContain(secret, written, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("[REDACTED", written);
            Assert.Contains(nameof(InvalidOperationException), written);
            Assert.Contains(result.RunId, written);
        }

        [Fact]
        public async Task ExecuteAllAsync_TimeoutLog_DoesNotRepeatSecretsFromTheExceptionMessage()
        {
            const string secret = "00112233445566778899AABBCCDDEEFF";
            var (_, host, _) = CreateTriple(new ThrowingExecService(
                () => new TimeoutException("Key=" + secret)));
            using var logs = CapturedLogs.Install();

            var result = await host.ExecuteAllAsync(ExecutionRequest.Empty);

            Assert.Equal(ExecutionFailureReason.TimedOut, result.FailureReason);
            var entry = Assert.Single(logs.From(nameof(TaskExecHostImpl)), e => e.Level == Serilog.Events.LogEventLevel.Warning);
            Assert.Null(entry.Exception);
            Assert.DoesNotContain(secret, CapturedLogs.EverythingWrittenFor(entry), StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ExecuteAllAsync_SubscriberFault_IsLoggedWithoutLeakingSecretsFromItsMessage()
        {
            const string secret = "00112233445566778899AABBCCDDEEFF";
            var (_, host, _) = CreateTriple(new CapturingExecService());
            host.ExecutionCompleted += (_, _) => throw new InvalidOperationException("Key=" + secret);
            using var logs = CapturedLogs.Install();

            var result = await host.ExecuteAllAsync(ExecutionRequest.Empty);

            Assert.Equal(ExecutionOutcome.Success, result.Outcome);   // the run is unaffected
            var entry = Assert.Single(logs.From(nameof(TaskExecHostImpl)), e => e.Level == Serilog.Events.LogEventLevel.Error);
            Assert.Null(entry.Exception);
            var written = CapturedLogs.EverythingWrittenFor(entry);
            Assert.DoesNotContain(secret, written, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("[REDACTED", written);
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
                {
                    cts.Cancel();   // cancellation arrives while task 0 is running
                }
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
                Assert.Equal(TaskSkipReasons.NotReached, o.SkipReason);
            });
            Assert.False(host.IsRunning);
        }

        [Fact]
        public async Task ExecuteAllAsync_TaskThrows_ReportsFaultedWithPartialOutcomes()
        {
            var service = new DescriptorInjectingService((i, task) => ct =>
            {
                if (i == 1)
                {
                    throw new InvalidOperationException("task blew up");
                }
                MarkSuccess(task);
                return Task.CompletedTask;
            });
            var (project, host) = Create(service);
            project.ReplaceTaskCollection(NewTasks(3));

            var result = await host.ExecuteAllAsync(ExecutionRequest.Empty);

            Assert.Equal(ExecutionOutcome.Failure, result.Outcome);
            Assert.Equal(ExecutionFailureReason.Faulted, result.FailureReason);
            Assert.Equal(3, result.TaskOutcomes.Count);
            Assert.True(result.TaskOutcomes[0].IsSuccess);

            // The task that threw is a failure, not an unknown: it ran and did not complete.
            Assert.False(result.TaskOutcomes[1].IsSuccess);
            Assert.Null(result.TaskOutcomes[1].SkipReason);
            Assert.Equal(1, result.FailedTasks);

            // The run never got to the third task.
            Assert.Null(result.TaskOutcomes[2].IsSuccess);
            Assert.Equal(TaskSkipReasons.NotReached, result.TaskOutcomes[2].SkipReason);
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

        [Theory]
        [InlineData("german", "de-DE")]
        [InlineData("english", "en-US")]
        public void AppContext_ThroughTheHostInterface_ReportsTheConfiguredLanguage(string configured, string expected)
        {
            var appData = Path.Combine(Path.GetTempPath(), "RFiDGear.Tests", Guid.NewGuid().ToString("N"));
            try
            {
                var live = new ChipTaskHandlerModel();
                using var host = new HeadlessExtensionHost(
                    new DatabaseReaderWriter(new ProjectManager(appData)),
                    new CapturingExecService(),
                    () => live,
                    action => { action(); return Task.CompletedTask; },
                    _ => { },
                    string.Empty,
                    new ExtensionOperationCoordinator(),
                    () => configured);
                IRFiDGearExtensionHost api = host;

                Assert.Equal(expected, api.AppContext.UICultureName);
                Assert.False(string.IsNullOrWhiteSpace(api.AppContext.RFiDGearVersion));
            }
            finally
            {
                Directory.Delete(appData, recursive: true);
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

    // ── TaskOutcomeMapper: what the loop did becomes the outcome list ─────────

    public class TaskOutcomeMapperTests
    {
        private static StubTask Task(string index) => new StubTask { CurrentTaskIndex = index };

        [Fact]
        public void SkipReasonTokens_AreStableContractValues()
        {
            Assert.Equal("ExecuteConditionNotMet", TaskSkipReasons.ExecuteConditionNotMet);
            Assert.Equal("NotExecuted", TaskSkipReasons.NotExecuted);
            Assert.Equal("TaskModelUnavailable", TaskSkipReasons.TaskModelUnavailable);
            Assert.Equal("NotReached", TaskSkipReasons.NotReached);
        }

        [Fact]
        public void ConditionSkippedTask_ReportsTheRealReasonEvenThoughItsStateSaysCompleted()
        {
            // Handlers mark a task Completed after skipping it, so state cannot tell skip from run.
            var ran = Task("0");
            var skipped = Task("1");
            skipped.ExecutionState = TaskExecutionState.Completed;
            var observations = new[]
            {
                TestObservations.Executed(0, ran, success: true),
                TestObservations.Skipped(1, skipped, TaskSkipReasons.ExecuteConditionNotMet),
            };

            var outcomes = TaskOutcomeMapper.Build(new object?[] { ran, skipped }, observations);

            Assert.Equal(TaskSkipReasons.ExecuteConditionNotMet, outcomes[1].SkipReason);
            Assert.Null(outcomes[1].IsSuccess);
            Assert.Equal(1, outcomes[1].Position);
        }

        [Fact]
        public void ExecutedTask_WhoseExecutionStateWasNeverUpdated_HasNoSkipReason()
        {
            // Generic-chip tasks run without ever touching ExecutionState.
            var chipCheck = Task("0");
            Assert.Equal(TaskExecutionState.NotStarted, chipCheck.ExecutionState);

            var outcomes = TaskOutcomeMapper.Build(
                new object?[] { chipCheck },
                new[] { TestObservations.Executed(0, chipCheck, success: true) });

            var outcome = Assert.Single(outcomes);
            Assert.True(outcome.IsSuccess);
            Assert.Null(outcome.SkipReason);
        }

        [Fact]
        public void ExecutedTask_WhoseSuccessIsUnknown_IsNullWithNoSkipReason()
        {
            var task = Task("0");

            var outcome = Assert.Single(TaskOutcomeMapper.Build(
                new object?[] { task },
                new[] { TestObservations.Executed(0, task, success: null, error: ERROR.Empty) }));

            Assert.Null(outcome.IsSuccess);
            Assert.Null(outcome.SkipReason);
        }

        [Fact]
        public void FailedTask_IsReportedAsFailure()
        {
            var task = Task("0");

            var outcome = Assert.Single(TaskOutcomeMapper.Build(
                new object?[] { task },
                new[] { TestObservations.Executed(0, task, success: false, error: ERROR.AuthFailure) }));

            Assert.False(outcome.IsSuccess);
            Assert.Equal(ERROR.AuthFailure.ToString(), outcome.NormalizedErrorCode);
        }

        [Fact]
        public void OutOfOrderObservations_AreReportedInExecutionOrderThenUnvisitedTasks()
        {
            var tasks = new object?[] { Task("0"), Task("1"), Task("2") };
            // The run jumped from task 0 straight to task 2.
            var observations = new[]
            {
                TestObservations.Executed(0, (IGenericTask)tasks[0]!, success: true),
                TestObservations.Executed(2, (IGenericTask)tasks[2]!, success: true),
            };

            var outcomes = TaskOutcomeMapper.Build(tasks, observations);

            Assert.Equal(new[] { "0", "2", "1" }, outcomes.Select(o => o.ProjectTaskIndex));
            Assert.Equal(new[] { 0, 1, 2 }, outcomes.Select(o => o.Position));
            Assert.Equal(TaskSkipReasons.NotReached, outcomes[2].SkipReason);
            Assert.Null(outcomes[2].IsSuccess);
            Assert.Equal(ERROR.Empty.ToString(), outcomes[2].NormalizedErrorCode);
        }

        [Fact]
        public void ObservationWithoutTaskModel_UsesUnknownTypeNameAndThePositionAsIndex()
        {
            var observation = new TaskExecutionObservation(
                1, taskId: null!, task: null!, TaskLoopTerminalEvaluator.SkippedDecision,
                wasSuccessful: null, ERROR.Empty, TaskSkipReasons.TaskModelUnavailable);

            var outcome = Assert.Single(TaskOutcomeMapper.Build(new object?[] { null, null }, new[] { observation }),
                o => o.SkipReason == TaskSkipReasons.TaskModelUnavailable);

            Assert.Equal("Unknown", outcome.TaskTypeName);
            Assert.Equal("1", outcome.ProjectTaskIndex);
        }

        [Fact]
        public void WithoutObservations_EveryTaskIsNotReached()
        {
            var tasks = new object?[] { Task("0"), Task("1") };

            var outcomes = TaskOutcomeMapper.Build(tasks, Array.Empty<TaskExecutionObservation>());

            Assert.Equal(2, outcomes.Count);
            Assert.All(outcomes, o =>
            {
                Assert.Null(o.IsSuccess);
                Assert.Equal(TaskSkipReasons.NotReached, o.SkipReason);
            });
        }

        [Fact]
        public void EveryProjectTask_AppearsExactlyOnce()
        {
            var tasks = Enumerable.Range(0, 5).Select(i => (object?)Task(i.ToString())).ToArray();
            var observations = new[]
            {
                TestObservations.Executed(4, (IGenericTask)tasks[4]!, success: true),
                TestObservations.Executed(1, (IGenericTask)tasks[1]!, success: false, error: ERROR.TransportError),
            };

            var outcomes = TaskOutcomeMapper.Build(tasks, observations);

            Assert.Equal(5, outcomes.Count);
            Assert.Equal(new[] { "0", "1", "2", "3", "4" }, outcomes.Select(o => o.ProjectTaskIndex).OrderBy(i => i));
            Assert.Equal(Enumerable.Range(0, 5), outcomes.Select(o => o.Position));
        }

        [Fact]
        public void NonTaskItemInTheProject_IsReportedAsNotReachedWithItsTypeName()
        {
            var outcome = Assert.Single(TaskOutcomeMapper.Build(
                new[] { new object() },
                Array.Empty<TaskExecutionObservation>()));

            Assert.Equal("Object", outcome.TaskTypeName);
            Assert.Equal("0", outcome.ProjectTaskIndex);
        }
    }

    // ── TaskExecutionService: the observation sink ────────────────────────────

    public class TaskExecutionObservationSinkTests
    {
        [Fact]
        public async Task PrePopulatedSink_KeepsItsEntries_ButNeverInfluencesTheTerminalStatus()
        {
            var task = new StubTask { CurrentTaskIndex = "0" };
            // A failure the run did not produce. If the evaluation read the sink it would report Failure.
            var foreign = TestObservations.Executed(9, new StubTask { CurrentTaskIndex = "9" }, success: false, error: ERROR.TransportError);
            var sink = new List<TaskExecutionObservation> { foreign };
            var service = new TaskExecutionService(
                new ReaderDeviceProvider(), new NullDispatcherTimerAdapter(), new NullDispatcherTimerAdapter());
            var request = new TaskExecutionRequest
            {
                TaskDescriptors = new[]
                {
                    new TaskDescriptor(0, task, ct =>
                    {
                        task.ExecutionState = TaskExecutionState.Completed;
                        task.IsTaskCompletedSuccessfully = true;
                        task.CurrentTaskErrorLevel = ERROR.NoError;
                        return Task.CompletedTask;
                    }),
                },
                Observations = sink,
            };

            TaskExecutionResult result = null!;
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                result = await service.ExecuteOnceAsync(request);
            });

            Assert.Equal(TaskLoopTerminalStatus.Success, result.TerminalStatus);
            Assert.Equal(2, sink.Count);
            Assert.Same(foreign, sink[0]);
            Assert.Equal(TaskLoopTerminalEvaluator.ExecutedDecision, sink[1].Decision);
        }
    }

    /// <summary>Builds the observations the real task loop records, for use in test doubles.</summary>
    internal static class TestObservations
    {
        public static TaskExecutionObservation Executed(
            int position, IGenericTask task, bool? success, ERROR error = ERROR.NoError) =>
            new TaskExecutionObservation(
                position, task.CurrentTaskIndex, task, TaskLoopTerminalEvaluator.ExecutedDecision, success, error);

        public static TaskExecutionObservation Skipped(int position, IGenericTask task, string reason) =>
            new TaskExecutionObservation(
                position, task.CurrentTaskIndex, task, TaskLoopTerminalEvaluator.SkippedDecision,
                wasSuccessful: null, task.CurrentTaskErrorLevel, reason);
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

    /// <summary>
    /// Behaves like a task loop that stopped after its first <c>reached</c> tasks: it observed only those and
    /// reports the terminal status the real evaluator gives a run that was left unfinished, <c>Failure</c>.
    /// </summary>
    internal sealed class EarlyEndExecService : ITaskExecutionService
    {
        private readonly int _reached;

        public EarlyEndExecService(int reached) => _reached = reached;

        public event EventHandler<TaskExecutionCompletedEventArgs> ExecutionCompleted
        {
            add { }
            remove { }
        }

        public int CurrentTaskIndex => 0;

        public Task<TaskExecutionResult> ExecuteOnceAsync(
            TaskExecutionRequest request, CancellationToken ct = default)
        {
            var position = 0;
            foreach (var item in request.TaskHandler?.TaskCollection ?? new ObservableCollection<object>())
            {
                if (position >= _reached)
                {
                    break;
                }

                if (item is IGenericTask task)
                {
                    task.IsTaskCompletedSuccessfully = true;
                    task.CurrentTaskErrorLevel = ERROR.NoError;
                    request.Observations?.Add(TestObservations.Executed(position, task, success: true));
                }

                position++;
            }

            return Task.FromResult(new TaskExecutionResult
            {
                RunId = request.RunId,
                TerminalStatus = TaskLoopTerminalStatus.Failure
            });
        }

        public void HandleTaskTimeout() { }
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

            var position = 0;
            foreach (var item in MarkTasksCompleted
                ? request.TaskHandler?.TaskCollection ?? new ObservableCollection<object>()
                : new ObservableCollection<object>())
            {
                if (item is IGenericTask task)
                {
                    task.ExecutionState = TaskExecutionState.Completed;
                    task.IsTaskCompletedSuccessfully = true;
                    task.CurrentTaskErrorLevel = ERROR.NoError;

                    // Record what the real loop would: one observation per executed task.
                    request.Observations?.Add(TestObservations.Executed(position, task, success: true));
                }

                position++;
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
        private TaskExecutionState _executionState = TaskExecutionState.NotStarted;

        /// <summary>Runs whenever <see cref="ExecutionState"/> is assigned, so a test can make a reset fail part-way.</summary>
        public Action? OnExecutionStateAssigned { get; set; }

        public TaskExecutionState ExecutionState
        {
            get => _executionState;
            set
            {
                OnExecutionStateAssigned?.Invoke();
                _executionState = value;
            }
        }
    }
}
