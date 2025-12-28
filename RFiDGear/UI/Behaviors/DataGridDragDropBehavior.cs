using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RFiDGear.Infrastructure.Tasks.Interfaces;
using RFiDGear.ViewModel.TaskSetupViewModels;

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

            if (!TryAssignTaskIndexForDrop(collection, draggedItem, newIndex, out var errorMessage))
            {
                collection.Remove(draggedItem);

                if (oldIndex >= collection.Count)
                {
                    collection.Add(draggedItem);
                }
                else
                {
                    collection.Insert(oldIndex, draggedItem);
                }

                MessageBox.Show(errorMessage, "Task Index Update", MessageBoxButton.OK, MessageBoxImage.Warning);
                grid.SelectedItem = draggedItem;
                grid.Items.Refresh();
                return;
            }

            grid.SelectedItem = draggedItem;
            grid.Items.Refresh();
        }

        /// <summary>
        /// Attempts to assign a new task index to the dragged item based on its neighbors.
        /// </summary>
        /// <param name="items">The reordered task collection.</param>
        /// <param name="draggedItem">The task being moved.</param>
        /// <param name="targetIndex">The new position in the collection.</param>
        /// <param name="errorMessage">The error message to show when no valid index is available.</param>
        /// <returns><see langword="true"/> when the task index was updated; otherwise <see langword="false"/>.</returns>
        internal static bool TryAssignTaskIndexForDrop(IList items, object draggedItem, int targetIndex, out string errorMessage)
        {
            if (draggedItem is not IGenericTask draggedTask)
            {
                errorMessage = "The dragged item does not support task indexing.";
                return false;
            }

            var taskCollection = items as ObservableCollection<object> ?? new ObservableCollection<object>(items.Cast<object>());

            var hasPreviousIndex = TryGetNeighborIndex(items, targetIndex - 1, out var previousIndex, out var previousExists);
            if (previousExists && !hasPreviousIndex)
            {
                errorMessage = "Unable to read the previous task index.";
                return false;
            }

            var hasNextIndex = TryGetNeighborIndex(items, targetIndex + 1, out var nextIndex, out var nextExists);
            if (nextExists && !hasNextIndex)
            {
                errorMessage = "Unable to read the next task index.";
                return false;
            }

            if (hasPreviousIndex && hasNextIndex)
            {
                if (previousIndex >= nextIndex - 1)
                {
                    errorMessage = "No available task index between the surrounding tasks.";
                    return false;
                }

                return TryAssignTaskIndexInRange(draggedTask, taskCollection, previousIndex + 1, nextIndex - 1, out errorMessage);
            }

            if (!hasPreviousIndex && hasNextIndex)
            {
                if (nextIndex <= 0)
                {
                    errorMessage = "No available task index before the target task.";
                    return false;
                }

                return TryAssignTaskIndexInRange(draggedTask, taskCollection, 0, nextIndex - 1, out errorMessage);
            }

            if (hasPreviousIndex && !hasNextIndex)
            {
                var candidate = previousIndex + 1;
                return TryAssignTaskIndexCandidate(draggedTask, taskCollection, candidate, out errorMessage);
            }

            return TryAssignTaskIndexCandidate(draggedTask, taskCollection, 0, out errorMessage);
        }

        /// <summary>
        /// Attempts to assign the first valid task index within the provided range.
        /// </summary>
        /// <param name="draggedTask">The task being moved.</param>
        /// <param name="taskCollection">The collection used for duplicate validation.</param>
        /// <param name="minCandidate">The first candidate index.</param>
        /// <param name="maxCandidate">The last candidate index.</param>
        /// <param name="errorMessage">The error message to show when no valid index is available.</param>
        /// <returns><see langword="true"/> when the task index was updated; otherwise <see langword="false"/>.</returns>
        private static bool TryAssignTaskIndexInRange(IGenericTask draggedTask, ObservableCollection<object> taskCollection, int minCandidate, int maxCandidate, out string errorMessage)
        {
            for (var candidate = minCandidate; candidate <= maxCandidate; candidate++)
            {
                if (TryAssignTaskIndexCandidate(draggedTask, taskCollection, candidate, out errorMessage))
                {
                    return true;
                }
            }

            errorMessage = "No available task index between the surrounding tasks.";
            return false;
        }

        /// <summary>
        /// Attempts to assign a specific task index after validation.
        /// </summary>
        /// <param name="draggedTask">The task being moved.</param>
        /// <param name="taskCollection">The collection used for duplicate validation.</param>
        /// <param name="candidateIndex">The candidate task index.</param>
        /// <param name="errorMessage">The error message to show when validation fails.</param>
        /// <returns><see langword="true"/> when the task index was updated; otherwise <see langword="false"/>.</returns>
        private static bool TryAssignTaskIndexCandidate(IGenericTask draggedTask, ObservableCollection<object> taskCollection, int candidateIndex, out string errorMessage)
        {
            if (candidateIndex < 0)
            {
                errorMessage = "Task index must be non-negative.";
                return false;
            }

            var candidateText = candidateIndex.ToString(CultureInfo.CurrentCulture);
            if (!TaskIndexValidation.TryValidateTaskIndex(candidateText, taskCollection, draggedTask, out errorMessage))
            {
                return false;
            }

            draggedTask.CurrentTaskIndex = candidateText;
            errorMessage = null;
            return true;
        }

        private static bool TryGetNeighborIndex(IList items, int neighborPosition, out int taskIndex, out bool neighborExists)
        {
            taskIndex = -1;
            neighborExists = neighborPosition >= 0 && neighborPosition < items.Count;
            if (!neighborExists)
            {
                return false;
            }

            return TryGetTaskIndex(items[neighborPosition], out taskIndex);
        }

        private static bool TryGetTaskIndex(object item, out int taskIndex)
        {
            if (item is IGenericTask task && int.TryParse(task.CurrentTaskIndex, out taskIndex))
            {
                return true;
            }

            taskIndex = -1;
            return false;
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
