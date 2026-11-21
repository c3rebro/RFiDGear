using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.Tasks;
using RFiDGear.Models;
using RFiDGear.ViewModel.TaskSetupViewModels;

using Xunit;

namespace RFiDGear.Tests
{
    /// <summary>
    /// The target of a checkpoint of the report task. A checkpoint says "write this content if task N ended with result E". A
    /// checkpoint without a target is a plain field with a fixed text (a date, a UID) and always writes. A checkpoint whose target
    /// names a task that does not exist (it was deleted, or the tasks were renumbered) used to write anyway, so a report could say
    /// "passed" with no result behind it.
    /// </summary>
    public class ReportCheckpointTargetTests
    {
        private static CommonTaskViewModel Finished(string index, ERROR result) => new CommonTaskViewModel
        {
            SelectedTaskType = TaskType_CommonTask.ExecuteProgram,
            CurrentTaskIndex = index,
            CurrentTaskErrorLevel = result
        };

        private static Checkpoint Mark(string field, string content, string? target, ERROR level = ERROR.NoError) => new Checkpoint
        {
            TemplateField = field,
            Content = content,
            TaskIndex = target,
            ErrorLevel = level
        };

        // A report task over the tasks "1" (ended without error) and "2" (ended as not true), and the given checkpoints.
        private static CommonTaskViewModel ReportTask(params Checkpoint[] checkpoints)
        {
            var tasks = new ObservableCollection<object> { Finished("1", ERROR.NoError), Finished("2", ERROR.IsNotTrue) };
            var report = new CommonTaskViewModel
            {
                SelectedTaskType = TaskType_CommonTask.CreateReport,
                CurrentTaskIndex = "9",
                GenericChip = new GenericChipModel("04AABBCC", CARD_TYPE.Mifare1K),
                Checkpoints = new ObservableCollection<Checkpoint>(checkpoints),
                AvailableTasks = tasks
            };
            tasks.Add(report);

            return report;
        }

        private static string? Value(Dictionary<string, string?> report, string field) =>
            report.TryGetValue(field, out var value) ? value : null;

        [Fact]
        public async Task ACheckpointWithoutATarget_IsAlwaysWritten()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var kit = new ReportTestKit();

                var report = await kit.RunAsync(
                    ReportTask(Mark("A", "empty target", string.Empty), Mark("B", "no target at all", null), Mark("C", "blank", "  ")),
                    kit.Template("A", "B", "C"));

                Assert.Equal("empty target", Value(report, "A"));
                Assert.Equal("no target at all", Value(report, "B"));
                Assert.Equal("blank", Value(report, "C"));
            });
        }

        [Fact]
        public async Task ACheckpointWithATarget_IsWrittenOnlyWhenTheTaskEndedAsNamed()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var kit = new ReportTestKit();
                var task = ReportTask(
                    Mark("A", "task 1 passed", "1", ERROR.NoError),
                    Mark("B", "task 1 failed", "1", ERROR.IsNotTrue),
                    Mark("C", "task 2 failed as expected", "2", ERROR.IsNotTrue));

                var report = await kit.RunAsync(task, kit.Template("A", "B", "C"));

                Assert.Equal("task 1 passed", Value(report, "A"));
                Assert.True(string.IsNullOrEmpty(Value(report, "B")));
                Assert.Equal("task 2 failed as expected", Value(report, "C"));
                Assert.Empty(task.SkippedCheckpointTargets);
            });
        }

        [Fact]
        public async Task ACheckpointWhoseTargetDoesNotExist_WritesNothing_AndTheOthersStillWrite()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var kit = new ReportTestKit();
                var task = ReportTask(
                    Mark("A", "passed", "7", ERROR.NoError),
                    Mark("B", "a date", string.Empty),
                    Mark("C", "task 1 passed", "1", ERROR.NoError));

                var report = await kit.RunAsync(task, kit.Template("A", "B", "C"));

                Assert.True(string.IsNullOrEmpty(Value(report, "A")));
                Assert.Equal("a date", Value(report, "B"));
                Assert.Equal("task 1 passed", Value(report, "C"));
                Assert.Equal(new[] { "7" }, task.SkippedCheckpointTargets);
                Assert.Equal(ERROR.NoError, task.CurrentTaskErrorLevel);
            });
        }

        [Fact]
        public async Task TheSkippedTargetsOfARun_AreForgottenByTheNextRun()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var kit = new ReportTestKit();
                var task = ReportTask(Mark("A", "passed", "7", ERROR.NoError));
                var template = kit.Template("A");

                await kit.RunAsync(task, template);
                Assert.Equal(new[] { "7" }, task.SkippedCheckpointTargets);

                task.Checkpoints = new ObservableCollection<Checkpoint>(new[] { Mark("A", "passed", "1", ERROR.NoError) });
                var report = await kit.RunAsync(task, template, "second.pdf");

                Assert.Empty(task.SkippedCheckpointTargets);
                Assert.Equal("passed", Value(report, "A"));
            });
        }
    }
}
