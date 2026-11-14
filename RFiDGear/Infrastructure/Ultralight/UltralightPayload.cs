using System;
using System.Text;

namespace RFiDGear.Infrastructure.Ultralight
{
    /// <summary>
    /// The data of a write as the project keeps it: one string of hexadecimal digits, 8 per page (4 bytes). The string of a project
    /// has no separators; what a person types may have white space between the digits.
    /// </summary>
    public static class UltralightPayload
    {
        /// <summary>The most pages one write can hold: all pages of the largest chip.</summary>
        public static int MaxPages => UltralightMemoryMap.MaxPageCount;

        /// <summary>
        /// Reads the digits.
        /// </summary>
        /// <param name="text">The text. Spaces, tabs and line breaks are ignored; nothing else but hexadecimal digits is accepted
        /// (a prefix such as <c>0x</c> is not).</param>
        /// <param name="data">The bytes, a multiple of 4.</param>
        /// <returns>
        /// <c>false</c> for no text, no digits, a number of digits that is not a multiple of 8, a character that is not a digit,
        /// and more than <see cref="MaxPages"/> pages.
        /// </returns>
        public static bool TryParse(string text, out byte[] data)
        {
            data = null;

            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            var digits = new StringBuilder();

            foreach (var c in text)
            {
                if (c == ' ' || c == '\t' || c == '\r' || c == '\n')
                {
                    continue;
                }

                // Uri.IsHexDigit is true for 0-9, a-f and A-F only.
                if (!Uri.IsHexDigit(c) || digits.Length >= 8 * MaxPages)
                {
                    return false;
                }

                digits.Append(c);
            }

            if (digits.Length == 0 || digits.Length % 8 != 0)
            {
                return false;
            }

            data = Convert.FromHexString(digits.ToString());

            return true;
        }

        /// <summary>
        /// Writes the digits the way a project keeps them.
        /// </summary>
        /// <param name="data">The bytes, a multiple of 4.</param>
        /// <returns>Upper-case hexadecimal digits without separators.</returns>
        /// <exception cref="ArgumentException">The length is not a multiple of 4.</exception>
        public static string Format(byte[] data)
        {
            PageCount(data);

            return Convert.ToHexString(data);
        }

        /// <summary>
        /// The number of pages the bytes make.
        /// </summary>
        /// <param name="data">The bytes.</param>
        /// <returns>The length divided by 4.</returns>
        /// <exception cref="ArgumentException">The length is not a multiple of 4.</exception>
        public static int PageCount(byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (data.Length % UltralightMemoryMap.PageSize != 0)
            {
                throw new ArgumentException("The data is not a whole number of 4-byte pages.", nameof(data));
            }

            return data.Length / UltralightMemoryMap.PageSize;
        }
    }
}
