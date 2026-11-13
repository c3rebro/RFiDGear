#nullable enable
using RFiDGear.Services.SecureKeys;
using RFiDGear.Tests.SecureKeys;
using RFiDGear.View;
using RFiDGear.ViewModel;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using Xunit;
using static RFiDGear.Tests.RenderedTab;

namespace RFiDGear.Tests
{
    /// <summary>
    /// The key manager of the menu, rendered for real: the stored keys by name with a button to remove each, a name, a kind and a
    /// hidden box for the key that is stored, the status of the supplier, and the way out. Controls are found by their names.
    /// </summary>
    [Collection("WpfBindingTrace")]
    public class StoredKeysViewTests
    {
        private const string Prefix = WindowsCredentialKeyProvider.TargetPrefix;

        private sealed class Setup
        {
            internal FakeCredentialStore Store { get; } = new FakeCredentialStore();

            internal FakeSupplierStatus Supplier { get; } = new FakeSupplierStatus();

            internal ScriptedStoredKeysDialogs Dialogs { get; } = new ScriptedStoredKeysDialogs();

            internal string Culture { get; set; } = "en";

            internal Setup WithKeys(params string[] names)
            {
                foreach (var name in names)
                {
                    Store.Blobs[Prefix + name] = Encoding.Unicode.GetBytes("A1B2C3D4E5F6");
                }

                return this;
            }

            internal StoredKeysViewModel Create() =>
                StoredKeysViewModel.Create(new WindowsCredentialKeyProvider(Store), Supplier, Dialogs, TestText.In(Culture));
        }

        private static async Task OnWindow(Setup setup, Func<StoredKeysView, StoredKeysViewModel, Task> inspect)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var viewModel = setup.Create();
                var window = new StoredKeysView(viewModel) { ShowActivated = false };

