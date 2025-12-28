using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.Tasks.Interfaces;
using RFiDGear.UI.MVVMDialogs.ViewModels;
using RFiDGear.UI.MVVMDialogs.ViewModels.Interfaces;
using RFiDGear.ViewModel;

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

            if (!TryEnsureDragDropAllowed(grid, draggedItem))
            {
                return;
            }

            MoveItem(grid, draggedItem, targetItem);
        }

        private static void MoveItem(DataGrid grid, object draggedItem, object targetItem)
        {
            if (!TryEnsureDragDropAllowed(grid, draggedItem))
            {
                return;
            }

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

        /// <summary>
        /// Blocks drag-drop operations when the dragged task has an execute condition set.
        /// </summary>
        /// <param name="grid">The data grid that initiated the drag-drop.</param>
        /// <param name="draggedItem">The dragged item instance.</param>
        /// <returns><see langword="true"/> when drag-drop can proceed; otherwise <see langword="false"/>.</returns>
        private static bool TryEnsureDragDropAllowed(DataGrid grid, object draggedItem)
        {
            if (draggedItem is not IGenericTask task)
            {
                return true;
            }

            var dialogs = TryGetDialogCollection(grid);
            return !TryBlockDragDropWithDialog(task, dialogs);
        }

        /// <summary>
        /// Determines whether a task has an execute condition configured.
        /// </summary>
        /// <param name="task">The task to inspect.</param>
        /// <returns><see langword="true"/> when an execute condition is set; otherwise <see langword="false"/>.</returns>
        internal static bool HasExecuteCondition(IGenericTask task)
        {
            return task != null &&
                   (!string.IsNullOrWhiteSpace(task.SelectedExecuteConditionTaskIndex) ||
                    task.SelectedExecuteConditionErrorLevel != ERROR.Empty);
        }

        /// <summary>
        /// Shows a dialog and blocks drag-drop when the task has an execute condition configured.
        /// </summary>
        /// <param name="task">The task being dragged.</param>
        /// <param name="dialogs">The dialog collection used by the main window.</param>
        /// <returns><see langword="true"/> when drag-drop should be blocked; otherwise <see langword="false"/>.</returns>
        internal static bool TryBlockDragDropWithDialog(IGenericTask task, ObservableCollection<IDialogViewModel> dialogs)
        {
            if (!HasExecuteCondition(task))
            {
                return false;
            }

            dialogs?.Add(new CustomDialogViewModel
            {
                Caption = ResourceLoader.GetResource("messageBoxDefaultCaption"),
                Message = ResourceLoader.GetResource("messageDragDropExecuteConditionBlocked"),
                OnOk = sender => sender.Close()
            });

            return true;
        }

        private static ObservableCollection<IDialogViewModel> TryGetDialogCollection(DataGrid grid)
        {
            var mainWindowViewModel = grid?.DataContext as MainWindowViewModel
                ?? Application.Current?.MainWindow?.DataContext as MainWindowViewModel;

            return mainWindowViewModel?.Dialogs;
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
