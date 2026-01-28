using System;
using System.Globalization;

namespace RFiDGear.Extensions.VCNEditor.ViewModel
{
    /// <summary>
    /// Provides shared helpers for parsing date and time values in dialog view models.
    /// </summary>
    public static class TimeParsing
    {
        /// <summary>
        /// Parses a time value by combining it with the supplied date and matching the provided formats.
        /// </summary>
        /// <param name="baseDate">The date portion used to build the combined date/time string.</param>
        /// <param name="timeText">The time text expected in constant ("c") format.</param>
        /// <param name="culture">The culture used for parsing the time and date values.</param>
        /// <param name="parseFormats">The date/time formats used by <see cref="DateTime.ParseExact(string,string[],IFormatProvider,DateTimeStyles)"/>.</param>
        /// <returns>The parsed <see cref="DateTime"/> value.</returns>
        /// <exception cref="FormatException">
        /// Thrown when <paramref name="timeText"/> or the combined date/time string does not match the expected formats.
        /// </exception>
        public static DateTime ParseDateTimeFromTimeText(DateTime baseDate, string timeText, CultureInfo culture, string[] parseFormats)
        {
            var dateText = baseDate.ToShortDateString();
            var combinedText = $"{dateText} {TimeSpan.ParseExact(timeText, "c", culture)}";

            return DateTime.ParseExact(combinedText, parseFormats, culture, DateTimeStyles.None);
        }
    }
}
