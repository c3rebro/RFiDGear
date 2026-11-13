#nullable enable
using RFiDGear.Services.SecureKeys;
using RFiDGear.View;

using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

using Xunit;
using static RFiDGear.Tests.RenderedTab;

namespace RFiDGear.Tests
{
    /// <summary>
    /// The box in which a key is entered in the key manager, rendered for real: hidden until the person asks to see it, limited to
    /// the digits of the key, and coloured like the boxes of the task dialogs (green for a key, red for anything else) while it is
    /// typed. Controls are found by their names.
    /// </summary>
    [Collection("WpfBindingTrace")]
    public class KeyEntryBoxTests
    {
        private static async Task OnEntry(int hexLength, Func<Window, KeyEntryBox, PasswordBox, TextBox, CheckBox, Task> inspect)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var entry = new KeyEntryBox { HexLength = hexLength, Label = "Key", ShowKeyText = "Show key" };
                var window = new Window { Content = entry, Width = 400, Height = 100, ShowActivated = false, ShowInTaskbar = false };

                try
                {
                    window.Show();
                    await Settle(window);

                    await inspect(
                        window,
                        entry,
                        (PasswordBox)entry.FindName("HiddenBox"),
                        (TextBox)entry.FindName("ShownBox"),
                        (CheckBox)entry.FindName("ShowCheck"));
                }
                finally
                {
                    window.Close();
                }
            });
        }

        private static async Task Settle(Window window)
        {
            window.UpdateLayout();
            await Flush();
            window.UpdateLayout();
        }

        private static Color BackgroundOf(Control box) => ((SolidColorBrush)box.Background).Color;

        private static Color ForegroundOf(Control box) => ((SolidColorBrush)box.Foreground).Color;

        // ── Hidden, and shown on request ──

        [Fact]
        public async Task The_key_is_hidden_until_the_person_asks_to_see_it()
        {
            await OnEntry(12, (_, entry, hidden, shown, check) =>
            {
                Assert.False(entry.IsShown);
                Assert.False(check.IsChecked);
                Assert.Equal("Show key", check.Content);
                Assert.True(hidden.IsVisible);
                Assert.False(shown.IsVisible);

                return Task.CompletedTask;
            });
        }

        [Fact]
        public async Task Showing_the_key_shows_what_was_entered_and_hiding_it_again_takes_it_back_into_the_hidden_box()
        {
            await OnEntry(12, async (window, entry, hidden, shown, check) =>
            {
                hidden.Password = "a1b2c3";

                check.IsChecked = true;
                await Settle(window);

                Assert.True(entry.IsShown);
                Assert.True(shown.IsVisible);
                Assert.False(hidden.IsVisible);
                Assert.Equal("a1b2c3", shown.Text);

                // The digits are in one box at a time.
                Assert.Equal(string.Empty, hidden.Password);

                shown.Text = "a1b2c3d4";
                check.IsChecked = false;
                await Settle(window);

                Assert.False(shown.IsVisible);
                Assert.True(hidden.IsVisible);
                Assert.Equal("a1b2c3d4", hidden.Password);
                Assert.Equal(string.Empty, shown.Text);
            });
        }

        // ── The limit ──

        [Theory]
        [InlineData(12)]
        [InlineData(32)]
        [InlineData(48)]
        public async Task The_input_is_limited_to_the_digits_of_the_key_in_both_boxes(int hexLength)
        {
            await OnEntry(hexLength, (_, _, hidden, shown, _) =>
            {
                Assert.Equal(hexLength, hidden.MaxLength);
                Assert.Equal(hexLength, shown.MaxLength);

                return Task.CompletedTask;
            });
        }

        [Fact]
        public async Task A_change_of_the_kind_of_key_moves_the_limit_and_judges_the_entry_again()
        {
            await OnEntry(12, async (window, entry, hidden, shown, _) =>
            {
                hidden.Password = "A1B2C3D4E5F6";
                Assert.True(entry.IsKeyValid);

                entry.HexLength = 32;
                await Settle(window);

                Assert.Equal(32, hidden.MaxLength);
                Assert.Equal(32, shown.MaxLength);
                Assert.False(entry.IsKeyValid);
            });
        }

        // ── The colour ──

        [Theory]
        [InlineData("A1B2C3D4E5F6", true)]
        [InlineData("a1b2c3d4e5f6", true)]
        [InlineData("A1B2C3D4E5F", false)]
        [InlineData("A1B2C3D4E5G6", false)]
        [InlineData("A1B2 C3D4E5F", false)]
        [InlineData("", false)]
        public async Task The_box_turns_green_for_a_key_and_red_for_anything_else_while_it_is_hidden(string digits, bool valid)
        {
            await OnEntry(12, async (window, entry, hidden, _, _) =>
            {
                hidden.Password = digits;
                await Settle(window);

                Assert.Equal(valid, entry.IsKeyValid);
                Assert.Equal(valid ? Colors.White : Colors.Red, BackgroundOf(hidden));
                Assert.Equal(valid ? Colors.Green : Colors.Black, ForegroundOf(hidden));
            });
        }

        [Theory]
        [InlineData("A1B2C3D4E5F6", true)]
        [InlineData("A1B2C3D4E5G6", false)]
        [InlineData("A1B2C3", false)]
        public async Task The_box_that_shows_the_key_is_judged_and_coloured_the_same_way(string digits, bool valid)
        {
            await OnEntry(12, async (window, entry, _, shown, check) =>
            {
                check.IsChecked = true;
                shown.Text = digits;
                await Settle(window);

                Assert.Equal(valid, entry.IsKeyValid);
                Assert.Equal(valid ? Colors.White : Colors.Red, BackgroundOf(shown));
                Assert.Equal(valid ? Colors.Green : Colors.Black, ForegroundOf(shown));
            });
        }

        [Fact]
        public async Task The_judgement_survives_showing_and_hiding_the_key()
        {
            await OnEntry(12, async (window, entry, hidden, _, check) =>
            {
                hidden.Password = "A1B2C3D4E5F6";
                check.IsChecked = true;
                await Settle(window);
                Assert.True(entry.IsKeyValid);

                check.IsChecked = false;
                await Settle(window);
                Assert.True(entry.IsKeyValid);
            });
        }

        // ── What is entered ──

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task The_entered_digits_are_handed_out_as_a_secret_the_caller_owns_in_either_mode(bool shownMode)
        {
            await OnEntry(12, async (window, entry, hidden, shown, check) =>
            {
                check.IsChecked = shownMode;
                if (shownMode)
                {
                    shown.Text = "000102030405";
                }
                else
                {
                    hidden.Password = "000102030405";
                }

                await Settle(window);

                using var entered = entry.GetEntered();
                Span<byte> key = stackalloc byte[6];

                Assert.Equal(6, KeyEntry.Read(entered, 12, key));
                Assert.Equal("000102030405", Convert.ToHexString(key));
            });
        }

        [Fact]
        public async Task Clearing_empties_both_boxes_and_hides_the_key_again()
        {
            await OnEntry(12, async (window, entry, hidden, shown, check) =>
            {
                check.IsChecked = true;
                shown.Text = "A1B2";
                await Settle(window);

                entry.Clear();
                await Settle(window);

                Assert.Equal(string.Empty, shown.Text);
                Assert.Equal(string.Empty, hidden.Password);
                Assert.False(entry.IsShown);
                Assert.True(hidden.IsVisible);
                Assert.False(entry.IsKeyValid);
            });
        }

        // ── Enter and names ──

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Enter_in_either_box_is_reported_and_handled_so_that_it_closes_nothing(bool shownMode)
        {
            await OnEntry(12, async (window, entry, hidden, shown, check) =>
            {
                check.IsChecked = shownMode;
                await Settle(window);
                var raised = 0;
                entry.EnterPressed += (_, _) => raised++;
                UIElement box = shownMode ? shown : hidden;

                var args = new System.Windows.Input.KeyEventArgs(
                    System.Windows.Input.Keyboard.PrimaryDevice,
                    PresentationSource.FromVisual(box),
                    0,
                    System.Windows.Input.Key.Enter)
                {
                    RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent,
                };
                box.RaiseEvent(args);

                Assert.True(args.Handled);
                Assert.Equal(1, raised);
            });
        }

        [Fact]
        public async Task Both_boxes_and_the_check_box_have_the_name_the_window_gave_the_entry()
        {
            await OnEntry(12, (_, _, hidden, shown, check) =>
            {
                Assert.Equal("Key", System.Windows.Automation.AutomationProperties.GetName(hidden));
                Assert.Equal("Key", System.Windows.Automation.AutomationProperties.GetName(shown));
                Assert.Equal("Show key", check.Content);

                return Task.CompletedTask;
            });
        }
    }
}
