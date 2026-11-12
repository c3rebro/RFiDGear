#nullable enable
namespace RFiDGear.Services.SecureKeys
{
    /// <summary>
    /// The shape of the names that end up in project files. They are checked by hand, character by character: a regular
    /// expression would let a trailing line feed through <c>$</c>.
    /// </summary>
    internal static class SecureKeySyntax
    {
        /// <summary>The longest provider id.</summary>
        internal const int MaxProviderIdLength = 64;

        /// <summary>
        /// Returns whether <paramref name="id"/> is a valid provider id: 1 to 64 lower-case ASCII letters, digits,
        /// <c>.</c>, <c>_</c> or <c>-</c>, compared ordinally.
        /// </summary>
        /// <param name="id">The text to check.</param>
        /// <returns><c>true</c> when it is a valid id.</returns>
        internal static bool IsValidProviderId(string? id)
        {
            if (string.IsNullOrEmpty(id) || id.Length > MaxProviderIdLength)
            {
                return false;
            }

            foreach (var c in id)
            {
                var allowed = c is >= 'a' and <= 'z' or >= '0' and <= '9' or '.' or '_' or '-';

                if (!allowed)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
