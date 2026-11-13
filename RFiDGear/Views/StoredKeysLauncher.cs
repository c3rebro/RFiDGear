#nullable enable
using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using RFiDGear.Infrastructure;
using RFiDGear.Services.SecureKeys;

namespace RFiDGear.View
{
    /// <summary>Shows the key manager of the menu as a modal window of the main window, on the real Credential Manager.</summary>
    internal sealed class WpfStoredKeysLauncher : IStoredKeysLauncher
    {
        /// <inheritdoc />
        public void Show(Window? owner)
        {
            var dispatcher = Dispatcher.CurrentDispatcher;

            using var viewModel = StoredKeysViewModel.Create(
                new WindowsCredentialKeyProvider(new NativeCredentialStore()),
                new BridgeKeySupplierStatus(),
                new WpfStoredKeysDialogs(() => Application.Current?.Windows.OfType<Window>().FirstOrDefault(candidate => candidate.IsActive) ?? owner),
                post: action => dispatcher.BeginInvoke(action));

            new StoredKeysView(viewModel) { Owner = owner }.ShowDialog();
        }
    }

    /// <summary>The questions of the key manager of the menu, asked with message boxes.</summary>
    internal sealed class WpfStoredKeysDialogs : IStoredKeysDialogs
    {
        private readonly Func<Window?> _owner;

        /// <summary>Creates the dialogs.</summary>
        /// <param name="owner">Gets the window that the dialogs belong to; may give <see langword="null"/>.</param>
        internal WpfStoredKeysDialogs(Func<Window?> owner)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        /// <inheritdoc />
        public bool ConfirmRemove(string reference) => Ask("messageStoredKeysConfirmRemove", reference);

        /// <inheritdoc />
        public bool ConfirmReplace(string reference) => Ask("messageStoredKeysConfirmReplace", reference);

        private bool Ask(string textKey, string reference)
        {
            var text = string.Format(CultureInfo.CurrentCulture, ResourceLoader.GetResource(textKey), reference);
            var caption = ResourceLoader.GetResource("windowTitleKeyManager");
            var owner = _owner();

            var answer = owner != null
                ? MessageBox.Show(owner, text, caption, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No)
                : MessageBox.Show(text, caption, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);

            return answer == MessageBoxResult.Yes;
        }
    }
}
