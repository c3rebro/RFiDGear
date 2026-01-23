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

        public ObservableCollection<MenuItem> BuildNodeMenu(
            ICommand addNewTaskCommand,
            ICommand addOrEditCommand,
            ICommand deleteCommand,
            ICommand executeSelectedCommand,
            ICommand resetSelectedCommand,
            ICommand executeAllCommand,
            ICommand resetAllCommand,
            ICommand resetReportPathCommand)
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
                Header = resourceResolver("contextMenuItemResetAllItems"),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Command = resetAllCommand
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

        public ObservableCollection<MenuItem> BuildEmptySpaceMenu(
            ICommand createGenericTaskCommand,
            ICommand createGenericChipTaskCommand,
            ICommand createClassicTaskCommand,
            ICommand createDesfireTaskCommand,
            ICommand createUltralightTaskCommand)
        {
            var emptySpaceContextMenuItems = new ObservableCollection<MenuItem>();

            emptySpaceContextMenuItems.Add(BuildCreateTaskMenu(
                createGenericTaskCommand,
                createGenericChipTaskCommand,
                createClassicTaskCommand,
                createDesfireTaskCommand,
                createUltralightTaskCommand));

            return emptySpaceContextMenuItems;
        }

        private MenuItem BuildCreateTaskMenu(
            ICommand createGenericTaskCommand,
            ICommand createGenericChipTaskCommand,
            ICommand createClassicTaskCommand,
            ICommand createDesfireTaskCommand,
            ICommand createUltralightTaskCommand)
        {
            var createTaskMenu = new MenuItem
            {
                Header = resourceResolver("menuItemCreateTaskHeader"),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };

            createTaskMenu.Items.Add(new MenuItem
            {
                Header = resourceResolver("menuItemCreateGenericTaskHeader"),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Command = createGenericTaskCommand
            });

            createTaskMenu.Items.Add(new MenuItem
            {
                Header = resourceResolver("menuItemCreateGenericChipTaskHeader"),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Command = createGenericChipTaskCommand
            });

            var mifareMenu = new MenuItem
            {
                Header = resourceResolver("menuItemMifareHeader"),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };

            mifareMenu.Items.Add(new MenuItem
            {
                Header = resourceResolver("menuItemAddEditMifareClassicTaskHeader"),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Command = createClassicTaskCommand
            });

            mifareMenu.Items.Add(new MenuItem
            {
                Header = resourceResolver("menuItemAddEditMifareDesfireTaskHeader"),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Command = createDesfireTaskCommand
            });

            mifareMenu.Items.Add(new MenuItem
            {
                Header = resourceResolver("menuItemAddEditMifarePlusTaskHeader"),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                IsEnabled = false
            });

            mifareMenu.Items.Add(new MenuItem
            {
                Header = resourceResolver("menuItemAddEditMifareSAMTaskHeader"),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                IsEnabled = false
            });

            mifareMenu.Items.Add(new MenuItem
            {
                Header = resourceResolver("menuItemAddEditMifareUltralightTaskHeader"),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Command = createUltralightTaskCommand
            });

            createTaskMenu.Items.Add(mifareMenu);

            var tagItMenu = new MenuItem
            {
                Header = resourceResolver("menuItemTagItHeader"),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                IsEnabled = false
            };

            tagItMenu.Items.Add(new MenuItem
            {
                Header = resourceResolver("menuItemAddEditTagitHFIPlusTaskHeader"),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                IsEnabled = false
            });

            createTaskMenu.Items.Add(tagItMenu);

            return createTaskMenu;
        }
    }
}
