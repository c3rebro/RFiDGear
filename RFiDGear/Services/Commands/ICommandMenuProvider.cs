using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Input;

namespace RFiDGear.Services.Commands
{
    public interface ICommandMenuProvider
    {
        ObservableCollection<MenuItem> RowContextMenuItems { get; }
        ObservableCollection<MenuItem> EmptySpaceContextMenuItems { get; }
        ObservableCollection<MenuItem> EmptyTreeViewContextMenuItems { get; }
        void BuildMenus(ICommand getAddEditCommand,
            ICommand executeSelectedCommand,
            ICommand deleteSelectedCommand,
            ICommand resetSelectedCommand,
            ICommand executeAllCommand,
            ICommand resetReportCommand,
            ICommand readChipCommand);
    }
}
