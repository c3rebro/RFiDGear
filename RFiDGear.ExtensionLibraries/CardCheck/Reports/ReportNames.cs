using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace RFiDGear.ExtensionLibraries.CardCheck
{
    /// <summary>
    /// Turns a job or chip number into one file-name segment, so that two different numbers never share a name.
    /// Pure: nothing is read or written.
    /// </summary>
    internal static class ReportNames
    {
        /// <summary>The character that marks a name that had to be changed. A number that contains it is changed too.</summary>
        internal const char EscapeMarker = '~';

        /// <summary>How many hex digits of the hash follow the marker.</summary>
        internal const int HashLength = 8;

        // The superscript digits one, two and three (U+00B9, U+00B2, U+00B3), which Windows treats like 1, 2 and 3 in
        // COM and LPT names. Given as character codes so that the source holds no invisible look-alikes.
        private static readonly string[] SuperscriptDigits = new[] { 0x00B9, 0x00B2, 0x00B3 }
            .Select(code => ((char)code).ToString())
            .ToArray();

        // The device names Windows reserves, with or without an extension: the classic ones, COM and LPT with 0-9 and
        // with the superscript digits 1-3, and the console handles.
        private static readonly string[] ReservedDeviceNames = new[] { "CON", "PRN", "AUX", "NUL", "CONIN$", "CONOUT$" }
            .Concat(new[] { "COM", "LPT" }.SelectMany(prefix =>
                Enumerable.Range(0, 10).Select(digit => prefix + digit.ToString(System.Globalization.CultureInfo.InvariantCulture))
                    .Concat(SuperscriptDigits.Select(superscript => prefix + superscript))))
            .ToArray();

        /// <summary>
        /// A name made only of characters that are allowed is used exactly as it is (after trimming blanks). Each
        /// character that is not allowed, and the escape marker itself, becomes <c>_</c>, and the marker plus
        /// <see cref="HashLength"/> hex digits of the SHA-256 of the original are appended. Because the marker only
        /// ever appears for a changed name, a changed name cannot equal an unchanged one, and two different originals
        /// have different names unless their hashes collide. A name that is empty, ends in a dot (so also <c>.</c>
        /// and <c>..</c>), or starts with a device name such as <c>NUL</c> (with or without a dot and an extension)
        /// is refused.
        /// </summary>
        /// <param name="value">The job or chip number as entered.</param>
        /// <param name="what">The word used in failure codes, <c>job</c> or <c>chip</c>.</param>
        /// <param name="alsoNotAllowed">Further characters that are treated as not allowed, for example the hyphen that
        /// separates job and chip in a file name.</param>
        /// <returns>The segment, or why the value cannot name a file.</returns>
        public static Result<string> ToSegment(string? value, string what, string alsoNotAllowed = "")
        {
            var original = value?.Trim();

            if (string.IsNullOrEmpty(original))
            {
                return Result<string>.Fail(new Failure(FailureKind.InvalidInput, $"report.{what}.empty", $"The {what} number is empty."));
            }

            // A trailing dot is dropped by Windows, so such a name would not be the file that was asked for. That
            // includes "." and ".." themselves.
            if (original.EndsWith('.') || IsDeviceName(original))
            {
                return Result<string>.Fail(new Failure(FailureKind.InvalidInput, $"report.{what}.invalid", $"The {what} number cannot be used as a file name."));
            }

            var invalid = Path.GetInvalidFileNameChars();
            var replaced = false;
            var builder = new StringBuilder(original.Length + HashLength + 1);

            foreach (var c in original)
            {
                if (Array.IndexOf(invalid, c) >= 0 || c == EscapeMarker || alsoNotAllowed.IndexOf(c) >= 0)
                {
                    builder.Append('_');
                    replaced = true;
                }
                else
                {
                    builder.Append(c);
                }
            }

            if (replaced)
            {
                builder.Append(EscapeMarker).Append(ShortHash(original));
            }

            return Result<string>.Ok(builder.ToString());
        }

        private static bool IsDeviceName(string name)
        {
            // Windows looks at the part before the first dot: "NUL.txt" is the device, "NUL-1" is not.
            var dot = name.IndexOf('.', StringComparison.Ordinal);
            var stem = (dot < 0 ? name : name.Substring(0, dot)).TrimEnd(' ');

            return Array.Exists(ReservedDeviceNames, reserved => string.Equals(reserved, stem, StringComparison.OrdinalIgnoreCase));
        }

        private static string ShortHash(string original)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(original));

            return Convert.ToHexString(hash, 0, HashLength / 2).ToLowerInvariant();
        }
    }
}
