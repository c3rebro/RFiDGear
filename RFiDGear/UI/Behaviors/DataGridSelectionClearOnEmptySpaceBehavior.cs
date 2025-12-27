using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Serilog;

namespace RFiDGear.UI.Behaviors
{
    public static class DataGridSelectionClearOnEmptySpaceBehavior
    {
        public static readonly DependencyProperty ClearSelectionOnEmptySpaceProperty = DependencyProperty.RegisterAttached(
            "ClearSelectionOnEmptySpace",
            typeof(bool),
            typeof(DataGridSelectionClearOnEmptySpaceBehavior),
            new PropertyMetadata(false, OnClearSelectionChanged));

        private static readonly ILogger Logger = Log.ForContext(typeof(DataGridSelectionClearOnEmptySpaceBehavior));

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
            var grid = sender as DataGrid;
            if (grid == null)
            {
                return;
            }

            grid.PreviewMouseDown -= DataGridOnPreviewMouseDown;
            grid.ContextMenuOpening -= DataGridOnContextMenuOpening;

            if (e.NewValue is bool shouldHandle && shouldHandle)
            {
                grid.PreviewMouseDown += DataGridOnPreviewMouseDown;
                grid.ContextMenuOpening += DataGridOnContextMenuOpening;
            }
        }

        private static void DataGridOnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            var grid = sender as DataGrid;
            if (grid == null)
            {
                return;
            }

            TryClearSelection(grid, e.OriginalSource as DependencyObject);
        }

        private static void DataGridOnContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            var grid = sender as DataGrid;
            if (grid == null)
            {
                return;
            }

            TryClearSelection(grid, e.OriginalSource as DependencyObject);
        }

        private static void TryClearSelection(DataGrid grid, DependencyObject? source)
        {
            if (IsDataGridRow(source))
            {
                return;
            }

            if (grid.SelectedItem == null && grid.SelectedItems.Count == 0)
            {
                return;
            }

            try
            {
                grid.UnselectAll();
                grid.SelectedItem = null;
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Unable to clear DataGrid selection after mouse interaction");
            }
        }

        private static bool IsDataGridRow(DependencyObject? source)
        {
            var current = source;
            while (current != null)
            {
                if (current is DataGridRow)
                {
                    return true;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return false;
        }
    }
}
