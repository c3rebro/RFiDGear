using RFiDGear.Infrastructure.AccessControl;
using RFiDGear.Infrastructure.Tasks;
using RFiDGear.ViewModel.TaskSetupViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Threading;
using Xunit;

namespace RFiDGear.Tests
{
    /// <summary>
    /// The "Card Mastering" tab of the DESFire task dialog, rendered for real. The tests for the view model only check the
    /// individual visibility properties, so they could not see that a group, whose visibility is a different property than
    /// that of the controls inside it, hid the whole comparison of the Check: App Config task, or that the panel with the
    /// application and the key was shown but disabled for it. Here a control has to be visible and enabled in a shown
    /// window. Controls are found by what they are bound to, so that the text, which follows the language, is not asserted.
    /// </summary>
    [Collection("WpfBindingTrace")]
    public class CardMasteringViewTests
    {
        public static IEnumerable<object[]> AppLevelCheckTasks()
        {
            foreach (var taskType in new[] { TaskType_MifareDesfireTask.ReadAppSettings, TaskType_MifareDesfireTask.CheckAppKeyCount })
            {
                // A task that is edited is selected before the dialog is shown, a new one is picked in the dialog.
                yield return new object[] { taskType, false };
                yield return new object[] { taskType, true };
            }
        }

