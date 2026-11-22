using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using RFiDGear.UI.MVVMDialogs.ViewModels.Interfaces;

namespace RFiDGear.ViewModel
{
    /// <summary>
    /// The dialog of the Store edition about the license: it shows the trial that is running (and can be closed), or the end
    /// of the trial (and cannot be closed without a decision: the person buys, or the program closes). It is modal.
    /// </summary>
    public sealed class LicenseDialogViewModel : ObservableObject, IUserDialogViewModel
    {
        private string _caption;
        private string _message;
        private string _buyText;
        private string _closeText;
        private string _errorText;
        private bool _isBusy;

        /// <summary>
        /// Initializes a new instance of the <see cref="LicenseDialogViewModel"/> class.
        /// </summary>
        /// <param name="trialHasEnded"><see langword="true"/> when the trial is over, so closing the dialog closes the program.</param>
        public LicenseDialogViewModel(bool trialHasEnded)
        {
            TrialHasEnded = trialHasEnded;
        }

        /// <inheritdoc />
        public event EventHandler DialogClosing;

        /// <summary>Gets a value indicating whether the trial is over: the dialog can then only be left by buying or by closing the program.</summary>
        public bool TrialHasEnded { get; }

        /// <summary>Gets or sets what the buy button does: asks the Store to sell the program and tells whether it is licensed afterwards.</summary>
        public Func<Task<bool>> BuyAsync { get; set; }

        /// <summary>Gets or sets what closing the dialog does when the trial is over: closes the program.</summary>
        public Action OnExit { get; set; }

        /// <summary>Gets or sets the caption of the window.</summary>
        public string Caption
        {
            get => _caption;
            set => SetProperty(ref _caption, value);
        }

        /// <summary>Gets or sets the text that says where the trial stands.</summary>
        public string Message
        {
            get => _message;
            set => SetProperty(ref _message, value);
        }

        /// <summary>Gets or sets the text of the buy button.</summary>
        public string BuyText
        {
            get => _buyText;
            set => SetProperty(ref _buyText, value);
        }

        /// <summary>Gets or sets the text of the close button.</summary>
        public string CloseText
        {
            get => _closeText;
            set => SetProperty(ref _closeText, value);
        }

        /// <summary>Gets or sets what is shown when the purchase did not go through.</summary>
        public string PurchaseFailedText { get; set; }

        /// <summary>Gets the text of the last failed purchase; empty when there is none.</summary>
        public string ErrorText
        {
            get => _errorText;
            private set
            {
                if (SetProperty(ref _errorText, value))
                {
                    OnPropertyChanged(nameof(HasError));
                }
            }
        }

        /// <summary>Gets a value indicating whether a failed purchase is shown.</summary>
        public bool HasError => !string.IsNullOrEmpty(_errorText);

        /// <summary>Gets a value indicating whether the Store's purchase is open, so that a second one is not started.</summary>
        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    OnPropertyChanged(nameof(IsNotBusy));
                }
            }
        }

        /// <summary>Gets a value indicating whether no purchase is open.</summary>
        public bool IsNotBusy => !_isBusy;

        /// <summary>Gets the command of the buy button.</summary>
        public IAsyncRelayCommand BuyCommand => new AsyncRelayCommand(BuyNowAsync);

        /// <summary>Gets the command of the close button.</summary>
        public ICommand CloseCommand => new RelayCommand(CloseOrExit);

        /// <summary>Gets a dummy anchor for the localization converter of the view.</summary>
        public string LocalizationResourceSet { get; set; }

        /// <inheritdoc />
        public bool IsModal => true;

        /// <inheritdoc />
        public void RequestClose() => CloseOrExit();

        /// <inheritdoc />
        public void Close() => DialogClosing?.Invoke(this, EventArgs.Empty);

        /// <inheritdoc />
        public void Show(IList<IDialogViewModel> collection) => collection.Add(this);

        private async Task BuyNowAsync()
        {
            if (IsBusy || BuyAsync == null)
            {
                return;
            }

            IsBusy = true;
            ErrorText = string.Empty;

            try
            {
                if (await BuyAsync())
                {
                    Close();
                }
                else
                {
                    ErrorText = PurchaseFailedText;
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        // A running trial's dialog is only closed. When the trial is over the person decided not to buy: the program closes (the
        // window asks first if another program is running a job through the bridge, and the dialog stays if the person says no).
        private void CloseOrExit()
        {
            if (!TrialHasEnded)
            {
                Close();
                return;
            }

            OnExit?.Invoke();
        }
    }
}
