#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Security;
using System.Security.Cryptography;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace RFiDGear.Services.SecureKeys
{
    /// <summary>What the editor tells the person after it tried to store a key.</summary>
    internal enum KeySlotMessage
    {
        /// <summary>The key was stored under the reference.</summary>
        KeyStored,

        /// <summary>The key store refused; the detail is its error code.</summary>
        KeyNotStored,

        /// <summary>What was entered is not a key of the right length; the detail is the number of digits needed.</summary>
        KeyInvalid,
    }

    /// <summary>The questions the editor asks the person. The control answers them with windows, the tests with a script.</summary>
    internal interface IKeySlotEditorDialogs
    {
        /// <summary>Asks whether the key that the task holds may be discarded in favour of a reference.</summary>
        /// <returns><c>true</c> when the person agrees.</returns>
        bool ConfirmReplaceKey();

        /// <summary>Asks for a key to store, with the digits hidden.</summary>
        /// <param name="hexLength">How many digits the key has.</param>
        /// <returns>The digits as they were entered, which the caller disposes; <c>null</c> when the person cancelled.</returns>
        SecureString? AskForKey(int hexLength);

        /// <summary>Tells the person what happened.</summary>
        /// <param name="message">What happened.</param>
        /// <param name="detail">The error code of the key store or the number of digits that were needed; <c>0</c> otherwise.</param>
        void Show(KeySlotMessage message, int detail);
    }

    /// <summary>
    /// What one key slot editor shows and does for one slot of a task: whether the key is typed into the task or named by a
    /// reference to a key store, the reference itself, and storing a key under it. It holds no key. The embedded key stays
    /// in the control that the editor wraps; this class only decides when it is discarded.
    /// </summary>
    internal sealed class KeySlotEditorState : ObservableObject, IDisposable
    {
        private readonly IKeySlotHost _host;
        private readonly string _slotId;
        private readonly IReadOnlyList<SecureKeyProviderInfo> _providers;
        private readonly IKeySlotEditorDialogs _dialogs;
        private readonly WindowsCredentialKeyProvider _keyStore;

        /// <summary>Creates the state of the editor of one slot.</summary>
        /// <param name="host">The task.</param>
        /// <param name="slotId">The slot.</param>
        /// <param name="providers">The providers a slot can be bound to.</param>
        /// <param name="dialogs">How to ask and tell the person.</param>
        /// <param name="keyStore">The Windows Credential Manager provider, to store a key with.</param>
        internal KeySlotEditorState(
            IKeySlotHost host,
            string slotId,
            IReadOnlyList<SecureKeyProviderInfo> providers,
            IKeySlotEditorDialogs dialogs,
            WindowsCredentialKeyProvider keyStore)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _slotId = slotId ?? throw new ArgumentNullException(nameof(slotId));
            _providers = providers ?? throw new ArgumentNullException(nameof(providers));
            _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
            _keyStore = keyStore ?? throw new ArgumentNullException(nameof(keyStore));

            ToggleModeCommand = new RelayCommand(ToggleMode, () => IsReferenceMode || CanUseReference);
            StoreKeyCommand = new RelayCommand(StoreKey, () => CanStoreKey);
            _host.PropertyChanged += OnHostPropertyChanged;
        }

        /// <summary>Gets whether the slot takes its key from a provider.</summary>
        public bool IsReferenceMode => _host.IsKeyBound(_slotId);

        /// <summary>Gets whether the slot takes the key that is typed into the task.</summary>
        public bool IsEmbeddedMode => !IsReferenceMode;

        /// <summary>Gets the providers a slot can be bound to.</summary>
        public IReadOnlyList<SecureKeyProviderInfo> Providers => _providers;

        /// <summary>Gets whether the person can choose between providers; with one there is nothing to choose.</summary>
        public bool ShowProviderChoice => _providers.Count > 1;

        /// <summary>Gets whether there is a provider to bind to at all.</summary>
        public bool CanUseReference => _providers.Count > 0;

        /// <summary>Gets or sets the provider of the binding.</summary>
        public string ProviderId
        {
            get => _host.GetKeyBinding(_slotId)?.ProviderId ?? DefaultProviderId;
            set
            {
                if (IsReferenceMode && value != null && !string.Equals(value, ProviderId, StringComparison.Ordinal))
                {
                    _host.BindKeySlot(_slotId, value, Reference);
                }
            }
        }

        /// <summary>Gets or sets the reference of the binding.</summary>
        public string Reference
        {
            get => _host.GetKeyBinding(_slotId)?.Reference ?? string.Empty;
            set
            {
                if (IsReferenceMode && !string.Equals(value ?? string.Empty, Reference, StringComparison.Ordinal))
                {
                    _host.BindKeySlot(_slotId, ProviderId, value ?? string.Empty);
                }
            }
        }

        /// <summary>Gets whether the reference is one that a provider could be asked for; empty is not.</summary>
        public bool IsReferenceValid => SecureKeySyntax.IsValidReference(Reference);

        /// <summary>Gets whether the provider of the binding is one a key can be stored in from here: the Credential Manager is.</summary>
        public bool ProviderOffersStoreKey =>
            IsReferenceMode && string.Equals(ProviderId, WindowsCredentialKeyProvider.ProviderId, StringComparison.Ordinal);

        /// <summary>Gets whether a key can be stored under the reference: the provider takes keys, and the reference is valid.</summary>
        public bool CanStoreKey => ProviderOffersStoreKey && IsReferenceValid;

        /// <summary>Gets the command that switches between the key in the task and a reference.</summary>
        public ICommand ToggleModeCommand { get; }

        /// <summary>Gets the command that stores a key under the reference.</summary>
        public ICommand StoreKeyCommand { get; }

        private string DefaultProviderId =>
            _providers.Any(provider => provider.Id == WindowsCredentialKeyProvider.ProviderId)
                ? WindowsCredentialKeyProvider.ProviderId
                : _providers.FirstOrDefault()?.Id ?? string.Empty;

        /// <summary>
        /// Switches to a reference when the slot has a key, and back when it has a reference. Discarding a key asks first,
        /// and a refusal changes nothing. The binding is written before the key is cleared.
        /// </summary>
        internal void ToggleMode()
        {
            if (IsReferenceMode)
            {
                _host.UnbindKeySlot(_slotId);
                return;
            }

            if (!CanUseReference)
            {
                return;
            }

            if (_host.HasEmbeddedKey(_slotId) && !_dialogs.ConfirmReplaceKey())
            {
                // The button looks as it did before it was pressed.
                OnPropertyChanged(string.Empty);
                return;
            }

            _host.BindKeySlot(_slotId, DefaultProviderId, string.Empty);
        }

        /// <summary>
        /// Asks for a key and stores it under the reference in the Credential Manager. The key is read from the entry
        /// without becoming a string and is erased afterwards; it is not shown again.
        /// </summary>
        internal void StoreKey()
        {
            if (!CanStoreKey)
            {
                return;
            }

            var hexLength = _host.ExpectedKeyHexLength(_slotId);
            using var entered = _dialogs.AskForKey(hexLength);

            if (entered == null)
            {
                return;
            }

            Span<byte> key = stackalloc byte[WindowsCredentialKeyProvider.MaxKeyBytes];

            try
            {
                var length = KeyEntry.Read(entered, hexLength, key);

                if (length == 0)
                {
                    _dialogs.Show(KeySlotMessage.KeyInvalid, hexLength);
                    return;
                }

                var error = _keyStore.Store(Reference, key.Slice(0, length));

                if (error == 0)
                {
                    _dialogs.Show(KeySlotMessage.KeyStored, 0);
                }
                else
                {
                    _dialogs.Show(KeySlotMessage.KeyNotStored, error);
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
            }
        }

        /// <inheritdoc />
        public void Dispose() => _host.PropertyChanged -= OnHostPropertyChanged;

        private void OnHostPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // The bindings are replaced as a whole; a change of the type of a key changes how many digits a key has.
            if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == "KeyBindings")
            {
                OnPropertyChanged(string.Empty);
                (ToggleModeCommand as RelayCommand)?.NotifyCanExecuteChanged();
                (StoreKeyCommand as RelayCommand)?.NotifyCanExecuteChanged();
            }
        }
    }
}
