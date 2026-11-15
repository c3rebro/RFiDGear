using CommunityToolkit.Mvvm.ComponentModel;

using System;
using System.Linq;

namespace RFiDGear.ViewModel.TaskSetupViewModels
{
    /// <summary>
    /// One row of the grid of pages in the dialog of an Ultralight task: a page number and the four bytes of the page as eight
    /// hexadecimal digits. In a write the person types the digits and they are the data of the task; in a read the row shows what the
    /// last read gave and cannot be typed into. A row is a view of the task and no part of the project.
    /// </summary>
    public sealed class UltralightPageRow : ObservableObject
    {
        private int pageNumber;
        private string hex;

        internal UltralightPageRow(string hex, bool isReadOnly)
        {
            this.hex = Normalize(hex);
            IsReadOnly = isReadOnly;
        }

        /// <summary>
        /// Gets the number of the page. The task numbers its rows from its first page, and numbers them again when that page changes or
        /// a row is removed.
        /// </summary>
        public int PageNumber
        {
            get => pageNumber;
            internal set => SetProperty(ref pageNumber, value);
        }

        /// <summary>
        /// Gets or sets the four bytes of the page as hexadecimal digits: no white space, upper case. A value that is not eight digits
        /// is kept as it is typed, so that the person sees what is wrong, and the row is not valid.
        /// </summary>
        public string Hex
        {
            get => hex;
            set
            {
                hex = Normalize(value);

                // Raised even when the digits are the same: the box shows what was typed, and shows the normalized text after it.
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsValid));
            }
        }

        /// <summary>Gets whether the row can be typed into: a write can, a read only shows.</summary>
        public bool IsReadOnly { get; }

        /// <summary>
        /// Gets whether the row holds the eight hexadecimal digits of a page: <see langword="true"/> or <see langword="false"/> for a
        /// row of a write, and <see langword="null"/> for a row that only shows (there is nothing to be right or wrong about).
        /// </summary>
        public bool? IsValid => IsReadOnly ? (bool?)null : IsWholePage(hex);

        /// <summary>The mark that the data of a task puts in front of the rows, and between them, while a row is not a whole page.</summary>
        internal const char RowMark = '|';

        /// <summary>Tells whether digits are the eight hexadecimal digits of one page.</summary>
        /// <param name="digits">The digits, as the row holds them.</param>
        /// <returns><see langword="true"/> for exactly eight hexadecimal digits.</returns>
        internal static bool IsWholePage(string digits) => digits != null && digits.Length == 8 && digits.All(Uri.IsHexDigit);

        // No white space, upper case, and never the mark of the rows: a row that held it would be cut in two when the data is read again.
        private static string Normalize(string value) =>
            string.IsNullOrEmpty(value) ? string.Empty : string.Concat(value.Where(c => !char.IsWhiteSpace(c) && c != RowMark)).ToUpperInvariant();
    }
}
