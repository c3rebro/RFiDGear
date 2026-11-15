using RFiDGear.Infrastructure.Tasks;
using RFiDGear.Infrastructure.Ultralight;
using RFiDGear.Services.SecureKeys;
using RFiDGear.Tests.KeySlots;
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
using System.Windows.Media.Imaging;

using Xunit;

namespace RFiDGear.Tests
{
    /// <summary>
    /// The dialog of an Ultralight task as it is shown: the window with its three tabs bound to the view model, at the smallest size it
    /// can have. A control is found by what it is bound to, and must be visible and enabled when it should take input. Text is not
    /// asserted: it follows the language of the machine.
    /// </summary>
    public class UltralightDialogViewTests
    {
        private const int Chip = 0;
        private const int Keys = 1;
        private const int Pages = 2;

        private static MifareUltralightSetupViewModel Write(string hex = "A1B2C3D401020304", int start = 4) =>
            new MifareUltralightSetupViewModel
            {
                SelectedTaskType = TaskType_MifareUltralightTask.WriteData, CurrentTaskIndex = "0", StartPage = start, WriteDataHex = hex,
            };

        private static MifareUltralightSetupViewModel Read(int start = 4, int count = 3) =>
            new MifareUltralightSetupViewModel
            {
                SelectedTaskType = TaskType_MifareUltralightTask.ReadData, CurrentTaskIndex = "0", StartPage = start, PageCount = count,
            };

        // Shows the window at the given size, on the thread of the window: the view models make controls and cannot be made elsewhere.
        private static async Task OnWindow(Func<MifareUltralightSetupViewModel> create, Func<MifareUltralightSetupView, MifareUltralightSetupViewModel, Task> inspect, double width = 900, double height = 700)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var viewModel = create();
                var window = new MifareUltralightSetupView
                {
                    DataContext = viewModel,
                    Width = width,
                    Height = height,
                    ShowActivated = false,
                    ShowInTaskbar = false,
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    Left = 0,
                    Top = 0,
                };

                try
                {
                    window.Show();
                    await Settle(window);
                    await inspect(window, viewModel);
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
            await RenderedTab.Flush();
            window.UpdateLayout();
        }

        private static async Task SelectTab(Window window, int index)
        {
            Tabs(window).SelectedIndex = index;
            await Settle(window);
        }

        private static TabControl Tabs(Window window) => RenderedTab.LogicalDescendants(window).OfType<TabControl>().First();

        private static IEnumerable<DependencyObject> VisualDescendants(DependencyObject root)
        {
            var count = VisualTreeHelper.GetChildrenCount(root);

            for (var index = 0; index < count; index++)
            {
                var child = VisualTreeHelper.GetChild(root, index);

                yield return child;

                foreach (var nested in VisualDescendants(child))
                {
                    yield return nested;
                }
            }
        }

        private static List<KeySlotEditor> Editors(Window window) =>
            RenderedTab.LogicalDescendants(window).OfType<KeySlotEditor>().ToList();

        // ── The window ──

        [Fact]
        public async Task TheWindow_HasThreeTabs_AndTheInputsAreOffWhileAnOperationRuns()
        {
            await OnWindow(() => Read(), (window, _) =>
            {
                var tabs = Tabs(window);

                Assert.Equal(3, tabs.Items.OfType<TabItem>().Count(item => item.Visibility == Visibility.Visible));
                Assert.Equal("AreInputsEnabled", RenderedTab.BindingPath(tabs, UIElement.IsEnabledProperty));

                return Task.CompletedTask;
            });
        }

        [Fact]
        public async Task TheTaskTypeIndexDescriptionAndExecuteCondition_AreInTheWindow_AndTakeInput()
        {
            await OnWindow(() => Read(), (window, _) =>
            {
                foreach (var path in new[] { "SelectedTaskType", "CurrentTaskIndex", "SelectedTaskDescription", "SelectedExecuteConditionErrorLevel", "SelectedExecuteConditionTaskIndex" })
                {
                    var found = RenderedTab.LogicalDescendants(window).OfType<FrameworkElement>()
                        .Where(element => RenderedTab.BindingPath(element, ComboBox.SelectedItemProperty) == path || RenderedTab.BindingPath(element, TextBox.TextProperty) == path)
                        .ToList();

                    Assert.True(found.Count == 1, $"expected one control bound to {path}, found {found.Count}");
                    RenderedTab.AssertUsable(found[0], path);
                }

                return Task.CompletedTask;
            });
        }

