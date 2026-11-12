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

        // ── Switching ──

        [Fact]
        public async Task Switching_a_slot_to_a_reference_hides_the_key_box_and_shows_the_reference_and_back()
        {
            await OnClassicTab(() => new MifareClassicSetupViewModel(), async (view, task) =>
            {
                var editor = EditorOf(view, SecureKeySlots.ClassicBCurrent);
                var keyBox = (TextBox)editor.EmbeddedKey;
                var panel = (FrameworkElement)editor.FindName("ReferencePanel");

                Assert.True(keyBox.IsVisible);
                Assert.False(panel.IsVisible);

                StateOf(editor).ToggleMode();
                await Flush();
                view.UpdateLayout();

                Assert.True(task.IsKeyBound(SecureKeySlots.ClassicBCurrent));
                Assert.False(keyBox.IsVisible);
                Assert.True(panel.IsVisible);
                Assert.True(((FrameworkElement)editor.FindName("ReferenceBox")).IsVisible);

                StateOf(editor).ToggleMode();
                await Flush();
                view.UpdateLayout();

                Assert.False(task.IsKeyBound(SecureKeySlots.ClassicBCurrent));
                Assert.True(keyBox.IsVisible);
                Assert.False(panel.IsVisible);
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
        public async Task The_reference_box_is_bound_to_the_editor_and_never_to_a_key_of_the_task()
        {
            await OnClassicTab(() => new MifareClassicSetupViewModel(), async (view, task) =>
            {
                var editor = EditorOf(view, SecureKeySlots.ClassicACurrent);
                StateOf(editor).ToggleMode();
                await Flush();
                var referenceBox = (TextBox)editor.FindName("ReferenceBox");

                Assert.Equal("Reference", BindingPath(referenceBox, TextBox.TextProperty));
                Assert.IsType<KeySlotEditorState>(referenceBox.DataContext);
                Assert.Equal(SecureKeySyntax.MaxReferenceLength, referenceBox.MaxLength);

                referenceBox.Text = "cards/main";

                Assert.Equal("cards/main", task.GetKeyBinding(SecureKeySlots.ClassicACurrent).Reference);

                // The key of the task stays what the binding made it: nothing.
                Assert.True(string.IsNullOrEmpty(task.ClassicKeyAKeyCurrent));
            });
        }

        [Fact]
        public async Task A_slot_that_holds_a_key_is_switched_only_when_the_person_agrees()
        {
            await OnClassicTab(() => new MifareClassicSetupViewModel { ClassicKeyAKeyCurrent = "A1B2C3D4E5F6" }, (view, task) =>
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

                // The same decision as the editor of the tab makes, with the question answered "no".
                Assert.Equal(1, dialogs.Asked);
                Assert.False(task.IsKeyBound(SecureKeySlots.ClassicACurrent));
                Assert.Equal("A1B2C3D4E5F6", task.ClassicKeyAKeyCurrent);
                Assert.True(((TextBox)editor.EmbeddedKey).IsVisible);

                return Task.CompletedTask;
            });
        }

        [Fact]
        public async Task The_store_button_is_there_for_the_credential_manager_and_waits_for_a_valid_reference()
        {
            // A task type enables the group of the keys; without one the whole group, and so the button, is disabled.
            await OnClassicTab(() => new MifareClassicSetupViewModel { SelectedTaskType = TaskType_MifareClassicTask.ReadData }, async (view, task) =>
            {
                var editor = EditorOf(view, SecureKeySlots.ClassicACurrent);
                var button = (Button)editor.FindName("StoreKeyButton");
                Assert.False(button.IsVisible);

                StateOf(editor).ToggleMode();
                await Flush();
                view.UpdateLayout();

                Assert.True(button.IsVisible, "the store button is not visible");
                Assert.False(button.IsEnabled);

                ((TextBox)editor.FindName("ReferenceBox")).Text = "cards/main";
                await Flush();

                Assert.Equal("cards/main", StateOf(editor).Reference);
                Assert.True(StateOf(editor).CanStoreKey);
                Assert.True(button.Command.CanExecute(null));
                Assert.True(button.IsEnabled);
            });
        }

        [Fact]
        public async Task The_provider_box_appears_only_when_there_is_more_than_one_provider_to_choose_from()
        {
            await OnClassicTab(() => new MifareClassicSetupViewModel(), async (view, task) =>
            {
                var editor = EditorOf(view, SecureKeySlots.ClassicACurrent);
                StateOf(editor).ToggleMode();
                await Flush();
                view.UpdateLayout();

                Assert.False(((FrameworkElement)editor.FindName("ProviderBox")).IsVisible);
            });

            UseProviders(
                WindowsCredentialKeyProvider.Registration(),
                new SecureKeyProviderRegistration("other.vault", "Other vault", ScriptedSecureKeyProvider.Failing(SecureKeyFailureReason.NotFound)));

            await OnClassicTab(() => new MifareClassicSetupViewModel(), async (view, task) =>
            {
                var editor = EditorOf(view, SecureKeySlots.ClassicACurrent);
                StateOf(editor).ToggleMode();
                await Flush();
                view.UpdateLayout();

                var box = (ComboBox)editor.FindName("ProviderBox");
                Assert.True(box.IsVisible);
                Assert.Equal(2, box.Items.Count);
                Assert.Equal(WindowsCredentialKeyProvider.ProviderId, box.SelectedValue);
            });
        }

        [Fact]
        public async Task Without_a_provider_the_mode_cannot_be_switched()
        {
            UseProviders();

            await OnClassicTab(() => new MifareClassicSetupViewModel(), (view, task) =>
            {
                var editor = EditorOf(view, SecureKeySlots.ClassicACurrent);
                var button = (Button)editor.FindName("ModeButton");

                Assert.False(button.IsEnabled);
                Assert.True(((TextBox)editor.EmbeddedKey).IsVisible);

                return Task.CompletedTask;
            });
        }

        [Fact]
        public async Task A_task_that_is_already_bound_is_shown_with_its_reference()
        {
            await OnClassicTab(
                () =>
                {
                    var task = new MifareClassicSetupViewModel();
                    task.BindKeySlot(SecureKeySlots.ClassicACurrent, WindowsCredentialKeyProvider.ProviderId, "cards/main");

                    return task;
                },
                (view, task) =>
                {
                    var editor = EditorOf(view, SecureKeySlots.ClassicACurrent);

                    Assert.False(((TextBox)editor.EmbeddedKey).IsVisible);
                    Assert.Equal("cards/main", ((TextBox)editor.FindName("ReferenceBox")).Text);

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
                (view, task) =>
                {
                    Assert.Empty(BlankCaptions(view));

                    return Task.CompletedTask;
                });
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
                    Assert.True(tabs.IsEnabled);

                    using (ReaderDeviceScope.Use(new KeyRecordingReader()))
                    {
                        var running = task.CommandDelegator.ExecuteAsync(TaskType_MifareDesfireTask.ReadData);
                        await provider.Asked;
                        await Flush();

                        Assert.False(tabs.IsEnabled);

                        provider.Release();
                        await running;
                        await Flush();
                    }

                    Assert.True(tabs.IsEnabled);
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
                    Assert.True(tabs.IsEnabled);

                    using (ReaderDeviceScope.Use(new KeyRecordingReader()))
                    {
                        var running = task.CommandDelegator.ExecuteAsync(TaskType_MifareClassicTask.ReadData);
                        await provider.Asked;
                        await Flush();

                        Assert.False(tabs.IsEnabled);

                        provider.Release();
                        await running;
                        await Flush();
                    }

                    Assert.True(tabs.IsEnabled);
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

            public System.Security.SecureString? AskForKey(int hexLength) => null;

            public void Show(KeySlotMessage message, int detail)
            {
                // Nothing is ever stored in this test, so there is nothing to tell.
            }
        }
    }
}
