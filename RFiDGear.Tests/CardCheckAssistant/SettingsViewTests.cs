using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

using RFiDGear.Extensions.CardCheckAssistant.Services;
using RFiDGear.Extensions.CardCheckAssistant.ViewModels;
using RFiDGear.Extensions.CardCheckAssistant.Views;

using Xunit;

using static RFiDGear.Tests.CardCheckAssistant.ViewModelTestSupport;

using Keys = RFiDGear.Extensions.CardCheckAssistant.Localization.ExtensionStrings.Keys;
using Rig = RFiDGear.Tests.CardCheckAssistant.SettingsViewModelTests.Rig;

namespace RFiDGear.Tests.CardCheckAssistant
{
    /// <summary>
    /// The settings view against its view model: every control is bound to what it should show or do, errors and warnings
    /// look different, and every state is shown without a binding that cannot be resolved.
    /// </summary>
    [Collection("WpfBindingTrace")]
    public class SettingsViewTests
    {
        private static Window Show(SettingsView view)
        {
            var window = new Window { Content = view, Width = 700, Height = 820 };
            window.Show();

            return window;
        }

        private static T Named<T>(FrameworkElement root, string name)
            where T : class
        {
            var found = root.FindName(name);
            Assert.NotNull(found);

            return Assert.IsAssignableFrom<T>(found);
        }

        private static IEnumerable<T> Descendants<T>(DependencyObject root)
            where T : DependencyObject
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is T match)
                {
                    yield return match;
                }