        [Theory]
        [InlineData(Chip)]
        [InlineData(Keys)]
        [InlineData(Pages)]
        public async Task EveryTab_HasItsCaptions_AndNothingIsBlank(int tab)
        {
            await OnWindow(() => Write(), async (window, _) =>
            {
                await SelectTab(window, tab);

                Assert.Empty(RenderedTab.BlankCaptions(window));
                Assert.All(Tabs(window).Items.OfType<TabItem>().Where(item => item.Visibility == Visibility.Visible), item => Assert.False(string.IsNullOrWhiteSpace(item.Header as string)));
            });
        }

        [Theory]
        [InlineData(Chip)]
        [InlineData(Keys)]
        [InlineData(Pages)]
        public async Task EveryTab_FitsTheWindowAtItsSmallestSize(int tab)
        {
            await OnWindow(() => Write(), async (window, viewModel) =>
            {
                await SelectTab(window, tab);
                viewModel.Authentication = UltralightAuth.Password;
                await Settle(window);

                Assert.True(window.ActualWidth >= 899.5 && window.ActualHeight >= 699.5, "the window has its smallest size");

                // What a person has to reach is inside the window: the buttons, boxes and check boxes of the tab and the two buttons at the bottom.
                var reachable = VisualDescendants(window).OfType<FrameworkElement>()
                    .Where(element => (element is ButtonBase || element is ComboBox || element is TextBox || element is CheckBox) && element.IsVisible && element.ActualWidth > 0)
                    .ToList();

                Assert.NotEmpty(reachable);
                Assert.All(reachable, element =>
                {
                    var corner = element.TransformToAncestor(window).Transform(new Point(element.ActualWidth, element.ActualHeight));

                    Assert.True(corner.X <= window.ActualWidth + 0.5, $"{element.GetType().Name} {RenderedTab.BindingPath(element, TextBox.TextProperty)} is outside the window on the right ({corner.X})");
                    Assert.True(corner.Y <= window.ActualHeight + 0.5, $"{element.GetType().Name} {RenderedTab.BindingPath(element, TextBox.TextProperty)} is outside the window at the bottom ({corner.Y})");
                });
            });
        }

        // ── The tab of the chip ──

        [Fact]
        public async Task TheChipTab_HasTheChipAndTheAuthenticationAndTheOptIn_BoundToTheTask()
        {
            await OnWindow(() => Read(), async (window, viewModel) =>
            {
                await SelectTab(window, Chip);

                var chip = RenderedTab.One<ComboBox>(window, Selector.SelectedItemProperty, "ExpectedChip");
                var auth = RenderedTab.One<ComboBox>(window, Selector.SelectedItemProperty, "Authentication");
                var optIn = RenderedTab.One<CheckBox>(window, ToggleButton.IsCheckedProperty, "AllowProtectedPages");

                RenderedTab.AssertUsable(chip, "the chip");
                RenderedTab.AssertUsable(auth, "the authentication");
                RenderedTab.AssertUsable(optIn, "the opt-in");

                // The lists have every value of the enumeration, in its order, and a choice reaches the task.
                Assert.Equal(Enum.GetValues(typeof(UltralightExpectation)).Length, chip.Items.Count);
                Assert.Equal(Enum.GetValues(typeof(UltralightAuth)).Length, auth.Items.Count);

                chip.SelectedIndex = (int)UltralightExpectation.Ntag215;
                auth.SelectedIndex = (int)UltralightAuth.Password;
                optIn.IsChecked = true;

                Assert.Equal(UltralightExpectation.Ntag215, viewModel.ExpectedChip);
                Assert.Equal(UltralightAuth.Password, viewModel.Authentication);
                Assert.True(viewModel.AllowProtectedPages);
            });
        }

        [Fact]
        public async Task TheChipTab_ShowsWhatTheTaskHolds()
        {
            await OnWindow(
                () =>
                {
                    var task = Read();
                    task.ExpectedChip = UltralightExpectation.C;
                    task.Authentication = UltralightAuth.UltralightC;
                    task.AllowProtectedPages = true;

                    return task;
                },
                async (window, _) =>
                {
                    await SelectTab(window, Chip);

                    Assert.Equal((int)UltralightExpectation.C, RenderedTab.One<ComboBox>(window, Selector.SelectedItemProperty, "ExpectedChip").SelectedIndex);
                    Assert.Equal((int)UltralightAuth.UltralightC, RenderedTab.One<ComboBox>(window, Selector.SelectedItemProperty, "Authentication").SelectedIndex);
                    Assert.True(RenderedTab.One<CheckBox>(window, ToggleButton.IsCheckedProperty, "AllowProtectedPages").IsChecked);
                });
        }

