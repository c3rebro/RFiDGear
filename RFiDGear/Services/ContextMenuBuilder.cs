using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RFiDGear.Infrastructure;
using RFiDGear.Services.Interfaces;

namespace RFiDGear.Services
{
    /// <summary>
    /// Builds context menus for the task tree view.
    /// </summary>
    public class ContextMenuBuilder : IContextMenuBuilder
    {
        private readonly Func<string, string> resourceResolver;

        public ContextMenuBuilder()
            : this(ResourceLoader.GetResource)
        {
        }

        public ContextMenuBuilder(Func<string, string> resourceResolver)
        {
            this.resourceResolver = resourceResolver ?? throw new ArgumentNullException(nameof(resourceResolver));
        }

        public ObservableCollection<MenuItem> BuildNodeMenu(ICommand addNewTaskCommand, ICommand addOrEditCommand, ICommand deleteCommand, ICommand executeSelectedCommand, ICommand resetSelectedCommand, ICommand executeAllCommand, ICommand resetReportPathCommand)
        {
            var rowContextMenuItems = new ObservableCollection<MenuItem>();

            rowContextMenuItems.Add(new MenuItem
            {
                Header = resourceResolver("contextMenuItemAddNewTask"),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Command = addNewTaskCommand
            });

            rowContextMenuItems.Add(new MenuItem
            {
                Header = resourceResolver("contextMenuItemAddOrEditTask"),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Command = addOrEditCommand
            });

            rowContextMenuItems.Add(new MenuItem
            {
                Header = resourceResolver("contextMenuItemDeleteSelectedItem"),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Command = deleteCommand
            });

            rowContextMenuItems.Add(null);

            rowContextMenuItems.Add(new MenuItem
            {
                Header = resourceResolver("contextMenuItemExecuteSelectedItem"),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Command = executeSelectedCommand
            });

            rowContextMenuItems.Add(new MenuItem
            {
                Header = resourceResolver("contextMenuItemResetSelectedItem"),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Command = resetSelectedCommand
            });

            rowContextMenuItems.Add(null);

            rowContextMenuItems.Add(new MenuItem
            {
                Header = resourceResolver("contextMenuItemExecuteAllItems"),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Command = executeAllCommand
            });

            rowContextMenuItems.Add(new MenuItem
            {
                Header = resourceResolver("contextMenuItemResetReportPath"),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Command = resetReportPathCommand
            });

            return rowContextMenuItems;
        }

        public ObservableCollection<MenuItem> BuildEmptyTreeMenu(ICommand readChipCommand)
        {
            var emptySpaceTreeViewContextMenu = new ObservableCollection<MenuItem>();

            emptySpaceTreeViewContextMenu.Add(new MenuItem
            {
                Header = resourceResolver("contextMenuItemReadChipPublic"),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Command = readChipCommand
            });

            return emptySpaceTreeViewContextMenu;
        }

        public ObservableCollection<MenuItem> BuildEmptySpaceMenu(ICommand addNewTaskCommand)
        {
            var emptySpaceContextMenuItems = new ObservableCollection<MenuItem>();

            emptySpaceContextMenuItems.Add(new MenuItem
            {
                Header = resourceResolver("contextMenuItemAddNewTask"),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Command = addNewTaskCommand
            });

            return emptySpaceContextMenuItems;
        }
    }
}
