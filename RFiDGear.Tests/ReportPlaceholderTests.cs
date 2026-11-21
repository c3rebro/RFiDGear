using System.Collections.ObjectModel;
using System.Threading.Tasks;

using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.Tasks;
using RFiDGear.Models;
using RFiDGear.ViewModel.TaskSetupViewModels;

using Xunit;

namespace RFiDGear.Tests
{
    /// <summary>
    /// The placeholders of the report task. <c>%FREECLASSICMEM</c> promised the free memory of a MIFARE Classic card, but nothing
    /// ever set that value: the field of the report stayed blank without a word. A placeholder either gives a value or does not
    /// exist, and a text that is no placeholder is written as typed, so that it shows in the report.
    /// </summary>
    public class ReportPlaceholderTests
    {
        private static CommonTaskViewModel ReportTask(params Checkpoint[] checkpoints) => new CommonTaskViewModel
        {
            SelectedTaskType = TaskType_CommonTask.CreateReport,
            GenericChip = new GenericChipModel("04AABBCC", CARD_TYPE.Mifare1K),
            Checkpoints = new ObservableCollection<Checkpoint>(checkpoints)
        };

        // A checkpoint without a target task: a field with a text that is always written.
        private static Checkpoint Field(string field, string content) => new Checkpoint
        {
            TemplateField = field,
            Content = content,
            TaskIndex = string.Empty,
            ErrorLevel = ERROR.NoError
        };

        [Fact]
        public async Task APlaceholderWithAValue_IsReplaced()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var kit = new ReportTestKit();

                var report = await kit.RunAsync(ReportTask(Field("Uid", "card %UID")), kit.Template("Uid"));

                Assert.Equal("card 04AABBCC", report["Uid"]);
            });
        }

        [Fact]
        public async Task TheRemovedPlaceholderFreeClassicMem_IsWrittenAsTyped_NotAsABlankField()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var kit = new ReportTestKit();

                var report = await kit.RunAsync(
                    ReportTask(Field("Memory", "free: %FREECLASSICMEM"), Field("Uid", "%UID")), kit.Template("Memory", "Uid"));

                Assert.Equal("free: %FREECLASSICMEM", report["Memory"]);
                Assert.Equal("04AABBCC", report["Uid"]);
            });
        }
    }
}
