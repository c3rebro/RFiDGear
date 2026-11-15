#nullable enable
using RFiDGear.Infrastructure.ExtensionHost;
using RFiDGear.Services.Sam;
using RFiDGear.Services.SecureKeys;
using RFiDGear.Tests.SecureKeys;
using RFiDGear.View;

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

using Elatec.NET.Sam;

using Xunit;
using static RFiDGear.Tests.RenderedTab;

namespace RFiDGear.Tests.Sam
{
    /// <summary>
    /// The SAM setup of the menu, rendered for real: the readers with a SAM, the master key, the activation, the key that is written,
    /// and what the steps said. Controls are found by their names.
    /// </summary>
    [Collection("WpfBindingTrace")]
    public class SamSetupViewTests
    {
        private const string ReaderName = "HID Global OMNIKEY 5422 Smartcard Reader 0";
        private const string TypedKeyHex = "00112233445566778899AABBCCDDEEFF";
        private const string Prefix = WindowsCredentialKeyProvider.TargetPrefix;

        private sealed class Setup
        {
            internal Setup()
            {
                Source = new FakeSamReaderSource
                {
                    Answer = apdu => Convert.ToHexString(apdu) == "8060000000" ? SamTestData.VersionAnswer() : new byte[] { 0x90, 0x00 },
                }.With(ReaderName, SamTestData.SamAtr);
            }

            internal FakeCredentialStore Store { get; } = new FakeCredentialStore();

            internal FakeSamReaderSource Source { get; }

            internal FakeHostSession Session { get; } = new FakeHostSession();

            internal ScriptedSamDialogs Dialogs { get; } = new ScriptedSamDialogs();

            internal string Culture { get; set; } = "en";

            internal Setup WithKeys(params string[] names)
            {
                foreach (var name in names)
                {
                    Store.Blobs[Prefix + name] = Encoding.Unicode.GetBytes("FFEEDDCCBBAA99887766554433221100");
                }

                return this;
            }

            internal SamSetupViewModel Create()
            {
                var keyStore = new WindowsCredentialKeyProvider(Store);
                var service = new SamSetupService(new SamReaderFinder(Source), _ => Session, Dialogs, new SamKeyReader(SamTestKeys.RegistryOf(keyStore)));

                return SamSetupViewModel.Create(service, keyStore, TestText.In(Culture));
            }
        }

        private static async Task OnWindow(Setup setup, Func<SamSetupView, SamSetupViewModel, Task> inspect)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var viewModel = setup.Create();
                var window = new SamSetupView(viewModel) { ShowActivated = false };

