#nullable enable
using RFiDGear.Services.SecureKeys;
using RFiDGear.Tests.SecureKeys;
using RFiDGear.View;

using System;
using System.Collections.Generic;
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
    /// The button at the bottom of a section of keys: it looks like the buttons around it, and it opens the key manager for the
    /// key slot editors that are visible in its section, in the order they stand on the screen.
    /// </summary>
    [Collection("WpfBindingTrace")]
    public class KeyManagerButtonTests
    {
        private sealed class RecordingLauncher : IKeyManagerLauncher
        {
            public List<(string[] Slots, Window? Owner)> Shown { get; } = new List<(string[], Window?)>();

            public void Show(KeyManagerViewModel viewModel, Window? owner) =>
                Shown.Add((viewModel.Rows.Select(row => row.SlotId).ToArray(), owner));
        }

        // A group box with a grid of four rows, the button in the last. It is made on the thread of the window, as every control is.
        private sealed class Section
        {
            internal FakeKeySlotHost Host { get; } = new FakeKeySlotHost();

            internal RecordingLauncher Launcher { get; } = new RecordingLauncher();

            internal GroupBox Box { get; } = new GroupBox();

            internal Grid Grid { get; } = new Grid();

            internal KeyManagerButton Button { get; }

            internal Section()
            {
                for (var row = 0; row < 4; row++)
                {
                    Grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                }

                Button = new KeyManagerButton
                {
                    Launcher = Launcher,
                    Providers = () => new[] { new SecureKeyProviderInfo(WindowsCredentialKeyProvider.ProviderId, "x") },
                    KeyStore = () => new WindowsCredentialKeyProvider(new FakeCredentialStore()),
                    SupplierStatus = new FakeSupplierStatus(),
                    Text = TestText.English,
                };
                Grid.SetRow(Button, 3);
                Grid.Children.Add(Button);
                Box.Content = Grid;
            }

            // Editors are added in the order of the markup, which need not be the order on the screen.
            internal KeySlotEditor AddEditor(string slot, int row, bool visible = true)
            {
                var editor = new KeySlotEditor
                {
                    Host = Host,
                    SlotId = slot,
                    EmbeddedKey = new TextBox { Height = 26 },
                    Visibility = visible ? Visibility.Visible : Visibility.Collapsed,
                };
                Grid.SetRow(editor, row);
                Grid.Children.Add(editor);

                return editor;
            }
        }

        // Makes the section on the STA thread, lets the test arrange it, shows it in a window and lets the test look.
        private static async Task Shown(Action<Section> arrange, Func<Section, Window, Task> inspect)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var section = new Section();
                arrange(section);
                var window = new Window { Content = section.Box, Width = 600, Height = 400, ShowActivated = false, ShowInTaskbar = false };

                try
                {
                    window.Show();
                    window.UpdateLayout();
                    await Flush();
                    window.UpdateLayout();

                    await inspect(section, window);
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [Fact]
        public async Task The_button_looks_like_the_buttons_at_the_bottom_of_a_group_box_and_its_tooltip_wraps()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var button = new KeyManagerButton();

                Assert.Equal(26, button.Height);
                Assert.Equal(new Thickness(5, 2, 5, 2), button.Padding);
                Assert.Equal(HorizontalAlignment.Stretch, button.HorizontalAlignment);
                Assert.Equal(VerticalAlignment.Bottom, button.VerticalAlignment);
                Assert.False(string.IsNullOrWhiteSpace(button.Content as string));

                var tip = Assert.IsType<ToolTip>(button.ToolTip);
                Assert.True(tip.MaxWidth <= 400);
                Assert.Equal(TextWrapping.Wrap, Assert.IsType<TextBlock>(tip.Content).TextWrapping);
            });
        }

        [Fact]
        public async Task The_visible_editors_of_the_section_are_found_in_the_order_they_stand_on_the_screen()
        {
            await Shown(
                section =>
                {
                    section.AddEditor(SecureKeySlots.DesfireAppNew, row: 2);
                    section.AddEditor(SecureKeySlots.DesfireAppOld, row: 1, visible: false);
                    section.AddEditor(SecureKeySlots.DesfireAppCurrent, row: 0);
                },
                (section, _) =>
                {
                    var editors = section.Button.FindEditors();

                    Assert.Equal(new[] { SecureKeySlots.DesfireAppCurrent, SecureKeySlots.DesfireAppNew }, editors.Select(editor => editor.SlotId).ToArray());

                    return Task.CompletedTask;
                });
        }

        [Fact]
        public async Task Opening_gives_the_manager_the_slots_of_the_section_and_the_window_as_owner_and_lets_go_of_the_task_after()
        {
            await Shown(
                section =>
                {
                    section.AddEditor(SecureKeySlots.DesfireRead, row: 0);
                    section.AddEditor(SecureKeySlots.DesfireWrite, row: 1);
                },
                (section, window) =>
                {
                    var listenersBefore = section.Host.Listeners;

                    Assert.True(section.Button.Open());

                    var (slots, owner) = Assert.Single(section.Launcher.Shown);
                    Assert.Equal(new[] { SecureKeySlots.DesfireRead, SecureKeySlots.DesfireWrite }, slots);
                    Assert.Same(window, owner);

                    // The manager made a state for each slot and disposed them with itself: only the editors still listen.
                    Assert.Equal(listenersBefore, section.Host.Listeners);

                    return Task.CompletedTask;
                });
        }

        [Fact]
        public async Task A_section_with_no_visible_key_opens_nothing()
        {
            await Shown(
                section => section.AddEditor(SecureKeySlots.DesfireRead, row: 0, visible: false),
                (section, _) =>
                {
                    Assert.False(section.Button.Open());
                    Assert.Empty(section.Launcher.Shown);

                    return Task.CompletedTask;
                });
        }

        [Fact]
        public async Task A_click_opens_the_manager()
        {
            await Shown(
                section => section.AddEditor(SecureKeySlots.DesfireRead, row: 0),
                (section, _) =>
                {
                    section.Button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

                    Assert.Single(section.Launcher.Shown);

                    return Task.CompletedTask;
                });
        }

        [Fact]
        public async Task A_scope_that_is_given_replaces_the_group_box()
        {
            await Shown(
                section =>
                {
                    section.AddEditor(SecureKeySlots.DesfireRead, row: 0);
                    var other = new Grid();
                    other.Children.Add(new KeySlotEditor { Host = section.Host, SlotId = SecureKeySlots.DesfireWrite, EmbeddedKey = new TextBox() });
                    Grid.SetRow(other, 1);
                    section.Grid.Children.Add(other);
                    section.Button.Scope = other;
                },
                (section, _) =>
                {
                    Assert.Equal(new[] { SecureKeySlots.DesfireWrite }, section.Button.FindEditors().Select(editor => editor.SlotId).ToArray());

                    return Task.CompletedTask;
                });
        }
    }
}
