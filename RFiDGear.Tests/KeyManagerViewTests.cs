#nullable enable
using RFiDGear.Services.SecureKeys;
using RFiDGear.Tests.SecureKeys;
using RFiDGear.View;

using System;
using System.Collections.Generic;
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
    /// The key manager window of a task, rendered for real: a row for each slot, a typed key or a reference, the key store with a
    /// tooltip for each entry, the keys that are stored to choose from by name, a group of its own in which a new key is created
    /// (a box in which a key is entered hidden or shown, checked while it is typed, and cleared after it was stored), the status of
    /// the supplier, and the way out. Controls are found by their names in the template of a row.
    /// </summary>
    [Collection("WpfBindingTrace")]
    public class KeyManagerViewTests
    {
        private const string Prefix = WindowsCredentialKeyProvider.TargetPrefix;

        private static readonly SecureKeyProviderInfo CredentialManager = new SecureKeyProviderInfo(WindowsCredentialKeyProvider.ProviderId, "Windows Credential Manager");
        private static readonly SecureKeyProviderInfo ExternalApplication = new SecureKeyProviderInfo(BridgeKeyProvider.ProviderId, "Connected external application");

        private sealed class Setup
        {
            internal FakeKeySlotHost Host { get; } = new FakeKeySlotHost();

            internal FakeCredentialStore Store { get; } = new FakeCredentialStore();

            internal FakeSupplierStatus Supplier { get; } = new FakeSupplierStatus();

            internal string Culture { get; set; } = "en";

            internal Setup WithKeys(params string[] names)
            {
                foreach (var name in names)
                {
                    Store.Blobs[Prefix + name] = Encoding.Unicode.GetBytes("A1B2C3D4E5F6");
                }

                return this;
            }

            internal KeyManagerViewModel Create(params string[] slots) => KeyManagerViewModel.Create(
                Host,
                slots,
                new[] { CredentialManager, ExternalApplication },
                new ScriptedKeyDialogs(),
                new WindowsCredentialKeyProvider(Store),
                Supplier,
                TestText.In(Culture));
        }

        private static async Task OnWindow(Setup setup, string[] slots, Func<KeyManagerView, KeyManagerViewModel, Task> inspect)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var viewModel = setup.Create(slots);
                var window = new KeyManagerView(viewModel) { ShowActivated = false };

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

        // ── Finding things ──

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

        // The box that holds the key of a row while it is hidden; the entry around it is the control called KeyBox.
        private static PasswordBox Hidden(DependencyObject window, int row = 0) => Of<PasswordBox>(window, "HiddenBox", row);

        private static TextBox Shown(DependencyObject window, int row = 0) => Of<TextBox>(window, "ShownBox", row);

        private static async Task Toggle(KeyManagerView window, int row)
        {
            Of<Button>(window, "ModeButton", row).Command.Execute(null);
            await Settle(window);
        }

        private static async Task Click(KeyManagerView window, string button, int row = 0)
        {
            Of<Button>(window, button, row).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            await Settle(window);
        }

        private static bool WrapsAt(ToolTip tip, double width) =>
            tip.Style is { } style && style.Setters.OfType<Setter>().Any(setter => setter.Property == FrameworkElement.MaxWidthProperty && Equals(setter.Value, width));

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

        private const string Key32 = "000102030405060708090a0b0c0d0e0f";

        // ── The rows ──

        [Fact]
        public async Task A_slot_with_a_typed_key_shows_its_name_and_a_sentence_and_no_reference_controls()
        {
            await OnWindow(new Setup(), new[] { SecureKeySlots.DesfireAppCurrent, SecureKeySlots.DesfireAppNew }, (window, _) =>
            {
                Assert.Equal(2, All<StackPanel>(window, "ReferencePanel").Count());
                Assert.All(All<StackPanel>(window, "ReferencePanel"), panel => Assert.False(panel.IsVisible));
                Assert.All(All<TextBlock>(window, "TypedKeyText"), text => Assert.True(text.IsVisible));
                Assert.Equal("Key manager", window.Title);
                Assert.False(Of<TextBlock>(window, "NoSlotsText").IsVisible);
                Assert.False(Of<TextBlock>(window, "SupplierStatus").IsVisible);

                return Task.CompletedTask;
            });
        }

        [Fact]
        public async Task A_section_without_a_key_says_so_and_has_no_rows()
        {
            await OnWindow(new Setup(), Array.Empty<string>(), (window, _) =>
            {
                Assert.True(Of<TextBlock>(window, "NoSlotsText").IsVisible);
                Assert.Empty(All<Button>(window, "ModeButton"));

                return Task.CompletedTask;
            });
        }

        [Fact]
        public async Task Using_a_reference_with_the_credential_manager_offers_the_stored_keys_and_a_group_in_which_a_new_key_is_created()
        {
            var setup = new Setup();

            await OnWindow(setup, new[] { SecureKeySlots.DesfireRead }, async (window, _) =>
            {
                await Toggle(window, 0);

                Assert.True(setup.Host.IsKeyBound(SecureKeySlots.DesfireRead));
                Assert.True(Of<StackPanel>(window, "ReferencePanel").IsVisible);
                Assert.False(Of<TextBlock>(window, "TypedKeyText").IsVisible);

                var providers = Of<ComboBox>(window, "ProviderBox");
                Assert.Equal(2, providers.Items.Count);
                Assert.Equal(WindowsCredentialKeyProvider.ProviderId, providers.SelectedValue);

                // The key store is a list of the keys that exist, and a new key is another thing, in a group of its own.
                Assert.True(Of<StackPanel>(window, "StoredKeyPanel").IsVisible);
                Assert.True(Of<ComboBox>(window, "StoredKeyBox").IsVisible);
                Assert.False(Of<Grid>(window, "FreeReferencePanel").IsVisible);
                var group = Of<GroupBox>(window, "NewKeyGroup");
                Assert.True(group.IsVisible);
                Assert.Equal("Create a new key", group.Header);

                // The box takes no more characters than the key has digits, and the name of a new key has the limit of a name.
                Assert.Equal(32, Hidden(window).MaxLength);
                Assert.Equal(32, Shown(window).MaxLength);
                Assert.Equal(SecureKeySyntax.MaxReferenceLength, Of<TextBox>(window, "NewNameBox").MaxLength);
                Assert.True(Of<CheckBox>(window, "ShowCheck").IsVisible);
            });
        }

        [Fact]
        public async Task The_stored_keys_are_offered_by_name_and_choosing_one_binds_the_slot_to_it()
        {
            var setup = new Setup().WithKeys("cards/b", "cards/a");

            await OnWindow(setup, new[] { SecureKeySlots.DesfireRead }, async (window, _) =>
            {
                await Toggle(window, 0);

                var list = Of<ComboBox>(window, "StoredKeyBox");
                Assert.Equal(new[] { "cards/a", "cards/b" }, list.Items.Cast<StoredKeyChoice>().Select(choice => choice.Reference));
                Assert.False(Of<TextBlock>(window, "NoStoredKeysText").IsVisible);

                list.SelectedValue = "cards/b";
                await Settle(window);

                Assert.Equal($"bind {SecureKeySlots.DesfireRead} {WindowsCredentialKeyProvider.ProviderId} 'cards/b'", setup.Host.Calls.Last());
                Assert.Equal("cards/b", setup.Host.GetKeyBinding(SecureKeySlots.DesfireRead)?.Reference);
                Assert.Empty(setup.Store.Reads);
            });
        }

        [Fact]
        public async Task Without_a_stored_key_the_window_says_to_create_one()
        {
            await OnWindow(new Setup(), new[] { SecureKeySlots.DesfireRead }, async (window, _) =>
            {
                await Toggle(window, 0);

                Assert.Empty(Of<ComboBox>(window, "StoredKeyBox").Items);
                Assert.True(Of<TextBlock>(window, "NoStoredKeysText").IsVisible);
                Assert.Equal("No key is stored yet. Create one below.", Of<TextBlock>(window, "NoStoredKeysText").Text);
            });
        }

        [Fact]
        public async Task A_list_that_cannot_be_read_says_why_and_not_that_no_key_is_stored()
        {
            var setup = new Setup();
            setup.Store.EnumerateError = CredentialErrors.AccessDenied;

            await OnWindow(setup, new[] { SecureKeySlots.DesfireRead }, async (window, _) =>
            {
                await Toggle(window, 0);

                Assert.True(Of<TextBlock>(window, "StoredKeysErrorText").IsVisible);
                Assert.Equal("The stored keys could not be listed (error 5).", Of<TextBlock>(window, "StoredKeysErrorText").Text);
                Assert.False(Of<TextBlock>(window, "NoStoredKeysText").IsVisible);
            });
        }

        [Fact]
        public async Task A_slot_that_names_a_key_that_is_not_stored_shows_the_name_in_the_list_and_says_so()
        {
            var setup = new Setup().WithKeys("cards/a");
            setup.Host.BindKeySlot(SecureKeySlots.DesfireRead, WindowsCredentialKeyProvider.ProviderId, "gone/key");
            setup.Host.Calls.Clear();

            await OnWindow(setup, new[] { SecureKeySlots.DesfireRead }, (window, _) =>
            {
                var list = Of<ComboBox>(window, "StoredKeyBox");

                Assert.Equal("gone/key", list.SelectedValue);
                Assert.Equal("gone/key (not stored)", Assert.IsType<StoredKeyChoice>(list.SelectedItem).Text);

                // Showing the window did not change what the slot names.
                Assert.Empty(setup.Host.Calls);

                return Task.CompletedTask;
            });
        }

        [Fact]
        public async Task Another_key_store_takes_the_name_as_text_and_has_no_list_and_no_group_for_a_new_key()
        {
            await OnWindow(new Setup(), new[] { SecureKeySlots.DesfireRead }, async (window, _) =>
            {
                await Toggle(window, 0);
                Of<ComboBox>(window, "ProviderBox").SelectedValue = BridgeKeyProvider.ProviderId;
                await Settle(window);

                Assert.False(Of<StackPanel>(window, "StoredKeyPanel").IsVisible);
                Assert.True(Of<Grid>(window, "FreeReferencePanel").IsVisible);
                Assert.Equal(SecureKeySyntax.MaxReferenceLength, Of<TextBox>(window, "ReferenceBox").MaxLength);
            });
        }

        // ── Tooltips ──

        [Fact]
        public async Task Every_tooltip_of_the_window_wraps_and_each_key_store_has_one_of_its_own()
        {
            await OnWindow(new Setup(), new[] { SecureKeySlots.DesfireRead }, async (window, _) =>
            {
                await Toggle(window, 0);
                var style = (Style)window.FindResource("KeySlotToolTipStyle");

                foreach (var owner in new FrameworkElement[]
                {
                    Of<Button>(window, "ModeButton"), Of<ComboBox>(window, "ProviderBox"), Of<ComboBox>(window, "StoredKeyBox"),
                    Of<TextBox>(window, "ReferenceBox"), Of<TextBox>(window, "NewNameBox"), Of<Button>(window, "StoreButton"),
                    Of<CheckBox>(window, "ShowCheck"),
                })
                {
                    var tip = Assert.IsType<ToolTip>(owner.ToolTip);

                    // The box for the key has its own copy of the style, which wraps in the same way.
                    Assert.True(ReferenceEquals(style, tip.Style) || WrapsAt(tip, 380), owner.Name + " has a tooltip that does not wrap");

                    // The duration is read from the owner of a tooltip: asked here for each of them, not for the window.
                    Assert.True(ToolTipService.GetShowDuration(owner) >= 20000, owner.Name + " shows its tooltip for " + ToolTipService.GetShowDuration(owner) + " ms");
                }

                // The key can only be stored under a valid name and as a key; the button says why while it is disabled.
                Assert.True(ToolTipService.GetShowOnDisabled(Of<Button>(window, "StoreButton")));

                // The entries of the list: instantiate the template of an item, as the list does for each one.
                var providers = Of<ComboBox>(window, "ProviderBox");
                var item = Assert.IsType<Grid>(providers.ItemTemplate.LoadContent());
                item.DataContext = providers.Items[0];
                var itemTip = Assert.IsType<ToolTip>(item.ToolTip);

                Assert.Same(style, itemTip.Style);

                // Its text is the tooltip of the entry the template is made for, not one text for all of them.
                Assert.Equal(nameof(KeySlotProviderItem.ToolTip), System.Windows.Data.BindingOperations.GetBinding(itemTip, ContentControl.ContentProperty)?.Path.Path);
            });
        }

        // ── Creating a new key ──

        [Fact]
        public async Task A_new_key_is_stored_under_the_new_name_the_slot_uses_it_the_list_shows_it_and_the_box_is_cleared()
        {
            var setup = new Setup().WithKeys("cards/other");

            await OnWindow(setup, new[] { SecureKeySlots.DesfireRead }, async (window, _) =>
            {
                await Toggle(window, 0);
                Of<TextBox>(window, "NewNameBox").Text = "cards/main";
                var box = Hidden(window);
                box.Password = Key32;
                await Settle(window);

                var button = Of<Button>(window, "StoreButton");
                Assert.True(button.IsEnabled);
                await Click(window, "StoreButton");

                Assert.Equal(
                    Encoding.Unicode.GetBytes("000102030405060708090A0B0C0D0E0F"),
                    setup.Store.Blobs[Prefix + "cards/main"]);
                Assert.Equal(string.Empty, box.Password);

                // The slot names the key that was made for it, and the list has it, selected.
                var list = Of<ComboBox>(window, "StoredKeyBox");
                Assert.Equal("cards/main", setup.Host.GetKeyBinding(SecureKeySlots.DesfireRead)?.Reference);
                Assert.Equal(new[] { "cards/main", "cards/other" }, list.Items.Cast<StoredKeyChoice>().Select(choice => choice.Reference));
                Assert.Equal("cards/main", list.SelectedValue);
                Assert.Equal(string.Empty, Of<TextBox>(window, "NewNameBox").Text);

                var result = Of<TextBlock>(window, "ResultText");
                Assert.True(result.IsVisible);
                Assert.Equal("The key was stored under the reference.", result.Text);
            });
        }

        [Fact]
        public async Task A_key_can_be_shown_while_it_is_entered_and_is_stored_from_the_box_that_shows_it()
        {
            var setup = new Setup();

            await OnWindow(setup, new[] { SecureKeySlots.DesfireRead }, async (window, _) =>
            {
                await Toggle(window, 0);
                Of<TextBox>(window, "NewNameBox").Text = "cards/main";
                Hidden(window).Password = Key32;
                await Settle(window);

                Of<CheckBox>(window, "ShowCheck").IsChecked = true;
                await Settle(window);

                Assert.True(Shown(window).IsVisible);
                Assert.Equal(Key32, Shown(window).Text);

                await Click(window, "StoreButton");

                Assert.Equal(Encoding.Unicode.GetBytes("000102030405060708090A0B0C0D0E0F"), setup.Store.Blobs[Prefix + "cards/main"]);
                Assert.Equal(string.Empty, Shown(window).Text);
                Assert.False(Shown(window).IsVisible);
            });
        }

        [Fact]
        public async Task The_box_turns_green_for_a_key_and_red_for_anything_else_and_the_store_button_waits_for_a_key()
        {
            await OnWindow(new Setup(), new[] { SecureKeySlots.DesfireRead }, async (window, _) =>
            {
                await Toggle(window, 0);
                Of<TextBox>(window, "NewNameBox").Text = "cards/main";
                var box = Hidden(window);
                var button = Of<Button>(window, "StoreButton");

                box.Password = "0102";
                await Settle(window);
                Assert.False(Of<KeyEntryBox>(window, "KeyBox").IsKeyValid);
                Assert.Equal(Colors.Red, ((SolidColorBrush)box.Background).Color);
                Assert.False(button.IsEnabled);

                box.Password = Key32;
                await Settle(window);
                Assert.True(Of<KeyEntryBox>(window, "KeyBox").IsKeyValid);
                Assert.Equal(Colors.White, ((SolidColorBrush)box.Background).Color);
                Assert.Equal(Colors.Green, ((SolidColorBrush)box.Foreground).Color);
                Assert.True(button.IsEnabled);
            });
        }

        [Fact]
        public async Task The_name_of_a_new_key_is_not_coloured_while_it_is_typed_but_the_store_button_still_waits_for_a_valid_one()
        {
            await OnWindow(new Setup(), new[] { SecureKeySlots.DesfireRead }, async (window, _) =>
            {
                await Toggle(window, 0);
                var name = Of<TextBox>(window, "NewNameBox");
                var plain = name.Background;
                Hidden(window).Password = Key32;

                foreach (var typed in new[] { string.Empty, "not a name", "cards/main" })
                {
                    name.Text = typed;
                    await Settle(window);

                    Assert.Equal(plain, name.Background);
                    Assert.NotEqual(Brushes.Green, name.Foreground);
                    Assert.Equal(typed == "cards/main", Of<Button>(window, "StoreButton").IsEnabled);
                }
            });
        }

        [Fact]
        public async Task A_name_that_is_stored_already_is_not_written_to_the_window_points_to_the_list_and_the_digits_stay()
        {
            var setup = new Setup().WithKeys("cards/main");

            await OnWindow(setup, new[] { SecureKeySlots.DesfireRead }, async (window, _) =>
            {
                await Toggle(window, 0);
                Of<TextBox>(window, "NewNameBox").Text = "cards/main";
                var box = Hidden(window);
                box.Password = Key32;
                await Settle(window);

                await Click(window, "StoreButton");

                Assert.Empty(setup.Store.Writes);
                Assert.Equal(Key32, box.Password);
                Assert.True(Of<TextBlock>(window, "ResultText").IsVisible);
                Assert.Contains("stored already", Of<TextBlock>(window, "ResultText").Text);
            });
        }

        [Fact]
        public async Task Enter_in_the_box_of_the_key_stores_it_and_never_closes_the_window()
        {
            var setup = new Setup();

            await OnWindow(setup, new[] { SecureKeySlots.DesfireRead }, async (window, _) =>
            {
                await Toggle(window, 0);
                Of<TextBox>(window, "NewNameBox").Text = "cards/main";
                var box = Hidden(window);
                box.Password = Key32;

                var args = PressEnter(box);
                await Settle(window);

                Assert.True(args.Handled);
                Assert.True(window.IsVisible);
                Assert.Equal(
                    Encoding.Unicode.GetBytes("000102030405060708090A0B0C0D0E0F"),
                    setup.Store.Blobs[Prefix + "cards/main"]);
                Assert.Equal(string.Empty, box.Password);
            });
        }

        [Fact]
        public async Task Enter_when_no_key_can_be_stored_does_nothing_and_keeps_the_digits_and_the_window()
        {
            var setup = new Setup();

            await OnWindow(setup, new[] { SecureKeySlots.DesfireRead }, async (window, _) =>
            {
                await Toggle(window, 0);
                var box = Hidden(window);
                box.Password = "0001";

                // No name yet and not a key: nothing can be stored. The digits stay where the person can still finish them.
                var args = PressEnter(box);
                await Settle(window);

                Assert.True(args.Handled);
                Assert.True(window.IsVisible);
                Assert.Empty(setup.Store.Writes);
                Assert.Equal("0001", box.Password);
            });
        }

        [Fact]
        public async Task A_key_that_was_entered_and_not_stored_does_not_stay_in_the_window_when_it_closes()
        {
            // Close, Escape and the title bar all end in the same place; a row that was switched back to a typed key is collapsed
            // and has its boxes still, with whatever was typed into them.
            await OnWindow(new Setup(), new[] { SecureKeySlots.DesfireRead, SecureKeySlots.DesfireWrite }, async (window, _) =>
            {
                await Toggle(window, 0);
                await Toggle(window, 1);
                var hidden = Hidden(window, 0);
                var collapsed = Hidden(window, 1);
                hidden.Password = "0001";
                collapsed.Password = "0002";
                Of<CheckBox>(window, "ShowCheck", 1).IsChecked = true;
                Shown(window, 1).Text = "0003";
                await Toggle(window, 1);
                Assert.False(Shown(window, 1).IsVisible);

                window.Close();
                await Flush();

                Assert.Equal(string.Empty, hidden.Password);
                Assert.Equal(string.Empty, collapsed.Password);
                Assert.Equal(string.Empty, Shown(window, 1).Text);
            });
        }

        [Fact]
        public async Task The_close_button_is_not_the_default_button_so_that_enter_closes_nothing()
        {
            await OnWindow(new Setup(), new[] { SecureKeySlots.DesfireRead }, (window, _) =>
            {
                var close = Of<Button>(window, "CloseButton");

                Assert.False(close.IsDefault);
                Assert.True(close.IsCancel);

                return Task.CompletedTask;
            });
        }

        [Fact]
        public async Task Each_control_of_a_row_has_an_accessible_name_that_names_its_slot_and_the_key_is_not_in_it()
        {
            await OnWindow(new Setup(), new[] { SecureKeySlots.DesfireAppCurrent, SecureKeySlots.DesfireAppNew }, async (window, _) =>
            {
                await Toggle(window, 0);
                await Toggle(window, 1);
                Hidden(window, 0).Password = "A1B2C3";

                foreach (var (row, slotName) in new[] { (0, "Application key, current"), (1, "Application key, new") })
                {
                    foreach (var control in new FrameworkElement[]
                    {
                        Of<Button>(window, "ModeButton", row), Of<ComboBox>(window, "ProviderBox", row), Of<ComboBox>(window, "StoredKeyBox", row),
                        Of<TextBox>(window, "ReferenceBox", row), Of<TextBox>(window, "NewNameBox", row), Hidden(window, row), Shown(window, row),
                        Of<Button>(window, "StoreButton", row),
                    })
                    {
                        var accessibleName = System.Windows.Automation.AutomationProperties.GetName(control);

                        Assert.StartsWith(slotName + ": ", accessibleName);
                        Assert.DoesNotContain("A1B2C3", accessibleName);
                    }
                }

                // The same control in two rows is told apart by its name.
                Assert.NotEqual(
                    System.Windows.Automation.AutomationProperties.GetName(Hidden(window, 0)),
                    System.Windows.Automation.AutomationProperties.GetName(Hidden(window, 1)));
            });
        }

        // ── The external application ──

        [Fact]
        public async Task The_status_of_the_supplier_appears_with_the_external_application_and_follows_it()
        {
            var setup = new Setup();

            await OnWindow(setup, new[] { SecureKeySlots.DesfireRead }, async (window, viewModel) =>
            {
                await Toggle(window, 0);
                Assert.False(Of<TextBlock>(window, "SupplierStatus").IsVisible);

                Of<ComboBox>(window, "ProviderBox").SelectedValue = BridgeKeyProvider.ProviderId;
                await Settle(window);

                var status = Of<TextBlock>(window, "SupplierStatus");
                Assert.True(status.IsVisible);
                Assert.Contains("not running", status.Text);
                Assert.False(Of<StackPanel>(window, "StoredKeyPanel").IsVisible);

                setup.Supplier.Current = KeySupplierAvailability.Connected;
                viewModel.RefreshSupplierStatus();
                await Settle(window);

                Assert.Equal("A connected program supplies keys.", status.Text);
            });
        }

        // ── The way out ──

        [Fact]
        public async Task The_close_button_closes_the_window()
        {
            await OnWindow(new Setup(), new[] { SecureKeySlots.DesfireRead }, (window, _) =>
            {
                var closed = false;
                window.Closed += (_, _) => closed = true;

                var button = Of<Button>(window, "CloseButton");
                Assert.True(button.IsCancel);
                button.Command.Execute(null);

                Assert.True(closed);

                return Task.CompletedTask;
            });
        }

        [Theory]
        [InlineData("en")]
        [InlineData("de")]
        public async Task The_window_fits_its_content_in_both_languages_without_the_buttons_leaving_it(string culture)
        {
            var setup = new Setup { Culture = culture }.WithKeys("cards/a-rather-long-name-of-a-key", "cards/b");

            await OnWindow(setup, SecureKeySlots.Classic.Take(4).ToArray(), async (window, _) =>
            {
                await Toggle(window, 0);
                await Toggle(window, 1);

                Assert.True(window.ActualWidth >= 460);
                Assert.True(window.ActualHeight <= 780);

                foreach (var button in new[] { Of<Button>(window, "CloseButton"), Of<Button>(window, "StoreButton", 0), Of<Button>(window, "StoreButton", 1) })
                {
                    var position = button.TransformToAncestor(window).Transform(new Point(0, 0));
                    Assert.True(position.X + button.ActualWidth <= window.ActualWidth + 0.5, button.Name + " is outside the window");
                }

                var check = Of<CheckBox>(window, "ShowCheck", 0);
                Assert.True(check.TransformToAncestor(window).Transform(new Point(check.ActualWidth, 0)).X <= window.ActualWidth + 0.5, "the check box is outside the window");
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

            System.IO.Directory.CreateDirectory(folder);

            foreach (var culture in new[] { "en", "de" })
            {
                var setup = new Setup { Culture = culture }.WithKeys("cards/application-main", "orders/4711", "migrated/0123456789abcdef");
                setup.Host.EmbeddedKeys[SecureKeySlots.DesfirePiccCurrent] = "0000";

                await OnWindow(setup, new[] { SecureKeySlots.DesfirePiccCurrent, SecureKeySlots.DesfireAppCurrent, SecureKeySlots.DesfireAppNew }, async (window, viewModel) =>
                {
                    // The plain controls of a row exist for every row, whether it is shown or not, so the index is the row. The group in
                    // which a key is created is only built for a row that was shown: the first one that was is index 0.
                    await Toggle(window, 1);
                    Of<ComboBox>(window, "StoredKeyBox", 1).SelectedValue = "cards/application-main";
                    Of<TextBox>(window, "NewNameBox", 0).Text = "cards/new";
                    Hidden(window, 0).Password = "0001020304050607";
                    await Toggle(window, 2);
                    Of<ComboBox>(window, "ProviderBox", 2).SelectedValue = BridgeKeyProvider.ProviderId;
                    Of<TextBox>(window, "ReferenceBox", 2).Text = "orders/4711";
                    setup.Supplier.Current = KeySupplierAvailability.NoSupplier;
                    viewModel.RefreshSupplierStatus();
                    await Settle(window);

                    // The root of the window's own visuals, which has the size of the client area; the content alone has a margin that
                    // would put its right and bottom edge outside the picture.
                    var root = (FrameworkElement)VisualTreeHelper.GetChild(window, 0);
                    var bitmap = new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth), (int)Math.Ceiling(root.ActualHeight), 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(root);
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));

                    using var file = System.IO.File.Create(System.IO.Path.Combine(folder, "key-manager-" + culture + ".png"));
                    encoder.Save(file);
                });
            }
        }
    }
}
