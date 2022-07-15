using RFiDGear.ViewModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;

using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using System.Windows.Interactivity;

namespace RFiDGear
{
    //	public delegate void TreeViewNodeMouseAction(object sender, TreeNodeMouseClickEventArgs e);

    /// <summary>
    /// Description of MainForm.
    /// </summary>
    ///
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {

        public MainWindow()
        {
            InitializeComponent();
            // if(!DataGridBehavior.GetAutoscroll(myDataGrid))
            //   DataGridBehavior.SetAutoscroll(myDataGrid, true);
        }

        private void WindowMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void OnAutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            e.Column.Header = ((PropertyDescriptor)e.PropertyDescriptor).DisplayName;
        }

        private void MainWindowTreeViewControlMouseButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender != null)
            {
                TreeView item = sender as TreeView;

                DependencyObject dep = (DependencyObject)e.OriginalSource;
                while ((dep != null) && !(dep is TreeViewItem))
                {
                    dep = VisualTreeHelper.GetParent(dep);
                }
                if (dep == null)
                {
                    foreach (object o in item.Items)
                    {
                        if (o is RFiDChipParentLayerViewModel && (o as RFiDChipParentLayerViewModel).Children != null)
                        {
                            foreach (RFiDChipChildLayerViewModel child in (o as RFiDChipParentLayerViewModel).Children)
                            {
                                child.IsSelected = false;

                                if (child.Children != null)
                                {
                                    foreach (RFiDChipGrandChildLayerViewModel grandChild in child.Children)
                                    {
                                        grandChild.IsSelected = false;
                                    }
                                }
                            }

                            (o as RFiDChipParentLayerViewModel).IsSelected = false;
                        }

                    }
                    return;
                }
            }
        }
    }

    public class SelectingItemAttachedProperty
    {
        public static readonly DependencyProperty SelectingItemProperty = DependencyProperty.RegisterAttached(
            "SelectingItem",
            typeof(object),
            typeof(SelectingItemAttachedProperty),
            new PropertyMetadata(default(object), OnSelectingItemChanged));

        public static object GetSelectingItem(DependencyObject target)
        {
            return (object)target.GetValue(SelectingItemProperty);
        }

        public static void SetSelectingItem(DependencyObject target, object value)
        {
            target.SetValue(SelectingItemProperty, value);
        }

        static void OnSelectingItemChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            var grid = sender as DataGrid;
            if (grid == null || grid.SelectedItem == null)
            {
                return;
            }

            // Works with .Net 4.5
            grid.Dispatcher.InvokeAsync(() =>
            {
                grid.UpdateLayout();

                if (grid.SelectedItem != null)
                {
                    grid.ScrollIntoView(grid.SelectedItem, null);
                }
            });

            // Works with .Net 4.0
            grid.Dispatcher.BeginInvoke((Action)(() =>
            {
                grid.UpdateLayout();
                if (grid.SelectedItem != null)
                {
                    grid.ScrollIntoView(grid.SelectedItem, null);
                }
            }));
        }
    }
    /*
    public static class DataGridBehavior
        {
            public static readonly DependencyProperty AutoscrollProperty = DependencyProperty.RegisterAttached(
                "Autoscroll", typeof(bool), typeof(DataGridBehavior), new PropertyMetadata(default(bool), AutoscrollChangedCallback));

            public static readonly DependencyProperty ScrollToPositionProperty = DependencyProperty.RegisterAttached(
                "ScrollPosition", typeof(int), typeof(DataGridBehavior), new PropertyMetadata(default(int), ScrollPositionChangedCallback));

        private static readonly Dictionary<DataGrid, NotifyCollectionChangedEventHandler> handlersDict = new Dictionary<DataGrid, NotifyCollectionChangedEventHandler>();

        private static void AutoscrollChangedCallback(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
            {
                var dataGrid = dependencyObject as DataGrid;
                if (dataGrid == null)
                {
                    throw new InvalidOperationException("Dependency object is not DataGrid.");
                }

                if ((bool)args.NewValue)
                {
                    Subscribe(dataGrid);
                    dataGrid.Unloaded += DataGridOnUnloaded;
                    dataGrid.Loaded += DataGridOnLoaded;
                }
                else
                {
                    Unsubscribe(dataGrid);
                    dataGrid.Unloaded -= DataGridOnUnloaded;
                    dataGrid.Loaded -= DataGridOnLoaded;
                }
            }

        private static void ScrollPositionChangedCallback(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
        {
            var dataGrid = dependencyObject as DataGrid;
            if (dataGrid == null)
            {
                throw new InvalidOperationException("Dependency object is not DataGrid.");
            }

            if ((bool)args.NewValue)
            {
                Subscribe(dataGrid);
                dataGrid.Unloaded += DataGridOnUnloaded;
                dataGrid.Loaded += DataGridOnLoaded;
            }
            else
            {
                Unsubscribe(dataGrid);
                dataGrid.Unloaded -= DataGridOnUnloaded;
                dataGrid.Loaded -= DataGridOnLoaded;
            }
        }

        private static void Subscribe(DataGrid dataGrid)
            {
                var handler = new NotifyCollectionChangedEventHandler((sender, eventArgs) => ScrollToEnd(dataGrid));
                if(!handlersDict.ContainsKey(dataGrid))
                    handlersDict.Add(dataGrid, handler);
                ((INotifyCollectionChanged)dataGrid.Items).CollectionChanged += handler;
                ScrollToEnd(dataGrid);
            }

        private static void Unsubscribe(DataGrid dataGrid)
            {
                NotifyCollectionChangedEventHandler handler;
                handlersDict.TryGetValue(dataGrid, out handler);
                if (handler == null)
                {
                    return;
                }
                ((INotifyCollectionChanged)dataGrid.Items).CollectionChanged -= handler;
                handlersDict.Remove(dataGrid);
            }

        private static void DataGridOnLoaded(object sender, RoutedEventArgs routedEventArgs)
        {
            var dataGrid = (DataGrid)sender;
            if (GetAutoscroll(dataGrid))
            {
                Subscribe(dataGrid);
            }
        }

        private static void DataGridOnUnloaded(object sender, RoutedEventArgs routedEventArgs)
        {
            var dataGrid = (DataGrid)sender;
            if (GetAutoscroll(dataGrid))
            {
                Unsubscribe(dataGrid);
            }
        }

        private static void ScrollToEnd(DataGrid datagrid)
        {
            if (datagrid.Items.Count == 0)
            {
                return;
            }

            try
            {
                datagrid.ScrollIntoView(datagrid.SelectedIndex != -1 ? datagrid.Items[datagrid.SelectedIndex] : null);
            }
            catch { }
            
        }

        private static void ScrollToPosition(DataGrid datagrid)
        {
            if (datagrid.Items.Count == 0)
            {
                return;
            }
            datagrid.ScrollIntoView(datagrid.Items[1]);
        }

        public static void SetScrollPosition(DependencyObject element, int value)
        {
            element.SetValue(ScrollToPositionProperty, value);
        }

        public static void SetAutoscroll(DependencyObject element, bool value)
        {
            element.SetValue(AutoscrollProperty, value);
        }

        public static bool GetAutoscroll(DependencyObject element)
        {
            return (bool)element.GetValue(AutoscrollProperty);
        }
        }
    */
}