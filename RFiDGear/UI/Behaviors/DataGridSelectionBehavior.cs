using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using RFiDGear.UI.Selection;
using Serilog;

namespace RFiDGear.UI.Behaviors
{
    public static class DataGridSelectionBehavior
    {
        public static readonly DependencyProperty SelectingItemProperty = DependencyProperty.RegisterAttached(
            "SelectingItem",
            typeof(object),
            typeof(DataGridSelectionBehavior),
            new PropertyMetadata(default, OnSelectingItemChanged));

        private static readonly ILogger Logger = Log.ForContext(typeof(DataGridSelectionBehavior));

        public static object GetSelectingItem(DependencyObject target)
        {
            return target.GetValue(SelectingItemProperty);
        }

        public static void SetSelectingItem(DependencyObject target, object value)
        {
            target.SetValue(SelectingItemProperty, value);
        }

        private static void OnSelectingItemChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            var grid = sender as DataGrid;
            if (grid == null || grid.SelectedItem == null || !grid.Columns.Any())
            {
                return;
            }

            void PerformScroll()
            {
                grid.UpdateLayout();
                if (grid.SelectedItem != null)
                {
                    grid.ScrollIntoView(grid.SelectedItem, grid.Columns[0]);
                }
            }
            SelectionScrollHelper.ScrollSelection(
                () => grid.SelectedItem,
                () =>
                {
                    grid.Dispatcher.InvokeAsync(PerformScroll);
                },
                Logger);

            SelectionScrollHelper.ScrollSelection(
                () => grid.SelectedItem,
                () =>
                {
                    grid.Dispatcher.BeginInvoke((Action)PerformScroll);
                },
                Logger);
        }
    }
}
