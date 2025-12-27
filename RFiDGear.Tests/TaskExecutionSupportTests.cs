using System;
using System.Collections.ObjectModel;
using System.Threading;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.Tasks.Interfaces;
using RFiDGear.Models;
using RFiDGear.Services;
using RFiDGear.Services.TaskExecution;
using Xunit;

namespace RFiDGear.Tests
{
    public class TaskDescriptorTests
    {
        [Fact]
        public void Constructor_UsesTaskCurrentIndexWhenAvailable()
        {
            var task = new StubTaskModel
            {
                CurrentTaskIndex = "task-7"
            };

            var descriptor = new TaskDescriptor(7, task);

            Assert.Equal("task-7", descriptor.Id);
            Assert.Equal(task, descriptor.Task);
            Assert.Equal(7, descriptor.Index);
        }

        [Fact]
        public void Constructor_FallsBackToIndexWhenTaskIsMissing()
        {
            var descriptor = new TaskDescriptor(3, null!);

            Assert.Equal("3", descriptor.Id);
            Assert.Null(descriptor.Task);
            Assert.Equal(3, descriptor.Index);
        }
    }

    public class TaskExecutionTimeoutsTests
    {
        [Fact]
        public void Default_ReturnsDistinctInstancesWithUnsetTimeouts()
        {
            var first = TaskExecutionTimeouts.Default;
            var second = TaskExecutionTimeouts.Default;

            Assert.Null(first.DeviceDiscoveryTimeout);
            Assert.Null(first.ChipHydrationTimeout);
            Assert.Null(first.SelectionSyncTimeout);
            Assert.Null(first.TaskLoopTimeout);
            Assert.NotSame(first, second);
        }
    }

    public class AppStartupContextTests
    {
        [Fact]
        public void Constructor_DefaultsNullArgumentsToEmptyArray()
        {
            using var mutex = new Mutex();
#pragma warning disable CS8625 // Intentional null to validate default handling
            var context = new AppStartupContext(null, mutex, (string[])null!);
#pragma warning restore CS8625

            Assert.NotNull(context.Arguments);
            Assert.Empty(context.Arguments);
        }

        [Fact]
        public void Constructor_ThrowsWhenMutexIsNull()
        {
#pragma warning disable CS8625 // Intentional null to validate guard clause
            Assert.Throws<ArgumentNullException>(() => new AppStartupContext(null, null!, Array.Empty<string>()));
#pragma warning restore CS8625
        }
    }

    internal class StubTaskModel : IGenericTask
    {
        public bool? IsTaskCompletedSuccessfully { get; set; }

        public ERROR SelectedExecuteConditionErrorLevel { get; set; }

        public string SelectedExecuteConditionTaskIndex { get; set; } = string.Empty;

        public ERROR CurrentTaskErrorLevel { get; set; }

        public string CurrentTaskIndex { get; set; } = string.Empty;

        public int SelectedTaskIndexAsInt => int.TryParse(CurrentTaskIndex, out var index) ? index : -1;

        public ObservableCollection<TaskAttemptResult> AttemptResults { get; } = new();
    }
}
