using RFiDGear.ViewModel;

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System;
using RFiDGear.Model;

namespace RFiDGear
{
    /// <summary>
    /// Description of MainForm.
    /// </summary>
    ///
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            this.MaxHeight = (uint)SystemParameters.MaximizedPrimaryScreenHeight-8;
        }

        private void WindowMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                DragMove();
            }
            catch { } // workaround wpfui windowbar probl.
        }

        private void OnAutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            e.Column.Header = ((PropertyDescriptor)e.PropertyDescriptor).DisplayName;
        }

        private void MainWindowTreeViewControlMouseButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (sender != null)
                {
                    var item = sender as TreeView;

                    var dep = (DependencyObject)e.OriginalSource;
                    while ((dep != null) && !(dep is System.Windows.Controls.TreeViewItem))
                    {
                        dep = VisualTreeHelper.GetParent(dep);
                    }
                    if (dep == null)
                    {
                        foreach (var o in item.Items)
                        {
                            if (o is RFiDChipParentLayerViewModel && (o as RFiDChipParentLayerViewModel).Children != null)
                            {
                                foreach (var child in (o as RFiDChipParentLayerViewModel).Children)
                                {
                                    child.IsSelected = false;

                                    if (child.Children != null)
                                    {
                                        foreach (var grandChild in child.Children)
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
            //Missing Visual implementation in "InitOnFirstRun" Method of Textblock
            catch
            {
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
            var grid = sender as System.Windows.Controls.DataGrid;
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
                    grid.ScrollIntoView(grid.SelectedItem, grid.Columns[0]);
                    
                }
            });

            // Works with .Net 4.0
            grid.Dispatcher.BeginInvoke((Action)(() =>
            {
                grid.UpdateLayout();
                if (grid.SelectedItem != null)
                {
                    grid.ScrollIntoView(grid.SelectedItem, grid.Columns[0]);
                }
            }));
        }
    }
}