                foreach (var nested in Descendants<T>(child))
                {
                    yield return nested;
                }
            }
        }

        // The text that tells what is wrong with a path: the one block of the field's template bound to "Message".
        private static TextBlock MessageOf(FrameworkElement view, string fieldName) =>
            Descendants<TextBlock>(Named<ContentControl>(view, fieldName))
                .Single(block => BindingOperations.GetBinding(block, TextBlock.TextProperty)?.Path.Path == "Message");

        private static Color ColorOf(Brush brush) => Assert.IsType<SolidColorBrush>(brush).Color;

        [Fact]
        public async Task TheView_IsWiredToTheViewModel_ControlByControl()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var rig = await Rig.OpenAsync(SettingsViewModelTests.Loaded());
                var vm = rig.Vm;
                var view = new SettingsView(vm);
                var window = Show(view);
                try
                {
                    await Flush();

                    // The buttons do what the view model says; Enter saves, and nothing else is the default.
                    Assert.Same(vm.OkCommand, Named<Button>(view, "SaveButton").Command);
                    Assert.Same(vm.CancelCommand, Named<Button>(view, "CancelButton").Command);
                    Assert.Same(vm.TestConnectionCommand, Named<Button>(view, "TestButton").Command);
                    Assert.Same(vm.AddTextBlockCommand, Named<Button>(view, "AddTextBlockButton").Command);
                    Assert.Same(vm.RemoveTextBlockCommand, Named<Button>(view, "RemoveTextBlockButton").Command);
                    Assert.Equal(new[] { "SaveButton" }, Descendants<Button>(view).Where(button => button.IsDefault).Select(button => button.Name));
                    var escape = Assert.Single(view.InputBindings.OfType<System.Windows.Input.KeyBinding>());
                    Assert.Equal((System.Windows.Input.Key.Escape, vm.CancelCommand), (escape.Key, escape.Command));
                    Assert.Same(vm.OutputFolder.BrowseCommand, Descendants<Button>(Named<ContentControl>(view, "OutputFolderField")).Single().Command);
                    Assert.Same(vm.MemoryProject.BrowseCommand, Descendants<Button>(Named<ContentControl>(view, "MemoryProjectField")).Single().Command);

                    // What was loaded is shown, and nothing is wrong yet.
                    Assert.True(Named<Panel>(view, "ContentPanel").IsEnabled);
                    Assert.Equal(("srv", "orders", "dbo.T_Orders"), (Named<TextBox>(view, "ServerBox").Text, Named<TextBox>(view, "DatabaseBox").Text, Named<TextBox>(view, "TableBox").Text));
                    Assert.True(Named<CheckBox>(view, "OneFolderPerJobBox").IsChecked);
                    Assert.False(Named<CheckBox>(view, "RemoveTemporaryReportsBox").IsChecked);
                    Assert.True(Named<RadioButton>(view, "EnglishReportButton").IsChecked);
                    Assert.False(Named<RadioButton>(view, "GermanReportButton").IsChecked);
                    Assert.Equal("out", Descendants<TextBox>(Named<ContentControl>(view, "OutputFolderField")).Single().Text);
                    Assert.Equal("check.rfPrj", Descendants<TextBox>(Named<ContentControl>(view, "DefaultProjectField")).Single().Text);
                    Assert.Equal(new[] { "Greeting", "Closing" }, Named<ListBox>(view, "TextBlockList").Items.Cast<object>().Select(item => ((TextBlockViewModel)item).Name));
                    Assert.Equal(Visibility.Collapsed, Named<Border>(view, "ProblemBanner").Visibility);
                    Assert.Equal(Visibility.Collapsed, Named<ProgressBar>(view, "ProgressIndicator").Visibility);
                    Assert.Equal(Visibility.Collapsed, Named<TextBlock>(view, "SaveErrorText").Visibility);

                    // What is entered in the view reaches the view model.
                    Named<TextBox>(view, "ServerBox").Text = "other";
                    Named<RadioButton>(view, "GermanReportButton").IsChecked = true;
                    Assert.Equal("other", vm.DatabaseServer);
                    Assert.True(vm.IsGermanReport);

                    // Every text box has a name of its own for a screen reader, in the language of the page, and the three
                    // path editors, which share one template, are told apart.
                    string[] CaptionsNow() => new[]
                    {
                        rig.Strings[Keys.OutputFolderLabel], rig.Strings[Keys.DefaultProjectLabel], rig.Strings[Keys.MemoryProjectLabel],
                        rig.Strings[Keys.DatabaseServerLabel], rig.Strings[Keys.DatabaseNameLabel], rig.Strings[Keys.DatabaseTableLabel],
                        rig.Strings[Keys.TextBlockNameLabel], rig.Strings[Keys.TextBlockContentLabel],
                    };

                    window.UpdateLayout();
                    var boxes = Descendants<TextBox>(view).ToList();
                    Assert.Equal(CaptionsNow().OrderBy(name => name), boxes.Select(AutomationProperties.GetName).OrderBy(name => name));
                    Assert.Equal(rig.Strings[Keys.OutputFolderLabel], AutomationProperties.GetName(Descendants<TextBox>(Named<ContentControl>(view, "OutputFolderField")).Single()));
                    Assert.Equal(rig.Strings[Keys.MemoryProjectLabel], AutomationProperties.GetName(Descendants<TextBox>(Named<ContentControl>(view, "MemoryProjectField")).Single()));

                    rig.Strings.Refresh("de-DE");
                    await Flush();
                    Assert.Equal(CaptionsNow().OrderBy(name => name), boxes.Select(AutomationProperties.GetName).OrderBy(name => name));
                    Assert.Equal("Ordner für die Berichte", AutomationProperties.GetName(Descendants<TextBox>(Named<ContentControl>(view, "OutputFolderField")).Single()));
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [Fact]
        public async Task Errors_AreRedUnderTheirOwnField_WarningsAreAmber_AndTheTestResultIsGreenOrRed()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var rig = await Rig.OpenAsync(SettingsViewModelTests.Loaded());
                var vm = rig.Vm;
                var view = new SettingsView(vm);
                var window = Show(view);
                try
                {
                    // An error stops the save, is red under its own field only, and the save says why it did not happen.
                    rig.Folders.Answer = FolderCheck.NotFullyQualified;
                    await vm.OkCommand.ExecuteAsync();
                    window.UpdateLayout();
                    var error = MessageOf(view, "OutputFolderField");
                    Assert.Equal((Visibility.Visible, rig.Strings[Keys.FolderNotAbsolute], Colors.Firebrick), (error.Visibility, error.Text, ColorOf(error.Foreground)));
                    Assert.Equal(Visibility.Collapsed, MessageOf(view, "DefaultProjectField").Visibility);
                    Assert.Equal(Visibility.Collapsed, MessageOf(view, "MemoryProjectField").Visibility);
                    Assert.Equal((Visibility.Visible, rig.Strings[Keys.FixEntries]), (Named<TextBlock>(view, "SaveErrorText").Visibility, Named<TextBlock>(view, "SaveErrorText").Text));

                    // A warning is amber and does not stop the save.
                    vm.OutputFolder.Value = "somewhere";
                    rig.Folders.Answer = FolderCheck.NotFound;
                    await vm.OkCommand.ExecuteAsync();
                    window.UpdateLayout();
                    var warning = MessageOf(view, "OutputFolderField");
                    Assert.Equal((Visibility.Visible, rig.Strings[Keys.FolderNotFound], Colors.DarkGoldenrod), (warning.Visibility, warning.Text, ColorOf(warning.Foreground)));
                    Assert.Equal(new[] { true }, rig.Closed);

                    // The test of the connection: green if it worked, red if not, and nothing before it was tried.
                    var result = Named<TextBlock>(view, "TestResultLine");
                    Assert.Equal(Visibility.Collapsed, result.Visibility);
                    await vm.TestConnectionCommand.ExecuteAsync();
                    window.UpdateLayout();
                    Assert.Equal((Visibility.Visible, rig.Strings[Keys.TestSucceeded], Colors.DarkGreen), (result.Visibility, result.Text, ColorOf(result.Foreground)));

                    rig.Editor.TestResult = ConnectionTestOutcome.Unreachable;
                    await vm.TestConnectionCommand.ExecuteAsync();
                    window.UpdateLayout();
                    Assert.Equal((rig.Strings[Keys.TestUnreachable], Colors.Firebrick), (result.Text, ColorOf(result.Foreground)));

                    // A text block is edited next to the list, which shows a problem with a name in red.
                    var editor = Named<Panel>(view, "TextBlockEditor");
                    Assert.Equal(Visibility.Collapsed, editor.Visibility);
                    var list = Named<ListBox>(view, "TextBlockList");
                    list.SelectedIndex = 1;
                    await Flush();
                    Assert.Same(vm.TextBlocks[1], vm.SelectedTextBlock);
                    Assert.Equal((Visibility.Visible, "Closing"), (editor.Visibility, Named<TextBox>(view, "TextBlockNameBox").Text));
                    Assert.Equal(Visibility.Collapsed, Named<TextBlock>(view, "TextBlockNameError").Visibility);
                    Named<TextBox>(view, "TextBlockNameBox").Text = "greeting";
                    window.UpdateLayout();
                    Assert.Equal((Visibility.Visible, rig.Strings[Keys.TextBlockNameDuplicate]), (Named<TextBlock>(view, "TextBlockNameError").Visibility, Named<TextBlock>(view, "TextBlockNameError").Text));
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [Fact]
        public async Task TheView_ShowsEveryStateWithoutABindingError()
        {
            var listener = BindingErrorListener.Start();

            try
            {
                await StaTestRunner.RunOnStaThreadAsync(async () =>
                {
                    // The settings window with values, every kind of problem, a test that is running and one that failed.
                    using (var rig = await Rig.OpenAsync(SettingsViewModelTests.Loaded(SettingsProblem.DamagedFileSetAside)))
                    {
                        var vm = rig.Vm;
                        var window = Show(new SettingsView(vm));
                        try
                        {
                            await Flush();
                            vm.AddTextBlockCommand.Execute(null);
                            vm.TextBlocks[2].Name = "greeting";
                            vm.DatabaseName = string.Empty;
                            rig.Folders.Answer = FolderCheck.Unusable;
                            rig.Paths.Answers["check.rfPrj"] = ProjectPathCheck.NotFound;
                            await vm.OkCommand.ExecuteAsync();

                            rig.Editor.HoldTest();
                            var testing = vm.TestConnectionCommand.ExecuteAsync();
                            await WaitUntil(() => rig.Editor.Tests.Count == 1, "the test to start");
                            await Flush();
                            rig.Editor.TestResult = ConnectionTestOutcome.AccessDenied;
                            rig.Editor.ReleaseTest();
                            await testing;

                            rig.Strings.Refresh("de-DE");
                            vm.SelectedTextBlock = vm.TextBlocks[0];
                            vm.RemoveTextBlockCommand.Execute(null);
                            vm.SelectedTextBlock = null;
                            rig.Editor.SaveResult = SettingsSaveOutcome.Busy;
                            rig.Folders.Answer = FolderCheck.Valid;
                            vm.DatabaseName = "orders";
                            vm.TextBlocks[1].Name = "Third";
                            await vm.OkCommand.ExecuteAsync();
                            window.UpdateLayout();

                            Assert.True(vm.HasSaveError);
                        }
                        finally
                        {
                            window.Close();
                        }
                    }

                    // And a settings file that cannot be saved over.
                    using var blocked = await Rig.OpenAsync(SettingsViewModelTests.Loaded(SettingsProblem.NewerVersion));
                    var blockedView = new SettingsView(blocked.Vm);
                    var blockedWindow = Show(blockedView);
                    try
                    {
                        await Flush();
                        blockedWindow.UpdateLayout();

                        Assert.True(blocked.Vm.HasProblem);
                        Assert.Equal(Visibility.Visible, Named<Border>(blockedView, "ProblemBanner").Visibility);
                        Assert.False(Named<Panel>(blockedView, "ContentPanel").IsEnabled);
                    }
                    finally
                    {
                        blockedWindow.Close();
                    }
                });
            }
            finally
            {
                listener.Stop();
            }

            Assert.Empty(listener.Messages);
        }

        [Fact]
        public async Task TheBindingErrorCheck_HearsABindingThatCannotBeResolved()
        {
            // The check above is only worth something if it can fail: this breaks a binding on purpose and expects to hear it.
            var listener = BindingErrorListener.Start();
            try
            {
                await StaTestRunner.RunOnStaThreadAsync(() =>
                {
                    var text = new TextBlock { DataContext = new object() };
                    text.SetBinding(TextBlock.TextProperty, new Binding("NoSuchProperty"));
                    var window = new Window { Content = text };
                    window.Show();
                    window.UpdateLayout();
                    window.Close();
                });
            }
            finally
            {
                listener.Stop();
            }

            Assert.Contains(listener.Messages, message => message.Contains("NoSuchProperty", StringComparison.Ordinal));
        }
    }
}
