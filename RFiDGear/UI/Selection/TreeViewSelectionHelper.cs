using System;
using System.Collections.Generic;
using System.Linq;
using RFiDGear.UI.Selection.Interfaces;
using Serilog;

namespace RFiDGear.UI.Selection
{
    public static class TreeViewSelectionHelper
    {
        public static void ClearSelection(IEnumerable<ITreeSelectionNode> nodes, ILogger logger)
        {
            if (nodes == null)
            {
                return;
            }

            foreach (var node in nodes)
            {
                ClearSelection(node, logger);
            }
        }

        public static void ClearSelection(ITreeSelectionNode node, ILogger logger)
        {
            if (node == null)
            {
                return;
            }

            try
            {
                node.IsSelected = false;
                if (node.SelectionChildren == null)
                {
                    return;
                }

                foreach (var child in node.SelectionChildren.ToList())
                {
                    ClearSelection(child, logger);
                }
            }
            catch (Exception ex)
            {
                logger?.Warning(ex, "Failed to clear selection on tree node");
            }
        }
    }
}
