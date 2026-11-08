using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.Tasks;
using RFiDGear.ViewModel.TaskSetupViewModels;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Xunit;
using static RFiDGear.Tests.RenderedTab;

namespace RFiDGear.Tests
{
    /// <summary>
    /// The task dialog as the user gets it: the window, with its task-type drop-down and its tabs, not a single tab on its
    /// own. The tab views are tested by themselves; this asks whether the task, picked in the real window, shows what it
    /// needs on its tab.
    /// </summary>
    public class CheckFileSettingsDialogTests
    {
        [Fact]
        public async Task PickingTheFileSettingsCheck_ShowsWhatToCompare_OnTheFileTab()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var viewModel = new MifareDesfireSetupViewModel();
                var window = new RFiDGear.View.MifareDesfireSetupView { DataContext = viewModel, ShowActivated = false, ShowInTaskbar = false };
                try
                {
                    window.Show();
                    window.UpdateLayout();
                    await Flush();

                    // Pick the task the way the user does: its name in the drop-down. That the check is the last entry of
                    // the list is pinned with the enum, in CheckFileSettingsTaskTypeTests.
                    var taskTypes = One<ComboBox>(window, Selector.SelectedItemProperty, nameof(MifareDesfireSetupViewModel.SelectedTaskType));
                    var name = ResourceLoader.GetResource("ENUM.TaskType_MifareDesfireTask.CheckFileSettings");
                    Assert.Contains(name, taskTypes.Items.Cast<object>());

                    taskTypes.SelectedItem = name;
                    Assert.Equal(TaskType_MifareDesfireTask.CheckFileSettings, viewModel.SelectedTaskType);

                    var fileTab = LogicalDescendants(window).OfType<TabItem>()
                        .Single(tab => tab.Content is RFiDGear.View.TabPageMifareDesfireFileMasteringView);
                    fileTab.IsSelected = true;
                    window.UpdateLayout();
                    await Flush();
                    window.UpdateLayout();

                    var content = (FrameworkElement)fileTab.Content;
                    foreach (var tick in new[]
                    {
                        nameof(MifareDesfireSetupViewModel.IsFileTypeCompared),
                        nameof(MifareDesfireSetupViewModel.IsFileSizeCompared),
                        nameof(MifareDesfireSetupViewModel.IsFileCommunicationModeCompared),
                        nameof(MifareDesfireSetupViewModel.IsFileReadRightCompared),
                        nameof(MifareDesfireSetupViewModel.IsFileWriteRightCompared),
                        nameof(MifareDesfireSetupViewModel.IsFileReadWriteRightCompared),
                        nameof(MifareDesfireSetupViewModel.IsFileChangeRightCompared),
                    })
                    {
                        AssertUsable(One<CheckBox>(content, ToggleButton.IsCheckedProperty, tick), tick);
                    }

                    AssertUsable(One<RadioButton>(content, ToggleButton.IsCheckedProperty, nameof(MifareDesfireSetupViewModel.IsFileCompareModeAll)), "all must match");
                    AssertUsable(One<RadioButton>(content, ToggleButton.IsCheckedProperty, nameof(MifareDesfireSetupViewModel.IsFileCompareModeAny)), "one must match");
                    AssertUsable(One<ComboBox>(content, Selector.SelectedItemProperty, nameof(MifareDesfireSetupViewModel.FileSizeRelation)), "the size relation");

                    // Unticking the size switches off the input that belongs to it.
                    var size = One<TextBox>(content, TextBox.TextProperty, nameof(MifareDesfireSetupViewModel.FileSizeCurrent));
                    AssertUsable(size, "the file size");
                    One<CheckBox>(content, ToggleButton.IsCheckedProperty, nameof(MifareDesfireSetupViewModel.IsFileSizeCompared)).IsChecked = false;
                    Assert.False(viewModel.IsFileSizeCompared);
                    Assert.False(size.IsEnabled);
                }
                finally
                {
                    window.Close();
                }
            });
        }
    }
}
