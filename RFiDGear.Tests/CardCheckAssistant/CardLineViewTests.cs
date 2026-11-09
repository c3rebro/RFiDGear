using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

using RFiDGear.Extensions.CardCheckAssistant.ViewModels;
using RFiDGear.Extensions.CardCheckAssistant.Views;

using Xunit;

using static RFiDGear.Tests.CardCheckAssistant.ViewModelTestSupport;

using Keys = RFiDGear.Extensions.CardCheckAssistant.Localization.ExtensionStrings.Keys;

namespace RFiDGear.Tests.CardCheckAssistant
{
    /// <summary>
    /// The card line and the Read card button as the operator sees them: the page shows the line and the button, the
    /// button looks at the reader and the line follows, the button is off while tasks run, no binding of it is wrong, and
    /// the page still fits at the smallest size it declares.
    /// </summary>
    [Collection("WpfBindingTrace")]
    public class CardLineViewTests
    {
        private static async Task Show(
            Func<CardCheckAssistantView, CardCheckAssistantViewModel, FakeExtensionHost, Task> check,
            Size? pageSize = null)
        {
            var listener = BindingErrorListener.Start();
            try
            {
                await StaTestRunner.RunOnStaThreadAsync(async () =>
                {
                    var host = new FakeExtensionHost("en-US");
                    host.ReaderHost.Show(Snapshots.NoCard(), raise: false);
                    var viewModel = new CardCheckAssistantViewModel(host, Dispatcher.CurrentDispatcher, new FakeFileDialogService());
                    var view = new CardCheckAssistantView(viewModel);
                    var window = new Window { Content = view, Width = 900, Height = 700 };
                    if (pageSize is { } size)
                    {
                        view.Width = size.Width;
                        view.Height = size.Height;
                        window.SizeToContent = SizeToContent.WidthAndHeight;
                    }

                    window.Show();
                    try
                    {
                        await viewModel.Open();
                        await Flush();
                        window.UpdateLayout();

                        await check(view, viewModel, host);
                    }
                    finally
                    {
                        host.ExecutionHost.SetRunningQuietly(false);
                        window.Close();
                    }
                });
            }
            finally
            {
                listener.Stop();
            }

            Assert.Empty(listener.Messages);
        }

        private static T Named<T>(FrameworkElement root, string name)
            where T : class
        {
            var found = root.FindName(name);
            Assert.NotNull(found);

            return Assert.IsAssignableFrom<T>(found);
        }

        [Fact]
        public async Task ThePageShowsTheLineAndTheButton_AndThePressedButtonLooksAtTheReader()
        {
            await Show(async (view, viewModel, host) =>
            {
                var line = Named<TextBlock>(view, "CardLine");
                var button = Named<Button>(view, "ReadCardButton");
                Assert.Equal("Card: no card found", line.Text);
                Assert.Equal(viewModel.Strings[Keys.ReadCardButton], button.Content);
                Assert.True(button.IsEnabled);
                var tip = Assert.IsType<string>(line.ToolTip);
                Assert.Contains(viewModel.Strings[Keys.CardLineHint], tip, StringComparison.Ordinal);

                var run = Named<Button>(view, "RunButton");
                var gate = NewGate();
                host.ReaderHost.ScriptScan(Snapshots.Card("04AABB"), gate);
                button.Command.Execute(null);
                await Flush();

                // While the scan is pending: the line says so, and neither button can be pressed.
                Assert.Equal("Card: reading...", line.Text);
                Assert.False(button.IsEnabled);
                Assert.False(run.IsEnabled);

                gate.SetResult(true);
                await WaitUntil(() => line.Text == "Card: DESFireEV1, UID 04AABB", "the line to show the card");
                await Flush();

                Assert.Equal(1, host.ReaderHost.ScanCount);
                Assert.True(button.IsEnabled);
                Assert.True(run.IsEnabled);
            });
        }

        [Fact]
        public async Task TheButtonIsOff_WhileTasksRun_AndTheLineFollowsTheLanguage()
        {
            await Show(async (view, _, host) =>
            {
                var line = Named<TextBlock>(view, "CardLine");
                var button = Named<Button>(view, "ReadCardButton");

                host.ExecutionHost.SetRunning(true);
                await Flush();
                Assert.False(button.IsEnabled);

                host.ExecutionHost.SetRunning(false);
                await Flush();
                Assert.True(button.IsEnabled);

                host.Context.ChangeCulture("de-DE");
                await Flush();
                Assert.Equal("Karte: keine Karte gefunden", line.Text);
                Assert.Equal("Karte lesen", button.Content);
            });
        }

        [Fact]
        public async Task AtTheSmallestSizeThePageDeclares_TheCardRowFitsAndTheRunButtonStaysInView()
        {
            // The card row sits above the fields and takes its height from the room the results have (the page declares
            // 30 units more height for it). A long line is cut short with an ellipsis instead of pushing the button out
            // of the page.
            var smallest = new Size(620, 490);
            await Show(
                async (view, _, host) =>
                {
                    host.ReaderHost.Show(Snapshots.Card(new string('A', 120)));
                    await Flush();
                    view.UpdateLayout();

                    var line = Named<TextBlock>(view, "CardLine");
                    var natural = new TextBlock { Text = line.Text, FontFamily = line.FontFamily, FontSize = line.FontSize };
                    natural.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    Assert.True(natural.DesiredSize.Width > line.ActualWidth, "the line fits its room, so this checks nothing");
                    Assert.Equal(TextTrimming.CharacterEllipsis, line.TextTrimming);

                    var row = Named<Grid>(view, "CardRow");
                    var button = Named<Button>(view, "ReadCardButton");
                    var run = Named<Button>(view, "RunButton");
                    var buttonRight = button.TransformToAncestor(view).Transform(new Point(button.ActualWidth, 0)).X;
                    var runBottom = run.TransformToAncestor(view).Transform(new Point(0, run.ActualHeight)).Y;

                    Assert.True(row.ActualWidth <= smallest.Width, "the card row is wider than the page");
                    Assert.True(buttonRight <= smallest.Width, "the Read card button is out of the page");
                    Assert.True(row.ActualHeight <= 32, "the card row takes more than one line and a button: " + row.ActualHeight);
                    Assert.True(runBottom <= smallest.Height - 150, "the run button leaves less than 150 units for the results: " + runBottom);
                },
                smallest);
        }
    }
}
