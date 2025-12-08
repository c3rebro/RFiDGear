using Microsoft.VisualStudio.TestTools.UnitTesting;
using RFiDGear.Services;
using System.Windows.Controls;
using System.Windows.Input;

namespace RFiDGear.Tests.Services
{
    [TestClass]
    public class ContextMenuBuilderIntegrationTests
    {
        [TestMethod]
        public void BuildNodeMenu_ProducesExpectedItems()
        {
            var builder = new ContextMenuBuilder(key => key);
            var command = new DummyCommand();

            var menu = builder.BuildNodeMenu(command, command, command, command, command, command, command);

            Assert.AreEqual(9, menu.Count);
            Assert.AreEqual("contextMenuItemAddNewTask", (menu[0] as MenuItem).Header);
            Assert.AreEqual("contextMenuItemAddOrEditTask", (menu[1] as MenuItem).Header);
            Assert.IsNull(menu[3]);
        }

        [TestMethod]
        public void BuildEmptyMenus_ReturnsSingleAction()
        {
            var builder = new ContextMenuBuilder(key => key);
            var command = new DummyCommand();

            var emptyTree = builder.BuildEmptyTreeMenu(command);
            var emptySpace = builder.BuildEmptySpaceMenu(command);

            Assert.AreEqual(1, emptyTree.Count);
            Assert.AreEqual("contextMenuItemReadChipPublic", (emptyTree[0] as MenuItem).Header);
            Assert.AreEqual(1, emptySpace.Count);
            Assert.AreEqual("contextMenuItemAddNewTask", (emptySpace[0] as MenuItem).Header);
        }

        private class DummyCommand : ICommand
        {
            public event System.EventHandler CanExecuteChanged;

            public bool CanExecute(object parameter) => true;

            public void Execute(object parameter)
            {
            }
        }
    }
}
