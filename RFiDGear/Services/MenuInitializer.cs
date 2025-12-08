using System;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Input;
using RFiDGear.Services.Interfaces;

namespace RFiDGear.Services
{
    public interface IMenuInitializer
    {
        MenuInitializationResult Initialize(
            IContextMenuBuilder contextMenuBuilder,
            ICommand addEditCommand,
            ICommand deleteSelectedCommand,
            ICommand writeSelectedOnceCommand,
            ICommand resetSelectedStatusCommand,
            ICommand writeToChipOnceCommand,
            ICommand resetReportTaskDirectoryCommand,
            ICommand readChipCommand);
    }

    public class MenuInitializationResult
    {
        public MenuInitializationResult(
            ObservableCollection<MenuItem> rowContextMenuItems,
            ObservableCollection<MenuItem> emptySpaceContextMenuItems,
            ObservableCollection<MenuItem> emptySpaceTreeViewContextMenu)
        {
            RowContextMenuItems = rowContextMenuItems ?? throw new ArgumentNullException(nameof(rowContextMenuItems));
            EmptySpaceContextMenuItems = emptySpaceContextMenuItems ?? throw new ArgumentNullException(nameof(emptySpaceContextMenuItems));
            EmptySpaceTreeViewContextMenu = emptySpaceTreeViewContextMenu ?? throw new ArgumentNullException(nameof(emptySpaceTreeViewContextMenu));
        }

        public ObservableCollection<MenuItem> RowContextMenuItems { get; }

        public ObservableCollection<MenuItem> EmptySpaceContextMenuItems { get; }

        public ObservableCollection<MenuItem> EmptySpaceTreeViewContextMenu { get; }
    }

    public class MenuInitializer : IMenuInitializer
    {
        public MenuInitializationResult Initialize(
            IContextMenuBuilder contextMenuBuilder,
            ICommand addEditCommand,
            ICommand deleteSelectedCommand,
            ICommand writeSelectedOnceCommand,
            ICommand resetSelectedStatusCommand,
            ICommand writeToChipOnceCommand,
            ICommand resetReportTaskDirectoryCommand,
            ICommand readChipCommand)
        {
            if (contextMenuBuilder == null)
            {
                throw new ArgumentNullException(nameof(contextMenuBuilder));
            }

            var rowContextMenuItems = contextMenuBuilder.BuildNodeMenu(
                addEditCommand,
                addEditCommand,
                deleteSelectedCommand,
                writeSelectedOnceCommand,
                resetSelectedStatusCommand,
                writeToChipOnceCommand,
                resetReportTaskDirectoryCommand);

            var emptySpaceContextMenuItems = contextMenuBuilder.BuildEmptySpaceMenu(addEditCommand);
            var emptySpaceTreeViewContextMenu = contextMenuBuilder.BuildEmptyTreeMenu(readChipCommand);

            return new MenuInitializationResult(rowContextMenuItems, emptySpaceContextMenuItems, emptySpaceTreeViewContextMenu);
        }
    }
}
