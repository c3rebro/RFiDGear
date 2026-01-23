using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RFiDGear.Infrastructure;

namespace RFiDGear.Services.Commands
{
    public class CommandMenuProvider : ICommandMenuProvider
    {
        public ObservableCollection<MenuItem> RowContextMenuItems { get; } = new ObservableCollection<MenuItem>();
        public ObservableCollection<MenuItem> EmptySpaceContextMenuItems { get; } = new ObservableCollection<MenuItem>();
        public ObservableCollection<MenuItem> EmptyTreeViewContextMenuItems { get; } = new ObservableCollection<MenuItem>();

        public void BuildMenus(ICommand getAddEditCommand,
            ICommand executeSelectedCommand,
            ICommand deleteSelectedCommand,
            ICommand resetSelectedCommand,
            ICommand executeAllCommand,
            ICommand resetAllCommand,
            ICommand resetReportCommand,
            ICommand readChipCommand)
        {
            EmptySpaceContextMenuItems.Clear();
            RowContextMenuItems.Clear();
            EmptyTreeViewContextMenuItems.Clear();

            EmptySpaceContextMenuItems.Add(new MenuItem()
            {
                Header = ResourceLoader.GetResource("contextMenuItemAddNewTask"),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Command = getAddEditCommand
            });

            RowContextMenuItems.Add(new MenuItem()
            {
                Header = ResourceLoader.GetResource("contextMenuItemAddNewTask"),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Command = getAddEditCommand
            });

            RowContextMenuItems.Add(new MenuItem()
            {
                Header = ResourceLoader.GetResource("contextMenuItemAddOrEditTask"),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Command = getAddEditCommand
            });

            RowContextMenuItems.Add(new MenuItem()
            {
                Header = ResourceLoader.GetResource("contextMenuItemDeleteSelectedItem"),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Command = deleteSelectedCommand
            });

            RowContextMenuItems.Add(null);

            RowContextMenuItems.Add(new MenuItem()
            {
                Header = ResourceLoader.GetResource("contextMenuItemExecuteSelectedItem"),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Command = executeSelectedCommand
            });

            RowContextMenuItems.Add(new MenuItem()
            {
                Header = ResourceLoader.GetResource("contextMenuItemResetSelectedItem"),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Command = resetSelectedCommand
            });

            RowContextMenuItems.Add(null);

            RowContextMenuItems.Add(new MenuItem()
            {
                Header = ResourceLoader.GetResource("contextMenuItemExecuteAllItems"),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Command = executeAllCommand
            });

            RowContextMenuItems.Add(new MenuItem()
            {
                Header = ResourceLoader.GetResource("contextMenuItemResetAllItems"),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Command = resetAllCommand
            });

            RowContextMenuItems.Add(new MenuItem()
            {
                Header = ResourceLoader.GetResource("contextMenuItemResetReportPath"),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Command = resetReportCommand
            });

            EmptyTreeViewContextMenuItems.Add(new MenuItem()
            {
                Header = ResourceLoader.GetResource("contextMenuItemReadChipPublic"),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Command = readChipCommand
            });
        }
    }
}
