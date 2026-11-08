using RFiDGear.Infrastructure.Tasks;
using RFiDGear.ViewModel.TaskSetupViewModels;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Xunit;
using static RFiDGear.Tests.RenderedTab;

namespace RFiDGear.Tests
{
    /// <summary>
    /// The dialog of the Check File Settings task, rendered: every input the check needs is there and usable, the actions
    /// of the other file tasks are not, and nothing that is shown is blank. The task has two tabs, the application (id and
    /// key) and the file (what is expected of it).
    /// </summary>
    public class CheckFileSettingsViewTests
    {
        private static readonly string[] SettingTicks =
        {
            nameof(MifareDesfireSetupViewModel.IsFileTypeCompared),
            nameof(MifareDesfireSetupViewModel.IsFileSizeCompared),
            nameof(MifareDesfireSetupViewModel.IsFileCommunicationModeCompared),
            nameof(MifareDesfireSetupViewModel.IsFileReadRightCompared),
            nameof(MifareDesfireSetupViewModel.IsFileWriteRightCompared),
            nameof(MifareDesfireSetupViewModel.IsFileReadWriteRightCompared),
            nameof(MifareDesfireSetupViewModel.IsFileChangeRightCompared),
        };

        // A task that is edited is selected before the dialog is shown, a new one is picked in the dialog.
        public static IEnumerable<object[]> BeforeAndAfter() => new[] { new object[] { false }, new object[] { true } };

        [Theory]
        [MemberData(nameof(BeforeAndAfter))]
        public async Task TheFileTab_ShowsWhatIsExpected_AndTheCheckAction(bool selectTypeAfterRender)
        {
            await InspectAsync<RFiDGear.View.TabPageMifareDesfireFileMasteringView>(selectTypeAfterRender, (view, _) =>
            {
                AssertUsable(One<TextBox>(view, TextBox.TextProperty, nameof(MifareDesfireSetupViewModel.FileNumberCurrent)), "the file number");
                AssertUsable(One<TextBox>(view, TextBox.TextProperty, nameof(MifareDesfireSetupViewModel.FileSizeCurrent)), "the file size");

                foreach (var comboBox in new[]
                {
                    nameof(MifareDesfireSetupViewModel.SelectedDesfireFileType),
                    nameof(MifareDesfireSetupViewModel.SelectedDesfireFileCryptoMode),
                    nameof(MifareDesfireSetupViewModel.SelectedDesfireFileAccessRightRead),
                    nameof(MifareDesfireSetupViewModel.SelectedDesfireFileAccessRightWrite),
                    nameof(MifareDesfireSetupViewModel.SelectedDesfireFileAccessRightReadWrite),
                    nameof(MifareDesfireSetupViewModel.SelectedDesfireFileAccessRightChange),
                })
                {
                    AssertUsable(One<ComboBox>(view, Selector.SelectedItemProperty, comboBox), comboBox);
                }

                AssertUsable(One<Button>(view, ButtonBase.CommandProperty, nameof(MifareDesfireSetupViewModel.CheckFileSettingsRelayCommand)), "the check action");

                // And the choice of what is compared: a check box for each setting, how many have to match, and the
                // relation of the size.
                foreach (var tick in SettingTicks)
                {
                    AssertUsable(One<CheckBox>(view, ToggleButton.IsCheckedProperty, tick), tick);
                }

                AssertUsable(One<RadioButton>(view, ToggleButton.IsCheckedProperty, nameof(MifareDesfireSetupViewModel.IsFileCompareModeAll)), "all must match");
                AssertUsable(One<RadioButton>(view, ToggleButton.IsCheckedProperty, nameof(MifareDesfireSetupViewModel.IsFileCompareModeAny)), "one must match");
                AssertUsable(One<ComboBox>(view, Selector.SelectedItemProperty, nameof(MifareDesfireSetupViewModel.FileSizeRelation)), "the size relation");

                // A check that only reads has no use for the actions that change a card.
                foreach (var action in new[]
                {
                    nameof(MifareDesfireSetupViewModel.CreateFileCommand),
                    nameof(MifareDesfireSetupViewModel.DeleteFileCommand),
                    nameof(MifareDesfireSetupViewModel.ChangeFileSettingsCommand),
                })
                {
                    Assert.False(One<Button>(view, ButtonBase.CommandProperty, action).IsVisible, action);
                }
            });
        }