        [Fact]
        public async Task Finder_LocatesAControlWhoseGroupIsCollapsed()
        {
            // The tests below rely on a control that is not shown still being found, so that they fail because it is not
            // visible and not because it is missing, and on a path that does not exist finding nothing.
            await InspectAsync(TaskType_MifareDesfireTask.FormatDesfireCard, false, view =>
            {
                var checkBoxes = Bound<CheckBox>(view, ToggleButton.IsCheckedProperty, "IsAllowChangeMKChecked");

                Assert.Single(checkBoxes);
                Assert.False(checkBoxes[0].IsVisible);
                Assert.Empty(Bound<CheckBox>(view, ToggleButton.IsCheckedProperty, "NoSuchProperty"));
            });
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task CheckAppConfig_ShowsTheExpectedKeySettingsAndTheCompareAction(bool selectTypeAfterRender)
        {
            await InspectAsync(TaskType_MifareDesfireTask.ReadAppSettings, selectTypeAfterRender, view =>
            {
                foreach (var path in KeySettingFlags)
                {
                    AssertUsable(One<CheckBox>(view, ToggleButton.IsCheckedProperty, path), path);
                }

                var modes = LogicalDescendants(view).OfType<RadioButton>().ToList();
                Assert.Equal(3, modes.Count);
                foreach (var mode in modes)
                {
                    AssertUsable(mode, "comparison mode " + BindingPath(mode, ToggleButton.IsCheckedProperty));
                }

                // "All selected" has always been the behaviour of a task that never chose.
                Assert.True(One<RadioButton>(view, ToggleButton.IsCheckedProperty, "IsCompareModeAll").IsChecked);

                AssertUsable(One<Button>(view, ButtonBase.CommandProperty, "ReadAppSettingsRelayCommand"), "the compare button");
            });
        }

        [Theory]
        [MemberData(nameof(AppLevelCheckTasks))]
        public async Task AppLevelCheck_EnablesTheApplicationAndTheKeyInputs(TaskType_MifareDesfireTask taskType, bool selectTypeAfterRender)
        {
            await InspectAsync(taskType, selectTypeAfterRender, view =>
            {
                // The check reads these; a panel that is shown but disabled leaves nobody to say which application and key.
                AssertUsable(One<TextBox>(view, TextBox.TextProperty, "AppNumberCurrent"), "the application id");
                AssertUsable(One<TextBox>(view, TextBox.TextProperty, "DesfireAppKeyCurrent"), "the application key");
                AssertUsable(One<ComboBox>(view, Selector.SelectedItemProperty, "SelectedDesfireAppKeyEncryptionTypeCurrent"), "the key type");
                AssertUsable(One<ComboBox>(view, Selector.SelectedItemProperty, "SelectedDesfireAppKeyNumberCurrent"), "the key number");
                AssertUsable(One<Button>(view, ButtonBase.CommandProperty, "AuthenticateToCardApplicationCommand"), "the authenticate button");
            });
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task CheckAppKeyCount_ShowsTheExpectedKeyCountAndTheCompareAction(bool selectTypeAfterRender)
        {
            await InspectAsync(TaskType_MifareDesfireTask.CheckAppKeyCount, selectTypeAfterRender, view =>
            {
                AssertUsable(One<ComboBox>(view, Selector.SelectedItemProperty, "SelectedDesfireAppMaxNumberOfKeys"), "the expected key count");
                AssertUsable(One<Button>(view, ButtonBase.CommandProperty, "CheckAppKeyCountRelayCommand"), "the compare button");
            });
        }

        [Fact]
        public async Task CheckAppConfig_ChoosingAModeInTheWindow_SetsTheModeAndFollowsIt()
        {
            await InspectAsync(TaskType_MifareDesfireTask.ReadAppSettings, false, (view, viewModel) =>
            {
                var all = One<RadioButton>(view, ToggleButton.IsCheckedProperty, nameof(MifareDesfireSetupViewModel.IsCompareModeAll));
                var any = One<RadioButton>(view, ToggleButton.IsCheckedProperty, nameof(MifareDesfireSetupViewModel.IsCompareModeAny));
                var exact = One<RadioButton>(view, ToggleButton.IsCheckedProperty, nameof(MifareDesfireSetupViewModel.IsCompareModeExact));

                // From the window to the view model, for the two modes that are not the default.
                any.IsChecked = true;
                Assert.Equal(DesfireAppSettingsCompareMode.AnySelected, viewModel.AppSettingsCompareMode);
                exact.IsChecked = true;
                Assert.Equal(DesfireAppSettingsCompareMode.Exact, viewModel.AppSettingsCompareMode);
                Assert.False(any.IsChecked);

                // And back, as when a saved task is opened.
                viewModel.AppSettingsCompareMode = DesfireAppSettingsCompareMode.AllSelected;
                view.UpdateLayout();
                Assert.True(all.IsChecked);
                Assert.False(exact.IsChecked);
            });
        }

        [Fact]
        public async Task CheckAppConfig_ChoosingAPolicyInTheWindow_SetsThePolicy()
        {
            await InspectAsync(TaskType_MifareDesfireTask.ReadAppSettings, false, (view, viewModel) =>
            {
                viewModel.AppSettingsCompareMode = DesfireAppSettingsCompareMode.Exact;
                view.UpdateLayout();
                var policy = One<ComboBox>(view, Selector.SelectedItemProperty, nameof(MifareDesfireSetupViewModel.SelectedDesfireAppKeySettingsCreateNewApp));
                Assert.NotNull(policy.SelectedItem);

                // The list is master key, same key, frozen; the text is localized, so the entry is picked by position.
                policy.SelectedIndex = 2;

                Assert.Equal(AccessCondition_MifareDesfireAppCreation.ChangeKeyFrozen, viewModel.SelectedDesfireAppKeySettingsCreateNewApp);

                viewModel.AppSettingsCompareMode = DesfireAppSettingsCompareMode.AllSelected;
                view.UpdateLayout();
                Assert.False(policy.IsVisible);
            });
        }

        [Theory]
        [InlineData(TaskType_MifareDesfireTask.PICCMasterKeyChangeover)]
        [InlineData(TaskType_MifareDesfireTask.PICCMasterKeySettingsChangeover)]
        public async Task PiccKeyTasks_StillShowTheKeySettingsButNoComparison(TaskType_MifareDesfireTask taskType)
        {
            await InspectAsync(taskType, false, view =>
            {
                foreach (var path in KeySettingFlags)
                {
                    AssertUsable(One<CheckBox>(view, ToggleButton.IsCheckedProperty, path), path);
                }

                Assert.All(LogicalDescendants(view).OfType<RadioButton>(), mode => Assert.False(mode.IsVisible));
                Assert.False(One<Button>(view, ButtonBase.CommandProperty, "ReadAppSettingsRelayCommand").IsVisible);
                Assert.False(One<Button>(view, ButtonBase.CommandProperty, "CheckAppKeyCountRelayCommand").IsVisible);
            });
        }

        [Theory]
        [InlineData(DesfireAppSettingsCompareMode.AllSelected, false)]
        [InlineData(DesfireAppSettingsCompareMode.AnySelected, false)]
        [InlineData(DesfireAppSettingsCompareMode.Exact, true)]
        public async Task CheckAppConfig_ShowsTheExpectedPolicyOnlyForTheExactMode(DesfireAppSettingsCompareMode mode, bool shown)
        {
            // The upper nibble of the byte is a policy, not a set of bits, so only the exact comparison needs it.
            await InspectAsync(TaskType_MifareDesfireTask.ReadAppSettings, false, (view, viewModel) =>
            {
                var policy = One<ComboBox>(view, Selector.SelectedItemProperty, nameof(MifareDesfireSetupViewModel.SelectedDesfireAppKeySettingsCreateNewApp));
                Assert.False(policy.IsVisible, "the policy is shown before a mode is chosen");

                viewModel.AppSettingsCompareMode = mode;
                view.UpdateLayout();

                if (shown)
                {
                    AssertUsable(policy, "the expected change-key policy");
                }
                else
                {
                    Assert.False(policy.IsVisible);
                }
            });
        }

        [Theory]
        [InlineData(TaskType_MifareDesfireTask.PICCMasterKeyChangeover)]
        [InlineData(TaskType_MifareDesfireTask.PICCMasterKeySettingsChangeover)]
        [InlineData(TaskType_MifareDesfireTask.CheckAppKeyCount)]
        public async Task OtherTasks_NeverShowTheExpectedPolicy(TaskType_MifareDesfireTask taskType)
        {
            await InspectAsync(taskType, false, (view, viewModel) =>
            {
                // A mode saved by a Check: App Config task on a task that has no comparison must not bring it back.
                viewModel.AppSettingsCompareMode = DesfireAppSettingsCompareMode.Exact;
                view.UpdateLayout();

                Assert.False(One<ComboBox>(view, Selector.SelectedItemProperty, nameof(MifareDesfireSetupViewModel.SelectedDesfireAppKeySettingsCreateNewApp)).IsVisible);
            });
        }

        [Theory]
        [InlineData(TaskType_MifareDesfireTask.FormatDesfireCard)]
        [InlineData(TaskType_MifareDesfireTask.ChangeDefault)]
        [InlineData(TaskType_MifareDesfireTask.CheckAppKeyCount)]
        public async Task OtherTasks_HideTheKeySettings(TaskType_MifareDesfireTask taskType)
        {
            await InspectAsync(taskType, false, view =>
            {
                foreach (var path in KeySettingFlags)
                {
                    Assert.False(One<CheckBox>(view, ToggleButton.IsCheckedProperty, path).IsVisible, path);
                }
            });
        }

        [Theory]
        [InlineData(TaskType_MifareDesfireTask.ReadAppSettings)]
        [InlineData(TaskType_MifareDesfireTask.CheckAppKeyCount)]
        [InlineData(TaskType_MifareDesfireTask.PICCMasterKeySettingsChangeover)]
        public async Task EveryCaptionThatIsShown_HasText(TaskType_MifareDesfireTask taskType)
        {
            // A resource key that does not exist gives an empty caption and no error, so a label can silently go blank.
            await InspectAsync(taskType, false, view =>
            {
                var blank = new List<string>();
                foreach (var element in LogicalDescendants(view).OfType<FrameworkElement>().Where(element => element.IsVisible))
                {
                    if (element is ContentControl content && content.Content is string text && string.IsNullOrWhiteSpace(text))
                    {
                        blank.Add(element.GetType().Name + " content");
                    }

                    if (element is HeaderedContentControl headered && (headered.Header is string header ? string.IsNullOrWhiteSpace(header) : headered.Header == null))
                    {
                        blank.Add(element.GetType().Name + " header");
                    }
                }

                Assert.True(blank.Count == 0, "blank captions: " + string.Join(", ", blank));
            });
        }

        private static readonly string[] KeySettingFlags =
        {
            "IsAllowChangeMKChecked",
            "IsAllowListingWithoutMKChecked",
            "IsAllowCreateDelWithoutMKChecked",
            "IsAllowConfigChangableChecked",
        };

        private static Task InspectAsync(TaskType_MifareDesfireTask taskType, bool selectTypeAfterRender, Action<FrameworkElement> inspect) =>
            InspectAsync(taskType, selectTypeAfterRender, (view, _) => inspect(view));

        private static async Task InspectAsync(
            TaskType_MifareDesfireTask taskType,
            bool selectTypeAfterRender,
            Action<FrameworkElement, MifareDesfireSetupViewModel> inspect)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var viewModel = new MifareDesfireSetupViewModel();
                if (!selectTypeAfterRender)
                {
                    viewModel.SelectedTaskType = taskType;
                }

                var view = new RFiDGear.View.TabPageMifareDesfireCardMasteringView { DataContext = viewModel };
                var window = new Window { Content = view, Width = 1100, Height = 800, ShowActivated = false, ShowInTaskbar = false };
                try
                {
                    window.Show();
                    if (selectTypeAfterRender)
                    {
                        viewModel.SelectedTaskType = taskType;
                    }

                    window.UpdateLayout();
                    await Flush();
                    window.UpdateLayout();

                    inspect(view, viewModel);
                }
                finally
                {
                    window.Close();
                }
            });
        }