                try
                {
                    window.Show();
                    await Settle(window);

                    await inspect(window, viewModel);
                }
                finally
                {
                    if (window.IsVisible)
                    {
                        window.Close();
                    }
                }
            });
        }

        private static async Task Settle(Window window)
        {
            window.UpdateLayout();
            await Flush();
            window.UpdateLayout();
        }

        private static IEnumerable<T> All<T>(DependencyObject root, string name)
            where T : FrameworkElement
        {
            var count = VisualTreeHelper.GetChildrenCount(root);

            for (var index = 0; index < count; index++)
            {
                var child = VisualTreeHelper.GetChild(root, index);

                if (child is T match && match.Name == name)
                {
                    yield return match;
                }

                foreach (var nested in All<T>(child, name))
                {
                    yield return nested;
                }
            }
        }

        private static T Of<T>(DependencyObject root, string name, int row = 0)
            where T : FrameworkElement => All<T>(root, name).ElementAt(row);

        private static bool WrapsAt(ToolTip tip, double width) =>
            tip.Style is { } style && style.Setters.OfType<Setter>().Any(setter => setter.Property == FrameworkElement.MaxWidthProperty && Equals(setter.Value, width));

        // The box that holds the key while it is hidden; the entry around it is the control called KeyBox.
        private static PasswordBox Hidden(DependencyObject window) => Of<PasswordBox>(window, "HiddenBox");

        private static System.Windows.Input.KeyEventArgs PressEnter(UIElement target)
        {
            var args = new System.Windows.Input.KeyEventArgs(
                System.Windows.Input.Keyboard.PrimaryDevice,
                PresentationSource.FromVisual(target),
                0,
                System.Windows.Input.Key.Enter)
            {
                RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent,
            };

            target.RaiseEvent(args);

            return args;
        }

        // ── The list ──

        [Fact]
        public async Task The_stored_keys_are_listed_by_name_each_with_a_button_that_removes_it()
        {
            await OnWindow(new Setup().WithKeys("cards/b", "cards/a"), (window, _) =>
            {
                Assert.Equal(new[] { "cards/a", "cards/b" }, All<TextBlock>(window, "ReferenceText").Select(text => text.Text));
                Assert.Equal(2, All<Button>(window, "RemoveButton").Count());
                Assert.False(Of<TextBlock>(window, "NoKeysText").IsVisible);
                Assert.False(Of<TextBlock>(window, "ListErrorText").IsVisible);
                Assert.Equal("Key manager", window.Title);

                return Task.CompletedTask;
            });
        }

        [Fact]
        public async Task Without_a_key_the_window_says_so()
        {
            await OnWindow(new Setup(), (window, _) =>
            {
                Assert.True(Of<TextBlock>(window, "NoKeysText").IsVisible);
                Assert.Empty(All<Button>(window, "RemoveButton"));

                return Task.CompletedTask;
            });
        }

        [Fact]
        public async Task A_list_that_cannot_be_read_says_why_and_not_that_no_key_is_stored()
        {
            var setup = new Setup();
            setup.Store.EnumerateError = CredentialErrors.AccessDenied;

            await OnWindow(setup, (window, _) =>
            {
                Assert.True(Of<TextBlock>(window, "ListErrorText").IsVisible);
                Assert.Equal("The stored keys could not be listed (error 5).", Of<TextBlock>(window, "ListErrorText").Text);
                Assert.False(Of<TextBlock>(window, "NoKeysText").IsVisible);

                return Task.CompletedTask;
            });
        }

        [Fact]
        public async Task The_remove_button_removes_the_key_after_the_person_agreed_and_the_list_follows()
        {
            var setup = new Setup().WithKeys("cards/a", "cards/b");

            await OnWindow(setup, async (window, _) =>
            {
                Of<Button>(window, "RemoveButton", 0).Command.Execute(null);
                await Settle(window);

                Assert.Equal(new[] { "cards/a" }, setup.Dialogs.Removed);
                Assert.Equal(Prefix + "cards/a", Assert.Single(setup.Store.Deletes));
                Assert.Equal(new[] { "cards/b" }, All<TextBlock>(window, "ReferenceText").Select(text => text.Text));
                Assert.Equal("The key 'cards/a' was removed.", Of<TextBlock>(window, "ResultText").Text);
            });
        }

        // ── Storing a key ──

        [Fact]
        public async Task A_key_that_is_entered_is_stored_under_the_name_the_box_is_cleared_and_the_list_shows_it()
        {
            var setup = new Setup();

            await OnWindow(setup, async (window, _) =>
            {
                Of<TextBox>(window, "NameBox").Text = "cards/main";
                Of<ComboBox>(window, "KindBox").SelectedValue = 12;
                var box = Hidden(window);
                box.Password = "a1b2c3d4e5f6";
                await Settle(window);

                var button = Of<Button>(window, "StoreButton");
                Assert.True(button.IsEnabled);
                button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                await Settle(window);

                Assert.Equal(Encoding.Unicode.GetBytes("A1B2C3D4E5F6"), setup.Store.Blobs[Prefix + "cards/main"]);
                Assert.Equal(string.Empty, box.Password);
                Assert.Equal("The key was stored under the reference.", Of<TextBlock>(window, "ResultText").Text);
                Assert.Equal(new[] { "cards/main" }, All<TextBlock>(window, "ReferenceText").Select(text => text.Text));
            });
        }

        [Fact]
        public async Task A_key_of_the_wrong_length_waits_in_the_box_the_store_button_is_disabled_and_enter_does_nothing()
        {
            var setup = new Setup();

            await OnWindow(setup, async (window, _) =>
            {
                Of<TextBox>(window, "NameBox").Text = "cards/main";
                var box = Hidden(window);
                box.Password = "0102";
                await Settle(window);

                // The box says so with its colour, the button waits, and nothing the person typed is thrown away.
                Assert.False(Of<KeyEntryBox>(window, "KeyBox").IsKeyValid);
                Assert.False(Of<Button>(window, "StoreButton").IsEnabled);

                var args = PressEnter(box);
                await Settle(window);

                Assert.True(args.Handled);
                Assert.Empty(setup.Store.Writes);
                Assert.Equal("0102", box.Password);

                box.Password = "0102030405060708090a0b0c0d0e0f10";
                await Settle(window);

                Assert.True(Of<KeyEntryBox>(window, "KeyBox").IsKeyValid);
                Assert.True(Of<Button>(window, "StoreButton").IsEnabled);
            });
        }

        [Fact]
        public async Task A_key_can_be_shown_while_it_is_entered_and_is_stored_from_the_box_that_shows_it()
        {
            var setup = new Setup();

            await OnWindow(setup, async (window, _) =>
            {
                Of<TextBox>(window, "NameBox").Text = "cards/main";
                Of<ComboBox>(window, "KindBox").SelectedValue = 12;
                Hidden(window).Password = "a1b2c3d4e5f6";
                await Settle(window);

                var check = Of<CheckBox>(window, "ShowCheck");
                Assert.Equal("Show key", check.Content);
                check.IsChecked = true;
                await Settle(window);

                var shown = Of<TextBox>(window, "ShownBox");
                Assert.True(shown.IsVisible);
                Assert.Equal("a1b2c3d4e5f6", shown.Text);
                Assert.True(Of<Button>(window, "StoreButton").IsEnabled);

                Of<Button>(window, "StoreButton").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                await Settle(window);

                // Stored, and the box is empty and hidden again.
                Assert.Equal(Encoding.Unicode.GetBytes("A1B2C3D4E5F6"), setup.Store.Blobs[Prefix + "cards/main"]);
                Assert.Equal(string.Empty, shown.Text);
                Assert.False(shown.IsVisible);
                Assert.False(Of<KeyEntryBox>(window, "KeyBox").IsShown);
            });
        }

        [Fact]
        public async Task The_box_takes_no_more_characters_than_the_kind_of_key_has_digits()
        {
            await OnWindow(new Setup(), async (window, _) =>
            {
                var kinds = Of<ComboBox>(window, "KindBox");

                foreach (var hexLength in new[] { 12, 32, 48 })
                {
                    kinds.SelectedValue = hexLength;
                    await Settle(window);

                    Assert.Equal(hexLength, Hidden(window).MaxLength);
                    Assert.Equal(hexLength, Of<TextBox>(window, "ShownBox").MaxLength);
                }
            });
        }

        [Fact]
        public async Task The_name_of_a_new_key_is_not_coloured_while_it_is_typed_but_the_store_button_still_waits_for_a_valid_one()
        {
            await OnWindow(new Setup(), async (window, _) =>
            {
                var name = Of<TextBox>(window, "NameBox");
                var plain = name.Background;

                foreach (var typed in new[] { string.Empty, "not a name", "cards/main" })
                {
                    name.Text = typed;
                    await Settle(window);

                    Assert.Equal(plain, name.Background);
                    Assert.NotEqual(Brushes.Green, name.Foreground);
                }

                name.Text = "not a name";
                await Settle(window);
                Assert.False(Of<Button>(window, "StoreButton").IsEnabled);
            });
        }

        [Fact]
        public async Task Enter_in_the_box_of_the_key_stores_it_and_never_closes_the_window()
        {
            var setup = new Setup();

            await OnWindow(setup, async (window, _) =>
            {
                Of<TextBox>(window, "NameBox").Text = "cards/main";
                Of<ComboBox>(window, "KindBox").SelectedValue = 12;
                var box = Hidden(window);
                box.Password = "A1B2C3D4E5F6";

                var args = PressEnter(box);
                await Settle(window);

                Assert.True(args.Handled);
                Assert.True(window.IsVisible);
                Assert.Equal(Encoding.Unicode.GetBytes("A1B2C3D4E5F6"), setup.Store.Blobs[Prefix + "cards/main"]);
                Assert.Equal(string.Empty, box.Password);
            });
        }

        [Fact]
        public async Task Enter_without_a_valid_name_does_nothing_and_keeps_the_digits_and_the_window()
        {
            var setup = new Setup();

            await OnWindow(setup, async (window, _) =>
            {
                var box = Hidden(window);
                box.Password = "0001";

                var args = PressEnter(box);
                await Settle(window);

                Assert.True(args.Handled);
                Assert.True(window.IsVisible);
                Assert.Empty(setup.Store.Writes);
                Assert.Equal("0001", box.Password);
            });
        }

        [Fact]
        public async Task A_replacement_that_the_person_declines_keeps_the_digits_in_the_box()
        {
            var setup = new Setup().WithKeys("cards/main");
            setup.Dialogs.AgreeToReplace = false;

            await OnWindow(setup, async (window, _) =>
            {
                Of<TextBox>(window, "NameBox").Text = "cards/main";
                Of<ComboBox>(window, "KindBox").SelectedValue = 12;
                var box = Hidden(window);
                box.Password = "000102030405";

                Of<Button>(window, "StoreButton").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                await Settle(window);

                Assert.Equal(new[] { "cards/main" }, setup.Dialogs.Replaced);
                Assert.Empty(setup.Store.Writes);
                Assert.Equal("000102030405", box.Password);
            });
        }

        [Fact]
        public async Task A_key_that_was_entered_and_not_stored_does_not_stay_in_the_window_when_it_closes()
        {
            await OnWindow(new Setup(), async (window, _) =>
            {
                var box = Hidden(window);
                box.Password = "0001";
                Of<CheckBox>(window, "ShowCheck").IsChecked = true;
                Of<TextBox>(window, "ShownBox").Text = "0002";

                window.Close();
                await Flush();

                Assert.Equal(string.Empty, box.Password);
                Assert.Equal(string.Empty, Of<TextBox>(window, "ShownBox").Text);
            });
        }

        [Fact]
        public async Task The_store_button_waits_for_a_valid_name_and_for_a_key()
        {
            await OnWindow(new Setup(), async (window, _) =>
            {
                var button = Of<Button>(window, "StoreButton");
                Assert.False(button.IsEnabled);

                // A name alone is not enough: the box that holds the key says that what is in it is not one.
                Of<TextBox>(window, "NameBox").Text = "cards/main";
                await Settle(window);
                Assert.False(button.IsEnabled);

                Hidden(window).Password = "000102030405060708090a0b0c0d0e0f";
                await Settle(window);
                Assert.True(button.IsEnabled);

                // And a key alone is not enough.
                Of<TextBox>(window, "NameBox").Text = "not a name";
                await Settle(window);
                Assert.False(button.IsEnabled);
            });
        }

        [Fact]
        public async Task The_close_button_closes_the_window_and_is_not_the_default_button_so_that_enter_closes_nothing()
        {
            await OnWindow(new Setup(), (window, _) =>
            {
                var close = Of<Button>(window, "CloseButton");
                var closed = false;
                window.Closed += (_, _) => closed = true;

                Assert.False(close.IsDefault);
                Assert.True(close.IsCancel);
                close.Command.Execute(null);

                Assert.True(closed);

                return Task.CompletedTask;
            });
        }

        // ── Tooltips and names ──

        [Fact]
        public async Task Every_tooltip_of_the_window_wraps_and_stays_long_enough_to_be_read()
        {
            await OnWindow(new Setup().WithKeys("cards/a"), (window, _) =>
            {
                var style = (Style)window.FindResource("KeySlotToolTipStyle");

                foreach (var owner in new FrameworkElement[]
                {
                    Of<TextBlock>(window, "ReferenceText"), Of<Button>(window, "RemoveButton"), Of<TextBox>(window, "NameBox"),
                    Of<ComboBox>(window, "KindBox"), Of<Button>(window, "StoreButton"), Of<CheckBox>(window, "ShowCheck"),
                })
                {
                    var tip = Assert.IsType<ToolTip>(owner.ToolTip);

                    // The box for the key has its own copy of the style, which wraps in the same way.
                    Assert.True(ReferenceEquals(style, tip.Style) || WrapsAt(tip, 380), owner.Name + " has a tooltip that does not wrap");
                    Assert.True(ToolTipService.GetShowDuration(owner) >= 20000, owner.Name + " shows its tooltip for " + ToolTipService.GetShowDuration(owner) + " ms");
                }

                Assert.True(ToolTipService.GetShowOnDisabled(Of<Button>(window, "StoreButton")));

                return Task.CompletedTask;
            });
        }

        [Fact]
        public async Task The_buttons_that_remove_a_key_are_told_apart_by_their_accessible_names_which_hold_no_key()
        {
            await OnWindow(new Setup().WithKeys("cards/a", "cards/b"), (window, _) =>
            {
                var names = All<Button>(window, "RemoveButton").Select(System.Windows.Automation.AutomationProperties.GetName).ToList();

                Assert.Equal(new[] { "Remove: cards/a", "Remove: cards/b" }, names);

                return Task.CompletedTask;
            });
        }

        // ── The supplier ──

        [Fact]
        public async Task The_status_of_the_supplier_is_always_shown_and_follows_it()
        {
            var setup = new Setup();

            await OnWindow(setup, async (window, viewModel) =>
            {
                var status = Of<TextBlock>(window, "SupplierStatus");
                Assert.True(status.IsVisible);
                Assert.Contains("not running", status.Text);

                setup.Supplier.Current = KeySupplierAvailability.Connected;
                viewModel.RefreshSupplierStatus();
                await Settle(window);

                Assert.Equal("A connected program supplies keys.", status.Text);
            });
        }

        // ── The menu ──

        [Fact]
        public async Task The_command_of_the_menu_item_shows_the_key_manager_once()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var folder = Directory.CreateTempSubdirectory("rfidgear-keymanager-");

                try
                {
                    var (viewModel, _) = MainWindowViewModelTests.CreateUninitializedViewModel(folder.FullName);
                    var launcher = new RecordingStoredKeysLauncher();
                    viewModel.StoredKeysLauncher = launcher;

                    viewModel.OpenKeyManagerCommand.Execute(null);

                    Assert.Equal(1, launcher.Shown);
                }
                finally
                {
                    folder.Delete(recursive: true);
                }
            });
        }

        private sealed class RecordingStoredKeysLauncher : IStoredKeysLauncher
        {
            public int Shown { get; private set; }

            public Window? LastOwner { get; private set; }

            public void Show(Window? owner)
            {
                Shown++;
                LastOwner = owner;
            }
        }

        // ── Size ──

        [Theory]
        [InlineData("en")]
        [InlineData("de")]
        public async Task The_window_fits_its_content_in_both_languages_with_many_keys_and_the_buttons_stay_in_it(string culture)
        {
            var names = Enumerable.Range(0, 40).Select(index => "cards/a-rather-long-name-of-a-key-that-is-number-" + index).ToArray();

            await OnWindow(new Setup { Culture = culture }.WithKeys(names), async (window, _) =>
            {
                Of<TextBox>(window, "NameBox").Text = "cards/main";
                await Settle(window);

                Assert.True(window.ActualWidth >= 460);
                Assert.True(window.ActualHeight <= 780, "the window is " + window.ActualHeight + " px high");

                foreach (var button in All<Button>(window, "RemoveButton").Take(5).Concat(new[] { Of<Button>(window, "CloseButton"), Of<Button>(window, "StoreButton") }))
                {
                    var right = button.TransformToAncestor(window).Transform(new Point(button.ActualWidth, 0)).X;
                    Assert.True(right <= window.ActualWidth + 0.5, button.Name + " is outside the window");
                }
            });
        }

        // ── Pictures, when asked ──

        [Fact]
        public async Task The_window_is_rendered_to_pictures_when_a_folder_is_given()
        {
            // For a person to look at, like the API snapshots that are updated on request: nothing happens without the variable.
            var folder = Environment.GetEnvironmentVariable("RFIDGEAR_RENDER_DIR");

            if (string.IsNullOrWhiteSpace(folder))
            {
                return;
            }

            Directory.CreateDirectory(folder);

            foreach (var culture in new[] { "en", "de" })
            {
                var setup = new Setup { Culture = culture }.WithKeys("cards/application-main", "orders/4711", "migrated/0123456789abcdef");
                setup.Supplier.Current = KeySupplierAvailability.NoSupplier;

                await OnWindow(setup, async (window, viewModel) =>
                {
                    Of<TextBox>(window, "NameBox").Text = "cards/new";
                    viewModel.RefreshSupplierStatus();
                    await Settle(window);

                    var root = (FrameworkElement)VisualTreeHelper.GetChild(window, 0);
                    var bitmap = new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth), (int)Math.Ceiling(root.ActualHeight), 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(root);
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));

                    using var file = File.Create(Path.Combine(folder, "stored-keys-" + culture + ".png"));
                    encoder.Save(file);
                });
            }
        }
    }
}
