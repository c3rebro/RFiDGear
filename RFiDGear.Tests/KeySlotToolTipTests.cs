using RFiDGear.View;

using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

using Xunit;

namespace RFiDGear.Tests
{
    /// <summary>
    /// The tooltips of the key slot controls wrap. A tooltip given as text is one line, and a long text ran off the screen
    /// and could not be read. The tooltips of the editor are elements with the wrapping style, which has a maximum width.
    /// </summary>
    [Collection("WpfBindingTrace")]
    public class KeySlotToolTipTests
    {
        private const string LongText =
            "A key store that holds the keys of this PC and hands them out when a task needs one, so that the project saves " +
            "only a name and never the key itself, which is the whole point of using a reference instead of typing the key.";

        private static async Task OnSta(System.Action<KeySlotEditor, Style> check)
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var editor = new KeySlotEditor();
                var style = (Style)editor.FindResource("KeySlotToolTipStyle");
                check(editor, style);
            });
        }

        [Fact]
        public async Task A_long_text_in_the_tooltip_style_wraps_at_a_maximum_width_instead_of_running_on_one_line()
        {
            await OnSta((_, style) =>
            {
                var shortTip = Measured(new ToolTip { Style = style, Content = "Short." });
                var longTip = Measured(new ToolTip { Style = style, Content = LongText });

                Assert.True(longTip.Width <= 420, "the tooltip is " + longTip.Width + " px wide");
                Assert.True(longTip.Height >= shortTip.Height * 2, "the long text did not wrap");
            });
        }

        [Fact]
        public async Task Without_the_style_the_same_text_runs_on_one_line_which_is_what_the_style_is_for()
        {
            await OnSta((_, _) =>
            {
                var plain = Measured(new ToolTip { Content = LongText });

                Assert.True(plain.Width > 900, "the plain tooltip is only " + plain.Width + " px wide");
            });
        }

        [Fact]
        public async Task Every_tooltip_of_the_editor_is_an_element_with_the_wrapping_style()
        {
            await OnSta((editor, style) =>
            {
                foreach (var name in new[] { "ModeButton", "ProviderBox", "ReferenceBox", "StoreKeyButton" })
                {
                    var element = (FrameworkElement)editor.FindName(name);
                    var tip = Assert.IsType<ToolTip>(element.ToolTip);

                    Assert.Same(style, tip.Style);
                }
            });
        }

        [Fact]
        public async Task The_tooltips_of_the_editor_stay_open_long_enough_to_be_read_and_show_on_the_disabled_store_button()
        {
            await OnSta((editor, _) =>
            {
                Assert.True(ToolTipService.GetShowDuration(editor) >= 20000);
                Assert.True(ToolTipService.GetShowOnDisabled((FrameworkElement)editor.FindName("StoreKeyButton")));
            });
        }

        private static Size Measured(ToolTip tip)
        {
            tip.ApplyTemplate();
            tip.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            return tip.DesiredSize;
        }
    }
}
