using System;
using System.Runtime.Versioning;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RFiDGear.UI.MVVMDialogs.Presenters.Interfaces;

namespace RFiDGear.Extensions.DesfirePluginSample.ViewModel
{
    /// <summary>
    /// View model that drives the DESFire sample plugin view and demo dialog.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class DesfireSampleViewModel : ObservableObject
    {
        private readonly IDialogBoxPresenter<DesfireSampleDialogViewModel> _dialogPresenter;
        private readonly ICommand _showDialogCommand;

        /// <summary>
        /// Initializes a new instance of the <see cref="DesfireSampleViewModel"/> class.
        /// </summary>
        public DesfireSampleViewModel()
            : this(new DesfireSampleDialogPresenter())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DesfireSampleViewModel"/> class.
        /// </summary>
        /// <param name="dialogPresenter">Presenter responsible for showing dialogs.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="dialogPresenter"/> is null.</exception>
        public DesfireSampleViewModel(IDialogBoxPresenter<DesfireSampleDialogViewModel> dialogPresenter)
        {
            _dialogPresenter = dialogPresenter ?? throw new ArgumentNullException(nameof(dialogPresenter));
            _showDialogCommand = new RelayCommand(ShowDialog);
        }

        /// <summary>
        /// Gets a command that opens the DESFire sample dialog.
        /// </summary>
        public ICommand ShowDialogCommand => _showDialogCommand;

        private void ShowDialog()
        {
            var dialogViewModel = new DesfireSampleDialogViewModel
            {
                Title = "DESFire sample dialog",
                Body = "This dialog is shown from the DESFire sample view model using MVVM dialogs.",
                ConfirmText = "Confirm",
                CancelText = "Cancel"
            };

            _dialogPresenter.Show(dialogViewModel);
        }
    }
}
