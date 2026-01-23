using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Input;

namespace RFiDGear.Services.Commands
{
    public interface ICommandMenuBuilder
    {
        CommandMenuSet BuildMenus(ICommand getAddEditCommand,
            ICommand executeSelectedCommand,
            ICommand deleteSelectedCommand,
            ICommand resetSelectedCommand,
            ICommand executeAllCommand,
            ICommand resetAllCommand,
            ICommand resetReportCommand,
            ICommand readChipCommand);
    }

    public class CommandMenuSet
    {
        public CommandMenuSet(ObservableCollection<MenuItem> rowContextMenuItems,
            ObservableCollection<MenuItem> emptySpaceContextMenuItems,
            ObservableCollection<MenuItem> emptyTreeViewContextMenuItems)
        {
            RowContextMenuItems = rowContextMenuItems;
            EmptySpaceContextMenuItems = emptySpaceContextMenuItems;
            EmptyTreeViewContextMenuItems = emptyTreeViewContextMenuItems;
        }

        public ObservableCollection<MenuItem> RowContextMenuItems { get; }

        public ObservableCollection<MenuItem> EmptySpaceContextMenuItems { get; }

        public ObservableCollection<MenuItem> EmptyTreeViewContextMenuItems { get; }
    }
}
