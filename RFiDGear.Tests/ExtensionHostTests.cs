using RFiDGear.Contracts;
using RFiDGear.Infrastructure.ExtensionHost;
using RFiDGear.Infrastructure.FileAccess;
using RFiDGear.Services.TaskExecution;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

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
            // Any value that isn't "german" maps to "en-US"
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

    public class ProjectHostImplTests
    {
        [Fact]
        public async Task LoadAsync_NullPath_ReturnsPathNotFound()
        {
            var host = new ProjectHostImpl(new DatabaseReaderWriter());
            var result = await host.LoadAsync(null!);
            Assert.False(result.Success);
            Assert.Equal(ProjectOpenError.PathNotFound, result.Error);
        }

        [Fact]
        public async Task LoadAsync_WhitespacePath_ReturnsPathNotFound()
        {
            var host = new ProjectHostImpl(new DatabaseReaderWriter());
            var result = await host.LoadAsync("   ");
            Assert.False(result.Success);
            Assert.Equal(ProjectOpenError.PathNotFound, result.Error);
        }

        [Fact]
        public async Task LoadAsync_NonExistentFile_ReturnsPathNotFound()
        {
            var host = new ProjectHostImpl(new DatabaseReaderWriter());
            var result = await host.LoadAsync(@"C:\this\path\does\not\exist.rfPrj");
            Assert.False(result.Success);
            Assert.Equal(ProjectOpenError.PathNotFound, result.Error);
        }

        [Fact]
        public async Task LoadAsync_WhileExecutionRunning_ReturnsBusy()
        {
            var host = new ProjectHostImpl(
                new DatabaseReaderWriter(), isExecutionRunning: () => true);
            // Path doesn't matter — busy check happens before file-exists check.
            var result = await host.LoadAsync(@"C:\does-not-matter.rfPrj");
            Assert.False(result.Success);
            Assert.Equal(ProjectOpenError.Busy, result.Error);
        }

        [Fact]
        public async Task LoadAsync_InvalidXmlFile_ReturnsFailureWithoutSettingCurrentPath()
        {
            var tmp = Path.GetTempFileName();
            try
            {
                await File.WriteAllTextAsync(tmp, "this is not xml");
                var host = new ProjectHostImpl(new DatabaseReaderWriter());

                ProjectOpenResult result = null!;
                await StaTestRunner.RunOnStaThreadAsync(async () =>
                {
                    result = await host.LoadAsync(tmp);
                });

                Assert.False(result.Success);
                Assert.True(
                    result.Error is ProjectOpenError.UnsupportedFormat or ProjectOpenError.Unknown,
                    $"Expected UnsupportedFormat or Unknown, got {result.Error}");
                // CurrentPath must not be set when load fails.
                Assert.Null(host.CurrentPath);
            }
            finally
            {
                File.Delete(tmp);
            }
        }

        [Fact]
        public async Task LoadAsync_CancellationBeforeRead_ThrowsOperationCanceled()
        {
            var tmp = Path.GetTempFileName();
            try
            {
                await File.WriteAllTextAsync(tmp, "data");
                var host = new ProjectHostImpl(new DatabaseReaderWriter());
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
            var host = new ProjectHostImpl(new DatabaseReaderWriter());
            bool fired = false;
            host.ProjectChanged += (_, _) => fired = true;

            await host.LoadAsync("no-such-file.rfPrj");

            Assert.False(fired);
        }
    }

    // ── TaskExecHostImpl — exception/cancel handling ──────────────────────────

    public class TaskExecHostImplTests
    {
        [Fact]
        public async Task ExecuteAllAsync_WhenServiceThrowsOperationCanceled_ReturnsFailureResult()
        {
            var execService = new CancellingExecService();
            var project = new ProjectHostImpl(new DatabaseReaderWriter());
            var host = new TaskExecHostImpl(execService, project);

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
            var project = new ProjectHostImpl(new DatabaseReaderWriter());
            var host = new TaskExecHostImpl(execService, project);

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
            var project = new ProjectHostImpl(new DatabaseReaderWriter());
            var host = new TaskExecHostImpl(execService, project);

            var firstRunTask = host.ExecuteAllAsync(ExecutionRequest.Empty);
            // Give first run time to start.
            await Task.Delay(20);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => host.ExecuteAllAsync(ExecutionRequest.Empty));

            execService.Unblock();
            await firstRunTask;
        }

        [Fact]
        public async Task ExecuteAllAsync_WithVariables_PassesThroughToService()
        {
            var execService = new CapturingExecService();
            var project = new ProjectHostImpl(new DatabaseReaderWriter());
            var host = new TaskExecHostImpl(execService, project);

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
            var project = new ProjectHostImpl(new DatabaseReaderWriter());
            var host = new TaskExecHostImpl(execService, project);

            var result = await host.ExecuteAllAsync(ExecutionRequest.Empty);

            Assert.Empty(result.TaskOutcomes);
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
            return Task.FromResult(new TaskExecutionResult
            {
                TerminalStatus = TaskLoopTerminalStatus.Success
            });
        }

        public void HandleTaskTimeout() { }
    }
}