        [Theory]
        [MemberData(nameof(BeforeAndAfter))]
        public async Task TheApplicationTab_ShowsTheApplicationAndTheKey_AndNotThePanelOfTheNewKey(bool selectTypeAfterRender)
        {
            await InspectAsync<RFiDGear.View.TabPageMifareDesfireApplicationMasteringView>(selectTypeAfterRender, (view, _) =>
            {
                AssertUsable(One<TextBox>(view, TextBox.TextProperty, nameof(MifareDesfireSetupViewModel.AppNumberCurrent)), "the application id");
                AssertUsable(One<TextBox>(view, TextBox.TextProperty, nameof(MifareDesfireSetupViewModel.DesfireAppKeyCurrent)), "the key");
                AssertUsable(One<ComboBox>(view, Selector.SelectedItemProperty, nameof(MifareDesfireSetupViewModel.SelectedDesfireAppKeyEncryptionTypeCurrent)), "the key type");
                AssertUsable(One<ComboBox>(view, Selector.SelectedItemProperty, nameof(MifareDesfireSetupViewModel.SelectedDesfireAppKeyNumberCurrent)), "the key number");

                // The panel for a new key and the settings to apply is what the other application tasks use; this one
                // changes nothing, and the panel would squeeze the inputs it needs.
                var targetSections = LogicalDescendants(view)
                    .OfType<GroupBox>()
                    .Where(group => BindingPath(group, UIElement.VisibilityProperty) == nameof(MifareDesfireSetupViewModel.ShowAppKeyTargetSection))
                    .ToList();
                Assert.NotEmpty(targetSections);
                Assert.All(targetSections, group => Assert.False(group.IsVisible));
            });
        }

        [Fact]
        public async Task TheApplicationTab_StillShowsThePanelOfTheNewKey_ForTheTasksThatChangeAKey()
        {
            // The other half of the rule above: the panel is only hidden for this task.
            await RenderedTab.InspectAsync<RFiDGear.View.TabPageMifareDesfireApplicationMasteringView>(
                TaskType_MifareDesfireTask.ApplicationKeyChangeover,
                false,
                (view, _) =>
                {
                    var targetSections = LogicalDescendants(view)
                        .OfType<GroupBox>()
                        .Where(group => BindingPath(group, UIElement.VisibilityProperty) == nameof(MifareDesfireSetupViewModel.ShowAppKeyTargetSection))
                        .ToList();

                    Assert.NotEmpty(targetSections);
                    Assert.All(targetSections, group => Assert.True(group.IsVisible));
                });
        }

        // The other file tasks have no choice. The tab is shared, so their inputs must stay as they were, and usable,
        // whatever the ticks of the check say.
        [Theory]
        [InlineData(TaskType_MifareDesfireTask.ReadData)]
        [InlineData(TaskType_MifareDesfireTask.WriteData)]
        [InlineData(TaskType_MifareDesfireTask.CreateFile)]
        [InlineData(TaskType_MifareDesfireTask.ChangeFileSettings)]
        public async Task TheChoiceOfWhatIsCompared_IsOnlyOfferedByTheFileSettingsCheck(TaskType_MifareDesfireTask taskType)
        {
            await RenderedTab.InspectAsync<RFiDGear.View.TabPageMifareDesfireFileMasteringView>(taskType, false, (view, viewModel) =>
            {
                viewModel.IsFileTypeCompared = false;
                viewModel.IsFileSizeCompared = false;
                viewModel.IsFileCommunicationModeCompared = false;
                viewModel.IsFileReadRightCompared = false;
                viewModel.IsFileWriteRightCompared = false;
                viewModel.IsFileReadWriteRightCompared = false;
                viewModel.IsFileChangeRightCompared = false;

                foreach (var tick in SettingTicks)
                {
                    Assert.False(One<CheckBox>(view, ToggleButton.IsCheckedProperty, tick).IsVisible, tick);
                }

                Assert.False(One<RadioButton>(view, ToggleButton.IsCheckedProperty, nameof(MifareDesfireSetupViewModel.IsFileCompareModeAll)).IsVisible);
                Assert.False(One<RadioButton>(view, ToggleButton.IsCheckedProperty, nameof(MifareDesfireSetupViewModel.IsFileCompareModeAny)).IsVisible);
                Assert.False(One<ComboBox>(view, Selector.SelectedItemProperty, nameof(MifareDesfireSetupViewModel.FileSizeRelation)).IsVisible);

                AssertUsable(One<TextBox>(view, TextBox.TextProperty, nameof(MifareDesfireSetupViewModel.FileSizeCurrent)), "the file size");
                AssertUsable(One<ComboBox>(view, Selector.SelectedItemProperty, nameof(MifareDesfireSetupViewModel.SelectedDesfireFileType)), "the file type");
                AssertUsable(One<ComboBox>(view, Selector.SelectedItemProperty, nameof(MifareDesfireSetupViewModel.SelectedDesfireFileCryptoMode)), "the communication mode");
            });
        }

