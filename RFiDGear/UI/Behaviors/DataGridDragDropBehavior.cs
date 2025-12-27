using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RFiDGear.Infrastructure.Tasks.Interfaces;

namespace RFiDGear.UI.Behaviors
{
    public static class DataGridDragDropBehavior
    {
        public static readonly DependencyProperty EnableRowDragDropProperty = DependencyProperty.RegisterAttached(
            "EnableRowDragDrop",
            typeof(bool),
            typeof(DataGridDragDropBehavior),
            new PropertyMetadata(false, OnEnableRowDragDropChanged));

        private static readonly DependencyProperty DragStartPointProperty = DependencyProperty.RegisterAttached(
            "DragStartPoint",
            typeof(Point?),
            typeof(DataGridDragDropBehavior));

        public static bool GetEnableRowDragDrop(DependencyObject target)
        {
            return (bool)target.GetValue(EnableRowDragDropProperty);
        }

        public static void SetEnableRowDragDrop(DependencyObject target, bool value)
        {
            target.SetValue(EnableRowDragDropProperty, value);
        }

        private static void OnEnableRowDragDropChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            var grid = dependencyObject as DataGrid;
            if (grid == null)
            {
                return;
            }

            if ((bool)e.NewValue)
            {
                grid.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
                grid.PreviewMouseMove += OnPreviewMouseMove;
                grid.DragOver += OnDragOver;
                grid.Drop += OnDrop;
            }
            else
            {
                grid.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
                grid.PreviewMouseMove -= OnPreviewMouseMove;
                grid.DragOver -= OnDragOver;
                grid.Drop -= OnDrop;
            }
        }

        private static void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var grid = sender as DataGrid;
            if (grid != null)
            {
                grid.SetValue(DragStartPointProperty, e.GetPosition(null));
            }
        }

        private static void OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            var grid = sender as DataGrid;
            if (grid == null || e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            var dragStartPoint = grid.GetValue(DragStartPointProperty) as Point?;
            if (dragStartPoint == null)
            {
                return;
            }

            var currentPosition = e.GetPosition(null);

            if (Math.Abs(currentPosition.X - dragStartPoint.Value.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(currentPosition.Y - dragStartPoint.Value.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            if (TryGetRowFromPoint(grid, e.GetPosition(grid), out var row) && row?.Item != null)
            {
                var data = new DataObject(row.Item.GetType(), row.Item);
                DragDrop.DoDragDrop(grid, data, DragDropEffects.Move);
                grid.SetValue(DragStartPointProperty, null);
                e.Handled = true;
            }
        }

        private static void OnDragOver(object sender, DragEventArgs e)
        {
            var grid = sender as DataGrid;
            if (grid != null)
            {
                e.Effects = CanHandleDrag(grid, e.Data) ? DragDropEffects.Move : DragDropEffects.None;
                e.Handled = true;
            }
        }

        private static void OnDrop(object sender, DragEventArgs e)
        {
            var grid = sender as DataGrid;
            if (grid == null || !CanHandleDrag(grid, e.Data))
            {
                return;
            }

            var draggedItem = e.Data.GetData(e.Data.GetFormats().FirstOrDefault()) as object;
            if (draggedItem == null)
            {
                return;
            }

            TryGetRowFromPoint(grid, e.GetPosition(grid), out var targetRow);
            var targetItem = targetRow?.Item;

            MoveItem(grid, draggedItem, targetItem);
        }

        private static void MoveItem(DataGrid grid, object draggedItem, object targetItem)
        {
            var collection = grid.ItemsSource as IList ?? grid.Items as IList;
            if (collection == null)
            {
                return;
            }

            var oldIndex = collection.IndexOf(draggedItem);
            if (oldIndex < 0)
            {
                return;
            }

            var newIndex = targetItem == null ? collection.Count - 1 : collection.IndexOf(targetItem);
            if (newIndex < 0 || oldIndex == newIndex)
            {
                return;
            }

            collection.RemoveAt(oldIndex);
            if (newIndex >= collection.Count)
            {
                collection.Add(draggedItem);
            }
            else
            {
                collection.Insert(newIndex, draggedItem);
            }

            UpdateTaskIndices(collection);
            grid.SelectedItem = draggedItem;
            grid.Items.Refresh();
        }

        private static void UpdateTaskIndices(IEnumerable items)
        {
            var index = 0;
            foreach (var item in items)
            {
                if (item is IGenericTask task)
                {
                    task.CurrentTaskIndex = index.ToString(CultureInfo.CurrentCulture);
                }

                index++;
            }
        }

        private static bool CanHandleDrag(DataGrid grid, IDataObject data)
        {
            if (grid.ItemsSource is IList)
            {
                return data.GetFormats().Any(format => data.GetDataPresent(format));
            }

            return false;
        }

        private static bool TryGetRowFromPoint(DataGrid grid, Point point, out DataGridRow row)
        {
            var hitTestResult = VisualTreeHelper.HitTest(grid, point);
            row = hitTestResult?.VisualHit.FindVisualParent<DataGridRow>();
            return row != null;
        }
    }

    internal static class VisualTreeHelperExtensions
    {
        public static T FindVisualParent<T>(this DependencyObject child) where T : DependencyObject
        {
            var parentObject = VisualTreeHelper.GetParent(child);

            if (parentObject == null)
            {
                return null;
            }

            var parent = parentObject as T;
            if (parent != null)
            {
                return parent;
            }

            return FindVisualParent<T>(parentObject);
        }
    }
}
