using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using RFiDGear.Contracts;
using RFiDGear.Infrastructure.ExtensionHost;
using RFiDGear.Infrastructure.FileAccess;
using RFiDGear.Services.TaskExecution;

using Serilog.Events;
using Xunit;

namespace RFiDGear.Tests
{
    /// <summary>
    /// The coordinator is the one place that knows when a run starts and ends, whoever started it, so the
    /// change notification is raised from there.
    /// </summary>
    public class ExtensionOperationCoordinatorChangeTests
    {
        [Fact]
        public void TryBeginExecution_RaisesOnceAfterTheStateHasChanged()
        {
            var coordinator = new ExtensionOperationCoordinator();
            var observed = new List<bool>();
            coordinator.IsExecutingChanged += (_, _) => observed.Add(coordinator.IsExecuting);

            Assert.True(coordinator.TryBeginExecution());

            Assert.Equal(new[] { true }, observed);
        }

        [Fact]
        public void EndExecution_RaisesOnceAfterTheStateHasChanged()
        {
            var coordinator = new ExtensionOperationCoordinator();
            Assert.True(coordinator.TryBeginExecution());
            var observed = new List<bool>();
            coordinator.IsExecutingChanged += (_, _) => observed.Add(coordinator.IsExecuting);

            coordinator.EndExecution();

            Assert.Equal(new[] { false }, observed);
        }

        [Fact]
        public void RefusedAttempts_RaiseNothing()
        {
            var executing = new ExtensionOperationCoordinator();
            Assert.True(executing.TryBeginExecution());
            var loading = new ExtensionOperationCoordinator();
            Assert.True(loading.TryBeginLoad());
            var mutating = new ExtensionOperationCoordinator();
            Assert.True(mutating.TryBeginProjectOperation());

            var raised = 0;
            foreach (var coordinator in new[] { executing, loading, mutating })
            {
                coordinator.IsExecutingChanged += (_, _) => raised++;
            }

            Assert.False(executing.TryBeginExecution());
            Assert.False(executing.TryBeginLoad());
            Assert.False(executing.TryBeginProjectOperation());
            Assert.False(loading.TryBeginExecution());
            Assert.False(mutating.TryBeginExecution());

            Assert.Equal(0, raised);
        }

        [Fact]
        public void LoadsAndProjectEdits_RaiseNothing()
        {
            var coordinator = new ExtensionOperationCoordinator();
            var raised = 0;
            coordinator.IsExecutingChanged += (_, _) => raised++;

            Assert.True(coordinator.TryBeginLoad());
            coordinator.EndLoad();
            Assert.True(coordinator.TryBeginProjectOperation());
            coordinator.EndProjectOperation();

            Assert.Equal(0, raised);
        }

        [Fact]
        public void ReleasingWhileNotExecuting_RaisesNothingButStillResets()
        {
            var raised = 0;

            var idle = new ExtensionOperationCoordinator();
            idle.IsExecutingChanged += (_, _) => raised++;
            idle.EndExecution();

            var loading = new ExtensionOperationCoordinator();
            loading.IsExecutingChanged += (_, _) => raised++;
            Assert.True(loading.TryBeginLoad());
            loading.EndExecution();   // the unconditional reset these methods have always had
            Assert.False(loading.IsLoading);

            var mutating = new ExtensionOperationCoordinator();
            mutating.IsExecutingChanged += (_, _) => raised++;
            Assert.True(mutating.TryBeginProjectOperation());
            mutating.EndLoad();
            Assert.False(mutating.IsMutating);

            Assert.Equal(0, raised);
            Assert.True(loading.TryBeginExecution());   // all three really are idle again
        }

        [Fact]
        public void EndLoadAndEndProjectOperation_WhileExecuting_RaiseBecauseTheyReleaseTheRun()
        {
            var viaLoad = new ExtensionOperationCoordinator();
            var viaProject = new ExtensionOperationCoordinator();
            var observed = new List<string>();
            viaLoad.IsExecutingChanged += (_, _) => observed.Add("load:" + viaLoad.IsExecuting);
            viaProject.IsExecutingChanged += (_, _) => observed.Add("project:" + viaProject.IsExecuting);
            Assert.True(viaLoad.TryBeginExecution());
            Assert.True(viaProject.TryBeginExecution());
            observed.Clear();

            viaLoad.EndLoad();
            viaProject.EndProjectOperation();

            Assert.Equal(new[] { "load:False", "project:False" }, observed);
        }

        [Fact]
        public void ThrowingHandler_IsLoggedAndDoesNotHoldTheGateOrStarveTheOthers()
        {
            using var logs = CapturedLogs.Install();
            var coordinator = new ExtensionOperationCoordinator();
            var second = new List<bool>();
            coordinator.IsExecutingChanged += (_, _) => throw new InvalidOperationException("handler failure");
            coordinator.IsExecutingChanged += (_, _) => second.Add(coordinator.IsExecuting);

            Assert.True(coordinator.TryBeginExecution());   // a throwing handler must not turn this into an exception
            Assert.True(coordinator.IsExecuting);
            coordinator.EndExecution();                      // nor stop the release
            Assert.False(coordinator.IsExecuting);

            Assert.Equal(new[] { true, false }, second);
            var errors = logs.From(nameof(ExtensionOperationCoordinator)).Where(e => e.Level == LogEventLevel.Error).ToList();
            Assert.Equal(2, errors.Count);
            Assert.True(coordinator.TryBeginExecution(), "the gate can be taken again");
        }

