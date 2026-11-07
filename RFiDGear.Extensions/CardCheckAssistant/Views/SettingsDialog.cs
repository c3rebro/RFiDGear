using System;
using System.Windows;

using RFiDGear.Extensions.CardCheckAssistant.Localization;
using RFiDGear.Extensions.CardCheckAssistant.Services;
using RFiDGear.Extensions.CardCheckAssistant.ViewModels;

using Keys = RFiDGear.Extensions.CardCheckAssistant.Localization.ExtensionStrings.Keys;

namespace RFiDGear.Extensions.CardCheckAssistant.Views
{
    /// <summary>
    /// The settings window: a modal window with a <see cref="SettingsView"/> in it. The window only does what a view model
    /// cannot: it closes when asked to, and it asks the view model before it lets itself be closed, so a window closed
    /// with the cross or Alt+F4 does not lose changes without asking.
    /// </summary>
    internal sealed class SettingsDialog : ISettingsDialog
    {
        private readonly IAssistantSettingsEditor _editor;
        private readonly IFileDialogService _dialogs;
        private readonly ISettingsPrompts _prompts;

        /// <param name="editor">Reads, saves and tests the settings.</param>
        /// <param name="dialogs">The pickers.</param>
        /// <param name="prompts">The question asked before changes are thrown away; a message box when omitted.</param>
        public SettingsDialog(IAssistantSettingsEditor editor, IFileDialogService dialogs, ISettingsPrompts? prompts = null)
        {
            _editor = editor ?? throw new ArgumentNullException(nameof(editor));
            _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
            _prompts = prompts ?? new MessageBoxSettingsPrompts();
        }

        /// <inheritdoc/>
        public bool Show(LocalizedStrings strings) => Show(strings, null);

        /// <summary>Shows the window; the second argument lets a test act once the window is on the screen.</summary>
        /// <param name="strings">The page's texts.</param>
        /// <param name="whenShown">Called once, when the window has been loaded; none when omitted.</param>
        /// <returns><c>true</c> if the settings were saved.</returns>
        internal bool Show(LocalizedStrings strings, Action<Window, SettingsViewModel>? whenShown)
        {
            if (strings == null)
            {
                throw new ArgumentNullException(nameof(strings));
            }

            var saved = false;
            var owner = ImportDialogs.ActiveWindow();

            using var viewModel = new SettingsViewModel(_editor, _dialogs, _prompts, strings);
            var window = new Window
            {
                Title = strings[Keys.SettingsTitle],
                Content = new SettingsView(viewModel),
                Owner = owner,
                ShowInTaskbar = false,
                Width = 660,
                Height = 740,
                MinWidth = 600,
                MinHeight = 520,
                WindowStartupLocation = owner == null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner
            };

            // Closing is asked for by the view model; the window then asks it once more, because that is also how the cross
            // arrives. A request that is refused (changes the operator wants to keep) leaves the window as it is.
            viewModel.CloseRequested += (_, e) =>
            {
                saved = e.Saved;
                window.Close();
            };
            window.Closing += (_, e) =>
            {
                if (!viewModel.TryRequestClose())
                {
                    e.Cancel = true;
                }
            };

            if (whenShown != null)
            {
                window.Loaded += (_, _) => whenShown(window, viewModel);
            }

            window.ShowDialog();

            return saved;
        }
    }

    /// <summary>The question before changes are thrown away, as a message box over the window the operator is working in.</summary>
    internal sealed class MessageBoxSettingsPrompts : ISettingsPrompts
    {
        /// <inheritdoc/>
        public bool ConfirmDiscard(string title, string message)
        {
            var owner = ImportDialogs.ActiveWindow();

            // "No" is what Escape, the close button and the Enter key mean: changes are kept unless the operator says yes.
            var answer = owner == null
                ? MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No)
                : MessageBox.Show(owner, message, title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);

            return answer == MessageBoxResult.Yes;
        }
    }
}
