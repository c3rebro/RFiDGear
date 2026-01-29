using System;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RFiDGear.UI.MVVMDialogs.ViewModels.Interfaces;

namespace RFiDGear.Extensions.DesfirePluginSample.ViewModel
{
    /// <summary>
    /// Dialog view model for the DESFire sample plugin.
    /// </summary>
    public class DesfireSampleDialogViewModel : ObservableObject, IUserDialogViewModel
    {
        private readonly ICommand _okCommand;
        private readonly ICommand _cancelCommand;
        private readonly ICommand _closeCommand;
        private string _caption = string.Empty;
        private string _message = string.Empty;
        private string _confirmText = "Ok";
        private string _cancelText = "Cancel";

        /// <summary>
        /// Initializes a new instance of the <see cref="DesfireSampleDialogViewModel"/> class.
        /// </summary>
        /// <param name="isModal">Whether the dialog should be modal.</param>
        public DesfireSampleDialogViewModel(bool isModal = true)
        {
            IsModal = isModal;
            _okCommand = new RelayCommand(Close);
            _cancelCommand = new RelayCommand(Close);
            _closeCommand = new RelayCommand(Close);
        }

        /// <summary>
        /// Gets a value indicating whether the dialog is modal.
        /// </summary>
        public bool IsModal { get; }

        /// <summary>
        /// Gets or sets the dialog caption displayed in the title bar.
        /// </summary>
        public string Caption
        {
            get => _caption;
            set
            {
                if (SetProperty(ref _caption, value))
                {
                    OnPropertyChanged(nameof(Title));
                }
            }
        }

        /// <summary>
        /// Gets or sets the dialog message displayed in the body area.
        /// </summary>
        public string Message
        {
            get => _message;
            set
            {
                if (SetProperty(ref _message, value))
                {
                    OnPropertyChanged(nameof(Body));
                }
            }
        }

        /// <summary>
        /// Gets or sets the dialog title text.
        /// </summary>
        public string Title
        {
            get => Caption;
            set => Caption = value;
        }

        /// <summary>
        /// Gets or sets the dialog body text.
        /// </summary>
        public string Body
        {
            get => Message;
            set => Message = value;
        }

        /// <summary>
        /// Gets or sets the confirm button label.
        /// </summary>
        public string ConfirmText
        {
            get => _confirmText;
            set => SetProperty(ref _confirmText, value);
        }

        /// <summary>
        /// Gets or sets the cancel button label.
        /// </summary>
        public string CancelText
        {
            get => _cancelText;
            set => SetProperty(ref _cancelText, value);
        }

        /// <summary>
        /// Gets the command that confirms the dialog.
        /// </summary>
        public ICommand OkCommand => _okCommand;

        /// <summary>
        /// Gets the command that cancels the dialog.
        /// </summary>
        public ICommand CancelCommand => _cancelCommand;

        /// <summary>
        /// Gets the command that closes the dialog.
        /// </summary>
        public ICommand CloseCommand => _closeCommand;

        /// <summary>
        /// Raised when the dialog requests to close.
        /// </summary>
        public event EventHandler? DialogClosing;

        /// <summary>
        /// Requests that the dialog be closed.
        /// </summary>
        public void RequestClose()
        {
            Close();
        }

        private void Close()
        {
            DialogClosing?.Invoke(this, EventArgs.Empty);
        }
    }
}
