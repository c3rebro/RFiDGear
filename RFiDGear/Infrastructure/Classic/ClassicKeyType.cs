#nullable enable
using System;

namespace RFiDGear.Infrastructure.Classic
{
    /// <summary>The two keys of a MIFARE Classic sector.</summary>
    public enum ClassicKeyType
    {
        /// <summary>Key A.</summary>
        KeyA = 0,

        /// <summary>Key B.</summary>
        KeyB = 1
    }

    /// <summary>Which of the two keys an access condition allows for an operation.</summary>
    [Flags]
    public enum ClassicKeyAccess
    {
        /// <summary>No key: the operation is never allowed.</summary>
        None = 0,

        /// <summary>Key A only.</summary>
        KeyA = 1,

        /// <summary>Key B only.</summary>
        KeyB = 2,

        /// <summary>Either key.</summary>
        KeyAOrB = KeyA | KeyB
    }

    /// <summary>Questions about a <see cref="ClassicKeyAccess"/>.</summary>
    public static class ClassicKeyAccessExtensions
    {
        /// <summary>Tells whether the access allows an operation with the given key.</summary>
        /// <param name="access">The access that an access condition grants.</param>
        /// <param name="key">The key that the operation would log in with.</param>
        /// <returns><c>true</c> when that key is allowed.</returns>
        public static bool Allows(this ClassicKeyAccess access, ClassicKeyType key) =>
            key == ClassicKeyType.KeyA
                ? (access & ClassicKeyAccess.KeyA) != 0
                : (access & ClassicKeyAccess.KeyB) != 0;
    }
}
