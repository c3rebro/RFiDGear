using System.Windows.Input;

namespace RFiDGear.Services.Commands
{
    public class CommandMenuBuilder : ICommandMenuBuilder
    {
        private readonly ICommandMenuProvider commandMenuProvider;

        public CommandMenuBuilder(ICommandMenuProvider commandMenuProvider)
        {
            this.commandMenuProvider = commandMenuProvider;
        }

        public CommandMenuSet BuildMenus(ICommand getAddEditCommand,
            ICommand executeSelectedCommand,
            ICommand deleteSelectedCommand,
            ICommand resetSelectedCommand,
            ICommand executeAllCommand,
            ICommand resetReportCommand,
            ICommand readChipCommand)
        {
            commandMenuProvider.BuildMenus(getAddEditCommand, executeSelectedCommand, deleteSelectedCommand, resetSelectedCommand,
                executeAllCommand, resetReportCommand, readChipCommand);

            return new CommandMenuSet(commandMenuProvider.RowContextMenuItems,
                commandMenuProvider.EmptySpaceContextMenuItems,
                commandMenuProvider.EmptyTreeViewContextMenuItems);
        }
    }
}
