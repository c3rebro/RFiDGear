using System;
using System.IO;

using Microsoft.Win32;

namespace RFiDGear.Extensions.CardCheckAssistant.Services
{
    /// <summary>Lets the user pick the project file; a seam so the view model can be tested without a dialog.</summary>
    internal interface IFileDialogService
    {
        /// <summary>Shows the picker.</summary>
        /// <param name="currentPath">The path already entered, to start from; may be blank.</param>
        /// <param name="title">The dialog title, in the page's language.</param>
        /// <param name="filter">The file filter in <see cref="FileDialog.Filter"/> form, in the page's language.</param>
        /// <returns>The chosen full path, or <c>null</c> if the user cancelled.</returns>
        string? PickProjectFile(string? currentPath, string title, string filter);
    }

    /// <summary>The Windows open-file dialog.</summary>
    internal sealed class OpenFileDialogService : IFileDialogService
    {
        /// <inheritdoc/>
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
                if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                {
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
