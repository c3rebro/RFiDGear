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
    /// The key manager window, rendered for real: a row for each slot, a typed key or a reference, the key store with a tooltip
    /// for each entry, a box in which a key is entered hidden and cleared after it was stored, the status of the supplier, and
    /// the way out. Controls are found by their names in the template of a row.
    /// </summary>
    [Collection("WpfBindingTrace")]
    public class KeyManagerViewTests
    {
        private static readonly SecureKeyProviderInfo CredentialManager = new SecureKeyProviderInfo(WindowsCredentialKeyProvider.ProviderId, "Windows Credential Manager");
        private static readonly SecureKeyProviderInfo ExternalApplication = new SecureKeyProviderInfo(BridgeKeyProvider.ProviderId, "Connected external application");

        private sealed class Setup
        {
            internal FakeKeySlotHost Host { get; } = new FakeKeySlotHost();

            internal FakeCredentialStore Store { get; } = new FakeCredentialStore();

            internal FakeSupplierStatus Supplier { get; } = new FakeSupplierStatus();

            internal string Culture { get; set; } = "en";

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
                    window.UpdateLayout();
                    await Flush();
                    window.UpdateLayout();

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

        private static async Task Toggle(KeyManagerView window, int row)
        {
            Of<Button>(window, "ModeButton", row).Command.Execute(null);
            window.UpdateLayout();
            await Flush();
            window.UpdateLayout();
        }

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
        public async Task Using_a_reference_shows_the_key_store_the_name_and_the_box_for_a_key_and_the_slot_is_bound()
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

                // The key can be stored for the Credential Manager, and the box takes a key of any length: a limit would
                // silently cut a pasted key that is too long.
                Assert.True(Of<StackPanel>(window, "StorePanel").IsVisible);
                Assert.Equal(0, Of<PasswordBox>(window, "KeyBox").MaxLength);
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
                    Of<Button>(window, "ModeButton"), Of<ComboBox>(window, "ProviderBox"),
                    Of<TextBox>(window, "ReferenceBox"), Of<Button>(window, "StoreButton"),
                })
                {
                    Assert.Same(style, Assert.IsType<ToolTip>(owner.ToolTip).Style);
                }

                // The entries of the list: instantiate the template of an item, as the list does for each one.
                var providers = Of<ComboBox>(window, "ProviderBox");
                var item = Assert.IsType<Grid>(providers.ItemTemplate.LoadContent());
                item.DataContext = providers.Items[0];
                var tip = Assert.IsType<ToolTip>(item.ToolTip);

                Assert.Same(style, tip.Style);

                // Its text is the tooltip of the entry the template is made for, not one text for all of them.
                Assert.Equal(nameof(KeySlotProviderItem.ToolTip), System.Windows.Data.BindingOperations.GetBinding(tip, ContentControl.ContentProperty)?.Path.Path);
            });
        }

        // ── Storing a key ──

        [Fact]
        public async Task A_key_that_is_entered_is_stored_under_the_name_and_the_box_is_cleared()
        {
            var setup = new Setup();

            await OnWindow(setup, new[] { SecureKeySlots.DesfireRead }, async (window, _) =>
            {
                await Toggle(window, 0);
                Of<TextBox>(window, "ReferenceBox").Text = "cards/main";
                var box = Of<PasswordBox>(window, "KeyBox");
                box.Password = "000102030405060708090a0b0c0d0e0f";
                window.UpdateLayout();
                await Flush();

                var button = Of<Button>(window, "StoreButton");
                Assert.True(button.IsEnabled);
                button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                window.UpdateLayout();
                await Flush();

                Assert.Equal(
                    Encoding.Unicode.GetBytes("000102030405060708090A0B0C0D0E0F"),
                    setup.Store.Blobs[WindowsCredentialKeyProvider.TargetPrefix + "cards/main"]);
                Assert.Equal(string.Empty, box.Password);

                var result = Of<TextBlock>(window, "ResultText");
                Assert.True(result.IsVisible);
                Assert.Equal("The key was stored under the reference.", result.Text);
            });
        }

        [Fact]
        public async Task A_key_of_the_wrong_length_is_not_stored_and_the_box_is_cleared_all_the_same()
        {
            var setup = new Setup();

            await OnWindow(setup, new[] { SecureKeySlots.DesfireRead }, async (window, _) =>
            {
                await Toggle(window, 0);
                Of<TextBox>(window, "ReferenceBox").Text = "cards/main";
                var box = Of<PasswordBox>(window, "KeyBox");
                box.Password = "0102";
                window.UpdateLayout();
                await Flush();

                Of<Button>(window, "StoreButton").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                window.UpdateLayout();
                await Flush();

                Assert.Empty(setup.Store.Writes);
                Assert.Equal(string.Empty, box.Password);
                Assert.True(Of<TextBlock>(window, "ResultText").IsVisible);
            });
        }

        [Fact]
        public async Task A_key_that_was_entered_and_not_stored_does_not_stay_in_the_window_when_it_closes()
        {
            // Close, Escape and the title bar all end in the same place; rows that are collapsed have their boxes too.
            await OnWindow(new Setup(), new[] { SecureKeySlots.DesfireRead, SecureKeySlots.DesfireWrite }, async (window, _) =>
            {
                await Toggle(window, 0);
                var shown = Of<PasswordBox>(window, "KeyBox", 0);
                var collapsed = Of<PasswordBox>(window, "KeyBox", 1);
                shown.Password = "0001";
                collapsed.Password = "0002";

                window.Close();
                await Flush();

                Assert.Equal(string.Empty, shown.Password);
                Assert.Equal(string.Empty, collapsed.Password);
            });
        }

        [Fact]
        public async Task Enter_in_the_box_of_a_key_stores_it_and_never_closes_the_window()
        {
            var setup = new Setup();

            await OnWindow(setup, new[] { SecureKeySlots.DesfireRead }, async (window, _) =>
            {
                await Toggle(window, 0);
                Of<TextBox>(window, "ReferenceBox").Text = "cards/main";
                var box = Of<PasswordBox>(window, "KeyBox");
                box.Password = "000102030405060708090a0b0c0d0e0f";

                var args = PressEnter(box);
                window.UpdateLayout();
                await Flush();

                Assert.True(args.Handled);
                Assert.True(window.IsVisible);
                Assert.Equal(
                    Encoding.Unicode.GetBytes("000102030405060708090A0B0C0D0E0F"),
                    setup.Store.Blobs[WindowsCredentialKeyProvider.TargetPrefix + "cards/main"]);
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
                var box = Of<PasswordBox>(window, "KeyBox");
                box.Password = "0001";

                // No name yet: nothing can be stored. The digits stay where the person can still finish them.
                var args = PressEnter(box);
                await Flush();

                Assert.True(args.Handled);
                Assert.True(window.IsVisible);
                Assert.Empty(setup.Store.Writes);
                Assert.Equal("0001", box.Password);
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
                Of<PasswordBox>(window, "KeyBox", 0).Password = "A1B2C3";

                foreach (var (name, slotName) in new[] { (0, "Application key, current"), (1, "Application key, new") })
                {
                    foreach (var control in new FrameworkElement[]
                    {
                        Of<Button>(window, "ModeButton", name), Of<ComboBox>(window, "ProviderBox", name),
                        Of<TextBox>(window, "ReferenceBox", name), Of<PasswordBox>(window, "KeyBox", name), Of<Button>(window, "StoreButton", name),
                    })
                    {
                        var accessibleName = System.Windows.Automation.AutomationProperties.GetName(control);

                        Assert.StartsWith(slotName + ": ", accessibleName);
                        Assert.DoesNotContain("A1B2C3", accessibleName);
                    }
                }

                // The same control in two rows is told apart by its name.
                Assert.NotEqual(
                    System.Windows.Automation.AutomationProperties.GetName(Of<PasswordBox>(window, "KeyBox", 0)),
                    System.Windows.Automation.AutomationProperties.GetName(Of<PasswordBox>(window, "KeyBox", 1)));
            });
        }

        // Presses Enter in a control the way the keyboard does, as far as the window can tell.
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

        [Fact]
        public async Task The_store_button_waits_for_a_valid_name()
        {
            await OnWindow(new Setup(), new[] { SecureKeySlots.DesfireRead }, async (window, _) =>
            {
                await Toggle(window, 0);

                Assert.False(Of<Button>(window, "StoreButton").IsEnabled);

                Of<TextBox>(window, "ReferenceBox").Text = "cards/main";
                window.UpdateLayout();
                await Flush();

                Assert.True(Of<Button>(window, "StoreButton").IsEnabled);
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
                window.UpdateLayout();
                await Flush();

                var status = Of<TextBlock>(window, "SupplierStatus");
                Assert.True(status.IsVisible);
                Assert.Contains("not running", status.Text);
                Assert.False(Of<StackPanel>(window, "StorePanel").IsVisible);

                setup.Supplier.Current = KeySupplierAvailability.Connected;
                viewModel.RefreshSupplierStatus();
                window.UpdateLayout();
                await Flush();

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
            var setup = new Setup { Culture = culture };

            await OnWindow(setup, SecureKeySlots.Classic.Take(4).ToArray(), async (window, _) =>
            {
                await Toggle(window, 0);
                await Toggle(window, 1);

                Assert.True(window.ActualWidth >= 460);
                Assert.True(window.ActualHeight <= 780);

                var close = Of<Button>(window, "CloseButton");
                var position = close.TransformToAncestor(window).Transform(new Point(0, 0));
                Assert.True(position.X + close.ActualWidth <= window.ActualWidth + 0.5, "the close button is outside the window");
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
                var setup = new Setup { Culture = culture };
                setup.Host.EmbeddedKeys[SecureKeySlots.DesfirePiccCurrent] = "0000";

                await OnWindow(setup, new[] { SecureKeySlots.DesfirePiccCurrent, SecureKeySlots.DesfireAppCurrent, SecureKeySlots.DesfireAppNew }, async (window, viewModel) =>
                {
                    await Toggle(window, 1);
                    Of<TextBox>(window, "ReferenceBox", 0).Text = "cards/application-main";
                    await Toggle(window, 2);
                    Of<ComboBox>(window, "ProviderBox", 1).SelectedValue = BridgeKeyProvider.ProviderId;
                    Of<TextBox>(window, "ReferenceBox", 1).Text = "orders/4711";
                    setup.Supplier.Current = KeySupplierAvailability.NoSupplier;
                    viewModel.RefreshSupplierStatus();
                    window.UpdateLayout();
                    await Flush();
                    window.UpdateLayout();

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