        // ── The tab of the keys ──

        [Fact]
        public async Task TheKeysTab_HasAnEditorPerKeySlot_ForTheTaskThatIsEdited()
        {
            await OnWindow(() => Read(), async (window, viewModel) =>
            {
                await SelectTab(window, Keys);

                var editors = Editors(window);

                Assert.Equal(SecureKeySlots.Ultralight.OrderBy(slot => slot, StringComparer.Ordinal), editors.Select(editor => editor.SlotId).OrderBy(slot => slot, StringComparer.Ordinal));
                Assert.All(editors, editor => Assert.Same(viewModel, editor.Host));
                Assert.Equal(2, RenderedTab.LogicalDescendants(window).OfType<KeyManagerButton>().Count());
            });
        }

        [Theory]
        [InlineData(UltralightAuth.None, false, false)]
        [InlineData(UltralightAuth.UltralightC, true, false)]
        [InlineData(UltralightAuth.Password, false, true)]
        public async Task TheKeyBoxesOfTheAuthenticationInUse_TakeInput_AndTheOthersDoNot(UltralightAuth auth, bool keyOn, bool passwordOn)
        {
            await OnWindow(() => Read(), async (window, viewModel) =>
            {
                await SelectTab(window, Keys);
                viewModel.Authentication = auth;
                await Settle(window);

                var editors = Editors(window).ToDictionary(editor => editor.SlotId);

                Assert.Equal(keyOn, editors[SecureKeySlots.UltralightCKey].IsEnabled);
                Assert.Equal(passwordOn, editors[SecureKeySlots.UltralightPassword].IsEnabled);
                Assert.Equal(passwordOn, editors[SecureKeySlots.UltralightPack].IsEnabled);
                Assert.All(editors.Values, editor => Assert.True(editor.IsVisible));
            });
        }

        [Fact]
        public async Task ATypedKey_ReachesTheTask_AndABoundKeyShowsItsReferenceInsteadOfTheKey()
        {
            await OnWindow(() => Read(), async (window, viewModel) =>
            {
                await SelectTab(window, Keys);
                viewModel.Authentication = UltralightAuth.UltralightC;
                await Settle(window);

                var box = RenderedTab.One<TextBox>(window, TextBox.TextProperty, "UltralightCKey");
                box.Text = "00112233445566778899aabbccddeeff";

                Assert.Equal("00112233445566778899AABBCCDDEEFF", viewModel.UltralightCKey);

                viewModel.BindKeySlot(SecureKeySlots.UltralightCKey, "windows-credential-manager", "cards/ultralight-c");
                await Settle(window);

                // The key is not in the project and not in the box: the box shows where it comes from.
                Assert.Equal(string.Empty, viewModel.UltralightCKey);
                Assert.Contains(RenderedTab.LogicalDescendants(window).OfType<TextBox>().Select(text => text.Text), text => text.Contains("cards/ultralight-c", StringComparison.Ordinal));
            });
        }

        // ── The tab of the pages ──

        [Fact]
        public async Task AWritesPages_AreTypedInto_AndTheDataOfTheTaskChanges()
        {
            await OnWindow(() => Write(), async (window, viewModel) =>
            {
                await SelectTab(window, Pages);

                var grid = RenderedTab.One<DataGrid>(window, ItemsControl.ItemsSourceProperty, "PageRows");
                var boxes = VisualDescendants(grid).OfType<DataGridCell>().SelectMany(cell => VisualDescendants(cell).OfType<TextBox>()).ToList();

                Assert.Equal(2, boxes.Count);
                Assert.All(boxes, box => Assert.False(box.IsReadOnly));

                boxes[1].Text = "ff00ff00";

                Assert.Equal("A1B2C3D4FF00FF00", viewModel.WriteDataHex);
            });
        }

