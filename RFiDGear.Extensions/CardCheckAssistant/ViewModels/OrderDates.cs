using System;
using System.Globalization;

namespace RFiDGear.Extensions.CardCheckAssistant.ViewModels
{
    /// <summary>How the dates of an order are shown and grouped. The order system hands them over as text.</summary>
    internal static class OrderDates
    {
        /// <summary>What stands for a group with no name.</summary>
        internal const string Dash = "-";

        /// <summary>Gets the date for the operator: the short date and time of the present culture, or the text as it came when it is no date.</summary>
        /// <param name="text">The date as the order system gave it.</param>
        /// <returns>The text to show; empty for no date.</returns>
        public static string Show(string? text) =>
            TryParse(text, out var date) ? date.ToString("g", CultureInfo.CurrentCulture) : (text ?? string.Empty).Trim();

        /// <summary>Gets the month of a date, such as <c>2026-11</c>, so months sort and group the same in every culture.</summary>
        /// <param name="text">The date as the order system gave it.</param>
        /// <returns>The month; empty when the text is no date.</returns>
        public static string Month(string? text) =>
            TryParse(text, out var date) ? date.ToString("yyyy-MM", CultureInfo.InvariantCulture) : string.Empty;

        /// <summary>Gets the text, or a dash for a group that would have no name.</summary>
        /// <param name="text">The name of the group.</param>
        /// <returns>The name.</returns>
        public static string NamedOrDash(string? text) => string.IsNullOrWhiteSpace(text) ? Dash : text!.Trim();

        private static bool TryParse(string? text, out DateTime date)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                date = default;
                return false;
            }

            return DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.None, out date)
                || DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
        }
    }
}
