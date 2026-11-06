using System.Text.RegularExpressions;

namespace RFiDGear.Services.TaskExecution
{
    /// <summary>
    /// Best-effort redaction of key-like text before it is written to a log.
    /// <para>
    /// Exception messages and stack traces can carry key material (for example <c>"Key=..."</c> or a key in a
    /// hexadecimal dump). This removes the shapes keys are known to be written in and then cuts the text to
    /// <see cref="MaxLength"/> characters. Redaction runs first, so a cut can never leave half of a secret behind.
    /// </para>
    /// <para>
    /// It is deliberately greedy: harmless text of the same shape, such as a MAC address, the last 12 digits of a
    /// GUID, or a variable called <c>keyboard=...</c>, is redacted too. It cannot recognise a secret that has
    /// neither a label directly followed by <c>:</c> or <c>=</c> nor a key-like shape, for example a Base64 key,
    /// decimal bytes, hexadecimal of another length or in groups, <c>password is ...</c>, or an XML element.
    /// </para>
    /// </summary>
    internal static class TaskExecutionLogSanitizer
    {
        /// <summary>The longest text <see cref="Sanitize"/> returns.</summary>
        internal const int MaxLength = 256;

        // A word that names secret material, followed by ':' or '=' (a closing quote may sit in between, as in
        // JSON: "password":"x"). The word may be part of a longer identifier (KeyA, MasterKey, CardKey2,
        // UserPassword). The value is a quoted string, with backslash escapes, that ends at a space, comma,
        // semicolon, closing bracket or the end of the text; any other value runs up to the next space, comma or
        // semicolon, so key="abc"def is taken whole.
        private static readonly Regex SensitiveAssignmentPattern = new Regex(
            @"(?i)\b\w*(?:key|secret|passw(?:or)?d|pwd|passphrase|token|credential|payload)\w*[""']?\s*[:=]\s*(?:""(?:\\.|[^""\\])*""(?=[\s,;)}\]]|$)|'(?:\\.|[^'\\])*'(?=[\s,;)}\]]|$)|[^\s,;]+)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // Six or more bytes written as pairs of hex digits with separators, with or without 0x:
        // "FF FF FF FF FF FF", "A0:A1:A2:A3:A4:A5", "0xA0, 0xA1, 0xA2, 0xA3, 0xA4, 0xA5".
        private static readonly Regex SeparatedHexPattern = new Regex(
            @"(?i)(?<![0-9a-z])(?:0x)?[0-9a-f]{2}(?:[\s:,\-]+(?:0x)?[0-9a-f]{2}){5,}(?![0-9a-z])",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // A run of hex digits that is exactly a key length: 6 bytes (MIFARE Classic), 8 bytes (DES),
        // 16 bytes (AES, 2K3DES) or 24 bytes (3K3DES). Longer or shorter runs, such as 4-, 7- or 10-byte UIDs,
        // are left alone.
        private static readonly Regex KeyLikeHexPattern = new Regex(
            @"(?i)(?<![0-9a-f])(?:[0-9a-f]{48}|[0-9a-f]{32}|[0-9a-f]{16}|[0-9a-f]{12})(?![0-9a-f])",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>
        /// Redacts key-like text and limits the result to <see cref="MaxLength"/> characters.
        /// </summary>
        /// <param name="value">The text to clean, for example an exception message or stack trace.</param>
        /// <returns>
        /// The redacted, length-limited text, or <paramref name="value"/> itself when it is null, empty, or only
        /// whitespace.
        /// </returns>
        public static string Sanitize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            // Hex first, labels last. A label's value ends at the first space or comma, so if the label rule ran
            // first, "Key: A0 A1 A2 A3 A4 A5" would lose only the first byte and leave five behind. With the hex
            // rules first the whole key is already one token and the label rule replaces that.
            var sanitized = SeparatedHexPattern.Replace(value, "[REDACTED_HEX]");
            sanitized = KeyLikeHexPattern.Replace(sanitized, "[REDACTED_HEX]");
            sanitized = SensitiveAssignmentPattern.Replace(sanitized, "[REDACTED]");
            return sanitized.Length > MaxLength ? sanitized.Substring(0, MaxLength) : sanitized;
        }
    }
}
