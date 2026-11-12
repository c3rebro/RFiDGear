#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Input;
using RFiDGear.Infrastructure;
using RFiDGear.Services.SecureKeys;
using RFiDGear.UI.MVVMDialogs.ViewModels;
using RFiDGear.UI.MVVMDialogs.ViewModels.Interfaces;

namespace RFiDGear.ViewModel
{
    /// <summary>The questions of the key migration, asked through the dialog collection of the main window.</summary>
    internal sealed class MvvmKeyMigrationDialogs : IKeyMigrationDialogs
    {
        private readonly IList<IDialogViewModel> _dialogs;
        private readonly Window? _parent;

        /// <summary>Creates the dialogs.</summary>
        /// <param name="dialogs">The dialog collection that the main window shows.</param>
        /// <param name="parent">The window the dialogs belong to; may be <see langword="null"/>.</param>
        internal MvvmKeyMigrationDialogs(IList<IDialogViewModel> dialogs, Window? parent)
        {
            _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
            _parent = parent;
        }

        /// <inheritdoc />
        public bool Confirm(string text) => Ask(text, MessageBoxButton.OKCancel, MessageBoxImage.Information) == MessageBoxResult.OK;

        /// <inheritdoc />
        public string? ChooseSource()
        {
            var dialog = new OpenFileDialogViewModel
            {
                Title = ResourceLoader.GetResource("windowCaptionKeyMigrationSource"),
                Filter = ResourceLoader.GetResource("filterStringSaveTasks"),
                ParentWindow = _parent!,
                Multiselect = false,
            };

            return dialog.Show(_dialogs) ? dialog.FileName : null;
        }

        /// <inheritdoc />
        public string? ChooseTarget(string suggestedPath)
        {
            var dialog = new SaveFileDialogViewModel
            {
                Title = ResourceLoader.GetResource("windowCaptionKeyMigrationTarget"),
                Filter = ResourceLoader.GetResource("filterStringSaveTasks"),
                InitialDirectory = Path.GetDirectoryName(suggestedPath),
                FileName = Path.GetFileName(suggestedPath),
                ParentWindow = _parent!,
            };

            return dialog.Show(_dialogs) ? dialog.FileName : null;
        }

        /// <inheritdoc />
        public void Inform(string text, bool isError) =>
            Ask(text, MessageBoxButton.OK, isError ? MessageBoxImage.Error : MessageBoxImage.Information);

        /// <inheritdoc />
        public bool AskDeleteOriginal(string text) => Ask(text, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

        /// <inheritdoc />
        public IDisposable Working()
        {
            var previous = Mouse.OverrideCursor;
            Mouse.OverrideCursor = Cursors.AppStarting;

            return new Restore(() => Mouse.OverrideCursor = previous);
        }

        private MessageBoxResult Ask(string text, MessageBoxButton buttons, MessageBoxImage image) =>
            new MessageBoxViewModel
            {
                ParentWindow = _parent!,
                Caption = ResourceLoader.GetResource("captionKeyMigration"),
                Message = text,
                Buttons = buttons,
                Image = image,
            }.Show(_dialogs);

        private sealed class Restore : IDisposable
        {
            private Action? _restore;

            internal Restore(Action restore)
            {
                _restore = restore;
            }

            public void Dispose()
            {
                _restore?.Invoke();
                _restore = null;
            }
        }
    }
}
