#nullable enable
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.ExtensionPackages;
using RFiDGear.UI.MVVMDialogs.ViewModels;
using RFiDGear.UI.MVVMDialogs.ViewModels.Interfaces;

namespace RFiDGear.ViewModel
{
    /// <summary>The questions of the installation of an extension package, asked through the dialog collection of the main window.</summary>
    internal sealed class MvvmExtensionInstallDialogs : IExtensionInstallDialogs
    {
        private readonly IList<IDialogViewModel> _dialogs;
        private readonly Window? _parent;

        /// <summary>Creates the dialogs.</summary>
        /// <param name="dialogs">The dialog collection that the main window shows.</param>
        /// <param name="parent">The window the dialogs belong to; may be <see langword="null"/>.</param>
        internal MvvmExtensionInstallDialogs(IList<IDialogViewModel> dialogs, Window? parent)
        {
            _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
            _parent = parent;
        }

        /// <inheritdoc />
        public string? ChoosePackage()
        {
            var dialog = new OpenFileDialogViewModel
            {
                Title = ResourceLoader.GetResource("windowCaptionExtensionPackage"),
                Filter = ResourceLoader.GetResource("filterStringExtensionPackages"),
                ParentWindow = _parent!,
                Multiselect = false,
            };

            return dialog.Show(_dialogs) ? dialog.FileName : null;
        }

        /// <inheritdoc />
        public bool Confirm(string text) => Ask(text, MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK;

        /// <inheritdoc />
        public void Inform(string text, bool isError) =>
            Ask(text, MessageBoxButton.OK, isError ? MessageBoxImage.Error : MessageBoxImage.Information);

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
                Caption = ResourceLoader.GetResource("captionExtensionInstall"),
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
