using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Input;

namespace RFiDGear.Services.Interfaces
{
    /// <summary>
    /// Creates context menus for the main window tree interactions.
    /// </summary>
    public interface IContextMenuBuilder
    {
        ObservableCollection<MenuItem> BuildNodeMenu(ICommand addNewTaskCommand, ICommand addOrEditCommand, ICommand deleteCommand, ICommand executeSelectedCommand, ICommand resetSelectedCommand, ICommand executeAllCommand, ICommand resetReportPathCommand);

        ObservableCollection<MenuItem> BuildEmptyTreeMenu(ICommand readChipCommand);

        ObservableCollection<MenuItem> BuildEmptySpaceMenu(
            ICommand createGenericTaskCommand,
            ICommand createGenericChipTaskCommand,
            ICommand createClassicTaskCommand,
            ICommand createDesfireTaskCommand,
            ICommand createUltralightTaskCommand);
    }
}