        [Fact]
        public void StartingAgainFromTheNotification_IsRefused()
        {
            var coordinator = new ExtensionOperationCoordinator();
            bool? nestedAdmitted = null;
            var notifications = 0;
            coordinator.IsExecutingChanged += (_, _) =>
            {
                // Only the first notification starts again; a regression that also announced the refused attempt
                // would otherwise recurse until the test host crashes instead of failing this assertion.
                if (notifications++ == 0 && coordinator.IsExecuting)
                {
                    nestedAdmitted = coordinator.TryBeginExecution();
                }
            };

            Assert.True(coordinator.TryBeginExecution());

            Assert.False(nestedAdmitted);
            Assert.Equal(1, notifications);   // the refused nested attempt announced nothing
        }

        [Fact]
        public async Task ConcurrentRuns_RaiseOneChangePerAdmissionAndPerRelease()
        {
            var coordinator = new ExtensionOperationCoordinator();
            var raised = 0;
            var admitted = 0;
            coordinator.IsExecutingChanged += (_, _) => Interlocked.Increment(ref raised);

            await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
            {
                for (var i = 0; i < 200; i++)
                {
                    if (coordinator.TryBeginExecution())
                    {
                        Interlocked.Increment(ref admitted);
                        coordinator.EndExecution();
                    }
                }
            })));

            Assert.True(admitted > 0);
            Assert.Equal(2 * admitted, raised);
            Assert.False(coordinator.IsExecuting);
        }
    }

    /// <summary>
    /// What an extension sees: <see cref="ITaskExecHost.IsRunningChanged"/>, republished by the host with itself as
    /// the sender and the guarantees the contract documents.
    /// </summary>
    [Collection("AppDataPath")]
    public class TaskExecHostIsRunningChangedTests
    {
        private static TaskExecHostImpl CreateHost(ITaskExecutionService service)
        {
            var coordinator = new ExtensionOperationCoordinator();
            var project = new ProjectHostImpl(new DatabaseReaderWriter(), coordinator);
            return new TaskExecHostImpl(service, project, coordinator);
        }

        [Fact]
        public async Task AcceptedRun_RaisesTrueThenWorkThenFalseThenCompleted()
        {
            var events = new List<string>();
            var host = CreateHost(new CallbackExecService(() => events.Add("work")));
            host.IsRunningChanged += (_, _) => events.Add("running:" + host.IsRunning);
            host.ExecutionCompleted += (_, _) => events.Add("completed:" + host.IsRunning);

            await host.ExecuteAllAsync(ExecutionRequest.Empty);

            Assert.Equal(
                new[] { "running:True", "work", "running:False", "completed:False" },
                events);
        }

        [Fact]
        public async Task TheHostIsTheSender_NotTheCoordinator()
        {
            var host = CreateHost(new CapturingExecService());
            var senders = new List<object?>();
            ITaskExecHost api = host;   // what an extension sees: the event as declared on the contract
            api.IsRunningChanged += (sender, _) => senders.Add(sender);

            await host.ExecuteAllAsync(ExecutionRequest.Empty);

            Assert.Equal(2, senders.Count);
            Assert.All(senders, sender => Assert.Same(host, sender));
        }

        [Fact]
        public async Task CancelledRun_RaisesBothChanges()
        {
            var host = CreateHost(new CancellingExecService());
            var events = new List<string>();
            host.IsRunningChanged += (_, _) => events.Add("running:" + host.IsRunning);
            host.ExecutionCompleted += (_, _) => events.Add("completed");
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var result = await host.ExecuteAllAsync(ExecutionRequest.Empty, cts.Token);

            Assert.Equal(ExecutionFailureReason.Cancelled, result.FailureReason);
            Assert.Equal(new[] { "running:True", "running:False", "completed" }, events);
        }

        [Fact]
        public async Task FaultedRun_RaisesBothChanges()
        {
            var host = CreateHost(new FaultingExecService());
            var events = new List<string>();
            host.IsRunningChanged += (_, _) => events.Add("running:" + host.IsRunning);
            host.ExecutionCompleted += (_, _) => events.Add("completed");

            var result = await host.ExecuteAllAsync(ExecutionRequest.Empty);

            Assert.Equal(ExecutionFailureReason.Faulted, result.FailureReason);
            Assert.Equal(new[] { "running:True", "running:False", "completed" }, events);
        }

        [Fact]
        public async Task ArgumentError_BeforeAdmission_RaisesNothing()
        {
            var host = CreateHost(new CapturingExecService());
            var raised = 0;
            host.IsRunningChanged += (_, _) => raised++;
            var relativeTarget = new ExecutionRequest(new[]
            {
                new KeyValuePair<string, string>("REPORTTARGETPATH", "relative.pdf")
            });

            await Assert.ThrowsAsync<ArgumentException>(() => host.ExecuteAllAsync(relativeTarget));

            Assert.Equal(0, raised);
            Assert.False(host.IsRunning);
        }

        [Fact]
        public async Task RefusedCall_WhileARunIsActive_RaisesNothingMore()
        {
            var service = new BlockingExecService();
            var host = CreateHost(service);
            var events = new List<string>();
            host.IsRunningChanged += (_, _) => events.Add("running:" + host.IsRunning);

            var firstRun = host.ExecuteAllAsync(ExecutionRequest.Empty);
            Assert.Equal(new[] { "running:True" }, events);   // raised before the first await, i.e. before any task work

            await Assert.ThrowsAsync<InvalidOperationException>(() => host.ExecuteAllAsync(ExecutionRequest.Empty));
            Assert.Equal(new[] { "running:True" }, events);

            service.Unblock();
            await firstRun;
            Assert.Equal(new[] { "running:True", "running:False" }, events);
        }

        [Fact]
        public async Task ThrowingSubscriber_DoesNotAffectTheRunOrTheOtherSubscribers_AndLeaksNoSecrets()
        {
            const string secret = "00112233445566778899AABBCCDDEEFF";
            var host = CreateHost(new CapturingExecService());
            var later = new List<bool>();
            host.IsRunningChanged += (_, _) => throw new InvalidOperationException("Key=" + secret);
            host.IsRunningChanged += (_, _) => later.Add(host.IsRunning);
            using var logs = CapturedLogs.Install();

            var result = await host.ExecuteAllAsync(ExecutionRequest.Empty);

            Assert.Equal(ExecutionOutcome.Success, result.Outcome);   // the run is unaffected
            Assert.Equal(new[] { true, false }, later);
            var entries = logs.From(nameof(TaskExecHostImpl)).Where(e => e.Level == LogEventLevel.Error).ToList();
            Assert.Equal(2, entries.Count);   // one per change
            foreach (var entry in entries)
            {
                Assert.Null(entry.Exception);
                var written = CapturedLogs.EverythingWrittenFor(entry);
                Assert.DoesNotContain(secret, written, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("[REDACTED", written);
            }
        }

        [Fact]
        public async Task StartingARunFromTheTrueNotification_IsRefusedAsBusy()
        {
            var host = CreateHost(new CapturingExecService());
            Task<ExecResult>? nested = null;
            var notifications = 0;
            host.IsRunningChanged += (_, _) =>
            {
                // Only the first notification starts again, so a regression that announced the refused attempt
                // fails the count below instead of recursing until the test host crashes.
                if (notifications++ == 0 && host.IsRunning)
                {
                    nested = host.ExecuteAllAsync(ExecutionRequest.Empty);
                }
            };

            var result = await host.ExecuteAllAsync(ExecutionRequest.Empty);

            Assert.Equal(ExecutionOutcome.Success, result.Outcome);
            Assert.NotNull(nested);
            Assert.True(nested!.IsFaulted, "the nested call is refused before it is admitted");
            Assert.IsType<InvalidOperationException>(nested.Exception!.InnerException);
            Assert.Equal(2, notifications);   // one for each end of the outer run; the refused nested call added none
        }

        [Fact]
        public async Task MainWindowRun_IsAnnouncedOnTheUiThread()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var tempRoot = Path.Combine(Path.GetTempPath(), "RFiDGear.Tests", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempRoot);
                try
                {
                    var viewModel = await MainWindowViewModelTests.CreateInitializedViewModelAsync(tempRoot);
                    var execution = viewModel.ExtensionHost.Execution;
                    var states = new List<bool>();
                    var threads = new List<int>();
                    execution.IsRunningChanged += (_, _) =>
                    {
                        states.Add(execution.IsRunning);
                        threads.Add(Environment.CurrentManagedThreadId);
                    };

                    await viewModel.WriteToChipOnceCommand.ExecuteAsync(null);

                    Assert.Equal(new[] { true, false }, states);
                    Assert.All(threads, id => Assert.Equal(Environment.CurrentManagedThreadId, id));
                    Assert.False(execution.IsRunning);
                }
                finally
                {
                    Directory.Delete(tempRoot, recursive: true);
                }
            });
        }

        /// <summary>Runs a callback in place of the task loop, so the position of the work in an event sequence is visible.</summary>
        private sealed class CallbackExecService : ITaskExecutionService
        {
            private readonly Action _work;

            public CallbackExecService(Action work) => _work = work;

            public event EventHandler<TaskExecutionCompletedEventArgs> ExecutionCompleted
            {
                add { }
                remove { }
            }

            public int CurrentTaskIndex => 0;

            public Task<TaskExecutionResult> ExecuteOnceAsync(
                TaskExecutionRequest request, CancellationToken ct = default)
            {
                _work();
                return Task.FromResult(new TaskExecutionResult
                {
                    RunId = request.RunId,
                    TerminalStatus = TaskLoopTerminalStatus.Success
                });
            }

            public void HandleTaskTimeout()
            {
            }
        }
    }
}
