#nullable enable
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

using static RFiDGear.Tests.RenderedTab;

namespace RFiDGear.Tests
{
    /// <summary>
    /// Opens the tooltip of a control the way the pointer would and reads what it shows. A tooltip opens in a popup of its own, which
    /// is not in the tree of the control: a binding that has to find the control by walking up from the tooltip finds nothing there,
    /// and the tooltip is empty. Looking at the style and at the path of a binding does not show that.
    /// </summary>
    internal static class OpenedToolTip
    {
        /// <summary>Opens the tooltip of a control and returns its content.</summary>
        /// <param name="window">The window that shows the control.</param>
        /// <param name="owner">The control that has the tooltip.</param>
        /// <returns>What the tooltip shows once it is open.</returns>
        internal static async Task<object?> ContentOf(Window window, FrameworkElement owner)
        {
            var tip = (ToolTip)owner.ToolTip;
            tip.PlacementTarget = owner;
            tip.IsOpen = true;

            try
            {
                window.UpdateLayout();
                await Flush();

                return tip.Content;
            }
            finally
            {
                tip.IsOpen = false;
            }
        }
    }
}