        [Fact]
        public async Task AddingAndRemovingAPage_ChangesTheGrid_ThroughTheButtons()
        {
            await OnWindow(() => Write("A1B2C3D4"), async (window, viewModel) =>
            {
                await SelectTab(window, Pages);

                var add = RenderedTab.One<Button>(window, ButtonBase.CommandProperty, "AddPageCommand");
                RenderedTab.AssertUsable(add, "add page");

                add.Command.Execute(null);
                await Settle(window);

                Assert.Equal(2, viewModel.PageRows.Count);
                Assert.False(viewModel.PageRows[1].IsValid);

                // The buttons of the rows, and not the one in the corner of the grid.
                var remove = VisualDescendants(RenderedTab.One<DataGrid>(window, ItemsControl.ItemsSourceProperty, "PageRows")).OfType<DataGridCell>().SelectMany(cell => VisualDescendants(cell).OfType<Button>()).ToList();

                Assert.Equal(2, remove.Count);

                remove[0].Command.Execute(remove[0].CommandParameter);

                // What is left is the empty row, which is not a page: the data says so.
                Assert.Single(viewModel.PageRows);
                Assert.False(UltralightPayload.TryParse(viewModel.WriteDataHex, out _));
            });
        }

        [Fact]
        public async Task AReadsPages_CannotBeTypedInto_AndOnlyTheReadButtonIsThere()
        {
            await OnWindow(() => Read(), async (window, _) =>
            {
                await SelectTab(window, Pages);

                var grid = RenderedTab.One<DataGrid>(window, ItemsControl.ItemsSourceProperty, "PageRows");
                var boxes = VisualDescendants(grid).OfType<DataGridCell>().SelectMany(cell => VisualDescendants(cell).OfType<TextBox>()).ToList();

                Assert.Equal(3, boxes.Count);
                Assert.All(boxes, box => Assert.True(box.IsReadOnly));

                RenderedTab.AssertUsable(RenderedTab.One<Button>(window, ButtonBase.CommandProperty, "ReadDataCommand"), "read");
                Assert.False(RenderedTab.One<Button>(window, ButtonBase.CommandProperty, "WriteDataCommand").IsVisible);
                Assert.False(RenderedTab.One<Button>(window, ButtonBase.CommandProperty, "AddPageCommand").IsVisible);
                RenderedTab.AssertUsable(RenderedTab.One<TextBox>(window, TextBox.TextProperty, "PageCountText"), "the number of pages");
            });
        }

        [Fact]
        public async Task AWritesPages_HaveTheWriteButton_AndNoNumberOfPages()
        {
            await OnWindow(() => Write(), async (window, _) =>
            {
                await SelectTab(window, Pages);

                RenderedTab.AssertUsable(RenderedTab.One<Button>(window, ButtonBase.CommandProperty, "WriteDataCommand"), "write");
                Assert.False(RenderedTab.One<Button>(window, ButtonBase.CommandProperty, "ReadDataCommand").IsVisible);
                Assert.False(RenderedTab.One<TextBox>(window, TextBox.TextProperty, "PageCountText").IsVisible);
                RenderedTab.AssertUsable(RenderedTab.One<TextBox>(window, TextBox.TextProperty, "StartPageText"), "the first page");
            });
        }

