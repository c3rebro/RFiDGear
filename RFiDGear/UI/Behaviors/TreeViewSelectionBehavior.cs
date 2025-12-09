using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RFiDGear.UI.Selection;
using RFiDGear.UI.Selection.Interfaces;
using Serilog;

namespace RFiDGear.UI.Behaviors
{
    public static class TreeViewSelectionBehavior
    {
        public static readonly DependencyProperty ClearSelectionOnEmptySpaceProperty = DependencyProperty.RegisterAttached(
            "ClearSelectionOnEmptySpace",
            typeof(bool),
            typeof(TreeViewSelectionBehavior),
            new PropertyMetadata(false, OnClearSelectionChanged));

        private static readonly ILogger Logger = Log.ForContext(typeof(TreeViewSelectionBehavior));

        public static bool GetClearSelectionOnEmptySpace(DependencyObject target)
        {
            return (bool)target.GetValue(ClearSelectionOnEmptySpaceProperty);
        }

        public static void SetClearSelectionOnEmptySpace(DependencyObject target, bool value)
        {
            target.SetValue(ClearSelectionOnEmptySpaceProperty, value);
        }

        private static void OnClearSelectionChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            var treeView = sender as TreeView;
            if (treeView == null)
            {
                return;
            }

            treeView.PreviewMouseDown -= TreeViewOnPreviewMouseDown;

            if (e.NewValue is bool shouldHandle && shouldHandle)
            {
                treeView.PreviewMouseDown += TreeViewOnPreviewMouseDown;
            }
        }

        private static void TreeViewOnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            var treeView = sender as TreeView;
            if (treeView == null)
            {
                return;
            }

            try
            {
                if (IsTreeViewItem(e.OriginalSource as DependencyObject))
                {
                    return;
                }

                var selectionNodes = treeView.Items
                    .OfType<ITreeSelectionNode>()
                    .ToList();

                TreeViewSelectionHelper.ClearSelection(selectionNodes, Logger);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Unable to clear TreeView selection after mouse interaction");
            }
        }

        private static bool IsTreeViewItem(DependencyObject source)
        {
            var current = source;
            while (current != null)
            {
                if (current is TreeViewItem)
                {
                    return true;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return false;
        }
    }
}
