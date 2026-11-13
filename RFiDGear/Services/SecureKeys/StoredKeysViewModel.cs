#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security;
using System.Windows;
using System.Windows.Input;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using RFiDGear.Infrastructure;

namespace RFiDGear.Services.SecureKeys
{
    /// <summary>How the key manager of the menu is shown. The application uses a window, the tests a recording.</summary>
    internal interface IStoredKeysLauncher
    {
        /// <summary>Shows the key manager and returns when the person closed it.</summary>
        /// <param name="owner">The window it belongs to; <see langword="null"/> for none.</param>
        void Show(Window? owner);
    }

    /// <summary>The questions the key manager of the menu asks the person. The window answers them with message boxes, the tests with a script.</summary>
    internal interface IStoredKeysDialogs
    {
        /// <summary>Asks whether a stored key may be removed from the Credential Manager.</summary>
        /// <param name="reference">The name of the key.</param>
        /// <returns><see langword="true"/> when the person agrees.</returns>
        bool ConfirmRemove(string reference);

        /// <summary>Asks whether a key that is stored under this name may be replaced by the key that was entered.</summary>
        /// <param name="reference">The name of the key.</param>
        /// <returns><see langword="true"/> when the person agrees.</returns>
        bool ConfirmReplace(string reference);
    }

    /// <summary>One stored key in the list: its name, and the command that removes it. The key itself is never part of it.</summary>
    internal sealed class StoredKeyRow
    {
        /// <summary>Creates the row.</summary>
        /// <param name="reference">The name of the key.</param>
        /// <param name="removeText">The text of the button.</param>
        /// <param name="removeToolTip">The tooltip of the button.</param>
        /// <param name="removeCommand">What the button does.</param>
        internal StoredKeyRow(string reference, string removeText, string removeToolTip, ICommand removeCommand)
        {
            Reference = reference;
            RemoveText = removeText;
            RemoveToolTip = removeToolTip;
            RemoveCommand = removeCommand;
        }

        /// <summary>Gets the name of the key in the Credential Manager, without the prefix.</summary>
        public string Reference { get; }

        /// <summary>Gets the text of the button that removes the key.</summary>
        public string RemoveText { get; }

        /// <summary>Gets the tooltip of the button that removes the key.</summary>
        public string RemoveToolTip { get; }

        /// <summary>Gets the command that removes the key, after the person agreed.</summary>
        public ICommand RemoveCommand { get; }
    }

    /// <summary>A kind of key that can be stored: how many hexadecimal digits it has, and what the person reads for it.</summary>
    internal sealed class KeyKindItem
    {
        /// <summary>Creates the item.</summary>
        /// <param name="hexLength">The number of hexadecimal digits of such a key.</param>
        /// <param name="displayName">What the person reads.</param>
        internal KeyKindItem(int hexLength, string displayName)
        {
            HexLength = hexLength;
            DisplayName = displayName;
        }

        /// <summary>Gets the number of hexadecimal digits of such a key.</summary>
        public int HexLength { get; }

        /// <summary>Gets what the person reads for the kind.</summary>
        public string DisplayName { get; }
    }

    /// <summary>
    /// What the key manager of the menu shows: the keys that are stored in the Windows Credential Manager under the prefix of
    /// RFiDGear, listed by name only; a way to store a key under a name, with a question before an existing key is replaced; and a
    /// way to remove a key, with a question first. No key is ever read, shown or kept here: the digits of a key that is stored
    /// pass through <see cref="StoreEntered"/> from the box of the window into the Credential Manager, and nowhere else.
    /// </summary>
    internal sealed class StoredKeysViewModel : ObservableObject, IDisposable
    {
        private const int DefaultHexLength = 32;

        private readonly WindowsCredentialKeyProvider _keyStore;
        private readonly IKeySupplierStatus _supplier;
        private readonly IStoredKeysDialogs _dialogs;
        private readonly KeySlotProviderCatalog _catalog;
        private readonly Func<string, string> _text;
        private readonly Action<Action> _post;
        private IReadOnlyList<StoredKeyRow> _keys = Array.Empty<StoredKeyRow>();
        private int _listError;
        private string _newName = string.Empty;
        private int _selectedHexLength = DefaultHexLength;
        private string _resultText = string.Empty;
        private bool _resultIsError;
        private bool _disposed;