                try
                {
                    window.Show();

                    // The window searches when it opens, off its thread: wait for the answer.
                    await Until(() => viewModel.ReaderNames.Count > 0 || viewModel.HasResult);
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

        private static async Task Until(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(10);

            while (!condition() && DateTime.UtcNow < deadline)
            {
                await Task.Delay(10);
                await Flush();
            }

            Assert.True(condition(), "the window did not get there in time");
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

        // The password box that holds a key while it is hidden, inside the entry that is called MasterKeyBox or NewKeyBox.
        private static PasswordBox Hidden(DependencyObject window, string entryName) => Of<PasswordBox>(Of<FrameworkElement>(window, entryName), "HiddenBox");

        private static void Click(ButtonBase button) => button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

        private static bool WrapsAt(ToolTip tip, double width) =>
            tip.Style is { } style && style.Setters.OfType<Setter>().Any(setter => setter.Property == FrameworkElement.MaxWidthProperty && Equals(setter.Value, width));

        // ── Opening ──

        [Fact]
        public async Task The_window_searches_when_it_opens_and_shows_the_reader_with_the_sam()
        {
            await OnWindow(new Setup().WithKeys("sam/master"), (window, viewModel) =>
            {
                Assert.Equal("MIFARE SAM AV3 setup", window.Title);
                Assert.Equal(new[] { ReaderName }, Of<ComboBox>(window, "ReaderBox").Items.Cast<string>());
                Assert.Equal(ReaderName, Of<ComboBox>(window, "ReaderBox").SelectedItem);
                Assert.Equal("A SAM was found. Check it to connect.", Of<TextBlock>(window, "StateText").Text);
                Assert.Contains("Found a MIFARE SAM AV3", Of<TextBlock>(window, "ResultText").Text, StringComparison.Ordinal);
                Assert.Contains("Found a MIFARE SAM AV3", Of<TextBox>(window, "StatusBox").Text, StringComparison.Ordinal);

                // Before the SAM is checked, only the check can be done.
                Assert.True(Of<Button>(window, "CheckButton").IsEnabled);
                Assert.False(Of<Button>(window, "AuthenticateButton").IsEnabled);
                Assert.False(Of<Button>(window, "ActivateButton").IsEnabled);
                Assert.False(Of<Button>(window, "WriteButton").IsEnabled);

                // Both lists of stored keys offer the keys by name, and the choice to type the key first.
                foreach (var name in new[] { "MasterStoredBox", "NewStoredBox" })
                {
                    Assert.Equal(new[] { string.Empty, "sam/master" }, Of<ComboBox>(window, name).Items.Cast<SamKeyChoice>().Select(c => c.Reference));
                }

                Assert.True(viewModel.MasterUsesFactoryKey);

                return Task.CompletedTask;
            });
        }

        [Fact]
        public async Task The_window_is_in_german_when_the_language_is()
        {
            await OnWindow(new Setup { Culture = "de" }, (window, _) =>
            {
                Assert.Equal("MIFARE-SAM-AV3-Einrichtung", window.Title);
                Assert.Equal("SAM prüfen", Of<Button>(window, "CheckButton").Content);
                Assert.Equal("SAM aktivieren...", Of<Button>(window, "ActivateButton").Content);
                Assert.Equal("Schlüssel schreiben", Of<Button>(window, "WriteButton").Content);

                return Task.CompletedTask;
            });
        }

        // ── The steps, through the buttons ──

        [Fact]
        public async Task The_steps_can_be_taken_one_after_the_other_with_the_buttons_and_a_typed_key_is_cleared_after_it_was_used()
        {
            var setup = new Setup();

            await OnWindow(setup, async (window, viewModel) =>
            {
                Of<Button>(window, "CheckButton").Command.Execute(null);
                await Until(() => viewModel.State == SamSetupState.Connected);
                await Settle(window);

                Assert.True(Of<Button>(window, "AuthenticateButton").IsEnabled);
                Assert.Contains("04010105002301", Of<TextBlock>(window, "VersionText").Text, StringComparison.Ordinal);

                // The factory key is used: the stored list and the key box are off.
                Assert.False(Of<ComboBox>(window, "MasterStoredBox").IsEnabled);
                Assert.False(Of<FrameworkElement>(window, "MasterKeyBox").IsEnabled);

                Click(Of<Button>(window, "AuthenticateButton"));
                await Until(() => viewModel.State == SamSetupState.Authenticated);
                await Settle(window);

                Assert.Equal(new byte[16], setup.Session.KeyCopies.Single());
                Assert.True(Of<Button>(window, "WriteButton").IsEnabled);

                // The key that is written is typed into the hidden box, and the box is empty when the SAM was asked.
                Of<TextBox>(window, "EntryBox").Text = "7";
                Hidden(window, "NewKeyBox").Password = TypedKeyHex;
                await Settle(window);

                Click(Of<Button>(window, "WriteButton"));
                await Until(() => setup.Session.Calls.Any(call => call.StartsWith("change", StringComparison.Ordinal)));
                await Until(() => !viewModel.IsRunning);
                await Settle(window);

                Assert.Equal(7, setup.Session.LastEntryNumber);
                Assert.Equal(TypedKeyHex, Convert.ToHexString(setup.Session.KeyCopies.Last()));
                Assert.Equal(string.Empty, Hidden(window, "NewKeyBox").Password);
                Assert.Contains("key entry 7", Of<TextBlock>(window, "ResultText").Text, StringComparison.Ordinal);
            });
        }

        [Fact]
        public async Task A_typed_master_key_is_used_and_cleared_and_an_entry_that_is_no_key_stays_for_the_person_to_correct()
        {
            var setup = new Setup();

            await OnWindow(setup, async (window, viewModel) =>
            {
                Of<Button>(window, "CheckButton").Command.Execute(null);
                await Until(() => viewModel.State == SamSetupState.Connected);

                Of<CheckBox>(window, "FactoryKeyBox").IsChecked = false;
                await Settle(window);

                Assert.True(Of<FrameworkElement>(window, "MasterKeyBox").IsEnabled);

                // Four digits are no key: nothing is sent and the digits stay.
                Hidden(window, "MasterKeyBox").Password = "0011";
                Click(Of<Button>(window, "AuthenticateButton"));
                await Until(() => viewModel.ResultIsError);
                await Until(() => !viewModel.IsRunning);

                Assert.Empty(setup.Session.Calls);
                Assert.Equal("0011", Hidden(window, "MasterKeyBox").Password);

                Hidden(window, "MasterKeyBox").Password = TypedKeyHex;
                Click(Of<Button>(window, "AuthenticateButton"));
                await Until(() => viewModel.State == SamSetupState.Authenticated);
                await Until(() => !viewModel.IsRunning);
                await Settle(window);

                Assert.Equal(TypedKeyHex, Convert.ToHexString(setup.Session.KeyCopies.Single()));
                Assert.Equal(string.Empty, Hidden(window, "MasterKeyBox").Password);
            });
        }

        [Fact]
        public async Task Choosing_a_stored_key_turns_the_box_for_the_key_off()
        {
            await OnWindow(new Setup().WithKeys("sam/master"), async (window, viewModel) =>
            {
                // With the factory key there is nothing to type: the box is not there (an empty box is red, and would look like a mistake).
                Assert.Equal(Visibility.Collapsed, Of<FrameworkElement>(window, "MasterKeyBox").Visibility);
                Assert.Equal(Visibility.Visible, Of<FrameworkElement>(window, "NewKeyBox").Visibility);

                Of<CheckBox>(window, "FactoryKeyBox").IsChecked = false;
                await Settle(window);

                Assert.True(Of<ComboBox>(window, "MasterStoredBox").IsEnabled);
                Assert.True(Of<FrameworkElement>(window, "MasterKeyBox").IsEnabled);
                Assert.Equal(Visibility.Visible, Of<FrameworkElement>(window, "MasterKeyBox").Visibility);

                Of<ComboBox>(window, "MasterStoredBox").SelectedValue = "sam/master";
                await Settle(window);

                Assert.Equal("sam/master", viewModel.MasterReference);
                Assert.False(Of<FrameworkElement>(window, "MasterKeyBox").IsEnabled);
                Assert.Equal(Visibility.Collapsed, Of<FrameworkElement>(window, "MasterKeyBox").Visibility);

                Of<ComboBox>(window, "NewStoredBox").SelectedValue = "sam/master";
                await Settle(window);

                Assert.False(Of<FrameworkElement>(window, "NewKeyBox").IsEnabled);
                Assert.Equal(Visibility.Collapsed, Of<FrameworkElement>(window, "NewKeyBox").Visibility);
            });
        }

        [Fact]
        public async Task A_sam_that_says_not_activated_turns_the_activation_on_and_a_no_to_the_question_changes_nothing()
        {
            var setup = new Setup();
            setup.Session.AuthenticateFails = new SamException("part 1", 0x6985);

            await OnWindow(setup, async (window, viewModel) =>
            {
                Of<Button>(window, "CheckButton").Command.Execute(null);
                await Until(() => viewModel.State == SamSetupState.Connected);
                Click(Of<Button>(window, "AuthenticateButton"));
                await Until(() => viewModel.State == SamSetupState.NotActivated);
                await Until(() => !viewModel.IsRunning);
                await Settle(window);

                Assert.True(Of<Button>(window, "ActivateButton").IsEnabled);
                Assert.Equal("The SAM is not activated.", Of<TextBlock>(window, "StateText").Text);
                Assert.True(Of<TextBlock>(window, "ResultText").IsVisible);

                setup.Dialogs.Agree = false;
                Click(Of<Button>(window, "ActivateButton"));
                await Until(() => setup.Dialogs.ActivationAsked == 1);
                await Until(() => !viewModel.IsRunning);

                Assert.DoesNotContain(setup.Session.Calls, call => call.StartsWith("activate", StringComparison.Ordinal));
                Assert.Equal(SamSetupState.NotActivated, viewModel.State);
            });
        }

        // ── Closing ──

        [Fact]
        public async Task The_window_cannot_be_closed_while_a_step_runs_and_closes_when_it_is_over()
        {
            var setup = new Setup();
            var release = new TaskCompletionSource();

            await OnWindow(setup, async (window, viewModel) =>
            {
                Of<Button>(window, "CheckButton").Command.Execute(null);
                await Until(() => viewModel.State == SamSetupState.Connected);

                setup.Session.AuthenticateBlocker = release.Task;
                Click(Of<Button>(window, "AuthenticateButton"));
                await Until(() => viewModel.IsRunning);
                await Settle(window);

                Assert.False(Of<Button>(window, "AuthenticateButton").IsEnabled);

                window.Close();

                Assert.True(window.IsVisible, "the window was closed while a card call ran");

                release.SetResult();
                await Until(() => !viewModel.IsRunning);

                window.Close();

                Assert.False(window.IsVisible);
            });
        }

        [Fact]
        public async Task The_button_that_closes_is_the_cancel_button_and_not_the_default()
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

        // ── Tooltips ──

        [Fact]
        public async Task Every_tooltip_of_the_window_wraps_stays_long_enough_and_has_its_text_when_it_opens()
        {
            await OnWindow(new Setup(), async (window, viewModel) =>
            {
                var style = (Style)window.FindResource("KeySlotToolTipStyle");

                foreach (var (owner, expected) in new (FrameworkElement, string)[]
                {
                    (Of<Button>(window, "CheckButton"), viewModel.CheckToolTip),
                    (Of<CheckBox>(window, "FactoryKeyBox"), viewModel.FactoryKeyToolTip),
                    (Of<ComboBox>(window, "MasterStoredBox"), viewModel.StoredKeyToolTip),
                    (Of<Button>(window, "AuthenticateButton"), viewModel.AuthenticateToolTip),
                    (Of<Button>(window, "ActivateButton"), viewModel.ActivateToolTip),
                    (Of<TextBox>(window, "EntryBox"), viewModel.EntryToolTip),
                    (Of<ComboBox>(window, "NewStoredBox"), viewModel.StoredKeyToolTip),
                    (Of<Button>(window, "WriteButton"), viewModel.WriteToolTip),
                })
                {
                    Assert.False(string.IsNullOrWhiteSpace(expected), owner.Name + " has no text for its tooltip");

                    var tip = Assert.IsType<ToolTip>(owner.ToolTip);
                    Assert.True(ReferenceEquals(style, tip.Style) || WrapsAt(tip, 380), owner.Name + " has a tooltip that does not wrap");
                    Assert.True(ToolTipService.GetShowDuration(owner) >= 20000, owner.Name + " shows its tooltip for " + ToolTipService.GetShowDuration(owner) + " ms");
                    Assert.Equal(expected, await OpenedToolTip.ContentOf(window, owner));
                }

                // A button that is off still says why.
                Assert.True(ToolTipService.GetShowOnDisabled(Of<Button>(window, "AuthenticateButton")));
                Assert.True(ToolTipService.GetShowOnDisabled(Of<Button>(window, "ActivateButton")));
                Assert.True(ToolTipService.GetShowOnDisabled(Of<Button>(window, "WriteButton")));
            });
        }

        // ── Size ──

        [Theory]
        [InlineData("en")]
        [InlineData("de")]
        public async Task The_window_fits_its_content_in_both_languages_with_long_reader_and_key_names_and_the_buttons_stay_in_it(string culture)
        {
            var setup = new Setup { Culture = culture }.WithKeys(Enumerable.Range(0, 30).Select(index => "cards/a-rather-long-name-of-a-key-that-is-number-" + index).ToArray());
            setup.Source.With("HID Global OMNIKEY 5422 Smartcard Reader With A Rather Long Name Number Two 1", SamTestData.SamAtr);

            await OnWindow(setup, async (window, viewModel) =>
            {
                Of<Button>(window, "CheckButton").Command.Execute(null);
                await Until(() => viewModel.State == SamSetupState.Connected);
                Click(Of<Button>(window, "AuthenticateButton"));
                await Until(() => viewModel.State == SamSetupState.Authenticated);
                await Until(() => !viewModel.IsRunning);
                await Settle(window);

                Assert.True(window.ActualWidth >= 560);
                Assert.True(window.ActualHeight <= 900, "the window is " + window.ActualHeight + " px high");

                foreach (var name in new[] { "RefreshButton", "CheckButton", "AuthenticateButton", "ActivateButton", "WriteButton", "CloseButton" })
                {
                    var button = Of<Button>(window, name);
                    var right = button.TransformToAncestor(window).Transform(new Point(button.ActualWidth, 0)).X;
                    Assert.True(right <= window.ActualWidth + 0.5, name + " is outside the window");
                }
            });
        }

        // ── The menu ──

        [Fact]
        public async Task The_command_of_the_menu_item_shows_the_setup_once_with_the_gate_of_the_reader_and_a_way_to_say_that_it_is_busy()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var folder = Directory.CreateTempSubdirectory("rfidgear-samsetup-");

                try
                {
                    var (viewModel, _) = MainWindowViewModelTests.CreateUninitializedViewModel(folder.FullName);
                    var launcher = new RecordingSamSetupLauncher();
                    viewModel.SamSetupLauncher = launcher;

                    viewModel.OpenSamSetupCommand.Execute(null);

                    Assert.Equal(1, launcher.Shown);
                    Assert.Same(viewModel.ReaderGate, launcher.Gate);

                    // The coordinator of the main window: the one that a run, a load and a scan of an extension are admitted by.
                    var coordinator = typeof(RFiDGear.ViewModel.MainWindowViewModel).GetField("_coordinator", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(viewModel);
                    Assert.Same(coordinator, launcher.Coordinator);

                    // The view model of this test has no main window, so the window would have no owner (as for the Key manager).
                    Assert.Null(launcher.Owner);

                    // What the window says about the reader is what the setup asks before it opens, and what it tells the window.
                    viewModel.IsReaderBusy = false;
                    Assert.False(launcher.IsReaderBusy!());
                    viewModel.IsReaderBusy = true;
                    Assert.True(launcher.IsReaderBusy!());

                    launcher.SetBusy!(true);
                    Assert.True(viewModel.IsReaderBusy);

                    launcher.SetBusy(false);
                    Assert.False(viewModel.IsReaderBusy);
                }
                finally
                {
                    folder.Delete(recursive: true);
                }
            });
        }

