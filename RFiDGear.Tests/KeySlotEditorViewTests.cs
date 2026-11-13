using RFiDGear.Contracts;
using RFiDGear.Infrastructure.Tasks;
using RFiDGear.Models;
using RFiDGear.Services.SecureKeys;
using RFiDGear.Tests.KeySlots;
using RFiDGear.Tests.SecureKeys;
using RFiDGear.View;
using RFiDGear.ViewModel.TaskSetupViewModels;

using System;
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
    /// The key slot editor in the four tabs that have keys, rendered for real: every key is wrapped by an editor for its slot
    /// and keeps its box, switching a slot hides the box and shows the reference, and the reference box never reaches the
    /// setter of a key. Controls are found by what they are bound to, so the text, which follows the language, is not asserted.
    /// </summary>
    [Collection("WpfBindingTrace")]
    public class KeySlotEditorViewTests : IDisposable
    {
        public KeySlotEditorViewTests()
        {
            UseProviders(WindowsCredentialKeyProvider.Registration());
        }

        public void Dispose()
        {
            KeyResolutionScope.SetDefault(null);
        }

        // ── Every key has an editor ──

        [Fact]
        public async Task The_card_mastering_tab_edits_its_keys_through_editors()
        {
            await OnDesfireTab<TabPageMifareDesfireCardMasteringView>(() => new MifareDesfireSetupViewModel(), (view, _) =>
            {
                AssertEditors(
                    view,
                    (SecureKeySlots.DesfirePiccNew, "DesfireMasterKeyTarget"),
                    (SecureKeySlots.DesfirePiccCurrent, "DesfireMasterKeyCurrent"),
                    (SecureKeySlots.DesfireAppCurrent, "DesfireAppKeyCurrent"));

                return Task.CompletedTask;
            });
        }

        [Fact]
        public async Task The_application_mastering_tab_edits_its_keys_through_editors()
        {
            await OnDesfireTab<TabPageMifareDesfireApplicationMasteringView>(() => new MifareDesfireSetupViewModel(), (view, _) =>
            {
                AssertEditors(
                    view,
                    (SecureKeySlots.DesfireAppNew, "DesfireAppKeyTarget"),
                    (SecureKeySlots.DesfireAppCurrent, "DesfireAppKeyCurrent"),
                    (SecureKeySlots.DesfireAppOld, "DesfireAppKeyCurrentOld"));

                // The new key is as long as its own type says, not as the current key.
                var newKeyBox = (TextBox)EditorOf(view, SecureKeySlots.DesfireAppNew).EmbeddedKey;
                Assert.Equal(nameof(MifareDesfireSetupViewModel.SelectedDesfireAppKeyEncryptionTypeTarget), BindingPath(newKeyBox, TextBox.MaxLengthProperty));

                return Task.CompletedTask;
            });
        }

        [Fact]
        public async Task The_data_explorer_tab_edits_its_keys_through_editors()
        {
            await OnDesfireTab<TabPageMifareDesfireDataExplorerView>(() => new MifareDesfireSetupViewModel(), (view, _) =>
            {
                AssertEditors(
                    view,
                    (SecureKeySlots.DesfireRead, "DesfireReadKeyCurrent"),
                    (SecureKeySlots.DesfireWrite, "DesfireWriteKeyCurrent"));

                return Task.CompletedTask;
            });
        }

        [Fact]
        public async Task The_classic_key_tab_edits_its_keys_through_editors()
        {
            await OnClassicTab(() => new MifareClassicSetupViewModel(), (view, _) =>
            {
                AssertEditors(
                    view,
                    (SecureKeySlots.ClassicACurrent, "ClassicKeyAKeyCurrent"),
                    (SecureKeySlots.ClassicBCurrent, "ClassicKeyBKeyCurrent"),
                    (SecureKeySlots.ClassicATarget, "ClassicKeyAKeyTarget"),
                    (SecureKeySlots.ClassicBTarget, "ClassicKeyBKeyTarget"),
                    (SecureKeySlots.ClassicMadACurrent, "ClassicMADKeyAKeyCurrent"),
                    (SecureKeySlots.ClassicMadBCurrent, "ClassicMADKeyBKeyCurrent"),
                    (SecureKeySlots.ClassicMadATarget, "ClassicMADKeyAKeyTarget"),
                    (SecureKeySlots.ClassicMadBTarget, "ClassicMADKeyBKeyTarget"));

                return Task.CompletedTask;
            });
        }

        [Fact]
        public async Task Together_the_four_tabs_cover_every_slot_there_is()
        {
            var covered = new List<string>();

            await OnDesfireTab<TabPageMifareDesfireCardMasteringView>(() => new MifareDesfireSetupViewModel(), (view, _) => Collect(view, covered));
            await OnDesfireTab<TabPageMifareDesfireApplicationMasteringView>(() => new MifareDesfireSetupViewModel(), (view, _) => Collect(view, covered));
            await OnDesfireTab<TabPageMifareDesfireDataExplorerView>(() => new MifareDesfireSetupViewModel(), (view, _) => Collect(view, covered));
            await OnClassicTab(() => new MifareClassicSetupViewModel(), (view, _) => Collect(view, covered));

            // The application key is edited in two tabs.
            var expected = SecureKeySlots.Desfire.Concat(SecureKeySlots.Classic).Concat(new[] { SecureKeySlots.DesfireAppCurrent });
            Assert.Equal(expected.OrderBy(slot => slot, StringComparer.Ordinal), covered.OrderBy(slot => slot, StringComparer.Ordinal));
        }

        // ── What the editor shows (the choice is made in the key manager; the state does what the manager does) ──

        [Fact]
        public async Task A_slot_named_by_a_reference_greys_out_its_key_box_and_shows_the_caption_and_back()
        {
            // A task type enables the group of the keys; without one the whole group is disabled and says nothing about the slot.
            await OnClassicTab(() => new MifareClassicSetupViewModel { SelectedTaskType = TaskType_MifareClassicTask.ReadData }, async (view, task) =>
            {
                var editor = EditorOf(view, SecureKeySlots.ClassicBCurrent);
                var keyBox = (TextBox)editor.EmbeddedKey;
                var caption = (TextBlock)editor.FindName("CaptionRow");

                // The frame of the line is what collapses; the line inside keeps the size it last had.
                var frame = (FrameworkElement)editor.FindName("CaptionFrame");

                // The box is covered by a plain grey field while the slot has a reference: the box is empty then, which the view would show as red.
                var cover = (FrameworkElement)editor.FindName("ReferenceCover");

                Assert.True(keyBox.IsEnabled);
                Assert.False(cover.IsVisible);
                Assert.False(caption.IsVisible);
                Assert.Equal(0, frame.ActualHeight);

                StateOf(editor).ToggleMode();
                await Flush();
                view.UpdateLayout();

                Assert.True(task.IsKeyBound(SecureKeySlots.ClassicBCurrent));
                Assert.True(keyBox.IsVisible);
                Assert.False(keyBox.IsEnabled);
                Assert.True(cover.IsVisible);
                Assert.True(caption.IsVisible);
                Assert.True(frame.ActualHeight > 0);

                StateOf(editor).ToggleMode();
                await Flush();
                view.UpdateLayout();

                Assert.False(task.IsKeyBound(SecureKeySlots.ClassicBCurrent));
                Assert.True(keyBox.IsEnabled);
                Assert.False(cover.IsVisible);
                Assert.False(caption.IsVisible);
                Assert.Equal(0, frame.ActualHeight);
            });
        }

        [Fact]
        public async Task The_key_box_still_edits_the_key_of_the_task()
        {
            await OnClassicTab(() => new MifareClassicSetupViewModel(), (view, task) =>
            {
                ((TextBox)EditorOf(view, SecureKeySlots.ClassicACurrent).EmbeddedKey).Text = "A1B2C3D4E5F6";

                Assert.Equal("A1B2C3D4E5F6", task.ClassicKeyAKeyCurrent);

                return Task.CompletedTask;
            });
        }

        [Fact]
        public async Task The_caption_is_bound_to_the_editor_and_never_to_a_key_of_the_task()
        {
            await OnClassicTab(() => new MifareClassicSetupViewModel(), async (view, task) =>
            {
                var editor = EditorOf(view, SecureKeySlots.ClassicACurrent);
                var caption = (TextBlock)editor.FindName("CaptionRow");
                StateOf(editor).ToggleMode();
                StateOf(editor).Reference = "cards/main";
                await Flush();
                view.UpdateLayout();

                Assert.Equal("State.ReferenceCaption", BindingPath(caption, TextBlock.TextProperty));
                Assert.Contains("cards/main", caption.Text);
                Assert.Equal("cards/main", task.GetKeyBinding(SecureKeySlots.ClassicACurrent).Reference);

                // The key of the task stays what the binding made it: nothing.
                Assert.True(string.IsNullOrEmpty(task.ClassicKeyAKeyCurrent));
            });
        }

        [Fact]
        public async Task A_slot_that_holds_a_key_is_switched_only_when_the_person_agrees()
        {
            await OnClassicTab(() => new MifareClassicSetupViewModel { ClassicKeyAKeyCurrent = "A1B2C3D4E5F6", SelectedTaskType = TaskType_MifareClassicTask.ReadData }, (view, task) =>
            {
                var editor = EditorOf(view, SecureKeySlots.ClassicACurrent);
                var dialogs = new RefusingDialogs();
                var state = new KeySlotEditorState(
                    task,
                    SecureKeySlots.ClassicACurrent,
                    new[] { new SecureKeyProviderInfo(WindowsCredentialKeyProvider.ProviderId, "x") },
                    dialogs,
                    new WindowsCredentialKeyProvider(new FakeCredentialStore()));

                state.ToggleMode();

                // The same decision as the key manager makes, with the question answered "no".
                Assert.Equal(1, dialogs.Asked);
                Assert.False(task.IsKeyBound(SecureKeySlots.ClassicACurrent));
                Assert.Equal("A1B2C3D4E5F6", task.ClassicKeyAKeyCurrent);
                Assert.True(((TextBox)editor.EmbeddedKey).IsEnabled);

                return Task.CompletedTask;
            });
        }

        [Fact]
        public async Task A_task_that_is_already_bound_is_shown_with_its_reference_below_the_greyed_out_key_box()
        {
            await OnClassicTab(
                () =>
                {
                    var task = new MifareClassicSetupViewModel { SelectedTaskType = TaskType_MifareClassicTask.ReadData };
                    task.BindKeySlot(SecureKeySlots.ClassicACurrent, WindowsCredentialKeyProvider.ProviderId, "cards/main");

                    return task;
                },
                (view, _) =>
                {
                    var editor = EditorOf(view, SecureKeySlots.ClassicACurrent);
                    var caption = (TextBlock)editor.FindName("CaptionRow");

                    Assert.False(((TextBox)editor.EmbeddedKey).IsEnabled);
                    Assert.True(caption.IsVisible);
                    Assert.Contains("cards/main", caption.Text);

                    return Task.CompletedTask;
                });
        }

        [Fact]
        public async Task No_caption_is_blank_while_slots_show_a_reference()
        {
            await OnDesfireTab<TabPageMifareDesfireApplicationMasteringView>(
                () =>
                {
                    var task = new MifareDesfireSetupViewModel();
                    task.BindKeySlot(SecureKeySlots.DesfireAppCurrent, WindowsCredentialKeyProvider.ProviderId, "apps/main");

                    return task;
                },
                (view, _) =>
                {
                    Assert.Empty(BlankCaptions(view));

                    return Task.CompletedTask;
                });
        }

        // ── The layout does not move ──

        [Fact]
        public async Task A_reference_adds_the_caption_below_the_key_box_and_moves_nothing_else()
        {
            await OnClassicTab(() => new MifareClassicSetupViewModel(), async (view, _) =>
            {
                var editor = EditorOf(view, SecureKeySlots.ClassicACurrent);
                var host = (FrameworkElement)editor.FindName("EmbeddedHost");
                var caption = (FrameworkElement)editor.FindName("CaptionFrame");
                var widthBefore = editor.ActualWidth;
                var boxBefore = (host.ActualWidth, host.ActualHeight);
                var heightBefore = editor.ActualHeight;

                StateOf(editor).ToggleMode();

                // The longest name there is: a caption that ran on would push the columns apart.
                StateOf(editor).Reference = new string('a', SecureKeySyntax.MaxReferenceLength);
                await Flush();
                view.UpdateLayout();

                Assert.Equal(widthBefore, editor.ActualWidth);
                Assert.Equal(boxBefore, (host.ActualWidth, host.ActualHeight));

                // Taller by the caption and no more than its height and the margin below it.
                Assert.True(editor.ActualHeight > heightBefore, "the caption has no height");
                Assert.True(editor.ActualHeight - heightBefore <= caption.ActualHeight + caption.Margin.Bottom + 0.5, "the editor grew by " + (editor.ActualHeight - heightBefore));
            });
        }

        // ── Every key has a way to its manager ──

        [Fact]
        public async Task In_every_task_type_a_visible_key_that_can_be_edited_has_a_visible_enabled_manager_button_in_its_group_box()
        {
            // There is no way to switch a slot except the key manager: a key whose button is hidden could never be bound.
            foreach (var type in Enum.GetValues<TaskType_MifareDesfireTask>())
            {
                await OnDesfireTab<TabPageMifareDesfireCardMasteringView>(() => new MifareDesfireSetupViewModel { SelectedTaskType = type }, (view, _) => AssertEveryKeyHasItsButton(view, type.ToString()));
                await OnDesfireTab<TabPageMifareDesfireApplicationMasteringView>(() => new MifareDesfireSetupViewModel { SelectedTaskType = type }, (view, _) => AssertEveryKeyHasItsButton(view, type.ToString()));
                await OnDesfireTab<TabPageMifareDesfireDataExplorerView>(() => new MifareDesfireSetupViewModel { SelectedTaskType = type }, (view, _) => AssertEveryKeyHasItsButton(view, type.ToString()));
            }

            foreach (var type in Enum.GetValues<TaskType_MifareClassicTask>())
            {
                await OnClassicTab(() => new MifareClassicSetupViewModel { SelectedTaskType = type }, (view, _) => AssertEveryKeyHasItsButton(view, type.ToString()));
            }
        }

        [Fact]
        public async Task The_button_of_the_classic_key_group_opens_the_manager_for_the_four_keys_in_it_in_screen_order()
        {
            await OnClassicTab(() => new MifareClassicSetupViewModel { SelectedTaskType = TaskType_MifareClassicTask.ReadData }, (view, _) =>
            {
                var recorder = new RecordingLauncher();
                var keyA = EditorOf(view, SecureKeySlots.ClassicACurrent);
                var button = LogicalDescendants(view).OfType<KeyManagerButton>().First(candidate => candidate.IsVisible && EditorGroupBox(candidate) is { } box && IsInside(keyA, box));
                button.Launcher = recorder;
                button.KeyStore = () => new WindowsCredentialKeyProvider(new FakeCredentialStore());

                button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

                // Current and new side by side, key A above key B.
                Assert.Equal(
                    new[] { SecureKeySlots.ClassicACurrent, SecureKeySlots.ClassicATarget, SecureKeySlots.ClassicBCurrent, SecureKeySlots.ClassicBTarget },
                    Assert.Single(recorder.Opened));

                return Task.CompletedTask;
            });
        }

        // ── The tabs at the smallest size of their windows ──

        [Theory]
        [InlineData(900, 700)]
        [InlineData(1000, 750)]
        public async Task A_reference_moves_nothing_sideways_and_the_manager_buttons_stay_inside_the_window(int width, int height)
        {
            // The data explorer, at the smallest size, already reaches over the edge of its own group box; what this asks is that a
            // reference makes it no worse, and that the buttons are in the window.
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var tabs = AllTabs();

                foreach (var (name, make) in tabs)
                {
                    var typed = await LayoutOf(make(false), width, height);
                    var bound = await LayoutOf(make(true), width, height);

                    Assert.NotEmpty(typed.Keys);
                    Assert.Equal(typed.Keys.Keys.OrderBy(slot => slot, StringComparer.Ordinal), bound.Keys.Keys.OrderBy(slot => slot, StringComparer.Ordinal));

                    foreach (var (slot, before) in typed.Keys)
                    {
                        var after = bound.Keys[slot];

                        Assert.True(after.Right <= before.Right + 0.5, name + " " + slot + ": a reference moves the key box from " + before.Right + " to " + after.Right);
                        Assert.True(Math.Abs(after.Width - before.Width) <= 0.5, name + " " + slot + ": a reference changes the width of the key box from " + before.Width + " to " + after.Width);
                    }

                    Assert.NotEmpty(bound.Buttons);

                    foreach (var button in bound.Buttons)
                    {
                        Assert.True(button.Height >= 26 - 0.5, name + ": a manager button is squeezed to " + button.Height);
                        Assert.True(button.Bottom <= height, name + ": a manager button reaches below the window (" + button.Bottom + " of " + height + ")");
                        Assert.True(button.Right <= width, name + ": a manager button reaches beyond the window (" + button.Right + " of " + width + ")");
                    }
                }
            });
        }

        // Every tab that has keys, as a view and its task: typed, or with every key named by a reference.
        private static (string Name, Func<bool, (FrameworkElement View, object Task)> Make)[] AllTabs() => new (string Name, Func<bool, (FrameworkElement View, object Task)> Make)[]
        {
            ("card mastering", bound => (new TabPageMifareDesfireCardMasteringView(), bound ? BoundDesfire(TaskType_MifareDesfireTask.PICCMasterKeyChangeover) : new MifareDesfireSetupViewModel { SelectedTaskType = TaskType_MifareDesfireTask.PICCMasterKeyChangeover })),
            ("application mastering", bound => (new TabPageMifareDesfireApplicationMasteringView(), bound ? BoundDesfire(TaskType_MifareDesfireTask.ApplicationKeyChangeover) : new MifareDesfireSetupViewModel { SelectedTaskType = TaskType_MifareDesfireTask.ApplicationKeyChangeover })),
            ("data explorer", bound => (new TabPageMifareDesfireDataExplorerView(), bound ? BoundDesfire(TaskType_MifareDesfireTask.ReadData) : new MifareDesfireSetupViewModel { SelectedTaskType = TaskType_MifareDesfireTask.ReadData })),
            ("classic keys", bound => (new TabPageMifareClassicKeySetupView(), bound ? BoundClassic() : new MifareClassicSetupViewModel { SelectedTaskType = TaskType_MifareClassicTask.ReadData })),
        };

        [Fact]
        public async Task The_tabs_are_rendered_to_pictures_when_a_folder_is_given()
        {
            // For a person to look at, like the API snapshots that are updated on request: nothing happens without the variable.
            var folder = Environment.GetEnvironmentVariable("RFIDGEAR_RENDER_DIR");

            if (string.IsNullOrWhiteSpace(folder))
            {
                return;
            }

            System.IO.Directory.CreateDirectory(folder);

            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                foreach (var (width, height) in new[] { (900, 700), (1000, 750), (1200, 750) })
                {
                    foreach (var (name, make) in AllTabs())
                    {
                        foreach (var bound in new[] { false, true })
                        {
                            var (view, task) = make(bound);
                            view.DataContext = task;
                            var window = new Window { Content = view, Width = width, Height = height, ShowActivated = false, ShowInTaskbar = false };

                            try
                            {
                                window.Show();
                                window.UpdateLayout();
                                await Flush();
                                window.UpdateLayout();

                                var root = (FrameworkElement)VisualTreeHelper.GetChild(window, 0);
                                var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth), (int)Math.Ceiling(root.ActualHeight), 96, 96, PixelFormats.Pbgra32);
                                bitmap.Render(root);
                                var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                                encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));

                                using var file = System.IO.File.Create(System.IO.Path.Combine(folder, "tab-" + name.Replace(' ', '-') + (bound ? "-reference-" : "-typed-") + width + "x" + height + ".png"));
                                encoder.Save(file);
                            }
                            finally
                            {
                                window.Close();
                            }
                        }
                    }
                }
            });
        }
        private sealed record KeyBounds(double Right, double Width);

        private sealed record ButtonBounds(double Right, double Bottom, double Height);

        private sealed record TabLayout(Dictionary<string, KeyBounds> Keys, List<ButtonBounds> Buttons);

        // Shows a tab alone in a window of the given size and measures where its keys and manager buttons are.
        private static async Task<TabLayout> LayoutOf((FrameworkElement View, object Task) tab, int width, int height)
        {
            tab.View.DataContext = tab.Task;
            var window = new Window { Content = tab.View, Width = width, Height = height, ShowActivated = false, ShowInTaskbar = false };

            try
            {
                window.Show();
                window.UpdateLayout();
                await Flush();
                window.UpdateLayout();

                var keys = EditorsIn(tab.View)
                    .Where(editor => editor.IsVisible)
                    .GroupBy(editor => editor.SlotId)
                    .ToDictionary(
                        group => group.Key,
                        group => new KeyBounds(group.First().TransformToAncestor(window).Transform(new Point(group.First().ActualWidth, 0)).X, group.First().ActualWidth));

                var buttons = LogicalDescendants(tab.View)
                    .OfType<KeyManagerButton>()
                    .Where(button => button.IsVisible)
                    .Select(button => new ButtonBounds(
                        button.TransformToAncestor(window).Transform(new Point(button.ActualWidth, 0)).X,
                        button.TransformToAncestor(window).Transform(new Point(0, button.ActualHeight)).Y,
                        button.ActualHeight))
                    .ToList();

                return new TabLayout(keys, buttons);
            }
            finally
            {
                window.Close();
            }
        }

        // ── The dialog while the keys are obtained ──

        [Fact]
        public async Task The_desfire_dialog_takes_no_input_while_a_provider_is_asked()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var provider = new BlockingKeyProvider(16);
                KeyResolutionScope.SetDefault(new KeyResolutionContext(provider.Registry(), "run-1", "0", null, interactive: false));
                var task = new MifareDesfireSetupViewModel
                {
                    SelectedTaskType = TaskType_MifareDesfireTask.ReadData,
                    CurrentTaskIndex = "0",
                    AppNumberCurrent = "1",
                    FileNumberCurrent = "2",
                    FileSizeCurrent = "16",
                    KeyBindings = new[] { new KeyBindingEntry { Slot = SecureKeySlots.DesfireRead, ProviderId = BlockingKeyProvider.Id, Reference = "ref/read" } },
                };
                var window = new RFiDGear.View.MifareDesfireSetupView { DataContext = task, ShowActivated = false, ShowInTaskbar = false };

                try
                {
                    window.Show();
                    window.UpdateLayout();
                    await Flush();

                    var tabs = One<TabControl>(window, UIElement.IsEnabledProperty, nameof(MifareDesfireSetupViewModel.AreInputsEnabled));
                    var taskGroup = One<GroupBox>(window, UIElement.IsEnabledProperty, nameof(MifareDesfireSetupViewModel.AreInputsEnabled));
                    var typeSelector = One<ComboBox>(window, Selector.SelectedItemProperty, nameof(MifareDesfireSetupViewModel.SelectedTaskType));
                    Assert.True(tabs.IsEnabled);
                    Assert.True(typeSelector.IsEnabled);

                    using (ReaderDeviceScope.Use(new KeyRecordingReader()))
                    {
                        var running = task.CommandDelegator.ExecuteAsync(TaskType_MifareDesfireTask.ReadData);
                        await provider.Asked;
                        await Flush();

                        // The tabs and the task type with the index, the description and the conditions above them: changing
                        // the type meanwhile would change keys that the command is waiting for.
                        Assert.False(tabs.IsEnabled);
                        Assert.False(taskGroup.IsEnabled);
                        Assert.False(typeSelector.IsEnabled);

                        provider.Release();
                        await running;
                        await Flush();
                    }

                    Assert.True(tabs.IsEnabled);
                    Assert.True(taskGroup.IsEnabled);
                    Assert.True(typeSelector.IsEnabled);
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [Fact]
        public async Task The_classic_dialog_takes_no_input_while_a_provider_is_asked()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var provider = new BlockingKeyProvider(6);
                KeyResolutionScope.SetDefault(new KeyResolutionContext(provider.Registry(), "run-1", "0", null, interactive: false));
                var task = new MifareClassicSetupViewModel
                {
                    SelectedTaskType = TaskType_MifareClassicTask.ReadData,
                    CurrentTaskIndex = "0",
                    KeyBindings = new[]
                    {
                        new KeyBindingEntry { Slot = SecureKeySlots.ClassicACurrent, ProviderId = BlockingKeyProvider.Id, Reference = "ref/a" },
                        new KeyBindingEntry { Slot = SecureKeySlots.ClassicBCurrent, ProviderId = BlockingKeyProvider.Id, Reference = "ref/b" },
                    },
                };
                var window = new RFiDGear.View.MifareClassicSetupView { DataContext = task, ShowActivated = false, ShowInTaskbar = false };

                try
                {
                    window.Show();
                    window.UpdateLayout();
                    await Flush();

                    var tabs = One<TabControl>(window, UIElement.IsEnabledProperty, nameof(MifareClassicSetupViewModel.AreInputsEnabled));
                    var taskGroup = One<GroupBox>(window, UIElement.IsEnabledProperty, nameof(MifareClassicSetupViewModel.AreInputsEnabled));
                    var typeSelector = One<ComboBox>(window, Selector.SelectedItemProperty, nameof(MifareClassicSetupViewModel.SelectedTaskType));
                    Assert.True(tabs.IsEnabled);
                    Assert.True(typeSelector.IsEnabled);

                    using (ReaderDeviceScope.Use(new KeyRecordingReader()))
                    {
                        var running = task.CommandDelegator.ExecuteAsync(TaskType_MifareClassicTask.ReadData);
                        await provider.Asked;
                        await Flush();

                        // The tabs and the task type with the index, the description and the conditions above them: changing
                        // the type meanwhile would change keys that the command is waiting for.
                        Assert.False(tabs.IsEnabled);
                        Assert.False(taskGroup.IsEnabled);
                        Assert.False(typeSelector.IsEnabled);

                        provider.Release();
                        await running;
                        await Flush();
                    }

                    Assert.True(tabs.IsEnabled);
                    Assert.True(taskGroup.IsEnabled);
                    Assert.True(typeSelector.IsEnabled);
                }
                finally
                {
                    window.Close();
                }
            });
        }

        // ── Helpers ──

        private static Task OnDesfireTab<TView>(Func<MifareDesfireSetupViewModel> create, Func<TView, MifareDesfireSetupViewModel, Task> inspect)
            where TView : FrameworkElement, new() =>
            InspectAsync<TView, MifareDesfireSetupViewModel>(create, inspect);

        private static Task OnClassicTab(Func<MifareClassicSetupViewModel> create, Func<TabPageMifareClassicKeySetupView, MifareClassicSetupViewModel, Task> inspect) =>
            InspectAsync<TabPageMifareClassicKeySetupView, MifareClassicSetupViewModel>(create, inspect);

        private static void UseProviders(params SecureKeyProviderRegistration[] providers) =>
            KeyResolutionScope.SetDefault(new KeyResolutionContext(
                SecureKeyRegistry.Create(providers, Array.Empty<Lazy<ISecureKeyProvider, RFiDGear.ExtensionSdk.ISecureKeyProviderMetadata>>()),
                "run-1",
                "0",
                null,
                interactive: true));

        private static List<KeySlotEditor> EditorsIn(FrameworkElement view) => LogicalDescendants(view).OfType<KeySlotEditor>().ToList();

        private static KeySlotEditor EditorOf(FrameworkElement view, string slot) => Assert.Single(EditorsIn(view), editor => editor.SlotId == slot);

        private static KeySlotEditorState StateOf(KeySlotEditor editor) => Assert.IsType<KeySlotEditorState>(editor.State);

        private static Task Collect(FrameworkElement view, List<string> covered)
        {
            covered.AddRange(EditorsIn(view).Select(editor => editor.SlotId));

            return Task.CompletedTask;
        }

        private static void AssertEditors(FrameworkElement view, params (string Slot, string Property)[] expected)
        {
            var editors = EditorsIn(view);

            // Exactly the keys of the tab, each once: a key without an editor could not be bound.
            Assert.Equal(expected.Select(e => e.Slot).OrderBy(slot => slot, StringComparer.Ordinal), editors.Select(editor => editor.SlotId).OrderBy(slot => slot, StringComparer.Ordinal));

            foreach (var (slot, property) in expected)
            {
                var editor = EditorOf(view, slot);

                // The editor keeps the box of the key, and the box edits the property of that very slot.
                var box = Assert.IsType<TextBox>(editor.EmbeddedKey);
                Assert.Equal(property, BindingPath(box, TextBox.TextProperty));

                // The task can be edited: the editor has its state.
                Assert.Same(view.DataContext, editor.Host);
                Assert.NotNull(editor.State);
            }
        }

        private sealed class RefusingDialogs : IKeySlotEditorDialogs
        {
            public int Asked { get; private set; }

            public bool ConfirmReplaceKey()
            {
                Asked++;

                return false;
            }
        }

        private sealed class RecordingLauncher : IKeyManagerLauncher
        {
            public List<string[]> Opened { get; } = new List<string[]>();

            public void Show(KeyManagerViewModel viewModel, Window? owner) =>
                Opened.Add(viewModel.Rows.Select(row => row.SlotId).ToArray());
        }

        // A task of which every slot is named by a reference, so that every caption shows.
        private static MifareDesfireSetupViewModel BoundDesfire(TaskType_MifareDesfireTask type)
        {
            var task = new MifareDesfireSetupViewModel { SelectedTaskType = type };

            foreach (var slot in SecureKeySlots.Desfire)
            {
                task.BindKeySlot(slot, WindowsCredentialKeyProvider.ProviderId, "cards/" + slot);
            }

            return task;
        }

        private static MifareClassicSetupViewModel BoundClassic()
        {
            var task = new MifareClassicSetupViewModel { SelectedTaskType = TaskType_MifareClassicTask.ReadData };

            foreach (var slot in SecureKeySlots.Classic)
            {
                task.BindKeySlot(slot, WindowsCredentialKeyProvider.ProviderId, "cards/" + slot);
            }

            return task;
        }

        // The group box a control sits in, the nearest one: the section of keys that a manager button is for.
        private static GroupBox? EditorGroupBox(DependencyObject element)
        {
            for (var parent = VisualTreeHelper.GetParent(element); parent != null; parent = VisualTreeHelper.GetParent(parent))
            {
                if (parent is GroupBox box)
                {
                    return box;
                }
            }

            return null;
        }

        private static bool IsInside(DependencyObject element, DependencyObject ancestor)
        {
            for (var parent = VisualTreeHelper.GetParent(element); parent != null; parent = VisualTreeHelper.GetParent(parent))
            {
                if (ReferenceEquals(parent, ancestor))
                {
                    return true;
                }
            }

            return false;
        }

        // Every key that is shown and can be edited has a button that is shown and enabled in a group box that holds the key.
        private static Task AssertEveryKeyHasItsButton(FrameworkElement view, string taskType)
        {
            var buttons = LogicalDescendants(view).OfType<KeyManagerButton>().Where(button => button.IsVisible && button.IsEnabled).ToList();

            foreach (var editor in EditorsIn(view).Where(candidate => candidate.IsVisible && candidate.IsEnabled))
            {
                Assert.True(
                    buttons.Any(button => EditorGroupBox(button) is { } box && IsInside(editor, box)),
                    view.GetType().Name + " / " + taskType + ": the key " + editor.SlotId + " is shown and can be edited, but no manager button that is shown and enabled is in its section");
            }

            return Task.CompletedTask;
        }
    }
}
