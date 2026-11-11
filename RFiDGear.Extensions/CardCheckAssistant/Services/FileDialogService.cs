using System;
using System.IO;

using Microsoft.Win32;

namespace RFiDGear.Extensions.CardCheckAssistant.Services
{
    /// <summary>Lets the user pick a project file or a folder; a seam so the view model can be tested without a dialog.</summary>
    internal interface IFileDialogService
    {
        /// <summary>Shows the picker.</summary>
        /// <param name="currentPath">The path already entered, to start from; may be blank.</param>
        /// <param name="title">The dialog title, in the page's language.</param>
        /// <param name="filter">The file filter in <see cref="FileDialog.Filter"/> form, in the page's language.</param>
        /// <returns>The chosen full path, or <c>null</c> if the user cancelled.</returns>
        string? PickProjectFile(string? currentPath, string title, string filter);

        /// <summary>Shows the folder picker.</summary>
        /// <param name="currentPath">The folder already entered, to start from; may be blank.</param>
        /// <param name="title">The dialog title, in the page's language.</param>
        /// <returns>The chosen full path of the folder, or <c>null</c> if the user cancelled.</returns>
        string? PickFolder(string? currentPath, string title);

        /// <summary>Asks for the file the settings are to be written to.</summary>
        /// <param name="suggestedName">The name offered first.</param>
        /// <param name="title">The title of the dialog.</param>
        /// <param name="filter">The filter of the dialog.</param>
        /// <returns>The file, or <c>null</c> if the operator cancelled.</returns>
        string? PickSettingsFileToSave(string suggestedName, string title, string filter);

        /// <summary>Asks for a settings file to read.</summary>
        /// <param name="title">The title of the dialog.</param>
        /// <param name="filter">The filter of the dialog.</param>
        /// <returns>The file, or <c>null</c> if the operator cancelled.</returns>
        string? PickSettingsFileToOpen(string title, string filter);
    }

    /// <summary>The Windows open-file and open-folder dialogs.</summary>
    internal sealed class OpenFileDialogService : IFileDialogService
    {
        /// <inheritdoc/>
        public string? PickFolder(string? currentPath, string title)
        {
            var dialog = new OpenFolderDialog
            {
                Title = title,
                Multiselect = false
            };

            // Not checked for existence, for the same reason as in the file picker: that is a file-system call on the UI
            // thread, and the dialog copes with a folder that is not there.
            if (!string.IsNullOrWhiteSpace(currentPath))
            {
                try
                {
                    dialog.InitialDirectory = currentPath.Trim();
                }
                catch (ArgumentException)
                {
                    // The text is not a usable path; the dialog simply opens where it likes.
                }
            }

            return dialog.ShowDialog() == true ? dialog.FolderName : null;
        }

        /// <inheritdoc/>
        public string? PickSettingsFileToSave(string suggestedName, string title, string filter)
        {
            var dialog = new SaveFileDialog
            {
                Title = title,
                Filter = filter,
                FileName = suggestedName,
                DefaultExt = ".json",
                AddExtension = true,
                OverwritePrompt = true
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        public string? PickSettingsFileToOpen(string title, string filter)
        {
            var dialog = new OpenFileDialog
            {
                Title = title,
                Filter = filter,
                CheckFileExists = true,
                Multiselect = false
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        public string? PickProjectFile(string? currentPath, string title, string filter)
        {
            var dialog = new OpenFileDialog
            {
                Title = title,
                Filter = filter,
                CheckFileExists = true,
                Multiselect = false
            };

            TryStartFrom(dialog, currentPath);

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        private static void TryStartFrom(OpenFileDialog dialog, string? currentPath)
        {
            if (string.IsNullOrWhiteSpace(currentPath))
            {
                return;
            }

            try
            {
                var directory = Path.GetDirectoryName(currentPath.Trim());
                if (!string.IsNullOrEmpty(directory))
                {
                    // Not checked for existence: that would be a file-system call on the UI thread, which on an
                    // unreachable network path freezes the window. The dialog copes with a folder that is not there.
                    dialog.InitialDirectory = directory;
                    dialog.FileName = Path.GetFileName(currentPath.Trim());
                }
            }
            catch (ArgumentException)
            {
                // The text is not a usable path; the dialog simply opens where it likes.
                dialog.InitialDirectory = string.Empty;
            }
        }
    }
}
