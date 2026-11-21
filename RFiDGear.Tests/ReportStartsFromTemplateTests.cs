using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.ExtensionHost;
using RFiDGear.Infrastructure.FileAccess;
using RFiDGear.Infrastructure.Tasks;
using RFiDGear.Models;
using RFiDGear.Services.TaskExecution;
using RFiDGear.ViewModel.TaskSetupViewModels;

using Xunit;

namespace RFiDGear.Tests
{
    /// <summary>
    /// A report belongs to a run. The writer builds a report in a working copy of the template, to which every change of a field
    /// adds, and the main window keeps one writer from run to run: the report of a card started from the report of the card before, so
    /// a field that this card's run did not write (a checkpoint for a failed task, a text per result) kept the value of the card
    /// before. The service now tells the writer when a run starts; the report tasks of one run still add to one report.
    /// </summary>
    [Collection("CommonTaskStatics")]
    public class ReportStartsFromTemplateTests
    {
        private static CommonTaskViewModel ReportTask(string index, params (string Field, string Content)[] fields)
        {
            var checkpoints = new ObservableCollection<Checkpoint>();

            foreach (var (field, content) in fields)
            {
                // No target task: a plain field that is always written.
                checkpoints.Add(new Checkpoint { TemplateField = field, Content = content, TaskIndex = string.Empty, ErrorLevel = ERROR.NoError });
            }

            return new CommonTaskViewModel
            {
                SelectedTaskType = TaskType_CommonTask.CreateReport,
                CurrentTaskIndex = index,
                Checkpoints = checkpoints,
                AvailableTasks = new ObservableCollection<object>()
            };
        }

        private static ReportReaderWriter NewWriter(ReportTestKit kit, string template) =>
            new ReportReaderWriter(new ProjectManager(Path.Combine(kit.Folder, "work"))) { ReportTemplateFile = template };

        [Fact]
        public async Task TheReportTasksOfOneReport_GoOnAddingToIt()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var kit = new ReportTestKit();
                using var writer = NewWriter(kit, kit.Template("A", "B"));
                writer.ReportOutputPath = Path.Combine(kit.Folder, "report.pdf");

                await ReportTask("0", ("A", "one")).WriteReportCommand.ExecuteAsync(writer);
                await ReportTask("1", ("B", "two")).WriteReportCommand.ExecuteAsync(writer);

                var report = ReportTestKit.Read(writer.ReportOutputPath);
                Assert.Equal("one", report["A"]);
                Assert.Equal("two", report["B"]);
            });
        }

        [Fact]
        public async Task AWriterThatWasToldToStartANewReport_StartsFromTheTemplate()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var kit = new ReportTestKit();
                using var writer = NewWriter(kit, kit.Template("A", "B"));

                writer.ReportOutputPath = Path.Combine(kit.Folder, "first.pdf");
                await ReportTask("0", ("A", "card one"), ("B", "only the first card")).WriteReportCommand.ExecuteAsync(writer);

                writer.StartNewReport();
                writer.ReportOutputPath = Path.Combine(kit.Folder, "second.pdf");
                await ReportTask("0", ("A", "card two")).WriteReportCommand.ExecuteAsync(writer);

                var second = ReportTestKit.Read(writer.ReportOutputPath);
                Assert.Equal("card two", second["A"]);
                Assert.True(string.IsNullOrEmpty(second["B"]));
                Assert.Equal("only the first card", ReportTestKit.Read(Path.Combine(kit.Folder, "first.pdf"))["B"]);
            });
        }

        [Fact]
        public async Task TheServiceStartsANewReportForEveryRun_OfTheWriterOfTheWindow()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var kit = new ReportTestKit();
                var template = kit.Template("A", "B");
                using var writer = NewWriter(kit, template);
                var chip = new GenericChipModel("0123456789", CARD_TYPE.EM4102);
                var reader = new Reader.ScriptedReadProvider { GenericChip = chip, Card = chip };

                using (ReaderDeviceScope.Use(reader))
                {
                    var service = new TaskExecutionService(new ReaderDeviceProvider(), new NullDispatcherTimerAdapter(), new NullDispatcherTimerAdapter());

                    async Task RunAsync(string output, params (string Field, string Content)[] fields)
                    {
                        var handler = new ChipTaskHandlerModel();
                        handler.TaskCollection.Add(ReportTask("0", fields));

                        using var limit = new CancellationTokenSource(System.TimeSpan.FromSeconds(30));

                        await service.ExecuteOnceAsync(
                            new TaskExecutionRequest
                            {
                                TaskHandler = handler,
                                VariablesFromArgs = new Dictionary<string, string>(),
                                Timeouts = TaskExecutionTimeouts.Default,
                                ReportReaderWriter = writer,
                                ReportTemplateFile = template,
                                ReportOutputPath = Path.Combine(kit.Folder, output)
                            },
                            limit.Token);
                    }

                    await RunAsync("first.pdf", ("A", "card one"), ("B", "only the first card"));
                    await RunAsync("second.pdf", ("A", "card two"));
                }

                var first = ReportTestKit.Read(Path.Combine(kit.Folder, "first.pdf"));
                var second = ReportTestKit.Read(Path.Combine(kit.Folder, "second.pdf"));

                Assert.Equal("only the first card", first["B"]);
                Assert.Equal("card two", second["A"]);
                Assert.True(string.IsNullOrEmpty(second["B"]), "The second report holds a field of the first: " + second["B"]);
            });
        }
    }
}