        private sealed class RecordingSamSetupLauncher : ISamSetupLauncher
        {
            public int Shown { get; private set; }

            public Window? Owner { get; private set; }

            public ReaderAccessGate? Gate { get; private set; }

            public ExtensionOperationCoordinator? Coordinator { get; private set; }

            public Func<bool?>? IsReaderBusy { get; private set; }

            public Action<bool?>? SetBusy { get; private set; }

            public void Show(Window? owner, ReaderAccessGate gate, ExtensionOperationCoordinator coordinator, Func<bool?> isReaderBusy, Action<bool?> setBusy)
            {
                Shown++;
                Owner = owner;
                Gate = gate;
                Coordinator = coordinator;
                IsReaderBusy = isReaderBusy;
                SetBusy = setBusy;
            }
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
                foreach (var stage in new[] { "found", "authenticated" })
                {
                    var setup = new Setup { Culture = culture }.WithKeys("sam/master", "cards/application-main");

                    await OnWindow(setup, async (window, viewModel) =>
                    {
                        if (stage == "authenticated")
                        {
                            await viewModel.CheckAsync();
                            await viewModel.AuthenticateAsync(null);
                            viewModel.EntryNumberText = "1";
                        }

                        await Settle(window);

                        var root = (FrameworkElement)VisualTreeHelper.GetChild(window, 0);
                        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth), (int)Math.Ceiling(root.ActualHeight), 96, 96, PixelFormats.Pbgra32);
                        bitmap.Render(root);
                        var encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(bitmap));

                        using var file = File.Create(Path.Combine(folder, "sam-setup-" + stage + "-" + culture + ".png"));
                        encoder.Save(file);
                    });
                }
            }
        }
    }
}
