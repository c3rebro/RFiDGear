#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Security;
using System.Windows.Input;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace RFiDGear.Services.SecureKeys
{
    /// <summary>
    /// One slot in the key manager: whether its key is typed into the task or named by a reference, the key store and the name
    /// of the key, and storing a key under that name. It is a thin face on the <see cref="KeySlotEditorState"/> of the slot,
    /// so the manager decides exactly what the editor decided (asking before a typed key is discarded, binding through the task).
    /// It holds no key: a key that is stored is read from the entry that the window hands in, and the window clears that entry.
    /// </summary>
    internal sealed class KeyManagerSlotRow : ObservableObject, IDisposable
    {
        private readonly KeySlotEditorState _state;
        private readonly Func<string, string> _text;
        private string _resultText = string.Empty;
        private bool _resultIsError;

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
            }
        }

        /// <summary>Gets or sets the name of the key in the key store.</summary>
        public string Reference
        {
            get => _state.Reference;
            set
            {
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

        /// <summary>Gets whether a key can be stored now: the key store takes keys and the name is valid.</summary>
        public bool CanStoreKey => _state.CanStoreKey;

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

        /// <summary>Gets the tooltip of the button that stores the entered key.</summary>
        public string StoreToolTip => _text("toolTipKeySlotStoreKey");

        /// <summary>Gets what became of the last key that was stored, or empty.</summary>
        public string ResultText => _resultText;

        /// <summary>Gets whether there is something to say about the last key that was stored.</summary>
        public bool HasResult => _resultText.Length > 0;

        /// <summary>Gets whether the last key could not be stored.</summary>
        public bool ResultIsError => _resultIsError;

        /// <summary>
        /// Stores the key that was entered under the name of the slot and says what came of it. The key is read from the entry
        /// and erased; the entry is the caller's to dispose and to clear.
        /// </summary>
        /// <param name="entered">The digits as they were entered.</param>
        internal void StoreEntered(SecureString entered)
        {
            if (_state.StoreEntered(entered) is not { } result)
            {
                return;
            }

            switch (result.Message)
            {
                case KeySlotMessage.KeyStored:
                    SetResult(_text("messageKeySlotKeyStored"), false);
                    break;
                case KeySlotMessage.KeyNotStored:
                    SetResult(string.Format(CultureInfo.CurrentCulture, _text("messageKeySlotKeyNotStored"), result.Detail), true);
                    break;
                default:
                    SetResult(string.Format(CultureInfo.CurrentCulture, _text("messageKeySlotKeyInvalid"), result.Detail), true);
                    break;
            }
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
