using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Windows.Input;
using RFiDGear.UI.MVVMDialogs.ViewModels.Interfaces;

namespace RFiDGear.ViewModel
{
    /// <summary>
    /// Backs the small download-progress dialog shown while an update payload is being fetched.
    /// </summary>
    public class DownloadProgressViewModel : ObservableObject, IUserDialogViewModel
    {
        #region Commands

        /// <summary>Cancels the in-progress download.</summary>
        public ICommand CancelCommand => new RelayCommand(Cancel);

        private void Cancel()
        {
            if (OnCancel != null)
                OnCancel(this);
            else
                Close();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Overall download progress, 0–100.
        /// </summary>
        public double ProgressValue
        {
            get => _progressValue;
            set { _progressValue = value; OnPropertyChanged(nameof(ProgressValue)); }
        }
        private double _progressValue;

        /// <summary>
        /// <see langword="true"/> while the server has not reported a content length and
        /// the progress bar should display as a marquee.
        /// </summary>
        public bool IsIndeterminate
        {
            get => _isIndeterminate;
            set { _isIndeterminate = value; OnPropertyChanged(nameof(IsIndeterminate)); }
        }
        private bool _isIndeterminate = true;

        /// <summary>
        /// Human-readable description of what is currently being downloaded.
        /// </summary>
        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(nameof(StatusText)); }
        }
        private string _statusText = "Preparing download…";

        #endregion

        #region IUserDialogViewModel Implementation

        public Action<DownloadProgressViewModel> OnCancel { get; set; }
        public Action<DownloadProgressViewModel> OnCloseRequest { get; set; }

        public bool IsModal { get; private set; }

        public virtual void RequestClose()
        {
            if (OnCloseRequest != null)
                OnCloseRequest(this);
            else
                Close();
        }

        public event EventHandler DialogClosing;

        public void Close() => DialogClosing?.Invoke(this, EventArgs.Empty);

        public void Show(IList<IDialogViewModel> collection) => collection.Add(this);

        #endregion

        #region Localization

        public string LocalizationResourceSet { get; set; }

        private string _caption = "Downloading Update";

        public string Caption
        {
            get => _caption;
            set { _caption = value; OnPropertyChanged(nameof(Caption)); }
        }

        #endregion
    }
}
