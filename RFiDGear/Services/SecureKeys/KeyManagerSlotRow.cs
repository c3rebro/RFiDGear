#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Security;
using System.Windows.Input;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace RFiDGear.Services.SecureKeys
{
    /// <summary>A key in the list of the keys that are stored: its name, and what the person reads for it.</summary>
    internal sealed class StoredKeyChoice
    {
        /// <summary>Creates the choice.</summary>
        /// <param name="reference">The name of the key.</param>
        /// <param name="text">What the person reads.</param>
        internal StoredKeyChoice(string reference, string text)
        {
            Reference = reference;
            Text = text;
        }

        /// <summary>Gets the name of the key in the Credential Manager, without the prefix.</summary>
        public string Reference { get; }

        /// <summary>Gets what the person reads for the key.</summary>
        public string Text { get; }
    }

    /// <summary>
    /// One slot in the key manager: whether its key is typed into the task or named by a reference, the key store and the name
    /// of the key (chosen from the keys that are stored, for the Credential Manager), and creating a new key under a new name. It is a thin face on the <see cref="KeySlotEditorState"/> of the slot,
    /// so the manager decides exactly what the editor decided (asking before a typed key is discarded, binding through the task).
    /// It holds no key: a key that is stored is read from the entry that the window hands in, and the window clears that entry.
    /// </summary>
    internal sealed class KeyManagerSlotRow : ObservableObject, IDisposable
    {
        private readonly KeySlotEditorState _state;
        private readonly Func<string, string> _text;
        private string _resultText = string.Empty;
        private bool _resultIsError;
        private string _newName = string.Empty;
        private IReadOnlyList<StoredKeyChoice> _storedKeys = Array.Empty<StoredKeyChoice>();
        private int _listError;

        /// <summary>Creates the row of a slot.</summary>
        /// <param name="state">What decides for the slot.</param>
        /// <param name="slotName">The name of the slot in the language of the application.</param>
        /// <param name="providers">The key stores to choose from, described for the person.</param>
        /// <param name="text">Gives the text of a resource key.</param>
        internal KeyManagerSlotRow(KeySlotEditorState state, string slotName, IReadOnlyList<KeySlotProviderItem> providers, Func<string, string> text)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            SlotName = slotName ?? throw new ArgumentNullException(nameof(slotName));
            Providers = providers ?? throw new ArgumentNullException(nameof(providers));
            _text = text ?? throw new ArgumentNullException(nameof(text));

            ToggleModeCommand = new RelayCommand(OnToggleMode, () => _state.ToggleModeCommand.CanExecute(null));
            _state.PropertyChanged += OnStateChanged;
            RefreshStoredKeys();
        }

        /// <summary>Gets the id of the slot; see <see cref="SecureKeySlots"/>.</summary>
        public string SlotId => _state.SlotId;

        /// <summary>Gets the name of the slot in the language of the application.</summary>
        public string SlotName { get; }

        /// <summary>Gets the key stores to choose from.</summary>
        public IReadOnlyList<KeySlotProviderItem> Providers { get; }

        /// <summary>Gets whether the slot takes its key from a key store.</summary>
        public bool IsReferenceMode => _state.IsReferenceMode;

        /// <summary>Gets whether the slot takes the key that is typed into the task.</summary>
        public bool UsesTypedKey => !_state.IsReferenceMode;

        /// <summary>Gets or sets the id of the key store of the slot.</summary>
        public string ProviderId
        {
            get => _state.ProviderId;
            set
            {
                _state.ProviderId = value;
                ClearResult();
                RefreshStoredKeys();
            }
        }

        /// <summary>Gets or sets the name of the key in the key store.</summary>
        public string Reference
        {
            get => _state.Reference;
            set
            {
                // A list box that is emptied while its items are replaced reports no choice at all. That is not the person's choice,
                // and the binding of the slot is not touched by it.
                if (value == null)
                {
                    return;
                }

                _state.Reference = value;
                ClearResult();
            }
        }

        /// <summary>Gets how long a name can be.</summary>
        public int MaxReferenceLength => SecureKeySyntax.MaxReferenceLength;

        /// <summary>Gets whether the name is one a key store could be asked for; empty is not.</summary>
        public bool IsReferenceValid => _state.IsReferenceValid;

        /// <summary>Gets whether a key can be stored from here in the key store of the slot: the Credential Manager takes keys.</summary>
        public bool ProviderOffersStoreKey => _state.ProviderOffersStoreKey;

        /// <summary>Gets whether the slot names a key by choosing it from the stored keys: its key store is the Credential Manager.</summary>
        public bool ShowStoredKeyChoice => _state.ProviderOffersStoreKey;

        /// <summary>Gets whether the slot names a key by a name that is typed: it is named by another key store.</summary>
        public bool UsesFreeTextReference => _state.IsReferenceMode && !_state.ProviderOffersStoreKey;

        /// <summary>Gets the keys that are stored in the Credential Manager, to choose from, and the key that the slot names if it is not among them.</summary>
        public IReadOnlyList<StoredKeyChoice> StoredKeys => _storedKeys;

        /// <summary>Gets whether no key is stored yet.</summary>
        public bool HasNoStoredKeys => _listError == 0 && _storedKeys.Count == 0;

        /// <summary>Gets whether the stored keys could not be listed.</summary>
        public bool HasStoredKeysError => _listError != 0;

        /// <summary>Gets the sentence that says why the stored keys could not be listed.</summary>
        public string StoredKeysErrorText => _listError != 0
            ? string.Format(CultureInfo.CurrentCulture, _text("messageStoredKeysListFailed"), _listError)
            : string.Empty;

        /// <summary>Gets or sets the name that a new key is stored under.</summary>
        public string NewName
        {
            get => _newName;
            set
            {
                if (SetProperty(ref _newName, value ?? string.Empty))
                {
                    ClearResult();
                    OnPropertyChanged(nameof(CanStoreKey));
                }
            }
        }

        /// <summary>Gets whether a new key can be stored: the key store takes keys and the new name is valid. No live check colours the name.</summary>
        public bool CanStoreKey => _state.ProviderOffersStoreKey && SecureKeySyntax.IsValidReference(_newName);

        /// <summary>Gets how many hexadecimal digits the key of the slot has now.</summary>
        public int KeyHexLength => _state.ExpectedKeyHexLength;

        /// <summary>Gets whether the slot is named in the key store of the connected external application.</summary>
        public bool UsesExternalApplication =>
            _state.IsReferenceMode && string.Equals(_state.ProviderId, BridgeKeyProvider.ProviderId, StringComparison.Ordinal);

        /// <summary>Gets the command that switches between the typed key and a reference.</summary>
        public ICommand ToggleModeCommand { get; }

        /// <summary>Gets the text of the button that switches: it names the mode it leads to.</summary>
        public string ModeButtonText => _text(_state.IsReferenceMode ? "buttonContentKeyManagerUseKey" : "buttonContentKeyManagerUseReference");

        /// <summary>Gets the sentence for a slot whose key is typed into the task.</summary>
        public string TypedKeyText => _text("labelKeyManagerTypedKey");

        /// <summary>Gets the label of the choice of the key store.</summary>
        public string KeyStoreLabel => _text("labelKeyManagerKeyStore");

        /// <summary>Gets the label of the name of the key.</summary>
        public string KeyNameLabel => _text("labelKeyManagerKeyName");

        /// <summary>Gets the label of the box in which a key is entered, which says how many digits it has.</summary>
        public string KeyPrompt => string.Format(CultureInfo.CurrentCulture, _text("labelKeySlotStoreKeyPrompt"), _state.ExpectedKeyHexLength);

        /// <summary>Gets the tooltip of the button that switches between the typed key and a reference.</summary>
        public string ModeToolTip => _text("toolTipKeySlotMode");

        /// <summary>Gets the tooltip of the choice of the key store.</summary>
        public string ProviderToolTip => _text("toolTipKeySlotProvider");

        /// <summary>Gets the tooltip of the box for the name of the key.</summary>
        public string ReferenceToolTip => _text("toolTipKeySlotReference");

        /// <summary>Gets the text of the button that stores the entered key.</summary>
        public string StoreButtonText => _text("buttonContentKeyManagerStoreKey");

        /// <summary>Gets the tooltip of the button that stores a new key for this slot.</summary>
        public string StoreNewToolTip => _text("toolTipKeyManagerStoreNew");

        /// <summary>Gets the label of the choice of a stored key.</summary>
        public string StoredKeyLabel => _text("labelKeyManagerStoredKey");

        /// <summary>Gets the tooltip of the choice of a stored key.</summary>
        public string StoredKeyToolTip => _text("toolTipKeyManagerStoredKey");

        /// <summary>Gets the sentence for the choice of a stored key when no key is stored yet.</summary>
        public string NoStoredKeysText => _text("labelKeyManagerNoStoredKeys");

        /// <summary>Gets the header of the group in which a new key is created.</summary>
        public string NewKeyHeader => _text("labelKeyManagerNewKey");

        /// <summary>Gets the text of the check box that shows the key that is entered.</summary>
        public string ShowKeyText => _text("labelKeyManagerShowKey");

        /// <summary>Gets the tooltip of the check box that shows the key that is entered.</summary>
        public string ShowKeyToolTip => _text("toolTipKeyManagerShowKey");

        /// <summary>Gets what became of the last key that was stored, or empty.</summary>
        public string ResultText => _resultText;

        /// <summary>Gets whether there is something to say about the last key that was stored.</summary>
        public bool HasResult => _resultText.Length > 0;

        /// <summary>Gets whether the last key could not be stored.</summary>
        public bool ResultIsError => _resultIsError;

        /// <summary>
        /// Stores the key that was entered as a new key under the new name, and the slot uses it from then on. The key is read from the
        /// entry and erased; the entry is the caller's to dispose and to clear when this says that the key was tried.
        /// </summary>
        /// <param name="entered">The digits as they were entered.</param>
        /// <returns>
        /// <see langword="true"/> when a key was tried and the digits are no longer needed; <see langword="false"/> when nothing was
        /// stored and the digits are kept while the person goes on: no key can be stored under this name, the name is in use, the
        /// list could not be read, or the entry is not a key.
        /// </returns>
        internal bool StoreEntered(SecureString entered)
        {
            if (_state.StoreNewKey(_newName, entered) is not { } result)
            {
                return false;
            }

            switch (result.Message)
            {
                case KeySlotMessage.KeyStored:
                    SetResult(_text("messageKeySlotKeyStored"), false);
                    _newName = string.Empty;
                    OnPropertyChanged(nameof(NewName));
                    OnPropertyChanged(nameof(CanStoreKey));
                    RefreshStoredKeys();
                    return true;
                case KeySlotMessage.KeyNotStored:
                    SetResult(string.Format(CultureInfo.CurrentCulture, _text("messageKeySlotKeyNotStored"), result.Detail), true);
                    return true;
                case KeySlotMessage.NameInUse:
                    SetResult(string.Format(CultureInfo.CurrentCulture, _text("messageKeyManagerNameInUse"), _newName), true);
                    return false;
                case KeySlotMessage.ListUnreadable:
                    SetResult(string.Format(CultureInfo.CurrentCulture, _text("messageKeyManagerListUnreadable"), result.Detail), true);
                    return false;
                default:
                    SetResult(string.Format(CultureInfo.CurrentCulture, _text("messageKeySlotKeyInvalid"), result.Detail), true);
                    return false;
            }
        }

        /// <summary>
        /// Reads the stored keys again. The key that the slot names is in the list even when it is not stored (and says so), so that
        /// the choice shows what the slot does, and a key that is gone is seen as gone.
        /// </summary>
        internal void RefreshStoredKeys()
        {
            var choices = new List<StoredKeyChoice>();

            if (_state.ProviderOffersStoreKey)
            {
                var stored = _state.ListStoredKeys();
                _listError = stored.Error;
                choices.AddRange(stored.References.Select(reference => new StoredKeyChoice(reference, reference)));

                var current = _state.Reference;

                if (current.Length > 0 && !choices.Any(choice => string.Equals(choice.Reference, current, StringComparison.Ordinal)))
                {
                    choices.Insert(0, new StoredKeyChoice(current, string.Format(CultureInfo.CurrentCulture, _text("labelKeyManagerStoredKeyMissing"), current)));
                }
            }
            else
            {
                _listError = 0;
            }

            _storedKeys = choices;
            OnPropertyChanged(nameof(StoredKeys));
            OnPropertyChanged(nameof(HasNoStoredKeys));
            OnPropertyChanged(nameof(HasStoredKeysError));
            OnPropertyChanged(nameof(StoredKeysErrorText));

            // The choice shows the name of the slot again after its items were replaced.
            OnPropertyChanged(nameof(Reference));
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _state.PropertyChanged -= OnStateChanged;
            _state.Dispose();
        }

        private void OnToggleMode()
        {
            ClearResult();
            _state.ToggleMode();
            RefreshStoredKeys();
        }

        private void OnStateChanged(object? sender, PropertyChangedEventArgs e)
        {
            // The state says that everything may have changed: the task changed its bindings, or a refusal put the button back.
            OnPropertyChanged(string.Empty);
            (ToggleModeCommand as RelayCommand)?.NotifyCanExecuteChanged();
        }

        private void SetResult(string text, bool isError)
        {
            _resultText = text;
            _resultIsError = isError;
            OnPropertyChanged(nameof(ResultText));
            OnPropertyChanged(nameof(HasResult));
            OnPropertyChanged(nameof(ResultIsError));
        }

        private void ClearResult()
        {
            if (_resultText.Length > 0)
            {
                SetResult(string.Empty, false);
            }
        }
    }
}
