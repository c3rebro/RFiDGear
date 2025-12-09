using System;
using Serilog;

namespace RFiDGear.UI.Selection
{
    public static class SelectionScrollHelper
    {
        public static void ScrollSelection(Func<object> selectedItemProvider, Action performScroll, ILogger logger)
        {
            if (selectedItemProvider == null || performScroll == null)
            {
                return;
            }

            var selectedItem = selectedItemProvider();
            if (selectedItem == null)
            {
                return;
            }

            try
            {
                performScroll();
            }
            catch (Exception ex)
            {
                logger?.Warning(ex, "Failed to scroll to selected item");
            }
        }
    }
}