        /// <summary>Creates the view model; <see cref="Create"/> is the way to get one that reads the texts of the application.</summary>
        /// <param name="keyStore">The Windows Credential Manager provider.</param>
        /// <param name="supplier">Tells whether a program can supply keys.</param>
        /// <param name="dialogs">How to ask the person before a key is removed or replaced.</param>
        /// <param name="text">Gives the text of a resource key.</param>
        /// <param name="post">Runs an action on the thread of the window; <see langword="null"/> runs it where it is called.</param>
        internal StoredKeysViewModel(
            WindowsCredentialKeyProvider keyStore,
            IKeySupplierStatus supplier,
            IStoredKeysDialogs dialogs,
            Func<string, string> text,
            Action<Action>? post)
        {
            _keyStore = keyStore ?? throw new ArgumentNullException(nameof(keyStore));
            _supplier = supplier ?? throw new ArgumentNullException(nameof(supplier));
            _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
            _text = text ?? throw new ArgumentNullException(nameof(text));
            _post = post ?? (action => action());
            _catalog = new KeySlotProviderCatalog(_text);

            KeyKinds = new[]
            {
                new KeyKindItem(12, _text("labelStoredKeysTypeClassic")),
                new KeyKindItem(32, _text("labelStoredKeysTypeDesAes")),
                new KeyKindItem(48, _text("labelStoredKeysTypeThreeKey")),
            };

            CloseCommand = new RelayCommand(() => CloseRequested?.Invoke(this, EventArgs.Empty));

            _supplier.Changed += OnSupplierChanged;
            Refresh();
        }

        /// <summary>Raised when the person asks to close the window.</summary>
        internal event EventHandler? CloseRequested;

        /// <summary>Gets the stored keys, by name, in alphabetical order.</summary>
        public IReadOnlyList<StoredKeyRow> Keys => _keys;

        /// <summary>Gets whether any key is stored.</summary>
        public bool HasKeys => _keys.Count > 0;

        /// <summary>Gets whether the stored keys could not be listed.</summary>
        public bool HasListError => _listError != 0;

        /// <summary>Gets whether the list is empty because nothing is stored, and not because the Credential Manager refused.</summary>
        public bool ShowNoKeysText => !HasKeys && !HasListError;

        /// <summary>Gets the sentence that says why the list could not be shown.</summary>
        public string ListErrorText => HasListError
            ? string.Format(CultureInfo.CurrentCulture, _text("messageStoredKeysListFailed"), _listError)
            : string.Empty;

        /// <summary>Gets the kinds of key that can be stored.</summary>
        public IReadOnlyList<KeyKindItem> KeyKinds { get; }

        /// <summary>Gets or sets how many digits the key that is stored has; one of the numbers of <see cref="KeyKinds"/>.</summary>
        public int SelectedHexLength
        {
            get => _selectedHexLength;
            set
            {
                if (SetProperty(ref _selectedHexLength, value))
                {
                    OnPropertyChanged(nameof(KeyPrompt));
                }
            }
        }

        /// <summary>Gets or sets the name that a key is stored under.</summary>
        public string NewName
        {
            get => _newName;
            set
            {
                if (SetProperty(ref _newName, value ?? string.Empty))
                {
                    ClearResult();
                    OnPropertyChanged(nameof(IsNameValid));
                    OnPropertyChanged(nameof(CanStoreKey));
                }
            }
        }

        /// <summary>Gets whether the name is in the alphabet of references.</summary>
        public bool IsNameValid => SecureKeySyntax.IsValidReference(_newName);

        /// <summary>Gets whether a key can be stored: the name is valid.</summary>
        public bool CanStoreKey => IsNameValid;

        /// <summary>Gets the limit of the length of a name.</summary>
        public int MaxReferenceLength => SecureKeySyntax.MaxReferenceLength;

        /// <summary>Gets the sentence that asks for the digits of the key.</summary>
        public string KeyPrompt => string.Format(CultureInfo.CurrentCulture, _text("labelKeySlotStoreKeyPrompt"), _selectedHexLength);

