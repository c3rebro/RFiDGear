using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace RFiDGear.ExtensionLibraries.CardCheck
{
    /// <summary>
    /// Turns a job or chip number into one file-name segment. Pure: nothing is read or written.
    /// </summary>
    internal static class ReportNames
    {
        private static readonly string[] ReservedDeviceNames =
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
        };

        /// <summary>
        /// A name made only of characters Windows allows is used exactly as it is (after trimming blanks). Each
        /// character it forbids (which includes the control characters up to 31) becomes <c>_</c>, and <c>~</c> plus
        /// four hex digits of the SHA-256 of the original are appended, so <c>A/B</c> and <c>A?B</c> do not collide.
        /// A name that is empty, ends in a dot (so also <c>.</c> and <c>..</c>), or starts with a device name such
        /// as <c>NUL</c> (with or without a dot and an extension) is refused.
        /// </summary>
        /// <param name="value">The job or chip number as entered.</param>
        /// <param name="what">The word used in failure codes, <c>job</c> or <c>chip</c>.</param>
        /// <returns>The segment, or why the value cannot name a file.</returns>
        public static Result<string> ToSegment(string? value, string what)
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
            var builder = new StringBuilder(original.Length + 5);

            foreach (var c in original)
            {
                if (Array.IndexOf(invalid, c) >= 0)
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
                builder.Append('~').Append(ShortHash(original));
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

            return Convert.ToHexString(hash, 0, 2).ToLowerInvariant();
        }
    }
}
