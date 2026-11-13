#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Security;
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

        /// <summary>A key is stored under that name already, so nothing was stored.</summary>
        NameInUse,

        /// <summary>The stored keys could not be listed, so it is not known whether the name is free and nothing was stored; the detail is the error code.</summary>
        ListUnreadable,

        /// <summary>What was entered is not a key of the right length; the detail is the number of digits needed.</summary>
        KeyInvalid,
    }

    /// <summary>What came of storing a key that was entered.</summary>
    /// <param name="Message">What happened.</param>
    /// <param name="Detail">The error code of the key store or the number of digits that were needed; <c>0</c> otherwise.</param>
    internal readonly record struct KeySlotStoreResult(KeySlotMessage Message, int Detail);

    /// <summary>The question the editor asks the person. The control answers it with a message box, the tests with a script.</summary>
    internal interface IKeySlotEditorDialogs
    {
        /// <summary>Asks whether the key that the task holds may be discarded in favour of a reference.</summary>
        /// <returns><c>true</c> when the person agrees.</returns>
        bool ConfirmReplaceKey();
    }

    /// <summary>
    /// What one key slot editor shows and does for one slot of a task: whether the key is typed into the task or named by a
    /// reference to a key store, the reference itself, a line that says so, and storing a key under the reference. It holds no
    /// key. The embedded key stays in the control that the editor wraps; this class only decides when it is discarded.
    /// </summary>
    internal sealed class KeySlotEditorState : ObservableObject, IDisposable
    {
        private readonly IKeySlotHost _host;
        private readonly string _slotId;
        private readonly IReadOnlyList<SecureKeyProviderInfo> _providers;
        private readonly IKeySlotEditorDialogs _dialogs;
        private readonly WindowsCredentialKeyProvider _keyStore;
        private readonly Func<string, string> _text;
        private readonly KeySlotProviderCatalog _catalog;

        /// <summary>Creates the state of the editor of one slot.</summary>
        /// <param name="host">The task.</param>
        /// <param name="slotId">The slot.</param>
        /// <param name="providers">The providers a slot can be bound to.</param>
        /// <param name="dialogs">How to ask the person.</param>
        /// <param name="keyStore">The Windows Credential Manager provider, to store a key with.</param>
        /// <param name="text">Gives the text of a resource key; <see langword="null"/> for the resources of the application.</param>
        internal KeySlotEditorState(
            IKeySlotHost host,
            string slotId,
            IReadOnlyList<SecureKeyProviderInfo> providers,
            IKeySlotEditorDialogs dialogs,
            WindowsCredentialKeyProvider keyStore,
            Func<string, string>? text = null)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _slotId = slotId ?? throw new ArgumentNullException(nameof(slotId));
            _providers = providers ?? throw new ArgumentNullException(nameof(providers));
            _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
            _keyStore = keyStore ?? throw new ArgumentNullException(nameof(keyStore));
            _text = CachedText.Of(text ?? RFiDGear.Infrastructure.ResourceLoader.GetResource);
            _catalog = new KeySlotProviderCatalog(_text);

            ToggleModeCommand = new RelayCommand(ToggleMode, () => IsReferenceMode || CanUseReference);
            _host.PropertyChanged += OnHostPropertyChanged;
        }

        /// <summary>Gets the id of the slot this state is about; see <see cref="SecureKeySlots"/>.</summary>
        public string SlotId => _slotId;

        /// <summary>Gets how many hexadecimal digits the key of the slot has now, which follows the key type the person chose.</summary>
        public int ExpectedKeyHexLength => _host.ExpectedKeyHexLength(_slotId);

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

        /// <summary>Gets the command that switches between the key in the task and a reference.</summary>
        public ICommand ToggleModeCommand { get; }

        /// <summary>Gets the name of the key store of the binding in the language of the application; its id for a key store that is not listed.</summary>
        public string ProviderName =>
            _catalog.Describe(_providers).FirstOrDefault(item => item.Id == ProviderId)?.DisplayName ?? ProviderId;

        /// <summary>
        /// Gets the line below the key box that says which key the slot uses: the name of the key, which is what a person looks
        /// for. It is short on purpose, because the boxes of some tabs are narrow. Empty while the key is typed into the task,
        /// which is when the row has no height.
        /// </summary>
        public string ReferenceCaption => IsReferenceMode
            ? string.Format(System.Globalization.CultureInfo.CurrentCulture, _text("labelKeySlotReferenceCaption"), NameOfReference)
            : string.Empty;

        /// <summary>Gets the whole of it for a tooltip: the key store and the name. Empty while the key is typed into the task.</summary>
        public string ReferenceDetail => IsReferenceMode
            ? string.Format(System.Globalization.CultureInfo.CurrentCulture, _text("labelKeySlotReferenceDetail"), ProviderName, NameOfReference)
            : string.Empty;

        private string NameOfReference => Reference.Length > 0 ? Reference : _text("labelKeySlotReferenceNoName");

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

        /// <summary>Lists the keys that are stored in the Credential Manager, by name. No key is read.</summary>
        /// <returns>The error, and the names in alphabetical order.</returns>
        internal StoredKeyList ListStoredKeys() => _keyStore.List();

        /// <summary>
        /// Stores a new key in the Credential Manager under a name that is not in use and makes the slot use it. The caller owns the
        /// entry (the key manager has a box of its own for it) and disposes it. The key is read from the entry without becoming a
        /// string and is erased afterwards. A name that is in use is never written to, and neither is any name while the stored
        /// keys cannot be listed: then it is not known whether the name is free, and the key under it would be replaced unasked.
        /// </summary>
        /// <param name="name">The name of the new key.</param>
        /// <param name="entered">The digits as they were entered.</param>
        /// <returns>What came of it, or <see langword="null"/> when nothing was tried: the slot holds a typed key, its key store takes no keys from here, or the name is not valid.</returns>
        internal KeySlotStoreResult? StoreNewKey(string name, SecureString entered)
        {
            ArgumentNullException.ThrowIfNull(entered);

            if (!ProviderOffersStoreKey || !SecureKeySyntax.IsValidReference(name))
            {
                return null;
            }

            var stored = _keyStore.List();

            if (stored.Error != 0)
            {
                return new KeySlotStoreResult(KeySlotMessage.ListUnreadable, stored.Error);
            }

            if (stored.References.Any(reference => string.Equals(reference, name, StringComparison.OrdinalIgnoreCase)))
            {
                return new KeySlotStoreResult(KeySlotMessage.NameInUse, 0);
            }

            var result = KeyStorage.Store(_keyStore, name, ExpectedKeyHexLength, entered);

            if (result.Message == KeySlotMessage.KeyStored)
            {
                // The key was made for this slot: the slot names it now.
                Reference = name;
            }

            return result;
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
            }
        }
    }
}
