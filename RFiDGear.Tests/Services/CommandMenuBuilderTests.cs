using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RFiDGear.Services.Commands;

namespace RFiDGear.Tests.Services
{
    [TestClass]
    public class CommandMenuBuilderTests
    {
        [TestMethod]
        public void BuildMenus_ReturnsProviderCollections()
        {
            var provider = new FakeCommandMenuProvider();
            var builder = new CommandMenuBuilder(provider);

            var command = new RoutedCommand();
            var menus = builder.BuildMenus(command, command, command, command, command, command, command);

            Assert.AreSame(provider.RowContextMenuItems, menus.RowContextMenuItems);
            Assert.AreSame(provider.EmptySpaceContextMenuItems, menus.EmptySpaceContextMenuItems);
            Assert.AreSame(provider.EmptyTreeViewContextMenuItems, menus.EmptyTreeViewContextMenuItems);
            Assert.AreEqual(command, provider.LastAddEditCommand);
        }

        private class FakeCommandMenuProvider : ICommandMenuProvider
        {
            public ObservableCollection<MenuItem> RowContextMenuItems { get; } = new ObservableCollection<MenuItem>();
            public ObservableCollection<MenuItem> EmptySpaceContextMenuItems { get; } = new ObservableCollection<MenuItem>();
            public ObservableCollection<MenuItem> EmptyTreeViewContextMenuItems { get; } = new ObservableCollection<MenuItem>();

            public ICommand LastAddEditCommand { get; private set; }

            public void BuildMenus(ICommand getAddEditCommand, ICommand executeSelectedCommand, ICommand deleteSelectedCommand, ICommand resetSelectedCommand, ICommand executeAllCommand, ICommand resetReportCommand, ICommand readChipCommand)
            {
                RowContextMenuItems.Add(new MenuItem { Command = getAddEditCommand });
                EmptySpaceContextMenuItems.Add(new MenuItem { Command = executeSelectedCommand });
                EmptyTreeViewContextMenuItems.Add(new MenuItem { Command = readChipCommand });
                LastAddEditCommand = getAddEditCommand;
            }
        }
    }
}
