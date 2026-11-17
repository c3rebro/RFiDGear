#nullable enable
using System;
using System.Globalization;

namespace RFiDGear.Services.Sam
{
    /// <summary>
    /// A key that stays in a SAM: the number of a key entry and the version of the key in it. It is what a key slot of a task names when its
    /// key comes from the SAM (the provider id <see cref="ProviderId"/>, the reference <c>entry:version</c>, for example <c>3:0</c>). It holds
    /// no key and is no secret, but it is never logged: <see cref="ToString"/> says so.
    /// </summary>
    public readonly struct SamKeyReference : IEquatable<SamKeyReference>
    {
        /// <summary>The id of the key provider that stands for the SAM, as a constant for the places that need one (a <c>case</c> label).</summary>
        internal const string ProviderIdText = "sam";

        /// <summary>The id of the key provider that stands for the SAM.</summary>
        public static string ProviderId => ProviderIdText;

        /// <summary>The first key entry that holds a key of a card: entry 0 is the master key of the SAM, a host key.</summary>
        public static int FirstEntry => 1;

        /// <summary>The last key entry of the SAM.</summary>
        public static int LastEntry => 127;

        /// <summary>Creates the reference of a key entry and a key version.</summary>
        /// <param name="entry">The key entry, 1 to 127.</param>
        /// <param name="version">The version of the key in it, 0 to 255.</param>
        /// <exception cref="ArgumentOutOfRangeException">The entry is the master entry or outside the SAM.</exception>
        public SamKeyReference(byte entry, byte version)
        {
            if (entry < FirstEntry || entry > LastEntry)
            {
                throw new ArgumentOutOfRangeException(nameof(entry), "A key of a card is in the key entries 1 to 127.");
            }

            Entry = entry;
            Version = version;
        }

        /// <summary>Gets the key entry, 1 to 127 (0 only for <c>default</c>, which is no reference).</summary>
        public byte Entry { get; }

        /// <summary>Gets the version of the key in the entry.</summary>
        public byte Version { get; }

        /// <summary>Gets whether this is a reference (the entry is 1 to 127) and not <c>default</c>.</summary>
        public bool IsValid => Entry >= FirstEntry && Entry <= LastEntry;

        /// <summary>
        /// Reads a reference: two decimal numbers without signs, spaces or leading zeros, separated by a colon: the entry (1 to 127) and the
        /// version (0 to 255).
        /// </summary>
        /// <param name="text">The text, for example <c>3:0</c>.</param>
        /// <param name="reference">The reference; <c>default</c> when the text is none.</param>
        /// <returns><see langword="true"/> when the text is a reference.</returns>
        public static bool TryParse(string? text, out SamKeyReference reference)
        {
            reference = default;

            if (text == null)
            {
                return false;
            }

            var colon = text.IndexOf(':');

            if (colon < 1 || colon != text.LastIndexOf(':'))
            {
                return false;
            }

            if (!TryNumber(text.AsSpan(0, colon), out var entry) || !TryNumber(text.AsSpan(colon + 1), out var version))
            {
                return false;
            }

            if (entry < FirstEntry || entry > LastEntry || version > byte.MaxValue)
            {
                return false;
            }

            reference = new SamKeyReference((byte)entry, (byte)version);

            return true;
        }

        /// <summary>Gives the reference as it is stored in a binding: <c>entry:version</c>.</summary>
        /// <returns>The text.</returns>
        public string Format() => Entry.ToString(CultureInfo.InvariantCulture) + ":" + Version.ToString(CultureInfo.InvariantCulture);

        /// <summary>Says that this is a reference of a key and nothing more: a log or a message never shows it.</summary>
        /// <returns>A fixed text.</returns>
        public override string ToString() => "SamKeyReference [redacted]";

        /// <inheritdoc />
        public bool Equals(SamKeyReference other) => Entry == other.Entry && Version == other.Version;

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is SamKeyReference other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => (Entry << 8) | Version;

        /// <summary>Compares two references.</summary>
        /// <param name="left">The first.</param>
        /// <param name="right">The second.</param>
        /// <returns>Whether they name the same key.</returns>
        public static bool operator ==(SamKeyReference left, SamKeyReference right) => left.Equals(right);

        /// <summary>Compares two references.</summary>
        /// <param name="left">The first.</param>
        /// <param name="right">The second.</param>
        /// <returns>Whether they name different keys.</returns>
        public static bool operator !=(SamKeyReference left, SamKeyReference right) => !left.Equals(right);

        // Up to three decimal digits, no sign, no space, no leading zero (but 0 itself).
        private static bool TryNumber(ReadOnlySpan<char> digits, out int value)
        {
            value = 0;

            if (digits.Length < 1 || digits.Length > 3 || (digits.Length > 1 && digits[0] == '0'))
            {
                return false;
            }

            foreach (var c in digits)
            {
                if (c < '0' || c > '9')
                {
                    return false;
                }

                value = (value * 10) + (c - '0');
            }

            return true;
        }
    }
}