using System;
using System.Runtime.Versioning;
using System.Windows;
using RFiDGear.Extensions.DesfirePluginSample.View;
using RFiDGear.UI.MVVMDialogs.Presenters.Interfaces;

namespace RFiDGear.Extensions.DesfirePluginSample.ViewModel
{
    /// <summary>
    /// Presenter that displays the DESFire sample dialog view.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class DesfireSampleDialogPresenter : IDialogBoxPresenter<DesfireSampleDialogViewModel>
    {
        /// <summary>
        /// Shows the dialog view model using the DESFire sample dialog window.
        /// </summary>
        /// <param name="viewModel">The dialog view model to present.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="viewModel"/> is null.</exception>
        public void Show(DesfireSampleDialogViewModel viewModel)
        {
            if (viewModel == null)
            {
                throw new ArgumentNullException(nameof(viewModel));
            }

            var dialog = new DesfireSampleDialogView
            {
                DataContext = viewModel,
                Owner = Application.Current?.MainWindow
            };

            bool isClosing = false;

            EventHandler closeHandler = (sender, args) =>
            {
                if (isClosing)
                {
                    return;
                }

                isClosing = true;
                dialog.Close();
            };

            viewModel.DialogClosing += closeHandler;

            dialog.Closed += (sender, args) =>
            {
                viewModel.DialogClosing -= closeHandler;
            };

            dialog.Closing += (sender, args) =>
            {
                if (isClosing)
                {
                    return;
                }

                isClosing = true;
                viewModel.RequestClose();
            };

            if (viewModel.IsModal)
            {
                dialog.ShowDialog();
            }
            else
            {
                dialog.Show();
            }
        }
    }
}