        [Fact]
        public async Task ASettingThatIsNotCompared_HasItsInputSwitchedOff()
        {
            await InspectAsync<RFiDGear.View.TabPageMifareDesfireFileMasteringView>(false, (view, viewModel) =>
            {
                var size = One<TextBox>(view, TextBox.TextProperty, nameof(MifareDesfireSetupViewModel.FileSizeCurrent));
                var relation = One<ComboBox>(view, Selector.SelectedItemProperty, nameof(MifareDesfireSetupViewModel.FileSizeRelation));
                var read = One<ComboBox>(view, Selector.SelectedItemProperty, nameof(MifareDesfireSetupViewModel.SelectedDesfireFileAccessRightRead));
                var write = One<ComboBox>(view, Selector.SelectedItemProperty, nameof(MifareDesfireSetupViewModel.SelectedDesfireFileAccessRightWrite));

                viewModel.IsFileSizeCompared = false;
                viewModel.IsFileReadRightCompared = false;

                Assert.False(size.IsEnabled);
                Assert.False(relation.IsEnabled);
                Assert.False(read.IsEnabled);
                Assert.True(write.IsEnabled);

                viewModel.IsFileSizeCompared = true;

                Assert.True(size.IsEnabled);
                Assert.True(relation.IsEnabled);
            });
        }

        [Fact]
        public async Task ASizeThatIsWrong_IsMarkedRedOnlyWhileItIsCompared()
        {
            await InspectAsync<RFiDGear.View.TabPageMifareDesfireFileMasteringView>(false, (view, viewModel) =>
            {
                var size = One<TextBox>(view, TextBox.TextProperty, nameof(MifareDesfireSetupViewModel.FileSizeCurrent));

                viewModel.FileSizeCurrent = "not a size";
                Assert.Equal(Colors.Red, ((SolidColorBrush)size.Background).Color);

                viewModel.IsFileSizeCompared = false;
                Assert.NotEqual((Color?)Colors.Red, (size.Background as SolidColorBrush)?.Color);
            });
        }

        [Fact]
        public async Task EveryCaptionOfTheFileTab_HasText()
        {
            await InspectAsync<RFiDGear.View.TabPageMifareDesfireFileMasteringView>(false, (view, _) =>
                Assert.True(BlankCaptions(view).Count == 0, "blank captions: " + string.Join(", ", BlankCaptions(view))));
        }

        [Fact]
        public async Task EveryCaptionOfTheApplicationTab_HasText()
        {
            await InspectAsync<RFiDGear.View.TabPageMifareDesfireApplicationMasteringView>(false, (view, _) =>
                Assert.True(BlankCaptions(view).Count == 0, "blank captions: " + string.Join(", ", BlankCaptions(view))));
        }

        private static Task InspectAsync<TView>(bool selectTypeAfterRender, System.Action<FrameworkElement, MifareDesfireSetupViewModel> inspect)
            where TView : FrameworkElement, new() =>
            RenderedTab.InspectAsync<TView>(TaskType_MifareDesfireTask.CheckFileSettings, selectTypeAfterRender, inspect);
    }
}
