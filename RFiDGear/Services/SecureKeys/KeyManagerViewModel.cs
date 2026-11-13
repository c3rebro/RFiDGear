#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using RFiDGear.Infrastructure;

namespace RFiDGear.Services.SecureKeys
{
    /// <summary>How the key manager window is shown. The control uses windows, the tests a recording.</summary>
    internal interface IKeyManagerLauncher
    {
        /// <summary>Shows the key manager and returns when the person closed it.</summary>
        /// <param name="viewModel">What the window shows; the launcher does not dispose it.</param>
        /// <param name="owner">The window it belongs to; <see langword="null"/> for none.</param>
        void Show(KeyManagerViewModel viewModel, System.Windows.Window? owner);
    }

    /// <summary>
    /// What the key manager window shows: one row for each key slot of a section of a task, and whether a program can supply
    /// keys. Changes are applied at once, through the task, as in the editor of a key; the task dialog is the transaction (it
    /// works on a copy of the task and drops it on Cancel), so the manager has nothing to commit and nothing to roll back.
    /// </summary>
    internal sealed class KeyManagerViewModel : ObservableObject, IDisposable
    {
        private readonly IKeySupplierStatus _supplier;
        private readonly KeySlotProviderCatalog _catalog;
        private readonly Func<string, string> _text;
        private readonly Action<Action> _post;
        private bool _disposed;

        /// <summary>Creates the view model.</summary>
        /// <param name="rows">The slots, in the order to show them.</param>
        /// <param name="supplier">Tells whether a program can supply keys.</param>
        /// <param name="catalog">Gives the text that says so.</param>
        /// <param name="text">Gives the text of a resource key.</param>
        /// <param name="post">
        /// Runs an action on the thread of the window; <see langword="null"/> runs it where it is called. The status of the supplier
        /// changes on the threads of the bridge, and a window may only be touched from its own.
        /// </param>
        internal KeyManagerViewModel(
            IReadOnlyList<KeyManagerSlotRow> rows,
            IKeySupplierStatus supplier,
            KeySlotProviderCatalog catalog,
            Func<string, string> text,
            Action<Action>? post = null)
        {
            Rows = rows ?? throw new ArgumentNullException(nameof(rows));
            _supplier = supplier ?? throw new ArgumentNullException(nameof(supplier));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _text = text ?? throw new ArgumentNullException(nameof(text));
            _post = post ?? (action => action());

            CloseCommand = new RelayCommand(() => CloseRequested?.Invoke(this, EventArgs.Empty));

            _supplier.Changed += OnSupplierChanged;

            foreach (var row in Rows)
            {
                row.PropertyChanged += OnRowChanged;
            }
        }

        /// <summary>Raised when the person asks to close the window.</summary>
        internal event EventHandler? CloseRequested;

        /// <summary>Gets the slots, in the order to show them.</summary>
        public IReadOnlyList<KeyManagerSlotRow> Rows { get; }

        /// <summary>Gets whether there is a key to manage at all.</summary>
        public bool HasRows => Rows.Count > 0;

        /// <summary>Gets the title of the window.</summary>
        public string Title => _text("windowTitleKeyManager");

        /// <summary>Gets the sentence that says what the choices mean.</summary>
        public string IntroText => _text("labelKeyManagerIntro");

        /// <summary>Gets the sentence for a section that has no key.</summary>
        public string NoSlotsText => _text("labelKeyManagerNoSlots");

        /// <summary>Gets the text of the button that closes the window.</summary>
        public string CloseText => _text("buttonContentKeyManagerClose");

        /// <summary>Gets the sentence that says whether a program can supply keys now.</summary>
        public string SupplierStatusText => _catalog.StatusText(_supplier.Current);

        /// <summary>Gets whether a program is connected and supplies keys now.</summary>
        public bool SupplierIsConnected => _supplier.Current == KeySupplierAvailability.Connected;

        /// <summary>Gets whether the status is worth showing: some slot is named in the key store of the external application.</summary>
        public bool ShowSupplierStatus => Rows.Any(row => row.UsesExternalApplication);

        /// <summary>Gets the command that closes the window.</summary>
        public ICommand CloseCommand { get; }

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

            foreach (var row in Rows)
            {
                row.PropertyChanged -= OnRowChanged;
                row.Dispose();
            }
        }

        /// <summary>Makes the view model of a section of a task.</summary>
        /// <param name="host">The task.</param>
        /// <param name="slotIds">The slots of the section, in the order to show them.</param>
        /// <param name="providers">The key stores the registry knows.</param>
        /// <param name="dialogs">How to ask the person before a typed key is discarded.</param>
        /// <param name="keyStore">The Windows Credential Manager provider, to store a key with.</param>
        /// <param name="supplier">Tells whether a program can supply keys.</param>
        /// <param name="text">Gives the text of a resource key; <see langword="null"/> for the resources of the application.</param>
        /// <param name="post">Runs an action on the thread of the window; <see langword="null"/> runs it where it is called.</param>
        /// <returns>The view model, which the caller disposes.</returns>
        internal static KeyManagerViewModel Create(
            IKeySlotHost host,
            IReadOnlyList<string> slotIds,
            IReadOnlyList<SecureKeyProviderInfo> providers,
            IKeySlotEditorDialogs dialogs,
            WindowsCredentialKeyProvider keyStore,
            IKeySupplierStatus supplier,
            Func<string, string>? text = null,
            Action<Action>? post = null)
        {
            ArgumentNullException.ThrowIfNull(host);
            ArgumentNullException.ThrowIfNull(slotIds);

            // A text is read from the resources once for the life of the manager. The resources are read from the settings on every
            // lookup, and a change of a name makes every row ask for all its texts again; the language cannot change while the
            // window is open (it needs a restart).
            var resolve = CachedText.Of(text ?? ResourceLoader.GetResource);
            var catalog = new KeySlotProviderCatalog(resolve);
            var items = catalog.Describe(providers);

            var rows = slotIds
                .Distinct(StringComparer.Ordinal)
                .Select(slotId => new KeyManagerSlotRow(
                    new KeySlotEditorState(host, slotId, providers, dialogs, keyStore, resolve),
                    KeySlotNames.For(slotId, resolve),
                    items,
                    resolve))
                .ToList();

            return new KeyManagerViewModel(rows, supplier, catalog, resolve, post);
        }

        private void OnSupplierChanged(object? sender, EventArgs e) => _post(RefreshSupplierStatus);

        private void OnRowChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) =>
            OnPropertyChanged(nameof(ShowSupplierStatus));
    }
}
