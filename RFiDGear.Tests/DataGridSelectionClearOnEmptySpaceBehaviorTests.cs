using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using RFiDGear.UI.Behaviors;
using Xunit;

namespace RFiDGear.Tests
{
    public class DataGridSelectionClearOnEmptySpaceBehaviorTests
    {
        [Fact]
        public async Task PreviewRightClickOnEmptySpace_ClearsSelection()
        {
            await RunOnStaThreadAsync(() =>
            {
                if (Application.Current == null)
                {
                    new Application();
                }

                var items = new ObservableCollection<string> { "First", "Second" };
                var grid = BuildDataGrid(items);
                var window = new Window { Content = grid, Width = 300, Height = 200 };

                window.Show();
                grid.SelectedItem = items[0];
                grid.UpdateLayout();

                RaisePreviewMouseDown(grid, MouseButton.Right);

                Assert.Null(grid.SelectedItem);

                window.Close();
            });
        }

        [Fact]
        public async Task EmptySpaceContextMenu_DoesNotEnterEditMode()
        {
            await RunOnStaThreadAsync(() =>
            {
                if (Application.Current == null)
                {
                    new Application();
                }

                var items = new ObservableCollection<string> { "First", "Second" };
                var grid = BuildDataGrid(items);
                var window = new Window { Content = grid, Width = 300, Height = 200 };

                window.Show();
                grid.SelectedItem = items[0];
                grid.UpdateLayout();

                var row = (DataGridRow)grid.ItemContainerGenerator.ContainerFromItem(items[0]);
                Assert.NotNull(row);
                Assert.False(row.IsEditing);

                RaiseContextMenuOpening(grid);

                Assert.Null(grid.SelectedItem);
                Assert.False(row.IsEditing);

                window.Close();
            });
        }

        private static DataGrid BuildDataGrid(ObservableCollection<string> items)
        {
            var grid = new DataGrid
            {
                AutoGenerateColumns = false,
                ItemsSource = items
            };

            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Name",
                Binding = new System.Windows.Data.Binding()
            });

            DataGridSelectionClearOnEmptySpaceBehavior.SetClearSelectionOnEmptySpace(grid, true);

            return grid;
        }

        private static void RaisePreviewMouseDown(UIElement element, MouseButton button)
        {
            var args = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, button)
            {
                RoutedEvent = UIElement.PreviewMouseDownEvent,
                Source = element
            };

            element.RaiseEvent(args);
        }

        private static void RaiseContextMenuOpening(UIElement element)
        {
            var args = CreateContextMenuEventArgs(element);
            args.RoutedEvent = FrameworkElement.ContextMenuOpeningEvent;
            args.Source = element;

            element.RaiseEvent(args);
        }

        private static ContextMenuEventArgs CreateContextMenuEventArgs(UIElement element)
        {
            var routedEvent = FrameworkElement.ContextMenuOpeningEvent;
            var constructors = typeof(ContextMenuEventArgs).GetConstructors();

            foreach (var constructor in constructors)
            {
                var parameters = constructor.GetParameters();
                if (parameters.Length == 1 && parameters[0].ParameterType == typeof(RoutedEvent))
                {
                    return (ContextMenuEventArgs)constructor.Invoke(new object[] { routedEvent });
                }

                if (parameters.Length == 2)
                {
                    if (parameters[0].ParameterType == typeof(RoutedEvent)
                        && parameters[1].ParameterType == typeof(object))
                    {
                        return (ContextMenuEventArgs)constructor.Invoke(new object[] { routedEvent, element });
                    }

                    if (parameters[0].ParameterType == typeof(object)
                        && parameters[1].ParameterType == typeof(bool))
                    {
                        return (ContextMenuEventArgs)constructor.Invoke(new object[] { element, true });
                    }
                }

                if (parameters.Length == 3
                    && parameters[0].ParameterType == typeof(RoutedEvent)
                    && parameters[1].ParameterType == typeof(object)
                    && parameters[2].ParameterType == typeof(bool))
                {
                    return (ContextMenuEventArgs)constructor.Invoke(new object[] { routedEvent, element, true });
                }
            }

            throw new InvalidOperationException("Unable to locate a ContextMenuEventArgs constructor.");
        }

        private static Task RunOnStaThreadAsync(Action action)
        {
            var tcs = new TaskCompletionSource<object>();

            var thread = new Thread(() =>
            {
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());

                Dispatcher.CurrentDispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        action();
                        tcs.SetResult(null);
                    }
                    catch (Exception ex)
                    {
                        tcs.SetException(ex);
                    }
                    finally
                    {
                        Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
                    }
                });

                Dispatcher.Run();
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();

            return tcs.Task;
        }
    }
}
