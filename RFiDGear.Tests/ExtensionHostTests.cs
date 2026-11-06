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
        public async Task LoadAsync_InvalidXmlFile_ReturnsFailureWithoutSettingCurrentPath()
        {
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
                Assert.True(
                    result.Error is ProjectOpenError.UnsupportedFormat or ProjectOpenError.Unknown,
                    $"Expected UnsupportedFormat or Unknown, got {result.Error}");
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
        public async Task ExecuteAllAsync_WhenServiceThrowsOperationCanceled_ReturnsFailureResult()
        {
            var execService = new CancellingExecService();
            var (_, host, _) = CreateTriple(execService);

            ExecResult? raisedResult = null;
            host.ExecutionCompleted += (_, r) => raisedResult = r;

            var result = await host.ExecuteAllAsync(ExecutionRequest.Empty);

            Assert.Equal(ExecutionOutcome.Failure, result.Outcome);
            Assert.False(host.IsRunning, "Running flag must be released on cancellation.");
            Assert.NotNull(raisedResult);
            Assert.Equal(ExecutionOutcome.Failure, raisedResult!.Outcome);
        }

        [Fact]
        public async Task ExecuteAllAsync_WhenServiceThrowsGenericException_ReturnsFailureResult()
        {
            var execService = new FaultingExecService();
            var (_, host, _) = CreateTriple(execService);

            ExecResult? raisedResult = null;
            host.ExecutionCompleted += (_, r) => raisedResult = r;

            var result = await host.ExecuteAllAsync(ExecutionRequest.Empty);

            Assert.Equal(ExecutionOutcome.Failure, result.Outcome);
            Assert.False(host.IsRunning, "Running flag must be released on fault.");
            Assert.NotNull(raisedResult);
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
            await firstRunTask;
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
        public async Task ExecuteAllAsync_ThrowingSubscriber_EventFiredExactlyOnceAndCoordinatorReleased()
        {
            var execService = new CapturingExecService();
            var (_, host, _) = CreateTriple(execService);

            int eventCount = 0;
            host.ExecutionCompleted += (_, _) =>
            {
                Interlocked.Increment(ref eventCount);
                throw new InvalidOperationException("subscriber fault");
            };

            // The subscriber's exception propagates out of ExecuteAllAsync.
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => host.ExecuteAllAsync(ExecutionRequest.Empty));

            Assert.Equal(1, eventCount);
            Assert.False(host.IsRunning, "Coordinator must be released even when subscriber throws.");
        }

        [Fact]
        public async Task ExecuteAllAsync_WhenCancelled_PartialTaskOutcomesPreserved()
        {
            // Seed one task already marked as completed before the "cancellation" hits.
            var execService = new CancellingExecService();
            var (project, host, _) = CreateTriple(execService);

            var completedTask = new StubTask
            {
                IsTaskCompletedSuccessfully = true,
                CurrentTaskErrorLevel = ERROR.NoError,
                ExecutionState = TaskExecutionState.Completed,
                CurrentTaskIndex = "0"
            };
            project.ReplaceTaskCollection(new object[] { completedTask });

            var result = await host.ExecuteAllAsync(ExecutionRequest.Empty);

            Assert.Equal(ExecutionOutcome.Failure, result.Outcome);
            Assert.Single(result.TaskOutcomes);
            // The pre-seeded completed state was read by BuildTaskOutcomes in the catch block.
            Assert.Equal(true, result.TaskOutcomes[0].IsSuccess);
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
            var fixturePath = GenerateSingleCommonTaskFixture();
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
                    new KeyValuePair<string, string>("REPORTTARGETPATH", @"C:\Reports\out.pdf"),
                    new KeyValuePair<string, string>("$JOBNUMBER", "JOB-001"),
                    new KeyValuePair<string, string>("$CHIPNUMBER", "CHIP-042"),
                };
                var execResult = await host.Execution.ExecuteAllAsync(new ExecutionRequest(variables));

                // The execution service received the full variables dictionary.
                Assert.NotNull(execService.CapturedRequest);
                Assert.Equal(@"C:\Reports\out.pdf", execService.CapturedRequest!.VariablesFromArgs["REPORTTARGETPATH"]);
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
            }
        }

        /// <summary>
        /// Generates a temporary <c>.rfPrj</c> archive containing one <see cref="CommonTaskViewModel"/>.
        /// The archive is written to a uniquely-named file in the system temp directory.
        /// Caller is responsible for deleting the returned path when the test completes.
        /// </summary>
        /// <returns>Full path of the generated fixture file.</returns>
        private static string GenerateSingleCommonTaskFixture()
        {
            var model = new ChipTaskHandlerModel();
            model.TaskCollection.Add(new CommonTaskViewModel());

            var path = Path.Combine(Path.GetTempPath(), $"rfidgear-test-{Guid.NewGuid():N}.rfPrj");
            var serializer = new XmlSerializer(typeof(ChipTaskHandlerModel));
            using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry("taskdatabase.xml");
                using var entryStream = entry.Open();
                // Serialize directly to a UTF-8 StreamWriter so the XML declaration
                // matches the byte encoding and XmlDocument.Load succeeds.
                using var writer = new StreamWriter(entryStream, new UTF8Encoding(false));
                serializer.Serialize(writer, model);
            }

            return path;
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
            foreach (var task in request.TaskHandler?.TaskCollection?.OfType<IGenericTask>()
                ?? Enumerable.Empty<IGenericTask>())
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
