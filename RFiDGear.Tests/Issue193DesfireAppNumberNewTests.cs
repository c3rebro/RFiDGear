using RFiDGear.Infrastructure.Tasks;
using RFiDGear.UI.MVVMDialogs.ViewModels.Interfaces;
using RFiDGear.ViewModel.TaskSetupViewModels;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Xunit;

namespace RFiDGear.Tests
{
    public class Issue193DesfireAppNumberNewTests
    {
        [Fact]
        public async Task AppNumberNew_AcceptsNullForMissingXmlElement()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var viewModel = new MifareDesfireSetupViewModel();

                viewModel.AppNumberNew = null;

                Assert.Null(viewModel.AppNumberNew);
                Assert.Equal(false, viewModel.IsValidAppNumberNew);
                Assert.Equal(0, viewModel.AppNumberNewAsInt);
            });
        }

        [Fact]
        public async Task EditConstructor_PreservesCurrentAppWhenAppNumberNewIsMissing()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var source = new MifareDesfireSetupViewModel
                {
                    SelectedTaskType = TaskType_MifareDesfireTask.ReadData,
                    AppNumberCurrent = "0xF482D0"
                };

                Assert.Null(source.AppNumberNew);

                var editor = new MifareDesfireSetupViewModel(
                    source,
                    new ObservableCollection<IDialogViewModel>());

                Assert.Null(editor.AppNumberNew);
                Assert.Equal(source.AppNumberCurrent, editor.AppNumberCurrent);
                Assert.Equal(source.AppNumberCurrentAsInt, editor.AppNumberCurrentAsInt);
                Assert.True(editor.IsValidAppNumberCurrent);
            });
        }
    }
}