        // Lets everything already queued on the dispatcher run.
        private static async Task Flush() => await Dispatcher.Yield(DispatcherPriority.Background);

        private static IEnumerable<DependencyObject> LogicalDescendants(DependencyObject root)
        {
            // The logical tree, not the visual one: the contents of a group that is collapsed are never given a visual.
            foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            {
                yield return child;

                foreach (var nested in LogicalDescendants(child))
                {
                    yield return nested;
                }
            }
        }

        private static string BindingPath(DependencyObject element, DependencyProperty property) =>
            BindingOperations.GetBinding(element, property)?.Path?.Path ?? string.Empty;

        private static List<T> Bound<T>(DependencyObject root, DependencyProperty property, string path)
            where T : FrameworkElement =>
            LogicalDescendants(root).OfType<T>().Where(element => BindingPath(element, property) == path).ToList();

        private static T One<T>(DependencyObject root, DependencyProperty property, string path)
            where T : FrameworkElement
        {
            var found = Bound<T>(root, property, path);
            Assert.True(found.Count == 1, $"expected one {typeof(T).Name} bound to {path}, found {found.Count}");

            return found[0];
        }

        private static void AssertUsable(FrameworkElement element, string what)
        {
            Assert.True(element.IsVisible, what + " is not visible");
            Assert.True(element.IsEnabled, what + " is not enabled");
        }
    }
}