        /// <summary>Gets what came of the last thing that was tried; empty before.</summary>
        public string ResultText => _resultText;

        /// <summary>Gets whether there is something to say about the last thing that was tried.</summary>
        public bool HasResult => _resultText.Length > 0;

        /// <summary>Gets whether the last thing that was tried did not work.</summary>
        public bool ResultIsError => _resultIsError;

        /// <summary>Gets the title of the window.</summary>
        public string Title => _text("windowTitleKeyManager");

        /// <summary>Gets the sentence that says what the window is for.</summary>
        public string IntroText => _text("labelStoredKeysIntro");

        /// <summary>Gets the header of the list.</summary>
        public string ListHeader => _text("labelStoredKeysList");

        /// <summary>Gets the sentence for an empty list.</summary>
        public string NoKeysText => _text("labelStoredKeysNone");

        /// <summary>Gets the header of the part that stores a key.</summary>
        public string AddHeader => _text("labelStoredKeysAdd");

        /// <summary>Gets the label of the name of the key.</summary>
        public string NameLabel => _text("labelKeyManagerKeyName");

        /// <summary>Gets the tooltip of the name of the key.</summary>
        public string NameToolTip => _text("toolTipKeySlotReference");

        /// <summary>Gets the label of the kind of key.</summary>
        public string KindLabel => _text("labelStoredKeysType");

        /// <summary>Gets the tooltip of the kind of key.</summary>
        public string KindToolTip => _text("toolTipStoredKeysKind");

        /// <summary>Gets the text of the check box that shows the key that is entered.</summary>
        public string ShowKeyText => _text("labelKeyManagerShowKey");

        /// <summary>Gets the tooltip of the check box that shows the key that is entered.</summary>
        public string ShowKeyToolTip => _text("toolTipKeyManagerShowKey");

        /// <summary>Gets the text of the button that stores the key.</summary>
        public string StoreButtonText => _text("buttonContentKeyManagerStoreKey");

        /// <summary>Gets the tooltip of the button that stores the key.</summary>
        public string StoreToolTip => _text("toolTipKeySlotStoreKey");

        /// <summary>Gets the text of the button that closes the window.</summary>
        public string CloseText => _text("buttonContentKeyManagerClose");

        /// <summary>Gets the sentence that says whether a program can supply keys now.</summary>
        public string SupplierStatusText => _catalog.StatusText(_supplier.Current);

        /// <summary>Gets whether a program is connected and supplies keys now.</summary>
        public bool SupplierIsConnected => _supplier.Current == KeySupplierAvailability.Connected;

        /// <summary>Gets the command that closes the window.</summary>
        public ICommand CloseCommand { get; }

        /// <summary>Makes the view model.</summary>
        /// <param name="keyStore">The Windows Credential Manager provider.</param>
        /// <param name="supplier">Tells whether a program can supply keys.</param>
        /// <param name="dialogs">How to ask the person before a key is removed or replaced.</param>
        /// <param name="text">Gives the text of a resource key; <see langword="null"/> for the resources of the application.</param>
        /// <param name="post">Runs an action on the thread of the window; <see langword="null"/> runs it where it is called.</param>
        /// <returns>The view model, which the caller disposes.</returns>
        internal static StoredKeysViewModel Create(
            WindowsCredentialKeyProvider keyStore,
            IKeySupplierStatus supplier,
            IStoredKeysDialogs dialogs,
            Func<string, string>? text = null,
            Action<Action>? post = null) =>
            // The resources read the settings from disk on every lookup; the language cannot change while the window is open.
            new StoredKeysViewModel(keyStore, supplier, dialogs, CachedText.Of(text ?? ResourceLoader.GetResource), post);

        /// <summary>Reads the list from the Credential Manager again.</summary>
        internal void Refresh()
        {
            var list = _keyStore.List();

            _listError = list.Error;
            _keys = list.References.Select(CreateRow).ToList();

            OnPropertyChanged(nameof(Keys));
            OnPropertyChanged(nameof(HasKeys));
            OnPropertyChanged(nameof(HasListError));
            OnPropertyChanged(nameof(ShowNoKeysText));
            OnPropertyChanged(nameof(ListErrorText));
        }

