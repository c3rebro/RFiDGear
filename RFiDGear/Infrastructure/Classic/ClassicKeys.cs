#nullable enable
using System;
using System.Diagnostics.CodeAnalysis;

namespace RFiDGear.Infrastructure.Classic
{
    /// <summary>A MIFARE Classic key: 6 bytes, written as 12 hexadecimal digits.</summary>
    public static class ClassicKey
    {
        /// <summary>The length of a key in bytes.</summary>
        internal const int Length = 6;

        private const int DigitCount = Length * 2;

        /// <summary>Reads a key from its text.</summary>
        /// <param name="text">12 hexadecimal digits; spaces between the bytes are accepted (<c>ff ff ff ff ff ff</c>), nothing else.</param>
        /// <param name="key">The 6 bytes, when the text is a key.</param>
        /// <returns><c>false</c> for a text that is empty, too short or too long, or that holds a character that is no hexadecimal digit.</returns>
        public static bool TryParse(string? text, [NotNullWhen(true)] out byte[]? key)
        {
            key = null;

            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            var digits = new char[DigitCount];
            var count = 0;

            foreach (var c in text)
            {
                if (c == ' ')
                {
                    continue;
                }

                if (count == DigitCount || !char.IsAsciiHexDigit(c))
                {
                    return false;
                }

                digits[count++] = c;
            }

            if (count != DigitCount)
            {
                return false;
            }

            key = Convert.FromHexString(new string(digits));

            return true;
        }
    }

    /// <summary>
    /// The keys that an operation has for one sector: key A, key B, or both. The bytes are only held for the operation and are cleared when
    /// it ends (<see cref="Dispose"/>); <see cref="ToString"/> says which keys there are and nothing of them, so that a log line cannot leak one.
    /// </summary>
    public sealed class ClassicSectorKeys : IDisposable
    {
        private readonly byte[]? keyA;
        private readonly byte[]? keyB;

        private ClassicSectorKeys(byte[]? keyA, byte[]? keyB)
        {
            this.keyA = keyA;
            this.keyB = keyB;
        }

        /// <summary>Gets a value that says whether there is a key A.</summary>
        public bool HasKeyA => keyA != null;

        /// <summary>Gets a value that says whether there is a key B.</summary>
        public bool HasKeyB => keyB != null;

        /// <summary>Makes the keys of an operation from the texts that a task holds.</summary>
        /// <param name="keyA">Key A as text, or <c>null</c> or empty for none.</param>
        /// <param name="keyB">Key B as text, or <c>null</c> or empty for none.</param>
        /// <param name="keys">The keys, when every text that is not empty is a key (see <see cref="ClassicKey.TryParse(string?, out byte[]?)"/>).</param>
        /// <returns><c>false</c> when a text is neither empty nor a key: the operation must not go to the chip with it.</returns>
        public static bool TryCreate(string? keyA, string? keyB, [NotNullWhen(true)] out ClassicSectorKeys? keys)
        {
            keys = null;
            byte[]? a = null;
            byte[]? b = null;

            if (!string.IsNullOrWhiteSpace(keyA) && !ClassicKey.TryParse(keyA, out a))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(keyB) && !ClassicKey.TryParse(keyB, out b))
            {
                return false;
            }

            keys = new ClassicSectorKeys(a, b);

            return true;
        }

        /// <summary>Gives the bytes of a key for the port. They belong to this object and are cleared with it.</summary>
        /// <param name="type">The key.</param>
        /// <returns>The 6 bytes.</returns>
        internal byte[] Get(ClassicKeyType type) =>
            (type == ClassicKeyType.KeyA ? keyA : keyB) ?? throw new InvalidOperationException("The operation has no such key.");

        /// <summary>Tells whether there is a key of a type.</summary>
        /// <param name="type">The key.</param>
        /// <returns><c>true</c> when there is.</returns>
        internal bool Has(ClassicKeyType type) => type == ClassicKeyType.KeyA ? HasKeyA : HasKeyB;

        /// <summary>Clears the bytes of the keys.</summary>
        public void Dispose()
        {
            if (keyA != null)
            {
                Array.Clear(keyA);
            }

            if (keyB != null)
            {
                Array.Clear(keyB);
            }
        }

        /// <summary>Says which keys there are, never what they are.</summary>
        /// <returns>For example <c>ClassicSectorKeys(A, B)</c>.</returns>
        public override string ToString() =>
            "ClassicSectorKeys(" + (HasKeyA ? "A" : "-") + ", " + (HasKeyB ? "B" : "-") + ")";
    }
}
