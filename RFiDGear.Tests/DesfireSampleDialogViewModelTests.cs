using RFiDGear.Extensions.DesfirePluginSample.ViewModel;
using RFiDGear.UI.MVVMDialogs.Presenters.Interfaces;
using Xunit;

namespace RFiDGear.Tests
{
    /// <summary>
    /// Tests for DESFire sample dialog view models.
    /// </summary>
    public class DesfireSampleDialogViewModelTests
    {
        [Fact]
        public void CloseCommand_RaisesDialogClosing()
        {
            var viewModel = new DesfireSampleDialogViewModel();
            var wasClosed = false;

            viewModel.DialogClosing += (_, __) => wasClosed = true;

            viewModel.CloseCommand.Execute(null);

            Assert.True(wasClosed);
        }

        [Fact]
        public void ShowDialogCommand_UsesPresenter()
        {
            var presenter = new TestDialogPresenter();
            var viewModel = new DesfireSampleViewModel(presenter);

            viewModel.ShowDialogCommand.Execute(null);

            Assert.NotNull(presenter.LastViewModel);
            Assert.Equal("DESFire sample dialog", presenter.LastViewModel!.Title);
            Assert.Equal("This dialog is shown from the DESFire sample view model using MVVM dialogs.", presenter.LastViewModel.Body);
            Assert.Equal("Confirm", presenter.LastViewModel.ConfirmText);
            Assert.Equal("Cancel", presenter.LastViewModel.CancelText);
        }

        /// <summary>
        /// Captures dialog requests for verification.
        /// </summary>
        private sealed class TestDialogPresenter : IDialogBoxPresenter<DesfireSampleDialogViewModel>
        {
            public DesfireSampleDialogViewModel? LastViewModel { get; private set; }

            public void Show(DesfireSampleDialogViewModel viewModel)
            {
                LastViewModel = viewModel;
            }
        }
    }
}
