using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.Tasks;
using RFiDGear.Infrastructure.Tasks.Interfaces;
using RFiDGear.Models;
using RFiDGear.Services.TaskExecution;
using Xunit;

namespace RFiDGear.Tests
{
    /// <summary>
    /// Regression tests for the TransportError sentinel fix in TaskExecutionService.
    /// Uses TaskDescriptor-based requests (executor lambdas) to exercise RunTaskLoopAsync
    /// without requiring WPF ViewModels.
    /// </summary>
    public class TaskExecutionServiceTests
    {
        private static TaskExecutionService BuildService()
        {
            return new TaskExecutionService(
                new StubReaderDeviceProvider(),
                new StubDispatcherTimerAdapter(),
                new StubDispatcherTimerAdapter());
        }

        private static TaskExecutionRequest BuildRequest(IReadOnlyList<TaskDescriptor> descriptors)
        {
            return new TaskExecutionRequest
            {
                TaskDescriptors = descriptors,
                Timeouts = TaskExecutionTimeouts.Default
            };
        }

        [Fact]
        public async Task TransportError_TaskIsInvokedOnceAndLoopCompletes()
        {
            var invokeCount = 0;
            var task = new StubTaskModel { CurrentTaskIndex = "0" };

            var descriptors = new List<TaskDescriptor>
            {
                new TaskDescriptor(0, task, ct =>
                {
                    invokeCount++;
                    task.CurrentTaskErrorLevel = ERROR.TransportError;
                    return Task.CompletedTask;
                })
            };

            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                await BuildService().ExecuteOnceAsync(BuildRequest(descriptors));
            });

            Assert.Equal(1, invokeCount);
        }

        [Fact]
        public async Task TransportError_IsNotInvokedSecondTime()
        {
            var invokeCount = 0;
            var task = new StubTaskModel { CurrentTaskIndex = "0" };

            var descriptors = new List<TaskDescriptor>
            {
                new TaskDescriptor(0, task, ct =>
                {
                    invokeCount++;
                    task.CurrentTaskErrorLevel = ERROR.TransportError;
                    return Task.CompletedTask;
                })
            };

            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                await BuildService().ExecuteOnceAsync(BuildRequest(descriptors));
            });

            Assert.Equal(1, invokeCount);
        }

        [Fact]
        public async Task NoError_LoopAdvancesNormally()
        {
            var invokeCount = 0;
            var task = new StubTaskModel { CurrentTaskIndex = "0" };

            var descriptors = new List<TaskDescriptor>
            {
                new TaskDescriptor(0, task, ct =>
                {
                    invokeCount++;
                    task.CurrentTaskErrorLevel = ERROR.NoError;
                    return Task.CompletedTask;
                })
            };

            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                await BuildService().ExecuteOnceAsync(BuildRequest(descriptors));
            });

            Assert.Equal(1, invokeCount);
        }

        [Fact]
        public async Task TransportError_AttemptIsRecorded()
        {
            var task = new StubTaskModel { CurrentTaskIndex = "0" };

            var descriptors = new List<TaskDescriptor>
            {
                new TaskDescriptor(0, task, ct =>
                {
                    task.CurrentTaskErrorLevel = ERROR.TransportError;
                    return Task.CompletedTask;
                })
            };

            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                await BuildService().ExecuteOnceAsync(BuildRequest(descriptors));
            });

            Assert.Single(task.AttemptResults);
            Assert.Equal(ERROR.TransportError, task.AttemptResults[0].ErrorLevel);
        }

        [Fact]
        public async Task MultipleTasksWithTransportError_AllAdvance()
        {
            var invokeCounts = new int[3];
            var tasks = new[]
            {
                new StubTaskModel { CurrentTaskIndex = "0" },
                new StubTaskModel { CurrentTaskIndex = "1" },
                new StubTaskModel { CurrentTaskIndex = "2" }
            };

            var descriptors = new List<TaskDescriptor>
            {
                new TaskDescriptor(0, tasks[0], ct => { invokeCounts[0]++; tasks[0].CurrentTaskErrorLevel = ERROR.TransportError; return Task.CompletedTask; }),
                new TaskDescriptor(1, tasks[1], ct => { invokeCounts[1]++; tasks[1].CurrentTaskErrorLevel = ERROR.TransportError; return Task.CompletedTask; }),
                new TaskDescriptor(2, tasks[2], ct => { invokeCounts[2]++; tasks[2].CurrentTaskErrorLevel = ERROR.NoError; return Task.CompletedTask; })
            };

            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                await BuildService().ExecuteOnceAsync(BuildRequest(descriptors));
            });

            Assert.Equal(1, invokeCounts[0]);
            Assert.Equal(1, invokeCounts[1]);
            Assert.Equal(1, invokeCounts[2]);
        }

        [Fact]
        public void ExecutionState_DefaultsToNotStarted()
        {
            var task = new StubTaskModel();
            Assert.Equal(TaskExecutionState.NotStarted, task.ExecutionState);
        }

        private sealed class StubReaderDeviceProvider : IReaderDeviceProvider
        {
            public RFiDGear.Infrastructure.ReaderProviders.ReaderDevice GetInstance() => null;
        }

        private sealed class StubDispatcherTimerAdapter : IDispatcherTimerAdapter
        {
            public bool IsEnabled { get; set; }
            public object Tag { get; set; }
            public void Start() { }
            public void Stop() { }
            public void Dispose() { }
        }
    }
}
