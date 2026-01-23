using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using RFiDGear.Models;
using RFiDGear.UI.MVVMDialogs.ViewModels.Interfaces;
using RFiDGear.ViewModel;
using Xunit;

namespace RFiDGear.Tests
{
    public class RFiDChipChildLayerViewModelTests
    {
        [Fact]
        public async Task Constructor_WhenApplicationIsNull_InitializesContextMenu()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                Assert.Null(Application.Current);

                var dialogs = new ObservableCollection<IDialogViewModel>();
                var viewModel = new RFiDChipChildLayerViewModel(
                    new MifareDesfireAppModel(0),
                    null,
                    CARD_TYPE.DESFireEV1,
                    dialogs,
                    true);

                Assert.NotNull(viewModel.ContextMenu);
                Assert.NotEmpty(viewModel.ContextMenu);
            });
        }
    }
}
