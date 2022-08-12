﻿using RFiDGear.ViewModel;
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
}