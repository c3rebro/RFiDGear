using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using RFiDGear.Infrastructure;
using RFiDGear.Models;
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
        public QuickCheckKeysDialogViewModel(DefaultSpecification spec)
        {
            var desfireKeys = spec?.MifareDesfireDefaultSecuritySettings
                ?? new List<MifareDesfireDefaultKeys>();

            var picc = desfireKeys.FirstOrDefault(k => k.KeyType == KeyType_MifareDesFireKeyType.DefaultDesfireCardCardMasterKey);
            PiccMasterKeyValue = picc.Key ?? string.Empty;
            PiccMasterKeyEncType = picc.EncryptionType;

            var app = desfireKeys.FirstOrDefault(k => k.KeyType == KeyType_MifareDesFireKeyType.DefaultDesfireCardApplicationMasterKey);
            AppMasterKeyValue = app.Key ?? string.Empty;
            AppMasterKeyEncType = app.EncryptionType;

            var read = desfireKeys.FirstOrDefault(k => k.KeyType == KeyType_MifareDesFireKeyType.DefaultDesfireCardReadKey);
            ReadKeyValue = read.Key ?? string.Empty;
            ReadKeyEncType = read.EncryptionType;

            var write = desfireKeys.FirstOrDefault(k => k.KeyType == KeyType_MifareDesFireKeyType.DefaultDesfireCardWriteKey);
            WriteKeyValue = write.Key ?? string.Empty;
            WriteKeyEncType = write.EncryptionType;

            ClassicQuickCheckKeys = new ObservableCollection<string>(
                spec?.MifareClassicDefaultQuickCheckKeys ?? new List<string>());
        }

        #region DESFire Keys

        public string PiccMasterKeyValue
        {
            get => _piccMasterKeyValue;
            set
            {
                _piccMasterKeyValue = value;
                IsValidPiccMasterKeyValue = string.IsNullOrEmpty(value) ? (bool?)null
                    : CustomConverter.IsInHexFormat(value) && value.Length == 32;
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

        public DESFireKeyType PiccMasterKeyEncType
        {
            get => _piccMasterKeyEncType;
            set { _piccMasterKeyEncType = value; OnPropertyChanged(); }
        }
        private DESFireKeyType _piccMasterKeyEncType;

        public string AppMasterKeyValue
        {
            get => _appMasterKeyValue;
            set
            {
                _appMasterKeyValue = value;
                IsValidAppMasterKeyValue = string.IsNullOrEmpty(value) ? (bool?)null
                    : CustomConverter.IsInHexFormat(value) && value.Length == 32;
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

        public DESFireKeyType AppMasterKeyEncType
        {
            get => _appMasterKeyEncType;
            set { _appMasterKeyEncType = value; OnPropertyChanged(); }
        }
        private DESFireKeyType _appMasterKeyEncType;

        public string ReadKeyValue
        {
            get => _readKeyValue;
            set
            {
                _readKeyValue = value;
                IsValidReadKeyValue = string.IsNullOrEmpty(value) ? (bool?)null
                    : CustomConverter.IsInHexFormat(value) && value.Length == 32;
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

        public DESFireKeyType ReadKeyEncType
        {
            get => _readKeyEncType;
            set { _readKeyEncType = value; OnPropertyChanged(); }
        }
        private DESFireKeyType _readKeyEncType;

        public string WriteKeyValue
        {
            get => _writeKeyValue;
            set
            {
                _writeKeyValue = value;
                IsValidWriteKeyValue = string.IsNullOrEmpty(value) ? (bool?)null
                    : CustomConverter.IsInHexFormat(value) && value.Length == 32;
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

        public DESFireKeyType WriteKeyEncType
        {
            get => _writeKeyEncType;
            set { _writeKeyEncType = value; OnPropertyChanged(); }
        }
        private DESFireKeyType _writeKeyEncType;

        #endregion DESFire Keys

        #region Classic Keys

        public ObservableCollection<string> ClassicQuickCheckKeys { get; }

        /// <summary>
        /// Newline-separated representation of <see cref="ClassicQuickCheckKeys"/> for direct TextBox binding.
        /// Each line is one hex key (12 chars). Setter re-parses and updates the collection.
        /// </summary>
        public string ClassicKeysText
        {
            get => string.Join(", ", ClassicQuickCheckKeys);
            set
            {
                var allValid = true;
                var hasAny = false;
                ClassicQuickCheckKeys.Clear();
                if (value != null)
                {
                    foreach (var entry in value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var trimmed = entry.Trim();
                        if (trimmed.Length == 0) continue;
                        hasAny = true;
                        if (CustomConverter.IsInHexFormat(trimmed) && trimmed.Length == 12)
                            ClassicQuickCheckKeys.Add(trimmed);
                        else
                            allValid = false;
                    }
                }
                IsValidClassicKeysText = hasAny ? allValid : (bool?)null;
                OnPropertyChanged();
            }
        }

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

            spec.MifareClassicDefaultQuickCheckKeys = ClassicQuickCheckKeys.ToList();
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

        private void Ok() => OnOk?.Invoke(this);
        private void Cancel() => OnCancel?.Invoke(this);

        #endregion IUserDialogViewModel
    }
}