        [Fact]
        public async Task AMistakeInTheFirstPageBox_StopsTheButtonsAndTheOkButton_UntilItIsFixed()
        {
            await OnWindow(() => Write(start: 9), async (window, viewModel) =>
            {
                await SelectTab(window, Pages);
                var box = RenderedTab.One<TextBox>(window, TextBox.TextProperty, "StartPageText");
                var write = RenderedTab.One<Button>(window, ButtonBase.CommandProperty, "WriteDataCommand");
                var ok = RenderedTab.One<Button>(window, ButtonBase.CommandProperty, "OKCommand");
                var reader = new KeyRecordingReader { Connected = true };

                box.Text = "abc";
                await Settle(window);

                // The task kept page 9; the buttons are off, and pressing the one that is there does nothing on that page.
                Assert.Equal(9, viewModel.StartPage);
                Assert.False(write.IsEnabled);
                Assert.False(ok.IsEnabled);

                using (ReaderDeviceScope.Use(reader))
                {
                    await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)write.Command).ExecuteAsync(null);
                }

                Assert.Empty(reader.UltralightCalls);

                box.Text = "11";
                await Settle(window);

                Assert.Equal(11, viewModel.StartPage);
                Assert.True(write.IsEnabled);
                Assert.True(ok.IsEnabled);
            });
        }

        [Fact]
        public async Task AMistakeInTheNumberOfPagesBox_StopsTheReadButton()
        {
            await OnWindow(() => Read(), async (window, viewModel) =>
            {
                await SelectTab(window, Pages);
                var box = RenderedTab.One<TextBox>(window, TextBox.TextProperty, "PageCountText");
                var read = RenderedTab.One<Button>(window, ButtonBase.CommandProperty, "ReadDataCommand");

                box.Text = string.Empty;
                await Settle(window);

                Assert.Equal(3, viewModel.PageCount);
                Assert.False(read.IsEnabled);

                box.Text = "2";
                await Settle(window);

                Assert.Equal(2, viewModel.PageCount);
                Assert.True(read.IsEnabled);
            });
        }

        [Fact]
        public async Task TheReadButton_RunsTheTask_AndTheGridShowsWhatWasRead()
        {
            await OnWindow(() => Read(count: 2), async (window, _) =>
            {
                await SelectTab(window, Pages);
                var reader = new KeyRecordingReader { Connected = true };

                using (ReaderDeviceScope.Use(reader))
                {
                    var read = RenderedTab.One<Button>(window, ButtonBase.CommandProperty, "ReadDataCommand");

                    Assert.True(read.Command.CanExecute(null));
                    await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)read.Command).ExecuteAsync(null);
                }

                await Settle(window);

                var grid = RenderedTab.One<DataGrid>(window, ItemsControl.ItemsSourceProperty, "PageRows");
                var shown = VisualDescendants(grid).OfType<DataGridCell>().SelectMany(cell => VisualDescendants(cell).OfType<TextBox>()).Select(box => box.Text).ToList();

                // The reader of the test answers with the position of each byte.
                Assert.Equal(new[] { "10111213", "14151617" }, shown);
            });
        }

        [Fact]
        public async Task ATaskThatDoesNothing_HasNothingToDoOnThePagesTab()
        {
            await OnWindow(() => new MifareUltralightSetupViewModel { SelectedTaskType = TaskType_MifareUltralightTask.None }, async (window, viewModel) =>
            {
                await SelectTab(window, Pages);

                Assert.False(RenderedTab.One<DataGrid>(window, ItemsControl.ItemsSourceProperty, "PageRows").IsEnabled);
                Assert.Empty(viewModel.PageRows);
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

            var pictures = new (string Name, int Tab, Func<MifareUltralightSetupViewModel> Create)[]
            {
                ("ultralight-chip", Chip, () =>
                {
                    var task = Write();
                    task.ExpectedChip = UltralightExpectation.Ntag215;
                    task.Authentication = UltralightAuth.Password;
                    task.SelectedTaskDescription = "Write the NTAG215";

                    return task;
                }),
                ("ultralight-keys", Keys, () =>
                {
                    var task = Write();
                    task.ExpectedChip = UltralightExpectation.Ntag215;
                    task.Authentication = UltralightAuth.Password;
                    task.UltralightPassword = "A1B2C3D4";
                    task.BindKeySlot(SecureKeySlots.UltralightPack, "windows-credential-manager", "cards/ntag215-pack");

                    return task;
                }),
                ("ultralight-pages-write", Pages, () =>
                {
                    var task = Write("A1B2C3D4" + "01020304" + "FFEEDDCC" + "A1B");
                    task.ExpectedChip = UltralightExpectation.Ntag215;

                    return task;
                }),
                ("ultralight-pages-read", Pages, () => Read(count: 5)),
            };

            foreach (var (name, tab, create) in pictures)
            {
                await OnWindow(create, async (window, viewModel) =>
                {
                    await SelectTab(window, tab);

                    if (name.EndsWith("read", StringComparison.Ordinal))
                    {
                        using (ReaderDeviceScope.Use(new KeyRecordingReader { Connected = true }))
                        {
                            await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)viewModel.ReadDataCommand).ExecuteAsync(null);
                        }

                        await Settle(window);
                    }

                    // The root of the window's own visuals, which has the size of the client area.
                    var root = (FrameworkElement)VisualTreeHelper.GetChild(window, 0);
                    var bitmap = new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth), (int)Math.Ceiling(root.ActualHeight), 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(root);
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));

                    using var file = System.IO.File.Create(System.IO.Path.Combine(folder, name + ".png"));
                    encoder.Save(file);
                });
            }
        }
    }
}
