using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using RFiDGear.Infrastructure;
using RFiDGear.Models;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace RFiDGear.ViewModel
{
    public class DesfireAppKeyDialogViewModel : ObservableObject, UI.MVVMDialogs.ViewModels.Interfaces.IUserDialogViewModel
    {
        public DesfireAppKeyDialogViewModel()
        {
            AppKeyHex = "00000000000000000000000000000000";
            SelectedKeyType = DESFireKeyType.DF_KEY_AES;
            SelectedKeyNumber = 0;
        }

        public DESFireKeyType SelectedKeyType
        {
            get => _selectedKeyType;
            set
            {
                _selectedKeyType = value;
                OnPropertyChanged();
                IsValidAppKeyHex = string.IsNullOrEmpty(_appKeyHex) ? (bool?)null
                    : CustomConverter.IsInHexFormat(_appKeyHex) && _appKeyHex.Length == CustomConverter.GetExpectedKeyHexLength(value);
            }
        }
        private DESFireKeyType _selectedKeyType;

        public string AppKeyHex
        {
            get => _appKeyHex;
            set
            {
                _appKeyHex = value;
                IsValidAppKeyHex = string.IsNullOrEmpty(value) ? (bool?)null
                    : CustomConverter.IsInHexFormat(value) && value.Length == CustomConverter.GetExpectedKeyHexLength(_selectedKeyType);
                OnPropertyChanged();
            }
        }
        private string _appKeyHex;

        public bool? IsValidAppKeyHex
        {
            get => _isValidAppKeyHex;
            set { _isValidAppKeyHex = value; OnPropertyChanged(); }
        }
        private bool? _isValidAppKeyHex;

        public int SelectedKeyNumber
        {
            get => _selectedKeyNumber;
            set { _selectedKeyNumber = value; OnPropertyChanged(); }
        }
        private int _selectedKeyNumber;

        public IEnumerable<DESFireKeyType> AvailableEncryptionTypes { get; } =
            (DESFireKeyType[])Enum.GetValues(typeof(DESFireKeyType));

        public IEnumerable<int> AvailableKeyNumbers { get; } = Enumerable.Range(0, 14);

        /// <summary>Dummy anchor for XAML ResourceLoader converter.</summary>
        public string LocalizationResourceSet { get; set; }

        #region IUserDialogViewModel

        public bool IsModal => true;

        public event EventHandler DialogClosing;

        public void Close() => DialogClosing?.Invoke(this, EventArgs.Empty);

        public virtual void RequestClose() => OnCloseRequest?.Invoke(this);

        public string Caption
        {
            get => _caption;
            set { _caption = value; OnPropertyChanged(); }
        }
        private string _caption;

        public Action<DesfireAppKeyDialogViewModel> OnOk { get; set; }
        public Action<DesfireAppKeyDialogViewModel> OnCancel { get; set; }
        public Action<DesfireAppKeyDialogViewModel> OnCloseRequest { get; set; }

        public ICommand OkCommand => new RelayCommand(Ok);
        public ICommand CancelCommand => new RelayCommand(Cancel);

        private void Ok() => OnOk?.Invoke(this);
        private void Cancel() => OnCancel?.Invoke(this);

        #endregion IUserDialogViewModel
    }
}
