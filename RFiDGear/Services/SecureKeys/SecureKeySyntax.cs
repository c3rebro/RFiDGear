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

        /// <summary>The longest reference.</summary>
        internal const int MaxReferenceLength = 128;

        /// <summary>
        /// Returns whether <paramref name="reference"/> is a valid reference: 1 to 128 ASCII letters, digits or
        /// <c>.</c>, <c>_</c>, <c>:</c>, <c>/</c>, <c>-</c>. Only the shape is checked; what a reference means is up to the
        /// provider. The small alphabet keeps a reference from carrying markup, whitespace or a line break into a project
        /// file or a log.
        /// </summary>
        /// <param name="reference">The text to check.</param>
        /// <returns><c>true</c> when it is a valid reference.</returns>
        internal static bool IsValidReference(string? reference)
        {
            if (string.IsNullOrEmpty(reference) || reference.Length > MaxReferenceLength)
            {
                return false;
            }

            foreach (var c in reference)
            {
                var allowed = c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '.' or '_' or ':' or '/' or '-';

                if (!allowed)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Returns whether <paramref name="reference"/> is a valid reference for the provider <paramref name="providerId"/>: the shape of
        /// <see cref="IsValidReference"/>, and for the provider of the SAM (<see cref="RFiDGear.Services.Sam.SamKeyReference.ProviderId"/>)
        /// also what a key entry and a key version look like (<c>entry:version</c>, the entry 1 to 127). For another provider only the
        /// shape is checked: what a reference means is up to the provider.
        /// </summary>
        /// <param name="providerId">The provider the reference is for.</param>
        /// <param name="reference">The text to check.</param>
        /// <returns><c>true</c> when it is a valid reference for that provider.</returns>
        internal static bool IsValidReferenceFor(string? providerId, string? reference) =>
            IsValidReference(reference)
            && (!string.Equals(providerId, RFiDGear.Services.Sam.SamKeyReference.ProviderId, System.StringComparison.Ordinal)
                || RFiDGear.Services.Sam.SamKeyReference.TryParse(reference, out _));

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
