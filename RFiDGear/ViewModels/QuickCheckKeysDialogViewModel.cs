using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using RFiDGear.Infrastructure;
using RFiDGear.Models;
using RFiDGear.UI.MVVMDialogs.ViewModels;
using RFiDGear.UI.MVVMDialogs.ViewModels.Interfaces;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace RFiDGear.ViewModel
{
    public class QuickCheckKeysDialogViewModel : ObservableObject, IUserDialogViewModel
    {
        private readonly ObservableCollection<IDialogViewModel> _dialogs;

        public QuickCheckKeysDialogViewModel(DefaultSpecification spec, ObservableCollection<IDialogViewModel> dialogs = null)
        {
            var desfireKeys = spec?.MifareDesfireDefaultSecuritySettings
                ?? new List<MifareDesfireDefaultKeys>();

            var picc = desfireKeys.FirstOrDefault(k => k.KeyType == KeyType_MifareDesFireKeyType.DefaultDesfireCardCardMasterKey);
            PiccMasterKeyEncType = picc.EncryptionType;
            PiccMasterKeyValue = picc.Key ?? string.Empty;

            var app = desfireKeys.FirstOrDefault(k => k.KeyType == KeyType_MifareDesFireKeyType.DefaultDesfireCardApplicationMasterKey);
            AppMasterKeyEncType = app.EncryptionType;
            AppMasterKeyValue = app.Key ?? string.Empty;

            var read = desfireKeys.FirstOrDefault(k => k.KeyType == KeyType_MifareDesFireKeyType.DefaultDesfireCardReadKey);
            ReadKeyEncType = read.EncryptionType;
            ReadKeyValue = read.Key ?? string.Empty;

            var write = desfireKeys.FirstOrDefault(k => k.KeyType == KeyType_MifareDesFireKeyType.DefaultDesfireCardWriteKey);
            WriteKeyEncType = write.EncryptionType;
            WriteKeyValue = write.Key ?? string.Empty;

            ClassicQuickCheckKeys = new ObservableCollection<string>(
                spec?.MifareClassicDefaultQuickCheckKeys ?? new List<string>());
            _classicKeysRawText = string.Join(", ", ClassicQuickCheckKeys);

            _dialogs = dialogs;
        }

        #region DESFire Keys

        public DESFireKeyType PiccMasterKeyEncType
        {
            get => _piccMasterKeyEncType;
            set
            {
                _piccMasterKeyEncType = value;
                OnPropertyChanged();
                IsValidPiccMasterKeyValue = string.IsNullOrEmpty(_piccMasterKeyValue) ? (bool?)null
                    : CustomConverter.IsInHexFormat(_piccMasterKeyValue) && _piccMasterKeyValue.Length == CustomConverter.GetExpectedKeyHexLength(value);
            }
        }
        private DESFireKeyType _piccMasterKeyEncType;

        public string PiccMasterKeyValue
        {
            get => _piccMasterKeyValue;
            set
            {
                _piccMasterKeyValue = value;
                IsValidPiccMasterKeyValue = string.IsNullOrEmpty(value) ? (bool?)null
                    : CustomConverter.IsInHexFormat(value) && value.Length == CustomConverter.GetExpectedKeyHexLength(_piccMasterKeyEncType);
                OnPropertyChanged();
            }
        }
        private string _piccMasterKeyValue;

        public bool? IsValidPiccMasterKeyValue
        {
            get => _isValidPiccMasterKeyValue;
            set { _isValidPiccMasterKeyValue = value; OnPropertyChanged(); }
        }
        private bool? _isValidPiccMasterKeyValue;

        public DESFireKeyType AppMasterKeyEncType
        {
            get => _appMasterKeyEncType;
            set
            {
                _appMasterKeyEncType = value;
                OnPropertyChanged();
                IsValidAppMasterKeyValue = string.IsNullOrEmpty(_appMasterKeyValue) ? (bool?)null
                    : CustomConverter.IsInHexFormat(_appMasterKeyValue) && _appMasterKeyValue.Length == CustomConverter.GetExpectedKeyHexLength(value);
            }
        }
        private DESFireKeyType _appMasterKeyEncType;

        public string AppMasterKeyValue
        {
            get => _appMasterKeyValue;
            set
            {
                _appMasterKeyValue = value;
                IsValidAppMasterKeyValue = string.IsNullOrEmpty(value) ? (bool?)null
                    : CustomConverter.IsInHexFormat(value) && value.Length == CustomConverter.GetExpectedKeyHexLength(_appMasterKeyEncType);
                OnPropertyChanged();
            }
        }
        private string _appMasterKeyValue;

        public bool? IsValidAppMasterKeyValue
        {
            get => _isValidAppMasterKeyValue;
            set { _isValidAppMasterKeyValue = value; OnPropertyChanged(); }
        }
        private bool? _isValidAppMasterKeyValue;

        public DESFireKeyType ReadKeyEncType
        {
            get => _readKeyEncType;
            set
            {
                _readKeyEncType = value;
                OnPropertyChanged();
                IsValidReadKeyValue = string.IsNullOrEmpty(_readKeyValue) ? (bool?)null
                    : CustomConverter.IsInHexFormat(_readKeyValue) && _readKeyValue.Length == CustomConverter.GetExpectedKeyHexLength(value);
            }
        }
        private DESFireKeyType _readKeyEncType;

        public string ReadKeyValue
        {
            get => _readKeyValue;
            set
            {
                _readKeyValue = value;
                IsValidReadKeyValue = string.IsNullOrEmpty(value) ? (bool?)null
                    : CustomConverter.IsInHexFormat(value) && value.Length == CustomConverter.GetExpectedKeyHexLength(_readKeyEncType);
                OnPropertyChanged();
            }
        }
        private string _readKeyValue;

        public bool? IsValidReadKeyValue
        {
            get => _isValidReadKeyValue;
            set { _isValidReadKeyValue = value; OnPropertyChanged(); }
        }
        private bool? _isValidReadKeyValue;

        public DESFireKeyType WriteKeyEncType
        {
            get => _writeKeyEncType;
            set
            {
                _writeKeyEncType = value;
                OnPropertyChanged();
                IsValidWriteKeyValue = string.IsNullOrEmpty(_writeKeyValue) ? (bool?)null
                    : CustomConverter.IsInHexFormat(_writeKeyValue) && _writeKeyValue.Length == CustomConverter.GetExpectedKeyHexLength(value);
            }
        }
        private DESFireKeyType _writeKeyEncType;

        public string WriteKeyValue
        {
            get => _writeKeyValue;
            set
            {
                _writeKeyValue = value;
                IsValidWriteKeyValue = string.IsNullOrEmpty(value) ? (bool?)null
                    : CustomConverter.IsInHexFormat(value) && value.Length == CustomConverter.GetExpectedKeyHexLength(_writeKeyEncType);
                OnPropertyChanged();
            }
        }
        private string _writeKeyValue;

        public bool? IsValidWriteKeyValue
        {
            get => _isValidWriteKeyValue;
            set { _isValidWriteKeyValue = value; OnPropertyChanged(); }
        }
        private bool? _isValidWriteKeyValue;

        #endregion DESFire Keys

        #region Classic Keys

        public ObservableCollection<string> ClassicQuickCheckKeys { get; }

        /// <summary>
        /// Comma-separated representation of the Classic quick-check keys for direct TextBox binding.
        /// The setter validates the raw text and updates <see cref="IsValidClassicKeysText"/> without
        /// modifying <see cref="ClassicQuickCheckKeys"/>; the collection is only rebuilt at save time
        /// so that partial edits (temporarily invalid entries) are not stripped while the user is typing.
        /// </summary>
        public string ClassicKeysText
        {
            get => _classicKeysRawText;
            set
            {
                _classicKeysRawText = value ?? string.Empty;
                var allValid = true;
                var hasAny = false;
                if (value != null)
                {
                    foreach (var entry in value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var trimmed = entry.Trim();
                        if (trimmed.Length == 0) continue;
                        hasAny = true;
                        if (!CustomConverter.IsInHexFormat(trimmed) || trimmed.Length != 12)
                            allValid = false;
                    }
                }
                IsValidClassicKeysText = hasAny ? allValid : (bool?)null;
                OnPropertyChanged();
            }
        }
        private string _classicKeysRawText;

        public bool? IsValidClassicKeysText
        {
            get => _isValidClassicKeysText;
            set { _isValidClassicKeysText = value; OnPropertyChanged(); }
        }
        private bool? _isValidClassicKeysText;

        #endregion Classic Keys

        public bool DoNotAskAgain
        {
            get => _doNotAskAgain;
            set { _doNotAskAgain = value; OnPropertyChanged(); }
        }
        private bool _doNotAskAgain;

        public IEnumerable<DESFireKeyType> AvailableEncryptionTypes { get; } =
            (DESFireKeyType[])Enum.GetValues(typeof(DESFireKeyType));

        /// <summary>
        /// Writes the current dialog values back into the given <see cref="DefaultSpecification"/>.
        /// </summary>
        public void SaveToSettings(DefaultSpecification spec)
        {
            var keys = spec.MifareDesfireDefaultSecuritySettings;

            UpdateOrAddKey(keys, KeyType_MifareDesFireKeyType.DefaultDesfireCardCardMasterKey, PiccMasterKeyValue, PiccMasterKeyEncType);
            UpdateOrAddKey(keys, KeyType_MifareDesFireKeyType.DefaultDesfireCardApplicationMasterKey, AppMasterKeyValue, AppMasterKeyEncType);
            UpdateOrAddKey(keys, KeyType_MifareDesFireKeyType.DefaultDesfireCardReadKey, ReadKeyValue, ReadKeyEncType);
            UpdateOrAddKey(keys, KeyType_MifareDesFireKeyType.DefaultDesfireCardWriteKey, WriteKeyValue, WriteKeyEncType);

            var classicKeys = new List<string>();
            foreach (var entry in (_classicKeysRawText ?? string.Empty).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = entry.Trim();
                if (CustomConverter.IsInHexFormat(trimmed) && trimmed.Length == 12)
                    classicKeys.Add(trimmed);
            }
            spec.MifareClassicDefaultQuickCheckKeys = classicKeys;
        }

        private static void UpdateOrAddKey(List<MifareDesfireDefaultKeys> keys, KeyType_MifareDesFireKeyType keyType, string value, DESFireKeyType encType)
        {
            var idx = keys.FindIndex(k => k.KeyType == keyType);
            var updated = new MifareDesfireDefaultKeys(keyType, encType, value);
            if (idx >= 0)
                keys[idx] = updated;
            else
                keys.Add(updated);
        }

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

        /// <summary>
        /// Dummy binding anchor so ResourceLoader converter is invoked from XAML bindings
        /// that use ConverterParameter for localization.
        /// </summary>
        public string LocalizationResourceSet { get; set; }

        public Action<QuickCheckKeysDialogViewModel> OnOk { get; set; }
        public Action<QuickCheckKeysDialogViewModel> OnCancel { get; set; }
        public Action<QuickCheckKeysDialogViewModel> OnCloseRequest { get; set; }

        public ICommand OkCommand => new RelayCommand(Ok);
        public ICommand CancelCommand => new RelayCommand(Cancel);

        private void Ok()
        {
            if (IsValidClassicKeysText == false)
            {
                _dialogs?.Add(new CustomDialogViewModel
                {
                    Caption = ResourceLoader.GetResource("messageBoxInvalidClassicKeysCaption"),
                    Message = ResourceLoader.GetResource("messageBoxInvalidClassicKeysMessage"),
                    OnOk = sender => sender.Close()
                });
                return;
            }
            OnOk?.Invoke(this);
        }

        private void Cancel() => OnCancel?.Invoke(this);

        #endregion IUserDialogViewModel
    }
}
