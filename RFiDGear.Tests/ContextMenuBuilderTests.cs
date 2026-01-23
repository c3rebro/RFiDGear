using System.Threading.Tasks;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.Input;
using RFiDGear.Infrastructure;
using RFiDGear.Services;
using Xunit;

namespace RFiDGear.Tests
{
    public class ContextMenuBuilderTests
    {
        [Fact]
        public async Task BuildNodeMenu_BuildsResetAllItemWithCommand()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var addNewTaskCommand = new RelayCommand(() => { });
                var addEditTaskCommand = new RelayCommand(() => { });
                var deleteCommand = new RelayCommand(() => { });
                var executeSelectedCommand = new RelayCommand(() => { });
                var resetSelectedCommand = new RelayCommand(() => { });
                var executeAllCommand = new RelayCommand(() => { });
                var resetAllCommand = new RelayCommand(() => { });
                var resetReportPathCommand = new RelayCommand(() => { });

                var builder = new ContextMenuBuilder(key => key);

                var menuItems = builder.BuildNodeMenu(
                    addNewTaskCommand,
                    addEditTaskCommand,
                    deleteCommand,
                    executeSelectedCommand,
                    resetSelectedCommand,
                    executeAllCommand,
                    resetAllCommand,
                    resetReportPathCommand);

                var executeAllItem = Assert.IsType<MenuItem>(menuItems[7]);
                Assert.Equal("contextMenuItemExecuteAllItems", executeAllItem.Header);
                Assert.Same(executeAllCommand, executeAllItem.Command);

                var resetAllItem = Assert.IsType<MenuItem>(menuItems[8]);
                Assert.Equal("contextMenuItemResetAllItems", resetAllItem.Header);
                Assert.Same(resetAllCommand, resetAllItem.Command);

                var resetReportItem = Assert.IsType<MenuItem>(menuItems[9]);
                Assert.Equal("contextMenuItemResetReportPath", resetReportItem.Header);
                Assert.Same(resetReportPathCommand, resetReportItem.Command);
            });
        }

        [Fact]
        public async Task BuildEmptySpaceMenu_BuildsCreateTaskSubMenuWithCommands()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var createGenericTaskCommand = new RelayCommand(() => { });
                var createGenericChipTaskCommand = new RelayCommand(() => { });
                var createClassicTaskCommand = new RelayCommand(() => { });
                var createDesfireTaskCommand = new RelayCommand(() => { });
                var createUltralightTaskCommand = new RelayCommand(() => { });

                var builder = new ContextMenuBuilder(key => key);

                var menuItems = builder.BuildEmptySpaceMenu(
                    createGenericTaskCommand,
                    createGenericChipTaskCommand,
                    createClassicTaskCommand,
                    createDesfireTaskCommand,
                    createUltralightTaskCommand);

                var createTaskMenu = Assert.Single(menuItems);
                Assert.Equal("menuItemCreateTaskHeader", createTaskMenu.Header);

                Assert.Equal(4, createTaskMenu.Items.Count);

                var genericTaskItem = Assert.IsType<MenuItem>(createTaskMenu.Items[0]);
                Assert.Equal("menuItemCreateGenericTaskHeader", genericTaskItem.Header);
                Assert.Same(createGenericTaskCommand, genericTaskItem.Command);

                var genericChipTaskItem = Assert.IsType<MenuItem>(createTaskMenu.Items[1]);
                Assert.Equal("menuItemCreateGenericChipTaskHeader", genericChipTaskItem.Header);
                Assert.Same(createGenericChipTaskCommand, genericChipTaskItem.Command);

                var mifareMenu = Assert.IsType<MenuItem>(createTaskMenu.Items[2]);
                Assert.Equal("menuItemMifareHeader", mifareMenu.Header);
                Assert.Equal(5, mifareMenu.Items.Count);

                var classicMenuItem = Assert.IsType<MenuItem>(mifareMenu.Items[0]);
                Assert.Equal("menuItemAddEditMifareClassicTaskHeader", classicMenuItem.Header);
                Assert.Same(createClassicTaskCommand, classicMenuItem.Command);

                var desfireMenuItem = Assert.IsType<MenuItem>(mifareMenu.Items[1]);
                Assert.Equal("menuItemAddEditMifareDesfireTaskHeader", desfireMenuItem.Header);
                Assert.Same(createDesfireTaskCommand, desfireMenuItem.Command);

                var plusMenuItem = Assert.IsType<MenuItem>(mifareMenu.Items[2]);
                Assert.Equal("menuItemAddEditMifarePlusTaskHeader", plusMenuItem.Header);
                Assert.False(plusMenuItem.IsEnabled);

                var samMenuItem = Assert.IsType<MenuItem>(mifareMenu.Items[3]);
                Assert.Equal("menuItemAddEditMifareSAMTaskHeader", samMenuItem.Header);
                Assert.False(samMenuItem.IsEnabled);

                var ultralightMenuItem = Assert.IsType<MenuItem>(mifareMenu.Items[4]);
                Assert.Equal("menuItemAddEditMifareUltralightTaskHeader", ultralightMenuItem.Header);
                Assert.Same(createUltralightTaskCommand, ultralightMenuItem.Command);

                var tagItMenu = Assert.IsType<MenuItem>(createTaskMenu.Items[3]);
                Assert.Equal("menuItemTagItHeader", tagItMenu.Header);
                Assert.False(tagItMenu.IsEnabled);
                Assert.Single(tagItMenu.Items);

                var tagItChild = Assert.IsType<MenuItem>(tagItMenu.Items[0]);
                Assert.Equal("menuItemAddEditTagitHFIPlusTaskHeader", tagItChild.Header);
                Assert.False(tagItChild.IsEnabled);
            });
        }
    }
}
