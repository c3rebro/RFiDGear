using RFiDGear.Infrastructure.Tasks;
using RFiDGear.Tests.CardCheckAssistant;
using RFiDGear.ViewModel.TaskSetupViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using Xunit;
using static RFiDGear.Tests.CardCheckAssistant.ViewModelTestSupport;

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
                Assert.True(modes.Count >= 2, "the comparison modes are missing");
                foreach (var mode in modes)
                {
                    AssertUsable(mode, "comparison mode " + BindingPath(mode, ToggleButton.IsCheckedProperty));
                }

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

                    if (element is HeaderedContentControl headered && headered.Header is string header && string.IsNullOrWhiteSpace(header))
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

        private static async Task InspectAsync(TaskType_MifareDesfireTask taskType, bool selectTypeAfterRender, Action<FrameworkElement> inspect)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var viewModel = new MifareDesfireSetupViewModel();
                if (!selectTypeAfterRender)
                {
                    viewModel.SelectedTaskType = taskType;
                }

                var view = new RFiDGear.View.TabPageMifareDesfireCardMasteringView { DataContext = viewModel };
                var window = new Window { Content = view, Width = 1100, Height = 800 };
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

                    inspect(view);
                }
                finally
                {
                    window.Close();
                }
            });
        }

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