        /// <summary>
        /// Stores the key that was entered under the name, after asking when a key with that name is stored already. The digits
        /// are read from the entry without becoming a string and are erased; the caller owns the entry and clears it when this
        /// says that the key was tried.
        /// </summary>
        /// <param name="entered">The digits as they were entered.</param>
        /// <returns>
        /// <see langword="true"/> when a key was tried and the digits are no longer needed; <see langword="false"/> when nothing was
        /// stored and the digits are kept while the person goes on: the name is not valid, the list could not be read, the person did
        /// not agree to replace the key, or the entry is not a key.
        /// </returns>
        internal bool StoreEntered(SecureString entered)
        {
            ArgumentNullException.ThrowIfNull(entered);

            if (!CanStoreKey)
            {
                return false;
            }

            // Another program may have stored or removed a key since the list was read; the question is about what is there now.
            Refresh();

            // A list that cannot be read is no proof that the name is free: a key under it would be replaced without the question.
            // Nothing is tried, the error stays in view and the digits stay in the box.
            if (HasListError)
            {
                return false;
            }

            if (_keys.Any(row => string.Equals(row.Reference, _newName, StringComparison.OrdinalIgnoreCase)) && !_dialogs.ConfirmReplace(_newName))
            {
                return false;
            }

            var result = KeyStorage.Store(_keyStore, _newName, _selectedHexLength, entered);

            switch (result.Message)
            {
                case KeySlotMessage.KeyStored:
                    SetResult(_text("messageKeySlotKeyStored"), false);
                    Refresh();
                    break;
                case KeySlotMessage.KeyNotStored:
                    SetResult(string.Format(CultureInfo.CurrentCulture, _text("messageKeySlotKeyNotStored"), result.Detail), true);
                    break;
                default:
                    SetResult(string.Format(CultureInfo.CurrentCulture, _text("messageKeySlotKeyInvalid"), result.Detail), true);
                    break;
            }

            // An entry that is not a key is not stored and is not thrown away: the person can correct it, which is what the colour of the
            // box is for.
            return result.Message != KeySlotMessage.KeyInvalid;
        }

        /// <summary>
        /// Asks the supplier again. A client that registers as supplier raises no event, so the window calls this now and then
        /// while it is open.
        /// </summary>
        internal void RefreshSupplierStatus()
        {
            if (_disposed)
            {
                return;
            }

            OnPropertyChanged(nameof(SupplierStatusText));
            OnPropertyChanged(nameof(SupplierIsConnected));
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _supplier.Changed -= OnSupplierChanged;
        }

        private StoredKeyRow CreateRow(string reference) =>
            new StoredKeyRow(
                reference,
                _text("buttonContentStoredKeysRemove"),
                _text("toolTipStoredKeysRemove"),
                new RelayCommand(() => Remove(reference)));

        private void Remove(string reference)
        {
            if (!_dialogs.ConfirmRemove(reference))
            {
                return;
            }

            var error = _keyStore.Remove(reference);

            // A key that is gone already, removed by another program while the window was open, is what the person wanted.
            if (error == 0 || error == CredentialErrors.NotFound)
            {
                SetResult(string.Format(CultureInfo.CurrentCulture, _text("messageStoredKeysRemoved"), reference), false);
            }
            else
            {
                SetResult(string.Format(CultureInfo.CurrentCulture, _text("messageStoredKeysNotRemoved"), reference, error), true);
            }

            Refresh();
        }

        private void OnSupplierChanged(object? sender, EventArgs e) => _post(RefreshSupplierStatus);

        private void ClearResult()
        {
            if (_resultText.Length == 0 && !_resultIsError)
            {
                return;
            }

            SetResult(string.Empty, false);
        }

        private void SetResult(string text, bool isError)
        {
            _resultText = text;
            _resultIsError = isError;
            OnPropertyChanged(nameof(ResultText));
            OnPropertyChanged(nameof(HasResult));
            OnPropertyChanged(nameof(ResultIsError));
        }
    }
}
