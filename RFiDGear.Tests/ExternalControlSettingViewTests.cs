#nullable enable
using RFiDGear.Infrastructure;
using RFiDGear.View;

using System.Globalization;
using System.Linq;
using System.Resources;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

using Xunit;
using static RFiDGear.Tests.RenderedTab;

namespace RFiDGear.Tests
{
    /// <summary>
    /// The setting that switches the external control on, in the Misc tab of the settings window: a label that fits the smallest
    /// window in both languages, and a tooltip that breaks its lines instead of running off the screen.
    /// </summary>
    [Collection("WpfBindingTrace")]
    public class ExternalControlSettingViewTests
    {
        // The smallest width of the settings window (SetupView.MinWidth), less what the tab control and the margins of the tab take.
        private const double SmallestWindow = 480;
        private const double Chrome = 60;

        private static readonly ResourceManager Manifest =
            new ResourceManager("RFiDGear.Resources.Manifest", typeof(ResourceLoader).Assembly);

        private sealed class StubSettings
        {
            public string LocalizationResourceSet { get; } = string.Empty;

            public bool LoadOnStart { get; set; }

            public bool CheckOnStart { get; set; }

            public bool EnableExternalControl { get; set; }
        }

        private static async Task OnTab(System.Func<Window, CheckBox, Task> inspect)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var tab = new TabPageMiscReaderSettingsView { DataContext = new StubSettings() };
                var window = new Window { Content = tab, Width = SmallestWindow, Height = 300, ShowActivated = false, ShowInTaskbar = false };

                try
                {
                    window.Show();
                    window.UpdateLayout();
                    await Flush();
                    window.UpdateLayout();

                    var box = LogicalDescendants(tab).OfType<CheckBox>().Single(candidate => (candidate.IsChecked is false) && candidate.ToolTip != null);

                    await inspect(window, box);
                }
                finally
                {
                    window.Close();
                }
            });
        }

        // ── The label ──

        [Fact]
        public void The_label_names_the_setting_in_a_few_words_in_both_languages()
        {
            Assert.Equal("Enable JSON-RPC pipe server", Manifest.GetString("labelEnableExternalControl", new CultureInfo("en")));
            Assert.Equal("JSON-RPC Pipe Server aktivieren", Manifest.GetString("labelEnableExternalControl", new CultureInfo("de")));
        }

        [Theory]
        [InlineData("en")]
        [InlineData("de")]
        public async Task The_label_fits_the_smallest_settings_window_in_both_languages(string culture)
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var box = new CheckBox { Content = Manifest.GetString("labelEnableExternalControl", new CultureInfo(culture)) };
                box.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

                Assert.True(box.DesiredSize.Width + Chrome <= SmallestWindow, culture + ": the label is " + box.DesiredSize.Width + " px wide");
            });
        }

        [Fact]
        public async Task The_check_box_in_the_tab_shows_that_label()
        {
            // A check box is clipped to the room it is given, so the width it takes is measured above, on the text; this is the link.
            await OnTab((_, box) =>
            {
                Assert.Equal(ResourceLoader.GetResource("labelEnableExternalControl"), box.Content);

                return Task.CompletedTask;
            });
        }

        // ── The tooltip ──

        [Fact]
        public async Task The_tooltip_is_an_element_with_a_maximum_width_that_wraps_its_text_and_has_it()
        {
            await OnTab(async (window, box) =>
            {
                // A tooltip given as text is one line, and a long text ran off the screen.
                var tip = Assert.IsType<ToolTip>(box.ToolTip);
                var expected = ResourceLoader.GetResource("toolTipEnableExternalControl");
                Assert.False(string.IsNullOrWhiteSpace(expected));

                tip.PlacementTarget = box;
                tip.IsOpen = true;

                try
                {
                    window.UpdateLayout();
                    await Flush();

                    var text = Assert.IsType<TextBlock>(tip.Content);
                    Assert.Equal(expected, text.Text);
                    Assert.Equal(TextWrapping.Wrap, text.TextWrapping);
                    Assert.True(tip.ActualWidth <= 420, "the tooltip is " + tip.ActualWidth + " px wide");

                    // More than one line of text.
                    Assert.True(text.ActualHeight >= text.FontSize * 2.5, "the text is " + text.ActualHeight + " px high");
                }
                finally
                {
                    tip.IsOpen = false;
                }
            });
        }
    }
}
