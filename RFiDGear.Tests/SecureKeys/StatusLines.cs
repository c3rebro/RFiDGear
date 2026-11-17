#nullable enable
using System.Text.RegularExpressions;

namespace RFiDGear.Tests.SecureKeys
{
    /// <summary>
    /// The status text of a task is one line per event, each starting with the time: <c>16.11.2026 11:05:09: the message</c>. A test that looks for
    /// text that must not be in a message (a reference such as <c>1:0</c>) has to look at the message and not at the time, in which
    /// <c>11:05</c> contains that very text.
    /// </summary>
    internal static class StatusLines
    {
        private static readonly Regex Timestamp = new Regex(@"^.*?\d{1,2}:\d{2}:\d{2}(?:\s?[AaPp][Mm])?: ", RegexOptions.Multiline | RegexOptions.CultureInvariant);

        /// <summary>Removes the time that starts each line.</summary>
        /// <param name="status">The status text.</param>
        /// <returns>The messages, one per line.</returns>
        internal static string WithoutTimestamps(string status) => Timestamp.Replace(status, string.Empty);
    }
}